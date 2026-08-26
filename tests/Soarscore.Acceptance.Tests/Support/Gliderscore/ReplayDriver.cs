// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-1 — the
// replay half of the steel thread. Drives one fixture through the PUBLIC
// command surface only (every write goes over AcceptanceFixture.Client via
// ApiClient, same JSON options as the Api): publish class definition, create
// competition, register pilots, prescribe the realised draw (D5), accept,
// open an Entry for every scores-raw slot (D4), capture decoded measurements,
// complete each task-round, finalise. The only non-HTTP scoring surface in
// the whole harness is Comparator's Q1 grain, not this file.
//
// Decisions implemented here, cited by their story numbers:
//   D4 — every scores-raw row gets an Entry, including all-zero placeholder
//        rows; a flight-less entry yields NoResult ⇒ cell 0, which is what puts
//        GS's placeholder zeros into the drop-candidate pool. Only non-zero
//        inputs are captured: packed-mmss times decoded Fix-style ("500.0" =
//        300 s, arithmetic story Handoff §3), Landing > 0 as landingDistance
//        metres. Laps is ignored for duration-family fixtures (trap 6).
//   D5 — draw derivation: drop re-flight rows, dedupe phantom repeats keeping
//        the highest oracle NormalisedScore per (pilot, original round), then
//        fail loudly on any partition violation. Rounds ascending RoundNo,
//        groups ascending GroupNo, members in SeqNo order (prescribe-story
//        decision 4 — list order IS the flying order).
//   Trap 5 — a fixture with two timekeepers is refused loudly rather than
//        mis-modelled as a single-timekeeper decode.
//
// WI-3 widens two places, both cited where they happen below:
//   - round-scoped parameter bindings (f3j-international's round-1 target time,
//     oracle-reconciled knowledge that competition.json does not carry);
//   - the duration-family capture map: the two columns f3j-international's
//     score terms read are captured for every FLOWN slot including zeros,
//     because a score term over an uncaptured metric throws — while placeholder
//     rows stay flight-less exactly as D4 requires.
//
// WI-4 widens three places for f3k-sample-comp, cited where they happen:
//   - per-round task prescription: F3KTaskByRound names a GS task per round, so
//     the phase is authored ChooseFromCatalogue and every prescribed round
//     carries its TaskRef;
//   - the F3K capture map, PER GS TASK CODE (trap 6): ScrArr(0..6) = Laps,
//     Time1Mins, Time1Secs, Time2Mins, Time2Secs, Landing,
//     FlightScoreDeduction — each non-zero packed-mmss slot becomes ONE flight
//     carrying flightTime; slot order is preserved because task D pairs
//     flights positionally with descending ladder targets (ExactlyNInOrder).
//     The map and the per-task arithmetic it feeds were PROVEN against
//     expected-scores.json before this harness trusted them (90/90 raw cells);
//   - Scores.Penalty rows replay as competition-scoped penalties via
//     /record-competition-penalty — post-sum per-pilot deductions (provenance),
//     which our pipeline applies after aggregation before ranking
//     (ScoringService.GetAggregatePenalties), the same placement as GS.
//
// Deliberately NOT widened here: /record-entry-penalty calls. The WI-3 brief
// expected Scores.FlightScoreDeduction to replay as an entry-scoped DeductPoints
// penalty definition, but PenaltyEngine.ApplyRawPenalties honours only zeroing
// effects and GetAggregatePenalties filters to TaskRound/Competition scope, so
// such a penalty would change no score anywhere — and GS subtracts FltPenalty
// INSIDE RawScore pre-normalisation anyway, which an aggregate-stage deduction
// could not reproduce. The deduction is therefore part of the fixture's class
// definition (a −1 rate term over a captured deduction column) and reaches the
// engine through the ordinary score pipeline. See Comparator.cs grain-1 notes.

using System.Net.Http.Json;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

/// <summary>Everything the comparator needs to find its way around the replay.</summary>
public sealed record ReplayOutcome(
    CompetitionId CompetitionId,
    int PhaseOrdinal,
    IReadOnlyDictionary<int, string> TaskCodeByRoundNo,
    IReadOnlyDictionary<int, int> RoundOrdinalByRoundNo,
    IReadOnlyDictionary<(int RoundNo, int GroupNo), GroupId> GroupIdByRoundAndGroup,
    IReadOnlyDictionary<(int RoundNo, int GroupNo, long PilotNo), EntryId> EntryIdBySlot,
    IReadOnlyDictionary<long, CompetitorId> CompetitorByPilotNo);

public sealed class ReplayDriver(HttpClient client)
{
    private const string CdName = "Gliderscore replay harness";

    // WI-3 — round-scoped parameter bindings, keyed by fixture slug. The
    // mechanism is generic (POST /bind-parameter per entry, phase 0 = the
    // prescribed first phase); only the DATA is per-fixture.
    //
    // f3j-international: GS scored its round 1 against a 540 s target — every
    // one of the round's 22 over-target decay witnesses implies T = (t + TS)/2
    // = 540 exactly, while rounds 2-16 never decay and hold durTargetTime =
    // 600. The export carries no DurTargetTimeByRound rows at all (the table
    // is empty in the source .mdb), so this value exists nowhere in the
    // fixture's input files: it is oracle-reconciled knowledge, authored here
    // beside the capture maps rather than parsed (trap 6 precedent). The class
    // definition declares `targetTime` PerRound with default 600; this binding
    // pins round 1 to 540 before any entry opens (a round-scoped bind freezes
    // once flights exist).
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<(string Parameter, int RoundNo, decimal Value)>>
        RoundParameterBindings = new Dictionary<string, IReadOnlyList<(string, int, decimal)>>
        {
            ["f3j-international"] = [("targetTime", 1, 540m)],
        };

    public async Task<ReplayOutcome> ReplayAsync(GliderscoreFixture fixture)
    {
        // ------------------------------------------------------------ publish
        // Fails loudly here if the authored definition does not pass adoption
        // validation — which is how WI-1 proves it does.
        var contentHash = await PostAsync<string>(
            "/publish-class-definition", new PublishClassDefinition(fixture.Definition));

        // ------------------------------------------------------------- create
        // Name/emails carry a run slug so scenarios sharing one store never
        // collide (the CapturingAScoreSteps discipline). CompDate gives the dates.
        var slug = Guid.NewGuid().ToString("N");
        var compDate = DateOnly.Parse(
            fixture.Competition.Identity.CompDate.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);

        var competitionId = await PostAsync<CompetitionId>(
            "/create-competition",
            new CreateCompetition(
                $"{fixture.Competition.Identity.CompName} ({slug})",
                "Gliderscore replay",
                compDate,
                compDate,
                contentHash));

        // ----------------------------------------------------------- register
        // D-trap 9: compPilots row order; names joined from the pilots table by
        // PilotNo; emails slug-unique per run (Person.IsPlausibleEmail forbids
        // whitespace). GS StartNo numbering is irrelevant — keys are internal ids.
        var pilotNames = fixture.Entries.Pilots.Rows.ToDictionary(p => p.PilotNo, p => $"{p.FirstName} {p.LastName}");
        var competitorByPilotNo = new Dictionary<long, CompetitorId>();

        foreach (var row in fixture.Entries.CompPilots.Rows)
        {
            var personId = await PostAsync<PersonId>(
                "/register-person",
                new RegisterPerson(
                    pilotNames.GetValueOrDefault(row.PilotNo, $"Pilot {row.PilotNo}"),
                    new ContactDetails { Email = $"gliderscore-{slug}-pilot-{row.PilotNo}@example.com".ToLowerInvariant() },
                    null));
            competitorByPilotNo[row.PilotNo] = await PostAsync<CompetitorId>(
                "/register-competitor", new RegisterCompetitor(competitionId, personId));
        }

        // -------------------------------------------------------------- draw
        var keptRows = DeriveDrawRows(fixture);

        // WI-4 — the fixture's per-round GS task schedule (empty for the
        // duration-family fixtures, whose FixedSequence phases prescribe a null
        // TaskRef and repeat their single task).
        var taskByRoundNo = F3KTaskByRound(fixture);

        var prescribedRounds = keptRows
            .GroupBy(r => r.RoundNo)
            .OrderBy(g => g.Key)
            .Select(roundRows =>
            {
                var groups = roundRows
                    .GroupBy(r => r.GroupNo)
                    .OrderBy(g => g.Key)
                    .Select(g => new PrescribedGroup(
                        g.OrderBy(r => r.SeqNo).Select(r => competitorByPilotNo[r.PilotNo]).ToList()))
                    .ToList();

                return new PrescribedRound(
                    TaskRef: taskByRoundNo.GetValueOrDefault(roundRows.Key),
                    Groups: groups);
            })
            .ToList();

        await PostAsync<CompetitionId>("/prescribe-draw", new PrescribeDraw(competitionId, prescribedRounds, CdName));
        await PostAsync<CompetitionId>("/accept-draw", new AcceptDraw(competitionId));

        // Fixture ordinals are pure position math over the kept rows: rounds
        // ascending RoundNo, 1-based. Needed here already — a round-scoped
        // parameter bind names the round by this ordinal.
        var roundNosAscending = keptRows.Select(r => r.RoundNo).Distinct().OrderBy(n => n).ToList();
        var roundOrdinalByRoundNo = roundNosAscending
            .Select((roundNo, index) => (roundNo, ordinal: index + 1))
            .ToDictionary(pair => pair.roundNo, pair => pair.ordinal);

        // WI-3 — apply the fixture's round-scoped parameter binds while every
        // task-round is still Drawn and no entry exists (the decide function
        // refuses a bind into a round that has started flying). Phase 0 is the
        // prescribed first phase (Phase.Ordinal is Phases.Length at draw time).
        if (RoundParameterBindings.GetValueOrDefault(fixture.Slug) is { } binds)
        {
            foreach (var (parameter, roundNo, value) in binds)
            {
                await PostAsync<CompetitionId>(
                    "/bind-parameter",
                    new BindParameter(
                        competitionId, parameter, MeasuredValue.Of(value), CdName,
                        PhaseOrdinal: 0, RoundOrdinal: roundOrdinalByRoundNo[roundNo]));
            }
        }

        // --------------------------------------------- read back drawn structure
        // Ordinals are assigned by position at prescription time (Competition.
        // PrescribeDraw): Phase.Ordinal is Phases.Length at draw time — 0 for a
        // first phase — while Round/TaskRound/Group ordinals are 1-based. Read
        // them back rather than assume, and key everything the comparator needs
        // by FIXTURE coordinates (RoundNo/GroupNo/PilotNo), not engine ordinals.
        var view = await ApiClient.GetAsync<CompetitionView>(client, $"/competition?id={competitionId.Value}");
        var phase = view.Competition.Phases.Single();
        var roundsAscending = phase.Rounds.OrderBy(r => r.Ordinal).ToList();

        var groupIdByRoundAndGroup = new Dictionary<(int RoundNo, int GroupNo), GroupId>();
        var taskCodeByRoundNo = new Dictionary<int, string>();

        foreach (var roundNo in roundNosAscending)
        {
            var taskRound = roundsAscending[roundOrdinalByRoundNo[roundNo] - 1].TaskRounds.Single();
            taskCodeByRoundNo[roundNo] = taskRound.TaskRef;
            var groupNosAscending = keptRows
                .Where(r => r.RoundNo == roundNo)
                .Select(r => r.GroupNo).Distinct().OrderBy(n => n).ToList();

            for (var i = 0; i < groupNosAscending.Count; i++)
            {
                groupIdByRoundAndGroup[(roundNo, groupNosAscending[i])] =
                    taskRound.Groups.OrderBy(g => g.Ordinal).ElementAt(i).Id;
            }
        }

        // --------------------------------------------------- D4 cell universe
        // Outer walk is per ROUND, inner per group: a round's groups share ONE
        // task-round, and completing it closes the round to new entries
        // (openEntry.taskRoundClosed) — WI-3 exposed this with f3j-
        // international's four groups per round; single-group fixtures never
        // noticed.
        var entryIdBySlot = new Dictionary<(int RoundNo, int GroupNo, long PilotNo), EntryId>();

        foreach (var roundNo in roundNosAscending)
        {
            var roundOrdinal = roundOrdinalByRoundNo[roundNo];
            var groupNosAscending = keptRows
                .Where(r => r.RoundNo == roundNo)
                .Select(r => r.GroupNo).Distinct().OrderBy(n => n);

            foreach (var groupNo in groupNosAscending)
            {
                var groupId = groupIdByRoundAndGroup[(roundNo, groupNo)];

                foreach (var row in keptRows
                    .Where(r => r.RoundNo == roundNo && r.GroupNo == groupNo)
                    .OrderBy(r => r.SeqNo))
                {
                    var entryId = await PostAsync<EntryId>(
                        "/open-entry",
                        new OpenEntry(
                            competitionId, phase.Ordinal, roundOrdinal, 1,
                            groupId, competitorByPilotNo[row.PilotNo]));

                    entryIdBySlot[(row.RoundNo, row.GroupNo, row.PilotNo)] = entryId;

                    // Placeholder rows stay flight-less ⇒ NoResult ⇒ cell 0 (D4).
                    // A FLOWN slot opens ONE FLIGHT PER NON-ZERO SLOT VALUE —
                    // exactly one for the duration family (WI-3), up to seven
                    // for F3K's packed columns (WI-4); captures carry the
                    // flight sequence they belong to. Deliberate zeros inside a
                    // flown slot — see CaptureInputs (WI-3 widening).
                    var captures = CaptureInputs(fixture, row);

                    if (captures is not null)
                    {
                        var flights = captures
                            .GroupBy(c => c.Flight)
                            .OrderBy(g => g.Key)
                            .ToList();

                        foreach (var flight in flights)
                        {
                            await PostAsync<EntryId>("/open-flight", new OpenFlight(entryId));

                            foreach (var (metric, value, _) in flight)
                            {
                                await PostAsync<EntryId>(
                                    "/capture-measurement",
                                    new CaptureMeasurement(entryId, flight.Key, metric, MeasuredValue.Of(value)));
                            }
                        }
                    }
                }
            }

            await PostAsync<CompetitionId>(
                "/complete-task-round",
                new CompleteTaskRound(competitionId, phase.Ordinal, roundOrdinal, 1));
        }

        // WI-4 — Scores.Penalty rows are post-sum per-pilot competition
        // penalties (provenance; arithmetic story Drop-worst §6): GS subtracts
        // each pilot's penalty total from the summed kept cells afterwards.
        // One POST per occurrence row — GetAggregatePenalties counts rows per
        // infraction type and PerOccurrence accrual multiplies the points, so
        // pilot 56's two rows become one 200 deduction. Recorded before
        // finalise, though RecordPenalty imposes no finalisation gate.
        foreach (var row in fixture.ScoresRaw.Rows.Where(r => r.Penalty > 0))
        {
            await PostAsync<CompetitionId>(
                "/record-competition-penalty",
                new RecordCompetitionPenalty(
                    competitionId, CompetitionPenaltyInfractionType, PenaltyScope.Competition,
                    competitorByPilotNo[row.PilotNo], TaskRound: null, By: CdName));
        }

        await PostAsync<CompetitionId>("/finalise-competition", new FinaliseCompetition(competitionId, CdName));

        return new ReplayOutcome(
            CompetitionId: competitionId,
            PhaseOrdinal: phase.Ordinal,
            TaskCodeByRoundNo: taskCodeByRoundNo,
            RoundOrdinalByRoundNo: roundOrdinalByRoundNo,
            GroupIdByRoundAndGroup: groupIdByRoundAndGroup,
            EntryIdBySlot: entryIdBySlot,
            CompetitorByPilotNo: competitorByPilotNo);
    }

    // ------------------------------------------------------------- D5 draw

    /// <summary>The scores-raw rows that form the realised draw, after D5's filters.</summary>
    private static List<ScoresRow> DeriveDrawRows(GliderscoreFixture fixture)
    {
        // Step 1 — re-flight rows have no base-draw prescription path yet
        // (deferred-decisions.md "Draw"); WI-6 designs that mapping.
        var rows = fixture.ScoresRaw.Rows
            .Where(r => !(r.ReFlightNo > 0 || r.OriginalRoundNo != r.RoundNo))
            .ToList();

        // Step 2 — phantom repeats (GS phantom groups, f3j-international R1/G5):
        // a pilot twice in one round keeps the row whose oracle NormalisedScore
        // is highest for that (pilot, original round) — GS's best-per-original-
        // round aggregation, which is what makes the phantom neutral.
        var kept = new List<ScoresRow>();

        foreach (var group in rows.GroupBy(r => (r.RoundNo, r.PilotNo)))
        {
            kept.Add(group.Count() == 1
                ? group.First()
                : group.OrderByDescending(row => OracleNormalisedScore(fixture, row)).First());
        }

        // Step 3 — the survivor set must partition cleanly; anything else is a
        // derivation bug and fails loudly here rather than as a score diff.
        var duplicated = kept
            .GroupBy(r => (r.RoundNo, r.PilotNo))
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicated is not null)
        {
            throw new InvalidOperationException(
                $"Fixture '{fixture.Slug}': draw derivation failed — pilot {duplicated.Key.PilotNo} still appears "
                + $"{duplicated.Count()} times in round {duplicated.Key.RoundNo} after deduplication.");
        }

        return kept;
    }

    /// <summary>
    /// Oracle lookup for D5 step 2, keyed exactly as expected-scores.json's
    /// keyFormat states: {"TaskNo"}/{"RoundNo"}/{"GroupNo"}/{"ReFlightNo"}/{"PilotNo"}.
    /// A missing oracle cell ranks lowest rather than throwing — deduplication
    /// needs an ordering, not a verdict.
    /// </summary>
    private static decimal OracleNormalisedScore(GliderscoreFixture fixture, ScoresRow row) =>
        fixture.ExpectedScores.Scores.TryGetValue(
                $"{row.TaskNo}/{row.OriginalRoundNo}/{row.GroupNo}/{row.ReFlightNo}/{row.PilotNo}",
                out var cell)
            ? cell.NormalisedScore
            : 0m;

    // ---------------------------------------------------------- D4 capture

    // WI-4 — the infraction type f3k-sample-comp's class definition declares
    // for its Scores.Penalty column (post-sum per-pilot competition penalty).
    private const string CompetitionPenaltyInfractionType = "competitionPenalty";

    /// <summary>
    /// One decoded slot value: the metric it is captured under, its value in
    /// seconds, and WHICH flight of the entry carries it (1-based, slot order).
    /// The duration family yields only flight 1; F3K's packed columns become
    /// flights 1..n in ScrArr order.
    /// </summary>
    private sealed record SlotCapture(string Metric, decimal Value, int Flight);

    /// <summary>The (metric, value, flight) triples this row contributes, or null.</summary>
    private static List<SlotCapture>? CaptureInputs(GliderscoreFixture fixture, ScoresRow row) =>
        fixture.Competition.Identity.GsCompClass == "F3K"
            ? CaptureF3KInputs(fixture, row)
            : CaptureDurationInputs(fixture, row);

    /// <summary>
    /// Duration family (WI-3 capture map). A row counts as flown iff it
    /// carries a packed-mmss flight time — verified for every duration fixture
    /// so far that non-flown rows are all-zero across every Scores column.
    /// For a flown slot the two columns f3j-international's score terms READ
    /// are captured even when zero: FlightInterpreter throws on a score-term
    /// metric that was never measured, and a zero landing / zero deduction is
    /// an observed fact of the flight, not missing data. D4's "only non-zero
    /// inputs" letter gives way here; its intent (placeholders stay
    /// flight-less, nothing is manufactured) does not.
    ///
    ///   flightTime           — Time1Mins decoded Fix-style ("500.0" = 300 s,
    ///                          Handoff §3); captured when non-zero.
    ///   landingDistance      — metres; ALWAYS captured for a flown slot. The
    ///                          class lookup scores a zero as zero (GS's
    ///                          exact-match miss on the empty distance).
    ///   lateLandingDeduction — Scores.FlightScoreDeduction payload points;
    ///                          ALWAYS captured for a flown slot. durFlightPenalty=1
    ///                          selects GS's late-landing scheme, under which
    ///                          the payload is subtracted from RawScore
    ///                          pre-normalisation — expressed in the class
    ///                          definition as a −1 rate term over this metric.
    /// Laps is ignored (trap 6); Time2* would mean two timekeepers (trap 5).
    /// </summary>
    private static List<SlotCapture>? CaptureDurationInputs(GliderscoreFixture fixture, ScoresRow row)
    {
        var dur = DurFamilyRow.Of(fixture.Competition)
            ?? throw new InvalidOperationException(
                $"Fixture '{fixture.Slug}': no Dur family row — not a duration-family fixture.");

        if (dur.DurNumberOfTimekeepers != 1)
        {
            throw new NotSupportedException(
                $"Fixture '{fixture.Slug}': durNumberOfTimekeepers = {dur.DurNumberOfTimekeepers}. Two-timekeeper "
                + "fixtures are not supported by the harness yet (index.md 'Still open'; story trap 5).");
        }

        if (row.Time1Mins <= 0m)
        {
            return null;
        }

        // Only metrics the fixture's own definition declares may be captured —
        // the earlier fixtures declare no deduction column, so their flown
        // slots keep the two-capture shape.
        var declared = fixture.Definition.Phases
            .SelectMany(p => p.Tasks)
            .SelectMany(t => t.Metrics)
            .Select(m => m.Name)
            .ToHashSet();

        var captures = new List<SlotCapture>();

        if (declared.Contains("flightTime"))
        {
            captures.Add(new SlotCapture("flightTime", DecodePackedMinutesSeconds(row.Time1Mins), Flight: 1));
        }

        if (declared.Contains("landingDistance"))
        {
            captures.Add(new SlotCapture("landingDistance", row.Landing, Flight: 1));
        }

        if (declared.Contains("lateLandingDeduction"))
        {
            captures.Add(new SlotCapture("lateLandingDeduction", row.FlightScoreDeduction, Flight: 1));
        }

        return captures;
    }

    // ------------------------------------------------------- F3K capture map

    /// <summary>
    /// The per-round GS task schedule (WI-4): F3KTaskByRound's round → task
    /// code. Empty for every non-F3K fixture — their phases are FixedSequence
    /// over one task and prescribe a null TaskRef.
    /// </summary>
    private static IReadOnlyDictionary<int, string> F3KTaskByRound(GliderscoreFixture fixture) =>
        fixture.Competition.ScheduleTables?.F3KTaskByRound?.Rows.ToDictionary(r => r.RoundNo, r => r.Task)
        ?? new Dictionary<int, string>();

    /// <summary>
    /// The F3K slot-column capture map, PER GS TASK CODE (trap 6). GS packs up
    /// to seven inputs into one Scores row — ScrArr(0..6) = Laps, Time1Mins,
    /// Time1Secs, Time2Mins, Time2Secs, Landing, FlightScoreDeduction
    /// (arithmetic story, F3K section) — and CalcRawScoreF3K reads a
    /// task-specific PREFIX as flight times, each mmss-decoded. The map below
    /// was derived from that Select Case and PROVEN against expected-scores.json
    /// before this harness trusted it: replaying decode → per-task cap /
    /// ladder-clamp → sum reproduces all 90 oracle RawScore cells exactly.
    ///
    ///   G    'Best5 2:00max' — five time slots, each clamped at 120 s;
    ///   A(1) 'L1 5max in 10m' — the Laps slot alone (packed), clamped at 300 s;
    ///   F    'Best3 3:00max' — three time slots, each clamped at 180 s;
    ///   D    'Ladder (Not FAI)' — ALL SEVEN slots positionally clamped at the
    ///        ladder targets 30/45/60/75/90/105/120 s (the landing-distance and
    ///        deduction slots count as flight times here — provenance; pilot
    ///        13 R4: Landing 145→105, Deduction 200→120);
    ///   C(3) 'AllUp 3:00*5' — five time slots, each clamped at 180 s;
    ///   X    'NoTaskSet' — placeholder rounds 6–9, nothing ever captured.
    ///
    /// GS's working-window reduction (1 s per flown flight, D5) and its
    /// violation-zeroing are NOT expressible in our timing model — but they
    /// never bite this fixture's recorded data: every recorded slot set fits
    /// inside its reduced window with margin, so no cell needs ledgering.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> F3KSlotMap =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["G"] = ["Laps", "Time1Mins", "Time1Secs", "Time2Mins", "Time2Secs"],
            ["A(1)"] = ["Laps"],
            ["F"] = ["Laps", "Time1Mins", "Time1Secs"],
            ["D"] = [
                "Laps", "Time1Mins", "Time1Secs", "Time2Mins", "Time2Secs",
                "Landing", "FlightScoreDeduction"],
            ["C(3)"] = ["Laps", "Time1Mins", "Time1Secs", "Time2Mins", "Time2Secs"],
            ["X"] = [],
        };

    private static List<SlotCapture>? CaptureF3KInputs(GliderscoreFixture fixture, ScoresRow row)
    {
        var taskByRound = F3KTaskByRound(fixture);
        var taskCode = taskByRound.GetValueOrDefault(row.RoundNo)
            ?? throw new InvalidOperationException(
                $"Fixture '{fixture.Slug}': F3K schedule names no task for round {row.RoundNo}.");

        if (!F3KSlotMap.ContainsKey(taskCode))
        {
            throw new NotSupportedException(
                $"Fixture '{fixture.Slug}': round {row.RoundNo} names GS task '{taskCode}', which is not in "
                + "the F3K slot-column capture map — widen F3KSlotMap with its CalcRawScoreF3K semantics first.");
        }

        var captures = new List<SlotCapture>();
        var seenNonZero = false;
        var gapAfterNonZero = false;

        for (var i = 0; i < F3KSlotMap[taskCode].Count; i++)
        {
            var decoded = DecodePackedMinutesSeconds(ColumnValue(row, F3KSlotMap[taskCode][i]));

            if (decoded == 0m)
            {
                // Task D pairs flights POSITIONALLY with descending ladder
                // targets (ExactlyNInOrder + targetValues): a skipped slot
                // BETWEEN flown ones would shift every later flight onto the
                // wrong rung. Fail loudly rather than mis-score — every
                // recorded row in this fixture is prefix-shaped.
                if (taskCode == "D" && seenNonZero)
                {
                    gapAfterNonZero = true;
                }

                continue;
            }

            if (gapAfterNonZero)
            {
                throw new InvalidOperationException(
                    $"Fixture '{fixture.Slug}': round {row.RoundNo} pilot {row.PilotNo} has an interior zero "
                    + $"in ladder task D's slots (slot {i} follows a gap) — positional targets would mispair.");
            }

            seenNonZero = true;
            captures.Add(new SlotCapture("flightTime", decoded, Flight: captures.Count + 1));
        }

        return captures.Count == 0 ? null : captures;
    }

    private static decimal ColumnValue(ScoresRow row, string column) => column switch
    {
        "Laps" => row.Laps,
        "Time1Mins" => row.Time1Mins,
        "Time1Secs" => row.Time1Secs,
        "Time2Mins" => row.Time2Mins,
        "Time2Secs" => row.Time2Secs,
        "Landing" => row.Landing,
        "FlightScoreDeduction" => row.FlightScoreDeduction,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unknown F3K slot column."),
    };

    /// <summary>
    /// GS packed mmss.s decode with Fix-truncation toward zero
    /// (Scoring_MOD.vb GetTimeInSeconds via arithmetic story Handoff §3):
    /// seconds = Fix(v/100)·60 + (v − 100·Fix(v/100)). "500.0" → 300 s.
    /// </summary>
    private static decimal DecodePackedMinutesSeconds(decimal packed)
    {
        var minutes = Math.Truncate(packed / 100m);

        return minutes * 60m + (packed - 100m * minutes);
    }

    // ---------------------------------------------------------------- misc

    private async Task<T> PostAsync<T>(string path, object command)
    {
        using var response = await client.PostAsJsonAsync(path, command, ApiClient.Options);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Replay POST {path} returned {(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(body, ApiClient.Options)!;
    }
}
