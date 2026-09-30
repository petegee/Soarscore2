using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
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
///
/// The competition-grain extension below pins
/// kanban/backlog/competition-total-floor-and-fai-floor-audit.md WI-5 as
/// reworked by owner decision 2026-09-30: the class-level
/// <c>FloorTotalAtZero</c> datum (FAI F5J 5.5.11.12 n, corpus-wide) floors the
/// post-deduction total through <see cref="ScoringService.ScoreCompetition"/>.
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

    // --------------------------------- competition grain (WI-5 of the floor audit)
    //
    // Through ScoreCompetition with synthetic single-phase classes — the same
    // class-agnostic style as above, with Competition/Entry scaffolding
    // following ScoringServicePropertyTests.BuildCompetitionWithPenalties
    // (AdoptedRules, DrawPhase, DrawAccepted, entries keyed by EntryId,
    // Competition-scoped penalties carrying a CompetitorRef subject). The
    // rate-1 task carries no normalisation, so each round score IS the
    // captured value and the phase aggregate IS the plain sum.

    private static ClassDefinition MakeFlooredClassDefinition(
        TaskDefinition task,
        bool? floorTotal,
        ImmutableArray<PenaltyDefinition> penalties = default,
        ImmutableArray<DropPolicy> drops = default) => new()
    {
        Name = "Synthetic",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = ReflightSelection.Replacement,
            OthersScore = ReflightSelection.BetterOf,
        },
        Penalties = penalties.IsDefault ? [] : penalties,
        FloorTotalAtZero = floorTotal,
        Phases =
        [
            new PhaseDefinition
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new ValidityRule { MinRounds = 1 },
                Drops = drops.IsDefault ? [] : drops,
                Tasks = [task],
            },
        ],
    };

    private static PenaltyDefinition AggregateDeductDef(
        string infractionType, decimal points,
        PenaltyAccrual accrual = PenaltyAccrual.OncePerAttempt) => new()
    {
        InfractionType = infractionType,
        Accrual = accrual,
        Effects = [new PenaltyEffectSpec(PenaltyEffect.DeductPoints, points)],
    };

    /// <summary>
    /// Two competitors, one round per element of <paramref name="subjectRounds"/>
    /// (== <paramref name="otherRounds"/> in length), each round scoring exactly
    /// its captured value. No competition penalties recorded — the caller sets
    /// <c>Competition.Penalties</c> via <c>with</c> once it holds the ids.
    /// </summary>
    private static (Competition Competition, Dictionary<EntryId, Entry> Entries, CompetitorId Subject, CompetitorId Other)
        BuildFlooredCompetition(
            bool? floorTotal,
            decimal[] subjectRounds,
            decimal[] otherRounds,
            ImmutableArray<PenaltyDefinition> penalties = default,
            bool? taskFloor = null,
            ImmutableArray<DropPolicy> drops = default)
    {
        subjectRounds.Length.Should().Be(otherRounds.Length);

        var task = MakeTask(taskFloor);
        var classDefinition = MakeFlooredClassDefinition(task, floorTotal, penalties, drops);

        var adoptedRules = new AdoptedRules
        {
            Definition = classDefinition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = classDefinition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Competition Floor Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);
        var subject = CompetitorId.New();
        var other = CompetitorId.New();
        foreach (var competitor in new[] { subject, other })
        {
            var registered = competition.RegisterCompetitor(competitor, PersonId.New(), Now);
            competition = competition.Apply(registered.Value);
        }

        var drawn = competition.DrawPhase(subjectRounds.Length, [], Now);
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
                    var rounds = competitorRef == subject ? subjectRounds : otherRounds;
                    var opened = competition.OpenEntry(
                        EntryId.New(), 0, round.Ordinal, taskRound.Ordinal, group.Id, competitorRef, ReflightRole.Original, Now);
                    opened.IsSuccess.Should().BeTrue();

                    var entry = Entry.Create(opened.Value).Apply(new FlightOpened(1, Now));
                    var captured = entry.CaptureMeasurement(
                        1, "value", MeasuredValue.Of(rounds[round.Ordinal - 1]), Now, MetricDefs);
                    captured.IsSuccess.Should().BeTrue();
                    entry = entry.Apply(captured.Value);

                    entries[entry.Id] = entry;
                }
            }
        }

        return (competition, entries, subject, other);
    }

    private static CompetitionResult ScoreWholeCompetition(
        Competition competition, Dictionary<EntryId, Entry> entries)
    {
        var result = ScoringService.ScoreCompetition(competition, entries);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    // ------------------------------------------------- (a) non-negative → untouched

    [Fact]
    public void Competition_floor_leaves_a_non_negative_total_minus_penalties_untouched()
    {
        var defs = (ImmutableArray<PenaltyDefinition>)[AggregateDeductDef("safety", 100m)];
        var (competition, entries, subject, other) = BuildFlooredCompetition(
            floorTotal: true, subjectRounds: [500m], otherRounds: [400m], penalties: defs);
        competition = competition with
        {
            Penalties = [new Penalty { InfractionType = "safety", Scope = PenaltyScope.Competition, CompetitorRef = subject }],
        };

        var result = ScoreWholeCompetition(competition, entries);

        // 500 − 100 = 400 on BOTH keys — the floor must not touch it.
        result.Scores[subject.ToString()].Score.Should().Be(400m);
        result.Scores[subject.ToString()].PreDropScore.Should().Be(400m);
        // Someone else's penalty never lands here.
        result.Scores[other.ToString()].Score.Should().Be(400m);
        result.Scores[other.ToString()].PreDropScore.Should().Be(400m);
    }

    // --------------------------------------- (b) floored rounds + penalty → 0

    [Fact]
    public void Competition_floor_records_zero_when_floored_rounds_plus_aggregate_penalty_go_negative()
    {
        // The 5.5.11.12 n shape: the round grain floors −160 → 0 (task datum),
        // so the phase aggregate is 0; the aggregate 100 then takes it to −100
        // and the competition floor records 0 — on BOTH keys.
        var defs = (ImmutableArray<PenaltyDefinition>)[AggregateDeductDef("safety", 100m)];
        var (competition, entries, subject, _) = BuildFlooredCompetition(
            floorTotal: true, subjectRounds: [-160m], otherRounds: [200m],
            penalties: defs, taskFloor: true);
        competition = competition with
        {
            Penalties = [new Penalty { InfractionType = "safety", Scope = PenaltyScope.Competition, CompetitorRef = subject }],
        };

        var result = ScoreWholeCompetition(competition, entries);

        result.Scores[subject.ToString()].Score.Should().Be(0m);
        result.Scores[subject.ToString()].PreDropScore.Should().Be(0m);
    }

    // ------------------------------------------------- (c) no datum → identity

    [Fact]
    public void Without_the_datum_a_negative_total_passes_through()
    {
        // D4's aggregate-grain identity, mirroring the comp-121 pin through
        // ScoreCompetition: 100 − 300 = −200 is recorded as scored — the floor
        // exists ONLY where the class datum states it.
        var defs = (ImmutableArray<PenaltyDefinition>)[AggregateDeductDef("safety", 300m)];
        var (competition, entries, subject, _) = BuildFlooredCompetition(
            floorTotal: null, subjectRounds: [100m], otherRounds: [100m], penalties: defs);
        competition = competition with
        {
            Penalties = [new Penalty { InfractionType = "safety", Scope = PenaltyScope.Competition, CompetitorRef = subject }],
        };

        var result = ScoreWholeCompetition(competition, entries);

        result.Scores[subject.ToString()].Score.Should().Be(-200m);
        result.Scores[subject.ToString()].PreDropScore.Should().Be(-200m);
    }

    // ------------------------------------------------- (d) drop interaction

    [Fact]
    public void Competition_floor_applies_after_drops_with_a_dropped_rounds_penalty_retained()
    {
        // 00-general sec 5: penalties are retained even when their round drops.
        // The TaskRound-scoped 1200 names round 1 — the round the drop discards
        // (500 < 1000) — yet still deducts at the aggregate stage. So the
        // post-drop aggregate is 1000, 1000 − 1200 floors to 0, while the
        // pre-drop 1500 − 1200 = 300 passes the floor untouched: the floor acts
        // AFTER drops + deduction, on each key independently.
        var drops = (ImmutableArray<DropPolicy>)[new DropPolicy
        {
            Dimension = DropDimension.ByRound,
            DropCount = 1,
            ApplyWhenRoundsCompletedAtLeast = 1,
        }];
        var defs = (ImmutableArray<PenaltyDefinition>)[AggregateDeductDef("late", 1200m)];
        var (competition, entries, subject, _) = BuildFlooredCompetition(
            floorTotal: true, subjectRounds: [500m, 1000m], otherRounds: [400m, 400m],
            penalties: defs, drops: drops);
        competition = competition with
        {
            Penalties = [new Penalty
            {
                InfractionType = "late",
                Scope = PenaltyScope.TaskRound,
                CompetitorRef = subject,
                TaskRound = new TaskRoundCoordinate(0, 1, 1),
            }],
        };

        var result = ScoreWholeCompetition(competition, entries);

        // Had the dropped round's penalty been discarded with it, Score would
        // read 1000; had the floor acted before the deduction, −200. It reads 0.
        result.Scores[subject.ToString()].Score.Should().Be(0m);
        result.Scores[subject.ToString()].PreDropScore.Should().Be(300m);
    }

    // ------------------------------------------------- (e) the flag survives

    [Fact]
    public void Competition_floor_preserves_the_disqualified_flag()
    {
        // The floor rewrites the FinalCompetitorScore the flag rides on — the
        // rewrite must not drop it. Mixed-effect definition (the F20 shape):
        // Disqualify flags while DeductPoints 500 takes 100 → −400 → 0.
        var defs = (ImmutableArray<PenaltyDefinition>)[new PenaltyDefinition
        {
            InfractionType = "misconduct",
            Effects =
            [
                new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 500m),
                new PenaltyEffectSpec(PenaltyEffect.Disqualify),
            ],
        }];
        var (competition, entries, subject, other) = BuildFlooredCompetition(
            floorTotal: true, subjectRounds: [100m], otherRounds: [200m], penalties: defs);
        competition = competition with
        {
            Penalties = [new Penalty { InfractionType = "misconduct", Scope = PenaltyScope.Competition, CompetitorRef = subject }],
        };

        var result = ScoreWholeCompetition(competition, entries);

        result.Scores[subject.ToString()].Score.Should().Be(0m);
        result.Scores[subject.ToString()].PreDropScore.Should().Be(0m);
        result.Scores[subject.ToString()].Disqualified.Should().BeTrue();
        result.Scores[other.ToString()].Disqualified.Should().BeFalse();
    }

    // ----------------------------------------------------- the named invariant

    // Invariant (WI-5, property): for any competitor with any round scores and
    // any aggregate penalties, under a class stating the competition floor,
    // the final score is exactly max(0, sum − penalties) and satisfies
    // score ≥ 0 — on BOTH keys (no drops in this class, so both keys share
    // the one oracle).
    [Fact]
    public void Competition_floor_is_exactly_max_of_zero_and_sum_minus_penalties()
    {
        (from values in SignedValue.Array[1, 4]
         from occurrences in Gen.Int[0, 5]
         select (values, occurrences))
        .Sample(t =>
        {
            var defs = (ImmutableArray<PenaltyDefinition>)[AggregateDeductDef("safety", 100m, PenaltyAccrual.PerOccurrence)];
            var otherRounds = Enumerable.Repeat(50m, t.values.Length).ToArray();
            var (competition, entries, subject, _) = BuildFlooredCompetition(
                floorTotal: true, subjectRounds: t.values, otherRounds: otherRounds, penalties: defs);
            competition = competition with
            {
                Penalties = [.. Enumerable.Range(0, t.occurrences).Select(_ => new Penalty
                {
                    InfractionType = "safety",
                    Scope = PenaltyScope.Competition,
                    CompetitorRef = subject,
                })],
            };

            var result = ScoreWholeCompetition(competition, entries);

            var expected = Math.Max(0m, t.values.Sum() - 100m * t.occurrences);
            var row = result.Scores[subject.ToString()];
            return row.Score == expected
                && row.PreDropScore == expected
                && row.Score >= 0m
                && row.PreDropScore >= 0m;
        });
    }

    // Invariant (WI-5, property): the tail-merge pin — two competitors with
    // sum − penalties at a < b ≤ 0 both record 0.
    [Fact]
    public void Competition_floor_merges_the_sub_zero_tail_into_ties_at_zero()
    {
        (from b in Gen.Int[-200_000, 0]
         from delta in Gen.Int[1, 100_000]
         select (a: (b - delta) / 100m, b: b / 100m))
        .Sample(t =>
        {
            var (competition, entries, subject, other) = BuildFlooredCompetition(
                floorTotal: true, subjectRounds: [t.a], otherRounds: [t.b]);

            var result = ScoreWholeCompetition(competition, entries);

            return result.Scores[subject.ToString()].Score == 0m
                && result.Scores[other.ToString()].Score == 0m
                && result.Scores[subject.ToString()].PreDropScore == 0m
                && result.Scores[other.ToString()].PreDropScore == 0m;
        });
    }
}