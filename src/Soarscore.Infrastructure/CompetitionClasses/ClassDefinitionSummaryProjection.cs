// The Inline projection for the `class_library` read model —
// kanban/completed/class-definition-adoption-steel-thread-plan.md WI-5,
// LADR-0001 §2/§4.3. Portable ballast: groups the raw events the store hands it
// back into per-stream order and replays them through Application's
// ClassDefinitionProjection.Apply, which is the only part of this that would
// survive a store swap. Registered Inline for read-your-own-writes
// (POST /publish-class-definition immediately followed by
// GET /class-definitions) even though `class_library` enforces no uniqueness
// invariant the way `people` does. Mirrors People/PersonSummaryProjection.cs.
//
// kanban/completed/jasperfx-shared-store-contracts.md WI-3 split this in two:
// a store-agnostic fold against JasperFx's IJasperFxProjection<TOperations> /
// IDocumentWriteOperations, plus a per-store registration shim
// (MartenClassDefinitionSummaryProjection, at the foot of this file) — the only
// thing here that names Marten.

using JasperFx.Events;
using JasperFx.Events.Documents;
using JasperFx.Events.Projections;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Infrastructure.CompetitionClasses;

internal class ClassDefinitionSummaryProjection<TOperations> : IJasperFxProjection<TOperations>
    where TOperations : IDocumentWriteOperations
{
    public async Task ApplyAsync(TOperations operations, IReadOnlyList<IEvent> events, CancellationToken cancellation)
    {
        foreach (var stream in events.GroupBy(e => e.StreamId))
        {
            // The shared IDocumentReadOperations.LoadAsync<T>(Guid) overload, called
            // directly — kanban/completed/jasperfx-shared-store-contracts.md WI-6. Alone
            // of the four projections this one needs no per-store load seam:
            // ClassDefinitionSummary.Id is a bare Guid by deliberate design (see its XML
            // doc — the id is the raw StreamId, and a content hash is not recoverable from
            // it), so the strong-typed-id limit that forces the other three to override a
            // LoadCurrentAsync member simply does not arise here. That limit, and the
            // acceptance-suite run that proved it, are written down on
            // People/PersonSummaryProjection.cs's LoadCurrentAsync.
            var current = await operations.LoadAsync<ClassDefinitionSummary>(stream.Key, cancellation);

            foreach (var e in stream.OrderBy(e => e.Version))
            {
                if (e.Data is ClassDefinitionEvent classDefinitionEvent)
                {
                    current = ClassDefinitionProjection.Apply(current, classDefinitionEvent);
                }
            }

            if (current is not null)
            {
                operations.Store(current);
            }
        }
    }
}

// Registration marker only — and, unlike the other three, nothing else.
// Marten.Events.Projections.IProjection is IJasperFxProjection<Marten.IDocumentOperations>
// plus IMartenRegistrable, which declares no instance members, so the base class above
// already satisfies every member and this type exists purely so MartenConfig can register it.
internal sealed class MartenClassDefinitionSummaryProjection
    : ClassDefinitionSummaryProjection<Marten.IDocumentOperations>, Marten.Events.Projections.IProjection;

// The Fisher/SQLite shim — kanban/completed/multi-backend-deployment.md WI-3.
// Registration marker only, and alone of the four it needs nothing else on
// either backend: ClassDefinitionSummary.Id is a bare Guid, so the shared
// contract's Guid identity overload reaches it directly. That the *same* one of
// four projections is the simple one on both stores is the point — the seam is
// the contract's, not any store's.
internal sealed class FisherClassDefinitionSummaryProjection
    : ClassDefinitionSummaryProjection<Fisher.IDocumentSession>, Fisher.Projections.IProjection;
