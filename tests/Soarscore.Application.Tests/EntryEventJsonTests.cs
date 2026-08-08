using System.Text.Json;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Application.Tests;

public class EntryEventJsonTests
{
    private static readonly EntryId SampleId = EntryId.New();
    private static readonly CompetitionId SampleCompetition = CompetitionId.New();
    private static readonly GroupId SampleGroup = GroupId.New();
    private static readonly CompetitorId SampleCompetitor = CompetitorId.New();

    private static readonly TimeWindow SampleWorkingTime = new()
    {
        Start = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 1, 10, 9, 10, 0, TimeSpan.Zero),
    };

    private static EntryOpened SampleOpened(DateTimeOffset at) =>
        new(SampleId, SampleWorkingTime, SampleCompetition, 1, 1, 1, SampleGroup, SampleCompetitor, ReflightRole.Original, at);

    [Fact]
    public void Events_round_trip_through_SoarscoreEventJson_byte_for_byte()
    {
        EntryEvent opened = SampleOpened(DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(opened, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<EntryEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        reemitted.Should().Be(json);
        reread.Should().BeOfType<EntryOpened>();
    }

    [Fact]
    public void WorkingTime_with_no_end_round_trips_and_omits_the_end_property()
    {
        // WorkingTimeKind.UntilAllFlightsComplete (finding 2): End is null, not a
        // fabricated instant. A nullable DateTimeOffset has no custom converter —
        // unlike MeasuredValue.Number's decimal? — so this is its own code path.
        var openEnded = SampleWorkingTime with { End = null };
        EntryEvent opened = SampleOpened(DateTimeOffset.UtcNow) with { WorkingTime = openEnded };

        var json = JsonSerializer.Serialize(opened, SoarscoreEventJson.Options);

        json.Should().NotContain("\"end\"");

        var reread = JsonSerializer.Deserialize<EntryEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        reemitted.Should().Be(json);
        var rereadOpened = reread.Should().BeOfType<EntryOpened>().Which;
        rereadOpened.WorkingTime.End.Should().BeNull();
        rereadOpened.WorkingTime.Start.Should().Be(openEnded.Start);
    }

    [Fact]
    public void MeasurementCaptured_round_trips_and_carries_the_kind_discriminator()
    {
        EntryEvent captured = new MeasurementCaptured(
            1,
            new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(123.4500000m), CapturedAt = DateTimeOffset.UtcNow });

        var json = JsonSerializer.Serialize(captured, SoarscoreEventJson.Options);

        json.Should().Contain("\"$kind\":\"measurementCaptured\"");
        // LADR-0001 §4 item 6: decimals inside event JSON are strings, never numbers.
        json.Should().Contain("\"123.4500000\"");

        var reread = JsonSerializer.Deserialize<EntryEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);
        reemitted.Should().Be(json);
    }
}
