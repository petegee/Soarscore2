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

    private static EntryOpened SampleOpened(DateTimeOffset at) =>
        new(SampleId, SampleCompetition, 1, 1, 1, SampleGroup, SampleCompetitor, ReflightRole.Original, at);

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

    [Fact]
    public void MeasurementCaptured_naming_an_instrument_round_trips_with_it()
    {
        EntryEvent captured = new MeasurementCaptured(
            1,
            new Measurement
            {
                Metric = "landingDistance",
                Value = MeasuredValue.Of(96m),
                Instrument = "nz-f3j-side",
                CapturedAt = DateTimeOffset.UtcNow,
            });

        var json = JsonSerializer.Serialize(captured, SoarscoreEventJson.Options);

        json.Should().Contain("\"instrument\":\"nz-f3j-side\"");
        var reread = JsonSerializer.Deserialize<EntryEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);
        reemitted.Should().Be(json);
        reread.Should().BeOfType<MeasurementCaptured>()
            .Which.Measurement.Instrument.Should().Be("nz-f3j-side");
    }

    [Fact]
    public void MeasurementAmended_carrying_an_instrument_round_trips_with_it()
    {
        EntryEvent amended = new MeasurementAmended(
            1,
            "landingDistance",
            new Amendment
            {
                NewValue = MeasuredValue.Of(91m),
                Instrument = "nz-f3j-side",
                Reason = "misread the tape",
                By = "the scorer",
                At = DateTimeOffset.UtcNow,
            });

        var json = JsonSerializer.Serialize(amended, SoarscoreEventJson.Options);

        json.Should().Contain("\"instrument\":\"nz-f3j-side\"");
        var reread = JsonSerializer.Deserialize<EntryEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);
        reemitted.Should().Be(json);
        reread.Should().BeOfType<MeasurementAmended>()
            .Which.Amendment.Instrument.Should().Be("nz-f3j-side");
    }

    [Fact]
    public void Legacy_Measurement_payload_without_instrument_deserialises_as_a_distance_naming_none()
    {
        // Pre-WI-3 payloads carry no instrument property at all (and
        // WhenWritingNull omits it for distance measurements still): both
        // shapes read back as a measurement naming none — the distance path.
        EntryEvent captured = new MeasurementCaptured(
            1,
            new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(120m), CapturedAt = DateTimeOffset.UtcNow });

        var json = JsonSerializer.Serialize(captured, SoarscoreEventJson.Options);

        json.Should().NotContain("instrument");
        var reread = (MeasurementCaptured)JsonSerializer.Deserialize<EntryEvent>(json, SoarscoreEventJson.Options)!;
        reread.Measurement.Instrument.Should().BeNull();
        reread.Measurement.EffectiveInstrument.Should().BeNull();
    }
}
