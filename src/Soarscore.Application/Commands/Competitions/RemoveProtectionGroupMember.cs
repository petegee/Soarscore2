// kanban/in-progress/teams-mvp.md WI-6. The AddProtectionGroupMember template —
// the decide's phase gate (removeProtectionMember.drawExists) is the same
// "reject the draw first" rule, and the membership-must-exist check sits there
// too.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record RemoveProtectionGroupMember(
    CompetitionId CompetitionRef,
    CompetitorId CompetitorRef,
    ProtectionGroupId GroupRef) : ICommand<CompetitionId>;

public sealed class RemoveProtectionGroupMemberHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<RemoveProtectionGroupMember, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(RemoveProtectionGroupMember command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = competition.RemoveProtectionGroupMember(
            command.CompetitorRef, command.GroupRef, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<CompetitionId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(
            command.CompetitionRef.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<CompetitionId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<CompetitionId>.Success(command.CompetitionRef);
    }
}
