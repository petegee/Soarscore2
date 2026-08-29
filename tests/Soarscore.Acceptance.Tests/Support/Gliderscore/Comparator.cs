// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-1 — the
// compare half of the steel thread (decision D6): exact decimal comparison at
// three grains, NO tolerance anywhere, minus a committed per-fixture divergence
// ledger that starts EMPTY.
//
//   grain 1 (raw)     — GS's persisted Scores.RawScore vs our pre-normalisation
//                       score. WI-3 (pre-normalisation-score-view-field.md D6)
//                       splits the mechanism on a runtime classification of the
//                       loaded class definition (ScoreNormalisedFree): if every
//                       task of every phase carries an EMPTY scoreNormalised
//                       array, the value is read straight off GET
//                       /task-round-result's preNormalisationScore column —
//                       with nothing composed after normalising, the engine's
//                       pre-normalisation score IS GS's composed raw. Fixtures
//                       whose classes DO author scoreNormalised terms
//                       (ales-sample-comp, option-2) keep the Q1 IN-PROCESS
//                       mirror: the harness folds
//                       the Competition and Entry streams through the direct
//                       provider (AcceptanceFixture.EventStore) and replays the
//                       public granular pipeline exactly as ScoringService.
//                       ScoreGroup does before NormalisationEngine —
//                       ParameterResolver.ResolveTask → MeasurementDigest.Resolve
//                       per flight → FlightInterpreter.Interpret →
//                       ScoringService.SelectFlights → PenaltyEngine.ApplyRawPenalties
//                       — taking TaskResult.RawScore before any normalisation.
//                       On that path one semantic mapping is applied and
//                       documented below
//                       (GsEquivalentRaw): under the story's D3 arrangement the
//                       landing lookup lives in ScoreNormalised, while GS composes
//                       landing INTO its persisted RawScore BEFORE normalising
//                       (arithmetic story, divergence D1 — NS = NormTime + landing).
//                       The comparator therefore adds the ScoreNormalised terms'
//                       contributions onto our pre-normalisation raw so both sides
//                       speak GS's composition; the engine's own evaluation of
//                       those same terms is exercised end-to-end by grain 2.
//                       WI-4 (in-process path only): the task definition
//                       resolves PER ROUND (f3k-sample-comp prescribes a
//                       different GS task each round); that fixture is
//                       scoreNormalised-free and now takes the HTTP path,
//                       leaving ales-sample-comp as the mapping's remaining user.
//   grain 2 (normalised) — GS NormalisedScore vs GET /task-round-result per
//                       round. CompetitorTaskResultView.RawScore carries the
//                       POST-normalisation value (NormalisationEngine.cs line 151
//                       overwrites TaskResult.RawScore), which is exactly what
//                       the oracle column holds.
//   grain 3 (ranking) — expected-result.json rank strings vs GET
//                       /competition-result placings. Our placing n matches
//                       oracle "n" AND "=n"; every "=n" group must contain
//                       exactly the set of pilots we place at n (RankingEngine
//                       shares the numeric place among ties, which is what "=n"
//                       records — story trap 3 notes GS's extra RawScore
//                       secondary key may fire elsewhere; triage then, not now).
//
// Oracle decimals are parsed as System.Decimal straight from the JSON literals
// and compared with == (D6: binary32-widened oracle values repr clean at their
// written decimal count; a tolerance big enough to absorb float32 noise would
// mask exactly the bugs this harness exists to catch).
//
// WI-5 adds three SELF-CHECKS around the same machinery (no production change):
//
//   • conservation — per competitor, exactly:
//         Σ our grain-2 normalised cells            (every /task-round-result row,
//                                                   destination-keyed: CountsFor ?? own round)
//       − Σ contributions of the engine's DROPPED cells
//       − aggregate-penalty deductions              (f3k-sample-comp's −100s)
//         == the competitor's /competition-result Score,
//     and the Disqualified flags agree. The dropped set is the ENGINE'S OWN
//     decision: these very cells are arranged into TaskRoundScores exactly as
//     ScoreCompetition arranges them and folded through ScoringService.Aggregate,
//     whose PhaseScores.DroppedScores carries what its policy removed — the
//     harness never re-implements a tie-break (story trap 2). The identity is
//     stated purely over OUR data, so f3j-international's ledgered phantom
//     cells (never replayed) stand on neither side and need no subtraction;
//     the D6 ledger remains the grain comparisons' business alone. What it
//     catches: a pilot→competitor mapping slip anywhere in the harness
//     (per-competitor sums go asymmetric), a future aggregation/drop/penalty
//     regression that keeps every cell value intact but corrupts totals, and
//     drift between the two read paths (/task-round-result vs
//     /competition-result).
//
//   • ledger strictness — the compare tail (ledger subtraction + report
//     assembly) is extracted into public Comparator.BuildReport so the
//     self-test can drive it against a synthetic mismatch: fails unledgered,
//     passes only under exactly its own ledger entry, still fails under an
//     entry naming someone else.
//
//   • ConservationBreak / ComparisonReport.ConservationTable surface the
//     conservation verdict in the same diff-table spirit as the grains.

using System.Collections.Immutable;
using System.Net.Http.Json;
using Soarscore.Application;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;

// Both namespaces declare a TaskRoundState (the write-side aggregate's and the
// scoring pipeline's) and both are imported above; alias them apart for
// CheckConservation's state collapse.
using CompetitionTaskRoundState = Soarscore.Domain.Competitions.TaskRoundState;
using ScoringTaskRoundState = Soarscore.Domain.Scoring.TaskRoundState;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

/// <summary>One unledgered difference. Delta is Ours − Expected where both exist.</summary>
public sealed record GrainMismatch(
    string Grain,
    long PilotNo,
    int RoundNo,
    int GroupNo,
    decimal? Ours,
    decimal? Expected,
    string Detail)
{
    public string Delta =>
        Ours is { } ours && Expected is { } expected ? (ours - expected).ToString(System.Globalization.CultureInfo.InvariantCulture) : "n/a";
}

/// <summary>
/// One conservation violation (WI-5): the competitor's kept-cell sum minus
/// dropped contributions minus aggregate penalties did not reproduce the
/// published final score (or the Disqualified flags disagreed).
/// ExpectedFinal = AggregateAfterDrops − PenaltyDeduction; Actual is what
/// /competition-result published.
/// </summary>
public sealed record ConservationBreak(
    string CompetitorRef,
    decimal CellSum,
    decimal DroppedSum,
    decimal AggregateAfterDrops,
    decimal PenaltyDeduction,
    decimal ExpectedFinal,
    decimal ActualFinal,
    bool ExpectedDisqualified,
    bool ActualDisqualified)
{
    public string Detail =>
        $"expected {ExpectedFinal} (aggregate {AggregateAfterDrops} − penalties {PenaltyDeduction}) "
        + $"but /competition-result says {ActualFinal}"
        + ((ExpectedDisqualified, ActualDisqualified) switch
        {
            (true, false) => " and engine disqualification was not published",
            (false, true) => " but engine recorded no disqualification",
            _ => "",
        }) + $"; Σ cells {CellSum} − dropped {DroppedSum} = aggregate {AggregateAfterDrops}.";
}

public sealed record ComparisonReport(
    IReadOnlyList<GrainMismatch> RawMismatches,
    IReadOnlyList<GrainMismatch> NormalisedMismatches,
    IReadOnlyList<GrainMismatch> RankingMismatches,
    IReadOnlyList<ConservationBreak> ConservationBreaks,
    int RawCellsCompared,
    int NormalisedCellsCompared,
    int RankingPilotsCompared,
    int OracleCells)
{
    public bool AllGrainsExact =>
        RawMismatches.Count == 0 && NormalisedMismatches.Count == 0 && RankingMismatches.Count == 0;

    /// <summary>WI-5 — the conservation self-check held for every competitor.</summary>
    public bool Conserves => ConservationBreaks.Count == 0;

    /// <summary>The ONE diff table (D6): pilot × round × grain, ours / expected / delta.</summary>
    public string DiffTable()
    {
        var all = RawMismatches.Concat(NormalisedMismatches).Concat(RankingMismatches).ToList();

        var lines = new List<string>
        {
            $"GliderScore replay comparison failed — {all.Count} unledgered mismatch(es):",
            "grain       | round | group | pilot | ours     | expected | delta",
            "------------|-------|-------|-------|----------|----------|------",
        };

        lines.AddRange(all
            .OrderBy(m => m.Grain).ThenBy(m => m.RoundNo).ThenBy(m => m.GroupNo).ThenBy(m => m.PilotNo)
            .Select(m => string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0,-11} | {1,5} | {2,5} | {3,5} | {4,-8} | {5,-8} | {6}",
                m.Grain, m.RoundNo, m.GroupNo, m.PilotNo,
                m.Ours?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)",
                m.Expected?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)",
                m.Delta)));

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>The WI-5 conservation verdict in the same diff-table spirit.</summary>
    public string ConservationTable()
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;

        var lines = new List<string>
        {
            $"GliderScore replay conservation broken for {ConservationBreaks.Count} competitor(s) — "
            + "Σ kept grain-2 cells − dropped cells − aggregate penalties ≠ final score:",
            "competitor                            | cells   | -dropped | =agg     | -pen     | expected | actual",
            "--------------------------------------|---------|----------|----------|----------|----------|-------",
        };

        lines.AddRange(ConservationBreaks
            .OrderBy(b => b.CompetitorRef, StringComparer.Ordinal)
            .Select(b => string.Format(
                invariant,
                "{0,-37} | {1,-7} | {2,-8} | {3,-8} | {4,-8} | {5,-8} | {6}",
                b.CompetitorRef,
                b.CellSum.ToString(invariant),
                b.DroppedSum.ToString(invariant),
                b.AggregateAfterDrops.ToString(invariant),
                b.PenaltyDeduction.ToString(invariant),
                b.ExpectedFinal.ToString(invariant),
                b.ActualFinal.ToString(invariant))));

        return string.Join(Environment.NewLine, lines);
    }
}

public static class Comparator
{
    public static async Task<ComparisonReport> CompareAsync(
        GliderscoreFixture fixture, ReplayOutcome outcome, IEventStore eventStore, HttpClient client)
    {
        // Single-task scope guard (story scope guard v1): the oracle key embeds
        // GS's TaskNo, so the fixture must carry exactly one.
        var taskNos = fixture.ScoresRaw.Rows.Select(r => r.TaskNo).Distinct().ToList();
        if (taskNos.Count != 1)
        {
            throw new NotSupportedException(
                $"Fixture '{fixture.Slug}': expected exactly one GS TaskNo, found [{string.Join(", ", taskNos)}].");
        }

        var competition = await LoadCompetitionAsync(eventStore, outcome);
        var entries = await LoadEntriesAsync(eventStore, outcome);

        var rawMismatches = new List<GrainMismatch>();
        var normalisedMismatches = new List<GrainMismatch>();
        var rankingMismatches = new List<GrainMismatch>();
        var comparedRaw = new HashSet<string>();
        var comparedNormalised = new HashSet<string>();

        // WI-5 — grain 2's cells are collected as they are compared, so the
        // conservation self-check folds exactly what the read path published.
        var cellsByCompetitor = new Dictionary<CompetitorId, List<TaskRoundScore>>();

        // WI-3 (pre-normalisation-score-view-field.md D6): grain 1 splits on
        // the runtime classification — HTTP preNormalisationScore where the
        // class is scoreNormalised-free, the Q1 in-process mirror otherwise.
        if (ScoreNormalisedFree(fixture))
        {
            await CompareRawGrainViaHttpAsync(fixture, outcome, client, taskNos[0], comparedRaw, rawMismatches);
        }
        else
        {
            await CompareRawGrainAsync(fixture, outcome, competition, entries, taskNos[0], comparedRaw, rawMismatches);
        }
        await CompareNormalisedGrainAsync(
            fixture, outcome, client, entries, taskNos[0], comparedNormalised, normalisedMismatches, cellsByCompetitor);

        // One fetch serves both the ranking grain and the conservation check.
        var finalScores = await GetAsync<CompetitionScoreView>(
            client, $"/competition-result?competitionRef={outcome.CompetitionId.Value}");
        CompareRankingGrain(fixture, outcome, finalScores, rankingMismatches);

        // Ledgered divergences are SUBTRACTED (D6); the remainder must be empty.
        // Coverage is enforced symmetrically: an oracle cell we never compared,
        // or an our-cell with no oracle counterpart, is itself a mismatch.
        EnsureOracleCoverage(fixture.ExpectedScores.Scores.Keys, comparedRaw, "raw", rawMismatches);
        EnsureOracleCoverage(fixture.ExpectedScores.Scores.Keys, comparedNormalised, "normalised", normalisedMismatches);

        // WI-5 — conservation runs over OUR cells and the PUBLISHED finals,
        // independent of whether any grain matched: a break is evidence in its
        // own right, not a consequence of a grain mismatch.
        var conservationBreaks = CheckConservation(outcome, competition, finalScores, cellsByCompetitor);

        return BuildReport(
            fixture,
            rawMismatches,
            normalisedMismatches,
            rankingMismatches,
            conservationBreaks,
            comparedRaw.Count,
            comparedNormalised.Count,
            fixture.ExpectedResult.Ranks.Length,
            fixture.ExpectedScores.Scores.Count);
    }

    /// <summary>
    /// The compare tail every caller shares: subtract the fixture's divergence
    /// ledger (D6) from each grain's mismatches and assemble the report. Public
    /// for the WI-5 ledger-strictness self-test, which drives it against a
    /// synthetic mismatch — fails unledgered, passes only under exactly its own
    /// ledger entry.
    /// </summary>
    public static ComparisonReport BuildReport(
        GliderscoreFixture fixture,
        IReadOnlyList<GrainMismatch> rawMismatches,
        IReadOnlyList<GrainMismatch> normalisedMismatches,
        IReadOnlyList<GrainMismatch> rankingMismatches,
        IReadOnlyList<ConservationBreak> conservationBreaks,
        int rawCellsCompared,
        int normalisedCellsCompared,
        int rankingPilotsCompared,
        int oracleCells)
    {
        var rawRemainder = SubtractLedger(fixture, rawMismatches).ToList();
        var normalisedRemainder = SubtractLedger(fixture, normalisedMismatches).ToList();
        var rankingRemainder = SubtractLedger(fixture, rankingMismatches).ToList();

        return new ComparisonReport(
            RawMismatches: rawRemainder,
            NormalisedMismatches: normalisedRemainder,
            RankingMismatches: rankingRemainder,
            ConservationBreaks: conservationBreaks,
            RawCellsCompared: rawCellsCompared,
            NormalisedCellsCompared: normalisedCellsCompared,
            RankingPilotsCompared: rankingPilotsCompared,
            OracleCells: oracleCells);
    }

    // ------------------------------------------------------------- grain 1

    /// <summary>
    /// WI-3 classification (pre-normalisation-score-view-field.md D6.1/D6.2):
    /// true iff every task of every phase of the fixture's LOADED class
    /// definition carries an empty ScoreNormalised array — nothing is composed
    /// after normalising, so the engine's pre-normalisation score is already
    /// GS's composed raw and grain 1 can read it over HTTP. Derived at runtime
    /// from the definition, never a per-slug table.
    /// </summary>
    private static bool ScoreNormalisedFree(GliderscoreFixture fixture) =>
        fixture.Definition.Phases.SelectMany(phase => phase.Tasks)
            .All(task => task.ScoreNormalised.IsEmpty);

    /// <summary>
    /// WI-3 grain 1 over HTTP (pre-normalisation-score-view-field.md D6.3) for
    /// scoreNormalised-free fixtures: one GET /task-round-result per round —
    /// the same plumbing as grain 2 — reading each row's PreNormalisationScore
    /// and comparing it exactly against the oracle RawScore. ALL rows compare,
    /// not just Original ones (trap 10): a make-up row's oracle cell sits at
    /// its HOSTING (round, group), exactly as grain 2 treats it, and the
    /// fetched row universe equals outcome.EntryIdBySlot by construction
    /// (D6.5) — so RecordCell bookkeeping stays identical to the legacy path
    /// and EnsureOracleCoverage remains honest.
    /// </summary>
    private static async Task CompareRawGrainViaHttpAsync(
        GliderscoreFixture fixture,
        ReplayOutcome outcome,
        HttpClient client,
        int taskNo,
        HashSet<string> compared,
        List<GrainMismatch> mismatches)
    {
        // D6.2 belt-and-braces: routing and classification are the same
        // predicate today; if they ever disagree, refuse loudly rather than
        // compare a value that is not GS's composition.
        if (!ScoreNormalisedFree(fixture))
        {
            throw new NotSupportedException(
                $"Fixture '{fixture.Slug}': routed to the HTTP raw grain but its class definition "
                + "carries ScoreNormalised terms — the classification and the routing disagree.");
        }

        var groupByGroupId = outcome.GroupIdByRoundAndGroup.ToDictionary(kv => kv.Value, kv => kv.Key);
        var pilotByCompetitor = outcome.CompetitorByPilotNo.ToDictionary(kv => kv.Value, kv => kv.Key);

        foreach (var roundNo in outcome.RoundOrdinalByRoundNo.Keys.OrderBy(n => n))
        {
            var views = await GetAsync<IReadOnlyList<GroupScoreView>>(
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
                    RecordCell("raw", pilotNo, roundOfView, groupNo, taskNo, compared, mismatches);

                    AddIfDifferent(
                        mismatches, "raw", pilotNo, roundOfView, groupNo, result.PreNormalisationScore,
                        OracleCell(fixture, taskNo, roundOfView, groupNo, pilotNo)?.RawScore);
                }
            }
        }
    }

    private static async Task CompareRawGrainAsync(
        GliderscoreFixture fixture,
        ReplayOutcome outcome,
        Competition competition,
        IReadOnlyDictionary<EntryId, Entry> entries,
        int taskNo,
        HashSet<string> compared,
        List<GrainMismatch> mismatches)
    {
        var classDef = competition.AdoptedRules.Definition;

        foreach (var group in outcome.EntryIdBySlot.GroupBy(kv => (kv.Key.RoundNo, kv.Key.GroupNo)))
        {
            // WI-4 — the task is per ROUND (f3k-sample-comp prescribes a
            // different GS task code each round); resolve this round's task.
            var taskDef = classDef.Phases[outcome.PhaseOrdinal]
                .Tasks.Single(t => t.Code == outcome.TaskCodeByRoundNo[group.Key.RoundNo]);

            // Round-scoped bindings win per per-round-parameter-bindings-plan.md;
            // the same flattening ScoreTaskRoundHandler performs over HTTP.
            var bindings = ScoringService.FlattenParameterBindings(
                competition.ParameterBindings, outcome.PhaseOrdinal,
                outcome.RoundOrdinalByRoundNo[group.Key.RoundNo]);

            // Q1: resolve ONCE per group, exactly as ScoreGroup does.
            var resolvedTask = ParameterResolver.ResolveTask(taskDef, bindings, classDef.Parameters);

            foreach (var (slot, entryId) in group.OrderBy(kv => kv.Key.PilotNo))
            {
                var entry = entries[entryId];

                var interpretedFlights = entry.Flights
                    .Select(flight =>
                    {
                        var resolved = MeasurementDigest.Resolve(flight);
                        return FlightInterpreter.Interpret(resolvedTask, flight.Sequence, resolved.Metrics);
                    })
                    .ToImmutableArray();

                var taskResult = ScoringService.SelectFlights(entry, resolvedTask, bindings, interpretedFlights);
                taskResult = PenaltyEngine.ApplyRawPenalties(taskResult, EntryPenalties(entry), classDef.Penalties);

                var ours = GsEquivalentRaw(resolvedTask, taskResult);
                RecordCell("raw", slot.PilotNo, slot.RoundNo, slot.GroupNo, taskNo, compared, mismatches);

                AddIfDifferent(
                    mismatches, "raw", slot.PilotNo, slot.RoundNo, slot.GroupNo, ours,
                    OracleCell(fixture, taskNo, slot.RoundNo, slot.GroupNo, slot.PilotNo)?.RawScore);
            }
        }
    }

    /// <summary>
    /// Our pre-normalisation score expressed in GS's RawScore composition — see
    /// this file's header. NoResult keeps the engine's own cell (0), matching
    /// GS's placeholder-zero rows.
    ///
    /// WI-3 verification (f3j-international, drops + deductions): no entry-
    /// penalty mirroring is added here, because that fixture expresses its
    /// late-landing deduction inside `score` (a −1 rate term over a captured
    /// deduction column), so our raw is ALREADY GS-composed
    /// (time + landing − deduction) before this mapping. A fixture that
    /// authored a DeductPoints entry-scoped penalty definition would need the
    /// deduction mirrored here AND at grain 2 it could not be mirrored at all:
    /// PenaltyEngine.ApplyRawPenalties honours zeroing effects only and
    /// GetAggregatePenalties filters to TaskRound/Competition scope, so such a
    /// penalty reaches no scoring stage — see ReplayDriver.cs header.
    /// </summary>
    private static decimal GsEquivalentRaw(ResolvedTask task, TaskResult result)
    {
        if (result.State != TaskResultState.Valid || result.Selection is null)
        {
            return result.RawScore;
        }

        return result.RawScore + result.Selection.Flights.Sum(flight =>
            task.ScoreNormalised.Sum(term => EvaluatePostNormalisationTerm(term, flight.Metrics)));
    }

    /// <summary>
    /// Minimal mirror of FlightInterpreter.EvaluateTerm for the ScoreNormalised
    /// stage. EvaluateTerm is internal to Soarscore.Domain (Q1 declined widening
    /// the production surface), so the harness re-evaluates ONLY the term kinds
    /// the corpus's option-2 fixtures use there — LookupTerm and ConstantTerm —
    /// and refuses anything else loudly rather than guessing.
    /// </summary>
    private static decimal EvaluatePostNormalisationTerm(ScoreTerm term, IReadOnlyDictionary<string, MeasuredValue> metrics) =>
        term switch
        {
            ConstantTerm constant => constant.Value,
            LookupTerm lookup => EvaluateLookup(lookup, metrics),
            _ => throw new NotSupportedException(
                $"ScoreNormalised term kind {term.GetType().Name} is not supported by the comparator yet "
                + "(FlightInterpreter.EvaluateTerm is internal; widen this mirror with the fixture that needs it)."),
        };

    private static decimal EvaluateLookup(LookupTerm term, IReadOnlyDictionary<string, MeasuredValue> metrics)
    {
        if (!metrics.TryGetValue(term.MetricRef, out var value) || value.Number is null)
        {
            throw new InvalidOperationException(
                $"Metric '{term.MetricRef}' was never captured but the class's ScoreNormalised terms read it.");
        }

        foreach (var row in term.Rows)
        {
            if (row.UpTo is null || value.Number.Value <= row.UpTo.Value)
            {
                return row.Points;
            }
        }

        return 0m;
    }

    // ------------------------------------------------------------- grain 2

    private static async Task CompareNormalisedGrainAsync(
        GliderscoreFixture fixture,
        ReplayOutcome outcome,
        HttpClient client,
        IReadOnlyDictionary<EntryId, Entry> entries,
        int taskNo,
        HashSet<string> compared,
        List<GrainMismatch> mismatches,
        Dictionary<CompetitorId, List<TaskRoundScore>> cellsByCompetitor)
    {
        var groupByGroupId = outcome.GroupIdByRoundAndGroup.ToDictionary(kv => kv.Value, kv => kv.Key);
        var pilotByCompetitor = outcome.CompetitorByPilotNo.ToDictionary(kv => kv.Value, kv => kv.Key);

        foreach (var roundNo in outcome.RoundOrdinalByRoundNo.Keys.OrderBy(n => n))
        {
            var views = await GetAsync<IReadOnlyList<GroupScoreView>>(
                client,
                $"/task-round-result?competitionRef={outcome.CompetitionId.Value}"
                + $"&phaseOrdinal={outcome.PhaseOrdinal}"
                + $"&roundOrdinal={outcome.RoundOrdinalByRoundNo[roundNo]}"
                + "&taskRoundOrdinal=1");

            foreach (var view in views)
            {
                var (roundOfView, groupNo) = groupByGroupId[view.GroupRef];

                // reflight-aggregate-destination.md WI-4 (trap 10): ALL rows
                // compare, not just Original ones — a make-up row's oracle
                // cell sits at its HOSTING (round, group) (keyFormat is
                // RoundNo-keyed), where GS scored it, even though its score
                // aggregates into the counts-for round's slot.
                foreach (var result in view.Results)
                {
                    var pilotNo = pilotByCompetitor[result.CompetitorRef];
                    RecordCell("normalised", pilotNo, roundOfView, groupNo, taskNo, compared, mismatches);

                    AddIfDifferent(
                        mismatches, "normalised", pilotNo, roundOfView, groupNo, result.RawScore,
                        OracleCell(fixture, taskNo, roundOfView, groupNo, pilotNo)?.NormalisedScore);

                    // WI-5 — the cell as ScoreCompetition would arrange it,
                    // destination-aware per reflight-aggregate-destination.md
                    // WI-4: the TaskRoundScore keys to the entry's
                    // CountsForRoundOrdinal ?? its own round (D6 — the make-up
                    // normalises in the hosting group but aggregates into the
                    // destination round's slot; PhaseAggregator matches it
                    // against the destination round's walk). Destinations are
                    // read from the already-loaded entry streams — the task-
                    // round view carries none (trap 9: EntrySummary stays
                    // coordinate-only). The task code/task-round ordinal come
                    // from the HOSTING task-round; a second cell for one
                    // DESTINATION would mean two live entries collapsed into
                    // one slot — fail loudly here rather than let Aggregate's
                    // FirstOrDefault pick one silently.
                    var entry = entries[outcome.EntryIdBySlot[(roundOfView, groupNo, pilotNo)]];
                    var cell = new TaskRoundScore(
                        outcome.TaskCodeByRoundNo[roundOfView],
                        entry.CountsForRoundOrdinal ?? outcome.RoundOrdinalByRoundNo[roundOfView],
                        TaskOrdinal: 1,
                        result.RawScore);

                    if (!cellsByCompetitor.TryGetValue(result.CompetitorRef, out var cells))
                    {
                        cellsByCompetitor[result.CompetitorRef] = cells = [];
                    }

                    if (cells.Any(c => c.RoundOrdinal == cell.RoundOrdinal && c.TaskOrdinal == cell.TaskOrdinal))
                    {
                        throw new InvalidOperationException(
                            $"Fixture '{fixture.Slug}': pilot {pilotNo} has two normalised cells for destination "
                            + $"round {cell.RoundOrdinal} (hosting round {roundOfView}) — the destination-aware law "
                            + "(D6) should have refused or collapsed that shape.");
                    }

                    cells.Add(cell);
                }
            }
        }
    }

    // ------------------------------------------------------------- grain 3

    private static void CompareRankingGrain(
        GliderscoreFixture fixture,
        ReplayOutcome outcome,
        CompetitionScoreView finalScores,
        List<GrainMismatch> mismatches)
    {
        var pilotByCompetitor = outcome.CompetitorByPilotNo.ToDictionary(kv => kv.Value, kv => kv.Key);

        var placingByPilot = finalScores.Scores
            .Where(s => s.Placing.HasValue)
            .ToDictionary(s => pilotByCompetitor[s.CompetitorRef], s => s.Placing!.Value);

        // Our placing n must match oracle rank "n"/"=n"; and every rank-n tie
        // group must contain EXACTLY the set of pilots we place at n.
        var oraclePilotsAtPlace = new Dictionary<int, List<long>>();

        foreach (var rank in fixture.ExpectedResult.Ranks)
        {
            var place = int.Parse(rank.Rank.TrimStart('='));

            if (!oraclePilotsAtPlace.TryGetValue(place, out var pilots))
            {
                pilots = [];
                oraclePilotsAtPlace[place] = pilots;
            }

            pilots.Add(rank.PilotNo);

            if (!placingByPilot.TryGetValue(rank.PilotNo, out var placing))
            {
                mismatches.Add(new GrainMismatch("ranking", rank.PilotNo, 0, 0, null, place,
                    $"oracle rank '{rank.Rank}' but we recorded no placing (disqualified or absent)."));
            }
            else if (placing != place)
            {
                mismatches.Add(new GrainMismatch("ranking", rank.PilotNo, 0, 0, placing, place,
                    $"oracle rank '{rank.Rank}' but we placed {placing}."));
            }
        }

        var ourPilotsAtPlace = placingByPilot
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToHashSet());

        foreach (var (place, oraclePilots) in oraclePilotsAtPlace)
        {
            var ours = ourPilotsAtPlace.GetValueOrDefault(place, []);

            if (!ours.SetEquals(oraclePilots))
            {
                foreach (var pilotNo in oraclePilots.Union(ours).Order())
                {
                    var inOracle = oraclePilots.Contains(pilotNo);
                    var inOurs = ours.Contains(pilotNo);

                    if (inOracle != inOurs)
                    {
                        mismatches.Add(new GrainMismatch(
                            "ranking", pilotNo, 0, 0, inOurs ? place : null, inOracle ? place : null,
                            $"'={place}' tie-group membership differs: oracle {(inOracle ? "includes" : "excludes")} "
                            + $"pilot {pilotNo}, ours {(inOurs ? "includes" : "excludes")} them."));
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------ conservation

    /// <summary>
    /// WI-5 self-check 2 — conservation, per competitor, EXACTLY:
    ///
    ///   Σ our grain-2 normalised cells        (every /task-round-result row,
    ///                                         destination-keyed per the entry's
    ///                                         CountsFor ?? own round)
    /// − Σ contributions of the engine's dropped cells
    /// − aggregate-penalty deductions         (PenaltyEngine over the subject-filtered
    ///                                         competition penalties — f3k's −100s)
    /// == the competitor's /competition-result Score,
    ///
    /// with the Disqualified flags agreeing. See this file's header for why it
    /// is stated purely over our data (ledgered phantom cells stand on neither
    /// side) and what it catches.
    /// </summary>
    private static IReadOnlyList<ConservationBreak> CheckConservation(
        ReplayOutcome outcome,
        Competition competition,
        CompetitionScoreView finalScores,
        IReadOnlyDictionary<CompetitorId, List<TaskRoundScore>> cellsByCompetitor)
    {
        if (cellsByCompetitor.Count == 0)
        {
            return [];
        }

        var classDef = competition.AdoptedRules.Definition;
        var phaseDefinition = classDef.Phases[outcome.PhaseOrdinal];

        // The round structure exactly as ScoreCompetition walks it — including
        // its write-side state collapse (Drawn/InProgress score as Complete).
        var phase = competition.Phases.Single(p => p.Ordinal == outcome.PhaseOrdinal);
        var rounds = phase.Rounds.OrderBy(r => r.Ordinal)
            .Select(round => new RoundData(
                round.Ordinal,
                round.TaskRounds.OrderBy(tr => tr.Ordinal)
                    .Select(tr => new TaskRoundData(
                        tr.Ordinal,
                        tr.TaskRef,
                        tr.State switch
                        {
                            // The write-side state collapse ScoreCompetition performs.
                            CompetitionTaskRoundState.Annulled => ScoringTaskRoundState.Annulled,
                            CompetitionTaskRoundState.Complete => ScoringTaskRoundState.Complete,
                            CompetitionTaskRoundState.Drawn or CompetitionTaskRoundState.InProgress =>
                                ScoringTaskRoundState.Complete,
                            _ => throw new ArgumentOutOfRangeException(
                                nameof(competition), tr.State, "Unknown TaskRoundState."),
                        }))
                    .ToImmutableArray()))
            .ToImmutableArray();

        var breaks = new List<ConservationBreak>();
        var finalByCompetitor = finalScores.Scores.ToDictionary(s => s.CompetitorRef, s => s);

        foreach (var (competitorId, cells) in cellsByCompetitor)
        {
            // Same ref-string convention as ScoreCompetition: Entry.CompetitorRef /
            // view CompetitorRef .ToString() on both sides of every comparison.
            var competitorRef = competitorId.ToString();

            var allScores = cells
                .Select((cell, index) => (cell, index))
                .ToDictionary(x => $"{x.cell.RoundOrdinal}|{x.cell.TaskOrdinal}|{x.index}", x => x.cell);

            // The engine's own aggregation AND its own drop decision — no
            // harness re-implementation of tie-breaks (story trap 2).
            var phaseScores = ScoringService.Aggregate(competitorRef, phaseDefinition, rounds, allScores);

            // Mirrors ScoringService.GetAggregatePenalties (private there):
            // TaskRound/Competition-scoped penalties whose subject is this
            // competitor, grouped by infraction type — one recorded Penalty is
            // one occurrence. PenaltyEngine does the arithmetic.
            var aggregatePenalties = competition.Penalties
                .Where(p => p.Scope is PenaltyScope.TaskRound or PenaltyScope.Competition)
                .Where(p => p.CompetitorRef is { } subject && subject.ToString() == competitorRef)
                .GroupBy(p => p.InfractionType)
                .Select(g => new RecordedPenalty(g.Key, g.Count()))
                .ToImmutableArray();

            var applied = PenaltyEngine.ApplyAggregatePenalties(
                phaseScores.Aggregate, aggregatePenalties, classDef.Penalties);

            var droppedSum = phaseScores.DroppedScores.Sum(s => s.Score);
            var expectedFinal = phaseScores.Aggregate - applied.Deduction;

            if (!finalByCompetitor.TryGetValue(competitorId, out var final))
            {
                breaks.Add(new ConservationBreak(
                    competitorRef,
                    cells.Sum(c => c.Score),
                    droppedSum,
                    phaseScores.Aggregate,
                    applied.Deduction,
                    expectedFinal,
                    ActualFinal: 0m,
                    ExpectedDisqualified: applied.Disqualified,
                    ActualDisqualified: false));
            }
            else if (expectedFinal != final.Score || applied.Disqualified != final.Disqualified)
            {
                breaks.Add(new ConservationBreak(
                    competitorRef,
                    cells.Sum(c => c.Score),
                    droppedSum,
                    phaseScores.Aggregate,
                    applied.Deduction,
                    expectedFinal,
                    final.Score,
                    applied.Disqualified,
                    final.Disqualified));
            }
        }

        return breaks;
    }

    // -------------------------------------------------------------- ledger

    private static IEnumerable<GrainMismatch> SubtractLedger(GliderscoreFixture fixture, IEnumerable<GrainMismatch> mismatches) =>
        mismatches.Where(m => !fixture.Divergences.Any(d =>
            d.Grain.Equals(m.Grain, StringComparison.OrdinalIgnoreCase)
            && (d.Round is null || d.Round == m.RoundNo)
            && (d.Group is null || d.Group == m.GroupNo)
            && (d.PilotNo is null || d.Covers(m.PilotNo))));

    // ------------------------------------------------------------ plumbing

    private static void RecordCell(
        string grain, long pilotNo, int roundNo, int groupNo, int taskNo,
        HashSet<string> compared, List<GrainMismatch> mismatches)
    {
        if (!compared.Add($"{taskNo}/{roundNo}/{groupNo}/0/{pilotNo}"))
        {
            mismatches.Add(new GrainMismatch(grain, pilotNo, roundNo, groupNo, null, null,
                "cell compared twice — duplicate replay slots for one (round, group, pilot)."));
        }
    }

    private static void AddIfDifferent(
        List<GrainMismatch> mismatches,
        string grain, long pilotNo, int roundNo, int groupNo, decimal ours, decimal? expected)
    {
        if (expected is null)
        {
            mismatches.Add(new GrainMismatch(grain, pilotNo, roundNo, groupNo, ours, null,
                "no oracle cell for this (round, group, pilot)."));
        }
        else if (ours != expected.Value)
        {
            mismatches.Add(new GrainMismatch(grain, pilotNo, roundNo, groupNo, ours, expected, "exact-decimal mismatch."));
        }
    }

    /// <summary>Every oracle cell must have been compared by EVERY grain — absence is a harness bug, surfaced as a mismatch.</summary>
    private static void EnsureOracleCoverage(
        Dictionary<string, ExpectedCell>.KeyCollection oracleKeys, HashSet<string> compared, string grain, List<GrainMismatch> mismatches)
    {
        foreach (var key in oracleKeys)
        {
            if (!compared.Contains(key))
            {
                var parts = key.Split('/');
                mismatches.Add(new GrainMismatch(
                    grain, long.Parse(parts[4]), int.Parse(parts[1]), int.Parse(parts[2]), null, null,
                    $"oracle cell {key} was never compared — replay produced no slot for it."));
            }
        }
    }

    private static ExpectedCell? OracleCell(GliderscoreFixture fixture, int taskNo, int roundNo, int groupNo, long pilotNo) =>
        fixture.ExpectedScores.Scores.GetValueOrDefault($"{taskNo}/{roundNo}/{groupNo}/0/{pilotNo}");

    /// <summary>Mirrors ScoringService.GetEntryPenalties (private there): Flight/Entry-scoped penalties grouped by infraction type.</summary>
    private static ImmutableArray<RecordedPenalty> EntryPenalties(Entry entry) =>
        entry.Penalties
            .Where(p => p.Scope is PenaltyScope.Flight or PenaltyScope.Entry)
            .GroupBy(p => p.InfractionType)
            .Select(g => new RecordedPenalty(g.Key, g.Count()))
            .ToImmutableArray();

    private static async Task<Competition> LoadCompetitionAsync(IEventStore eventStore, ReplayOutcome outcome)
    {
        var read = await eventStore.ReadStreamAsync(outcome.CompetitionId.Value, 0);

        if (read.IsFailure)
        {
            throw new InvalidOperationException($"Could not read competition stream: {read.Code} {read.Message}");
        }

        return read.Value.Aggregate(
            (Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
    }

    private static async Task<IReadOnlyDictionary<EntryId, Entry>> LoadEntriesAsync(IEventStore eventStore, ReplayOutcome outcome)
    {
        var entries = new Dictionary<EntryId, Entry>();

        foreach (var entryId in outcome.EntryIdBySlot.Values.Distinct())
        {
            var read = await eventStore.ReadStreamAsync(entryId.Value, 0);

            if (read.IsFailure)
            {
                throw new InvalidOperationException($"Could not read entry stream {entryId.Value}: {read.Code} {read.Message}");
            }

            entries[entryId] = read.Value.Aggregate(
                (Entry?)null, (current, e) => Entry.Apply(current, (EntryEvent)e))!;
        }

        return entries;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Comparator GET {url} returned {(int)response.StatusCode}: {body}");
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(body, ApiClient.Options)!;
    }
}
