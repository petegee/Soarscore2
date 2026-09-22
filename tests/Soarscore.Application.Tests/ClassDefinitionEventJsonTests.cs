using System.Text.Json;
using AwesomeAssertions;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests;

public class ClassDefinitionEventJsonTests
{
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    [Fact]
    public void Events_round_trip_through_SoarscoreEventJson_byte_for_byte()
    {
        var hash = ClassDefinitionHashing.ComputeContentHash(SampleDefinition);
        ClassDefinitionEvent published = new ClassDefinitionPublished(hash, SampleDefinition, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(published, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<ClassDefinitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        reemitted.Should().Be(json);
        reread.Should().BeOfType<ClassDefinitionPublished>();
    }

    [Fact]
    public void Decimals_serialise_as_JSON_strings_not_numbers()
    {
        // LADR-0001 §4.6, isolated from any one field in the corpus: a bare
        // decimal, serialised through the shared event options, must be a JSON
        // string token — never a JSON number a JS client could parse as `double`.
        var json = JsonSerializer.Serialize(599.9999999m, SoarscoreEventJson.Options);

        json.Should().Be("\"599.9999999\"");

        var reread = JsonSerializer.Deserialize<decimal>(json, SoarscoreEventJson.Options);
        reread.Should().Be(599.9999999m);
    }

    [Fact]
    public void Published_event_serialises_with_the_kind_discriminator()
    {
        var hash = ClassDefinitionHashing.ComputeContentHash(SampleDefinition);
        ClassDefinitionEvent published = new ClassDefinitionPublished(hash, SampleDefinition, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(published, SoarscoreEventJson.Options);

        json.Should().Contain("\"$kind\":\"classDefinitionPublished\"");
    }

    // add-recorded-predicate.md WI-5: the third Predicate subtype rides the
    // same event-JSON contract — $kind: "isRecorded", metricRef carried —
    // through the real NDC F5J definition (85c-nz-f5j-ndc), whose flight gate
    // carries recorded("startHeight") (5.5.11.7 e, carried by NZ.0.3 c).

    [Fact]
    public void IsRecorded_predicate_round_trips_with_its_kind_discriminator_and_metricRef()
    {
        var definition = Corpus.All.Single(c => c.FileName == "85c-nz-f5j-ndc").Definition;
        ClassDefinitionEvent published = new ClassDefinitionPublished(
            ClassDefinitionHashing.ComputeContentHash(definition), definition, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(published, SoarscoreEventJson.Options);

        json.Should().Contain("\"$kind\":\"isRecorded\"");

        var reread = JsonSerializer.Deserialize<ClassDefinitionEvent>(json, SoarscoreEventJson.Options);
        JsonSerializer.Serialize(reread, SoarscoreEventJson.Options).Should().Be(json);

        var gate = reread.Should().BeOfType<ClassDefinitionPublished>().Which
            .Definition.Phases.SelectMany(p => p.Tasks).Single(t => t.Code == "D").FlightValidWhen;
        gate.Should().BeOfType<AllOf>().Which.Children
            .OfType<IsRecorded>().Should().ContainSingle().Which.MetricRef.Should().Be("startHeight");
    }
}
