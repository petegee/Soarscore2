// LADR-0003 chooses "System.Text.Json + source generation". Source generation
// and polymorphism have a history of not composing, so the spike exercises it
// rather than assuming it: this context is asked to serialise and deserialise
// the same two definitions, and Program.cs compares the result against the
// reflection-based output byte for byte.
//
// Two things to know, both visible here:
//
//  1. The mode must be Metadata. JsonSourceGenerationMode.Serialization emits a
//     fast path that cannot write a type discriminator, so a polymorphic
//     hierarchy silently falls back to reflection under it — which defeats the
//     point on a trimmed or AOT deployment.
//  2. Every derived type needs its own [JsonSerializable]. The generator does
//     not walk [JsonDerivedType] for you; omit one and the failure is at run
//     time, not at build.

using System.Text.Json.Serialization;

namespace Soarscore.Spike.ClassModel;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ClassDefinition))]
// --- the closed hierarchies, one line each -------------------------------
[JsonSerializable(typeof(ScoreTerm))]
[JsonSerializable(typeof(RateTerm))]
[JsonSerializable(typeof(LookupTerm))]
[JsonSerializable(typeof(PiecewiseTerm))]
[JsonSerializable(typeof(ConstantTerm))]
[JsonSerializable(typeof(ConditionalTerm))]
[JsonSerializable(typeof(Predicate))]
[JsonSerializable(typeof(Comparison))]
[JsonSerializable(typeof(AllOf))]
[JsonSerializable(typeof(FlightSelection))]
[JsonSerializable(typeof(LastFlight))]
[JsonSerializable(typeof(AllFlights))]
[JsonSerializable(typeof(LastNFlights))]
[JsonSerializable(typeof(BestNFlights))]
[JsonSerializable(typeof(ExactlyNInOrder))]
// --- the converter-backed unions -----------------------------------------
[JsonSerializable(typeof(NumberOrParam))]
[JsonSerializable(typeof(FlagOrParam))]
public partial class ClassDefinitionContext : JsonSerializerContext;
