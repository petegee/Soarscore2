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
    private int _commandsIssued;                                   // honesty only, unasserted
    private ReplayOutcome _outcome = null!;

    // One authored entry-block row, exactly as the feature table carries it.
    // Time is null when the row is the explicit "no flight" marker; the
    // Laps/Height/Penalty cells are null when that column is absent from the
    // fixture's tables.
    private sealed record EnteredRow(
        long PilotNo, string PilotName, int RoundNo,
        string? Time, string? Landing, string? Laps, string? Height, string? Penalty);

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
                Penalty: table.Header.Contains("Penalty") ? row["Penalty"] : null));
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

        mismatches.Should().BeEmpty(
            "the hand-authored tables must reproduce scores-raw.json cell for cell — authoring errors surface HERE, never as comparator failures (story decision 3)"
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, mismatches)}");
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
    /// place, name, score and the dropped column pinned exactly as GS
    /// displayed them. The nothing-dropped witness (Σ of each competitor's
    /// per-round normalised cells == the engine's final Score) holds only
    /// because ales configures no drops and no penalties; a drop-bearing
    /// fixture widens this check to the engine's own dropped-cell
    /// contributions — the conservation machinery already knows them
    /// (Comparator.CheckConservation).
    /// </summary>
    [Then(@"^the final placings are$")]
    public async Task ThenTheFinalPlacingsAre(Table table)
    {
        var finalScores = await ApiClient.GetAsync<CompetitionScoreView>(
            AcceptanceFixture.Client, $"/competition-result?competitionRef={_competitionId.Value}");

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

            row["Dropped"].Should().Be("—",
                "ales configures no drops (Drop1AtRound..Drop5AtRound all 99)");
        }

        var scoreByCompetitor = finalScores.Scores.ToDictionary(s => s.CompetitorRef);
        var pilotNoByCompetitor = _competitorByPilotNo.ToDictionary(kv => kv.Value, kv => kv.Key);

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

        // The nothing-dropped witness: Σ of each competitor's per-round
        // post-normalisation RawScore (ScoreTaskRound.cs:26-27 — the view's
        // RawScore carries the post-normalisation value) across rounds must
        // equal the engine's final Score exactly.
        _fixture.ScoresRaw.Rows.Where(r => r.Penalty != 0).Should().BeEmpty(
            "ales records no penalties — a penalty-bearing fixture widens the nothing-dropped witness");

        var cellSums = new Dictionary<CompetitorId, decimal>();

        foreach (var roundNo in _roundOrdinalByRoundNo.Keys.OrderBy(n => n))
        {
            var views = await ApiClient.GetAsync<IReadOnlyList<GroupScoreView>>(
                AcceptanceFixture.Client,
                $"/task-round-result?competitionRef={_competitionId.Value}"
                + $"&phaseOrdinal={_phaseOrdinal}"
                + $"&roundOrdinal={_roundOrdinalByRoundNo[roundNo]}"
                + "&taskRoundOrdinal=1");

            foreach (var view in views)
            {
                foreach (var result in view.Results)
                {
                    cellSums[result.CompetitorRef] = cellSums.GetValueOrDefault(result.CompetitorRef) + result.RawScore;
                }
            }
        }

        foreach (var competitorId in _competitorByPilotNo.Values)
        {
            var name = CompetitorName(competitorId);

            cellSums.TryGetValue(competitorId, out var cellSum).Should().BeTrue(
                $"{name} must appear in every scored task-round");

            cellSum.Should().Be(scoreByCompetitor[competitorId].Score,
                $"{name}: Σ of the per-round normalised cells must equal the engine's final Score exactly (nothing dropped, nothing penalised)");
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

        _fixture.Divergences.Should().BeEmpty(
            "ales's ledger must stay so — a populated ledger means the referee was excused a real mismatch");
    }

    // ----------------------------------------------------------------- helpers

    private string PilotName(long pilotNo) => _pilotNameByNo.GetValueOrDefault(pilotNo, $"pilot {pilotNo}");

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
