// Catalogue-to-snapshot mapping — kanban/backlog/tape-points-landing-seeds.md
// WI-3. TapeDefinition.cs defers this mapping here rather than pre-empting
// it: the catalogue stays nullable where the evidence is silent, and the
// competition's declaration snapshots a WI-1 reading scale. Field-for-field:
// Unit, every mark (UpTo is required on the snapshot — the catalogue's
// integrity check guarantees bounded marks, so a null UpTo here is corrupt
// catalogue data and throws rather than guessing), Reading, and the off-scale
// reading or its honest null. The snapshot drops the catalogue Name: the
// declaration's instrument name is the competition's own word for the side it
// used, resolved within that declaration.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class TapeMapping
{
    public static ReadingScale ToReadingScale(this TapeDefinition tape) =>
        new()
        {
            Unit = tape.Unit,
            Marks = [.. tape.Marks.Select(m => new ScaleMark(
                m.UpTo ?? throw new InvalidOperationException(
                    $"Tape '{tape.Name}' has an unbounded mark — the terminal band belongs to offTapeReading."),
                m.Reading))],
            OffScaleReading = tape.OffTapeReading,
        };
}
