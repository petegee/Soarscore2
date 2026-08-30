// Scoring result types — kanban/completed/scoring-service-plan.md WI-1.
//
// Pure data, no behaviour. All types are sealed records. These are the value
// objects produced and consumed by the scoring pipeline stages (WI-3..WI-9).
//
// ResolvedTask is a snapshot of TaskDefinition with all NumberOrParam/FlagOrParam
// resolved to concrete values. Pipeline stages consume this — they never see
// unresolved parameter refs. It lives here, not in CompetitionClasses, because
// it is produced by ParameterResolver (a scoring concern) and consumed
// exclusively by the scoring pipeline (Issue #7).

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

// --------------------------------------------------------------- measurements

/// <summary>
/// Holds the effective measurements for one flight, with amendments resolved.
/// This is what all pipeline stages read — they never see raw Amendment lists.
/// </summary>
public sealed record ResolvedMeasurements(
    IReadOnlyDictionary<string, MeasuredValue> Metrics
);

// --------------------------------------------------------------- flight interpretation

/// <summary>
/// Per-term contribution: metric consumed (always the uncapped raw value, so
/// WI-4 can sum them for CapScope.PerTask) and points contributed.
/// </summary>
public sealed record TermContribution(
    decimal MetricConsumed,   // the metric value this term read (0 for Constant/Conditional)
    decimal Points            // the points this term contributed
);

public enum FlightResultState { Valid, NoResult }

/// <summary>One flight's effective measurements, with amendments resolved.</summary>
public sealed record FlightResult(
    FlightResultState State,
    ResolvedMeasurements Measurements
);

/// <summary>
/// One flight, evaluated through flightValidWhen and raw score terms.
/// </summary>
public sealed record InterpretedFlight(
    FlightResult Result,
    decimal Score,
    /// <summary>
    /// Per-term contributions: term list index → (raw metric consumed, points contributed).
    /// FlightSelector needs this for CapScope.PerTask rate terms.
    /// </summary>
    IReadOnlyDictionary<int, TermContribution> TermContributions
)
{
    /// <summary>Pass-through so callers need not know Result holds Measurements holds Metrics.</summary>
    public IReadOnlyDictionary<string, MeasuredValue> Metrics => Result.Measurements.Metrics;
}

// --------------------------------------------------------------- flight selection

/// <summary>
/// Which flights were selected and what target each was assigned.
/// </summary>
public sealed record SelectedFlights(
    ImmutableArray<InterpretedFlight> Flights,
    /// <summary>Maps flight sequence → assigned target value (null if no target for that flight).</summary>
    IReadOnlyDictionary<int, decimal?> TargetAssignments
);

public enum TaskResultState { Valid, NoResult }

/// <summary>One Entry's result for one task.</summary>
public sealed record TaskResult(
    TaskResultState State,
    SelectedFlights? Selection,  // null when NoResult
    decimal RawScore,            // summed + clamped + rounded
    /// <summary>
    /// Flag-only Disqualify marker, set only by the raw-stage engine path
    /// (PenaltyEngine.ApplyRawPenalties) for Disqualify effects on
    /// Flight/Entry-scoped records — no arithmetic change: state stays Valid
    /// and RawScore untouched (aggregate-stage Disqualify does not zero, so
    /// entry-scoped does not either), and OR-accumulated through the
    /// ScoreCompetition walk into
    /// <see cref="FinalCompetitorScore.Disqualified"/>
    /// (kanban/in-progress/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-2,
    /// D-B2). RankingEngine excludes flagged competitors from placings.
    /// </summary>
    bool Disqualified = false
);

// --------------------------------------------------------------- normalisation

/// <summary>One group's worth of task results, after normalisation.</summary>
public sealed record GroupResult(
    /// <summary>CompetitorRef → TaskResult. Includes NoResult entries.</summary>
    ImmutableDictionary<string, TaskResult> Results,
    /// <summary>The competitor with the best normalised score, or null if no valid results.</summary>
    string? WinnerRef,
    /// <summary>Count of Valid (non-NoResult) results in this group.</summary>
    int ValidCount,
    /// <summary>
    /// Per-result-key pre-normalisation score: the <see cref="TaskResult.RawScore"/>
    /// this row held when it entered Normalise, preserved through the overwrite.
    /// Keys are exactly <see cref="Results"/>' keys. Single writer:
    /// <see cref="NormalisationEngine.Normalise"/>
    /// (kanban/in-progress/pre-normalisation-score-view-field.md#WI-1).
    /// </summary>
    ImmutableDictionary<string, decimal> PreNormalisationScores,
    /// <summary>
    /// True when ValidCount &lt; MinValidResults — the group is annulled (Issue #5).
    /// The orchestrator reads this flag to annul the TaskRound on the Competition aggregate.
    /// </summary>
    bool IsAnnulled = false
);

// --------------------------------------------------------------- phase aggregation

/// <summary>One competitor's task-round score for one task in one round.</summary>
public sealed record TaskRoundScore(
    string TaskCode,
    int RoundOrdinal,
    int TaskOrdinal,
    decimal Score
);

/// <summary>One competitor's scores across a phase, after drops.</summary>
public sealed record PhaseScores(
    string CompetitorRef,
    decimal Aggregate,
    ImmutableArray<TaskRoundScore> AllScores,     // before drops
    ImmutableArray<TaskRoundScore> DroppedScores  // which scores were dropped
)
{
    /// <summary>The phase's pre-drop total: post-drop aggregate plus the dropped cells.</summary>
    public decimal PreDropAggregate => Aggregate + DroppedScores.Sum(s => s.Score);
}

// --------------------------------------------------------------- penalties

/// <summary>Penalties applied to one competitor at the aggregate stage.</summary>
public sealed record PenaltyApplication(
    decimal Deduction,
    bool Disqualified
);

/// <summary>
/// The task-round stage's outcome: the (possibly zeroed or deducted)
/// TaskResult plus the Disqualify flag accrued from the same records
/// (kanban/in-progress/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-2,
/// D-B1). Every declared effect acts at the stage that owns the record —
/// Zero* → NoResult, DeductPoints → subtract, Disqualify → the flag here,
/// carried out of the walk to final assembly. Flag-only (D-B2): no score
/// change, OR-accumulated into <see cref="FinalCompetitorScore.Disqualified"/>.
/// </summary>
public sealed record RawPenaltyApplication(
    TaskResult Result,
    bool Disqualified
);

/// <summary>A recorded penalty against an Entry or Competition.</summary>
public sealed record RecordedPenalty(
    string InfractionType,
    int OccurrenceCount
);

// --------------------------------------------------------------- ranking

/// <summary>A competitor's final score before ranking.</summary>
public sealed record FinalCompetitorScore(
    string CompetitorRef,
    decimal Score,
    /// <summary>
    /// Ranking's secondary ladder key: the pre-drop total, less the same
    /// aggregate-penalty deduction as <see cref="Score"/>. Score ties break on
    /// this before places are shared
    /// (kanban/completed/ranking-secondary-rawscore-key.md). Not the per-flight,
    /// pre-normalisation RawScore — do not conflate the two.
    /// </summary>
    decimal PreDropScore,
    bool Disqualified
);

/// <summary>The final competition result.</summary>
public sealed record CompetitionResult(
    ImmutableDictionary<string, FinalCompetitorScore> Scores,
    ImmutableDictionary<string, int> Placings
);

// --------------------------------------------------------------- resolved task

/// <summary>
/// A TaskDefinition with all NumberOrParam/FlagOrParam resolved to concrete values.
/// Pipeline stages consume this — they never see unresolved parameter refs.
/// </summary>
public sealed record ResolvedTask(
    string Code,
    string Name,
    ImmutableArray<MetricDefinition> Metrics,
    FlightSelection Flights,
    ResolvedTiming Timing,
    ResolvedGroupConstraint? Group,
    Normalisation? Normalise,
    Predicate? ValidWhen,
    Predicate? FlightValidWhen,
    Rounding? RawScore,
    ReflightRule? Reflight,
    ImmutableArray<ScoreTerm> Score,
    ImmutableArray<ScoreTerm> ScoreNormalised
);

public sealed record ResolvedTiming(
    WorkingTimeKind Kind,
    decimal? WorkingTime,
    decimal? PreparationTime,
    int? MaxLaunches
);

public sealed record ResolvedGroupConstraint(
    decimal MinPerGroup,
    int? MinValidResults
);
