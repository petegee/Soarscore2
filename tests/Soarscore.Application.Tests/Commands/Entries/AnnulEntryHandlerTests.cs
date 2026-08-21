// kanban/in-progress/annul-and-penalise-the-second-entry-thread.md WI-7. Covers
// AnnulEntryHandler directly against a FakeEventStore — the same style as
// AmendMeasurementHandlerTests.cs, minus the Competition read (an annulment has
// no class-definition cost to validate against). Covers the IClock instant
// reaching the Annulment.At, the recorded Reason/By, the domain defect code
// surfaced unchanged, and the optimistic-concurrency append.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Xunit;

using Soarscore.Application.Tests.Shared.Entries;

namespace Soarscore.Application.Tests.Commands.Entries;

public class AnnulEntryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);

    private static (FakeEventStore Store, EntryId EntryId) SeedOpenEntry()
    {
        var store = new FakeEventStore();
        var entryId = EntryId.New();
        var opened = new EntryOpened(
            entryId, CompetitionId.New(), 0, 1, 1, GroupId.New(), CompetitorId.New(), ReflightRole.Original, Now);
        store.AppendAsync(entryId.Value, ExpectedVersion.NoStream, [opened]).GetAwaiter().GetResult();
        return (store, entryId);
    }

    [Fact]
    public async Task Annulling_an_open_entry_appends_an_EntryAnnulled_with_clock_at_and_the_recorded_ruling()
    {
        var (store, entryId) = SeedOpenEntry();
        var handler = new AnnulEntryHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AnnulEntry(entryId, "the competitor re-flew under protest", "the jury"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(entryId);

        var stream = store.Streams[entryId.Value];
        stream.Should().HaveCount(2); // EntryOpened + EntryAnnulled
        var annulled = stream[1].Should().BeOfType<EntryAnnulled>().Subject;
        annulled.Annulment.Reason.Should().Be("the competitor re-flew under protest");
        annulled.Annulment.By.Should().Be("the jury");
        // At comes from IClock, never the caller.
        annulled.Annulment.At.Should().Be(Now);
    }

    [Fact]
    public async Task Annulling_an_unknown_entry_fails_with_entry_notFound()
    {
        var store = new FakeEventStore();
        var handler = new AnnulEntryHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AnnulEntry(EntryId.New(), "r", "b"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.notFound");
    }

    [Fact]
    public async Task Annulling_with_a_blank_reason_fails_with_the_domain_code_surfaced_unchanged()
    {
        var (store, entryId) = SeedOpenEntry();
        var handler = new AnnulEntryHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AnnulEntry(entryId, "  ", "the jury"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("annulEntry.reasonRequired");
    }

    [Fact]
    public async Task A_stale_read_version_fails_with_eventStore_concurrencyConflict_on_append()
    {
        var (store, entryId) = SeedOpenEntry();

        // Another scorer opened a flight for real between this handler's read
        // and its append.
        await store.AppendAsync(
            entryId.Value, ExpectedVersion.Exact(1), [new FlightOpened(1, Now)], TestContext.Current.CancellationToken);

        var staleReadStore = new StaleReadEventStore(store, entryId.Value, visibleCount: 1);
        var handler = new AnnulEntryHandler(staleReadStore, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AnnulEntry(entryId, "r", "b"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("eventStore.concurrencyConflict");
    }

    /// <summary>
    /// Wraps a real FakeEventStore but truncates one stream's ReadStreamAsync
    /// result — standing in for a read that happened before a concurrent append
    /// landed, so the handler computes an ExpectedVersion.Exact that is already
    /// stale by the time it appends. Mirrors AmendMeasurementHandlerTests's
    /// double of the same name.
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
