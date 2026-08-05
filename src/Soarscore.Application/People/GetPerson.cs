// docs/plans/command-side-steel-thread-plan.md WI-6. Served by folding the
// stream, never from the `people` read model — high-level-architecture.md:
// "If querying by ID, then you must use load the stream." This is the one
// query IPeopleQuery deliberately has no method for (IPeopleQuery.cs).

using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Application.People;

public sealed record GetPerson(PersonId Id) : IQuery<Person>;

public sealed class GetPersonHandler(IEventStore eventStore) : IQueryHandler<GetPerson, Person>
{
    public async Task<Result<Person>> HandleAsync(GetPerson query, CancellationToken cancellationToken)
    {
        var loaded = await PersonLoader.LoadAsync(eventStore, query.Id, cancellationToken);
        return loaded.IsFailure
            ? Result<Person>.Failure(loaded.Code!, loaded.Message!, loaded.Defects)
            : Result<Person>.Success(loaded.Value.Person);
    }
}
