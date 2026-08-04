// The eleven seed definitions, in file-name order.
//
// Seven FAI classes and four NZ national ones. The NZ classes are here because
// they are a DIFFERENT rulebook — they found four things the FAI corpus could
// not (F24-F27), and one of them, F24, would have mis-scored a class that
// adopted and ran cleanly.

using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.SeedData;

/// <param name="FileName">The definition's file-name stem.</param>
public sealed record SeedClass(string FileName, ClassDefinition Definition);

public static class Corpus
{
    public static ImmutableArray<SeedClass> All =>
    [
        new("10-f3k", SeedF3K.Definition),
        new("20-f3b", SeedF3B.Definition),
        new("30-f5j", SeedF5J.Definition),
        new("40-f5k", SeedF5K.Definition),
        new("50-f3j", SeedF3J.Definition),
        new("60-f5l", SeedF5L.Definition),
        new("70-f3f", SeedF3F.Definition),
        new("80-nz-m-ales200", SeedNzMAles200.Definition),
        new("81-nz-m-ndc", SeedNzMNdc.Definition),
        new("83-nz-n-ales123", SeedNzNAles123.Definition),
        new("85-nz-p-radian", SeedNzPRadian.Definition),
    ];
}
