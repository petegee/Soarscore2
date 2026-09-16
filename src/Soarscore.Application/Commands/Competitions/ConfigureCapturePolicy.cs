// authentication-and-authorisation.md WI-7 (D10). Configuring who may enter
// which scores for one competition. One cross-aggregate read before the
// decide — the CreateCompetition precedent: Competition cannot read people
// (Competition.cs ConfigureCapturePolicy's own remark), so the handler
// confirms every allow-listed capturer exists in the people read model. It is
// a best-effort pre-read for a fast, specific failure, never the arbiter
// (RegisterPerson email precedent): a capturer registered between the check
// and the append is additive and harmless, and the decide still owns the
// policy's shape (emptyAllowList / capturersIgnored).

using Soarscore.Application.Queries.People;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Commands.Competitions;

public sealed record ConfigureCapturePolicy(CompetitionId CompetitionRef, CapturePolicy Policy) : ICommand<CompetitionId>;

public sealed class ConfigureCapturePolicyHandler(IEventStore eventStore, IPeopleQuery peopleQuery, IClock clock)
    : ICommandHandler<ConfigureCapturePolicy, CompetitionId>
{
    public async Task<Result<CompetitionId>> HandleAsync(ConfigureCapturePolicy command, CancellationToken cancellationToken)
    {
        // Only an AllowList names capturers, so only an AllowList has people
        // to confirm (the decide refuses capturers on every other mode —
        // competition.capturePolicy.capturersIgnored). A null list binds
        // straight through from JSON and is the decide's emptyAllowList, not
        // an existence-check concern.
        if (command.Policy.Mode == CapturePolicyMode.AllowList && command.Policy.Capturers is { Count: > 0 } capturers)
        {
            var known = await peopleQuery.FindByIdsAsync(capturers, cancellationToken);
            var missing = capturers.Except(known.Select(p => p.Id)).ToList();
            if (missing.Count > 0)
            {
                return Result<CompetitionId>.Failure(
                    "competition.capturePolicy.unknownPerson",
                    $"No person found with id {missing[0]} — every allow-listed capturer must be a registered person.");
            }
        }

        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, version) = loaded.Value;
        var decision = competition.ConfigureCapturePolicy(command.Policy, clock.UtcNow);
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