// The Inline projection for the `entry_index` read model —
// kanban/completed/capture-a-score-steel-thread-plan.md WI-9, LADR-0001 §2/§4.3.
// Portable ballast: groups the raw events the store hands it back into
// per-stream order and replays them through Application's
// EntryProjection.Apply, which is the only part of this that would survive a
// store swap. Mirrors Competitions/CompetitionSummaryProjection.cs.
//
// kanban/completed/jasperfx-shared-store-contracts.md WI-3 split this in two:
// a store-agnostic fold against JasperFx's IJasperFxProjection<TOperations> /
// IDocumentWriteOperations, plus a per-store registration shim
// (MartenEntryIndexProjection, at the foot of this file) — the only thing here
// that names Marten.

using JasperFx.Events;
using JasperFx.Events.Documents;
using JasperFx.Events.Projections;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain.Entries;

namespace Soarscore.Infrastructure.Entries;

internal class EntryIndexProjection<TOperations> : IJasperFxProjection<TOperations>
    where TOperations : IDocumentWriteOperations
{
    public async Task ApplyAsync(TOperations operations, IReadOnlyList<IEvent> events, CancellationToken cancellation)
    {
        foreach (var stream in events.GroupBy(e => e.StreamId))
        {
            var current = await LoadCurrentAsync(operations, stream.Key, cancellation);

            foreach (var e in stream.OrderBy(e => e.Version))
            {
                if (e.Data is EntryEvent entryEvent)
                {
                    current = EntryProjection.Apply(current, entryEvent);
                }
            }

            if (current is not null)
            {
                operations.Store(current);
            }
        }
    }

    // The strong-typed-id seam — EntrySummary.Id is an EntryId, not a bare Guid.
    // kanban/completed/jasperfx-shared-store-contracts.md WI-6. The full finding (why
    // the shared contract's Guid-only identity overloads cannot express this, and how the
    // acceptance suite proved it on 2026-08-16) is written down once, on
    // People/PersonSummaryProjection.cs's LoadCurrentAsync. It applies verbatim here.
    protected virtual async Task<EntrySummary?> LoadCurrentAsync(
        TOperations operations, Guid streamId, CancellationToken cancellation)
        => await operations.LoadAsync<EntrySummary>(streamId, cancellation);
}

// Registration marker plus the one per-store override. Marten.Events.Projections.IProjection
// is IJasperFxProjection<Marten.IDocumentOperations> plus IMartenRegistrable, which declares
// no instance members — the base class above satisfies every member of both.
internal sealed class MartenEntryIndexProjection
    : EntryIndexProjection<Marten.IDocumentOperations>, Marten.Events.Projections.IProjection
{
    // Marten's runtime-dispatching LoadAsync<T>(object id) overload, which an EntryId binds
    // to because it declares no conversion to Guid. Mirrors
    // People/PersonSummaryProjection.cs's override exactly.
    protected override async Task<EntrySummary?> LoadCurrentAsync(
        Marten.IDocumentOperations operations, Guid streamId, CancellationToken cancellation)
        => await operations.LoadAsync<EntrySummary>(new EntryId(streamId), cancellation);
}

// The Fisher/SQLite shim — kanban/completed/multi-backend-deployment.md WI-3.
// Mirrors the Marten shim above exactly; the full note on why both stores need a
// strong-typed-id load override is on People/PersonSummaryProjection.cs.
internal sealed class FisherEntryIndexProjection
    : EntryIndexProjection<Fisher.IDocumentSession>, Fisher.Projections.IProjection
{
    protected override async Task<EntrySummary?> LoadCurrentAsync(
        Fisher.IDocumentSession operations, Guid streamId, CancellationToken cancellation)
        => await operations.LoadAsync<EntrySummary, EntryId>(new EntryId(streamId), cancellation);
}
