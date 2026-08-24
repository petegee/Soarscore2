// kanban/in-progress/reflight-scoring-rulings.md WI-5. The command that gives
// a CD ruling somewhere to live where the class rulebook is silent — the plain
// BindParameter/RecordCompetitionPenalty template:
// CompetitionLoader.LoadAsync -> decide -> AppendAsync at ExpectedVersion.Exact.
//
// The command/handler return convention mirrors RecordCompetitionPenalty
// exactly (planner's call 6): ICommand<CompetitionId> and Result<CompetitionId>
// — a ruling names an act on an existing competition; unlike AppendReflightGroup
// it mints nothing the caller needs back.
//
// `Reason` and `Selection` are NOT validated here — they are substantive parts
// of the ruling, so the decide function owns them
// (Competition.RecordReflightRuling's doc comment). At comes from IClock, per
// the ReflightGroups precedent.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Commands.Competitions;

/// <summary>
/// Records one CD ruling of which score counts for a re-flight: which of
/// <see cref="CompetitorRef"/>'s two attempts for the named task-round stands,
/// Replacement or BetterOf. Accepted only where the adopted class's resolved
/// rulebook is silent (UndefinedRequiresRuling); superseding rulings for the
/// same key accumulate — last logged wins.
/// </summary>
public sealed record RecordReflightRuling(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    CompetitorId CompetitorRef,
    ReflightSelection Selection,
    string Reason,
    string? By) : ICommand<CompetitionId>;

public sealed class RecordReflightRulingHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<RecordReflightRuling, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(RecordReflightRuling command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;

        var decision = competition.RecordReflightRuling(new ReflightRuling
        {
            TaskRound = new TaskRoundCoordinate(command.PhaseOrdinal, command.RoundOrdinal, command.TaskRoundOrdinal),
            CompetitorRef = command.CompetitorRef,
            Selection = command.Selection,
            Reason = command.Reason,
            By = command.By,
            At = clock.UtcNow,
        });
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
