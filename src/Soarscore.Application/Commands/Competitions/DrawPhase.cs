// kanban/completed/phase-drawn-steel-thread-plan.md WI-3. The plain
// RenamePerson/WithdrawCompetitor read->fold->decide->append template — no
// cross-aggregate read, the class definition is already sitting in
// AdoptedRules, copied in at CreateCompetition.

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

/// <summary>
/// <see cref="TaskRefs"/> is <c>IReadOnlyList&lt;string&gt;?</c>, not
/// <c>ImmutableArray&lt;string&gt;</c> — the first command in the repo to
/// carry a collection. An omitted <c>ImmutableArray&lt;T&gt;</c> property
/// deserialises to <c>default</c> — an uninitialised struct that throws on
/// enumeration rather than reading as empty — so the nullable list keeps the
/// omitted case a plain <c>null</c>; converted at the handler boundary.
/// Optional and defaulted so every existing caller of /draw-phase (the
/// FixedSequence classes) keeps working untouched.
/// </summary>
public sealed record DrawPhase(CompetitionId CompetitionId, int Rounds, IReadOnlyList<string>? TaskRefs = null) : ICommand<CompetitionId>;

public sealed class DrawPhaseHandler(IEventStore eventStore, IClock clock) : ICommandHandler<DrawPhase, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(DrawPhase command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var taskRefs = command.TaskRefs?.ToImmutableArray() ?? [];
        var decision = competition.DrawPhase(command.Rounds, taskRefs, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<CompetitionId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        // ExpectedVersion.Exact is the arbiter if two organisers draw
        // concurrently — the loser's retry re-reads Phases non-empty and
        // fails cleanly with drawPhase.alreadyDrawn, never a corrupted
        // schedule. No retry loop — no other handler in this codebase has one.
        var append = await eventStore.AppendAsync(
            command.CompetitionId.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<CompetitionId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<CompetitionId>.Success(command.CompetitionId);
    }
}
