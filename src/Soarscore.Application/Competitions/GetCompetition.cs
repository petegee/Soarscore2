// docs/plans/create-competition-steel-thread-plan.md WI-3. Served by folding
// the stream, never from the `competitions` read model —
// high-level-architecture.md: "If querying by ID, then you must use load the
// stream." This is the one query ICompetitionsQuery deliberately has no
// method for (ICompetitionsQuery.cs). Mirrors People/GetPerson.cs.

using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Competitions;

public readonly record struct GetCompetition(CompetitionId Id) : IQuery<Competition>;

public sealed class GetCompetitionHandler(IEventStore eventStore) : IQueryHandler<GetCompetition, Competition>
{
    public async Task<Result<Competition>> HandleAsync(GetCompetition query, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, query.Id, cancellationToken);
        return loaded.IsFailure
            ? Result<Competition>.Failure(loaded.Code!, loaded.Message!, loaded.Defects)
            : Result<Competition>.Success(loaded.Value.Competition);
    }
}
