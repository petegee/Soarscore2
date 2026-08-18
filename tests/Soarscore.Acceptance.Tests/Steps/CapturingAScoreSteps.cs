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
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
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

    [Given(@"^a drawn preliminary phase of (\d+) rounds$")]
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

    // --------------------------------------------------------------- helpers

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
