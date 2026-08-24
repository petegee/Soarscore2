// kanban/completed/capture-a-score-steel-thread-plan.md WI-13 — step definitions
// for Features/CapturingAScore.feature. Every Given/When step that mutates
// state goes through AcceptanceFixture.Client, real HTTP against the real
// Soarscore.Api (Microsoft.AspNetCore.Mvc.Testing) over a real Testcontainers
// PostgreSQL. Then steps that need an Entry's full folded state fall back to
// EntryReader's direct stream read — see EntryReader.cs's header for why (no
// GetEntry query exists yet).
//
// One instance of this class per scenario (Reqnroll's default binding
// lifetime), so the private fields below are safely scenario-scoped even
// though AcceptanceFixture's HttpClient/IEventStore are shared run-wide.

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class CapturingAScoreSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private ClassDefinition? _classDefinition;
    private string? _classContentHash;
    private CompetitionId _competitionId;
    private readonly List<CompetitorId> _competitors = [];
    private readonly Dictionary<(int Round, int Group), GroupId> _groupIds = new();
    private EntryId _entryId;

    // Populated by the amend scenario's full-flight When step, keyed by
    // competitor ordinal — the correction must be assertable back against the
    // specific entry it was made on, not the last entry opened.
    private readonly Dictionary<int, EntryId> _entryIds = new();

    // Populated by the refused-penalty scenario's When step, read by its Then.
    private HttpResponseMessage? _rawResponse;

    // ---------------------------------------------------------------- Given

    [Given(@"^a published (.+) class definition$")]
    public async Task GivenAPublishedClassDefinition(string className)
    {
        _classDefinition = ResolveDefinition(className);
        _classContentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(_classDefinition));
    }

    [Given(@"^a competition adopting it with (\d+) registered competitors$")]
    public async Task GivenACompetitionAdoptingItWithRegisteredCompetitors(int count)
    {
        // Person.IsPlausibleEmail (Person.cs) rejects any whitespace, so the
        // per-scenario uniqueness slug used in pilot emails must be
        // whitespace-free even though the competition's own Name field has no
        // such restriction.
        var slug = Guid.NewGuid().ToString("N");
        var competitionName = $"Acceptance {slug}";
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition(competitionName, "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), _classContentHash!));

        for (var i = 0; i < count; i++)
        {
            var email = $"pilot-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }
    }

    [Given(@"^groupSize bound to (\d+) by the contest director$")]
    public async Task GivenGroupSizeBoundToByTheContestDirector(int groupSize)
    {
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/bind-parameter",
            new BindParameter(_competitionId, "groupSize", MeasuredValue.Of((decimal)groupSize), "The contest director"));
    }

    [Given(@"^a drawn preliminary phase of (\d+) rounds?$")]
    public async Task GivenADrawnPreliminaryPhaseOfRounds(int rounds)
    {
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, rounds));
    }

    // For a ChooseFromCatalogue phase (F3K): the table names the task the CD
    // picked for each round, in round order — DrawPhase's taskRefs. Round
    // ordinals in the table are expected 1..N with no gaps; only the order
    // of the rows is read, not the "round" column's values themselves.
    [Given(@"^a drawn preliminary phase with these tasks$")]
    public async Task GivenADrawnPreliminaryPhaseWithTheseTasks(Table table)
    {
        var taskRefs = table.Rows.Select(row => row["task"]).ToList();
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/draw-phase", new DrawPhase(_competitionId, taskRefs.Count, taskRefs));
    }

    // ----------------------------------------------------------------- When

    [When(@"^the scorer opens an entry for competitor (\d+) in round (\d+), group (\d+)$")]
    public async Task WhenTheScorerOpensAnEntryForCompetitorInRoundGroup(int competitorOrdinal, int roundOrdinal, int groupOrdinal)
    {
        var groupId = await ResolveGroupIdAsync(roundOrdinal, groupOrdinal);
        var competitorId = _competitors[competitorOrdinal - 1];

        // Phase.Ordinal is Phases.Length AT DRAW TIME (Competition.DrawPhase),
        // so the first (and, in this feature, only) phase ever drawn is
        // Ordinal 0 — unlike Round/TaskRound/Group, which are 1-based. Same
        // nuance EntryCaptureEventStoreTests.cs documents (WI-12).
        _entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, roundOrdinal, 1, groupId, competitorId));
    }

    [When(@"^the scorer opens a flight$")]
    public async Task WhenTheScorerOpensAFlight()
    {
        // Opening a flight carries no caller-supplied fact at all since
        // kanban/completed/remove-flight-launchat.md — no launch instant, and
        // the sequence is derived from the Entry's own state.
        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(_entryId));
    }

    [When(@"^the scorer records that the launch was outside the working time$")]
    public async Task WhenTheScorerRecordsThatTheLaunchWasOutsideTheWorkingTime()
    {
        // The F3K.7 regression, driven over real HTTP. A launch before the
        // working time began scores zero — it is not refused — and the fact
        // travels as a captured observation the class declares
        // (`launchedInWorkingTime`, SeedF3K.cs:22), never as a window check in
        // the core system. This step is what stops that rule migrating out of
        // the class model.
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/capture-measurement",
            new CaptureMeasurement(_entryId, 1, "launchedInWorkingTime", MeasuredValue.Of(false)));
    }

    [When(@"^the scorer captures flightTime of (\d+) seconds$")]
    public async Task WhenTheScorerCapturesFlightTimeOfSeconds(int seconds)
    {
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/capture-measurement", new CaptureMeasurement(_entryId, 1, "flightTime", MeasuredValue.Of((decimal)seconds)));
    }

    // WI-6 (kanban/in-progress/out-of-order-flight-entry.md, decision 4): the
    // explicit launch label. Sequence is "which launch this was", not when it
    // was typed — posting 2 before 1 must be accepted, which is that story's
    // whole point.
    [When(@"^the scorer opens flight (\d+)$")]
    public async Task WhenTheScorerOpensFlight(int sequence)
    {
        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(_entryId, sequence));
    }

    // WI-6. "Valid" carries every fact the task's FlightValidWhen demands
    // (SeedF3K.cs: launchedInWorkingTime per F3K.7, landedWithinWindow per
    // F3K.9.3). A flight missing either is silently zeroed at scoring while
    // still counting as Valid — so without these flags the out-of-order
    // scenarios below could pass on all-zero cards.
    [When(@"^the scorer records a valid (\d+) second flight on flight (\d+)$")]
    public async Task WhenTheScorerRecordsAValidSecondFlightOnFlight(int seconds, int sequence)
    {
        foreach (var flag in new[] { "launchedInWorkingTime", "landedWithinWindow" })
        {
            await ApiClient.PostCommandAsync<EntryId>(
                Client, "/capture-measurement",
                new CaptureMeasurement(_entryId, sequence, flag, MeasuredValue.Of(true)));
        }

        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/capture-measurement",
            new CaptureMeasurement(_entryId, sequence, "flightTime", MeasuredValue.Of((decimal)seconds)));
    }

    // WI-6, the duplicate refusal: raw response held for its Then to inspect,
    // mirroring the undeclared-penalty pair at the bottom of this file.
    [When(@"^the scorer opens flight (\d+) again$")]
    public async Task WhenTheScorerOpensFlightAgain(int sequence)
    {
        _rawResponse = await ApiClient.PostCommandRawAsync(
            Client, "/open-flight", new OpenFlight(_entryId, sequence));
    }

    /// <summary>
    /// The amend scenario's composite When: opens competitor's entry and its
    /// one flight, captures a flight time, then captures the rest of F5J's
    /// declared metrics (each contributing zero to the raw score, so raw ==
    /// flightTime — the same convention ScoringACompetitionSteps.cs's header
    /// records). Keeping the corresponding EntryId by competitor ordinal lets
    /// the Then steps read the exact entry the correction was made on.
    /// </summary>
    [When(@"^the scorer records a full F5J flight for competitor (\d+) with a flight time of (\d+) seconds$")]
    public async Task WhenTheScorerRecordsAFullF5JFlightForCompetitor(int competitorOrdinal, int seconds)
    {
        await WhenTheScorerOpensAnEntryForCompetitorInRoundGroup(competitorOrdinal, 1, 1);
        _entryIds[competitorOrdinal] = _entryId;
        await WhenTheScorerOpensAFlight();
        await WhenTheScorerCapturesFlightTimeOfSeconds(seconds);
        await CaptureTheOtherF5JMetricsAsync();
    }

    [When(@"^the scorer corrects the flight time to (\d+) seconds$")]
    public async Task WhenTheScorerCorrectsTheFlightTimeToSeconds(int seconds)
    {
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/amend-measurement",
            new AmendMeasurement(_entryId, 1, "flightTime", MeasuredValue.Of((decimal)seconds),
                "mistyped the flight time", "the contest director"));
    }

    [When(@"^the jury annuls the entry for a recorded reason$")]
    public async Task WhenTheJuryAnnullsTheEntryForARecordedReason()
    {
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/annul-entry",
            new AnnulEntry(_entryId, "the competitor re-flew under protest", "the jury"));
    }

    [When(@"^the scorer records an entry penalty with an undeclared infraction type$")]
    public async Task WhenTheScorerRecordsAnEntryPenaltyWithAnUndeclaredInfractionType()
    {
        _rawResponse = await ApiClient.PostCommandRawAsync(
            Client, "/record-entry-penalty",
            new RecordEntryPenalty(_entryId, "madeUp", PenaltyScope.Flight, null));
    }

    // ----------------------------------------------------------------- Then

    [Then(@"^the entry holds one flight with a flightTime of (\d+)$")]
    public async Task ThenTheEntryHoldsOneFlightWithAFlightTimeOf(int expectedSeconds)
    {
        var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryId, TestContext.Current.CancellationToken);

        entry.Flights.Should().ContainSingle();
        entry.Flights[0].Measurements.Should().ContainSingle(m => m.Metric == "flightTime");
        entry.Flights[0].Measurements[0].Value.Should().Be(MeasuredValue.Of((decimal)expectedSeconds));
    }

    [Then(@"^the entry appears in the index for round (\d+), group (\d+)$")]
    public async Task ThenTheEntryAppearsInTheIndexForRoundGroup(int roundOrdinal, int groupOrdinal)
    {
        var groupId = await ResolveGroupIdAsync(roundOrdinal, groupOrdinal);
        var url = $"/entries?competitionRef={_competitionId.Value}&phaseOrdinal=0&roundOrdinal={roundOrdinal}&taskRoundOrdinal=1&groupRef={groupId.Value}";

        var matches = await ApiClient.GetAsync<List<EntrySummary>>(Client, url);

        matches.Should().ContainSingle(e => e.Id == _entryId);
    }

    [Then(@"^the task leaves the working time open-ended$")]
    public async Task ThenTheTaskLeavesTheWorkingTimeOpenEnded()
    {
        // Absence is the truthful encoding under UntilAllFlightsComplete: the
        // round ends when the last flight does, so there is no stored
        // clock-stamped window to check (removed by
        // kanban/completed/remove-stored-working-time.md — it was manufactured
        // from whoever-opened-the-entry's wall clock, never a fact anyone
        // observed). The truth now lives where the rule does — the adopted
        // class definition: the timing kind leaves the working time open, and
        // the model encodes that as genuine absence, a null, not a default.
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var competition = view.Competition;

        // The same ordinals the When step opened the entry against — phase 0,
        // round 1, task-round 1 (Competition.Phase is 0-based at draw time;
        // Round and TaskRound are 1-based; see WhenTheScorerOpensAnEntry...).
        var phase = competition.Phases.Single(p => p.Ordinal == 0);
        var round = phase.Rounds.Single(r => r.Ordinal == 1);
        var taskRound = round.TaskRounds.Single(tr => tr.Ordinal == 1);
        var task = competition.AdoptedRules.Definition.Phases
            .SelectMany(p => p.Tasks)
            .First(t => t.Code == taskRound.TaskRef);

        task.Timing.Kind.Should().Be(WorkingTimeKind.UntilAllFlightsComplete);
        task.Timing.WorkingTime.Should().BeNull();
    }

    [Then(@"^the flight is recorded with both the false start and the flight time$")]
    public async Task ThenTheFlightIsRecordedWithBothTheFalseStartAndTheFlightTime()
    {
        var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryId, TestContext.Current.CancellationToken);

        entry.Flights.Should().ContainSingle();
        var measurements = entry.Flights[0].Measurements;

        // Both facts are present: the infraction did not suppress the flight
        // time. F3K.7 zeroes this flight at scoring time, via the class's own
        // FlightValidWhen — the raw record still says what was flown.
        measurements.Should().ContainSingle(m => m.Metric == "launchedInWorkingTime")
            .Which.Value.Flag.Should().BeFalse();
        measurements.Should().ContainSingle(m => m.Metric == "flightTime")
            .Which.Value.Number.Should().Be(62m);
    }

    [Then(@"^the winner of the group is competitor (\d+)$")]
    public async Task ThenTheWinnerOfTheGroupIsCompetitor(int competitorOrdinal)
    {
        var view = (await TaskRoundResultAsync()).Single();

        view.WinnerRef.Should().Be(_competitors[competitorOrdinal - 1]);
    }

    [Then(@"^the corrected competitor scores (\d+), the mistyped (\d+) having been replaced$")]
    public async Task ThenTheCorrectedCompetitorScoresTheMistypedHavingBeenReplaced(int expectedScore, int _)
    {
        var view = (await TaskRoundResultAsync()).Single();
        var result = view.Results.Single(r => r.CompetitorRef == _competitors[0]);

        // The score is built from the corrected 412, not the mistyped 4120: if
        // the correction had not landed, the (capped) 600 would have won the
        // group instead. 1000 * 412 / 400 == 1030.
        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be((decimal)expectedScore);
    }

    [Then(@"^the entry still holds the other metrics captured alongside the flight time$")]
    public async Task ThenTheEntryStillHoldsTheOtherMetricsCapturedAlongsideTheFlightTime()
    {
        // The story's whole point: today's only remedy — annulling the whole
        // Entry — would destroy these. A correction must leave them intact.
        var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryIds[1], TestContext.Current.CancellationToken);

        var measurements = entry.Flights[0].Measurements;
        measurements.Should().ContainSingle(m => m.Metric == "flightTime");
        measurements.Should().Contain(m => m.Metric == "startHeight");
        measurements.Should().Contain(m => m.Metric == "startHeightRecorded");
        measurements.Should().Contain(m => m.Metric == "overflySeconds");
        measurements.Should().Contain(m => m.Metric == "touchedByCompetitor");
        measurements.Should().Contain(m => m.Metric == "landingDistance");
    }

    [Then(@"^the original (\d+) is still readable next to the correction$")]
    public async Task ThenTheOriginalValueIsStillReadableNextToTheCorrection(int originalValue)
    {
        // The append-only promise, asserted end to end: the mistyped original
        // survives, and the recorded correction names who fixed it and why.
        var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryIds[1], TestContext.Current.CancellationToken);

        var measurement = entry.Flights[0].Measurements.Single(m => m.Metric == "flightTime");
        measurement.Value.Should().Be(MeasuredValue.Of((decimal)originalValue));

        var amendment = measurement.Amendments.Should().ContainSingle().Subject;
        amendment.NewValue.Should().Be(MeasuredValue.Of(412m));
        amendment.Reason.Should().Be("mistyped the flight time");
        amendment.By.Should().Be("the contest director");
    }

    [Then(@"^the entry still holds the flight time and carries the recorded annulment$")]
    public async Task ThenTheEntryStillHoldsTheFlightTimeAndCarriesTheRecordedAnnulment()
    {
        var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryId, TestContext.Current.CancellationToken);

        // Append-only: the captured flight time survives the annulment.
        entry.Flights.Should().ContainSingle();
        entry.Flights[0].Measurements.Should().ContainSingle(m => m.Metric == "flightTime");
        entry.Flights[0].Measurements[0].Value.Should().Be(MeasuredValue.Of(412m));

        // The ruling is recorded beside the data, not an overwrite of it.
        entry.Annulment.Should().NotBeNull();
        entry.Annulment!.Reason.Should().Be("the competitor re-flew under protest");
        entry.Annulment.By.Should().Be("the jury");
    }

    [Then(@"^a further capture against the annulled entry is refused$")]
    public async Task ThenAFurtherCaptureAgainstTheAnnulledEntryIsRefused()
    {
        var response = await ApiClient.PostCommandRawAsync(
            Client, "/capture-measurement",
            new CaptureMeasurement(_entryId, 1, "startHeight", MeasuredValue.Of(0m)));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(response)).Should().Be("entry.annulled");
    }

    [Then(@"^the penalty is refused as an undeclared infraction type$")]
    public async Task ThenThePenaltyIsRefusedAsAnUndeclaredInfractionType()
    {
        _rawResponse!.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(_rawResponse)).Should().Be("recordPenalty.infractionTypeNotDeclared");
    }

    // WI-6. Identical captured values under F3K's winner-scores-1000
    // normalisation must land on exactly 1000 apiece: Task D sums both
    // launches, so any order-dependence in how either card's flights were
    // read or accepted would break the tie apart.
    [Then(@"^both competitors score identically in the group result$")]
    public async Task ThenBothCompetitorsScoreIdenticallyInTheGroupResult()
    {
        var view = (await TaskRoundResultAsync()).Single();
        var first = view.Results.Single(r => r.CompetitorRef == _competitors[0]);
        var second = view.Results.Single(r => r.CompetitorRef == _competitors[1]);

        first.State.Should().Be(TaskResultState.Valid);
        second.State.Should().Be(TaskResultState.Valid);
        first.RawScore.Should().Be(second.RawScore);
        first.RawScore.Should().Be(1000m);
    }

    // WI-6, the positional-selection regression (the story's finding 2, killed
    // by decision 3's sequence-sorted fold): 500 == 1000 * 120 / 240 only falls
    // out if LAUNCH 2's time was scored. Had selection followed typing recency,
    // both cards would read 120 and this row would sit at 1000 beside its rival.
    [Then(@"^competitor (\d+) scores (\d+) against that last-launch flight$")]
    public async Task ThenCompetitorScoresAgainstThatLastLaunchFlight(int competitorOrdinal, int expectedScore)
    {
        var view = (await TaskRoundResultAsync()).Single();
        var result = view.Results.Single(r => r.CompetitorRef == _competitors[competitorOrdinal - 1]);

        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be((decimal)expectedScore);
    }

    [Then(@"^the second open is refused as a duplicated launch$")]
    public async Task ThenTheSecondOpenIsRefusedAsADuplicatedLaunch()
    {
        _rawResponse!.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(_rawResponse)).Should().Be("openFlight.duplicateSequence");
    }

    // --------------------------------------------------------------- helpers

    /// <summary>
    /// Captures the rest of F5J's declared metrics on the current entry's
    /// flight, each fixed to a value contributing zero to the raw score (so raw
    /// == flightTime) — the same convention ScoringACompetitionSteps.cs uses.
    /// </summary>
    private async Task CaptureTheOtherF5JMetricsAsync()
    {
        await ApiClient.PostCommandAsync<EntryId>(Client, "/capture-measurement",
            new CaptureMeasurement(_entryId, 1, "startHeight", MeasuredValue.Of(0m)));
        await ApiClient.PostCommandAsync<EntryId>(Client, "/capture-measurement",
            new CaptureMeasurement(_entryId, 1, "startHeightRecorded", MeasuredValue.Of(true)));
        await ApiClient.PostCommandAsync<EntryId>(Client, "/capture-measurement",
            new CaptureMeasurement(_entryId, 1, "overflySeconds", MeasuredValue.Of(0m)));
        await ApiClient.PostCommandAsync<EntryId>(Client, "/capture-measurement",
            new CaptureMeasurement(_entryId, 1, "touchedByCompetitor", MeasuredValue.Of(false)));
        await ApiClient.PostCommandAsync<EntryId>(Client, "/capture-measurement",
            new CaptureMeasurement(_entryId, 1, "landingDistance", MeasuredValue.Of(100m))); // beyond the last row -> Rest(0)
    }

    private async Task<List<GroupScoreView>> TaskRoundResultAsync() =>
        await ApiClient.GetAsync<List<GroupScoreView>>(Client,
            $"/task-round-result?competitionRef={_competitionId.Value}&phaseOrdinal=0&roundOrdinal=1&taskRoundOrdinal=1");

    private async Task<GroupId> ResolveGroupIdAsync(int roundOrdinal, int groupOrdinal)
    {
        var key = (roundOrdinal, groupOrdinal);
        if (_groupIds.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var phase = view.Competition.Phases.Single();
        var round = phase.Rounds.Single(r => r.Ordinal == roundOrdinal);
        var group = round.TaskRounds.Single().Groups.Single(g => g.Ordinal == groupOrdinal);

        _groupIds[key] = group.Id;
        return group.Id;
    }

    private static async Task<string> ReadProblemTitleAsync(HttpResponseMessage response)
    {
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("title").GetString()!;
    }

    private static ClassDefinition ResolveDefinition(string className) => className switch
    {
        "F5J" => Corpus.All.Single(c => c.FileName == "30-f5j").Definition,
        "NZ Class M ALES 200" => Corpus.All.Single(c => c.FileName == "80-nz-m-ales200").Definition,
        "F3K" => Corpus.All.Single(c => c.FileName == "10-f3k").Definition,
        _ => throw new NotSupportedException($"No class definition wired up for '{className}'."),
    };
}
