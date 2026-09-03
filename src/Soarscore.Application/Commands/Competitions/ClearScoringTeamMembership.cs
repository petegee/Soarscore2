// kanban/in-progress/teams-mvp.md WI-6. The WithdrawCompetitor template —
// clears whatever scoring-team membership the competitor holds, whatever team
// it named (the decide refuses a clear with nothing to clear). This is the
// explicit correction that takes a withdrawn member out of their team's
// classification, and the path that frees a competitor to join a different
// team.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record ClearScoringTeamMembership(CompetitionId CompetitionRef, CompetitorId CompetitorRef)
    : ICommand<CompetitionId>;

public sealed class ClearScoringTeamMembershipHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<ClearScoringTeamMembership, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(ClearScoringTeamMembership command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = competition.ClearScoringTeamMembership(command.CompetitorRef, clock.UtcNow);
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
