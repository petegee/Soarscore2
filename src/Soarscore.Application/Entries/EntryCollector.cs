// EntryCollector — docs/plans/scoring-steel-thread-plan.md WI-6.
//
// Assembles every Entry in a competition for the scoring queries: fan out
// through IEntryQuery.FindAsync (the entry_index read model — exactly the job
// it was built for, "which Entry streams exist where") to learn which Entry
// streams exist, then fold each one via EntryLoader.LoadAsync.
//
// Reading every stream is correct here, not lazy: LADR-0001 §3 forbids
// projecting scores, and at this project's stated scale — ≤20 pilots × ≤8
// rounds — the worst case is ~160 short streams.

using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Entries;

internal static class EntryCollector
{
    public static async Task<Result<IReadOnlyDictionary<EntryId, Entry>>> CollectAsync(
        IEventStore eventStore,
        IEntryQuery entryQuery,
        CompetitionId competitionRef,
        CancellationToken cancellationToken)
    {
        var summaries = await entryQuery.FindAsync(
            competitionRef,
            phaseOrdinal: null,
            roundOrdinal: null,
            taskRoundOrdinal: null,
            groupRef: null,
            competitorRef: null,
            cancellationToken);

        var entries = new Dictionary<EntryId, Entry>(summaries.Count);

        foreach (var summary in summaries)
        {
            var loaded = await EntryLoader.LoadAsync(eventStore, summary.Id, cancellationToken);
            if (loaded.IsFailure)
            {
                return Result<IReadOnlyDictionary<EntryId, Entry>>.Failure(
                    loaded.Code!, loaded.Message!, loaded.Defects);
            }

            entries[summary.Id] = loaded.Value.Entry;
        }

        return Result<IReadOnlyDictionary<EntryId, Entry>>.Success(entries);
    }
}
