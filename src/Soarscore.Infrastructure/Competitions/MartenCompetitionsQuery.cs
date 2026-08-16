// The Marten adapter for ICompetitionsQuery — kanban/completed/create-competition-steel-thread-plan.md
// WI-1. Reads the `competitions` read model only; never the event log.
// Mirrors CompetitionClasses/MartenClassLibraryQuery.cs.
//
// Not registered in DI yet — that wiring is WI-4's job (this file compiles
// standalone until then).

using Marten;
using Soarscore.Application.Queries.Competitions;

namespace Soarscore.Infrastructure.Competitions;

public sealed class MartenCompetitionsQuery(IDocumentStore store) : ICompetitionsQuery
{
    public async Task<IReadOnlyList<CompetitionSummary>> SearchAsync(DateOnly? onOrAfter, string? classContentHash, CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();
        IQueryable<CompetitionSummary> query = session.Query<CompetitionSummary>();

        if (onOrAfter is not null)
        {
            query = query.Where(s => s.StartDate >= onOrAfter.Value);
        }

        if (!string.IsNullOrWhiteSpace(classContentHash))
        {
            query = query.Where(s => s.ClassContentHash == classContentHash);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
