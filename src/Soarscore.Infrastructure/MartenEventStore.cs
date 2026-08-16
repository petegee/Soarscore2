// The Marten-specific half of the IEventStore adapter —
// kanban/completed/jasperfx-shared-store-contracts.md WI-4, originally
// kanban/completed/command-side-steel-thread-plan.md WI-7, LADR-0001
// §4.1/§4.4/§4.8. The portable body lives in JasperFxEventStore.cs; what is
// left here is the four things the JasperFx shared contracts do not reach.
// This is the only place a Marten or Npgsql exception is caught, and the only
// file in the pair that names Marten or Npgsql at all.
//
// Two things here are not documented by the package and were confirmed
// empirically against a running PostgreSQL before being relied on:
//
//  - StoreOptions.Events.AppendMode must be Rich (ServiceCollectionExtensions.cs).
//    Marten 9's default Quick mode does not support the version-checked
//    Append(streamId, expectedVersion, events) overload this adapter needs —
//    it silently produces an unrelated exception instead of a clean
//    concurrency failure. Rich mode's extra bookkeeping is immaterial at this
//    project's scale (single-digit writes/minute, NFR).
//  - Guid.Empty is not just "a stream that doesn't exist" to Marten: Append
//    throws ArgumentOutOfRangeException for it, but FetchStreamAsync silently
//    drops the stream-id filter entirely and returns every event across every
//    stream in the log, unfiltered — confirmed empirically (WI-8 smoke test:
//    GET /person?id=00000000-… returned another person's data, folded
//    together with the Tombstone event from an unrelated stream). Both
//    AppendAsync and ReadStreamAsync reject Guid.Empty before calling Marten
//    at all, rather than leaning on that inconsistency — the guard is in the
//    base, applied to every backend.
//
// (The third empirical finding, the meaning of `expectedVersion`, is a
// property of the shared contract rather than of Marten, and is recorded on
// JasperFxEventStore.cs.)
//
// Beware the type names: Marten.Events.IEventStoreOperations and
// Marten.Events.IQueryEventStore derive from the JasperFx.Events interfaces of
// exactly the same simple names, so both are written out in full below. The
// JasperFx.Events.Documents namespace is deliberately not imported for the same
// class of reason — its ToListAsync would collide with Marten's own.

using Marten;
using Marten.Exceptions;
using Npgsql;
using Soarscore.Application;
using Soarscore.Domain;

namespace Soarscore.Infrastructure;

public sealed class MartenEventStore(IDocumentStore store) : JasperFxEventStore(store)
{
    /// <summary>
    /// Marten's <c>IDocumentSession.Events</c>. The session handed back by the
    /// shared <c>IDocumentSessionFactory.LightweightSession()</c> is a Marten
    /// <see cref="IDocumentSession"/>; its <c>Events</c> property is a
    /// <c>Marten.Events.IEventStoreOperations</c>, which derives from the
    /// JasperFx contract the base is written against.
    /// </summary>
    protected override JasperFx.Events.IEventStoreOperations EventOperationsOf(
        JasperFx.Events.Documents.IDocumentSessionOperations session) =>
        ((IDocumentSession)session).Events;

    /// <summary>
    /// Marten's <c>IQuerySession.Events</c> — the read-only counterpart. A
    /// Marten <see cref="IDocumentSession"/> is also an
    /// <see cref="IQuerySession"/>, so this serves both the query session and
    /// the write session.
    /// </summary>
    protected override JasperFx.Events.IQueryEventStore EventQueriesOf(
        JasperFx.Events.Documents.IDocumentReadOperations session) =>
        ((IQuerySession)session).Events;

    /// <summary>
    /// Marten wants the version the stream will hold AFTER the append.
    /// Confirmed empirically against a running PostgreSQL; see
    /// JasperFxEventStore.cs's header for why this is a per-store answer and
    /// what happens when it is wrong.
    /// </summary>
    protected override long AppendExpectedVersion(long currentVersion, int eventCount) =>
        currentVersion + eventCount;

    /// <summary>
    /// The two Marten/Npgsql append failures with a domain meaning. The
    /// collision check comes first deliberately: Marten wraps some failures, so
    /// its own typed exception is the more precise signal and must be read
    /// before the generic unique-violation walk gets a chance to match. The
    /// shared concurrency failure is handled by the base, ahead of this.
    /// </summary>
    protected override Result<long>? TranslateAppendException(Exception exception, Guid streamId)
    {
        if (exception is ExistingStreamIdCollisionException)
        {
            return Result<long>.Failure(
                "eventStore.streamAlreadyExists",
                $"Stream {streamId} already exists — expected no stream.");
        }

        if (FindUniqueViolation(exception) is { } violation)
        {
            return Result<long>.Failure(
                "eventStore.uniqueConstraintViolation",
                $"A unique constraint was violated: {violation.ConstraintName}.");
        }

        return null;
    }

    /// <summary>
    /// Per-store by decision, not by oversight —
    /// kanban/completed/jasperfx-shared-store-contracts.md WI-4 "Decision":
    /// the portable query API has no sequence cursor, and ordered replay
    /// (LADR-0001 §4.10) is the only reason this method exists.
    /// </summary>
    public override async Task<Result<IReadOnlyList<RecordedEvent>>> ReadAllAsync(
        long fromPosition,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();

        var events = await session.Events.QueryAllRawEvents()
            .Where(e => e.Sequence >= fromPosition)
            .OrderBy(e => e.Sequence)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        // Marten interleaves its own Tombstone marker events into the global stream
        // for the high-water-mark agent (LADR-0001 §1) — infrastructure bookkeeping,
        // never a domain event, and must not reach the Application layer.
        IReadOnlyList<RecordedEvent> result = events
            .Where(e => e.Data is IDomainEvent)
            .Select(e => new RecordedEvent(e.StreamId, e.Version, e.Sequence, (IDomainEvent)e.Data))
            .ToList();
        return Result<IReadOnlyList<RecordedEvent>>.Success(result);
    }

    /// <summary>
    /// Walks the exception chain for a Postgres unique-violation (SqlState 23505). Marten
    /// wraps the low-level Npgsql exception in its own type (e.g. DocumentAlreadyExistsException
    /// for the `people` projection's email index), so the stable signal is the inner
    /// PostgresException, not Marten's wrapper.
    /// </summary>
    private static PostgresException? FindUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
            {
                return pg;
            }
        }

        return null;
    }
}
