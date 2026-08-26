// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-5 — step
// definitions for the two self-check scenarios that do not extend the four
// fixture scenarios: replay determinism (check 1) and ledger strictness
// (check 3). Conservation (check 2) rides every replay through ReplaySteps'
// conservation Then step, because the comparator computes it during the same
// pass it already makes.
//
// Determinism replays one fixture TWICE within a single scenario against the
// shared run store and asserts both replays issued identical command counts —
// a pure function of the fixture data, so any difference means state bled
// across replays. Ledger strictness needs no store at all: it drives
// Comparator.BuildReport directly against a synthetic in-memory mismatch.

using System.Text.Json;
using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Acceptance.Tests.Support.Gliderscore;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class HarnessSelfCheckSteps
{
    // ------------------------------------------------------------ check 1

    private GliderscoreFixture? _fixture;
    private ReplayOutcome _firstOutcome = null!;
    private ReplayOutcome _secondOutcome = null!;
    private ComparisonReport _firstReport = null!;
    private ComparisonReport _secondReport = null!;

    [When(@"^the harness replays the GliderScore fixture ""(.+)"" twice within this scenario$")]
    public async Task WhenTheHarnessReplaysTheFixtureTwiceWithinThisScenario(string slug)
    {
        _fixture = FixtureLoader.Load(slug);

        var firstDriver = new ReplayDriver(AcceptanceFixture.Client);
        _firstOutcome = await firstDriver.ReplayAsync(_fixture);
        _firstReport = await Comparator.CompareAsync(
            _fixture, _firstOutcome, AcceptanceFixture.EventStore, AcceptanceFixture.Client);

        var secondDriver = new ReplayDriver(AcceptanceFixture.Client);
        _secondOutcome = await secondDriver.ReplayAsync(_fixture);
        _secondReport = await Comparator.CompareAsync(
            _fixture, _secondOutcome, AcceptanceFixture.EventStore, AcceptanceFixture.Client);
    }

    [Then(@"^both replays ran against fresh competitions in the shared store$")]
    public void ThenBothReplaysRanAgainstFreshCompetitions() =>
        _firstOutcome.CompetitionId.Should().NotBe(_secondOutcome.CompetitionId,
            "the second replay must be a fresh competition in the same shared store, not a re-run of the first");

    [Then(@"^both replays issued identical command counts$")]
    public void ThenBothReplaysIssuedIdenticalCommandCounts()
    {
        _firstOutcome.CommandsIssued.Should().BePositive(
            "a replay that issues no commands has not replayed anything");

        _firstOutcome.CommandsIssued.Should().Be(
            _secondOutcome.CommandsIssued,
            $"two replays of {_fixture!.Slug} in one run must issue identical command sequences; "
            + "a difference means state bled across replays through the shared store");
    }

    [Then(@"^both replays compare exact at all three grains modulo the fixture ledger$")]
    public void ThenBothReplaysCompareExactAtAllThreeGrains()
    {
        _firstReport.AllGrainsExact.Should().BeTrue(
            $"the FIRST replay must compare exact at all three grains modulo the ledger."
            + $"{Environment.NewLine}{_firstReport.DiffTable()}");

        _secondReport.AllGrainsExact.Should().BeTrue(
            $"the SECOND replay must compare exact at all three grains modulo the ledger."
            + $"{Environment.NewLine}{_secondReport.DiffTable()}");
    }

    // ------------------------------------------------------------ check 3

    private long _pilotNo;
    private int _roundNo;
    private int _groupNo;
    private GrainMismatch _seededMismatch = null!;
    private ComparisonReport _ledgerReport = null!;

    [Given(@"^a synthetic comparison carrying one normalised-grain mismatch for pilot (\d+) in round (\d+) group (\d+), ours (.+) versus oracle (.+)$")]
    public void GivenASyntheticComparisonCarryingOneNormalisedGrainMismatch(
        long pilotNo, int roundNo, int groupNo, string ours, string expected)
    {
        _pilotNo = pilotNo;
        _roundNo = roundNo;
        _groupNo = groupNo;
        _seededMismatch = new GrainMismatch(
            "normalised", pilotNo, roundNo, groupNo,
            decimal.Parse(ours, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            "seeded self-test mismatch");
    }

    [When(@"^the comparator subtracts an empty ledger$")]
    public void WhenTheComparatorSubtractsAnEmptyLedger() =>
        BuildSyntheticReport([]);

    [When(@"^the comparator subtracts a ledger entry covering exactly that cell$")]
    public void WhenTheComparatorSubtractsALedgerEntryCoveringExactlyThatCell() =>
        BuildSyntheticReport([ExactCoverEntry(_pilotNo)]);

    [When(@"^the comparator subtracts a ledger entry naming a different pilot instead$")]
    public void WhenTheComparatorSubtractsALedgerEntryNamingADifferentPilotInstead() =>
        BuildSyntheticReport([ExactCoverEntry(_pilotNo + 1)]);

    [Then(@"^the report fails with a diff table naming the seeded mismatch$")]
    public void ThenTheReportFailsWithADiffTableNamingTheSeededMismatch()
    {
        var report = SyntheticReport();

        report.AllGrainsExact.Should().BeFalse("an unledgered or unabsorbed mismatch must fail the comparison");

        var table = report.DiffTable();
        table.Should().StartWith("GliderScore replay comparison failed — 1 unledgered mismatch(es):",
            "exactly the seeded mismatch survives subtraction");
        table.Should().Contain("normalised").And.Contain(
            _seededMismatch.Ours!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .And.Contain(_seededMismatch.Expected!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .And.Contain(_seededMismatch.Delta);
    }

    [Then(@"^the report compares exact$")]
    public void ThenTheReportComparesExact()
    {
        SyntheticReport().AllGrainsExact.Should().BeTrue(
            "the ledger entry covers exactly the seeded mismatch, so nothing may remain"
            + $"{Environment.NewLine}{SyntheticReport().DiffTable()}");
    }

    // -------------------------------------------------------------- plumbing

    private void BuildSyntheticReport(IReadOnlyList<DivergenceEntry> divergences) =>
        _ledgerReport = Comparator.BuildReport(
            SyntheticFixture(divergences),
            [],
            [_seededMismatch],
            [],
            [],
            rawCellsCompared: 1,
            normalisedCellsCompared: 1,
            rankingPilotsCompared: 0,
            oracleCells: 1);

    private ComparisonReport SyntheticReport() =>
        _ledgerReport ?? throw new InvalidOperationException(
            "No synthetic report built yet — a When step must precede this Then step.");

    /// <summary>
    /// The ledger-entry shape of divergences.json naming exactly the seeded
    /// cell (grain normalised, its round/group, its pilot).
    /// </summary>
    private DivergenceEntry ExactCoverEntry(long pilotNo) => new(
        "normalised",
        _roundNo,
        _groupNo,
        JsonSerializer.SerializeToElement(pilotNo),
        "self-test: covers exactly the seeded synthetic mismatch");

    /// <summary>
    /// A minimal in-memory fixture — only its divergence ledger is read on the
    /// BuildReport path, so every other file stands in as an empty shell.
    /// </summary>
    private static GliderscoreFixture SyntheticFixture(IReadOnlyList<DivergenceEntry> divergences) => new(
        Slug: "synthetic-ledger-self-test",
        Directory: "",
        Competition: new CompetitionFile(
            new CompetitionIdentity(0, "synthetic", "F3J", "2026-01-01"),
            new CompetitionScoring(1, 0, 0),
            new FamilyRowsTable()),
        Entries: new EntriesFile(new CompPilotsTable([]), new PilotsTable([])),
        ScoresRaw: new ScoresRawFile([]),
        ExpectedScores: new ExpectedScoresFile(new Dictionary<string, ExpectedCell>()),
        ExpectedResult: new ExpectedResultFile([]),
        Divergences: divergences,
        Definition: null!); // never read by the BuildReport path
}
