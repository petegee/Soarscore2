using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property tests for the metric absence semantics — the three invariants named
/// at planning in WI-5 of kanban/in-progress/metric-absence-semantics.md:
///
/// 1. P-AbsenceTransparency — scoring is observationally identical whether an
///    assumed-declared metric is explicitly captured AT ITS ASSUMED VALUE or
///    not captured at all. The property that makes absence semantics safe.
/// 2. P-ExplicitCaptureWins — a captured value different from the assumed
///    value always scores as the captured value; the assumption never
///    displaces an observation.
/// 3. P-PendingArithmeticallyInvisible — the CsCheck sweep of the
///    Pending-then-filled invariant ("score what it can"); its example-based
///    per-stage half lives in MetricAbsenceSemanticsTests.cs.
///
/// Generator strategy: four representative task/class shapes that declare
/// assumptions — the shared synthetic fixture (Flag AND Number assumptions,
/// the Number one deliberately non-zero) plus three real seed tasks, SeedF3J
/// Task D (Number + two Flag assumptions, a landing LookupTerm, overfly gates)
/// and SeedF3K Tasks C and E (three Flag assumptions; Poker's
/// declared-before-launch target). Every shape carries a per-flight capture
/// plan generator (one captured-or-absent slot per declared metric, in
/// declared order, integer values so capture precision is identity) built with
/// the shared MetricAbsenceFixtures generators. The WI-3
/// <c>ClassDefinitionFixtures.AbsenceSemantics()</c> fixture lives in
/// Soarscore.Application.Tests, which this project cannot reference — hence
/// the local shapes.
/// </summary>
public class MetricAbsenceSemanticsPropertyTests
{
    // The seed snapshots must precede Shapes: static field initializers run in
    // declaration order, and the Shapes entries close over these instances.

    private static readonly ClassDefinition F3JClass = SeedF3J.Definition;
    private static readonly TaskDefinition F3JTaskD = F3JClass.Phases[0].Tasks[0];   // Code "D"

    private static readonly ClassDefinition F3KClass = SeedF3K.Definition;
    private static readonly TaskDefinition F3KTaskC = F3KClass.Phases[0].Tasks.Single(t => t.Code == "C");
    private static readonly TaskDefinition F3KTaskE = F3KClass.Phases[0].Tasks.Single(t => t.Code == "E");

    /// <summary>One fully-captured synthetic flight, in declared metric order.</summary>
    private static readonly Gen<MeasuredValue[]> SyntheticCompleteFlight =
        from flightTime in MetricAbsenceFixtures.Number(1, 600)
        from landing in MetricAbsenceFixtures.Number(0, 15)
        from landedOut in MetricAbsenceFixtures.Flag
        from overfly in MetricAbsenceFixtures.Number(0, 90)
        select new[] { flightTime, landing, landedOut, overfly };

    /// <summary>
    /// The generator of values ≠ assumed for each of a shape's assumed metrics:
    /// per metric, a value generator whose range excludes the assumption.
    /// </summary>
    private static Gen<(string Metric, MeasuredValue Value)> DisplaceOne(
        params (string Name, Gen<MeasuredValue> Values)[] metrics) =>
        Gen.OneOf(metrics.Select(m => m.Values.Select(v => (m.Name, v))).ToArray());

    private static readonly AbsenceShape[] Shapes =
    [
        new(
            "synthetic",
            MetricAbsenceFixtures.SyntheticTask,
            MetricAbsenceFixtures.SyntheticClass(MetricAbsenceFixtures.SyntheticTask),
            (from flightTime in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(1, 600))
             from landing in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(0, 15))
             from landedOut in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Flag)
             from overfly in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(0, 90))
             select new MeasuredValue?[] { flightTime, landing, landedOut, overfly }),
            SyntheticCompleteFlight,
            DisplaceOne(
                ("landedOut", Gen.Const(MeasuredValue.Of(true))),
                ("overflySeconds", MetricAbsenceFixtures.Number(0, 90).Where(v => v.Number!.Value != 10m)))),

        new(
            "F3J/D",
            F3JTaskD,
            F3JClass,
            (from flightTime in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(1, 600))
             from landing in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(0, 15))
             from overfly in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(0, 60))
             from touched in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Flag)
             from rested in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Flag)
             select new MeasuredValue?[] { flightTime, landing, overfly, touched, rested }),
            (from flightTime in MetricAbsenceFixtures.Number(1, 600)
             from landing in MetricAbsenceFixtures.Number(0, 15)
             from overfly in MetricAbsenceFixtures.Number(0, 60)
             from touched in MetricAbsenceFixtures.Flag
             from rested in MetricAbsenceFixtures.Flag
             select new[] { flightTime, landing, overfly, touched, rested }),
            DisplaceOne(
                ("overflySeconds", MetricAbsenceFixtures.Number(1, 60)),
                ("touchedByCompetitor", Gen.Const(MeasuredValue.Of(true))),
                ("restedWithin75m", Gen.Const(MeasuredValue.Of(false))))),

        new(
            "F3K/C",
            F3KTaskC,
            F3KClass,
            (from flightTime in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(1, 180))
             from window in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Flag)
             from launch in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Flag)
             from signal in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Flag)
             select new MeasuredValue?[] { flightTime, window, launch, signal }),
            (from flightTime in MetricAbsenceFixtures.Number(1, 180)
             from window in MetricAbsenceFixtures.Flag
             from launch in MetricAbsenceFixtures.Flag
             from signal in MetricAbsenceFixtures.Flag
             select new[] { flightTime, window, launch, signal }),
            DisplaceOne(
                ("landedWithinWindow", Gen.Const(MeasuredValue.Of(false))),
                ("launchedInWorkingTime", Gen.Const(MeasuredValue.Of(false))),
                ("launchedOnSignal", Gen.Const(MeasuredValue.Of(false))))),

        new(
            "F3K/E",
            F3KTaskE,
            F3KClass,
            (from flightTime in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(1, 180))
             from window in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Flag)
             from launch in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Flag)
             from target in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(1, 180))
             select new MeasuredValue?[] { flightTime, window, launch, target }),
            (from flightTime in MetricAbsenceFixtures.Number(1, 180)
             from window in MetricAbsenceFixtures.Flag
             from launch in MetricAbsenceFixtures.Flag
             from target in MetricAbsenceFixtures.Number(1, 180)
             select new[] { flightTime, window, launch, target }),
            DisplaceOne(
                ("landedWithinWindow", Gen.Const(MeasuredValue.Of(false))),
                ("launchedInWorkingTime", Gen.Const(MeasuredValue.Of(false))))),
    ];

    // ================================================== invariant 1: Transparency

    /// <summary>
    /// P-AbsenceTransparency
    /// (kanban/in-progress/metric-absence-semantics.md#wi-5, invariant 1):
    /// scoring is observationally identical whether an assumed-declared metric
    /// is explicitly captured AT ITS ASSUMED VALUE or not captured at all.
    /// For any generated shape and any generated capture subset — per flight,
    /// per declared metric, captured-or-absent, values of the metric's kind in
    /// plausible ranges — the entry that additionally captures every absent
    /// assumed metric at its assumed value must produce exactly the outcome the
    /// bare subset produces: state, raw score entering normalisation, final
    /// score, and awaited-capture diagnostics. The assumption is only ever the
    /// absence's value.
    /// </summary>
    [Fact]
    public void P_AbsenceTransparency_scoring_with_explicit_assumed_captures_equals_scoring_without()
    {
        (from shape in Gen.OneOfConst(Shapes)
         from flightCount in Gen.Int[1, 3]
         from plans in shape.FlightPlan.Array[flightCount]
         select (shape, plans))
        .Sample(t =>
        {
            var bare = MetricAbsenceFixtures.Score(
                t.shape.Task, t.shape.Class,
                BuildEntry(t.shape.Task, t.plans, explicitAssumptions: false));

            var explicitAssumed = MetricAbsenceFixtures.Score(
                t.shape.Task, t.shape.Class,
                BuildEntry(t.shape.Task, t.plans, explicitAssumptions: true));

            AssertSameOutcome(explicitAssumed, bare, $"shape={t.shape.Label}");
        });
    }

    // ============================================= invariant 2: Explicit-wins

    /// <summary>
    /// P-ExplicitCaptureWins
    /// (kanban/in-progress/metric-absence-semantics.md#wi-5, invariant 2):
    /// a captured value v ≠ assumed on an assumed-declared metric always scores
    /// as the captured value — the assumption never displaces an observation.
    /// Property: with every other referenced metric captured, scoring the class
    /// WITH the assumption against a flight capturing v is identical to scoring
    /// the SAME class WITHOUT that assumption, same flight — the no-assumption
    /// computation. For a Flag gate this is the companion statement verbatim:
    /// capturing false against an assumed-true gate invalidates exactly as a
    /// false capture does in the assumption-free class. Shares the shapes and
    /// value generators with invariant 1.
    /// </summary>
    [Fact]
    public void P_ExplicitCaptureWins_a_captured_value_scores_as_it_does_without_the_assumption()
    {
        (from shape in Gen.OneOfConst(Shapes)
         from displaced in shape.Displaced
         from complete in shape.CompleteFlight
         select (shape, displaced, complete))
        .Sample(t =>
        {
            // One flight capturing every referenced metric, the chosen assumed
            // one at a value different from its assumption.
            var plan = (MeasuredValue[])t.complete.Clone();
            plan[IndexOfMetric(t.shape.Task, t.displaced.Metric)] = t.displaced.Value;
            var entry = BuildEntry(t.shape.Task, [plan], explicitAssumptions: false);

            var (twinTask, twinClass) = WithoutAssumption(t.shape.Task, t.shape.Class, t.displaced.Metric);

            var withAssumption = MetricAbsenceFixtures.Score(t.shape.Task, t.shape.Class, entry);
            var withoutAssumption = MetricAbsenceFixtures.Score(twinTask, twinClass, entry);

            AssertSameOutcome(withAssumption, withoutAssumption, $"shape={t.shape.Label}, {t.displaced.Metric}={t.displaced.Value}");
        });
    }

    // ================== invariant 3 (sweep half): Pending-then-filled

    /// <summary>
    /// P-PendingArithmeticallyInvisible
    /// (kanban/in-progress/metric-absence-semantics.md#wi-5, invariant 3, the
    /// CsCheck sweep of Pending-then-filled): for any subset S of an entry's
    /// flights, the score of the entry whose flights outside S are pending
    /// (each misses flightTime, a declared referenced metric with no
    /// assumption) equals the score of the entry containing exactly S's flights
    /// — pending flights are arithmetically invisible ("score what it can").
    ///
    /// All five generated selection kinds satisfy this BECAUSE pending flights
    /// are filtered before selection (FlightSelector.SelectAndScore step 1b):
    /// AllFlights, LastFlight, LastNFlights, ExactlyNInOrder and BestNFlights
    /// each rank and count over the complete flights only, and both entries
    /// carry the same complete flights under the same sequence numbers.
    /// </summary>
    [Fact]
    public void P_PendingArithmeticallyInvisible_a_partially_pending_entry_scores_its_complete_flights_only()
    {
        (from selection in Gen.OneOfConst<FlightSelection>(
             new AllFlights(),
             new LastFlight(),
             new LastNFlights(2),
             new ExactlyNInOrder { Count = 2 },
             new BestNFlights { Count = 2 })
         from flightCount in Gen.Int[1, 3]
         from completeMask in Gen.Bool.Array[flightCount]
         from completePlans in SyntheticCompleteFlight.Array[flightCount]
         select (selection, flightCount, completeMask, completePlans))
        .Sample(t =>
        {
            var task = MetricAbsenceFixtures.SyntheticTask with { Flights = t.selection };
            var classDef = MetricAbsenceFixtures.SyntheticClass(task);

            var partial = EntryFromMask(t.completePlans, t.completeMask, pendingFlightsOutsideSubset: true);
            var exact = EntryFromMask(t.completePlans, t.completeMask, pendingFlightsOutsideSubset: false);

            var partialGroup = MetricAbsenceFixtures.Score(task, classDef, partial);
            var exactGroup = MetricAbsenceFixtures.Score(task, classDef, exact);
            var because = $"selection={t.selection.GetType().Name}, S={DescribeSubset(t.completeMask)}";

            partialGroup.Results[MetricAbsenceFixtures.RowKey(0)].State
                .Should().Be(exactGroup.Results[MetricAbsenceFixtures.RowKey(0)].State, because);
            partialGroup.Results[MetricAbsenceFixtures.RowKey(0)].RawScore
                .Should().Be(exactGroup.Results[MetricAbsenceFixtures.RowKey(0)].RawScore, because);
            SelectedShouldMatch(
                partialGroup.Results[MetricAbsenceFixtures.RowKey(0)].Selection,
                exactGroup.Results[MetricAbsenceFixtures.RowKey(0)].Selection,
                because);
        });
    }

    // ------------------------------------------------------------- helpers

    /// <summary>
    /// Fold an entry whose flights carry exactly the generated plan's captures;
    /// with <paramref name="explicitAssumptions"/> every absent assumed metric
    /// is additionally captured at its assumed value — the invariant-1 "with"
    /// variant. Values are integers, so the seeds' capture precision is
    /// identity and both variants store exactly what was generated.
    /// </summary>
    private static Entry BuildEntry(TaskDefinition task, MeasuredValue?[][] plans, bool explicitAssumptions)
    {
        var entry = MetricAbsenceFixtures.OpenEntry(flightCount: plans.Length);

        for (var flightIndex = 0; flightIndex < plans.Length; flightIndex++)
        {
            var sequence = flightIndex + 1;

            for (var metricIndex = 0; metricIndex < task.Metrics.Length; metricIndex++)
            {
                var metric = task.Metrics[metricIndex];
                var captured = plans[flightIndex][metricIndex]
                    ?? (explicitAssumptions ? metric.WhenNotRecorded : null);

                if (captured is { } value)
                    entry = MetricAbsenceFixtures.Capture(entry, sequence, metric.Name, value, task.Metrics);
            }
        }

        return entry;
    }

    /// <summary>
    /// Fold the entry the sweep compares: every flight in the subset (mask
    /// true) carries its complete plan; with
    /// <paramref name="pendingFlightsOutsideSubset"/> flights outside it carry
    /// the plan minus flightTime — pending on a declared unassumed referenced
    /// metric — and without it they are not opened at all. Sequence numbers
    /// match between the two entries, so positional selection sees the same
    /// complete list both sides.
    /// </summary>
    private static Entry EntryFromMask(MeasuredValue[][] completePlans, bool[] completeMask, bool pendingFlightsOutsideSubset)
    {
        var entry = MetricAbsenceFixtures.OpenEntry(flightCount: 0);

        for (var flightIndex = 0; flightIndex < completePlans.Length; flightIndex++)
        {
            var inSubset = completeMask[flightIndex];
            if (!inSubset && !pendingFlightsOutsideSubset)
                continue;

            var sequence = flightIndex + 1;
            entry = entry.Apply(new FlightOpened(sequence, MetricAbsenceFixtures.Now));

            for (var metricIndex = 0; metricIndex < MetricAbsenceFixtures.SyntheticTask.Metrics.Length; metricIndex++)
            {
                var metric = MetricAbsenceFixtures.SyntheticTask.Metrics[metricIndex];
                if (!inSubset && metric.Name == PendingMetric)
                    continue;   // flightTime absent → the flight pends

                entry = MetricAbsenceFixtures.Capture(entry, sequence, metric.Name, completePlans[flightIndex][metricIndex]);
            }
        }

        return entry;
    }

    private const string PendingMetric = "flightTime";

    private static string DescribeSubset(bool[] completeMask) =>
        string.Join("", completeMask.Select(inSubset => inSubset ? '1' : '0'));

    /// <summary>
    /// The observational equality of invariants 1 and 2: state, raw score
    /// entering normalisation, final score, awaited-capture diagnostics.
    /// </summary>
    private static void AssertSameOutcome(GroupResult left, GroupResult right, string because)
    {
        var leftRow = left.Results[MetricAbsenceFixtures.RowKey(0)];
        var rightRow = right.Results[MetricAbsenceFixtures.RowKey(0)];

        leftRow.State.Should().Be(rightRow.State, because);
        left.PreNormalisationScores[MetricAbsenceFixtures.RowKey(0)]
            .Should().Be(right.PreNormalisationScores[MetricAbsenceFixtures.RowKey(0)], $"{because}: raw score");
        leftRow.RawScore.Should().Be(rightRow.RawScore, $"{because}: final score");
        leftRow.AwaitingCapture.SequenceEqual(rightRow.AwaitingCapture)
            .Should().BeTrue($"{because}: awaited-capture diagnostics");
    }

    /// <summary>
    /// Selection equivalence for the sweep: same launches (the flight.sequence
    /// intrinsic) and same flight scores — or both null.
    /// </summary>
    private static void SelectedShouldMatch(SelectedFlights? left, SelectedFlights? right, string because)
    {
        if (left is null)
        {
            right.Should().BeNull(because);
            return;
        }

        right.Should().NotBeNull(because);

        static (int Sequence, decimal Score)[] Profile(SelectedFlights selection) =>
            [.. selection.Flights.Select(f => (
                Sequence: (int)f.Metrics["flight.sequence"].Number!.Value,
                Score: f.Score))];

        Profile(left).Should().Equal(Profile(right!), because);
    }

    private static int IndexOfMetric(TaskDefinition task, string metricName)
    {
        for (var i = 0; i < task.Metrics.Length; i++)
        {
            if (task.Metrics[i].Name == metricName)
                return i;
        }

        throw new ArgumentException($"'{metricName}' is not declared by task '{task.Code}'.");
    }

    /// <summary>
    /// The same class and task with ONE metric's whenNotRecorded stripped — the
    /// assumption-free twin the captured value must be indistinguishable under.
    /// </summary>
    private static (TaskDefinition Task, ClassDefinition Class) WithoutAssumption(
        TaskDefinition task, ClassDefinition classDef, string metricName)
    {
        var twinTask = task with
        {
            Metrics = [.. task.Metrics.Select(m => m.Name == metricName ? m with { WhenNotRecorded = null } : m)],
        };

        var twinClass = classDef with
        {
            Phases = [.. classDef.Phases.Select(phase => phase with
            {
                Tasks = [.. phase.Tasks.Select(declared => declared.Code == twinTask.Code ? twinTask : declared)],
            })],
        };

        return (twinTask, twinClass);
    }
}

/// <summary>One representative task/class shape: the task and its class, a
/// per-flight capture-plan generator (a captured-or-absent slot per declared
/// metric, in declared order — the absent slots are what the absence semantics
/// act on), a complete-capture generator for the explicit-wins property, and
/// the generator of values ≠ assumed for each of the shape's assumed
/// metrics.</summary>
internal sealed record AbsenceShape(
    string Label,
    TaskDefinition Task,
    ClassDefinition Class,
    Gen<MeasuredValue?[]> FlightPlan,
    Gen<MeasuredValue[]> CompleteFlight,
    Gen<(string Metric, MeasuredValue Value)> Displaced);
