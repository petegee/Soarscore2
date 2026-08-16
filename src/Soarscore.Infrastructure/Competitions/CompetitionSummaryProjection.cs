// The Inline projection for the `competitions` read model —
// kanban/completed/create-competition-steel-thread-plan.md WI-1, LADR-0001 §2/§4.3.
// Portable ballast: groups the raw events the store hands it back into
// per-stream order and replays them through Application's
// CompetitionProjection.Apply, which is the only part of this that would
// survive a store swap. Mirrors CompetitionClasses/ClassDefinitionSummaryProjection.cs.
//
// kanban/completed/jasperfx-shared-store-contracts.md WI-3 split this in two:
// a store-agnostic fold against JasperFx's IJasperFxProjection<TOperations> /
// IDocumentWriteOperations, plus a per-store registration shim
// (MartenCompetitionSummaryProjection, at the foot of this file) — the only thing
// here that names Marten.

using JasperFx.Events;
using JasperFx.Events.Documents;
using JasperFx.Events.Projections;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;

namespace Soarscore.Infrastructure.Competitions;

internal class CompetitionSummaryProjection<TOperations> : IJasperFxProjection<TOperations>
    where TOperations : IDocumentWriteOperations
{
    public async Task ApplyAsync(TOperations operations, IReadOnlyList<IEvent> events, CancellationToken cancellation)
    {
        foreach (var stream in events.GroupBy(e => e.StreamId))
        {
            var current = await LoadCurrentAsync(operations, stream.Key, cancellation);

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

    // The strong-typed-id seam — CompetitionSummary.Id is a CompetitionId, not a bare
    // Guid. kanban/completed/jasperfx-shared-store-contracts.md WI-6. The full finding
    // (why the shared contract's Guid-only identity overloads cannot express this, and
    // how the acceptance suite proved it on 2026-08-16) is written down once, on
    // People/PersonSummaryProjection.cs's LoadCurrentAsync. It applies verbatim here.
    protected virtual async Task<CompetitionSummary?> LoadCurrentAsync(
        TOperations operations, Guid streamId, CancellationToken cancellation)
        => await operations.LoadAsync<CompetitionSummary>(streamId, cancellation);
}

// Registration marker plus the one per-store override. Marten.Events.Projections.IProjection
// is IJasperFxProjection<Marten.IDocumentOperations> plus IMartenRegistrable, which declares
// no instance members — the base class above satisfies every member of both.
internal sealed class MartenCompetitionSummaryProjection
    : CompetitionSummaryProjection<Marten.IDocumentOperations>, Marten.Events.Projections.IProjection
{
    // Marten's runtime-dispatching LoadAsync<T>(object id) overload, which a CompetitionId
    // binds to because it declares no conversion to Guid. Mirrors
    // People/PersonSummaryProjection.cs's override exactly.
    protected override async Task<CompetitionSummary?> LoadCurrentAsync(
        Marten.IDocumentOperations operations, Guid streamId, CancellationToken cancellation)
        => await operations.LoadAsync<CompetitionSummary>(new CompetitionId(streamId), cancellation);
}
