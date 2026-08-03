// The Competition Class definition, as sealed C# records.
//
// Transcribed from docs/soaring-domain-class-diagram.md §2 and §3. One record
// per model element, one property per attribute; nothing here is a notation
// construct. Sugar (`metricSet`, `rows`, `bands`, `like`) has no representation
// — per notation §7.1 it expands before adoption, so the model only ever holds
// the expanded instance.
//
// Polymorphism is declared with hand-written discriminators per LADR-0003:
// a namespace or type rename must never orphan a historical event.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Soarscore.Spike.ClassModel;

// ---------------------------------------------------------------- enumerations

public enum MeasuredKind { Number, Flag }
public enum ParameterBindingPoint { CompetitionSetup, BeforeFlying, PerRound }
public enum PhaseType { Preliminary, Flyoff }
public enum CompositionKind { FixedSequence, ChooseFromCatalogue }
public enum DropDimension { ByRound, ByTask }
public enum PromotionKind { TopN, TopPercent }
public enum FinalRankingKind { SinglePhase, LastPhaseReplaces, SplitByPromotion }
public enum ReflightSelection { Replacement, BetterOf, NotPermitted, UndefinedRequiresRuling }
public enum PenaltyEffect { DeductPoints, ZeroFlight, ZeroRound, ZeroTask, Disqualify }
public enum PenaltyAccrual { OncePerAttempt, PerOccurrence }
public enum TargetAssignment { None, AnyOrder, InOrder }
public enum CapScope { PerFlight, PerTask }
public enum ScoreStage { RawScore, Normalised }
public enum Comparator { LessThan, LessOrEqual, GreaterThan, GreaterOrEqual, EqualTo }
public enum WorkingTimeKind { Fixed, UntilAllFlightsComplete }
public enum NormalisationDirection { HigherIsBetter, LowerIsBetter }
public enum RoundingMode { Truncate, HalfUp, Ceiling }

// ------------------------------------------------------- literal-or-parameter
//
// A ParameterRef stands in for a literal in exactly thirteen slots (class
// diagram §2). Twelve of them are numeric; PromotionRule.carryPenalties is the
// one that is not — see the note in NumberOrParam.cs.

// FINDING. These two unions carry NO [JsonPolymorphic] attribute, and cannot:
// System.Text.Json throws at configuration time —
//
//   NotSupportedException: The converter for derived type
//   'NumberOrParam' does not support metadata writes or reads.
//
// — the moment a type declares polymorphism metadata AND a JsonConverter<T> is
// registered for it. A hand-written converter and attribute-declared
// discriminators are mutually exclusive per type. The two routes in LADR-0003's
// open question are therefore a genuine either/or, and the choice is made once
// per hierarchy rather than blended. Both forms are emitted for comparison by
// registering one converter or the other (see Json.cs).

/// <summary>A number, or a reference to a declared Parameter.</summary>
public abstract record NumberOrParam
{
    private NumberOrParam() { }

    public sealed record Literal(decimal Value) : NumberOrParam;

    public sealed record Ref(string ParameterName) : NumberOrParam;

    public static implicit operator NumberOrParam(decimal v) => new Literal(v);
    public static implicit operator NumberOrParam(int v) => new Literal(v);

    public static NumberOrParam Param(string name) => new Ref(name);
}

/// <summary>A flag, or a reference to a declared Parameter.</summary>
public abstract record FlagOrParam
{
    private FlagOrParam() { }

    public sealed record Literal(bool Value) : FlagOrParam;

    public sealed record Ref(string ParameterName) : FlagOrParam;

    public static implicit operator FlagOrParam(bool v) => new Literal(v);

    public static FlagOrParam Param(string name) => new Ref(name);
}

// --------------------------------------------------------------- value objects

public sealed record MeasuredValue
{
    public required MeasuredKind Kind { get; init; }
    public decimal? Number { get; init; }
    public bool? Flag { get; init; }

    public static MeasuredValue Of(decimal n) => new() { Kind = MeasuredKind.Number, Number = n };
    public static MeasuredValue Of(bool f) => new() { Kind = MeasuredKind.Flag, Flag = f };
}

public sealed record Rounding(RoundingMode Mode, decimal Precision);

public sealed record Parameter
{
    public required string Name { get; init; }
    public MeasuredKind Kind { get; init; } = MeasuredKind.Number;

    /// <summary>Unset is `no default` — the rules leave the value entirely open (F12).</summary>
    public MeasuredValue? DefaultValue { get; init; }

    public ImmutableArray<MeasuredValue> AllowedValues { get; init; } = [];
    public ParameterBindingPoint BoundAt { get; init; } = ParameterBindingPoint.CompetitionSetup;
}

public sealed record ReflightRule
{
    public required ReflightSelection EntitledScores { get; init; }
    public required ReflightSelection OthersScore { get; init; }

    /// <summary>Absent means no new group is ever formed; never zero (class diagram §2).</summary>
    public NumberOrParam? MinNewGroupSize { get; init; }
}

public sealed record PenaltyEffectSpec(PenaltyEffect Effect, decimal? Points = null);

public sealed record PenaltyDefinition
{
    public required string InfractionType { get; init; }
    public ImmutableArray<string> ExclusionGroups { get; init; } = [];
    public PenaltyAccrual Accrual { get; init; } = PenaltyAccrual.OncePerAttempt;
    public required ImmutableArray<PenaltyEffectSpec> Effects { get; init; }
}

public sealed record RoundComposition
{
    public CompositionKind Kind { get; init; } = CompositionKind.FixedSequence;
    public int TasksPerRound { get; init; } = 1;
    public bool RequireDistinctTaskPerRound { get; init; }

    /// <summary>Unset means the rules state no ceiling.</summary>
    public int? MaxRounds { get; init; }
}

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
    public NumberOrParam? MinTasks { get; init; }
}

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

public sealed record MetricDefinition
{
    public required string Name { get; init; }
    public required MeasuredKind Kind { get; init; }
    public string? Unit { get; init; }
    public bool DeclaredBeforeLaunch { get; init; }

    /// <summary>Capture precision; a Flag has nothing to round.</summary>
    public Rounding? Precision { get; init; }
}

public sealed record TaskTiming
{
    public required WorkingTimeKind Kind { get; init; }

    /// <summary>Populated iff Kind is Fixed. Seconds.</summary>
    public NumberOrParam? WorkingTime { get; init; }

    /// <summary>Seconds.</summary>
    public NumberOrParam? PreparationTime { get; init; }

    /// <summary>Unset means the task limits launches not at all.</summary>
    public NumberOrParam? MaxLaunches { get; init; }
}

public sealed record GroupConstraint
{
    public required NumberOrParam MinPerGroup { get; init; }
    public int? MinValidResults { get; init; }
}

public sealed record Normalisation
{
    public required NormalisationDirection Direction { get; init; }
    public required int WinnerScore { get; init; }
    public Rounding? Round { get; init; }
}

public sealed record Band(NumberOrParam? From, NumberOrParam? To, decimal RatePerUnit);
// From/To null are the notation's `any` — unbounded below / above.

public sealed record LookupRow(decimal? UpTo, decimal Points);
// UpTo null is unbounded; legal only on the last row.

// ------------------------------------------------------------------ predicates

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(Comparison), "comparison")]
[JsonDerivedType(typeof(AllOf), "allOf")]
public abstract record Predicate
{
    private protected Predicate() { }
}

public sealed record Comparison : Predicate
{
    public required string LeftMetricRef { get; init; }
    public required Comparator Op { get; init; }

    /// <summary>Exactly one of RightMetricRef / RightValue is populated.</summary>
    public string? RightMetricRef { get; init; }

    public MeasuredValue? RightValue { get; init; }
}

public sealed record AllOf : Predicate
{
    /// <summary>2..* — a one-element conjunction is unwritable.</summary>
    public required ImmutableArray<Predicate> Children { get; init; }
}

// ------------------------------------------------------------ flight selection

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(LastFlight), "last")]
[JsonDerivedType(typeof(AllFlights), "all")]
[JsonDerivedType(typeof(LastNFlights), "lastN")]
[JsonDerivedType(typeof(BestNFlights), "bestN")]
[JsonDerivedType(typeof(ExactlyNInOrder), "exactlyN")]
public abstract record FlightSelection
{
    private protected FlightSelection() { }
}

public sealed record LastFlight : FlightSelection;

public sealed record AllFlights : FlightSelection;

public sealed record LastNFlights(int Count) : FlightSelection;

public sealed record BestNFlights : FlightSelection
{
    public required int Count { get; init; }

    /// <summary>Null ranks the candidates by score (F3K.11.5); set ranks by raw value (F3K.11.8).</summary>
    public string? RankByMetric { get; init; }

    public TargetAssignment Targets { get; init; } = TargetAssignment.None;

    /// <summary>In the units of the metric the score term consumes, not points.</summary>
    public ImmutableArray<decimal> TargetValues { get; init; } = [];
}

public sealed record ExactlyNInOrder : FlightSelection
{
    public required int Count { get; init; }
    public TargetAssignment Targets { get; init; } = TargetAssignment.InOrder;
    public ImmutableArray<decimal> TargetValues { get; init; } = [];
}

// ----------------------------------------------------------------- score terms

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(RateTerm), "rate")]
[JsonDerivedType(typeof(LookupTerm), "lookup")]
[JsonDerivedType(typeof(PiecewiseTerm), "piecewise")]
[JsonDerivedType(typeof(ConstantTerm), "constant")]
[JsonDerivedType(typeof(ConditionalTerm), "conditional")]
public abstract record ScoreTerm
{
    private protected ScoreTerm() { }

    public ScoreStage ApplyAt { get; init; } = ScoreStage.RawScore;
}

public sealed record RateTerm : ScoreTerm
{
    public required string MetricRef { get; init; }
    public required decimal Rate { get; init; }

    /// <summary>Clamps the metric consumed, not the points produced.</summary>
    public NumberOrParam? Cap { get; init; }

    public CapScope CapScope { get; init; } = CapScope.PerFlight;
}

public sealed record LookupTerm : ScoreTerm
{
    public required string MetricRef { get; init; }

    /// <summary>1..*, ascending, at most one unbounded row and it is last.</summary>
    public required ImmutableArray<LookupRow> Rows { get; init; }
}

public sealed record PiecewiseTerm : ScoreTerm
{
    public required string MetricRef { get; init; }

    /// <summary>Bands are evaluated over (metric − origin); null means 0.</summary>
    public NumberOrParam? Origin { get; init; }

    /// <summary>1..*, cumulative, ordered; adjacent bands must meet.</summary>
    public required ImmutableArray<Band> Bands { get; init; }
}

public sealed record ConstantTerm : ScoreTerm
{
    public required decimal Value { get; init; }
}

public sealed record ConditionalTerm : ScoreTerm
{
    public required Predicate When { get; init; }
    public required ScoreTerm Then { get; init; }

    /// <summary>Absent contributes 0 to the sum.</summary>
    public ScoreTerm? Else { get; init; }
}

// ----------------------------------------------------------------------- tasks

public sealed record TaskDefinition
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required ImmutableArray<MetricDefinition> Metrics { get; init; }
    public required FlightSelection Flights { get; init; }
    public required TaskTiming Timing { get; init; }
    public GroupConstraint? Group { get; init; }
    public Normalisation? Normalise { get; init; }
    public Predicate? ValidWhen { get; init; }
    public Predicate? FlightValidWhen { get; init; }

    /// <summary>Rounding of the raw score, before normalising.</summary>
    public Rounding? RawScore { get; init; }

    /// <summary>Overrides the class default for this task only.</summary>
    public ReflightRule? Reflight { get; init; }

    /// <summary>1..* across both stages; staged by ScoreTerm.ApplyAt.</summary>
    public required ImmutableArray<ScoreTerm> Score { get; init; }
}

public sealed record PhaseDefinition
{
    public required int Ordinal { get; init; }
    public required PhaseType Type { get; init; }
    public RoundComposition Rounds { get; init; } = new();
    public required ValidityRule Validity { get; init; }

    /// <summary>Ordered; the first policy whose gates all hold applies. May be empty.</summary>
    public ImmutableArray<DropPolicy> Drops { get; init; } = [];

    public PromotionRule? Promotion { get; init; }
    public required ImmutableArray<TaskDefinition> Tasks { get; init; }
}

public sealed record ClassDefinition
{
    public required string Name { get; init; }

    /// <summary>Empty for a national class.</summary>
    public string? FaiDesignation { get; init; }

    public required string Version { get; init; }

    /// <summary>Unset means SinglePhase, and is only available to a one-phase class.</summary>
    public FinalRankingKind? FinalRanking { get; init; }

    public ImmutableArray<Parameter> Parameters { get; init; } = [];
    public required ReflightRule Reflight { get; init; }
    public ImmutableArray<PenaltyDefinition> Penalties { get; init; } = [];
    public required ImmutableArray<PhaseDefinition> Phases { get; init; }
}
