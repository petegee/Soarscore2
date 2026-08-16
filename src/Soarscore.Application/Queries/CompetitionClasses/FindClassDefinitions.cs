// docs/plans/class-definition-adoption-steel-thread-plan.md WI-4. Resolves
// through IClassLibraryQuery (WI-3) — the cross-stream search the
// `class_library` read model exists for. Not GetClassDefinition: that goes
// through IEventStore and folds (GetClassDefinition.cs), per
// high-level-architecture.md's "querying by ID loads the stream" rule.
// Mirrors People/FindPeople.cs.

using Soarscore.Domain;

namespace Soarscore.Application.Queries.CompetitionClasses;

// ActiveOnly defaults to false — WI-9 finding: [AsParameters] binding for a
// non-nullable bool with no default fails the whole request (400) the moment
// ?activeOnly= is omitted from the query string, which GET /class-definitions
// (no filters at all) always does.
public readonly record struct FindClassDefinitions(string? Name, bool ActiveOnly = false) : IQuery<IReadOnlyList<ClassDefinitionSummary>>;

public sealed class FindClassDefinitionsHandler(IClassLibraryQuery classLibraryQuery)
    : IQueryHandler<FindClassDefinitions, IReadOnlyList<ClassDefinitionSummary>>
{
    public async Task<Result<IReadOnlyList<ClassDefinitionSummary>>> HandleAsync(FindClassDefinitions query, CancellationToken cancellationToken)
    {
        var matches = await classLibraryQuery.SearchAsync(query.Name, query.ActiveOnly, cancellationToken);
        return Result<IReadOnlyList<ClassDefinitionSummary>>.Success(matches);
    }
}
