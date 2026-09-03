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

    [Then(@"^kept normalised cells minus dropped cells and aggregate penalties conserve into every final score$")]
    public void ThenKeptNormalisedCellsConserveIntoEveryFinalScore()
    {
        // WI-5 self-check 2 — per competitor: Σ our grain-2 normalised cells −
        // the engine's own dropped-cell contributions − aggregate penalties ==
        // the /competition-result Score, exactly (Comparator.CheckConservation
        // states it fully). Catches a silently dropped replay slot or a
        // pilot→competitor mapping slip masquerading as a scoring diff.
        var report = Report();

        report.ConservationBreaks.Should().BeEmpty(
            $"conservation must hold exactly for every competitor of {Fixture.Slug}; "
            + $"{report.ConservationBreaks.Count} competitor(s) broke the identity."
            + $"{Environment.NewLine}{report.ConservationTable()}");
    }

    [Then(@"^the derived team standings match the fixture's team semantics exactly$")]
    public void ThenTheDerivedTeamStandingsMatchExactly()
    {
        // teams-mvp.md WI-9 — the team grain: where the fixture declared team
        // scoring with the MVP's own method (NbrForTeamScore == 3), every
        // standing must match the classification contract applied to the
        // oracle-verified individual result (contributors, totals, tie-break
        // evidence, member states, order, shared places). No ledger entry ever
        // excuses this grain — where the fixture's method differs, the
        // comparison does not run (T1), so a mismatch is a defect.
        var report = Report();

        report.TeamGrainMismatches.Should().BeEmpty(
            $"the team grain must match exactly for all {report.TeamsCompared} standing(s) of {Fixture.Slug}."
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
        //
        // WI-6 widening — `trap 3` is accepted beside D1–D6, and only trap 3:
        // the story's own planning answer pre-authorises exactly this ledgered
        // divergence when GS's Score DESC/RawScore DESC ladder secondary key
        // fires against our Score-only ranking (jerilderie-2010 pilots 4/21),
        // and WI-8 records it in deferred-decisions.md. It is not an
        // arithmetic-story divergence (GS complies with its own documented
        // ladder), so citing a D-number there would be dishonest. The pinning
        // "records exactly N" step keeps the widened acceptance from hiding
        // untriaged growth.
        //
        // `N1` is retired: normalisation-lower-clamp.md landed the engine's
        // lower clamp, emptying f5j-nz-south-island's ledger (its 4 entries
        // discharged), so the token licenses nothing and is dropped from the
        // regex below. Keeping it live would let an untriaged divergence pass.
        //
        // `R1` joins them per the WI-6 stop-and-triage ruling (2026-08-28,
        // orchestrator + story owner): GS computes and persists RawScore in
        // binary64, so comp 54's committed oracle carries three unrounded
        // double-sum artefacts verbatim (R4/G2 P85, R5/G1 P77, R5/G3 P128 —
        // named by the fixture's own valuesAsPersisted note); Soarscore's
        // FlightInterpreter computes exact decimal, so ours reprs clean at
        // 1dp and differs by ≤ 1e-13. Representation divergence only — the
        // normalised grain washes it out on the 1-dp HalfUp grid. The
        // planning sweep's "417/417 exact" was double-faithful; the harness
        // compares exact decimals, so the artefact cells are ledgered, not
        // tolerated. Same precedent: cite the token honestly, pin the count.
        //
        // `T1` joins them per teams-mvp.md WI-9 (owner decision 8, 2026-09-02):
        // GS's NbrForTeamScore names how many members score per team, and the
        // MVP's classification method is FIXED at three (bestThreeScoreSum) —
        // a fixture declaring NbrForTeamScore ≠ 3 (f3k-sample-comp = 2,
        // jerilderie-2010 = 4) declares a method the MVP does not have. The
        // adapter never emulates it: memberships map per decision 8, the
        // classification stays unconfigured, the team grain does not run, and
        // one documentary T1 entry per fixture pins why. Ledgered only where
        // team scoring is actually active (UseTeams=true) — an inert
        // NbrForTeamScore under UseTeams=false (f3j-international-flyoff) is
        // not a divergence. Individual grains are unaffected either way:
        // team membership is not an input to any individual score.
        var unattributed = Fixture!.Divergences
            .Where(d => !System.Text.RegularExpressions.Regex.IsMatch(
                d.Reason, @"\bD[1-6]\b|\btrap\s*3\b|\bR1\b|\bT1\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .ToList();

        unattributed.Should().BeEmpty(
            $"every ledgered divergence must cite an arithmetic-story divergence ID; "
            + $"{Fixture.Slug} carries {unattributed.Count} without one.");
    }

    [Then(@"^the fixture's float32 persist-cast witness property holds over its scored normalised cells$")]
    public void ThenTheFixtureSFloat32PersistCastWitnessPropertyHoldsOverItsScoredNormalisedCells()
    {
        // nz-fixture-replay-scenarios.md D6 — the G4 comparator-property step,
        // over the fixture's oracle NormalisedScore values ALONE (no replay
        // data, no comparator changes). GS persists NormalisedScore through a
        // binary32 cast, so emulating `float f = (float)(double)ns` must
        // re-round (HalfUp 1dp) back to the stored value for EVERY scored
        // cell — otherwise the oracle's own storage would be lossy and the
        // harness's exact-decimal comparison unsound. Two pins from the
        // story's Verified ground truth ("G4 float32 persist-cast property —
        // pinned 99/162") keep the property from going silently vacuous: the
        // scored universe is exactly 162 cells, and exactly 99 of them carry
        // cast residue ((double)f != exact). Parent G4 discipline: assert the
        // cast BEHAVIOUR, never literal repr bits.
        //
        // Referenced only by comp 45's scenario (f5j-christchurch-2019) — its
        // stored values are clean exact-1dp values, which is exactly the store
        // this property guards.
        const int expectedScoredCells = 162;
        const int expectedCastResidue = 99;

        var scored = Fixture!.ExpectedScores.Scores.Values
            .Where(cell => cell.NormalisedScore != 0m)
            .ToList();

        scored.Should().HaveCount(
            expectedScoredCells,
            $"the story's ground truth pins comp 45's scored normalised universe at {expectedScoredCells} cells "
            + "(324 total, NS ≠ 0); a different count means the fixture or the property's universe changed.");

        var residue = new List<decimal>();

        foreach (var cell in scored)
        {
            double exact = (double)cell.NormalisedScore;
            float f = (float)exact;

            if ((double)f != exact)
            {
                residue.Add(cell.NormalisedScore);
            }

            Math.Round((decimal)(double)f, 1, MidpointRounding.AwayFromZero)
                .Should().Be(cell.NormalisedScore,
                    "GS's binary32 persist cast re-rounds to the stored 1-dp value for every scored cell "
                    + "(nz-fixture-replay-scenarios.md ground truth; parent G4 discipline: assert the cast "
                    + "behaviour, never repr bits).");
        }

        residue.Should().HaveCount(
            expectedCastResidue,
            $"the story's ground truth pins the cast-residue count at exactly {expectedCastResidue} of "
            + $"{expectedScoredCells} scored cells; a different count means the binary32 arithmetic shifted — "
            + "stop and re-derive by hand before touching anything.");
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
