// kanban/completed/capture-a-score-steel-thread-plan.md WI-13. No GetEntry query
// exists yet (EntrySummary.cs: "a future work item, mirroring GetCompetition"),
// so a Then step asserting an Entry's full folded state — a flight, a
// measurement, a flight — has no HTTP surface to read it from.
// Reads the raw stream directly and folds it with the public
// Entry.Apply, exactly EntryCaptureEventStoreTests.cs's own LoadEntryAsync
// (WI-12) — the same internal-EntryLoader shape, inlined here because
// EntryLoader is `internal` to Soarscore.Application.

using Soarscore.Application;
using Soarscore.Domain.Entries;

namespace Soarscore.Acceptance.Tests.Support;

public static class EntryReader
{
    public static async Task<Entry> LoadAsync(IEventStore eventStore, EntryId id, CancellationToken cancellationToken)
    {
        var read = await eventStore.ReadStreamAsync(id.Value, 0, cancellationToken);
        if (read.IsFailure)
        {
            throw new InvalidOperationException($"Could not read Entry stream {id}: {read.Code} {read.Message}");
        }

        return read.Value.Aggregate((Entry?)null, (current, e) => Entry.Apply(current, (EntryEvent)e))!;
    }
}
