// operational-tie-break-resolution.md WI-3. The command that gives a recorded
// tie-break outcome somewhere to land — the plainest load-decide-append in the
// codebase (RecordReflightRuling's handler is the template; FinaliseCompetition
// for the loader shape).
//
// The handler does NOT check that the group is currently pending (D6/NFR-4):
// the outcome lands whenever the CD records it, and an outcome matching no
// halted group is inert, never refused.

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Commands.Competitions;

/// <summary>
/// Records the CD's resolution of one tie group whose ladder halted on an
/// operational or UndefinedRequiresRuling rung: the group's resolved ordering
/// (per-competitor placing within the group, ties explicit) plus the directive
/// it resolves. Validated for shape and rulebook context only — never for
/// pending-ness (D6). Re-recording supersedes: the log keeps every decision,
/// last-logged wins at lookup (D4).
/// </summary>
public sealed record RecordTieBreakOutcome(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    TieBreakDirective Directive,
    ImmutableArray<TieBreakOutcomePlacing> Placings,
    string Reason,
    string? By = null) : ICommand<CompetitionId>;

public sealed class RecordTieBreakOutcomeHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<RecordTieBreakOutcome, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(RecordTieBreakOutcome command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;

        var decision = competition.RecordTieBreakOutcome(new TieBreakOutcome
        {
            PhaseOrdinal = command.PhaseOrdinal,
            Directive = command.Directive,
            Placings = command.Placings,
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
