// authentication-and-authorisation.md WI-7. The plain RenamePerson
// read→fold→decide→append template, plus one guard: revoking the last
// Organiser is refused with person.lastOrganiser, because it would strand
// the system's authority — nobody left able to grant the role back.
//
// That guard is a CROSS-STREAM READ guarding a UX deadlock, not an aggregate
// invariant — Competition cannot answer "how many organisers are there" any
// more than Person can (the BindParameter roundHasEntries precedent: a fact
// the aggregate cannot answer for itself, resolved by the handler and passed
// alongside the decide). The read-model count is deliberately race-tolerant:
// two concurrent revokes of the last two organisers can both read count 2 and
// both succeed, and a concurrent grant can push the count back up after the
// read. Accepted and documented (tech-debt.md, WI-11) — the alternative, a
// read-check-write arbiter, is exactly what LADR-0001 §4.4 forbids.
//
// The decide runs first: a role the person does not hold refuses in the
// decide (person.roleNotHeld) and never reaches the guard, so lastOrganiser
// means exactly "revoking Organiser from a holder, and they are the only one".

using Soarscore.Application.Queries.People;
using Soarscore.Application.Shared.People;
using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Application.Commands.People;

public sealed record RevokeRole(PersonId PersonRef, PersonRole Role) : ICommand<PersonId>;

public sealed class RevokeRoleHandler(IEventStore eventStore, IPeopleQuery peopleQuery, IClock clock)
    : ICommandHandler<RevokeRole, PersonId>
{
    public async Task<Result<PersonId>> HandleAsync(RevokeRole command, CancellationToken cancellationToken)
    {
        var loaded = await PersonLoader.LoadAsync(eventStore, command.PersonRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PersonId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (person, version) = loaded.Value;
        var decision = person.RevokeRole(command.Role, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<PersonId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        // See the header: cross-stream read, UX-deadlock guard, not an
        // aggregate invariant; race-tolerant by design. Only for an Organiser
        // revoke the decide has already accepted — other roles never pay for
        // the lookup.
        if (command.Role == PersonRole.Organiser &&
            await peopleQuery.CountByRoleAsync(PersonRole.Organiser, cancellationToken) == 1)
        {
            return Result<PersonId>.Failure(
                "person.lastOrganiser",
                "Refusing to revoke the only Organiser — no one would be left able to grant roles back.");
        }

        var append = await eventStore.AppendAsync(
            command.PersonRef.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<PersonId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<PersonId>.Success(command.PersonRef);
    }
}