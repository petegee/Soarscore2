// Event contracts for the CompetitionClass aggregate — docs/aggregate-roots.md
// §1, LADR-0002 §5.
//
// CompetitionClass's identity is a content hash over the canonical
// serialisation, not a minted id (LADR-0002 §5, Domain/Shared.cs) — "edit"
// therefore has no meaning as a mutation of an existing stream: different
// content is a different hash, hence a different stream. What IS a mutation
// of one stream is retiring it from the active library while its history
// stays in the log (LADR-0002 §5, "History is free"). Two events only:
//
//   ClassDefinitionPublished — the ingestion pipeline's terminal step
//     (LADR-0002 §4: deserialise -> Validate -> canonicalise+hash -> append).
//     One per distinct content hash; republishing identical content targets
//     the same stream and is a safe no-op at the domain level.
//   ClassDefinitionRetired — removes it from what a new Competition may adopt.
//     A Competition that already adopted a retired definition is unaffected
//     (AdoptedRules is a copy — aggregate-roots.md §3).
//
// Validation (the sixteen adoption checks, LADR-0003 "Hand-written Validate()")
// and content-hash computation (ClassDefinitionHashing.cs) are the ingestion
// pipeline's job, not this file's — these are the two facts the pipeline
// commits once it has run them.

using System.Text.Json.Serialization;

namespace Soarscore.Domain.CompetitionClasses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(ClassDefinitionPublished), "classDefinitionPublished")]
[JsonDerivedType(typeof(ClassDefinitionRetired), "classDefinitionRetired")]
public abstract record ClassDefinitionEvent
{
    private protected ClassDefinitionEvent() { }
}

/// <summary>
/// The terminal step of ingestion (LADR-0002 §4). <see cref="ContentHash"/> is
/// this stream's business identity — the full SHA-256 hex digest over the
/// canonical serialisation (ADR-0002 §5), computed by
/// <see cref="ClassDefinitionHashing"/>. Carried on the event, not just implied
/// by the stream key, because Infrastructure's Marten stream key here is a
/// derived Guid (see ClassDefinitionStreamId), and the true identity must not
/// depend on that derivation surviving unchanged.
/// </summary>
public sealed record ClassDefinitionPublished(
    string ContentHash,
    ClassDefinition Definition,
    DateTimeOffset At) : ClassDefinitionEvent;

/// <summary>Removes a definition from the active library. History is untouched (LADR-0002 §5).</summary>
public sealed record ClassDefinitionRetired(
    string Reason,
    DateTimeOffset At) : ClassDefinitionEvent;
