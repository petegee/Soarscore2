// ScoreTermRefs — kanban/in-progress/per-term-score-breakdown.md WI-2.
//
// The single shared conditional-unwrap for "which metric does this term
// consume": Rate/Lookup/Piecewise name it directly, ConstantTerm names none
// (null), ConditionalTerm unwraps to the taken branch shape (Then preferred,
// Else fallback). Factored here — not duplicated in Application — so the
// per-term wire projection (ScoreTaskRoundHandler.MapGroupResult) and the
// engine's own target-metric walk (FlightSelector.FindPrimaryMetric) cannot
// drift the first time a gate shape changes.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

public static class ScoreTermRefs
{
    /// <summary>
    /// The metric this term consumes, or null for <see cref="ConstantTerm"/>
    /// (which consumes none). Conditional branches unwrap to the branch
    /// metric (Then preferred, Else fallback) — the same unwrap the engine's
    /// target-clamp path uses. Never a class or landing special-case (NFR-1):
    /// landing is just whichever term consumes the class's landing metric.
    /// </summary>
    public static string? GetTermMetricRef(ScoreTerm term) => term switch
    {
        RateTerm t => t.MetricRef,
        LookupTerm t => t.MetricRef,
        PiecewiseTerm t => t.MetricRef,
        ConditionalTerm t => GetTermMetricRef(t.Then)
                           ?? (t.Else is not null ? GetTermMetricRef(t.Else) : null),
        _ => null,
    };
}
