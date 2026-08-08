// The Marten adapter for IEntryQuery — docs/plans/capture-a-score-steel-thread-plan.md
// WI-9. Reads the `entry_index` read model only; never the event log. Mirrors
// Competitions/MartenCompetitionsQuery.cs.

using Marten;
using Soarscore.Application.Entries;
using Soarscore.Domain.Competitions;

namespace Soarscore.Infrastructure.Entries;

public sealed class MartenEntryQuery(IDocumentStore store) : IEntryQuery
{
    public async Task<IReadOnlyList<EntrySummary>> FindAsync(
        CompetitionId competitionRef,
        int? phaseOrdinal,
        int? roundOrdinal,
        int? taskRoundOrdinal,
        GroupId? groupRef,
        CompetitorId? competitorRef,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();
        IQueryable<EntrySummary> query = session.Query<EntrySummary>()
            .Where(s => s.CompetitionRef == competitionRef);

        if (phaseOrdinal is not null)
        {
            query = query.Where(s => s.PhaseOrdinal == phaseOrdinal.Value);
        }

        if (roundOrdinal is not null)
        {
            query = query.Where(s => s.RoundOrdinal == roundOrdinal.Value);
        }

        if (taskRoundOrdinal is not null)
        {
            query = query.Where(s => s.TaskRoundOrdinal == taskRoundOrdinal.Value);
        }

        if (groupRef is not null)
        {
            query = query.Where(s => s.GroupRef == groupRef.Value);
        }

        if (competitorRef is not null)
        {
            query = query.Where(s => s.CompetitorRef == competitorRef.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
