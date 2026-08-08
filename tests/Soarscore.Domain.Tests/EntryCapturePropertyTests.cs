using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property tests for the Entry capture decide functions
/// (<see cref="Entry.OpenFlight"/>, <see cref="Entry.CaptureMeasurement"/>) —
/// docs/plans/capture-a-score-steel-thread-plan.md WI-5. Five invariants,
/// each its own named test so a failure names the invariant that broke, not
/// just "a test failed".
///
/// Invariants 3 and 4 are generic over <see cref="Corpus.All"/> rather than a
/// hard-coded class list — the same technique BindParameterPropertyTests'
/// property 5 uses — because that is how CLAUDE.md's core architectural law
/// ("the core system must not know about any specific competition class") is
/// asserted rather than assumed.
/// </summary>
public class EntryCapturePropertyTests
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

    private static Entry OpenSampleEntry() =>
        Entry.Create(new EntryOpened(
            SampleId, SampleWorkingTime, SampleCompetition, 1, 1, 1,
            SampleGroup, SampleCompetitor, ReflightRole.Original, DateTimeOffset.UtcNow));

    private static Entry OpenSampleEntryWithOneFlight() =>
        OpenSampleEntry().Apply(new FlightOpened(1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

    // ============================================================ invariant 1
    // Capture is append-only: folding the events an accepted OpenFlight /
    // CaptureMeasurement decision produces never removes or alters a
    // previously recorded Measurement — Flight.Measurements only grows, and
    // every earlier element stays exactly as it was. Entry.cs:76-81 and
    // aggregate-roots.md §4 both state this in prose; this is the first test
    // to exercise it against the write path.

    private enum StepKind { OpenFlight, CaptureMeasurement }

    private static readonly ImmutableArray<string> SampleMetricNames = ["flightTime", "landingBonus", "distance"];

    private static readonly ImmutableArray<MetricDefinition> SampleMetricDefs =
        [.. SampleMetricNames.Select(name => new MetricDefinition { Name = name, Kind = MeasuredKind.Number })];

    private static readonly Gen<(StepKind Kind, int Pick, int MetricIndex)> AppendOnlyStep =
        from kind in Gen.OneOfConst(StepKind.OpenFlight, StepKind.CaptureMeasurement)
        from pick in Gen.Int[0, 999]
        from metricIndex in Gen.Int[0, SampleMetricNames.Length - 1]
        select (kind, pick, metricIndex);

    [Fact]
    public void Capture_is_append_only()
    {
        AppendOnlyStep.Array[0, 60].Sample(steps =>
        {
            var entry = OpenSampleEntry();

            foreach (var step in steps)
            {
                var before = entry.Flights;

                entry = step.Kind == StepKind.OpenFlight
                    ? ApplyOpenFlightStep(entry)
                    : ApplyCaptureMeasurementStep(entry, step.Pick, step.MetricIndex);

                var after = entry.Flights;

                // Never shrinks.
                after.Length.Should().BeGreaterThanOrEqualTo(before.Length);

                // Every flight present before is unchanged in its own fields,
                // and its Measurements only ever grows — never loses or
                // rewrites an earlier element.
                for (var i = 0; i < before.Length; i++)
                {
                    var flightBefore = before[i];
                    var flightAfter = after[i];

                    flightAfter.Sequence.Should().Be(flightBefore.Sequence);
                    flightAfter.LaunchAt.Should().Be(flightBefore.LaunchAt);
                    flightAfter.Measurements.Length.Should().BeGreaterThanOrEqualTo(flightBefore.Measurements.Length);

                    for (var m = 0; m < flightBefore.Measurements.Length; m++)
                    {
                        flightAfter.Measurements[m].Should().Be(flightBefore.Measurements[m]);
                    }
                }
            }
        });
    }

    private static Entry ApplyOpenFlightStep(Entry entry)
    {
        var sequence = entry.Flights.Length + 1;
        var result = entry.OpenFlight(sequence, DateTimeOffset.UtcNow, maxLaunches: null, at: DateTimeOffset.UtcNow);
        return result.IsSuccess ? entry.Apply(result.Value) : entry;
    }

    private static Entry ApplyCaptureMeasurementStep(Entry entry, int pick, int metricIndex)
    {
        if (entry.Flights.Length == 0)
        {
            return entry;
        }

        var sequence = (pick % entry.Flights.Length) + 1;
        var metric = SampleMetricNames[metricIndex];
        var result = entry.CaptureMeasurement(
            sequence, metric, MeasuredValue.Of(pick / 10m), DateTimeOffset.UtcNow, SampleMetricDefs);

        // A rejected capture (e.g. captureMeasurement.alreadyCaptured) leaves
        // the entry untouched — that IS the invariant, not a case to work
        // around.
        return result.IsSuccess ? entry.Apply(result.Value) : entry;
    }

    // ============================================================ invariant 2
    // Flight sequences are contiguous and 1-based: after any accepted
    // sequence of OpenFlight decisions, Flights.Select(f => f.Sequence)
    // equals [1..n]. The fold navigates by sequence (Entry.cs:216-220), so a
    // gap would silently misroute every later measurement. Attempted
    // sequences deliberately drift from the correct next value (delta
    // -2..+2) so most attempts are rejected by openFlight.sequenceOutOfOrder
    // and only the exactly-right one is ever accepted.

    private static readonly Gen<int> SequenceDelta = Gen.Int[-2, 2];

    [Fact]
    public void Flight_sequences_are_contiguous_and_1_based()
    {
        SequenceDelta.Array[0, 40].Sample(deltas =>
        {
            var entry = OpenSampleEntry();

            foreach (var delta in deltas)
            {
                var attemptedSequence = entry.Flights.Length + 1 + delta;
                var result = entry.OpenFlight(
                    attemptedSequence, DateTimeOffset.UtcNow, maxLaunches: null, at: DateTimeOffset.UtcNow);

                if (result.IsSuccess)
                {
                    entry = entry.Apply(result.Value);
                }
                else
                {
                    result.Code.Should().Be("openFlight.sequenceOutOfOrder");
                }
            }

            entry.Flights.Select(f => f.Sequence).Should().Equal(Enumerable.Range(1, entry.Flights.Length));
        });
    }

    // ============================================================ invariant 3
    // The launch limit is exactly the class's, for every class in the
    // corpus: for each seed definition and each of its tasks, OpenFlight
    // accepts exactly MaxLaunches flights and refuses the next; where
    // MaxLaunches is unset it accepts an unbounded run (probed with a
    // large-but-bounded count, not literally unbounded). Resolved via
    // ParameterResolver — F3K TaskC's launches.C is a parameterised
    // MaxLaunches with a declared default of 3, so resolution must happen
    // the same way OpenEntry resolves a parameterised WorkingTime.

    private const int UnboundedProbeCount = 50;

    private static readonly IReadOnlyDictionary<string, MeasuredValue> EmptyBindings =
        new Dictionary<string, MeasuredValue>();

    [Fact]
    public void The_launch_limit_is_exactly_the_classs_for_every_class_in_the_corpus()
    {
        foreach (var seedClass in Corpus.All)
        {
            var definition = seedClass.Definition;
            foreach (var phase in definition.Phases)
            {
                foreach (var task in phase.Tasks)
                {
                    var maxLaunches = ResolveMaxLaunches(task, definition.Parameters);
                    AssertLaunchLimit(maxLaunches);
                }
            }
        }
    }

    private static int? ResolveMaxLaunches(TaskDefinition task, ImmutableArray<Parameter> declaredParameters) =>
        task.Timing.MaxLaunches is { } maxLaunches
            ? (int)ParameterResolver.Resolve(maxLaunches, EmptyBindings, declaredParameters)
            : null;

    private static void AssertLaunchLimit(int? maxLaunches)
    {
        var entry = OpenSampleEntry();
        var acceptCount = maxLaunches ?? UnboundedProbeCount;

        for (var sequence = 1; sequence <= acceptCount; sequence++)
        {
            var result = entry.OpenFlight(sequence, DateTimeOffset.UtcNow, maxLaunches, at: DateTimeOffset.UtcNow);
            result.IsSuccess.Should().BeTrue();
            entry = entry.Apply(result.Value);
        }

        entry.Flights.Length.Should().Be(acceptCount);

        if (maxLaunches is not null)
        {
            var next = entry.OpenFlight(acceptCount + 1, DateTimeOffset.UtcNow, maxLaunches, at: DateTimeOffset.UtcNow);
            next.IsFailure.Should().BeTrue();
            next.Code.Should().Be("openFlight.maxLaunchesExceeded");
        }
    }

    // ============================================================ invariant 4
    // Only declared metrics are ever stored: for any capture accepted
    // against any task in the corpus, the stored Measurement.Metric is one
    // of that task's declared metric names and its MeasuredValue.Kind
    // matches that metric's declared Kind. Each attempt mixes a declared or
    // fabricated metric name with a matching or mismatched value Kind, so
    // most attempts are rejected (metricNotDeclared / kindMismatch) and only
    // genuinely valid captures succeed.

    private static readonly Gen<(int Pick, bool UseDeclaredName, bool UseMatchingKind, decimal NumericValue, bool FlagValue)>
        CaptureAttempt =
            from pick in Gen.Int[0, 999]
            from useDeclaredName in Gen.Bool
            from useMatchingKind in Gen.Bool
            from numericValue in Gen.Int[0, 100_000].Select(i => i / 100m)
            from flagValue in Gen.Bool
            select (pick, useDeclaredName, useMatchingKind, numericValue, flagValue);

    [Fact]
    public void Only_declared_metrics_are_ever_stored()
    {
        foreach (var seedClass in Corpus.All)
        {
            foreach (var phase in seedClass.Definition.Phases)
            {
                foreach (var task in phase.Tasks)
                {
                    if (task.Metrics.IsDefaultOrEmpty)
                    {
                        continue;
                    }

                    AssertOnlyDeclaredMetricsStored(task);
                }
            }
        }
    }

    private static void AssertOnlyDeclaredMetricsStored(TaskDefinition task)
    {
        CaptureAttempt.Sample(
            t =>
            {
                var entry = OpenSampleEntryWithOneFlight();

                string metricName;
                MeasuredKind attemptedKind;

                if (t.UseDeclaredName)
                {
                    var declared = task.Metrics[t.Pick % task.Metrics.Length];
                    metricName = declared.Name;
                    attemptedKind = t.UseMatchingKind ? declared.Kind : Flip(declared.Kind);
                }
                else
                {
                    metricName = $"undeclaredMetric{t.Pick}";
                    attemptedKind = t.Pick % 2 == 0 ? MeasuredKind.Number : MeasuredKind.Flag;
                }

                var value = attemptedKind == MeasuredKind.Number
                    ? MeasuredValue.Of(t.NumericValue)
                    : MeasuredValue.Of(t.FlagValue);

                var result = entry.CaptureMeasurement(1, metricName, value, DateTimeOffset.UtcNow, task.Metrics);

                if (result.IsFailure)
                {
                    return;
                }

                var stored = result.Value.Measurement;
                task.Metrics.Should().Contain(m => m.Name == stored.Metric);
                task.Metrics.First(m => m.Name == stored.Metric).Kind.Should().Be(stored.Value.Kind);
            },
            iter: 15);
    }

    private static MeasuredKind Flip(MeasuredKind kind) =>
        kind == MeasuredKind.Number ? MeasuredKind.Flag : MeasuredKind.Number;

    // ============================================================ invariant 5
    // Decide and fold agree: for any accepted command sequence, folding the
    // appended events reproduces the same Entry the decide functions were
    // reasoning about. EntryModelBasedFoldTests already model-checks the
    // fold against hand-built events; this extends the same model-checking
    // approach to events the decide path (OpenFlight / CaptureMeasurement)
    // itself produced, so the reference model must also mirror
    // captureMeasurement.alreadyCaptured's rejection to stay in lockstep.

    private sealed class DecideFlightModel
    {
        public required int Sequence { get; init; }

        public required List<string> Measurements { get; init; }
    }

    private sealed class DecideModel
    {
        public List<DecideFlightModel> Flights { get; } = [];
    }

    private sealed class DecideActual
    {
        public required Entry Value { get; set; }
    }

    private static readonly GenOperation<DecideActual, DecideModel> DecideOpenFlight =
        Gen.Operation<DecideActual, DecideModel>(
            "OpenFlight",
            actual =>
            {
                var sequence = actual.Value.Flights.Length + 1;
                var result = actual.Value.OpenFlight(
                    sequence, DateTimeOffset.UtcNow, maxLaunches: null, at: DateTimeOffset.UtcNow);
                actual.Value = actual.Value.Apply(result.Value);
            },
            model => model.Flights.Add(new DecideFlightModel { Sequence = model.Flights.Count + 1, Measurements = [] }));

    private static readonly GenOperation<DecideActual, DecideModel> DecideCaptureMeasurement =
        (from pick in Gen.Int[0, 999] from metricIndex in Gen.Int[0, SampleMetricNames.Length - 1] select (pick, metricIndex))
        .Operation<DecideActual, DecideModel>(
            p => $"CaptureMeasurement(#{p.pick}, {SampleMetricNames[p.metricIndex]})",
            (actual, p) =>
            {
                if (actual.Value.Flights.Length == 0)
                {
                    return;
                }

                var sequence = (p.pick % actual.Value.Flights.Length) + 1;
                var metric = SampleMetricNames[p.metricIndex];
                var result = actual.Value.CaptureMeasurement(
                    sequence, metric, MeasuredValue.Of(p.pick / 10m), DateTimeOffset.UtcNow, SampleMetricDefs);

                if (result.IsSuccess)
                {
                    actual.Value = actual.Value.Apply(result.Value);
                }
            },
            (model, p) =>
            {
                if (model.Flights.Count == 0)
                {
                    return;
                }

                var sequence = (p.pick % model.Flights.Count) + 1;
                var metric = SampleMetricNames[p.metricIndex];
                var flight = model.Flights.Single(f => f.Sequence == sequence);

                // Mirrors captureMeasurement.alreadyCaptured: a second value
                // for a metric already captured on this flight is rejected
                // by the decide function, so the model must reject it too.
                if (!flight.Measurements.Contains(metric))
                {
                    flight.Measurements.Add(metric);
                }
            });

    private static readonly Gen<(DecideActual actual, DecideModel model)> DecideInitial =
        Gen.Int[0, 0].Select(_ => (new DecideActual { Value = OpenSampleEntry() }, new DecideModel()));

    [Fact]
    public void Decide_and_fold_agree()
    {
        Check.SampleModelBased(DecideInitial, [DecideOpenFlight, DecideCaptureMeasurement], DecideStructurallyEqual);
    }

    private static bool DecideStructurallyEqual(DecideActual actual, DecideModel model)
    {
        var flights = actual.Value.Flights;
        if (flights.Length != model.Flights.Count)
        {
            return false;
        }

        for (var i = 0; i < flights.Length; i++)
        {
            var flight = flights[i];
            var flightModel = model.Flights[i];
            if (flight.Sequence != flightModel.Sequence || flight.Measurements.Length != flightModel.Measurements.Count)
            {
                return false;
            }

            for (var j = 0; j < flight.Measurements.Length; j++)
            {
                if (flight.Measurements[j].Metric != flightModel.Measurements[j])
                {
                    return false;
                }
            }
        }

        return true;
    }
}
