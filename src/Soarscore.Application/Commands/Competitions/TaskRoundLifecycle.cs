// kanban/completed/task-round-lifecycle.md WI-5 — the three task-round
// lifecycle commands. All three are the plain BindParameter template:
// CompetitionLoader.LoadAsync -> decide -> AppendAsync at
// ExpectedVersion.Exact. They sit in one file because they are one lifecycle
// and each is a handful of lines; splitting them would triple the boilerplate
// without separating anything.
//
// Unlike BindParameter, `Reason` is NOT validated here: it is a substantive
// record of a ruling rather than a self-declared audit breadcrumb, so the
// decide function owns it (Competition.AnnulTaskRound's doc comment).

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

/// <summary>
/// The CD asserting that this task-round's scores are in and settled. Never
/// inferred and never a side effect — NFR-4, and reversible by
/// <see cref="ReopenTaskRound"/>.
/// </summary>
public sealed record CompleteTaskRound(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal) : ICommand<CompetitionId>;

public sealed record AnnulTaskRound(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    string Reason) : ICommand<CompetitionId>;

/// <summary>Returns a closed task-round to Drawn, so a late score is accepted rather than refused.</summary>
public sealed record ReopenTaskRound(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    string Reason) : ICommand<CompetitionId>;

public sealed class CompleteTaskRoundHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<CompleteTaskRound, CompetitionId>
{
    public Task<Result<CompetitionId>> HandleAsync(CompleteTaskRound command, CancellationToken cancellationToken) =>
        TaskRoundLifecycle.AppendAsync(
            eventStore, command.CompetitionRef, cancellationToken,
            competition => competition.CompleteTaskRound(
                command.PhaseOrdinal, command.RoundOrdinal, command.TaskRoundOrdinal, clock.UtcNow));
}

public sealed class AnnulTaskRoundHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<AnnulTaskRound, CompetitionId>
{
    public Task<Result<CompetitionId>> HandleAsync(AnnulTaskRound command, CancellationToken cancellationToken) =>
        TaskRoundLifecycle.AppendAsync(
            eventStore, command.CompetitionRef, cancellationToken,
            competition => competition.AnnulTaskRound(
                command.PhaseOrdinal, command.RoundOrdinal, command.TaskRoundOrdinal, command.Reason, clock.UtcNow));
}

public sealed class ReopenTaskRoundHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<ReopenTaskRound, CompetitionId>
{
    public Task<Result<CompetitionId>> HandleAsync(ReopenTaskRound command, CancellationToken cancellationToken) =>
        TaskRoundLifecycle.AppendAsync(
            eventStore, command.CompetitionRef, cancellationToken,
            competition => competition.ReopenTaskRound(
                command.PhaseOrdinal, command.RoundOrdinal, command.TaskRoundOrdinal, command.Reason, clock.UtcNow));
}

/// <summary>
/// The load -> decide -> append body the three handlers above share. Generic
/// in the event type so each decide function keeps its own
/// <c>Result&lt;T&gt;</c> return rather than being widened to the base event.
/// </summary>
internal static class TaskRoundLifecycle
{
    public static async Task<Result<CompetitionId>> AppendAsync<TEvent>(
        IEventStore eventStore,
        CompetitionId competitionRef,
        CancellationToken cancellationToken,
        Func<Competition, Result<TEvent>> decide)
        where TEvent : CompetitionEvent
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, competitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = decide(competition);
        if (decision.IsFailure)
        {
            return Result<CompetitionId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(
            competitionRef.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);

        return append.IsFailure
            ? Result<CompetitionId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<CompetitionId>.Success(competitionRef);
    }
}
