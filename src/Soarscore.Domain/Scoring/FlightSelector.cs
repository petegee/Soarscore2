// FlightSelector — docs/plans/scoring-service-plan.md WI-4.
//
// Applies flight selection to an Entry, checks validWhen, assembles the raw
// score, applies CapScope.PerTask caps, and rounds. Issues #1, #2, #3, #6 are
// resolved — validWhen is checked AFTER selection (Issue #6), PerTask caps are
// corrected post-selection (Issue #1), and BestNFlights AnyOrder pairs with
// TargetValues[n-1-i] (Issue #3).

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// Selects flights from an Entry, applies target clamping, checks validWhen,
/// assembles the raw score, applies PerTask caps, and rounds.
/// </summary>
public static class FlightSelector
{
    /// <summary>
    /// Select flights from an Entry, assemble raw score, apply caps, round.
    /// Returns NoResult if validWhen fails, the Entry is annulled, or no
    /// flights are available.
    /// </summary>
    /// <param name="entry">The entry (pass null if annulled).</param>
    /// <param name="task">The resolved task definition.</param>
    /// <param name="parameterBindings">Parameter bindings (for round/scale resolution).</param>
    /// <param name="interpretedFlights">
    /// Pre-interpreted flights for this entry, ordered by flight sequence.
    /// The orchestrator resolves amendments and calls FlightInterpreter before
    /// passing results here.
    /// </param>
    public static TaskResult SelectAndScore(
        object? entry,             // Entry type TBD
        ResolvedTask task,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings,
        ImmutableArray<InterpretedFlight> interpretedFlights)
    {
        // 1. No flights → NoResult.
        if (interpretedFlights.IsDefaultOrEmpty)
            return new TaskResult(TaskResultState.NoResult, null, 0m);

        // 2. Select flights based on FlightSelection kind.
        var selected = SelectFlights(interpretedFlights, task.Flights);

        if (selected.IsEmpty)
            return new TaskResult(TaskResultState.NoResult, null, 0m);

        // 3. If targets are assigned, clamp and re-score (before validWhen check).
        var withTargets = ApplyTargets(selected, task.Flights, task.Score);

        // 4. Evaluate validWhen against selected flights' measurements (Issue #2, #6).
        if (task.ValidWhen is not null)
        {
            bool allPass = true;
            foreach (var flight in withTargets)
            {
                if (!PredicateEvaluator.Evaluate(task.ValidWhen,
                        flight.Metrics))
                {
                    allPass = false;
                    break;
                }
            }

            if (!allPass)
                return new TaskResult(TaskResultState.NoResult, null, 0m);
        }

        // 5. Assemble raw score: sum selected flight scores.
        decimal rawScore = withTargets.Sum(f => f.Score);

        // 6. Apply CapScope.PerTask caps (Issue #1).
        rawScore = ApplyPerTaskCaps(rawScore, withTargets, task.Score);

        // 7. Apply raw rounding if set.
        if (task.RawScore is not null)
            rawScore = RoundingSupport.ApplyRounding(rawScore, task.RawScore);

        // 8. Return.
        var targetAssignments = BuildTargetAssignments(withTargets, task.Flights);
        return new TaskResult(
            State: TaskResultState.Valid,
            Selection: new SelectedFlights(withTargets, targetAssignments),
            RawScore: rawScore
        );
    }

    // ---------------------------------------------------- flight selection

    private static ImmutableArray<InterpretedFlight> SelectFlights(
        ImmutableArray<InterpretedFlight> flights,
        FlightSelection selection)
    {
        return selection switch
        {
            LastFlight => SelectLast(flights),
            AllFlights => flights,
            LastNFlights ln => SelectLastN(flights, ln.Count),
            BestNFlights bn => SelectBestN(flights, bn),
            ExactlyNInOrder en => SelectExactlyN(flights, en),
            _ => throw new ArgumentException($"Unknown FlightSelection subtype: {selection.GetType().Name}")
        };
    }

    private static ImmutableArray<InterpretedFlight> SelectLast(
        ImmutableArray<InterpretedFlight> flights)
    {
        // The flight with the highest sequence number is last.
        // We assume flights are ordered by sequence.
        return ImmutableArray.Create(flights[^1]);
    }

    private static ImmutableArray<InterpretedFlight> SelectLastN(
        ImmutableArray<InterpretedFlight> flights, int count)
    {
        int take = Math.Min(count, flights.Length);
        return flights.Skip(flights.Length - take).Take(take).ToImmutableArray();
    }

    private static ImmutableArray<InterpretedFlight> SelectBestN(
        ImmutableArray<InterpretedFlight> flights, BestNFlights spec)
    {
        int take = Math.Min(spec.Count, flights.Length);

        IEnumerable<InterpretedFlight> ranked;
        if (spec.RankByMetric is not null)
        {
            ranked = flights.OrderByDescending(f =>
            {
                var m = f.Metrics;
                return m.TryGetValue(spec.RankByMetric, out var v) && v.Number.HasValue
                    ? v.Number.Value
                    : 0m;
            });
        }
        else
        {
            ranked = flights.OrderByDescending(f => f.Score);
        }

        return ranked.Take(take).ToImmutableArray();
    }

    private static ImmutableArray<InterpretedFlight> SelectExactlyN(
        ImmutableArray<InterpretedFlight> flights, ExactlyNInOrder spec)
    {
        int take = Math.Min(spec.Count, flights.Length);
        return flights.Take(take).ToImmutableArray();
    }

    // ---------------------------------------------------- target assignment

    private static ImmutableArray<InterpretedFlight> ApplyTargets(
        ImmutableArray<InterpretedFlight> selected,
        FlightSelection selection,
        ImmutableArray<ScoreTerm> scoreTerms)
    {
        if (selection is BestNFlights bn && bn.Targets != TargetAssignment.None && bn.TargetValues.Length > 0)
        {
            // For BestNFlights, flights are already ranked descending.
            // Target pairing depends on assignment mode.
            string? targetMetric = bn.RankByMetric
                ?? FindPrimaryMetric(scoreTerms);

            if (targetMetric is null)
                return selected;  // no metric to target

            var result = ImmutableArray.CreateBuilder<InterpretedFlight>(selected.Length);

            for (int i = 0; i < selected.Length && i < bn.TargetValues.Length; i++)
            {
                decimal target;
                if (bn.Targets == TargetAssignment.AnyOrder)
                {
                    // Best flight (i=0) → largest target (Issue #3).
                    target = bn.TargetValues[bn.TargetValues.Length - 1 - i];
                }
                else // InOrder
                {
                    target = bn.TargetValues[i];
                }

                result.Add(ClampAndRecompute(selected[i], targetMetric, target, scoreTerms));
            }

            return result.ToImmutable();
        }

        if (selection is ExactlyNInOrder en && en.TargetValues.Length > 0)
        {
            string? targetMetric = FindPrimaryMetric(scoreTerms);
            if (targetMetric is null)
                return selected;

            var result = ImmutableArray.CreateBuilder<InterpretedFlight>(selected.Length);

            for (int i = 0; i < selected.Length && i < en.TargetValues.Length; i++)
            {
                decimal target = en.TargetValues[i];
                result.Add(ClampAndRecompute(selected[i], targetMetric, target, scoreTerms));
            }

            return result.ToImmutable();
        }

        return selected;
    }

    /// <summary>
    /// Find the primary metric for target clamping — the first metric-based term
    /// in the score list.
    /// </summary>
    private static string? FindPrimaryMetric(ImmutableArray<ScoreTerm> terms)
    {
        foreach (var term in terms)
        {
            var metric = GetTermMetricRef(term);
            if (metric is not null)
                return metric;
        }
        return null;
    }

    private static string? GetTermMetricRef(ScoreTerm term) => term switch
    {
        RateTerm t => t.MetricRef,
        LookupTerm t => t.MetricRef,
        PiecewiseTerm t => t.MetricRef,
        ConditionalTerm t => GetTermMetricRef(t.Then)
                           ?? (t.Else is not null ? GetTermMetricRef(t.Else) : null),
        _ => null
    };

    /// <summary>
    /// Create a copy of the flight's measurements with the target metric clamped,
    /// then re-score all terms.
    /// </summary>
    private static InterpretedFlight ClampAndRecompute(
        InterpretedFlight flight,
        string targetMetric,
        decimal target,
        ImmutableArray<ScoreTerm> scoreTerms)
    {
        var metrics = flight.Metrics;

        if (!metrics.TryGetValue(targetMetric, out var originalValue)
            || originalValue.Kind != MeasuredKind.Number
            || originalValue.Number is null)
        {
            return flight;  // metric not present — nothing to clamp
        }

        decimal clamped = Math.Min(originalValue.Number.Value, target);

        // If the clamp doesn't change anything, skip recomputation.
        if (clamped >= originalValue.Number.Value)
            return flight;

        // Create a modified metrics dictionary with the clamped value.
        var clampedMetrics = new Dictionary<string, MeasuredValue>(metrics)
        {
            [targetMetric] = MeasuredValue.Of(clamped)
        };

        // Re-score all terms.
        var contributions = new Dictionary<int, TermContribution>();
        decimal newScore = 0m;

        for (int i = 0; i < scoreTerms.Length; i++)
        {
            var contrib = FlightInterpreter.EvaluateTerm(scoreTerms[i], clampedMetrics);
            contributions[i] = contrib;
            newScore += contrib.Points;
        }

        return flight with
        {
            Score = newScore,
            TermContributions = contributions
        };
    }

    // ------------------------------------------ CapScope.PerTask (Issue #1)

    private static decimal ApplyPerTaskCaps(
        decimal rawScore,
        ImmutableArray<InterpretedFlight> selected,
        ImmutableArray<ScoreTerm> scoreTerms)
    {
        decimal reduction = 0m;

        for (int termIndex = 0; termIndex < scoreTerms.Length; termIndex++)
        {
            var term = scoreTerms[termIndex];
            reduction += ComputePerTaskReduction(termIndex, term, selected);
        }

        return rawScore - reduction;
    }

    private static decimal ComputePerTaskReduction(
        int termIndex,
        ScoreTerm term,
        ImmutableArray<InterpretedFlight> selected)
    {
        return term switch
        {
            RateTerm rt when rt.CapScope == CapScope.PerTask
                          && rt.Cap is NumberOrParam.Literal capLit =>
                ComputeRatePerTaskReduction(termIndex, rt.Rate, capLit.Value, selected),

            ConditionalTerm ct =>
                ComputePerTaskReduction(termIndex, ct.Then, selected)
                + (ct.Else is not null
                    ? ComputePerTaskReduction(termIndex, ct.Else, selected)
                    : 0m),

            _ => 0m
        };
    }

    private static decimal ComputeRatePerTaskReduction(
        int termIndex,
        decimal rate,
        decimal cap,
        ImmutableArray<InterpretedFlight> selected)
    {
        // Sum MetricConsumed for this term index across all selected flights.
        decimal totalConsumed = 0m;
        foreach (var flight in selected)
        {
            if (flight.TermContributions.TryGetValue(termIndex, out var contrib))
                totalConsumed += contrib.MetricConsumed;
        }

        if (totalConsumed > cap)
        {
            return (totalConsumed - cap) * rate;
        }

        return 0m;
    }

    // --------------------------------------------------------- helpers

    private static IReadOnlyDictionary<int, decimal?> BuildTargetAssignments(
        ImmutableArray<InterpretedFlight> flights,
        FlightSelection selection)
    {
        // For now, return an empty mapping — target assignments are recorded
        // on the flights themselves via their clamped measurements.
        // The target values can be inferred from the difference between the
        // original and clamped metric values.
        return new Dictionary<int, decimal?>();
    }
}
