// Marten stream identity for the CompetitionClass aggregate.
//
// LADR-0002 §5 makes CompetitionClass's real identity a content hash, and
// Shared.cs deliberately mints no ClassDefinitionId for it. The other three
// aggregates use a `readonly record struct XId(Guid)` shape that Marten's
// strong-typed-identifier convention resolves to a Guid-identified stream
// directly. A hash string doesn't fit that convention, and this store is not
// worth running with mixed Guid/string stream identity (LADR-0001 keeps this
// lightweight — one Marten store, one identity scheme) for the sake of one
// aggregate out of four.
//
// The bridge: derive a deterministic Guid from the first 16 bytes of the
// SHA-256 content hash and use THAT as the Marten stream key. It is stable
// (same content always maps to the same stream), collision-safe at this
// project's scale (a handful of class definitions, ever), and it is purely a
// storage-key concern — the business identity a Competition's AdoptedRules
// and every ClassDefinitionEvent actually carries is still the full hash
// string, never this Guid.
//
// Lives in Application, not Infrastructure (kanban/completed/class-definition-adoption-steel-thread-plan.md
// WI-4), despite deriving a Marten stream key: PublishClassDefinitionHandler
// must supply this Guid to IEventStore.AppendAsync and GetClassDefinition must
// supply the same Guid to ReadStreamAsync, and both are Application-layer code
// that must not depend on Infrastructure. The function itself has zero
// Marten/Npgsql dependency (pure Guid-from-bytes arithmetic), so this is a
// relocation, not a redesign — Infrastructure keeps calling it from its new
// home, and nothing about its behaviour changes.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Shared.CompetitionClasses;

public static class ClassDefinitionStreamId
{
    public static Guid From(string contentHash)
    {
        var bytes = Convert.FromHexString(contentHash);
        if (bytes.Length < 16)
            throw new ArgumentException($"Content hash is too short to derive a stream id from: '{contentHash}'.", nameof(contentHash));

        return new Guid(bytes[..16]);
    }

    public static Guid From(ClassDefinitionPublished @event) => From(@event.ContentHash);
}
