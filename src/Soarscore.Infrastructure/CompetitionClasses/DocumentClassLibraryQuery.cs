// The document-store adapter for IClassLibraryQuery — kanban/completed/class-definition-adoption-steel-thread-plan.md
// WI-3/WI-5. Reads the `class_library` read model only; never the event log.
// Mirrors People/DocumentPeopleQuery.cs.
//
// Written against JasperFx's store-agnostic document contracts rather than
// Marten's own types — kanban/completed/jasperfx-shared-store-contracts.md
// WI-2. The store underneath is still Marten; this class no longer names it.

using JasperFx.Events.Documents;
using Soarscore.Application.Queries.CompetitionClasses;

namespace Soarscore.Infrastructure.CompetitionClasses;

public sealed class DocumentClassLibraryQuery(IDocumentSessionFactory sessions) : IClassLibraryQuery
{
    public async Task<ClassDefinitionSummary?> FindByHashAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        await using var session = sessions.QuerySession();
        return await session.Query<ClassDefinitionSummary>().FirstOrDefaultAsync(s => s.ContentHash == contentHash, cancellationToken);
    }

    public async Task<IReadOnlyList<ClassDefinitionSummary>> SearchAsync(string? name, bool activeOnly, CancellationToken cancellationToken = default)
    {
        await using var session = sessions.QuerySession();
        IQueryable<ClassDefinitionSummary> query = session.Query<ClassDefinitionSummary>();

        if (!string.IsNullOrWhiteSpace(name))
        {
            // Case-insensitive by the same decision, and for the same reason, as
            // People/DocumentPeopleQuery.cs's SearchByNameAsync — see the note
            // there (multi-backend-deployment.md WI-5). The two name searches on
            // this system's query ports must not disagree about what a search
            // means.
            query = query.Where(s => s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        if (activeOnly)
        {
            query = query.Where(s => s.RetiredAt == null);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
