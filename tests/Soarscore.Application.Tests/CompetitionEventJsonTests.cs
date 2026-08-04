using System.Text.Json;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.CompetitionClasses;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests;

public class CompetitionEventJsonTests
{
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = SampleDefinition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SampleDefinition.Version,
            AdoptedAt = DateTimeOffset.UtcNow,
        };

    private static CompetitionCreated SampleCreatedEvent(DateTimeOffset? at = null) =>
        new(
            CompetitionId.New(),
            "Club Champs 2026",
            "Auckland",
            new DateOnly(2026, 3, 14),
            new DateOnly(2026, 3, 15),
            "1.0.0",
            SampleAdoptedRules(),
            at ?? DateTimeOffset.UtcNow);

    [Fact]
    public void Events_round_trip_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent created = SampleCreatedEvent();

        var json = JsonSerializer.Serialize(created, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        Assert.Equal(json, reemitted);
        Assert.IsType<CompetitionCreated>(reread);
    }

    [Fact]
    public void Created_event_serialises_with_the_kind_discriminator()
    {
        CompetitionEvent created = SampleCreatedEvent();

        var json = JsonSerializer.Serialize(created, SoarscoreEventJson.Options);

        Assert.Contains("\"$kind\":\"competitionCreated\"", json);
    }

    [Fact]
    public void Finalised_event_round_trips_with_decimal_aggregate_as_a_json_string()
    {
        var finalisation = new Finalisation
        {
            Scope = FinalisationScope.Competition,
            Revision = 1,
            By = "CD",
            At = DateTimeOffset.UtcNow,
            DeclaredResults =
            [
                new DeclaredResult
                {
                    CompetitorRef = CompetitorId.New(),
                    Aggregate = 599.9999999m,
                    Placing = 1,
                    Promoted = true,
                },
            ],
        };
        CompetitionEvent finalised = new Finalised(finalisation);

        var json = JsonSerializer.Serialize(finalised, SoarscoreEventJson.Options);

        Assert.Contains("\"aggregate\":\"599.9999999\"", json);

        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);
        Assert.Equal(json, reemitted);
    }
}
