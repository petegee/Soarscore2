// kanban/in-progress/seed-definition-parallel-run.md WI-3 — step definitions
// for Features/ParallelRunningAGliderscoreFixture.feature. The When step
// parallel-runs the fixture through the public command surface under a SEED
// class (ReplayDriver's parallel-run mode: the seed definition is published
// instead of the fixture's GS-mirrored twin, its parameters bound from the
// comp's actual config before the draw) and immediately runs the parallel-run
// compare (ParallelRunComparator): the difference set between the seed-run
// and GS's oracle, judged by set-equality against the pair's ledger — no
// ledger subtraction, no tolerance. The Then steps each assert one leg of the
// claim — the verdict (exactness discipline + escalation law), the product
// grain (final placings), and the ledger's own triage discipline — and every
// failure carries the report's full Render() so a kind-3 (engine divergence)
// suspicion surfaces with its evidence instead of anyone editing the ledger
// to fit.
//
// One instance per scenario (Reqnroll's default binding lifetime), so the
// fields below are scenario-scoped; AcceptanceFixture's HttpClient/IEventStore
// are shared run-wide exactly as in every sibling feature. Step BINDINGS are
// global, so the Given "the fixture corpus manifest" is ReplaySteps' shared
// verbatim — this class adds only the parallel-run steps.

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Acceptance.Tests.Support.Gliderscore;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class ParallelRunSteps
{
    private ParallelRunLedger? _ledger;
    private ParallelRunReport _report = null!;

    // ------------------------------------------------------------------ When

    [When(@"^the harness parallel-runs the GliderScore fixture ""(.+)"" under the seed class ""(.+)""$")]
    public async Task WhenTheHarnessParallelRunsTheGliderScoreFixtureUnderTheSeedClass(
        string fixtureSlug, string seedSlug)
    {
        _ledger = ParallelRunLedgerLoader.Load(fixtureSlug, seedSlug);

        // The ledger's pair block must agree with the pair actually run — the
        // file's own identity, not just its path (the comparator verifies the
        // seed-class name/version against the ADOPTED definition; the pair
        // block is the loader's contract here).
        _ledger.Pair.Fixture.Should().Be(fixtureSlug,
            "the pair's ledger must describe the fixture actually parallel-run.");
        _ledger.Pair.Seed.Should().Be(seedSlug,
            "the pair's ledger must describe the seed class actually parallel-run.");

        // SeedDefinitionLoader keys by JSON FILE name, the ledger loader by
        // slug — the two artifact conventions meet here.
        var seed = SeedDefinitionLoader.Load($"{seedSlug}.json");

        var fixture = FixtureLoader.Load(fixtureSlug);

        var outcome = await new ReplayDriver(AcceptanceFixture.Client)
            .ReplayAsync(fixture, new ParallelRunMode(seed));

        _report = await ParallelRunComparator.CompareAsync(
            fixture, _ledger, outcome, AcceptanceFixture.EventStore, AcceptanceFixture.Client);
    }

    // ------------------------------------------------------------------ Then

    [Then(@"^the parallel-run verdict is exactly the triaged differences$")]
    public void ThenTheParallelRunVerdictIsExactlyTheTriagedDifferences()
    {
        var report = Report();

        // Exactness discipline (WI-3.2): Exact on a ledgered pair is a
        // ledger/state CONTRADICTION — the ledger carries witnessed triaged
        // entries, so an exact run means the ledger no longer describes the
        // pair. A scenario failure demanding re-triage, never a quiet pass
        // and never an emptied ledger.
        report.Exact.Should().BeFalse(
            $"PARALLEL RUN EXACT on ({report.FixtureSlug}, {report.SeedSlug}) contradicts the ledger: it carries "
            + $"{report.TriagedEntries} triaged entries, so an exact run means the ledger no longer describes the "
            + "pair — stop and re-triage (seed-definition-parallel-run.md WI-3.2)."
            + $"{Environment.NewLine}{report.Render()}");

        // Escalation law (WI-2.3): the computed difference set is exactly the
        // triaged set. A Mismatch in either direction fails here with the
        // full report — classification is never done by editing the ledger.
        report.TriagedSetMatches.Should().BeTrue(
            $"the parallel-run claim is 'the differences are exactly the triaged set'; "
            + $"compared raw {report.RawCellsCompared}/{report.OracleCells} oracle cells, normalised "
            + $"{report.NormalisedCellsCompared}/{report.OracleCells}, ranking {report.RankingPilotsCompared} pilots."
            + $"{Environment.NewLine}{report.Render()}");

        report.UntriagedDifferences.Should().BeEmpty(
            "an untriaged computed difference escalates for human triage — a genuine engine "
            + "divergence (triage kind 3) is a defect, never a ledger edit."
            + $"{Environment.NewLine}{report.Render()}");

        report.MissingDifferences.Should().BeEmpty(
            "a triaged difference that fails to appear FAILS the scenario — the ledger no "
            + "longer describes the pair."
            + $"{Environment.NewLine}{report.Render()}");

        report.ProvenanceBreaks.Should().BeEmpty(
            "the run must have run under the ledger's disclosed provenance (P4) — the "
            + "parallel-run claim is stated given it."
            + $"{Environment.NewLine}{report.Render()}");
    }

    [Then(@"^the final placings match the GliderScore oracle exactly$")]
    public void ThenTheFinalPlacingsMatchTheGliderScoreOracleExactly()
    {
        // The product grain (WI-2.2): where the two would have split on the
        // day. The ledger triages nothing here — this pair's entries are all
        // raw-grain — so any ranking difference fails the run outright, and
        // the compared-count pin keeps a silently shrunk comparison from
        // faking the match.
        var report = Report();

        report.RankingPilotsCompared.Should().BePositive(
            "the ranking grain must actually compare the oracle's placings — a zero would "
            + "mean nothing was compared.");

        report.ComputedDifferences
            .Where(mismatch => mismatch.Grain == "ranking")
            .Should().BeEmpty(
                $"the final placings must match the GS oracle exactly for all "
                + $"{report.RankingPilotsCompared} pilots — the product of the parallel run."
                + $"{Environment.NewLine}{report.Render()}");
    }

    [Then(@"^every ledgered difference is a triaged rulebook-vs-local-practice difference with a citation$")]
    public void ThenEveryLedgeredDifferenceIsATriagedRulebookVsLocalPracticeDifferenceWithACitation()
    {
        // Triage kinds 1 (local variation) and 2 (seed-class authoring gap)
        // are the rulebook-vs-local-practice differences a ledger may hold;
        // kind 3 (engine divergence) NEVER does — it escalates to a defect
        // (story What). And every entry must cite its rulebook or
        // local-practice authority: a difference without one is an untriaged
        // excuse, not a documented divergence.
        var ledger = Ledger();

        ledger.TriagedDifferences.Should().NotBeEmpty(
            "the pair's ledger carries the triaged set the verdict is judged against; an empty "
            + "ledger on this pair is a re-triage that must be re-authored, not drifted to.");

        var untriagedOrUncited = ledger.TriagedDifferences
            .Where(entry => entry.TriageKind is not (1 or 2)
                            || string.IsNullOrWhiteSpace(entry.Citation))
            .ToList();

        untriagedOrUncited.Should().BeEmpty(
            $"every ledgered difference must be a triaged rulebook-vs-local-practice difference "
            + $"(kind 1 local variation or kind 2 seed-class authoring gap) with a citation; "
            + $"{ledger.Pair.Fixture} carries {untriagedOrUncited.Count} without one.");
    }

    // -------------------------------------------------------------- plumbing

    private ParallelRunReport Report() =>
        _report ?? throw new InvalidOperationException(
            "No parallel-run comparison has run yet — the parallel-run When step must precede the comparison Then steps.");

    private ParallelRunLedger Ledger() =>
        _ledger ?? throw new InvalidOperationException(
            "No ledger loaded yet — the parallel-run When step must precede the comparison Then steps.");
}
