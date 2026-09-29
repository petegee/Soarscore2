// Generic tape-measure landing scale — the steel-tape/tape-measure instrument
// a contest declares when nose-to-spot is measured with a distance-marked
// tape rather than a points-marked or class-prescribed one
// (kanban/completed/tape-measure-scale.md).
//
// This is NOT Class M's prescribed instrument: SeedTapeNzAlesM10m.cs is the
// metre-marked tape NZ.7.4(c)(i) prescribes ("marked in 1 meter increments"),
// with OffTapeReading deliberately null. This scale is the generic
// centimetre-marked tape any contest may declare, and it DOES carry an
// off-scale reading (see below).
//
// This instrument reads in METRES, not points: mark (n, n) says a nose in
// (n-0.01, n] reads n metres — centimetre marks 0.01 m .. 15.00 m, 1500
// marks, Reading == UpTo throughout. The metre IS the reading, so composing
// with any distance-keyed landing table is the identity, exactly as for
// SeedTapeNzAlesM10m.cs: the class seeds state their tables' distances, not
// a second transcription of them.
//
// Why 15 m: the scale must cover every corpus landing-table boundary — the
// F3J/F3B-side tables run to 15 m, and the ALES N/P 25-point band boundary
// sits at 15 m. A shorter tape would leave those award boundaries past its
// last mark, which TapeComposition refuses as a straddle (ReadingScale.cs:
// TerminalStraddler) rather than approximates.
//
// Off-scale reading 0: an entered 0.0 is not a mark — the first mark is UpTo
// 0.01 / Reading 0.01, above 0 — so it resolves off-scale, denoting
// (15, inf) → 0 points through the existing OffScaleReading machinery, with
// no engine change. The engine zero-branch is declined; this tape is the
// outstanding half (kanban/deferred-decisions.md: the exact-zero convention
// entry — "tape-measure 0.0 via off-scale once the tape-measure scale
// exists").
//
// Loop, not enumeration: the 1500 marks are generated programmatically
// because TapeMarks/TapeIntegrity only need ascending distinct marks —
// ascending is enforced at authoring (TapeMarks.ThenUpTo throws), distinct
// readings at close, and both re-verified at emission (TapeIntegrity.Check).
// 1500 explicit ThenUpTo lines add no safety beyond what those checks
// already give; they add only drift surface.
//
// Composes (identity, Reading == UpTo) with every distance-keyed landing
// lookup — every class boundary in the corpus falls on a centimetre mark
// and the 15 m last mark covers the furthest one, so no band straddles.
// Verified by composing each pairing (1500 marks + the off-scale reading =
// 1501 awards each):
//   - 50-f3j (F3J.10.5), 60-f5l (5.5.12.11.2)
//   - 20-f3b (F3B.2.3 d)
//   - 30-f5j, 85c-nz-f5j-ndc (5.5.11.12 h)
//   - 86-nz-x5j (NZ.7.6(c)(v), table at NZ.4.13)
//   - 80-nz-m-ales200, 81-nz-m-ndc (NZ.7.4(c)(ii), table at NZ.4.13)
//   - 83-nz-n-ales123 (NZ.7.5(f)), 85-nz-p-radian (NZ.7.7(e)(v))
//   - 87-nz-h-thermal-2m (NZ.5.5(f)(i))
// Refuses — a tape band straddling two awards is refused, never approximated
// (decision 2); a metre scale cannot score a unitless metric either:
//   - 40-f5k, 85d-nz-f5k-ndc: lookup over intrinsic flight.sequence — unit
//     mismatch (verified: composing distance rows with a null unit refuses
//     tapeComposition.unitMismatch, as for every metre scale).
// No pairing to compose (no LookupTerm at all):
// 10-f3k, 70-f3f, 85b-nz-f3k-ndc, 90-aggregate.

using System.Collections.Immutable;

namespace Soarscore.SeedData;

public static class SeedTapeMeasure
{
    // The off-scale reading is a single literal: the builder's OffTape() and
    // the stored OffTapeReading below must agree, so both name this const.
    private const decimal OffTape = 0;

    private static ImmutableArray<TapeMark> Marks
    {
        get
        {
            var scale = TapeMarks.UpTo(0.01m, 0.01m);
            for (var cm = 2; cm <= 1500; cm++)
            {
                var metres = cm / 100m;
                scale.ThenUpTo(metres, metres);
            }
            return scale.OffTape(OffTape);
        }
    }

    public static TapeDefinition Definition => new()
    {
        Name = "Tape measure",
        Unit = "m",
        Marks = Marks,
        OffTapeReading = OffTape,
    };
}
