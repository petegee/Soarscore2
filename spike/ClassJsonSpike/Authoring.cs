// Smart constructors — ADR-0002 §2. Not a fluent builder: these are ordinary
// static factories over the same records, and their whole job is to make a
// handful of adoption checks unwritable rather than merely checked.
//
//   Bands  — each band's lower bound IS the previous band's upper bound, so
//            check 7 (a gap or an overlap between bands, a silent mis-score)
//            cannot be expressed.
//   Rows   — at most one unbounded row and it is last, likewise.
//   P.All  — AllOf's 2..* multiplicity, carried by the signature.

using System.Collections.Immutable;

namespace Soarscore.Spike.ClassModel;

// ------------------------------------------------------------------ band lists

public sealed class BandList
{
    private readonly ImmutableArray<Band>.Builder _bands = ImmutableArray.CreateBuilder<Band>();
    private NumberOrParam? _cursor;

    private BandList(NumberOrParam? start) => _cursor = start;

    /// <summary>The notation's `any..&lt;to&gt;` — the first band, unbounded below.</summary>
    public static BandList Below(NumberOrParam to, decimal rate)
    {
        var l = new BandList(null);
        l._bands.Add(new Band(null, to, rate));
        l._cursor = to;
        return l;
    }

    /// <summary>Start the list at a bound, with no band below it.</summary>
    public static BandList From(NumberOrParam from) => new(from);

    public BandList UpTo(NumberOrParam to, decimal rate)
    {
        _bands.Add(new Band(_cursor, to, rate));
        _cursor = to;
        return this;
    }

    /// <summary>The notation's `&lt;b&gt;..any` — closes the list, unbounded above.</summary>
    public ImmutableArray<Band> Rest(decimal rate)
    {
        _bands.Add(new Band(_cursor, null, rate));
        return _bands.ToImmutable();
    }
}

// ------------------------------------------------------------------ row lists

public sealed class RowList
{
    private readonly ImmutableArray<LookupRow>.Builder _rows = ImmutableArray.CreateBuilder<LookupRow>();

    public static RowList UpTo(decimal upTo, decimal points)
    {
        var l = new RowList();
        l._rows.Add(new LookupRow(upTo, points));
        return l;
    }

    public RowList Then(decimal upTo, decimal points)
    {
        _rows.Add(new LookupRow(upTo, points));
        return this;
    }

    /// <summary>The notation's `any -&gt; &lt;pts&gt;` — the unbounded final row.</summary>
    public ImmutableArray<LookupRow> Rest(decimal points)
    {
        _rows.Add(new LookupRow(null, points));
        return _rows.ToImmutable();
    }

    /// <summary>Close the list with a bounded final row.</summary>
    public ImmutableArray<LookupRow> End() => _rows.ToImmutable();
}

// ----------------------------------------------------------------- predicates

public static class P
{
    public static Comparison Ge(string metric, decimal value) => Cmp(metric, Comparator.GreaterOrEqual, value);
    public static Comparison Le(string metric, decimal value) => Cmp(metric, Comparator.LessOrEqual, value);
    public static Comparison Gt(string metric, decimal value) => Cmp(metric, Comparator.GreaterThan, value);
    public static Comparison Lt(string metric, decimal value) => Cmp(metric, Comparator.LessThan, value);
    public static Comparison Eq(string metric, decimal value) => Cmp(metric, Comparator.EqualTo, value);

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

// ---------------------------------------------------------------- score terms

public static class T
{
    public static RateTerm Rate(string metric, decimal rate, NumberOrParam? cap = null,
                                CapScope capScope = CapScope.PerFlight) =>
        new() { MetricRef = metric, Rate = rate, Cap = cap, CapScope = capScope };

    public static ConstantTerm Constant(decimal value) => new() { Value = value };

    public static LookupTerm Lookup(string metric, ImmutableArray<LookupRow> rows) =>
        new() { MetricRef = metric, Rows = rows };

    public static PiecewiseTerm Piecewise(string metric, ImmutableArray<Band> bands, NumberOrParam? origin = null) =>
        new() { MetricRef = metric, Origin = origin, Bands = bands };

    public static ConditionalTerm When(Predicate when, ScoreTerm then, ScoreTerm? otherwise = null) =>
        new() { When = when, Then = then, Else = otherwise };
}
