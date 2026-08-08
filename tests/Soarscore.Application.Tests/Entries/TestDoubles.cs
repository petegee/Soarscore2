// Hand-written fakes (LADR-0003 "Doubles") for the WI-6/WI-7 handler tests —
// a real DI container and a real store are Infrastructure/Api's composition
// concerns (WI-9), not something a handler test needs. Local to this
// namespace rather than reused from People/TestDoubles.cs or
// Competitions/TestDoubles.cs, following the same precedent
// Competitions/TestDoubles.cs's header records for not sharing People's copy.

using Soarscore.Application.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Tests.Entries;

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

/// <summary>In-memory stand-in for the Marten-backed `entry_index` read model (WI-9).</summary>
internal sealed class FakeEntryQuery : IEntryQuery
{
    private readonly List<EntrySummary> _entries = [];

    public void Seed(EntrySummary summary) => _entries.Add(summary);

    public Task<IReadOnlyList<EntrySummary>> FindAsync(
        CompetitionId competitionRef,
        int? phaseOrdinal,
        int? roundOrdinal,
        int? taskRoundOrdinal,
        GroupId? groupRef,
        CompetitorId? competitorRef,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<EntrySummary> query = _entries.Where(e => e.CompetitionRef == competitionRef);

        if (phaseOrdinal is { } phase)
        {
            query = query.Where(e => e.PhaseOrdinal == phase);
        }

        if (roundOrdinal is { } round)
        {
            query = query.Where(e => e.RoundOrdinal == round);
        }

        if (taskRoundOrdinal is { } taskRound)
        {
            query = query.Where(e => e.TaskRoundOrdinal == taskRound);
        }

        if (groupRef is { } group)
        {
            query = query.Where(e => e.GroupRef == group);
        }

        if (competitorRef is { } competitor)
        {
            query = query.Where(e => e.CompetitorRef == competitor);
        }

        return Task.FromResult<IReadOnlyList<EntrySummary>>(query.ToList());
    }
}

/// <summary>Hand-written fake (LADR-0003 "Doubles") — resolves handlers from a fixed dictionary, no real DI container.</summary>
internal sealed class FakeServiceProvider(Dictionary<Type, object> services) : IServiceProvider
{
    public object? GetService(Type serviceType) => services.GetValueOrDefault(serviceType);
}
