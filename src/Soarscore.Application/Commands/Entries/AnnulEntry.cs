// kanban/in-progress/annul-and-penalise-the-second-entry-thread.md WI-7. The
// Entry-annul command: the plain AmendMeasurement template — EntryLoader for
// the Entry's state, decide, append at ExpectedVersion.Exact — but with one
// load, not two: an annulment is a ruling with no class-definition cost to
// validate against, so no Competition read is needed. At comes from IClock,
// never the caller — the same rule capture and amend hold, and the reason
// MeasurementDigest's latest-by-At ordering can be trusted.

using Soarscore.Application.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Commands.Entries;

public sealed record AnnulEntry(EntryId EntryRef, string Reason, string By) : ICommand<EntryId>;

public sealed class AnnulEntryHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<AnnulEntry, EntryId>
{
    public async Task<Result<EntryId>> HandleAsync(AnnulEntry command, CancellationToken cancellationToken)
    {
        var loadedEntry = await EntryLoader.LoadAsync(eventStore, command.EntryRef, cancellationToken);
        if (loadedEntry.IsFailure)
        {
            return Result<EntryId>.Failure(loadedEntry.Code!, loadedEntry.Message!, loadedEntry.Defects);
        }

        var (entry, version) = loadedEntry.Value;

        var decision = entry.AnnulEntry(command.Reason, command.By, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<EntryId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(
            command.EntryRef.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<EntryId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<EntryId>.Success(command.EntryRef);
    }
}
