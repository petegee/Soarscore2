// Hand-written fakes (LADR-0003 "Doubles") for the WI-6 handler tests — a
// real DI container and a real store are Infrastructure/Api's composition
// concerns (WI-7/WI-9), not something a handler test needs.

using Soarscore.Application.Queries.People;
using Soarscore.Domain;

namespace Soarscore.Application.Tests.Shared.People;

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}

/// <summary>
/// In-memory stand-in for the event store. Enforces exactly the two things
/// ExpectedVersion promises to check — NoStream and Exact — so handler tests
/// can prove the concurrency/first-append shape without a real store. It does
/// not enforce email uniqueness: that invariant lives at the Marten unique
/// index (WI-7) and is proved against a real Postgres in WI-9, not here.
/// </summary>
internal sealed class FakeEventStore : IEventStore
{
    private readonly Dictionary<Guid, List<IDomainEvent>> _streams = [];

    public Task<Result<long>> AppendAsync(
        Guid streamId, ExpectedVersion expected, IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        var stream = _streams.GetValueOrDefault(streamId) ?? [];

        if (expected.IsNoStream && stream.Count != 0)
        {
            return Task.FromResult(Result<long>.Failure("eventStore.streamExists", $"Stream {streamId} already exists."));
        }

        if (expected.IsExact && expected.Version != stream.Count)
        {
            return Task.FromResult(Result<long>.Failure("eventStore.concurrencyConflict", $"Expected version {expected.Version} but stream {streamId} is at {stream.Count}."));
        }

        var updated = new List<IDomainEvent>(stream);
        updated.AddRange(events);
        _streams[streamId] = updated;

        return Task.FromResult(Result<long>.Success((long)updated.Count));
    }

    public Task<Result<IReadOnlyList<IDomainEvent>>> ReadStreamAsync(
        Guid streamId, long fromVersion, CancellationToken cancellationToken = default)
    {
        var stream = _streams.GetValueOrDefault(streamId) ?? [];
        IReadOnlyList<IDomainEvent> slice = stream.Skip((int)fromVersion).ToList();
        return Task.FromResult(Result<IReadOnlyList<IDomainEvent>>.Success(slice));
    }

    public Task<Result<IReadOnlyList<RecordedEvent>>> ReadAllAsync(
        long fromPosition, int batchSize, CancellationToken cancellationToken = default)
    {
        var all = _streams
            .SelectMany(stream => stream.Value.Select((e, index) => new RecordedEvent(stream.Key, index + 1, index + 1, e)))
            .Skip((int)fromPosition)
            .Take(batchSize)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<RecordedEvent>>.Success(all));
    }
}

/// <summary>In-memory stand-in for the Marten-backed `people` read model (WI-7), folded with the real PeopleProjection.</summary>
internal sealed class FakePeopleQuery : IPeopleQuery
{
    private readonly List<PersonSummary> _people = [];

    public void Seed(PersonSummary summary) => _people.Add(summary);

    public Task<PersonSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_people.FirstOrDefault(p => p.Email == email));

    public Task<IReadOnlyList<PersonSummary>> SearchByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PersonSummary>>(
            _people.Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList());
}

/// <summary>Hand-written fake (LADR-0003 "Doubles") — resolves handlers from a fixed dictionary, no real DI container.</summary>
internal sealed class FakeServiceProvider(Dictionary<Type, object> services) : IServiceProvider
{
    public object? GetService(Type serviceType) => services.GetValueOrDefault(serviceType);
}
