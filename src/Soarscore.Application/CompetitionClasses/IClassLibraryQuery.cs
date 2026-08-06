// The `class_library` read model's query port — docs/plans/class-definition-adoption-steel-thread-plan.md
// WI-3, LADR-0001 §4.2. Defined here, implemented in Soarscore.Infrastructure
// against Marten; IDocumentSession never appears above that project. Mirrors
// People/IPeopleQuery.cs.
//
// No get-by-hash-returning-the-full-definition here, deliberately — the same
// rule WI-5 of the command-side plan states for people: a lookup that needs
// the full ClassDefinition folds the stream (GetClassDefinition, WI-4). This
// interface exists solely for the cross-stream search a stream can't answer.

namespace Soarscore.Application.CompetitionClasses;

public interface IClassLibraryQuery
{
    Task<ClassDefinitionSummary?> FindByHashAsync(string contentHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassDefinitionSummary>> SearchAsync(string? name, bool activeOnly, CancellationToken cancellationToken = default);
}
