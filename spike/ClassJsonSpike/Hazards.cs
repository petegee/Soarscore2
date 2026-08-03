// One hazard the corpus does not currently trip, demonstrated rather than
// asserted, because the class diagram records a near-miss for it.
//
// The discriminator here is named "kind". Six non-polymorphic records already
// serialise a real property under that name — MeasuredValue.Kind,
// Parameter.Kind, MetricDefinition.Kind, TaskTiming.Kind, RoundComposition.Kind
// and PromotionRule.Kind. Nothing collides today because none of those six is
// polymorphic, and two of them were one design decision away from being so: the
// class diagram §3 says splitting TaskTiming into Fixed /
// UntilAllFlightsComplete subtypes "was considered and declined", and §2 says
// the same of PromotionRule.
//
// FINDING, and it is the sharp one. System.Text.Json does not reject the
// collision. It writes BOTH properties —
//
//     {"kind":"fixed","kind":"Fixed"}
//
// — with no error, no warning, and no build failure. The document is emitted,
// hashed, stored, and only fails when something tries to read it back
// ("Deserialized object contains a duplicate 'kind' metadata property").
// The failure is therefore at read time, on the far side of the event store,
// for a definition that was written without complaint.
//
// The fix is free and is a naming choice made once: use a discriminator that
// cannot be a model property name. This spike's model would take "$kind".

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soarscore.Spike.ClassModel;

public static class Hazards
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(Collides), "fixed")]
    public abstract record HasAKindProperty
    {
        public WorkingTimeKind Kind { get; init; }
    }

    public sealed record Collides : HasAKindProperty;

    // The same shape with a discriminator that cannot collide.
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
    [JsonDerivedType(typeof(DoesNotCollide), "fixed")]
    public abstract record HasAKindPropertySafely
    {
        public WorkingTimeKind Kind { get; init; }
    }

    public sealed record DoesNotCollide : HasAKindPropertySafely;

    /// <summary>
    /// True if the collision is what is described above: written silently,
    /// unreadable afterwards.
    /// </summary>
    public static bool CollisionIsSilentOnWriteAndFatalOnRead()
    {
        var written = JsonSerializer.Serialize<HasAKindProperty>(new Collides(), SoarscoreJson.Hashable);
        if (written != """{"kind":"fixed","kind":"Fixed"}""") return false;

        try
        {
            JsonSerializer.Deserialize<HasAKindProperty>(written, SoarscoreJson.Hashable);
            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    /// <summary>True if renaming the discriminator is the whole of the fix.</summary>
    public static bool PrefixedDiscriminatorRoundTrips()
    {
        var written = JsonSerializer.Serialize<HasAKindPropertySafely>(new DoesNotCollide(), SoarscoreJson.Hashable);
        var back = JsonSerializer.Deserialize<HasAKindPropertySafely>(written, SoarscoreJson.Hashable);
        return written == """{"$kind":"fixed","kind":"Fixed"}""" && back is DoesNotCollide;
    }
}
