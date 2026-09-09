// WI-1 of tape-points-landing-seeds.md — the composition function, proven
// against the shipped corpus without a duplicate table in a test.
//
// Every award table below is extracted from Corpus.All definitions, and every
// instrument snapshot is mapped from the WI-2 tape catalogue (TapeMapping) —
// no landing table and no tape scale is re-transcribed here. The sweep oracle
// (DirectAward) reads the class table at an in-band distance; the pinned
// anchors beside it are the story's owner-supplied screenshot values, cited,
// not a table.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;
using ScoreTerm = Soarscore.Domain.PublishedClassDefinition.ScoreTerm;

namespace Soarscore.Domain.Tests;

public class TapeCompositionTests
{
    // ------------------------------------------------------------ extraction
    // Every award table and metric unit is read out of the shipped corpus.

    private static (LookupTerm Lookup, string? Unit) LandingLookup(string fileName)
    {
        var definition = Corpus.All.First(c => c.FileName == fileName).Definition;
        foreach (var phase in definition.Phases)
        {
            foreach (var task in phase.Tasks)
            {
                foreach (var term in task.Score.Concat(task.ScoreNormalised))
                {
                    var found = FindLandingLookup(term);
                    if (found is not null)
                    {
                        var metric = task.Metrics.First(m => m.Name == found.MetricRef);
                        return (found, metric.Unit);
                    }
                }
            }
        }

        throw new InvalidOperationException($"No landing lookup in '{fileName}'.");
    }

    private static LookupTerm? FindLandingLookup(ScoreTerm term) => term switch
    {
        LookupTerm lookup when lookup.MetricRef == "landingDistance" => lookup,
        ConditionalTerm conditional => FindLandingLookup(conditional.Then)
            ?? (conditional.Else is null ? null : FindLandingLookup(conditional.Else)),
        _ => null,
    };

    /// <summary>
    /// The class table read at one distance: the first row whose upper bound
    /// covers it. Test-local oracle — the same walk the distance path
    /// evaluates — throwing past a bounded-final table rather than guessing.
    /// </summary>
    private static decimal DirectAward(IReadOnlyList<LookupRow> rows, decimal distance)
    {
        foreach (var row in rows)
        {
            if (row.UpTo is null || distance <= row.UpTo.Value)
                return row.Points;
        }

        throw new InvalidOperationException("Award table covers every distance in range; unreachable.");
    }

    private static ReadingScale NzF3JSide() => SeedTapeNzF3JSide.Definition.ToReadingScale();

    private static ReadingScale NzF3BSide() => SeedTapeNzF3BSide.Definition.ToReadingScale();

    private static ReadingScale AlesMetreTape() => SeedTapeNzAlesM10m.Definition.ToReadingScale();

    // ---------------------------------------------------------------- identity

    [Fact]
    public void F3J_side_tape_is_identity_over_the_F3J_table()
    {
        var (lookup, unit) = LandingLookup("50-f3j");
        var tape = NzF3JSide();

        var composed = TapeComposition.Compose(tape, unit, lookup.Rows);
        composed.IsSuccess.Should().BeTrue(composed.Code);

        // 23 marks plus the distinguished off-the-tape reading — derived
        // counts, not literals: the reading looks like points because the
        // composition is the identity (story §The arithmetic that settles it).
        composed.Value.Awards.Should().HaveCount(tape.Marks.Length + 1);
        foreach (var award in composed.Value.Awards)
        {
            var resolved = composed.Value.Resolve(award.Reading);
            resolved.IsSuccess.Should().BeTrue();
            resolved.Value.Should().Be(award.Reading, $"reading {award.Reading}");
        }
    }

    [Fact]
    public void F3J_side_tape_is_identity_over_the_F5L_table()
    {
        var (lookup, unit) = LandingLookup("60-f5l");
        var tape = NzF3JSide();

        var composed = TapeComposition.Compose(tape, unit, lookup.Rows);
        composed.IsSuccess.Should().BeTrue(composed.Code);

        foreach (var award in composed.Value.Awards)
            composed.Value.Resolve(award.Reading).Value.Should().Be(award.Reading);
    }

    [Fact]
    public void F3B_side_tape_is_identity_over_the_F3B_table()
    {
        // The F3B side is F3B.2.3 d read backwards (owner-confirmed printed in
        // points, 2026-09-09), so composing it with its own table is the
        // identity — the same coincidence that makes F3J readings look like
        // points, now witnessed for F3B.
        var (lookup, unit) = LandingLookup("20-f3b");
        var tape = NzF3BSide();

        var composed = TapeComposition.Compose(tape, unit, lookup.Rows);
        composed.IsSuccess.Should().BeTrue(composed.Code);

        // 15 marks plus the distinguished off-the-tape reading — derived
        // counts, not literals.
        composed.Value.Awards.Should().HaveCount(tape.Marks.Length + 1);
        foreach (var award in composed.Value.Awards)
        {
            var resolved = composed.Value.Resolve(award.Reading);
            resolved.IsSuccess.Should().BeTrue();
            resolved.Value.Should().Be(award.Reading, $"reading {award.Reading}");
        }

        TapeComposition.Resolve(tape, unit, lookup.Rows, 100m).Value.Should().Be(100m);
        TapeComposition.Resolve(tape, unit, lookup.Rows, 95m).Value.Should().Be(95m);
        TapeComposition.Resolve(tape, unit, lookup.Rows, 30m).Value.Should().Be(30m);
        TapeComposition.Resolve(tape, unit, lookup.Rows, 0m).Value.Should().Be(0m);
    }

    // ------------------------------------------------------- owner reproductions

    [Fact]
    public void F3J_side_tape_reproduces_F5J_enter_landing_row_for_row()
    {
        var (lookup, unit) = LandingLookup("30-f5j");
        var tape = NzF3JSide();

        var composed = TapeComposition.Compose(tape, unit, lookup.Rows);
        composed.IsSuccess.Should().BeTrue(composed.Code);

        // Row-for-row against the corpus oracle: each reading resolves to the
        // class table's own award at an in-band distance (the mark's inclusive
        // upper bound, the last mark's end plus one for the terminal band).
        var lastUpTo = tape.Marks[^1].UpTo;
        foreach (var mark in tape.Marks)
            TapeComposition.Resolve(tape, unit, lookup.Rows, mark.Reading).Value
                .Should().Be(DirectAward(lookup.Rows, mark.UpTo), $"reading {mark.Reading}");
        TapeComposition.Resolve(tape, unit, lookup.Rows, tape.OffScaleReading!.Value).Value
            .Should().Be(DirectAward(lookup.Rows, lastUpTo + 1m), "off-the-tape reading");

        // Owner anchors from the story's verified arithmetic table
        // (tape-points-landing-seeds.md §The arithmetic that settles it).
        TapeComposition.Resolve(tape, unit, lookup.Rows, 100m).Value.Should().Be(50m);
        TapeComposition.Resolve(tape, unit, lookup.Rows, 95m).Value.Should().Be(45m);
        TapeComposition.Resolve(tape, unit, lookup.Rows, 90m).Value.Should().Be(40m);
        TapeComposition.Resolve(tape, unit, lookup.Rows, 55m).Value.Should().Be(5m);
        TapeComposition.Resolve(tape, unit, lookup.Rows, 0m).Value.Should().Be(0m);
    }

    [Fact]
    public void F3J_side_tape_reproduces_F3B_enter_points_row_for_row()
    {
        // Verified row-for-row against gliderscore/f3b-enter-points.png since
        // 2026-09-09 (previously owner-description-only): 30-90 in fives plus
        // 91-95 -> 95 and 96-100 -> 100.
        var (lookup, unit) = LandingLookup("20-f3b");
        var tape = NzF3JSide();

        var composed = TapeComposition.Compose(tape, unit, lookup.Rows);
        composed.IsSuccess.Should().BeTrue(composed.Code);

        var lastUpTo = tape.Marks[^1].UpTo;
        foreach (var mark in tape.Marks)
            TapeComposition.Resolve(tape, unit, lookup.Rows, mark.Reading).Value
                .Should().Be(DirectAward(lookup.Rows, mark.UpTo), $"reading {mark.Reading}");
        TapeComposition.Resolve(tape, unit, lookup.Rows, tape.OffScaleReading!.Value).Value
            .Should().Be(DirectAward(lookup.Rows, lastUpTo + 1m), "off-the-tape reading");

        // Owner anchors (story §The arithmetic that settles it: 91-95 -> 95,
        // 96-100 -> 100).
        TapeComposition.Resolve(tape, unit, lookup.Rows, 100m).Value.Should().Be(100m);
        TapeComposition.Resolve(tape, unit, lookup.Rows, 95m).Value.Should().Be(95m);
        TapeComposition.Resolve(tape, unit, lookup.Rows, 90m).Value.Should().Be(90m);
        TapeComposition.Resolve(tape, unit, lookup.Rows, 0m).Value.Should().Be(0m);
    }

    // ---------------------------------------------------------------- refusals

    /// <summary>
    /// The shipped F3B side (TapeCorpus seeds it since the 2026-09-09 owner
    /// evidence: printed in points, gliderscore/f3b-enter-points.png). Its
    /// {1 ... 15} boundaries straddle F3J's 0.2-granular awards — that side of
    /// the tape cannot score this class.
    /// </summary>
    private static ReadingScale ShippedF3BSide() => NzF3BSide();

    [Fact]
    public void Shipped_F3B_side_cannot_score_F3J_or_F5L()
    {
        // Story: a reading of 95 there means (1, 2], which F3J splits five
        // ways — that side of the tape cannot score this class.
        var tape = ShippedF3BSide();
        var (f3j, f3jUnit) = LandingLookup("50-f3j");
        var (f5l, f5lUnit) = LandingLookup("60-f5l");

        var refusedF3J = TapeComposition.Compose(tape, f3jUnit, f3j.Rows);
        refusedF3J.IsFailure.Should().BeTrue("the F3B side cannot score F3J");
        refusedF3J.Code.Should().Be("tapeComposition.straddledBand");
        refusedF3J.Message.Should().Contain("100").And.Contain("[0, 1]");

        var refusedF5L = TapeComposition.Compose(tape, f5lUnit, f5l.Rows);
        refusedF5L.IsFailure.Should().BeTrue("the F3B side cannot score F5L");
        refusedF5L.Code.Should().Be("tapeComposition.straddledBand");
    }

    [Fact]
    public void Metre_tape_cannot_score_ALES_N_or_P()
    {
        // The shipped ALES M instrument (no off-tape reading — the catalogue
        // is honest about the tape ending at 10 m) against the N/P tables,
        // whose 25-point boundary at 15 m lies past its last mark.
        var tape = AlesMetreTape();
        tape.OffScaleReading.Should().BeNull("the catalogue defines no beyond-the-tape reading");
        var (nzN, nzNUnit) = LandingLookup("83-nz-n-ales123");
        var (nzP, nzPUnit) = LandingLookup("85-nz-p-radian");

        var refusedN = TapeComposition.Compose(tape, nzNUnit, nzN.Rows);
        refusedN.IsFailure.Should().BeTrue("ALES M's metre tape cannot score ALES N");
        refusedN.Code.Should().Be("tapeComposition.straddledBand");
        refusedN.Message.Should().Contain("(10, inf)").And.Contain("15");

        var refusedP = TapeComposition.Compose(tape, nzPUnit, nzP.Rows);
        refusedP.IsFailure.Should().BeTrue("ALES M's metre tape cannot score ALES P");
        refusedP.Code.Should().Be("tapeComposition.straddledBand");
    }

    [Fact]
    public void Metre_tape_cannot_score_F3J()
    {
        // Seed-cited (SeedTapeNzAlesM10m header): the 0.2 m award steps
        // straddle the 1 m marks — reading 1 means [0, 1], which F3J splits
        // five ways.
        var tape = AlesMetreTape();
        var (f3j, unit) = LandingLookup("50-f3j");

        var refused = TapeComposition.Compose(tape, unit, f3j.Rows);
        refused.IsFailure.Should().BeTrue();
        refused.Code.Should().Be("tapeComposition.straddledBand");
        refused.Message.Should().Contain("1").And.Contain("[0, 1]");
    }

    [Fact]
    public void Metre_tape_composes_with_the_NZ_M_table()
    {
        // Positive control: the instrument is sound — its own class's table
        // has exactly the tape's boundaries, so every mark composes.
        var tape = AlesMetreTape();
        var (nzM, unit) = LandingLookup("80-nz-m-ales200");

        var composed = TapeComposition.Compose(tape, unit, nzM.Rows);
        composed.IsSuccess.Should().BeTrue(composed.Code);
        composed.Value.Awards.Should().HaveCount(tape.Marks.Length);

        foreach (var mark in tape.Marks)
            TapeComposition.Resolve(tape, unit, nzM.Rows, mark.Reading).Value
                .Should().Be(DirectAward(nzM.Rows, mark.UpTo), $"reading {mark.Reading}");
    }

    [Fact]
    public void Reading_outside_the_tape_set_is_refused()
    {
        var (lookup, unit) = LandingLookup("50-f3j");
        var tape = NzF3JSide();

        foreach (var unknown in new[] { 101m, -1m, 95.5m })
        {
            var resolved = TapeComposition.Resolve(tape, unit, lookup.Rows, unknown);
            resolved.IsFailure.Should().BeTrue($"reading {unknown} has no mark on the scale");
            resolved.Code.Should().Be("tapeComposition.unknownReading");
        }
    }

    [Fact]
    public void Repeated_reading_is_refused()
    {
        var (lookup, unit) = LandingLookup("50-f3j");

        var repeatedMark = new ReadingScale
        {
            Unit = "m",
            Marks = [new ScaleMark(1m, 50m), new ScaleMark(2m, 50m)],
            OffScaleReading = 0m,
        };
        var refused = TapeComposition.Compose(repeatedMark, unit, lookup.Rows);
        refused.IsFailure.Should().BeTrue();
        refused.Code.Should().Be("tapeComposition.repeatedReading");

        var offScaleCollision = new ReadingScale
        {
            Unit = "m",
            Marks = [new ScaleMark(1m, 50m), new ScaleMark(2m, 45m)],
            OffScaleReading = 50m,
        };
        var refusedOffScale = TapeComposition.Compose(offScaleCollision, unit, lookup.Rows);
        refusedOffScale.IsFailure.Should().BeTrue();
        refusedOffScale.Code.Should().Be("tapeComposition.repeatedReading");
    }

    [Fact]
    public void Non_monotone_tape_is_refused()
    {
        var (lookup, unit) = LandingLookup("50-f3j");

        var descending = new ReadingScale
        {
            Unit = "m",
            Marks = [new ScaleMark(2m, 45m), new ScaleMark(1m, 50m)],
            OffScaleReading = 0m,
        };
        TapeComposition.Compose(descending, unit, lookup.Rows).Code
            .Should().Be("tapeComposition.tapeNotMonotone");

        var duplicateBound = new ReadingScale
        {
            Unit = "m",
            Marks = [new ScaleMark(1m, 50m), new ScaleMark(1m, 45m)],
            OffScaleReading = 0m,
        };
        TapeComposition.Compose(duplicateBound, unit, lookup.Rows).Code
            .Should().Be("tapeComposition.tapeNotMonotone");

        var empty = new ReadingScale
        {
            Unit = "m",
            Marks = [],
            OffScaleReading = 0m,
        };
        TapeComposition.Compose(empty, unit, lookup.Rows).Code
            .Should().Be("tapeComposition.tapeNotMonotone");

        var nonPositive = new ReadingScale
        {
            Unit = "m",
            Marks = [new ScaleMark(0m, 50m)],
            OffScaleReading = 0m,
        };
        TapeComposition.Compose(nonPositive, unit, lookup.Rows).Code
            .Should().Be("tapeComposition.tapeNotMonotone");
    }

    [Fact]
    public void Unit_mismatch_is_refused()
    {
        var (lookup, _) = LandingLookup("50-f3j");
        var tape = NzF3JSide();
        tape.Unit.Should().Be("m");

        var wrongUnit = TapeComposition.Compose(tape, "s", lookup.Rows);
        wrongUnit.IsFailure.Should().BeTrue();
        wrongUnit.Code.Should().Be("tapeComposition.unitMismatch");

        // A unitless metric (e.g. a lookup over an intrinsic count) cannot be
        // scored off a metre scale either — never guess an instrument.
        var unitless = TapeComposition.Compose(tape, null, lookup.Rows);
        unitless.IsFailure.Should().BeTrue();
        unitless.Code.Should().Be("tapeComposition.unitMismatch");
    }
}
