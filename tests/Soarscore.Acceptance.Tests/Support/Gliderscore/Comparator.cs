// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-1 — the
// compare half of the steel thread (decision D6): exact decimal comparison at
// three grains, NO tolerance anywhere, minus a committed per-fixture divergence
// ledger that starts EMPTY.
//
//   grain 1 (raw)     — GS's persisted Scores.RawScore vs our pre-normalisation
//                       score, computed IN-PROCESS per Q1: the harness folds
//                       the Competition and Entry streams through the direct
//                       provider (AcceptanceFixture.EventStore) and replays the
//                       public granular pipeline exactly as ScoringService.
//                       ScoreGroup does before NormalisationEngine —
//                       ParameterResolver.ResolveTask → MeasurementDigest.Resolve
//                       per flight → FlightInterpreter.Interpret →
//                       ScoringService.SelectFlights → PenaltyEngine.ApplyRawPenalties
//                       — taking TaskResult.RawScore before any normalisation.
//                       One semantic mapping is applied and documented below
//                       (GsEquivalentRaw): under the story's D3 arrangement the
//                       landing lookup lives in ScoreNormalised, while GS composes
//                       landing INTO its persisted RawScore BEFORE normalising
//                       (arithmetic story, divergence D1 — NS = NormTime + landing).
//                       The comparator therefore adds the ScoreNormalised terms'
//                       contributions onto our pre-normalisation raw so both sides
//                       speak GS's composition; the engine's own evaluation of
//                       those same terms is exercised end-to-end by grain 2.
//                       WI-4: the task definition resolves PER ROUND (f3k-
//                       sample-comp prescribes a different GS task each round);
//                       its fixtures carry no ScoreNormalised terms, so there
//                       GsEquivalentRaw passes our raw straight through.
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

using System.Collections.Immutable;
using System.Net.Http.Json;
using Soarscore.Application;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;

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

public sealed record ComparisonReport(
    IReadOnlyList<GrainMismatch> RawMismatches,
    IReadOnlyList<GrainMismatch> NormalisedMismatches,
    IReadOnlyList<GrainMismatch> RankingMismatches,
    int RawCellsCompared,
    int NormalisedCellsCompared,
    int RankingPilotsCompared,
    int OracleCells)
{
    public bool AllGrainsExact =>
        RawMismatches.Count == 0 && NormalisedMismatches.Count == 0 && RankingMismatches.Count == 0;

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

        await CompareRawGrainAsync(fixture, outcome, competition, entries, taskNos[0], comparedRaw, rawMismatches);
        await CompareNormalisedGrainAsync(fixture, outcome, client, taskNos[0], comparedNormalised, normalisedMismatches);
        await CompareRankingGrain(fixture, outcome, client, rankingMismatches);

        // Ledgered divergences are SUBTRACTED (D6); the remainder must be empty.
        // Coverage is enforced symmetrically: an oracle cell we never compared,
        // or an our-cell with no oracle counterpart, is itself a mismatch.
        EnsureOracleCoverage(fixture.ExpectedScores.Scores.Keys, comparedRaw, "raw", rawMismatches);
        EnsureOracleCoverage(fixture.ExpectedScores.Scores.Keys, comparedNormalised, "normalised", normalisedMismatches);

        var rawRemainder = SubtractLedger(fixture, rawMismatches).ToList();
        var normalisedRemainder = SubtractLedger(fixture, normalisedMismatches).ToList();
        var rankingRemainder = SubtractLedger(fixture, rankingMismatches).ToList();

        return new ComparisonReport(
            RawMismatches: rawRemainder,
            NormalisedMismatches: normalisedRemainder,
            RankingMismatches: rankingRemainder,
            RawCellsCompared: comparedRaw.Count,
            NormalisedCellsCompared: comparedNormalised.Count,
            RankingPilotsCompared: fixture.ExpectedResult.Ranks.Length,
            OracleCells: fixture.ExpectedScores.Scores.Count);
    }

    // ------------------------------------------------------------- grain 1

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
        int taskNo,
        HashSet<string> compared,
        List<GrainMismatch> mismatches)
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

                foreach (var result in view.Results.Where(r => r.Role == ReflightRole.Original))
                {
                    var pilotNo = pilotByCompetitor[result.CompetitorRef];
                    RecordCell("normalised", pilotNo, roundOfView, groupNo, taskNo, compared, mismatches);

                    AddIfDifferent(
                        mismatches, "normalised", pilotNo, roundOfView, groupNo, result.RawScore,
                        OracleCell(fixture, taskNo, roundOfView, groupNo, pilotNo)?.NormalisedScore);
                }
            }
        }
    }

    // ------------------------------------------------------------- grain 3

    private static async Task CompareRankingGrain(
        GliderscoreFixture fixture,
        ReplayOutcome outcome,
        HttpClient client,
        List<GrainMismatch> mismatches)
    {
        var view = await GetAsync<CompetitionScoreView>(
            client, $"/competition-result?competitionRef={outcome.CompetitionId.Value}");
        var pilotByCompetitor = outcome.CompetitorByPilotNo.ToDictionary(kv => kv.Value, kv => kv.Key);

        var placingByPilot = view.Scores
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
