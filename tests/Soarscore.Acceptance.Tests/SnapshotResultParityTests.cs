// gs_01_f5k-snapshot-result-parity.md verification — negative comparator
// cases for the snapshot/result-oracle contract. Pure unit tests: they drive
// Comparator.CompareRankingGrain (places + bidirectional population) and
// Comparator.CompareFinalTotalsGrain (exact totals) directly against synthetic
// fixtures, so no store, HTTP or replay is involved. The real F5K snapshot is
// pinned through the acceptance scenario beside these cases.
//
// Invariant under test: equality requires the same ranked identity set and
// equal values for every declared external result field; internal
// conservation is an additional, independent assertion (the acceptance
// scenario's conservation step) and plays no part here.
//
// Property-based cases were considered: without an FsCheck dependency the
// same arbitrary-identity coverage comes from xunit theories over varied
// pilot numbers and places below, which exercise the set logic rather than
// fixed examples.

using System.Collections.Immutable;
using AwesomeAssertions;
using Xunit;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Acceptance.Tests.Support.Gliderscore;

namespace Soarscore.Acceptance.Tests;

public sealed class SnapshotResultParityTests
{
    // The real F5K snapshot shape: five ranked pilots with exact totals and
    // places, plus pilot 88 declared zero-only unranked.
    private static readonly ExpectedRank[] F5KRanks =
    [
        new(80, "1"),
        new(75, "2"),
        new(82, "3"),
        new(79, "4"),
        new(102, "5"),
    ];

    private static readonly ExpectedPilotTotal[] F5KTotals =
    [
        new(80, 5000.000m),
        new(75, 4928.100m),
        new(82, 4796.100m),
        new(79, 4359.900m),
        new(102, 4319.200m),
    ];

    private static readonly ExpectedUnrankedPilot[] F5KUnranked = [new(88, 0.000m)];

    private static readonly Dictionary<long, decimal> F5KScores = new()
    {
        [80] = 5000.000m,
        [75] = 4928.100m,
        [82] = 4796.100m,
        [79] = 4359.900m,
        [102] = 4319.200m,
        [88] = 0.000m,
    };

    private static readonly Dictionary<long, int> F5KPlacings = new()
    {
        [80] = 1,
        [75] = 2,
        [82] = 3,
        [79] = 4,
        [102] = 5,
        [88] = 6,
    };

    [Fact]
    public void ExactSnapshot_ComparesClean()
    {
        var mismatches = Compare(F5KRanks, F5KTotals, F5KUnranked, F5KScores, F5KPlacings);

        mismatches.Should().BeEmpty("the real snapshot shape — exact totals, exact places, declared zero-only 88 — must compare clean");
    }

    [Fact]
    public void WrongTotal_WithUnchangedPlaces_Fails()
    {
        // The placeholder-zero-discard shape: pilot 75 publishes the full
        // six-cell sum 5817.600 (dropping an unflown zero) at the same
        // placing 2 GS gives 4928.100 (dropping the worst real score).
        var scores = new Dictionary<long, decimal>(F5KScores) { [75] = 5817.600m };

        var mismatches = Compare(F5KRanks, F5KTotals, F5KUnranked, scores, F5KPlacings);

        var total = mismatches.Should().ContainSingle(
            "a wrong total with unchanged places must fail exactly once").Subject;
        total.Grain.Should().Be("total");
        total.PilotNo.Should().Be(75);
        total.Ours.Should().Be(5817.600m);
        total.Expected.Should().Be(4928.100m);
    }

    [Fact]
    public void ExtraRankedCompetitor_Fails()
    {
        var scores = new Dictionary<long, decimal>(F5KScores) { [99] = 4000.000m };
        var placings = new Dictionary<long, int>(F5KPlacings) { [99] = 6 };

        // Pilot 88 still holds 6 here, so move him out of the way: the point
        // is the undeclared extra, not the placing collision.
        placings[88] = 7;

        var mismatches = Compare(F5KRanks, F5KTotals, F5KUnranked, scores, placings);

        mismatches.Should().ContainSingle(
            "an extra ranked competitor fails, even at a place the oracle never names")
            .Which.PilotNo.Should().Be(99);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(99)]
    [InlineData(1001)]
    public void ExtraCompetitor_AtUnnamedPlace_Fails_ForArbitraryIdentities(long extraPilot)
    {
        var scores = new Dictionary<long, decimal>(F5KScores) { [extraPilot] = 100.000m };
        var placings = new Dictionary<long, int>(F5KPlacings) { [extraPilot] = 7 };

        var mismatches = Compare(F5KRanks, F5KTotals, F5KUnranked, scores, placings);

        mismatches.Should().Contain(m => m.PilotNo == extraPilot,
            "an extra competitor fails whatever identity it carries");
    }

    [Theory]
    [InlineData(80)]
    [InlineData(75)]
    [InlineData(82)]
    [InlineData(79)]
    [InlineData(102)]
    public void MissingRankedCompetitor_Fails_ForEveryOraclePilot(long missingPilot)
    {
        var scores = new Dictionary<long, decimal>(F5KScores);
        scores.Remove(missingPilot);
        var placings = new Dictionary<long, int>(F5KPlacings);
        placings.Remove(missingPilot);

        var mismatches = Compare(F5KRanks, F5KTotals, F5KUnranked, scores, placings);

        mismatches.Should().Contain(m => m.PilotNo == missingPilot,
            "a missing ranked competitor fails whichever pilot goes missing");
    }

    [Fact]
    public void UndeclaredZeroOnlyExtra_Fails()
    {
        // Zero-only treatment is explicit: the same pilot 88 shape with no
        // declaration must fail rather than pass silently.
        var mismatches = Compare(F5KRanks, F5KTotals, Unranked: [], F5KScores, F5KPlacings);

        mismatches.Should().ContainSingle().Which.PilotNo.Should().Be(88);
    }

    [Fact]
    public void DeclaredZeroOnly_WithNonZeroTotal_Fails()
    {
        var scores = new Dictionary<long, decimal>(F5KScores) { [88] = 10.000m };

        var mismatches = Compare(F5KRanks, F5KTotals, F5KUnranked, scores, F5KPlacings);

        var total = mismatches.Should().ContainSingle().Subject;
        total.Grain.Should().Be("total");
        total.PilotNo.Should().Be(88);
    }

    [Fact]
    public void DeclaredZeroOnly_HoldingRankedPlace_Fails()
    {
        // A zero total is not enough on its own: the declared-unranked pilot
        // must sort strictly behind every ranked place.
        var placings = new Dictionary<long, int>(F5KPlacings) { [88] = 3 };

        var mismatches = Compare(F5KRanks, F5KTotals, F5KUnranked, F5KScores, placings);

        mismatches.Should().Contain(
            m => m.PilotNo == 88 && m.Detail.Contains("unrankedZeroOnly"),
            "the declared-unranked pilot must sort strictly behind every ranked place");
    }

    [Fact]
    public void NoDeclaredTotals_ComparesPlacesOnly()
    {
        // Fixtures without a snapshot oracle keep the place-only grain: wildly
        // different scores never reach a totals comparison that was never
        // declared.
        var scores = new Dictionary<long, decimal>(F5KScores) { [75] = 5817.600m };

        var mismatches = Compare(F5KRanks, Totals: [], Unranked: [], scores, F5KPlacingsWithout88());

        mismatches.Should().BeEmpty("no declared totals means no totals comparison");
    }

    // -------------------------------------------------------------- plumbing

    private static Dictionary<long, int> F5KPlacingsWithout88()
    {
        var placings = new Dictionary<long, int>(F5KPlacings);
        placings.Remove(88);
        return placings;
    }

    private static List<GrainMismatch> Compare(
        ExpectedRank[] ranks,
        IReadOnlyList<ExpectedPilotTotal> Totals,
        IReadOnlyList<ExpectedUnrankedPilot> Unranked,
        Dictionary<long, decimal> scores,
        Dictionary<long, int> placings)
    {
        var fixture = SyntheticFixture(ranks, Totals, Unranked);
        var outcome = SyntheticOutcome(scores.Keys.Union(placings.Keys).Distinct().Order());
        var finalScores = SyntheticFinalScores(outcome, scores, placings);
        var mismatches = new List<GrainMismatch>();

        Comparator.CompareRankingGrain(fixture, outcome, finalScores, mismatches);
        Comparator.CompareFinalTotalsGrain(fixture, outcome, finalScores, mismatches);

        return mismatches;
    }

    private static GliderscoreFixture SyntheticFixture(
        ExpectedRank[] ranks,
        IReadOnlyList<ExpectedPilotTotal> Totals,
        IReadOnlyList<ExpectedUnrankedPilot> Unranked) => new(
        Slug: "synthetic-snapshot-parity",
        Directory: "",
        Competition: new CompetitionFile(
            new CompetitionIdentity(0, "synthetic", "F5K2024", "2026-01-01"),
            new CompetitionScoring(1, 1, 0),
            new FamilyRowsTable()),
        Entries: new EntriesFile(new CompPilotsTable([]), new PilotsTable([])),
        ScoresRaw: new ScoresRawFile([]),
        ExpectedScores: new ExpectedScoresFile(new Dictionary<string, ExpectedCell>()),
        ExpectedResult: new ExpectedResultFile(
            ranks,
            Totals,
            Unranked,
            new ExpectedScoredWindow(1, 6),
            Lifecycle: "finalised-snapshot-of-scored-window",
            ExcludedRounds: [7, 8, 9, 10]),
        Divergences: [],
        Definition: null!);

    private static ReplayOutcome SyntheticOutcome(IEnumerable<long> pilotNos)
    {
        var byPilot = pilotNos.ToDictionary(p => p, _ => CompetitorId.New());

        return new ReplayOutcome(
            CompetitionId: new CompetitionId(Guid.CreateVersion7()),
            PhaseOrdinal: 0,
            TaskCodeByRoundNo: new Dictionary<int, string>(),
            RoundOrdinalByRoundNo: new Dictionary<int, int>(),
            GroupIdByRoundAndGroup: new Dictionary<(int RoundNo, int GroupNo), GroupId>(),
            EntryIdBySlot: new Dictionary<(int RoundNo, int GroupNo, long PilotNo), EntryId>(),
            CompetitorByPilotNo: byPilot,
            CommandsIssued: 0);
    }

    private static CompetitionScoreView SyntheticFinalScores(
        ReplayOutcome outcome,
        Dictionary<long, decimal> scores,
        Dictionary<long, int> placings)
    {
        var views = scores.Select(kv => new CompetitorFinalScoreView(
            outcome.CompetitorByPilotNo[kv.Key],
            kv.Value,
            Disqualified: false,
            placings.TryGetValue(kv.Key, out var placing) ? placing : null));

        return new CompetitionScoreView([.. views]);
    }
}
