// The event-store port — kanban/completed/command-side-steel-thread-plan.md WI-3,
// LADR-0001 §4.1. Exactly the three methods the ADR permits, and no more: no
// IQueryable, no Marten type appears above Soarscore.Infrastructure, which is
// the only project allowed to implement this interface (WI-7).

using Soarscore.Domain;

namespace Soarscore.Application;

/// <summary>
/// The concurrency check for <see cref="IEventStore.AppendAsync"/>. Backed by
/// the store's <c>(stream_id, version)</c> uniqueness constraint, never by a
/// read-check-write (LADR-0001 §4.4) — this type only states the caller's
/// expectation, it does not perform the check.
/// </summary>
public readonly record struct ExpectedVersion
{
    private enum Kind { Any, NoStream, Exact }

    private readonly Kind _kind;

    private ExpectedVersion(Kind kind, long version)
    {
        _kind = kind;
        Version = version;
    }

    /// <summary>The version being appended after, meaningful only when this is <see cref="Exact"/>.</summary>
    public long Version { get; }

    public bool IsAny => _kind == Kind.Any;

    public bool IsNoStream => _kind == Kind.NoStream;

    public bool IsExact => _kind == Kind.Exact;

    /// <summary>No concurrency check — the caller does not care what else has happened to the stream.</summary>
    public static ExpectedVersion Any { get; } = new(Kind.Any, default);

    /// <summary>The stream must not already exist — the shape of every aggregate's first append.</summary>
    public static ExpectedVersion NoStream { get; } = new(Kind.NoStream, default);

    /// <summary>The stream must be at exactly this version — the shape of every mutation, per the WI-6 handler template.</summary>
    public static ExpectedVersion Exact(long version) => new(Kind.Exact, version);
}

/// <summary>
/// One event as read back from the store, carrying the addressing metadata a
/// single-stream fold does not need but a cross-stream replay does.
/// <see cref="IEventStore.ReadStreamAsync"/> already knows its one streamId,
/// so it returns bare events; <see cref="IEventStore.ReadAllAsync"/> spans
/// every stream in the log (LADR-0001 §4.10's full read-model replay) and
/// must say which stream, and which position, each event belongs to.
/// </summary>
public sealed record RecordedEvent(Guid StreamId, long Version, long Position, IDomainEvent Event);

public interface IEventStore
{
    Task<Result<long>> AppendAsync(
        Guid streamId,
        ExpectedVersion expected,
        IReadOnlyList<IDomainEvent> events,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<IDomainEvent>>> ReadStreamAsync(
        Guid streamId,
        long fromVersion,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<RecordedEvent>>> ReadAllAsync(
        long fromPosition,
        int batchSize,
        CancellationToken cancellationToken = default);
}
