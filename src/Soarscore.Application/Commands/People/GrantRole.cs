// authentication-and-authorisation.md WI-7. The plain RenamePerson
// read→fold→decide→append template: GrantRole's decide (WI-3) refuses a role
// the person already holds (person.roleAlreadyHeld); the handler only loads,
// decides and appends — no role logic lives here. D3's bootstrap grant is
// LinkSignIn's business, deliberately not this command's: an organiser
// granting a held role is a mistake worth surfacing, while a re-sign-in must
// stay silent.

using Soarscore.Application.Shared.People;
using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Application.Commands.People;

public sealed record GrantRole(PersonId PersonRef, PersonRole Role) : ICommand<PersonId>;

public sealed class GrantRoleHandler(IEventStore eventStore, IClock clock) : ICommandHandler<GrantRole, PersonId>
{
    public async Task<Result<PersonId>> HandleAsync(GrantRole command, CancellationToken cancellationToken)
    {
        var loaded = await PersonLoader.LoadAsync(eventStore, command.PersonRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PersonId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (person, version) = loaded.Value;
        var decision = person.GrantRole(command.Role, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<PersonId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(
            command.PersonRef.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<PersonId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<PersonId>.Success(command.PersonRef);
    }
}