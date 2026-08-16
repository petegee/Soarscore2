// kanban/completed/phase-drawn-steel-thread-plan.md WI-6a.
//
// A hand-built Rounds fixture with known, hand-computed counts — 3 rounds x
// 2 groups of 3 over 6 competitors, varying membership each round (not the
// same partition repeated, so this exercises the summation across rounds,
// not just within one). WI-2's brute-force fairness oracle lives in
// Soarscore.Domain.Tests (PhaseDrawPropertyTests) as private search
// machinery scoped to that test project — not reusable across the assembly
// boundary — so this gets its "verified against known-correct small-N
// counts" the same way the plan's own worked example does: by hand, which is
// entirely tractable at this scale (15 pairs, 18 pair-formations total).

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;
using Xunit;

namespace Soarscore.Application.Tests.Queries.Competitions;

public class PairwiseCoOccurrenceTests
{
    [Fact]
    public void Compute_against_a_hand_built_three_round_fixture_matches_hand_computed_counts()
    {
        var a = CompetitorId.New();
        var b = CompetitorId.New();
        var c = CompetitorId.New();
        var d = CompetitorId.New();
        var e = CompetitorId.New();
        var f = CompetitorId.New();

        // Round1: {A,B,C} {D,E,F}
        // Round2: {A,D,E} {B,C,F}
        // Round3: {A,F,B} {C,D,E}
        var rounds = ImmutableArray.Create(
            Round(1, Group(1, a, b, c), Group(2, d, e, f)),
            Round(2, Group(1, a, d, e), Group(2, b, c, f)),
            Round(3, Group(1, a, f, b), Group(2, c, d, e)));

        var entries = PairwiseCoOccurrence.ComputeEntries(rounds);

        var byPair = entries.ToDictionary(x => (x.CompetitorA, x.CompetitorB), x => x.Count);

        // Hand-computed: AB=2 (R1,R3), AC=1 (R1), AD=1 (R2), AE=1 (R2), AF=1 (R3),
        // BC=2 (R1,R2), BF=2 (R2,R3), CD=1 (R3), CE=1 (R3), CF=1 (R2),
        // DE=3 (R1,R2,R3), DF=1 (R1), EF=1 (R1). BD=BE=0 (never co-grouped, absent).
        Lookup(byPair, a, b).Should().Be(2);
        Lookup(byPair, a, c).Should().Be(1);
        Lookup(byPair, a, d).Should().Be(1);
        Lookup(byPair, a, e).Should().Be(1);
        Lookup(byPair, a, f).Should().Be(1);
        Lookup(byPair, b, c).Should().Be(2);
        Lookup(byPair, b, f).Should().Be(2);
        Lookup(byPair, c, d).Should().Be(1);
        Lookup(byPair, c, e).Should().Be(1);
        Lookup(byPair, c, f).Should().Be(1);
        Lookup(byPair, d, e).Should().Be(3);
        Lookup(byPair, d, f).Should().Be(1);
        Lookup(byPair, e, f).Should().Be(1);

        byPair.Should().NotContainKey(PairKey(b, d));
        byPair.Should().NotContainKey(PairKey(b, e));
        entries.Sum(x => x.Count).Should().Be(18); // 3 rounds x 2 groups x C(3,2) pairs

        entries.Should().BeInAscendingOrder(x => x.CompetitorA.Value).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Compute_against_a_phase_with_no_rounds_returns_no_entries()
    {
        var entries = PairwiseCoOccurrence.ComputeEntries([]);

        entries.Should().BeEmpty();
    }

    private static int Lookup(Dictionary<(CompetitorId, CompetitorId), int> byPair, CompetitorId x, CompetitorId y) =>
        byPair.GetValueOrDefault(PairKey(x, y));

    private static (CompetitorId, CompetitorId) PairKey(CompetitorId x, CompetitorId y) =>
        x.Value.CompareTo(y.Value) <= 0 ? (x, y) : (y, x);

    private static Group Group(int ordinal, params CompetitorId[] members) =>
        new() { Id = GroupId.New(), Ordinal = ordinal, CompetitorRefs = [.. members] };

    private static Round Round(int ordinal, params Group[] groups) =>
        new()
        {
            Ordinal = ordinal,
            TaskRounds = [new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "D", Groups = [.. groups] }],
        };
}
