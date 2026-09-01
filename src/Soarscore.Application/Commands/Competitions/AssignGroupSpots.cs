// kanban/in-progress/lane-assignment.md WI-3. The command that makes a
// group's field spots explicit data: the CD (or a consuming setup UI)
// assigns — or re-assigns — the complete spot mapping for one group of a
// task-round. Whole-replacement semantics (story decision D3): the payload
// is the complete mapping and replaces whatever was there; until assigned a
// group simply reads unassigned (D2).
//
// The plain CompetitionLoader.LoadAsync -> decide -> AppendAsync walk at
// ExpectedVersion.Exact (the AcceptDraw.cs template): no cross-aggregate
// fact is needed at assignment time (finding 9) — the decide function
// re-derives the group's live membership from the fold itself, so this
// handler needs no port beyond IEventStore/IClock.
//
// The command returns the GroupId it named — the caller already knows it,
// but the shape keeps the competition-command family uniform
// (AppendReflightGroup, ICommand<GroupId>).

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

/// <summary>
/// Assigns (or replaces) the complete field-spot mapping for one group of a
/// task-round. <see cref="Spots"/> must cover every live member of the group
/// (drawn ∧ not withdrawn) with distinct positive spots — the decide function
/// (<see cref="Competition.AssignGroupSpots"/>) validates against the fold,
/// never the caller's word; success stores the spots as given.
/// </summary>
public sealed record AssignGroupSpots(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    GroupId GroupRef,
    IReadOnlyList<GroupSpot> Spots) : ICommand<GroupId>;

public sealed class AssignGroupSpotsHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<AssignGroupSpots, GroupId>
{
    public async Task<Result<GroupId>> HandleAsync(
        AssignGroupSpots command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<GroupId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = competition.AssignGroupSpots(
            command.PhaseOrdinal,
            command.RoundOrdinal,
            command.TaskRoundOrdinal,
            command.GroupRef,
            command.Spots,
            clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<GroupId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(
            command.CompetitionRef.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        if (append.IsFailure)
        {
            return Result<GroupId>.Failure(append.Code!, append.Message!, append.Defects);
        }

        return Result<GroupId>.Success(decision.Value.GroupRef);
    }
}
