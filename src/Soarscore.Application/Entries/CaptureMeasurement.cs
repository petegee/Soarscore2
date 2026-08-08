// docs/plans/capture-a-score-steel-thread-plan.md WI-8. The highest-volume
// write in the system. Loads the Entry for its state and the Competition
// (via the Entry's own coordinate — WI-1) for the task's declared Metrics,
// via TaskResolver — same shape as OpenFlightHandler. CapturedAt comes from
// IClock, never the caller; Metric/Value are the timekeeper's raw
// observation, validated and rounded by Entry.CaptureMeasurement (WI-4).

using Soarscore.Application.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Entries;

public sealed record CaptureMeasurement(
    EntryId EntryRef, int FlightSequence, string Metric, MeasuredValue Value) : ICommand<EntryId>;

public sealed class CaptureMeasurementHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<CaptureMeasurement, EntryId>
{
    public async Task<Result<EntryId>> HandleAsync(CaptureMeasurement command, CancellationToken cancellationToken)
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

        var decision = entry.CaptureMeasurement(
            command.FlightSequence, command.Metric, command.Value, clock.UtcNow, resolvedTask.Value.Metrics);
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
