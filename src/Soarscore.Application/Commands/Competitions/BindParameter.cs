// kanban/completed/bind-parameter-steel-thread-plan.md WI-4. The plain
// RenamePerson/DrawPhase read->fold->decide->append template — no
// cross-aggregate read, the class definition is already sitting in
// AdoptedRules. The one addition over that template: `By` is validated here,
// not in Competition.BindParameter, because the trust model has no auth
// (CLAUDE.md: club tool, no sign-off, the event log is the audit trail) — By
// is a self-declared CD name, an audit breadcrumb rather than an
// authorisation claim, so its only handler-level obligation is "non-empty",
// checked before the decide function runs.

using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Commands.Competitions;

public sealed record BindParameter(
    CompetitionId CompetitionRef,
    string ParameterName,
    MeasuredValue Value,
    string By,
    int? PhaseOrdinal = null,
    int? RoundOrdinal = null) : ICommand<CompetitionId>;

public sealed class BindParameterHandler(IEventStore eventStore, IEntryQuery entryQuery, IClock clock)
    : ICommandHandler<BindParameter, CompetitionId>
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

        // "Has this round actually started flying" — the fact
        // Competition cannot answer for itself, resolved here and passed in
        // (kanban/completed/task-round-lifecycle.md WI-9). Only for a genuinely
        // round-scoped bind: an unscoped one can never be round-frozen, so it
        // takes no extra query.
        var roundHasEntries = false;
        if (command.PhaseOrdinal is { } phaseOrdinal && command.RoundOrdinal is { } roundOrdinal)
        {
            var entries = await entryQuery.FindAsync(
                command.CompetitionRef, phaseOrdinal, roundOrdinal, null, null, null, cancellationToken);
            roundHasEntries = entries.Count > 0;
        }

        var decision = competition.BindParameter(
            command.ParameterName, command.Value, command.By, clock.UtcNow,
            command.PhaseOrdinal, command.RoundOrdinal, roundHasEntries);
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
