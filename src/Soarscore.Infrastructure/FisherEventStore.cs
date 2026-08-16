// The Fisher/SQLite-specific half of the IEventStore adapter —
// kanban/completed/multi-backend-deployment.md WI-2. The portable body lives
// in JasperFxEventStore.cs; what is left here is the four things the JasperFx
// shared contracts do not reach, and nothing else. That it IS only those four
// is the story's whole claim, and this file is where it is either true or not.
//
// This is the only place a Fisher or SQLite exception is caught, and — with
// FisherConfig.cs — one of only two files in the project that name Fisher.
// Read it beside MartenEventStore.cs: the two are the same four members with
// different vendor names inside them.
//
// The Guid.Empty guard, the Tombstone filter and the meaning of
// `expectedVersion` are all in the base and apply here unchanged; see
// JasperFxEventStore.cs. There is no Fisher counterpart to Marten's
// `AppendMode = Rich` — see FisherConfig.cs for why none is needed.

using Microsoft.Data.Sqlite;
using Soarscore.Application;
using Soarscore.Domain;

namespace Soarscore.Infrastructure;

public sealed class FisherEventStore(Fisher.IDocumentStore store) : JasperFxEventStore(store)
{
    /// <summary>
    /// Fisher's <c>IDocumentSession.Events</c>. Same seam as Marten's, for the
    /// same reason — the <c>.Events</c> accessor is on no shared session
    /// contract (kanban/completed/jasperfx-shared-store-contracts.md WI-4). The
    /// session the shared <c>IDocumentSessionFactory.LightweightSession()</c>
    /// hands back is a <see cref="Fisher.IDocumentSession"/>, whose
    /// <c>Events</c> is a <c>Fisher.Events.EventOperations</c> — a concrete
    /// class implementing the JasperFx contract, where Marten's is an interface
    /// deriving from it. Either way the cast is one line and nothing is wrapped.
    /// </summary>
    protected override JasperFx.Events.IEventStoreOperations EventOperationsOf(
        JasperFx.Events.Documents.IDocumentSessionOperations session) =>
        ((Fisher.IDocumentSession)session).Events;

    /// <summary>The read-only counterpart. A Fisher <c>IDocumentSession</c> is
    /// also an <see cref="Fisher.IQuerySession"/>, so this serves both.</summary>
    protected override JasperFx.Events.IQueryEventStore EventQueriesOf(
        JasperFx.Events.Documents.IDocumentReadOperations session) =>
        ((Fisher.IQuerySession)session).Events;

    /// <summary>
    /// Fisher wants the version the stream holds BEFORE the append — the
    /// opposite of Marten's reading of the same shared-contract argument. So
    /// <c>ExpectedVersion.Exact(v)</c> passes straight through, and
    /// <paramref name="eventCount"/> is unused.
    /// </summary>
    /// <remarks>
    /// Established by spiking Fisher directly: against a stream holding one
    /// event, <c>Append(id, 1, …)</c> succeeds and <c>Append(id, 2, …)</c>
    /// throws <c>EventStreamUnexpectedMaxEventIdException("expected 2 but was
    /// 1")</c>. On Marten the same two calls behave the other way round. See
    /// JasperFxEventStore.cs's header — this is the divergence that made this
    /// member abstract.
    /// </remarks>
    protected override long AppendExpectedVersion(long currentVersion, int eventCount) =>
        currentVersion;

    /// <summary>
    /// The two Fisher/SQLite append failures with a domain meaning, in the same
    /// order and to the same two failure codes MartenEventStore.cs uses — the
    /// codes are the Application layer's contract and cannot vary by backend.
    /// The shared concurrency failure is handled by the base, ahead of this.
    /// </summary>
    protected override Result<long>? TranslateAppendException(Exception exception, Guid streamId)
    {
        if (exception is Fisher.Exceptions.ExistingStreamIdCollisionException)
        {
            return Result<long>.Failure(
                "eventStore.streamAlreadyExists",
                $"Stream {streamId} already exists — expected no stream.");
        }

        if (FindUniqueViolation(exception) is { } violation)
        {
            return Result<long>.Failure(
                "eventStore.uniqueConstraintViolation",
                $"A unique constraint was violated: {violation.Message}.");
        }

        return null;
    }

    /// <summary>
    /// Per-store by decision (kanban/completed/jasperfx-shared-store-contracts.md
    /// WI-4 "Decision"), and per-store by necessity here: Fisher has no
    /// <c>QueryAllRawEvents()</c> — the LINQ-over-the-event-log surface Marten's
    /// implementation orders and pages with. What it has is
    /// <c>QueryEventsAsync(Expression&lt;Func&lt;IEvent,bool&gt;&gt;)</c>, a
    /// filter with no ordering and no limit, so the sort and the page are
    /// applied here in memory after the filter has run in the database.
    /// </summary>
    /// <remarks>
    /// That is a real difference in cost, not just in code: this reads every
    /// event at or after <paramref name="fromPosition"/> to return
    /// <paramref name="batchSize"/> of them. It is acceptable only because of
    /// what this method is for — LADR-0001 §4.10's replay path, which walks the
    /// whole log by construction — and at this project's scale (≤20 pilots, ≤8
    /// rounds/day, a season's log measured in thousands of events, and a store
    /// that is a local file). It has no production callers today. If one
    /// appears that pages a large log incrementally, this needs Fisher's
    /// <c>IAdvancedSql</c> and a hand-written ORDER BY / LIMIT instead.
    /// </remarks>
    public override async Task<Result<IReadOnlyList<RecordedEvent>>> ReadAllAsync(
        long fromPosition,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();

        var events = await session.Events.QueryEventsAsync(
            e => e.Sequence >= fromPosition, cancellationToken);

        // The same Tombstone-filtering rule the base and MartenEventStore.cs
        // apply — a store's own bookkeeping marker events must never reach the
        // Application layer — followed by the ordering and paging the query
        // itself could not express.
        IReadOnlyList<RecordedEvent> result = events
            .Where(e => e.Data is IDomainEvent)
            .OrderBy(e => e.Sequence)
            .Take(batchSize)
            .Select(e => new RecordedEvent(e.StreamId, e.Version, e.Sequence, (IDomainEvent)e.Data))
            .ToList();
        return Result<IReadOnlyList<RecordedEvent>>.Success(result);
    }

    /// <summary>
    /// Walks the exception chain for a SQLite constraint violation, the
    /// counterpart of MartenEventStore.cs's PostgresException SqlState 23505
    /// walk — and for the same reason: Fisher wraps the low-level
    /// Microsoft.Data.Sqlite exception in its own type, so the stable signal is
    /// the inner <see cref="SqliteException"/>, not the wrapper.
    /// </summary>
    /// <remarks>
    /// SQLite reports every constraint failure as primary result code 19
    /// (SQLITE_CONSTRAINT) and distinguishes them only in the extended code:
    /// 2067 SQLITE_CONSTRAINT_UNIQUE (a UNIQUE index) and 1555
    /// SQLITE_CONSTRAINT_PRIMARYKEY (an INTEGER PRIMARY KEY / rowid collision).
    /// Both are the uniqueness failure `people`'s email index exists to
    /// produce, so both map to the same code; the other extended codes under 19
    /// (CHECK, NOT NULL, FOREIGN KEY, TRIGGER) are different failures and are
    /// deliberately left to propagate untranslated, exactly as the base's
    /// contract for a null return requires.
    /// </remarks>
    private static SqliteException? FindUniqueViolation(Exception ex)
    {
        const int SqliteConstraintUnique = 2067;
        const int SqliteConstraintPrimaryKey = 1555;

        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite
                && sqlite.SqliteExtendedErrorCode is SqliteConstraintUnique or SqliteConstraintPrimaryKey)
            {
                return sqlite;
            }
        }

        return null;
    }
}
