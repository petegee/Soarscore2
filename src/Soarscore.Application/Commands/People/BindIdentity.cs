// authentication-and-authorisation.md WI-7 (D12). An organiser binds any
// external identity — an interactive provider account, or a client-credentials
// client id — to an existing person: the machine-actor provisioning path
// ("the machine is just a person"), the pre-provisioning path for humans
// (the account exists before its owner ever signs in), and later account
// recovery. The plain RenamePerson read→fold→decide→append template; blank
// provider/subject is the domain decide's refusal (person.identity.blank) —
// the handler does not pre-check, the decide is the single validator.
// Uniqueness across people stays with the store's unique index (WI-8), as
// with every IdentityLinked append.

using Soarscore.Application.Shared.People;
using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Application.Commands.People;

public sealed record BindIdentity(PersonId PersonRef, string Provider, string Subject) : ICommand<PersonId>;

public sealed class BindIdentityHandler(IEventStore eventStore, IClock clock) : ICommandHandler<BindIdentity, PersonId>
{
    public async Task<Result<PersonId>> HandleAsync(BindIdentity command, CancellationToken cancellationToken)
    {
        var loaded = await PersonLoader.LoadAsync(eventStore, command.PersonRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PersonId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (person, version) = loaded.Value;
        var decision = person.LinkIdentity(command.Provider, command.Subject, clock.UtcNow);
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