using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property-based tests for draw protection — kanban/in-progress/teams-mvp.md
/// WI-4, the four protection invariants the story names (protected separation,
/// structural preservation, fairness priority, regression) plus the
/// infeasibility behaviour (owner decision 5, least-bad). CsCheck, in
/// PhaseDrawPropertyTests's style, driving the real <see cref="PhaseDraw"/>
/// algorithm; the decide-level facts at the end drive
/// <see cref="Competition.DrawPhase"/>'s pair derivation end to end.
/// </summary>
public class DrawProtectionPropertyTests
{
    private static ImmutableArray<CompetitorId> Field(int size) =>
        Enumerable.Range(0, size).Select(_ => CompetitorId.New()).ToImmutableArray();

    private static readonly DateTimeOffset At = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------ input generators

    // Same shape as PhaseDrawPropertyTests's StructuralInput — the regression
    // property reruns exactly that input distribution through the new overload
    // with pairs = [].
    private static readonly Gen<(int Field, int MinPerGroup, int Rounds)> StructuralInput =
        from field in Gen.Int[4, 16]
        from minPerGroup in Gen.Int[2, Math.Max(2, field / 2)]
        from rounds in Gen.Int[1, 5]
        select (field, minPerGroup, rounds);

    private static readonly Gen<(int FieldSize, int MinPerGroup, int Rounds, int[] Perm, bool BigSecond)> SeparationInput =
        from fieldSize in Gen.Int[6, 14]
        from minPerGroup in Gen.Int[2, Math.Max(2, fieldSize / 2)]
        from rounds in Gen.Int[1, 4]
        from perm in Gen.Shuffle(Enumerable.Range(0, fieldSize).ToArray())
        from bigSecond in Gen.Bool
        select (fieldSize, minPerGroup, rounds, perm, bigSecond);

    private static readonly Gen<(int FieldSize, int MinPerGroup, int Rounds, int PairCount, int[] Perm)> AnyProtectionInput =
        from fieldSize in Gen.Int[4, 14]
        from minPerGroup in Gen.Int[2, Math.Max(2, fieldSize / 2)]
        from rounds in Gen.Int[1, 4]
        from pairCount in Gen.Int[1, 4]
        from perm in Gen.Shuffle(Enumerable.Range(0, fieldSize).ToArray())
        select (fieldSize, minPerGroup, rounds, Math.Min(pairCount, fieldSize - 1), perm);

    // Brute-force oracle scale, one notch smaller than PhaseDrawPropertyTests's
    // FairnessInput (4..8 / 1..3): the joint search multiplies the partition
    // count by the protection dimension, so this stays at field 4..6 / rounds
    // 1..2 where the exhaustive comparison is still cheap (WI-2's precedent —
    // the greedy is not claimed optimal at full contest scale, only held to
    // the true optimum where "optimal" is checkable).
    private static readonly Gen<(int FieldSize, int MinPerGroup, int Rounds, int PairCount, int[] Perm)> FairnessInput =
        from fieldSize in Gen.Int[4, 6]
        from minPerGroup in Gen.Int[2, Math.Min(3, fieldSize / 2)]
        from rounds in Gen.Int[1, 2]
        from pairCount in Gen.Int[1, 2]
        from perm in Gen.Shuffle(Enumerable.Range(0, fieldSize).ToArray())
        select (fieldSize, minPerGroup, rounds, pairCount, perm);

    private static readonly Gen<(int FieldSize, int Rounds, int[] Perm)> InfeasibleInput =
        from fieldSize in Gen.Int[4, 12]
        from rounds in Gen.Int[1, 3]
        from perm in Gen.Shuffle(Enumerable.Range(0, fieldSize).ToArray())
        select (fieldSize, rounds, perm);

    // ------------------------------------------------------------- regression

    [Fact]
    public void BuildGroups_with_no_protected_pairs_is_exactly_the_original_overload()
    {
        StructuralInput.Sample(t =>
        {
            var field = Field(t.Field);
            var minPerGroupByRound = Enumerable.Repeat(t.MinPerGroup, t.Rounds).ToImmutableArray();

            var original = PhaseDraw.BuildGroups(field, minPerGroupByRound);
            var withEmptyPairs = PhaseDraw.BuildGroups(field, minPerGroupByRound, []);

            SameDraw(withEmptyPairs, original).Should().BeTrue();
            AssertEveryCompetitorExactlyOncePerRound(withEmptyPairs, field, t.Rounds);
        });
    }

    // --------------------------------------------------- protected separation

    [Fact]
    public void BuildGroups_co_groups_no_protected_pair_when_a_zero_violation_partition_exists()
    {
        SeparationInput.Sample(t =>
        {
            var field = Field(t.FieldSize);
            var groupCount = Math.Max(1, t.FieldSize / t.MinPerGroup);

            // Two DISJOINT protection groups cut from the permutation — a pair,
            // plus a trio only when the group count allows one. Every group
            // sized within the group count makes a zero-violation partition
            // provably exist (spread each protection group one member per
            // drawn group), which is the precondition this property assumes.
            var first = t.Perm.Take(2).ToArray();
            var second = t.BigSecond && groupCount >= 3
                ? t.Perm.Skip(2).Take(3).ToArray()
                : t.Perm.Skip(2).Take(2).ToArray();
            var pairs = ProtectionPairs(field, first, second);
            var protectedSet = PairSet(pairs);

            var rounds = PhaseDraw.BuildGroups(
                field, Enumerable.Repeat(t.MinPerGroup, t.Rounds).ToImmutableArray(), pairs);

            foreach (var groups in rounds)
            {
                RoundViolations(groups, protectedSet).Should().Be(0);
            }

            AssertEveryCompetitorExactlyOncePerRound(rounds, field, t.Rounds);
        }, iter: 50);
    }

    // -------------------------------------------------- structural preservation

    [Fact]
    public void BuildGroups_with_protection_places_every_competitor_exactly_once_per_round()
    {
        AnyProtectionInput.Sample(t =>
        {
            var field = Field(t.FieldSize);

            // A CHAIN of pairs — (p0,p1), (p1,p2), … — deliberately arbitrary
            // shape: members shared across pairs, so the input may be
            // infeasible and the draw least-bad. Protection must never
            // duplicate, lose, or add a competitor regardless.
            var pairs = Enumerable.Range(0, t.PairCount)
                .Select(i => new ProtectedPair(field[t.Perm[i]], field[t.Perm[i + 1]]))
                .ToImmutableArray();

            var rounds = PhaseDraw.BuildGroups(
                field, Enumerable.Repeat(t.MinPerGroup, t.Rounds).ToImmutableArray(), pairs);

            AssertEveryCompetitorExactlyOncePerRound(rounds, field, t.Rounds);
        });
    }

    // ------------------------------------------------------- fairness priority

    [Fact]
    public void BuildGroups_matches_the_brute_force_joint_optimum_minimum_violations_then_repeats()
    {
        FairnessInput.Sample(t =>
        {
            var field = Field(t.FieldSize);

            var pairs = Enumerable.Range(0, t.PairCount)
                .Select(i => new ProtectedPair(field[t.Perm[i]], field[t.Perm[i + 1]]))
                .ToImmutableArray();
            var protectedSet = PairSet(pairs);

            var actualRounds = PhaseDraw.BuildGroups(
                field, Enumerable.Repeat(t.MinPerGroup, t.Rounds).ToImmutableArray(), pairs);
            var actual = DrawMetrics(actualRounds, protectedSet);

            var optimum = JointOptimum(field, t.MinPerGroup, t.Rounds, protectedSet, actual);

            actual.MaxRoundViolations.Should().Be(optimum.MaxRoundViolations);
            actual.MaxPairwise.Should().Be(optimum.MaxPairwise);
        }, iter: 50);
    }

    // ---------------------------------------------------------- infeasibility

    [Fact]
    public void BuildGroups_with_a_protection_group_larger_than_the_group_count_returns_the_least_bad_draw()
    {
        InfeasibleInput.Sample(t =>
        {
            var field = Field(t.FieldSize);
            var minPerGroup = 2;
            var groupCount = t.FieldSize / minPerGroup; // ≥ 2 for fieldSize ≥ 4

            // One protection group with one member more than there are drawn
            // groups: zero violations is unreachable (pigeonhole — two of them
            // must share a group), and the balanced spread achieves exactly
            // one co-grouped pair, so the least-bad draw (owner decision 5)
            // must return exactly one violation per round and terminate.
            var members = t.Perm.Take(groupCount + 1).ToArray();
            var pairs = ProtectionPairs(field, members);
            var protectedSet = PairSet(pairs);

            var rounds = PhaseDraw.BuildGroups(
                field, Enumerable.Repeat(minPerGroup, t.Rounds).ToImmutableArray(), pairs);

            rounds.Length.Should().Be(t.Rounds);
            foreach (var groups in rounds)
            {
                RoundViolations(groups, protectedSet).Should().Be(1);
            }

            AssertEveryCompetitorExactlyOncePerRound(rounds, field, t.Rounds);
        }, iter: 50);
    }

    // --------------------------------------- DrawPhase wiring (decide-level)

    [Fact]
    public void DrawPhase_keeps_a_protection_pair_in_different_groups_in_every_round()
    {
        var competition = F3JWith(12);
        var (a, b) = (competition.Competitors[0].Id, competition.Competitors[1].Id);
        competition = WithProtectionGroup(competition, "Helpers", a, b);

        var result = competition.DrawPhase(3, [], At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "draw succeeded");
        AssertPairNeverCoGrouped(result.Value, a, b);
        AssertDrawnExactlyOncePerRound(result.Value, 12);
    }

    [Fact]
    public void DrawPhase_unions_across_groups_and_dedups_pairs_by_canonical_key()
    {
        var competition = F3JWith(12);
        var ids = competition.Competitors.Select(c => c.Id).ToArray();
        // Two groups overlapping on one competitor, plus a third naming the
        // SAME pair as the first in reverse — the derived pair set is the
        // DEDUPED union {(a,b), (b,c)}, still perfectly separable in 2 groups.
        competition = WithProtectionGroup(competition, "Helpers", ids[0], ids[1]);
        competition = WithProtectionGroup(competition, "Deputies", ids[1], ids[2]);
        competition = WithProtectionGroup(competition, "Again", ids[1], ids[0]);

        var result = competition.DrawPhase(2, [], At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "draw succeeded");
        AssertPairNeverCoGrouped(result.Value, ids[0], ids[1]);
        AssertPairNeverCoGrouped(result.Value, ids[1], ids[2]);
        AssertDrawnExactlyOncePerRound(result.Value, 12);
    }

    [Fact]
    public void DrawPhase_drops_a_withdrawn_member_from_its_protection_groups()
    {
        var competition = F3JWith(13);
        var ids = competition.Competitors.Select(c => c.Id).ToArray();
        competition = WithProtectionGroup(competition, "Helpers", ids[0], ids[1], ids[2]);

        var withdrawn = competition.WithdrawCompetitor(ids[2], At);
        withdrawn.IsSuccess.Should().BeTrue(withdrawn.Code ?? "withdrawn");
        competition = competition.Apply(withdrawn.Value);

        var result = competition.DrawPhase(2, [], At);

        // 13 registered, one withdrawn → a live field of 12 in two groups, so
        // the surviving pair (ids[0], ids[1]) is still separable; the
        // withdrawn member drops out of the pair set with the draw field.
        result.IsSuccess.Should().BeTrue(result.Code ?? "draw succeeded");
        AssertPairNeverCoGrouped(result.Value, ids[0], ids[1]);
        AssertDrawnExactlyOncePerRound(result.Value, 12);
        result.Value.Rounds.Should().OnlyContain(round =>
            round.TaskRounds[0].Groups.SelectMany(g => g.CompetitorRefs).All(id => id != ids[2]));
    }

    [Fact]
    public void DrawPhase_returns_the_least_bad_draw_when_a_pair_cannot_be_separated()
    {
        // 11 live competitors at F3J's minPerGroup 6 → one group of 11: no
        // partition can separate the pair, and owner decision 5 demands the
        // least-bad draw — a successful minimum-violation draw, never a
        // rejection-upfront.
        var competition = F3JWith(11);
        var (a, b) = (competition.Competitors[0].Id, competition.Competitors[1].Id);
        competition = WithProtectionGroup(competition, "Helpers", a, b);

        var result = competition.DrawPhase(2, [], At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "draw succeeded");
        foreach (var round in result.Value.Rounds)
        {
            round.TaskRounds[0].Groups.Should().ContainSingle()
                .Which.CompetitorRefs.Should().Contain(a).And.Contain(b);
        }
        AssertDrawnExactlyOncePerRound(result.Value, 11);
    }

    // ---------------------------------------------------------- test helpers

    private static Competition F3JWith(int competitorCount)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = SeedF3J.Definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SeedF3J.Definition.Version,
            AdoptedAt = At,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Draw Protection Test Comp", "Nowhere",
            new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 3),
            "1.0.0", adoptedRules, At);

        var competition = Competition.Create(created);

        for (var i = 0; i < competitorCount; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), At);
            competition = competition.Apply(registered.Value);
        }

        return competition;
    }

    private static Competition WithProtectionGroup(Competition competition, string name, params CompetitorId[] members)
    {
        var defined = competition.DefineProtectionGroup(ProtectionGroupId.New(), name, At);
        defined.IsSuccess.Should().BeTrue(defined.Code ?? "protection group defined");
        competition = competition.Apply(defined.Value);

        foreach (var member in members)
        {
            var added = competition.AddProtectionGroupMember(member, defined.Value.Group.Id, At);
            added.IsSuccess.Should().BeTrue(added.Code ?? "protection member added");
            competition = competition.Apply(added.Value);
        }

        return competition;
    }

    private static void AssertPairNeverCoGrouped(PhaseDrawn drawn, CompetitorId a, CompetitorId b)
    {
        foreach (var round in drawn.Rounds)
        {
            foreach (var group in round.TaskRounds[0].Groups)
            {
                (group.CompetitorRefs.Contains(a) && group.CompetitorRefs.Contains(b))
                    .Should().BeFalse($"round {round.Ordinal}: protected pair co-grouped");
            }
        }
    }

    private static void AssertDrawnExactlyOncePerRound(PhaseDrawn drawn, int liveFieldSize)
    {
        foreach (var round in drawn.Rounds)
        {
            var placed = round.TaskRounds[0].Groups.SelectMany(g => g.CompetitorRefs).ToArray();
            placed.Length.Should().Be(liveFieldSize);
            placed.Distinct().Count().Should().Be(liveFieldSize);
        }
    }

    private static void AssertEveryCompetitorExactlyOncePerRound(
        ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> rounds,
        ImmutableArray<CompetitorId> field,
        int roundCount)
    {
        rounds.Length.Should().Be(roundCount);
        foreach (var groups in rounds)
        {
            var placed = groups.SelectMany(g => g).ToArray();
            placed.Length.Should().Be(field.Length);
            placed.Distinct().Count().Should().Be(field.Length);
            placed.Should().BeEquivalentTo(field);
        }
    }

    private static ImmutableArray<ProtectedPair> ProtectionPairs(ImmutableArray<CompetitorId> field, params int[][] groups)
    {
        var pairs = new List<ProtectedPair>();

        foreach (var group in groups)
        {
            for (var i = 0; i < group.Length; i++)
            {
                for (var j = i + 1; j < group.Length; j++)
                {
                    pairs.Add(new ProtectedPair(field[group[i]], field[group[j]]));
                }
            }
        }

        return [.. pairs];
    }

    private static HashSet<(CompetitorId, CompetitorId)> PairSet(ImmutableArray<ProtectedPair> pairs) =>
        pairs.Select(p => PairKey(p.A, p.B)).ToHashSet();

    /// <summary>Protected pairs co-grouped within ONE round's groups.</summary>
    private static int RoundViolations(
        ImmutableArray<ImmutableArray<CompetitorId>> groups,
        HashSet<(CompetitorId, CompetitorId)> protectedPairs)
    {
        var violations = 0;

        foreach (var group in groups)
        {
            for (var i = 0; i < group.Length; i++)
            {
                for (var j = i + 1; j < group.Length; j++)
                {
                    if (protectedPairs.Contains(PairKey(group[i], group[j])))
                    {
                        violations++;
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>The repeat objective unchanged (max pairwise co-occurrence across the draw) plus the protection dimension (max per-round violation count).</summary>
    private static (int MaxRoundViolations, int MaxPairwise) DrawMetrics(
        ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> rounds,
        HashSet<(CompetitorId, CompetitorId)> protectedPairs)
    {
        var pairCount = new Dictionary<(CompetitorId, CompetitorId), int>();
        var maxViolations = 0;
        var maxPairwise = 0;

        foreach (var groups in rounds)
        {
            var roundViolations = 0;

            foreach (var group in groups)
            {
                for (var i = 0; i < group.Length; i++)
                {
                    for (var j = i + 1; j < group.Length; j++)
                    {
                        var key = PairKey(group[i], group[j]);
                        var count = pairCount.GetValueOrDefault(key) + 1;
                        pairCount[key] = count;
                        if (count > maxPairwise)
                        {
                            maxPairwise = count;
                        }

                        if (protectedPairs.Contains(key))
                        {
                            roundViolations++;
                        }
                    }
                }
            }

            if (roundViolations > maxViolations)
            {
                maxViolations = roundViolations;
            }
        }

        return (maxViolations, maxPairwise);
    }

    private static bool SameDraw(
        ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> left,
        ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var r = 0; r < left.Length; r++)
        {
            if (left[r].Length != right[r].Length)
            {
                return false;
            }

            for (var g = 0; g < left[r].Length; g++)
            {
                if (!left[r][g].SequenceEqual(right[r][g]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // ---------------------------------------------------- brute-force oracle

    /// <summary>
    /// Exhaustive lexicographic optimum over every valid partition-per-round
    /// (same group-count/size formula PhaseDraw uses, so the oracle searches
    /// the same-shaped partitions): minimum max-per-round protection
    /// violations FIRST, then minimum max pairwise co-occurrence — the
    /// fairness-priority ordering, extended with the protection dimension.
    /// Branch-and-bound: <paramref name="incumbent"/> seeds the bound with
    /// BuildGroups's own (already-valid) result, which prunes most of the
    /// tree at this scale.
    /// </summary>
    private static (int MaxRoundViolations, int MaxPairwise) JointOptimum(
        ImmutableArray<CompetitorId> field,
        int minPerGroup,
        int roundCount,
        HashSet<(CompetitorId, CompetitorId)> protectedPairs,
        (int MaxRoundViolations, int MaxPairwise) incumbent)
    {
        var groupCount = Math.Max(1, field.Length / minPerGroup);
        var sizes = GroupSizes(field.Length, groupCount);
        var fieldIndex = field
            .Select((c, i) => (c, i))
            .ToDictionary(x => x.c, x => x.i);

        var partitions = AllPartitions(field, sizes, fieldIndex, -1).ToImmutableArray();

        return JointSearch(partitions, roundCount, protectedPairs, [], 0, 0, incumbent);
    }

    private static (int MaxRoundViolations, int MaxPairwise) JointSearch(
        ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> partitions,
        int roundsRemaining,
        HashSet<(CompetitorId, CompetitorId)> protectedPairs,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount,
        int currentViolations,
        int currentPairwise,
        (int MaxRoundViolations, int MaxPairwise) best)
    {
        if (roundsRemaining == 0)
        {
            return BetterThan((currentViolations, currentPairwise), best)
                ? (currentViolations, currentPairwise)
                : best;
        }

        foreach (var partition in partitions)
        {
            var roundViolations = 0;
            var roundMax = currentPairwise;
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

                        if (protectedPairs.Contains(key))
                        {
                            roundViolations++;
                        }
                    }
                }
            }

            var nextViolations = Math.Max(currentViolations, roundViolations);

            // Both metrics only ever grow across further rounds, so a partial
            // already at or past the incumbent lexicographically can never
            // produce a strictly better complete assignment — prune.
            if (BetterThan((nextViolations, roundMax), best))
            {
                best = JointSearch(partitions, roundsRemaining - 1, protectedPairs, pairCount, nextViolations, roundMax, best);
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

    /// <summary>Minimum violations first, then the repeat objective — the fairness-priority ordering.</summary>
    private static bool BetterThan((int MaxRoundViolations, int MaxPairwise) candidate, (int MaxRoundViolations, int MaxPairwise) incumbent) =>
        candidate.MaxRoundViolations < incumbent.MaxRoundViolations
        || (candidate.MaxRoundViolations == incumbent.MaxRoundViolations && candidate.MaxPairwise < incumbent.MaxPairwise);

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
    /// produces), canonicalised against re-deriving the same unordered
    /// partition once per permutation of equal-size slots.
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
