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
// reflight-aggregate-destination.md WI-4 widens three more places, cited
// where they happen below:
//   - destination-bearing make-up rows (OriginalRoundNo ≠ RoundNo): D5 step 1
//     still keeps them out of the DRAW derivation (the prescription half stays
//     deferred), but now COLLECTS them and opens each one's entry in a
//     per-round SECOND pass — role Entitled, countsForRoundOrdinal =
//     OriginalRoundNo, reason recorded — then flies + captures it exactly as
//     any row. The two-pass order is trap 2's: the handler's Original branch
//     refuses any open once a live entry exists, so every make-up must come
//     after its competitor's Original in the same task-round; pass 1 opens
//     every regular slot, pass 2 the make-ups in SeqNo order.
//   - SyntheticSlots splits per trap 3: the two make-up fixtures' slots become
//     DRAW-PRESCRIPTION-ONLY (prescribeDraw.competitorMissing stays satisfied,
//     but NO entry is opened — the destination cell fills the slot), while
//     f3k-southern-fling's retired-pilot slots stay flight-less entries.
//   - OracleNormalisedScore keys by RoundNo, per the fixture's keyFormat
//     {"TaskNo"}/{"RoundNo"}/{"GroupNo"}/{"ReFlightNo"}/{"PilotNo"} (trap 1:
//     the old OriginalRoundNo key was harmless while every row had
//     OriginalRoundNo == RoundNo and fatal once make-ups replay).
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

/// <summary>
/// Everything the comparator needs to find its way around the replay.
/// </summary>
/// <param name="CommandsIssued">WI-5 self-check: how many command POSTs this
/// replay made — a pure function of the fixture data, so two replays of one
/// fixture in one run must agree exactly (a difference means state bled across
/// replays through the shared store).</param>
public sealed record ReplayOutcome(
    CompetitionId CompetitionId,
    int PhaseOrdinal,
    IReadOnlyDictionary<int, string> TaskCodeByRoundNo,
    IReadOnlyDictionary<int, int> RoundOrdinalByRoundNo,
    IReadOnlyDictionary<(int RoundNo, int GroupNo), GroupId> GroupIdByRoundAndGroup,
    IReadOnlyDictionary<(int RoundNo, int GroupNo, long PilotNo), EntryId> EntryIdBySlot,
    IReadOnlyDictionary<long, CompetitorId> CompetitorByPilotNo,
    int CommandsIssued);

public sealed class ReplayDriver(HttpClient client)
{
    private const string CdName = "Gliderscore replay harness";

    // WI-5 self-check 1 — the command counter behind ReplayOutcome.CommandsIssued.
    // One POST = one count, in ReplayAsync order; reads (GETs) are not commands.
    private int _commandsIssued;

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

    // WI-6 — slots a fixture needs prescribed although no scores-raw row backs
    // them (the same per-fixture-data pattern as the round binds above).
    //
    // reflight-aggregate-destination.md WI-4 (trap 3) splits the one table in
    // two. BOTH kinds are prescribed — prescribeDraw.competitorMissing demands
    // every registered competitor appear in every round — but only the second
    // kind gets an entry opened.
    //
    // SyntheticPrescriptionOnlySlots — make-up fixtures: the slot exists only
    // because the pilot's appearance that round is a RE-FLIGHT make-up flown
    // elsewhere (D5 step 1 drops that row from the draw). Under the faithful
    // mapping NO entry is opened here — the make-up's score aggregates into
    // this round's slot from its hosting round (counts-for), and a flight-less
    // entry would both refuse nothing and DOUBLE the slot (the D8 check would
    // then refuse the make-up open itself):
    //   jerilderie-2010: excluding the re-flight row leaves pilot 29 absent
    //     from round 12 entirely (R13/G1 SeqNo=14, OriginalRoundNo=12).
    //   f5j-hawkes-bay-trials (comp 135): pilot 128 is absent from rounds 1–4
    //     entirely — his first four appearances are the four re-flight rows,
    //     which D5 step 1 drops (R1/R3 sizes {G1 6, G2 5, G3 6} → group 2;
    //     R2/R4 {G1 5, G2 6, G3 6} → group 1).
    //
    // SyntheticFlightLessSlots — flight-less-entry slots, unchanged (D4): a
    // flight-less entry yields NoResult ⇒ cell 0, which is what puts GS's
    // placeholder zeros into the drop-candidate pool. f3k-southern-fling
    // (comp 17): pilot 89 Retired=true after round 8, absent R9–R15 (7 missing
    // slots; R9–R15 sizes {G1 5, G2 5, G3 4} → group 3). These are a retired
    // pilot's zeros, NOT make-ups — their behaviour is untouched (trap 3).
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<(int RoundNo, int GroupNo, long PilotNo)>>
        SyntheticPrescriptionOnlySlots = new Dictionary<string, IReadOnlyList<(int, int, long)>>
        {
            ["jerilderie-2010"] = [(12, 1, 29)],
            ["f5j-hawkes-bay-trials"] =
                [(1, 2, 128), (2, 1, 128), (3, 2, 128), (4, 1, 128)],
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<(int RoundNo, int GroupNo, long PilotNo)>>
        SyntheticFlightLessSlots = new Dictionary<string, IReadOnlyList<(int, int, long)>>
        {
            ["f3k-southern-fling"] =
                [(9, 3, 89), (10, 3, 89), (11, 3, 89), (12, 3, 89), (13, 3, 89), (14, 3, 89), (15, 3, 89)],
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
        //
        // nz-fixture-replay-scenarios.md D5 item 1 — CompDate is empty or null
        // in ALL FIVE NZ fixtures' competition.json (story Verified ground
        // truth), so the unconditional parse below threw on every one of them.
        // The replay date is scoring-irrelevant (it feeds only
        // CreateCompetition's display dates, never a score), so a fixed
        // 2000-01-01 stands in for a missing one. Fixtures with real dates —
        // the seven originals — parse exactly as before.
        var slug = Guid.NewGuid().ToString("N");
        var compDate = string.IsNullOrWhiteSpace(fixture.Competition.Identity.CompDate)
            ? new DateOnly(2000, 1, 1)
            : DateOnly.Parse(
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
        var (keptRows, reflightRows) = DeriveDrawRows(fixture);

        // WI-6 — append the fixture's synthetic slots (see the two tables) as
        // all-zero rows: zero time keeps CaptureDurationInputs flight-less
        // (NoResult ⇒ cell 0) for the flight-less kind, a SeqNo past every
        // real row puts the slot last in its group's flying order, and
        // OriginalRoundNo = RoundNo keeps any accidental re-derivation honest.
        // Appended AFTER derivation so D5's partition assertion judges the
        // real rows alone. BOTH kinds join keptRows — the prescription
        // structure (rounds, groups, drawn membership) needs every slot —
        // but the prescription-only kind is filtered out of the entry-opening
        // walk below (trap 3: no entry where a make-up's destination cell
        // fills the slot).
        var prescriptionOnlySlots = (SyntheticPrescriptionOnlySlots.GetValueOrDefault(fixture.Slug) ?? [])
            .Select(s => (s.RoundNo, s.GroupNo, s.PilotNo))
            .ToHashSet();

        foreach (var (roundNo, groupNo, pilotNo) in SyntheticPrescriptionOnlySlots.GetValueOrDefault(fixture.Slug) ?? [])
        {
            keptRows.Add(new ScoresRow(
                TaskNo: 1, RoundNo: roundNo, GroupNo: groupNo, ReFlightNo: 0, PilotNo: pilotNo,
                SeqNo: keptRows.Where(r => r.RoundNo == roundNo).Max(r => r.SeqNo) + 1,
                Laps: 0m, Time1Mins: 0m, Time1Secs: 0m, Time2Mins: 0m, Time2Secs: 0m,
                FlightScoreDeduction: 0m, Landing: 0m, Penalty: 0, OriginalRoundNo: roundNo));
        }

        foreach (var (roundNo, groupNo, pilotNo) in SyntheticFlightLessSlots.GetValueOrDefault(fixture.Slug) ?? [])
        {
            keptRows.Add(new ScoresRow(
                TaskNo: 1, RoundNo: roundNo, GroupNo: groupNo, ReFlightNo: 0, PilotNo: pilotNo,
                SeqNo: keptRows.Where(r => r.RoundNo == roundNo).Max(r => r.SeqNo) + 1,
                Laps: 0m, Time1Mins: 0m, Time1Secs: 0m, Time2Mins: 0m, Time2Secs: 0m,
                FlightScoreDeduction: 0m, Landing: 0m, Penalty: 0, OriginalRoundNo: roundNo));
        }

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

        // The flight + capture half both passes share (reflight-aggregate-
        // destination.md WI-4): every opened entry — regular, flight-less or
        // make-up — is flown and captured identically (CaptureInputs unchanged).
        async Task FlyAndCaptureAsync(EntryId entryId, ScoresRow row)
        {
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

        foreach (var roundNo in roundNosAscending)
        {
            var roundOrdinal = roundOrdinalByRoundNo[roundNo];
            var groupNosAscending = keptRows
                .Where(r => r.RoundNo == roundNo)
                .Select(r => r.GroupNo).Distinct().OrderBy(n => n);

            // Pass 1 (trap 2) — every regular slot, group-ascending then
            // SeqNo: the prescription-only synthetic slots are NOT opened
            // (trap 3 — the destination cell fills them via the make-up's
            // counts-for).
            foreach (var groupNo in groupNosAscending)
            {
                var groupId = groupIdByRoundAndGroup[(roundNo, groupNo)];

                foreach (var row in keptRows
                    .Where(r => r.RoundNo == roundNo && r.GroupNo == groupNo
                                && !prescriptionOnlySlots.Contains((r.RoundNo, r.GroupNo, r.PilotNo)))
                    .OrderBy(r => r.SeqNo))
                {
                    var entryId = await PostAsync<EntryId>(
                        "/open-entry",
                        new OpenEntry(
                            competitionId, phase.Ordinal, roundOrdinal, 1,
                            groupId, competitorByPilotNo[row.PilotNo]));

                    entryIdBySlot[(row.RoundNo, row.GroupNo, row.PilotNo)] = entryId;
                    await FlyAndCaptureAsync(entryId, row);
                }
            }

            // Pass 2 (trap 2) — the round's make-up rows, SeqNo order within
            // the pass. Every competitor's Original in this task-round is open
            // by now, which the handler's Original-branch law requires (any
            // live entry blocks an Original open, so a make-up must never
            // precede its competitor's Original in the same task-round) — GS's
            // own flying order satisfies it. Role Entitled (D2), destination
            // OriginalRoundNo, reason recorded (D4). Fixture rounds are
            // contiguous from 1 in every corpus fixture (verified: jerilderie
            // 1–14, comp 135 1–16), so the GS RoundNo IS the round ordinal.
            foreach (var row in reflightRows
                .Where(r => r.RoundNo == roundNo)
                .OrderBy(r => r.SeqNo).ThenBy(r => r.GroupNo))
            {
                var groupId = groupIdByRoundAndGroup[(roundNo, row.GroupNo)];
                var entryId = await PostAsync<EntryId>(
                    "/open-entry",
                    new OpenEntry(
                        competitionId, phase.Ordinal, roundOrdinal, 1,
                        groupId, competitorByPilotNo[row.PilotNo],
                        Role: ReflightRole.Entitled,
                        CountsForRoundOrdinal: (int)row.OriginalRoundNo,
                        Reason: $"Gliderscore re-flight row (OriginalRoundNo={row.OriginalRoundNo})"));

                entryIdBySlot[(row.RoundNo, row.GroupNo, row.PilotNo)] = entryId;
                await FlyAndCaptureAsync(entryId, row);
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
            CompetitorByPilotNo: competitorByPilotNo,
            CommandsIssued: _commandsIssued);
    }

    // ------------------------------------------------------------- D5 draw

    /// <summary>
    /// The scores-raw rows that form the realised draw, after D5's filters —
    /// and the re-flight rows step 1 removes. reflight-aggregate-destination.md
    /// WI-4: those rows are destination-bearing make-ups (every corpus row
    /// carrying them is OriginalRoundNo ≠ RoundNo, ReFlightNo = 0); they stay
    /// out of the draw (the prescription half stays deferred) but are now
    /// COLLECTED so the replay can open each one's entry in the second pass.
    /// </summary>
    private static (List<ScoresRow> Kept, List<ScoresRow> ReflightRows) DeriveDrawRows(GliderscoreFixture fixture)
    {
        // Step 1 — re-flight rows have no base-draw prescription path yet
        // (deferred-decisions.md "Draw"); collected for the WI-4 make-up pass.
        var reflightRows = new List<ScoresRow>();
        var rows = fixture.ScoresRaw.Rows
            .Where(r =>
            {
                if (r.ReFlightNo > 0 || r.OriginalRoundNo != r.RoundNo)
                {
                    reflightRows.Add(r);
                    return false;
                }

                return true;
            })
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

        return (kept, reflightRows);
    }

    /// <summary>
    /// Oracle lookup for D5 step 2, keyed exactly as expected-scores.json's
    /// keyFormat states: {"TaskNo"}/{"RoundNo"}/{"GroupNo"}/{"ReFlightNo"}/{"PilotNo"}
    /// — RoundNo, NOT OriginalRoundNo (reflight-aggregate-destination.md WI-4,
    /// trap 1: the oracle is RoundNo-keyed; the old OriginalRoundNo key was
    /// harmless while the rows seen here all had OriginalRoundNo == RoundNo,
    /// and fatal once make-up rows replay). A missing oracle cell ranks lowest
    /// rather than throwing — deduplication needs an ordering, not a verdict.
    /// </summary>
    private static decimal OracleNormalisedScore(GliderscoreFixture fixture, ScoresRow row) =>
        fixture.ExpectedScores.Scores.TryGetValue(
                $"{row.TaskNo}/{row.RoundNo}/{row.GroupNo}/{row.ReFlightNo}/{row.PilotNo}",
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

        // nz-fixture-replay-scenarios.md D5 item 5 — F5J's height term. For an
        // F5J fixture Scores.FlightScoreDeduction carries the launch HEIGHT in
        // metres (story trap 5: idx=3, NOT a deduction payload), and the F5J
        // class definitions (D3) declare it as the `launchHeight` metric their
        // piecewise score term reads. The mutual exclusion with the
        // lateLandingDeduction arm above is by DEFINITION CONTENT, not by
        // driver branching: the F5J definitions declare launchHeight, the f3j
        // fixtures declare lateLandingDeduction — never both (trap 5: never
        // author lateLandingDeduction for an F5J fixture). Same Flight: 1 arm
        // shape as its siblings.
        if (declared.Contains("launchHeight"))
        {
            captures.Add(new SlotCapture("launchHeight", row.FlightScoreDeduction, Flight: 1));
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
    /// nz-fixture-replay-scenarios.md D5 item 2 widens the map to the full
    /// 16-code catalogue the NZ fixtures' schedules name (story Verified
    /// ground truth, "F3K task catalogue" — proven cell-exact, 417/417 across
    /// comps 17 and 54; the VB source is not available on this machine, so the
    /// committed oracles are the proof of record and new-code task names are
    /// neutral descriptions, not GS's unverified strings):
    ///   A(2) — one flight from the Laps slot (packed), capped at 300 s;
    ///   B(1), B(2), D(1) — two slots (Laps, Time1Mins), UNCAPPED sum
    ///        (witnessed above would-be caps: B(1) 182, D(1) 300 — trap 4);
    ///   C(1) — three slots, each clamped at 180 s (the cap never bites this
    ///        data; AllUp-family convention, noted per trap 4);
    ///   E(1), I, J, M — three slots, uncapped sums (witnessed: E(1) 277.5,
    ///        I 200, M 309.8 — trap 4);
    ///   E    — five slots, uncapped sum (old E, no 2024 window reduction);
    ///   K    — five slots POSITIONALLY clamped at 60/90/120/150/180 (comp 17
    ///        P81 R1 slots [47,49,43,0,180] → 319); zero slots KEEP their seat
    ///        (D5 item 4 — see the capture policy below);
    ///   H    — four slots sorted DESCENDING, then positionally clamped at
    ///        240/180/120/60 (comp 17 R5 P76 slots [221,60,120,166] → sorted
    ///        [221,166,120,60] → 567; D5 item 3 — see the capture policy);
    ///   L    — one flight from the Laps slot (packed), uncapped.
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
            ["A(2)"] = ["Laps"],
            ["L"] = ["Laps"],
            ["B(1)"] = ["Laps", "Time1Mins"],
            ["B(2)"] = ["Laps", "Time1Mins"],
            ["D(1)"] = ["Laps", "Time1Mins"],
            ["F"] = ["Laps", "Time1Mins", "Time1Secs"],
            ["C(1)"] = ["Laps", "Time1Mins", "Time1Secs"],
            ["E(1)"] = ["Laps", "Time1Mins", "Time1Secs"],
            ["I"] = ["Laps", "Time1Mins", "Time1Secs"],
            ["J"] = ["Laps", "Time1Mins", "Time1Secs"],
            ["M"] = ["Laps", "Time1Mins", "Time1Secs"],
            ["E"] = ["Laps", "Time1Mins", "Time1Secs", "Time2Mins", "Time2Secs"],
            ["K"] = ["Laps", "Time1Mins", "Time1Secs", "Time2Mins", "Time2Secs"],
            ["H"] = ["Laps", "Time1Mins", "Time1Secs", "Time2Mins"],
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

        // nz-fixture-replay-scenarios.md D5 items 3 and 4 — two codes carry a
        // proven slot discipline beyond the plain skip-zeros walk, each SPIKE-
        // PROVEN against the engine before adoption:
        //   K (D5 item 4) — capture ALL FIVE slots, zeros included. K pairs
        //     targets to SLOT positions (exactlyN 5, InOrder 60/90/120/150/180),
        //     and comp 17 witnesses one interior gap (R1 P89, slots
        //     [47,49,43,0,180] → GS raw 319): a skipped zero would shift every
        //     later flight onto the wrong rung. Spike: the engine accepts a
        //     zero flightTime flight and exactlyN selection scores it
        //     min(0, target) = 0 with positional alignment intact. An all-zero
        //     K row stays flight-less (D4 cell 0). Scoped to K ONLY — D keeps
        //     its prefix-shaped discipline (trap 7).
        //   H (D5 item 3) — decode the four slots, keep the non-zero values,
        //     sort them DESCENDING, then assign flights. GS sorts then clamps
        //     positionally against the descending targets 240/180/120/60
        //     (44/44; comp 17 R5 P76 slots [221,60,120,166] → sorted
        //     [221,166,120,60] → 567). Spike: exactlyN selection with FEWER
        //     flights than its count pairs in order against the leading
        //     targets without throwing, so H rows whose zero slots drop out
        //     replay cleanly.
        // Every other code keeps the plain walk below: zeros contribute
        // nothing under `all`/`last` selection, and the only exactlyN code it
        // serves is D, whose recorded rows are all prefix-shaped.
        return taskCode switch
        {
            "K" => CaptureKSlotsInSlotOrder(row, F3KSlotMap[taskCode]),
            "H" => CaptureHSlotsSortedDescending(row, F3KSlotMap[taskCode]),
            _ => CaptureSlotsSkippingZeros(fixture, row, taskCode, F3KSlotMap[taskCode]),
        };
    }

    /// <summary>Task K (nz-fixture-replay-scenarios.md D5 item 4): all five
    /// slots as flights, zeros included, in slot order — targets pair to SLOT
    /// positions. All-zero rows stay flight-less.</summary>
    private static List<SlotCapture>? CaptureKSlotsInSlotOrder(ScoresRow row, IReadOnlyList<string> slots)
    {
        var decoded = slots.Select(s => DecodePackedMinutesSeconds(ColumnValue(row, s))).ToList();

        return decoded.All(v => v == 0m)
            ? null
            : decoded
                .Select((value, index) => new SlotCapture("flightTime", value, Flight: index + 1))
                .ToList();
    }

    /// <summary>Task H (nz-fixture-replay-scenarios.md D5 item 3): the non-zero
    /// slot values sorted descending become flights 1..n — GS sorts then clamps
    /// positionally against its descending target ladder.</summary>
    private static List<SlotCapture>? CaptureHSlotsSortedDescending(ScoresRow row, IReadOnlyList<string> slots)
    {
        var decoded = slots
            .Select(s => DecodePackedMinutesSeconds(ColumnValue(row, s)))
            .Where(v => v != 0m)
            .OrderByDescending(v => v)
            .ToList();

        return decoded.Count == 0
            ? null
            : decoded
                .Select((value, index) => new SlotCapture("flightTime", value, Flight: index + 1))
                .ToList();
    }

    private static List<SlotCapture>? CaptureSlotsSkippingZeros(
        GliderscoreFixture fixture, ScoresRow row, string taskCode, IReadOnlyList<string> slots)
    {
        var captures = new List<SlotCapture>();
        var seenNonZero = false;
        var gapAfterNonZero = false;

        for (var i = 0; i < slots.Count; i++)
        {
            var decoded = DecodePackedMinutesSeconds(ColumnValue(row, slots[i]));

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
        _commandsIssued++;

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
