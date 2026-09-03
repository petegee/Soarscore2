// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-1 — the
// compare half of the steel thread (decision D6): exact decimal comparison at
// three grains, NO tolerance anywhere, minus a committed per-fixture divergence
// ledger that starts EMPTY.
//
//   grain 1 (raw)     — GS's persisted Scores.RawScore vs our composed
//                       pre-normalisation value. ONE mechanism serves ALL
//                       fixtures (http-grain-one-metric-bridge.md D1–D5): per
//                       round, GET /task-round-result — the same plumbing as
//                       grain 2 — takes each row's preNormalisationScore (the
//                       engine's raw score after raw penalties, captured
//                       entering Normalise), and for Valid rows the resolved
//                       round task's ScoreNormalised terms are evaluated
//                       against each slot's DECODED flight metrics and added
//                       onto it. The metrics come from the replayed entry
//                       streams — every flight of the slot's entry, D2-guarded
//                       to be selection-equivalent, decoded exactly as
//                       FlightInterpreter.Interpret builds them (resolved
//                       metrics + the flight.sequence intrinsic) without
//                       calling Interpret. One semantic mapping is applied:
//                       under the arithmetic story's D1 arrangement the
//                       landing lookup lives in ScoreNormalised, while GS
//                       composes landing INTO its persisted RawScore BEFORE
//                       normalising
//                       (resolve-gliderscore-scoring-arithmetic.md D1 —
//                       NS = NormTime + landing). The comparator therefore
//                       adds the ScoreNormalised terms' contributions onto the
//                       fetched pre-normalisation raw so both sides speak GS's
//                       composition; the engine's own evaluation of those same
//                       terms is exercised end-to-end by grain 2. Raw-stage
//                       entry-penalty deductions remain parked
//                       (kanban/backlog/entry-scoped-deduct-points-penalties-inert.md);
//                       if one ever lands, that is a new triage.
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

/// <summary>
/// teams-mvp.md WI-9 — one team-grain deviation: a standing that does not
/// match the MVP classification contract applied to the oracle-verified
/// individual result (contributors, totals, tie-break evidence, member
/// states, order or shared places). Unlike the score grains there is no
/// GliderScore team-standings oracle in the corpus, and no ledger entry ever
/// excuses this grain: where the fixture's declared team method is not the
/// MVP's, the comparison does not run at all (T1), so a mismatch here is
/// always a defect, never a triaged divergence.
/// </summary>
public sealed record TeamMismatch(string Team, string Detail);

public sealed record ComparisonReport(
    IReadOnlyList<GrainMismatch> RawMismatches,
    IReadOnlyList<GrainMismatch> NormalisedMismatches,
    IReadOnlyList<GrainMismatch> RankingMismatches,
    IReadOnlyList<TeamMismatch> TeamGrainMismatches,
    IReadOnlyList<ConservationBreak> ConservationBreaks,
    int RawCellsCompared,
    int NormalisedCellsCompared,
    int RankingPilotsCompared,
    int OracleCells,
    int TeamsCompared = 0)
{
    public bool AllGrainsExact =>
        RawMismatches.Count == 0
        && NormalisedMismatches.Count == 0
        && RankingMismatches.Count == 0
        && TeamGrainMismatches.Count == 0;

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

        // WI-9 — the team grain's deviations are prose-shaped (a contributor
        // set, a tie-break value), so they ride the same report under the
        // score-grain table rather than being forced into its numeric columns.
        if (TeamGrainMismatches.Count > 0)
        {
            lines.Add("");
            lines.Add($"team grain — {TeamGrainMismatches.Count} unledgered mismatch(es) over {TeamsCompared} standing(s):");
            lines.AddRange(TeamGrainMismatches
                .OrderBy(m => m.Team, StringComparer.Ordinal)
                .ThenBy(m => m.Detail, StringComparer.Ordinal)
                .Select(m => $"  team {m.Team}: {m.Detail}"));
        }

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

        // WI-2 (http-grain-one-metric-bridge.md D5): the classification split
        // is closed — EVERY fixture routes through the one HTTP bridge, which
        // composes the ScoreNormalised contribution onto the fetched
        // preNormalisationScore (D1).
        await CompareRawGrainViaHttpAsync(
            fixture, outcome, competition, entries, client, taskNos[0], comparedRaw, rawMismatches);
        await CompareNormalisedGrainAsync(
            fixture, outcome, client, entries, taskNos[0], comparedNormalised, normalisedMismatches, cellsByCompetitor);

        // One fetch serves both the ranking grain and the conservation check.
        var finalScores = await GetAsync<CompetitionScoreView>(
            client, $"/competition-result?competitionRef={outcome.CompetitionId.Value}");
        CompareRankingGrain(fixture, outcome, finalScores, rankingMismatches);

        // teams-mvp.md WI-9 — the team grain, only where semantics overlap
        // (the fixture declared team scoring with the MVP's own method).
        var (teamMismatches, derivedStandings) =
            await CompareTeamGrainAsync(fixture, outcome, competition, finalScores, client);

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
            teamMismatches,
            conservationBreaks,
            comparedRaw.Count,
            comparedNormalised.Count,
            fixture.ExpectedResult.Ranks.Length,
            fixture.ExpectedScores.Scores.Count,
            teamMismatches.Count == 0 && derivedStandings is { } standings ? standings.Standings.Length : 0);
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
        IReadOnlyList<TeamMismatch> teamMismatches,
        IReadOnlyList<ConservationBreak> conservationBreaks,
        int rawCellsCompared,
        int normalisedCellsCompared,
        int rankingPilotsCompared,
        int oracleCells,
        int teamsCompared = 0)
    {
        var rawRemainder = SubtractLedger(fixture, rawMismatches).ToList();
        var normalisedRemainder = SubtractLedger(fixture, normalisedMismatches).ToList();
        var rankingRemainder = SubtractLedger(fixture, rankingMismatches).ToList();

        return new ComparisonReport(
            RawMismatches: rawRemainder,
            NormalisedMismatches: normalisedRemainder,
            RankingMismatches: rankingRemainder,
            TeamGrainMismatches: teamMismatches,
            ConservationBreaks: conservationBreaks,
            RawCellsCompared: rawCellsCompared,
            NormalisedCellsCompared: normalisedCellsCompared,
            RankingPilotsCompared: rankingPilotsCompared,
            OracleCells: oracleCells,
            TeamsCompared: teamsCompared);
    }

    // ------------------------------------------------------------- grain 1

    /// <summary>
    /// Grain 1 over HTTP — the ONE mechanism for ALL fixtures
    /// (http-grain-one-metric-bridge.md D1–D5): one GET /task-round-result per
    /// round — the same plumbing as grain 2 — reading each row's
    /// PreNormalisationScore, composing the ScoreNormalised terms'
    /// contributions over the slot entry's decoded flight metrics (D2-guarded
    /// to be selection-equivalent), and comparing exactly against the oracle
    /// RawScore. ALL rows compare, not just Original ones (trap 10): a make-up
    /// row's oracle cell sits at its HOSTING (round, group), exactly as grain
    /// 2 treats it, and the fetched row universe equals outcome.EntryIdBySlot
    /// by construction (D6.5 of the prior story) — so RecordCell bookkeeping
    /// and EnsureOracleCoverage remain honest.
    /// </summary>
    private static async Task CompareRawGrainViaHttpAsync(
        GliderscoreFixture fixture,
        ReplayOutcome outcome,
        Competition competition,
        IReadOnlyDictionary<EntryId, Entry> entries,
        HttpClient client,
        int taskNo,
        HashSet<string> compared,
        List<GrainMismatch> mismatches)
    {
        var classDef = competition.AdoptedRules.Definition;
        var groupByGroupId = outcome.GroupIdByRoundAndGroup.ToDictionary(kv => kv.Value, kv => kv.Key);
        var pilotByCompetitor = outcome.CompetitorByPilotNo.ToDictionary(kv => kv.Value, kv => kv.Key);

        // D3 — the task resolves PER ROUND (WI-4 of the prior story: f3k
        // prescribes a different task each round); cache per round number.
        var resolvedTaskByRoundNo = new Dictionary<int, ResolvedTask>();

        foreach (var roundNo in outcome.RoundOrdinalByRoundNo.Keys.OrderBy(n => n))
        {
            var taskDef = classDef.Phases[outcome.PhaseOrdinal]
                .Tasks.Single(t => t.Code == outcome.TaskCodeByRoundNo[roundNo]);

            // Round-scoped bindings win per per-round-parameter-bindings-plan.md;
            // the same flattening ScoreTaskRoundHandler performs over HTTP.
            var bindings = ScoringService.FlattenParameterBindings(
                competition.ParameterBindings, outcome.PhaseOrdinal,
                outcome.RoundOrdinalByRoundNo[roundNo]);

            var resolvedTask = ParameterResolver.ResolveTask(taskDef, bindings, classDef.Parameters);
            resolvedTaskByRoundNo[roundNo] = resolvedTask;

            // D2.3 — target-clamp guard (http-grain-one-metric-bridge.md#WI-1):
            // the engine evaluates ScoreNormalised over metrics AFTER
            // ApplyTargets/ClampAndRecompute rewrote them, while this bridge
            // decodes unclamped metrics from the entry stream. Refuse rather
            // than guess; never reached by the corpus.
            if (!resolvedTask.ScoreNormalised.IsEmpty
                && TargetBearing(resolvedTask.Flights, out var selectionKind))
            {
                throw new NotSupportedException(
                    $"Fixture '{fixture.Slug}': task '{resolvedTask.Code}' (round {roundNo}) carries ScoreNormalised "
                    + $"terms over a target-bearing flight selection ({selectionKind}) — the bridge decodes UNCLAMPED "
                    + "metrics, so exact composition is impossible; widen the harness or expose the selection over HTTP.");
            }

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

                    // D1 — the slot's entry, keyed by the hosting (round, group, pilot).
                    var entry = entries[outcome.EntryIdBySlot[(roundOfView, groupNo, pilotNo)]];

                    // D1 — composed = fetched preNormalisationScore + the
                    // ScoreNormalised contribution, gated on the row state ONLY
                    // (trap 1: the row state is the only gate; NoResult
                    // contributes 0 even with flights on the entry).
                    var contribution = 0m;

                    if (result.State == TaskResultState.Valid)
                    {
                        var roundResolvedTask = resolvedTaskByRoundNo[roundOfView];

                        // D2.2 — contradiction guard: Valid with a flight-less
                        // entry is impossible under FlightSelector.
                        if (entry.Flights.IsEmpty)
                        {
                            throw new InvalidOperationException(
                                $"Fixture '{fixture.Slug}': slot (round {roundOfView}, group {groupNo}, pilot {pilotNo}) "
                                + "is Valid but its entry holds no flights — FlightSelector cannot produce that shape.");
                        }

                        // D2.1 — flight-count guard: with at most one flight
                        // every selection kind yields exactly that flight, so
                        // all-flights ≡ selected-flights; with an EMPTY
                        // ScoreNormalised the sum is empty and every selection
                        // is equivalent by construction (the F3K corpus's
                        // multi-flight tasks are all scoreNormalised-free).
                        // AllFlights carries no targets and needs no guard.
                        if (!roundResolvedTask.ScoreNormalised.IsEmpty
                            && roundResolvedTask.Flights is not AllFlights && entry.Flights.Length > 1)
                        {
                            throw new NotSupportedException(
                                $"Fixture '{fixture.Slug}': slot (round {roundOfView}, group {groupNo}, pilot {pilotNo}) "
                                + $"holds {entry.Flights.Length} flights but task '{roundResolvedTask.Code}' selects "
                                + $"{roundResolvedTask.Flights.GetType().Name} — the all-flights bridge composition "
                                + "cannot be proven selection-equivalent.");
                        }

                        // D4 — build each flight's metrics exactly as
                        // FlightInterpreter.Interpret does (resolved metrics +
                        // the flight.sequence intrinsic) WITHOUT calling
                        // Interpret: evaluating raw score terms is not the
                        // bridge's business, and the engine's step-7 metrics
                        // are these regardless of flightValidWhen (trap 2:
                        // no per-flight validity gating — the row state is the
                        // only gate).
                        foreach (var flight in entry.Flights)
                        {
                            var metrics = new Dictionary<string, MeasuredValue>(
                                MeasurementDigest.Resolve(flight).Metrics)
                            {
                                ["flight.sequence"] = MeasuredValue.Of(flight.Sequence)
                            };

                            foreach (var term in roundResolvedTask.ScoreNormalised)
                            {
                                contribution += EvaluatePostNormalisationTerm(term, metrics);
                            }
                        }
                    }

                    var composed = result.PreNormalisationScore + contribution;

                    AddIfDifferent(
                        mismatches, "raw", pilotNo, roundOfView, groupNo, composed,
                        OracleCell(fixture, taskNo, roundOfView, groupNo, pilotNo)?.RawScore);
                }
            }
        }
    }

    /// <summary>
    /// D2.3 — true iff the selection is target-bearing (the engine clamps
    /// selected flights' metrics to assigned targets before evaluating
    /// ScoreNormalised). Only BestNFlights and ExactlyNInOrder carry targets.
    /// </summary>
    private static bool TargetBearing(FlightSelection selection, out string kind)
    {
        kind = selection.GetType().Name;

        return selection switch
        {
            BestNFlights bn => bn.TargetValues.Length > 0,
            ExactlyNInOrder en => en.TargetValues.Length > 0,
            _ => false,
        };
    }

    /// <summary>
    /// The harness-side mirror for the engine's ScoreNormalised stage
    /// (http-grain-one-metric-bridge.md D5): evaluates one term against a slot
    /// flight's DECODED metrics (resolved metrics + the flight.sequence
    /// intrinsic, D4) — the contribution added onto the fetched
    /// preNormalisationScore. FlightInterpreter.EvaluateTerm is internal to
    /// Soarscore.Domain, so the harness re-evaluates ONLY the term kinds the
    /// corpus uses there — LookupTerm and ConstantTerm — and refuses anything
    /// else loudly rather than guessing.
    /// </summary>
    private static decimal EvaluatePostNormalisationTerm(ScoreTerm term, IReadOnlyDictionary<string, MeasuredValue> metrics) =>
        term switch
        {
            ConstantTerm constant => constant.Value,
            LookupTerm lookup => EvaluateLookup(lookup, metrics),
            _ => throw new NotSupportedException(
                $"ScoreNormalised term kind {term.GetType().Name} is not supported by the comparator yet "
                + "(the bridge composes ScoreNormalised onto the fetched preNormalisationScore; "
                + "widen this mirror with the fixture that needs it)."),
        };

    /// <summary>
    /// LookupTerm against the decoded slot metrics (http-grain-one-metric-bridge.md
    /// D5): the first row whose UpTo bounds the value; a null UpTo row is
    /// unbounded. The missing-metric throw is the widening gate, not dead code
    /// (trap 4); the all-rows-exhausted ⇒ 0 fallback mirrors the engine's shape
    /// and stays verbatim (trap 5) — the two sides agree by construction, not
    /// by data luck.
    /// </summary>
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

    // ------------------------------------------------------------- team grain

    /// <summary>
    /// teams-mvp.md WI-9 — the team grain. Runs ONLY where semantics overlap:
    /// the fixture declared team scoring active (UseTeams=true, populated team
    /// numbers) with NbrForTeamScore == 3, which IS the MVP's fixed
    /// three-contributor method (decision 8). A different NbrForTeamScore is a
    /// different method — T1-ledgered, never emulated — and for those fixtures
    /// this grain does not run at all; a UseTeams=false fixture computes no
    /// team scores in GS either, so nothing overlaps there.
    ///
    /// The corpus carries no GliderScore team-standings output (the transcripts'
    /// Team column is display-only), so the oracle here is the MVP
    /// classification contract itself (teams-mvp.md WI-5) applied to the
    /// individual result the three score grains already proved against GS:
    /// contributor selection (score DESC → placing ASC → competitor id), the
    /// totals and tie-break evidence derived from those contributors, every
    /// member's contribution state, and the declared order with shared places.
    /// Team membership is not an input to any individual score (WI-9 property
    /// 1), so this grain can never disturb the three above it.
    /// </summary>
    /// <returns>The mismatches, plus the derived standings when a comparison
    /// ran (null when the grain was skipped) — the report's TeamsCompared.</returns>
    private static async Task<(IReadOnlyList<TeamMismatch> Mismatches, TeamClassificationResult? Standings)>
        CompareTeamGrainAsync(
            GliderscoreFixture fixture,
            ReplayOutcome outcome,
            Competition competition,
            CompetitionScoreView finalScores,
            HttpClient client)
    {
        var triage = fixture.Competition.Triage;

        var overlap = triage?.UseTeams == true
            && triage?.NbrForTeamScore == 3
            && fixture.Entries.CompPilots.Rows.Any(r => (r.Team ?? 0) > 0);

        if (!overlap)
        {
            return ([], null);
        }

        var mismatches = new List<TeamMismatch>();
        var pilotByCompetitor = outcome.CompetitorByPilotNo.ToDictionary(kv => kv.Value, kv => kv.Key);

        string NameOf(CompetitorId competitor) =>
            pilotByCompetitor.TryGetValue(competitor, out var pilotNo)
                ? $"pilot {pilotNo}"
                : competitor.ToString();

        var derived = (await GetAsync<TeamStandingsView>(
                client, $"/competition-team-result?competitionRef={outcome.CompetitionId.Value}"))
            .Derived;

        if (derived is null)
        {
            mismatches.Add(new TeamMismatch("(none)",
                "the derived team standings are null although the fixture declared team scoring with the MVP's own "
                + "method (NbrForTeamScore == 3) and the replay mapped its teams."));
            return (mismatches, null);
        }

        if (derived.Method != TeamClassificationEngine.MethodBestThreeScoreSum)
        {
            mismatches.Add(new TeamMismatch("(metadata)",
                $"method '{derived.Method}' is not the MVP's '{TeamClassificationEngine.MethodBestThreeScoreSum}'."));
        }

        if (derived.SourceClassification != TeamClassificationEngine.SourceCompetitionFinalAggregate)
        {
            mismatches.Add(new TeamMismatch("(metadata)",
                $"source classification '{derived.SourceClassification}' is not "
                + $"'{TeamClassificationEngine.SourceCompetitionFinalAggregate}'."));
        }

        if (derived.Standings.Length != competition.ScoringTeams.Length)
        {
            mismatches.Add(new TeamMismatch("(universe)",
                $"{derived.Standings.Length} standings for {competition.ScoringTeams.Length} defined teams."));
        }

        var finalByCompetitor = finalScores.Scores.ToDictionary(s => s.CompetitorRef);
        var membershipsByTeam = competition.ScoringTeamMemberships
            .GroupBy(m => m.TeamRef)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var team in competition.ScoringTeams)
        {
            var standing = derived.Standings.FirstOrDefault(s => s.TeamRef == team.Id);
            if (standing is null)
            {
                mismatches.Add(new TeamMismatch(team.Name, "no standing for a defined team."));
                continue;
            }

            if (standing.Name != team.Name)
            {
                mismatches.Add(new TeamMismatch(team.Name, $"standing carries name '{standing.Name}'."));
            }

            var memberships = membershipsByTeam.GetValueOrDefault(team.Id, []);

            // Membership set — exactly the aggregate's records for this team.
            // CompetitorId carries no IComparable, so ordering goes by .Value
            // (the same key GetTeamRosters uses for its deterministic order).
            var expectedMemberRefs = memberships.Select(m => m.CompetitorRef).OrderBy(c => c.Value).ToList();
            var actualMemberRefs = standing.Members.Select(m => m.CompetitorRef).OrderBy(c => c.Value).ToList();

            if (!expectedMemberRefs.SequenceEqual(actualMemberRefs))
            {
                mismatches.Add(new TeamMismatch(team.Name,
                    $"members [{string.Join(", ", actualMemberRefs.Select(NameOf))}] but the aggregate holds "
                    + $"[{string.Join(", ", expectedMemberRefs.Select(NameOf))}]."));
            }

            // The MVP contributor contract (WI-5): the three highest individual
            // aggregate scores among eligible members holding a competition
            // placing, score DESC → placing ASC → competitor id ASC.
            var expectedContributors = memberships
                .Where(m => m.Contributes)
                .Select(m => (Ref: m.CompetitorRef, Final: finalByCompetitor.GetValueOrDefault(m.CompetitorRef)))
                .Where(x => x.Final is { } final && !final.Disqualified && final.Placing.HasValue)
                .OrderByDescending(x => x.Final!.Score)
                .ThenBy(x => x.Final!.Placing!.Value)
                .ThenBy(x => x.Ref.Value)
                .Take(3)
                .Select(x => (x.Ref, Score: x.Final!.Score, Placing: x.Final!.Placing!.Value))
                .ToList();

            var actualContributors = standing.Contributors
                .Select(c => (Ref: c.CompetitorRef, Score: c.Score, Placing: c.Placing))
                .ToList();

            if (!actualContributors.SequenceEqual(expectedContributors))
            {
                mismatches.Add(new TeamMismatch(team.Name,
                    $"contributors [{DescribeContributors(actualContributors, NameOf)}] but the contract selects "
                    + $"[{DescribeContributors(expectedContributors, NameOf)}]."));
            }

            var chosenRefs = expectedContributors.Select(c => c.Ref).ToHashSet();

            // Totals and tie-break evidence are functions of the contributors.
            if (standing.Total != expectedContributors.Sum(c => c.Score))
            {
                mismatches.Add(new TeamMismatch(team.Name,
                    $"total {standing.Total} but the contributors sum to {expectedContributors.Sum(c => c.Score)}."));
            }

            if (standing.PlacingSum != expectedContributors.Sum(c => c.Placing))
            {
                mismatches.Add(new TeamMismatch(team.Name,
                    $"placing sum {standing.PlacingSum} but the contributors' placings sum to "
                    + $"{expectedContributors.Sum(c => c.Placing)}."));
            }

            var expectedBest = expectedContributors.Count == 0
                ? (int?)null
                : expectedContributors.Min(c => c.Placing);

            if (standing.BestIndividualPlacing != expectedBest)
            {
                mismatches.Add(new TeamMismatch(team.Name,
                    $"best individual placing {standing.BestIndividualPlacing?.ToString() ?? "(none)"} but the "
                    + $"contributors' best is {expectedBest?.ToString() ?? "(none)"}."));
            }

            // Every member's contribution state, mirrored from the contract's
            // own rules (score survives withdrawal; disqualified holds no
            // placing; Contributes=false is the defending-champion case).
            foreach (var member in memberships)
            {
                var actual = standing.Members.FirstOrDefault(m => m.CompetitorRef == member.CompetitorRef);
                if (actual is null)
                {
                    continue; // already mismatched as a membership-set difference
                }

                var expectedState = ExpectedMemberState(
                    finalByCompetitor.GetValueOrDefault(member.CompetitorRef), member.Contributes, chosenRefs);

                if (actual.State != expectedState)
                {
                    mismatches.Add(new TeamMismatch(team.Name,
                        $"{NameOf(member.CompetitorRef)} holds state {actual.State} but the contract says {expectedState}."));
                }
            }
        }

        // Declared order: Total DESC → placing sum ASC → best individual
        // placing ASC (nulls last) → team name, and places follow the
        // shared-place convention over teams equal on the three rungs.
        for (var i = 1; i < derived.Standings.Length; i++)
        {
            if (CompareRungs(derived.Standings[i - 1], derived.Standings[i]) > 0)
            {
                mismatches.Add(new TeamMismatch(derived.Standings[i].Name,
                    $"stands after '{derived.Standings[i - 1].Name}' but sorts before it on the declared rungs."));
            }
        }

        var place = 1;
        var index = 0;

        while (index < derived.Standings.Length)
        {
            var end = index + 1;

            while (end < derived.Standings.Length && SameRungs(derived.Standings[index], derived.Standings[end]))
            {
                end++;
            }

            for (var k = index; k < end; k++)
            {
                if (derived.Standings[k].Placing != place)
                {
                    mismatches.Add(new TeamMismatch(derived.Standings[k].Name,
                        $"holds place {derived.Standings[k].Placing} but the shared-place convention gives {place}."));
                }
            }

            place += end - index;
            index = end;
        }

        return (mismatches, derived);
    }

    private static string DescribeContributors(
        IReadOnlyList<(CompetitorId Ref, decimal Score, int Placing)> contributors,
        Func<CompetitorId, string> nameOf) =>
        string.Join(", ", contributors.Select(c => $"{nameOf(c.Ref)}={c.Score}@{c.Placing}"));

    /// <summary>The harness-side mirror of the engine's member-state rules.</summary>
    private static TeamContributionState ExpectedMemberState(
        CompetitorFinalScoreView? final, bool eligible, HashSet<CompetitorId> chosen) =>
        final is null ? TeamContributionState.NoScoreYet
        : final.Disqualified ? TeamContributionState.Disqualified
        : !eligible ? TeamContributionState.Ineligible
        : chosen.Contains(final.CompetitorRef) ? TeamContributionState.Contributor
        : TeamContributionState.EligibleNotCounting;

    private static int CompareRungs(TeamStanding a, TeamStanding b)
    {
        var c = b.Total.CompareTo(a.Total);
        if (c != 0)
        {
            return c;
        }

        c = a.PlacingSum.CompareTo(b.PlacingSum);
        if (c != 0)
        {
            return c;
        }

        c = (a.BestIndividualPlacing.HasValue, b.BestIndividualPlacing.HasValue) switch
        {
            (true, true) => a.BestIndividualPlacing!.Value.CompareTo(b.BestIndividualPlacing!.Value),
            (true, false) => -1,
            (false, true) => 1,
            _ => 0,
        };
        if (c != 0)
        {
            return c;
        }

        return string.CompareOrdinal(a.Name, b.Name);
    }

    private static bool SameRungs(TeamStanding a, TeamStanding b) =>
        a.Total == b.Total
        && a.PlacingSum == b.PlacingSum
        && a.BestIndividualPlacing.Equals(b.BestIndividualPlacing);

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
