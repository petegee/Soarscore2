// ParameterRef — docs/soaring-domain-class-diagram.md §2, notation §3.
//
// A ParameterRef may stand in for a literal in exactly THIRTEEN slots and
// nowhere else. Twelve are numeric and one is not, which is why there are two
// unions rather than one:
//
//   TaskTiming.workingTime          TaskTiming.maxLaunches
//   RateTerm.cap                    PiecewiseTerm.origin
//   Band.from                       Band.to                       (F27)
//   GroupConstraint.minPerGroup     ValidityRule.minRounds
//   PromotionRule.topN              PromotionRule.minGroupSize
//   PromotionRule.maxGroupSize      ReflightRule.minNewGroupSize
//   PromotionRule.carryPenalties                                  <- FlagOrParam
//
// Typing exactly those thirteen slots as NumberOrParam / FlagOrParam and every
// other numeric slot as decimal makes adoption check 4 — "a ParameterRef occurs
// only in the thirteen slots that permit one" — UNREPRESENTABLE rather than
// checked (ADR-0002 §2). The check stays in the inventory because the JSON path
// is untrusted and a document naming a parameter in a decimal slot is a type
// error there, not here.

namespace Soarscore.Domain.CompetitionClasses;

// NOTE (LADR-0003, spike finding 1). Neither union carries [JsonPolymorphic],
// and neither can: System.Text.Json throws NotSupportedException at
// configuration time the moment a type declares polymorphism metadata AND a
// JsonConverter<T> is registered for it. The converters win here because they
// collapse thirteen slots of {"kind":"literal","value":599} to 599, which is
// 9-19% of the artefact and rather more of its readability.

/// <summary>A number, or a reference to a declared <see cref="Parameter"/>.</summary>
public abstract record NumberOrParam
{
    private protected NumberOrParam() { }

    public sealed record Literal(decimal Value) : NumberOrParam;

    public sealed record Ref(string ParameterName) : NumberOrParam;

    public static implicit operator NumberOrParam(decimal v) => new Literal(v);

    public static implicit operator NumberOrParam(int v) => new Literal(v);

    /// <summary>The notation's <c>param(&lt;name&gt;)</c>.</summary>
    public static NumberOrParam Param(string name) => new Ref(name);

    public static NumberOrParam FromDecimal(decimal v) => new Literal(v);

    public static NumberOrParam FromInt32(int v) => new Literal(v);
}

/// <summary>A flag, or a reference to a declared <see cref="Parameter"/>.</summary>
public abstract record FlagOrParam
{
    private protected FlagOrParam() { }

    public sealed record Literal(bool Value) : FlagOrParam;

    public sealed record Ref(string ParameterName) : FlagOrParam;

    public static implicit operator FlagOrParam(bool v) => new Literal(v);

    /// <summary>The notation's <c>param(&lt;name&gt;)</c>.</summary>
    public static FlagOrParam Param(string name) => new Ref(name);

    public static FlagOrParam FromBoolean(bool v) => new Literal(v);
}
