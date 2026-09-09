// FlightInterpreter — kanban/completed/scoring-service-plan.md WI-3.
//
// Evaluates one Flight's measurements through flightValidWhen and through the
// raw score terms. This is a pure function — same inputs, same outputs.
// Flight-local boundary: it sees one Flight's measurements + flight.sequence
// intrinsic. It never sees sibling flights or task-level state.

using System.Collections.Immutable;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;

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
    /// <param name="task">The resolved task definition.</param>
    /// <param name="flightSequence">The flight's 1-based sequence number.</param>
    /// <param name="resolvedMetrics">
    /// Pre-resolved effective measurements for this flight (amendments applied
    /// — MeasurementDigest.Resolve is the orchestrator's job, not this one's).
    /// </param>
    /// <param name="instruments">
    /// The effective instrument per metric (tape-points-landing-seeds.md WI-4),
    /// from <see cref="ResolvedMeasurements.Instruments"/> — null (or absent
    /// key) names no instrument: a distance. A reading is composed with the
    /// class table through WI-1's <see cref="TapeComposition"/> inside the
    /// lookup's own evaluation only; every predicate, validity gate and
    /// non-lookup term reads the observation untouched (owner decision 7).
    /// </param>
    /// <param name="declaredInstruments">
    /// The competition's declared set (WI-3) — the only scales a reading may
    /// name. Default (empty) is the competition that declared nothing: every
    /// measurement takes the existing distance path byte for byte.
    /// </param>
    public static InterpretedFlight Interpret(
        ResolvedTask task,
        int flightSequence,
        IReadOnlyDictionary<string, MeasuredValue> resolvedMetrics,
        IReadOnlyDictionary<string, string>? instruments = null,
        ImmutableArray<DeclaredInstrument> declaredInstruments = default)
    {
        // 1. Build metric dictionary: resolved metrics + flight.sequence intrinsic
        var metrics = new Dictionary<string, MeasuredValue>(resolvedMetrics)
        {
            [Intrinsic.FlightSequence] = MeasuredValue.Of(flightSequence)
        };

        // Normalise the instrument map to the ResolvedMeasurements convention
        // (null when no reading is on board) so results compare stably.
        if (instruments is not null && instruments.Count == 0)
            instruments = null;

        // The declared unit per metric, for WI-1's unit-match check at
        // composition time (the declaration already checked it — this is the
        // scoring-time half of the same gate, never a guess).
        Dictionary<string, string?>? metricUnits = null;
        if (!declaredInstruments.IsDefaultOrEmpty)
        {
            metricUnits = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var metric in task.Metrics)
                metricUnits[metric.Name] = metric.Unit;
        }

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
                        Measurements: new ResolvedMeasurements(metrics, instruments)
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
            var contribution = EvaluateTerm(task.Score[i], metrics, instruments, declaredInstruments, metricUnits);
            contributions[i] = contribution;
            totalScore += contribution.Points;
        }

        return new InterpretedFlight(
            Result: new FlightResult(
                State: FlightResultState.Valid,
                Measurements: new ResolvedMeasurements(metrics, instruments)
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
        IReadOnlyDictionary<string, MeasuredValue> metrics,
        IReadOnlyDictionary<string, string>? instruments = null,
        ImmutableArray<DeclaredInstrument> declaredInstruments = default,
        IReadOnlyDictionary<string, string?>? metricUnits = null)
    {
        return term switch
        {
            ConstantTerm t => EvaluateConstant(t),
            RateTerm t => EvaluateRate(t, metrics),
            LookupTerm t => EvaluateLookup(t, metrics, instruments, declaredInstruments, metricUnits),
            PiecewiseTerm t => EvaluatePiecewise(t, metrics),
            ConditionalTerm t => EvaluateConditional(t, metrics, instruments, declaredInstruments, metricUnits),
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

    private static TermContribution EvaluateLookup(
        LookupTerm term,
        IReadOnlyDictionary<string, MeasuredValue> metrics,
        IReadOnlyDictionary<string, string>? instruments,
        ImmutableArray<DeclaredInstrument> declaredInstruments,
        IReadOnlyDictionary<string, string?>? metricUnits)
    {
        // A reading names its instrument: compose it with the class table
        // through WI-1's pure function, inside this lookup's own evaluation.
        // A measurement naming none is a distance and takes the existing walk
        // below, byte for byte — including the fallthrough.
        if (instruments is not null
            && instruments.TryGetValue(term.MetricRef, out var instrument)
            && instrument is not null)
        {
            return EvaluateComposedLookup(term, metrics, instrument, declaredInstruments, metricUnits);
        }

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

    /// <summary>
    /// The composed evaluation (tape-points-landing-seeds.md WI-4): the
    /// reading's band, denoted by the named declared scale, awards what the
    /// class table gives every distance in that band (owner decision 5). The
    /// declaration gate already refused every pairing this function cannot
    /// compose, so each throw below is unreachable through the write path —
    /// a loud refusal, never a guess and never a distance fallback. The
    /// contribution consumes the READING (the observation, decision 4), not a
    /// reverse-mapped distance: there is no reverse-distance API.
    /// </summary>
    private static TermContribution EvaluateComposedLookup(
        LookupTerm term,
        IReadOnlyDictionary<string, MeasuredValue> metrics,
        string instrument,
        ImmutableArray<DeclaredInstrument> declaredInstruments,
        IReadOnlyDictionary<string, string?>? metricUnits)
    {
        DeclaredInstrument? binding = null;
        foreach (var declared in declaredInstruments)
        {
            if (declared.Instrument == instrument)
            {
                binding = declared;
                break;
            }
        }

        if (binding is null)
            throw new ArgumentException(
                $"Measurement '{term.MetricRef}' names instrument '{instrument}', which this competition " +
                "declares nothing for — a reading names a declared instrument or none, never an arbitrary one.");

        if (binding.Metric != term.MetricRef)
            throw new ArgumentException(
                $"Instrument '{instrument}' is declared for metric '{binding.Metric}', not '{term.MetricRef}'.");

        string? unit = null;
        metricUnits?.TryGetValue(term.MetricRef, out unit);
        var composed = TapeComposition.Compose(binding.Scale, unit, term.Rows);
        if (composed.IsFailure)
            throw new ArgumentException(
                $"Instrument '{instrument}' cannot score metric '{term.MetricRef}': " +
                $"that side of the tape cannot score this class [{composed.Code}]: {composed.Message}");

        var reading = GetNumberMetric(term.MetricRef, metrics);
        var award = composed.Value.Resolve(reading);
        if (award.IsFailure)
            throw new ArgumentException(
                $"Reading {reading} is not on instrument '{instrument}'s reading set [{award.Code}].");

        return new TermContribution(MetricConsumed: reading, Points: award.Value);
    }

    // kanban/completed/signed-width-piecewise-integration.md. The walk from the
    // origin to metric − origin is a SIGNED integral ∫rate·d(adjusted): the
    // direction of travel (the sign of `adjusted`) multiplies the accumulated
    // rate × width. A band above the origin contributes +width × rate; a band
    // below it has its rate's sign flipped by the direction of travel, which is
    // how the notation writes a bonus — "A negative rate over a negative
    // portion is what makes a low launch a bonus"
    // (docs/competition-class-notation.md, the `from <origin>` bullet). FAI F5K's
    // Below(0, −0.5) is correct as written; NZ F5K NDC's below-origin bonus
    // bands carry the same negative-rate encoding (NZ.3.16.29 g).
    private static TermContribution EvaluatePiecewise(PiecewiseTerm term, IReadOnlyDictionary<string, MeasuredValue> metrics)
    {
        var metricValue = GetNumberMetric(term.MetricRef, metrics);

        // Resolve origin: after ResolveTask, Origin is either null or a Literal
        decimal origin = term.Origin is NumberOrParam.Literal l ? l.Value : 0m;
        decimal adjusted = metricValue - origin;

        // The signed walk spans from 0 to adjusted; the direction of travel is
        // the sign of adjusted (0 contributes nothing — no band is traversed).
        decimal direction = adjusted < 0m ? -1m : 1m;
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

        return new TermContribution(MetricConsumed: metricValue, Points: total * direction);
    }

    private static TermContribution EvaluateConditional(
        ConditionalTerm term,
        IReadOnlyDictionary<string, MeasuredValue> metrics,
        IReadOnlyDictionary<string, string>? instruments,
        ImmutableArray<DeclaredInstrument> declaredInstruments,
        IReadOnlyDictionary<string, string?>? metricUnits)
    {
        // The condition reads the observations untouched (owner decision 7):
        // eligibility gates (F3J.10.8/.9 touch and overfly) still remove the
        // bonus for either input form. Composition rewrites only what the
        // branch's LookupTerm is evaluated against.
        if (PredicateEvaluator.Evaluate(term.When, metrics))
        {
            return EvaluateTerm(term.Then, metrics, instruments, declaredInstruments, metricUnits);
        }
        else if (term.Else is not null)
        {
            return EvaluateTerm(term.Else, metrics, instruments, declaredInstruments, metricUnits);
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
