// kanban/in-progress/teams-mvp.md WI-6. The DefineScoringTeam template — the
// handler mints the ProtectionGroupId and the decide owns the name checks.
// Protection groups are a draw-only concept, so unlike AddProtectionGroupMember
// this command has no phase gate either: defining a named group before any
// draw exists is the normal sequence.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record DefineProtectionGroup(CompetitionId CompetitionRef, string Name) : ICommand<ProtectionGroupId>;

public sealed class DefineProtectionGroupHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<DefineProtectionGroup, ProtectionGroupId>
{
    public async Task<Result<ProtectionGroupId>> HandleAsync(DefineProtectionGroup command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<ProtectionGroupId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;

        var id = ProtectionGroupId.New();
        var decision = competition.DefineProtectionGroup(id, command.Name, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<ProtectionGroupId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(
            command.CompetitionRef.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<ProtectionGroupId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<ProtectionGroupId>.Success(id);
    }
}
