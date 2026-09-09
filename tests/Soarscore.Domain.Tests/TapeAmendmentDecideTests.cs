// kanban/backlog/tape-points-landing-seeds.md WI-3 — the capture/amendment
// instrument transitions the shared proof file does not pin: the wrong value
// kind against a named instrument, every amendment instrument transition
// (reading to reading, distance to reading, reading to another tape, reading
// back to none with the distance precision re-applied), and the effective
// instrument across a correction history. The declaration, reading-set and
// mixed-form properties live in TapeLandingScaleProofTests.cs.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

public class TapeAmendmentDecideTests
{
    private static readonly EntryId SampleId = EntryId.New();
    private static readonly CompetitionId SampleCompetition = CompetitionId.New();
    private static readonly GroupId SampleGroup = GroupId.New();
    private static readonly CompetitorId SampleCompetitor = CompetitorId.New();

    private static readonly MetricDefinition LandingDistanceMetric = new()
    {
        Name = "landingDistance",
        Kind = MeasuredKind.Number,
        Unit = "m",
        Precision = new Rounding(RoundingMode.Truncate, 0.1m),
    };

    private static readonly MetricDefinition FlightTimeMetric = new()
    {
        Name = "flightTime",
        Kind = MeasuredKind.Number,
        Unit = "s",
        Precision = new Rounding(RoundingMode.Truncate, 1m),
    };

    private static readonly ImmutableArray<MetricDefinition> SampleMetrics =
        [LandingDistanceMetric, FlightTimeMetric];

    private static readonly ReadingScale F3JSide = new()
    {
        Unit = "m",
        Marks =
        [
            new ScaleMark(0.2m, 100m), new ScaleMark(0.4m, 99m), new ScaleMark(1m, 96m),
            new ScaleMark(2m, 91m), new ScaleMark(3m, 90m), new ScaleMark(15m, 30m),
        ],
        OffScaleReading = 0m,
    };

    private static readonly ReadingScale OtherSide = new()
    {
        Unit = "m",
        Marks = [new ScaleMark(1m, 50m), new ScaleMark(10m, 5m)],
        OffScaleReading = null,
    };

    private static readonly ImmutableArray<DeclaredInstrument> Declared =
    [
        new DeclaredInstrument { Instrument = "nz-f3j-side", Metric = "landingDistance", Scale = F3JSide },
        new DeclaredInstrument { Instrument = "other-side", Metric = "landingDistance", Scale = OtherSide },
    ];

    private static Entry WithOneOpenFlight() =>
        Entry.Create(new EntryOpened(
            SampleId, SampleCompetition, 1, 1, 1,
            SampleGroup, SampleCompetitor, ReflightRole.Original, DateTimeOffset.UtcNow))
        .Apply(new FlightOpened(1, DateTimeOffset.UtcNow));

    private static Entry WithCaptured(
        string metric, MeasuredValue value, string? instrument, ImmutableArray<DeclaredInstrument> declared)
    {
        var entry = WithOneOpenFlight();
        var captured = entry.CaptureMeasurement(1, metric, value, DateTimeOffset.UtcNow, SampleMetrics, instrument, declared);
        captured.IsSuccess.Should().BeTrue($"{captured.Code}: {captured.Message}");
        return entry.Apply(captured.Value);
    }

    [Fact]
    public void Capture_with_the_wrong_value_kind_against_a_named_instrument_fails_with_kindMismatch()
    {
        var entry = WithOneOpenFlight();

        var result = entry.CaptureMeasurement(
            1, "landingDistance", MeasuredValue.Of(true), DateTimeOffset.UtcNow, SampleMetrics, "nz-f3j-side", Declared);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("captureMeasurement.kindMismatch");
    }

    [Fact]
    public void Amend_with_the_wrong_value_kind_against_a_named_instrument_fails_with_kindMismatch()
    {
        var entry = WithCaptured("landingDistance", MeasuredValue.Of(96m), "nz-f3j-side", Declared);

        var result = entry.AmendMeasurement(
            1, "landingDistance", MeasuredValue.Of(false), "mistype", "the scorer",
            DateTimeOffset.UtcNow, SampleMetrics, "nz-f3j-side", Declared);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("amendMeasurement.kindMismatch");
    }

    [Fact]
    public void Amend_reading_to_reading_restates_the_instrument_with_no_rounding()
    {
        var entry = WithCaptured("landingDistance", MeasuredValue.Of(96m), "nz-f3j-side", Declared);

        var result = entry.AmendMeasurement(
            1, "landingDistance", MeasuredValue.Of(100m), "misread the tape", "the scorer",
            DateTimeOffset.UtcNow, SampleMetrics, "nz-f3j-side", Declared);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amendment.NewValue.Should().Be(MeasuredValue.Of(100m));
        result.Value.Amendment.Instrument.Should().Be("nz-f3j-side");

        var amended = entry.Apply(result.Value);
        var measurement = amended.Flights.Single().Measurements.Single(m => m.Metric == "landingDistance");
        measurement.Amendments.Should().ContainSingle(a => a.NewValue == MeasuredValue.Of(100m));
        measurement.EffectiveInstrument.Should().Be("nz-f3j-side");
    }

    [Fact]
    public void Amend_distance_to_reading_switches_the_instrument()
    {
        var entry = WithCaptured("landingDistance", MeasuredValue.Of(1.5m), null, []);

        var result = entry.AmendMeasurement(
            1, "landingDistance", MeasuredValue.Of(91m), "was read off the tape after all", "the scorer",
            DateTimeOffset.UtcNow, SampleMetrics, "nz-f3j-side", Declared);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amendment.Instrument.Should().Be("nz-f3j-side");
        entry.Apply(result.Value).Flights.Single().Measurements
            .Single(m => m.Metric == "landingDistance").EffectiveInstrument.Should().Be("nz-f3j-side");
    }

    [Fact]
    public void Amend_reading_to_another_tape_validates_against_the_new_scale()
    {
        var entry = WithCaptured("landingDistance", MeasuredValue.Of(96m), "nz-f3j-side", Declared);

        // 96 is on the F3J side but not on the other side — the amendment is refused.
        var refused = entry.AmendMeasurement(
            1, "landingDistance", MeasuredValue.Of(96m), "wrong side", "the scorer",
            DateTimeOffset.UtcNow, SampleMetrics, "other-side", Declared);

        refused.IsFailure.Should().BeTrue();
        refused.Code.Should().Be("amendMeasurement.readingNotOnScale");

        // 50 is on the other side — the switch lands.
        var switched = entry.AmendMeasurement(
            1, "landingDistance", MeasuredValue.Of(50m), "wrong side", "the scorer",
            DateTimeOffset.UtcNow, SampleMetrics, "other-side", Declared);

        switched.IsSuccess.Should().BeTrue();
        entry.Apply(switched.Value).Flights.Single().Measurements
            .Single(m => m.Metric == "landingDistance").EffectiveInstrument.Should().Be("other-side");
    }

    [Fact]
    public void Amend_reading_back_to_none_reapplies_the_distance_precision()
    {
        var entry = WithCaptured("landingDistance", MeasuredValue.Of(96m), "nz-f3j-side", Declared);

        // Clearing to none is a distance again: Truncate 0.1 re-applies.
        var result = entry.AmendMeasurement(
            1, "landingDistance", MeasuredValue.Of(1.57m), "was taped, not read", "the scorer",
            DateTimeOffset.UtcNow, SampleMetrics, null, Declared);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amendment.NewValue.Number.Should().Be(1.5m);
        result.Value.Amendment.Instrument.Should().BeNull();

        var measurement = entry.Apply(result.Value).Flights.Single().Measurements
            .Single(m => m.Metric == "landingDistance");
        measurement.EffectiveInstrument.Should().BeNull();
        measurement.Amendments[^1].NewValue.Number.Should().Be(1.5m);
    }

    [Fact]
    public void Amend_naming_an_undeclared_instrument_appends_nothing()
    {
        var entry = WithCaptured("landingDistance", MeasuredValue.Of(1.5m), null, []);
        var before = entry;

        var result = entry.AmendMeasurement(
            1, "landingDistance", MeasuredValue.Of(96m), "guess", "the scorer",
            DateTimeOffset.UtcNow, SampleMetrics, "nz-f3j-side", []);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("amendMeasurement.instrumentNotDeclared");
        entry.Should().Be(before, "an invalid amendment appends no event, so the state is untouched");
        entry.Flights.Single().Measurements.Single().Amendments.Should().BeEmpty();
    }

    [Fact]
    public void Effective_instrument_follows_the_latest_restatement_across_two_amendments()
    {
        var entry = WithCaptured("landingDistance", MeasuredValue.Of(1.5m), null, []);

        var first = entry.AmendMeasurement(
            1, "landingDistance", MeasuredValue.Of(96m), "read it", "the scorer",
            DateTimeOffset.UtcNow, SampleMetrics, "nz-f3j-side", Declared);
        first.IsSuccess.Should().BeTrue();
        entry = entry.Apply(first.Value);

        var second = entry.AmendMeasurement(
            1, "landingDistance", MeasuredValue.Of(2.5m), "taped instead", "the scorer",
            DateTimeOffset.UtcNow, SampleMetrics, null, Declared);
        second.IsSuccess.Should().BeTrue();
        entry = entry.Apply(second.Value);

        var measurement = entry.Flights.Single().Measurements.Single(m => m.Metric == "landingDistance");
        measurement.Amendments.Should().HaveCount(2, "correction history is retained, never overwritten");
        measurement.EffectiveInstrument.Should().BeNull("the latest restatement wins");
        measurement.Amendments[^1].NewValue.Number.Should().Be(2.5m);
    }
}
