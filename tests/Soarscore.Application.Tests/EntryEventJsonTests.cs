using System.Text.Json;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Application.Tests;

public class EntryEventJsonTests
{
    private static readonly EntryId SampleId = EntryId.New();
    private static readonly GroupId SampleGroup = GroupId.New();
    private static readonly CompetitorId SampleCompetitor = CompetitorId.New();

    private static readonly TimeWindow SampleWorkingTime = new()
    {
        Start = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 1, 10, 9, 10, 0, TimeSpan.Zero),
    };

    private static EntryOpened SampleOpened(DateTimeOffset at) =>
        new(SampleId, SampleWorkingTime, SampleGroup, SampleCompetitor, ReflightRole.Original, at);

    [Fact]
    public void Events_round_trip_through_SoarscoreEventJson_byte_for_byte()
    {
        EntryEvent opened = SampleOpened(DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(opened, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<EntryEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        Assert.Equal(json, reemitted);
        Assert.IsType<EntryOpened>(reread);
    }

    [Fact]
    public void MeasurementCaptured_round_trips_and_carries_the_kind_discriminator()
    {
        EntryEvent captured = new MeasurementCaptured(
            1,
            new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(123.4500000m), CapturedAt = DateTimeOffset.UtcNow });

        var json = JsonSerializer.Serialize(captured, SoarscoreEventJson.Options);

        Assert.Contains("\"$kind\":\"measurementCaptured\"", json);
        // LADR-0001 §4 item 6: decimals inside event JSON are strings, never numbers.
        Assert.Contains("\"123.4500000\"", json);

        var reread = JsonSerializer.Deserialize<EntryEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);
        Assert.Equal(json, reemitted);
    }
}
