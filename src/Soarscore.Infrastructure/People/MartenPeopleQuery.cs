// The Marten adapter for IPeopleQuery — docs/plans/command-side-steel-thread-plan.md
// WI-5/WI-7. Reads the `people` read model only; never the event log. No
// get-by-id here by design — see IPeopleQuery.cs.

using Marten;
using Soarscore.Application.People;

namespace Soarscore.Infrastructure.People;

public sealed class MartenPeopleQuery(IDocumentStore store) : IPeopleQuery
{
    public async Task<PersonSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();
        return await session.Query<PersonSummary>().FirstOrDefaultAsync(p => p.Email == email, cancellationToken);
    }

    public async Task<IReadOnlyList<PersonSummary>> SearchByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();
        return await session.Query<PersonSummary>()
            .Where(p => p.Name.Contains(name))
            .ToListAsync(cancellationToken);
    }
}
