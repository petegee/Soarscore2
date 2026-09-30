// kanban/backlog/turn-around-window-score-validation.md WI-4.
//
// CsCheck property for the named invariant: for any Fixed-working-time task
// with a flightTime-consumed term, ceiling either unbounded or W >= ceiling,
// and any selection of n > 1 valid flights: warnings ⟺ (sum > W or sum == W
// → window) else (sum > W-n → turn-around); scores equal the no-story scores
// exactly; UntilAllFlightsComplete / per-launch-ceiling / single-flight /
// NoResult / pending / Poker-shaped inputs never warn. Generated over corpus
// task shapes (incl. F5K-A/D target shapes and the F5K-C ceiling shape) ×
// small flight-counts/sums straddling both thresholds.
//
// Binds WI-1's WindowPlausibility.Check and ScoreGroup step 2e directly; the
// expected codes below are recomputed in this file from independently read
// (sum, W, n) plus structural applicability — never by calling the engine's
// own ceiling helper — so the property is a genuine conformance check.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

public class TurnaroundWindowPlausibilityPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------- shape catalogue

    private sealed record Shape(
        string Name,
        Func<ResolvedTask> Resolve,
        int FlightsToOpen,
        bool ExpectApplicable,
        bool WholeSeconds = false);

    private static ResolvedTask ResolveCorpus(
        string fileName, string taskCode,
        IReadOnlyDictionary<string, MeasuredValue>? bindings = null)
    {
        var cls = Corpus.All.Single(c => c.FileName == fileName);
        var task = cls.Definition.Phases.SelectMany(p => p.Tasks).First(t => t.Code == taskCode);
        return ParameterResolver.ResolveTask(
            task, bindings ?? new Dictionary<string, MeasuredValue>(), cls.Definition.Parameters);
    }

    private static (TaskDefinition Task, ClassDefinition ClassDef, IReadOnlyDictionary<string, MeasuredValue> Bindings)
        UnresolvedCorpus(string fileName, string taskCode, IReadOnlyDictionary<string, MeasuredValue>? bindings = null)
    {
        var cls = Corpus.All.Single(c => c.FileName == fileName);
        var task = cls.Definition.Phases.SelectMany(p => p.Tasks).First(t => t.Code == taskCode);
        return (task, cls.Definition, bindings ?? new Dictionary<string, MeasuredValue>());
    }

    private static readonly ImmutableArray<Shape> Shapes =
    [
        // The organiser's Task D: AllFlights, W = 600, cap 300/flight.
        new("F3K-D", () => ResolveCorpus("10-f3k", "D"), 2, true),
        // Same task with the per-flight cap removed: proves the raw-sum
        // visibility half (a 350 s recorded flight still sums 350).
        new("F3K-D-uncapped", () => ResolveCorpus("10-f3k", "D") with
        {
            Score = [new RateTerm { MetricRef = "flightTime", Rate = 1 }],
        }, 2, true),
        // F5K-A target shape: BestN 4, targets 60/120/180/240 sum 600, W = 600.
        new("F5K-A", () => ResolveCorpus("40-f5k", "A",
            new Dictionary<string, MeasuredValue>
            {
                ["nlh"] = MeasuredValue.Of(60m),
                ["minPerGroup"] = MeasuredValue.Of(5m),
            }), 4, true, WholeSeconds: true),
        // F5K-D target shape: BestN 3, targets 180/180/240 sum 600, W = 600 —
        // the seeded PerTask 599 clamp coexists with the window warning.
        new("F5K-D", () => ResolveCorpus("40-f5k", "D",
            new Dictionary<string, MeasuredValue>
            {
                ["nlh"] = MeasuredValue.Of(60m),
                ["minPerGroup"] = MeasuredValue.Of(5m),
            }), 3, true, WholeSeconds: true),
        // F3K-B: param-bound working time (resolved literal or param alike).
        new("F3K-B-param-W", () => ResolveCorpus("10-f3k", "B",
            new Dictionary<string, MeasuredValue>
            {
                ["workingTime.B"] = MeasuredValue.Of(600m),
                ["maxFlight.B"] = MeasuredValue.Of(240m),
            }), 2, true),
        // F3K-F: BestN 3, per-flight cap 180 × 3 = 540 ≤ 600 → applies.
        new("F3K-F", () => ResolveCorpus("10-f3k", "F"), 3, true),
        // Multi-term task: only flightTime indices sum (constant + other
        // metric terms contribute points, never window sum).
        new("multi-term", () => ResolveCorpus("10-f3k", "D") with
        {
            Score =
            [
                new RateTerm { MetricRef = "flightTime", Rate = 1, Cap = 300m },
                new ConstantTerm { Value = -30m },
                new RateTerm { MetricRef = "launchAltitude", Rate = 2 },
            ],
            Metrics = [.. ResolveCorpus("10-f3k", "D").Metrics,
                new MetricDefinition { Name = "launchAltitude", Kind = MeasuredKind.Number }],
        }, 2, true),
        // Unbounded ceiling: no targets, no per-flight cap → applies, the
        // thresholds themselves are the guard.
        new("uncapped-unbounded", () => ResolveCorpus("10-f3k", "D") with
        {
            Flights = new AllFlights(),
            Timing = new ResolvedTiming(WorkingTimeKind.Fixed, 600m, null, null),
            Score = [new RateTerm { MetricRef = "flightTime", Rate = 1 }],
        }, 2, true),
        // F5K-C per-launch-window shape: 3 × 240 = 720 > 241 → skip.
        new("F5K-C-ceiling-skip", () => ResolveCorpus("40-f5k", "C",
            new Dictionary<string, MeasuredValue>
            {
                ["nlh"] = MeasuredValue.Of(60m),
                ["minPerGroup"] = MeasuredValue.Of(5m),
            }), 3, false, WholeSeconds: true),
        // F3K-C: UntilAllFlightsComplete → skip (no class datum for W).
        new("F3K-C-until-all", () => ResolveCorpus("10-f3k", "C",
            new Dictionary<string, MeasuredValue>
            {
                ["launches.C"] = MeasuredValue.Of(3m),
            }), 3, false),
        // F3K-E Poker: scored sum is nominated targetTime, not flown time.
        new("F3K-E-poker", () => ResolveCorpus("10-f3k", "E",
            new Dictionary<string, MeasuredValue>
            {
                ["workingTime.E"] = MeasuredValue.Of(600m),
            }), 3, false),
        // F3K-N: BestN 1 — a single scored flight needs no turn-around.
        new("F3K-N-single", () => ResolveCorpus("10-f3k", "N"), 3, false),
    ];

    // ------------------------------------------------------- flight builders

    private static ImmutableArray<InterpretedFlight> InterpretCard(ResolvedTask task, decimal[] flightTimes)
    {
        var builder = ImmutableArray.CreateBuilder<InterpretedFlight>(flightTimes.Length);
        for (var i = 0; i < flightTimes.Length; i++)
        {
            var metrics = new Dictionary<string, MeasuredValue>();
            foreach (var metric in task.Metrics)
            {
                metrics[metric.Name] = metric.Name switch
                {
                    "flightTime" => MeasuredValue.Of(flightTimes[i]),
                    // F3K-E Poker nominates before launch; an achieved target
                    // keeps the card scorable — Poker stays excluded
                    // structurally (no flightTime-consumed term).
                    "targetTime" => MeasuredValue.Of(flightTimes[i]),
                    // Neutral launch: exactly NLH (bound to 60 above), so the
                    // 5.5.10.4 bands integrate to zero from their origin.
                    "launchAltitude" => MeasuredValue.Of(60m),
                    _ when metric.Kind == MeasuredKind.Number => MeasuredValue.Of(0m),
                    // Flags are recorded exceptions: absence resolves to the
                    // class-declared assumption (compliance), so state it.
                    _ => metric.WhenNotRecorded ?? MeasuredValue.Of(true),
                };
            }
            builder.Add(FlightInterpreter.Interpret(task, i + 1, metrics));
        }
        return builder.ToImmutable();
    }

    private static Entry FoldEntry(ResolvedTask task, CompetitorId competitorRef, decimal[] flightTimes)
    {
        var groupRef = GroupId.New();
        var entry = Entry.Create(new EntryOpened(
            EntryId.New(), CompetitionId.New(), 0, 1, 1,
            groupRef, competitorRef, ReflightRole.Original, Now));
        for (var i = 0; i < flightTimes.Length; i++)
        {
            var seq = i + 1;
            entry = entry.Apply(new FlightOpened(seq, Now.AddSeconds(seq)));
            foreach (var metric in task.Metrics)
            {
                var value = metric.Name switch
                {
                    "flightTime" => MeasuredValue.Of(flightTimes[i]),
                    "targetTime" => MeasuredValue.Of(flightTimes[i]),
                    "launchAltitude" => MeasuredValue.Of(60m),
                    _ when metric.Kind == MeasuredKind.Number => MeasuredValue.Of(0m),
                    _ => MeasuredValue.Of(false),
                };
                // Flags are recorded exceptions; compliance is the absence
                // path — capture only what the flight actually carries. F3K
                // flag metrics default-true on absence, F5K's overfly flag
                // defaults-false; capturing `false` for a default-true flag
                // would record an infraction, so skip flags entirely.
                if (metric.Kind == MeasuredKind.Flag)
                    continue;
                var captured = entry.CaptureMeasurement(seq, metric.Name, value, Now, task.Metrics);
                captured.IsSuccess.Should().BeTrue();
                entry = entry.Apply(captured.Value);
            }
        }
        return entry;
    }

    private static decimal[] SplitSum(decimal sum, int n)
    {
        // Deterministic split, no clamping: RateTerm caps preserve raw
        // MetricConsumed. Whole sums distribute integrally (F5K whole-second
        // shapes must never see a fractional flight time); fractional sums
        // put the remainder on the first flight.
        var parts = new decimal[n];
        var basePart = Math.Floor(sum / n);
        for (var i = 0; i < n; i++)
            parts[i] = basePart;
        var remainder = sum - basePart * n;
        if (remainder == Math.Truncate(remainder))
        {
            for (var i = 0; i < remainder; i++)
                parts[i % n] += 1m;
        }
        else
        {
            parts[0] += remainder;
        }
        return parts;
    }

    /// <summary>
    /// The expected code from independently read (sum, W, n) plus the shape's
    /// structural applicability — the engine's own ceiling helper is never
    /// consulted, so this is a genuine conformance check on Check.
    /// </summary>
    private static string? ExpectedCode(Shape shape, ResolvedTask task, TaskResult result, decimal observed)
    {
        if (!shape.ExpectApplicable)
            return null;
        if (result.State != TaskResultState.Valid || result.Selection is null)
            return null;
        var n = result.Selection.Flights.Length;
        if (n <= 1)
            return null;
        if (task.Timing.Kind != WorkingTimeKind.Fixed || task.Timing.WorkingTime is null)
            return null;
        var w = task.Timing.WorkingTime.Value;
        if (!task.Score.Any(t => ScoreTermRefs.GetTermMetricRef(t) == "flightTime"))
            return null;
        if (observed > w || observed == w)
            return WindowPlausibility.WindowSumExceeded;
        if (observed > w - n)
            return WindowPlausibility.TurnaroundCapExceeded;
        return null;
    }

    private static decimal ObservedSum(ResolvedTask task, TaskResult result)
    {
        var indices = task.Score
            .Select((term, i) => (term, i))
            .Where(x => ScoreTermRefs.GetTermMetricRef(x.term) == "flightTime")
            .Select(x => x.i)
            .ToImmutableArray();
        var sum = 0m;
        foreach (var flight in result.Selection!.Flights)
            foreach (var i in indices)
                if (flight.TermContributions.TryGetValue(i, out var c))
                    sum += c.MetricConsumed;
        return sum;
    }

    // ------------------------------------------------------- the property

    [Fact]
    public void Warning_bands_scores_and_exclusions_match_the_story_over_corpus_shapes()
    {
        (from shapeIndex in Gen.Int[0, Shapes.Length - 1]
         from k in Gen.Int[0, 5]
         select (shapeIndex, k))
        .Sample(t =>
        {
            var shape = Shapes[t.shapeIndex];
            var task = shape.Resolve();

            var w = task.Timing.WorkingTime ?? 600m;
            var n = shape.FlightsToOpen;

            // Straddle both thresholds. Fractional shapes (F3K 0.1 s
            // precision) sweep half-second deltas around each anchor; whole-
            // second shapes (F5K) sweep {-1, 0} at each anchor — all sums
            // stay whole and, for target shapes, within the targets.
            decimal anchor, delta;
            if (shape.WholeSeconds)
            {
                anchor = t.k % 2 == 0 ? w - n : w;
                delta = (t.k / 2) % 2 == 0 ? -1m : 0m;
            }
            else
            {
                delta = new[] { -1.5m, -1m, -0.5m, 0m, 0.5m, 1m }[t.k];
                anchor = delta < 0 ? w - n : w;
            }
            var sum = anchor + delta;
            if (sum < 0)
                sum = 0;

            var result = FlightSelector.SelectAndScore(
                null, task, new Dictionary<string, MeasuredValue>(),
                InterpretCard(task, SplitSum(sum, n)));

            result.State.Should().Be(TaskResultState.Valid, $"{shape.Name} sum {sum}");
            var observed = ObservedSum(task, result);

            var expected = ExpectedCode(shape, task, result, observed);
            var actual = WindowPlausibility.Check(task, result, "pilot", "group", task.Code);
            actual?.Code.Should().Be(expected, $"{shape.Name} sum {sum} observed {observed}");

            // Scores equal the no-story scores: RawScore is exactly the
            // selected flights' points less the literal PerTask-cap
            // reduction — recomputed here from the Selection, never from the
            // warning band. A warning-shaped sum scores its arithmetic raw.
            var expectedRaw = result.Selection!.Flights.Sum(f => f.Score);
            foreach (var term in task.Score)
            {
                if (term is RateTerm rt
                    && rt.CapScope == CapScope.PerTask
                    && rt.Cap is NumberOrParam.Literal capLit)
                {
                    var termIndex = task.Score.IndexOf(term);
                    var consumed = result.Selection.Flights.Sum(f =>
                        f.TermContributions.TryGetValue(termIndex, out var c) ? c.MetricConsumed : 0m);
                    if (consumed > capLit.Value)
                        expectedRaw -= (consumed - capLit.Value) * rt.Rate;
                }
            }
            // F5K shapes truncate raw to whole points (5.5.10.15 F4b); the
            // generator keeps their sums whole so truncation is identity —
            // assert the premise rather than re-implementing rounding.
            if (task.RawScore is not null)
                expectedRaw.Should().Be(Math.Truncate(expectedRaw), $"{shape.Name} premise: whole-point raws");
            result.RawScore.Should().Be(expectedRaw, $"{shape.Name} sum {sum}");
        });
    }

    // ------------------------------------------------------- ScoreGroup step 2e

    [Fact]
    public void ScoreGroup_carries_warnings_beside_unchanged_scores()
    {
        var (taskDef, classDef, bindings) = UnresolvedCorpus("10-f3k", "D");
        var resolved = ResolveCorpus("10-f3k", "D");
        var groupRef = GroupId.New().ToString();

        var clean = CompetitorId.New();
        var capped = CompetitorId.New();
        var window = CompetitorId.New();
        var entries = ImmutableDictionary<string, Entry>.Empty
            .Add(clean.ToString(), FoldEntry(resolved, clean, [300m, 298m]))
            .Add(capped.ToString(), FoldEntry(resolved, capped, [299.5m, 299m]))
            .Add(window.ToString(), FoldEntry(resolved, window, [300m, 300m]));

        var group = ScoringService.ScoreGroup(groupRef, taskDef, classDef, entries, bindings);

        group.Warnings[clean.ToString()].Should().BeEmpty();
        group.Warnings[capped.ToString()].Should().ContainSingle()
            .Which.Code.Should().Be(WindowPlausibility.TurnaroundCapExceeded);
        group.Warnings[window.ToString()].Should().ContainSingle()
            .Which.Code.Should().Be(WindowPlausibility.WindowSumExceeded);

        // Warn-through: the pre-normalisation raws are the arithmetic sums.
        group.PreNormalisationScores[clean.ToString()].Should().Be(598m);
        group.PreNormalisationScores[capped.ToString()].Should().Be(598.5m);
        group.PreNormalisationScores[window.ToString()].Should().Be(600m);
    }

    // ------------------------------------------------------- pinned boundaries

    [Fact]
    public void TaskD_600_600_window_598_clean_598_point_5_turnaround()
    {
        var task = Shapes[0].Resolve(); // F3K-D

        TaskResult Score(decimal a, decimal b) => FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(),
            InterpretCard(task, [a, b]));

        WindowPlausibility.Check(task, Score(300m, 300m), "p", "g", "D")
            ?.Code.Should().Be(WindowPlausibility.WindowSumExceeded);
        WindowPlausibility.Check(task, Score(300m, 298m), "p", "g", "D")
            .Should().BeNull();
        WindowPlausibility.Check(task, Score(299.5m, 299m), "p", "g", "D")
            ?.Code.Should().Be(WindowPlausibility.TurnaroundCapExceeded);

        // Single-flight selections never fire, however maximal.
        var singleTask = Shapes.First(s => s.Name == "F3K-N-single").Resolve();
        var single = FlightSelector.SelectAndScore(
            null, singleTask, new Dictionary<string, MeasuredValue>(),
            InterpretCard(singleTask, [599m, 599m, 599m]));
        single.Selection!.Flights.Should().ContainSingle();
        WindowPlausibility.Check(singleTask, single, "p", "g", "N").Should().BeNull();
    }

    [Fact]
    public void NoResult_pending_and_Poker_inputs_never_warn()
    {
        var task = Shapes[0].Resolve(); // F3K-D

        var noResult = new TaskResult(TaskResultState.NoResult, null, 0m);
        WindowPlausibility.Check(task, noResult, "p", "g", "D").Should().BeNull();

        // Pending flights never enter Selection: a pending-only card is
        // NoResult-shaped and carries no warning.
        var pendingFlight = new InterpretedFlight(
            new FlightResult(
                FlightResultState.Pending,
                new ResolvedMeasurements(new Dictionary<string, MeasuredValue>()),
                new PendingFlightDiagnostic(1, "flightTime")),
            0m,
            ImmutableDictionary<int, TermContribution>.Empty);
        var pendingOnly = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(), [pendingFlight]);
        pendingOnly.State.Should().Be(TaskResultState.NoResult);
        WindowPlausibility.Check(task, pendingOnly, "p", "g", "D").Should().BeNull();

        // Poker: flown time without a flightTime-consumed term.
        var poker = Shapes.First(s => s.Name == "F3K-E-poker").Resolve();
        var pokerResult = FlightSelector.SelectAndScore(
            null, poker, new Dictionary<string, MeasuredValue>(),
            InterpretCard(poker, [300m, 300m, 300m]));
        WindowPlausibility.Check(poker, pokerResult, "p", "g", "E").Should().BeNull();
    }

    [Fact]
    public void F5K_D_clamp_and_warning_coexist_with_points_unchanged()
    {
        var task = Shapes.First(s => s.Name == "F5K-D").Resolve();
        var result = FlightSelector.SelectAndScore(
            null, task, new Dictionary<string, MeasuredValue>(),
            InterpretCard(task, [180m, 180m, 240m]));

        result.State.Should().Be(TaskResultState.Valid);
        // The rulebook's own cap 599 perTask shapes the points …
        result.RawScore.Should().Be(599m);
        // … while the window warning fires on the raw 600 sum.
        WindowPlausibility.Check(task, result, "p", "g", "D")
            ?.Code.Should().Be(WindowPlausibility.WindowSumExceeded);
    }
}
