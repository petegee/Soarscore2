// Shared by GetClassDefinition: read the stream, fold it, hand back the
// PublishedClassDefinition aggregate. Not a port — a private helper over the
// IEventStore port, so it stays internal to this project. Mirrors
// People/PersonLoader.cs; no next-append version is needed here since
// RetireClassDefinition (the only mutation) is out of scope this thread.

using Soarscore.Domain;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.CompetitionClasses;

internal static class ClassDefinitionLoader
{
    public static async Task<Result<PublishedClassDefinition>> LoadAsync(
        IEventStore eventStore, string contentHash, CancellationToken cancellationToken)
    {
        var streamId = ClassDefinitionStreamId.From(contentHash);
        var read = await eventStore.ReadStreamAsync(streamId, 0, cancellationToken);
        if (read.IsFailure)
        {
            return Result<PublishedClassDefinition>.Failure(read.Code!, read.Message!, read.Defects);
        }

        var events = read.Value;
        if (events.Count == 0)
        {
            return Result<PublishedClassDefinition>.Failure("classDefinition.notFound", $"No class definition found with content hash {contentHash}.");
        }

        var definition = events.Aggregate((PublishedClassDefinition?)null, (current, e) => PublishedClassDefinition.Apply(current, (ClassDefinitionEvent)e))!;
        return Result<PublishedClassDefinition>.Success(definition);
    }
}
