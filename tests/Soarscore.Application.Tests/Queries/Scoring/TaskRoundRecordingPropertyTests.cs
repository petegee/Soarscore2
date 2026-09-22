// kanban/completed/entry-completeness-indicator.md WI-3.
//
// Property tests for RecordingCore (internal, Application/Queries/Scoring/
// TaskRoundRecording.cs), driven directly over hand-built aggregate state —
// no store, no clock. Three invariants, named up front per CLAUDE.md:
//
//   P1 — buckets partition expected. For any shape, Expected is exactly the
//        drawn-and-not-withdrawn field, and NotRecorded ⊎
//        RecordedWithoutFlight ⊎ (recorded-and-flown) partitions it disjointly,
//        preserving draw order; nobody outside Expected appears anywhere.
//
//   P2 — gap soundness. Every reported missing-metric list is exactly
//        declared-minus-captured for that flight (hence a subsequence of the
//        declared names, in declared order), reported for live entries of
//        expected competitors only, and a flight with every declared metric
//        captured never appears.
//
//   P3 — the awaited distinction (metric-absence-semantics.md WI-3).
//        AwaitingCapture is exactly the missing metrics the task references
//        and declares no assumed value for — absence on an assumed metric
//        resolves to the assumption, absence on an unreferenced one scoring
//        never reads — while MissingMetrics stays the pure recorded fact, a
//        superset of AwaitingCapture.
//
// This is what no example suite can cover: withdrawal-after-entry,
// annulled-only entries and the reflight double-entry interact
// combinatorially. The generator includes them all, plus noise entries at
// wrong coordinates and a foreign group that the coordinate filter must drop,
// and pads capture masks with false so unrecorded metrics are common rather
// than edge-case.
//
// Non-vacuity: each interesting shape class is counted across the run and
// asserted to have occurred at least once; weakening any oracle makes this
// test fail (checked during this thread).

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests.Queries.Scoring;

public class TaskRoundRecordingPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);

    private const int PhaseOrdinal = 0;
    private const int RoundOrdinal = 1;
    private const int TaskRoundOrdinal = 1;

    private enum Noise { None, WrongGroup, WrongTaskRound }

    private sealed record GenFlight(int Sequence, ImmutableArray<bool> Captured);

    /// <summary>An intended entry before ids exist. <see cref="CompetitorIndex"/> is modulo field size.</summary>
    private sealed record GenEntry(int CompetitorIndex, bool Annulled, Noise Where, ImmutableArray<GenFlight> Flights);

    /// <summary>The generated intent paired with the aggregate state built from it.</summary>
    private sealed record PlacedEntry(GenEntry Spec, Entry Entry);

    private sealed record LiveRow(int Index, List<PlacedEntry> Live);

    /// <summary>Referenced and Assumed are masks over Metrics: which names the
    /// task's predicates/terms read and which declare a whenNotRecorded
    /// assumed value — the two facts AwaitingCapture's distinction is made
    /// from (metric-absence-semantics.md WI-3).</summary>
    private sealed record Shape(
        ImmutableArray<string> Metrics,
        ImmutableArray<bool> Referenced,
        ImmutableArray<bool> Assumed,
        ImmutableArray<bool> Withdrawn,
        ImmutableArray<GenEntry> Entries);

    private sealed record World(
        Competition Competition,
        Group Group,
        ImmutableArray<CompetitorId> CompetitorIds,
        IReadOnlyList<PlacedEntry> Entries)
    {
        public IReadOnlyDictionary<EntryId, Entry> EntriesById =>
            Entries.Select(p => p.Entry).ToDictionary(e => e.Id);
    }

    private static ImmutableArray<bool> PaddedMask(bool[] prefix, int metricCount)
    {
        var mask = prefix.ToImmutableArray();
        while (mask.Length < metricCount)
        {
            mask = mask.Add(false);
        }

        return mask;
    }

    private static Gen<GenFlight> GenFlightFor(int metricCount) =>
        from sequence in Gen.Int[1, 5]
        from prefix in Gen.Bool.Array[0, metricCount]
        select new GenFlight(sequence, PaddedMask(prefix, metricCount));

    private static Gen<GenEntry> GenEntryFor(int metricCount) =>
        from competitorIndex in Gen.Int[0, 7]
        from annulled in Gen.Bool
        from noise in Gen.Int[0, 4]
        from flights in GenFlightFor(metricCount).Array[0, 3]
        select new GenEntry(competitorIndex, annulled, (Noise)(noise / 3), [.. flights]);

    private static readonly Gen<Shape> Shapes =
        from metricCount in Gen.Int[2, 4]
        from referenced in Gen.Bool.Array[0, metricCount]
        from assumed in Gen.Bool.Array[0, metricCount]
        from withdrawn in Gen.Bool.Array[1, 8]
        from entries in GenEntryFor(metricCount).Array[0, 12]
        select new Shape(
            [.. Enumerable.Range(0, metricCount).Select(i => $"m{i}")],
            PaddedMask(referenced, metricCount),
            PaddedMask(assumed, metricCount),
            [.. withdrawn],
            [.. entries]);

    [Fact]
    public void Buckets_partition_expected_and_gaps_are_sound()
    {
        var sawUnflownEntry = false;
        var sawMetricGap = false;
        var sawAwaitedGap = false;
        var sawAssumedGap = false;
        var sawWithdrawnWithEntries = false;
        var sawSoleAnnulledEntry = false;
        var sawReflightDoubleEntry = false;
        var sawNoiseEntry = false;

        Shapes.Sample(shape =>
        {
            var fieldSize = shape.Withdrawn.Length;
            var metricNames = shape.Metrics;
            var world = BuildWorld(shape, fieldSize);
            var entriesById = world.EntriesById;

            // The declared metrics as the class would carry them: an assumed
            // value on the generated assumed subset, none elsewhere — and the
            // referenced set exactly the handler passes from the Domain's walk.
            var declaredMetrics = metricNames
                .Select((name, i) => new MetricDefinition
                {
                    Name = name,
                    Kind = MeasuredKind.Number,
                    WhenNotRecorded = shape.Assumed[i] ? MeasuredValue.Of(true) : null,
                })
                .ToImmutableArray();
            var referencedMetrics = metricNames
                .Where((_, i) => shape.Referenced[i])
                .ToImmutableHashSet(StringComparer.Ordinal);

            // ---- oracle: the bucketing rules restated independently ----------
            var expectedIndexes = Enumerable.Range(0, fieldSize).Where(i => !shape.Withdrawn[i]).ToList();

            bool AtCoordinate(PlacedEntry p) =>
                p.Entry.PhaseOrdinal == PhaseOrdinal
                && p.Entry.RoundOrdinal == RoundOrdinal
                && p.Entry.TaskRoundOrdinal == TaskRoundOrdinal
                && p.Entry.GroupRef == world.Group.Id;

            var liveRows = Enumerable.Range(0, fieldSize)
                .Select(i => new LiveRow(
                    i,
                    world.Entries
                        .Where(p => !p.Spec.Annulled && p.Spec.Where == Noise.None
                                 && p.Spec.CompetitorIndex % fieldSize == i && AtCoordinate(p))
                        .ToList()))
                .ToList();

            foreach (var row in liveRows)
            {
                var live = row.Live;
                if (live.Count > 0 && shape.Withdrawn[row.Index])
                {
                    sawWithdrawnWithEntries = true;
                }

                if (live.Count >= 2)
                {
                    sawReflightDoubleEntry = true;
                }

                if (live.Count == 0 && !shape.Withdrawn[row.Index]
                    && shape.Entries.Any(e => e.CompetitorIndex % fieldSize == row.Index))
                {
                    sawSoleAnnulledEntry = true;
                }

                if (live.Any(e => e.Spec.Flights.Length == 0))
                {
                    sawUnflownEntry = true;
                }
            }

            var recordedOracle = liveRows.Where(x => x.Live.Count > 0).Select(x => x.Index).ToHashSet();
            var flownOracle = liveRows
                .Where(x => x.Live.Any(p => p.Spec.Flights.Length > 0))
                .Select(x => x.Index)
                .ToHashSet();
            var notRecordedOracle = expectedIndexes.Where(i => !recordedOracle.Contains(i)).ToList();
            var withoutFlightOracle = expectedIndexes
                .Where(i => recordedOracle.Contains(i) && !flownOracle.Contains(i))
                .ToList();

            // The gap oracle: declared-minus-captured per flight of every live
            // entry, plus the awaited subset — referenced by the task and
            // declared without an assumption (P3).
            var expectedGaps = new Dictionary<(EntryId, int), (string[] Missing, string[] Awaiting)>();
            foreach (var row in liveRows.Where(x => !shape.Withdrawn[x.Index]))
            {
                foreach (var placed in row.Live)
                {
                    foreach (var flight in placed.Spec.Flights)
                    {
                        var missing = metricNames.Zip(flight.Captured)
                            .Where(pair => !pair.Second)
                            .Select(pair => pair.First)
                            .ToArray();
                        if (missing.Length > 0)
                        {
                            var awaiting = missing
                                .Where(name => shape.Referenced[metricNames.IndexOf(name)]
                                            && !shape.Assumed[metricNames.IndexOf(name)])
                                .ToArray();
                            expectedGaps[(placed.Entry.Id, flight.Sequence)] = (missing, awaiting);
                            sawMetricGap = true;

                            if (awaiting.Length > 0)
                            {
                                sawAwaitedGap = true;
                            }

                            if (missing.Any(name => shape.Assumed[metricNames.IndexOf(name)]))
                            {
                                sawAssumedGap = true;
                            }
                        }
                    }
                }
            }

            foreach (var placed in world.Entries)
            {
                if (placed.Spec.Where != Noise.None)
                {
                    sawNoiseEntry = true;
                }
            }

            // ---- act ---------------------------------------------------------
            // add-recorded-predicate.md WI-3 adds the recordedness set; the
            // generated shapes declare no IsRecorded predicates, so it is empty.
            var view = RecordingCore.ComputeGroupViews(
                world.Competition, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
                [world.Group], entriesById, declaredMetrics, referencedMetrics,
                ImmutableHashSet<string>.Empty);

            // ---- P1: buckets partition expected ------------------------------
            view.Should().ContainSingle();
            var g = view[0];

            g.ExpectedCompetitorRefs.Select(c => world.CompetitorIds.IndexOf(c)).Should().Equal(expectedIndexes);
            g.NotRecordedCompetitorRefs.Select(c => world.CompetitorIds.IndexOf(c)).Should().Equal(notRecordedOracle);
            g.RecordedWithoutFlightCompetitorRefs.Select(c => world.CompetitorIds.IndexOf(c)).Should().Equal(withoutFlightOracle);

            g.ExpectedCompetitorRefs
                .Where(c => !g.NotRecordedCompetitorRefs.Contains(c)
                         && !g.RecordedWithoutFlightCompetitorRefs.Contains(c))
                .Select(c => world.CompetitorIds.IndexOf(c))
                .Should().Equal(expectedIndexes.Where(flownOracle.Contains));

            g.ExpectedCompetitorRefs.Should().OnlyHaveUniqueItems();
            g.NotRecordedCompetitorRefs.Should().OnlyHaveUniqueItems();
            g.RecordedWithoutFlightCompetitorRefs.Should().OnlyHaveUniqueItems();

            // ---- P2/P3: gap soundness and the awaited distinction ------------
            var expectedIds = g.ExpectedCompetitorRefs.ToHashSet();
            var actualGaps = new Dictionary<(EntryId, int), FlightGapsView>();
            foreach (var entryGaps in g.MetricGaps)
            {
                expectedIds.Should().Contain(entryGaps.CompetitorRef);
                foreach (var flightGaps in entryGaps.Flights)
                {
                    actualGaps[(entryGaps.EntryRef, flightGaps.Sequence)] = flightGaps;
                }
            }

            actualGaps.Should().HaveSameCount(expectedGaps);
            foreach (var (key, expected) in expectedGaps)
            {
                actualGaps[key].MissingMetrics.Should().Equal(expected.Missing); // exact set AND declared order
                actualGaps[key].AwaitingCapture.Should().Equal(expected.Awaiting);
            }
        });

        sawUnflownEntry.Should().BeTrue("the generator should produce recorded-but-unflown entries");
        sawMetricGap.Should().BeTrue("the generator should produce flights with missing metrics");
        sawAwaitedGap.Should().BeTrue("the generator should produce flights missing referenced unassumed metrics");
        sawAssumedGap.Should().BeTrue("the generator should produce flights missing assumed metrics");
        sawWithdrawnWithEntries.Should().BeTrue("the generator should produce withdrawals after entries were opened");
        sawSoleAnnulledEntry.Should().BeTrue("the generator should produce competitors whose only entry is annulled");
        sawReflightDoubleEntry.Should().BeTrue("the generator should produce competitors holding two live entries");
        sawNoiseEntry.Should().BeTrue("the generator should produce entries at wrong coordinates/groups");
    }

    // --------------------------------------- the recordedness carve-out (story WI-3 item 3)

    /// <summary>
    /// WI-5 of kanban/in-progress/add-recorded-predicate.md, the F5J-NDC-shaped
    /// case through the real handler inputs: the referenced and recordedness
    /// sets come from the Domain's walks over the real NDC F5J task D
    /// (85c-nz-f5j-ndc), exactly as GetTaskRoundRecordingHandler passes them. A
    /// flight with only flightTime recorded: blank startHeight sits under
    /// MissingMetrics — the column stays demanded (blank ⇒ zero, typed ⇒
    /// valid, one input) — and never under AwaitingCapture, because its absence
    /// is the gate's false (5.5.11.7 e), not a capture gap; landingDistance, an
    /// ordinary awaited metric, keeps its awaiting fact untouched, and the
    /// assumed exceptions stay out of AwaitingCapture as they always were.
    /// The generated property above covers the general masks; this pins the
    /// real seed's shape.
    /// </summary>
    [Fact]
    public void Recordedness_gate_facts_show_under_missing_metrics_and_never_under_awaiting_capture()
    {
        var taskDefinition = SeedF5jNdc.Definition.Phases.SelectMany(p => p.Tasks).Single(t => t.Code == "D");
        var referencedMetrics = FlightMetricResolution.ReferencedMetrics(taskDefinition);
        var isRecordedReferencedMetrics = FlightMetricResolution.IsRecordedReferencedMetrics(taskDefinition);

        // The handler inputs this test proves the view honours.
        isRecordedReferencedMetrics.Should().ContainSingle().Which.Should().Be("startHeight",
            "the recordedness walk is the story's WI-1 output — startHeight's only absence path is the gate");
        referencedMetrics.Should().Contain("landingDistance",
            "landingDistance is the ordinary awaited metric the view must keep reporting");

        var competitorId = CompetitorId.New();
        var competition = new Competition
        {
            Id = CompetitionId.New(),
            Name = "Recordedness view",
            Location = "Nowhere",
            StartDate = new DateOnly(2026, 9, 22),
            EndDate = new DateOnly(2026, 9, 22),
            EvaluatorVersion = "1.0.0",
            Competitors =
            [
                new Competitor
                {
                    Id = competitorId,
                    PersonRef = PersonId.New(),
                    CompetitorNumber = 1,
                    RegisteredAt = Now,
                },
            ],
            Phases = [],
            AdoptedRules = new AdoptedRules
            {
                Definition = SeedF5jNdc.Definition,
                SourceClassId = "content-hash-recordedness-view",
                SourceVersion = SeedF5jNdc.Definition.Version!,
                AdoptedAt = Now,
            },
        };

        var groupRef = GroupId.New();
        var group = new Group { Id = groupRef, Ordinal = 1, CompetitorRefs = [competitorId] };

        var entry = new Entry
        {
            Id = EntryId.New(),
            CompetitionRef = competition.Id,
            PhaseOrdinal = PhaseOrdinal,
            RoundOrdinal = RoundOrdinal,
            TaskRoundOrdinal = TaskRoundOrdinal,
            GroupRef = groupRef,
            CompetitorRef = competitorId,
            Role = ReflightRole.Original,
            Flights =
            [
                new Flight
                {
                    Sequence = 1,
                    Measurements =
                    [
                        new Measurement
                        {
                            Metric = "flightTime",
                            Value = MeasuredValue.Of(600m),
                            CapturedAt = Now,
                        },
                    ],
                },
            ],
        };

        var view = RecordingCore.ComputeGroupViews(
            competition, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            [group], new Dictionary<EntryId, Entry> { [entry.Id] = entry },
            taskDefinition.Metrics, referencedMetrics, isRecordedReferencedMetrics);

        view.Should().ContainSingle();
        var gaps = view[0].MetricGaps.Should().ContainSingle().Which.Flights.Should().ContainSingle().Which;

        gaps.Sequence.Should().Be(1);
        gaps.MissingMetrics.Should().Equal(
            new[] { "startHeight", "landingDistance", "overflySeconds", "touchedByCompetitor", "landedWithin75m" },
            "MissingMetrics is the pure recorded fact, in declared order — the startHeight column stays demanded");
        gaps.AwaitingCapture.Should().Equal(new[] { "landingDistance" },
            "startHeight's absence is the gate's false (5.5.11.7 e) — a fact, never an await; landingDistance is the ordinary awaited metric, untouched");
    }

    // ------------------------------------------------------------------ world

    /// <summary>
    /// Builds the aggregate state RecordingCore consumes, keeping each
    /// generated spec beside the Entry minted from it so the oracle can key
    /// gaps by real ids without any index bookkeeping.
    /// </summary>
    private static World BuildWorld(Shape shape, int fieldSize)
    {
        var competitorIds = Enumerable.Range(0, fieldSize)
            .Select(i => new Competitor
            {
                Id = CompetitorId.New(),
                PersonRef = PersonId.New(),
                CompetitorNumber = i + 1,
                RegisteredAt = Now,
                WithdrawnAt = shape.Withdrawn[i] ? Now : null,
            })
            .ToList();
        var ids = competitorIds.Select(c => c.Id).ToImmutableArray();

        var competition = new Competition
        {
            Id = CompetitionId.New(),
            Name = "Recording Property Test",
            Location = "Nowhere",
            StartDate = new DateOnly(2026, 8, 24),
            EndDate = new DateOnly(2026, 8, 25),
            EvaluatorVersion = "1.0.0",
            Competitors = [.. competitorIds],
            Phases = [],
            AdoptedRules = new AdoptedRules
            {
                Definition = SeedF3K.Definition,
                SourceClassId = "content-hash-recording",
                SourceVersion = SeedF3K.Definition.Version!,
                AdoptedAt = Now,
            },
        };

        var groupRef = GroupId.New();
        var group = new Group { Id = groupRef, Ordinal = 1, CompetitorRefs = ids };

        var placed = shape.Entries
            .Select(spec =>
            {
                var entryId = EntryId.New();
                var taskRoundOrdinal = spec.Where == Noise.WrongTaskRound ? 9 : TaskRoundOrdinal;
                var entryGroupRef = spec.Where == Noise.WrongGroup ? GroupId.New() : groupRef;

                return new PlacedEntry(spec, new Entry
                {
                    Id = entryId,
                    CompetitionRef = competition.Id,
                    PhaseOrdinal = PhaseOrdinal,
                    RoundOrdinal = RoundOrdinal,
                    TaskRoundOrdinal = taskRoundOrdinal,
                    GroupRef = entryGroupRef,
                    CompetitorRef = ids[spec.CompetitorIndex % fieldSize],
                    Role = ReflightRole.Original,
                    Annulment = spec.Annulled ? new Annulment { Reason = "test", By = "tester", At = Now } : null,
                    Flights = [.. spec.Flights.Select(f => new Flight
                    {
                        Sequence = f.Sequence,
                        Measurements = [.. shape.Metrics.Zip(f.Captured)
                            .Where(pair => pair.Second)
                            .Select(pair => new Measurement
                            {
                                Metric = pair.First,
                                Value = MeasuredValue.Of(1m),
                                CapturedAt = Now,
                            })],
                    })],
                });
            })
            .ToList();

        return new World(competition, group, ids, placed);
    }
}
