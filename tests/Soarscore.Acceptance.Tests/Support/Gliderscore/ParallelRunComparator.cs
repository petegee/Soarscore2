// kanban/in-progress/seed-definition-parallel-run.md WI-2 items 2–3 — the
// parallel-run comparator: the difference set between GS's oracle
// (expected-scores.json / expected-result.json) and a seed-run (ReplayDriver's
// parallel-run mode), the set-equality verdict against the pair's ledger
// (ParallelRunLedger), and the report the WI-3 scenario asserts with.
//
// The claim's shape differs from parity, so the compare differs too:
//
//   • The raw grain compares the seed-run's pre-normalisation score AS
//     PRODUCED against GS's stored RawScore — NO D1 composition. Parity's
//     grain 1 (Comparator.CompareRawGrainViaHttpAsync) composes the
//     ScoreNormalised terms onto the fetched preNormalisationScore so both
//     sides speak GS's composition, because parity proves ENGINE EQUIVALENCE
//     under GS-mirrored configs. The parallel run proves the opposite shape:
//     where the rulebook composition and the club's local practice produce
//     DIFFERENT PAPERS. Composing here would re-enact GS's composition on the
//     seed side and erase exactly the difference the ledger triages — the
//     anti-goal (seed classes are never tuned to GS) applied to the comparison
//     itself. The normalised grain (post-normalisation cells incl. the
//     ScoreNormalised terms) and the ranking grain are the parity walks
//     reused verbatim.
//
//   • NO ledger is subtracted. The computed set IS the product; the verdict is
//     set-equality against the triaged set, wildcard-aware in both directions:
//       — a computed difference outside the triaged set, or
//       — a triaged difference that fails to appear,
//     fails the verdict (WI-2.3). Classification into triage kind 3 (engine
//     divergence) is never done by editing the ledger to fit — an untriaged
//     difference is reported verbatim for human triage.
//
//   • Exactness is a first-class, self-describing outcome (WI-3.2): an empty
//     computed set against an empty triaged set renders as EXACT, loudly —
//     "may come out exact" must not quietly become a tuned expectation.
//
//   • Provenance is verified, not just disclosed (P4): the run's actual
//     ParallelRunBindings must equal the ledger's binding block, the adopted
//     definition must carry the ledger's seed class name/version, and every
//     metric-mapping disclosure must match the ADOPTED seed class's declared
//     whenNotRecorded value. A drift means the run did not run under the
//     disclosed provenance — the claim is stated *given* that provenance, so
//     a drift fails rather than passing on a stale disclosure. The fix is
//     re-authoring the ledger beside a re-triage, never re-authoring the seed
//     to fit.
//
// Out of scope by the three-grain contract (and noted here so the absence is
// deliberate, not forgotten): the parity harness's conservation self-check and
// team grains. The parallel-run claim is about the score papers and placings
// against GS's oracle; conservation is a parity-path integrity check over our
// own data, and ales's UseTeams=false leaves no team grain to run.

using Soarscore.Application;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

/// <summary>The set-equality verdict, exactness first (WI-3.2).</summary>
public enum ParallelRunVerdict
{
    /// <summary>Empty computed set against an empty triaged set — the pair is exact, loudly.</summary>
    Exact,

    /// <summary>The computed difference set is exactly the triaged set.</summary>
    MatchesTriagedSet,

    /// <summary>An untriaged computed difference, a triaged difference that failed to
    /// appear, or a provenance break. Escalates for human triage — never a ledger edit.</summary>
    Mismatch,
}

/// <summary>
/// The parallel-run compare product: the computed difference set (ledger-shaped,
/// minus nothing), the two failure directions of the set-equality, the
/// provenance verdict, and a self-describing render the scenario asserts with.
/// </summary>
public sealed record ParallelRunReport(
    string FixtureSlug,
    string SeedSlug,
    ParallelRunVerdict Verdict,
    IReadOnlyList<GrainMismatch> ComputedDifferences,
    IReadOnlyList<GrainMismatch> UntriagedDifferences,
    IReadOnlyList<ParallelRunDifferenceEntry> MissingDifferences,
    IReadOnlyList<string> ProvenanceBreaks,
    int RawCellsCompared,
    int NormalisedCellsCompared,
    int RankingPilotsCompared,
    int OracleCells,
    int TriagedEntries)
{
    public bool Exact => Verdict == ParallelRunVerdict.Exact;

    /// <summary>True for Exact and MatchesTriagedSet — the scenario's "the parallel-run
    /// ledger is exactly the triaged set" assertion (Exact is its empty case).</summary>
    public bool TriagedSetMatches => Verdict != ParallelRunVerdict.Mismatch;

    public string Render()
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;

        var lines = new List<string>
        {
            Verdict switch
            {
                ParallelRunVerdict.Exact =>
                    $"PARALLEL RUN EXACT — fixture '{FixtureSlug}' under seed '{SeedSlug}': the seed-run and the "
                    + "GS oracle agree at every grain (no computed differences, no triaged differences). "
                    + "Exactness is a reported outcome, not a tuned expectation: the club could have run the "
                    + "seed class in parallel and recorded identical score papers and placings.",
                ParallelRunVerdict.MatchesTriagedSet =>
                    $"PARALLEL RUN VERDICT — fixture '{FixtureSlug}' under seed '{SeedSlug}': the computed "
                    + $"difference set is EXACTLY the triaged set ({TriagedEntries} triaged, "
                    + $"{ComputedDifferences.Count} computed, every entry witnessed). The parallel run reports "
                    + "exactly the triaged differences and nothing else.",
                _ =>
                    $"PARALLEL RUN VERDICT — fixture '{FixtureSlug}' under seed '{SeedSlug}': FAIL — the computed "
                    + "difference set is NOT exactly the triaged set (seed-definition-parallel-run.md WI-2.3: "
                    + "never reclassify by editing the ledger to fit; escalate for human triage).",
            },
            "grain       | round | group | pilot | seed-run  | GS oracle | delta",
            "------------|-------|-------|-------|-----------|-----------|------",
        };

        lines.AddRange(ComputedDifferences
            .OrderBy(m => m.Grain, StringComparer.Ordinal)
            .ThenBy(m => m.RoundNo).ThenBy(m => m.GroupNo).ThenBy(m => m.PilotNo)
            .Select(m => string.Format(
                invariant,
                "{0,-11} | {1,5} | {2,5} | {3,5} | {4,-9} | {5,-9} | {6}",
                m.Grain, m.RoundNo, m.GroupNo, m.PilotNo,
                m.Ours?.ToString(invariant) ?? "(none)",
                m.Expected?.ToString(invariant) ?? "(none)",
                m.Delta)));

        if (ComputedDifferences.Count == 0)
        {
            lines.Add("(no computed differences)");
        }

        if (UntriagedDifferences.Count > 0)
        {
            lines.Add("");
            lines.Add(
                $"UNTRIAGED computed differences ({UntriagedDifferences.Count}) — outside the triaged set; a "
                + "genuine engine divergence (triage kind 3) escalates to a defect, never a ledger edit:");
            lines.AddRange(UntriagedDifferences.Select(m =>
                $"  {m.Grain} r{m.RoundNo}/g{m.GroupNo} p{m.PilotNo}: seed-run {m.Ours?.ToString(invariant) ?? "(none)"} "
                + $"vs GS oracle {m.Expected?.ToString(invariant) ?? "(none)"} — {m.Detail}"));
        }

        if (MissingDifferences.Count > 0)
        {
            lines.Add("");
            lines.Add(
                $"TRIAGED differences that failed to appear ({MissingDifferences.Count}) — a triaged difference "
                + "that fails to appear FAILS the scenario (WI-2.3):");
            lines.AddRange(MissingDifferences.Select(e =>
                $"  kind {e.TriageKind} {e.Grain} r{e.Round?.ToString() ?? "*"}/g{e.Group?.ToString() ?? "*"} "
                + $"p{e.PilotToken()}: {e.Difference}"));
        }

        if (ProvenanceBreaks.Count > 0)
        {
            lines.Add("");
            lines.Add(
                $"PROVENANCE breaks ({ProvenanceBreaks.Count}) — the run did not run under the ledger's "
                + "disclosed provenance (P4):");
            lines.AddRange(ProvenanceBreaks.Select(b => $"  {b}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public static class ParallelRunComparator
{
    public static async Task<ParallelRunReport> CompareAsync(
        GliderscoreFixture fixture,
        ParallelRunLedger ledger,
        ReplayOutcome outcome,
        IEventStore eventStore,
        HttpClient client)
    {
        // Single-task scope guard, same as the parity compare: the oracle key
        // embeds GS's TaskNo, so the fixture must carry exactly one.
        var taskNos = fixture.ScoresRaw.Rows.Select(r => r.TaskNo).Distinct().ToList();
        if (taskNos.Count != 1)
        {
            throw new NotSupportedException(
                $"Fixture '{fixture.Slug}': expected exactly one GS TaskNo, found [{string.Join(", ", taskNos)}].");
        }

        // The adopted definition (what /publish-class-definition actually
        // accepted — the seed class in parallel-run mode) backs the provenance
        // verification; the entry streams back the normalised walk's
        // destination-collision guard, exactly as on the parity path.
        var competition = await Comparator.LoadCompetitionAsync(eventStore, outcome);
        var entries = await Comparator.LoadEntriesAsync(eventStore, outcome);

        var rawMismatches = new List<GrainMismatch>();
        var normalisedMismatches = new List<GrainMismatch>();
        var rankingMismatches = new List<GrainMismatch>();
        var comparedRaw = new HashSet<string>();
        var comparedNormalised = new HashSet<string>();

        // WI-2 — the three grains over the seed-run, NO ledger subtraction.
        await CompareRawGrainAsProducedAsync(
            fixture, outcome, client, taskNos[0], comparedRaw, rawMismatches);

        // The normalised walk's destination-collision guard rides the
        // collected cells (the parity conservation check re-arranges the same
        // read path through Comparator.ConservationByCompetitor —
        // literal-record-f3k-sample-comp.md WI-4); here the walk runs for the
        // grain and its destination-collision guard alone, so the collected
        // cells are
        // discarded.
        var discardedCells = new Dictionary<CompetitorId, List<TaskRoundScore>>();
        await Comparator.CompareNormalisedGrainAsync(
            fixture, outcome, client, entries, taskNos[0], comparedNormalised, normalisedMismatches, discardedCells);

        var finalScores = await Comparator.GetAsync<CompetitionScoreView>(
            client, $"/competition-result?competitionRef={outcome.CompetitionId.Value}");
        Comparator.CompareRankingGrain(fixture, outcome, finalScores, rankingMismatches);

        // Coverage, same discipline as parity: an oracle cell never compared is
        // itself a computed difference (a slot that failed to open must not
        // silently shrink the difference set toward the triaged set).
        //
        // f5j-christchurch-parallel-run-witness.md WI-2 item 2 — under a
        // declared scored window the coverage universe IS the window: oracle
        // cells outside it are deliberately uncompared (a declared scope,
        // recorded in the ledger, never a silent shrink). Without this the
        // 11-round window yields 126 spurious "never compared" mismatches per
        // grain. No window behaves exactly as today (the ales precedent).
        IEnumerable<string> oracleUniverse = ledger.Provenance.ScoredWindowRounds is { } scoredWindow
            ? fixture.ExpectedScores.Scores.Keys.Where(key => OracleRoundNo(key) <= scoredWindow)
            : fixture.ExpectedScores.Scores.Keys;

        Comparator.EnsureOracleCoverage(
            oracleUniverse, comparedRaw, "raw", rawMismatches);
        Comparator.EnsureOracleCoverage(
            oracleUniverse, comparedNormalised, "normalised", normalisedMismatches);

        // The computed set, ledger-shaped and minus nothing.
        var computed = rawMismatches.Concat(normalisedMismatches).Concat(rankingMismatches).ToList();

        // Set-equality in both directions (WI-2.3), wildcard-aware.
        var untriaged = computed
            .Where(mismatch => !ledger.TriagedDifferences.Any(entry => entry.Covers(mismatch)))
            .ToList();
        var missing = ledger.TriagedDifferences
            .Where(entry => !computed.Any(mismatch => entry.Covers(mismatch)))
            .ToList();

        var provenanceBreaks = CheckProvenance(fixture, ledger, outcome, competition);

        var verdict =
            provenanceBreaks.Count > 0 || untriaged.Count > 0 || missing.Count > 0
                ? ParallelRunVerdict.Mismatch
                : computed.Count == 0 && ledger.TriagedDifferences.Count == 0
                    ? ParallelRunVerdict.Exact
                    : ParallelRunVerdict.MatchesTriagedSet;

        return new ParallelRunReport(
            FixtureSlug: fixture.Slug,
            SeedSlug: ledger.Pair.Seed,
            Verdict: verdict,
            ComputedDifferences: computed,
            UntriagedDifferences: untriaged,
            MissingDifferences: missing,
            ProvenanceBreaks: provenanceBreaks,
            RawCellsCompared: comparedRaw.Count,
            NormalisedCellsCompared: comparedNormalised.Count,
            RankingPilotsCompared: fixture.ExpectedResult.Ranks.Length,
            OracleCells: fixture.ExpectedScores.Scores.Count,
            TriagedEntries: ledger.TriagedDifferences.Count);
    }

    // ------------------------------------------------------- grain 1 (raw)

    /// <summary>
    /// The parallel-run raw grain: the seed-run's pre-normalisation score AS
    /// PRODUCED against GS's stored RawScore, per cell — see the file header
    /// for why there is NO D1 composition here. The walk mirrors
    /// Comparator.CompareRawGrainViaHttpAsync's fetch/coordinate/compare shape
    /// minus the term evaluation, so it needs neither the adopted definition's
    /// resolved tasks nor the entry streams (no ScoreNormalised terms are
    /// evaluated, hence none of that walk's D2 guards apply either).
    /// </summary>
    private static async Task CompareRawGrainAsProducedAsync(
        GliderscoreFixture fixture,
        ReplayOutcome outcome,
        HttpClient client,
        int taskNo,
        HashSet<string> compared,
        List<GrainMismatch> mismatches)
    {
        var groupByGroupId = outcome.GroupIdByRoundAndGroup.ToDictionary(kv => kv.Value, kv => kv.Key);
        var pilotByCompetitor = outcome.CompetitorByPilotNo.ToDictionary(kv => kv.Value, kv => kv.Key);

        foreach (var roundNo in outcome.RoundOrdinalByRoundNo.Keys.OrderBy(n => n))
        {
            var views = await Comparator.GetAsync<IReadOnlyList<GroupScoreView>>(
                client,
                $"/task-round-result?competitionRef={outcome.CompetitionId.Value}"
                + $"&phaseOrdinal={outcome.PhaseOrdinal}"
                + $"&roundOrdinal={outcome.RoundOrdinalByRoundNo[roundNo]}"
                + "&taskRoundOrdinal=1");

            foreach (var view in views)
            {
                var (roundOfView, groupNo) = groupByGroupId[view.GroupRef];

                foreach (var result in view.Results)
                {
                    var pilotNo = pilotByCompetitor[result.CompetitorRef];
                    Comparator.RecordCell("raw", pilotNo, roundOfView, groupNo, taskNo, compared, mismatches);

                    Comparator.AddIfDifferent(
                        mismatches, "raw", pilotNo, roundOfView, groupNo,
                        result.PreNormalisationScore,
                        Comparator.OracleCell(fixture, taskNo, roundOfView, groupNo, pilotNo)?.RawScore);
                }
            }
        }
    }

    // ---------------------------------------------------------- provenance

    /// <summary>One oracle key's RoundNo — the keyFormat is
    /// {"TaskNo"}/{"RoundNo"}/{"GroupNo"}/{"ReFlightNo"}/{"PilotNo"}.</summary>
    private static int OracleRoundNo(string oracleKey) =>
        int.Parse(oracleKey.Split('/')[1], System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The P4 verification: the run must have run under the ledger's disclosed
    /// provenance. Publication proof (the adoption content hash), the actual
    /// bindings vs the ledger's binding block, the adopted definition's seed
    /// identity, and every metric-mapping disclosure against the adopted seed
    /// class's own declared whenNotRecorded — the proof that the disclosed
    /// values are seed-declared and engine-resolved, never harness-emitted.
    ///
    /// f5j-christchurch-parallel-run-witness.md WI-2 item 6 widens four
    /// places, all additive and null-tolerant (ledgers without them verify
    /// exactly as before): number-valued metric mappings, derived-metric
    /// declarations, the scored-window round-count check, and the declared
    /// reading-scale match.
    /// </summary>
    private static List<string> CheckProvenance(
        GliderscoreFixture fixture,
        ParallelRunLedger ledger,
        ReplayOutcome outcome,
        Competition competition)
    {
        var breaks = new List<string>();

        if (string.IsNullOrEmpty(outcome.DefinitionContentHash))
        {
            breaks.Add(
                $"the replay outcome carries no definition content hash — the seed class was not published "
                + "through /publish-class-definition (WI-1's publication proof).");
        }

        var adopted = competition.AdoptedRules.Definition;

        if (adopted.Name != ledger.SeedClass.Name || adopted.Version != ledger.SeedClass.Version)
        {
            breaks.Add(
                $"the adopted class is '{adopted.Name} — {adopted.Version}' but the ledger discloses "
                + $"'{ledger.SeedClass.Name} — {ledger.SeedClass.Version}'.");
        }

        if (outcome.ParallelRunBindings is null)
        {
            breaks.Add(
                $"the replay outcome carries no parallel-run bindings — the run was not a parallel-run "
                + "replay, so the ledger's binding provenance cannot be verified.");
        }
        else
        {
            var actual = outcome.ParallelRunBindings;

            foreach (var declared in ledger.Provenance.ParameterBindings)
            {
                if (!actual.TryGetValue(declared.Parameter, out var value))
                {
                    breaks.Add(
                        $"the ledger binds '{declared.Parameter}' = {declared.Value} but the run bound no "
                        + "such parameter.");
                }
                else if (value != declared.Value)
                {
                    breaks.Add(
                        $"the ledger binds '{declared.Parameter}' = {declared.Value} but the run bound "
                        + $"{value}.");
                }
            }

            foreach (var (parameter, value) in actual)
            {
                if (!ledger.Provenance.ParameterBindings.Any(b => b.Parameter == parameter))
                {
                    breaks.Add(
                        $"the run bound '{parameter}' = {value} but the ledger's provenance block names no "
                        + "such binding.");
                }
            }
        }

        var declaredMetrics = adopted.Phases
            .SelectMany(p => p.Tasks)
            .SelectMany(t => t.Metrics)
            .ToList();

        foreach (var mapping in ledger.Provenance.MetricMappings)
        {
            var matches = declaredMetrics.Where(m => m.Name == mapping.Metric).ToList();

            if (matches.Count == 0)
            {
                breaks.Add(
                    $"the ledger discloses metric '{mapping.Metric}' but the adopted seed class declares no "
                    + "such metric.");
                continue;
            }

            // f5j-christchurch-parallel-run-witness.md WI-3 — a multi-phase
            // seed declares its shared task's metrics once PER PHASE (30-f5j's
            // preliminary and fly-off carry the same task), so a name may
            // match more than once. Identical duplicates name one disclosure
            // unambiguously; genuinely DIFFERING declarations stay a break.
            if (matches.Select(m => $"{m.Kind}/{Describe(m.WhenNotRecorded)}").Distinct().Count() > 1)
            {
                breaks.Add(
                    $"the ledger discloses metric '{mapping.Metric}' but the adopted seed class declares it "
                    + "differently across phases — the disclosure cannot name which declaration it means.");
                continue;
            }

            var whenNotRecorded = matches[0].WhenNotRecorded;

            // WI-2 item 6 — Flag (bool) and Number (decimal) whenNotRecorded
            // values are both expressible; anything else is not a value the
            // seed could have declared and breaks loudly.
            var resolves = mapping.Resolved.ValueKind switch
            {
                System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False =>
                    whenNotRecorded is { Kind: MeasuredKind.Flag, Flag: { } flag }
                    && flag == mapping.Resolved.GetBoolean(),
                System.Text.Json.JsonValueKind.Number =>
                    whenNotRecorded is { Kind: MeasuredKind.Number, Number: { } number }
                    && number == mapping.Resolved.GetDecimal(),
                _ => false,
            };

            if (!resolves)
            {
                breaks.Add(
                    $"the ledger discloses '{mapping.Metric}' resolving to {mapping.Resolved.GetRawText()} when unrecorded, "
                    + $"but the adopted seed class declares whenNotRecorded = {Describe(whenNotRecorded)} — "
                    + "the disclosure and the seed have drifted; re-author the ledger beside a re-triage, "
                    + "never the seed to fit.");
            }
        }

        // WI-2 item 6 — every derived metric is declared by the adopted seed
        // (name match across phases — see the multi-phase note above: a shared
        // task's metric matches once per phase, so only genuinely differing
        // declarations are ambiguous): the derivation re-expresses
        // a foil column under the seed's own metric name, so a name the seed
        // never declares is a disclosure bug.
        foreach (var derived in ledger.Provenance.DerivedMetrics ?? [])
        {
            var matches = declaredMetrics.Where(m => m.Name == derived.Metric).ToList();

            if (matches.Count == 0)
            {
                breaks.Add(
                    $"the ledger derives metric '{derived.Metric}' from '{derived.Source}' but the adopted seed class "
                    + "declares no such metric — the derivation names nothing the run could capture under.");
            }
            else if (matches.Select(m => $"{m.Kind}/{Describe(m.WhenNotRecorded)}").Distinct().Count() > 1)
            {
                breaks.Add(
                    $"the ledger derives metric '{derived.Metric}' but the adopted seed class declares it "
                    + "differently across phases — the disclosure cannot name which declaration it means.");
            }
        }

        // WI-2 item 6 — the declared scored window equals the run's prescribed
        // round count: the run must have run under the disclosed window.
        if (ledger.Provenance.ScoredWindowRounds is { } scoredWindow)
        {
            var prescribed = outcome.RoundOrdinalByRoundNo.Count;

            if (prescribed != scoredWindow)
            {
                breaks.Add(
                    $"the ledger declares scoredWindowRounds {scoredWindow} but the run prescribed {prescribed} "
                    + "rounds — the run did not run under the disclosed window; re-triage, never silently shrink.");
            }
        }

        // WI-2 item 6 — every declared reading instrument matches the
        // competition's actual declaration: the harness-chosen name resolves,
        // the metric agrees, and the scale equals the tape slug's corpus
        // scale (the snapshot the declaration was built from).
        foreach (var declared in ledger.Provenance.DeclaredInstruments ?? [])
        {
            var tape = TapeCorpus.All.FirstOrDefault(t => t.FileName == declared.TapeSlug);

            if (tape is null)
            {
                breaks.Add(
                    $"the ledger declares instrument '{declared.Instrument}' on tape slug '{declared.TapeSlug}', "
                    + "which TapeCorpus names no tape for — the declared scale cannot be verified.");
                continue;
            }

            var actual = competition.DeclaredInstruments?.Instruments
                .FirstOrDefault(i => i.Instrument == declared.Instrument);

            if (actual is null)
            {
                breaks.Add(
                    $"the ledger declares instrument '{declared.Instrument}' for metric '{declared.Metric}' but the "
                    + "competition declared no such instrument — the run did not run under the disclosed scale.");
                continue;
            }

            if (!string.Equals(actual.Metric, declared.Metric, StringComparison.Ordinal))
            {
                breaks.Add(
                    $"the ledger declares instrument '{declared.Instrument}' for metric '{declared.Metric}' but the "
                    + $"competition declared it for metric '{actual.Metric}'.");
            }

            if (!ReadingScalesEqual(tape.Tape.ToReadingScale(), actual.Scale))
            {
                breaks.Add(
                    $"the ledger declares instrument '{declared.Instrument}' on tape '{declared.TapeSlug}' but the "
                    + "competition's declared scale differs from that tape's corpus scale — the run did not run "
                    + "under the disclosed scale.");
            }
        }

        return breaks;
    }

    private static bool ReadingScalesEqual(ReadingScale expected, ReadingScale actual) =>
        string.Equals(expected.Unit, actual.Unit, StringComparison.Ordinal)
        && expected.OffScaleReading == actual.OffScaleReading
        && expected.Marks.SequenceEqual(actual.Marks);

    private static string Describe(MeasuredValue? value) => value switch
    {
        null => "(no declared assumption)",
        { Kind: MeasuredKind.Flag, Flag: { } flag } => $"flag {flag}",
        { Kind: MeasuredKind.Number, Number: { } number } => $"number {number}",
        _ => value.Kind.ToString(),
    };
}
