// kanban/in-progress/teams-mvp.md WI-6. The plain RegisterCompetitor
// read→fold→decide→append template — the handler mints the ScoringTeamId (the
// new entity's identity is the response, RegisterCompetitor's precedent) and
// the decide owns the name checks. No draw gate ever — the draw never sees
// scoring teams (owner decision 6).

using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record DefineScoringTeam(CompetitionId CompetitionRef, string Name) : ICommand<ScoringTeamId>;

public sealed class DefineScoringTeamHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<DefineScoringTeam, ScoringTeamId>
{
    public async Task<Result<ScoringTeamId>> HandleAsync(DefineScoringTeam command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<ScoringTeamId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;

        var id = ScoringTeamId.New();
        var decision = competition.DefineScoringTeam(id, command.Name, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<ScoringTeamId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(
            command.CompetitionRef.Value, ExpectedVersion.Exact(version), [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<ScoringTeamId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<ScoringTeamId>.Success(id);
    }
}
