// NZ F3B-side landing tape — the F3B-graduated side of the club's standard
// double-sided tape.
//
// Scale clause: F3B.2.3 d (docs/rules/f3b.md:45-56), the rulebook landing
// table, READ BACKWARDS — 100 <- [0, 1], 95 <- (1, 2], ... 30 <- (14, 15] —
// exactly as the F3J side is NZ.2.4.4 read backwards. The inversion is
// well-defined because the table's awards are strictly decreasing.
//
// Evidence (WI-0 question b, resolved 2026-09-09 — do not re-open without new
// evidence): the owner asserts the physical F3B side is printed in points,
// and GliderScore's "F3B Enter Points" table (gliderscore/f3b-enter-points.png)
// corroborates both halves of the model:
//   - its identity rows {30, 35 ... 100} are exactly F3B.2.3 d's award set,
//     consistent with a side printed in those points; and
//   - its extra 91-94 / 96-99 rows are the F3J side pre-composed (verified
//     row-for-row by TapeCompositionTests), which is why the owner confirms
//     that at an F3B competition using this table, a scorer reading the F3J
//     side still scores correctly — one fused table serves both sides, and
//     the system models the two facts (scale + award table) instead.
// Authority is the clause plus the owner assertion; the fused table is a
// verification target, never a transcription source.
//
// Off-tape reading 0: the club writes 0 for a landing past the tape's 15 m end
// (story decision 8 — off-the-tape, 0 m and no measurement are three distinct
// facts). It denotes (15, inf), never the spot.
//
// Composes (boundaries {1 ... 15} refine each table's):
//   - identity: 20-f3b (F3B.2.3 d)
//   - 30-f5j, 85c-nz-f5j-ndc (5.5.11.12 h); 86-nz-x5j (NZ.2.4.5);
//     80-nz-m-ales200, 81-nz-m-ndc (NZ.3.12.2 b, table at NZ.2.4.5) — {1 ... 10}
//   - 83-nz-n-ales123 (NZ.3.13.1 e), 85-nz-p-radian (NZ.3.15.1 e) — {7, 15}
// Refuses, loudly (a reading of 95 here means (1, 2], which F3J splits five
// ways — that side of the tape cannot score this class):
// 50-f3j (F3J.10.5), 60-f5l (5.5.12.11.2) — straddledBand.
// Refuses (unit mismatch — a metre scale cannot score a unitless metric):
// 40-f5k, 85d-nz-f5k-ndc (lookup over intrinsic flight.sequence).
// No pairing to compose (no LookupTerm at all):
// 10-f3k, 70-f3f, 85b-nz-f3k-ndc, 90-aggregate.

using System.Collections.Immutable;

namespace Soarscore.SeedData;

public static class SeedTapeNzF3BSide
{
    // F3B.2.3 d read backwards — cf. SeedF3B.LandingRows, the same fifteen
    // boundaries. One rulebook table, one scale: transcribing the fused
    // GliderScore table instead would re-enact the F22/F24 failure shape
    // (SeedF3J.cs:36-41) this catalogue exists to avoid.
    // The off-tape reading is a single literal: the builder's OffTape() and the
    // stored OffTapeReading below must agree, so both name this const.
    private const decimal OffTape = 0;

    private static ImmutableArray<TapeMark> Marks =>
        TapeMarks
            .UpTo(1m, 100)
            .Then(2m, 95)
            .Then(3m, 90)
            .Then(4m, 85)
            .Then(5m, 80)
            .Then(6m, 75)
            .Then(7m, 70)
            .Then(8m, 65)
            .Then(9m, 60)
            .Then(10m, 55)
            .Then(11m, 50)
            .Then(12m, 45)
            .Then(13m, 40)
            .Then(14m, 35)
            .Then(15m, 30)
            .OffTape(OffTape);

    public static TapeDefinition Definition => new()
    {
        Name = "NZ F3B side",
        Unit = "m",
        Marks = Marks,
        OffTapeReading = OffTape,
    };
}
