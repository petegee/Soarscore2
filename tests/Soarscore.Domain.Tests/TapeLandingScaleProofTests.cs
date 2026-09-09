// WI-5 of kanban/backlog/tape-points-landing-seeds.md — the shared prerequisite
// proof: composition fidelity, refinement-iff, instrument equivalence and
// eligibility invariance as named properties, plus membership / refusal /
// three-way-distinction examples and the WI-3 declaration/capture/amendment
// contract.
//
// Contract posture (read the story first):
//   WI-1 (TapeComposition + ReadingScale) and WI-2 (TapeCorpus, 3 tapes) are
//   REAL here — properties (a) and (b) target them directly, with class tables
//   extracted from Corpus.All (never re-transcribed) and scales mapped from
//   the catalogue (never re-transcribed).
//   WI-3 (declaration / capture / amendment / correction) is REAL — its decide
//   functions are asserted directly, with exact refusal codes.
//   WI-4 (composition at resolution: scoring, completeness, reporting) is
//   REAL: Measurement.Instrument rides resolution into the composed lookup,
//   and every other lookup — and every predicate, gate and stage — is
//   untouched. The two WI4Gap_ pins flipped with it (equivalence at the
//   scoring grain; the off-scale reading composes to no bonus).
// Recorded findings:
//   F-WI4-1 (discharged by WI-4): a reading used to score as its raw number
//     in metres (reading 98 on F3J scored 0, not 98; the off-tape 0 scored as
//     the spot landing). Reading_scores_identically_whether_read_or_measured
//     and the three-way distinction now assert the composed behaviour.
//   F-WI4-2: completeness already counts either form (a reading satisfies the
//     landing input) — conformance, asserted live.
//   F-MAP-1: no production code maps TapeDefinition to the ReadingScale
//     snapshot a declaration carries; this file maps the catalogue in-test.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;
using Predicate = Soarscore.Domain.PublishedClassDefinition.Predicate;
using ScoreTerm = Soarscore.Domain.PublishedClassDefinition.ScoreTerm;

namespace Soarscore.Domain.Tests;

public class TapeLandingScaleProofTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 9, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------ helpers

    /// <summary>
    /// The independent oracle for fidelity: the class table applied to a
    /// distance directly (the existing inclusive-upper-bound walk). Production
    /// composition (<see cref="TapeComposition"/>) must agree with it on every
    /// distance whenever the pairing composes.
    /// </summary>
    private static decimal DirectAward(IReadOnlyList<LookupRow> rows, decimal d)
    {
        foreach (var row in rows)
            if (row.UpTo is null || d <= row.UpTo.Value)
                return row.Points;
        throw new InvalidOperationException("Lookup rows cover every distance; unreachable.");
    }

    /// <summary>Which reading the scale gives distance d (band semantics).</summary>
    private static decimal ReadingOf(ReadingScale scale, decimal d)
    {
        foreach (var mark in scale.Marks)
            if (d <= mark.UpTo)
                return mark.Reading;
        return scale.OffScaleReading!.Value;
    }

    private sealed record LandingTable(string FileName, string TaskCode, string Stage, Predicate? When, LookupTerm Lookup);

    private static ImmutableArray<LandingTable> LandingTables()
    {
        var builder = ImmutableArray.CreateBuilder<LandingTable>();
        foreach (var seed in Corpus.All)
        {
            foreach (var phase in seed.Definition.Phases)
            {
                foreach (var task in phase.Tasks)
                {
                    Collect(task.Code, "score", task.Score, builder, seed.FileName);
                    Collect(task.Code, "scoreNormalised", task.ScoreNormalised, builder, seed.FileName);
                }
            }
        }
        return builder.ToImmutable();

        static void Collect(string taskCode, string stage, ImmutableArray<ScoreTerm> terms,
            ImmutableArray<LandingTable>.Builder builder, string fileName)
        {
            foreach (var term in terms)
                CollectTerm(taskCode, stage, term, When: null, builder, fileName);
        }

        static void CollectTerm(string taskCode, string stage, ScoreTerm term, Predicate? When,
            ImmutableArray<LandingTable>.Builder builder, string fileName)
        {
            switch (term)
            {
                case LookupTerm lookup when lookup.MetricRef == "landingDistance":
                    builder.Add(new LandingTable(fileName, taskCode, stage, When, lookup));
                    break;
                case ConditionalTerm conditional:
                    CollectTerm(taskCode, stage, conditional.Then, conditional.When, builder, fileName);
                    if (conditional.Else is not null)
                        CollectTerm(taskCode, stage, conditional.Else, When, builder, fileName);
                    break;
            }
        }
    }

    /// <summary>
    /// The NZ F3J-side snapshot, mapped from the WI-2 catalogue (finding
    /// F-MAP-1): marks in order with the catalogue's off-scale reading. No
    /// number is re-transcribed — the marks below ARE the catalogue's.
    /// </summary>
    private static ReadingScale NzF3JSideScale()
    {
        var tape = TapeCorpus.All.First(t => t.FileName == "tape-nz-f3j-side").Tape;
        return new ReadingScale
        {
            Unit = tape.Unit,
            Marks = tape.Marks.Select(m => new ScaleMark(m.UpTo!.Value, m.Reading)).ToImmutableArray(),
            OffScaleReading = tape.OffTapeReading,
        };
    }

    private static ReadingScale AlesMetreScale()
    {
        var tape = TapeCorpus.All.First(t => t.FileName == "tape-nz-ales-m-10m").Tape;
        return new ReadingScale
        {
            Unit = tape.Unit,
            Marks = tape.Marks.Select(m => new ScaleMark(m.UpTo!.Value, m.Reading)).ToImmutableArray(),
            OffScaleReading = tape.OffTapeReading,
        };
    }

    /// <summary>
    /// The NZ F3B-side snapshot, mapped from the catalogue like the F3J side
    /// above (seeded since the 2026-09-09 owner evidence: printed in points).
    /// </summary>
    private static ReadingScale NzF3BSideScale()
    {
        var tape = TapeCorpus.All.First(t => t.FileName == "tape-nz-f3b-side").Tape;
        return new ReadingScale
        {
            Unit = tape.Unit,
            Marks = tape.Marks.Select(m => new ScaleMark(m.UpTo!.Value, m.Reading)).ToImmutableArray(),
            OffScaleReading = tape.OffTapeReading,
        };
    }

    private static ImmutableArray<DeclaredInstrument> DeclaredNzF3JSide() =>
        [new DeclaredInstrument { Instrument = "nz-f3j-side", Metric = "landingDistance", Scale = NzF3JSideScale() }];

    private static Competition AdoptedCompetition(ClassDefinition definition)
    {
        var adopted = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        return Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Tape Proof Competition", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adopted, Now));
    }

    private static ClassDefinition F3JDefinition => Corpus.All.First(c => c.FileName == "50-f3j").Definition;

    private static Entry OpenFlightWith()
    {
        var entry = Entry.Create(new EntryOpened(EntryId.New(), CompetitionId.New(), 0, 1, 1,
            GroupId.New(), CompetitorId.New(), ReflightRole.Original, Now));
        return entry.Apply(new FlightOpened(1, Now));
    }

    private static Entry Capture(Entry entry, string metric, MeasuredValue value,
        ImmutableArray<MetricDefinition> metrics, string? instrument = null,
        ImmutableArray<DeclaredInstrument> declared = default)
    {
        var captured = entry.CaptureMeasurement(1, metric, value, Now, metrics, instrument, declared);
        captured.IsSuccess.Should().BeTrue($"{metric}: {captured.Code} {captured.Message}");
        return entry.Apply(captured.Value);
    }

    private static ResolvedTask TaskWith(ImmutableArray<ScoreTerm> score, ImmutableArray<MetricDefinition> metrics) =>
        new(Code: "T", Name: "Test",
            Metrics: metrics,
            Flights: new LastFlight(),
            Timing: new ResolvedTiming(WorkingTimeKind.Fixed, null, null, null),
            Group: null, Normalise: null, ValidWhen: null, FlightValidWhen: null,
            RawScore: null, Reflight: null,
            Score: score,
            ScoreNormalised: ImmutableArray<ScoreTerm>.Empty);

    private static Dictionary<string, MeasuredValue> BaseMetrics(decimal landingDistance) => new()
    {
        ["flightTime"] = MeasuredValue.Of(500m),
        ["landingDistance"] = MeasuredValue.Of(landingDistance),
        ["overflySeconds"] = MeasuredValue.Of(0m),
        ["startHeight"] = MeasuredValue.Of(0m),
        ["startHeightRecorded"] = MeasuredValue.Of(true),
        ["touchedByCompetitor"] = MeasuredValue.Of(false),
        ["restedWithin75m"] = MeasuredValue.Of(true),
        ["landedInDefinedArea"] = MeasuredValue.Of(true),
        ["landedInLandingArea"] = MeasuredValue.Of(true),
        ["amrtPresetsCorrect"] = MeasuredValue.Of(true),
        ["timingDeviationInFavour"] = MeasuredValue.Of(false),
        ["atRestBy12Min"] = MeasuredValue.Of(true),
        ["lostPart"] = MeasuredValue.Of(false),
        ["touchedBeforeMeasuring"] = MeasuredValue.Of(false),
        ["damagedAndNotSafelyFlyable"] = MeasuredValue.Of(false),
        ["motorRestarted"] = MeasuredValue.Of(false),
        ["airborneAtRoundEnd"] = MeasuredValue.Of(false),
    };

    // ============================================================ WI-2 catalogue

    [Fact]
    public void Tape_catalogue_is_counted_separately_with_no_class_count_delta()
    {
        TapeCorpus.ExpectedCount.Should().Be(3);
        TapeCorpus.All.Should().HaveCount(TapeCorpus.ExpectedCount);
        TapeCorpus.All.Select(t => t.FileName)
            .Should().BeEquivalentTo("tape-nz-f3j-side", "tape-nz-f3b-side", "tape-nz-ales-m-10m");
        foreach (var tape in TapeCorpus.All)
            TapeIntegrity.Check(tape.FileName, tape.Tape).Should().BeEmpty($"{tape.FileName} must emit clean");

        // The story's hard boundary: no class definition changes, no new class
        // seed, no 31-f5j-tape-points variant from this story or its consumer.
        Corpus.All.Should().HaveCount(16);
        Corpus.All.Select(c => c.FileName).Should().NotContain("31-f5j-tape-points");
    }

    [Fact]
    public void Catalogue_F3J_side_inverts_the_corpus_F3J_table()
    {
        // The scale IS the award table read backwards: every mark's reading is
        // the award the corpus F3J table gives the mark's band, and the mark
        // set is the boundary set. Asserted structurally, not by
        // re-transcription.
        var scale = NzF3JSideScale();
        var f3j = LandingTables().First(t => t.FileName == "50-f3j").Lookup.Rows;
        var boundaries = f3j.Where(r => r.UpTo is not null).Select(r => r.UpTo!.Value).ToArray();
        scale.Marks.Select(m => m.UpTo).Should().BeEquivalentTo(boundaries, "same boundary set");
        foreach (var mark in scale.Marks)
            DirectAward(f3j, mark.UpTo).Should().Be(mark.Reading, $"mark at {mark.UpTo}");
        scale.OffScaleReading.Should().Be(0m, "the club writes 0 past the tape's end");
    }

    // ============================================================ property (a)

    [Fact]
    public void Property_a_Every_corpus_landing_table_composes_with_the_F3J_side()
    {
        var scale = NzF3JSideScale();
        var tables = LandingTables();
        tables.Should().NotBeEmpty();

        foreach (var table in tables)
        {
            var composed = TapeComposition.Compose(scale, "m", table.Lookup.Rows);
            composed.IsSuccess.Should().BeTrue(
                $"{table.FileName}/{table.TaskCode}/{table.Stage} must compose: {composed.Code} {composed.Message}");

            (from cents in Gen.Int[0, 2500]
             select cents / 100m).Sample(d =>
             {
                 var reading = ReadingOf(scale, d);
                 TapeComposition.Resolve(scale, "m", table.Lookup.Rows, reading).IsSuccess.Should().BeTrue();
                 composed.Value.Resolve(reading).Value.Should().Be(
                     DirectAward(table.Lookup.Rows, d),
                     $"{table.FileName} d={d} reading={reading}");
             });
        }
    }

    [Fact]
    public void Property_a_Composition_fidelity_holds_for_generated_tapes_and_tables()
    {
        (from gaps in Gen.Int[1, 40].Array[2, 6]
         from cents in Gen.Int[0, 2500]
         select (gaps: gaps.ToArray(), d: cents / 100m)).Sample(t =>
         {
             var bounds = new List<decimal>();
             decimal running = 0m;
             foreach (var g in t.gaps) { running += g / 10m; bounds.Add(running); }
             var scale = new ReadingScale
             {
                 Unit = "m",
                 Marks = bounds.Select((b, i) => new ScaleMark(b, 1000m + i)).ToImmutableArray(),
                 OffScaleReading = 0m,
             };
             var classBounds = bounds.Where((_, i) => i % 2 == 0).ToArray();
             var rows = classBounds.Select((b, i) => new LookupRow(b, 100m - i * 7m))
                 .Append(new LookupRow(null, 0m)).ToImmutableArray();

             var composed = TapeComposition.Compose(scale, "m", rows);
             composed.IsSuccess.Should().BeTrue("subset boundaries must compose");
             var reading = ReadingOf(scale, t.d);
             composed.Value.Resolve(reading).Value.Should().Be(DirectAward(rows, t.d));
         });
    }

    [Fact]
    public void Every_F3J_reading_composes_to_its_identity_award()
    {
        var scale = NzF3JSideScale();
        scale.ReadingSet.Should().HaveCount(24, "23 marks plus the distinguished off-scale reading");
        var f3j = LandingTables().First(t => t.FileName == "50-f3j").Lookup.Rows;
        var composed = TapeComposition.Compose(scale, "m", f3j);
        composed.IsSuccess.Should().BeTrue();
        foreach (var reading in scale.ReadingSet.Where(r => r != scale.OffScaleReading!.Value))
            composed.Value.Resolve(reading).Value.Should().Be(reading, "F3J composition is the identity");
        composed.Value.Resolve(scale.OffScaleReading!.Value).Value.Should().Be(0m, "off-scale carries no bonus");
    }

    [Fact]
    public void Owner_F5J_enter_landing_table_is_reproduced_row_for_row()
    {
        var scale = NzF3JSideScale();
        var f5j = LandingTables().First(t => t.FileName == "30-f5j").Lookup.Rows;
        var expected = new Dictionary<decimal, decimal>
        {
            [100] = 50, [99] = 50, [98] = 50, [97] = 50, [96] = 50,
            [95] = 45, [94] = 45, [93] = 45, [92] = 45, [91] = 45,
            [90] = 40, [85] = 35, [80] = 30, [75] = 25, [70] = 20,
            [65] = 15, [60] = 10, [55] = 5, [50] = 0, [45] = 0,
            [40] = 0, [35] = 0, [30] = 0, [0] = 0,
        };
        foreach (var reading in scale.ReadingSet)
            TapeComposition.Resolve(scale, "m", f5j, reading).Value.Should().Be(expected[reading], $"reading {reading}");
    }

    [Fact]
    public void Owner_F3B_enter_points_bands_are_reproduced()
    {
        var scale = NzF3JSideScale();
        var f3b = LandingTables().First(t => t.FileName == "20-f3b").Lookup.Rows;
        foreach (var reading in new[] { 96m, 97m, 98m, 99m, 100m })
            TapeComposition.Resolve(scale, "m", f3b, reading).Value.Should().Be(100m, $"reading {reading}");
        foreach (var reading in new[] { 91m, 92m, 93m, 94m, 95m })
            TapeComposition.Resolve(scale, "m", f3b, reading).Value.Should().Be(95m, $"reading {reading}");
        TapeComposition.Resolve(scale, "m", f3b, 90m).Value.Should().Be(90m);
        TapeComposition.Resolve(scale, "m", f3b, 30m).Value.Should().Be(30m);
        TapeComposition.Resolve(scale, "m", f3b, 0m).Value.Should().Be(0m);
    }

    [Fact]
    public void Ales_metre_tape_composes_where_it_covers_and_refuses_past_its_last_mark()
    {
        // The catalogue's null off-scale (honest — no invented reading) means
        // the composed rows end at the last mark, while an award boundary past
        // it is a loud straddle refusal (the seed's own refusal list).
        var scale = AlesMetreScale();
        scale.OffScaleReading.Should().BeNull("the catalogue withholds what the evidence does not state");
        var f5j = LandingTables().First(t => t.FileName == "30-f5j").Lookup.Rows;
        var nzN = LandingTables().First(t => t.FileName == "83-nz-n-ales123").Lookup.Rows;

        var composed = TapeComposition.Compose(scale, "m", f5j);
        composed.IsSuccess.Should().BeTrue("F5J's boundaries {1..10} lie within the 10 m marks");
        composed.Value.Awards.Should().HaveCount(10, "no terminal row without an off-scale reading");
        composed.Value.Resolve(10m).Value.Should().Be(5m);

        var refused = TapeComposition.Compose(scale, "m", nzN);
        refused.IsSuccess.Should().BeFalse("the 25-point boundary at 15 m lies past the last mark");
        refused.Code.Should().Be("tapeComposition.straddledBand");
        refused.Message.Should().Contain("(10, inf)");
    }

    // ============================================================ property (b)

    [Fact]
    public void Property_b_Refinement_is_exactly_the_composition_condition()
    {
        (from gaps in Gen.Int[1, 40].Array[2, 6]
         from dropEvery in Gen.Int[1, 3]
         from perturbIndex in Gen.Int[0, 99]
         select (gaps: gaps.ToArray(), dropEvery, perturbIndex)).Sample(t =>
         {
             var bounds = new List<decimal>();
             decimal running = 0m;
             foreach (var g in t.gaps) { running += g / 10m; bounds.Add(running); }
             Func<ReadingScale> Scale = () => new()
             {
                 Unit = "m",
                 Marks = bounds.Select((b, i) => new ScaleMark(b, 1000m + i)).ToImmutableArray(),
                 OffScaleReading = 0m,
             };
             Func<IEnumerable<decimal>, ImmutableArray<LookupRow>> Rows = b =>
                 b.Select((x, i) => new LookupRow(x, 50m - i)).Append(new LookupRow(null, 0m)).ToImmutableArray();

             var subset = bounds.Where((_, i) => i % t.dropEvery != 0).ToArray();
             var verdict = TapeComposition.Compose(Scale(), "m", Rows(subset));
             if (subset.All(bounds.Contains))
                 verdict.IsSuccess.Should().BeTrue("every class boundary is a tape boundary");
             else
             {
                 verdict.IsSuccess.Should().BeFalse();
                 verdict.Code.Should().Be("tapeComposition.straddledBand");
             }

             if (subset.Length > 0)
             {
                 // Near-miss: +0.05 is 2dp, so it cannot coincide with any 1dp
                 // tape boundary, while gaps >= 0.1 keep the table ascending.
                 var victim = t.perturbIndex % subset.Length;
                 var nearMiss = subset.ToArray();
                 nearMiss[victim] = nearMiss[victim] + 0.05m;
                 var refusal = TapeComposition.Compose(Scale(), "m", Rows(nearMiss));
                 refusal.IsSuccess.Should().BeFalse("a near-miss boundary must refuse, not approximate");
                 refusal.Code.Should().Be("tapeComposition.straddledBand");
                 refusal.Message.Should().Contain("straddles the award boundary at");
             }
         });
    }

    [Fact]
    public void WI1_refused_pairings_refuse_loudly()
    {
        // Story §"It also correctly fails where it should". The F3B side is
        // the shipped catalogue tape (owner evidence 2026-09-09: printed in
        // points) — the refusal is asserted against the real scale.
        var f3bSide = NzF3BSideScale();
        var f3j = LandingTables().First(t => t.FileName == "50-f3j").Lookup.Rows;
        var f5l = LandingTables().First(t => t.FileName == "60-f5l").Lookup.Rows;
        var nzP = LandingTables().First(t => t.FileName == "85-nz-p-radian").Lookup.Rows;

        var r1 = TapeComposition.Compose(f3bSide, "m", f3j);
        r1.IsSuccess.Should().BeFalse("the F3B side cannot score F3J");
        r1.Code.Should().Be("tapeComposition.straddledBand");
        r1.Message.Should().Contain("straddles");
        TapeComposition.Compose(f3bSide, "m", f5l).IsSuccess.Should().BeFalse("the F3B side cannot score F5L");

        var r3 = TapeComposition.Compose(AlesMetreScale(), "m", nzP);
        r3.IsSuccess.Should().BeFalse("the metre tape cannot score ALES P past its last mark");
        r3.Code.Should().Be("tapeComposition.straddledBand");
    }

    [Fact]
    public void Composition_refuses_malformed_scales_units_and_unknown_readings()
    {
        var f3j = LandingTables().First(t => t.FileName == "50-f3j").Lookup.Rows;

        TapeComposition.Compose(new ReadingScale
        {
            Unit = "m",
            Marks = [new ScaleMark(2m, 90m), new ScaleMark(1m, 95m)],
            OffScaleReading = 0m,
        }, "m", f3j).Code.Should().Be("tapeComposition.tapeNotMonotone");

        TapeComposition.Compose(new ReadingScale
        {
            Unit = "m",
            Marks = [new ScaleMark(1m, 90m), new ScaleMark(2m, 90m)],
            OffScaleReading = 0m,
        }, "m", f3j).Code.Should().Be("tapeComposition.repeatedReading");

        TapeComposition.Compose(NzF3JSideScale(), "ft", f3j).Code.Should().Be("tapeComposition.unitMismatch");
        TapeComposition.Compose(NzF3JSideScale(), null, f3j).Code
            .Should().Be("tapeComposition.unitMismatch", "a metre scale cannot score a unitless metric");

        var composed = TapeComposition.Compose(NzF3JSideScale(), "m", f3j);
        composed.IsSuccess.Should().BeTrue();
        var unknown = composed.Value.Resolve(87.5m);
        unknown.IsSuccess.Should().BeFalse();
        unknown.Code.Should().Be("tapeComposition.unknownReading");
    }

    // ============================================================ WI-3 declaration

    [Fact]
    public void Declaration_accepts_a_composable_set_and_an_empty_set()
    {
        var competition = AdoptedCompetition(F3JDefinition);

        var declared = competition.DeclareInstruments(DeclaredNzF3JSide(), "cd", Now);
        declared.IsSuccess.Should().BeTrue($"{declared.Code} {declared.Message}");
        competition = competition.Apply(declared.Value);
        competition.DeclaredInstruments.Should().NotBeNull();
        competition.DeclaredInstruments!.Instruments.Should().ContainSingle();
        competition.DeclaredInstruments.By.Should().Be("cd");
        competition.DeclaredInstruments.At.Should().Be(Now);

        AdoptedCompetition(F3JDefinition).DeclareInstruments([], "cd", Now).IsSuccess
            .Should().BeTrue("an empty set is valid and is the default");
    }

    [Fact]
    public void Declaration_refuses_straddled_pairings_at_declaration_time()
    {
        var competition = AdoptedCompetition(F3JDefinition);
        var f3bSide = new ReadingScale
        {
            Unit = "m",
            Marks = Enumerable.Range(1, 15).Select(i => new ScaleMark(i, 100m - (i - 1) * 5m)).ToImmutableArray(),
            OffScaleReading = 0m,
        };
        var refused = competition.DeclareInstruments(
            [new DeclaredInstrument { Instrument = "nz-f3b-side", Metric = "landingDistance", Scale = f3bSide }],
            "cd", Now);
        refused.IsSuccess.Should().BeFalse("that side of the tape cannot score this class");
        refused.Code.Should().Be("tapeComposition.straddledBand", "the declaration keeps WI-1's stable code");
        refused.Message.Should().Contain("cannot score this class");
        competition.DeclaredInstruments.Should().BeNull("a refused declaration appends no state");
    }

    [Fact]
    public void Declaration_refuses_non_compositional_defects_with_named_codes()
    {
        var scale = NzF3JSideScale();
        DeclaredInstrument Binding(string instrument, string metric, ReadingScale s) =>
            new() { Instrument = instrument, Metric = metric, Scale = s };

        AdoptedCompetition(F3JDefinition).DeclareInstruments(
            [Binding("t1", "landingDistance", scale), Binding("t1", "landingDistance", scale)], "cd", Now)
            .Code.Should().Be("declareInstruments.duplicateInstrument");
        AdoptedCompetition(F3JDefinition).DeclareInstruments(
            [Binding("t1", "noSuchMetric", scale)], "cd", Now)
            .Code.Should().Be("declareInstruments.metricNotDeclared");
        AdoptedCompetition(F3JDefinition).DeclareInstruments(
            [Binding("t1", "touchedByCompetitor", scale)], "cd", Now)
            .Code.Should().Be("declareInstruments.metricNotNumeric", "a tape reads numbers, not flags");
        AdoptedCompetition(F3JDefinition).DeclareInstruments(
            [Binding("t1", "landingDistance", new ReadingScale
            {
                Unit = "m",
                Marks = [new ScaleMark(2m, 90m), new ScaleMark(1m, 95m)],
                OffScaleReading = 0m,
            })], "cd", Now)
            .Code.Should().Be("tapeComposition.tapeNotMonotone", "non-ascending marks are refused");
    }

    [Fact]
    public void Declaration_correction_is_retroactive_by_rederivation_with_reason()
    {
        var competition = AdoptedCompetition(F3JDefinition);
        competition.CorrectInstrumentDeclaration([], "fix", "cd", Now).Code
            .Should().Be("correctInstrumentDeclaration.notDeclared", "a first set is a declaration, not a correction");

        competition = competition.Apply(competition.DeclareInstruments(DeclaredNzF3JSide(), "cd", Now).Value);
        competition.DeclareInstruments(DeclaredNzF3JSide(), "cd", Now).Code
            .Should().Be("declareInstruments.alreadyDeclared", "a second declaration is a correction's job");
        competition.CorrectInstrumentDeclaration([], "", "cd", Now).Code
            .Should().Be("correctInstrumentDeclaration.reasonRequired");

        var corrected = competition.CorrectInstrumentDeclaration([], "the tapes never arrived", "cd", Now);
        corrected.IsSuccess.Should().BeTrue($"{corrected.Code} {corrected.Message}");
        competition = competition.Apply(corrected.Value);
        competition.DeclaredInstruments!.Instruments.Should().BeEmpty("the correction replaces the set whole");
    }

    // ============================================================ WI-3 capture + amendment

    [Fact]
    public void Capture_validates_readings_against_the_named_scale_without_precision()
    {
        var metrics = F3JDefinition.Phases[0].Tasks[0].Metrics;
        var declared = DeclaredNzF3JSide();

        // Every mark plus the off-scale reading captures verbatim.
        var scale = NzF3JSideScale();
        foreach (var reading in scale.ReadingSet)
        {
            var entry = OpenFlightWith();
            var captured = entry.CaptureMeasurement(1, "landingDistance", MeasuredValue.Of(reading), Now,
                metrics, "nz-f3j-side", declared);
            captured.IsSuccess.Should().BeTrue($"reading {reading}: {captured.Code} {captured.Message}");
            var measurement = entry.Apply(captured.Value).Flights[0].Measurements.Single(m => m.Metric == "landingDistance");
            measurement.Value.Number.Should().Be(reading, "a reading is never rounded into the scale");
            measurement.Instrument.Should().Be("nz-f3j-side");
            measurement.EffectiveInstrument.Should().Be("nz-f3j-side");
        }

        // A distance naming none takes the existing path byte for byte —
        // declared precision included.
        var distance = Capture(OpenFlightWith(), "landingDistance", MeasuredValue.Of(0.55m), metrics);
        distance.Flights[0].Measurements.Single(m => m.Metric == "landingDistance").Value.Number
            .Should().Be(0.5m, "F3J landingDistance declares Truncate 0.1");
        distance.Flights[0].Measurements.Single(m => m.Metric == "landingDistance").Instrument.Should().BeNull();
    }

    [Fact]
    public void Capture_refuses_off_scale_undeclared_and_misbound_instruments()
    {
        var metrics = F3JDefinition.Phases[0].Tasks[0].Metrics;
        var declared = DeclaredNzF3JSide();

        OpenFlightWith().CaptureMeasurement(1, "landingDistance", MeasuredValue.Of(87.5m), Now,
                metrics, "nz-f3j-side", declared).Code
            .Should().Be("captureMeasurement.readingNotOnScale");
        OpenFlightWith().CaptureMeasurement(1, "landingDistance", MeasuredValue.Of(101m), Now,
                metrics, "nz-f3j-side", declared).Code
            .Should().Be("captureMeasurement.readingNotOnScale");
        OpenFlightWith().CaptureMeasurement(1, "landingDistance", MeasuredValue.Of(98m), Now,
                metrics, "nz-f3b-side", declared).Code
            .Should().Be("captureMeasurement.instrumentNotDeclared",
                "a measurement names a declared instrument or none, never an arbitrary one");
        OpenFlightWith().CaptureMeasurement(1, "flightTime", MeasuredValue.Of(98m), Now,
                metrics, "nz-f3j-side", declared).Code
            .Should().Be("captureMeasurement.instrumentMetricMismatch",
                "the tape is declared for landingDistance, not flightTime");

        // A second capture is refused as today; changing the value is an
        // explicit amendment.
        var entry = Capture(OpenFlightWith(), "landingDistance", MeasuredValue.Of(98m), metrics,
            "nz-f3j-side", declared);
        entry.CaptureMeasurement(1, "landingDistance", MeasuredValue.Of(96m), Now, metrics, "nz-f3j-side", declared).Code
            .Should().Be("captureMeasurement.alreadyCaptured");
    }

    [Fact]
    public void Amendment_changes_reading_and_instrument_with_history_retained()
    {
        var metrics = F3JDefinition.Phases[0].Tasks[0].Metrics;
        var declared = DeclaredNzF3JSide();
        var entry = Capture(Capture(OpenFlightWith(), "flightTime", MeasuredValue.Of(500m), metrics),
            "landingDistance", MeasuredValue.Of(0.5m), metrics);

        // Distance -> reading: name the instrument, restate the value as a mark.
        var toReading = entry.AmendMeasurement(1, "landingDistance", MeasuredValue.Of(98m),
            "tape freed up", "scorer", Now, metrics, "nz-f3j-side", declared);
        toReading.IsSuccess.Should().BeTrue($"{toReading.Code} {toReading.Message}");
        entry = entry.Apply(toReading.Value);
        entry.Flights[0].Measurements.Single(m => m.Metric == "landingDistance").EffectiveInstrument
            .Should().Be("nz-f3j-side");

        // Reading -> amended reading: the off-scale correction is refused, the
        // on-scale one retained with reason, author and time.
        entry.AmendMeasurement(1, "landingDistance", MeasuredValue.Of(87.5m),
            "misread", "scorer", Now, metrics, "nz-f3j-side", declared).Code
            .Should().Be("amendMeasurement.readingNotOnScale");
        var amended = entry.AmendMeasurement(1, "landingDistance", MeasuredValue.Of(96m),
            "misread the tape", "scorer", Now, metrics, "nz-f3j-side", declared);
        amended.IsSuccess.Should().BeTrue();
        entry = entry.Apply(amended.Value);
        var measurement = entry.Flights[0].Measurements.Single(m => m.Metric == "landingDistance");
        measurement.Value.Number.Should().Be(0.5m, "the original distance survives — append-only");
        measurement.Amendments.Should().HaveCount(2);
        measurement.Amendments[^1].Should().BeEquivalentTo(new Amendment
        {
            NewValue = MeasuredValue.Of(96m),
            Instrument = "nz-f3j-side",
            Reason = "misread the tape",
            By = "scorer",
            At = Now,
        });
    }

    // ============================================================ property (c)

    [Fact]
    public void Property_c_Capture_grain_mixed_forms_coexist_in_one_group()
    {
        // Decision 3's field requirement: one group, some landings read off the
        // declared tape, the rest tape-measured — in any order, both accepted.
        var metrics = F3JDefinition.Phases[0].Tasks[0].Metrics;
        var declared = DeclaredNzF3JSide();

        var readerEntry = Capture(OpenFlightWith(), "landingDistance", MeasuredValue.Of(98m), metrics,
            "nz-f3j-side", declared);
        var measureEntry = Capture(OpenFlightWith(), "landingDistance", MeasuredValue.Of(0.5m), metrics);

        readerEntry.Flights[0].Measurements.Single(m => m.Metric == "landingDistance").EffectiveInstrument
            .Should().Be("nz-f3j-side");
        measureEntry.Flights[0].Measurements.Single(m => m.Metric == "landingDistance").EffectiveInstrument
            .Should().BeNull();
    }

    [Fact]
    public void Reading_scores_identically_whether_read_or_measured()
    {
        // WI-4 landed: composition at resolution (owner decision 5). The same
        // landing scores identically read off the declared tape (reading 98
        // naming nz-f3j-side) or measured with a tape measure (0.5 m naming
        // none) — the tape changes the observation's scale, never the rule.
        var metrics = F3JDefinition.Phases[0].Tasks[0].Metrics;
        var declared = DeclaredNzF3JSide();
        var table = LandingTables().First(t => t.FileName == "50-f3j");
        var task = TaskWith([new ConditionalTerm { When = table.When!, Then = table.Lookup }], metrics);

        Entry ScoreOne(MeasuredValue landing, string? instrument)
        {
            var entry = Capture(OpenFlightWith(), "flightTime", MeasuredValue.Of(500m), metrics);
            entry = Capture(entry, "overflySeconds", MeasuredValue.Of(0m), metrics);
            entry = Capture(entry, "touchedByCompetitor", MeasuredValue.Of(false), metrics);
            entry = Capture(entry, "restedWithin75m", MeasuredValue.Of(true), metrics);
            return Capture(entry, "landingDistance", landing, metrics, instrument, declared);
        }

        var byReading = FlightMetricResolution.InterpretAllFlights(
            ScoreOne(MeasuredValue.Of(98m), "nz-f3j-side"), task, declared).Single();
        var byDistance = FlightMetricResolution.InterpretAllFlights(
            ScoreOne(MeasuredValue.Of(0.5m), null), task, declared).Single();

        byReading.Result.State.Should().Be(FlightResultState.Valid);
        byReading.Score.Should().Be(98m, "reading 98 denotes (0.4, 0.6], awarded 98 by F3J.10.5");
        byDistance.Score.Should().Be(98m);
        // The contribution consumes the READING (decision 4: never a
        // reverse-mapped distance), and reporting keeps value with scale.
        byReading.TermContributions.Single().Value.MetricConsumed.Should().Be(98m);
        byReading.Result.Measurements.Instruments.Should().ContainKey("landingDistance")
            .WhoseValue.Should().Be("nz-f3j-side");
        byDistance.Result.Measurements.Instruments.Should().BeNull();
    }

    [Fact]
    public void Property_c_Eligibility_gates_hold_for_both_forms()
    {
        // Decision 7 at the current grain: the touch/overfly forfeit zeroes the
        // bonus whether the landing was read or measured — composition (WI-4)
        // rewrites only what the LookupTerm is evaluated against, inside the
        // existing conditional.
        var metrics = F3JDefinition.Phases[0].Tasks[0].Metrics;
        var declared = DeclaredNzF3JSide();
        var table = LandingTables().First(t => t.FileName == "50-f3j");
        var task = TaskWith([new ConditionalTerm { When = table.When!, Then = table.Lookup }], metrics);

        foreach (var touched in new[] { false, true })
        {
            Entry ScoreOne(MeasuredValue landing, string? instrument)
            {
                var entry = Capture(OpenFlightWith(), "flightTime", MeasuredValue.Of(500m), metrics);
                entry = Capture(entry, "overflySeconds", MeasuredValue.Of(0m), metrics);
                entry = Capture(entry, "touchedByCompetitor", MeasuredValue.Of(touched), metrics);
                entry = Capture(entry, "restedWithin75m", MeasuredValue.Of(true), metrics);
                return Capture(entry, "landingDistance", landing, metrics, instrument, declared);
            }

            var byReading = FlightMetricResolution.InterpretAllFlights(ScoreOne(MeasuredValue.Of(98m), "nz-f3j-side"), task, declared).Single();
            var byDistance = FlightMetricResolution.InterpretAllFlights(ScoreOne(MeasuredValue.Of(0.5m), null), task, declared).Single();
            if (touched)
            {
                byReading.Score.Should().Be(0m, "touch forfeits the bonus for a reading too");
                byDistance.Score.Should().Be(0m, "touch forfeits the bonus for a distance");
            }
            else
            {
                byDistance.Score.Should().Be(98m);
                byReading.Score.Should().Be(98m, "WI-4: the same landing read off the tape composes to the same award");
            }
        }
    }

    // ============================================================ property (d)

    [Fact]
    public void Property_d_Landing_value_cannot_defeat_eligibility_or_validity()
    {
        var tables = LandingTables().Where(t => t.When is not null).ToImmutableArray();
        tables.Should().NotBeEmpty();

        (from cents in Gen.Int[0, 3000]
         from overfly in Gen.Int[0, 4].Select(i => new decimal[] { 0m, 1m, 30m, 60m, 61m, 90m }[i])
         from flags in Gen.Int[0, 4095]
         select (d: cents / 100m, overfly, flags)).Sample(t =>
         {
             var metrics = BaseMetrics(t.d);
             metrics["overflySeconds"] = MeasuredValue.Of(t.overfly);
             var flagNames = new[]
             {
                 "touchedByCompetitor", "restedWithin75m", "landedInDefinedArea",
                 "landedInLandingArea", "amrtPresetsCorrect", "timingDeviationInFavour",
                 "atRestBy12Min", "lostPart", "touchedBeforeMeasuring",
                 "damagedAndNotSafelyFlyable", "motorRestarted", "airborneAtRoundEnd",
             };
             for (var i = 0; i < flagNames.Length; i++)
                 metrics[flagNames[i]] = MeasuredValue.Of((t.flags & (1 << i)) != 0);

             foreach (var table in tables)
             {
                 var task = TaskWith([new ConditionalTerm { When = table.When!, Then = table.Lookup }],
                     ImmutableArray<MetricDefinition>.Empty);
                 var gate = PredicateEvaluator.Evaluate(table.When!, metrics);
                 var score = FlightInterpreter.Interpret(task, 1, metrics).Score;
                 if (!gate)
                     score.Should().Be(0m, $"{table.FileName}/{table.Stage} gate fails so d={t.d} must contribute nothing");
             }
         });
    }

    [Fact]
    public void Property_d_Flight_validity_gates_hold_regardless_of_landing_value()
    {
        var f3jValidWhen = F3JDefinition.Phases[0].Tasks[0].FlightValidWhen;
        var f5jValidWhen = Corpus.All.First(c => c.FileName == "30-f5j")
            .Definition.Phases[0].Tasks[0].FlightValidWhen;
        f3jValidWhen.Should().NotBeNull();
        f5jValidWhen.Should().NotBeNull();

        (from cents in Gen.Int[0, 2500]
         from overfly in Gen.Int[0, 3].Select(i => new decimal[] { 0m, 60m, 61m, 120m }[i])
         from rested in Gen.Bool
         from heightRecorded in Gen.Bool
         select (d: cents / 100m, overfly, rested, heightRecorded)).Sample(t =>
         {
             foreach (var (validWhen, name) in new[] { (f3jValidWhen!, "F3J"), (f5jValidWhen!, "F5J") })
             {
                  var metrics = BaseMetrics(t.d);
                  metrics["overflySeconds"] = MeasuredValue.Of(t.overfly);
                  metrics["restedWithin75m"] = MeasuredValue.Of(t.rested);
                  metrics["startHeightRecorded"] = MeasuredValue.Of(t.heightRecorded);
                  // WI-1 kanban/in-progress/f5j-christchurch-parallel-run-witness.md:
                  // canonical F5J's flightValidWhen also gates landedWithin75m
                  // (5.5.11.7 d); hold it compliant so this property keeps
                  // sweeping overfly/height-recorded against the landing value.
                  metrics["landedWithin75m"] = MeasuredValue.Of(true);
                 var task = TaskWith([new ConstantTerm { Value = 1m }], ImmutableArray<MetricDefinition>.Empty)
                     with { FlightValidWhen = validWhen };
                 var result = FlightInterpreter.Interpret(task, 1, metrics);
                 if (!PredicateEvaluator.Evaluate(validWhen, metrics))
                     result.Score.Should().Be(0m, $"{name} invalid flight must score 0 whatever d={t.d} is");
                 else
                     result.Score.Should().Be(1m);
             }
         });
    }

    // ============================================================ three-way distinction

    [Fact]
    public void Off_tape_zero_vs_zero_metres_vs_no_measurement_are_three_facts()
    {
        // Decision 8, end to end (WI-4): the off-scale reading composes to no
        // bonus; 0 m captures as a distance (top award through the real
        // pipeline); no measurement pends awaiting landingDistance.
        var metrics = F3JDefinition.Phases[0].Tasks[0].Metrics;
        var declared = DeclaredNzF3JSide();
        var table = LandingTables().First(t => t.FileName == "50-f3j");
        var task = TaskWith([new ConditionalTerm { When = table.When!, Then = table.Lookup }], metrics);

        Entry Base()
        {
            var entry = Capture(OpenFlightWith(), "flightTime", MeasuredValue.Of(500m), metrics);
            entry = Capture(entry, "overflySeconds", MeasuredValue.Of(0m), metrics);
            entry = Capture(entry, "touchedByCompetitor", MeasuredValue.Of(false), metrics);
            return Capture(entry, "restedWithin75m", MeasuredValue.Of(true), metrics);
        }

        var offTapeEntry = Capture(Base(), "landingDistance", MeasuredValue.Of(0m), metrics, "nz-f3j-side", declared);
        offTapeEntry.Flights[0].Measurements.Single(m => m.Metric == "landingDistance").EffectiveInstrument
            .Should().Be("nz-f3j-side", "off-scale is recorded as a reading on the named tape, not a distance");
        FlightMetricResolution.InterpretAllFlights(offTapeEntry, task, declared).Single().Score.Should().Be(0m,
            "the off-scale reading denotes (15, inf): no bonus");

        var spotOn = FlightMetricResolution.InterpretAllFlights(
            Capture(Base(), "landingDistance", MeasuredValue.Of(0m), metrics), task, declared).Single();
        spotOn.Score.Should().Be(100m, "0 m is on the spot: the top award");

        var missing = FlightMetricResolution.InterpretAllFlights(Base(), task, declared).Single();
        missing.Result.State.Should().Be(FlightResultState.Pending);
        missing.Result.Awaited.Should().NotBeNull();
        missing.Result.Awaited!.AwaitedMetric.Should().Be("landingDistance");
    }

    [Fact]
    public void Reading_satisfies_landing_completeness_like_a_distance()
    {
        // Decision 9's implemented half (finding F-WI4-2): a valid reading
        // fulfils the landing input — the flight must not pend waiting for a
        // distance as well.
        var metrics = F3JDefinition.Phases[0].Tasks[0].Metrics;
        var declared = DeclaredNzF3JSide();
        var table = LandingTables().First(t => t.FileName == "50-f3j");
        var task = TaskWith([new ConditionalTerm { When = table.When!, Then = table.Lookup }], metrics);

        var entry = Capture(OpenFlightWith(), "flightTime", MeasuredValue.Of(500m), metrics);
        entry = Capture(entry, "landingDistance", MeasuredValue.Of(98m), metrics, "nz-f3j-side", declared);
        var interpreted = FlightMetricResolution.InterpretAllFlights(entry, task, declared).Single();
        interpreted.Result.State.Should().NotBe(FlightResultState.Pending);
    }

    // ============================================================ lookup behaviour

    [Fact]
    public void Exact_boundaries_and_adjacent_representables_follow_inclusive_upper_bounds()
    {
        var rows = LandingTables().First(t => t.FileName == "50-f3j").Lookup.Rows;
        var bounded = rows.Where(r => r.UpTo is not null).ToArray();
        for (var i = 0; i < bounded.Length; i++)
        {
            var b = bounded[i].UpTo!.Value;
            DirectAward(rows, b).Should().Be(bounded[i].Points, $"exact boundary {b}");
            DirectAward(rows, b - 0.001m).Should().Be(bounded[i].Points, $"just below {b}");
            var next = i + 1 < bounded.Length ? bounded[i + 1].Points : rows[^1].Points;
            DirectAward(rows, b + 0.001m).Should().Be(next, $"just above {b}");
        }
    }

    [Fact]
    public void Unbounded_last_rows_cover_every_distance()
    {
        foreach (var table in LandingTables())
        {
            var last = table.Lookup.Rows[^1];
            DirectAward(table.Lookup.Rows, 1_000_000m).Should().Be(last.Points,
                $"{table.FileName}/{table.TaskCode}/{table.Stage}");
        }
    }

    [Fact]
    public void Scale_authoring_refuses_repeated_readings_non_ascending_and_blank()
    {
        NzF3JSideScale().Should().NotBeNull();

        TapeComposition.Compose(new ReadingScale
        {
            Unit = "m",
            Marks = [new ScaleMark(1m, 90m), new ScaleMark(2m, 90m)],
            OffScaleReading = 0m,
        }, "m", LandingTables().First(t => t.FileName == "50-f3j").Lookup.Rows)
            .Code.Should().Be("tapeComposition.repeatedReading");

        // The SeedData builder makes the same shapes unwritable at authoring.
        Action repeat = () => TapeMarks.UpTo(1m, 5m).Then(2m, 5m).OffTape(0m);
        repeat.Should().Throw<InvalidOperationException>().WithMessage("*distinct*");
        Action collide = () => TapeMarks.UpTo(1m, 0m).OffTape(0m);
        collide.Should().Throw<InvalidOperationException>().WithMessage("*repeats a mark*");
        Action descend = () => TapeMarks.UpTo(2m, 90m).Then(1m, 95m).OffTape(0m);
        descend.Should().Throw<InvalidOperationException>().WithMessage("*ascend*");
    }

    [Fact]
    public void Off_scale_readings_are_not_members_of_the_named_tape()
    {
        var scale = NzF3JSideScale();
        foreach (var offScale in new[] { 101m, -1m, 87.5m, 95.5m, 30.5m })
            scale.ContainsReading(offScale).Should().BeFalse($"reading {offScale} has no mark on the F3J side");
    }
}
