using System.Text.Json;
using Soarscore.Application.CompetitionClasses;
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

        Assert.Equal(json, reemitted);
        Assert.IsType<ClassDefinitionPublished>(reread);
    }

    [Fact]
    public void Decimals_serialise_as_JSON_strings_not_numbers()
    {
        // LADR-0001 §4.6, isolated from any one field in the corpus: a bare
        // decimal, serialised through the shared event options, must be a JSON
        // string token — never a JSON number a JS client could parse as `double`.
        var json = JsonSerializer.Serialize(599.9999999m, SoarscoreEventJson.Options);

        Assert.Equal("\"599.9999999\"", json);

        var reread = JsonSerializer.Deserialize<decimal>(json, SoarscoreEventJson.Options);
        Assert.Equal(599.9999999m, reread);
    }

    [Fact]
    public void Published_event_serialises_with_the_kind_discriminator()
    {
        var hash = ClassDefinitionHashing.ComputeContentHash(SampleDefinition);
        ClassDefinitionEvent published = new ClassDefinitionPublished(hash, SampleDefinition, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(published, SoarscoreEventJson.Options);

        Assert.Contains("\"$kind\":\"classDefinitionPublished\"", json);
    }
}
