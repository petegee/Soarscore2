using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Entry.AmendMeasurement"/> —
/// kanban/completed/amend-a-measurement.md WI-2. One example per failure code,
/// plus the happy path asserting the emitted <see cref="Amendment"/> carries the
/// rounded value, the reason, the by and the clock's instant. Mirrors
/// CaptureMeasurementDecideTests.cs: amendment is the correcting counterpart of
/// capture, and the two defect sets together make the capture/amend pair total.
///
/// Home to property tests P1–P4 (this WI, named during planning per CLAUDE.md's
/// testing approach). All four assert that the capture and amend write paths
/// agree, which is exactly where a second fold can silently diverge from the
/// first.
/// </summary>
public class AmendMeasurementDecideTests
{
    private static readonly EntryId SampleId = EntryId.New();
    private static readonly CompetitionId SampleCompetition = CompetitionId.New();
    private static readonly GroupId SampleGroup = GroupId.New();
    private static readonly CompetitorId SampleCompetitor = CompetitorId.New();

    /// <summary>F3K-shaped Truncate 0.1 s flightTime — the same metric CaptureMeasurementDecideTests uses.</summary>
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

    private static Entry WithOneOpenFlight() =>
        Entry.Create(new EntryOpened(
            SampleId, SampleCompetition, 1, 1, 1,
            SampleGroup, SampleCompetitor, ReflightRole.Original, DateTimeOffset.UtcNow))
        .Apply(new FlightOpened(1, DateTimeOffset.UtcNow));

    /// <summary>An entry with one open flight and a flightTime Measurement already captured on it.</summary>
    private static Entry WithCapturedFlightTime(decimal value, ImmutableArray<MetricDefinition> metrics)
    {
        var entry = WithOneOpenFlight();
        var captured = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(value), DateTimeOffset.UtcNow, metrics);
        captured.IsSuccess.Should().BeTrue();
        return entry.Apply(captured.Value);
    }

    // ------------------------------------------------------------------- FAILURES

    [Fact]
    public void AmendMeasurement_against_an_annulled_entry_fails_with_a_stable_code()
    {
        var entry = WithCapturedFlightTime(120m, SampleMetrics).Apply(new EntryAnnulled(
            new Annulment { Reason = "n/a", By = "Jury", At = DateTimeOffset.UtcNow }));

        var result = entry.AmendMeasurement(1, "flightTime", MeasuredValue.Of(121m),
            "mistype", "the scorer", DateTimeOffset.UtcNow, SampleMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.annulled");
    }

    [Fact]
    public void AmendMeasurement_against_a_flight_that_was_never_opened_fails_with_a_stable_code()
    {
        var entry = WithCapturedFlightTime(120m, SampleMetrics);

        var result = entry.AmendMeasurement(2, "flightTime", MeasuredValue.Of(121m),
            "mistype", "the scorer", DateTimeOffset.UtcNow, SampleMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("amendMeasurement.flightNotFound");
    }

    [Fact]
    public void AmendMeasurement_against_a_metric_with_no_captured_value_fails_with_a_stable_code()
    {
        var entry = WithOneOpenFlight();

        var result = entry.AmendMeasurement(1, "flightTime", MeasuredValue.Of(121m),
            "mistype", "the scorer", DateTimeOffset.UtcNow, SampleMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("amendMeasurement.notCaptured");
    }

    [Fact]
    public void AmendMeasurement_against_a_metric_no_longer_declared_fails_with_a_stable_code()
    {
        // flightTime WAS captured (so notCaptured passes), but the metrics
        // array handed here no longer declares it — reachable only where a
        // definition has changed since capture (RulesAmended); kept so that
        // path cannot land silently.
        var entry = WithCapturedFlightTime(120m, SampleMetrics);
        var shrunkMetrics = ImmutableArray.Create(LandedInDefinedAreaMetric);

        var result = entry.AmendMeasurement(1, "flightTime", MeasuredValue.Of(121m),
            "mistype", "the scorer", DateTimeOffset.UtcNow, shrunkMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("amendMeasurement.metricNotDeclared");
    }

    [Fact]
    public void AmendMeasurement_with_a_value_kind_that_does_not_match_the_metric_fails_with_a_stable_code()
    {
        var entry = WithCapturedFlightTime(120m, SampleMetrics);

        // flightTime is a Number metric; a Flag value is the wrong kind.
        var result = entry.AmendMeasurement(1, "flightTime", MeasuredValue.Of(true),
            "mistype", "the scorer", DateTimeOffset.UtcNow, SampleMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("amendMeasurement.kindMismatch");
    }

    [Fact]
    public void AmendMeasurement_with_a_blank_reason_fails_with_a_stable_code()
    {
        var entry = WithCapturedFlightTime(120m, SampleMetrics);

        var result = entry.AmendMeasurement(1, "flightTime", MeasuredValue.Of(121m),
            "   ", "the scorer", DateTimeOffset.UtcNow, SampleMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("amendMeasurement.reasonRequired");
    }

    [Fact]
    public void AmendMeasurement_with_a_blank_by_fails_with_a_stable_code()
    {
        var entry = WithCapturedFlightTime(120m, SampleMetrics);

        var result = entry.AmendMeasurement(1, "flightTime", MeasuredValue.Of(121m),
            "mistype", "  ", DateTimeOffset.UtcNow, SampleMetrics);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("amendMeasurement.byRequired");
    }

    // -------------------------------------------------------------------- SUCCESS

    [Fact]
    public void AmendMeasurement_succeeds_carrying_rounded_value_reason_by_and_at()
    {
        var entry = WithCapturedFlightTime(120m, SampleMetrics);
        var at = new DateTimeOffset(2026, 1, 10, 9, 5, 30, TimeSpan.Zero);

        var result = entry.AmendMeasurement(1, "flightTime", MeasuredValue.Of(121.47m),
            "fat-fingered the flight time", "the contest director", at, SampleMetrics);

        result.IsSuccess.Should().BeTrue();
        result.Value.FlightSequence.Should().Be(1);
        result.Value.Metric.Should().Be("flightTime");
        // Rounded by the metric's declared precision, exactly as capture would.
        result.Value.Amendment.NewValue.Number.Should().Be(121.4m);
        result.Value.Amendment.Reason.Should().Be("fat-fingered the flight time");
        result.Value.Amendment.By.Should().Be("the contest director");
        // The instant is the caller's (the handler supplies IClock's), not invented here.
        result.Value.Amendment.At.Should().Be(at);
    }

    [Fact]
    public void Amending_a_Flag_kind_value_does_not_round_it_because_it_has_no_Precision()
    {
        var entry = WithOneOpenFlight();
        var captured = entry.CaptureMeasurement(
            1, "landedInDefinedArea", MeasuredValue.Of(false), DateTimeOffset.UtcNow, SampleMetrics);
        entry = entry.Apply(captured.Value);

        var result = entry.AmendMeasurement(1, "landedInDefinedArea", MeasuredValue.Of(true),
            "wrong tap on the tablet", "the scorer", DateTimeOffset.UtcNow, SampleMetrics);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amendment.NewValue.Should().Be(MeasuredValue.Of(true));
    }

    // ======================================================================= PROPERTY TESTS — P1..P4
    // The four invariants named in planning (WI-2). All four exercise the
    // capture-then-fold-then-amend path end to end — the fold is what an
    // accepted write replays, so a property that only looked at the decide
    // function's return value could believe two folds agree where the fold
    // disagrees.

    private static readonly DateTimeOffset Base = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly Gen<decimal> DecimalGen =
        Gen.Int[0, 100_000].Select(i => i / 100m);

    /// <summary>A (number, intended-correction-instant) pair for one amendment.</summary>
    private sealed record AmendmentFact(decimal Number, DateTimeOffset At);

    /// <summary>
    /// Narrow At offsets (0..20 s) deliberately raise the chance of colliding
    /// instants, so the tie-break half of the resolution rule (last-appended
    /// wins) is exercised, not only the strict-inequality half.
    /// </summary>
    private static readonly Gen<AmendmentFact> AmendmentFactGen =
        from offset in Gen.Int[0, 20]
        from number in DecimalGen
        select new AmendmentFact(number, Base.AddSeconds(offset));

    /// <summary>Folds a capture followed by amendments onto a fresh entry's first flight.</summary>
    private static Entry FoldAndAmend(
        decimal original, IReadOnlyList<AmendmentFact> facts, ImmutableArray<MetricDefinition> metrics)
    {
        var entry = WithOneOpenFlight();
        var captured = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(original), Base, metrics);
        captured.IsSuccess.Should().BeTrue();
        entry = entry.Apply(captured.Value);

        foreach (var fact in facts)
        {
            var decision = entry.AmendMeasurement(1, "flightTime", MeasuredValue.Of(fact.Number),
                "correction", "contest director", fact.At, metrics);
            decision.IsSuccess.Should().BeTrue();
            entry = entry.Apply(decision.Value);
        }

        return entry;
    }

    /// <summary>The effective amendment: greatest At, ties to the last-appended (highest index).</summary>
    private static AmendmentFact Latest(IReadOnlyList<AmendmentFact> facts) =>
        facts
            .Select((f, i) => (Fact: f, Index: i))
            .OrderByDescending(x => x.Fact.At)
            .ThenByDescending(x => x.Index)
            .First().Fact;

    /// <summary>Resolving a fresh measurement captured once from that number.</summary>
    private static MeasuredValue ResolvedIfCapturedOnce(decimal number) =>
        MeasurementDigest.Resolve(WithCapturedFlightTime(number, SampleMetrics).Flights[0]).Metrics["flightTime"];

    // ------------------------------------------------------- P1
    // An amendment is indistinguishable from having captured the right value.
    // For any original and non-empty sequence of amendments, resolving the
    // amended measurement equals resolving a measurement captured once with
    // the latest amendment's value. A reader must never tell a corrected
    // number from a right-first-time one.

    [Fact]
    public void An_amendment_is_indistinguishable_from_having_captured_the_right_value()
    {
        (from initialValue in DecimalGen
         from facts in AmendmentFactGen.Array[1, 5]
         select (initialValue, facts))
        .Sample(t =>
        {
            var amended = FoldAndAmend(t.initialValue, t.facts, SampleMetrics);
            var expected = ResolvedIfCapturedOnce(Latest(t.facts).Number);

            MeasurementDigest.Resolve(amended.Flights[0]).Metrics["flightTime"]
                .Should().Be(expected);
        });
    }

    // ------------------------------------------------------- P2
    // Amending never destroys history: folding n amendments leaves the folded
    // Measurement.Value at the originally captured value and
    // Amendments.Length == n, with every appended Amendment present in order.

    [Fact]
    public void Amending_never_destroys_history()
    {
        (from original in DecimalGen
         from facts in AmendmentFactGen.Array[1, 5]
         select (original, facts))
        .Sample(t =>
        {
            var entry = WithOneOpenFlight();
            var captured = entry.CaptureMeasurement(1, "flightTime", MeasuredValue.Of(t.original), Base, SampleMetrics);
            captured.IsSuccess.Should().BeTrue();
            entry = entry.Apply(captured.Value);

            var appended = new List<Amendment>();
            foreach (var fact in t.facts)
            {
                var decision = entry.AmendMeasurement(1, "flightTime", MeasuredValue.Of(fact.Number),
                    "correction", "reporter", fact.At, SampleMetrics);
                decision.IsSuccess.Should().BeTrue();
                appended.Add(decision.Value.Amendment);
                entry = entry.Apply(decision.Value);
            }

            var measurement = entry.Flights[0].Measurements.Single(m => m.Metric == "flightTime");
            // The original survives unaltered — amend folds beside, not over.
            measurement.Value.Should().Be(captured.Value.Measurement.Value);
            measurement.Amendments.Length.Should().Be(appended.Count);
            // Every appended Amendment present, and in append order.
            measurement.Amendments.Should().Equal(appended);
        });
    }

    // ------------------------------------------------------- P3
    // Capture and amendment round identically: for any two.sample value, the
    // NewValue on the emitted MeasurementAmended equals the Value on the
    // MeasurementCaptured that the same input would produce. Guards the one
    // place the two write paths can drift (finding 5).

    [Fact]
    public void Capture_and_amendment_round_identically()
    {
        DecimalGen.Sample(value =>
        {
            var capture = WithOneOpenFlight().CaptureMeasurement(
                1, "flightTime", MeasuredValue.Of(value), Base, SampleMetrics);
            var amendment = WithCapturedFlightTime(value, SampleMetrics).AmendMeasurement(
                1, "flightTime", MeasuredValue.Of(value), "correction", "scorer", Base, SampleMetrics);

            amendment.Value.Amendment.NewValue.Should().Be(capture.Value.Measurement.Value);
        });
    }

    // ------------------------------------------------------- P4
    // Resolution follows At, not append order. For a sequence of amendments
    // with out-of-order arrivals and colliding At, the resolved value is the
    // one with the greatest At; ties go to the last appended. This is
    // MeasurementDigest's documented rule, exercised here through the write
    // path against an adversarial ordering.

    [Fact]
    public void Amended_resolution_follows_At_then_append_order()
    {
        (from originalValue in DecimalGen
         from facts in AmendmentFactGen.Array[1, 5]
         select (originalValue, facts))
        .Sample(t =>
        {
            var amended = FoldAndAmend(t.originalValue, t.facts, SampleMetrics);
            var expected = ResolvedIfCapturedOnce(Latest(t.facts).Number);

            MeasurementDigest.Resolve(amended.Flights[0]).Metrics["flightTime"]
                .Should().Be(expected);
        });
    }
}