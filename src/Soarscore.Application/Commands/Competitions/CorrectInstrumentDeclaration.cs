// kanban/backlog/tape-points-landing-seeds.md WI-3 (owner decisions 3, 4).
// The correction path for the declared set of instruments: replaces the set
// whole, retroactive by re-derivation, retaining reason, author and time —
// the RulesAmendment shape. Reason is validated in the decide function, not
// here: a correction's justification is a substantive record, not an audit
// breadcrumb (AmendMeasurement's precedent). By stays the handler's
// breadcrumb (BindParameter's precedent).

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record CorrectInstrumentDeclaration(
    CompetitionId CompetitionRef,
    ImmutableArray<DeclaredInstrument> Instruments,
    string Reason,
    string By) : ICommand<CompetitionId>;

public sealed class CorrectInstrumentDeclarationHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<CorrectInstrumentDeclaration, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(CorrectInstrumentDeclaration command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        if (string.IsNullOrWhiteSpace(command.By))
        {
            return Result<CompetitionId>.Failure(
                "competition.instruments.byRequired", "By is required — a self-declared CD name, not an authorisation claim.");
        }

        var (competition, version) = loaded.Value;

        var decision = competition.CorrectInstrumentDeclaration(
            command.Instruments, command.Reason, command.By, clock.UtcNow);
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
