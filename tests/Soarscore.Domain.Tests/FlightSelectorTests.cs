using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Black-box sociable tests for FlightSelector (WI-4).
/// Tests flight selection, target assignment, validWhen, PerTask caps, and rounding.
/// </summary>
public class FlightSelectorTests
{
    // ------------------------------------------------------ LastFlight selection

    [Fact]
    public void LastFlight_selection_keeps_only_last()
    {
        var task = ResolveF3KTaskA();
        var flights = CreateFlights(task, [100m, 200m, 300m]);

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights);

        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be(300m);
        result.Selection.Should().NotBeNull();
        result.Selection!.Flights.Should().ContainSingle();
    }

    // ------------------------------------------------------ LastNFlights

    [Fact]
    public void LastNFlights_2_keeps_last_two()
    {
        var task = MakeLastNTask(2);
        var flights = CreateFlights(task, [100m, 200m, 300m, 400m]);

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights);

        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be(700m); // 300 + 400
    }

    // ------------------------------------------------------ AllFlights

    [Fact]
    public void AllFlights_selects_all_flights()
    {
        var task = MakeAllFlightsTask();
        var flights = CreateFlights(task, [100m, 200m, 300m]);

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights);

        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be(600m); // 100 + 200 + 300
    }

    // ------------------------------------------------------ BestNFlights by score

    [Fact]
    public void BestNFlights_3_by_score_keeps_best_3()
    {
        var task = MakeBestNByScoreTask(3);
        var flights = CreateFlights(task, [50m, 150m, 100m, 200m]);

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights);

        result.State.Should().Be(TaskResultState.Valid);
        // Best 3 by score: 200, 150, 100 = 450
        result.RawScore.Should().Be(450m);
    }

    // ------------------------------------------------------ F3K Task E Poker

    [Fact]
    public void F3K_TaskE_Poker_best_3_by_score()
    {
        // F3K.11.5: achieved target credits the target, best 3 flights
        // Flights: 45 (achieved), 0 (missed), 50 (achieved), 47 (achieved)
        // Best 3: 50, 47, 45 = 142
        var task = ResolveF3KTaskE();

        var flights = new List<InterpretedFlight>();
        // Flight 1: achieved 45
        flights.Add(InterpretFlight(task, 1, flightTime: 47m, targetTime: 45m));
        // Flight 2: missed
        flights.Add(InterpretFlight(task, 2, flightTime: 30m, targetTime: 60m));
        // Flight 3: achieved 50
        flights.Add(InterpretFlight(task, 3, flightTime: 52m, targetTime: 50m));
        // Flight 4: achieved 47
        flights.Add(InterpretFlight(task, 4, flightTime: 50m, targetTime: 47m));

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights.ToImmutableArray());

        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be(142m);
    }

    // ------------------------------------------------------ validWhen

    [Fact]
    public void ValidWhen_failing_returns_NoResult()
    {
        // F3B Task C has validWhen on courseCompleted and landedInDefinedArea
        var task = ResolveF3BTaskC();

        // Flight with courseCompleted=false → validWhen fails
        var flights = new List<InterpretedFlight>
        {
            InterpretFlight(task, 1, courseTime: 45m, courseCompleted: false, landedInDefinedArea: true)
        };

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), flights.ToImmutableArray());

        result.State.Should().Be(TaskResultState.NoResult);
        result.RawScore.Should().Be(0m);
        result.Selection.Should().BeNull();
    }

    // ------------------------------------------------------ Empty flights

    [Fact]
    public void Empty_flights_returns_NoResult()
    {
        var task = ResolveF3KTaskA();

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(),
            ImmutableArray<InterpretedFlight>.Empty);

        result.State.Should().Be(TaskResultState.NoResult);
        result.RawScore.Should().Be(0m);
    }

    // ------------------------------------------------------ helpers

    private static ResolvedTask ResolveF3KTaskA()
    {
        var f3k = SeedF3K.Definition;
        var taskA = f3k.Phases[0].Tasks.Single(t => t.Code == "A");
        return ParameterResolver.ResolveTask(taskA, new Dictionary<string, MeasuredValue>
        {
            ["workingTime.A"] = MeasuredValue.Of(600m),
        });
    }

    private static ResolvedTask ResolveF3KTaskE()
    {
        var f3k = SeedF3K.Definition;
        var taskE = f3k.Phases[0].Tasks.Single(t => t.Code == "E");
        return ParameterResolver.ResolveTask(taskE, new Dictionary<string, MeasuredValue>
        {
            ["workingTime.E"] = MeasuredValue.Of(600m),
        });
    }

    private static ResolvedTask ResolveF3BTaskC()
    {
        var f3b = SeedF3B.Definition;
        var taskC = f3b.Phases[0].Tasks.Single(t => t.Code == "C");
        return ParameterResolver.ResolveTask(taskC, new Dictionary<string, MeasuredValue>());
    }

    private static ResolvedTask ResolveF5KTaskA()
    {
        var f5k = SeedF5K.Definition;
        var taskA = f5k.Phases[0].Tasks.Single(t => t.Code == "A");
        return ParameterResolver.ResolveTask(taskA, new Dictionary<string, MeasuredValue>
        {
            ["nlh"] = MeasuredValue.Of(60m),
            ["minPerGroup"] = MeasuredValue.Of(5m),
        });
    }

    private static ResolvedTask MakeLastNTask(int count)
    {
        return ResolveF3KTaskA() with
        {
            Flights = new LastNFlights(count),
            ValidWhen = null,
            FlightValidWhen = null,
            // Remove 300s cap so flights score their full flight time
            Score = ImmutableArray.Create<ScoreTerm>(new RateTerm
            {
                MetricRef = "flightTime", Rate = 1, Cap = null, CapScope = CapScope.PerFlight
            }),
        };
    }

    private static ResolvedTask MakeAllFlightsTask()
    {
        return ResolveF3KTaskA() with
        {
            Flights = new AllFlights(),
            ValidWhen = null,
            FlightValidWhen = null,
            Score = ImmutableArray.Create<ScoreTerm>(new RateTerm
            {
                MetricRef = "flightTime", Rate = 1, Cap = null, CapScope = CapScope.PerFlight
            }),
        };
    }

    private static ResolvedTask MakeBestNByScoreTask(int count)
    {
        return ResolveF3KTaskA() with
        {
            Flights = new BestNFlights { Count = count },
            ValidWhen = null,
            FlightValidWhen = null,
            Score = ImmutableArray.Create<ScoreTerm>(new RateTerm
            {
                MetricRef = "flightTime", Rate = 1, Cap = null, CapScope = CapScope.PerFlight
            }),
        };
    }

    // ------------------------------------------------------ PerTask cap test fixture

    [Fact]
    public void CapScope_PerTask_caps_total_metric_across_flights()
    {
        // Simple rate 1 pt/s cap 599 perTask. 4 flights at 150s → total 600, capped 599.
        var task = MakePerTaskCappedTask();

        var flights = new List<InterpretedFlight>();
        for (int i = 1; i <= 4; i++)
        {
            flights.Add(InterpretFlightSimple(task, i, 150m));
        }

        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(),
            flights.ToImmutableArray());

        // 4 × 150 = 600, PerTask cap 599 → reduction (600-599)*1 = 1 → 599
        result.State.Should().Be(TaskResultState.Valid);
        result.RawScore.Should().Be(599m);
    }

    private static ResolvedTask MakePerTaskCappedTask()
    {
        return new ResolvedTask(
            Code: "T", Name: "Test",
            Metrics: ImmutableArray<MetricDefinition>.Empty,
            Flights: new AllFlights(),
            Timing: new ResolvedTiming(WorkingTimeKind.Fixed, null, null, null),
            Group: null, Normalise: null, ValidWhen: null, FlightValidWhen: null,
            RawScore: null, Reflight: null,
            Score: ImmutableArray.Create<ScoreTerm>(new RateTerm
            {
                MetricRef = "flightTime",
                Rate = 1,
                Cap = 599m,  // implicit conversion to NumberOrParam.Literal
                CapScope = CapScope.PerTask,
            }),
            ScoreNormalised: ImmutableArray<ScoreTerm>.Empty
        );
    }

    private ImmutableArray<InterpretedFlight> CreateFlights(ResolvedTask task, decimal[] flightTimes)
    {
        return flightTimes.Select((ft, i) =>
            InterpretFlightSimple(task, i + 1, ft)
        ).ToImmutableArray();
    }

    private static InterpretedFlight InterpretFlightSimple(ResolvedTask task, int seq, decimal flightTime)
    {
        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(flightTime),
            ["landedWithinWindow"] = MeasuredValue.Of(true),
            ["launchedInWorkingTime"] = MeasuredValue.Of(true),
            ["flight.sequence"] = MeasuredValue.Of(seq),
        };
        return FlightInterpreter.Interpret(null, task, seq, metrics);
    }

    private static InterpretedFlight InterpretFlight(
        ResolvedTask task, int seq,
        decimal flightTime = 0, decimal targetTime = 0,
        decimal courseTime = 0, bool courseCompleted = true, bool landedInDefinedArea = true,
        decimal launchAltitude = 60m)
    {
        var metrics = new Dictionary<string, MeasuredValue>();
        if (task.Code == "E") // Poker
        {
            metrics["flightTime"] = MeasuredValue.Of(flightTime);
            metrics["targetTime"] = MeasuredValue.Of(targetTime);
            metrics["landedWithinWindow"] = MeasuredValue.Of(true);
            metrics["launchedInWorkingTime"] = MeasuredValue.Of(true);
        }
        else if (task.Code == "C") // F3B Speed
        {
            metrics["courseTime"] = MeasuredValue.Of(courseTime);
            metrics["courseCompleted"] = MeasuredValue.Of(courseCompleted);
            metrics["landedInDefinedArea"] = MeasuredValue.Of(landedInDefinedArea);
        }
        else if (task.Code == "A") // F5K
        {
            metrics["flightTime"] = MeasuredValue.Of(flightTime);
            metrics["launchAltitude"] = MeasuredValue.Of(launchAltitude);
            metrics["landedOnField"] = MeasuredValue.Of(true);
            metrics["landedInPilotArea"] = MeasuredValue.Of(true);
            metrics["overflewLandingWindow"] = MeasuredValue.Of(false);
        }
        return FlightInterpreter.Interpret(null, task, seq, metrics);
    }
}
