// kanban/in-progress/literal-record-replay-scenarios.md WI-1..WI-5 — step
// definitions for Features/RecordingAGliderscoreFixture.feature: a whole
// GliderScore fixture readable as Gherkin. One "<pilot> enters his scores"
// block per pilot carries the whole draw; every command payload mirrors
// ReplayDriver's exactly (no new command surface), and the scenario closes
// with the cell-for-cell scores-raw self-check (WI-2), the literal placings
// (WI-4) and the JSON harness's three-grain comparison as referee (WI-5).
// All HTTP goes over AcceptanceFixture.Client via ApiClient — the Api's own
// JSON options — and the By/Location provenance carries "Gliderscore literal
// record" while the competition name and emails carry a run slug so the
// shared store never collides (mirror ReplayDriver.cs:387-418).
//
// One instance per scenario (Reqnroll's default binding lifetime), so the
// fields below are scenario-scoped — the same discipline as ReplaySteps.cs.

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Acceptance.Tests.Support.Gliderscore;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
// The Domain's PublishedClassDefinition also declares a Comparator (the class-
// definition ingestion one) — this file means the replay harness's.
using Comparator = Soarscore.Acceptance.Tests.Support.Gliderscore.Comparator;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class RecordingAGliderscoreFixtureSteps
{
    private const string CdName = "Gliderscore literal record";

    private static readonly Regex MmssPattern = new(@"^(\d+):(\d{1,2})$", RegexOptions.Compiled);

    // kanban/in-progress/literal-record-f3k-sample-comp.md WI-5 — the citation
    // discipline ReplaySteps.enforces over the committed ledger (D6).
    private static readonly Regex TriageCitationPattern =
        new(@"\bD[1-6]\b|\btrap\s*3\b|\bR1\b|\bT1\b", RegexOptions.Compiled);

    // kanban/in-progress/literal-record-f3k-sample-comp.md WI-2 — the slot
    // family's two zero-row markers (story decisions 1 and 2).
    private const string MarkerNoFlight = "no flight";
    private const string MarkerZeroUnrecorded = "zero (unrecorded)";

    // kanban/in-progress/literal-record-f3k-sample-comp.md WI-3 — the seven raw
    // columns GS packs per Scores row, in slot order; ReplayDriver.F3KSlotMap's
    // slot lists name these.
    private static readonly string[] RawSlotColumns =
    [
        "Laps", "Time1Mins", "Time1Secs", "Time2Mins", "Time2Secs", "Landing", "FlightScoreDeduction",
    ];

    private GliderscoreFixture _fixture = null!;
    private CompetitionId _competitionId;
    private int _phaseOrdinal;
    private string _runSlug = "";
    private string _contentHash = "";
    private Dictionary<(int RoundNo, int GroupNo), GroupId> _groupIdByRoundAndGroup = [];
    private Dictionary<(int RoundNo, int GroupNo, long PilotNo), EntryId> _entryIdBySlot = [];
    private Dictionary<long, CompetitorId> _competitorByPilotNo = [];
    private Dictionary<long, string> _pilotNameByNo = [];          // from entries.json
    private Dictionary<int, string> _taskCodeByRoundNo = [];
    private Dictionary<int, int> _roundOrdinalByRoundNo = [];
    private List<EnteredRow> _enteredRows = [];                    // tables as entered
    private HashSet<string> _enteredTableColumns = [];             // union of the tables' headers

    // kanban/in-progress/literal-record-f3k-sample-comp.md WI-2 — set by the
    // slot entry arm; the self-check's slot arm (WI-3) branches on it.
    private bool _slotFamilyTables;
    private int _commandsIssued;                                   // honesty only, unasserted
    private ReplayOutcome _outcome = null!;

    // One authored entry-block row, exactly as the feature table carries it.
    // Time is null when the row is the explicit "no flight" marker; the
    // Laps/Height/Penalty cells are null when that column is absent from the
    // fixture's tables.
    private sealed record EnteredRow(
        long PilotNo, string PilotName, int RoundNo,
        string? Time, string? Landing, string? Laps, string? Height, string? Penalty,
        string? Task, IReadOnlyList<string?> SlotCells);

    // ----------------------------------------------------------------- Given

    [Given(@"^the GliderScore fixture ""(.+)"" is loaded for literal recording$")]
    public void GivenTheGliderScoreFixtureIsLoadedForLiteralRecording(string slug)
    {
        _fixture = FixtureLoader.Load(slug);

        FixtureLoader.ActiveSlugs().Should().Contain(slug,
            $"the literal record covers active corpus fixtures only, and '{slug}' must be one of them");

        _pilotNameByNo = _fixture.Entries.Pilots.Rows
            .ToDictionary(p => (long)p.PilotNo, p => $"{p.FirstName} {p.LastName}");

        _pilotNameByNo.Values.Should().OnlyHaveUniqueItems(
            "the name-keyed entry and placings steps require unique pilot names");
    }

    [Given(@"^its class definition is published and a competition created$")]
    public async Task GivenItsClassDefinitionIsPublishedAndACompetitionCreated()
    {
        _contentHash = await PostAsync<string>(
            "/publish-class-definition", new PublishClassDefinition(_fixture.Definition));

        _contentHash.Should().NotBeEmpty(
            "the content hash is the adoption anchor /create-competition copies the definition from");

        _runSlug = Guid.NewGuid().ToString("N");
        var compDate = string.IsNullOrWhiteSpace(_fixture.Competition.Identity.CompDate)
            ? new DateOnly(2000, 1, 1)
            : DateOnly.Parse(_fixture.Competition.Identity.CompDate.Split(' ')[0], CultureInfo.InvariantCulture);

        _competitionId = await PostAsync<CompetitionId>(
            "/create-competition",
            new CreateCompetition(
                $"{_fixture.Competition.Identity.CompName} (literal {_runSlug})",
                CdName,
                compDate,
                compDate,
                _contentHash));
    }

    [Given(@"^its (\d+) pilots are registered under their fixture names$")]
    public async Task GivenItsPilotsAreRegisteredUnderTheirFixtureNames(int pilotCount)
    {
        _fixture.Entries.CompPilots.Rows.Length.Should().Be(pilotCount,
            $"the feature file pins '{_fixture.Slug}' at {pilotCount} registered pilots — a different count must force a feature-file revisit, not a silent pass");

        foreach (var row in _fixture.Entries.CompPilots.Rows)
        {
            var personId = await PostAsync<PersonId>(
                "/register-person",
                new RegisterPerson(
                    _pilotNameByNo[row.PilotNo],
                    new ContactDetails
                    {
                        Email = $"gliderscore-literal-{_runSlug}-pilot-{row.PilotNo}@example.com".ToLowerInvariant()
                    },
                    null));

            _competitorByPilotNo[row.PilotNo] = await PostAsync<CompetitorId>(
                "/register-competitor", new RegisterCompetitor(_competitionId, personId));
        }
    }

    [Given(@"^the draw is prescribed as (\d+) rounds? of one group in flying order and accepted$")]
    public async Task GivenTheDrawIsPrescribedAsRoundsOfOneGroupInFlyingOrderAndAccepted(int roundCount)
    {
        // The shape the feature text claims, asserted loudly — constraint (c)'s
        // guard: a fixture whose realised draw varies needs literal draw tables
        // authored first, never a silent mis-prescription.
        var rows = _fixture.ScoresRaw.Rows;

        rows.Where(r => r.ReFlightNo != 0 || r.OriginalRoundNo != r.RoundNo).Should().BeEmpty(
            "a re-flight row is the draw-table widening gate (story 'Before starting' (c)) — literal draw tables must be authored first");

        var roundNos = rows.Select(r => r.RoundNo).Distinct().OrderBy(n => n).ToList();
        roundNos.Should().HaveCount(roundCount,
            $"the feature text prescribes {roundCount} rounds");
        roundNos.Should().Equal(Enumerable.Range(1, roundCount).ToList(),
            "the fixture's RoundNo is contiguous from 1, so it doubles as the engine's round ordinal");

        foreach (var roundNo in roundNos)
        {
            var roundRows = rows.Where(r => r.RoundNo == roundNo).ToList();

            roundRows.Select(r => r.GroupNo).Distinct().Should().ContainSingle(
                $"the feature text prescribes one group per round; round {roundNo} carries more");

            roundRows.Select(r => r.SeqNo).Should().OnlyHaveUniqueItems(
                $"round {roundNo}'s flying order (SeqNo) must be unambiguous");
        }

        var flyingOrderByRound = roundNos.ToDictionary(
            roundNo => roundNo,
            roundNo => rows.Where(r => r.RoundNo == roundNo).OrderBy(r => r.SeqNo).Select(r => r.PilotNo).ToList());

        foreach (var roundNo in roundNos.Skip(1))
        {
            flyingOrderByRound[roundNo].Should().Equal(flyingOrderByRound[roundNos[0]],
                $"the feature text omits draw tables on the claim that the flying order never varies; round {roundNo} breaks it");
        }

        // The fixture's per-round task schedule, derived exactly as the driver
        // does (literal-record-f3k-sample-comp.md §4 Given 4) — empty for the
        // duration family, one row per prescribed round for a schedule-bearing one.
        var schedule = ReplayDriver.TaskByRound(_fixture);

        if (schedule.Count > 0)
        {
            schedule.Keys.Should().BeEquivalentTo(roundNos,
                "every prescribed round must have a task-schedule row, and vice versa");

            var declaredCodes = _fixture.Definition.Phases.Single()
                .Tasks.Select(t => t.Code)
                .ToHashSet();
            var unknownCodes = schedule.Values.Distinct().Where(code => !declaredCodes.Contains(code)).ToList();
            unknownCodes.Should().BeEmpty(
                $"the fixture's task schedule names task code(s) [{string.Join(", ", unknownCodes)}] that the published definition's phase tasks do not declare — prescribing a task the catalogue does not carry is an authoring error");
        }

        // Duration family: TaskByRound is empty, so every prescribed round
        // carries a null TaskRef — exactly as the driver prescribes
        // (ReplayDriver.cs:472-490). List order IS the flying order.
        var prescribedRounds = roundNos
            .Select(roundNo => new PrescribedRound(
                TaskRef: schedule.GetValueOrDefault(roundNo),
                Groups:
                [
                    new PrescribedGroup(
                        flyingOrderByRound[roundNo].Select(p => _competitorByPilotNo[p]).ToList()),
                ]))
            .ToList();

        await PostAsync<CompetitionId>("/prescribe-draw", new PrescribeDraw(_competitionId, prescribedRounds, CdName));
        await PostAsync<CompetitionId>("/accept-draw", new AcceptDraw(_competitionId));

        _roundOrdinalByRoundNo = roundNos
            .Select((roundNo, index) => (roundNo, ordinal: index + 1))
            .ToDictionary(pair => pair.roundNo, pair => pair.ordinal);

        // Read the drawn structure back rather than assume, and key everything
        // by fixture coordinates (RoundNo/GroupNo/PilotNo), not engine
        // ordinals — exactly as ReplayDriver.cs:568-588 does.
        var view = await ApiClient.GetAsync<CompetitionView>(
            AcceptanceFixture.Client, $"/competition?id={_competitionId.Value}");
        var phase = view.Competition.Phases.Single();
        _phaseOrdinal = phase.Ordinal;
        var roundsAscending = phase.Rounds.OrderBy(r => r.Ordinal).ToList();

        roundsAscending.Should().HaveCount(roundCount,
            "the prescribed draw must read back with one round per fixture round");

        foreach (var roundNo in roundNos)
        {
            var taskRound = roundsAscending[_roundOrdinalByRoundNo[roundNo] - 1].TaskRounds.Single();
            _taskCodeByRoundNo[roundNo] = taskRound.TaskRef;
            _groupIdByRoundAndGroup[(roundNo, 1)] = taskRound.Groups.OrderBy(g => g.Ordinal).Single().Id;
        }

        if (schedule.Count > 0)
        {
            var taskMismatches = schedule
                .Where(kv => _taskCodeByRoundNo.GetValueOrDefault(kv.Key) != kv.Value)
                .Select(kv =>
                    $"round {kv.Key}: drawn '{_taskCodeByRoundNo.GetValueOrDefault(kv.Key) ?? "<none>"}' vs prescribed '{kv.Value}'")
                .Concat(_taskCodeByRoundNo.Keys.Except(schedule.Keys)
                    .Select(roundNo => $"round {roundNo}: drawn '{_taskCodeByRoundNo[roundNo]}' but no task prescribed"))
                .ToList();

            taskMismatches.Should().BeEmpty(
                "the drawn task-rounds must carry the prescribed TaskRefs — _taskCodeByRoundNo must equal the schedule exactly (round → task code)"
                + (taskMismatches.Count > 0
                    ? $":{Environment.NewLine}{string.Join(Environment.NewLine, taskMismatches)}"
                    : string.Empty));
        }
    }

    // ------------------------------------------------------------------ When

    [When(@"^(.+) enters his scores$")]
    public async Task WhenEntersHisScores(string pilotName, Table table)
    {
        var matches = _pilotNameByNo.Where(kv => kv.Value == pilotName).ToList();
        matches.Should().HaveCount(1,
            $"unknown pilot name '{pilotName}' — the fixture's pilots are [{string.Join(", ", _pilotNameByNo.Values.Order())}]");
        var pilotNo = matches.Single().Key;

        // kanban/in-progress/literal-record-f3k-sample-comp.md WI-2 — the
        // entry blocks branch on table shape: the duration family carries a
        // Time column, the slot family Slot 1..7. A table carrying both or
        // neither is an authoring error, never a silent guess.
        var hasTime = table.Header.Contains("Time");
        var hasSlots = table.Header.Contains("Slot 1");

        (hasTime || hasSlots).Should().BeTrue(
            $"round table for {pilotName} carries neither a Time nor a Slot 1 column — the entry form is one family per table (duration: Time/Landing; slot: Task/Slot 1..Slot 7/Penalty)");

        (hasTime && hasSlots).Should().BeFalse(
            $"round table for {pilotName} carries BOTH a Time and a Slot 1 column — the entry form is one family per table (duration: Time/Landing; slot: Task/Slot 1..Slot 7/Penalty)");

        if (hasSlots)
        {
            await WhenEntersSlotFamilyScores(pilotName, pilotNo, table);
            return;
        }

        // The union column set is story decision 4: one family-agnostic step
        // definition, each fixture's table showing only the columns it uses.
        // Task is accepted-and-ignored for now (single-task fixtures carry no
        // Task column; a per-round task schedule fixture widens this step).
        string[] unionColumns = ["Round", "Task", "Laps", "Time", "Landing", "Height", "Penalty"];
        var unknownColumns = table.Header.Where(h => !unionColumns.Contains(h)).ToList();
        unknownColumns.Should().BeEmpty(
            $"unknown column(s) [{string.Join(", ", unknownColumns)}] — the union column set is [{string.Join(", ", unionColumns)}]");

        _enteredTableColumns.UnionWith(table.Header);

        foreach (var row in table.Rows)
        {
            var roundNo = int.Parse(row["Round"], CultureInfo.InvariantCulture);

            var timeCell = row["Time"];
            var timeMatch = MmssPattern.Match(timeCell);
            var isMarker = timeCell == "no flight";

            (isMarker || timeMatch.Success).Should().BeTrue(
                $"round {roundNo}, {pilotName}: Time cell '{timeCell}' is neither 'no flight' nor m:ss");

            var flightSeconds = isMarker
                ? (decimal?)null
                : decimal.Parse(timeMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 60m
                  + decimal.Parse(timeMatch.Groups[2].Value, CultureInfo.InvariantCulture);

            var landing = table.Header.Contains("Landing") ? row["Landing"] : null;
            var landingValue = 0m;

            if (flightSeconds is not null)
            {
                landing.Should().NotBeNull(
                    $"round {roundNo}, {pilotName}: a flown row must carry a Landing cell");

                decimal.TryParse(landing, NumberStyles.Number, CultureInfo.InvariantCulture, out landingValue).Should()
                    .BeTrue($"round {roundNo}, {pilotName}: Landing cell '{landing}' must be an invariant decimal");
            }

            var groupId = _groupIdByRoundAndGroup[(roundNo, 1)];
            var entryId = await PostAsync<EntryId>(
                "/open-entry",
                new OpenEntry(
                    _competitionId, _phaseOrdinal, _roundOrdinalByRoundNo[roundNo], 1,
                    groupId, _competitorByPilotNo[pilotNo]));

            _entryIdBySlot[(roundNo, 1, pilotNo)] = entryId;

            if (flightSeconds is { } seconds)
            {
                await PostAsync<EntryId>("/open-flight", new OpenFlight(entryId));
                await PostAsync<EntryId>(
                    "/capture-measurement",
                    new CaptureMeasurement(entryId, 1, "flightTime", MeasuredValue.Of(seconds)));
                await PostAsync<EntryId>(
                    "/capture-measurement",
                    new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(landingValue)));
            }

            _enteredRows.Add(new EnteredRow(
                pilotNo, pilotName, roundNo,
                Time: isMarker ? null : timeCell,
                Landing: landing,
                Laps: table.Header.Contains("Laps") ? row["Laps"] : null,
                Height: table.Header.Contains("Height") ? row["Height"] : null,
                Penalty: table.Header.Contains("Penalty") ? row["Penalty"] : null,
                Task: null,
                SlotCells: []));
        }
    }

    // kanban/in-progress/literal-record-f3k-sample-comp.md WI-2 — the slot
    // family's entry arm. The authored m:ss slot cells are decoded with the
    // same mmss rule as the duration arm; WHICH slots a round reads comes
    // from the fixture's prescribed schedule via ReplayDriver.F3KSlotMap —
    // the Task cells never drive behaviour (the self-check pins them).
    private async Task WhenEntersSlotFamilyScores(string pilotName, long pilotNo, Table table)
    {
        string[] unionColumns =
        [
            "Round", "Task", "Time", "Landing", "Laps", "Height", "Penalty",
            "Slot 1", "Slot 2", "Slot 3", "Slot 4", "Slot 5", "Slot 6", "Slot 7",
        ];

        var unknownColumns = table.Header.Where(h => !unionColumns.Contains(h)).ToList();
        unknownColumns.Should().BeEmpty(
            $"unknown column(s) [{string.Join(", ", unknownColumns)}] — the slot-table union column set is [{string.Join(", ", unionColumns)}]");

        table.Header.Should().Contain("Task",
            "the Task column is live for slot tables — the per-round task schedule prescribes what each round reads");

        _enteredTableColumns.UnionWith(table.Header);
        _slotFamilyTables = true;

        string? SlotCell(Reqnroll.DataTableRow row, int i) =>
            table.Header.Contains($"Slot {i + 1}") ? row[$"Slot {i + 1}"] : null;

        foreach (var row in table.Rows)
        {
            var roundNo = int.Parse(row["Round"], CultureInfo.InvariantCulture);

            _taskCodeByRoundNo.TryGetValue(roundNo, out var taskCode).Should().BeTrue(
                $"round {roundNo} has no drawn task round — a slot table can only enter prescribed rounds");

            if (!ReplayDriver.F3KSlotMap.TryGetValue(taskCode!, out var slots))
            {
                throw new NotSupportedException(
                    $"Fixture '{_fixture.Slug}': round {roundNo} names GS task '{taskCode}', which is not in "
                    + "the F3K slot-column capture map — widen F3KSlotMap with its CalcRawScoreF3K semantics first.");
            }

            var captures = new List<decimal>();
            var unflown = false;

            for (var i = 0; i < 7; i++)
            {
                var header = $"Slot {i + 1}";
                var cell = SlotCell(row, i);

                if (cell is null || cell == "—")
                {
                    continue;
                }

                var mmss = MmssPattern.Match(cell);

                if (mmss.Success)
                {
                    (i < slots.Count).Should().BeTrue(
                        $"round {roundNo}, {pilotName}: {header} cell '{cell}' names a slot task '{taskCode}' does not read (it reads {slots.Count} slot(s)) — such cells must be '—'");

                    captures.Add(ParseMmss(cell));
                    continue;
                }

                (cell == MarkerNoFlight || cell == MarkerZeroUnrecorded).Should().BeTrue(
                    $"round {roundNo}, {pilotName}: {header} cell '{cell}' is neither '—', m:ss, '{MarkerNoFlight}' nor '{MarkerZeroUnrecorded}'");

                (i == 0 && !unflown).Should().BeTrue(
                    $"round {roundNo}, {pilotName}: marker '{cell}' is only legal in Slot 1 of an unflown row");

                unflown = true;
            }

            if (unflown)
            {
                captures.Should().BeEmpty(
                    $"round {roundNo}, {pilotName}: a '{MarkerNoFlight}'/'{MarkerZeroUnrecorded}' row must carry no flight slot values");
            }

            var groupId = _groupIdByRoundAndGroup[(roundNo, 1)];
            var entryId = await PostAsync<EntryId>(
                "/open-entry",
                new OpenEntry(
                    _competitionId, _phaseOrdinal, _roundOrdinalByRoundNo[roundNo], 1,
                    groupId, _competitorByPilotNo[pilotNo]));

            _entryIdBySlot[(roundNo, 1, pilotNo)] = entryId;

            // kanban/in-progress/literal-record-f3k-sample-comp.md WI-2 — the
            // penalty arm: one POST per occurrence row (pilot 56's two rows →
            // two occurrences → one 200 deduction via PerOccurrence accrual),
            // exactly ReplayDriver.cs:741-748's payload.
            var penaltyCell = table.Header.Contains("Penalty") ? row["Penalty"] : null;

            if (penaltyCell is not null && penaltyCell != "—")
            {
                var parsed = int.TryParse(penaltyCell, NumberStyles.Integer, CultureInfo.InvariantCulture, out var penalty)
                             && penalty > 0;

                parsed.Should().BeTrue(
                    $"round {roundNo}, {pilotName}: Penalty cell '{penaltyCell}' is neither '—' nor a positive integer");

                await PostAsync<CompetitionId>(
                    "/record-competition-penalty",
                    new RecordCompetitionPenalty(
                        _competitionId, ReplayDriver.CompetitionPenaltyInfractionType, PenaltyScope.Competition,
                        _competitorByPilotNo[pilotNo], TaskRound: null, By: CdName));
            }

            for (var i = 0; i < captures.Count; i++)
            {
                await PostAsync<EntryId>("/open-flight", new OpenFlight(entryId));
                await PostAsync<EntryId>(
                    "/capture-measurement",
                    new CaptureMeasurement(entryId, i + 1, "flightTime", MeasuredValue.Of(captures[i])));
            }

            _enteredRows.Add(new EnteredRow(
                pilotNo, pilotName, roundNo,
                Time: null,
                Landing: table.Header.Contains("Landing") ? row["Landing"] : null,
                Laps: table.Header.Contains("Laps") ? row["Laps"] : null,
                Height: table.Header.Contains("Height") ? row["Height"] : null,
                Penalty: penaltyCell,
                Task: row["Task"],
                SlotCells: [.. Enumerable.Range(0, 7).Select(i => SlotCell(row, i))]));
        }
    }

    // ------------------------------------------------------------------ Then

    [Then(@"^the entered tables match the fixture's scores-raw exactly, cell for cell$")]
    public void ThenTheEnteredTablesMatchTheFixtureScoresRawExactlyCellForCell()
    {
        // Story decision 3's in-scenario self-check: the hand-authored tables
        // are diffed against scores-raw.json cell for cell, and EVERY mismatch
        // is collected (never first-only) with the cell named — an authoring
        // error must surface here, never disguised as a comparator failure.
        var mismatches = new List<string>();
        var rows = _fixture.ScoresRaw.Rows;

        var enteredKeys = _enteredRows.Select(r => (r.RoundNo, r.PilotNo)).ToHashSet();
        var fixtureKeys = rows.Select(r => (r.RoundNo, r.PilotNo)).ToHashSet();

        foreach (var entered in _enteredRows.Where(e => !fixtureKeys.Contains((e.RoundNo, e.PilotNo))))
        {
            mismatches.Add(
                $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered a row but the fixture has none");
        }

        foreach (var fixtureRow in rows.Where(f => !enteredKeys.Contains((f.RoundNo, f.PilotNo))))
        {
            mismatches.Add(
                $"scores-raw mismatch — round {fixtureRow.RoundNo}, {PilotName(fixtureRow.PilotNo)}: no row entered but the fixture has one");
        }

        if (_slotFamilyTables)
        {
            // kanban/in-progress/literal-record-f3k-sample-comp.md WI-3 — the
            // slot family's arm. The duration checks below must NOT run here:
            // their Time1Mins flown-rule and Time1Secs/Time2Mins/Time2Secs
            // neutrality would fire on this fixture's real slot values (§4
            // Then 6).
            SlotArmSelfCheck(rows, mismatches);
        }
        else
        {
            foreach (var entered in _enteredRows.Where(e => fixtureKeys.Contains((e.RoundNo, e.PilotNo))))
            {
                var fixtureRow = rows.Single(f => f.RoundNo == entered.RoundNo && f.PilotNo == entered.PilotNo);

                // Flown ⇔ marker: a fixture row is unflown iff Time1Mins <= 0 (the
                // CaptureDurationInputs rule) — a discrepancy is named both ways.
                var fixtureFlown = fixtureRow.Time1Mins > 0m;
                var enteredFlown = entered.Time is not null;

                if (fixtureFlown != enteredFlown)
                {
                    mismatches.Add(fixtureFlown
                        ? $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered 'no flight' but fixture says Time1Mins {fixtureRow.Time1Mins} (a flight)"
                        : $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered Time '{entered.Time}' but fixture says Time1Mins {fixtureRow.Time1Mins} (no flight)");
                    continue;
                }

                if (!enteredFlown)
                {
                    if (entered.Landing != "—")
                    {
                        mismatches.Add(
                            $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered Landing '{entered.Landing ?? "<none>"}' but fixture says '—' (no flight, fixture Landing is 0)");
                    }

                    continue;
                }

                // Flown rows: authored seconds == the packed-mmss decode, and the
                // authored Landing == the fixture value exactly (invariant decimal).
                var decodedSeconds = ReplayDriver.DecodePackedMinutesSeconds(fixtureRow.Time1Mins);
                var authoredSeconds = ParseMmss(entered.Time!);

                if (authoredSeconds != decodedSeconds)
                {
                    mismatches.Add(
                        $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered Time '{entered.Time}' but fixture says '{FormatMmss(decodedSeconds)}'");
                }

                if (!decimal.TryParse(
                        entered.Landing, NumberStyles.Number, CultureInfo.InvariantCulture, out var authoredLanding))
                {
                    mismatches.Add(
                        $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered Landing '{entered.Landing ?? "<none>"}' but fixture says '{fixtureRow.Landing}'");
                }
                else if (authoredLanding != fixtureRow.Landing)
                {
                    mismatches.Add(
                        $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered Landing '{entered.Landing}' but fixture says '{fixtureRow.Landing}'");
                }
            }

            // Omitted-column honesty: the m:ss format packs its own seconds, so
            // Time1Secs is unexpressible in any table here, as are the second-
            // timekeeper columns (trap 5); Laps, the deduction payload (Height
            // absent) and Penalty are checked only while their union columns are
            // absent from the fixture's tables. A non-zero value behind an omitted
            // column is a named mismatch, never a silent pass.
            foreach (var fixtureRow in rows)
            {
                void Neutral(decimal value, string column)
                {
                    if (value != 0m)
                    {
                        mismatches.Add(
                            $"scores-raw mismatch — round {fixtureRow.RoundNo}, {PilotName(fixtureRow.PilotNo)}: fixture carries {column} {value} but the tables have no {column} column");
                    }
                }

                if (!_enteredTableColumns.Contains("Laps"))
                {
                    Neutral(fixtureRow.Laps, "Laps");
                }

                if (!_enteredTableColumns.Contains("Height"))
                {
                    Neutral(fixtureRow.FlightScoreDeduction, "FlightScoreDeduction");
                }

                if (!_enteredTableColumns.Contains("Penalty"))
                {
                    Neutral(fixtureRow.Penalty, "Penalty");
                }

                Neutral(fixtureRow.Time1Secs, "Time1Secs");
                Neutral(fixtureRow.Time2Mins, "Time2Mins");
                Neutral(fixtureRow.Time2Secs, "Time2Secs");
            }
        }

        mismatches.Should().BeEmpty(
            "the hand-authored tables must reproduce scores-raw.json cell for cell — authoring errors surface HERE, never as comparator failures (story decision 3)"
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, mismatches)}");
    }

    // kanban/in-progress/literal-record-f3k-sample-comp.md WI-3 — the slot
    // family's self-check (§4 Then 6): the authored Task/Slot/Penalty cells are
    // diffed against scores-raw.json through the fixture's own task schedule
    // and slot map (the cells never drive behaviour), every mismatch collected
    // with its cell named. A row is FLOWN on the authored side iff it carries
    // m:ss flight cells; marker rows record unflown rows, and an unflown row
    // without a marker is honest only when every slot cell is '—'.
    private void SlotArmSelfCheck(ScoresRow[] rows, List<string> mismatches)
    {
        var schedule = ReplayDriver.TaskByRound(_fixture);

        foreach (var entered in _enteredRows.Where(e => rows.Any(f => f.RoundNo == e.RoundNo && f.PilotNo == e.PilotNo)))
        {
            var fixtureRow = rows.Single(f => f.RoundNo == entered.RoundNo && f.PilotNo == entered.PilotNo);

            if (entered.SlotCells.Count != 7)
            {
                mismatches.Add(
                    $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: the row was entered with {entered.SlotCells.Count} slot cells — a slot-family scenario's tables must all carry Slot 1..Slot 7");
                continue;
            }

            // Task cell: authored == the fixture schedule's code for the round —
            // a wrong Task cell surfaces only here.
            if (!schedule.TryGetValue(entered.RoundNo, out var taskCode))
            {
                mismatches.Add(
                    $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered Task '{entered.Task ?? "<none>"}' but the fixture's schedule names no task for the round");
                continue;
            }

            if (entered.Task != taskCode)
            {
                mismatches.Add(
                    $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered Task '{entered.Task ?? "<none>"}' but fixture schedule says '{taskCode}'");
            }

            if (!ReplayDriver.F3KSlotMap.TryGetValue(taskCode, out var slots))
            {
                mismatches.Add(
                    $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: fixture schedule names GS task '{taskCode}', which is not in the F3K slot-column capture map — widen F3KSlotMap with its CalcRawScoreF3K semantics first");
                continue;
            }

            var decodedSlots = slots
                .Select(column => ReplayDriver.DecodePackedMinutesSeconds(ReplayDriver.ColumnValue(fixtureRow, column)))
                .ToList();

            var marker = entered.SlotCells
                .Select((cell, index) => (cell, index))
                .FirstOrDefault(p => p.cell is MarkerNoFlight or MarkerZeroUnrecorded);

            var fixtureFlown = decodedSlots.Any(value => value != 0m);
            var enteredFlown = entered.SlotCells.Any(cell => cell is not null && MmssPattern.IsMatch(cell));

            // Flown ⇔ marker, slot edition: a fixture row is unflown iff ALL its
            // task's slots decode to 0 (X rows always are) — a discrepancy is
            // named both ways.
            if (fixtureFlown != enteredFlown)
            {
                mismatches.Add(fixtureFlown
                    ? $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered {(marker.cell is null ? "no flight values" : $"'{marker.cell}'")} but fixture says task '{taskCode}' slots decode non-zero ({string.Join(", ", decodedSlots.Select(FormatMmss))}) (a flight)"
                    : $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: entered flight values ({string.Join(", ", entered.SlotCells.Where(cell => cell is not null && MmssPattern.IsMatch(cell)))}) but fixture says task '{taskCode}' slots all decode zero (no flight)");
                continue;
            }

            if (enteredFlown)
            {
                // Flown rows carry no markers — markers record unflown rows only.
                if (marker.cell is not null)
                {
                    mismatches.Add(
                        $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: Slot {marker.index + 1} marker '{marker.cell}' on a flown row — markers record unflown rows only");
                }

                for (var i = 0; i < 7; i++)
                {
                    var cell = entered.SlotCells[i];

                    if (i == marker.index)
                    {
                        continue;
                    }

                    if (i >= slots.Count)
                    {
                        if (cell is not (null or "—"))
                        {
                            mismatches.Add(
                                $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: Slot {i + 1} cell '{cell}' names a slot task '{taskCode}' does not read (it reads {slots.Count} slot(s)) — must be '—'");
                        }

                        continue;
                    }

                    var fixtureDecoded = decodedSlots[i];
                    var fixturePacked = ReplayDriver.ColumnValue(fixtureRow, slots[i]);

                    if (cell is null or "—")
                    {
                        if (fixtureDecoded != 0m)
                        {
                            mismatches.Add(
                                $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: Slot {i + 1} cell '—' but fixture says '{FormatMmss(fixtureDecoded)}' (packed {fixturePacked})");
                        }

                        continue;
                    }

                    var mmss = MmssPattern.Match(cell);

                    if (!mmss.Success)
                    {
                        mismatches.Add(
                            $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: Slot {i + 1} cell '{cell}' is neither '—' nor m:ss");
                        continue;
                    }

                    if (fixtureDecoded == 0m)
                    {
                        mismatches.Add(
                            $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: Slot {i + 1} cell '{cell}' but fixture says '—' (packed {fixturePacked})");
                    }
                    else if (ParseMmss(cell) != fixtureDecoded)
                    {
                        mismatches.Add(
                            $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: Slot {i + 1} cell '{cell}' but fixture says '{FormatMmss(fixtureDecoded)}' (packed {fixturePacked})");
                    }
                }
            }
            else
            {
                // Marker discipline (decision 2, machine-checked —
                // literal-record-f3k-sample-comp.md WI-3): 'no flight' is the
                // provenance-attested NoTaskSet arm, valid only on a row whose
                // task code is 'X'; 'zero (unrecorded)' is the undecidable arm,
                // valid only on an unflown row whose task is NOT 'X' (here
                // exactly one — R4 / pilot 42). A marker on the wrong arm is a
                // named mismatch.
                if (marker.cell is not null)
                {
                    var markerOnRightArm = marker.cell == MarkerNoFlight
                        ? taskCode == "X"
                        : taskCode != "X";

                    if (!markerOnRightArm)
                    {
                        mismatches.Add(
                            $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: marker '{marker.cell}' on task '{taskCode}' — '{MarkerNoFlight}' is valid only on 'X' rows, '{MarkerZeroUnrecorded}' only on unflown non-'X' rows");
                    }
                }

                // Unflown rows record no flights: every slot cell beyond the
                // marker must be '—' (markerless unflown rows included).
                for (var i = 0; i < 7; i++)
                {
                    if (i == marker.index)
                    {
                        continue;
                    }

                    if (entered.SlotCells[i] is not (null or "—"))
                    {
                        mismatches.Add(
                            $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: Slot {i + 1} cell '{entered.SlotCells[i]}' on an unflown row must be '—'");
                    }
                }
            }

            // Penalty: authored '—' ⇔ 0, integer otherwise, and it must equal
            // the fixture row's Penalty exactly.
            if (entered.Penalty is null or "—")
            {
                if (fixtureRow.Penalty != 0)
                {
                    mismatches.Add(
                        $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: Penalty cell '—' but fixture says {fixtureRow.Penalty}");
                }
            }
            else if (int.TryParse(entered.Penalty, NumberStyles.Integer, CultureInfo.InvariantCulture, out var authoredPenalty))
            {
                if (authoredPenalty != fixtureRow.Penalty)
                {
                    mismatches.Add(
                        $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: Penalty cell '{entered.Penalty}' but fixture says {fixtureRow.Penalty}");
                }
            }
            else
            {
                mismatches.Add(
                    $"scores-raw mismatch — round {entered.RoundNo}, {entered.PilotName}: Penalty cell '{entered.Penalty}' is neither '—' nor an integer");
            }
        }

        // Omitted-column honesty, slot edition: all seven raw columns are
        // potentially slots, so the neutrality question is which columns the
        // fixture's OWN schedule maps — every column no task maps must be
        // all-zero in scores-raw. Generic, never fixture-named (here the set is
        // empty — task D maps all seven).
        var mappedColumns = new HashSet<string>();

        foreach (var code in schedule.Values)
        {
            if (ReplayDriver.F3KSlotMap.TryGetValue(code, out var slotList))
            {
                foreach (var column in slotList)
                {
                    mappedColumns.Add(column);
                }
            }
        }

        foreach (var column in RawSlotColumns.Where(c => !mappedColumns.Contains(c)))
        {
            foreach (var fixtureRow in rows)
            {
                var value = ReplayDriver.ColumnValue(fixtureRow, column);

                if (value != 0m)
                {
                    mismatches.Add(
                        $"scores-raw mismatch — round {fixtureRow.RoundNo}, {PilotName(fixtureRow.PilotNo)}: fixture carries {column} {value} but no task in the fixture's schedule maps {column}");
                }
            }
        }
    }

    [Then(@"^round (\d+) is completed and scored$")]
    public async Task ThenRoundIsCompletedAndScored(int roundNo)
    {
        roundNo.Should().BeLessThanOrEqualTo(_roundOrdinalByRoundNo.Count,
            "only a prescribed round can be completed");

        // "Scored" is narrative — scoring is a read model; nothing to POST.
        await PostAsync<CompetitionId>(
            "/complete-task-round",
            new CompleteTaskRound(_competitionId, _phaseOrdinal, RoundOrdinal: roundNo, 1));
    }

    [Then(@"^the competition is finalised$")]
    public async Task ThenTheCompetitionIsFinalised()
    {
        await PostAsync<CompetitionId>("/finalise-competition", new FinaliseCompetition(_competitionId, CdName));

        // The referee's ReplayOutcome — the same fields, keyed by the same
        // fixture coordinates, accumulated exactly as ReplayDriver's is — kept
        // for the three-grain comparison step.
        _outcome = new ReplayOutcome(
            CompetitionId: _competitionId,
            PhaseOrdinal: _phaseOrdinal,
            TaskCodeByRoundNo: _taskCodeByRoundNo,
            RoundOrdinalByRoundNo: _roundOrdinalByRoundNo,
            GroupIdByRoundAndGroup: _groupIdByRoundAndGroup,
            EntryIdBySlot: _entryIdBySlot,
            CompetitorByPilotNo: _competitorByPilotNo,
            CommandsIssued: _commandsIssued,
            DefinitionContentHash: _contentHash);

        _outcome.CompetitionId.Should().Be(_competitionId,
            "the referee's outcome must name the competition this scenario just finalised");
    }

    /// <summary>
    /// literal-record-replay-scenarios.md WI-4 — decision 6's literal table:
    /// place, name, score, the dropped witness and the penalty column pinned
    /// exactly as GS displayed them. The Dropped cell is the load-bearing
    /// check — where every dropped candidate is a zero cell, conservation
    /// alone cannot catch a wrong-round drop, so the cell must be the
    /// engine's own dropped rounds
    /// (kanban/in-progress/literal-record-f3k-sample-comp.md WI-4); the
    /// witness CellSum − DroppedSum − PenaltyDeduction == FinalScore is the
    /// identity Comparator.CheckConservation asserts independently as the
    /// referee. All arms are data-driven off the table's columns and the
    /// engine — an ales table (no Penalty column, '—' drops) reduces to the
    /// original nothing-dropped form.
    /// </summary>
    [Then(@"^the final placings are$")]
    public async Task ThenTheFinalPlacingsAre(Table table)
    {
        var finalScores = await ApiClient.GetAsync<CompetitionScoreView>(
            AcceptanceFixture.Client, $"/competition-result?competitionRef={_competitionId.Value}");

        var conservationRows = await Comparator.ConservationByCompetitor(
            _outcome, AcceptanceFixture.EventStore, AcceptanceFixture.Client);
        var conservationByCompetitor = conservationRows.ToDictionary(r => r.Competitor);

        // Name → PilotNo → CompetitorId. The full universe must be covered so
        // the tie-group checks below are complete.
        var competitorByName = _pilotNameByNo.ToDictionary(kv => kv.Value, kv => _competitorByPilotNo[kv.Key]);

        table.Rows.Should().HaveCount(_competitorByPilotNo.Count,
            "the literal placings table must cover the full competitor universe, or the tie-group checks are incomplete");

        var names = table.Rows.Select(r => r["Name"]).ToList();
        names.Should().OnlyHaveUniqueItems("duplicate names would make the tie-group checks ambiguous");

        var tableNamesByPlace = new Dictionary<int, List<string>>();

        foreach (var row in table.Rows)
        {
            var name = row["Name"];

            competitorByName.TryGetValue(name, out var competitorId).Should().BeTrue(
                $"unknown placings-table name '{name}' — the fixture's pilots are [{string.Join(", ", competitorByName.Keys.Order())}]");

            var place = int.Parse(row["Place"].TrimStart('='), CultureInfo.InvariantCulture);

            if (!tableNamesByPlace.TryGetValue(place, out var atPlace))
            {
                tableNamesByPlace[place] = atPlace = [];
            }

            atPlace.Add(name);

            var conservationRow = conservationByCompetitor.GetValueOrDefault(competitorId);
            conservationRow.Should().NotBeNull(
                $"{name} must carry a conservation row — every competitor in the universe is scored");

            var expectedDroppedCell = conservationRow!.DroppedRoundOrdinals.Count == 0
                ? "—"
                : "Rnd" + string.Join(", ", conservationRow.DroppedRoundOrdinals);

            row["Dropped"].Should().Be(expectedDroppedCell,
                $"{name}: the Dropped cell must be the engine's own dropped rounds "
                + $"(engine dropped [{string.Join(", ", conservationRow.DroppedRoundOrdinals)}]) — conservation "
                + "alone cannot catch a wrong-round zero drop");
        }

        var scoreByCompetitor = finalScores.Scores.ToDictionary(s => s.CompetitorRef);
        var pilotNoByCompetitor = _competitorByPilotNo.ToDictionary(kv => kv.Value, kv => kv.Key);
        var hasPenaltyColumn = table.Header.Contains("Penalty");

        string CompetitorName(CompetitorId competitorId) =>
            pilotNoByCompetitor.TryGetValue(competitorId, out var pilotNo) ? PilotName(pilotNo) : competitorId.ToString();

        foreach (var row in table.Rows)
        {
            var competitorId = competitorByName[row["Name"]];
            var final = scoreByCompetitor[competitorId];
            var place = int.Parse(row["Place"].TrimStart('='), CultureInfo.InvariantCulture);

            final.Placing.Should().Be(place,
                $"{row["Name"]}: the engine's placing must match the literal table");
            final.Score.Should().Be(decimal.Parse(row["Score"], CultureInfo.InvariantCulture),
                $"{row["Name"]}: the engine's final score must match the literal table EXACTLY");

            if (!hasPenaltyColumn)
            {
                continue;
            }

            int.TryParse(row["Penalty"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var penalty).Should()
                .BeTrue($"{row["Name"]}: Penalty cell '{row["Penalty"]}' must be an invariant integer");

            conservationByCompetitor[competitorId].PenaltyDeduction.Should().Be(penalty,
                $"{row["Name"]}: the engine's aggregate-penalty deduction must match the literal Penalty cell");

            penalty.Should().Be(
                _fixture.ScoresRaw.Rows.Where(r => r.PilotNo == pilotNoByCompetitor[competitorId]).Sum(r => r.Penalty),
                $"{row["Name"]}: the literal Penalty cell must equal the fixture's Σ Penalty for the pilot");
        }

        // Tie integrity: the names the table shows at each place must be
        // exactly the competitors the engine places there.
        var engineNamesByPlace = finalScores.Scores
            .Where(s => s.Placing.HasValue)
            .GroupBy(s => s.Placing!.Value)
            .ToDictionary(g => g.Key, g => g.Select(s => CompetitorName(s.CompetitorRef)).ToHashSet());

        foreach (var (place, tableNames) in tableNamesByPlace)
        {
            var engineNames = engineNamesByPlace.GetValueOrDefault(place, []);

            tableNames.ToHashSet().SetEquals(engineNames).Should().BeTrue(
                $"place {place}: the tie group must contain exactly the competitors the engine places there — "
                + $"table [{string.Join(", ", tableNames)}] vs engine [{string.Join(", ", engineNames.Order())}]");
        }

        // The conservation witness, per competitor: Σ post-normalisation
        // per-round cells (ScoreTaskRound.cs:26-27 — the view's RawScore
        // carries the post-normalisation value) − the dropped cells'
        // contributions − aggregate penalties == the engine's final Score
        // exactly (Comparator.CheckConservation asserts the same identity
        // independently); the ales nothing-dropped, no-penalty tables reduce
        // it to the original Σ cells == Score.
        foreach (var competitorId in _competitorByPilotNo.Values)
        {
            var name = CompetitorName(competitorId);
            var conservationRow = conservationByCompetitor.GetValueOrDefault(competitorId);
            conservationRow.Should().NotBeNull(
                $"{name} must carry a conservation row — the competitor must appear in the scored task-rounds");

            (conservationRow!.CellSum - conservationRow.DroppedSum - conservationRow.PenaltyDeduction)
                .Should().Be(conservationRow.FinalScore,
                $"{name}: Σ cells {conservationRow.CellSum} − dropped {conservationRow.DroppedSum} − penalties "
                + $"{conservationRow.PenaltyDeduction} must equal the engine's final Score exactly");
        }
    }

    [Then(@"^the three-grain oracle comparison over the same competition still runs exact$")]
    public async Task ThenTheThreeGrainOracleComparisonOverTheSameCompetitionStillRunsExact()
    {
        // Story decision 6, belt and braces: the literal placings sit BESIDE
        // the JSON harness's machinery, which stays the referee — the same
        // three-grain exact comparison the ReplayingAGliderscoreFixture
        // scenarios run, over the competition this scenario drove by hand.
        var report = await Comparator.CompareAsync(
            _fixture, _outcome, AcceptanceFixture.EventStore, AcceptanceFixture.Client);

        report.AllGrainsExact.Should().BeTrue(
            "the literal record must reproduce the same competition the JSON harness proves exact"
            + $"{Environment.NewLine}{report.DiffTable()}");

        report.Conserves.Should().BeTrue(
            "score conservation must hold for every competitor"
            + $"{Environment.NewLine}{report.ConservationTable()}");

        // kanban/in-progress/literal-record-f3k-sample-comp.md WI-5 — the
        // referee may be excused exactly the committed ledger: _fixture
        // .Divergences (the fixture's triaged divergences.json) is compared
        // against the excused set the run carries on SubtractLedger's own
        // identity (grain case-insensitive; round, group and pilotNo
        // null-or-covers — Comparator.SubtractLedger), so no extra divergence
        // rides beyond the committed entries and none is missing: every entry
        // must excuse a real divergence the unledgered run carried (a
        // ledger-free mirror re-run through SubtractLedger's rule), a T1 entry
        // excuses its documentary non-run instead. For ales the ledger is
        // empty and every arm runs over nothing — the old empty-ledger
        // behaviour.
        var committed = _fixture.Divergences;
        var triage = _fixture.Competition.Triage;
        var violations = new List<string>();

        for (var i = 0; i < committed.Count; i++)
        {
            var entry = committed[i];
            var excerpt = entry.Reason is { } reasonText && reasonText.Length > 60
                ? reasonText[..60] + "…"
                : entry.Reason ?? "";
            var named = $"entry {i} (grain '{entry.Grain}', reason '{excerpt}')";

            if (string.IsNullOrWhiteSpace(entry.Grain))
            {
                violations.Add(
                    $"{named}: expected a non-empty grain SubtractLedger can match, found '{entry.Grain}'");
            }

            var pilotFormed = entry.PilotNo is not { } pilot
                || pilot.ValueKind == JsonValueKind.Number && pilot.TryGetInt64(out _)
                || pilot.ValueKind == JsonValueKind.String && pilot.GetString() == "*";

            if (!pilotFormed)
            {
                violations.Add(
                    $"{named}: expected PilotNo to be a pilot number or \"*\" (SubtractLedger's Covers), "
                    + $"found {entry.PilotNo?.GetRawText() ?? "null"}");
            }

            if (string.IsNullOrWhiteSpace(entry.Reason))
            {
                violations.Add(
                    $"{named}: expected a triage-cited reason (an entry lands only after human triage), found none");
            }
            else
            {
                if (!TriageCitationPattern.IsMatch(entry.Reason))
                {
                    violations.Add(
                        $"{named}: expected the reason to cite a triage decision (D1..D6 / trap 3 / R1 / T1), found '{entry.Reason}'");
                }

                var t1Cited = Regex.IsMatch(entry.Reason, @"\bT1\b", RegexOptions.IgnoreCase);

                if (t1Cited)
                {
                    if (triage?.UseTeams != true)
                    {
                        violations.Add(
                            $"{named}: expected the fixture's triage block to declare UseTeams=true for a T1-cited entry, "
                            + $"found {(triage is null ? "no triage block" : triage.UseTeams?.ToString() ?? "null")}");
                    }

                    if (triage?.NbrForTeamScore is not { } declaredTeam)
                    {
                        violations.Add(
                            $"{named}: expected the fixture's triage block to declare NbrForTeamScore for a T1-cited entry, found none");
                    }
                    else
                    {
                        if (!entry.Reason.Contains("teams-mvp.md", StringComparison.OrdinalIgnoreCase)
                            || !entry.Reason.Contains("decision 8", StringComparison.OrdinalIgnoreCase))
                        {
                            violations.Add(
                                $"{named}: expected the T1 reason to cite teams-mvp.md owner decision 8, found '{entry.Reason}'");
                        }

                        if (!entry.Reason.Contains($"NbrForTeamScore={declaredTeam}", StringComparison.OrdinalIgnoreCase))
                        {
                            violations.Add(
                                $"{named}: expected the T1 reason to name the declared NbrForTeamScore={declaredTeam}, found '{entry.Reason}'");
                        }

                        if (report.TeamsCompared != 0)
                        {
                            violations.Add(
                                $"{named}: expected the team grain not to have run (T1: the declared method is never emulated), "
                                + $"found {report.TeamsCompared} standing(s) compared");
                        }
                    }
                }
            }
        }

        violations.AddRange(committed
            .GroupBy(entry => (
                Grain: entry.Grain.Trim().ToLowerInvariant(),
                entry.Round,
                entry.Group,
                Pilot: IdentityPilotNo(entry)))
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"grain '{group.Key.Grain}' (round {group.Key.Round?.ToString(CultureInfo.InvariantCulture) ?? "any"}, "
                + $"group {group.Key.Group?.ToString(CultureInfo.InvariantCulture) ?? "any"}, pilot {group.Key.Pilot}): "
                + $"expected one committed entry per SubtractLedger identity, found {group.Count()}"));

        if (triage?.UseTeams == true && triage.NbrForTeamScore is { } declaredMethod && declaredMethod != 3)
        {
            var t1Count = committed
                .Count(entry => entry.Reason is { } citedReason
                    && Regex.IsMatch(citedReason, @"\bT1\b", RegexOptions.IgnoreCase));

            if (t1Count != 1)
            {
                violations.Add(
                    $"expected exactly one documentary T1 entry excusing the declared team method "
                    + $"(UseTeams=true, NbrForTeamScore={declaredMethod} — the grain does not run; "
                    + $"teams-mvp.md owner decision 8), found {t1Count}");
            }
        }

        if (committed.Count > 0)
        {
            var unledgeredFixture = _fixture with { Divergences = [] };
            var unledgeredReport = await Comparator.CompareAsync(
                unledgeredFixture, _outcome, AcceptanceFixture.EventStore, AcceptanceFixture.Client);
            var unledgeredMismatches = unledgeredReport.RawMismatches
                .Concat(unledgeredReport.NormalisedMismatches)
                .Concat(unledgeredReport.RankingMismatches)
                .ToList();

            foreach (var entry in committed)
            {
                if (entry.Reason is { } citedReason
                    && Regex.IsMatch(citedReason, @"\bT1\b", RegexOptions.IgnoreCase))
                {
                    continue;
                }

                var excusedCount = unledgeredMismatches.Count(mismatch =>
                    entry.Grain.Equals(mismatch.Grain, StringComparison.OrdinalIgnoreCase)
                    && (entry.Round is null || entry.Round == mismatch.RoundNo)
                    && (entry.Group is null || entry.Group == mismatch.GroupNo)
                    && (entry.PilotNo is null || entry.Covers(mismatch.PilotNo)));

                if (excusedCount == 0)
                {
                    violations.Add(
                        $"grain '{entry.Grain}' (round {entry.Round?.ToString(CultureInfo.InvariantCulture) ?? "any"}, "
                        + $"group {entry.Group?.ToString(CultureInfo.InvariantCulture) ?? "any"}, pilot {IdentityPilotNo(entry)}): "
                        + $"expected the committed entry to excuse a real divergence of the unledgered run "
                        + $"({unledgeredMismatches.Count} mismatch(es) carried), found none — "
                        + "an extra divergence beyond the excused set");
                }
            }
        }

        violations.Should().BeEmpty(
            "the referee may be excused exactly the committed ledger — no extra divergence beyond the committed entries, none missing:"
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    // ----------------------------------------------------------------- helpers

    private string PilotName(long pilotNo) => _pilotNameByNo.GetValueOrDefault(pilotNo, $"pilot {pilotNo}");

    // kanban/in-progress/literal-record-f3k-sample-comp.md WI-5 — a committed
    // ledger entry's SubtractLedger identity, rendered for the excused-set
    // comparison (pilot as Covers sees it: number or "*").
    private static string IdentityPilotNo(DivergenceEntry entry) =>
        entry.PilotNo is not { } pilot ? "any"
        : pilot.ValueKind == JsonValueKind.Number && pilot.TryGetInt64(out var number)
            ? number.ToString(CultureInfo.InvariantCulture)
        : pilot.ValueKind == JsonValueKind.String ? pilot.GetString() ?? "any"
        : pilot.GetRawText();

    private static decimal ParseMmss(string cell)
    {
        var match = MmssPattern.Match(cell);

        return decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 60m
               + decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
    }

    private static string FormatMmss(decimal seconds)
    {
        var minutes = Math.Truncate(seconds / 60m);

        return $"{minutes}:{seconds - minutes * 60m:00}";
    }

    private async Task<T> PostAsync<T>(string path, object command)
    {
        _commandsIssued++;

        return await ApiClient.PostCommandAsync<T>(AcceptanceFixture.Client, path, command);
    }
}
