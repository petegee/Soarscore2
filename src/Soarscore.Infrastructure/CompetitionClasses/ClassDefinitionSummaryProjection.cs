// The Marten Inline projection shim for the `class_library` read model —
// docs/plans/class-definition-adoption-steel-thread-plan.md WI-5,
// LADR-0001 §2/§4.3. Portable ballast: groups the raw events Marten hands it
// back into per-stream order and replays them through Application's
// ClassDefinitionProjection.Apply, which is the only part of this that would
// survive a store swap. Registered Inline for read-your-own-writes
// (POST /publish-class-definition immediately followed by
// GET /class-definitions) even though `class_library` enforces no uniqueness
// invariant the way `people` does. Mirrors People/PersonSummaryProjection.cs.

using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Infrastructure.CompetitionClasses;

internal sealed class ClassDefinitionSummaryProjection : IProjection
{
    public async Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken cancellation)
    {
        foreach (var stream in events.GroupBy(e => e.StreamId))
        {
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
