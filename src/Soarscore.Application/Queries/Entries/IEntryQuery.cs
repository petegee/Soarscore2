// The `entry_index` read model's query port — kanban/completed/capture-a-score-steel-thread-plan.md
// WI-7, LADR-0001 §4.2. Defined here, implemented in Soarscore.Infrastructure
// against Marten; IDocumentSession never appears above that project. Mirrors
// People/IPeopleQuery.cs and Competitions/ICompetitionsQuery.cs.
//
// No IQueryable (LADR-0001 §4.2) — one method taking every filter EntrySummary
// can be sliced by, all optional so a caller can narrow from "everything in
// this competition" down to "this one competitor's entry in this task-round".
//
// Deliberately no get-by-id method: high-level-architecture.md is explicit
// that querying by ID folds the stream, which is EntryLoader's (WI-6) job,
// not this interface's.

using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Queries.Entries;

public interface IEntryQuery
{
    Task<IReadOnlyList<EntrySummary>> FindAsync(
        CompetitionId competitionRef,
        int? phaseOrdinal,
        int? roundOrdinal,
        int? taskRoundOrdinal,
        GroupId? groupRef,
        CompetitorId? competitorRef,
        CancellationToken cancellationToken = default);
}
