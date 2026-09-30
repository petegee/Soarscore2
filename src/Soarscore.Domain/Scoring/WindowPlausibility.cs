// WindowPlausibility — kanban/backlog/turn-around-window-score-validation.md WI-1.
//
// Warn-through plausibility checks on the field readout: given a resolved task
// with a Fixed working time W and an entry with n > 1 selected flights, the
// uncapped flight-time sum is compared against W (window rule) and W − n
// (turn-around rule). Scores are never touched — at most one ScoreWarning per
// entry, window checked first. Pure function of (resolvedTask, taskResult) plus
// the identifiers the message names; no per-class branches (NFR-1).

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// The two warn-through plausibility checks on one entry's scored selection.
/// </summary>
public static class WindowPlausibility
{
    /// <summary>Flight-time sum reaches the working time (checked first).</summary>
    public const string WindowSumExceeded = "score.windowSumExceeded";

    /// <summary>Flight-time sum exceeds the working time minus flight count.</summary>
    public const string TurnaroundCapExceeded = "score.turnaroundCapExceeded";

    /// <summary>
    /// Check one entry's task result against its resolved task. Returns the
    /// single applicable warning or null when the row is clean or the check
    /// does not apply (see the story's Applicability exclusions).
    /// </summary>
    /// <param name="resolvedTask">The resolved task (carries Timing, Flights, Score).</param>
    /// <param name="taskResult">The entry's task result (carries State, Selection).</param>
    /// <param name="competitorRef">The entry's row key, named in the message.</param>
    /// <param name="groupName">The group, named in the message.</param>
    /// <param name="taskCode">The task code, named in the message.</param>
    public static ScoreWarning? Check(
        ResolvedTask resolvedTask,
        TaskResult taskResult,
        string competitorRef,
        string groupName,
        string taskCode)
    {
        // Not Valid (or NoResult) → skip. Selection null (or empty) → skip.
        if (taskResult.State != TaskResultState.Valid)
            return null;
        if (taskResult.Selection is null || taskResult.Selection.Flights.IsDefaultOrEmpty)
            return null;

        int n = taskResult.Selection.Flights.Length;
        if (n <= 1)
            return null;

        // UntilAllFlightsComplete (working time is not a class datum) → skip.
        // WorkingTime null → skip.
        if (resolvedTask.Timing.Kind != WorkingTimeKind.Fixed)
            return null;
        if (resolvedTask.Timing.WorkingTime is null)
            return null;
        decimal w = resolvedTask.Timing.WorkingTime.Value;

        // Term indices whose (conditionally unwrapped) metric ref is flightTime.
        // None → skip (Poker-shaped tasks score nominated targetTime, not flown time).
        var flightTimeIndices = new List<int>();
        for (int i = 0; i < resolvedTask.Score.Length; i++)
        {
            if (ScoreTermRefs.GetTermMetricRef(resolvedTask.Score[i]) == "flightTime")
                flightTimeIndices.Add(i);
        }
        if (flightTimeIndices.Count == 0)
            return null;

        // Per-launch-window ceiling test (the F5K Task C case, stated
        // generically): skip when the task's own scoring allows more than the
        // shared window could ever contain — ceiling finite and W < ceiling.
        if (IsPerLaunchWindow(resolvedTask, w))
            return null;

        // The uncapped raw sum (MetricConsumed is never clamped); a missing
        // index contributes 0, never throws.
        decimal flightTimeSum = 0m;
        foreach (var flight in taskResult.Selection.Flights)
        {
            foreach (int i in flightTimeIndices)
            {
                if (flight.TermContributions.TryGetValue(i, out var contrib))
                    flightTimeSum += contrib.MetricConsumed;
            }
        }

        // Window rule first, then the turn-around rule. Exact decimal
        // comparison: the == W arm fires only for multi-flight selections
        // (guaranteed here by the n > 1 gate), where zero turn-around time is
        // physically impossible; exactly at the turn-around cap is clean.
        if (flightTimeSum > w || flightTimeSum == w)
            return new ScoreWarning(
                WindowSumExceeded,
                $"Entry {competitorRef} in group {groupName}: flight-time sum {flightTimeSum}s reaches the {w}s working time (task {taskCode})");

        decimal cap = w - n;
        if (flightTimeSum > cap)
            return new ScoreWarning(
                TurnaroundCapExceeded,
                $"Entry {competitorRef} in group {groupName}: flight-time sum {flightTimeSum}s exceeds the turn-around cap {cap}s (working time {w}s minus {n} flights, task {taskCode})");

        return null;
    }

    /// <summary>
    /// True when the task's own scoring ceiling exceeds the shared window, so
    /// the flights cannot share one window (per-launch windows). Structural:
    /// nMax is the selection's flight count (BestN/LastN/ExactlyN Count,
    /// AllFlights → MaxLaunches, LastFlight → 1); ceiling is the TargetValues
    /// sum where targets are assigned, else the per-flight PerFlight RateTerm
    /// cap × nMax; unbounded where neither exists (then this returns false).
    /// </summary>
    private static bool IsPerLaunchWindow(ResolvedTask resolvedTask, decimal w)
    {
        int? nMax = resolvedTask.Flights switch
        {
            BestNFlights bn => bn.Count,
            LastNFlights ln => ln.Count,
            ExactlyNInOrder en => en.Count,
            AllFlights => resolvedTask.Timing.MaxLaunches,
            LastFlight => 1,
            _ => null,
        };
        if (nMax is null)
            return false;

        decimal? ceiling = CeilingOf(resolvedTask, nMax.Value);
        return ceiling.HasValue && w < ceiling.Value;
    }

    private static decimal? CeilingOf(ResolvedTask resolvedTask, int nMax)
    {
        if (resolvedTask.Flights is BestNFlights bn
            && !bn.TargetValues.IsDefaultOrEmpty && bn.TargetValues.Length > 0)
            return SumOf(bn.TargetValues);

        if (resolvedTask.Flights is ExactlyNInOrder en
            && !en.TargetValues.IsDefaultOrEmpty && en.TargetValues.Length > 0)
            return SumOf(en.TargetValues);

        decimal perFlight = 0m;
        bool anyFlightTimeTerm = false;
        for (int i = 0; i < resolvedTask.Score.Length; i++)
        {
            if (ScoreTermRefs.GetTermMetricRef(resolvedTask.Score[i]) != "flightTime")
                continue;
            anyFlightTimeTerm = true;
            var cap = TryGetPerFlightCap(resolvedTask.Score[i]);
            if (cap is null)
                return null;
            perFlight += cap.Value;
        }

        if (!anyFlightTimeTerm)
            return null;
        return perFlight * nMax;
    }

    private static decimal SumOf(System.Collections.Immutable.ImmutableArray<decimal> values)
    {
        decimal sum = 0m;
        foreach (var v in values)
            sum += v;
        return sum;
    }

    /// <summary>
    /// The PerFlight literal cap on a (possibly conditionally wrapped) rate
    /// term, unwrapped generically — never per-task. Null where no such cap
    /// exists (uncapped, PerTask-scoped, or a non-rate term).
    /// </summary>
    private static decimal? TryGetPerFlightCap(ScoreTerm term) => term switch
    {
        RateTerm rt => rt.CapScope == CapScope.PerFlight && rt.Cap is NumberOrParam.Literal l
            ? l.Value
            : null,
        ConditionalTerm ct => TryGetPerFlightCap(ct.Then)
            ?? (ct.Else is not null ? TryGetPerFlightCap(ct.Else) : null),
        _ => null,
    };
}
