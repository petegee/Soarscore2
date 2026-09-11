// The document-store adapter for IPeopleQuery — kanban/completed/command-side-steel-thread-plan.md
// WI-5/WI-7. Reads the `people` read model only; never the event log. No
// get-by-id here by design — see IPeopleQuery.cs.
//
// Written against JasperFx's store-agnostic document contracts rather than
// Marten's own types — kanban/completed/jasperfx-shared-store-contracts.md
// WI-2. This class names no store at all, and there are now two underneath it.
//
// kanban/completed/multi-backend-deployment.md WI-5 — the name search is the
// one query on this port whose meaning was an accident of the store rather than
// a decision. See SearchByNameAsync.

using JasperFx.Events.Documents;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;

namespace Soarscore.Infrastructure.People;

public sealed class DocumentPeopleQuery(IDocumentSessionFactory sessions) : IPeopleQuery
{
    public async Task<PersonSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var session = sessions.QuerySession();
        return await session.Query<PersonSummary>().FirstOrDefaultAsync(p => p.Email == email, cancellationToken);
    }

    /// <summary>
    /// Case-insensitive substring match on the pilot's name — a decision, and
    /// tested as one (NameSearchTests, which runs against every backend).
    /// </summary>
    /// <remarks>
    /// kanban/completed/multi-backend-deployment.md WI-5. Plain
    /// <c>Contains(name)</c> compiles on every backend and means something
    /// slightly different on each: Postgres <c>LIKE</c> and SQLite <c>instr</c>
    /// are both case- and accent-sensitive, but neither store promises that and
    /// neither is what a secretary typing "lovelace" into a search box means.
    /// Saying <c>OrdinalIgnoreCase</c> out loud makes the behaviour ours rather
    /// than each store's default — Marten compiles it to <c>ILIKE</c> and Fisher
    /// to a lowered <c>instr</c>, and the test asserts they agree.
    /// </remarks>
    public async Task<IReadOnlyList<PersonSummary>> SearchByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var session = sessions.QuerySession();
        return await session.Query<PersonSummary>()
            .Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// In-memory filter, same finding as DocumentEntryQuery's three id-typed
    /// filters: strong-typed ids are stored as nested JSON objects, so a
    /// server-side <c>Where</c> on <see cref="PersonSummary.Id"/> would hit
    /// Marten's bare-scalar duck-typing. The people read model is the whole
    /// club's population — a few dozen rows at this project's scale.
    /// </summary>
    public async Task<IReadOnlyList<PersonSummary>> FindByIdsAsync(IReadOnlyList<PersonId> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var session = sessions.QuerySession();
        var all = await session.Query<PersonSummary>().ToListAsync(cancellationToken);
        return all.Where(p => ids.Contains(p.Id)).ToList();
    }
}
