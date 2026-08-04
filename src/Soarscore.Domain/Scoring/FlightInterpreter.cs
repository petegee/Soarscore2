// FlightInterpreter — docs/plans/scoring-service-plan.md WI-3.
//
// Evaluates one Flight's measurements through flightValidWhen and through the
// raw score terms. This is a pure function — same inputs, same outputs.
// Flight-local boundary: it sees one Flight's measurements + flight.sequence
// intrinsic. It never sees sibling flights or task-level state.

using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// Evaluates a single flight through its task's validity gate and score terms.
/// Produces an InterpretedFlight with the score and per-term breakdown.
/// </summary>
public static class FlightInterpreter
{
    /// <summary>
    /// Evaluate one flight: resolve measurements, apply flightValidWhen,
    /// evaluate each raw score term, return score + per-term breakdown.
    /// </summary>
    /// <param name="flight">The flight (unused directly — measurements come pre-resolved).</param>
    /// <param name="task">The resolved task definition.</param>
    /// <param name="flightSequence">The flight's 1-based sequence number.</param>
    /// <param name="resolvedMetrics">
    /// Pre-resolved effective measurements for this flight (amendments applied).
    /// </param>
    public static InterpretedFlight Interpret(
        object? flight,           // Flight type TBD — measurements come pre-resolved
        ResolvedTask task,
        int flightSequence,
        IReadOnlyDictionary<string, MeasuredValue> resolvedMetrics)
    {
        // 1. Build metric dictionary: resolved metrics + flight.sequence intrinsic
        var metrics = new Dictionary<string, MeasuredValue>(resolvedMetrics)
        {
            [Intrinsic.FlightSequence] = MeasuredValue.Of(flightSequence)
        };

        // 2. Evaluate flightValidWhen
        if (task.FlightValidWhen is not null)
        {
            if (!PredicateEvaluator.Evaluate(task.FlightValidWhen, metrics))
            {
                // Flight is zeroed, still counted (State = Valid).
                // All TermContributions are zeroed.
                return new InterpretedFlight(
                    Result: new FlightResult(
                        State: FlightResultState.Valid,
                        Measurements: new ResolvedMeasurements(metrics)
                    ),
                    Score: 0m,
                    TermContributions: new Dictionary<int, TermContribution>()
                );
            }
        }

        // 3. Evaluate raw score terms
        var contributions = new Dictionary<int, TermContribution>();
        decimal totalScore = 0m;

        for (int i = 0; i < task.Score.Length; i++)
        {
            var contribution = EvaluateTerm(task.Score[i], metrics);
            contributions[i] = contribution;
            totalScore += contribution.Points;
        }

        return new InterpretedFlight(
            Result: new FlightResult(
                State: FlightResultState.Valid,
                Measurements: new ResolvedMeasurements(metrics)
            ),
            Score: totalScore,
            TermContributions: contributions
        );
    }

    /// <summary>
    /// Evaluate a single ScoreTerm against resolved measurements.
    /// Internal static — shared with WI-5 (NormalisationEngine) for ScoreNormalised terms.
    /// </summary>
    internal static TermContribution EvaluateTerm(
        ScoreTerm term,
        IReadOnlyDictionary<string, MeasuredValue> metrics)
    {
        return term switch
        {
            ConstantTerm t => EvaluateConstant(t),
            RateTerm t => EvaluateRate(t, metrics),
            LookupTerm t => EvaluateLookup(t, metrics),
            PiecewiseTerm t => EvaluatePiecewise(t, metrics),
            ConditionalTerm t => EvaluateConditional(t, metrics),
            _ => throw new ArgumentException($"Unknown ScoreTerm subtype: {term.GetType().Name}")
        };
    }

    // --------------------------------------------------------- private

    private static TermContribution EvaluateConstant(ConstantTerm term)
    {
        return new TermContribution(MetricConsumed: 0m, Points: term.Value);
    }

    private static TermContribution EvaluateRate(RateTerm term, IReadOnlyDictionary<string, MeasuredValue> metrics)
    {
        var rawMetric = GetNumberMetric(term.MetricRef, metrics);

        // Resolve cap: after ResolveTask, Cap is either null or a Literal
        decimal? capValue = term.Cap is NumberOrParam.Literal l ? l.Value : null;

        // PerFlight: clamp the metric. PerTask: leave uncapped — WI-4 corrects.
        decimal effectiveMetric = rawMetric;
        if (capValue.HasValue && term.CapScope == CapScope.PerFlight)
            effectiveMetric = Math.Min(rawMetric, capValue.Value);

        decimal points = effectiveMetric * term.Rate;

        return new TermContribution(
            MetricConsumed: rawMetric,  // always the uncapped raw value
            Points: points
        );
    }

    private static TermContribution EvaluateLookup(LookupTerm term, IReadOnlyDictionary<string, MeasuredValue> metrics)
    {
        var metricValue = GetNumberMetric(term.MetricRef, metrics);

        // Walk rows in ascending order. First row where metricValue <= UpTo (or UpTo null) wins.
        foreach (var row in term.Rows)
        {
            if (row.UpTo is null || metricValue <= row.UpTo.Value)
            {
                return new TermContribution(MetricConsumed: metricValue, Points: row.Points);
            }
        }

        // Should not reach here — the last row should have null UpTo.
        return new TermContribution(MetricConsumed: metricValue, Points: 0m);
    }

    private static TermContribution EvaluatePiecewise(PiecewiseTerm term, IReadOnlyDictionary<string, MeasuredValue> metrics)
    {
        var metricValue = GetNumberMetric(term.MetricRef, metrics);

        // Resolve origin: after ResolveTask, Origin is either null or a Literal
        decimal origin = term.Origin is NumberOrParam.Literal l ? l.Value : 0m;
        decimal adjusted = metricValue - origin;

        // The adjusted value spans from 0 to adjusted (or adjusted to 0 if negative).
        decimal valueStart = Math.Min(0m, adjusted);
        decimal valueEnd = Math.Max(0m, adjusted);

        decimal total = 0m;

        foreach (var band in term.Bands)
        {
            // Resolve band bounds: after ResolveTask, From/To are Literal or null
            decimal bandStart = band.From is NumberOrParam.Literal fl
                ? fl.Value
                : decimal.MinValue;  // null → unbounded below

            decimal bandEnd = band.To is NumberOrParam.Literal tl
                ? tl.Value
                : decimal.MaxValue;  // null → unbounded above

            // Find the overlap between [bandStart, bandEnd] and [valueStart, valueEnd]
            decimal overlapStart = Math.Max(bandStart, valueStart);
            decimal overlapEnd = Math.Min(bandEnd, valueEnd);

            if (overlapEnd > overlapStart)
            {
                decimal width = overlapEnd - overlapStart;
                total += width * band.RatePerUnit;
            }
        }

        return new TermContribution(MetricConsumed: metricValue, Points: total);
    }

    private static TermContribution EvaluateConditional(
        ConditionalTerm term,
        IReadOnlyDictionary<string, MeasuredValue> metrics)
    {
        if (PredicateEvaluator.Evaluate(term.When, metrics))
        {
            return EvaluateTerm(term.Then, metrics);
        }
        else if (term.Else is not null)
        {
            return EvaluateTerm(term.Else, metrics);
        }
        else
        {
            return new TermContribution(MetricConsumed: 0m, Points: 0m);
        }
    }

    /// <summary>
    /// Read a Number metric from the measurements dictionary.
    /// Throws if the metric is missing or is a Flag.
    /// </summary>
    private static decimal GetNumberMetric(string metricRef, IReadOnlyDictionary<string, MeasuredValue> metrics)
    {
        if (!metrics.TryGetValue(metricRef, out var value))
            throw new ArgumentException(
                $"Metric '{metricRef}' referenced by a score term is not in the measurements dictionary.");

        if (value.Kind != MeasuredKind.Number)
            throw new ArgumentException(
                $"Metric '{metricRef}' is a {value.Kind}, but a Number metric was expected.");

        return value.Number ?? throw new ArgumentException(
            $"Metric '{metricRef}' has no Number value.");
    }

    /// <summary>
    /// Intrinsic metric names — the one flight fact the model exposes (F6).
    /// </summary>
    private static class Intrinsic
    {
        public const string FlightSequence = "flight.sequence";
    }
}
