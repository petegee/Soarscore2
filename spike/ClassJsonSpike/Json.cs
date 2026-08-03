// Serialisation options and the two hand-written converters.
//
// The question this spike exists to answer is whether the deep ScoreTerm /
// Predicate nesting needs a custom converter or can live on attribute-declared
// discriminators. The answer, below, is that the *hierarchies* need nothing:
// [JsonPolymorphic] + [JsonDerivedType] carries them. The only converters
// written by hand are for the two literal-or-parameter unions, and they exist
// for readability of the artefact rather than for correctness.

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soarscore.Spike.ClassModel;

public static class SoarscoreJson
{
    /// <summary>The canonical form: unions collapse to a bare number or {"param": …}.</summary>
    public static readonly JsonSerializerOptions Canonical = Build(collapseUnions: true, indented: true);

    /// <summary>The same corpus with the unions left to the polymorphism metadata.</summary>
    public static readonly JsonSerializerOptions Verbose = Build(collapseUnions: false, indented: true);

    /// <summary>Canonical, unindented — what a content hash would be taken over.</summary>
    public static readonly JsonSerializerOptions Hashable = Build(collapseUnions: true, indented: false);

    /// <summary>
    /// FINDING. Canonical, but with the .NET 9 option that lets the type
    /// discriminator appear anywhere in the object rather than first.
    ///
    /// Without it, STJ throws NotSupportedException — "the JSON payload for
    /// polymorphic interface or abstract type 'Predicate' must specify a type
    /// discriminator" — on a document that is otherwise identical and contains
    /// the discriminator. Anything that reorders keys produces one: a formatter,
    /// a JSON-sorting pre-commit hook, most languages' dictionary round trip,
    /// and a human writing the obvious `{"metricRef": …, "kind": "rate"}`.
    /// On the POST path that is a rejection the author cannot act on, since the
    /// message names a property their document already has.
    /// </summary>
    public static readonly JsonSerializerOptions Lenient = BuildLenient();

    private static JsonSerializerOptions BuildLenient()
    {
        var o = Build(collapseUnions: true, indented: true);
        o.AllowOutOfOrderMetadataProperties = true;
        return o;
    }

    /// <summary>Canonical, resolved through the source-generated context.</summary>
    public static readonly JsonSerializerOptions SourceGenerated = BuildSourceGenerated();

    private static JsonSerializerOptions BuildSourceGenerated()
    {
        var o = Build(collapseUnions: true, indented: true);
        o.TypeInfoResolver = ClassDefinitionContext.Default;
        return o;
    }

    private static JsonSerializerOptions Build(bool collapseUnions, bool indented)
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = indented,
            // ADR-0002 §4: the input is untrusted, so nesting depth is bounded.
            // 64 is the framework default; the corpus's deepest path is well
            // inside it (see Program.cs, which measures it).
            MaxDepth = 64,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        if (collapseUnions)
        {
            o.Converters.Add(new NumberOrParamConverter());
            o.Converters.Add(new FlagOrParamConverter());
        }
        else
        {
            // What the same corpus costs with the union written as a tagged
            // object at every site — the shape [JsonDerivedType] would produce
            // if the unions took that route. It has to be a converter here
            // because a type cannot carry both (see the finding in Model.cs).
            o.Converters.Add(new TaggedNumberOrParamConverter());
            o.Converters.Add(new TaggedFlagOrParamConverter());
        }
        return o;
    }
}

/// <summary>
/// A number, or {"param":"name"}. Thirteen slots in the model accept one, so a
/// discriminated object at every site is thirteen kinds of noise; this collapses
/// the common case to the literal it is.
/// </summary>
public sealed class NumberOrParamConverter : JsonConverter<NumberOrParam>
{
    public override NumberOrParam Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => new NumberOrParam.Literal(reader.GetDecimal()),
            JsonTokenType.StartObject => new NumberOrParam.Ref(ReadParamObject(ref reader)),
            _ => throw new JsonException($"Expected a number or a parameter reference, found {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, NumberOrParam value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case NumberOrParam.Literal l:
                writer.WriteNumberValue(l.Value);
                break;
            case NumberOrParam.Ref r:
                WriteParamObject(writer, r.ParameterName);
                break;
        }
    }

    internal static string ReadParamObject(ref Utf8JsonReader reader)
    {
        // reader is on StartObject.
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "param")
            throw new JsonException("A parameter reference is an object with exactly one property, \"param\".");
        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            throw new JsonException("\"param\" takes a parameter name.");
        var name = reader.GetString()!;
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException("A parameter reference carries no other properties.");
        return name;
    }

    internal static void WriteParamObject(Utf8JsonWriter writer, string name)
    {
        writer.WriteStartObject();
        writer.WriteString("param", name);
        writer.WriteEndObject();
    }
}

/// <summary>
/// The comparison arm: {"kind":"literal","value":599} / {"kind":"param","parameterName":"nlh"},
/// which is byte-for-byte what [JsonDerivedType] would emit for these unions.
/// </summary>
public sealed class TaggedNumberOrParamConverter : JsonConverter<NumberOrParam>
{
    public override NumberOrParam Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions options)
    {
        var (kind, value) = Tagged.Read(ref reader);
        return kind switch
        {
            "literal" => new NumberOrParam.Literal(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)),
            "param" => new NumberOrParam.Ref(value),
            _ => throw new JsonException($"Unknown NumberOrParam kind \"{kind}\"."),
        };
    }

    public override void Write(Utf8JsonWriter writer, NumberOrParam value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case NumberOrParam.Literal l:
                writer.WriteString("kind", "literal");
                writer.WriteNumber("value", l.Value);
                break;
            case NumberOrParam.Ref r:
                writer.WriteString("kind", "param");
                writer.WriteString("parameterName", r.ParameterName);
                break;
        }
        writer.WriteEndObject();
    }
}

/// <summary>The comparison arm for FlagOrParam.</summary>
public sealed class TaggedFlagOrParamConverter : JsonConverter<FlagOrParam>
{
    public override FlagOrParam Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions options)
    {
        var (kind, value) = Tagged.Read(ref reader);
        return kind switch
        {
            "literal" => new FlagOrParam.Literal(bool.Parse(value)),
            "param" => new FlagOrParam.Ref(value),
            _ => throw new JsonException($"Unknown FlagOrParam kind \"{kind}\"."),
        };
    }

    public override void Write(Utf8JsonWriter writer, FlagOrParam value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case FlagOrParam.Literal l:
                writer.WriteString("kind", "literal");
                writer.WriteBoolean("value", l.Value);
                break;
            case FlagOrParam.Ref r:
                writer.WriteString("kind", "param");
                writer.WriteString("parameterName", r.ParameterName);
                break;
        }
        writer.WriteEndObject();
    }
}

internal static class Tagged
{
    /// <summary>Reads {"kind": …, "value"|"parameterName": …} as two strings.</summary>
    internal static (string Kind, string Value) Read(ref Utf8JsonReader reader)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("kind", out var kind))
            throw new JsonException("Expected a tagged union object.");
        var payload = root.TryGetProperty("value", out var v) ? v
                    : root.TryGetProperty("parameterName", out var n) ? n
                    : throw new JsonException("Tagged union carries no payload.");
        return (kind.GetString()!, payload.ToString());
    }
}

/// <summary>A boolean, or {"param":"name"}. PromotionRule.carryPenalties only.</summary>
public sealed class FlagOrParamConverter : JsonConverter<FlagOrParam>
{
    public override FlagOrParam Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => new FlagOrParam.Literal(true),
            JsonTokenType.False => new FlagOrParam.Literal(false),
            JsonTokenType.StartObject => new FlagOrParam.Ref(NumberOrParamConverter.ReadParamObject(ref reader)),
            _ => throw new JsonException($"Expected a boolean or a parameter reference, found {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, FlagOrParam value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case FlagOrParam.Literal l:
                writer.WriteBooleanValue(l.Value);
                break;
            case FlagOrParam.Ref r:
                NumberOrParamConverter.WriteParamObject(writer, r.ParameterName);
                break;
        }
    }
}
