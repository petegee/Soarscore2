using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Sociable facts for WI-3's read-path fixes
/// (kanban/in-progress/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-3):
/// aggregate-scoped Zero* records route into the task-round stage through
/// <see cref="ScoringService.ScoreGroup"/>'s taskRoundPenalties parameter
/// (D-A1, D-A2), and a coordinate-less aggregate Zero* record is refused
/// loudly rather than zeroing nothing (D-A3). The F3B nonConformingWinch SHAPE
/// (ZeroFlight + DeductPoints 1000 — shape-alike, not the seed class) pins
/// D-A4's exact two-stage behaviour so a refactor cannot silently drop either
/// half. Fixture style mirrors ScoringServicePropertyTests: a small synthetic
/// class, not a corpus one — what is under test is the routing, which is
/// class-agnostic (NFR-1).
/// </summary>
public class ScoringServiceZeroRoutingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] MetricNames = ["alpha", "bravo", "charlie", "delta"];

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [.. MetricNames.Select(n => new MetricDefinition { Name = n, Kind = MeasuredKind.Number })];

    // One RateTerm per metric, rate 1 — the raw score is just the sum of the
    // captured values (100 per multiplier unit), so expected numbers stay
    // readable in the assertions below.
    private static readonly ImmutableArray<ScoreTerm> ScoreTerms =
        [.. MetricNames.Select(n => (ScoreTerm)new RateTerm { MetricRef = n, Rate = 1 })];

    private static TaskDefinition MakeTask() => new()
    {
        Code = "T",
        Name = "Test task",
        Metrics = MetricDefs,
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = ScoreTerms,
        // Winner-finding only happens with normalisation configured
        // (NormalisationEngine step 4) — the winner-finding-exclusion
        // assertions below need a WinnerRef to read.
        Normalise = new Normalisation
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,
        },
    };

    private static ClassDefinition MakeClassDefinition(
        TaskDefinition task, ImmutableArray<PenaltyDefinition> penalties) => new()
    {
        Name = "Synthetic",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = ReflightSelection.Replacement,
            OthersScore = ReflightSelection.BetterOf,
        },
        Penalties = penalties,
        Phases =
        [
            new PhaseDefinition
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new ValidityRule { MinRounds = 1 },
                Tasks = [task],
            },
        ],
    };

    // F3B.2.2 p shape (SeedF3B.cs:164-172): the rule zeroes the flight AND
    // deducts 1000 from the final score — two effects at two points in the
    // pipeline (F20). Shape-alike, not the seed class (D-A4).
    private static readonly ImmutableArray<PenaltyDefinition> NonConformingWinchDefs =
    [
        new PenaltyDefinition
        {
            InfractionType = "nonConformingWinch",
            Effects =
            [
                new PenaltyEffectSpec(PenaltyEffect.ZeroFlight),
                new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 1000),
            ],
        },
    ];

    // A PURE-Zero definition — no DeductPoints co-effect, so its only
    // observable effect is the zeroing itself (keeps the routing facts free
    // of D-A4's two-stage arithmetic).
    private static readonly ImmutableArray<PenaltyDefinition> PureZeroDefs =
    [
        new PenaltyDefinition
        {
            InfractionType = "motorRestart",
            Effects = [new PenaltyEffectSpec(PenaltyEffect.ZeroFlight)],
        },
    ];

    private static readonly IReadOnlyDictionary<string, MeasuredValue> EmptyBindings =
        new Dictionary<string, MeasuredValue>();

    private static decimal ValueFor(string metric, int multiplier) =>
        (Array.IndexOf(MetricNames, metric) + 1) * 10m * multiplier;

    private static Entry CapturedEntry(CompetitorId competitorRef, GroupId groupRef, int multiplier)
    {
        var entry = Entry.Create(new EntryOpened(
            EntryId.New(),
            CompetitionId.New(), 0, 1, 1,
            groupRef, competitorRef, ReflightRole.Original, Now)).Apply(new FlightOpened(1, Now));

        foreach (var metric in MetricNames)
        {
            var captured = entry.CaptureMeasurement(
                1, metric, MeasuredValue.Of(ValueFor(metric, multiplier)), Now, MetricDefs);
            captured.IsSuccess.Should().BeTrue();
            entry = entry.Apply(captured.Value);
        }

        return entry;
    }

    /// <summary>
    /// Two competitors in one group, the subject flying a 400 raw against the
    /// other's 100 — the subject would WIN the group but for the zero, so a
    /// routing failure shows up as a wrong <c>WinnerRef</c>, not just a wrong
    /// number.
    /// </summary>
    private static (ImmutableDictionary<string, Entry> Entries, CompetitorId Subject, CompetitorId Other)
        BuildTwoCompetitorGroup(int subjectMultiplier = 4, int otherMultiplier = 1)
    {
        var groupRef = GroupId.New();
        var subject = CompetitorId.New();
        var other = CompetitorId.New();

        var entries = ImmutableDictionary<string, Entry>.Empty
            .Add(subject.ToString(), CapturedEntry(subject, groupRef, subjectMultiplier))
            .Add(other.ToString(), CapturedEntry(other, groupRef, otherMultiplier));

        return (entries, subject, other);
    }

    /// <summary>
    /// A field of <paramref name="fieldSize"/> competitors, each scoring a
    /// clean 100 aggregate through one drawn round (0/1/1) of the normalising
    /// synthetic task, adopting a class whose penalties are
    /// <paramref name="penalties"/>. Mirrors ScoringServicePropertyTests'
    /// BuildCompetitionWithPenalties.
    /// </summary>
    private static (Competition Competition, Dictionary<EntryId, Entry> Entries, List<CompetitorId> Competitors)
        BuildCompetition(int fieldSize, ImmutableArray<PenaltyDefinition> penalties)
    {
        var task = MakeTask();
        var classDefinition = MakeClassDefinition(task, penalties);

        var adoptedRules = new AdoptedRules
        {
            Definition = classDefinition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = classDefinition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Zero Routing Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);
        var competitorIds = new List<CompetitorId>();

        for (var i = 0; i < fieldSize; i++)
        {
            var id = CompetitorId.New();
            competition = competition.Apply(competition.RegisterCompetitor(id, PersonId.New(), Now).Value);
            competitorIds.Add(id);
        }

        var drawn = competition.DrawPhase(1, [], Now);
        drawn.IsSuccess.Should().BeTrue();
        competition = competition.Apply(drawn.Value);
        // Entries open only against an accepted draw (D4) — arrangement here.
        competition = competition.Apply(new DrawAccepted(0, Now));

        var entries = new Dictionary<EntryId, Entry>();

        foreach (var round in competition.Phases[0].Rounds)
        {
            var taskRound = round.TaskRounds[0];
            foreach (var group in taskRound.Groups)
            {
                foreach (var competitorRef in group.CompetitorRefs)
                {
                    var opened = competition.OpenEntry(
                        EntryId.New(), 0, round.Ordinal, taskRound.Ordinal, group.Id, competitorRef, ReflightRole.Original, Now);
                    opened.IsSuccess.Should().BeTrue();

                    var entry = Entry.Create(opened.Value).Apply(new FlightOpened(1, Now));
                    foreach (var metric in MetricNames)
                    {
                        var captured = entry.CaptureMeasurement(
                            1, metric, MeasuredValue.Of(ValueFor(metric, 1)), Now, MetricDefs);
                        entry = entry.Apply(captured.Value);
                    }

                    entries[entry.Id] = entry;
                }
            }
        }

        return (competition, entries, competitorIds);
    }

    // ------------------------------------------------- D-A1: the routed Zero*

    [Fact]
    public void An_aggregate_scoped_zero_record_zeroes_the_subjects_task_round_through_score_group()
    {
        // D-A1: a TaskRound/Competition-scoped record of a Zero*-carrying
        // definition acts at the task-round stage via the SAME raw-stage engine
        // path an entry-scoped one takes — ScoreGroup's taskRoundPenalties
        // parameter, not a third apply function. Identical semantics pinned:
        // NoResult, Selection null, RawScore 0, excluded from winner-finding.
        var task = MakeTask();
        var classDef = MakeClassDefinition(task, PureZeroDefs);
        var (entries, subject, other) = BuildTwoCompetitorGroup();

        // The map the ScoreCompetition walk builds per D-A2, keyed by
        // stringified CompetitorRef (here built directly — ScoreGroup's
        // parameter is the surface under test).
        var taskRoundPenalties = ImmutableDictionary<string, ImmutableArray<RecordedPenalty>>.Empty
            .Add(subject.ToString(), [new RecordedPenalty("motorRestart", 1)]);

        var group = ScoringService.ScoreGroup(
            "group", task, classDef, entries, EmptyBindings, taskRoundPenalties);

        var subjectResult = group.Results[subject.ToString()];
        subjectResult.State.Should().Be(TaskResultState.NoResult);
        subjectResult.RawScore.Should().Be(0m);
        subjectResult.Selection.Should().BeNull();
        subjectResult.Disqualified.Should().BeFalse();

        // Winner-finding exclusion: the subject flew 400 against 100 — had the
        // routed record silently done nothing, the subject would be the winner.
        group.WinnerRef.Should().Be(other.ToString());

        // The other competitor is untouched by someone else's record.
        var otherResult = group.Results[other.ToString()];
        otherResult.State.Should().Be(TaskResultState.Valid);
        otherResult.Disqualified.Should().BeFalse();
        group.PreNormalisationScores[other.ToString()].Should().Be(100m);
    }

    // ------------------------------------------------- D-A4: the exact shape

    [Fact]
    public void NonConformingWinch_shape_zeroes_the_task_round_and_deducts_1000_at_the_aggregate_stage()
    {
        // D-A4: both halves of one recorded aggregate-scoped infraction act,
        // each exactly once — the ZeroFlight half zeroes the named task-round
        // at the raw stage (part 1), the DeductPoints half deducts flat at the
        // aggregate stage (part 2). The final score of a zeroed round is 0, so
        // final == −1000 pins "deducted exactly once, flat": a dropped
        // deduction would read 0 and a double-counted one −2000.
        var task = MakeTask();
        var classDef = MakeClassDefinition(task, NonConformingWinchDefs);
        var coordinate = new TaskRoundCoordinate(0, 1, 1);
        var (entries, subject, other) = BuildTwoCompetitorGroup();

        // Part 1 — the raw stage, fed through the production routing helper
        // (D-A2) exactly as ScoreCompetition builds it.
        var routed = ScoringService.GetTaskRoundZeroPenalties(
            [new Penalty
            {
                InfractionType = "nonConformingWinch",
                Scope = PenaltyScope.TaskRound,
                CompetitorRef = subject,
                TaskRound = coordinate,
            }],
            classDef,
            coordinate);
        routed.IsSuccess.Should().BeTrue();

        var group = ScoringService.ScoreGroup(
            "group", task, classDef, entries, EmptyBindings, routed.Value);

        var subjectResult = group.Results[subject.ToString()];
        subjectResult.State.Should().Be(TaskResultState.NoResult);
        subjectResult.RawScore.Should().Be(0m);
        subjectResult.Selection.Should().BeNull();
        // The definition carries no Disqualify effect, so the flag must stay
        // down — D-A4's exact shape, not just its zeroing half.
        subjectResult.Disqualified.Should().BeFalse();

        // Part 2 — the full walk: the zeroed round contributes 0 to the
        // aggregate, and the same record's DeductPoints half deducts exactly
        // 1000 flat at the aggregate stage.
        var (competition, walkedEntries, competitors) = BuildCompetition(2, NonConformingWinchDefs);
        competition = competition with
        {
            Penalties =
            [
                new Penalty
                {
                    InfractionType = "nonConformingWinch",
                    Scope = PenaltyScope.TaskRound,
                    CompetitorRef = competitors[0],
                    TaskRound = coordinate,
                },
            ],
        };

        var result = ScoringService.ScoreCompetition(competition, walkedEntries);
        result.IsSuccess.Should().BeTrue();

        // The subject: a zeroed round (total 0) less exactly one 1000 deduction.
        var subjectFinal = result.Value.Scores[competitors[0].ToString()];
        subjectFinal.Score.Should().Be(0m - 1000m);
        subjectFinal.PreDropScore.Should().Be(0m - 1000m);
        subjectFinal.Disqualified.Should().BeFalse();

        // The other competitor: their own 100 normalised to the 1000 winner
        // score, untouched by the subject's record.
        var otherFinal = result.Value.Scores[competitors[1].ToString()];
        otherFinal.Score.Should().Be(1000m);
        otherFinal.Disqualified.Should().BeFalse();
    }

    // ------------------------------------------------- D-A3: the loud refusal

    [Fact]
    public void A_coordinate_less_aggregate_zero_record_fails_scoring_loudly()
    {
        // D-A3 read side: every zeroing clause in the corpus names its round,
        // so a coordinate-less aggregate Zero* record is incomplete data. The
        // write side now rejects it (recordPenalty.zeroEffectRequiresTaskRound);
        // for events already in a log the walk refuses loudly in the D7 house
        // style — never silence, never a silent zero-nothing.
        var (competition, walkedEntries, competitors) = BuildCompetition(2, NonConformingWinchDefs);
        competition = competition with
        {
            Penalties =
            [
                new Penalty
                {
                    InfractionType = "nonConformingWinch",
                    Scope = PenaltyScope.Competition,
                    CompetitorRef = competitors[0],
                    TaskRound = null,
                },
            ],
        };

        var result = ScoringService.ScoreCompetition(competition, walkedEntries);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.zeroEffectUnanchored");

        // The map helper itself refuses the same record — ScoreCompetition's
        // failure surfaces through it (D-A2's routing input, D-A3's check).
        var map = ScoringService.GetTaskRoundZeroPenalties(
            competition.Penalties,
            competition.AdoptedRules.Definition,
            new TaskRoundCoordinate(0, 1, 1));

        map.IsFailure.Should().BeTrue();
        map.Code.Should().Be("score.zeroEffectUnanchored");
    }
}
