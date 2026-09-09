// kanban/backlog/tape-points-landing-seeds.md WI-3 (owner decision 3). The
// first declaration of the competition's set of instruments in use — possibly
// empty — each binding a reading scale to a named metric. The BindParameter
// read->fold->decide->append template: `By` is validated here, not in
// Competition.DeclareInstruments, because the trust model has no auth
// (CLAUDE.md) — By is a self-declared CD name, an audit breadcrumb rather
// than an authorisation claim, so its only handler-level obligation is
// "non-empty", checked before the decide function runs.

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record DeclareInstruments(
    CompetitionId CompetitionRef,
    ImmutableArray<DeclaredInstrument> Instruments,
    string By) : ICommand<CompetitionId>;

public sealed class DeclareInstrumentsHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<DeclareInstruments, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(DeclareInstruments command, CancellationToken cancellationToken)
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

        var decision = competition.DeclareInstruments(command.Instruments, command.By, clock.UtcNow);
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
