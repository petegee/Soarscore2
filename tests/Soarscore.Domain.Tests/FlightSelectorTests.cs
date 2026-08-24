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
/// Black-box sociable tests for FlightSelector (WI-4).
/// Tests flight selection, target assignment, validWhen, PerTask caps, and rounding.
/// P4 (kanban/in-progress/out-of-order-flight-entry.md WI-5) adds the
/// capture-order independence property over the real ScoreGroup pipeline.
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

    // ------------------------------------------------------ P4 (out-of-order capture)

    // One real corpus task per positional selection kind (finding 2), each
    // with the raw score the three flights [100.5, 300.75, 250.25] must yield:
    // Task A selects only launch 3 (cap 300); Task B the last two (each
    // capped at maxFlight.B = 240); Task M's ExactlyNInOrder clamps launches
    // 1..3 to targets [180, 300, 420] and scores all three. ScoreGroup's
    // returned RawScore is the NORMALISED group score, so the expected raw
    // value is pinned through the preserved selection instead.
    private static readonly IReadOnlyList<(TaskDefinition Task, ClassDefinition ClassDef, IReadOnlyDictionary<string, MeasuredValue> Bindings, decimal ExpectedRawScore)>
        PositionalTaskCases =
        [
            (F3KTask("A"), SeedF3K.Definition,
                new Dictionary<string, MeasuredValue> { ["workingTime.A"] = MeasuredValue.Of(600m) },
                250.25m),
            (F3KTask("B"), SeedF3K.Definition,
                new Dictionary<string, MeasuredValue>
                {
                    ["workingTime.B"] = MeasuredValue.Of(600m),
                    ["maxFlight.B"] = MeasuredValue.Of(240m),
                },
                480m),
            (F3KTask("M"), SeedF3K.Definition,
                new Dictionary<string, MeasuredValue>(),
                650.75m),
        ];

    /// <summary>
    /// P4 — Selection is capture-order independent
    /// (kanban/in-progress/out-of-order-flight-entry.md WI-5): folding the same
    /// FlightOpened + MeasurementCaptured events in a shuffled arrival order and
    /// scoring through the real ScoreGroup pipeline yields the same selected
    /// flights and raw score as folding them in launch order, for every
    /// positional selection kind the corpus uses. Pins finding 2 — the
    /// regression decision 3's sorted fold exists to kill — at the selector
    /// level, complementing P1's fold-level property.
    /// </summary>
    [Fact]
    public void Selection_is_capture_order_independent_for_every_positional_kind()
    {
        (from caseIndex in Gen.Int[0, PositionalTaskCases.Count - 1]
         from order in Gen.Shuffle(new[] { 1, 2, 3 })
         select (caseIndex, order))
        .Sample(t =>
        {
            var (task, classDef, bindings, expectedRawScore) = PositionalTaskCases[t.caseIndex];

            var sorted = ScoreThroughPipeline(task, classDef, bindings, [1, 2, 3]);
            var shuffled = ScoreThroughPipeline(task, classDef, bindings, t.order);

            sorted.State.Should().Be(shuffled.State);
            shuffled.RawScore.Should().Be(sorted.RawScore);
            sorted.Selection.Should().NotBeNull();
            shuffled.Selection.Should().NotBeNull();
            SelectedSequences(shuffled).Should().Equal(SelectedSequences(sorted));
            shuffled.Selection!.Flights.Select(f => f.Score)
                .Should().Equal(sorted.Selection!.Flights.Select(f => f.Score));

            // The concrete oracle: the selected flights' per-flight scores sum
            // to the expected raw value, whichever way the card was typed.
            sorted.Selection!.Flights.Sum(f => f.Score).Should().Be(expectedRawScore);
        });
    }

    private static IEnumerable<int> SelectedSequences(TaskResult result) =>
        result.Selection!.Flights.Select(f => (int)f.Metrics["flight.sequence"].Number!.Value);

    private static TaskDefinition F3KTask(string code) =>
        SeedF3K.Definition.Phases.SelectMany(p => p.Tasks).First(t => t.Code == code);

    // Distinct per-launch times whose best flight is NOT the last one, so a
    // positional misread of an unsorted flight list cannot pass by accident.
    private static readonly decimal[] FlightTimes = [100.5m, 300.75m, 250.25m];

    private static TaskResult ScoreThroughPipeline(
        TaskDefinition task,
        ClassDefinition classDef,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        int[] openOrder)
    {
        var groupRef = GroupId.New();
        var competitorRef = CompetitorId.New();
        var at = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);
        var entryKey = competitorRef.ToString();

        // Each launch folds as a block — open then its measurements — with
        // blocks in the caller's arrival order; the captures of one launch may
        // therefore precede another launch's open.
        var entry = Entry.Create(new EntryOpened(
            EntryId.New(), CompetitionId.New(), 1, 1, 1,
            groupRef, competitorRef, ReflightRole.Original, at));

        foreach (var sequence in openOrder)
        {
            entry = entry.Apply(new FlightOpened(sequence, at.AddSeconds(sequence)));
            entry = entry.Apply(new MeasurementCaptured(sequence,
                new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(FlightTimes[sequence - 1]), CapturedAt = at }));
            entry = entry.Apply(new MeasurementCaptured(sequence,
                new Measurement { Metric = "landedWithinWindow", Value = MeasuredValue.Of(true), CapturedAt = at }));
            entry = entry.Apply(new MeasurementCaptured(sequence,
                new Measurement { Metric = "launchedInWorkingTime", Value = MeasuredValue.Of(true), CapturedAt = at }));
        }

        var entries = ImmutableDictionary<string, Entry>.Empty.Add(entryKey, entry);
        var group = ScoringService.ScoreGroup(groupRef.ToString(), task, classDef, entries, bindings);
        return group.Results[entryKey];
    }

    // ------------------------------------------------------ helpers

    private static ResolvedTask ResolveF3KTaskA()
    {
        var f3k = SeedF3K.Definition;
        var taskA = f3k.Phases[0].Tasks.Single(t => t.Code == "A");
        return ParameterResolver.ResolveTask(taskA, new Dictionary<string, MeasuredValue>
        {
            ["workingTime.A"] = MeasuredValue.Of(600m),
        }, []);
    }

    private static ResolvedTask ResolveF3KTaskE()
    {
        var f3k = SeedF3K.Definition;
        var taskE = f3k.Phases[0].Tasks.Single(t => t.Code == "E");
        return ParameterResolver.ResolveTask(taskE, new Dictionary<string, MeasuredValue>
        {
            ["workingTime.E"] = MeasuredValue.Of(600m),
        }, []);
    }

    private static ResolvedTask ResolveF3BTaskC()
    {
        var f3b = SeedF3B.Definition;
        var taskC = f3b.Phases[0].Tasks.Single(t => t.Code == "C");
        return ParameterResolver.ResolveTask(taskC, new Dictionary<string, MeasuredValue>(), []);
    }

    private static ResolvedTask ResolveF5KTaskA()
    {
        var f5k = SeedF5K.Definition;
        var taskA = f5k.Phases[0].Tasks.Single(t => t.Code == "A");
        return ParameterResolver.ResolveTask(taskA, new Dictionary<string, MeasuredValue>
        {
            ["nlh"] = MeasuredValue.Of(60m),
            ["minPerGroup"] = MeasuredValue.Of(5m),
        }, []);
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
        return FlightInterpreter.Interpret(task, seq, metrics);
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
        return FlightInterpreter.Interpret(task, seq, metrics);
    }
}
