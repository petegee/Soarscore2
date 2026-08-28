// The canonical serialisation — ADR-0002 §1, §4 and LADR-0003.
//
// Three settings are not defaults and each one is a spike finding:
//
//  1. TypeDiscriminatorPropertyName = "$kind", declared on the model itself. A
//     discriminator that shadows a real property emits BOTH keys, silently.
//  2. AllowOutOfOrderMetadataProperties on the READ path. A document carrying
//     the discriminator anywhere but first is otherwise rejected, with a message
//     naming a property the document already has — a rejection a class author
//     POSTing a definition cannot act on.
//  3. MaxDepth well inside 64. The corpus's deepest path is 11 (F5K Task E), so
//     ADR-0002 §4's input limit sits at 24 with room for a class nobody has
//     written yet.
//
// The two hand-written converters (NumberOrParamConverter, FlagOrParamConverter,
// now in Soarscore.Domain.CompetitionClasses — moved once Application's event
// contracts needed them too) are NOT required for correctness — the tagged
// form round-trips identically. They collapse thirteen slots' worth of
// {"kind":"literal","value":599} to 599, which is 9-19% of the artefact and
// rather more of its readability. A presentation decision about the reviewable
// seed corpus, and reversible.

using System.Text.Json;
using System.Text.Json.Serialization;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SoarscoreJson
{
    /// <summary>ADR-0002 §4: the input is untrusted, so nesting depth is bounded.</summary>
    public const int IngestionMaxDepth = 24;

    /// <summary>What is checked in: the canonical form, indented, for a reviewable diff.</summary>
    public static readonly JsonSerializerOptions Canonical = Build(indented: true, lenient: false);

    /// <summary>Canonical, unindented — what a content hash is taken over (ADR-0002 §5).</summary>
    public static readonly JsonSerializerOptions Hashable = Build(indented: false, lenient: false);

    /// <summary>The ingestion path: out-of-order discriminators tolerated, canonical order recoverable.</summary>
    public static readonly JsonSerializerOptions Ingestion = Build(indented: true, lenient: true);

    /// <summary>Canonical, resolved through the source-generated context.</summary>
    public static readonly JsonSerializerOptions SourceGenerated = BuildSourceGenerated();

    private static JsonSerializerOptions Build(bool indented, bool lenient)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = indented,
            MaxDepth = IngestionMaxDepth,
            AllowOutOfOrderMetadataProperties = lenient,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new NumberOrParamConverter());
        options.Converters.Add(new FlagOrParamConverter());
        return options;
    }

    private static JsonSerializerOptions BuildSourceGenerated()
    {
        var options = Build(indented: true, lenient: false);
        options.TypeInfoResolver = ClassDefinitionContext.Default;
        return options;
    }
}

// Source generation composes with the polymorphic hierarchies ONLY under
// GenerationMode = Metadata and with every derived type given its own
// [JsonSerializable]: JsonSourceGenerationMode.Serialization emits a fast path
// that cannot write a discriminator, and the generator does not walk
// [JsonDerivedType], so an omission fails at RUN time, not at build. Program.cs
// compares its output against reflection byte for byte on all twelve classes.
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ClassDefinition))]
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
[JsonSerializable(typeof(NumberOrParam))]
[JsonSerializable(typeof(FlagOrParam))]
public sealed partial class ClassDefinitionContext : JsonSerializerContext;
