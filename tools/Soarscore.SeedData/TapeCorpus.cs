// The tape catalogue — tape-points-landing-seeds.md WI-2 (kanban lane moves;
// the filename is the stable citation).
//
// Reference data beside the classes, counted SEPARATELY from them: Corpus.All
// keeps its sixteen and gains nothing from this story, and TapeCorpus.All has
// its own pinned count below. A new instrument is a new SeedTape entry here;
// a new class touches nothing here (NFR-2, additive on both axes).
//
// Seeded (WI-2 done-when: the tool is green, the tapes emit canonically):
//   - tape-nz-f3j-side — the F3J-graduated side, NZ.2.4.4.
//   - tape-nz-f3b-side — the F3B-graduated side, F3B.2.3 d read backwards.
//     Evidence: owner assertion 2026-09-09 that the physical side is printed
//     in points, corroborated by gliderscore/f3b-enter-points.png (identity
//     rows exactly F3B.2.3 d's award set; extra 91-94/96-99 rows are the F3J
//     side pre-composed, verified row-for-row — one fused table serving both
//     sides). See SeedTapeNzF3BSide.cs; do not re-open without new evidence.
//   - tape-nz-ales-m-10m — ALES M's metre-marked instrument, NZ.3.12.2 a.

using System.Collections.Immutable;

namespace Soarscore.SeedData;

/// <param name="FileName">The tape's file-name stem.</param>
public sealed record SeedTape(string FileName, TapeDefinition Tape);

public static class TapeCorpus
{
    /// <summary>
    /// The pinned tape count (WI-2). A new instrument bumps this literal in the
    /// same commit that adds its SeedTape — the count moves only by an explicit
    /// catalogue change, never silently.
    /// </summary>
    public const int ExpectedCount = 3;

    public static ImmutableArray<SeedTape> All =>
    [
        new("tape-nz-f3j-side", SeedTapeNzF3JSide.Definition),
        new("tape-nz-f3b-side", SeedTapeNzF3BSide.Definition),
        new("tape-nz-ales-m-10m", SeedTapeNzAlesM10m.Definition),
    ];
}
