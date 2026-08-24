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
/// kanban/completed/capture-a-score-steel-thread-plan.md WI-5, updated by
/// kanban/in-progress/out-of-order-flight-entry.md WI-4/WI-5 (flights may now
/// be opened out of order; the fold keeps Flights ascending by Sequence).
/// Each test is a named invariant so a failure names the invariant that broke,
/// not just "a test failed".
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

    private static Entry OpenSampleEntry() =>
        Entry.Create(new EntryOpened(
            SampleId, SampleCompetition, 1, 1, 1,
            SampleGroup, SampleCompetitor, ReflightRole.Original, DateTimeOffset.UtcNow));

    private static Entry OpenSampleEntryWithOneFlight() =>
        OpenSampleEntry().Apply(new FlightOpened(1, DateTimeOffset.UtcNow));

    // ============================================================ invariant 1
    // Capture is append-only: folding the events an accepted OpenFlight /
    // CaptureMeasurement decision produces never removes or alters a
    // previously recorded Measurement — Flight.Measurements only grows, and
    // every earlier element stays exactly as it was. Entry.cs:76-81 and
    // aggregate-roots.md §4 both state this in prose; this is the first test
    // to exercise it against the write path.
    //
    // Flights are compared BY SEQUENCE across before/after, not by position:
    // since out-of-order opens are legal (out-of-order-flight-entry.md WI-4)
    // the sorted fold can insert mid-list and reorder positions, so an index
    // walk would compare the wrong flights.

    private enum StepKind { OpenFlight, CaptureMeasurement }

    private static readonly ImmutableArray<string> SampleMetricNames = ["flightTime", "landingBonus", "distance"];

    private static readonly ImmutableArray<MetricDefinition> SampleMetricDefs =
        [.. SampleMetricNames.Select(name => new MetricDefinition { Name = name, Kind = MeasuredKind.Number })];

    // The small positive range open attempts are drawn from (WI-4): wide
    // enough that a walk produces gaps and out-of-order inserts.
    private const int GeneratedSequenceRange = 8;

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
                    ? ApplyOpenFlightStep(entry, step.Pick)
                    : ApplyCaptureMeasurementStep(entry, step.Pick, step.MetricIndex);

                var after = entry.Flights;

                // Never shrinks.
                after.Length.Should().BeGreaterThanOrEqualTo(before.Length);

                // Every flight present before is unchanged in its own fields,
                // and its Measurements only ever grows — never loses or
                // rewrites an earlier element. Matched by Sequence because
                // insertion may have moved positions.
                foreach (var flightBefore in before)
                {
                    var flightAfter = after.Single(f => f.Sequence == flightBefore.Sequence);

                    flightAfter.Measurements.Length.Should().BeGreaterThanOrEqualTo(flightBefore.Measurements.Length);

                    for (var m = 0; m < flightBefore.Measurements.Length; m++)
                    {
                        flightAfter.Measurements[m].Should().Be(flightBefore.Measurements[m]);
                    }
                }
            }
        });
    }

    private static Entry ApplyOpenFlightStep(Entry entry, int pick)
    {
        if (entry.Flights.Length >= GeneratedSequenceRange)
        {
            return entry;
        }

        // An arbitrary unused positive value in the range — not Length+1,
        // which contiguity used to force (out-of-order-flight-entry.md WI-4).
        // Skipping taken values keeps every attempt accepted, so this walk
        // exercises insertion positions rather than rejections.
        var sequence = NextUnused((pick % GeneratedSequenceRange) + 1, entry.Flights.Select(f => f.Sequence));

        var result = entry.OpenFlight(sequence, maxLaunches: null, at: DateTimeOffset.UtcNow);
        return result.IsSuccess ? entry.Apply(result.Value) : entry;
    }

    private static Entry ApplyCaptureMeasurementStep(Entry entry, int pick, int metricIndex)
    {
        if (entry.Flights.Length == 0)
        {
            return entry;
        }

        // Pick an existing flight by position IN THE SORTED LIST and use its
        // sequence value: with gaps legal, count arithmetic no longer names a
        // flight (out-of-order-flight-entry.md WI-4).
        var sequence = entry.Flights[pick % entry.Flights.Length].Sequence;
        var metric = SampleMetricNames[metricIndex];
        var result = entry.CaptureMeasurement(
            sequence, metric, MeasuredValue.Of(pick / 10m), DateTimeOffset.UtcNow, SampleMetricDefs);

        // A rejected capture (e.g. captureMeasurement.alreadyCaptured) leaves
        // the entry untouched — that IS the invariant, not a case to work
        // around.
        return result.IsSuccess ? entry.Apply(result.Value) : entry;
    }

    /// <summary>
    /// The first unused sequence at or (wrapping) after <paramref name="start"/>
    /// within <see cref="GeneratedSequenceRange"/>. Callers guarantee fewer
    /// than that many flights exist, so this terminates. Mirrors how the tests'
    /// reference models derive the same value, keeping actual and model in
    /// lockstep.
    /// </summary>
    private static int NextUnused(int start, IEnumerable<int> taken)
    {
        var sequence = start;
        while (taken.Contains(sequence))
        {
            sequence = (sequence % GeneratedSequenceRange) + 1;
        }

        return sequence;
    }

    // ============================================================ invariant 2
    // Sequences are unique, positive, and ascending: after any walk of OpenFlight
    // decisions over a small positive range with repeats included, the folded
    // Flights' sequences are strictly ascending — each >= 1, no duplicates.
    // Contiguity is deliberately gone (out-of-order-flight-entry.md decision 2 /
    // WI-4): gaps mean "not entered yet", so [1, 2, 5] is as legal as [1, 2, 3].
    // With every attempt positive and maxLaunches unset, the only possible
    // rejection is openFlight.duplicateSequence. Fold idempotence is invariant
    // 5's business and is not repeated here.

    private static readonly Gen<int> AttemptedSequence = Gen.Int[1, GeneratedSequenceRange];

    [Fact]
    public void Flight_sequences_are_unique_positive_and_ascending()
    {
        AttemptedSequence.Array[0, 40].Sample(attempts =>
        {
            var entry = OpenSampleEntry();

            foreach (var attemptedSequence in attempts)
            {
                var result = entry.OpenFlight(
                    attemptedSequence, maxLaunches: null, at: DateTimeOffset.UtcNow);

                if (result.IsSuccess)
                {
                    entry = entry.Apply(result.Value);
                }
                else
                {
                    result.Code.Should().Be("openFlight.duplicateSequence");
                }
            }

            var sequences = entry.Flights.Select(f => f.Sequence).ToList();
            sequences.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
            sequences.Should().OnlyContain(s => s >= 1);
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
    // The scrambled-order leg (WI-4) checks the limit is a count of flights,
    // not an ordinal property of the sequence values; P3 below generalises it
    // to arbitrary permutations on concrete maxima.

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
            var result = entry.OpenFlight(sequence, maxLaunches, at: DateTimeOffset.UtcNow);
            result.IsSuccess.Should().BeTrue();
            entry = entry.Apply(result.Value);
        }

        entry.Flights.Length.Should().Be(acceptCount);

        if (maxLaunches is { } max)
        {
            // The same limit, hit in a scrambled (here: descending) order,
            // overflows at exactly the same count (out-of-order-flight-entry.md
            // WI-4): all max opens succeed, the next fails.
            var scrambled = OpenSampleEntry();
            for (var sequence = max; sequence >= 1; sequence--)
            {
                var descending = scrambled.OpenFlight(sequence, maxLaunches, at: DateTimeOffset.UtcNow);
                descending.IsSuccess.Should().BeTrue();
                scrambled = scrambled.Apply(descending.Value);
            }

            scrambled.Flights.Length.Should().Be(max);

            var overflow = scrambled.OpenFlight(max + 1, maxLaunches, at: DateTimeOffset.UtcNow);
            overflow.Code.Should().Be("openFlight.maxLaunchesExceeded");
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
    // captureMeasurement.alreadyCaptured's rejection and the fold's
    // insertion-by-sequence (out-of-order-flight-entry.md WI-4) to stay in
    // lockstep — DecideStructurallyEqual's positional walk stays valid
    // because BOTH sides are ascending by Sequence.

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

    // Out-of-order opens (WI-4): each attempt derives an unused positive value
    // in the bounded range, skipping collisions exactly as a caller would —
    // so gaps arise and the fold's insertion order is exercised, while
    // actual and model derive the SAME sequence from their (identical) state.

    private static readonly GenOperation<DecideActual, DecideModel> DecideOpenFlight =
        Gen.Int[1, GeneratedSequenceRange].Operation<DecideActual, DecideModel>(
            pick => $"OpenFlight({pick}→unused)",
            (actual, pick) =>
            {
                if (actual.Value.Flights.Length >= GeneratedSequenceRange)
                {
                    return;
                }

                var sequence = NextUnused(pick, actual.Value.Flights.Select(f => f.Sequence));
                var result = actual.Value.OpenFlight(sequence, maxLaunches: null, at: DateTimeOffset.UtcNow);
                actual.Value = actual.Value.Apply(result.Value);
            },
            (model, pick) =>
            {
                if (model.Flights.Count >= GeneratedSequenceRange)
                {
                    return;
                }

                var sequence = NextUnused(pick, model.Flights.Select(f => f.Sequence));
                InsertModelFlight(model.Flights, sequence);
            });

    private static void InsertModelFlight(List<DecideFlightModel> flights, int sequence)
    {
        // Mirrors Entry.Apply(FlightOpened): insert at the first flight whose
        // Sequence is greater, keeping the list ascending by Sequence.
        var index = flights.FindIndex(f => f.Sequence > sequence);
        flights.Insert(index < 0 ? flights.Count : index,
            new DecideFlightModel { Sequence = sequence, Measurements = [] });
    }

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

                // Position INTO the sorted flight list, then take that
                // flight's sequence: gaps make count arithmetic meaningless
                // under out-of-order opens (out-of-order-flight-entry.md WI-4).
                var sequence = actual.Value.Flights[p.pick % actual.Value.Flights.Length].Sequence;
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

                var flight = model.Flights[p.pick % model.Flights.Count];
                var metric = SampleMetricNames[p.metricIndex];

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

    // ============================================================ P1 (WI-5)
    // Capture-order independence — the story's own invariant
    // (kanban/in-progress/out-of-order-flight-entry.md): a retrospectively
    // completed card must be indistinguishable from a live-typed one. Each
    // flight is planned as a block (its open plus any measurement payloads);
    // folding the blocks in ANY order and folding them in sequence order produce
    // structurally equal Entries — same sequence-per-flight, same measurements
    // per flight. Block-local open-then-capture ordering is what keeps every
    // capture accepted; block-level shuffling is the arrival-order freedom.

    private sealed record PlannedCapture(string Metric, MeasuredValue Value);

    private sealed record FlightPlan(int Sequence, IReadOnlyList<PlannedCapture> Captures);

    private static readonly int[] SequencePool = [1, 2, 3, 4];

    [Fact]
    public void Capture_order_does_not_change_the_folded_entry()
    {
        (from order in Gen.Shuffle(SequencePool)
         from count in Gen.Int[1, SequencePool.Length]
         from includes in Gen.Bool.Array[SequencePool.Length * SampleMetricNames.Length]
         from values in Gen.Int[0, 100_000].Array[SequencePool.Length * SampleMetricNames.Length]
         select (order, count, includes, values))
        .Sample(t =>
        {
            var plans = new List<FlightPlan>();
            for (var i = 0; i < t.count; i++)
            {
                var captures = new List<PlannedCapture>();
                for (var m = 0; m < SampleMetricNames.Length; m++)
                {
                    var slot = (i * SampleMetricNames.Length) + m;
                    if (t.includes[slot])
                    {
                        captures.Add(new PlannedCapture(SampleMetricNames[m], MeasuredValue.Of(t.values[slot] / 100m)));
                    }
                }

                // t.order's first `count` entries are a random DISTINCT subset of
                // the pool, so gaps arise exactly as they do in real retrospective
                // entry.
                plans.Add(new FlightPlan(t.order[i], captures));
            }

            var canonical = FoldPlans(plans.OrderBy(p => p.Sequence));
            var permuted = FoldPlans(plans);

            AssertSameFlightsBySequence(canonical, permuted);
        });
    }

    private static Entry FoldPlans(IEnumerable<FlightPlan> plans)
    {
        var entry = OpenSampleEntry();
        foreach (var plan in plans)
        {
            entry = entry.Apply(new FlightOpened(plan.Sequence, DateTimeOffset.UtcNow));
            foreach (var capture in plan.Captures)
            {
                entry = entry.Apply(new MeasurementCaptured(
                    plan.Sequence,
                    new Measurement { Metric = capture.Metric, Value = capture.Value, CapturedAt = DateTimeOffset.UtcNow }));
            }
        }

        return entry;
    }

    private static void AssertSameFlightsBySequence(Entry expected, Entry actual)
    {
        actual.Flights.Select(f => f.Sequence).Should().Equal(expected.Flights.Select(f => f.Sequence));

        for (var i = 0; i < expected.Flights.Length; i++)
        {
            var expectedFlight = expected.Flights[i];
            var actualFlight = actual.Flights[i];

            actualFlight.Measurements.Length.Should().Be(expectedFlight.Measurements.Length);
            for (var m = 0; m < expectedFlight.Measurements.Length; m++)
            {
                actualFlight.Measurements[m].Metric.Should().Be(expectedFlight.Measurements[m].Metric);
                actualFlight.Measurements[m].Value.Should().Be(expectedFlight.Measurements[m].Value);
            }
        }
    }

    // ============================================================ P2 (WI-5)
    // Sortedness is an aggregate invariant: after any interleaving of accepted
    // and rejected opens — non-positive attempts and duplicates included —
    // Flights is strictly ascending by Sequence with no duplicates. This guards
    // WI-2 (the sorted fold) directly, over a wider attempt range than
    // invariant 2 exercises.

    private static readonly Gen<int> AnySequenceAttempt = Gen.Int[-2, GeneratedSequenceRange];

    [Fact]
    public void Flights_stay_sorted_after_accepted_and_rejected_opens_interleave()
    {
        AnySequenceAttempt.Array[0, 50].Sample(attempts =>
        {
            var entry = OpenSampleEntry();

            foreach (var attempted in attempts)
            {
                var result = entry.OpenFlight(attempted, maxLaunches: null, at: DateTimeOffset.UtcNow);
                if (result.IsSuccess)
                {
                    entry = entry.Apply(result.Value);
                }
                else
                {
                    result.Code.Should().BeOneOf("openFlight.sequenceNotPositive", "openFlight.duplicateSequence");
                }
            }

            var sequences = entry.Flights.Select(f => f.Sequence).ToArray();
            sequences.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
            sequences.Should().OnlyContain(s => s >= 1);
        });
    }

    // ============================================================ P3 (WI-5)
    // The launch limit is a count, not an ordinal: for any permutation of
    // 1..max (concrete maxima via generation), accepting all of them succeeds
    // and the next open — whatever its value — fails
    // openFlight.maxLaunchesExceeded. Generalises invariant 3 beyond the
    // in-order case (out-of-order-flight-entry.md WI-5).

    private static readonly int[] ProbeMaxima = [2, 4, 7];

    [Fact]
    public void The_launch_limit_is_a_count_not_an_ordinal()
    {
        (from max in Gen.OneOfConst(ProbeMaxima)
         from order in Gen.Shuffle(Enumerable.Range(1, max).ToArray())
         select (max, order))
        .Sample(t =>
        {
            var entry = OpenSampleEntry();

            foreach (var sequence in t.order)
            {
                var result = entry.OpenFlight(sequence, t.max, at: DateTimeOffset.UtcNow);
                result.IsSuccess.Should().BeTrue($"launch {sequence} of {t.max}");
                entry = entry.Apply(result.Value);
            }

            entry.Flights.Length.Should().Be(t.max);

            var overflow = entry.OpenFlight(t.max + 1, t.max, at: DateTimeOffset.UtcNow);
            overflow.Code.Should().Be("openFlight.maxLaunchesExceeded");

            var farOverflow = entry.OpenFlight(t.max * 10, t.max, at: DateTimeOffset.UtcNow);
            farOverflow.Code.Should().Be("openFlight.maxLaunchesExceeded");
        });
    }
}
