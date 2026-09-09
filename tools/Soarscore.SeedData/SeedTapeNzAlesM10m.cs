// ALES M 10 m landing tape — the metre-marked instrument Class M prescribes.
//
// Instrument clause: NZ.3.12.2 a (docs/rules/nz/source-docs/nzmaa-s5-soaring-2024.md:1677),
// "The landing circle will consist of a 10 meter tape marked in 1 meter
// increments." (The story text cites this sentence as "NZ.3.12.1 a"; the
// sentence lives under 3.12.2 Landing, so the seed cites where it is.)
// Award table: NZ.2.4.5 (source-docs:515-540) — 1 m -> 50 ... 10 m -> 5,
// over 10 m -> 0 — reached via NZ.3.12.2 b. Authority is the clauses; no
// GliderScore table is cited.
//
// This instrument reads in METRES, not points: mark (n, n) says a nose in
// (n-1, n] reads n metres. The metre IS the reading, so composing with
// NZ.2.4.5 is the identity — which is why the class seeds state that table's
// distances, not a second transcription of them. The table's "Next full metre"
// rounding lives on the class's landingDistance metric (Ceiling 1 m), not on
// the tape.
//
// No off-tape reading: the instrument ends at 10 m and nothing is printed past
// it. A landing beyond the tape is recorded as a distance naming no
// instrument — decision 3's base case, and the field's reality when the tapes
// run out — awarding 0 through NZ.2.4.5's unbounded row. Inventing a reading
// for that band would be the guess the story forbids, so End() states there is
// none and OffTapeReading stays null deliberately.
//
// Composes with the {1 ... 10}-bounded landing lookups, whose boundaries are
// exactly this tape's:
//   - 30-f5j, 85c-nz-f5j-ndc (5.5.11.12 h)
//   - 86-nz-x5j (NZ.3.14.2 (second c), table at NZ.2.4.5)
//   - 80-nz-m-ales200, 81-nz-m-ndc (NZ.3.12.2 b, table at NZ.2.4.5)
// Refuses — a tape band straddling two awards is refused, never approximated
// (decision 2); the refusal must name the reading, its band and the straddled
// awards (WI-1):
//   - 50-f3j (F3J.10.5), 60-f5l (5.5.12.11.2): the 0.2 m award steps straddle
//     the 1 m marks — e.g. reading 1 means (0, 1], which F3J splits five ways.
//   - 20-f3b (F3B.2.3 d): award bands 11-15 m lie past the tape's last mark.
//   - 83-nz-n-ales123 (NZ.3.13.1 e), 85-nz-p-radian (NZ.3.15.1 e): the 25-point
//     band boundary at 15 m lies past the tape's last mark.
//   - 40-f5k, 85d-nz-f5k-ndc: lookup over intrinsic flight.sequence — unit
//     mismatch, a metre scale cannot score a unitless metric.
// No pairing to compose (no LookupTerm at all):
// 10-f3k, 70-f3f, 85b-nz-f3k-ndc, 90-aggregate.
//
// Fixture need: ales-sample-comp scheme 6 ("ALES Landing", marks 1..10) is
// this instrument verbatim.

using System.Collections.Immutable;

namespace Soarscore.SeedData;

public static class SeedTapeNzAlesM10m
{
    private static ImmutableArray<TapeMark> Marks =>
        TapeMarks
            .UpTo(1, 1)
            .Then(2, 2)
            .Then(3, 3)
            .Then(4, 4)
            .Then(5, 5)
            .Then(6, 6)
            .Then(7, 7)
            .Then(8, 8)
            .Then(9, 9)
            .Then(10, 10)
            .End();

    public static TapeDefinition Definition => new()
    {
        Name = "ALES M 10 m tape",
        Unit = "m",
        Marks = Marks,
        // No off-tape reading — deliberately null (see the header). End()
        // above states the same; the two agree by construction, not by a
        // restated literal.
    };
}
