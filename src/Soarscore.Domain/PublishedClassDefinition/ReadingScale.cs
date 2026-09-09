// The reading scale — kanban/backlog/tape-points-landing-seeds.md WI-1.
//
// A reading scale is a declared instrument's scale: an ordered set of marks,
// each mapping a reading to a distance band, plus a distinguished off-scale
// reading for the terminal band beyond the last mark. A class definition's
// LookupTerm stays the rulebook-keyed award table it is; TapeComposition
// derives the reading-to-award pairing from the two, and refuses rather than
// guesses wherever a tape band straddles two awards.
//
// Generic, definition-driven core: this file names no discipline, no class and
// no catalogue scheme. Band semantics are stated once, here: mark i's band is
// (UpTo[i-1], UpTo[i]], the first mark's is [0, UpTo[0]], and the terminal
// band carrying the off-scale reading is (UpTo[last], inf) — the same
// inclusive-upper-bound reading the award table itself uses.

using System.Collections.Immutable;

namespace Soarscore.Domain.PublishedClassDefinition;

/// <summary>
/// One mark on a reading scale: a distance reading up to <see cref="UpTo"/>
/// (in the scale's <see cref="ReadingScale.Unit"/>) is recorded as
/// <see cref="Reading"/>. A reading is a scale position, not a distance and
/// not an award: it identifies a band, never a point.
/// </summary>
public sealed record ScaleMark(decimal UpTo, decimal Reading);

/// <summary>
/// An instrument's declared reading scale: the marks in ascending distance
/// order plus the distinguished reading recorded when the observation falls
/// beyond the last mark. The marks' bands partition [0, UpTo[last]]; the
/// off-scale reading owns (UpTo[last], inf).
/// </summary>
public sealed record ReadingScale
{
    /// <summary>The unit the marks' bands are stated in. Must equal the
    /// metric's declared unit at composition time.</summary>
    public required string Unit { get; init; }

    /// <summary>1..*, strictly ascending <see cref="ScaleMark.UpTo"/>.</summary>
    public required ImmutableArray<ScaleMark> Marks { get; init; }

    /// <summary>
    /// The reading recorded beyond the last mark, or null where the
    /// instrument has no beyond-the-tape reading (WI-3: the catalogue stays
    /// nullable where the evidence is silent — inventing a reading would be
    /// the guess the story forbids). Must differ from every mark's reading
    /// when populated. A null leaves the terminal band with no reading: a
    /// landing there is recorded as a distance naming no instrument, and a
    /// class boundary past the last mark is a loud straddle refusal, never an
    /// invented mark.
    /// </summary>
    public decimal? OffScaleReading { get; init; }

    /// <summary>
    /// Every reading the instrument can produce: each mark's plus the
    /// off-scale reading when it has one. Capture validates a reading as an
    /// exact member of this set (WI-3) — never rounded into it.
    /// </summary>
    public ImmutableArray<decimal> ReadingSet =>
        OffScaleReading is { } offScale
            ? [.. Marks.Select(m => m.Reading), offScale]
            : [.. Marks.Select(m => m.Reading)];

    /// <summary>Exact membership in <see cref="ReadingSet"/>.</summary>
    public bool ContainsReading(decimal reading) =>
        OffScaleReading == reading || Marks.Any(m => m.Reading == reading);
}

/// <summary>
/// One composed row: the reading, the distance band it denotes
/// (<see cref="LowerBound"/> exclusive except 0, <see cref="UpperBound"/>
/// inclusive, null for the unbounded terminal band) and the award the class
/// table gives every distance in that band.
/// </summary>
public sealed record ReadingAward(decimal Reading, decimal LowerBound, decimal? UpperBound, decimal Points);

/// <summary>
/// A tape fully composed with an award table: one <see cref="ReadingAward"/>
/// per reading in the tape's set, including the off-scale reading. Produced
/// by <see cref="TapeComposition.Compose"/>; that check is the
/// declaration-time gate, and <see cref="Resolve"/> is the resolution-time
/// read-off against the same derived rows — one source of truth.
/// </summary>
public sealed record ComposedReadingScale
{
    public required ReadingScale Tape { get; init; }

    public required ImmutableArray<ReadingAward> Awards { get; init; }

    /// <summary>
    /// Read one award off the composed rows. Refuses a reading the tape has
    /// no mark for — never a guess, never a distance fallback.
    /// </summary>
    public Result<decimal> Resolve(decimal reading)
    {
        foreach (var award in Awards)
        {
            if (award.Reading == reading)
                return Result<decimal>.Success(award.Points);
        }

        return Result<decimal>.Failure(
            "tapeComposition.unknownReading",
            $"Reading {reading} is not on this scale's reading set.");
    }
}

/// <summary>
/// Reading-to-band inversion plus band-to-award composition as a pure
/// function of a tape and an award table's rows. Total and non-throwing:
/// every refusal is a named <see cref="Result{T}"/> failure, never an
/// exception and never an approximation.
/// </summary>
public static class TapeComposition
{
    /// <summary>
    /// Compose every reading in the tape's set with the award table. Refuses
    /// a malformed scale, a unit mismatch and any band straddling two awards.
    /// This is the declaration-time gate: a refused pairing must never reach
    /// scoring in any form.
    /// </summary>
    /// <param name="tape">The declared instrument's reading scale.</param>
    /// <param name="metricUnit">The metric's declared unit (<c>null</c> for a
    /// unitless metric).</param>
    /// <param name="awardRows">The award table's rows, satisfying the
    /// LookupTerm adoption shape (ascending, at most one unbounded row and it
    /// last) and covering the tape's full range including the terminal band —
    /// a table ending bounded leaves the terminal band undefined and the
    /// pairing is refused.</param>
    public static Result<ComposedReadingScale> Compose(
        ReadingScale tape,
        string? metricUnit,
        ImmutableArray<LookupRow> awardRows)
    {
        if (tape.Marks.IsDefaultOrEmpty)
        {
            return Result<ComposedReadingScale>.Failure(
                "tapeComposition.tapeNotMonotone",
                "A reading scale carries at least one mark.");
        }

        for (var i = 0; i < tape.Marks.Length; i++)
        {
            var bound = tape.Marks[i].UpTo;
            var previous = i == 0 ? 0m : tape.Marks[i - 1].UpTo;
            if (bound <= 0m || bound <= previous)
            {
                return Result<ComposedReadingScale>.Failure(
                    "tapeComposition.tapeNotMonotone",
                    $"Mark {i} (up to {bound}) does not advance the scale: " +
                    "marks ascend strictly from 0.");
            }
        }

        var readings = new HashSet<decimal>();
        foreach (var mark in tape.Marks)
        {
            if (!readings.Add(mark.Reading))
            {
                return Result<ComposedReadingScale>.Failure(
                    "tapeComposition.repeatedReading",
                    $"Reading {mark.Reading} appears on more than one mark: " +
                    "inversion needs every reading to denote exactly one band.");
            }
        }

        if (tape.OffScaleReading is { } offScale && !readings.Add(offScale))
        {
            return Result<ComposedReadingScale>.Failure(
                "tapeComposition.repeatedReading",
                $"The off-scale reading {offScale} collides with a mark's reading: " +
                "the terminal band needs its own distinguished reading.");
        }

        if (!string.Equals(tape.Unit, metricUnit, StringComparison.Ordinal))
        {
            return Result<ComposedReadingScale>.Failure(
                "tapeComposition.unitMismatch",
                $"The scale reads in '{tape.Unit}' but the metric declares " +
                $"'{metricUnit ?? "<no unit>"}'.");
        }

        var awards = ImmutableArray.CreateBuilder<ReadingAward>();
        for (var i = 0; i < tape.Marks.Length; i++)
        {
            var mark = tape.Marks[i];
            var lower = i == 0 ? 0m : tape.Marks[i - 1].UpTo;
            var composed = ComposeBand(mark.Reading, lower, mark.UpTo, awardRows);
            if (composed.IsFailure)
                return Result<ComposedReadingScale>.Failure(composed.Code!, composed.Message!);
            awards.Add(composed.Value);
        }

        var lastUpTo = tape.Marks[^1].UpTo;
        if (tape.OffScaleReading is { } offScaleReading)
        {
            var terminal = ComposeBand(offScaleReading, lastUpTo, null, awardRows);
            if (terminal.IsFailure)
                return Result<ComposedReadingScale>.Failure(terminal.Code!, terminal.Message!);
            awards.Add(terminal.Value);
        }
        else
        {
            // No reading denotes the terminal band: a landing there is a
            // distance naming no instrument (WI-3), so the composed rows
            // simply end at the last mark. But an award boundary past the
            // last mark still straddles — a band no mark can read — and is
            // refused loudly rather than left to resolve nothing.
            var beyond = TerminalStraddler(lastUpTo, awardRows);
            if (beyond is { } refusal)
                return Result<ComposedReadingScale>.Failure(refusal.Code!, refusal.Message!);
        }

        return Result<ComposedReadingScale>.Success(new ComposedReadingScale
        {
            Tape = tape,
            Awards = awards.ToImmutable(),
        });
    }

    /// <summary>Convenience over <see cref="LookupTerm"/> and
    /// <see cref="MetricDefinition"/>: the unit is the metric's declared one,
    /// the rows the term's own.</summary>
    public static Result<ComposedReadingScale> Compose(
        ReadingScale tape,
        MetricDefinition metric,
        LookupTerm awardTable) =>
        Compose(tape, metric.Unit, awardTable.Rows);

    /// <summary>
    /// Resolve one reading through the pairing. Composes first, so a
    /// straddled pairing is refused here exactly as at declaration — one
    /// source of truth, not a second implementation.
    /// </summary>
    public static Result<decimal> Resolve(
        ReadingScale tape,
        string? metricUnit,
        ImmutableArray<LookupRow> awardRows,
        decimal reading) =>
        Compose(tape, metricUnit, awardRows).Match<Result<decimal>>(
            onSuccess: composed => composed.Resolve(reading),
            onFailure: failure => Result<decimal>.Failure(failure.Code!, failure.Message!));

    /// <summary>Convenience over <see cref="LookupTerm"/> and
    /// <see cref="MetricDefinition"/>.</summary>
    public static Result<decimal> Resolve(
        ReadingScale tape,
        MetricDefinition metric,
        LookupTerm awardTable,
        decimal reading) =>
        Resolve(tape, metric.Unit, awardTable.Rows, reading);

    // ------------------------------------------------------------ private

    /// <summary>
    /// The refusal for an award boundary past the last mark of an instrument
    /// with no off-scale reading: no mark covers the terminal band, so the
    /// boundary's two awards are straddled unreadably. Null when no boundary
    /// lies past the last mark — then the terminal band holds no boundary and
    /// the pairing composes, with composed rows ending at the last mark.
    /// </summary>
    private static Result<ReadingAward>? TerminalStraddler(decimal lastUpTo, ImmutableArray<LookupRow> awardRows)
    {
        decimal? hit = null;
        foreach (var row in awardRows)
        {
            if (row.UpTo is { } boundary && boundary > lastUpTo && (hit is null || boundary < hit))
                hit = boundary;
        }

        if (hit is not { } boundaryHit)
            return null;

        var owner = -1;
        for (var j = 0; j < awardRows.Length; j++)
        {
            if (awardRows[j].UpTo is null || boundaryHit <= awardRows[j].UpTo!.Value)
            {
                owner = j;
                break;
            }
        }

        var below = owner >= 0 ? awardRows[owner].Points : awardRows[^1].Points;
        var above = owner >= 0 && owner + 1 < awardRows.Length
            ? awardRows[owner + 1].Points.ToString()
            : "no award — beyond the award table";
        return Result<ReadingAward>.Failure(
            "tapeComposition.straddledBand",
            $"No mark on this scale covers ({lastUpTo}, inf), which straddles " +
            $"the award boundary at {boundaryHit} ({below} vs {above}).");
    }

    /// <summary>
    /// Compose one band: refuse when a bounded award boundary falls strictly
    /// inside it (the band straddles two awards) or when the award table does
    /// not cover it at all; otherwise the award the table gives the band.
    /// <paramref name="upper"/> null is the unbounded terminal band.
    /// </summary>
    private static Result<ReadingAward> ComposeBand(
        decimal reading,
        decimal lower,
        decimal? upper,
        ImmutableArray<LookupRow> awardRows)
    {
        decimal? straddler = null;
        foreach (var row in awardRows)
        {
            if (row.UpTo is not { } boundary)
                continue;
            if (upper is { } u ? lower < boundary && boundary < u : lower < boundary)
            {
                if (straddler is null || boundary < straddler)
                    straddler = boundary;
            }
        }

        if (straddler is { } hit)
        {
            // The awards either side of the straddled boundary: the row owning
            // the boundary itself, then the row above it.
            var owner = -1;
            for (var j = 0; j < awardRows.Length; j++)
            {
                if (awardRows[j].UpTo is null || hit <= awardRows[j].UpTo!.Value)
                {
                    owner = j;
                    break;
                }
            }

            var below = owner >= 0 ? awardRows[owner].Points : awardRows[^1].Points;
            var above = owner >= 0 && owner + 1 < awardRows.Length
                ? awardRows[owner + 1].Points.ToString()
                : "no award — beyond the award table";
            return Result<ReadingAward>.Failure(
                "tapeComposition.straddledBand",
                $"Reading {reading} denotes {BandText(lower, upper)}, which straddles " +
                $"the award boundary at {hit} ({below} vs {above}).");
        }

        var award = AwardAt(awardRows, upper);
        if (award is null)
        {
            return Result<ReadingAward>.Failure(
                "tapeComposition.straddledBand",
                $"Reading {reading} denotes {BandText(lower, upper)}, which lies beyond " +
                "the award table's last boundary: nothing there awards it.");
        }

        return Result<ReadingAward>.Success(new ReadingAward(reading, lower, upper, award.Value));
    }

    /// <summary>
    /// The award table read at one distance: the first row whose upper bound
    /// covers it. Null when no row does — a bounded-final table past its last
    /// boundary.
    /// </summary>
    private static decimal? AwardAt(ImmutableArray<LookupRow> awardRows, decimal? distance)
    {
        foreach (var row in awardRows)
        {
            if (row.UpTo is null)
                return row.Points;
            if (distance is { } d && d <= row.UpTo.Value)
                return row.Points;
        }

        return null;
    }

    private static string BandText(decimal lower, decimal? upper) =>
        upper is { } u
            ? lower == 0m ? $"[{lower}, {u}]" : $"({lower}, {u}]"
            : $"({lower}, inf)";
}
