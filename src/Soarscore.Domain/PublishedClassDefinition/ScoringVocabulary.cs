// The scoring vocabulary — docs/soaring-domain-class-diagram.md §3.
//
// This is the closed vocabulary NFR-2 refers to. No subtype is named for a
// discipline concept: a landing table is a LookupTerm over a distance metric, a
// launch-height penalty a PiecewiseTerm over a height metric. Grep this file for
// `landing`, `height`, `motor` or `lap` and there is nothing to find.
//
// The three hierarchies are closed with a `private protected` constructor, which
// keeps them un-extendable outside this assembly and makes a `switch` over the
// subtypes give a missing-arm warning. Discriminators are hand-written and the
// discriminator property is `$kind`, not `kind` (LADR-0003 / spike finding 3):
// a discriminator that shadows a real property emits BOTH keys with no error,
// no warning and no build failure, and fails only on read back — after the
// definition has been hashed and stored.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Soarscore.Domain.PublishedClassDefinition;

// --------------------------------------------------------------- measurements

/// <summary>A number or a flag; the rules require plain observations as well as quantities.</summary>
public sealed record MeasuredValue
{
    public required MeasuredKind Kind { get; init; }

    public decimal? Number { get; init; }

    public bool? Flag { get; init; }

    public static MeasuredValue Of(decimal n) => new() { Kind = MeasuredKind.Number, Number = n };

    public static MeasuredValue Of(bool f) => new() { Kind = MeasuredKind.Flag, Flag = f };
}

public sealed record Rounding(RoundingMode Mode, decimal Precision);

public sealed record MetricDefinition
{
    public required string Name { get; init; }

    public required MeasuredKind Kind { get; init; }

    public string? Unit { get; init; }

    /// <summary>A value the pilot nominates BEFORE releasing — a Poker target.</summary>
    public bool DeclaredBeforeLaunch { get; init; }

    /// <summary>Capture precision, 0..1: a Flag metric has nothing to round.</summary>
    public Rounding? Precision { get; init; }
}

// ------------------------------------------------------------ flight selection

/// <summary>
/// Five kinds, and the kind IS the type. Fourteen of the corpus's thirty
/// selections are `last` or `all` and carry no operand at all.
/// </summary>
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

    /// <summary>
    /// Null ranks the candidates by SCORE (F3K.11.5, Poker: an achieved target
    /// credits the target, so score is the only ordering that means anything).
    /// Set, it ranks by that metric's raw value — F3K.11.8 assigns targets to
    /// the four longest FLIGHTS, and no flight has a score until a target has
    /// been assigned to it, so ranking by score there is circular (F16).
    /// </summary>
    public string? RankByMetric { get; init; }

    public TargetAssignment Targets { get; init; } = TargetAssignment.None;

    /// <summary>
    /// In the UNITS OF THE METRIC the score term consumes, not points. Each
    /// selected flight's metric is clamped to its assigned target.
    /// <para>
    /// Under <see cref="TargetAssignment.AnyOrder"/> the pairing is by RANK, not
    /// by index: the longest flight takes the LARGEST target and so down
    /// (notation §5). The list is written ascending while the ranking descends,
    /// so pairing rank <c>i</c> with <c>TargetValues[i]</c> — which is what
    /// <see cref="ExactlyNInOrder"/> does, and the natural reading of an
    /// index-aligned array — scores F3K Task H's worked example at 333 where
    /// `F3K.11.8` states 569.
    /// </para>
    /// </summary>
    public ImmutableArray<decimal> TargetValues { get; init; } = [];
}

public sealed record ExactlyNInOrder : FlightSelection
{
    public required int Count { get; init; }

    /// <summary>
    /// Can only ever be InOrder, since the subtype's name is that statement. It
    /// stays because the notation writes `targets inOrder` and rule 1 requires
    /// the operand to name a model element.
    /// </summary>
    public TargetAssignment Targets { get; init; } = TargetAssignment.InOrder;

    public ImmutableArray<decimal> TargetValues { get; init; } = [];
}

// ------------------------------------------------------------------ predicates

/// <summary>
/// Two subtypes, so "exactly one of {leaf comparison, allOf} is populated" is
/// unrepresentable rather than checked at adoption. There is still no anyOf:
/// every multi-condition site in the eleven definitions is a conjunction, and
/// disjunction is readmitted with the first rule that cites it.
/// </summary>
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
    /// <summary>2..* — a one-element conjunction is a wrapper around its own child.</summary>
    public required ImmutableArray<Predicate> Children { get; init; }
}

// ---------------------------------------------------------------- score terms

/// <summary>
/// Bands are CUMULATIVE and are evaluated over (metric − PiecewiseTerm.origin):
/// 1 pt/s to 600 s then −1 pt/s scores 599 at 601 s. From/To null are the
/// notation's `any` — unbounded below / above. Both accept a ParameterRef (F27).
/// </summary>
public sealed record Band(NumberOrParam? From, NumberOrParam? To, decimal RatePerUnit);

/// <summary>UpTo null is unbounded (F9); legal only on the last row.</summary>
public sealed record LookupRow(decimal? UpTo, decimal Points);

/// <summary>
/// Five kinds with disjoint payloads. The base is ATTRIBUTE-FREE: the stage a
/// term lands at is a property of the LIST that holds it, not of the term — see
/// TaskDefinition's two term lists. It was an `applyAt` on this class, and a
/// nested then/else branch then carried a stage it could not vary independently
/// of its parent (40 of F5K's 84 sites).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(RateTerm), "rate")]
[JsonDerivedType(typeof(LookupTerm), "lookup")]
[JsonDerivedType(typeof(PiecewiseTerm), "piecewise")]
[JsonDerivedType(typeof(ConstantTerm), "constant")]
[JsonDerivedType(typeof(ConditionalTerm), "conditional")]
public abstract record ScoreTerm
{
    private protected ScoreTerm() { }
}

public sealed record RateTerm : ScoreTerm
{
    public required string MetricRef { get; init; }

    public required decimal Rate { get; init; }

    /// <summary>Clamps the METRIC consumed, not the points produced.</summary>
    public NumberOrParam? Cap { get; init; }

    /// <summary>PerTask clamps this term's contributions summed across the selected flights (F4a).</summary>
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

    /// <summary>Null means 0. Bands are evaluated over (metric − origin) (F5).</summary>
    public NumberOrParam? Origin { get; init; }

    /// <summary>1..*, cumulative, ordered; adjacent bands must meet.</summary>
    public required ImmutableArray<Band> Bands { get; init; }
}

public sealed record ConstantTerm : ScoreTerm
{
    /// <summary>Signed: a negative constant is a flat derived deduction (F3J's −30 overfly).</summary>
    public required decimal Value { get; init; }
}

public sealed record ConditionalTerm : ScoreTerm
{
    public required Predicate When { get; init; }

    public required ScoreTerm Then { get; init; }

    /// <summary>Absent contributes 0 to the sum. Three F5K terms need a real one.</summary>
    public ScoreTerm? Else { get; init; }
}

// --------------------------------------------------------------- task operands

public sealed record TaskTiming
{
    public required WorkingTimeKind Kind { get; init; }

    /// <summary>
    /// Populated if and only if Kind is Fixed. Under UntilAllFlightsComplete the
    /// working time is not a class datum at all — the round ends when the last
    /// flight does. Seconds.
    /// </summary>
    public NumberOrParam? WorkingTime { get; init; }

    /// <summary>Seconds.</summary>
    public NumberOrParam? PreparationTime { get; init; }

    /// <summary>Unset means the task limits launches not at all — half the corpus.</summary>
    public NumberOrParam? MaxLaunches { get; init; }
}

/// <summary>
/// Optional on a Task, and ABSENT IS NOT THE SAME STATEMENT AS a parameterised
/// minPerGroup: absent says the class does not GROUP-SCORE at all, where a
/// ParameterRef says it does and only the size is open. `minPerGroup 1` is a
/// fabricated rule and also tells the draw a group of one is an acceptable split.
/// </summary>
public sealed record GroupConstraint
{
    public required NumberOrParam MinPerGroup { get; init; }

    /// <summary>Unset means no group is annulled for want of valid results.</summary>
    public int? MinValidResults { get; init; }
}

/// <summary>
/// Optional on a Task (F25). Absent means the task does not normalise at all:
/// the raw score IS the task result and rounds aggregate raw points. There is no
/// normalisation that leaves scores unchanged, so absence is the only truthful
/// encoding of a class that does not normalise.
/// </summary>
public sealed record Normalisation
{
    public required NormalisationDirection Direction { get; init; }

    public required int WinnerScore { get; init; }

    /// <summary>0..1 (F12): F3B, F5J and F5L state no normalised precision.</summary>
    public Rounding? Round { get; init; }
}
