// kanban/completed/task-round-lifecycle.md WI-5 — the one non-trivial handler
// of that thread. Unlike the other three lifecycle commands it cannot just
// load-decide-append: Finalisation.DeclaredResults is 1..*, and a declared
// result is by definition what the leaderboard said at the moment of
// declaration. So this handler does what ScoreCompetitionHandler does —
// CompetitionLoader -> EntryCollector -> ScoringService — and maps the result
// into DeclaredResults before the decide function ever runs.
//
// teams-mvp.md WI-7 extends the same shape to the team classification: after
// scoring the individuals the handler runs TeamClassificationEngine.Classify
// (the same pure engine the standings query uses, on the same scored result)
// and maps the standings into DeclaredTeamResults, so the declaration freezes
// the full team classification — total, place, contributors, tie-break
// evidence — rather than re-deriving it on read.
//
// Cross-aggregate reads in a command handler are precedented:
// CreateCompetitionHandler reads a PublishedClassDefinition, OpenEntryHandler
// reads the Competition to decide an Entry event.
//
// The scoring call happens BEFORE the decide deliberately, so a scoring
// failure surfaces as its own code (score.reflightNotSupported,
// score.taskNotDeclared) rather than as a finalisation defect.

using System.Collections.Immutable;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Scoring;

namespace Soarscore.Application.Commands.Competitions;

/// <summary>
/// Competition-scope finalisation only. Phase-scope exists to name who was
/// PROMOTED into the next phase, and no second phase can be drawn yet, so
/// there is nothing to promote into — see the plan's decision 2.
/// </summary>
public sealed record FinaliseCompetition(
    CompetitionId CompetitionRef,
    string By) : ICommand<CompetitionId>;

public sealed class FinaliseCompetitionHandler(IEventStore eventStore, IEntryQuery entryQuery, IClock clock)
    : ICommandHandler<FinaliseCompetition, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(FinaliseCompetition command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;

        var entriesLoaded = await EntryCollector.CollectAsync(
            eventStore, entryQuery, command.CompetitionRef, cancellationToken);
        if (entriesLoaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(entriesLoaded.Code!, entriesLoaded.Message!, entriesLoaded.Defects);
        }

        var scored = ScoringService.ScoreCompetition(competition, entriesLoaded.Value);
        if (scored.IsFailure)
        {
            return Result<CompetitionId>.Failure(scored.Code!, scored.Message!, scored.Defects);
        }

        // Team standings at the moment of declaration — the same pure engine
        // ScoreTeamStandingsHandler runs, fed the same already-scored
        // individual result, then mapped into the declared shape exactly as
        // DeclaredResultsOf maps the individual result (teams-mvp.md decision
        // 4: capture the full declaration). Skipped when the classification is
        // disabled or never configured: the decide refuses any team result in
        // that state, and an empty declaration is the truth.
        var declaredTeamResults = ImmutableArray<DeclaredTeamResult>.Empty;

        if (competition.TeamClassification is { Enabled: true })
        {
            var classified = TeamClassificationEngine.Classify(
                scored.Value,
                competition.ScoringTeams,
                competition.ScoringTeamMemberships,
                competition.TeamClassification);
            if (classified.IsFailure)
            {
                return Result<CompetitionId>.Failure(classified.Code!, classified.Message!, classified.Defects);
            }

            declaredTeamResults = DeclaredTeamResultsOf(classified.Value);
        }

        var decision = competition.Finalise(
            DeclaredResultsOf(scored.Value), declaredTeamResults, command.By, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<CompetitionId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(
            command.CompetitionRef.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);

        return append.IsFailure
            ? Result<CompetitionId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<CompetitionId>.Success(command.CompetitionRef);
    }

    /// <summary>
    /// Maps the engine's result into DeclaredResults, mirroring
    /// ScoreCompetitionHandler's own mapping (including the string
    /// CompetitorRef -> CompetitorId parse, finding 3) — which is what makes
    /// the plan's invariant B, "a declared result is always re-derivable",
    /// hold by construction rather than by hope.
    /// </summary>
    private static ImmutableArray<DeclaredResult> DeclaredResultsOf(CompetitionResult result) =>
        result.Scores.Values
            .Select(s => new DeclaredResult
            {
                CompetitorRef = CompetitorId.Parse(s.CompetitorRef, null),
                Aggregate = s.Score,

                // 0 for a disqualified competitor: RankingEngine excludes them
                // from placings altogether, and DeclaredResult.Placing is not
                // nullable. The declared aggregate still records what they
                // scored, so nothing is lost.
                Placing = result.Placings.TryGetValue(s.CompetitorRef, out var placing) ? placing : 0,

                // Always false, per decision 2: promotion is phase-scope
                // finalisation's job, and no second phase can be drawn yet.
                Promoted = false,
            })
            .ToImmutableArray();

    /// <summary>
    /// Maps the engine's standings into DeclaredTeamResult — the 1:1 mapping
    /// the two shapes were field-named for (teams-mvp.md WI-7), so the
    /// declared-vs-derived read can diff the sections structurally.
    /// <see cref="TeamStanding.Members"/> is the one deliberate drop: the
    /// declaration records what was counted, not every member's contribution
    /// state. Together with DeclaredResultsOf this is what makes the plan's
    /// invariant B hold for teams by construction rather than by hope.
    /// </summary>
    private static ImmutableArray<DeclaredTeamResult> DeclaredTeamResultsOf(TeamClassificationResult classification) =>
        classification.Standings
            .Select(s => new DeclaredTeamResult
            {
                TeamRef = s.TeamRef,
                Name = s.Name,
                Total = s.Total,
                Placing = s.Placing,
                Contributors = [.. s.Contributors.Select(c => new DeclaredTeamContributor
                {
                    CompetitorRef = c.CompetitorRef,
                    Score = c.Score,
                    Placing = c.Placing,
                })],
                PlacingSum = s.PlacingSum,
                BestIndividualPlacing = s.BestIndividualPlacing,
            })
            .ToImmutableArray();
}
