// gs_02_complete-result-oracles.md verification — negative comparator cases
// for the complete-result grains (pre-drop totals, aggregate penalties,
// discards) and the availability/scope contract gate. Pure unit tests: they
// drive Comparator.ComparePreDropTotalsGrain, ComparePenaltyDeductionGrain,
// CompareDiscardsGrain and RequireCompleteOracleContract directly against
// synthetic fixtures and actuals, so no store, HTTP or replay is involved.
// The real corpus oracles are pinned through the acceptance scenarios beside
// these cases (the "complete result oracle matches exactly" step).
//
// Invariant under test: changing an asserted field value or a discarded
// identity breaks equality even when every other field and the final order
// is unchanged. Internal conservation plays no part here — it is asserted
// independently by the acceptance scenarios' conservation step.

using AwesomeAssertions;
using Xunit;
using Soarscore.Acceptance.Tests.Support.Gliderscore;

namespace Soarscore.Acceptance.Tests;

public sealed class CompleteResultOracleTests
{
    private static readonly ExpectedRank[] Ranks =
    [
        new(12, "1"),
        new(28, "2"),
        new(42, "3"),
    ];

    // f3k-sample-comp shape: R1-R5 real cells, R6-R9 placeholder zeros, one
    // R9-zero drop per pilot (latest-equally-bad-first), pilot 42 penalised.
    private static Dictionary<long, Comparator.ActualResult> BaseActuals() => new()
    {
        [12] = new Comparator.ActualResult(3749m, 3749m, 0m, "task", [9], [0m], 1),
        [28] = new Comparator.ActualResult(4609m, 4609m, 0m, "task", [9], [0m], 2),
        [42] = new Comparator.ActualResult(2957m, 3057m, 100m, "task", [9], [0m], 3),
    };

    private static GliderscoreFixture Fixture(
        IReadOnlyList<ExpectedPreDropTotal>? preDrop = null,
        IReadOnlyList<ExpectedPilotPenalty>? penalties = null,
        IReadOnlyList<ExpectedPilotDiscards>? discards = null,
        IReadOnlyList<ResultFieldAvailability>? availability = null,
        ExpectedScoredWindow? window = null,
        string? lifecycle = null,
        IReadOnlyList<int>? excluded = null,
        bool withScope = true,
        bool withAvailability = true) => new(
        Slug: "synthetic-complete-result",
        Directory: "",
        Competition: new CompetitionFile(
            new CompetitionIdentity(0, "synthetic", "F3K", "2026-01-01"),
            new CompetitionScoring(1, 0, 0),
            new FamilyRowsTable()),
        Entries: new EntriesFile(new CompPilotsTable([]), new PilotsTable([])),
        ScoresRaw: new ScoresRawFile([]),
        ExpectedScores: new ExpectedScoresFile(new Dictionary<string, ExpectedCell>()),
        ExpectedResult: new ExpectedResultFile(
            Ranks,
            Totals: [new(12, 3749m), new(28, 4609m), new(42, 2957m)],
            UnrankedZeroOnly: [],
            ScoredWindow: withScope ? (window ?? new ExpectedScoredWindow(1, 9)) : null,
            Lifecycle: withScope ? (lifecycle ?? "finalised-full-including-unflown-placeholders") : null,
            ExcludedRounds: withScope ? (excluded ?? []) : null,
            PreDropTotals: preDrop ?? [new(12, 3749m), new(28, 4609m), new(42, 3057m)],
            Penalties: penalties ?? [new(12, 0m), new(28, 0m), new(42, 100m)],
            Discards: discards ?? [
                new(12, "task", [9], [0m]),
                new(28, "task", [9], [0m]),
                new(42, "task", [9], [0m])],
            FieldAvailability: withAvailability ? (availability ?? AllAvailable()) : availability),
        Divergences: [],
        Definition: null!);

    private static List<ResultFieldAvailability> AllAvailable() =>
    [
        new("total", "available", "synthetic"),
        new("preDropTotal", "available", "synthetic"),
        new("penaltyDeduction", "available", "synthetic"),
        new("discards", "available", "synthetic"),
        new("places", "available", "synthetic"),
        new("population", "available", "synthetic"),
    ];

    [Fact]
    public void ExactCompleteResult_ComparesClean()
    {
        var fixture = Fixture();
        var actuals = BaseActuals();
        var mismatches = new List<GrainMismatch>();

        Comparator.ComparePreDropTotalsGrain(fixture, actuals, mismatches);
        Comparator.ComparePenaltyDeductionGrain(fixture, actuals, mismatches);
        Comparator.CompareDiscardsGrain(fixture, actuals, mismatches);

        mismatches.Should().BeEmpty("the exact shape must compare clean");
    }

    [Fact]
    public void WrongPreDrop_WithUnchangedTotal_Fails()
    {
        // A cell error masked by a compensating drop shift: the final total
        // is unchanged, so only the pre-drop grain can fail this shape.
        var actuals = BaseActuals();
        actuals[28] = actuals[28] with { PreDropTotal = 4600m };
        var mismatches = new List<GrainMismatch>();

        Comparator.ComparePreDropTotalsGrain(Fixture(), actuals, mismatches);

        var mismatch = mismatches.Should().ContainSingle(
            "a wrong pre-drop total with an unchanged final total must fail exactly once").Subject;
        mismatch.Grain.Should().Be("preDrop");
        mismatch.PilotNo.Should().Be(28);
        mismatch.Ours.Should().Be(4600m);
        mismatch.Expected.Should().Be(4609m);
    }

    [Fact]
    public void WrongPenalty_Fails()
    {
        var actuals = BaseActuals();
        actuals[42] = actuals[42] with { PenaltyDeduction = 0m };
        var mismatches = new List<GrainMismatch>();

        Comparator.ComparePenaltyDeductionGrain(Fixture(), actuals, mismatches);

        var mismatch = mismatches.Should().ContainSingle(
            "a wrong penalty deduction must fail exactly once").Subject;
        mismatch.Grain.Should().Be("penalty");
        mismatch.PilotNo.Should().Be(42);
    }

    [Fact]
    public void ZeroPenalty_AssertedExplicitly()
    {
        // Omission never means zero: pilots with no penalty carry explicit
        // zero expectations, and a nonzero actual against them fails.
        var actuals = BaseActuals();
        actuals[12] = actuals[12] with { PenaltyDeduction = 100m };
        var mismatches = new List<GrainMismatch>();

        Comparator.ComparePenaltyDeductionGrain(Fixture(), actuals, mismatches);

        mismatches.Should().ContainSingle().Which.PilotNo.Should().Be(12);
    }

    [Fact]
    public void WrongDiscardIdentity_PreservingTotal_Fails()
    {
        // Every dropped candidate here is a zero cell: dropping R6 instead
        // of R9 preserves the total exactly, so only the identity check
        // fails this shape (the f3k-sample-comp literal-record precedent).
        var actuals = BaseActuals();
        actuals[28] = actuals[28] with
        {
            DiscardedRounds = [6],
            DiscardedValues = [0m],
        };
        var mismatches = new List<GrainMismatch>();

        Comparator.CompareDiscardsGrain(Fixture(), actuals, mismatches);

        var mismatch = mismatches.Should().ContainSingle(
            "a wrong discarded identity preserving the total must fail exactly once").Subject;
        mismatch.Grain.Should().Be("discard");
        mismatch.PilotNo.Should().Be(28);
        mismatch.Detail.Should().Contain("identity");
    }

    [Fact]
    public void WrongDiscardValue_Fails()
    {
        var actuals = BaseActuals();
        actuals[12] = actuals[12] with { DiscardedValues = [10m] };
        var mismatches = new List<GrainMismatch>();

        Comparator.CompareDiscardsGrain(Fixture(), actuals, mismatches);

        var mismatch = mismatches.Should().ContainSingle(
            "a wrong discarded value must fail exactly once").Subject;
        mismatch.Grain.Should().Be("discard");
        mismatch.Detail.Should().Contain("values");
    }

    [Fact]
    public void WrongDiscardUnit_Fails()
    {
        // A round discard is not a task discard — the source distinction is
        // preserved, never normalised away.
        var actuals = BaseActuals();
        actuals[12] = actuals[12] with { DiscardUnit = "round" };
        var mismatches = new List<GrainMismatch>();

        Comparator.CompareDiscardsGrain(Fixture(), actuals, mismatches);

        var mismatch = mismatches.Should().ContainSingle(
            "a wrong discard unit must fail exactly once").Subject;
        mismatch.Grain.Should().Be("discard");
        mismatch.Detail.Should().Contain("unit");
    }

    [Fact]
    public void UndeclaredEngineDrop_Fails()
    {
        // An engine drop for a pilot with no oracle entry is a defect, never
        // a pass — unknown discard identity is never a proven empty set.
        var fixture = Fixture(discards: [new(12, "task", [9], [0m])]);
        var actuals = BaseActuals();
        var mismatches = new List<GrainMismatch>();

        Comparator.CompareDiscardsGrain(fixture, actuals, mismatches);

        mismatches.Should().Contain(m => m.PilotNo == 28,
            "an undeclared engine drop for pilot 28 must fail");
        mismatches.Should().Contain(m => m.PilotNo == 42,
            "an undeclared engine drop for pilot 42 must fail");
    }

    [Fact]
    public void DeclaredEmptyDiscards_CompareClean()
    {
        // A proven empty discard set (no drops can fire) compares clean —
        // the explicit empty declaration, never a silent omission.
        var fixture = Fixture(discards: [
            new(12, "round", [], []),
            new(28, "round", [], []),
            new(42, "round", [], [])]);
        var actuals = new Dictionary<long, Comparator.ActualResult>
        {
            [12] = new Comparator.ActualResult(3749m, 3749m, 0m, "round", [], [], 1),
            [28] = new Comparator.ActualResult(4609m, 4609m, 0m, "round", [], [], 2),
            [42] = new Comparator.ActualResult(2957m, 3057m, 100m, "round", [], [], 3),
        };
        var mismatches = new List<GrainMismatch>();

        Comparator.CompareDiscardsGrain(fixture, actuals, mismatches);

        mismatches.Should().BeEmpty("proven-empty discard sets compare clean");
    }

    [Fact]
    public void ZeroOnlyPilot_ExemptFromDiscards()
    {
        // The f5k pilot-88 shape: a declared zero-only pilot carries no
        // discard assertion even when the engine drops one of their tied
        // zero cells — the identity is unknown, never proven empty.
        var ranks = new[] { new ExpectedRank(12, "1"), new ExpectedRank(28, "2") };
        var fixture = new GliderscoreFixture(
            Slug: "synthetic-zero-only",
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
                Totals: [new(12, 1000m), new(28, 900m)],
                UnrankedZeroOnly: [new ExpectedUnrankedPilot(88, 0m)],
                ScoredWindow: new ExpectedScoredWindow(1, 2),
                Lifecycle: "finalised-snapshot-of-scored-window",
                ExcludedRounds: [],
                PreDropTotals: [new(12, 1000m), new(28, 900m), new(88, 0m)],
                Penalties: [new(12, 0m), new(28, 0m), new(88, 0m)],
                Discards: [new(12, "round", [], []), new(28, "round", [], [])],
                FieldAvailability: AllAvailable()),
            Divergences: [],
            Definition: null!);
        var actuals = new Dictionary<long, Comparator.ActualResult>
        {
            [12] = new Comparator.ActualResult(1000m, 1000m, 0m, "round", [], [], 1),
            [28] = new Comparator.ActualResult(900m, 900m, 0m, "round", [], [], 2),
            [88] = new Comparator.ActualResult(0m, 0m, 0m, "round", [1], [0m], 3),
        };
        var mismatches = new List<GrainMismatch>();

        Comparator.CompareDiscardsGrain(fixture, actuals, mismatches);

        mismatches.Should().BeEmpty("the zero-only pilot carries no discard assertion");
    }

    [Fact]
    public void AvailableButUnpopulatedPreDrop_Throws()
    {
        var action = () => Comparator.ComparePreDropTotalsGrain(
            Fixture(preDrop: []), BaseActuals(), []);

        action.Should().Throw<InvalidOperationException>(
            "an available field with no expectations is a missing oracle, never a silent pass");
    }

    [Fact]
    public void AvailableButUnpopulatedPenalty_Throws()
    {
        var action = () => Comparator.ComparePenaltyDeductionGrain(
            Fixture(penalties: []), BaseActuals(), []);

        action.Should().Throw<InvalidOperationException>(
            "an available field with no expectations is a missing oracle, never a silent pass");
    }

    [Fact]
    public void AvailableButUnpopulatedDiscards_Throws()
    {
        var action = () => Comparator.CompareDiscardsGrain(
            Fixture(discards: []), BaseActuals(), []);

        action.Should().Throw<InvalidOperationException>(
            "an available field with no expectations is a missing oracle, never a silent pass");
    }

    [Fact]
    public void MisalignedDiscardArrays_Throw()
    {
        var fixture = Fixture(discards: [new(12, "task", [9], [0m, 1m])]);
        var action = () => Comparator.CompareDiscardsGrain(fixture, BaseActuals(), []);

        action.Should().Throw<InvalidOperationException>(
            "parallel dropped-round/value arrays must align");
    }

    [Fact]
    public void MissingAvailability_Null_Throws()
    {
        var fixture = Fixture(withAvailability: false);
        var action = () => Comparator.RequireCompleteOracleContract(fixture);

        action.Should().Throw<InvalidOperationException>(
            "omission must not mean zero — availability is always explicit");
    }

    [Fact]
    public void MissingAvailability_Empty_Throws()
    {
        var fixture = Fixture(availability: []);
        var action = () => Comparator.RequireCompleteOracleContract(fixture);

        action.Should().Throw<InvalidOperationException>(
            "omission must not mean zero — availability is always explicit");
    }

    [Fact]
    public void MissingScope_Throws()
    {
        var fixture = Fixture(withScope: false);
        var action = () => Comparator.RequireCompleteOracleContract(fixture);

        action.Should().Throw<InvalidOperationException>(
            "every fixture declares its snapshot scope");
    }

    [Fact]
    public void UnknownAvailabilityStatus_Throws()
    {
        var availability = AllAvailable();
        availability[0] = new ResultFieldAvailability("total", "sometimes", "typo");
        var fixture = Fixture(availability: availability);
        var action = () => Comparator.RequireCompleteOracleContract(fixture);

        action.Should().Throw<InvalidOperationException>(
            "a typo must not silently narrow the oracle");
    }

    [Fact]
    public void MissingFieldDeclaration_Throws()
    {
        var availability = AllAvailable().Where(e => e.Field != "discards").ToList();
        var fixture = Fixture(availability: availability);
        var action = () => Comparator.RequireCompleteOracleContract(fixture);

        action.Should().Throw<InvalidOperationException>(
            "every field must be declared");
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("inapplicable")]
    public void NonAvailableStatuses_PassTheGate(string status)
    {
        // The gate accepts unavailable/inapplicable with a reason — the
        // grains then skip the field (never asserted, never zero-filled).
        // The corpus evidences every field, so no real fixture exercises
        // this arm; the mechanism is pinned here, not in real data.
        var availability = AllAvailable();
        availability[2] = new ResultFieldAvailability("penaltyDeduction", status, "synthetic: no evidence");
        var fixture = Fixture(availability: availability);

        var map = Comparator.RequireCompleteOracleContract(fixture);

        map["penaltyDeduction"].Should().Be(status);
    }
}
