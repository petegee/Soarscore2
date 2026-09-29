// TermBreakdownPropertyTests — kanban/in-progress/per-term-score-breakdown.md WI-4.
//
// The named invariant: for any task and any metric inputs, the sum of a
// selected flight's term Points equals that flight's pre-cap, pre-rounding
// score (the interpreter's own addition loop, FlightInterpreter.cs:94-99) —
// and every surfaced Points value is the exact TermContribution.Points the
// engine resolved (reference-decimal equality, never a re-computation). The
// wire projection (ScoreTaskRoundHandler.ProjectTerms) forwards those same
// contribution objects, so holding the invariant at the engine grain holds
// the breakdown's side of the no-silent-mismatch guard.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

public class TermBreakdownPropertyTests
{
    private static readonly Gen<decimal> FlightTime = Gen.Int[0, 700].Select(i => (decimal)i);
    private static readonly Gen<decimal> LandingDistance = Gen.Int[0, 30].Select(i => (decimal)i);

    private static ResolvedTask RatePlusLookupTask() => new(
        Code: "T", Name: "Test",
        Metrics:
        [
            new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" },
            new MetricDefinition { Name = "landingDistance", Kind = MeasuredKind.Number, Unit = "m" },
        ],
        Flights: new LastFlight(),
        Timing: new ResolvedTiming(WorkingTimeKind.Fixed, 600, null, null),
        Group: null,
        Normalise: null,
        ValidWhen: null,
        FlightValidWhen: null,
        RawScore: null,
        Reflight: null,
        Score:
        [
            new RateTerm { MetricRef = "flightTime", Rate = 1 },
            new LookupTerm
            {
                MetricRef = "landingDistance",
                Rows = [new LookupRow(5, 50), new LookupRow(10, 25), new LookupRow(null, 0)],
            },
        ],
        ScoreNormalised: []);

    private static ResolvedTask GatedLandingTask() => RatePlusLookupTask() with
    {
        Metrics =
        [
            new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" },
            new MetricDefinition { Name = "overflySeconds", Kind = MeasuredKind.Number, Unit = "s" },
            new MetricDefinition { Name = "landingDistance", Kind = MeasuredKind.Number, Unit = "m" },
        ],
        Score =
        [
            new RateTerm { MetricRef = "flightTime", Rate = 1 },
            new ConditionalTerm
            {
                When = new Comparison
                {
                    LeftMetricRef = "overflySeconds",
                    Op = Comparator.EqualTo,
                    RightValue = MeasuredValue.Of(0m),
                },
                Then = new LookupTerm
                {
                    MetricRef = "landingDistance",
                    Rows = [new LookupRow(5, 50), new LookupRow(null, 0)],
                },
            },
        ],
    };

    [Fact]
    public void P_TermPointsSumToFlightScore_RatePlusLookup()
    {
        (from time in FlightTime
         from landing in LandingDistance
         select (time, landing))
        .Sample(t =>
        {
            var interpreted = FlightInterpreter.Interpret(
                RatePlusLookupTask(), 1,
                new Dictionary<string, MeasuredValue>
                {
                    ["flightTime"] = MeasuredValue.Of(t.time),
                    ["landingDistance"] = MeasuredValue.Of(t.landing),
                });

            // One contribution per term index, verbatim decimals.
            if (interpreted.TermContributions.Count != 2)
                return false;
            var sum = interpreted.TermContributions.Values.Sum(c => c.Points);
            return sum == interpreted.Score
                && interpreted.TermContributions[0].MetricConsumed == t.time
                && interpreted.TermContributions[1].MetricConsumed == t.landing;
        });
    }

    [Fact]
    public void P_TermPointsSumToFlightScore_GatedLanding()
    {
        (from time in FlightTime
         from overfly in Gen.Int[0, 70].Select(i => (decimal)i)
         from landing in LandingDistance
         select (time, overfly, landing))
        .Sample(t =>
        {
            var interpreted = FlightInterpreter.Interpret(
                GatedLandingTask(), 1,
                new Dictionary<string, MeasuredValue>
                {
                    ["flightTime"] = MeasuredValue.Of(t.time),
                    ["overflySeconds"] = MeasuredValue.Of(t.overfly),
                    ["landingDistance"] = MeasuredValue.Of(t.landing),
                });

            var sum = interpreted.TermContributions.Values.Sum(c => c.Points);
            if (sum != interpreted.Score)
                return false;

            // The gated-out branch contributes its else-branch value (0, no
            // Else) while keeping the branch metric's identity.
            var landing = interpreted.TermContributions[1];
            return t.overfly == 0m
                ? landing.Points == (t.landing <= 5m ? 50m : 0m)
                : landing.Points == 0m && landing.MetricConsumed == 0m;
        });
    }

    [Fact]
    public void P_TermPointsSumToFlightScore_SurvivesSelection()
    {
        // Selection (LastFlight over several complete flights) preserves each
        // flight's own contributions — the projection reads them, never
        // re-derives them.
        (from times in Gen.Int[0, 600].Array[1, 4].Select(a => a.Select(i => (decimal)i).ToArray())
         select times)
        .Sample(times =>
        {
            var task = RatePlusLookupTask();
            var interpreted = times
                .Select((time, i) => FlightInterpreter.Interpret(
                    task, i + 1,
                    new Dictionary<string, MeasuredValue>
                    {
                        ["flightTime"] = MeasuredValue.Of(time),
                        ["landingDistance"] = MeasuredValue.Of(3m),
                    }))
                .ToImmutableArray();

            var result = FlightSelector.SelectAndScore(
                entry: null, task: task,
                parameterBindings: new Dictionary<string, MeasuredValue>(),
                interpretedFlights: interpreted);

            if (result.State != TaskResultState.Valid || result.Selection is null)
                return false;
            var selected = result.Selection.Flights.Should().ContainSingle().Subject;
            return selected.TermContributions.Values.Sum(c => c.Points) == selected.Score
                && result.RawScore == selected.Score;
        });
    }

    // ------------------------------------------------- ScoreTermRefs sharing

    [Fact]
    public void ScoreTermRefs_unwraps_conditional_branches_and_nulls_constants()
    {
        // WI-2's factor-not-duplicate guard: the wire projection and the
        // engine's target walk share this unwrap, pinned here.
        ScoreTermRefs.GetTermMetricRef(new RateTerm { MetricRef = "flightTime", Rate = 1 })
            .Should().Be("flightTime");
        ScoreTermRefs.GetTermMetricRef(new LookupTerm
            { MetricRef = "landingDistance", Rows = [new LookupRow(null, 0)] })
            .Should().Be("landingDistance");
        ScoreTermRefs.GetTermMetricRef(new PiecewiseTerm
            { MetricRef = "flightTime", Bands = [new Band(null, null, 1)] })
            .Should().Be("flightTime");
        ScoreTermRefs.GetTermMetricRef(new ConstantTerm { Value = -30 })
            .Should().BeNull();
        ScoreTermRefs.GetTermMetricRef(new ConditionalTerm
            {
                When = new Comparison
                {
                    LeftMetricRef = "overflySeconds",
                    Op = Comparator.EqualTo,
                    RightValue = MeasuredValue.Of(0m),
                },
                Then = new LookupTerm
                    { MetricRef = "landingDistance", Rows = [new LookupRow(null, 0)] },
            })
            .Should().Be("landingDistance");
        // Then contributes nothing (a bare constant): fall back to Else.
        ScoreTermRefs.GetTermMetricRef(new ConditionalTerm
            {
                When = new Comparison
                {
                    LeftMetricRef = "overflySeconds",
                    Op = Comparator.EqualTo,
                    RightValue = MeasuredValue.Of(0m),
                },
                Then = new ConstantTerm { Value = -30 },
                Else = new RateTerm { MetricRef = "flightTime", Rate = 1 },
            })
            .Should().Be("flightTime");
        // Neither branch names a metric: null, the ConstantTerm convention.
        ScoreTermRefs.GetTermMetricRef(new ConditionalTerm
            {
                When = new Comparison
                {
                    LeftMetricRef = "overflySeconds",
                    Op = Comparator.EqualTo,
                    RightValue = MeasuredValue.Of(0m),
                },
                Then = new ConstantTerm { Value = -30 },
            })
            .Should().BeNull();
    }
}
