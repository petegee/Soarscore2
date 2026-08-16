// The document-store adapter for ICompetitionsQuery — kanban/completed/create-competition-steel-thread-plan.md
// WI-1. Reads the `competitions` read model only; never the event log.
// Mirrors CompetitionClasses/DocumentClassLibraryQuery.cs.
//
// Written against JasperFx's store-agnostic document contracts rather than
// Marten's own types — kanban/completed/jasperfx-shared-store-contracts.md
// WI-2. The store underneath is still Marten; this class no longer names it.

using JasperFx.Events.Documents;
using Soarscore.Application.Queries.Competitions;

namespace Soarscore.Infrastructure.Competitions;

public sealed class DocumentCompetitionsQuery(IDocumentSessionFactory sessions) : ICompetitionsQuery
{
    public async Task<IReadOnlyList<CompetitionSummary>> SearchAsync(DateOnly? onOrAfter, string? classContentHash, CancellationToken cancellationToken = default)
    {
        await using var session = sessions.QuerySession();
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
