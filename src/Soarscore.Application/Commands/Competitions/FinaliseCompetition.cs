// kanban/completed/task-round-lifecycle.md WI-5 — the one non-trivial handler
// of that thread. Unlike the other three lifecycle commands it cannot just
// load-decide-append: Finalisation.DeclaredResults is 1..*, and a declared
// result is by definition what the leaderboard said at the moment of
// declaration. So this handler does what ScoreCompetitionHandler does —
// CompetitionLoader -> EntryCollector -> ScoringService — and maps the result
// into DeclaredResults before the decide function ever runs.
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

        var decision = competition.Finalise(DeclaredResultsOf(scored.Value), command.By, clock.UtcNow);
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
}
