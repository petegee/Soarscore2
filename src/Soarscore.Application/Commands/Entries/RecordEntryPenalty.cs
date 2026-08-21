// kanban/in-progress/annul-and-penalise-the-second-entry-thread.md WI-7. The
// Entry-scoped penalty command: the two-load shape from AmendMeasurementHandler
// — the Entry for its state, the Competition (via the Entry's own coordinate)
// for the adopted class's declared penalties. The Penalty is constructed here
// with CompetitorRef/TaskRound null: the Entry is its own subject and
// coordinate, and the decide function rejects any other shape. No TaskResolver —
// penalties are task-agnostic, and no IClock — Penalty carries no At (decision 2).

using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Commands.Entries;

public sealed record RecordEntryPenalty(
    EntryId EntryRef, string InfractionType, PenaltyScope Scope, string? By) : ICommand<EntryId>;

public sealed class RecordEntryPenaltyHandler(IEventStore eventStore)
    : ICommandHandler<RecordEntryPenalty, EntryId>
{
    public async Task<Result<EntryId>> HandleAsync(RecordEntryPenalty command, CancellationToken cancellationToken)
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

        var penalty = new Penalty
        {
            InfractionType = command.InfractionType,
            Scope = command.Scope,
            By = command.By,
        };

        var decision = entry.RecordPenalty(penalty, competition.AdoptedRules.Definition.Penalties);
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
