using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property-based tests for <see cref="PhaseDraw.BuildGroups"/> —
/// kanban/completed/phase-drawn-steel-thread-plan.md WI-2, the two invariants the
/// whole draw thread is about. CsCheck, in CompetitionFieldPropertyTests's
/// model-based style, driving the real pairing algorithm.
/// </summary>
public class PhaseDrawPropertyTests
{
    private static ImmutableArray<CompetitorId> Field(int size) =>
        Enumerable.Range(0, size).Select(_ => CompetitorId.New()).ToImmutableArray();

    // ------------------------------------------------------- structural

    private static readonly Gen<(int Field, int MinPerGroup, int Rounds)> StructuralInput =
        from field in Gen.Int[4, 16]
        from minPerGroup in Gen.Int[2, Math.Max(2, field / 2)]
        from rounds in Gen.Int[1, 5]
        select (field, minPerGroup, rounds);

    [Fact]
    public void BuildGroups_places_every_competitor_exactly_once_per_round_with_the_expected_group_sizes()
    {
        StructuralInput.Sample(t =>
        {
            var field = Field(t.Field);

            var rounds = PhaseDraw.BuildGroups(field, t.MinPerGroup, t.Rounds);

            rounds.Length.Should().Be(t.Rounds);

            // The real invariant PhaseDraw.BuildGroups builds to: groupCount
            // is chosen so no group need be smaller than minPerGroup, and the
            // field is then split as evenly as groupCount allows — NOT
            // "minPerGroup or minPerGroup + 1" (those only coincide when
            // minPerGroup happens to divide field.Length evenly; e.g. field
            // 11 / minPerGroup 4 gives two groups of 6 and 5, not 4-or-5).
            var groupCount = Math.Max(1, t.Field / t.MinPerGroup);
            var baseSize = t.Field / groupCount;

            foreach (var groups in rounds)
            {
                // Every competitor appears in exactly one group this round.
                var placed = groups.SelectMany(g => g).ToArray();
                placed.Length.Should().Be(t.Field);
                placed.Distinct().Count().Should().Be(t.Field);
                placed.Should().BeEquivalentTo(field);

                groups.Length.Should().Be(groupCount);
                groups.Should().OnlyContain(g => g.Length == baseSize || g.Length == baseSize + 1);
                groups.Should().OnlyContain(g => g.Length >= t.MinPerGroup);
            }
        });
    }

    // --------------------------------------------------------- fairness

    // Brute-force reference oracle, small inputs only: the partition search
    // space is combinatorial in both group count and round count, so this
    // runs at field 4..8 / rounds 1..3 rather than the plan's originally
    // proposed 4..9 / 1..4 — the extra corner (field 9 with many small
    // groups, or 4 rounds) pushes the *unordered*-partition count per round
    // into four figures, and raising either dimension by one more multiplies
    // the search depth on top of that. The greedy is not claimed optimal at
    // full contest scale (<=20 pilots, CLAUDE.md) — only checked for
    // correctness here, where "optimal" is itself checkable, mirroring the
    // rule text's own "as few times as possible", not "provably minimal".
    private static readonly Gen<(int Field, int MinPerGroup, int Rounds)> FairnessInput =
        from field in Gen.Int[4, 8]
        from minPerGroup in Gen.Int[2, Math.Max(2, field / 2)]
        from rounds in Gen.Int[1, 3]
        select (field, minPerGroup, rounds);

    [Fact]
    public void BuildGroups_maximum_pairwise_co_occurrence_matches_the_brute_force_minimum()
    {
        FairnessInput.Sample(t =>
        {
            var field = Field(t.Field);

            var actualRounds = PhaseDraw.BuildGroups(field, t.MinPerGroup, t.Rounds);
            var actualMax = MaxPairwise(actualRounds);

            var trueMinimum = TrueMinimumMaxPairwise(field, t.MinPerGroup, t.Rounds, actualMax);

            actualMax.Should().Be(trueMinimum);
        },
        iter: 50);
    }

    // ---------------------------------------------------- oracle helpers

    private static int MaxPairwise(ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> rounds)
    {
        var pairCount = new Dictionary<(CompetitorId, CompetitorId), int>();
        var max = 0;
        foreach (var groups in rounds)
        {
            foreach (var group in groups)
            {
                for (var i = 0; i < group.Length; i++)
                {
                    for (var j = i + 1; j < group.Length; j++)
                    {
                        var key = PairKey(group[i], group[j]);
                        var count = pairCount.GetValueOrDefault(key) + 1;
                        pairCount[key] = count;
                        if (count > max)
                        {
                            max = count;
                        }
                    }
                }
            }
        }

        return max;
    }

    /// <summary>
    /// Exhaustive search over every valid partition-per-round for the given
    /// field/minPerGroup, using PhaseDraw's own group-count/size formula (so
    /// the oracle partitions the same-shaped groups BuildGroups does), across
    /// every round, returning the true minimum achievable maximum pairwise
    /// count. Branch-and-bound: <paramref name="incumbent"/> seeds the bound
    /// with BuildGroups's own (already-valid) result, which is why most of
    /// the tree gets pruned immediately at this scale — the greedy is
    /// usually already optimal or very close for these small fields.
    /// </summary>
    private static int TrueMinimumMaxPairwise(
        ImmutableArray<CompetitorId> field, int minPerGroup, int roundCount, int incumbent)
    {
        var groupCount = Math.Max(1, field.Length / minPerGroup);
        var sizes = GroupSizes(field.Length, groupCount);
        var fieldIndex = field
            .Select((c, i) => (c, i))
            .ToDictionary(x => x.c, x => x.i);

        var partitions = AllPartitions(field, sizes, fieldIndex, -1).ToImmutableArray();
        var pairCount = new Dictionary<(CompetitorId, CompetitorId), int>();

        return Search(partitions, roundCount, pairCount, 0, incumbent);
    }

    private static int Search(
        ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> partitions,
        int roundsRemaining,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount,
        int currentMax,
        int best)
    {
        if (roundsRemaining == 0)
        {
            return Math.Min(best, currentMax);
        }

        foreach (var partition in partitions)
        {
            var roundMax = currentMax;
            var deltas = new List<((CompetitorId, CompetitorId) Key, int OldValue)>();

            foreach (var group in partition)
            {
                for (var i = 0; i < group.Length; i++)
                {
                    for (var j = i + 1; j < group.Length; j++)
                    {
                        var key = PairKey(group[i], group[j]);
                        var oldValue = pairCount.GetValueOrDefault(key);
                        deltas.Add((key, oldValue));
                        pairCount[key] = oldValue + 1;
                        if (oldValue + 1 > roundMax)
                        {
                            roundMax = oldValue + 1;
                        }
                    }
                }
            }

            // Pairwise counts only ever grow across further rounds, so a
            // partial max already at or past the incumbent can never
            // produce a strictly better complete assignment — prune.
            if (roundMax < best)
            {
                best = Search(partitions, roundsRemaining - 1, pairCount, roundMax, best);
            }

            foreach (var (key, oldValue) in deltas)
            {
                if (oldValue == 0)
                {
                    pairCount.Remove(key);
                }
                else
                {
                    pairCount[key] = oldValue;
                }
            }
        }

        return best;
    }

    /// <summary>Mirrors PhaseDraw's own private group-sizing formula, so the oracle's partitions have the same shape BuildGroups produces.</summary>
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
    /// Every way to partition <paramref name="remaining"/> into ordered slots
    /// of exactly <paramref name="sizes"/> (sizes non-increasing, as GroupSizes
    /// produces). Canonicalised against re-deriving the same unordered
    /// partition once per permutation of equal-size slots: within a run of
    /// equal sizes, each slot's chosen combination must have a strictly
    /// greater minimum field-index than the previous slot's — <paramref
    /// name="minIndexFloor"/> carries that constraint down the recursion.
    /// </summary>
    private static IEnumerable<ImmutableArray<ImmutableArray<CompetitorId>>> AllPartitions(
        ImmutableArray<CompetitorId> remaining,
        ImmutableArray<int> sizes,
        Dictionary<CompetitorId, int> fieldIndex,
        int minIndexFloor)
    {
        if (sizes.IsEmpty)
        {
            yield return ImmutableArray<ImmutableArray<CompetitorId>>.Empty;
            yield break;
        }

        var size = sizes[0];
        var nextIsSameSize = sizes.Length > 1 && sizes[1] == size;

        foreach (var combo in Combinations(remaining, size))
        {
            var minIndex = combo.Min(c => fieldIndex[c]);
            if (minIndex <= minIndexFloor)
            {
                continue;
            }

            var rest = remaining.Where(c => !combo.Contains(c)).ToImmutableArray();
            var childFloor = nextIsSameSize ? minIndex : -1;

            foreach (var tail in AllPartitions(rest, sizes.RemoveAt(0), fieldIndex, childFloor))
            {
                yield return tail.Insert(0, combo);
            }
        }
    }

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

    private static (CompetitorId, CompetitorId) PairKey(CompetitorId a, CompetitorId b) =>
        a.Value.CompareTo(b.Value) <= 0 ? (a, b) : (b, a);
}
