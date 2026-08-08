// ScoreCompetition — docs/plans/scoring-steel-thread-plan.md WI-7, slice 2.
//
// The whole-competition leaderboard: every group scored, phase-aggregated
// with drops, aggregate penalties applied, ranked. CompetitionLoader.LoadAsync
// -> EntryCollector.CollectAsync -> ScoringService.ScoreCompetition -> map the
// engine's string CompetitorRef (finding 3) back to CompetitorId.

using System.Collections.Immutable;
using Soarscore.Application.Competitions;
using Soarscore.Application.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Scoring;

namespace Soarscore.Application.Scoring;

/// <summary>One competitor's final, ranked result — the GET /competition-result response shape.</summary>
public sealed record CompetitorFinalScoreView(
    CompetitorId CompetitorRef,
    decimal Score,
    bool Disqualified,
    /// <summary>Null when disqualified — RankingEngine excludes disqualified competitors from placings.</summary>
    int? Placing);

public sealed record CompetitionScoreView(ImmutableArray<CompetitorFinalScoreView> Scores);

public readonly record struct ScoreCompetition(CompetitionId CompetitionRef) : IQuery<CompetitionScoreView>;

public sealed class ScoreCompetitionHandler(IEventStore eventStore, IEntryQuery entryQuery)
    : IQueryHandler<ScoreCompetition, CompetitionScoreView>
{
    public async Task<Result<CompetitionScoreView>> HandleAsync(
        ScoreCompetition query, CancellationToken cancellationToken)
    {
        var competitionLoaded = await CompetitionLoader.LoadAsync(eventStore, query.CompetitionRef, cancellationToken);
        if (competitionLoaded.IsFailure)
        {
            return Result<CompetitionScoreView>.Failure(
                competitionLoaded.Code!, competitionLoaded.Message!, competitionLoaded.Defects);
        }

        var entriesLoaded = await EntryCollector.CollectAsync(eventStore, entryQuery, query.CompetitionRef, cancellationToken);
        if (entriesLoaded.IsFailure)
        {
            return Result<CompetitionScoreView>.Failure(
                entriesLoaded.Code!, entriesLoaded.Message!, entriesLoaded.Defects);
        }

        var scored = ScoringService.ScoreCompetition(competitionLoaded.Value.Competition, entriesLoaded.Value);
        if (scored.IsFailure)
        {
            return Result<CompetitionScoreView>.Failure(scored.Code!, scored.Message!, scored.Defects);
        }

        return Result<CompetitionScoreView>.Success(MapCompetitionResult(scored.Value));
    }

    private static CompetitionScoreView MapCompetitionResult(CompetitionResult result)
    {
        var scores = result.Scores.Values
            .Select(s => new CompetitorFinalScoreView(
                CompetitorRef: CompetitorId.Parse(s.CompetitorRef, null),
                Score: s.Score,
                Disqualified: s.Disqualified,
                Placing: result.Placings.TryGetValue(s.CompetitorRef, out var placing) ? placing : null))
            .ToImmutableArray();

        return new CompetitionScoreView(scores);
    }
}
