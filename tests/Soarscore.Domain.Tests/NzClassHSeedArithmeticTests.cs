using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Targeted arithmetic locks for the NZ Class H seed (Thermal 2 Metre,
/// kanban/in-progress/nz-class-h-thermal-2m-seed.md WI-5), driven through the
/// same black-box harness as NzNdcSeedArithmeticTests: seed TaskDefinitions
/// resolved through ParameterResolver, evaluated by FlightInterpreter.
///
/// Every expected value is a number NZ.5.5 itself states or implies — one
/// point per second to the target ((e)(i)), minus one per second over
/// ((e)(ii), the Class N worked example NZ.7.5(d) fixes that reading for
/// identical wording), forfeit beyond 60 s over ((e)(iii)), landing 50 within
/// 15 m measured to the nose ((f)(i)) — or an owner decision recorded in the
/// story (the forfeit takes the landing bonus with it; the 15 m boundary
/// rounding).
/// </summary>
public class NzClassHSeedArithmeticTests
{
    // ------------------------------------------------------ per-task maxima

    /// <summary>
    /// The class's own arithmetic check: per-task max = target + landing 50 →
    /// 230 / 290 / 350 / 410 / 470, and the contest max (all five counted, no
    /// discard, raw sum) = 1750. An evaluator that produces more has
    /// normalised something, applied a discard, or awarded a landing bonus to
    /// a forfeited flight.
    /// </summary>
    [Theory]
    [InlineData(3, 230)]
    [InlineData(4, 290)]
    [InlineData(5, 350)]
    [InlineData(6, 410)]
    [InlineData(7, 470)]
    public void ClassH_per_task_maximum_is_target_plus_landing(int minutes, decimal expected)
    {
        var task = ResolveClassHTask(minutes);

        var result = FlightInterpreter.Interpret(task, 1, ClassHMetrics(minutes * 60, landing: 1));

        result.Score.Should().Be(expected);
    }

    // ------------------------------------------------------ the flight score

    [Fact]
    public void ClassH_under_target_scores_one_point_per_second()
    {
        // NZ.5.5(e)(i): a 150 s flight against the 3-minute target scores 150
        // — partial credit, no clamping (contrast F3K.11.8's target clamp).
        var task = ResolveClassHTask(3);

        var result = FlightInterpreter.Interpret(task, 1, ClassHMetrics(150, landing: 20));

        result.Score.Should().Be(150m);
    }

    [Fact]
    public void ClassH_overtime_is_deducted_one_point_per_second()
    {
        // NZ.5.5(e)(ii): 200 s against the 3-minute target = 180×1 + 20×(−1)
        // = 160 — the Class N worked example's reading (400 s → 320).
        var task = ResolveClassHTask(3);

        var result = FlightInterpreter.Interpret(task, 1, ClassHMetrics(200, landing: 20));

        result.Score.Should().Be(160m);
    }

    [Fact]
    public void ClassH_exactly_sixty_seconds_over_still_scores()
    {
        // NZ.5.5(e)(iii) "MORE than 60 secs": at exactly 60 s over the flight
        // scores target − 60 = 120. The clause's floor is a forfeit, not a
        // clamp.
        var task = ResolveClassHTask(3);

        var result = FlightInterpreter.Interpret(task, 1, ClassHMetrics(240, landing: 20));

        result.Score.Should().Be(120m);
    }

    [Fact]
    public void ClassH_beyond_sixty_seconds_over_forfeits_the_whole_flight_including_the_landing()
    {
        // NZ.5.5(e)(iii) via FlightValidWhen, owner decision 2: one second
        // past the forfeit line the flight — flight points AND landing bonus
        // — is zero, even landing on the spot. This is the cliff: 120 at
        // 240 s, 0 at 241 s.
        var task = ResolveClassHTask(3);

        var result = FlightInterpreter.Interpret(task, 1, ClassHMetrics(241, landing: 1));

        result.Score.Should().Be(0m);
    }

    // ------------------------------------------------------ the landing bonus

    [Fact]
    public void ClassH_landing_inside_the_fifteen_metre_circle_scores_fifty_and_the_flight_stands()
    {
        // NZ.5.5(f)(i): 50 points within the 15 m radius, measured to the
        // nose — the class's own single-step bonus, not the NZ.4.12/NZ.4.13
        // tables. 100 s flight + 50 = 150.
        var task = ResolveClassHTask(3);

        var result = FlightInterpreter.Interpret(task, 1, ClassHMetrics(100, landing: 7));

        result.Score.Should().Be(150m);
    }

    [Theory]
    [InlineData(14.9, 50)]  // Ceiling/1 m capture: 14.9 reads 15 — inside
    [InlineData(15, 50)]    // exactly on the circle
    [InlineData(15.1, 0)]   // 15.1 reads 16 — outside; the bonus is zero, the flight stands
    public void ClassH_landing_boundary_follows_the_ceiling_capture_rounding(decimal landing, decimal landingPoints)
    {
        // The F12 residual (no capture precision stated): Ceiling/1 m chosen
        // (the NZ.4.13 "next full metre" convention) — never awards a bonus
        // to a genuinely-outside reading.
        var task = ResolveClassHTask(3);

        var result = FlightInterpreter.Interpret(task, 1, ClassHMetrics(60, landing));

        result.Score.Should().Be(60 + landingPoints);
    }

    [Fact]
    public void ClassH_landing_outside_the_circle_keeps_the_flight_points()
    {
        // NZ.5.5(f)(i): outside the 15 m radius only the bonus is zero —
        // contrast the outside-the-FIELD penalty (landedOutsideFieldBounds),
        // which zeroes the whole flight and is a recorded infraction.
        var task = ResolveClassHTask(5);

        var result = FlightInterpreter.Interpret(task, 1, ClassHMetrics(280, landing: 16));

        result.Score.Should().Be(280m);
    }

    // ------------------------------------------------------ helpers

    private static ResolvedTask ResolveClassHTask(int minutes)
    {
        var task = SeedNzHThermal2m.Definition.Phases[0].Tasks.Single(t => t.Code == $"{minutes}");
        return ParameterResolver.ResolveTask(task, new Dictionary<string, MeasuredValue>(), []);
    }

    private static Dictionary<string, MeasuredValue> ClassHMetrics(decimal flightTime, decimal landing) => new()
    {
        ["flightTime"] = MeasuredValue.Of(flightTime),
        ["landingDistance"] = MeasuredValue.Of(landing),
    };
}