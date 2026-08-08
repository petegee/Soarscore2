// docs/plans/bind-parameter-steel-thread-plan.md WI-4. The plain
// RenamePerson/DrawPhase read->fold->decide->append template — no
// cross-aggregate read, the class definition is already sitting in
// AdoptedRules. The one addition over that template: `By` is validated here,
// not in Competition.BindParameter, because the trust model has no auth
// (CLAUDE.md: club tool, no sign-off, the event log is the audit trail) — By
// is a self-declared CD name, an audit breadcrumb rather than an
// authorisation claim, so its only handler-level obligation is "non-empty",
// checked before the decide function runs.

using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Competitions;

public sealed record BindParameter(
    CompetitionId CompetitionRef,
    string ParameterName,
    MeasuredValue Value,
    string By) : ICommand<CompetitionId>;

public sealed class BindParameterHandler(IEventStore eventStore, IClock clock) : ICommandHandler<BindParameter, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(BindParameter command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        if (string.IsNullOrWhiteSpace(command.By))
        {
            return Result<CompetitionId>.Failure(
                "competition.parameter.byRequired", "By is required — a self-declared CD name, not an authorisation claim.");
        }

        var (competition, version) = loaded.Value;
        var decision = competition.BindParameter(command.ParameterName, command.Value, command.By, clock.UtcNow);
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
