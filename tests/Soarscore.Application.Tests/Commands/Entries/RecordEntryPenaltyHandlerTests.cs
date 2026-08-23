// kanban/in-progress/annul-and-penalise-the-second-entry-thread.md WI-7. Covers
// RecordEntryPenaltyHandler directly against a FakeEventStore — the two-load
// shape from AmendMeasurementHandlerTests.cs (Entry for its state, Competition
// for the adopted class's declared penalties). The adopted F5K definition
// declares motorRestartInFlight (ZeroFlight, Flight scope). Covers the
// payload round-trip, the domain codes surfaced unchanged, and the concurrency
// append.

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

public class RecordEntryPenaltyHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F5K = SeedF5K.Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F5K,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F5K.Version,
            AdoptedAt = Now,
        };

    /// <summary>Seeds a competition adopting F5K, one registered competitor,
    /// a drawn phase, and an open Entry — the two streams the handler needs.</summary>
    private static (FakeEventStore Store, EntryId EntryId) SeedEntryUnderF5K()
    {
        var store = new FakeEventStore();
        var competitionId = CompetitionId.New();
        var created = new CompetitionCreated(
            competitionId, "Club Champs 2026", "Auckland",
            new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();

        var competitor = new Competitor
        {
            Id = CompetitorId.New(), PersonRef = PersonId.New(), CompetitorNumber = 1, RegisteredAt = Now,
        };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(1), [new CompetitorRegistered(competitor, Now)]).GetAwaiter().GetResult();

        var groupRef = GroupId.New();
        var group = new Group { Id = groupRef, Ordinal = 1, CompetitorRefs = [competitor.Id] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = Now, Status = "drawn" };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(2),
            [new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], Now)]).GetAwaiter().GetResult();

        // Fold the competition to open an entry.
        var competitionEvents = store.Streams[competitionId.Value];
        var competition = competitionEvents.Aggregate(
            (Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
        var opened = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitor.Id, ReflightRole.Original, Now).Value;
        store.AppendAsync(opened.Id.Value, ExpectedVersion.NoStream, [opened]).GetAwaiter().GetResult();

        return (store, opened.Id);
    }

    [Fact]
    public async Task Recording_a_declared_flight_penalty_appends_a_PenaltyRecorded_with_the_payload()
    {
        var (store, entryId) = SeedEntryUnderF5K();
        var handler = new RecordEntryPenaltyHandler(store);

        var result = await handler.HandleAsync(
            new RecordEntryPenalty(entryId, "motorRestartInFlight", PenaltyScope.Flight, "the scorer"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(entryId);

        var stream = store.Streams[entryId.Value];
        stream.Should().HaveCount(2); // EntryOpened + PenaltyRecorded
        var recorded = stream[1].Should().BeOfType<Domain.Entries.PenaltyRecorded>().Subject;
        recorded.Penalty.InfractionType.Should().Be("motorRestartInFlight");
        recorded.Penalty.Scope.Should().Be(PenaltyScope.Flight);
        recorded.Penalty.By.Should().Be("the scorer");
        recorded.Penalty.CompetitorRef.Should().BeNull();
        recorded.Penalty.TaskRound.Should().BeNull();
    }

    [Fact]
    public async Task Recording_against_an_unknown_entry_fails_with_entry_notFound()
    {
        var store = new FakeEventStore();
        var handler = new RecordEntryPenaltyHandler(store);

        var result = await handler.HandleAsync(
            new RecordEntryPenalty(EntryId.New(), "motorRestartInFlight", PenaltyScope.Flight, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.notFound");
    }

    [Fact]
    public async Task Recording_an_undeclared_infraction_type_fails_with_the_domain_code_surfaced_unchanged()
    {
        var (store, entryId) = SeedEntryUnderF5K();
        var handler = new RecordEntryPenaltyHandler(store);

        var result = await handler.HandleAsync(
            new RecordEntryPenalty(entryId, "madeUp", PenaltyScope.Flight, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.infractionTypeNotDeclared");
    }

    [Fact]
    public async Task A_stale_read_version_fails_with_eventStore_concurrencyConflict_on_append()
    {
        var (store, entryId) = SeedEntryUnderF5K();

        // Another scorer opened a flight for real between this handler's read
        // and its append.
        await store.AppendAsync(
            entryId.Value, ExpectedVersion.Exact(1), [new FlightOpened(1, Now)], TestContext.Current.CancellationToken);

        var staleReadStore = new StaleReadEventStore(store, entryId.Value, visibleCount: 1);
        var handler = new RecordEntryPenaltyHandler(staleReadStore);

        var result = await handler.HandleAsync(
            new RecordEntryPenalty(entryId, "motorRestartInFlight", PenaltyScope.Flight, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("eventStore.concurrencyConflict");
    }

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