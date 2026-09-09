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
//   - tape-nz-ales-m-10m — ALES M's metre-marked instrument, NZ.3.12.2 a.
//
// NOT seeded — the NZ F3B side, withheld for lack of evidence (story: "subject
// to evidence, do NOT guess"). What is known: the owner states F3B is read on
// the F3B side, which says WHICH side is used, not WHAT is printed on it. Two
// hypotheses fit everything in the tree: printed in F3B points (readings
// {100, 95 ... 30}) or printed in metres (readings {1 ... 15}, composing with
// F3B.2.3 d as the identity). F3B.2.3 d states distances and no instrument, so
// the rulebook does not settle it; the in-tree fixtures do not either — the
// metre-keyed "FAI 15 Metre Tape" table is distance entry, consistent with
// both, and no fused F3B-points table is citable (cite the clause, never
// GliderScore). Seeding either reading set would be a guess. Unblocked by:
// the physical tape's printed scale, or a fixture witnessing F3B-side readings
// verbatim. Until then the refusal is the feature — WI-1's composition must
// refuse an F3B-side pairing it has no scale for, loudly.

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
    public const int ExpectedCount = 2;

    public static ImmutableArray<SeedTape> All =>
    [
        new("tape-nz-f3j-side", SeedTapeNzF3JSide.Definition),
        new("tape-nz-ales-m-10m", SeedTapeNzAlesM10m.Definition),
    ];
}
