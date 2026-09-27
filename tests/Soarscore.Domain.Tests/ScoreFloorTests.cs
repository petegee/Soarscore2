using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// The minimum-score-floor story's pins (kanban/in-progress/minimum-score-floor.md WI-5):
/// a task-round score floors at zero exactly where the task's
/// <see cref="FloorAtZero"/> datum states it — the rulebook-stated route
/// (F5J 5.5.11.12 f; NZ.7.8(c)(iv)), never an engine policy.
///
/// The three facts the story names:
/// (a) a task with the datum floors a negative sum to 0 and leaves a
///     non-negative sum untouched;
/// (b) a task without the datum is byte-identical to today — the negative
///     raw passes through (deferred-decisions D4; comp 121's −2026 oracle);
/// (c) the floor composes with recorded penalties — applied AFTER the raw
///     penalties, so "any penalty points applied in the round will remain
///     effective" (5.5.11.12 f): a penalty that pushed the score to the
///     floor is embodied in the recorded 0, not cancelled.
///
/// Driven through <see cref="ScoringService.ScoreGroup"/> — the grain the
/// floor lives at — with the same synthetic-class style as
/// ScoringServiceZeroRoutingTests (class-agnostic, NFR-1).
/// </summary>
public class ScoreFloorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [new MetricDefinition { Name = "value", Kind = MeasuredKind.Number }];

    private static readonly IReadOnlyDictionary<string, MeasuredValue> EmptyBindings =
        new Dictionary<string, MeasuredValue>();

    /// <summary>One rate-1 term — the raw score IS the captured value.</summary>
    private static TaskDefinition MakeTask(bool? floor)
    {
        return new TaskDefinition
        {
            Code = "T",
            Name = "Test task",
            Metrics = MetricDefs,
            Flights = new LastFlight(),
            Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
            FloorAtZero = floor,
            Score = [new RateTerm { MetricRef = "value", Rate = 1 }],
        };
    }

    private static ClassDefinition MakeClassDefinition(
        TaskDefinition task, ImmutableArray<PenaltyDefinition> penalties = default) => new()
    {
        Name = "Synthetic",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = ReflightSelection.Replacement,
            OthersScore = ReflightSelection.BetterOf,
        },
        Penalties = penalties.IsDefault ? [] : penalties,
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

    private static Entry EntryWithValue(GroupId groupRef, CompetitorId competitorRef, decimal value)
    {
        var entry = Entry.Create(new EntryOpened(
            EntryId.New(), CompetitionId.New(), 0, 1, 1,
            groupRef, competitorRef, ReflightRole.Original, Now)).Apply(new FlightOpened(1, Now));

        var captured = entry.CaptureMeasurement(
            1, "value", MeasuredValue.Of(value), Now, MetricDefs);
        captured.IsSuccess.Should().BeTrue();

        return entry.Apply(captured.Value);
    }

    /// <summary>
    /// A pass-through group (no normalisation — the NDC shape) of two, with
    /// the two competitors' refs returned so assertions read the right row.
    /// </summary>
    private static (GroupResult Group, CompetitorId Subject, CompetitorId Other) ScoreTwo(
        TaskDefinition task, decimal subjectValue, decimal otherValue)
    {
        var classDef = MakeClassDefinition(task);
        var groupRef = GroupId.New();
        var subject = CompetitorId.New();
        var other = CompetitorId.New();

        var entries = ImmutableDictionary<string, Entry>.Empty
            .Add(subject.ToString(), EntryWithValue(groupRef, subject, subjectValue))
            .Add(other.ToString(), EntryWithValue(groupRef, other, otherValue));

        var group = ScoringService.ScoreGroup("group", task, classDef, entries, EmptyBindings);
        return (group, subject, other);
    }

    // ------------------------------------------------------------ (a) the floor

    [Fact]
    public void A_task_with_the_datum_floors_a_negative_task_round_score_to_zero()
    {
        // The story's worked example: 240 s from 300 m scores 240 − 400 = −160
        // at the flight grain; the floored task-round score is the rulebook's
        // recorded 0 (5.5.11.12 f). The rate-1 synthetic reaches the same
        // negative sum through a captured −160.
        var (group, subject, other) = ScoreTwo(
            MakeTask(floor: true), subjectValue: -160m, otherValue: 100m);

        group.Results[subject.ToString()].State.Should().Be(TaskResultState.Valid);
        group.Results[subject.ToString()].RawScore.Should().Be(0m);

        // A non-negative sum is untouched.
        group.Results[other.ToString()].State.Should().Be(TaskResultState.Valid);
        group.Results[other.ToString()].RawScore.Should().Be(100m);

        // PreNormalisationScores preserves what the next stage consumed —
        // the floored zero (owner decision 2026-09-27), not the true negative.
        group.PreNormalisationScores[subject.ToString()].Should().Be(0m);
        group.PreNormalisationScores[other.ToString()].Should().Be(100m);
    }

    // ------------------------------------------------- (b) no datum → identity

    [Fact]
    public void A_task_without_the_datum_passes_the_negative_raw_through_unchanged()
    {
        // D4's identity pass-through, now pinned through ScoreGroup: no
        // FloorAtZero datum → the negative raw is recorded as scored — the
        // comp-121 (−2026) shape. The floor exists ONLY where the datum says.
        var (group, subject, other) = ScoreTwo(MakeTask(floor: null), subjectValue: -160m, otherValue: 100m);

        group.Results[subject.ToString()].RawScore.Should().Be(-160m);
        group.Results[other.ToString()].RawScore.Should().Be(100m);
        group.PreNormalisationScores[subject.ToString()].Should().Be(-160m);
    }

    // -------------------------------------------------- (c) penalty composition

    /// <summary>An entry-scoped flat deduction — the raw-stage penalty shape.</summary>
    private static readonly ImmutableArray<PenaltyDefinition> DeductDefs =
    [
        new PenaltyDefinition
        {
            InfractionType = "wrongLaunchDirection",
            Effects = [new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 100)],
        },
    ];

    private static (GroupResult Group, CompetitorId Subject, CompetitorId Other) ScoreTwoWithPenalty(
        TaskDefinition task, decimal subjectValue, decimal otherValue)
    {
        var classDef = MakeClassDefinition(task, DeductDefs);
        var groupRef = GroupId.New();
        var subject = CompetitorId.New();
        var other = CompetitorId.New();

        var subjectEntry = EntryWithValue(groupRef, subject, subjectValue);
        var recorded = subjectEntry.RecordPenalty(
            new Penalty { InfractionType = "wrongLaunchDirection", Scope = PenaltyScope.Entry },
            classDef.Penalties);
        recorded.IsSuccess.Should().BeTrue();
        subjectEntry = subjectEntry.Apply(recorded.Value);

        var entries = ImmutableDictionary<string, Entry>.Empty
            .Add(subject.ToString(), subjectEntry)
            .Add(other.ToString(), EntryWithValue(groupRef, other, otherValue));

        var group = ScoringService.ScoreGroup("group", task, classDef, entries, EmptyBindings);
        return (group, subject, other);
    }

    [Fact]
    public void The_floor_composes_after_recorded_penalties_in_both_arrival_orders()
    {
        // The floor acts AFTER the raw penalties (5.5.11.12 f "any penalty
        // points applied in the round will remain effective"): a penalty that
        // pushed the score to the floor is embodied in the recorded 0, not
        // cancelled. Subject: score → penalty → floor (−160 − 100 = −260 → 0).
        var (group, subject, other) = ScoreTwoWithPenalty(
            MakeTask(floor: true), subjectValue: -160m, otherValue: 550m);

        group.Results[subject.ToString()].RawScore.Should().Be(0m);

        // The other competitor flies clean — 550, untouched by both the
        // penalty (not theirs) and the floor (non-negative).
        group.Results[other.ToString()].RawScore.Should().Be(550m);
    }

    [Fact]
    public void A_penalty_dragging_a_positive_score_below_zero_also_floors()
    {
        // 50 − 100 = −50 → the floor records 0. Without the datum the
        // PenaltyEngine's own Math.Max(0, …) already floors it — the datum
        // changes nothing here, which is the point: the two floor placements
        // agree.
        var (floored, subject, _) = ScoreTwoWithPenalty(
            MakeTask(floor: true), subjectValue: 50m, otherValue: 0m);
        var (unfloored, bareSubject, _) = ScoreTwoWithPenalty(
            MakeTask(floor: null), subjectValue: 50m, otherValue: 0m);

        floored.Results[subject.ToString()].RawScore.Should().Be(0m);
        unfloored.Results[bareSubject.ToString()].RawScore.Should().Be(0m);
    }

    [Fact]
    public void A_penalty_that_does_not_reach_the_floor_leaves_the_score_alone()
    {
        // 550 − 100 = 450 — the floor must not touch a non-negative result.
        var (group, subject, _) = ScoreTwoWithPenalty(
            MakeTask(floor: true), subjectValue: 550m, otherValue: 0m);

        group.Results[subject.ToString()].RawScore.Should().Be(450m);
    }

    // ------------------------------------------------------------- the flag

    [Fact]
    public void A_normalising_task_with_the_datum_floors_before_the_ratio()
    {
        // The 30-f5j shape (the one normalising seed class that carries the
        // datum): the floor acts at the raw stage, so normalisation consumes
        // the floored zero. With a positive winner the final score is 0
        // either way — the normalised-grain clamp would have caught it — but
        // what normalisation CONSUMED differs, and PreNormalisationScores
        // owes that truth (owner decision 2026-09-27): floored 0 with the
        // datum, the true −160 without it.
        var normalising = MakeTask(floor: true) with
        {
            Normalise = new Normalisation
            {
                Direction = NormalisationDirection.HigherIsBetter,
                WinnerScore = 1000,
            },
        };
        var (withDatum, subject, other) = ScoreTwo(normalising, subjectValue: -160m, otherValue: 100m);
        var (withoutDatum, bareSubject, _) = ScoreTwo(
            MakeTask(floor: null) with
            {
                Normalise = new Normalisation
                {
                    Direction = NormalisationDirection.HigherIsBetter,
                    WinnerScore = 1000,
                },
            },
            subjectValue: -160m, otherValue: 100m);

        withDatum.Results[subject.ToString()].RawScore.Should().Be(0m);
        withDatum.PreNormalisationScores[subject.ToString()].Should().Be(0m);
        withDatum.Results[other.ToString()].RawScore.Should().Be(1000m);

        withoutDatum.Results[bareSubject.ToString()].RawScore.Should().Be(0m);
        withoutDatum.PreNormalisationScores[bareSubject.ToString()].Should().Be(-160m);
    }

    [Fact]
    public void The_floor_preserves_the_disqualified_flag()
    {
        // The floor rewrites the TaskResult the flag rides on (D-B2) — the
        // `with` chain must not drop it.
        ImmutableArray<PenaltyDefinition> disqualifyDefs =
        [
            new PenaltyDefinition
            {
                InfractionType = "nonConformingModel",
                Effects = [new PenaltyEffectSpec(PenaltyEffect.Disqualify)],
            },
        ];
        var task = MakeTask(floor: true);
        var classDef = MakeClassDefinition(task, disqualifyDefs);
        var groupRef = GroupId.New();
        var subject = CompetitorId.New();

        var entry = EntryWithValue(groupRef, subject, -160m);
        var recorded = entry.RecordPenalty(
            new Penalty { InfractionType = "nonConformingModel", Scope = PenaltyScope.Entry },
            classDef.Penalties);
        recorded.IsSuccess.Should().BeTrue();
        entry = entry.Apply(recorded.Value);

        var group = ScoringService.ScoreGroup(
            "group", task, classDef,
            ImmutableDictionary<string, Entry>.Empty.Add(subject.ToString(), entry),
            EmptyBindings);

        group.Results[subject.ToString()].RawScore.Should().Be(0m);
        group.Results[subject.ToString()].Disqualified.Should().BeTrue();
    }

    // ----------------------------------------------------- the named invariant

    // Invariant (WI-5, property): for any task carrying FloorAtZero and any
    // metric inputs, the emitted task-round score equals max(0, the unfloored
    // sum) and satisfies score ≥ 0.
    private static readonly Gen<decimal> SignedValue =
        Gen.Int[-200_000, 200_000].Select(i => i / 100m);

    private static (GroupResult Group, string[] Refs) ScoreField(TaskDefinition task, decimal[] values)
    {
        var classDef = MakeClassDefinition(task);
        var groupRef = GroupId.New();

        var refs = new List<string>();
        var entries = ImmutableDictionary<string, Entry>.Empty;
        for (var i = 0; i < values.Length; i++)
        {
            var competitor = CompetitorId.New();
            refs.Add(competitor.ToString());
            entries = entries.Add(competitor.ToString(), EntryWithValue(groupRef, competitor, values[i]));
        }

        var group = ScoringService.ScoreGroup("group", task, classDef, entries, EmptyBindings);
        return (group, refs.ToArray());
    }

    [Fact]
    public void Floored_task_round_score_is_exactly_max_of_zero_and_the_unfloored_sum()
    {
        (from values in SignedValue.Array[1, 6]
         select values)
        .Sample(values =>
        {
            var (group, refs) = ScoreField(MakeTask(floor: true), values);

            return values.Zip(refs).All(pair =>
            {
                var row = group.Results[pair.Second];
                return row.RawScore == Math.Max(0m, pair.First) && row.RawScore >= 0m;
            });
        });
    }

    // Invariant (WI-5, property): for two competitors whose unfloored sums
    // a < b ≤ 0, both record 0 — the floor merges the sub-zero tail into ties
    // at zero (which is why a drop policy must still select exactly one cell;
    // pinned by the aggregator test below).
    [Fact]
    public void The_floor_merges_the_sub_zero_tail_into_ties_at_zero()
    {
        (from b in Gen.Int[-200_000, 0]
         from delta in Gen.Int[1, 100_000]
         select (a: (b - delta) / 100m, b: b / 100m))
        .Sample(t =>
        {
            var (group, refs) = ScoreField(MakeTask(floor: true), [t.a, t.b]);

            return refs.All(r => group.Results[r].RawScore == 0m);
        });
    }

    // ------------------------------------------------------------ the drop pin

    [Fact]
    public void A_drop_policy_still_selects_exactly_one_cell_when_floored_zeros_tie()
    {
        // Floored zeros are ties, and a class that drops must still drop
        // exactly DropCount cells among them — the tie-break (Latest default,
        // GS parity) selects the LAST of the tied rounds, never zero cells
        // and never two. 85c drops none, so this is pinned on the aggregator
        // directly (PhaseAggregator, the drop mechanism the walk uses).
        var phase = new PhaseDefinition
        {
            Ordinal = 1,
            Type = PhaseType.Preliminary,
            Rounds = new RoundComposition { Kind = CompositionKind.FixedSequence, TasksPerRound = 1 },
            Validity = new ValidityRule { MinRounds = 1 },
            Drops =
            [
                new DropPolicy
                {
                    Dimension = DropDimension.ByRound,
                    DropCount = 1,
                    ApplyWhenRoundsCompletedAtLeast = 1,
                    TieBreak = DropTieBreak.Latest,
                },
            ],
            Tasks = ImmutableArray<TaskDefinition>.Empty,
        };
        var rounds = Enumerable.Range(1, 3).Select(i => new RoundData(i,
            [new TaskRoundData(1, "T", Soarscore.Domain.Scoring.TaskRoundState.Complete)])).ToImmutableArray();
        var allScores = rounds.ToDictionary(
            r => $"r{r.RoundOrdinal}",
            r => new TaskRoundScore("T", r.RoundOrdinal, 1, 0m));

        var result = PhaseAggregator.Aggregate("C1", phase, rounds, allScores);

        result.DroppedScores.Length.Should().Be(1);
        result.DroppedScores[0].RoundOrdinal.Should().Be(3);   // Latest of the tied
        result.Aggregate.Should().Be(0m);
        result.AllScores.Length.Should().Be(3);
    }
}