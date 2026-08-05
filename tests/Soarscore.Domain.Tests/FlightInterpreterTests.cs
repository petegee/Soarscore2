using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Black-box sociable tests for FlightInterpreter (WI-3) and PredicateEvaluator.
/// Uses real seed-data TaskDefinitions resolved through ParameterResolver.
/// Tests focus on the worked examples from the test strategy.
/// </summary>
public class FlightInterpreterTests
{
    // ------------------------------------------------------ F3B Task A

    [Fact]
    public void F3B_TaskA_601s_with_landing_scores_599()
    {
        // F3B Task A: flightTime=601, landedInDefinedArea=true → flight points = 599
        // (600×1 + 1×(−1)). Landing bonus: flightTime > 630 so it's forfeit.
        var task = ResolveF3BTaskA();

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(601m),
            ["landedInDefinedArea"] = MeasuredValue.Of(true),
            ["atRestBy12Min"] = MeasuredValue.Of(true),
            ["touchedByCompetitor"] = MeasuredValue.Of(false),
            ["landingDistance"] = MeasuredValue.Of(1m),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Result.State.Should().Be(FlightResultState.Valid);
        // 601s: piecewise 600×1 + 1×(−1) = 599. Landing bonus: 601 ≤ 630, so
        // landing bonus applies: 100 points. Total: 699. This confirms the
        // piecewise bands are cumulative (600 not 601 for the first band).
        result.Score.Should().Be(699m);
    }

    [Fact]
    public void F3B_TaskA_601s_no_landing_scores_0()
    {
        // flightTime=601, landedInDefinedArea=false → score 0
        // (when predicate fails, no else → 0)
        var task = ResolveF3BTaskA();

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(601m),
            ["landedInDefinedArea"] = MeasuredValue.Of(false),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Result.State.Should().Be(FlightResultState.Valid);
        result.Score.Should().Be(0m);
    }

    [Fact]
    public void F3B_TaskA_with_landing_bonus()
    {
        // flightTime=540, landingDistance=1m, all flags good → score 640 (540 flight + 100 landing)
        var task = ResolveF3BTaskA();

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(540m),
            ["landedInDefinedArea"] = MeasuredValue.Of(true),
            ["landingDistance"] = MeasuredValue.Of(1m),
            ["atRestBy12Min"] = MeasuredValue.Of(true),
            ["touchedByCompetitor"] = MeasuredValue.Of(false),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Result.State.Should().Be(FlightResultState.Valid);
        result.Score.Should().Be(640m);
    }

    // ------------------------------------------------------ F3K Task A

    [Fact]
    public void F3K_TaskA_200s_valid_scores_200()
    {
        var task = ResolveF3KTaskA();

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(200m),
            ["landedWithinWindow"] = MeasuredValue.Of(true),
            ["launchedInWorkingTime"] = MeasuredValue.Of(true),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Result.State.Should().Be(FlightResultState.Valid);
        result.Score.Should().Be(200m);
    }

    [Fact]
    public void F3K_TaskA_landedOutsideWindow_zeroed_but_valid()
    {
        // flightTime=200, landedWithinWindow=false → score 0, State=Valid (zeroed, still counted)
        var task = ResolveF3KTaskA();

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(200m),
            ["landedWithinWindow"] = MeasuredValue.Of(false),
            ["launchedInWorkingTime"] = MeasuredValue.Of(true),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Result.State.Should().Be(FlightResultState.Valid);
        result.Score.Should().Be(0m);
    }

    // ------------------------------------------------------ F3K Task E (Poker)

    [Fact]
    public void F3K_TaskE_target_achieved_scores_target()
    {
        // flightTime=46, targetTime=45 → conditional true → rate targetTime → 45
        var task = ResolveF3KTaskE();

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(46m),
            ["targetTime"] = MeasuredValue.Of(45m),
            ["landedWithinWindow"] = MeasuredValue.Of(true),
            ["launchedInWorkingTime"] = MeasuredValue.Of(true),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Result.State.Should().Be(FlightResultState.Valid);
        result.Score.Should().Be(45m);
    }

    [Fact]
    public void F3K_TaskE_target_missed_scores_0()
    {
        // flightTime=44, targetTime=45 → conditional false → score 0 (no else)
        var task = ResolveF3KTaskE();

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(44m),
            ["targetTime"] = MeasuredValue.Of(45m),
            ["landedWithinWindow"] = MeasuredValue.Of(true),
            ["launchedInWorkingTime"] = MeasuredValue.Of(true),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Result.State.Should().Be(FlightResultState.Valid);
        result.Score.Should().Be(0m);
    }

    // ------------------------------------------------------ F5K Task A

    [Fact]
    public void F5K_TaskA_launch_at_NLH_plus_15_scores_minus_25()
    {
        // launchAltitude at NLH+15 with bands any..0 @ -0.5, 0..10 @ -1, 10..any @ -3
        // origin=60m (nlh) → adjusted=15 → portions:
        // 0..10 @ -1 (10 * -1 = -10), 10..15 @ -3 (5 * -3 = -15) → total -25
        var task = ResolveF5KTaskA();

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(180m),
            ["launchAltitude"] = MeasuredValue.Of(75m), // NLH 60 + 15
            ["landedOnField"] = MeasuredValue.Of(true),
            ["landedInPilotArea"] = MeasuredValue.Of(true),
            ["overflewLandingWindow"] = MeasuredValue.Of(false),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Result.State.Should().Be(FlightResultState.Valid);
        // flightTime=180, launchAltitude at NLH+15 → -25. No land/pilot/overfly deductions.
        result.Score.Should().Be(155m);
    }

    [Fact]
    public void F5K_TaskA_pilot_area_deduction()
    {
        var task = ResolveF5KTaskA();

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(180m),
            ["launchAltitude"] = MeasuredValue.Of(60m), // exactly at NLH → 0 adjustment
            ["landedOnField"] = MeasuredValue.Of(true),
            ["landedInPilotArea"] = MeasuredValue.Of(false),
            ["overflewLandingWindow"] = MeasuredValue.Of(false),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Result.State.Should().Be(FlightResultState.Valid);
        // flightTime=180, launch at NLH → 0, pilot area → -10. Total: 170
        result.Score.Should().Be(170m);
    }

    // ------------------------------------------------------ flight.sequence intrinsic

    [Fact]
    public void Flight_sequence_intrinsic_is_available()
    {
        var task = ResolveF5KTaskE();

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(180m),
            ["launchAltitude"] = MeasuredValue.Of(60m),
            ["landedOnField"] = MeasuredValue.Of(true),
            ["landedInPilotArea"] = MeasuredValue.Of(true),
            ["overflewLandingWindow"] = MeasuredValue.Of(false),
            ["targetTime"] = MeasuredValue.Of(150m),
            ["flight.sequence"] = MeasuredValue.Of(3),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Result.State.Should().Be(FlightResultState.Valid);
        result.Result.Measurements.Metrics.ContainsKey("flight.sequence").Should().BeTrue();
    }

    // ------------------------------------------------------ LookupTerm

    [Fact]
    public void LookupTerm_ascending_rows_select_first_matching()
    {
        var lookupTerm = new LookupTerm
        {
            MetricRef = "test",
            Rows = new[]
            {
                new LookupRow(UpTo: 1m, Points: 10m),
                new LookupRow(UpTo: 3m, Points: 20m),
                new LookupRow(UpTo: 5m, Points: 30m),
                new LookupRow(UpTo: null, Points: 40m),
            }.ToImmutableArray()
        };

        var task = new ResolvedTask(
            Code: "T", Name: "Test",
            Metrics: ImmutableArray<MetricDefinition>.Empty,
            Flights: new AllFlights(),
            Timing: new ResolvedTiming(WorkingTimeKind.Fixed, null, null, null),
            Group: null, Normalise: null, ValidWhen: null, FlightValidWhen: null,
            RawScore: null, Reflight: null,
            Score: ImmutableArray.Create<ScoreTerm>(lookupTerm),
            ScoreNormalised: ImmutableArray<ScoreTerm>.Empty
        );

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["test"] = MeasuredValue.Of(3m),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Score.Should().Be(20m);
    }

    // ------------------------------------------------------ PiecewiseTerm unbounded

    [Fact]
    public void PiecewiseTerm_unbounded_bands_work_correctly()
    {
        var piecewise = new PiecewiseTerm
        {
            MetricRef = "test",
            Bands = new[]
            {
                new Band(From: null, To: 100m, RatePerUnit: 1m),
                new Band(From: 100m, To: null, RatePerUnit: 2m),
            }.ToImmutableArray()
        };

        var task = new ResolvedTask(
            Code: "T", Name: "Test",
            Metrics: ImmutableArray<MetricDefinition>.Empty,
            Flights: new AllFlights(),
            Timing: new ResolvedTiming(WorkingTimeKind.Fixed, null, null, null),
            Group: null, Normalise: null, ValidWhen: null, FlightValidWhen: null,
            RawScore: null, Reflight: null,
            Score: ImmutableArray.Create<ScoreTerm>(piecewise),
            ScoreNormalised: ImmutableArray<ScoreTerm>.Empty
        );

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["test"] = MeasuredValue.Of(150m),
            ["flight.sequence"] = MeasuredValue.Of(1),
        };

        var result = FlightInterpreter.Interpret(null, task, 1, metrics);

        result.Score.Should().Be(200m);
    }

    // ------------------------------------------------------ PredicateEvaluator

    [Fact]
    public void PredicateEvaluator_AllOf_all_pass_returns_true()
    {
        var pred = new AllOf
        {
            Children = ImmutableArray.Create<Predicate>(
                new Comparison { LeftMetricRef = "a", Op = Comparator.EqualTo,
                    RightValue = MeasuredValue.Of(true) },
                new Comparison { LeftMetricRef = "b", Op = Comparator.GreaterThan,
                    RightValue = MeasuredValue.Of(10m) }
            )
        };

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["a"] = MeasuredValue.Of(true),
            ["b"] = MeasuredValue.Of(15m),
        };

        PredicateEvaluator.Evaluate(pred, metrics).Should().BeTrue();
    }

    [Fact]
    public void PredicateEvaluator_AllOf_one_fails_returns_false()
    {
        var pred = new AllOf
        {
            Children = ImmutableArray.Create<Predicate>(
                new Comparison { LeftMetricRef = "a", Op = Comparator.EqualTo,
                    RightValue = MeasuredValue.Of(true) },
                new Comparison { LeftMetricRef = "b", Op = Comparator.GreaterThan,
                    RightValue = MeasuredValue.Of(10m) }
            )
        };

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["a"] = MeasuredValue.Of(true),
            ["b"] = MeasuredValue.Of(5m),
        };

        PredicateEvaluator.Evaluate(pred, metrics).Should().BeFalse();
    }

    [Fact]
    public void PredicateEvaluator_flag_comparison_invalid_op_throws()
    {
        var pred = new Comparison
        {
            LeftMetricRef = "f",
            Op = Comparator.GreaterThan,  // invalid for flags
            RightValue = MeasuredValue.Of(true),
        };

        var metrics = new Dictionary<string, MeasuredValue>
        {
            ["f"] = MeasuredValue.Of(true),
        };

        FluentActions.Invoking(() => PredicateEvaluator.Evaluate(pred, metrics))
            .Should().Throw<ArgumentException>();
    }

    // ------------------------------------------------------ helpers

    private static ResolvedTask ResolveF3BTaskA()
    {
        var f3b = SeedF3B.Definition;
        var taskA = f3b.Phases[0].Tasks.Single(t => t.Code == "A");
        return ParameterResolver.ResolveTask(taskA, new Dictionary<string, MeasuredValue>());
    }

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

    private static ResolvedTask ResolveF5KTaskE()
    {
        var f5k = SeedF5K.Definition;
        var taskE = f5k.Phases[0].Tasks.Single(t => t.Code == "E");
        return ParameterResolver.ResolveTask(taskE, new Dictionary<string, MeasuredValue>
        {
            ["nlh"] = MeasuredValue.Of(60m),
            ["minPerGroup"] = MeasuredValue.Of(5m),
        });
    }
}
