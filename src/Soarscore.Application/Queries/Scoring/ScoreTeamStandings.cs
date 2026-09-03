// kanban/in-progress/teams-mvp.md WI-6/WI-7. The team leaderboard: the same
// scoring path ScoreCompetitionHandler runs (CompetitionLoader ->
// EntryCollector -> ScoringService.ScoreCompetition) feeds
// TeamClassificationEngine.Classify — the engine is pure and downstream of
// individual ranking (paper design principle 2), so the standings always
// derive from exactly what /competition-result reads. Disabled or
// never-configured classification is derived = null — a state, not an error
// (the engine's own doc comment); the individual scoring pipeline does not
// even run for it.
//
// WI-7 adds the declared section beside the derived one — the declared-vs-
// derived comparison the paper asks for, and the ONLY read surface for
// declared team results (no general finalisation read surface exists —
// kanban/deferred-decisions.md §Task-round lifecycle). Declared is read
// straight off the latest competition-scope finalisation, independent of the
// classification's current enabled state: the declaration was frozen at
// finalisation, and a post-finalisation correction (allowed, auditable,
// never retroactive — the story's consequence note) shows up as a
// derived-vs-declared divergence here, not as a mutated declaration.

using System.Collections.Immutable;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Scoring;

namespace Soarscore.Application.Queries.Scoring;

/// <summary>
/// The GET /competition-team-result response shape. <see cref="Derived"/> is
/// null when team classification is disabled or never configured — the
/// only read surface for the derived team standings. <see cref="Declared"/>
/// is the latest competition-scope finalisation's DeclaredTeamResults when one
/// exists, else null — the frozen half of the comparison.
/// </summary>
public sealed record TeamStandingsView(TeamClassificationResult? Derived, ImmutableArray<DeclaredTeamResult>? Declared);

public readonly record struct ScoreTeamStandings(CompetitionId CompetitionRef) : IQuery<TeamStandingsView>;

public sealed class ScoreTeamStandingsHandler(IEventStore eventStore, IEntryQuery entryQuery)
    : IQueryHandler<ScoreTeamStandings, TeamStandingsView>
{
    public async Task<Result<TeamStandingsView>> HandleAsync(
        ScoreTeamStandings query, CancellationToken cancellationToken)
    {
        var competitionLoaded = await CompetitionLoader.LoadAsync(eventStore, query.CompetitionRef, cancellationToken);
        if (competitionLoaded.IsFailure)
        {
            return Result<TeamStandingsView>.Failure(
                competitionLoaded.Code!, competitionLoaded.Message!, competitionLoaded.Defects);
        }

        var competition = competitionLoaded.Value.Competition;

        // The declared section is read off the fold before anything else, so a
        // finalisation is surfaced even when the classification has since been
        // disabled or never was configured — frozen data does not depend on
        // what derived work is possible today.
        var declared = competition.Finalisations
            .LastOrDefault(f => f.Scope == FinalisationScope.Competition)
            ?.DeclaredTeamResults;

        // Disabled or never configured: derived = null — a state, never a
        // failure (NFR-4: nothing here gates on scores existing either; with
        // scores absent the engine reports NoScoreYet members and zero totals).
        if (competition.TeamClassification is not { Enabled: true })
        {
            return Result<TeamStandingsView>.Success(new TeamStandingsView(Derived: null, Declared: declared));
        }

        var entriesLoaded = await EntryCollector.CollectAsync(eventStore, entryQuery, query.CompetitionRef, cancellationToken);
        if (entriesLoaded.IsFailure)
        {
            return Result<TeamStandingsView>.Failure(
                entriesLoaded.Code!, entriesLoaded.Message!, entriesLoaded.Defects);
        }

        var scored = ScoringService.ScoreCompetition(competition, entriesLoaded.Value);
        if (scored.IsFailure)
        {
            return Result<TeamStandingsView>.Failure(scored.Code!, scored.Message!, scored.Defects);
        }

        var classified = TeamClassificationEngine.Classify(
            scored.Value,
            competition.ScoringTeams,
            competition.ScoringTeamMemberships,
            competition.TeamClassification);
        if (classified.IsFailure)
        {
            return Result<TeamStandingsView>.Failure(classified.Code!, classified.Message!, classified.Defects);
        }

        return Result<TeamStandingsView>.Success(new TeamStandingsView(classified.Value, declared));
    }
}
