// The Marten Inline projection shim for the `people` read model —
// kanban/completed/command-side-steel-thread-plan.md WI-7, LADR-0001 §2/§4.3.
//
// Portable ballast: all it does is group the raw events Marten hands it back
// into per-stream order and replay them through Application's
// PeopleProjection.Apply, which is the only part of this that would survive a
// store swap. Registered Inline (never Async — LADR-0001 §2, the Person email
// uniqueness invariant is provable only inside the append transaction).

using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;

namespace Soarscore.Infrastructure.People;

internal sealed class PersonSummaryProjection : IProjection
{
    public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken cancellation)
    {
        foreach (var stream in events.GroupBy(e => e.StreamId))
        {
            // PersonSummary.Id is a PersonId, not a bare Guid — Marten's strong-typed-
            // identifier convention (also relied on for stream identity elsewhere) means
            // LoadAsync must be called with that type or it throws DocumentIdTypeMismatchException.
            var current = await operations.LoadAsync<PersonSummary>(new PersonId(stream.Key), cancellation);
            foreach (var e in stream.OrderBy(e => e.Version))
            {
                if (e.Data is PersonEvent personEvent)
                {
                    current = PeopleProjection.Apply(current, personEvent);
                }
            }

            if (current is not null)
            {
                operations.Store(current);
            }
        }
    }
}
