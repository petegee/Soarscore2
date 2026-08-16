// Hand-written fakes (LADR-0003 "Doubles") for the WI-3 handler tests — a
// real DI container and a real store are Infrastructure/Api's composition
// concerns (WI-4/WI-5), not something a handler test needs. Local to this
// namespace rather than reused from People/TestDoubles.cs or
// CompetitionClasses/TestDoubles.cs: both are `internal` to their own test
// namespace, and CompetitionClasses/TestDoubles.cs's FakeEventStore is the
// one whose NoStream-collision code ("eventStore.streamAlreadyExists")
// actually matches MartenEventStore.cs, so that is the version this copy
// follows.

using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;

namespace Soarscore.Application.Tests.Shared.Competitions;

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

/// <summary>In-memory stand-in for the Marten-backed `competitions` read model (WI-4).</summary>
internal sealed class FakeCompetitionsQuery : ICompetitionsQuery
{
    private readonly List<CompetitionSummary> _competitions = [];

    public void Seed(CompetitionSummary summary) => _competitions.Add(summary);

    public Task<IReadOnlyList<CompetitionSummary>> SearchAsync(
        DateOnly? onOrAfter, string? classContentHash, CancellationToken cancellationToken = default)
    {
        IEnumerable<CompetitionSummary> query = _competitions;
        if (onOrAfter is { } threshold)
        {
            query = query.Where(c => c.StartDate >= threshold);
        }

        if (!string.IsNullOrWhiteSpace(classContentHash))
        {
            query = query.Where(c => c.ClassContentHash == classContentHash);
        }

        return Task.FromResult<IReadOnlyList<CompetitionSummary>>(query.ToList());
    }
}

/// <summary>Hand-written fake (LADR-0003 "Doubles") — resolves handlers from a fixed dictionary, no real DI container.</summary>
internal sealed class FakeServiceProvider(Dictionary<Type, object> services) : IServiceProvider
{
    public object? GetService(Type serviceType) => services.GetValueOrDefault(serviceType);
}
