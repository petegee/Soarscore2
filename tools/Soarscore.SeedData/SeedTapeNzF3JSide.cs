// NZ F3J-side landing tape — the F3J-graduated side of the club's standard
// double-sided tape.
//
// Scale clause: NZ.4.12 (docs/rules/nz/source-docs/nzmaa-s5-soaring-2024.md:540-565),
// row-for-row identical to F3J.10.5 (docs/rules/f3j.md:38-55). The marks below
// are that table READ BACKWARDS: each mark's band is the set of distances the
// table awards that mark's reading for — 100 <- [0, 0.2], 99 <- (0.2, 0.4],
// ... 91 <- (1.8, 2.0], 90 <- (2.0, 3], 85 <- (3, 4], ... 30 <- (14, 15].
// The inversion is well-defined because the table's awards are strictly
// decreasing. Authority is the clause; GliderScore's fused tables are never
// cited — they restate this rule and can drift from it.
//
// Off-tape reading 0: the club writes 0 for a landing past the tape's 15 m end
// (story decision 8 — off-the-tape, 0 m and no measurement are three distinct
// facts). It denotes (15, inf), never the spot.
//
// Composes with every distance-keyed landing lookup in the class corpus — the
// tape's boundaries {0.2, 0.4 ... 2.0, 3, 4 ... 15} refine each table's:
//   - identity: 50-f3j (F3J.10.5), 60-f5l (5.5.12.11.2) — {0.2 ... 15}
//   - 20-f3b (F3B.2.3 d) — {1 ... 15}
//   - 30-f5j, 85c-nz-f5j-ndc (5.5.11.12 h); 86-nz-x5j (NZ.4.13);
//     80-nz-m-ales200, 81-nz-m-ndc (NZ.7.4(c)(ii), table at NZ.4.13) — {1 ... 10}
//   - 83-nz-n-ales123 (NZ.7.5(f)), 85-nz-p-radian (NZ.7.7(e)(v)) — {7, 15}
// Refuses (unit mismatch — a metre scale cannot score a unitless metric):
// 40-f5k, 85d-nz-f5k-ndc (lookup over intrinsic flight.sequence).
// No pairing to compose (no LookupTerm at all):
// 10-f3k, 70-f3f, 85b-nz-f3k-ndc, 90-aggregate.
//
// Fixture need: jerilderie-2010 scheme 3 readings (23 marks plus off-tape 0s)
// are this side verbatim, and f5j-christchurch-2019 / f5j-hawkes-bay-trials /
// f5j-nz-south-island scheme 11 is this side composed with 5.5.11.12 h.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedTapeNzF3JSide
{
    // NZ.4.12 read backwards — cf. SeedF3J.LandingRows (F3J.10.5), the same
    // twenty-three boundaries. One rulebook table, one scale: had this been
    // transcribed twice, a drifted mark would still emit, still compose and
    // still produce a plausible number — the F22/F24 failure shape
    // (SeedF3J.cs:36-41) this catalogue exists to avoid repeating.
    // The off-tape reading is a single literal: the builder's OffTape() and the
    // stored OffTapeReading below must agree, so both name this const.
    private const decimal OffTape = 0;

    private static ImmutableArray<TapeMark> Marks =>
        TapeMarks
            .UpTo(0.2m, 100)
            .ThenUpTo(0.4m, 99)
            .ThenUpTo(0.6m, 98)
            .ThenUpTo(0.8m, 97)
            .ThenUpTo(1.0m, 96)
            .ThenUpTo(1.2m, 95)
            .ThenUpTo(1.4m, 94)
            .ThenUpTo(1.6m, 93)
            .ThenUpTo(1.8m, 92)
            .ThenUpTo(2.0m, 91)
            .ThenUpTo(3.0m, 90)
            .ThenUpTo(4.0m, 85)
            .ThenUpTo(5, 80)
            .ThenUpTo(6, 75)
            .ThenUpTo(7, 70)
            .ThenUpTo(8, 65)
            .ThenUpTo(9, 60)
            .ThenUpTo(10, 55)
            .ThenUpTo(11, 50)
            .ThenUpTo(12, 45)
            .ThenUpTo(13, 40)
            .ThenUpTo(14, 35)
            .ThenUpTo(15, 30)
            .OffTape(OffTape);

    public static TapeDefinition Definition => new()
    {
        Name = "NZ F3J side",
        Unit = "m",
        Marks = Marks,
        OffTapeReading = OffTape,
    };
}
