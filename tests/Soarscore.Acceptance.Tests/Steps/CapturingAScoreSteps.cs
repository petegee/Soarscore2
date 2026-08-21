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

    [Then(@"^the entry's working time has no end$")]
    public async Task ThenTheEntrysWorkingTimeHasNoEnd()
    {
        var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryId, TestContext.Current.CancellationToken);

        entry.WorkingTime.End.Should().BeNull();
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

    private static ClassDefinition ResolveDefinition(string className) => className switch
    {
        "F5J" => Corpus.All.Single(c => c.FileName == "30-f5j").Definition,
        "NZ Class M ALES 200" => Corpus.All.Single(c => c.FileName == "80-nz-m-ales200").Definition,
        "F3K" => Corpus.All.Single(c => c.FileName == "10-f3k").Definition,
        _ => throw new NotSupportedException($"No class definition wired up for '{className}'."),
    };
}
