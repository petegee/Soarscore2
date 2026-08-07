// The draw's pairing algorithm — docs/plans/phase-drawn-steel-thread-plan.md
// WI-1. Pure and deterministic given a stable field ordering: no unseeded
// randomness, so a replay and a CsCheck shrink (WI-2) both reproduce the same
// output. GroupId is not minted here — this returns bare CompetitorId
// partitions; Competition.DrawPhase mints GroupId when it builds the Group
// records for the event, the only place a GroupId is actually needed.

using System.Collections.Immutable;

namespace Soarscore.Domain.Competitions;

public static class PhaseDraw
{
    /// <summary>
    /// Builds, for each of <paramref name="roundCount"/> rounds (outermost
    /// array), a partition of <paramref name="field"/> into groups — group
    /// count and sizes from <paramref name="minPerGroup"/> (fewest groups,
    /// F3K.9.1 / 5.5.11.8), and membership chosen round by round to minimise
    /// repeat pairings (00-general-rules.md#1 "as few times as possible") via
    /// a backtracking least-paired-first construction (see BuildOneRound).
    /// Not claimed optimal ACROSS rounds at full contest scale — each round
    /// is built to be the best available given prior rounds, not with
    /// lookahead onto rounds still to come — but WI-2's brute-force property
    /// test does hold this to the true joint-optimum at small field/round
    /// counts, which is what forced the backtracking: a single-pass,
    /// no-backtrack greedy can paint itself into a forced repeat in the last
    /// group of a round even at tiny N (field 6 / minPerGroup 2 / round 2 is
    /// WI-2's counterexample) — a known shape for this class of problem
    /// (resolvable designs / "social golfer"), not an implementation slip.
    /// </summary>
    public static ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> BuildGroups(
        ImmutableArray<CompetitorId> field, int minPerGroup, int roundCount)
    {
        var groupCount = Math.Max(1, field.Length / minPerGroup);
        var sizes = GroupSizes(field.Length, groupCount);

        var pairCount = new Dictionary<(CompetitorId, CompetitorId), int>();
        var rounds = ImmutableArray.CreateBuilder<ImmutableArray<ImmutableArray<CompetitorId>>>(roundCount);

        for (var r = 0; r < roundCount; r++)
        {
            var groups = BuildOneRound(field, sizes, pairCount);
            rounds.Add(groups);
            RecordPairings(groups, pairCount);
        }

        return rounds.MoveToImmutable();
    }

    /// <summary>
    /// <paramref name="groupCount"/> sizes summing to <paramref name="fieldSize"/>:
    /// <c>fieldSize / groupCount</c>, remainder spread one-per-group across the
    /// first groups. No rule states groups must be exactly equal, only that the
    /// split should favour fewer, fuller groups — see the plan's rules note.
    /// </summary>
    private static ImmutableArray<int> GroupSizes(int fieldSize, int groupCount)
    {
        var baseSize = fieldSize / groupCount;
        var remainder = fieldSize % groupCount;

        var builder = ImmutableArray.CreateBuilder<int>(groupCount);
        for (var g = 0; g < groupCount; g++)
        {
            builder.Add(g < remainder ? baseSize + 1 : baseSize);
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Finds the lowest ceiling C such that the field can be split into
    /// <paramref name="sizes"/>-shaped groups with no pair's resultant count
    /// (existing + 1, for any pair newly co-grouped this round) exceeding C —
    /// iterative deepening over C, backtracking (<see cref="TryBuildRound"/>)
    /// within each attempt. A single-pass fill can commit an early group to a
    /// choice that forces the last group into an avoidable repeat; escalating
    /// C and backtracking is what finds the partition a plain greedy misses.
    /// </summary>
    private static ImmutableArray<ImmutableArray<CompetitorId>> BuildOneRound(
        ImmutableArray<CompetitorId> field,
        ImmutableArray<int> sizes,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount)
    {
        var currentMax = pairCount.Count == 0 ? 0 : pairCount.Values.Max();

        for (var ceiling = Math.Max(1, currentMax); ; ceiling++)
        {
            var attempt = TryBuildRound(sizes, 0, field, pairCount, ceiling);
            if (attempt is not null)
            {
                return attempt.Value;
            }
        }
    }

    private static ImmutableArray<ImmutableArray<CompetitorId>>? TryBuildRound(
        ImmutableArray<int> sizes,
        int groupIndex,
        ImmutableArray<CompetitorId> unplaced,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount,
        int ceiling)
    {
        if (groupIndex == sizes.Length)
        {
            return ImmutableArray<ImmutableArray<CompetitorId>>.Empty;
        }

        foreach (var (group, rest) in CandidateGroups(unplaced, sizes[groupIndex], pairCount, ceiling))
        {
            var tail = TryBuildRound(sizes, groupIndex + 1, rest, pairCount, ceiling);
            if (tail is not null)
            {
                return tail.Value.Insert(0, group);
            }
        }

        return null;
    }

    /// <summary>
    /// Every <paramref name="size"/>-subset of <paramref name="unplaced"/>
    /// whose internal pairs all resolve to <paramref name="ceiling"/> or
    /// below, best-first: ranked by the worst single resultant pairing, then
    /// the sum of resultant pairings, then field order — the same ranking
    /// the original single-pass greedy used, so the common case (no
    /// backtracking needed) still picks exactly what it used to pick, on the
    /// first candidate tried.
    /// </summary>
    private static IEnumerable<(ImmutableArray<CompetitorId> Group, ImmutableArray<CompetitorId> Remaining)> CandidateGroups(
        ImmutableArray<CompetitorId> unplaced,
        int size,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount,
        int ceiling)
    {
        var candidates = new List<(ImmutableArray<CompetitorId> Group, int WorstPairing, int Sum)>();

        foreach (var combo in Combinations(unplaced, size))
        {
            var worst = 0;
            var sum = 0;
            var withinCeiling = true;

            for (var i = 0; i < combo.Length && withinCeiling; i++)
            {
                for (var j = i + 1; j < combo.Length; j++)
                {
                    var resultant = pairCount.GetValueOrDefault(PairKey(combo[i], combo[j])) + 1;
                    if (resultant > ceiling)
                    {
                        withinCeiling = false;
                        break;
                    }

                    sum += resultant;
                    if (resultant > worst)
                    {
                        worst = resultant;
                    }
                }
            }

            if (withinCeiling)
            {
                candidates.Add((combo, worst, sum));
            }
        }

        // Stable sort: List.Sort is not, and Combinations already enumerates
        // in field order, so an unstable sort would silently break the
        // "ties broken by field order" determinism this algorithm promises.
        var ordered = candidates.OrderBy(c => c.WorstPairing).ThenBy(c => c.Sum);

        foreach (var (group, _, _) in ordered)
        {
            var rest = unplaced.Where(c => !group.Contains(c)).ToImmutableArray();
            yield return (group, rest);
        }
    }

    /// <summary>Every k-subset of items, preserving relative field order within each subset.</summary>
    private static IEnumerable<ImmutableArray<CompetitorId>> Combinations(ImmutableArray<CompetitorId> items, int k)
    {
        if (k == 0)
        {
            yield return ImmutableArray<CompetitorId>.Empty;
            yield break;
        }

        if (items.Length < k)
        {
            yield break;
        }

        var first = items[0];
        var rest = items.RemoveAt(0);

        foreach (var combo in Combinations(rest, k - 1))
        {
            yield return combo.Insert(0, first);
        }

        foreach (var combo in Combinations(rest, k))
        {
            yield return combo;
        }
    }

    private static void RecordPairings(
        ImmutableArray<ImmutableArray<CompetitorId>> groups,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount)
    {
        foreach (var group in groups)
        {
            for (var i = 0; i < group.Length; i++)
            {
                for (var j = i + 1; j < group.Length; j++)
                {
                    var key = PairKey(group[i], group[j]);
                    pairCount[key] = pairCount.GetValueOrDefault(key) + 1;
                }
            }
        }
    }

    /// <summary>Unordered pair key — canonicalised by Guid comparison, not field order.</summary>
    private static (CompetitorId, CompetitorId) PairKey(CompetitorId a, CompetitorId b) =>
        a.Value.CompareTo(b.Value) <= 0 ? (a, b) : (b, a);
}
