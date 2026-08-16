// Shared by every WI-8 handler that mutates or reads a single Entry: read the
// stream, fold it, hand back both the aggregate and the version the next
// append must be Exact() against. Not a port — a private helper over the
// IEventStore port, so it stays internal to this project. Mirrors
// Competitions/CompetitionLoader.cs — Entry's fold, like Competition's,
// requires a non-null current after EntryOpened (Entry.cs's Require helper
// throws on any other event folded with no current), so this follows
// CompetitionLoader's pattern rather than ClassDefinitionLoader's.

using Soarscore.Domain;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Shared.Entries;

internal static class EntryLoader
{
    public static async Task<Result<(Entry Entry, long Version)>> LoadAsync(
        IEventStore eventStore, EntryId id, CancellationToken cancellationToken)
    {
        var read = await eventStore.ReadStreamAsync(id.Value, 0, cancellationToken);
        if (read.IsFailure)
        {
            return Result<(Entry, long)>.Failure(read.Code!, read.Message!, read.Defects);
        }

        var events = read.Value;
        if (events.Count == 0)
        {
            return Result<(Entry, long)>.Failure("entry.notFound", $"No entry found with id {id}.");
        }

        var entry = events.Aggregate((Entry?)null, (current, e) => Entry.Apply(current, (EntryEvent)e))!;
        return Result<(Entry, long)>.Success((entry, events.Count));
    }
}
