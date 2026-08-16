// kanban/completed/capture-a-score-steel-thread-plan.md WI-8. Loads the Entry for
// its state and the Competition (via the Entry's own coordinate — WI-1) for
// the task's resolved MaxLaunches, via TaskResolver. Two loads on the hot
// capture path is accepted, not overlooked (the plan's WI-8: sub-millisecond
// folds at this scale, and caching AdoptedRules outside the log would trade
// a correctness property for an unmeasured benchmark).
//
// No Sequence parameter: the handler derives it as Flights.Length + 1, so
// Entry.OpenFlight's contiguity check (WI-3) guards a fold bug, not the
// caller. LaunchAt is caller-supplied — a timekeeper's observed fact, not
// the moment the POST arrived (LADR-0001 §7) — everything else timestamped
// here comes from IClock.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Commands.Entries;

public sealed record OpenFlight(EntryId EntryRef, DateTimeOffset LaunchAt) : ICommand<EntryId>;

public sealed class OpenFlightHandler(IEventStore eventStore, IClock clock) : ICommandHandler<OpenFlight, EntryId>
{
    public async Task<Result<EntryId>> HandleAsync(OpenFlight command, CancellationToken cancellationToken)
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

        var sequence = entry.Flights.Length + 1;
        var decision = entry.OpenFlight(sequence, command.LaunchAt, resolvedTask.Value.Timing.MaxLaunches, clock.UtcNow);
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
