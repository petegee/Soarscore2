// kanban/completed/command-side-steel-thread-plan.md WI-6. Resolves through
// IPeopleQuery (WI-5) — the cross-stream lookups the `people` read model
// exists for. Not GetPerson: that goes through IEventStore and folds
// (GetPerson.cs) per high-level-architecture.md's "querying by ID loads the
// stream" rule.
//
// A record struct, not a record class — see GetPerson.cs: WI-8 binds this
// directly from the query string via [AsParameters], no separate Api-layer DTO.

using Soarscore.Domain;

namespace Soarscore.Application.Queries.People;

public readonly record struct FindPeople(string? Email, string? Name) : IQuery<IReadOnlyList<PersonSummary>>;

public sealed class FindPeopleHandler(IPeopleQuery peopleQuery) : IQueryHandler<FindPeople, IReadOnlyList<PersonSummary>>
{
    public async Task<Result<IReadOnlyList<PersonSummary>>> HandleAsync(FindPeople query, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            var match = await peopleQuery.FindByEmailAsync(query.Email, cancellationToken);
            return Result<IReadOnlyList<PersonSummary>>.Success(match is null ? [] : [match]);
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var matches = await peopleQuery.SearchByNameAsync(query.Name, cancellationToken);
            return Result<IReadOnlyList<PersonSummary>>.Success(matches);
        }

        return Result<IReadOnlyList<PersonSummary>>.Failure("findPeople.noCriteria", "Provide an email or a name to search by.");
    }
}
