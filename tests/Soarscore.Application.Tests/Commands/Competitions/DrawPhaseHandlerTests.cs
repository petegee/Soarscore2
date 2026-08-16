// kanban/completed/phase-drawn-steel-thread-plan.md WI-3. Covers DrawPhaseHandler
// directly against a FakeEventStore — same style as
// WithdrawCompetitorHandlerTests.cs: no cross-aggregate read, the adopted
// class definition is already sitting in AdoptedRules.

using System.Linq;
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

public class DrawPhaseHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F3J = SeedF3J.Definition; // literal MinPerGroup = 6

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F3J,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F3J.Version,
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

    private static void SeedRegisteredCompetitors(FakeEventStore store, CompetitionId competitionId, int count, long startingVersion)
    {
        var version = startingVersion;
        for (var i = 0; i < count; i++)
        {
            var competitor = new Competitor
            {
                Id = CompetitorId.New(),
                PersonRef = PersonId.New(),
                CompetitorNumber = i + 1,
                RegisteredAt = Now,
            };
            store.AppendAsync(
                competitionId.Value, ExpectedVersion.Exact(version), [new CompetitorRegistered(competitor, Now)]).GetAwaiter().GetResult();
            version++;
        }
    }

    [Fact]
    public async Task Drawing_with_a_sufficient_field_succeeds_and_appends_exactly_one_event_at_the_next_version()
    {
        var (store, competitionId) = SeedCompetition();
        SeedRegisteredCompetitors(store, competitionId, 12, startingVersion: 1);
        var handler = new DrawPhaseHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new DrawPhase(competitionId, 3), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(competitionId);

        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(14); // 1 created + 12 registered + 1 drawn
        var drawn = stream[13].Should().BeOfType<PhaseDrawn>().Subject;
        drawn.Rounds.Length.Should().Be(3);
    }

    [Fact]
    public async Task Drawing_a_catalogue_choice_phase_succeeds_and_carries_the_named_task_per_round()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var f3kAdoptedRules = new AdoptedRules
        {
            Definition = SeedF3K.Definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SeedF3K.Definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            id, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", f3kAdoptedRules, Now);
        await store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created], TestContext.Current.CancellationToken);
        SeedRegisteredCompetitors(store, id, 10, startingVersion: 1);
        var handler = new DrawPhaseHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DrawPhase(id, 3, ["A", "B", "C"]), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var stream = store.Streams[id.Value];
        var drawn = stream[^1].Should().BeOfType<PhaseDrawn>().Subject;
        drawn.Rounds.Select(r => r.TaskRounds[0].TaskRef).Should().Equal("A", "B", "C");
    }

    [Fact]
    public async Task Drawing_a_catalogue_choice_phase_with_no_selection_fails_with_taskSelectionRequired()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var f3kAdoptedRules = new AdoptedRules
        {
            Definition = SeedF3K.Definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SeedF3K.Definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            id, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", f3kAdoptedRules, Now);
        await store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created], TestContext.Current.CancellationToken);
        SeedRegisteredCompetitors(store, id, 10, startingVersion: 1);
        var handler = new DrawPhaseHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new DrawPhase(id, 3), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.taskSelectionRequired");
    }

    [Fact]
    public async Task Drawing_a_fixedSequence_phase_with_a_selection_fails_with_taskSelectionNotPermitted()
    {
        var (store, competitionId) = SeedCompetition();
        SeedRegisteredCompetitors(store, competitionId, 12, startingVersion: 1);
        var handler = new DrawPhaseHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DrawPhase(competitionId, 1, ["D"]), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.taskSelectionNotPermitted");
    }

    [Fact]
    public async Task Drawing_with_a_null_TaskRefs_still_draws_a_fixedSequence_class()
    {
        var (store, competitionId) = SeedCompetition();
        SeedRegisteredCompetitors(store, competitionId, 12, startingVersion: 1);
        var handler = new DrawPhaseHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new DrawPhase(competitionId, 3, null), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Drawing_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var handler = new DrawPhaseHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new DrawPhase(CompetitionId.New(), 3), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Drawing_domain_failure_codes_surface_unchanged_through_the_handler()
    {
        var (store, competitionId) = SeedCompetition();
        // No competitors registered — the field is empty.
        var handler = new DrawPhaseHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new DrawPhase(competitionId, 1), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.fieldEmpty");
    }

    [Fact]
    public async Task Drawing_twice_the_second_time_fails_with_drawPhase_alreadyDrawn()
    {
        var (store, competitionId) = SeedCompetition();
        SeedRegisteredCompetitors(store, competitionId, 6, startingVersion: 1);
        var handler = new DrawPhaseHandler(store, new FakeClock(Now));

        var first = await handler.HandleAsync(new DrawPhase(competitionId, 1), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.HandleAsync(new DrawPhase(competitionId, 1), TestContext.Current.CancellationToken);

        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("drawPhase.alreadyDrawn");
    }

    [Fact]
    public async Task A_stale_read_version_fails_with_eventStore_concurrencyConflict_on_append()
    {
        var (store, competitionId) = SeedCompetition();
        SeedRegisteredCompetitors(store, competitionId, 6, startingVersion: 1);

        // Another registration landed for real between this handler's read
        // and its append — the append's ExpectedVersion.Exact, computed from
        // the stale seven-event read below, no longer matches the store's
        // actual eight-event stream. Two organisers drawing concurrently is
        // the scenario this guards (WI-3): the loser's retry re-reads
        // Phases non-empty and fails cleanly with drawPhase.alreadyDrawn,
        // never a corrupted schedule — this test covers the append-race
        // itself, one level below that.
        var racingCompetitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 7,
            RegisteredAt = Now,
        };
        await store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(7), [new CompetitorRegistered(racingCompetitor, Now)],
            TestContext.Current.CancellationToken);

        var staleReadStore = new StaleReadEventStore(store, competitionId.Value, visibleCount: 7);
        var handler = new DrawPhaseHandler(staleReadStore, new FakeClock(Now));

        var result = await handler.HandleAsync(new DrawPhase(competitionId, 1), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("eventStore.concurrencyConflict");
    }

    /// <summary>
    /// Wraps a real FakeEventStore but truncates one stream's ReadStreamAsync
    /// result — standing in for a read that happened before a concurrent
    /// append landed, so the handler under test computes an
    /// ExpectedVersion.Exact that is already stale by the time it appends.
    /// Mirrors RegisterCompetitorHandlerTests's private double of the same name.
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
