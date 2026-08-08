using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Entry.CaptureMeasurement"/> —
/// docs/plans/capture-a-score-steel-thread-plan.md WI-4. One per failure
/// code, plus success for both MeasuredKind variants, plus rounding applied
/// at each RoundingMode — Truncate in particular, against an F3K-shaped
/// 0.1 s flightTime metric (finding 4).
/// </summary>
public class CaptureMeasurementDecideTests
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

    private static readonly MetricDefinition FlightTimeMetric = new()
    {
        Name = "flightTime",
        Kind = MeasuredKind.Number,
        Unit = "s",
        Precision = new Rounding(RoundingMode.Truncate, 0.1m),
    };

    private static readonly MetricDefinition LandedInDefinedAreaMetric = new()
    {
        Name = "landedInDefinedArea",
        Kind = MeasuredKind.Flag,
    };

    private static readonly ImmutableArray<MetricDefinition> SampleMetrics =
        [FlightTimeMetric, LandedInDefinedAreaMetric];

    private static Entry EntryWithOneOpenFlight()
    {
        var entry = Entry.Create(new EntryOpened(
            SampleId, SampleWorkingTime, SampleCompetition, 1, 1, 1,
            SampleGroup, SampleCompetitor, ReflightRole.Original, DateTimeOffset.UtcNow));

        return entry.Apply(new FlightOpened(1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CaptureMeasurement_against_an_annulled_entry_fails_with_a_stable_code()
    {
        var entry = EntryWithOneOpenFlight().Apply(new EntryAnnulled(
            new Annulment { Reason = "n/a", By = "Jury", At = DateTimeOffset.UtcNow }));

        var result = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(120m), DateTimeOffset.UtcNow, SampleMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.annulled");
    }

    [Fact]
    public void CaptureMeasurement_against_a_flight_that_was_never_opened_fails_with_a_stable_code()
    {
        var entry = EntryWithOneOpenFlight();

        var result = entry.CaptureMeasurement(2, "flightTime", MeasuredValue.Of(120m), DateTimeOffset.UtcNow, SampleMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("captureMeasurement.flightNotFound");
    }

    [Fact]
    public void CaptureMeasurement_against_a_metric_not_declared_by_the_task_fails_with_a_stable_code()
    {
        var entry = EntryWithOneOpenFlight();

        var result = entry.CaptureMeasurement(1, "windSpeed", MeasuredValue.Of(4m), DateTimeOffset.UtcNow, SampleMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("captureMeasurement.metricNotDeclared");
    }

    [Fact]
    public void CaptureMeasurement_with_a_value_kind_that_does_not_match_the_metric_fails_with_a_stable_code()
    {
        var entry = EntryWithOneOpenFlight();

        // flightTime is a Number metric; a Flag value is the wrong kind.
        var result = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(true), DateTimeOffset.UtcNow, SampleMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("captureMeasurement.kindMismatch");
    }

    [Fact]
    public void CaptureMeasurement_a_second_time_for_the_same_metric_fails_with_a_stable_code()
    {
        var entry = EntryWithOneOpenFlight();
        var first = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(120m), DateTimeOffset.UtcNow, SampleMetrics);
        first.IsSuccess.Should().BeTrue();
        entry = entry.Apply(first.Value);

        var second = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(121m), DateTimeOffset.UtcNow, SampleMetrics);

        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("captureMeasurement.alreadyCaptured");
    }

    [Fact]
    public void CaptureMeasurement_succeeds_for_a_Number_metric()
    {
        var entry = EntryWithOneOpenFlight();
        var capturedAt = new DateTimeOffset(2026, 1, 10, 9, 4, 0, TimeSpan.Zero);

        // Metric with no Precision so the stored value is untouched — rounding is its own set of tests below.
        var metrics = ImmutableArray.Create(FlightTimeMetric with { Precision = null });

        var result = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(120m), capturedAt, metrics);

        result.IsSuccess.Should().BeTrue();
        result.Value.FlightSequence.Should().Be(1);
        result.Value.Measurement.Metric.Should().Be("flightTime");
        result.Value.Measurement.Value.Should().Be(MeasuredValue.Of(120m));
        result.Value.Measurement.CapturedAt.Should().Be(capturedAt);
    }

    [Fact]
    public void CaptureMeasurement_succeeds_for_a_Flag_metric()
    {
        var entry = EntryWithOneOpenFlight();
        var capturedAt = DateTimeOffset.UtcNow;

        var result = entry.CaptureMeasurement(1, "landedInDefinedArea", MeasuredValue.Of(true), capturedAt, SampleMetrics);

        result.IsSuccess.Should().BeTrue();
        result.Value.Measurement.Metric.Should().Be("landedInDefinedArea");
        result.Value.Measurement.Value.Should().Be(MeasuredValue.Of(true));
    }

    [Fact]
    public void CaptureMeasurement_truncates_an_F3K_shaped_0_1s_metric_per_its_declared_precision()
    {
        var entry = EntryWithOneOpenFlight();

        // SampleMetrics' flightTime is RoundingMode.Truncate at 0.1 precision (F3K.7).
        var result = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(123.47m), DateTimeOffset.UtcNow, SampleMetrics);

        result.IsSuccess.Should().BeTrue();
        result.Value.Measurement.Value.Number.Should().Be(123.4m);
    }

    [Fact]
    public void CaptureMeasurement_rounds_HalfUp_per_its_declared_precision()
    {
        var entry = EntryWithOneOpenFlight();
        var metrics = ImmutableArray.Create(
            FlightTimeMetric with { Precision = new Rounding(RoundingMode.HalfUp, 0.1m) });

        var result = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(123.45m), DateTimeOffset.UtcNow, metrics);

        result.IsSuccess.Should().BeTrue();
        result.Value.Measurement.Value.Number.Should().Be(123.5m);
    }

    [Fact]
    public void CaptureMeasurement_rounds_Ceiling_per_its_declared_precision()
    {
        var entry = EntryWithOneOpenFlight();
        var metrics = ImmutableArray.Create(
            FlightTimeMetric with { Precision = new Rounding(RoundingMode.Ceiling, 0.1m) });

        var result = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(123.41m), DateTimeOffset.UtcNow, metrics);

        result.IsSuccess.Should().BeTrue();
        result.Value.Measurement.Value.Number.Should().Be(123.5m);
    }

    [Fact]
    public void CaptureMeasurement_does_not_round_a_Flag_metric_because_it_has_no_Precision()
    {
        var entry = EntryWithOneOpenFlight();

        var result = entry.CaptureMeasurement(1, "landedInDefinedArea", MeasuredValue.Of(false), DateTimeOffset.UtcNow, SampleMetrics);

        result.IsSuccess.Should().BeTrue();
        result.Value.Measurement.Value.Should().Be(MeasuredValue.Of(false));
    }
}
