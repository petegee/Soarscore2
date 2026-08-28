// Example-based facts for the destination-aware scoring law —
// kanban/in-progress/reflight-aggregate-destination.md WI-1. One competitor's
// score may be produced by a task-round other than the round it aggregates
// into (a make-up flight: role Entitled, CountsForRoundOrdinal naming the
// missed round), and each D7 refusal fires on its own shape. Mirrors
// ScoringServiceAnnulmentTests' fixture style: a small synthetic class whose
// task is un-normalised (raw pass-through), so a competitor's total is exactly
// the sum of their collapsed cells and every arithmetic expectation is exact.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

public class ReflightDestinationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [new MetricDefinition { Name = "raw", Kind = MeasuredKind.Number }];

    private static readonly ImmutableArray<ScoreTerm> ScoreTerms =
        [(ScoreTerm)new RateTerm { MetricRef = "raw", Rate = 1 }];

    private static TaskDefinition MakeTask(string code) => new()
    {
        Code = code,
        Name = $"Test task {code}",
        Metrics = MetricDefs,
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = ScoreTerms,
    };

    private static ClassDefinition MakeClassDefinition(
        ImmutableArray<TaskDefinition> tasks,
        ReflightSelection entitled = ReflightSelection.Replacement,
        ReflightSelection others = ReflightSelection.BetterOf,
        ImmutableArray<DropPolicy>? drops = null) => new()
    {
        Name = "Synthetic",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = entitled,
            OthersScore = others,
        },
        Phases =
        [
            new PhaseDefinition
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new ValidityRule { MinRounds = 1 },
                Drops = drops ?? [],
                Tasks = tasks,
            },
        ],
    };

    /// <summary>
    /// Three registered competitors; one Drawn group per round holding all of
    /// them, one task-round (ordinal 1) per round at the given task code.
    /// Returns the competition, the competitors in registration order, and the
    /// hosting group of each round.
    /// </summary>
    private static (Competition Competition, List<CompetitorId> Competitors, IReadOnlyDictionary<int, GroupId> GroupByRound) BuildCompetition(
        IReadOnlyList<(int RoundOrdinal, string TaskCode)> rounds,
        ReflightSelection entitled = ReflightSelection.Replacement,
        ReflightSelection others = ReflightSelection.BetterOf,
        ImmutableArray<DropPolicy>? drops = null)
    {
        var tasks = rounds.Select(r => r.TaskCode).Distinct().Select(MakeTask).ToImmutableArray();
        var adoptedRules = new AdoptedRules
        {
            Definition = MakeClassDefinition(tasks, entitled, others, drops),
            SourceClassId = "content-hash-synthetic",
            SourceVersion = "1.0",
            AdoptedAt = Now,
        };

        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Make-up Destination Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15), "1.0.0", adoptedRules, Now));

        var competitors = new List<CompetitorId>();
        for (var i = 0; i < 3; i++)
        {
            var id = CompetitorId.New();
            competition = competition.Apply(competition.RegisterCompetitor(id, PersonId.New(), Now).Value);
            competitors.Add(id);
        }

        var groupByRound = new Dictionary<int, GroupId>();
        var roundList = ImmutableArray.CreateBuilder<Round>();
        foreach (var (ordinal, taskCode) in rounds)
        {
            var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [.. competitors] };
            groupByRound[ordinal] = group.Id;
            roundList.Add(new Round
            {
                Ordinal = ordinal,
                TaskRounds =
                [
                    new TaskRound
                    {
                        Ordinal = 1,
                        State = Soarscore.Domain.Competitions.TaskRoundState.Drawn,
                        TaskRef = taskCode,
                        Groups = [group],
                    },
                ],
            });
        }

        competition = competition.Apply(new PhaseDrawn(
            0, PhaseType.Preliminary, new Draw { CreatedAt = Now, Status = "drawn" },
            roundList.ToImmutable(), Now));
        // Entries open only against an accepted draw (D4) — arrangement here.
        competition = competition.Apply(new DrawAccepted(0, Now));

        return (competition, competitors, groupByRound);
    }

    private static Entry CaptureEntry(
        Competition competition,
        GroupId group,
        CompetitorId competitor,
        int roundOrdinal,
        decimal raw,
        ReflightRole role = ReflightRole.Original,
        int? countsForRoundOrdinal = null)
    {
        var opened = competition.OpenEntry(
            EntryId.New(), 0, roundOrdinal, 1, group, competitor, role, Now,
            countsForRoundOrdinal,
            countsForRoundOrdinal is null ? null : "make-up for a missed round").Value;
        var entry = Entry.Create(opened).Apply(new FlightOpened(1, Now));
        var captured = entry.CaptureMeasurement(1, "raw", MeasuredValue.Of(raw), Now, MetricDefs);
        captured.IsSuccess.Should().BeTrue();
        return entry.Apply(captured.Value);
    }

    private static Dictionary<EntryId, Entry> Entries(params Entry[] list) => list.ToDictionary(e => e.Id);

    private static decimal Total(Result<CompetitionResult> result, CompetitorId competitor) =>
        result.Value.Scores[competitor.ToString()].Score;

    // ========================================================== the make-up cell

    /// <summary>
    /// The jerilderie-2010 shape in miniature: pilot 29 missed R12 and flew the
    /// make-up inside R13's group. Here the competitor misses round 2 and makes
    /// it up inside round 3. The destination-keyed cell must fill round 2's
    /// ladder slot with the real score, so the ByRound drop walk removes the
    /// genuinely worst round (round 1's 100) — total 400 + 700 = 1100. A
    /// synthesised zero (the pre-story behaviour) would drop round 2's 0 and
    /// total 800; a cell keyed to the hosting round would total 1200.
    /// </summary>
    [Fact]
    public void A_makeup_cell_fills_the_destination_rounds_slot_and_the_drop_walk_consumes_it()
    {
        var (competition, competitors, groups) =
            BuildCompetition([(1, "T"), (2, "T"), (3, "T")], drops:
            [
                new DropPolicy { Dimension = DropDimension.ByRound, DropCount = 1 },
            ]);

        var pilot = competitors[0];
        var entries = Entries(
            CaptureEntry(competition, groups[1], pilot, 1, raw: 100),
            CaptureEntry(competition, groups[3], pilot, 3, raw: 700),
            CaptureEntry(competition, groups[3], pilot, 3, raw: 400,
                ReflightRole.Entitled, countsForRoundOrdinal: 2),
            CaptureEntry(competition, groups[1], competitors[1], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[1], 2, raw: 100),
            CaptureEntry(competition, groups[3], competitors[1], 3, raw: 100),
            CaptureEntry(competition, groups[1], competitors[2], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[2], 2, raw: 100),
            CaptureEntry(competition, groups[3], competitors[2], 3, raw: 100));

        var result = ScoringService.ScoreCompetition(competition, entries);

        result.IsSuccess.Should().BeTrue();
        Total(result, pilot).Should().Be(1100m,
            "the make-up's 400 fills round 2's slot, so the drop removes round 1's real 100");
        Total(result, competitors[1]).Should().Be(200m);
        Total(result, competitors[2]).Should().Be(200m);
    }

    /// <summary>
    /// The comp-135 shape (pilot 128, round 5): an Original plus two make-ups
    /// with distinct destinations in ONE task-round — three live entries, three
    /// legitimate cells, the two destination slots filled. The old two-role
    /// shape law refused this outright.
    /// </summary>
    [Fact]
    public void An_original_plus_two_makeups_in_one_task_round_scores_three_cells()
    {
        var (competition, competitors, groups) = BuildCompetition([(1, "T"), (2, "T"), (3, "T")]);

        var pilot = competitors[0]; // absent from rounds 1 and 2 entirely
        var entries = Entries(
            CaptureEntry(competition, groups[3], pilot, 3, raw: 700),
            CaptureEntry(competition, groups[3], pilot, 3, raw: 225,
                ReflightRole.Entitled, countsForRoundOrdinal: 1),
            CaptureEntry(competition, groups[3], pilot, 3, raw: 299,
                ReflightRole.Entitled, countsForRoundOrdinal: 2),
            CaptureEntry(competition, groups[1], competitors[1], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[1], 2, raw: 100),
            CaptureEntry(competition, groups[3], competitors[1], 3, raw: 100),
            CaptureEntry(competition, groups[1], competitors[2], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[2], 2, raw: 100),
            CaptureEntry(competition, groups[3], competitors[2], 3, raw: 100));

        var result = ScoringService.ScoreCompetition(competition, entries);

        result.IsSuccess.Should().BeTrue();
        Total(result, pilot).Should().Be(700m + 225m + 299m,
            "one cell per destination: his own round 3 slot plus the missed rounds 1 and 2");
        Total(result, competitors[1]).Should().Be(300m);
        Total(result, competitors[2]).Should().Be(300m);
    }

    // ========================================================== D7 refusals

    /// <summary>D7 row 1: the counts-for names a round the phase's structure does not contain.</summary>
    [Fact]
    public void A_counts_for_naming_a_nonexistent_round_fails_unresolved()
    {
        // Round 2 was never drawn: the phase's structure is rounds 1 and 3.
        var (competition, competitors, groups) = BuildCompetition([(1, "T"), (3, "T")]);

        var pilot = competitors[0];
        var entries = Entries(
            CaptureEntry(competition, groups[1], pilot, 1, raw: 100),
            CaptureEntry(competition, groups[3], pilot, 3, raw: 700),
            CaptureEntry(competition, groups[3], pilot, 3, raw: 400,
                ReflightRole.Entitled, countsForRoundOrdinal: 2),
            CaptureEntry(competition, groups[1], competitors[1], 1, raw: 100),
            CaptureEntry(competition, groups[3], competitors[1], 3, raw: 100),
            CaptureEntry(competition, groups[1], competitors[2], 1, raw: 100),
            CaptureEntry(competition, groups[3], competitors[2], 3, raw: 100));

        var result = ScoringService.ScoreCompetition(competition, entries);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightDestinationUnresolved");
    }

    /// <summary>
    /// D7 row 1, trap 8: the destination round exists in the structure but was
    /// not walked — no entries anywhere in it, so the finding-5 filter dropped
    /// it and the cell would vanish silently inside Aggregate.
    /// </summary>
    [Fact]
    public void A_counts_for_naming_an_unflown_round_fails_unresolved()
    {
        var (competition, competitors, groups) = BuildCompetition([(1, "T"), (2, "T"), (3, "T")]);

        var pilot = competitors[0];
        var entries = Entries(
            CaptureEntry(competition, groups[1], pilot, 1, raw: 100),
            CaptureEntry(competition, groups[3], pilot, 3, raw: 700),
            CaptureEntry(competition, groups[3], pilot, 3, raw: 400,
                ReflightRole.Entitled, countsForRoundOrdinal: 2), // nobody flew round 2
            CaptureEntry(competition, groups[1], competitors[1], 1, raw: 100),
            CaptureEntry(competition, groups[3], competitors[1], 3, raw: 100),
            CaptureEntry(competition, groups[1], competitors[2], 1, raw: 100),
            CaptureEntry(competition, groups[3], competitors[2], 3, raw: 100));

        var result = ScoringService.ScoreCompetition(competition, entries);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightDestinationUnresolved");
    }

    /// <summary>
    /// D7 row 2: the destination round's task-round at the make-up's (task
    /// ordinal, task code) does not match the hosting task-round's — the F3B
    /// multi-task shape the story refuses loudly instead of modelling.
    /// </summary>
    [Fact]
    public void A_destination_task_mismatch_fails_loudly()
    {
        // Round 1 flies task B; rounds 2 and 3 fly task A. The make-up flown in
        // round 3 (task A) counts for round 1, whose slot is task B.
        var (competition, competitors, groups) = BuildCompetition([(1, "B"), (2, "A"), (3, "A")]);

        var pilot = competitors[0];
        var entries = Entries(
            CaptureEntry(competition, groups[1], pilot, 1, raw: 100),
            CaptureEntry(competition, groups[3], pilot, 3, raw: 700),
            CaptureEntry(competition, groups[3], pilot, 3, raw: 400,
                ReflightRole.Entitled, countsForRoundOrdinal: 1),
            CaptureEntry(competition, groups[1], competitors[1], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[1], 2, raw: 100),
            CaptureEntry(competition, groups[3], competitors[1], 3, raw: 100),
            CaptureEntry(competition, groups[1], competitors[2], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[2], 2, raw: 100),
            CaptureEntry(competition, groups[3], competitors[2], 3, raw: 100));

        var result = ScoringService.ScoreCompetition(competition, entries);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightDestinationTaskMismatch");
    }

    /// <summary>
    /// D7 row 3 (the D8 shape at score time): the pilot flew the destination
    /// round too, so two walked task-rounds contribute cells for one
    /// (competitor, destination round, task) slot.
    /// </summary>
    [Fact]
    public void A_makeup_for_a_round_the_pilot_also_flew_fails_destination_conflict()
    {
        var (competition, competitors, groups) = BuildCompetition([(1, "T"), (2, "T")]);

        var pilot = competitors[0];
        var entries = Entries(
            CaptureEntry(competition, groups[1], pilot, 1, raw: 100),
            CaptureEntry(competition, groups[2], pilot, 2, raw: 700),
            CaptureEntry(competition, groups[2], pilot, 2, raw: 400,
                ReflightRole.Entitled, countsForRoundOrdinal: 1),
            CaptureEntry(competition, groups[1], competitors[1], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[1], 2, raw: 100),
            CaptureEntry(competition, groups[1], competitors[2], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[2], 2, raw: 100));

        var result = ScoringService.ScoreCompetition(competition, entries);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightDestinationConflict");
    }

    /// <summary>
    /// Two make-ups for ONE destination in one task-round — per destination the
    /// two-role law holds, so a destination holding two reflight-role entries
    /// is shape corruption (D3: refuse loudly, never merge).
    /// </summary>
    [Fact]
    public void Two_makeups_for_one_destination_in_one_task_round_fail_shape()
    {
        var (competition, competitors, groups) = BuildCompetition([(1, "T"), (2, "T")]);

        var pilot = competitors[0];
        var entries = Entries(
            CaptureEntry(competition, groups[2], pilot, 2, raw: 700),
            CaptureEntry(competition, groups[2], pilot, 2, raw: 400,
                ReflightRole.Entitled, countsForRoundOrdinal: 1),
            CaptureEntry(competition, groups[2], pilot, 2, raw: 300,
                ReflightRole.Filler, countsForRoundOrdinal: 1),
            CaptureEntry(competition, groups[1], competitors[1], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[1], 2, raw: 100),
            CaptureEntry(competition, groups[1], competitors[2], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[2], 2, raw: 100));

        var result = ScoringService.ScoreCompetition(competition, entries);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightShapeUnsupported");
    }

    // ========================================================== the ruling

    /// <summary>
    /// A CD ruling is keyed to the hosting task-round and maps to the unique
    /// two-candidate destination: the own-round Original + reflight pair takes
    /// the ruled BetterOf, while the single-candidate make-up destination
    /// passes Select unchanged — the ruling cannot reach it, which is what
    /// makes the ruling's destination never ambiguous (D6).
    /// </summary>
    [Fact]
    public void The_ruling_maps_to_the_unique_two_candidate_destination()
    {
        var (competition, competitors, groups) = BuildCompetition(
            [(1, "T"), (2, "T")],
            entitled: ReflightSelection.UndefinedRequiresRuling,
            others: ReflightSelection.UndefinedRequiresRuling);

        var recorded = competition.RecordReflightRuling(new ReflightRuling
        {
            TaskRound = new TaskRoundCoordinate(0, 2, 1),
            CompetitorRef = competitors[0],
            Selection = ReflightSelection.BetterOf,
            Reason = "Jury ruled the second attempt stands better",
            At = Now,
        });
        recorded.IsSuccess.Should().BeTrue();
        competition = competition.Apply(recorded.Value);

        var pilot = competitors[0];
        var entries = Entries(
            CaptureEntry(competition, groups[2], pilot, 2, raw: 500),
            CaptureEntry(competition, groups[2], pilot, 2, raw: 300, ReflightRole.Entitled),
            CaptureEntry(competition, groups[2], pilot, 2, raw: 800,
                ReflightRole.Entitled, countsForRoundOrdinal: 1),
            CaptureEntry(competition, groups[1], competitors[1], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[1], 2, raw: 100),
            CaptureEntry(competition, groups[1], competitors[2], 1, raw: 100),
            CaptureEntry(competition, groups[2], competitors[2], 2, raw: 100));

        var result = ScoringService.ScoreCompetition(competition, entries);

        result.IsSuccess.Should().BeTrue();
        Total(result, pilot).Should().Be(500m + 800m,
            "the own-round pair collapses per the ruled BetterOf (max(500, 300)); the make-up's 800 stands alone");
    }
}
