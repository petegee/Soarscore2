// docs/plans/command-side-steel-thread-plan.md WI-6. The handler template
// every later mutation copies: read stream → fold to current state → call
// decide → append with ExpectedVersion.Exact(version).

using Soarscore.Application.Shared.People;
using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Application.Commands.People;

public sealed record RenamePerson(PersonId Id, string Name) : ICommand<PersonId>;

public sealed class RenamePersonHandler(IEventStore eventStore, IClock clock) : ICommandHandler<RenamePerson, PersonId>
{
    public async Task<Result<PersonId>> HandleAsync(RenamePerson command, CancellationToken cancellationToken)
    {
        var loaded = await PersonLoader.LoadAsync(eventStore, command.Id, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PersonId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (person, version) = loaded.Value;
        var decision = person.Rename(command.Name, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<PersonId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(command.Id.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<PersonId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<PersonId>.Success(command.Id);
    }
}
