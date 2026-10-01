// gs_03_exact-divergence-contracts.md verification — the exact-divergence
// contract for both harnesses. Pure unit tests: they drive
// Comparator.BuildReport/LedgerWitnesses/CheckLedgerScope (parity) and
// ParallelRunComparator.MatchDifferenceSets plus the ledger validation
// (parallel) directly against synthetic difference sets, so no store, HTTP
// or replay is involved. The real ledgers are pinned through the acceptance
// scenarios beside these cases.
//
// Invariant under test: only the declared difference set satisfies the
// contract — adding, removing, relocating or altering one difference fails.
// Perturbations sweep identity, grain, field (value side) and value around
// small valid sets (xunit theories over varied pilots/values, the
// SnapshotResultParityTests precedent — no property library is referenced).
// Exclusion and unsupported-comparison validation are tested separately
// from numeric matching.

using System.Text.Json;
using AwesomeAssertions;
using Xunit;
using Soarscore.Acceptance.Tests.Support.Gliderscore;

namespace Soarscore.Acceptance.Tests;

public sealed class ExactDivergenceContractTests
{
    // ------------------------------------------------------- parity: numeric

    // The valid declared set: two pinned numeric differences.
    private static List<DivergenceEntry> TwoCellLedger() =>
    [
        Numeric("raw", 1, 1, 7, ours: 505.0m, expected: 495.0m),
        Numeric("normalised", 2, 1, 9, ours: 800.5m, expected: 800.4m),
    ];

    private static List<GrainMismatch> TwoCellComputed() =>
    [
        new GrainMismatch("raw", 7, 1, 1, 505.0m, 495.0m, "exact-decimal mismatch."),
        new GrainMismatch("normalised", 9, 2, 1, 800.5m, 800.4m, "exact-decimal mismatch."),
    ];

    [Fact]
    public void DeclaredSet_ComparesClean()
    {
        var report = Build(TwoCellLedger(), TwoCellComputed());

        report.AllGrainsExact.Should().BeTrue("only the declared difference set is present");
        report.UnwitnessedLedgerEntries.Should().BeEmpty("both entries witness their pins");
        report.LedgerScopeBreaks.Should().BeEmpty("no documentary entries, no scope breaks");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(99)]
    public void RelocatedMismatch_Fails_EvenWithSameValues(long otherPilot)
    {
        // The same values at a different cell: the moved cell is untriaged
        // and the declared cell is stale.
        var computed = TwoCellComputed();
        computed[0] = computed[0] with { PilotNo = otherPilot };

        var report = Build(TwoCellLedger(), computed);

        report.AllGrainsExact.Should().BeFalse("a mismatch moved to another cell fails");
        report.RawMismatches.Should().ContainSingle()
            .Which.PilotNo.Should().Be(otherPilot);
        report.UnwitnessedLedgerEntries.Should().ContainSingle(
            "the declared cell no longer fires — a stale expectation fails in every mode");
    }

    [Theory]
    [InlineData(505.1)]
    [InlineData(500.0)]
    public void AlteredOursValue_Fails_AtTheSameKey(decimal otherOurs)
    {
        var computed = TwoCellComputed();
        computed[0] = computed[0] with { Ours = otherOurs };

        var report = Build(TwoCellLedger(), computed);

        report.AllGrainsExact.Should().BeFalse("an unexpected value at the same key fails");
        report.UnwitnessedLedgerEntries.Should().ContainSingle(
            "the declared pin no longer fires — a changed value fails its stale expectation");
    }

    [Theory]
    [InlineData(495.1)]
    [InlineData(490.0)]
    public void AlteredExpectedDelta_Fails_AtTheSameKey(decimal otherExpected)
    {
        var computed = TwoCellComputed();
        computed[0] = computed[0] with { Expected = otherExpected };

        var report = Build(TwoCellLedger(), computed);

        report.AllGrainsExact.Should().BeFalse("an unexpected delta at the same key fails");
        report.UnwitnessedLedgerEntries.Should().ContainSingle(
            "the declared pin no longer fires — a changed delta fails its stale expectation");
    }

    [Fact]
    public void AlteredGrain_Fails_AtTheSameCell()
    {
        var computed = TwoCellComputed();
        computed[0] = computed[0] with { Grain = "normalised" };

        var report = Build(TwoCellLedger(), computed);

        report.AllGrainsExact.Should().BeFalse("a difference at the wrong grain fails");
        report.UnwitnessedLedgerEntries.Should().ContainSingle(
            "the raw pin fires nowhere — grain is part of the declared identity");
    }

    [Fact]
    public void AddedDifference_Fails_WithoutDisturbingWitnesses()
    {
        var computed = TwoCellComputed();
        computed.Add(new GrainMismatch("raw", 3, 1, 1, 10.0m, 12.0m, "exact-decimal mismatch."));

        var report = Build(TwoCellLedger(), computed);

        report.AllGrainsExact.Should().BeFalse("adding one difference fails the contract");
        report.RawMismatches.Should().ContainSingle().Which.PilotNo.Should().Be(3);
        report.UnwitnessedLedgerEntries.Should().BeEmpty("both declared pins still fire");
    }

    [Fact]
    public void ResolvedDifference_FailsItsStaleExpectation_InEveryMode()
    {
        // No computed mismatches at all: the remainder is empty, but the
        // stale pins fail — BuildReport is the mode-independent path both
        // ledger modes share, so this failure fires in strict AND ledgered.
        var report = Build(TwoCellLedger(), []);

        report.AllGrainsExact.Should().BeTrue("nothing computed remains");
        report.UnwitnessedLedgerEntries.Should().HaveCount(2,
            "a resolved numerical difference fails its stale expectation in every mode");
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("NUMERIC ")]
    public void UnknownKindToken_Fails(string kind)
    {
        var ledger = TwoCellLedger();
        ledger[0] = ledger[0] with { Kind = kind };

        var act = () => Build(ledger, TwoCellComputed());

        act.Should().Throw<InvalidOperationException>(
            "an unknown kind token must fail rather than silently change the claim");
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("Permanent ")]
    public void UnknownDispositionToken_Fails(string disposition)
    {
        var ledger = TwoCellLedger();
        ledger[0] = ledger[0] with { Disposition = disposition };

        var act = () => Build(ledger, TwoCellComputed());

        act.Should().Throw<InvalidOperationException>(
            "an unknown disposition token must fail rather than silently demote the entry");
    }

    [Fact]
    public void NumericEntry_WithoutPins_Fails()
    {
        var ledger = TwoCellLedger();
        ledger[0] = ledger[0] with { Ours = null, Expected = null };

        var act = () => Build(ledger, TwoCellComputed());

        act.Should().Throw<InvalidOperationException>(
            "a numeric claim without pinned values cannot be matched bidirectionally");
    }

    // ------------------------------------- parity: excluded/synthetic scope

    [Fact]
    public void ExcludedCell_WitnessesCoverageGap_ButNothingNumeric()
    {
        var ledger = new List<DivergenceEntry>
        {
            Documentary("raw", 1, 5, PilotStar(), "excludedOracleCell"),
        };
        var gap = new GrainMismatch("raw", 7, 1, 5, null, null,
            "oracle cell 1/1/5/0/7 was never compared — replay produced no slot for it.");

        var report = Build(ledger, [gap]);

        report.AllGrainsExact.Should().BeTrue("the declared exclusion witnesses its coverage gap");
        report.UnwitnessedLedgerEntries.Should().BeEmpty("the gap is the witnessed shape");

        var valueMismatch = new GrainMismatch("raw", 7, 1, 5, 100.0m, 0.0m, "exact-decimal mismatch.");
        var red = Build(ledger, [valueMismatch]);

        red.AllGrainsExact.Should().BeFalse(
            "a value mismatch at an excluded identity is untriaged — documentary scope excuses nothing numeric");
        red.UnwitnessedLedgerEntries.Should().ContainSingle(
            "the coverage gap the entry declares never appeared");
    }

    [Fact]
    public void ExcludedCell_ResolvedNowCompared_FailsStale()
    {
        var ledger = new List<DivergenceEntry>
        {
            Documentary("raw", 1, 5, PilotStar(), "excludedOracleCell"),
        };

        var report = Build(ledger, []);

        report.UnwitnessedLedgerEntries.Should().ContainSingle(
            "the replay now compares the cell — the exclusion is discharged and must be removed");
    }

    [Fact]
    public void SyntheticSlot_WitnessesOrphan_ButNothingValued()
    {
        var ledger = new List<DivergenceEntry>
        {
            Documentary("raw", 9, 3, Pilot(89), "syntheticSlot"),
        };
        var orphan = new GrainMismatch("raw", 89, 9, 3, 0.0m, null,
            "no oracle cell for this (round, group, pilot).");

        var report = Build(ledger, [orphan]);

        report.AllGrainsExact.Should().BeTrue("the declared synthetic slot witnesses its orphan shape");
        report.UnwitnessedLedgerEntries.Should().BeEmpty("the orphan is the witnessed shape");
    }

    [Fact]
    public void SyntheticSlot_WithOracleCounterpart_FailsStale()
    {
        // The oracle now carries the cell and the replay compares it exact:
        // no orphan appears, so the synthetic claim is stale.
        var ledger = new List<DivergenceEntry>
        {
            Documentary("raw", 9, 3, Pilot(89), "syntheticSlot"),
        };

        var report = Build(ledger, []);

        report.UnwitnessedLedgerEntries.Should().ContainSingle(
            "no our-only slot exists — the synthetic claim is discharged and must be removed");
    }

    [Fact]
    public void WindowViolation_IsNeverDocumentaryScope()
    {
        // Null-valued but a different shape: an excluded entry must not
        // excuse a scored-window violation at its identity.
        var ledger = new List<DivergenceEntry>
        {
            Documentary("raw", 9, 3, Pilot(89), "excludedOracleCell"),
        };
        var violation = new GrainMismatch("raw", 89, 9, 3, null, null,
            "cell 5/9/3/0/89 lies outside the declared scored window R1–R8 — no comparison is claimed.");

        var report = Build(ledger, [violation]);

        report.AllGrainsExact.Should().BeFalse("a window violation is not a coverage gap");
        report.UnwitnessedLedgerEntries.Should().ContainSingle(
            "the declared coverage gap never appeared");
    }

    // ----------------------------------- parity: unsupported documentation

    [Fact]
    public void UnsupportedComparison_DisclosesUnrunTeamWork_WithoutNumericWitness()
    {
        var ledger = new List<DivergenceEntry> { UnsupportedTeamEntry() };

        var report = Build(ledger, [], useTeams: true, nbrForTeamScore: 2, teamsCompared: 0);

        report.AllGrainsExact.Should().BeTrue("the entry excuses no grain and witnesses nothing");
        report.UnwitnessedLedgerEntries.Should().BeEmpty(
            "an applicable documentary entry requires no computed mismatch");
        report.LedgerScopeBreaks.Should().BeEmpty(
            "the declared method (NbrForTeamScore=2) genuinely does not run");
    }

    [Fact]
    public void UnsupportedComparison_NeverCoversAnyComputedMismatch()
    {
        var entry = UnsupportedTeamEntry();
        var mismatch = new GrainMismatch("ranking", 7, 0, 0, 2, 1, "oracle rank '1' but we placed 2.");

        Comparator.LedgerWitnesses(entry, mismatch).Should().BeFalse(
            "the documentary entry discloses the unrun comparison without pretending to witness a mismatch");
        entry.CoversMismatch(mismatch).Should().BeFalse(
            "subtracting never reaches an unsupported comparison either");
    }

    [Theory]
    [InlineData(3, 0, "the MVP's own method now runs")]
    [InlineData(2, 4, "team standings were compared anyway")]
    public void UnsupportedComparison_StaleApplicability_Fails(int nbr, int teamsCompared, string because)
    {
        var ledger = new List<DivergenceEntry> { UnsupportedTeamEntry() };

        var report = Build(ledger, [], useTeams: true, nbrForTeamScore: nbr, teamsCompared: teamsCompared);

        report.LedgerScopeBreaks.Should().ContainSingle($"{because} — the documentary claim is stale");
    }

    [Fact]
    public void UnsupportedComparison_InertTeamKnob_Fails()
    {
        var ledger = new List<DivergenceEntry> { UnsupportedTeamEntry() };

        var report = Build(ledger, [], useTeams: false, nbrForTeamScore: 2, teamsCompared: 0);

        report.LedgerScopeBreaks.Should().ContainSingle(
            "an inert NbrForTeamScore under UseTeams=false is not a divergence — the entry never applied");
    }

    // ------------------------------------------------- parallel: exact sets

    private static ParallelRunLedger ParallelLedger(params ParallelRunDifferenceEntry[] entries) =>
        new(
            new ParallelRunPair("synthetic", "synthetic-seed"),
            new ParallelRunSeedClass("Synthetic", "v1"),
            new ParallelRunProvenance([], [], []),
            entries);

    private static ParallelRunDifferenceEntry RawClass(params ParallelRunExpectedCell[] cells) =>
        new(1, "raw", null, null, PilotStar(), "synthetic raw class", "synthetic citation",
            "permanent", cells);

    private static ParallelRunDifferenceEntry RankingPilot(long pilot, params ParallelRunExpectedCell[] cells) =>
        new(1, "ranking", null, null, Pilot(pilot), "synthetic placing split", "synthetic citation",
            "permanent", cells);

    private static List<GrainMismatch> ParallelComputed() =>
    [
        new GrainMismatch("raw", 7, 1, 1, 120.0m, 160.0m, "exact-decimal mismatch."),
        new GrainMismatch("raw", 9, 2, 1, 240.0m, 275.0m, "exact-decimal mismatch."),
        new GrainMismatch("ranking", 7, 0, 0, 13, 12, "oracle rank '12' but we placed 13."),
    ];

    private static ParallelRunLedger ParallelTwoEntryLedger() => ParallelLedger(
        RawClass(
            new ParallelRunExpectedCell(1, 1, 7, 160.0m, 120.0m),
            new ParallelRunExpectedCell(2, 1, 9, 275.0m, 240.0m)),
        RankingPilot(7, new ParallelRunExpectedCell(0, 0, 7, 12, 13)));

    [Fact]
    public void ParallelDeclaredSet_MatchesExactly()
    {
        var (untriaged, missing) = ParallelRunComparator.MatchDifferenceSets(
            ParallelComputed(), ParallelTwoEntryLedger());

        untriaged.Should().BeEmpty("every computed difference equals a declared cell");
        missing.Should().BeEmpty("every declared cell is witnessed");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(700)]
    public void ParallelRelocatedCell_Fails_EvenWithSameValues(long otherPilot)
    {
        var computed = ParallelComputed();
        computed[0] = computed[0] with { PilotNo = otherPilot };

        var (untriaged, missing) = ParallelRunComparator.MatchDifferenceSets(
            computed, ParallelTwoEntryLedger());

        untriaged.Should().ContainSingle("the moved cell is outside the declared set")
            .Which.PilotNo.Should().Be(otherPilot);
        missing.Should().ContainSingle("the declared cell no longer fires — stale in every mode");
    }

    [Fact]
    public void ParallelAlteredValue_Fails_AtTheSameKey()
    {
        var computed = ParallelComputed();
        computed[0] = computed[0] with { Ours = 121.0m };

        var (untriaged, missing) = ParallelRunComparator.MatchDifferenceSets(
            computed, ParallelTwoEntryLedger());

        untriaged.Should().ContainSingle("an unexpected value at the same key is untriaged");
        missing.Should().ContainSingle("the declared pin is now stale");
    }

    [Fact]
    public void ParallelAddedDifference_Fails_WithoutDisturbingTheRest()
    {
        var computed = ParallelComputed();
        computed.Add(new GrainMismatch("raw", 3, 1, 1, 10.0m, 12.0m, "exact-decimal mismatch."));

        var (untriaged, missing) = ParallelRunComparator.MatchDifferenceSets(
            computed, ParallelTwoEntryLedger());

        untriaged.Should().ContainSingle().Which.PilotNo.Should().Be(3);
        missing.Should().BeEmpty("every declared cell still fires");
    }

    [Fact]
    public void ParallelRemovedDifference_FailsMissing()
    {
        var computed = ParallelComputed();
        computed.RemoveAt(2);

        var (untriaged, missing) = ParallelRunComparator.MatchDifferenceSets(
            computed, ParallelTwoEntryLedger());

        untriaged.Should().BeEmpty("nothing computed is outside the set");
        missing.Should().ContainSingle("a resolved difference fails its stale expectation");
    }

    [Fact]
    public void ParallelMembershipLines_PinNullSides()
    {
        // A "=n" tie-group membership line carries null on the excluding
        // side — the declared cell pins null exactly there, and a valued
        // impostor at the same identity fails.
        var ledger = ParallelLedger(RankingPilot(7,
            new ParallelRunExpectedCell(0, 0, 7, 12, null)));
        var line = new GrainMismatch("ranking", 7, 0, 0, null, 12,
            "'=12' tie-group membership differs.");

        var (untriaged, missing) = ParallelRunComparator.MatchDifferenceSets([line], ledger);

        untriaged.Should().BeEmpty("null pins match null sides exactly");
        missing.Should().BeEmpty("the membership line is witnessed");

        var impostor = line with { Ours = 12 };
        var (untriaged2, missing2) = ParallelRunComparator.MatchDifferenceSets([impostor], ledger);

        untriaged2.Should().ContainSingle("a valued line is not the declared null-side line");
        missing2.Should().ContainSingle("the declared null-side line never appeared");
    }

    [Fact]
    public void ParallelEntry_WithoutCells_FailsValidation()
    {
        var ledger = ParallelLedger(RawClass());

        var act = () => ParallelLedgerEntries(ledger);

        act.Should().Throw<InvalidOperationException>(
            "an entry declaring no cells is an authoring bug, never a wildcard");
    }

    [Fact]
    public void ParallelCell_OutsideEntryScope_FailsValidation()
    {
        var ledger = ParallelLedger(RankingPilot(7,
            new ParallelRunExpectedCell(0, 0, 8, 12, 13)));

        var act = () => ParallelLedgerEntries(ledger);

        act.Should().Throw<InvalidOperationException>(
            "a declared cell outside its entry's pilot scope is a typo");
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("Permanent ")]
    public void ParallelUnknownDisposition_FailsValidation(string disposition)
    {
        var ledger = ParallelLedger(new ParallelRunDifferenceEntry(
            1, "raw", null, null, PilotStar(), "synthetic raw class", "synthetic citation",
            disposition, [new ParallelRunExpectedCell(1, 1, 7, 160.0m, 120.0m)]));

        var act = () => ParallelLedgerEntries(ledger);

        act.Should().Throw<InvalidOperationException>(
            "an unknown disposition token must fail rather than silently demote the entry");
    }

    // -------------------------------------------------------------- plumbing

    private static DivergenceEntry Numeric(string grain, int round, int group, long pilot, decimal ours, decimal expected) =>
        new(grain, round, group, Pilot(pilot), "synthetic numeric divergence",
            Disposition: "permanent", Kind: "numeric", Ours: ours, Expected: expected,
            Evidence: "synthetic evidence");

    private static DivergenceEntry Documentary(
        string grain, int? round, int? group, JsonElement? pilotNo, string kind) =>
        new(grain, round, group, pilotNo, $"synthetic {kind} scope",
            Disposition: "permanent", Kind: kind, Evidence: "synthetic evidence");

    private static DivergenceEntry UnsupportedTeamEntry() =>
        new("team", null, null, null, "synthetic T1 unsupported team method",
            Disposition: "permanent", Kind: "unsupportedComparison",
            Evidence: "synthetic triage NbrForTeamScore=2");

    private static JsonElement Pilot(long pilotNo) =>
        JsonSerializer.SerializeToElement(pilotNo);

    private static JsonElement PilotStar() =>
        JsonSerializer.SerializeToElement("*");

    private static ComparisonReport Build(
        IReadOnlyList<DivergenceEntry> divergences,
        IReadOnlyList<GrainMismatch> computed,
        bool useTeams = false,
        int? nbrForTeamScore = null,
        int teamsCompared = 0)
    {
        var raw = computed.Where(m => m.Grain == "raw").ToList();
        var normalised = computed.Where(m => m.Grain == "normalised").ToList();
        var ranking = computed.Where(m => m.Grain is "ranking" or "total" or "preDrop" or "penalty" or "discard").ToList();

        return Comparator.BuildReport(
            SyntheticFixture(divergences, useTeams, nbrForTeamScore),
            raw, normalised, ranking, [], [],
            rawCellsCompared: raw.Count,
            normalisedCellsCompared: normalised.Count,
            rankingPilotsCompared: 0,
            oracleCells: computed.Count,
            teamsCompared: teamsCompared);
    }

    private static GliderscoreFixture SyntheticFixture(
        IReadOnlyList<DivergenceEntry> divergences, bool useTeams, int? nbrForTeamScore) => new(
        Slug: "synthetic-exact-divergence-contract",
        Directory: "",
        Competition: new CompetitionFile(
            new CompetitionIdentity(0, "synthetic", "F3J", "2026-01-01"),
            new CompetitionScoring(1, 0, 0),
            new FamilyRowsTable(),
            Triage: new TriageTable(useTeams, null, nbrForTeamScore)),
        Entries: new EntriesFile(new CompPilotsTable([]), new PilotsTable([])),
        ScoresRaw: new ScoresRawFile([]),
        ExpectedScores: new ExpectedScoresFile(new Dictionary<string, ExpectedCell>()),
        ExpectedResult: new ExpectedResultFile([]),
        Divergences: divergences,
        Definition: null!);

    private static void ParallelLedgerEntries(ParallelRunLedger ledger)
    {
        foreach (var entry in ledger.TriagedDifferences)
        {
            entry.ValidateCells();
        }
    }
}
