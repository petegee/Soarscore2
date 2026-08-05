// The Marten adapter for IEventStore — docs/plans/command-side-steel-thread-plan.md
// WI-7, LADR-0001 §4.1/§4.4/§4.8. The only place a Marten or Npgsql exception
// is caught; nothing Marten-shaped escapes this project.
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
//  - Marten's `expectedVersion` for that overload is the stream version AFTER
//    the new events land (current version + events.Count), not before. Get
//    this backwards and every mutation after the first silently asserts the
//    wrong version and either always succeeds or always fails.
//
// ExpectedVersion.Exact(v) here always means "the stream currently has v
// events" (PersonLoader.cs's convention — v is events.Count from a prior
// read), so the translation below is Append(streamId, v + events.Count, events).

using Marten;
using Marten.Exceptions;
using Npgsql;
using Soarscore.Application;
using Soarscore.Domain;

namespace Soarscore.Infrastructure;

public sealed class MartenEventStore(IDocumentStore store) : IEventStore
{
    public async Task<Result<long>> AppendAsync(
        Guid streamId,
        ExpectedVersion expected,
        IReadOnlyList<IDomainEvent> events,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.LightweightSession();

        if (expected.IsNoStream)
        {
            session.Events.StartStream(streamId, events);
        }
        else if (expected.IsExact)
        {
            session.Events.Append(streamId, expected.Version + events.Count, events);
        }
        else
        {
            session.Events.Append(streamId, events);
        }

        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ExistingStreamIdCollisionException)
        {
            return Result<long>.Failure(
                "eventStore.streamAlreadyExists",
                $"Stream {streamId} already exists — expected no stream.");
        }
        catch (JasperFx.Events.EventStreamUnexpectedMaxEventIdException)
        {
            return Result<long>.Failure(
                "eventStore.concurrencyConflict",
                $"Stream {streamId} was modified since it was last read.");
        }
        catch (Exception ex) when (FindUniqueViolation(ex) is { } violation)
        {
            return Result<long>.Failure(
                "eventStore.uniqueConstraintViolation",
                $"A unique constraint was violated: {violation.ConstraintName}.");
        }

        var state = await session.Events.FetchStreamStateAsync(streamId, cancellationToken);
        return Result<long>.Success(state!.Version);
    }

    public async Task<Result<IReadOnlyList<IDomainEvent>>> ReadStreamAsync(
        Guid streamId,
        long fromVersion,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(streamId, fromVersion: fromVersion, token: cancellationToken);

        IReadOnlyList<IDomainEvent> result = events.Select(e => (IDomainEvent)e.Data).ToList();
        return Result<IReadOnlyList<IDomainEvent>>.Success(result);
    }

    public async Task<Result<IReadOnlyList<RecordedEvent>>> ReadAllAsync(
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
