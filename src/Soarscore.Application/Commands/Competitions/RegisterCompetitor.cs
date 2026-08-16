// docs/plans/register-competitor-steel-thread-plan.md WI-3. The RenamePerson
// read→fold→decide→append template, plus one cross-aggregate read — the same
// shape CreateCompetition.cs already established for confirming a referenced
// aggregate exists before deciding. Here it confirms the PersonId being
// registered is real; CreateCompetition confirms the referenced class
// definition is real. Both wrap the cross-aggregate lookup in a failure code
// of their own rather than propagating the child loader's code, because the
// check being made ("does this person exist, for the purpose of registering
// them here") is this handler's concern, not PersonLoader's.

using Soarscore.Application.Shared.People;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;

namespace Soarscore.Application.Commands.Competitions;

public sealed record RegisterCompetitor(CompetitionId CompetitionId, PersonId PersonId) : ICommand<CompetitorId>;

public sealed class RegisterCompetitorHandler(IEventStore eventStore, IClock clock) : ICommandHandler<RegisterCompetitor, CompetitorId>
{
    public async Task<Result<CompetitorId>> HandleAsync(RegisterCompetitor command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitorId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;

        // Cross-aggregate read, not a concurrency arbiter (LADR-0001 §4.4
        // forbids only the latter) — the residual race this accepts is a
        // person who does not exist yet, which this check already rejects;
        // Person has no delete (Person.cs), so nothing can go stale the other
        // way.
        var personLoaded = await PersonLoader.LoadAsync(eventStore, command.PersonId, cancellationToken);
        if (personLoaded.IsFailure)
        {
            return Result<CompetitorId>.Failure(
                "registerCompetitor.personNotFound", $"No person found with id {command.PersonId}.");
        }

        var id = CompetitorId.New();
        var decision = competition.RegisterCompetitor(id, command.PersonId, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<CompetitorId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(
            command.CompetitionId.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<CompetitorId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<CompetitorId>.Success(id);
    }
}
