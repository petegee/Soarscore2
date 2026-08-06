// docs/plans/register-competitor-steel-thread-plan.md WI-3. The plain
// RenamePerson read→fold→decide→append template — no cross-aggregate read,
// unlike RegisterCompetitor.cs: withdrawal addresses a CompetitorId that, by
// construction, is already in the field.

using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Competitions;

public sealed record WithdrawCompetitor(CompetitionId CompetitionId, CompetitorId CompetitorId) : ICommand<CompetitorId>;

public sealed class WithdrawCompetitorHandler(IEventStore eventStore, IClock clock) : ICommandHandler<WithdrawCompetitor, CompetitorId>
{
    public async Task<Result<CompetitorId>> HandleAsync(WithdrawCompetitor command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitorId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = competition.WithdrawCompetitor(command.CompetitorId, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<CompetitorId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(
            command.CompetitionId.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<CompetitorId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<CompetitorId>.Success(command.CompetitorId);
    }
}
