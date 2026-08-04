// Serialisation for NumberOrParam / FlagOrParam — ADR-0002 §1, LADR-0003.
//
// Relocated here (from tools/Soarscore.SeedData/Json.cs) once a second
// consumer needed it: Application's event contracts embed a full
// ClassDefinition (AdoptedRules, ClassDefinitionPublished), so the converters
// are shared by every project that serialises one, not just the seed-authoring
// tool. ADR-0002 §1 already makes this the model's call — "the wire shape is a
// property of the model, not of an adapter over it" — and System.Text.Json is
// in-box, not a NuGet package, so this does not touch "the Domain has ZERO
// PackageReference" (LADR-0003, project layout).
//
// ParameterReference.cs's own note explains why these exist at all: neither
// union can carry [JsonPolymorphic], because System.Text.Json throws at
// configuration time the moment a type declares polymorphism metadata AND has
// a JsonConverter<T> registered for it. A hand-written converter is the only
// way either type serialises, not a presentation choice over one that would
// otherwise work.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soarscore.Domain.PublishedClassDefinition;

/// <summary>A number, or <c>{"param":"name"}</c>. Twelve of the thirteen slots.</summary>
public sealed class NumberOrParamConverter : JsonConverter<NumberOrParam>
{
    public override NumberOrParam Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
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
            case NumberOrParam.Literal literal:
                writer.WriteNumberValue(literal.Value);
                break;
            case NumberOrParam.Ref reference:
                WriteParamObject(writer, reference.ParameterName);
                break;
        }
    }

    internal static string ReadParamObject(ref Utf8JsonReader reader)
    {
        // The reader is on StartObject.
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

/// <summary>A boolean, or <c>{"param":"name"}</c>. PromotionRule.carryPenalties only.</summary>
public sealed class FlagOrParamConverter : JsonConverter<FlagOrParam>
{
    public override FlagOrParam Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
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
            case FlagOrParam.Literal literal:
                writer.WriteBooleanValue(literal.Value);
                break;
            case FlagOrParam.Ref reference:
                NumberOrParamConverter.WriteParamObject(writer, reference.ParameterName);
                break;
        }
    }
}
