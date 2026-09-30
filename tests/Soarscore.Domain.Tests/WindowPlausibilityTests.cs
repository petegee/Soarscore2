using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// WI-1 pins for kanban/backlog/turn-around-window-score-validation.md:
/// warn-through plausibility flags computed per entry per group at ScoreGroup
/// step 2e. Scores are never touched — every assertion below pins both the
/// warning AND the unchanged score.
/// Driven through <see cref="ScoringService.ScoreGroup"/> (the grain the
/// check lives at) with synthetic classes (class-agnostic, NFR-1); no
/// normalisation, so RawScore IS the points sum and PreNormalisationScores
/// mirrors it.
/// </summary>
public class WindowPlausibilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly ImmutableArray<MetricDefinition> FlightTimeDefs =
        [new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number }];

    private static readonly IReadOnlyDictionary<string, MeasuredValue> EmptyBindings =
        new Dictionary<string, MeasuredValue>();

    private static TaskDefinition MakeTask(
        FlightSelection flights,
        TaskTiming timing,
        ImmutableArray<ScoreTerm> score,
        ImmutableArray<MetricDefinition>? metrics = null) => new()
        {
            Code = "D",
            Name = "Test task",
            Metrics = metrics ?? FlightTimeDefs,
            Flights = flights,
            Timing = timing,
            Score = score,
        };

    private static TaskDefinition TaskDShape() => MakeTask(
        new LastNFlights(2),
        new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        [new RateTerm { MetricRef = "flightTime", Rate = 1, Cap = 300, CapScope = CapScope.PerFlight }]);

    private static ClassDefinition MakeClassDefinition(TaskDefinition task) => new()
    {
        Name = "Synthetic",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = ReflightSelection.Replacement,
            OthersScore = ReflightSelection.BetterOf,
        },
        Phases =
        [
            new PhaseDefinition
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new ValidityRule { MinRounds = 1 },
                Tasks = [task],
            },
        ],
    };

    private static Entry EntryWithFlights(
        GroupId groupRef, CompetitorId competitorRef, string metric, decimal[] values)
    {
        var entry = Entry.Create(new EntryOpened(
            EntryId.New(), CompetitionId.New(), 0, 1, 1,
            groupRef, competitorRef, ReflightRole.Original, Now));
        for (int i = 0; i < values.Length; i++)
        {
            entry = entry.Apply(new FlightOpened(i + 1, Now));
            var captured = entry.CaptureMeasurement(i + 1, metric, MeasuredValue.Of(values[i]), Now, FlightTimeDefs);
            captured.IsSuccess.Should().BeTrue();
            entry = entry.Apply(captured.Value);
        }
        return entry;
    }

    private static (GroupResult Group, string Ref) ScoreOne(
        TaskDefinition task, decimal[] flightTimes,
        IReadOnlyDictionary<string, MeasuredValue>? bindings = null, string metric = "flightTime")
    {
        var classDef = MakeClassDefinition(task);
        var groupRef = GroupId.New();
        var competitor = CompetitorId.New();
        var entries = ImmutableDictionary<string, Entry>.Empty.Add(
            competitor.ToString(), EntryWithFlights(groupRef, competitor, metric, flightTimes));
        var group = ScoringService.ScoreGroup("group", task, classDef, entries, bindings ?? EmptyBindings);
        return (group, competitor.ToString());
    }

    // ------------------------------------------------- the organiser's worked examples

    [Fact]
    public void Task_D_300_plus_300_warns_window_with_scores_unchanged()
    {
        var (group, r) = ScoreOne(TaskDShape(), [300m, 300m]);

        group.Results[r].State.Should().Be(TaskResultState.Valid);
        group.Results[r].RawScore.Should().Be(600m);
        group.PreNormalisationScores[r].Should().Be(600m);

        group.Warnings[r].Should().ContainSingle();
        group.Warnings[r][0].Code.Should().Be("score.windowSumExceeded");
        group.Warnings[r][0].Message.Should().Be(
            $"Entry {r} in group group: flight-time sum 600s reaches the 600s working time (task D)");
    }

    [Fact]
    public void Task_D_300_plus_298_is_clean()
    {
        var (group, r) = ScoreOne(TaskDShape(), [300m, 298m]);

        group.Results[r].RawScore.Should().Be(598m);
        group.Warnings[r].Should().BeEmpty();
    }

    [Fact]
    public void Task_D_299_point_5_plus_299_warns_turnaround_only()
    {
        var (group, r) = ScoreOne(TaskDShape(), [299.5m, 299m]);

        group.Results[r].RawScore.Should().Be(598.5m);

        group.Warnings[r].Should().ContainSingle();
        group.Warnings[r][0].Code.Should().Be("score.turnaroundCapExceeded");
        group.Warnings[r][0].Message.Should().Be(
            $"Entry {r} in group group: flight-time sum 598.5s exceeds the turn-around cap 598s (working time 600s minus 2 flights, task D)");
    }

    [Fact]
    public void Exactly_at_the_turnaround_cap_is_clean()
    {
        // 299 + 299 = 598 == W − n: strict-greater, so no warning.
        var (group, r) = ScoreOne(TaskDShape(), [299m, 299m]);

        group.Results[r].RawScore.Should().Be(598m);
        group.Warnings[r].Should().BeEmpty();
    }

    // ------------------------------------------------- the n > 1 gate

    [Fact]
    public void Single_flight_selections_never_warn()
    {
        // Task N shape: 599 in a 600 s window is legal flying.
        var task = MakeTask(
            new LastFlight(),
            new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
            [new RateTerm { MetricRef = "flightTime", Rate = 1, Cap = 599, CapScope = CapScope.PerFlight }]);

        var (group, r) = ScoreOne(task, [100m, 200m, 599m]);

        group.Results[r].State.Should().Be(TaskResultState.Valid);
        group.Results[r].RawScore.Should().Be(599m);
        group.Warnings[r].Should().BeEmpty();
    }

    // ------------------------------------------------- per-launch ceiling test

    [Fact]
    public void F5K_C_shape_skips_via_the_ceiling_test()
    {
        // Three separate 4:01 windows: W = 241, per-flight cap 240,
        // AllFlights → MaxLaunches 3 → ceiling 720 > 241 → skip.
        var task = MakeTask(
            new AllFlights(),
            new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 241, MaxLaunches = 3 },
            [new RateTerm { MetricRef = "flightTime", Rate = 1, Cap = 240, CapScope = CapScope.PerFlight }]);

        var (group, r) = ScoreOne(task, [240m, 240m, 240m]);

        group.Results[r].RawScore.Should().Be(720m);
        group.Warnings[r].Should().BeEmpty();
    }

    [Fact]
    public void Unbounded_ceiling_defaults_to_apply()
    {
        // No per-flight cap and no targets: no ceiling to hide behind —
        // the thresholds themselves are the guard.
        var task = MakeTask(
            new LastNFlights(2),
            new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
            [new RateTerm { MetricRef = "flightTime", Rate = 1 }]);

        var (group, r) = ScoreOne(task, [300m, 300m]);

        group.Warnings[r].Should().ContainSingle();
        group.Warnings[r][0].Code.Should().Be("score.windowSumExceeded");
    }

    // ------------------------------------------------- F5K-D: clamp + warning coexist

    [Fact]
    public void F5K_D_shape_clamps_points_to_599_and_still_warns_window()
    {
        // Targets 180/180/240 sum to the 600 s window (ceiling 600 ≤ 600 →
        // apply); the seeded PerTask 599 cap shapes the points while the
        // window warning flags plausibility — the two coexist.
        var task = MakeTask(
            new BestNFlights
            {
                Count = 3,
                Targets = TargetAssignment.AnyOrder,
                TargetValues = [180m, 180m, 240m],
            },
            new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
            [new RateTerm { MetricRef = "flightTime", Rate = 1, Cap = 599, CapScope = CapScope.PerTask }]);

        var (group, r) = ScoreOne(task, [180m, 180m, 240m]);

        group.Results[r].RawScore.Should().Be(599m);
        group.Warnings[r].Should().ContainSingle();
        group.Warnings[r][0].Code.Should().Be("score.windowSumExceeded");
    }

    // ------------------------------------------------- structural exclusions

    [Fact]
    public void Poker_shape_with_no_flightTime_term_never_warns()
    {
        var defs = (ImmutableArray<MetricDefinition>)[new MetricDefinition { Name = "targetTime", Kind = MeasuredKind.Number }];
        var task = MakeTask(
            new LastNFlights(2),
            new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
            [new RateTerm { MetricRef = "targetTime", Rate = 1 }],
            defs);

        var classDef = MakeClassDefinition(task);
        var groupRef = GroupId.New();
        var competitor = CompetitorId.New();
        var entry = Entry.Create(new EntryOpened(
            EntryId.New(), CompetitionId.New(), 0, 1, 1,
            groupRef, competitor, ReflightRole.Original, Now));
        foreach (var seq in new[] { 1, 2 })
        {
            entry = entry.Apply(new FlightOpened(seq, Now));
            var captured = entry.CaptureMeasurement(seq, "targetTime", MeasuredValue.Of(300m), Now, defs);
            captured.IsSuccess.Should().BeTrue();
            entry = entry.Apply(captured.Value);
        }
        var group = ScoringService.ScoreGroup(
            "group", task, classDef,
            ImmutableDictionary<string, Entry>.Empty.Add(competitor.ToString(), entry),
            EmptyBindings);

        group.Results[competitor.ToString()].State.Should().Be(TaskResultState.Valid);
        group.Warnings[competitor.ToString()].Should().BeEmpty();
    }

    [Fact]
    public void UntilAllFlightsComplete_never_warns()
    {
        // Task C shape: the working time is not a class datum.
        var task = MakeTask(
            new LastNFlights(2),
            new TaskTiming { Kind = WorkingTimeKind.UntilAllFlightsComplete },
            [new RateTerm { MetricRef = "flightTime", Rate = 1, Cap = 300, CapScope = CapScope.PerFlight }]);

        var (group, r) = ScoreOne(task, [300m, 300m]);

        group.Results[r].RawScore.Should().Be(600m);
        group.Warnings[r].Should().BeEmpty();
    }

    [Fact]
    public void NoResult_and_pending_rows_carry_empty_warnings()
    {
        var task = TaskDShape();
        var classDef = MakeClassDefinition(task);
        var groupRef = GroupId.New();
        var scored = CompetitorId.New();
        var empty = CompetitorId.New();
        var pending = CompetitorId.New();

        var pendingEntry = Entry.Create(new EntryOpened(
            EntryId.New(), CompetitionId.New(), 0, 1, 1,
            groupRef, pending, ReflightRole.Original, Now));
        pendingEntry = pendingEntry.Apply(new FlightOpened(1, Now));

        var group = ScoringService.ScoreGroup(
            "group", task, classDef,
            ImmutableDictionary<string, Entry>.Empty
                .Add(scored.ToString(), EntryWithFlights(groupRef, scored, "flightTime", [300m, 300m]))
                .Add(empty.ToString(), Entry.Create(new EntryOpened(
                    EntryId.New(), CompetitionId.New(), 0, 1, 1,
                    groupRef, empty, ReflightRole.Original, Now)))
                .Add(pending.ToString(), pendingEntry),
            EmptyBindings);

        group.Results[empty.ToString()].State.Should().Be(TaskResultState.NoResult);
        group.Results[pending.ToString()].State.Should().Be(TaskResultState.NoResult);
        group.Warnings[empty.ToString()].Should().BeEmpty();
        group.Warnings[pending.ToString()].Should().BeEmpty();
        // Keys are exactly Results keys.
        group.Warnings.Keys.Should().BeEquivalentTo(group.Results.Keys);
        // The scored row still warns beside them.
        group.Warnings[scored.ToString()].Should().ContainSingle();
    }

    // ------------------------------------------------- W source + sum source

    [Fact]
    public void Param_bound_working_time_feeds_the_formula()
    {
        var task = MakeTask(
            new LastNFlights(2),
            new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = NumberOrParam.Param("workingTime.T") },
            [new RateTerm { MetricRef = "flightTime", Rate = 1, Cap = 300, CapScope = CapScope.PerFlight }]);

        var bindings = new Dictionary<string, MeasuredValue>
        {
            ["workingTime.T"] = MeasuredValue.Of(600m),
        };

        var (group, r) = ScoreOne(task, [300m, 300m], bindings);

        group.Warnings[r].Should().ContainSingle();
        group.Warnings[r][0].Code.Should().Be("score.windowSumExceeded");
    }

    [Fact]
    public void Per_flight_capped_points_still_sum_the_raw_recorded_time()
    {
        // 350 s recorded flights clamp to 300 in points but sum 700 toward
        // the window — MetricConsumed is the uncapped raw value.
        var (group, r) = ScoreOne(TaskDShape(), [350m, 350m]);

        group.Results[r].RawScore.Should().Be(600m);
        group.Warnings[r].Should().ContainSingle();
        group.Warnings[r][0].Code.Should().Be("score.windowSumExceeded");
    }

    [Fact]
    public void Only_flightTime_indices_sum()
    {
        // A second large term must not trip the thresholds on its own.
        var task = MakeTask(
            new LastNFlights(2),
            new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
            [new RateTerm { MetricRef = "flightTime", Rate = 1, Cap = 300, CapScope = CapScope.PerFlight },
             new RateTerm { MetricRef = "landingBonus", Rate = 1 }]);

        var defs = (ImmutableArray<MetricDefinition>)
        [
            new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number },
            new MetricDefinition { Name = "landingBonus", Kind = MeasuredKind.Number },
        ];
        var classDef = MakeClassDefinition(task with { Metrics = defs });
        var groupRef = GroupId.New();
        var competitor = CompetitorId.New();
        var entry = Entry.Create(new EntryOpened(
            EntryId.New(), CompetitionId.New(), 0, 1, 1,
            groupRef, competitor, ReflightRole.Original, Now));
        foreach (var (seq, time) in new[] { (1, 300m), (2, 298m) })
        {
            entry = entry.Apply(new FlightOpened(seq, Now));
            var t = entry.CaptureMeasurement(seq, "flightTime", MeasuredValue.Of(time), Now, defs);
            t.IsSuccess.Should().BeTrue();
            entry = entry.Apply(t.Value);
            var b = entry.CaptureMeasurement(seq, "landingBonus", MeasuredValue.Of(500m), Now, defs);
            b.IsSuccess.Should().BeTrue();
            entry = entry.Apply(b.Value);
        }
        var group = ScoringService.ScoreGroup(
            "group", task with { Metrics = defs }, classDef,
            ImmutableDictionary<string, Entry>.Empty.Add(competitor.ToString(), entry),
            EmptyBindings);

        // Points include the bonus, but the window sum (598) stays clean.
        group.Results[competitor.ToString()].RawScore.Should().Be(598m + 1000m);
        group.Warnings[competitor.ToString()].Should().BeEmpty();
    }

    [Fact]
    public void Missing_contribution_index_contributes_zero_never_throws()
    {
        // A flight zeroed by the flight gate carries no per-term
        // contributions; the sum skips the missing index instead of throwing.
        var task = MakeTask(
            new LastNFlights(2),
            new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
            [new RateTerm { MetricRef = "flightTime", Rate = 1, Cap = 300, CapScope = CapScope.PerFlight }]);
        var resolved = ParameterResolver.ResolveTask(
            task, EmptyBindings, MakeClassDefinition(task).Parameters);

        var emptyFlight = new InterpretedFlight(
            new FlightResult(
                FlightResultState.Valid,
                new ResolvedMeasurements(new Dictionary<string, MeasuredValue>())),
            0m,
            new Dictionary<int, TermContribution>());
        var fullFlight = new InterpretedFlight(
            new FlightResult(
                FlightResultState.Valid,
                new ResolvedMeasurements(new Dictionary<string, MeasuredValue>())),
            300m,
            new Dictionary<int, TermContribution>
            {
                [0] = new TermContribution(300m, 300m),
            });
        var result = new TaskResult(
            TaskResultState.Valid,
            new SelectedFlights([emptyFlight, fullFlight], new Dictionary<int, decimal?>()),
            300m);

        WindowPlausibility.Check(resolved, result, "C1", "group", "D").Should().BeNull();
    }
}
