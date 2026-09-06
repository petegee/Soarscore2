// FlightMetricResolution — kanban/in-progress/metric-absence-semantics.md WI-1.
//
// The single point where a flight's measurements meet the task's declared
// metric semantics. After MeasurementDigest.Resolve has produced the
// amendment-resolved measurements, this class applies the class-declared
// absence semantics, per metric, before anything interprets the flight:
//
//   Tier 1 — the metric declares WhenNotRecorded: the assumed value is
//   inserted into the metrics (explicit capture always wins — the assumption
//   is only ever triggered by absence), and the flight interprets now.
//
//   Tier 2 — the metric is declared without an assumption and still absent:
//   the flight interprets as FlightResultState.Pending — never an error —
//   naming the awaited metric. Missing measurements are never tier 3.
//
//   Tier 3 — the absent metric is not declared on the task at all: definition
//   integrity, not a capture gap (adoption's CheckMetricReferencesResolve
//   refuses this shape) — still a loud ArgumentException.
//
// The referenced-metric walk is purely structural over the task's predicate
// and term shapes (ResolvedTask and TaskDefinition hold the same
// Predicate/ScoreTerm/FlightSelection shapes): it never branches on a metric
// name or a class/discipline. BestNFlights.RankByMetric is deliberately NOT
// part of the referenced set — it already degrades to 0 when absent
// (FlightSelector's ranking) and has never been a throw site.

using System.Collections.Immutable;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

public static class FlightMetricResolution
{
    /// <summary>The one closed intrinsic flight fact (F6) — always present, never declared.</summary>
    private const string FlightSequenceIntrinsic = "flight.sequence";

    /// <summary>
    /// The metric refs the task actually reads: every predicate
    /// (ValidWhen, FlightValidWhen, every ConditionalTerm.When) and every
    /// score term (Score and ScoreNormalised lists, through ConditionalTerm
    /// Then/Else). Public and reusable — the Application read model's
    /// RecordingCore needs the same notion (story WI-3). ResolvedTask and
    /// TaskDefinition expose the same shapes, so callers pass whichever they
    /// hold component-wise; the two overloads below do it for them.
    /// </summary>
    public static IReadOnlySet<string> ReferencedMetrics(
        ImmutableArray<ScoreTerm> score,
        ImmutableArray<ScoreTerm> scoreNormalised,
        Predicate? validWhen,
        Predicate? flightValidWhen)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var term in score) CollectTermRefs(term, refs);
        foreach (var term in scoreNormalised) CollectTermRefs(term, refs);

        if (validWhen is not null) CollectPredicateRefs(validWhen, refs);
        if (flightValidWhen is not null) CollectPredicateRefs(flightValidWhen, refs);

        return refs;
    }

    public static IReadOnlySet<string> ReferencedMetrics(ResolvedTask task) =>
        ReferencedMetrics(task.Score, task.ScoreNormalised, task.ValidWhen, task.FlightValidWhen);

    public static IReadOnlySet<string> ReferencedMetrics(TaskDefinition task) =>
        ReferencedMetrics(task.Score, task.ScoreNormalised, task.ValidWhen, task.FlightValidWhen);

    /// <summary>
    /// Resolve, apply absence semantics and interpret every flight of an
    /// Entry, in sequence order. The orchestrator (ScoringService) routes all
    /// flight interpretation through here — after this point no consumer can
    /// observe a declared-but-uncaptured referenced metric as a throw.
    /// </summary>
    public static ImmutableArray<InterpretedFlight> InterpretAllFlights(Entry entry, ResolvedTask task)
    {
        var referenced = ReferencedMetrics(task);
        var declaredByName = new Dictionary<string, MetricDefinition>(task.Metrics.Length, StringComparer.Ordinal);
        foreach (var metric in task.Metrics)
            declaredByName[metric.Name] = metric;

        var builder = ImmutableArray.CreateBuilder<InterpretedFlight>(entry.Flights.Length);

        foreach (var flight in entry.Flights)
            builder.Add(ResolveAndInterpret(task, flight, referenced, declaredByName));

        return builder.ToImmutable();
    }

    /// <summary>
    /// One flight: digest-resolve, insert assumed values, interpret — or
    /// pend. Absent-AND-referenced-AND-undeclared throws (tier 3); the first
    /// such metric in the task's declared-metric order is the awaited one
    /// when the flight pends (tier 2, deterministic).
    /// </summary>
    private static InterpretedFlight ResolveAndInterpret(
        ResolvedTask task,
        Flight flight,
        IReadOnlySet<string> referenced,
        IReadOnlyDictionary<string, MetricDefinition> declaredByName)
    {
        var resolved = MeasurementDigest.Resolve(flight);
        var metrics = new Dictionary<string, MeasuredValue>(resolved.Metrics);

        // Tier 1: assumed values — absence is the only trigger, so a recorded
        // value (or amendment, already resolved by the digest) always wins.
        foreach (var metric in task.Metrics)
        {
            if (metric.WhenNotRecorded is { } assumed && !metrics.ContainsKey(metric.Name))
                metrics[metric.Name] = assumed;
        }

        // Tier 3 first: an absent referenced metric the task does not declare
        // at all is definition/config integrity (adoption's
        // CheckMetricReferencesResolve refuses the shape) — never a capture
        // gap — so it stays a loud failure regardless of tier-2 candidates.
        foreach (var metricRef in referenced)
        {
            if (!declaredByName.ContainsKey(metricRef)
                && metricRef != FlightSequenceIntrinsic
                && !metrics.ContainsKey(metricRef))
            {
                throw new ArgumentException(
                    $"Metric '{metricRef}' is referenced by task '{task.Code}' but is not declared on it.");
            }
        }

        // Tier 2: the first referenced metric, in the task's declared-metric
        // order, still absent after assumption insertion.
        string? awaited = null;
        foreach (var metric in task.Metrics)
        {
            if (referenced.Contains(metric.Name) && !metrics.ContainsKey(metric.Name))
            {
                awaited = metric.Name;
                break;
            }
        }

        if (awaited is null)
            return FlightInterpreter.Interpret(task, flight.Sequence, metrics);

        // Tier 2: the awaited metric is declared without an assumption —
        // Pending, never an error. Score 0, no term contributions, and the
        // measurements as resolved (assumptions inserted) so reporting sees
        // exactly what the flight does carry.
        return new InterpretedFlight(
            Result: new FlightResult(
                State: FlightResultState.Pending,
                Measurements: new ResolvedMeasurements(metrics),
                Awaited: new PendingFlightDiagnostic(flight.Sequence, awaited)
            ),
            Score: 0m,
            TermContributions: new Dictionary<int, TermContribution>()
        );
    }

    // --------------------------------------------------------- structural walk

    private static void CollectTermRefs(ScoreTerm term, ISet<string> refs)
    {
        switch (term)
        {
            case RateTerm t:
                refs.Add(t.MetricRef);
                break;
            case LookupTerm t:
                refs.Add(t.MetricRef);
                break;
            case PiecewiseTerm t:
                refs.Add(t.MetricRef);
                break;
            case ConditionalTerm t:
                CollectPredicateRefs(t.When, refs);
                CollectTermRefs(t.Then, refs);
                if (t.Else is not null)
                    CollectTermRefs(t.Else, refs);
                break;
        }
    }

    private static void CollectPredicateRefs(Predicate predicate, ISet<string> refs)
    {
        switch (predicate)
        {
            case Comparison c:
                refs.Add(c.LeftMetricRef);
                if (c.RightMetricRef is { } right)
                    refs.Add(right);
                break;
            case AllOf allOf:
                foreach (var child in allOf.Children)
                    CollectPredicateRefs(child, refs);
                break;
        }
    }
}
