// The Competition Class structure — docs/soaring-domain-class-diagram.md §2.
//
// The rulebook: an ordered list of phase definitions, each owning its own tasks
// and aggregation; the class owns only what is genuinely true of the whole
// event. ADR-0002 §1 makes the canonical JSON of a ClassDefinition the
// definition — the wire format, the stored artefact, and what a Competition
// copies into AdoptedRules.
//
// Nothing here is a notation construct. `metricSet`, `rows`, `bands` and `like`
// are notation sugar (§7.1) and expand before adoption, so the model only ever
// holds the expanded instance.

using System.Collections.Immutable;

namespace Soarscore.Domain.PublishedClassDefinition;

public sealed record Parameter
{
    public required string Name { get; init; }

    public MeasuredKind Kind { get; init; } = MeasuredKind.Number;

    /// <summary>
    /// Nullable — a Flag parameter has no unit. Where a ParameterRef consumes a
    /// parameter in a slot that has its own unit the two must agree, checked at
    /// adoption (check 7).
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// Unset is the notation's `no default` (F12): a value the rules leave
    /// ENTIRELY open, so the CD chooses at setup and the choice enters the event
    /// log. Reading a definition for these finds every rulebook silence.
    /// </summary>
    public MeasuredValue? DefaultValue { get; init; }

    /// <summary>The permitted bindings, where the rules state them (F8).</summary>
    public ImmutableArray<MeasuredValue> AllowedValues { get; init; } = [];

    public ParameterBindingPoint BoundAt { get; init; } = ParameterBindingPoint.CompetitionSetup;
}

/// <summary>
/// Two roles, one event: the entitled competitor takes the re-flight; everyone
/// else in the group takes the better of two. The class states the default; a
/// Task overrides it where its rules differ (F19).
/// </summary>
public sealed record ReflightRule
{
    public required ReflightSelection EntitledScores { get; init; }

    public required ReflightSelection OthersScore { get; init; }

    /// <summary>
    /// Nullable, and absent is NOT "unstated". Where the rulebook is silent the
    /// class declares a no-default Parameter and this holds a ParameterRef.
    /// Absent means the field is INAPPLICABLE because no new group is ever formed
    /// (F26; F3F.1.5 re-flies one pilot into the running order). Zero is never
    /// correct — it would assert that a group of none is an acceptable minimum.
    /// </summary>
    public NumberOrParam? MinNewGroupSize { get; init; }
}

/// <summary>
/// There is no appliedAt: the pipeline stage is a property of the EFFECT within
/// the stages where the recorded penalty's SCOPE makes it visible. Flight/Entry-
/// scoped records act entirely at the task-round stage — Zero* zero the raw
/// score and DeductPoints deducts pre-normalisation; TaskRound/Competition-
/// scoped records act on the final aggregate (DeductPoints/Disqualify).
/// Decision D1: kanban/in-progress/entry-scoped-deduct-points-penalties-inert.md#wi-1.
/// </summary>
public sealed record PenaltyEffectSpec(PenaltyEffect Effect, decimal? Points = null);

public sealed record PenaltyDefinition
{
    public required string InfractionType { get; init; }

    /// <summary>
    /// A LIST, because exclusion is PAIRWISE and not an equivalence class (F28).
    /// May be empty. Within one flight attempt at most one penalty from a group
    /// applies, the largest ACCRUED CONTRIBUTION winning; suppression is computed
    /// in one pass from the recorded infractions.
    /// </summary>
    public ImmutableArray<string> ExclusionGroups { get; init; } = [];

    /// <summary>PerOccurrence multiplies the deduction by the recorded occurrences (F23).</summary>
    public PenaltyAccrual Accrual { get; init; } = PenaltyAccrual.OncePerAttempt;

    /// <summary>1..*: one infraction may act twice at two points in the pipeline (F20).</summary>
    public required ImmutableArray<PenaltyEffectSpec> Effects { get; init; }
}

public sealed record RoundComposition
{
    public CompositionKind Kind { get; init; } = CompositionKind.FixedSequence;

    public int TasksPerRound { get; init; } = 1;

    public bool RequireDistinctTaskPerRound { get; init; }

    /// <summary>
    /// Nullable; unset means the rules state no ceiling. It bounds what may be
    /// SCHEDULED, which is why it is not on ValidityRule (F21).
    /// </summary>
    public int? MaxRounds { get; init; }
}

/// <summary>
/// Both gates are nullable and CONJUNCTIVE: a drop applies only when every
/// populated gate holds (F18). A phase holds an ORDERED list and the first whose
/// gates all hold applies; adoption rejects a list whose gates are not strictly
/// descending (F22).
/// </summary>
public sealed record DropPolicy
{
    public required DropDimension Dimension { get; init; }

    public required int DropCount { get; init; }

    public int? ApplyWhenRoundsCompletedAtLeast { get; init; }

    public int? ApplyWhenResultsAtLeast { get; init; }
}

public sealed record ValidityRule
{
    public required NumberOrParam MinRounds { get; init; }

    /// <summary>
    /// A discrete whole-number count, unlike <see cref="MinRounds"/> — not one
    /// of the thirteen ParameterRef-permitted slots (ParameterReference.cs),
    /// and the notation (§4) never showed a `param(...)` form for it either.
    /// </summary>
    public int? MinTasks { get; init; }
}

/// <summary>
/// topN and topPercent are a two-way choice and exactly one is populated, per
/// kind. Splitting this into two subtypes was considered and declined: one scalar
/// on each side against three shared attributes.
/// </summary>
public sealed record PromotionRule
{
    public required PromotionKind Kind { get; init; }

    public NumberOrParam? TopN { get; init; }

    public decimal? TopPercent { get; init; }

    public NumberOrParam? MinGroupSize { get; init; }

    /// <summary>Unset is the notation's `..unlimited`.</summary>
    public NumberOrParam? MaxGroupSize { get; init; }

    public FlagOrParam? CarryPenalties { get; init; }
}

public sealed record TaskDefinition
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required ImmutableArray<MetricDefinition> Metrics { get; init; }

    public required FlightSelection Flights { get; init; }

    public required TaskTiming Timing { get; init; }

    public GroupConstraint? Group { get; init; }

    public Normalisation? Normalise { get; init; }

    /// <summary>Decides whether the TASK has a result at all — NoResult, not zero (F2).</summary>
    public Predicate? ValidWhen { get; init; }

    /// <summary>Zeroes ONE FLIGHT while leaving it selected (F17).</summary>
    public Predicate? FlightValidWhen { get; init; }

    /// <summary>Rounding of the raw score, before normalising (F4b). Only F5K sets it.</summary>
    public Rounding? RawScore { get; init; }

    /// <summary>Overrides the class default for this task only (F19).</summary>
    public ReflightRule? Reflight { get; init; }

    /// <summary>
    /// 1..*. The raw terms build the value normalisation consumes, which is what
    /// all seven FAI classes want — F5J and F5L deliberately normalise their
    /// landing bonus along with the flight time.
    /// </summary>
    public required ImmutableArray<ScoreTerm> Score { get; init; }

    /// <summary>
    /// 0..*, added AFTER normalising and NOT scaled by it (F24). NZ.3.12.1 e:
    /// "landing points will be added to the normalized flight score". The two
    /// orders give different scores and, in a close group, a different ORDER.
    /// The list a term sits in names the stage it lands at; there is no applyAt.
    /// </summary>
    public ImmutableArray<ScoreTerm> ScoreNormalised { get; init; } = [];
}

/// <summary>
/// A flyoff changes working times, caps, available tasks and penalty carry-over.
/// Those rules live here, not on the class. There is no `mandatory` flag:
/// mandatoriness is conditional on the EVENT LEVEL, which nothing in the model
/// represents.
/// </summary>
public sealed record PhaseDefinition
{
    public required int Ordinal { get; init; }

    public required PhaseType Type { get; init; }

    public RoundComposition Rounds { get; init; } = new();

    public required ValidityRule Validity { get; init; }

    /// <summary>Ordered, first match wins; MAY BE EMPTY, and empty means no discard.</summary>
    public ImmutableArray<DropPolicy> Drops { get; init; } = [];

    /// <summary>Appears only on a phase after the first.</summary>
    public PromotionRule? Promotion { get; init; }

    public required ImmutableArray<TaskDefinition> Tasks { get; init; }
}

/// <summary>
/// The rulebook itself — a value object, not the aggregate root. The
/// aggregate root is <see cref="PublishedClassDefinition"/>, which wraps one
/// of these with its content-hash identity, publish/retire history and
/// events; this type carries none of that, deliberately (Shared.cs / WI-0
/// finding: "there is no ClassDefinitionId to mint"). No versioning semantics
/// either way (ADR-0002 §5): the Competition's copy in AdoptedRules is the
/// only thing that matters, and `version` is a free-text human label that
/// nothing resolves.
/// </summary>
public sealed record ClassDefinition
{
    public required string Name { get; init; }

    /// <summary>Empty for a national class — the four NZ definitions leave it blank.</summary>
    public string? FaiDesignation { get; init; }

    /// <summary>Provenance only. Nothing may resolve it (ADR-0002 §5).</summary>
    public required string Version { get; init; }

    /// <summary>
    /// Unset means SinglePhase, and is available only to a one-phase class — the
    /// phase list forces the value. Two adoption checks, one each way (11, 12).
    /// </summary>
    public FinalRankingKind? FinalRanking { get; init; }

    public ImmutableArray<Parameter> Parameters { get; init; } = [];

    public required ReflightRule Reflight { get; init; }

    public ImmutableArray<PenaltyDefinition> Penalties { get; init; } = [];

    /// <summary>1..*, ordered.</summary>
    public required ImmutableArray<PhaseDefinition> Phases { get; init; }
}
