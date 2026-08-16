// The Marten adapter for IClassLibraryQuery — docs/plans/class-definition-adoption-steel-thread-plan.md
// WI-3/WI-5. Reads the `class_library` read model only; never the event log.
// Mirrors People/MartenPeopleQuery.cs.

using Marten;
using Soarscore.Application.Queries.CompetitionClasses;

namespace Soarscore.Infrastructure.CompetitionClasses;

public sealed class MartenClassLibraryQuery(IDocumentStore store) : IClassLibraryQuery
{
    public async Task<ClassDefinitionSummary?> FindByHashAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();
        return await session.Query<ClassDefinitionSummary>().FirstOrDefaultAsync(s => s.ContentHash == contentHash, cancellationToken);
    }

    public async Task<IReadOnlyList<ClassDefinitionSummary>> SearchAsync(string? name, bool activeOnly, CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();
        IQueryable<ClassDefinitionSummary> query = session.Query<ClassDefinitionSummary>();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(s => s.Name.Contains(name));
        }

        if (activeOnly)
        {
            query = query.Where(s => s.RetiredAt == null);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
