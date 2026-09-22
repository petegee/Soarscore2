// kanban/completed/command-side-steel-thread-plan.md WI-6. Same handler template as
// RenamePerson.cs — see that file for the rationale.

using Soarscore.Application.Auth;
using Soarscore.Application.Shared.People;
using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Application.Commands.People;

// ISelfPersonCommand — same marker bridge as RenamePerson.cs.
public sealed record ChangePersonClubAffiliation(PersonId Id, ClubAffiliation? Club) : ICommand<PersonId>, ISelfPersonCommand
{
    PersonId ISelfPersonCommand.PersonRef => Id;
}

public sealed class ChangePersonClubAffiliationHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<ChangePersonClubAffiliation, PersonId>
{
    public async Task<Result<PersonId>> HandleAsync(ChangePersonClubAffiliation command, CancellationToken cancellationToken)
    {
        var loaded = await PersonLoader.LoadAsync(eventStore, command.Id, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PersonId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (person, version) = loaded.Value;
        var decision = person.ChangeClubAffiliation(command.Club, clock.UtcNow);
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
