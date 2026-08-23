// kanban/in-progress/reflight-groups.md WI-4. The command that makes a
// reflight group a first-class, recorded act — the last-but-one
// CompetitionEvent (after RulesAmended) to be unreachable.
//
// Mirrors the task-round lifecycle commands' shape (TaskRoundLifecycle.cs):
// CompetitionLoader.LoadAsync -> decide -> AppendAsync at
// ExpectedVersion.Exact. `Reason` is NOT validated here — it is a substantive
// record of an entitlement ruling, so the decide function owns it
// (Competition.AppendReflightGroup's doc comment).
//
// The command returns the minted GroupId so the caller can name the group's
// entries against it in the same breath (the reflight entries are opened
// against the emitted group, whose id only the append knows).

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

/// <summary>
/// Appends a new reflight group to an existing task-round. <see cref="Members"/>
/// are the fillers and entitled competitor(s) that actually re-flew — supplied
/// explicitly by the CD (planner's call: the random draw is a convenience
/// feature, not a rule this thread automates). <see cref="Reason"/> records the
/// entitlement basis (collision, hindrance, timing failure — F5J 5.5.11.6 b).
/// </summary>
public sealed record AppendReflightGroup(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    IReadOnlyList<CompetitorId> Members,
    string Reason) : ICommand<GroupId>;

public sealed class AppendReflightGroupHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<AppendReflightGroup, GroupId>
{
    public async Task<Result<GroupId>> HandleAsync(
        AppendReflightGroup command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<GroupId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = competition.AppendReflightGroup(
            command.PhaseOrdinal,
            command.RoundOrdinal,
            command.TaskRoundOrdinal,
            command.Members.ToImmutableArray(),
            command.Reason,
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

        return Result<GroupId>.Success(decision.Value.Group.Id);
    }
}