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
//
// authentication-and-authorisation.md WI-5 added the entryRef filter: the
// capture-policy pipeline (CapturePolicyPolicy, D10) resolves one
// entry-scoped command's competition with a single-key lookup. An EntryId is
// globally unique (a version-7 Guid minted per stream), so when entryRef is
// supplied it IS the complete key and competitionRef is NOT applied — the
// pipeline, which does not yet know the entry's competition, passes default
// and reads CompetitionRef off the returned row. The parameter sits after the
// CancellationToken so every existing call site (positional ct included)
// keeps compiling — the additive-only stance; callers using it pass it named.

using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;

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
        CancellationToken cancellationToken = default,
        EntryId? entryRef = null);
}
