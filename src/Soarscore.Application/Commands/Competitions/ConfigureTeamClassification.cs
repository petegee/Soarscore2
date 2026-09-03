// kanban/in-progress/teams-mvp.md WI-6. The WithdrawCompetitor template. The
// By check is the decide's (configureTeamClassification.byBlank), not the
// handler's: unlike BindParameter/PrescribeDraw, whose By checks are
// load-bearing before the decide runs, here By is purely the audit breadcrumb
// of who declared the policy, and the decide already owns the only check on
// it. Reconfiguration is allowed — last-wins, the log is the audit trail
// (ParameterBindings precedent).

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record ConfigureTeamClassification(CompetitionId CompetitionRef, bool Enabled, string By)
    : ICommand<CompetitionId>;

public sealed class ConfigureTeamClassificationHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<ConfigureTeamClassification, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(ConfigureTeamClassification command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = competition.ConfigureTeamClassification(command.Enabled, command.By, clock.UtcNow);
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
