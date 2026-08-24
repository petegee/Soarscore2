// kanban/in-progress/draw-acceptance-redraw.md WI-3. DrawPhase's template
// plus BindParameter's one addition: whether entries exist against the live
// phase is a fact Competition cannot answer for itself (D5 — entries reference
// the doomed draw's GroupIds), so it is resolved here via IEntryQuery and
// passed into the decide function as an already-resolved fact. Aggregate
// boundary holds; only the handler ever calls RejectDraw.

using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record RejectDraw(CompetitionId CompetitionRef, string Reason) : ICommand<CompetitionId>;

public sealed class RejectDrawHandler(IEventStore eventStore, IEntryQuery entryQuery, IClock clock)
    : ICommandHandler<RejectDraw, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(RejectDraw command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;

        // D5: only when a live phase exists can anything be entered against
        // it. With none, skip the query entirely — the decide's
        // noDrawnPhase fires first regardless.
        var phaseHasEntries = false;
        if (!competition.Phases.IsEmpty)
        {
            var liveOrdinal = competition.Phases.Single().Ordinal;
            var entries = await entryQuery.FindAsync(
                command.CompetitionRef, liveOrdinal, null, null, null, null, cancellationToken);
            phaseHasEntries = entries.Count > 0;
        }

        var decision = competition.RejectDraw(phaseHasEntries, command.Reason, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<CompetitionId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        // Reason travels for audit only — TaskRoundAnnulled's precedent;
        // ExpectedVersion.Exact arbitrates a concurrent accept/reject race.
        var append = await eventStore.AppendAsync(
            command.CompetitionRef.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<CompetitionId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<CompetitionId>.Success(command.CompetitionRef);
    }
}
