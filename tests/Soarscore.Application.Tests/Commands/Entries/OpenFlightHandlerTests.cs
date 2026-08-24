// kanban/completed/capture-a-score-steel-thread-plan.md WI-8. Covers
// OpenFlightHandler directly against a FakeEventStore — same style as
// RegisterCompetitorHandlerTests.cs. The fixture opens a real Entry by
// running Competition.OpenEntry's own decide function against a hand-built
// drawn Competition (mirrors OpenEntryDecideTests.BuildDrawnCompetition),
// then appends the resulting EntryOpened as the Entry stream's first event —
// exactly what OpenEntryHandler itself does, one layer up.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Entries;

namespace Soarscore.Application.Tests.Commands.Entries;

public class OpenFlightHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F3K = SeedF3K.Definition; // TaskD: Fixed WorkingTime = 600 s, MaxLaunches = 2

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F3K,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F3K.Version,
            AdoptedAt = Now,
        };

    private static (FakeEventStore Store, CompetitionId CompetitionId, EntryId EntryId) SeedOpenEntry()
    {
        var store = new FakeEventStore();
        var competitionId = CompetitionId.New();
        var created = new CompetitionCreated(
            competitionId, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();

        var competitor = new Competitor { Id = CompetitorId.New(), PersonRef = PersonId.New(), CompetitorNumber = 1, RegisteredAt = Now };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(1), [new CompetitorRegistered(competitor, Now)]).GetAwaiter().GetResult();

        var groupRef = GroupId.New();
        var group = new Group { Id = groupRef, Ordinal = 1, CompetitorRefs = [competitor.Id] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "D", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = Now, Status = "drawn" };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(2),
            [new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], Now), new DrawAccepted(0, Now)])
            .GetAwaiter().GetResult();

        var competitionEvents = store.Streams[competitionId.Value];
        var competition = competitionEvents.Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
        var opened = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitor.Id, ReflightRole.Original, Now).Value;
        store.AppendAsync(opened.Id.Value, ExpectedVersion.NoStream, [opened]).GetAwaiter().GetResult();

        return (store, competitionId, opened.Id);
    }

    [Fact]
    public async Task Opening_a_flight_on_a_drawn_entry_succeeds_and_appends_FlightOpened_at_sequence_1()
    {
        var (store, _, entryId) = SeedOpenEntry();
        var handler = new OpenFlightHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new OpenFlight(entryId), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(entryId);
        var stream = store.Streams[entryId.Value];
        stream.Should().HaveCount(2); // EntryOpened + FlightOpened
        var flightOpened = stream[1].Should().BeOfType<FlightOpened>().Subject;
        flightOpened.Sequence.Should().Be(1);
        flightOpened.At.Should().Be(Now);
    }

    [Fact]
    public async Task Opening_a_flight_against_an_unknown_entry_fails_with_entry_notFound()
    {
        var store = new FakeEventStore();
        var handler = new OpenFlightHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new OpenFlight(EntryId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.notFound");
    }

    [Fact]
    public async Task Opening_a_flight_fails_with_competition_notFound_when_the_entrys_competition_stream_is_missing()
    {
        var store = new FakeEventStore();
        var entryId = EntryId.New();
        var opened = new EntryOpened(
            entryId,
            CompetitionId.New(), 0, 1, 1, GroupId.New(), CompetitorId.New(), ReflightRole.Original, Now);
        await store.AppendAsync(entryId.Value, ExpectedVersion.NoStream, [opened], TestContext.Current.CancellationToken);
        var handler = new OpenFlightHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new OpenFlight(entryId), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Opening_more_flights_than_MaxLaunches_allows_fails_with_the_domain_code_surfaced_unchanged()
    {
        var (store, _, entryId) = SeedOpenEntry();
        var handler = new OpenFlightHandler(store, new FakeClock(Now));

        var first = await handler.HandleAsync(new OpenFlight(entryId), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();
        var second = await handler.HandleAsync(new OpenFlight(entryId), TestContext.Current.CancellationToken);
        second.IsSuccess.Should().BeTrue();

        var third = await handler.HandleAsync(new OpenFlight(entryId), TestContext.Current.CancellationToken);

        third.IsFailure.Should().BeTrue();
        third.Code.Should().Be("openFlight.maxLaunchesExceeded");
    }

    // out-of-order-flight-entry.md WI-3 / decision 4: an explicit sequence is
    // the caller's launch label, used verbatim — opening launch 2 first is
    // legal and leaves launch 1 as a gap.
    [Fact]
    public async Task An_explicit_sequence_is_recorded_verbatim_so_launch_2_can_be_typed_first()
    {
        var (store, _, entryId) = SeedOpenEntry();
        var handler = new OpenFlightHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new OpenFlight(entryId, 2), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var flightOpened = store.Streams[entryId.Value][1].Should().BeOfType<FlightOpened>().Subject;
        flightOpened.Sequence.Should().Be(2);
    }

    // Decision 4 again: once gaps exist, the omitted-sequence derivation must
    // be max-plus-one, not length-plus-one — for flights {2}, Length+1 would
    // mint the collision 2 while Max+1 correctly continues at 3.
    [Fact]
    public async Task Omitting_sequence_derives_max_plus_one_when_a_gap_exists()
    {
        var (store, _, entryId) = SeedOpenEntry();
        var handler = new OpenFlightHandler(store, new FakeClock(Now));

        var first = await handler.HandleAsync(new OpenFlight(entryId, 2), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.HandleAsync(new OpenFlight(entryId), TestContext.Current.CancellationToken);

        second.IsSuccess.Should().BeTrue();
        var flightOpened = store.Streams[entryId.Value][2].Should().BeOfType<FlightOpened>().Subject;
        flightOpened.Sequence.Should().Be(3);
    }

    [Fact]
    public async Task A_stale_read_version_fails_with_eventStore_concurrencyConflict_on_append()
    {
        var (store, _, entryId) = SeedOpenEntry();

        // Another scorer opened flight 1 for real between this handler's
        // read and its append.
        await store.AppendAsync(
            entryId.Value, ExpectedVersion.Exact(1), [new FlightOpened(1, Now.AddMinutes(1))],
            TestContext.Current.CancellationToken);

        var staleReadStore = new StaleReadEventStore(store, entryId.Value, visibleCount: 1);
        var handler = new OpenFlightHandler(staleReadStore, new FakeClock(Now));

        var result = await handler.HandleAsync(new OpenFlight(entryId), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("eventStore.concurrencyConflict");
    }

    [Fact]
    public void Appended_FlightOpened_folds_idempotently_when_applied_twice()
    {
        var (store, _, entryId) = SeedOpenEntry();
        var priorEvents = store.Streams[entryId.Value];
        var priorState = priorEvents.Aggregate((Entry?)null, (current, e) => Entry.Apply(current, (EntryEvent)e))!;
        var flightOpened = new FlightOpened(1, Now.AddMinutes(1));

        var first = priorState.Apply(flightOpened);
        var second = priorState.Apply(flightOpened);

        second.Flights.Select(f => f.Sequence).Should().Equal(first.Flights.Select(f => f.Sequence));
    }

    /// <summary>
    /// Wraps a real FakeEventStore but truncates one stream's ReadStreamAsync
    /// result — standing in for a read that happened before a concurrent
    /// append landed, so the handler under test computes an
    /// ExpectedVersion.Exact that is already stale by the time it appends.
    /// Mirrors Competitions/RegisterCompetitorHandlerTests's double of the
    /// same name.
    /// </summary>
    private sealed class StaleReadEventStore(IEventStore inner, Guid staleStreamId, int visibleCount) : IEventStore
    {
        public Task<Result<long>> AppendAsync(
            Guid streamId, ExpectedVersion expected, IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default) =>
            inner.AppendAsync(streamId, expected, events, cancellationToken);

        public async Task<Result<IReadOnlyList<IDomainEvent>>> ReadStreamAsync(
            Guid streamId, long fromVersion, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadStreamAsync(streamId, fromVersion, cancellationToken);
            if (read.IsFailure || streamId != staleStreamId)
            {
                return read;
            }

            return Result<IReadOnlyList<IDomainEvent>>.Success(read.Value.Take(visibleCount).ToList());
        }

        public Task<Result<IReadOnlyList<RecordedEvent>>> ReadAllAsync(
            long fromPosition, int batchSize, CancellationToken cancellationToken = default) =>
            inner.ReadAllAsync(fromPosition, batchSize, cancellationToken);
    }
}
