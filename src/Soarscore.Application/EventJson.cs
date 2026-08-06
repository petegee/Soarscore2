// Shared JSON conventions for event contracts — LADR-0001 §4.5-8, LADR-0003.
//
// Every aggregate's events share these options rather than each defining its
// own, so the conventions below hold uniformly across the whole log:
//
//  - `$kind` discriminators (LADR-0003 spike finding 1), matching the Domain's
//    own ScoreTerm/Predicate/FlightSelection convention exactly.
//  - Decimals as JSON strings, never JSON numbers (LADR-0001 §4.6): defends
//    against a JS integrator round-tripping a score through `double`, jsonb
//    numeric normalisation, and SQLite NUMERIC affinity silently narrowing a
//    `decimal`. The ADR names the exposure as MeasuredValue.number and
//    DeclaredResult.aggregate specifically; DecimalAsStringConverter below
//    reaches every ordinary `decimal` property in an event payload, which is
//    the same protection applied uniformly rather than chased field by field.
//    The one gap: NumberOrParam.Literal's decimal is written by
//    NumberOrParamConverter directly (WriteNumberValue, not delegated through
//    JsonSerializer), so it never reaches this converter. That value is
//    rulebook configuration (a rate, a cap), not a score, and reopening
//    NumberOrParamConverter for it is out of scope here.
//  - NumberOrParam / FlagOrParam converters (Soarscore.Domain.CompetitionClasses)
//    registered, because AdoptedRules and ClassDefinitionPublished embed a full
//    ClassDefinition, and those two types cannot serialise without them
//    (ParameterReferenceConverters.cs).
//  - AllowOutOfOrderMetadataProperties (docs/plans/class-definition-adoption-steel-thread-plan.md
//    WI-7 finding): jsonb does not preserve key order, so a `ClassDefinitionPublished`
//    read back from Postgres can — and, empirically, does — land with a nested
//    ScoreTerm/Predicate/FlightSelection's `$kind` discriminator anywhere but
//    first in its object. Without this, .NET's default polymorphic reader
//    rejects the document outright ("must specify a type discriminator") even
//    though the discriminator is present, just not first — a store-backed test
//    is what caught this; the seed tool's own round-trip test never touches
//    Postgres and so never exercised jsonb's reordering.
//
// Event-type name <-> CLR-type mapping (Marten's MapEventType, LADR-0001 §4.8)
// is an Infrastructure/Marten concern and is not here: the $kind strings on
// each event union already ARE the logical names, declared once beside the
// contracts they name.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application;

public static class SoarscoreEventJson
{
    public static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowOutOfOrderMetadataProperties = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DecimalAsStringConverter());
        options.Converters.Add(new NumberOrParamConverter());
        options.Converters.Add(new FlagOrParamConverter());
        return options;
    }
}

/// <summary>LADR-0001 §4.6: a `decimal` in an event payload is always a JSON string.</summary>
public sealed class DecimalAsStringConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture)
            : reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
