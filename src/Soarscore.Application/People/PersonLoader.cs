// Shared by every WI-6 handler that mutates or reads a single Person: read
// the stream, fold it, hand back both the aggregate and the version the next
// append must be Exact() against. Not a port — a private helper over the
// IEventStore port, so it stays internal to this project.

using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Application.People;

internal static class PersonLoader
{
    public static async Task<Result<(Person Person, long Version)>> LoadAsync(
        IEventStore eventStore, PersonId id, CancellationToken cancellationToken)
    {
        var read = await eventStore.ReadStreamAsync(id.Value, 0, cancellationToken);
        if (read.IsFailure)
        {
            return Result<(Person, long)>.Failure(read.Code!, read.Message!, read.Defects);
        }

        var events = read.Value;
        if (events.Count == 0)
        {
            return Result<(Person, long)>.Failure("person.notFound", $"No person found with id {id}.");
        }

        var person = events.Aggregate((Person?)null, (current, e) => Person.Apply(current, (PersonEvent)e))!;
        return Result<(Person, long)>.Success((person, events.Count));
    }
}
