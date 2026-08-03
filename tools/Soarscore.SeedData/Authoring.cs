// Smart constructors — ADR-0002 §2. NOT a fluent builder: these are ordinary
// static factories over the same records, and their whole job is to make a
// handful of adoption checks UNWRITABLE rather than merely checked.
//
//   Bands  — each band's lower bound IS the previous band's upper bound, so
//            check 8 (a gap or an overlap between bands, a silent mis-score) and
//            its F27 corollary (both sides of a join naming the SAME parameter)
//            cannot be expressed.
//   Rows   — at most one unbounded row and it is last (check 9), likewise.
//   P.All  — AllOf's 2..* multiplicity, carried by the signature.
//
// M and Params are plainer: they carry no check, and exist so a `metric` or a
// `param` line in seed-data/*.class transcribes to one line here. That matters
// for ADR-0002 §6's review, which reads each class against the RULE REFS.
//
// Everything else is object initialisers and `with`. `with` IS `like`: C# record
// copy semantics reproduce notation §7.1/§7.2 exactly, including the edge case
// that a restated block's omitted keyword takes the DEFAULT rather than the
// parent's value — which is what constructing a fresh value object and leaving a
// member unset does.

using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.SeedData;

// ------------------------------------------------------------------ band lists

/// <summary>
/// The notation's band block. Bands are cumulative and adjacent bands must meet;
/// this list carries the join, so no writer states a lower bound at all.
/// </summary>
public sealed class Bands
{
    private readonly ImmutableArray<Band>.Builder _bands = ImmutableArray.CreateBuilder<Band>();
    private NumberOrParam? _cursor;

    private Bands(NumberOrParam? start) => _cursor = start;

    /// <summary>The notation's <c>any..&lt;to&gt;</c> — a first band unbounded below.</summary>
    public static Bands Below(NumberOrParam to, decimal rate)
    {
        var list = new Bands(null);
        list._bands.Add(new Band(null, to, rate));
        list._cursor = to;
        return list;
    }

    /// <summary>Start the list at a bound, with no band below it.</summary>
    public static Bands From(NumberOrParam from) => new(from);

    public Bands UpTo(NumberOrParam to, decimal rate)
    {
        _bands.Add(new Band(_cursor, to, rate));
        _cursor = to;
        return this;
    }

    /// <summary>The notation's <c>&lt;b&gt;..any</c> — closes the list, unbounded above.</summary>
    public ImmutableArray<Band> Rest(decimal rate)
    {
        _bands.Add(new Band(_cursor, null, rate));
        return _bands.ToImmutable();
    }
}

// ------------------------------------------------------------------- row lists

/// <summary>The notation's lookup block. Rows ascend; only the last may be unbounded.</summary>
public sealed class Rows
{
    private readonly ImmutableArray<LookupRow>.Builder _rows = ImmutableArray.CreateBuilder<LookupRow>();

    private Rows() { }

    public static Rows UpTo(decimal upTo, decimal points)
    {
        var list = new Rows();
        list._rows.Add(new LookupRow(upTo, points));
        return list;
    }

    public Rows Then(decimal upTo, decimal points)
    {
        _rows.Add(new LookupRow(upTo, points));
        return this;
    }

    /// <summary>The notation's <c>any -&gt; &lt;pts&gt;</c> — the unbounded final row.</summary>
    public ImmutableArray<LookupRow> Rest(decimal points)
    {
        _rows.Add(new LookupRow(null, points));
        return _rows.ToImmutable();
    }

    /// <summary>Close the list with a bounded final row.</summary>
    public ImmutableArray<LookupRow> End() => _rows.ToImmutable();
}

// --------------------------------------------------------------------- metrics

/// <summary>One <c>metric</c> line of notation §5, one call.</summary>
public static class M
{
    public static MetricDefinition Number(
        string name, string unit, RoundingMode mode, decimal precision, bool declared = false) => new()
    {
        Name = name,
        Kind = MeasuredKind.Number,
        Unit = unit,
        Precision = new Rounding(mode, precision),
        DeclaredBeforeLaunch = declared,
    };

    public static MetricDefinition Flag(string name) => new()
    {
        Name = name,
        Kind = MeasuredKind.Flag,
    };
}

// ------------------------------------------------------------------ parameters

/// <summary>One <c>param</c> line of notation §3, one call. A null default is `no default` (F12).</summary>
public static class Params
{
    public static Parameter Number(
        string name,
        string? unit = null,
        decimal? @default = null,
        decimal[]? allowed = null,
        ParameterBindingPoint boundAt = ParameterBindingPoint.CompetitionSetup) => new()
    {
        Name = name,
        Kind = MeasuredKind.Number,
        Unit = unit,
        DefaultValue = @default is null ? null : MeasuredValue.Of(@default.Value),
        AllowedValues = allowed is null ? [] : [.. allowed.Select(MeasuredValue.Of)],
        BoundAt = boundAt,
    };

    /// <summary>A Flag parameter has no unit to state; all four in the corpus are carryPenalties.</summary>
    public static Parameter Flag(
        string name,
        bool? @default = null,
        ParameterBindingPoint boundAt = ParameterBindingPoint.CompetitionSetup) => new()
    {
        Name = name,
        Kind = MeasuredKind.Flag,
        DefaultValue = @default is null ? null : MeasuredValue.Of(@default.Value),
        BoundAt = boundAt,
    };
}

// ------------------------------------------------------------------ predicates

/// <summary>The notation's predicate grammar: a comparison, or <c>all(…)</c>.</summary>
public static class P
{
    public static Comparison Lt(string metric, decimal value) => Cmp(metric, Comparator.LessThan, value);

    public static Comparison Le(string metric, decimal value) => Cmp(metric, Comparator.LessOrEqual, value);

    public static Comparison Gt(string metric, decimal value) => Cmp(metric, Comparator.GreaterThan, value);

    public static Comparison Ge(string metric, decimal value) => Cmp(metric, Comparator.GreaterOrEqual, value);

    public static Comparison Eq(string metric, decimal value) => Cmp(metric, Comparator.EqualTo, value);

    /// <summary>A flag comparison — <c>&lt;metric&gt; == true|false</c>.</summary>
    public static Comparison Is(string metric, bool value) => new()
    {
        LeftMetricRef = metric,
        Op = Comparator.EqualTo,
        RightValue = MeasuredValue.Of(value),
    };

    /// <summary>A comparison whose right-hand side is another metric.</summary>
    public static Comparison Ge(string metric, string otherMetric) => new()
    {
        LeftMetricRef = metric,
        Op = Comparator.GreaterOrEqual,
        RightMetricRef = otherMetric,
    };

    /// <summary>AllOf is 2..*; the signature is what says so.</summary>
    public static AllOf All(Predicate first, Predicate second, params Predicate[] rest) =>
        new() { Children = [first, second, .. rest] };

    private static Comparison Cmp(string metric, Comparator op, decimal value) => new()
    {
        LeftMetricRef = metric,
        Op = op,
        RightValue = MeasuredValue.Of(value),
    };
}

// ----------------------------------------------------------------- score terms

/// <summary>The notation's five score terms, one call each.</summary>
public static class T
{
    public static RateTerm Rate(
        string metric, decimal rate, NumberOrParam? cap = null, CapScope capScope = CapScope.PerFlight) =>
        new() { MetricRef = metric, Rate = rate, Cap = cap, CapScope = capScope };

    public static ConstantTerm Constant(decimal value) => new() { Value = value };

    public static LookupTerm Lookup(string metric, ImmutableArray<LookupRow> rows) =>
        new() { MetricRef = metric, Rows = rows };

    public static PiecewiseTerm Piecewise(
        string metric, ImmutableArray<Band> bands, NumberOrParam? origin = null) =>
        new() { MetricRef = metric, Origin = origin, Bands = bands };

    /// <summary>An omitted <c>else</c> contributes 0 to the sum (notation §7.2).</summary>
    public static ConditionalTerm When(Predicate when, ScoreTerm then, ScoreTerm? otherwise = null) =>
        new() { When = when, Then = then, Else = otherwise };
}

// ------------------------------------------------------------------ intrinsics

public static class Intrinsic
{
    /// <summary>
    /// The one intrinsic flight fact (F6): which launch this flight was, 1-based.
    /// Required by 5.5.10.2 — F5K Task B selects only the last flight, so the
    /// cost of the earlier launches can be read nowhere else.
    /// </summary>
    public const string FlightSequence = "flight.sequence";
}
