using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Targeted arithmetic locks for the three NZ NDC seed classes
/// (kanban/in-progress/nz-ndc-seed-classes.md WI-5), driven through the same
/// black-box harness as FlightInterpreterTests: seed TaskDefinitions resolved
/// through ParameterResolver, evaluated by FlightInterpreter.
///
/// These are the tests that lock the launch-adjustment SIGN CONVENTION: the
/// below-origin bonus bands in SeedF5kNdc are POSITIVE-rate because
/// FlightInterpreter.EvaluatePiecewise integrates band rates over the UNSIGNED
/// width of [0, metric − origin]. A future signed-width evaluator (see
/// kanban/tech-debt.md — the FAI F5K below-bonus finding) would flip them, and
/// these tests are what fails when that happens.
///
/// Every expected value below is a number the rulebook itself states —
/// NZ.3.16.29's worked table, NZ.3.14.2/3's maxima, NZ.0.3 f's restated
/// 5.5.11.12 arithmetic — not a value this suite derived.
/// </summary>
public class NzNdcSeedArithmeticTests
{
    // ------------------------------------------------------ NZ F5K NDC launch adjustment

    /// <summary>
    /// NZ.3.16.29's own worked table (NLH 60): the LaunchAdjustment bands must
    /// reproduce every row. flightTime is 0 so the score IS the adjustment.
    /// </summary>
    [Theory]
    [InlineData(41, 30)]   // "41 meter : 30 seconds launch bonus" (Task B example)
    [InlineData(50, 12)]   // "50 meter : 12 seconds launch bonus" (Task A example)
    [InlineData(51, 10)]   // 51 (10 seconds)
    [InlineData(52, 8)]
    [InlineData(53, 6)]
    [InlineData(54, 4)]
    [InlineData(55, 3)]    // "55 meter : 3 seconds launch bonus" (Task A example)
    [InlineData(56, 2)]
    [InlineData(57, 1)]
    [InlineData(58, 0)]    // "No bonus for heights : 59 (0 seconds) and 58 (0 seconds)"
    [InlineData(59, 0)]
    [InlineData(60, 0)]    // exactly at the NLH
    [InlineData(61, 0)]    // "No launch penalty for heights : 61 (0 seconds) and 62 (0 seconds)"
    [InlineData(62, 0)]
    [InlineData(63, -1)]
    [InlineData(64, -2)]
    [InlineData(65, -3)]   // "65 meter : 3 seconds launch penalty" (Task A example)
    [InlineData(66, -4)]
    [InlineData(67, -6)]
    [InlineData(68, -8)]
    [InlineData(69, -10)]
    [InlineData(70, -12)]  // "70 meter : 12 seconds launch penalty" (Task A example)
    public void NzF5kNdc_launch_adjustment_reproduces_the_NZ_3_16_29_table(decimal altitude, decimal expected)
    {
        var task = ResolveNzF5kNdcTaskA();

        var result = FlightInterpreter.Interpret(task, 1, NzF5kMetrics(altitude));

        result.Score.Should().Be(expected,
            "NZ.3.16.29's worked table is the class's own arithmetic check");
    }

    // ------------------------------------------------------ X5J

    [Fact]
    public void X5J_clean_flight_is_glide_plus_landing()
    {
        // glide 300 s + landing 50 (inside 1 m, NZ.2.4.5) = 350.
        var task = ResolveX5jTaskD();

        var result = FlightInterpreter.Interpret(task, 1, X5jMetrics(
            glideTime: 300, restartRun: 0, restarted: false, airborneAtEnd: false, within75m: true, landing: 1));

        result.Score.Should().Be(350m);
    }

    [Fact]
    public void X5J_motor_restart_deducts_its_seconds_and_forfeits_the_landing()
    {
        // NZ.3.14.2 e: 300 glide less 20 restart seconds, no landing points:
        // 300 − 20 = 280.
        var task = ResolveX5jTaskD();

        var result = FlightInterpreter.Interpret(task, 1, X5jMetrics(
            glideTime: 300, restartRun: 20, restarted: true, airborneAtEnd: false, within75m: true, landing: 1));

        result.Score.Should().Be(280m);
    }

    [Fact]
    public void X5J_still_airborne_at_the_end_of_the_working_time_keeps_glide_but_loses_landing()
    {
        // NZ.3.14.2 (second d): the watch stops at the end of working time and
        // no landing points are awarded. There is no over-time deduction to
        // assert against — contrast the M-NDC Rest(-1) band.
        var task = ResolveX5jTaskD();

        var result = FlightInterpreter.Interpret(task, 1, X5jMetrics(
            glideTime: 590, restartRun: 0, restarted: false, airborneAtEnd: true, within75m: true, landing: 1));

        result.Score.Should().Be(590m);
    }

    [Fact]
    public void X5J_landing_beyond_75m_zeroes_the_flight()
    {
        // NZ.2.4.6 via FlightValidWhen.
        var task = ResolveX5jTaskD();

        var result = FlightInterpreter.Interpret(task, 1, X5jMetrics(
            glideTime: 300, restartRun: 0, restarted: false, airborneAtEnd: false, within75m: false, landing: 1));

        result.Score.Should().Be(0m);
    }

    // ------------------------------------------------------ F5J NDC

    [Fact]
    public void F5jNdc_perfect_200m_launch_round_scores_550_not_1000()
    {
        // NZ.0.3 d: the raw sum. 600 flight (5.5.11.12 c) + 50 landing (h)
        // − 100 start-height deduction at exactly 200 m (0.5/m, e) = 550.
        // An evaluator that normalised the group (5.5.11.12 m) would produce
        // 1000 — this assertion is the raw-sum lock.
        var task = ResolveF5jNdcTaskD();

        var result = FlightInterpreter.Interpret(task, 1, F5jNdcMetrics(
            flightTime: 600, startHeight: 200, overfly: 0, touched: false, heightRecorded: true, within75m: true, landing: 1));

        result.Score.Should().Be(550m);
    }

    [Fact]
    public void F5jNdc_within_one_minute_overfly_keeps_flight_points_and_loses_landing()
    {
        // 5.5.11.12 c/g/k via NZ.0.3 f/g: 600 flight (capped at the working
        // time), landing forfeited (k), no per-second overtime deduction (the
        // rules state none), − 100 at a 200 m launch = 500.
        var task = ResolveF5jNdcTaskD();

        var result = FlightInterpreter.Interpret(task, 1, F5jNdcMetrics(
            flightTime: 600, startHeight: 200, overfly: 30, touched: false, heightRecorded: true, within75m: true, landing: 1));

        result.Score.Should().Be(500m);
    }

    [Fact]
    public void F5jNdc_overflying_by_more_than_a_minute_zeroes_the_flight()
    {
        // 5.5.11.12 g "a zero score will be recorded", via FlightValidWhen.
        var task = ResolveF5jNdcTaskD();

        var result = FlightInterpreter.Interpret(task, 1, F5jNdcMetrics(
            flightTime: 600, startHeight: 200, overfly: 61, touched: false, heightRecorded: true, within75m: true, landing: 1));

        result.Score.Should().Be(0m);
    }

    [Fact]
    public void F5jNdc_landing_beyond_75m_zeroes_the_flight()
    {
        // NZ.0.3 h / 5.5.11.7 d — the metric SeedF5J omits and this class adds.
        var task = ResolveF5jNdcTaskD();

        var result = FlightInterpreter.Interpret(task, 1, F5jNdcMetrics(
            flightTime: 600, startHeight: 200, overfly: 0, touched: false, heightRecorded: true, within75m: false, landing: 1));

        result.Score.Should().Be(0m);
    }

    // ------------------------------------------------------ helpers

    private static ResolvedTask ResolveNzF5kNdcTaskA()
    {
        var taskA = SeedF5kNdc.Definition.Phases[0].Tasks.Single(t => t.Code == "A");
        return ParameterResolver.ResolveTask(taskA, new Dictionary<string, MeasuredValue>
        {
            ["minPerGroup"] = MeasuredValue.Of(5m),
        }, []);
    }

    private static Dictionary<string, MeasuredValue> NzF5kMetrics(decimal altitude) => new()
    {
        ["flightTime"] = MeasuredValue.Of(0m),           // the rate term contributes 0; the score IS the adjustment
        ["launchAltitude"] = MeasuredValue.Of(altitude),
        ["landedInLandingArea"] = MeasuredValue.Of(true),
        ["overflewLandingWindow"] = MeasuredValue.Of(false),
        ["launchedInWindow"] = MeasuredValue.Of(true),
        ["touchedBeforeStop"] = MeasuredValue.Of(false),
    };

    private static ResolvedTask ResolveX5jTaskD()
    {
        var taskD = SeedX5j.Definition.Phases[0].Tasks.Single(t => t.Code == "D");
        return ParameterResolver.ResolveTask(taskD, new Dictionary<string, MeasuredValue>
        {
            ["minNewGroup"] = MeasuredValue.Of(4m),
        }, []);
    }

    private static Dictionary<string, MeasuredValue> X5jMetrics(
        decimal glideTime, decimal restartRun, bool restarted, bool airborneAtEnd, bool within75m, decimal landing) => new()
    {
        ["glideTime"] = MeasuredValue.Of(glideTime),
        ["motorRestartRunTime"] = MeasuredValue.Of(restartRun),
        ["motorRestarted"] = MeasuredValue.Of(restarted),
        ["airborneAtRoundEnd"] = MeasuredValue.Of(airborneAtEnd),
        ["landedWithin75m"] = MeasuredValue.Of(within75m),
        ["landingDistance"] = MeasuredValue.Of(landing),
    };

    private static ResolvedTask ResolveF5jNdcTaskD()
    {
        var taskD = SeedF5jNdc.Definition.Phases[0].Tasks.Single(t => t.Code == "D");
        return ParameterResolver.ResolveTask(taskD, new Dictionary<string, MeasuredValue>(), []);
    }

    private static Dictionary<string, MeasuredValue> F5jNdcMetrics(
        decimal flightTime, decimal startHeight, decimal overfly, bool touched, bool heightRecorded, bool within75m, decimal landing) => new()
    {
        ["flightTime"] = MeasuredValue.Of(flightTime),
        ["startHeight"] = MeasuredValue.Of(startHeight),
        ["startHeightRecorded"] = MeasuredValue.Of(heightRecorded),
        ["landingDistance"] = MeasuredValue.Of(landing),
        ["overflySeconds"] = MeasuredValue.Of(overfly),
        ["touchedByCompetitor"] = MeasuredValue.Of(touched),
        ["landedWithin75m"] = MeasuredValue.Of(within75m),
    };
}
