// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-1 — step
// definitions for Features/ReplayingAGliderscoreFixture.feature. The When step
// replays the whole fixture through the public command surface (ReplayDriver)
// and immediately runs the three-grain exact comparison (Comparator); the Then
// steps each assert one grain's remainder is empty, so a failure names its
// grain — and every failure carries the story's ONE diff table
// (pilot × round × grain, ours / expected / delta).
//
// One instance per scenario (Reqnroll's default binding lifetime), so the
// fields below are scenario-scoped; AcceptanceFixture's HttpClient/IEventStore
// are shared run-wide exactly as in every sibling feature.

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Acceptance.Tests.Support.Gliderscore;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class ReplaySteps
{
    private GliderscoreFixture? _fixture;
    private ComparisonReport _report = null!;

    // ----------------------------------------------------------------- Given

    [Given(@"^the fixture corpus manifest$")]
    public void GivenTheFixtureCorpusManifest()
    {
        // Proves the loader can find tests/GliderscoreFixtures and read the
        // index.md tokenisation contract (a slug whose line contains "skipped"
        // is skip-listed) before the replay depends on either.
        FixtureLoader.ActiveSlugs().Should().Contain("ales-sample-comp");
    }

    // ------------------------------------------------------------------ When

    [When(@"^the harness replays the GliderScore fixture ""(.+)""$")]
    public async Task WhenTheHarnessReplaysTheGliderScoreFixture(string slug)
    {
        _fixture = FixtureLoader.Load(slug);

        var outcome = await new ReplayDriver(AcceptanceFixture.Client).ReplayAsync(_fixture);

        _report = await Comparator.CompareAsync(
            _fixture, outcome, AcceptanceFixture.EventStore, AcceptanceFixture.Client);
    }

    // ------------------------------------------------------------------ Then

    [Then(@"^every raw flight score matches the fixture oracle exactly$")]
    public void ThenEveryRawFlightScoreMatchesExactly()
    {
        var report = Report();

        report.RawMismatches.Should().BeEmpty(
            $"grain 1 (raw, pre-normalisation vs GS RawScore) must match with no tolerance; "
            + $"compared {report.RawCellsCompared} of {report.OracleCells} oracle cells."
            + $"{Environment.NewLine}{report.DiffTable()}");
    }

    [Then(@"^every normalised round score matches the fixture oracle exactly$")]
    public void ThenEveryNormalisedRoundScoreMatchesExactly()
    {
        var report = Report();

        report.NormalisedMismatches.Should().BeEmpty(
            $"grain 2 (normalised, /task-round-result vs GS NormalisedScore) must match with no tolerance; "
            + $"compared {report.NormalisedCellsCompared} of {report.OracleCells} oracle cells."
            + $"{Environment.NewLine}{report.DiffTable()}");
    }

    [Then(@"^the final ranking matches the fixture oracle exactly$")]
    public void ThenTheFinalRankingMatchesExactly()
    {
        var report = Report();

        report.RankingMismatches.Should().BeEmpty(
            $"grain 3 (ranking, /competition-result placings vs the oracle rank strings) must match "
            + $"for all {report.RankingPilotsCompared} pilots."
            + $"{Environment.NewLine}{report.DiffTable()}");
    }

    [Then(@"^the fixture carries no ledgered divergences$")]
    public void ThenTheFixtureCarriesNoLedgeredDivergences()
    {
        // D6: the ledger starts EMPTY and an entry lands only after human
        // triage. For this steel thread nothing may be excused — if this fails,
        // someone ledgered a real mismatch to get green.
        Fixture!.Divergences.Should().BeEmpty(
            $"{Fixture.Slug} is expected to match EXACTLY everywhere; "
            + "a populated ledger means a divergence was accepted without triage.");
    }

    [Then(@"^every ledgered divergence cites an arithmetic-story divergence ID$")]
    public void ThenEveryLedgeredDivergenceCitesAnArithmeticStoryId()
    {
        // D6's ledger schema: every reason cites resolve-gliderscore-scoring-
        // arithmetic.md's divergence IDs (D1/D3/D5/D6). A reason without one is
        // an untriaged excuse, not a documented divergence.
        var unattributed = Fixture!.Divergences
            .Where(d => !System.Text.RegularExpressions.Regex.IsMatch(d.Reason, @"\bD[1-6]\b"))
            .ToList();

        unattributed.Should().BeEmpty(
            $"every ledgered divergence must cite an arithmetic-story divergence ID; "
            + $"{Fixture.Slug} carries {unattributed.Count} without one.");
    }

    [Then(@"^the fixture ledger records exactly (\d+) accepted divergences$")]
    public void ThenTheFixtureLedgerRecordsExactlyAcceptedDivergences(int expected)
    {
        // WI-3: f3j-international's phantom R1 group 5 (D5) leaves five oracle
        // cells per grain that no replayed slot can produce — the ledger names
        // them so the exact-grain assertions above prove everything ELSE is
        // exact. This step pins the ledger to its reviewed size, so entries
        // cannot silently grow.
        Fixture!.Divergences.Should().HaveCount(expected,
            $"{Fixture.Slug}'s committed ledger was triaged at {expected} entries; "
            + "a different count means the fixture data or the ledger changed without re-triage.");
    }

    private ComparisonReport Report() =>
        _report ?? throw new InvalidOperationException(
            "No comparison has run yet — the replay When step must precede the comparison Then steps.");

    private GliderscoreFixture Fixture =>
        _fixture ?? throw new InvalidOperationException(
            "No fixture loaded yet — the replay When step must precede the comparison Then steps.");
}
