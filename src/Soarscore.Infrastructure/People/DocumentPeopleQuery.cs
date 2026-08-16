// The document-store adapter for IPeopleQuery — kanban/completed/command-side-steel-thread-plan.md
// WI-5/WI-7. Reads the `people` read model only; never the event log. No
// get-by-id here by design — see IPeopleQuery.cs.
//
// Written against JasperFx's store-agnostic document contracts rather than
// Marten's own types — kanban/completed/jasperfx-shared-store-contracts.md
// WI-2. The store underneath is still Marten; this class no longer names it.

using JasperFx.Events.Documents;
using Soarscore.Application.Queries.People;

namespace Soarscore.Infrastructure.People;

public sealed class DocumentPeopleQuery(IDocumentSessionFactory sessions) : IPeopleQuery
{
    public async Task<PersonSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var session = sessions.QuerySession();
        return await session.Query<PersonSummary>().FirstOrDefaultAsync(p => p.Email == email, cancellationToken);
    }

    public async Task<IReadOnlyList<PersonSummary>> SearchByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var session = sessions.QuerySession();
        return await session.Query<PersonSummary>()
            .Where(p => p.Name.Contains(name))
            .ToListAsync(cancellationToken);
    }
}
