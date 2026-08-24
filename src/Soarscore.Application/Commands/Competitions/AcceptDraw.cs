// kanban/in-progress/draw-acceptance-redraw.md WI-3. The plain DrawPhase
// read->fold->decide->append template — no cross-aggregate read: acceptance
// only moves the live phase's Draw.Status, so this handler needs no port
// beyond IEventStore/IClock.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record AcceptDraw(CompetitionId CompetitionId) : ICommand<CompetitionId>;

public sealed class AcceptDrawHandler(IEventStore eventStore, IClock clock) : ICommandHandler<AcceptDraw, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(AcceptDraw command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = competition.AcceptDraw(clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<CompetitionId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        // ExpectedVersion.Exact is the arbiter if an accept races a reject —
        // the loser's retry re-reads Phases and fails cleanly with its stable
        // code, never a half-moved draw.
        var append = await eventStore.AppendAsync(
            command.CompetitionId.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<CompetitionId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<CompetitionId>.Success(command.CompetitionId);
    }
}
