// The Marten Inline projection shim for the `competitions` read model —
// kanban/completed/create-competition-steel-thread-plan.md WI-1, LADR-0001 §2/§4.3.
// Portable ballast: groups the raw events Marten hands it back into
// per-stream order and replays them through Application's
// CompetitionProjection.Apply, which is the only part of this that would
// survive a store swap. Mirrors CompetitionClasses/ClassDefinitionSummaryProjection.cs.
//
// Not registered with Marten yet — the event-type mapping for
// CompetitionCreated and this projection's Inline registration are WI-4's
// job (kanban/completed/create-competition-steel-thread-plan.md), a later thread
// that depends on this one landing first.

using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;

namespace Soarscore.Infrastructure.Competitions;

internal sealed class CompetitionSummaryProjection : IProjection
{
    public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken cancellation)
    {
        foreach (var stream in events.GroupBy(e => e.StreamId))
        {
            // CompetitionSummary.Id is a CompetitionId, not a bare Guid — Marten's
            // strong-typed-identifier convention means LoadAsync must be called with
            // that type or it throws DocumentIdTypeMismatchException (same fix as
            // People/PersonSummaryProjection.cs).
            var current = await operations.LoadAsync<CompetitionSummary>(new CompetitionId(stream.Key), cancellation);

            foreach (var e in stream.OrderBy(e => e.Version))
            {
                if (e.Data is CompetitionEvent competitionEvent)
                {
                    current = CompetitionProjection.Apply(current, competitionEvent);
                }
            }

            if (current is not null)
            {
                operations.Store(current);
            }
        }
    }
}
