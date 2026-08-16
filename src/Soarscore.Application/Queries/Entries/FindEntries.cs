// kanban/completed/capture-a-score-steel-thread-plan.md WI-7. Resolves through
// IEntryQuery — the cross-stream lookup the `entry_index` read model exists
// for. Not GetEntry: that would go through IEventStore and fold (EntryLoader.cs),
// mirroring Competitions/FindCompetitions.cs and People/FindPeople.cs.
//
// Every filter is optional except CompetitionRef: unlike FindCompetitions
// (which lists across every competition), an entry_index lookup with no
// competition to scope it to would mean "every Entry in the system", which no
// caller of this thread's endpoints ever wants and IEntryQuery.FindAsync does
// not offer.

using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Queries.Entries;

public readonly record struct FindEntries(
    CompetitionId CompetitionRef,
    int? PhaseOrdinal,
    int? RoundOrdinal,
    int? TaskRoundOrdinal,
    GroupId? GroupRef,
    CompetitorId? CompetitorRef) : IQuery<IReadOnlyList<EntrySummary>>;

public sealed class FindEntriesHandler(IEntryQuery entryQuery) : IQueryHandler<FindEntries, IReadOnlyList<EntrySummary>>
{
    public async Task<Result<IReadOnlyList<EntrySummary>>> HandleAsync(FindEntries query, CancellationToken cancellationToken)
    {
        var matches = await entryQuery.FindAsync(
            query.CompetitionRef,
            query.PhaseOrdinal,
            query.RoundOrdinal,
            query.TaskRoundOrdinal,
            query.GroupRef,
            query.CompetitorRef,
            cancellationToken);

        return Result<IReadOnlyList<EntrySummary>>.Success(matches);
    }
}
