// kanban/in-progress/teams-mvp.md WI-6. The WithdrawCompetitor template — all
// validation sits in the decide, whose phase gate
// (addProtectionMember.drawExists) refuses membership edits while any live
// phase exists ("reject the draw first", owner decision 6). Multi-group
// membership is allowed and expected; only a duplicate of THIS group is
// refused.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record AddProtectionGroupMember(
    CompetitionId CompetitionRef,
    CompetitorId CompetitorRef,
    ProtectionGroupId GroupRef) : ICommand<CompetitionId>;

public sealed class AddProtectionGroupMemberHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<AddProtectionGroupMember, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(AddProtectionGroupMember command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = competition.AddProtectionGroupMember(
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
