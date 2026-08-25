// kanban/in-progress/prescribed-draw-import.md WI-3. The plain
// DrawPhase read->fold->decide->append template — no cross-aggregate read:
// AdoptedRules rides in the stream, and the no-entries precondition is
// structural — no live phase means no accepted draw, so entries cannot exist.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

/// <summary>
/// Sets the live phase slot's schedule explicitly instead of drawing fresh —
/// the prescribed-draw path beside /draw-phase. <see cref="Rounds"/> is
/// <c>IReadOnlyList</c>, not <c>ImmutableArray</c>, on the boundary
/// convention DrawPhase documents (an omitted ImmutableArray property
/// deserialises to a throwing default); it reuses the Domain's
/// <see cref="PrescribedRound"/>/<see cref="PrescribedGroup"/> beside the
/// aggregate — one source of truth for the payload shape. Members are listed
/// in flying order (SeqNo for imported comps) and preserved as-is; group and
/// round ordinals are assigned by position, never supplied. There is
/// deliberately no separate rounds-count property: the list is the single
/// source of truth. A null <c>TaskRef</c> is legal only where the phase is
/// FixedSequence; the decide's shared validation arbitrates, exactly as
/// /draw-phase does.
/// </summary>
public sealed record PrescribeDraw(CompetitionId CompetitionId, IReadOnlyList<PrescribedRound> Rounds, string By)
    : ICommand<CompetitionId>;

public sealed class PrescribeDrawHandler(IEventStore eventStore, IClock clock) : ICommandHandler<PrescribeDraw, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(PrescribeDraw command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        // By is checked here, not in Competition.PrescribeDraw, on
        // BindParameterHandler's reasoning: the trust model has no auth, so
        // By is a self-declared CD name — but unlike its audit-only role
        // there, an absent PrescribedBy would make a prescribed draw
        // indistinguishable from a generated one in the log (P1's whole
        // point), so "non-empty" is load-bearing before the decide runs.
        if (string.IsNullOrWhiteSpace(command.By))
        {
            return Result<CompetitionId>.Failure(
                "prescribeDraw.byRequired", "By is required — a self-declared CD name, not an authorisation claim.");
        }

        var (competition, version) = loaded.Value;
        var decision = competition.PrescribeDraw(command.Rounds, command.By, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<CompetitionId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        // ExpectedVersion.Exact is the arbiter if two organisers prescribe
        // concurrently — the loser's retry re-reads Phases non-empty and
        // fails cleanly with prescribeDraw.alreadyDrawn, never a corrupted
        // schedule. No retry loop — no other handler in this codebase has one.
        var append = await eventStore.AppendAsync(
            command.CompetitionId.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<CompetitionId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<CompetitionId>.Success(command.CompetitionId);
    }
}
