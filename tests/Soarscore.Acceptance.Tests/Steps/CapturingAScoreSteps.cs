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
    private DateTimeOffset _lastLaunchAt;

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

    [When(@"^the scorer opens a flight launched at (\d{1,2}:\d{2}:\d{2})$")]
    public async Task WhenTheScorerOpensAFlightLaunchedAt(string timeOfDay)
    {
        var time = TimeOnly.Parse(timeOfDay);

        // OpenFlight never checks LaunchAt against the working time (finding
        // 3), so which calendar day this resolves to has no bearing on
        // whether the command succeeds — only scenario 3's own step computes
        // a launch time relative to the entry's real WorkingTime.Start.
        var launchAt = new DateTimeOffset(DateOnly.FromDateTime(DateTime.UtcNow), time, TimeSpan.Zero);
        _lastLaunchAt = launchAt;

        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(_entryId, launchAt));
    }

    [When(@"^the scorer opens a flight launched (\d+) minutes before the working time begins$")]
    public async Task WhenTheScorerOpensAFlightLaunchedMinutesBeforeTheWorkingTimeBegins(int minutesBefore)
    {
        var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryId, TestContext.Current.CancellationToken);
        var launchAt = entry.WorkingTime.Start.AddMinutes(-minutesBefore);
        _lastLaunchAt = launchAt;

        // The finding-3 regression, driven over real HTTP: OpenFlight must
        // succeed here, not be refused, even though launchAt precedes
        // WorkingTime.Start (F3K.7).
        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(_entryId, launchAt));
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

    [Then(@"^the flight is recorded with its launch time unchanged$")]
    public async Task ThenTheFlightIsRecordedWithItsLaunchTimeUnchanged()
    {
        var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryId, TestContext.Current.CancellationToken);

        entry.Flights.Should().ContainSingle();
        entry.Flights[0].LaunchAt.Should().Be(_lastLaunchAt);
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
        // Not the real corpus F3K — AcceptanceF3KShape.cs's header explains why.
        "F3K" => AcceptanceF3KShape.Definition,
        _ => throw new NotSupportedException($"No class definition wired up for '{className}'."),
    };
}
