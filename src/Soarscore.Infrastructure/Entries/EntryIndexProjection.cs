// The Marten Inline projection shim for the `entry_index` read model —
// kanban/completed/capture-a-score-steel-thread-plan.md WI-9, LADR-0001 §2/§4.3.
// Portable ballast: groups the raw events Marten hands it back into
// per-stream order and replays them through Application's
// EntryProjection.Apply, which is the only part of this that would survive a
// store swap. Mirrors Competitions/CompetitionSummaryProjection.cs.

using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain.Entries;

namespace Soarscore.Infrastructure.Entries;

internal sealed class EntryIndexProjection : IProjection
{
    public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken cancellation)
    {
        foreach (var stream in events.GroupBy(e => e.StreamId))
        {
            // EntrySummary.Id is an EntryId, not a bare Guid — Marten's strong-typed-
            // identifier convention means LoadAsync must be called with that type or
            // it throws DocumentIdTypeMismatchException (same fix as
            // People/PersonSummaryProjection.cs and Competitions/CompetitionSummaryProjection.cs).
            var current = await operations.LoadAsync<EntrySummary>(new EntryId(stream.Key), cancellation);

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
}
