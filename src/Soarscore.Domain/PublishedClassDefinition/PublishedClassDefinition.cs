// The CompetitionClass aggregate root — docs/aggregate-roots.md §1, LADR-0002 §5.

namespace Soarscore.Domain.PublishedClassDefinition;

/// <summary>
/// The published, library-tracked state of one <see cref="ClassDefinition"/>:
/// its content hash, when it was published, and (if applicable) retired.
/// <see cref="ContentHash"/> and <see cref="RetiredAt"/> are library/ingestion
/// concepts (LADR-0002 §5), not properties of the rulebook itself — the
/// wrapped <see cref="Definition"/> carries neither, deliberately (Shared.cs:
/// "there is no ClassDefinitionId to mint").
/// </summary>
public sealed record PublishedClassDefinition
{
    /// <summary>
    /// Marten's document-identity convention (a property literally named `Id`)
    /// — NOT the business identity, which is <see cref="ContentHash"/>. Left
    /// unset by <see cref="Create"/>; Soarscore.Infrastructure's Marten
    /// registration is the only thing that reads or writes it, via
    /// ClassDefinitionStreamId's derivation from the content hash. Kept as a
    /// plain Guid on the aggregate itself rather than pushed out into an
    /// Infrastructure-only wrapper type: the wrapper would exist solely to
    /// carry this one field, which is exactly the per-aggregate wrapper shape
    /// WI-0 eliminated for the other three aggregates — introducing one back
    /// here, for one property nothing in Domain or Application ever reads,
    /// would buy purity at the cost of the symmetry this refactor is trying to
    /// establish. One documented, unused-outside-Infrastructure property beats
    /// a wrapper type (WI-4 option 1).
    /// </summary>
    public Guid Id { get; init; }

    public required string ContentHash { get; init; }

    public required ClassDefinition Definition { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>Null while active. A retired definition's history is untouched (LADR-0002 §5).</summary>
    public DateTimeOffset? RetiredAt { get; init; }

    /// <summary>The creation event. Every stream begins with exactly one of these.</summary>
    public static PublishedClassDefinition Create(ClassDefinitionPublished @event) => new()
    {
        ContentHash = @event.ContentHash,
        Definition = @event.Definition,
        PublishedAt = @event.At,
    };

    /// <summary>Null current folds to null — the ingestion pipeline never retires an unpublished hash.</summary>
    public PublishedClassDefinition? Apply(ClassDefinitionRetired @event) => this with { RetiredAt = @event.At };

    /// <summary>
    /// Generic replay entry point — same signature <c>ClassDefinitionProjection.Apply</c>
    /// had, so <c>stream.Events.Aggregate(...)</c>-style callers barely change.
    /// </summary>
    public static PublishedClassDefinition? Apply(PublishedClassDefinition? current, ClassDefinitionEvent @event) =>
        @event switch
        {
            ClassDefinitionPublished published => Create(published),
            ClassDefinitionRetired retired => current?.Apply(retired),
            _ => throw new ArgumentException($"Unknown ClassDefinitionEvent subtype: {@event.GetType().Name}"),
        };
}
