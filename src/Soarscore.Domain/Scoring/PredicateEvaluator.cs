// PredicateEvaluator — kanban/completed/scoring-service-plan.md WI-3 (shared).
//
// Evaluates Predicates (Comparison / AllOf) against a set of resolved
// measurements. Used by FlightInterpreter (flightValidWhen, score-term
// conditionals) and FlightSelector (validWhen).

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// Evaluates a Predicate against a dictionary of metric name → MeasuredValue.
/// Pure function — same predicate + same measurements → same result.
/// </summary>
public static class PredicateEvaluator
{
    /// <summary>
    /// Evaluate a Predicate against a set of resolved measurements.
    /// </summary>
    public static bool Evaluate(
        Predicate predicate,
        IReadOnlyDictionary<string, MeasuredValue> measurements)
    {
        return predicate switch
        {
            Comparison c => EvaluateComparison(c, measurements),
            AllOf a => a.Children.All(child => Evaluate(child, measurements)),
            _ => throw new ArgumentException($"Unknown Predicate subtype: {predicate.GetType().Name}")
        };
    }

    // --------------------------------------------------------- private

    private static bool EvaluateComparison(
        Comparison comp,
        IReadOnlyDictionary<string, MeasuredValue> measurements)
    {
        if (!measurements.TryGetValue(comp.LeftMetricRef, out var left))
            throw new ArgumentException(
                $"Predicate references metric '{comp.LeftMetricRef}' which is not in the measurements dictionary.");

        MeasuredValue right;
        if (comp.RightMetricRef is not null)
        {
            if (!measurements.TryGetValue(comp.RightMetricRef, out var r))
                throw new ArgumentException(
                    $"Predicate references metric '{comp.RightMetricRef}' which is not in the measurements dictionary.");
            right = r;
        }
        else if (comp.RightValue is not null)
        {
            right = comp.RightValue;
        }
        else
        {
            throw new ArgumentException(
                $"Comparison on metric '{comp.LeftMetricRef}' has neither RightMetricRef nor RightValue.");
        }

        // Both sides must be the same kind
        if (left.Kind != right.Kind)
            throw new ArgumentException(
                $"Comparison between {left.Kind} (left: '{comp.LeftMetricRef}') and {right.Kind} (right) is not supported.");

        return left.Kind switch
        {
            MeasuredKind.Number => CompareNumbers(
                left.Number ?? throw new ArgumentException($"Metric '{comp.LeftMetricRef}' has no Number value."),
                right.Number ?? throw new ArgumentException("Right side of comparison has no Number value."),
                comp.Op),

            MeasuredKind.Flag => CompareFlags(
                left.Flag ?? throw new ArgumentException($"Metric '{comp.LeftMetricRef}' has no Flag value."),
                right.Flag ?? throw new ArgumentException("Right side of comparison has no Flag value."),
                comp.Op),

            _ => throw new ArgumentException($"Unknown MeasuredKind: {left.Kind}")
        };
    }

    private static bool CompareNumbers(decimal left, decimal right, Comparator op) => op switch
    {
        Comparator.LessThan => left < right,
        Comparator.LessOrEqual => left <= right,
        Comparator.GreaterThan => left > right,
        Comparator.GreaterOrEqual => left >= right,
        Comparator.EqualTo => left == right,
        _ => throw new ArgumentException($"Unknown Comparator: {op}")
    };

    private static bool CompareFlags(bool left, bool right, Comparator op)
    {
        // Only EqualTo is meaningful for flags. The adoption check prevents other
        // operators on flags from reaching production, but we guard at runtime.
        if (op != Comparator.EqualTo)
            throw new ArgumentException(
                $"Comparator {op} is not supported for Flag metrics. Only EqualTo is valid for flags.");

        return left == right;
    }
}
