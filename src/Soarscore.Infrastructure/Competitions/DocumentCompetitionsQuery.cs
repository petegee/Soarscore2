// The document-store adapter for ICompetitionsQuery — kanban/completed/create-competition-steel-thread-plan.md
// WI-1. Reads the `competitions` read model only; never the event log.
// Mirrors CompetitionClasses/DocumentClassLibraryQuery.cs.
//
// Written against JasperFx's store-agnostic document contracts rather than
// Marten's own types — kanban/completed/jasperfx-shared-store-contracts.md
// WI-2. The store underneath is still Marten; this class no longer names it.

using JasperFx.Events.Documents;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;

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

    // authentication-and-authorisation.md WI-5 — the capture-policy policy's
    // one read. A real read already (no WI-8 dependency): the summary carries
    // the policy (WI-5's positional append) and CompetitionProjection folds
    // CapturePolicyConfigured onto it, so the store-backed tests in WI-8 need
    // no adapter change — only the event-type map entries.
    //
    // In-memory id filter, DocumentEntryQuery's header finding applied to
    // CompetitionId (strong-typed ids are stored as nested JSON objects, so a
    // server-side Where would duck-type straight onto a bare uuid). The
    // competitions read model is one row per competition ever — the same
    // trade FindByIdsAsync makes for people.
    public async Task<CapturePolicy?> FindCapturePolicyAsync(CompetitionId competitionRef, CancellationToken cancellationToken = default)
    {
        await using var session = sessions.QuerySession();
        var all = await session.Query<CompetitionSummary>().ToListAsync(cancellationToken);
        return all.FirstOrDefault(s => s.Id == competitionRef)?.CapturePolicy;
    }
}
