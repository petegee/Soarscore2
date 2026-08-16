// kanban/completed/register-competitor-steel-thread-plan.md WI-3. Covers
// RegisterCompetitorHandler directly against a FakeEventStore, no dispatcher
// needed — same style as CreateCompetitionHandlerTests.cs.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class RegisterCompetitorHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = SampleDefinition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SampleDefinition.Version,
            AdoptedAt = Now,
        };

    private static (FakeEventStore Store, CompetitionId CompetitionId) SeedCompetition()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var created = new CompetitionCreated(
            id, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return (store, id);
    }

    private static PersonId SeedPerson(FakeEventStore store)
    {
        var id = PersonId.New();
        var registered = new PersonRegistered(id, "Alex Pilot", new ContactDetails { Email = "alex@example.com" }, null, Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [registered]).GetAwaiter().GetResult();
        return id;
    }

    [Fact]
    public async Task Registering_a_known_person_succeeds_and_appends_exactly_one_event_at_the_next_version()
    {
        var (store, competitionId) = SeedCompetition();
        var personId = SeedPerson(store);
        var handler = new RegisterCompetitorHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new RegisterCompetitor(competitionId, personId), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();

        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(2);
        var registered = stream[1].Should().BeOfType<CompetitorRegistered>().Subject;
        registered.Competitor.Id.Should().Be(result.Value);
        registered.Competitor.PersonRef.Should().Be(personId);
        registered.Competitor.CompetitorNumber.Should().Be(1);
    }

    [Fact]
    public async Task Registering_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var personId = SeedPerson(store);
        var handler = new RegisterCompetitorHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new RegisterCompetitor(CompetitionId.New(), personId), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Registering_an_unknown_person_fails_with_registerCompetitor_personNotFound()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new RegisterCompetitorHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new RegisterCompetitor(competitionId, PersonId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("registerCompetitor.personNotFound");
    }

    [Fact]
    public async Task Registering_the_same_person_twice_fails_with_the_domain_code_surfaced_unchanged()
    {
        var (store, competitionId) = SeedCompetition();
        var personId = SeedPerson(store);
        var handler = new RegisterCompetitorHandler(store, new FakeClock(Now));

        var first = await handler.HandleAsync(
            new RegisterCompetitor(competitionId, personId), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.HandleAsync(
            new RegisterCompetitor(competitionId, personId), TestContext.Current.CancellationToken);

        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("competition.competitor.alreadyRegistered");
    }

    [Fact]
    public async Task A_stale_read_version_fails_with_eventStore_concurrencyConflict_on_append()
    {
        var (store, competitionId) = SeedCompetition();
        var personId = SeedPerson(store);

        // Another registration landed for real between this handler's read
        // and its append — the append's ExpectedVersion.Exact, computed from
        // the stale one-event read below, no longer matches the store's
        // actual two-event stream.
        var otherPerson = SeedPerson(store);
        var racingCompetitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = otherPerson,
            CompetitorNumber = 1,
            RegisteredAt = Now,
        };
        await store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(1), [new CompetitorRegistered(racingCompetitor, Now)],
            TestContext.Current.CancellationToken);

        var staleReadStore = new StaleReadEventStore(store, competitionId.Value, visibleCount: 1);
        var handler = new RegisterCompetitorHandler(staleReadStore, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new RegisterCompetitor(competitionId, personId), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("eventStore.concurrencyConflict");
    }

    /// <summary>
    /// Wraps a real FakeEventStore but truncates one stream's ReadStreamAsync
    /// result — standing in for a read that happened before a concurrent
    /// append landed, so the handler under test computes an
    /// ExpectedVersion.Exact that is already stale by the time it appends.
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
