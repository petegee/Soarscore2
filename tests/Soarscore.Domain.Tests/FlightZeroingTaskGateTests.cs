// WI-2 of kanban/backlog/flight-zeroing-vs-task-gate.md — failing tests first.
//
// Pins the WI-1 decision: a flight zeroed by `flightValidWhen` does NOT
// participate in the `task.ValidWhen` gate. The task gate judges countable
// selected flights only (countable = selected flights that passed the flight
// gate); a task-void condition on a countable flight still voids the cell.
// Named invariant: **a flightValidWhen failure zeroes exactly that flight's
// contribution and never changes selection count or cell state; a
// task.ValidWhen failure on a countable selected flight voids the cell.**
//
// All tests go through the real pipeline — FlightInterpreter.Interpret →
// FlightSelector.SelectAndScore, no mocks between stages.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;
using ScoreTerm = Soarscore.Domain.PublishedClassDefinition.ScoreTerm;

namespace Soarscore.Domain.Tests;

public class FlightZeroingTaskGateTests
{
    // ------------------------------------------------------ F5K Task A shape

    [Fact]
    public void F5K_TaskA_one_flight_off_field_zeroes_only_that_flight()
    {
        // 5.5.10.12 flight penalty b: "zero points for that flight only".
        // Four selected flights (BestN 4 of 4), the 55 s flight off-field.
        // The off-field flight stays selected with score 0; the cell stays
        // Valid with the raw score of the other three. The 55 s flight sits
        // below its 60 s target, so no clamping re-scores it (the clamp
        // regression is pinned separately below).
        var task = ResolveF5KTaskA();

        var flights = new List<InterpretedFlight>
        {
            InterpretF5K(task, 1, flightTime: 240m),
            InterpretF5K(task, 2, flightTime: 180m),
            InterpretF5K(task, 3, flightTime: 120m),
            InterpretF5K(task, 4, flightTime: 55m, landedOnField: false),
        };

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights.ToImmutableArray());

        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be(540m); // 240 + 180 + 120, zeroed flight contributes 0
        result.Selection.Should().NotBeNull();
        result.Selection!.Flights.Should().HaveCount(4); // zeroed flight still selected
        ScoreOf(result, 4).Should().Be(0m);
        ScoreOf(result, 1).Should().Be(240m);
        ScoreOf(result, 2).Should().Be(180m);
        ScoreOf(result, 3).Should().Be(120m);
    }

    [Fact]
    public void F5K_TaskB_last_flight_off_field_is_valid_zero_not_NoResult()
    {
        // F5K Task B / F3K.9.3: a zeroed LAST flight scores 0, stays selected,
        // never promotes its predecessor, and never voids the cell.
        var task = ResolveF5KTaskB();

        var flights = new List<InterpretedFlight>
        {
            InterpretF5K(task, 1, flightTime: 100m),
            InterpretF5K(task, 2, flightTime: 90m),
            InterpretF5K(task, 3, flightTime: 80m, landedOnField: false),
        };

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights.ToImmutableArray());

        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be(0m);
        result.Selection.Should().NotBeNull();
        result.Selection!.Flights.Should().ContainSingle(); // predecessor NOT promoted
        ScoreOf(result, 3).Should().Be(0m);
    }

    // ------------------------------------------------------ both-gate combination

    [Fact]
    public void Both_gate_task_zeroed_flight_does_not_void_the_cell()
    {
        // The WI-1 combination rule: a flight zeroed by flightValidWhen does
        // not participate in the task.ValidWhen gate — even when its original
        // metrics would fail that gate (they remain the record of what was
        // flown, not live gate input). Here flight 2 is off-field (zeroed) AND
        // its original metrics fail the task gate; the countable flight 1
        // passes it, so the cell is Valid with flight 1's score.
        var task = MakeTwoGateTask(new AllFlights());

        var flights = new List<InterpretedFlight>
        {
            InterpretTwoGate(task, 1, flightTime: 100m, onField: true, inZone: true),
            InterpretTwoGate(task, 2, flightTime: 200m, onField: false, inZone: false),
        };

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights.ToImmutableArray());

        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be(100m);
        result.Selection.Should().NotBeNull();
        result.Selection!.Flights.Should().HaveCount(2); // zeroed flight still selected
        ScoreOf(result, 2).Should().Be(0m);
        ScoreOf(result, 1).Should().Be(100m);
    }

    [Fact]
    public void Both_gate_task_flight_failing_only_flight_gate_never_trips_task_gate()
    {
        // Disjoint-predicate carve-out: a flight failing ONLY flightValidWhen
        // (its task-gate metric is compliant) must not trip the task gate.
        var task = MakeTwoGateTask(new AllFlights());

        var flights = new List<InterpretedFlight>
        {
            InterpretTwoGate(task, 1, flightTime: 100m, onField: true, inZone: true),
            InterpretTwoGate(task, 2, flightTime: 200m, onField: false, inZone: true),
        };

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights.ToImmutableArray());

        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be(100m);
        result.Selection!.Flights.Should().HaveCount(2);
        ScoreOf(result, 2).Should().Be(0m);
    }

    [Fact]
    public void Both_gate_task_countable_flight_failing_task_gate_still_voids_cell()
    {
        // Negative control: WI-1's carve-out must not defang the F3B-C/F3F
        // semantics. A countable (flight-gate-passing) flight failing the task
        // gate voids the whole cell even with the flight gate live beside it
        // — the zeroed flight's task-gate compliance does not save it.
        var task = MakeTwoGateTask(new AllFlights());

        var flights = new List<InterpretedFlight>
        {
            InterpretTwoGate(task, 1, flightTime: 100m, onField: true, inZone: false),
            InterpretTwoGate(task, 2, flightTime: 200m, onField: false, inZone: true),
        };

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights.ToImmutableArray());

        result.State.Should().Be(TaskResultState.NoResult);
        result.RawScore.Should().Be(0m);
        result.Selection.Should().BeNull();
    }

    [Fact]
    public void Both_gate_task_lone_zeroed_LastFlight_is_valid_zero_not_NoResult()
    {
        // The F3K.9.3 precedent on a both-gate task: a lone zeroed LastFlight
        // yields Valid 0, never NoResult, predecessor not promoted.
        var task = MakeTwoGateTask(new LastFlight());

        var flights = new List<InterpretedFlight>
        {
            InterpretTwoGate(task, 1, flightTime: 100m, onField: true, inZone: true),
            InterpretTwoGate(task, 2, flightTime: 80m, onField: false, inZone: false),
        };

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights.ToImmutableArray());

        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be(0m);
        result.Selection.Should().NotBeNull();
        result.Selection!.Flights.Should().ContainSingle();
        ScoreOf(result, 2).Should().Be(0m); // last flight stays last
    }

    // ------------------------------------------------------ ClampAndRecompute regression

    [Fact]
    public void Clamp_recompute_does_not_un_zero_a_flight_zeroed_by_flight_gate()
    {
        // F5K Task A: the off-field 300 s flight ranks longest, takes the 240 s
        // target, and ClampAndRecompute re-scores every term on the clamped
        // metrics WITHOUT re-checking flightValidWhen — silently un-zeroing it
        // (240 instead of 0). The zero must survive the clamp: the zeroed
        // flight contributes exactly 0 and the raw score is the other three.
        var task = ResolveF5KTaskA();

        var flights = new List<InterpretedFlight>
        {
            InterpretF5K(task, 1, flightTime: 300m, landedOnField: false), // clamped to 240
            InterpretF5K(task, 2, flightTime: 200m),                       // clamped to 180
            InterpretF5K(task, 3, flightTime: 110m),                       // under 120 target
            InterpretF5K(task, 4, flightTime: 50m),                        // under 60 target
        };

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights.ToImmutableArray());

        result.State.Should().Be(TaskResultState.Valid);
        result.Selection!.Flights.Should().HaveCount(4);
        ScoreOf(result, 1).Should().Be(0m);
        ScoreOf(result, 2).Should().Be(180m); // clamping still applies to countable flights
        ScoreOf(result, 3).Should().Be(110m);
        ScoreOf(result, 4).Should().Be(50m);
        result.RawScore.Should().Be(340m); // 180 + 110 + 50
    }

    // ------------------------------------------------------ the invariant as a property

    /// <summary>
    /// The WI-2 invariant over random flag-vectors on a fixed two-gate task:
    /// raw == the sum over countable (flight-gate-passing) flights of their
    /// scores, every zeroed selected flight scores 0 and stays selected, and
    /// the cell is NoResult iff some countable selected flight fails the task
    /// gate. Fails today for vectors where a zeroed flight's original metrics
    /// fail the task gate — the unwitnessed conflict this story exists to fix.
    /// </summary>
    [Fact]
    public void Zeroing_and_task_gate_invariant_holds_over_random_flag_vectors()
    {
        var task = MakeTwoGateTask(new AllFlights());
        decimal[] flightTimes = [100m, 200m, 150m];

        (from onField in Gen.Bool.Array[3]
         from inZone in Gen.Bool.Array[3]
         select (onField, inZone))
        .Sample(flags =>
        {
            var flights = Enumerable.Range(1, 3)
                .Select(i => InterpretTwoGate(task, i, flightTimes[i - 1],
                    flags.onField[i - 1], flags.inZone[i - 1]))
                .ToImmutableArray();

            var result = FlightSelector.SelectAndScore(
                null, task, new Dictionary<string, MeasuredValue>(), flights);

            var voids = flags.onField.Zip(flags.inZone, (on, inZ) => on && !inZ).Any(v => v);

            if (voids)
            {
                result.State.Should().Be(TaskResultState.NoResult);
                result.Selection.Should().BeNull();
                result.RawScore.Should().Be(0m);
            }
            else
            {
                result.State.Should().Be(TaskResultState.Valid);
                result.Selection.Should().NotBeNull();
                result.Selection!.Flights.Should().HaveCount(3);
                result.RawScore.Should().Be(flags.onField
                    .Select((on, i) => on ? flightTimes[i] : 0m).Sum());
                foreach (var flight in result.Selection!.Flights)
                {
                    var sequence = (int)flight.Metrics["flight.sequence"].Number!.Value;
                    if (!flags.onField[sequence - 1])
                        flight.Score.Should().Be(0m);
                }
            }
        });
    }

    // ------------------------------------------------------ helpers

    private static decimal ScoreOf(TaskResult result, int sequence) =>
        result.Selection!.Flights.Single(f =>
            (int)f.Metrics["flight.sequence"].Number!.Value == sequence).Score;

    /// <summary>
    /// A synthetic task carrying BOTH gates on disjoint flag metrics: the
    /// flight gate reads "onField" (F17, zeroes one flight), the task gate
    /// reads "inZone" (F2, voids the cell). No class-specific knowledge —
    /// both gates are plain class data the engine reads generically.
    /// </summary>
    private static ResolvedTask MakeTwoGateTask(FlightSelection flights) => new(
        Code: "ZT", Name: "Two-gate synthetic",
        Metrics: ImmutableArray<MetricDefinition>.Empty,
        Flights: flights,
        Timing: new ResolvedTiming(WorkingTimeKind.Fixed, null, null, null),
        Group: null, Normalise: null,
        ValidWhen: FlagIs("inZone", true),
        FlightValidWhen: FlagIs("onField", true),
        RawScore: null, Reflight: null,
        Score: ImmutableArray.Create<ScoreTerm>(new RateTerm
        {
            MetricRef = "flightTime", Rate = 1, Cap = null, CapScope = CapScope.PerFlight
        }),
        ScoreNormalised: ImmutableArray<ScoreTerm>.Empty
    );

    private static Comparison FlagIs(string metric, bool value) => new()
    {
        LeftMetricRef = metric,
        Op = Comparator.EqualTo,
        RightValue = MeasuredValue.Of(value),
    };

    private static InterpretedFlight InterpretTwoGate(
        ResolvedTask task, int seq, decimal flightTime, bool onField, bool inZone)
    {
        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(flightTime),
            ["onField"] = MeasuredValue.Of(onField),
            ["inZone"] = MeasuredValue.Of(inZone),
        };
        return FlightInterpreter.Interpret(task, seq, metrics);
    }

    private static InterpretedFlight InterpretF5K(
        ResolvedTask task, int seq, decimal flightTime, bool landedOnField = true)
    {
        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(flightTime),
            ["launchAltitude"] = MeasuredValue.Of(60m), // exactly at NLH → no adjustment
            ["landedOnField"] = MeasuredValue.Of(landedOnField),
            ["landedInPilotArea"] = MeasuredValue.Of(true),
            ["overflewLandingWindow"] = MeasuredValue.Of(false),
        };
        return FlightInterpreter.Interpret(task, seq, metrics);
    }

    private static ResolvedTask ResolveF5KTaskA()
    {
        var taskA = SeedF5K.Definition.Phases[0].Tasks.Single(t => t.Code == "A");
        return ParameterResolver.ResolveTask(taskA, new Dictionary<string, MeasuredValue>
        {
            ["nlh"] = MeasuredValue.Of(60m),
            ["minPerGroup"] = MeasuredValue.Of(5m),
        }, []);
    }

    private static ResolvedTask ResolveF5KTaskB()
    {
        var taskB = SeedF5K.Definition.Phases[0].Tasks.Single(t => t.Code == "B");
        return ParameterResolver.ResolveTask(taskB, new Dictionary<string, MeasuredValue>
        {
            ["nlh"] = MeasuredValue.Of(60m),
            ["minPerGroup"] = MeasuredValue.Of(5m),
        }, []);
    }
}
