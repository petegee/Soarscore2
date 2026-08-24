// kanban/completed/amend-a-measurement.md WI-6. Covers AmendMeasurementHandler
// directly against a FakeEventStore — the same style as
// CaptureMeasurementHandlerTests.cs, with a flight and a captured flightTime
// already on the fixture Entry so an amendment has something to correct. Covers
// the IClock instant reaching the Amendment.At, the rounded value, the recorded
// Reason/By, and the optimistic-concurrency append.

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

public class AmendMeasurementHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);

    // TaskD's Metrics: flightTime (Number, Truncate 0.1), landedWithinWindow
    // (Flag), launchedInWorkingTime (Flag) — F3K's FlightMetrics.
    private static readonly ClassDefinition F3K = SeedF3K.Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F3K,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F3K.Version,
            AdoptedAt = Now,
        };

    /// <summary>Seeds a competition and an Entry whose first flight already has a flightTime captured.</summary>
    private static (FakeEventStore Store, CompetitionId CompetitionId, EntryId EntryId) SeedEntryWithCapturedFlightTime(decimal flightTime = 412.37m)
    {
        var (store, competitionId, entryId) = SeedOpenEntryWithFlight();

        var captured = new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(flightTime), CapturedAt = Now };
        store.AppendAsync(entryId.Value, ExpectedVersion.Exact(2), [new MeasurementCaptured(1, captured)]).GetAwaiter().GetResult();

        return (store, competitionId, entryId);
    }

    private static (FakeEventStore Store, CompetitionId CompetitionId, EntryId EntryId) SeedOpenEntryWithFlight()
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

        var flightOpened = new FlightOpened(1, Now.AddMinutes(1));
        store.AppendAsync(opened.Id.Value, ExpectedVersion.Exact(1), [flightOpened]).GetAwaiter().GetResult();

        return (store, competitionId, opened.Id);
    }

    [Fact]
    public async Task Correcting_a_captured_metric_appends_a_MeasurementAmended_with_clock_at_and_the_recorded_actors()
    {
        var (store, _, entryId) = SeedEntryWithCapturedFlightTime();
        var handler = new AmendMeasurementHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AmendMeasurement(entryId, 1, "flightTime", MeasuredValue.Of(412.37m), "mistyped the flight time", "the contest director"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(entryId);

        var stream = store.Streams[entryId.Value];
        stream.Should().HaveCount(4); // EntryOpened + FlightOpened + MeasurementCaptured + MeasurementAmended
        var amended = stream[3].Should().BeOfType<MeasurementAmended>().Subject;
        amended.FlightSequence.Should().Be(1);
        amended.Metric.Should().Be("flightTime");
        // F3K.7 truncates to 0.1 s — the value is rounded like a capture's (finding 5).
        amended.Amendment.NewValue.Number.Should().Be(412.3m);
        amended.Amendment.Reason.Should().Be("mistyped the flight time");
        amended.Amendment.By.Should().Be("the contest director");
        // At comes from IClock, never the caller — captures carry the same rule.
        amended.Amendment.At.Should().Be(Now);
    }

    [Fact]
    public async Task Correcting_against_an_unknown_entry_fails_with_entry_notFound()
    {
        var store = new FakeEventStore();
        var handler = new AmendMeasurementHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AmendMeasurement(EntryId.New(), 1, "flightTime", MeasuredValue.Of(100m), "r", "b"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.notFound");
    }

    [Fact]
    public async Task Correcting_fails_with_competition_notFound_when_the_entrys_competition_stream_is_missing()
    {
        var store = new FakeEventStore();
        var entryId = EntryId.New();
        var opened = new EntryOpened(
            entryId,
            CompetitionId.New(), 0, 1, 1, GroupId.New(), CompetitorId.New(), ReflightRole.Original, Now);
        await store.AppendAsync(entryId.Value, ExpectedVersion.NoStream, [opened], TestContext.Current.CancellationToken);
        await store.AppendAsync(entryId.Value, ExpectedVersion.Exact(1), [new FlightOpened(1, Now)], TestContext.Current.CancellationToken);
        var handler = new AmendMeasurementHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AmendMeasurement(entryId, 1, "flightTime", MeasuredValue.Of(100m), "r", "b"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Correcting_a_metric_that_was_never_captured_fails_with_the_domain_code_surfaced_unchanged()
    {
        var (store, _, entryId) = SeedOpenEntryWithFlight(); // flight opened, nothing captured
        var handler = new AmendMeasurementHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AmendMeasurement(entryId, 1, "flightTime", MeasuredValue.Of(100m), "r", "b"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("amendMeasurement.notCaptured");
    }

    [Fact]
    public async Task A_stale_read_version_fails_with_eventStore_concurrencyConflict_on_append()
    {
        var (store, _, entryId) = SeedEntryWithCapturedFlightTime();

        // Another scorer captured landedWithinWindow for real between this
        // handler's read and its append.
        var racing = new Measurement { Metric = "landedWithinWindow", Value = MeasuredValue.Of(true), CapturedAt = Now };
        await store.AppendAsync(
            entryId.Value, ExpectedVersion.Exact(3), [new MeasurementCaptured(1, racing)], TestContext.Current.CancellationToken);

        var staleReadStore = new StaleReadEventStore(store, entryId.Value, visibleCount: 3);
        var handler = new AmendMeasurementHandler(staleReadStore, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AmendMeasurement(entryId, 1, "flightTime", MeasuredValue.Of(100m), "r", "b"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("eventStore.concurrencyConflict");
    }

    /// <summary>
    /// Wraps a real FakeEventStore but truncates one stream's ReadStreamAsync
    /// result — standing in for a read that happened before a concurrent
    /// append landed, so the handler under test computes an
    /// ExpectedVersion.Exact that is already stale by the time it appends.
    /// Mirrors CaptureMeasurementHandlerTests's double of the same name.
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