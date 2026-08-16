// docs/plans/class-definition-adoption-steel-thread-plan.md WI-4. Served by
// folding the stream, never from the `class_library` read model —
// high-level-architecture.md: "If querying by ID, then you must use load the
// stream." Mirrors People/GetPerson.cs; the query returns the plain
// ClassDefinition value object, not the wrapping PublishedClassDefinition
// aggregate — content hash, publish/retire history are library concerns the
// caller already has from FindClassDefinitions if it needs them.

using Soarscore.Domain;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Queries.CompetitionClasses;

public readonly record struct GetClassDefinition(string ContentHash) : IQuery<ClassDefinition>;

public sealed class GetClassDefinitionHandler(IEventStore eventStore) : IQueryHandler<GetClassDefinition, ClassDefinition>
{
    public async Task<Result<ClassDefinition>> HandleAsync(GetClassDefinition query, CancellationToken cancellationToken)
    {
        var loaded = await ClassDefinitionLoader.LoadAsync(eventStore, query.ContentHash, cancellationToken);
        return loaded.IsFailure
            ? Result<ClassDefinition>.Failure(loaded.Code!, loaded.Message!, loaded.Defects)
            : Result<ClassDefinition>.Success(loaded.Value.Definition);
    }
}
