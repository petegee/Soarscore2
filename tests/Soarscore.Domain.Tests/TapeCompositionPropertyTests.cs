// WI-1 of tape-points-landing-seeds.md — generated-scale properties over the
// Domain composition function (CsCheck where it adds value beyond the
// corpus examples in TapeCompositionTests).
//
// (a) Inversion fidelity: any strictly-decreasing award table inverts to a
// reading scale whose composition is the identity, and every in-band distance
// agrees with the direct award — the tape changes the observation's scale,
// never the rule (story decision 5).
// (b) Coarsening straddles: dropping an interior mark merges two bands around
// a class boundary, so composition must refuse with straddledBand —
// refinement is exactly the condition, and near-misses refuse rather than
// approximate (story decision 2).

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

public class TapeCompositionPropertyTests
{
    private static decimal DirectAward(IReadOnlyList<LookupRow> rows, decimal distance)
    {
        foreach (var row in rows)
        {
            if (row.UpTo is null || distance <= row.UpTo.Value)
                return row.Points;
        }

        throw new InvalidOperationException("Generated tables are terminally unbounded; unreachable.");
    }

    /// <summary>
    /// A generated award table: 2..6 strictly increasing boundaries with
    /// strictly decreasing positive awards and a terminal unbounded row, plus
    /// its inversion — a reading scale whose readings are the awards and whose
    /// off-scale reading is the club's 0.
    /// </summary>
    private static (ImmutableArray<LookupRow> Rows, ReadingScale Tape) GeneratedPair(int start, int[] gaps)
    {
        var bounds = new List<decimal>();
        decimal running = 0m;
        foreach (var gap in gaps)
        {
            running += gap / 10m;
            bounds.Add(running);
        }

        var rows = bounds.Select((b, i) => new LookupRow(b, start - (7m * i)))
            .Append(new LookupRow(null, 0m))
            .ToImmutableArray();
        var tape = new ReadingScale
        {
            Unit = "m",
            Marks = [.. bounds.Select((b, i) => new ScaleMark(b, start - (7m * i)))],
            OffScaleReading = 0m,
        };
        return (rows, tape);
    }

    [Fact]
    public void Generated_inverted_tables_compose_with_identity_and_fidelity()
    {
        (from start in Gen.Int[60, 300]
         from gaps in Gen.Int[1, 30].Array[2, 6]
         select (start, gaps: gaps.ToArray())).Sample(t =>
         {
             var (rows, tape) = GeneratedPair(t.start, t.gaps);

             var composed = TapeComposition.Compose(tape, "m", rows);
             composed.IsSuccess.Should().BeTrue(composed.Code);

             // Identity: each reading resolves to itself.
             foreach (var mark in tape.Marks)
                 TapeComposition.Resolve(tape, "m", rows, mark.Reading).Value
                     .Should().Be(mark.Reading);
             TapeComposition.Resolve(tape, "m", rows, 0m).Value.Should().Be(0m);

             // Fidelity: every in-band distance agrees with the direct award.
             var last = tape.Marks[^1].UpTo;
             for (var d = 0m; d <= last + 2m; d += 0.5m)
             {
                 var reading = tape.Marks.FirstOrDefault(m => d <= m.UpTo)?.Reading ?? 0m;
                 TapeComposition.Resolve(tape, "m", rows, reading).Value
                     .Should().Be(DirectAward(rows, d), $"distance {d}");
             }
         });
    }

    [Fact]
    public void Coarsening_a_tape_by_one_mark_straddles_and_refuses()
    {
        (from start in Gen.Int[60, 300]
         from gaps in Gen.Int[1, 30].Array[3, 6]
         select (start, gaps: gaps.ToArray())).Sample(t =>
         {
             var (rows, tape) = GeneratedPair(t.start, t.gaps);
             TapeComposition.Compose(tape, "m", rows).IsSuccess.Should().BeTrue("the full tape refines the table");

             // Drop an interior mark: the merged band spans the dropped
             // boundary strictly inside, so the pairing must refuse loudly.
             var victim = t.gaps.Length / 2;
             var coarsened = new ReadingScale
             {
                 Unit = "m",
                 Marks = [.. tape.Marks.Where((_, i) => i != victim)],
                 OffScaleReading = 0m,
             };

             var refused = TapeComposition.Compose(coarsened, "m", rows);
             refused.IsFailure.Should().BeTrue("a merged band straddles two awards");
             refused.Code.Should().Be("tapeComposition.straddledBand");
         });
    }
}
