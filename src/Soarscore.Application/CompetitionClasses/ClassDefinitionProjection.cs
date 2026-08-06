// The `class_library` read model — docs/plans/class-definition-adoption-steel-thread-plan.md
// WI-3, LADR-0001 §3/§4.3. One of the four read models the ADR permits; it
// exists solely so the library can be searched/listed across the whole
// population, which no single stream can answer for itself — mirrors
// People/PeopleProjection.cs exactly.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.CompetitionClasses;

/// <summary>
/// The projected row for one published definition. A read-side denormalisation
/// of <see cref="PublishedClassDefinition"/> — nothing here is authoritative;
/// GetClassDefinition (WI-4) resolves by folding the stream, never from this
/// document. Deliberately excludes the full <see cref="ClassDefinition"/>
/// itself — WI-3's own rule: a lookup that needs the whole definition folds
/// the stream.
/// </summary>
/// <param name="Id">
/// Marten's document-identity convention (a property literally named `Id`) —
/// NOT the business identity, which is <see cref="ContentHash"/>. Equal to
/// ClassDefinitionStreamId.From(ContentHash), the same derived Guid the
/// aggregate's own stream is keyed by, so the Infrastructure projection shim
/// can `Load`/`Store` this document by the Guid it already has from the raw
/// event's StreamId without needing to recover a content hash from it — a
/// one-way derivation, so nothing could. Same rationale as
/// <see cref="PublishedClassDefinition.Id"/>: one documented,
/// unused-outside-Infrastructure property beats a wrapper type.
/// </param>
public sealed record ClassDefinitionSummary(
    Guid Id,
    string ContentHash,
    string Name,
    string? FaiDesignation,
    string Version,
    DateTimeOffset PublishedAt,
    DateTimeOffset? RetiredAt);

public static class ClassDefinitionProjection
{
    /// <summary>
    /// Folds one <see cref="ClassDefinitionEvent"/> onto the current summary,
    /// or creates it from <see cref="ClassDefinitionPublished"/>. Folds
    /// <see cref="ClassDefinitionRetired"/> even though the command that
    /// produces it is out of scope this thread — the projection is total over
    /// the event union regardless of which commands exist yet, exactly as
    /// <see cref="PublishedClassDefinition.Apply(PublishedClassDefinition?, ClassDefinitionEvent)"/>
    /// already is.
    /// </summary>
    public static ClassDefinitionSummary? Apply(ClassDefinitionSummary? current, ClassDefinitionEvent @event) =>
        @event switch
        {
            ClassDefinitionPublished e => new ClassDefinitionSummary(
                ClassDefinitionStreamId.From(e.ContentHash), e.ContentHash, e.Definition.Name, e.Definition.FaiDesignation, e.Definition.Version, e.At, RetiredAt: null),
            ClassDefinitionRetired e => Require(current, e) with { RetiredAt = e.At },
            _ => throw new ArgumentException($"Unknown ClassDefinitionEvent subtype: {@event.GetType().Name}"),
        };

    private static ClassDefinitionSummary Require(ClassDefinitionSummary? current, ClassDefinitionEvent @event) =>
        current ?? throw new ArgumentException($"{@event.GetType().Name} projected with no current summary — a change event can never be first in the stream.");
}
