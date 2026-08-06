// Hand-written fakes (LADR-0003 "Doubles") for the WI-4 handler tests — a real
// DI container and a real store are Infrastructure/Api's composition concerns
// (WI-5/WI-9), not something a handler test needs. Not People/TestDoubles.cs's
// FakeEventStore: that double's NoStream-collision code
// ("eventStore.streamExists") does not match MartenEventStore.cs's real one
// ("eventStore.streamAlreadyExists"), which PublishClassDefinitionHandler
// matches on by exact string — this local double keeps that string honest.

using Soarscore.Application;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Domain;

namespace Soarscore.Application.Tests.CompetitionClasses;

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}

internal sealed class FakeEventStore : IEventStore
{
    private readonly Dictionary<Guid, List<IDomainEvent>> _streams = [];

    public IReadOnlyDictionary<Guid, List<IDomainEvent>> Streams => _streams;

    public Task<Result<long>> AppendAsync(
        Guid streamId, ExpectedVersion expected, IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        var stream = _streams.GetValueOrDefault(streamId) ?? [];

        if (expected.IsNoStream && stream.Count != 0)
        {
            return Task.FromResult(Result<long>.Failure("eventStore.streamAlreadyExists", $"Stream {streamId} already exists."));
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

/// <summary>In-memory stand-in for the Marten-backed `class_library` read model (WI-5).</summary>
internal sealed class FakeClassLibraryQuery : IClassLibraryQuery
{
    private readonly List<ClassDefinitionSummary> _summaries = [];

    public void Seed(ClassDefinitionSummary summary) => _summaries.Add(summary);

    public Task<ClassDefinitionSummary?> FindByHashAsync(string contentHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_summaries.FirstOrDefault(s => s.ContentHash == contentHash));

    public Task<IReadOnlyList<ClassDefinitionSummary>> SearchAsync(string? name, bool activeOnly, CancellationToken cancellationToken = default)
    {
        IEnumerable<ClassDefinitionSummary> query = _summaries;
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(s => s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        if (activeOnly)
        {
            query = query.Where(s => s.RetiredAt is null);
        }

        return Task.FromResult<IReadOnlyList<ClassDefinitionSummary>>(query.ToList());
    }
}

/// <summary>Hand-written fake (LADR-0003 "Doubles") — resolves handlers from a fixed dictionary, no real DI container.</summary>
internal sealed class FakeServiceProvider(Dictionary<Type, object> services) : IServiceProvider
{
    public object? GetService(Type serviceType) => services.GetValueOrDefault(serviceType);
}
