// operational-tie-break-resolution.md WI-3 (D8). The dedicated read surface for
// pending ties — the surface CompetitionResult.PendingTieBreaks' doc comment
// anticipated. The leaderboard view (CompetitionScoreView) is untouched.
//
// Handler: CompetitionLoader -> EntryCollector -> ScoringService.ScoreCompetition
// (the identical pipeline ScoreCompetitionHandler runs) -> map
// result.PendingTieBreaks. No person-name resolution: views carry refs (house
// pattern; no name-resolution query exists).

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;

namespace Soarscore.Application.Queries.Scoring;

public sealed record PendingTieBreakView(
    int PhaseOrdinal,
    ImmutableArray<CompetitorId> CompetitorRefs,
    /// <summary>The place the group currently shares (Placings of any member — all equal).</summary>
    int SharedPlace,
    /// <summary>The halt directive, serialized with its $kind discriminator (e.g. "tiebreakFlyoff").</summary>
    TieBreakDirective Directive);

public sealed record PendingTieBreaksView(ImmutableArray<PendingTieBreakView> Ties);

public readonly record struct GetPendingTieBreaks(CompetitionId CompetitionRef) : IQuery<PendingTieBreaksView>;

public sealed class GetPendingTieBreaksHandler(IEventStore eventStore, IEntryQuery entryQuery)
    : IQueryHandler<GetPendingTieBreaks, PendingTieBreaksView>
{
    public async Task<Result<PendingTieBreaksView>> HandleAsync(
        GetPendingTieBreaks query, CancellationToken cancellationToken)
    {
        var competitionLoaded = await CompetitionLoader.LoadAsync(eventStore, query.CompetitionRef, cancellationToken);
        if (competitionLoaded.IsFailure)
        {
            return Result<PendingTieBreaksView>.Failure(
                competitionLoaded.Code!, competitionLoaded.Message!, competitionLoaded.Defects);
        }

        var entriesLoaded = await EntryCollector.CollectAsync(eventStore, entryQuery, query.CompetitionRef, cancellationToken);
        if (entriesLoaded.IsFailure)
        {
            return Result<PendingTieBreaksView>.Failure(
                entriesLoaded.Code!, entriesLoaded.Message!, entriesLoaded.Defects);
        }

        var scored = ScoringService.ScoreCompetition(
            competitionLoaded.Value.Competition, entriesLoaded.Value,
            // As ScoreCompetitionHandler — the declared instruments compose
            // with the class table at resolution; no declaration here means
            // distances only.
            competitionLoaded.Value.Competition.DeclaredInstruments?.Instruments ?? []);
        if (scored.IsFailure)
        {
            return Result<PendingTieBreaksView>.Failure(scored.Code!, scored.Message!, scored.Defects);
        }

        return Result<PendingTieBreaksView>.Success(MapPendingTieBreaks(scored.Value));
    }

    private static PendingTieBreaksView MapPendingTieBreaks(CompetitionResult result)
    {
        var ties = result.PendingTieBreaks
            .Select(t => new PendingTieBreakView(
                // The only reachable ranking, the D9 single-phase stance —
                // trap 7: the positional index, NOT PhaseDefinition.Ordinal.
                PhaseOrdinal: 0,
                CompetitorRefs: t.CompetitorRefs.Select(r => CompetitorId.Parse(r, null)).ToImmutableArray(),
                SharedPlace: result.Placings[t.CompetitorRefs[0]],
                Directive: t.Directive))
            .ToImmutableArray();

        return new PendingTieBreaksView(ties);
    }
}
