// Shared by GetCompetitionHandler: read the stream, fold it, hand back both
// the aggregate and the version the next append must be Exact() against. Not
// a port — a private helper over the IEventStore port, so it stays internal
// to this project. Mirrors People/PersonLoader.cs — Competition's fold, like
// Person's, requires a non-null current after CompetitionCreated (Competition.cs's
// Require helper throws on any other event folded with no current), so this
// follows PersonLoader's pattern rather than ClassDefinitionLoader's.

using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Competitions;

internal static class CompetitionLoader
{
    public static async Task<Result<(Competition Competition, long Version)>> LoadAsync(
        IEventStore eventStore, CompetitionId id, CancellationToken cancellationToken)
    {
        var read = await eventStore.ReadStreamAsync(id.Value, 0, cancellationToken);
        if (read.IsFailure)
        {
            return Result<(Competition, long)>.Failure(read.Code!, read.Message!, read.Defects);
        }

        var events = read.Value;
        if (events.Count == 0)
        {
            return Result<(Competition, long)>.Failure("competition.notFound", $"No competition found with id {id}.");
        }

        var competition = events.Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
        return Result<(Competition, long)>.Success((competition, events.Count));
    }
}
