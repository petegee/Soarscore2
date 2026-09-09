// The landing tape catalogue — tape-points-landing-seeds.md WI-2 (kanban lane
// moves; the filename is the stable citation).
//
// A tape is an INSTRUMENT WITH A READING SCALE, defined independently of any
// class: an ordered list of marks mapping a reading to a distance band, plus a
// distinguished reading for the beyond-the-tape band where the club has one.
// The class's own rulebook-keyed landing table turns the band into points; the
// system composes the two (WI-1). A tape never awards points and a reading is
// never a distance — decision 6 — so the shape deliberately does NOT reuse
// LookupRow: its `Points` member would name an award where a reading belongs,
// making exactly the confusion this story removes unwritable. WI-1 promotes
// this shape to the Domain with the composition function; WI-2 keeps it here so
// the change is reference data only and no class definition is touched.
//
// Band semantics (story, Implementation boundaries): each mark's UpTo is an
// inclusive upper bound, so mark i's band is (UpTo[i-1], UpTo[i]], the first
// mark's is [0, UpTo[0]], and the beyond-the-tape band is (UpTo[last], inf).
// All marks are bounded by construction — the builder offers no unbounded
// Rest() — and the terminal band is carried by OffTapeReading instead:
//   - populated: that reading (e.g. the club's written 0) denotes the terminal
//     band. It must differ from every mark's reading, or two bands would share
//     one reading and inversion would be ambiguous.
//   - null: the instrument has no reading beyond its last mark (e.g. a 10 m
//     tape and a landing past its end). Such a landing is recorded as a
//     distance naming no instrument — decision 3's base case — never as a
//     fabricated tape reading. Omitting the mark is honest; inventing one is
//     the guess the story forbids.
//
// Integrity checks mirror the class pipeline's split: the TapeMarks builder
// makes a non-ascending or non-positive scale unwritable at authoring (like
// Authoring.cs's Bands/Row lists), and TapeIntegrity.Check re-verifies the
// finished catalogue at emission time (like adoption checks), failing the tool
// red. Inversion needs distinct readings, so a repeated reading is refused at
// authoring, loudly, per decision 2.
//
// Relation to WI-3's consumption shape: src/Soarscore.Domain/Instruments/
// ReadingScale.cs snapshots a scale inside a competition's declaration. The
// catalogue stays nullable where the evidence is silent (the metre tape has no
// beyond-the-tape reading — inventing one would be the guess this story
// forbids), so catalogue-to-snapshot mapping is WI-3/WI-4's to define, not
// this file's to pre-empt. One scale, one transcription: the marks below are
// the only place these numbers live.

using System.Collections.Immutable;

namespace Soarscore.SeedData;

/// <summary>
/// One mark on a tape's scale: every nose-to-spot distance in this mark's band
/// reads <see cref="Reading"/>. UpTo is always populated — the terminal band
/// belongs to <see cref="TapeDefinition.OffTapeReading"/>, never to a mark.
/// </summary>
public sealed record TapeMark(decimal? UpTo, decimal Reading);

/// <summary>
/// One side of the club's landing tape (double-sidedness is equipment, not
/// scoring — each side is its own tape), or a metre-marked instrument such as
/// ALES M's 10 m tape. Bands are in <see cref="Unit"/>, which must match the
/// metric's declared unit wherever the tape is declared (checked at
/// declaration time, WI-3 — never here).
/// </summary>
public sealed record TapeDefinition
{
    public required string Name { get; init; }

    public required string Unit { get; init; }

    /// <summary>1..*, strictly ascending, every UpTo populated.</summary>
    public required ImmutableArray<TapeMark> Marks { get; init; }

    /// <summary>
    /// The distinguished reading denoting the (UpTo[last], inf) band, or null
    /// where the instrument has no beyond-the-tape reading.
    /// </summary>
    public decimal? OffTapeReading { get; init; }
}

/// <summary>
/// The notation's scale block for tapes. Marks ascend; the close names the
/// beyond-the-tape reading or states there is none. Mirrors <see cref="Rows"/>.
/// </summary>
public sealed class TapeMarks
{
    private readonly ImmutableArray<TapeMark>.Builder _marks = ImmutableArray.CreateBuilder<TapeMark>();

    private TapeMarks(decimal upTo, decimal reading)
    {
        if (upTo <= 0)
            throw new InvalidOperationException($"tape-mark: first UpTo must be positive, was {upTo}.");
        _marks.Add(new TapeMark(upTo, reading));
    }

    public static TapeMarks UpTo(decimal upTo, decimal reading) => new(upTo, reading);

    public TapeMarks Then(decimal upTo, decimal reading)
    {
        if (upTo <= 0)
            throw new InvalidOperationException($"tape-mark: UpTo must be positive, was {upTo}.");
        if (upTo <= _marks[^1].UpTo)
            throw new InvalidOperationException(
                $"tape-mark: marks must ascend, was {_marks[^1].UpTo} then {upTo}.");
        _marks.Add(new TapeMark(upTo, reading));
        return this;
    }

    /// <summary>Close with the distinguished beyond-the-tape reading.</summary>
    public ImmutableArray<TapeMark> OffTape(decimal reading) =>
        Close(reading);

    /// <summary>Close stating the instrument has no beyond-the-tape reading.</summary>
    public ImmutableArray<TapeMark> End() =>
        Close(null);

    private ImmutableArray<TapeMark> Close(decimal? offTapeReading)
    {
        var readings = _marks.Select(m => m.Reading).ToArray();
        if (readings.Distinct().Count() != readings.Length)
            throw new InvalidOperationException("tape-mark: readings must be distinct — inversion needs them.");
        if (offTapeReading is not null && readings.Contains(offTapeReading.Value))
            throw new InvalidOperationException(
                $"tape-mark: off-tape reading {offTapeReading} repeats a mark's reading.");
        return _marks.ToImmutable();
    }
}

/// <summary>Emission-time verification for one tape — the adoption-check half.</summary>
public static class TapeIntegrity
{
    public static string[] Check(string fileName, TapeDefinition tape)
    {
        var defects = new List<string>();
        if (string.IsNullOrWhiteSpace(tape.Name))
            defects.Add($"{fileName}: tape has no name.");
        if (string.IsNullOrWhiteSpace(tape.Unit))
            defects.Add($"{fileName}: tape has no unit.");
        if (tape.Marks.IsEmpty)
            defects.Add($"{fileName}: tape has no marks.");
        for (var i = 0; i < tape.Marks.Length; i++)
        {
            var mark = tape.Marks[i];
            if (mark.UpTo is null)
                defects.Add($"{fileName}: mark {i} is unbounded — the terminal band belongs to offTapeReading.");
            else if (mark.UpTo <= 0)
                defects.Add($"{fileName}: mark {i} UpTo must be positive, was {mark.UpTo}.");
            else if (i > 0 && tape.Marks[i - 1].UpTo is not null && mark.UpTo <= tape.Marks[i - 1].UpTo)
                defects.Add($"{fileName}: marks must ascend, was {tape.Marks[i - 1].UpTo} then {mark.UpTo}.");
        }
        var readings = tape.Marks.Select(m => m.Reading).ToArray();
        if (readings.Distinct().Count() != readings.Length)
            defects.Add($"{fileName}: readings must be distinct — inversion needs them.");
        if (tape.OffTapeReading is not null && readings.Contains(tape.OffTapeReading.Value))
            defects.Add($"{fileName}: off-tape reading {tape.OffTapeReading} repeats a mark's reading.");
        return [.. defects];
    }
}
