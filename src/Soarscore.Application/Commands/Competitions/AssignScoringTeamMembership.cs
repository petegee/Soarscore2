// kanban/in-progress/teams-mvp.md WI-6. The plain WithdrawCompetitor
// read→fold→decide→append template. Naming the SAME team is the
// eligibility-correction path (flips Contributes — the decide refuses a
// different team while a membership exists); no draw gate and no finalisation
// gate — corrections after finalisation stay allowed, the divergence becomes
// visible through the declared-vs-derived read (WI-7).

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record AssignScoringTeamMembership(
    CompetitionId CompetitionRef,
    CompetitorId CompetitorRef,
    ScoringTeamId TeamRef,
    bool Contributes) : ICommand<CompetitionId>;

public sealed class AssignScoringTeamMembershipHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<AssignScoringTeamMembership, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(AssignScoringTeamMembership command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = competition.AssignScoringTeamMembership(
            command.CompetitorRef, command.TeamRef, command.Contributes, clock.UtcNow);
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
