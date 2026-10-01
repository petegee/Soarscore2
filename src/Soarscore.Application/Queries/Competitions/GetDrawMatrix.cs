// kanban/backlog/draw-fairness-matrix.md WI-1. Pure derivation over the folded
// Competition stream (CompetitionLoader.LoadAsync): the draw view (who is in
// which group) plus the who-meets-whom matrix and stats in one round trip, so
// the organiser can judge draw fairness without pivoting WI-6a's flat pair
// list by hand. No event, no projection change, no ICompetitionsQuery method.
//
// Four deliberate deviations from the GliderScore precedent, recorded so a
// later reader does not "align" them away:
//  1. Names are NOT resolved server-side — Competitor carries PersonRef only;
//     non-organisers resolve them through GET /competition-roster (any
//     authenticated caller; /people is organiser-only since the D4
//     narrowing). Organiser tooling may still join via the people reads.
//  2. Re-flight groups ARE included — the fold has no ReFlightNo marker;
//     Group.CompetitorRefs is all this query sees (teams-mvp.md WI-6
//     provenance-blind precedent: generated and prescribed draws read
//     identically).
//  3. No team/frequency clash suppression or overlays — that is
//     GET /draw-diagnostics' job; this query reports raw meeting counts,
//     never a pass/fail verdict.
//  4. MAD is computed over the UNDIRECTED pair universe (N choose 2, matching
//     PairwiseCoOccurrence semantics), not GliderScore's directed
//     dtPilotMeetings rows.

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Queries.Competitions;

/// <summary>One pilot of the field, ordered by CompetitorNumber asc on <see cref="DrawMatrixView.Pilots"/>.</summary>
public sealed record DrawMatrixPilotView(
    CompetitorId CompetitorRef,
    int CompetitorNumber,
    bool Withdrawn);

/// <summary>One drawn group in scope, in phase/round/task-round/group ordinal order with drawn order preserved.</summary>
public sealed record DrawMatrixGroupView(
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    int GroupOrdinal,
    GroupId GroupRef,
    ImmutableArray<CompetitorId> CompetitorRefs);

/// <summary>One unordered pair's meeting count — sparse: Count >= 1 only, zeros implied by absence.</summary>
public sealed record DrawMatrixEntryView(
    CompetitorId CompetitorA,
    CompetitorId CompetitorB,
    int Count);

/// <summary>Unordered pairs with exactly <see cref="Meetings"/> meetings — gap-filled 0..Max, zeros included.</summary>
public sealed record DrawMatrixDistributionEntry(
    int Meetings,
    int Count);

/// <summary>The GET /draw-matrix response shape: draw view plus matrix plus stats over ALL unordered pairs incl. never-met.</summary>
public sealed record DrawMatrixView(
    CompetitionId CompetitionRef,
    int? PhaseOrdinal,
    int? FromRound,
    int? ToRound,
    ImmutableArray<DrawMatrixPilotView> Pilots,
    ImmutableArray<DrawMatrixGroupView> Groups,
    ImmutableArray<DrawMatrixEntryView> Entries,
    ImmutableArray<DrawMatrixDistributionEntry> Distribution,
    int MinMeetings,
    int MaxMeetings,
    double MeanMeetings,
    double MeanAbsoluteDeviation);

public readonly record struct GetDrawMatrix(
    CompetitionId CompetitionRef,
    int? PhaseOrdinal = null,
    int? FromRound = null,
    int? ToRound = null)
    : IQuery<DrawMatrixView>;

public sealed class GetDrawMatrixHandler(IEventStore eventStore)
    : IQueryHandler<GetDrawMatrix, DrawMatrixView>
{
    public async Task<Result<DrawMatrixView>> HandleAsync(
        GetDrawMatrix query, CancellationToken cancellationToken)
    {
        if (query.FromRound.HasValue && query.ToRound.HasValue
            && query.FromRound.Value > query.ToRound.Value)
        {
            return Result<DrawMatrixView>.Failure(
                "drawMatrix.roundRangeInvalid",
                $"FromRound ({query.FromRound.Value}) must not be after ToRound ({query.ToRound.Value}).");
        }

        var loaded = await CompetitionLoader.LoadAsync(eventStore, query.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<DrawMatrixView>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var competition = loaded.Value.Competition;

        ImmutableArray<Phase> scopedPhases;
        if (query.PhaseOrdinal.HasValue)
        {
            var phase = competition.Phases.FirstOrDefault(p => p.Ordinal == query.PhaseOrdinal.Value);
            if (phase is null)
            {
                return Result<DrawMatrixView>.Failure(
                    "drawMatrix.phaseNotFound",
                    $"No phase with ordinal {query.PhaseOrdinal.Value} in competition {query.CompetitionRef}.");
            }

            scopedPhases = [phase];
        }
        else
        {
            scopedPhases = competition.Phases;
        }

        // Round window applies to Round.Ordinal WITHIN each in-scope phase —
        // rounds are per-phase numbered; no global round axis exists.
        var scopedRounds = scopedPhases
            .SelectMany(phase => phase.Rounds
                .Where(round =>
                    (!query.FromRound.HasValue || round.Ordinal >= query.FromRound.Value)
                    && (!query.ToRound.HasValue || round.Ordinal <= query.ToRound.Value)))
            .ToImmutableArray();

        // Pilot universe = the competition's full field, known pre-draw and
        // returned even when no groups are in scope. Drawn pilots stay listed
        // after withdrawal — the draw is intact on withdrawal.
        var competitorsById = competition.Competitors.ToDictionary(c => c.Id);
        var pilots = competition.Competitors
            .OrderBy(c => c.CompetitorNumber)
            .Select(c => new DrawMatrixPilotView(c.Id, c.CompetitorNumber, c.WithdrawnAt is not null))
            .ToImmutableArray();

        var groups = scopedPhases
            .OrderBy(p => p.Ordinal)
            .SelectMany(phase => phase.Rounds
                .Where(round =>
                    (!query.FromRound.HasValue || round.Ordinal >= query.FromRound.Value)
                    && (!query.ToRound.HasValue || round.Ordinal <= query.ToRound.Value))
                .OrderBy(r => r.Ordinal)
                .SelectMany(round => round.TaskRounds
                    .OrderBy(tr => tr.Ordinal)
                    .SelectMany(taskRound => taskRound.Groups
                        .OrderBy(g => g.Ordinal)
                        .Select(group => new DrawMatrixGroupView(
                            phase.Ordinal,
                            round.Ordinal,
                            taskRound.Ordinal,
                            group.Ordinal,
                            group.Id,
                            group.CompetitorRefs)))))
            .ToImmutableArray();

        var n = competition.Competitors.Length;
        if (n < 2)
        {
            return Result<DrawMatrixView>.Success(new DrawMatrixView(
                query.CompetitionRef,
                query.PhaseOrdinal,
                query.FromRound,
                query.ToRound,
                pilots,
                groups,
                [],
                [],
                0,
                0,
                0,
                0));
        }

        var counts = PairwiseCoOccurrence.Compute(scopedRounds);
        var totalPairs = n * (n - 1) / 2;

        // Canonical smaller-CompetitorNumber first — deviation from
        // ComputeEntries' Guid ordering is intentional: Guid order is
        // meaningless to the organiser; number order matches the Pilots axis
        // and GliderScore `#` refs.
        var entries = counts
            .Select(kvp =>
            {
                var numberA = competitorsById[kvp.Key.Item1].CompetitorNumber;
                var numberB = competitorsById[kvp.Key.Item2].CompetitorNumber;
                return numberA <= numberB
                    ? new DrawMatrixEntryView(kvp.Key.Item1, kvp.Key.Item2, kvp.Value)
                    : new DrawMatrixEntryView(kvp.Key.Item2, kvp.Key.Item1, kvp.Value);
            })
            .OrderBy(e => competitorsById[e.CompetitorA].CompetitorNumber)
            .ThenBy(e => competitorsById[e.CompetitorB].CompetitorNumber)
            .ToImmutableArray();

        var metPairs = counts.Count;
        var zeroPairs = totalPairs - metPairs;
        var max = counts.Values.DefaultIfEmpty(0).Max();
        var min = zeroPairs > 0 ? 0 : counts.Values.Min();

        // Gap-filled 0..Max histogram over the undirected pair universe
        // (GliderScore PictureBox1_Paint precedent), zeros included.
        var buckets = new int[max + 1];
        buckets[0] = zeroPairs;
        foreach (var count in counts.Values)
        {
            buckets[count]++;
        }

        var distribution = Enumerable.Range(0, max + 1)
            .Select(meetings => new DrawMatrixDistributionEntry(meetings, buckets[meetings]))
            .ToImmutableArray();

        // GliderScore CalcMeanAbsoluteDeviation port over the undirected pair
        // universe: Avg = Σ(meetings×count)/pairs,
        // MAD = Σ(|avg−meetings|×count)/pairs over the gap-filled distribution.
        var sum = 0L;
        foreach (var count in counts.Values)
        {
            sum += count;
        }

        var mean = (double)sum / totalPairs;

        var deviation = 0.0;
        for (var meetings = 0; meetings <= max; meetings++)
        {
            deviation += Math.Abs(mean - meetings) * buckets[meetings];
        }

        var mad = deviation / totalPairs;

        return Result<DrawMatrixView>.Success(new DrawMatrixView(
            query.CompetitionRef,
            query.PhaseOrdinal,
            query.FromRound,
            query.ToRound,
            pilots,
            groups,
            entries,
            distribution,
            min,
            max,
            mean,
            mad));
    }
}
