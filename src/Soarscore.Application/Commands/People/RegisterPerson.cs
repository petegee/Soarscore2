// kanban/completed/command-side-steel-thread-plan.md WI-6. Mints the PersonId
// (Guid.CreateVersion7() — PersonId.New(), LADR-0001 §4.9) and appends with
// ExpectedVersion.NoStream, the shape of every aggregate's first append.
//
// Duplicate email is deliberately not pre-checked: the unique index in the
// Inline `people` projection (WI-7) is the sole arbiter, and the Marten
// adapter translates the PostgreSQL violation back into a Result failure.
// Pre-checking here would be read-check-write, which LADR-0001 §4.4 forbids
// and which is racy under MVCC regardless.

using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Application.Commands.People;

public sealed record RegisterPerson(string Name, ContactDetails Contact, ClubAffiliation? Club) : ICommand<PersonId>;

public sealed class RegisterPersonHandler(IEventStore eventStore, IClock clock) : ICommandHandler<RegisterPerson, PersonId>
{
    public async Task<Result<PersonId>> HandleAsync(RegisterPerson command, CancellationToken cancellationToken)
    {
        var id = PersonId.New();

        var decision = Person.Register(id, command.Name, command.Contact, command.Club, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<PersonId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(id.Value, ExpectedVersion.NoStream, [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<PersonId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<PersonId>.Success(id);
    }
}
