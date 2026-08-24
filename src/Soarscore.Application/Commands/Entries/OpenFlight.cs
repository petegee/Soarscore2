// kanban/completed/capture-a-score-steel-thread-plan.md WI-8. Loads the Entry for
// its state and the Competition (via the Entry's own coordinate — WI-1) for
// the task's resolved MaxLaunches, via TaskResolver. Two loads on the hot
// capture path is accepted, not overlooked (the plan's WI-8: sub-millisecond
// folds at this scale, and caching AdoptedRules outside the log would trade
// a correctness property for an unmeasured benchmark).
//
// Sequence is optional (out-of-order-flight-entry.md decision 4). When the
// caller supplies it, it is the launch label they are recording — "my third
// launch", typed first, is sequence 3 — and it may arrive in any order; gaps
// are legal, duplicates and non-positive values are refused by the decide
// function. When omitted, the handler derives it as max + 1 of the flights
// already present (1 on an empty Entry): max-plus-one, never length-plus-one,
// because once gaps exist length-plus-one can mint a collision ({1, 3} → 3).
// The derivation is a convenience for the scorer-working-down-a-card workflow;
// the decide function remains the real guard.
//
// No LaunchAt either — kanban/completed/remove-flight-launchat.md
// removed it, since no rule wants a launch instant and the classes that care
// about launch timing declare a metric instead. The only timestamp is IClock's.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Commands.Entries;

public sealed record OpenFlight(EntryId EntryRef, int? Sequence = null) : ICommand<EntryId>;

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

        var sequence = command.Sequence ??
            (entry.Flights.Length == 0 ? 1 : entry.Flights.Max(f => f.Sequence) + 1);
        var decision = entry.OpenFlight(sequence, resolvedTask.Value.Timing.MaxLaunches, clock.UtcNow);
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
