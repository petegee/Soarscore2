// kanban/completed/amend-a-measurement.md WI-3. The correcting counterpart to
// CaptureMeasurementHandler (WI-8 of the capture plan): loads the Entry for its
// state and the Competition (via the Entry's own coordinate — WI-1) for the
// task's declared Metrics, via TaskResolver — the same two-load shape as
// CaptureMeasurement. At comes from IClock, never the caller — the same rule
// capture holds, and the reason MeasurementDigest's latest-by-At ordering can
// be trusted. Reason and By are the corrector's substantive record of the
// change (decided by the user 2026-08-18, amend-a-measurement.md decision 1):
// recorded, enforced on nobody's role.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Commands.Entries;

public sealed record AmendMeasurement(
    EntryId EntryRef, int FlightSequence, string Metric, MeasuredValue NewValue, string Reason, string By) : ICommand<EntryId>;

public sealed class AmendMeasurementHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<AmendMeasurement, EntryId>
{
    public async Task<Result<EntryId>> HandleAsync(AmendMeasurement command, CancellationToken cancellationToken)
    {
        var loadedEntry = await EntryLoader.LoadAsync(eventStore, command.EntryRef, cancellationToken);
        if (loadedEntry.IsFailure)
        {
            return Result<EntryId>.Failure(loadedEntry.Code!, loadedEntry.Message!, loadedEntry.Defects);
        }

        var (entry, version) = loadedEntry.Value;

        var loadedCompetition = await CompetitionLoader.LoadAsync(eventStore, entry.CompetitionRef, cancellationToken);
        if (loadedCompetition.IsFailure)
        {
            return Result<EntryId>.Failure(loadedCompetition.Code!, loadedCompetition.Message!, loadedCompetition.Defects);
        }

        var (competition, _) = loadedCompetition.Value;

        var resolvedTask = TaskResolver.Resolve(competition, entry.PhaseOrdinal, entry.RoundOrdinal, entry.TaskRoundOrdinal);
        if (resolvedTask.IsFailure)
        {
            return Result<EntryId>.Failure(resolvedTask.Code!, resolvedTask.Message!, resolvedTask.Defects);
        }

        var decision = entry.AmendMeasurement(
            command.FlightSequence, command.Metric, command.NewValue, command.Reason, command.By, clock.UtcNow, resolvedTask.Value.Metrics);
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