// The portable half of the IEventStore adapter —
// kanban/completed/jasperfx-shared-store-contracts.md WI-4, originally
// kanban/completed/command-side-steel-thread-plan.md WI-7, LADR-0001
// §4.1/§4.4/§4.8.
//
// Everything here is written against the JasperFx.Events shared contracts that
// Marten, Polecat and Fisher each implement as their own types — no wrapper, no
// adapter. A subclass supplies only the four things the shared contracts do not
// reach (see the abstract members below); nothing store-shaped appears in this
// file, and nothing store-shaped escapes the pair to the Application layer.
//
// The `expectedVersion` argument of the version-checked
// Append(streamId, expectedVersion, events) overload is the stream version
// AFTER the new events land (current version + events.Count), not before. Get
// this backwards and every mutation after the first silently asserts the wrong
// version and either always succeeds or always fails. This is the semantics of
// the shared JasperFx.Events.IEventOperations contract, so it holds for every
// store implementing it — but it is not documented by the package and was
// confirmed empirically against a running PostgreSQL before being relied on.
//
// ExpectedVersion.Exact(v) here always means "the stream currently has v
// events" (PersonLoader.cs's convention — v is events.Count from a prior
// read), so the translation below is Append(streamId, v + events.Count, events).
//
// A store must also be configured so that the version-checked overload is
// actually honoured — on Marten that is StoreOptions.Events.AppendMode = Rich,
// and MartenEventStore.cs records what happens when it is not.

using JasperFx.Events;
using JasperFx.Events.Documents;
using Soarscore.Application;
using Soarscore.Domain;

namespace Soarscore.Infrastructure;

/// <summary>
/// Store-agnostic <see cref="Soarscore.Application.IEventStore"/> implementation
/// over <see cref="IDocumentSessionFactory"/>. Subclass per backend and supply
/// the four abstract members.
/// </summary>
/// <remarks>
/// The port is written out in full because JasperFx.Events has an
/// <c>IEventStore</c> of its own — an unrelated interface, and not the one this
/// class implements.
/// </remarks>
public abstract class JasperFxEventStore(IDocumentSessionFactory sessions) : Soarscore.Application.IEventStore
{
    public async Task<Result<long>> AppendAsync(
        Guid streamId,
        ExpectedVersion expected,
        IReadOnlyList<IDomainEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (streamId == Guid.Empty)
        {
            return RejectEmptyStreamId<long>();
        }

        await using var session = sessions.LightweightSession();
        var eventOperations = EventOperationsOf(session);

        if (expected.IsNoStream)
        {
            eventOperations.StartStream(streamId, events);
        }
        else if (expected.IsExact)
        {
            eventOperations.Append(streamId, expected.Version + events.Count, events);
        }
        else
        {
            eventOperations.Append(streamId, events);
        }

        // Assigned from inside the exception filter below so that an exception
        // the backend does not recognise is left to propagate from the filter
        // itself — the stack is never unwound and rethrown here, which is what
        // the single-class version of this method did with `catch (Exception)
        // when (...)`.
        Result<long>? translated = null;

        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (EventStreamUnexpectedMaxEventIdException)
        {
            // The one append failure the shared contract names, so the one this
            // base translates itself. It is checked before the store-specific
            // translation because that is the order the Marten-only version of
            // this method used, and a store may well wrap a lower-level
            // constraint violation inside a concurrency failure.
            return Result<long>.Failure(
                "eventStore.concurrencyConflict",
                $"Stream {streamId} was modified since it was last read.");
        }
        catch (Exception ex) when ((translated = TranslateAppendException(ex, streamId)) is not null)
        {
            // Anything the backend recognises, it translates; anything it does
            // not is not ours to turn into a made-up failure code.
            return translated.Value;
        }

        // IEventStoreOperations derives from IQueryEventStore, so the write
        // session's own accessor answers this — no second cast.
        var state = await eventOperations.FetchStreamStateAsync(streamId, cancellationToken);
        return Result<long>.Success(state!.Version);
    }

    public async Task<Result<IReadOnlyList<IDomainEvent>>> ReadStreamAsync(
        Guid streamId,
        long fromVersion,
        CancellationToken cancellationToken = default)
    {
        if (streamId == Guid.Empty)
        {
            return RejectEmptyStreamId<IReadOnlyList<IDomainEvent>>();
        }

        await using var session = sessions.QuerySession();
        var events = await EventQueriesOf(session)
            .FetchStreamAsync(streamId, fromVersion: fromVersion, token: cancellationToken);

        // Same Tombstone-filtering rule as ReadAllAsync — a store's own
        // bookkeeping marker events can be interleaved into a real stream's row
        // set (confirmed on Marten, whose high-water-mark agent does exactly
        // that), and they must not reach the Application layer.
        IReadOnlyList<IDomainEvent> result = events
            .Where(e => e.Data is IDomainEvent)
            .Select(e => (IDomainEvent)e.Data)
            .ToList();
        return Result<IReadOnlyList<IDomainEvent>>.Success(result);
    }

    /// <summary>
    /// Per-store: there is no shared global-sequence read.
    /// kanban/completed/jasperfx-shared-store-contracts.md WI-4 "Decision"
    /// weighed the portable <c>QueryEventsAsync</c> and rejected it — no
    /// sequence cursor, so no replay ordering guarantee, which is the whole
    /// point of LADR-0001 §4.10's replay path.
    /// </summary>
    public abstract Task<Result<IReadOnlyList<RecordedEvent>>> ReadAllAsync(
        long fromPosition,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The <c>.Events</c> accessor is the one thing WI-4 found is not on any
    /// shared session contract: each store exposes it as its own derived type
    /// (Marten's <c>IDocumentSession.Events</c> is
    /// <c>Marten.Events.IEventStoreOperations</c>, which derives from the
    /// JasperFx interface of the same simple name). One cast per backend, in
    /// place of wrapping anything.
    /// </summary>
    protected abstract IEventStoreOperations EventOperationsOf(IDocumentSessionOperations session);

    /// <summary>The read-only counterpart of <see cref="EventOperationsOf"/>.</summary>
    protected abstract IQueryEventStore EventQueriesOf(IDocumentReadOperations session);

    /// <summary>
    /// Translates a store-specific append failure into a domain failure code
    /// (LADR-0001 §4.1 — nothing store-shaped escapes this project). Returns
    /// <c>null</c> for an exception the backend does not recognise, which
    /// <see cref="AppendAsync"/> then leaves to propagate untouched.
    /// </summary>
    protected abstract Result<long>? TranslateAppendException(Exception exception, Guid streamId);

    /// <summary>
    /// See MartenEventStore.cs's Guid.Empty note. No store is trusted to treat
    /// Guid.Empty as "a stream that does not exist", so both read and append
    /// reject it before the backend ever sees it.
    /// </summary>
    protected static Result<T> RejectEmptyStreamId<T>() =>
        Result<T>.Failure("eventStore.emptyStreamId", "A stream id must not be Guid.Empty.");
}
