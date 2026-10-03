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

        // gs-ledger-modes.md WI-3 — the ledger gate at the When tail: strict
        // (the default) fails any PENDING triaged difference after the full
        // compare, with its rendered evidence. The verdict's exactness and
        // set-equality assertions below are unchanged and still run on green
        // pairs; permanent entries are reported (corpus report), never failed.
        if (GsLedgerModeReader.FromEnvironment() == GsLedgerMode.Strict)
        {
            var pending = _ledger.TriagedDifferences.Where(e => !e.Permanent).ToList();

            pending.Should().BeEmpty(
                LedgerGate.PairDivergenceExplanation(pending)
                + $"{Environment.NewLine}{_report.Render()}");
        }
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
            + $"{report.NormalisedCellsCompared}/{report.OracleCells}, ranking {report.RankingPilotsCompared} pilots, "
            + $"teams {report.TeamStandingsCompared}/{report.OracleTeamStandings} standings."
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

    // f5j-christchurch-parallel-run-witness.md WI-3 — the declared-scale /
    // canonical exactness claim (decision 2): both sides compute min(t,600)·1
    // + the landing award − two-rate height with identical band arithmetic,
    // so every raw cell in the window compares exact (162 flown + 34
    // flight-less 0.0 + the 2 cancelled 0.0). A raw-grain mismatch is a
    // declaration/authoring bug or a kind-3 engine divergence — escalate,
    // never a ledger edit. The normalised class entry's exactness is its
    // gs_03 structured declared set: every computed normalised difference
    // must equal a declared cell and the counts must agree, so a silently
    // shrunk comparison cannot fake the match.
    [Then(@"^the raw grain is exact against the GliderScore oracle$")]
    public void ThenTheRawGrainIsExactAgainstTheGliderScoreOracle()
    {
        var report = Report();
        var ledger = Ledger();

        report.ComputedDifferences
            .Where(mismatch => mismatch.Grain == "raw")
            .Should().BeEmpty(
                "the raw grain is exact on this pair (declared NZ F3J-side scale composed with the seed's own "
                + "5.5.11.12 h rows, identical band arithmetic, no floor exercised, no overfly recorded) — a raw "
                + "mismatch is an authoring bug or a kind-3 engine divergence, never a ledger edit."
                + $"{Environment.NewLine}{report.Render()}");

        var declared = DeclaredCellCount(ledger, "normalised");

        declared.Should().BePositive(
            "the ledger's normalised grain must declare its exact witnessed difference set as structured cells.");

        report.ComputedDifferences
            .Count(mismatch => mismatch.Grain == "normalised")
            .Should().Be(declared,
                $"the ledger's normalised grain declares exactly {declared} witnessed difference cell(s) — every "
                + "computed normalised difference must equal a declared cell (identity and both pinned values)."
                + $"{Environment.NewLine}{report.Render()}");
    }

    // f5j-christchurch-parallel-run-witness.md WI-3 — the story's guarantee,
    // loud: the drop split (seed Σ best-10 vs GS Σ 11) moves final placings,
    // so the ranking grain must be NON-empty, every computed ranking mismatch
    // covered by a triaged per-pilot ranking entry, and every such entry
    // witnessed (none missing). The verdict step asserts the global
    // set-equality; this step asserts its ranking-grain legs with
    // grain-scoped evidence.
    [Then(@"^the final placings split from the GliderScore oracle exactly as the ledger triages$")]
    public void ThenTheFinalPlacingsSplitFromTheGliderScoreOracleExactlyAsTheLedgerTriages()
    {
        var report = Report();
        var ledger = Ledger();

        var rankingComputed = report.ComputedDifferences
            .Where(mismatch => mismatch.Grain == "ranking")
            .ToList();

        rankingComputed.Should().NotBeEmpty(
            "the drop split is this pair's guarantee — the seed drops each pilot's lowest round score "
            + "(applyWhenRoundsCompletedAtLeast 5 over 11 scored rounds) while GS summed everything — so an "
            + "empty ranking grain means the run did not witness the split."
            + $"{Environment.NewLine}{report.Render()}");

        var rankingEntries = ledger.TriagedDifferences
            .Where(entry => entry.Grain == "ranking")
            .ToList();

        rankingEntries.Should().NotBeEmpty(
            "the ranking grain — the product — is fully enumerated per pilot; a split with no per-pilot "
            + "entries is an uncurated run.");

        rankingComputed
            .Where(mismatch => !rankingEntries.Any(entry => entry.Covers(mismatch)))
            .Should().BeEmpty(
                "every computed ranking mismatch must be covered by a triaged per-pilot ranking entry — "
                + "an uncovered placing is a re-triage or a kind-3 escalation, never a ledger edit."
                + $"{Environment.NewLine}{report.Render()}");

        rankingEntries
            .Where(entry => !rankingComputed.Any(mismatch => entry.Covers(mismatch)))
            .Should().BeEmpty(
                "every triaged per-pilot ranking entry must be witnessed — a triaged placing that fails to "
                + "appear FAILS the scenario."
                + $"{Environment.NewLine}{report.Render()}");
    }

    // gs_10_teams-grain-parallel-comparison.md — the teams-grain leg: every
    // computed teams mismatch is covered by a triaged teams entry (identity
    // AND both pinned values, per (team, kind)), every teams entry is
    // witnessed, and the compared-count pin keeps a silently shrunk
    // comparison from faking the match. No non-empty assertion: a fully
    // equal grain is valid where the evidence supports it — the verdict step
    // asserts the global set-equality, this step its teams-grain legs.
    [Then(@"^the team standings match the GS team ladder exactly as the ledger triages$")]
    public void ThenTheTeamStandingsMatchTheGsTeamLadderExactlyAsTheLedgerTriages()
    {
        var report = Report();
        var ledger = Ledger();

        var teamsComputed = report.ComputedDifferences
            .Where(mismatch => mismatch.Grain == "teams")
            .ToList();

        var teamsEntries = ledger.TriagedDifferences
            .Where(entry => entry.Grain == "teams")
            .ToList();

        teamsComputed
            .Where(mismatch => !teamsEntries.Any(entry => entry.Covers(mismatch)))
            .Should().BeEmpty(
                "every computed teams mismatch must be covered by a triaged per-team entry — "
                + "an uncovered team total, place or contributor is a re-triage or a kind-3 escalation, "
                + "never a ledger edit."
                + $"{Environment.NewLine}{report.Render()}");

        teamsEntries
            .Where(entry => !teamsComputed.Any(mismatch => entry.Covers(mismatch)))
            .Should().BeEmpty(
                "every triaged per-team entry must be witnessed — a triaged team total that fails to "
                + "appear FAILS the scenario."
                + $"{Environment.NewLine}{report.Render()}");

        report.TeamStandingsCompared.Should().Be(report.OracleTeamStandings,
            $"the teams grain must compare every GS team standing through to completion "
            + $"(oracle carries {report.OracleTeamStandings}) — a short count is a skipped standing, "
            + "never parity."
            + $"{Environment.NewLine}{report.Render()}");
    }

    // f3j-international-parallel-run-retriage.md WI-2 item 2 — the count pins,
    // gs_03 exact: the ledger triages its raw and normalised differences as
    // structured declared cell sets, so the entry's exact witness is the
    // declared count per grain — the computed count per grain must equal it.
    // The raw grain is NON-empty by assertion; the ranking grain needs no
    // pin — it is fully enumerated per pilot and the split step asserts
    // coverage in both directions (identity AND both pinned values).
    [Then(@"^the witnessed split counts match the ledger's pins$")]
    public void ThenTheWitnessedSplitCountsMatchTheLedgerSPins()
    {
        var report = Report();
        var ledger = Ledger();

        var rawComputed = report.ComputedDifferences
            .Count(mismatch => mismatch.Grain == "raw");

        rawComputed.Should().BePositive(
            "the landing-0 sentinel split is this pair's raw-grain product — an empty raw grain means "
            + "the run did not witness the split."
            + $"{Environment.NewLine}{report.Render()}");

        var declaredRaw = DeclaredCellCount(ledger, "raw");

        rawComputed.Should().Be(declaredRaw,
            $"the ledger's raw grain declares exactly {declaredRaw} witnessed difference cell(s) — every "
            + "computed raw difference must equal a declared cell (identity and both pinned values)."
            + $"{Environment.NewLine}{report.Render()}");

        var normalisedComputed = report.ComputedDifferences
            .Count(mismatch => mismatch.Grain == "normalised");

        var declaredNormalised = DeclaredCellCount(ledger, "normalised");

        normalisedComputed.Should().Be(declaredNormalised,
            $"the ledger's normalised grain declares exactly {declaredNormalised} witnessed difference cell(s) — "
            + "every computed normalised difference must equal a declared cell (identity and both pinned values)."
            + $"{Environment.NewLine}{report.Render()}");
    }

    // gs_03 — the structured declared count for a grain: the distinct
    // (round, group, pilot, GS value, seed value) cells declared across the
    // grain's entries. No count is parsed from descriptive prose — the
    // ledger's structured fields are the expectation.
    private static int DeclaredCellCount(ParallelRunLedger ledger, string grain) =>
        ledger.TriagedDifferences
            .Where(entry => entry.Grain.Equals(grain, StringComparison.OrdinalIgnoreCase))
            .SelectMany(entry => entry.Cells ?? [])
            .Select(cell => (cell.Round, cell.Group, cell.PilotNo, cell.Gs, cell.Seed))
            .Distinct()
            .Count();

    // -------------------------------------------------------------- plumbing

    private ParallelRunReport Report() =>
        _report ?? throw new InvalidOperationException(
            "No parallel-run comparison has run yet — the parallel-run When step must precede the comparison Then steps.");

    private ParallelRunLedger Ledger() =>
        _ledger ?? throw new InvalidOperationException(
            "No ledger loaded yet — the parallel-run When step must precede the comparison Then steps.");
}
