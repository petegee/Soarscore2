// Hand-written fakes (LADR-0003 "Doubles") for the WI-6 handler tests — a
// real DI container and a real store are Infrastructure/Api's composition
// concerns (WI-7/WI-9), not something a handler test needs.

using Soarscore.Application.Queries.People;
using Soarscore.Domain;
using Soarscore.Domain.People;

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

    public Task<IReadOnlyList<PersonSummary>> FindByIdsAsync(IReadOnlyList<PersonId> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PersonSummary>>(_people.Where(p => ids.Contains(p.Id)).ToList());

    // WI-5 port additions (authentication-and-authorisation.md). Seeded
    // dictionaries rather than folds of IdentityLinked/RoleGranted events:
    // this fake stands in for the read model, and the projection that derives
    // those rows is WI-6/WI-8's subject with its own tests. FindIdentityAsync
    // is exercised by the WI-9 middleware tests' successors (WI-7's
    // LinkSignIn); CountByRoleAsync by the last-organiser guard.
    private readonly Dictionary<(string Provider, string Subject), IdentityMatch> _identities = [];

    public void SeedIdentity(string provider, string subject, PersonId personId, params PersonRole[] roles) =>
        _identities[(provider, subject)] = new IdentityMatch(personId, roles);

    public Task<IdentityMatch?> FindIdentityAsync(string provider, string subject, CancellationToken cancellationToken = default) =>
        Task.FromResult(_identities.GetValueOrDefault((provider, subject)));

    private readonly Dictionary<PersonRole, int> _roleCounts = [];

    public void SeedRoleCount(PersonRole role, int count) => _roleCounts[role] = count;

    public Task<int> CountByRoleAsync(PersonRole role, CancellationToken cancellationToken = default) =>
        Task.FromResult(_roleCounts.GetValueOrDefault(role, 0));
}

/// <summary>
/// D5's race in a can (authentication-and-authorisation.md WI-7): the
/// concurrent winner's identity row becomes readable only after the loser's
/// first append has been rejected — the first <see cref="FindIdentityAsync"/>
/// call misses, every later one delegates (and hits). <see cref="Inner"/> is
/// exposed so a test seeds the winner's row up front.
/// </summary>
internal sealed class RaceWinnerIdentityQuery(FakePeopleQuery inner) : IPeopleQuery
{
    public FakePeopleQuery Inner => inner;

    public int IdentityLookups { get; private set; }

    public Task<PersonSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        inner.FindByEmailAsync(email, cancellationToken);

    public Task<IReadOnlyList<PersonSummary>> SearchByNameAsync(string name, CancellationToken cancellationToken = default) =>
        inner.SearchByNameAsync(name, cancellationToken);

    public Task<IReadOnlyList<PersonSummary>> FindByIdsAsync(IReadOnlyList<PersonId> ids, CancellationToken cancellationToken = default) =>
        inner.FindByIdsAsync(ids, cancellationToken);

    public Task<int> CountByRoleAsync(PersonRole role, CancellationToken cancellationToken = default) =>
        inner.CountByRoleAsync(role, cancellationToken);

    public Task<IdentityMatch?> FindIdentityAsync(string provider, string subject, CancellationToken cancellationToken = default)
    {
        IdentityLookups++;
        return IdentityLookups == 1
            ? Task.FromResult<IdentityMatch?>(null)
            : inner.FindIdentityAsync(provider, subject, cancellationToken);
    }
}

/// <summary>
/// Decorator over <see cref="FakeEventStore"/> that fails the first
/// <c>violations</c> appends with the adapters' unique-index translation
/// (MartenEventStore.cs / FisherEventStore.cs surface the people projection's
/// email and (provider, subject) unique indexes as exactly this failure).
/// Counts every append so a test can prove the retry happened exactly once.
/// </summary>
internal sealed class FakeUniqueIndexEventStore(FakeEventStore inner, int violations) : IEventStore
{
    public int AppendCalls { get; private set; }

    public Task<Result<long>> AppendAsync(
        Guid streamId, ExpectedVersion expected, IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        AppendCalls++;
        if (AppendCalls <= violations)
        {
            return Task.FromResult(Result<long>.Failure(
                "eventStore.uniqueConstraintViolation",
                $"A unique index in the people projection rejected the append to stream {streamId}."));
        }

        return inner.AppendAsync(streamId, expected, events, cancellationToken);
    }

    public Task<Result<IReadOnlyList<IDomainEvent>>> ReadStreamAsync(
        Guid streamId, long fromVersion, CancellationToken cancellationToken = default) =>
        inner.ReadStreamAsync(streamId, fromVersion, cancellationToken);

    public Task<Result<IReadOnlyList<RecordedEvent>>> ReadAllAsync(
        long fromPosition, int batchSize, CancellationToken cancellationToken = default) =>
        inner.ReadAllAsync(fromPosition, batchSize, cancellationToken);
}

/// <summary>Hand-written fake (LADR-0003 "Doubles") — resolves handlers from a fixed dictionary, no real DI container.</summary>
internal sealed class FakeServiceProvider(Dictionary<Type, object> services) : IServiceProvider
{
    public object? GetService(Type serviceType) => services.GetValueOrDefault(serviceType);
}
