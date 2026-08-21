// kanban/in-progress/annul-and-penalise-the-second-entry-thread.md WI-7. The
// Competition-scoped penalty command: the plain BindParameter template —
// CompetitionLoader.LoadAsync -> decide -> AppendAsync at ExpectedVersion.Exact.
// The Competition holds the adopted rules, so the decide function validates the
// infraction type and the competitor/coordinate itself; the handler only
// constructs the Penalty payload. No IClock — Penalty carries no At (decision 2).

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record RecordCompetitionPenalty(
    CompetitionId CompetitionRef,
    string InfractionType,
    PenaltyScope Scope,
    CompetitorId CompetitorRef,
    TaskRoundCoordinate? TaskRound,
    string? By) : ICommand<CompetitionId>;

public sealed class RecordCompetitionPenaltyHandler(IEventStore eventStore)
    : ICommandHandler<RecordCompetitionPenalty, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(RecordCompetitionPenalty command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;

        var penalty = new Penalty
        {
            InfractionType = command.InfractionType,
            Scope = command.Scope,
            CompetitorRef = command.CompetitorRef,
            TaskRound = command.TaskRound,
            By = command.By,
        };

        var decision = competition.RecordPenalty(penalty);
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
