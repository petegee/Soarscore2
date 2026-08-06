// docs/plans/create-competition-steel-thread-plan.md WI-3. Resolves through
// ICompetitionsQuery (WI-1) — the cross-stream listing/filtering the
// `competitions` read model exists for. Not GetCompetition: that goes through
// IEventStore and folds (GetCompetition.cs).
//
// Unlike FindPeople, both filters are optional and neither is required — the
// plan states no "must supply at least one criterion" rule for this query, so
// none is invented here.

using Soarscore.Domain;

namespace Soarscore.Application.Competitions;

public readonly record struct FindCompetitions(DateOnly? OnOrAfter, string? ClassContentHash) : IQuery<IReadOnlyList<CompetitionSummary>>;

public sealed class FindCompetitionsHandler(ICompetitionsQuery competitionsQuery)
    : IQueryHandler<FindCompetitions, IReadOnlyList<CompetitionSummary>>
{
    public async Task<Result<IReadOnlyList<CompetitionSummary>>> HandleAsync(FindCompetitions query, CancellationToken cancellationToken)
    {
        var matches = await competitionsQuery.SearchAsync(query.OnOrAfter, query.ClassContentHash, cancellationToken);
        return Result<IReadOnlyList<CompetitionSummary>>.Success(matches);
    }
}
