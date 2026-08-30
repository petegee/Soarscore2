using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property tests for <see cref="ScoringService"/>'s two orchestration-level
/// invariants — WI-5 invariants 2 and 3 (kanban/completed/scoring-steel-thread-plan.md).
/// A small synthetic class/task, not a corpus one: these invariants are about
/// ScoringService's own orchestration (fold-order independence, purity), not
/// about any particular class's rules, so a minimal fixture is what isolates
/// them — ScoringCorpusPropertyTests (invariant 7) is where the real corpus
/// classes get exercised.
/// </summary>
public class ScoringServicePropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] MetricNames = ["alpha", "bravo", "charlie", "delta"];

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [.. MetricNames.Select(n => new MetricDefinition { Name = n, Kind = MeasuredKind.Number })];

    // One RateTerm per metric, rate 1 — the raw score is just the sum of the
    // captured values, so a fold-order bug would show up directly as a wrong
    // total rather than needing a more elaborate oracle.
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
    };

    private static ClassDefinition MakeClassDefinition(TaskDefinition task) => new()
    {
        Name = "Synthetic",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = ReflightSelection.Replacement,
            OthersScore = ReflightSelection.BetterOf,
        },
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

    private static readonly IReadOnlyDictionary<string, MeasuredValue> EmptyBindings =
        new Dictionary<string, MeasuredValue>();

    private static Entry OpenSampleEntry(CompetitorId competitorRef, GroupId groupRef) =>
        Entry.Create(new EntryOpened(
            EntryId.New(),
            CompetitionId.New(), 0, 1, 1,
            groupRef, competitorRef, ReflightRole.Original, Now));

    private static decimal ValueFor(string metric) => (Array.IndexOf(MetricNames, metric) + 1) * 10m;

    // ============================================================ invariant 2

    /// <summary>
    /// Scoring is order-invariant over distinct metrics: folding the same set
    /// of MeasurementCaptured events for an Entry, in any permutation, yields
    /// an identical GroupResult through ScoreGroup — the property that says
    /// scoring reads the fold (the final Entry state), not the event order.
    /// </summary>
    [Fact]
    public void Scoring_is_order_invariant_over_distinct_metrics()
    {
        (from permutationA in Gen.Shuffle(MetricNames)
         from permutationB in Gen.Shuffle(MetricNames)
         select (permutationA, permutationB))
        .Sample(t =>
        {
            var resultA = ScoreWithCaptureOrder(t.permutationA);
            var resultB = ScoreWithCaptureOrder(t.permutationB);

            resultA.State.Should().Be(resultB.State);
            resultA.RawScore.Should().Be(resultB.RawScore);
        });
    }

    private static TaskResult ScoreWithCaptureOrder(string[] captureOrder)
    {
        var groupRef = GroupId.New();
        var competitorRef = CompetitorId.New();

        var entry = OpenSampleEntry(competitorRef, groupRef)
            .Apply(new FlightOpened(1, Now));

        foreach (var metric in captureOrder)
        {
            var captured = entry.CaptureMeasurement(1, metric, MeasuredValue.Of(ValueFor(metric)), Now, MetricDefs);
            captured.IsSuccess.Should().BeTrue();
            entry = entry.Apply(captured.Value);
        }

        var task = MakeTask();
        var classDef = MakeClassDefinition(task);
        var entries = ImmutableDictionary<string, Entry>.Empty.Add(competitorRef.ToString(), entry);

        var group = ScoringService.ScoreGroup(groupRef.ToString(), task, classDef, entries, EmptyBindings);
        return group.Results[competitorRef.ToString()];
    }

    // ============================================================ invariant 3

    /// <summary>
    /// Scoring is a pure function: scoring the same Competition + entries
    /// twice through ScoreCompetition yields equal results. This is what
    /// makes LADR-0001 §3's "results are derived, never stored" safe — if
    /// scoring were not pure, re-deriving a leaderboard on demand could
    /// silently drift between reads of the same event log.
    /// </summary>
    [Fact]
    public void Scoring_is_a_pure_function()
    {
        (from fieldSize in Gen.Int[1, 8]
         from rounds in Gen.Int[1, 2]
         select (fieldSize, rounds))
        .Sample(t =>
        {
            var (competition, entries) = BuildScoredCompetition(t.fieldSize, t.rounds);

            var first = ScoringService.ScoreCompetition(competition, entries);
            var second = ScoringService.ScoreCompetition(competition, entries);

            first.IsSuccess.Should().BeTrue();
            second.IsSuccess.Should().BeTrue();
            second.Value.Scores.Should().BeEquivalentTo(first.Value.Scores);
            second.Value.Placings.Should().BeEquivalentTo(first.Value.Placings);
        });
    }

    private static (Competition Competition, Dictionary<EntryId, Entry> Entries) BuildScoredCompetition(
        int fieldSize, int rounds)
    {
        var task = MakeTask();
        var classDefinition = MakeClassDefinition(task);

        var adoptedRules = new AdoptedRules
        {
            Definition = classDefinition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = classDefinition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Purity Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);

        for (var i = 0; i < fieldSize; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now);
            competition = competition.Apply(registered.Value);
        }

        // task.Group is null (whole-field, one group), so DrawPhase needs no
        // parameter binding — see Competition.DrawPhase's minPerGroup default.
        var drawn = competition.DrawPhase(rounds, [], Now);
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
                        var captured = entry.CaptureMeasurement(1, metric, MeasuredValue.Of(ValueFor(metric)), Now, MetricDefs);
                        entry = entry.Apply(captured.Value);
                    }

                    entries[entry.Id] = entry;
                }
            }
        }

        return (competition, entries);
    }

    // ============================================================ WI-5 aggregate penalties
    // Subject-filtered aggregate penalties — kanban/in-progress/annul-and-penalise-the-second-entry-thread.md
    // WI-5. Before the Penalty payload gained a CompetitorRef, every
    // TaskRound/Competition-scoped penalty deducted from every competitor in
    // the field (finding 2). These facts and P4 pin the fix: an aggregate
    // penalty lands on its subject alone.

    private static readonly ImmutableArray<PenaltyDefinition> AggregatePenaltyDefs =
    [
        new() { InfractionType = "safetyZone", Effects = [new(PenaltyEffect.DeductPoints, 300)] },
        new() { InfractionType = "disqualify", Effects = [new(PenaltyEffect.Disqualify)] },
    ];

    private static readonly Gen<(string InfractionType, int SubjectIndex)> AggregatePenaltyFactGen =
        from infractionType in Gen.OneOfConst("safetyZone", "disqualify")
        from subjectIndex in Gen.Int[0, 2]
        select (infractionType, subjectIndex);

    /// <summary>
    /// A field of <paramref name="fieldSize"/> competitors, each scoring a clean
    /// 100 aggregate (the synthetic task's raw == sum of metric values), adopting
    /// a class whose penalties are <paramref name="penalties"/>. Starts with no
    /// recorded competition penalties — the caller sets <c>Competition.Penalties</c>
    /// via <c>with</c> once it knows the competitor ids (returned in registration
    /// == draw order).
    /// </summary>
    private static (Competition Competition, Dictionary<EntryId, Entry> Entries, List<CompetitorId> Competitors)
        BuildCompetitionWithPenalties(
            int fieldSize,
            ImmutableArray<PenaltyDefinition> penalties)
    {
        var task = MakeTask();
        var classDefinition = MakeClassDefinition(task) with { Penalties = penalties };

        var adoptedRules = new AdoptedRules
        {
            Definition = classDefinition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = classDefinition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Aggregate Penalty Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);
        var competitorIds = new List<CompetitorId>();

        for (var i = 0; i < fieldSize; i++)
        {
            var id = CompetitorId.New();
            var registered = competition.RegisterCompetitor(id, PersonId.New(), Now);
            competition = competition.Apply(registered.Value);
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
                        var captured = entry.CaptureMeasurement(1, metric, MeasuredValue.Of(ValueFor(metric)), Now, MetricDefs);
                        entry = entry.Apply(captured.Value);
                    }

                    entries[entry.Id] = entry;
                }
            }
        }

        return (competition, entries, competitorIds);
    }

    private static CompetitionResult Score(Competition competition, Dictionary<EntryId, Entry> entries)
    {
        var result = ScoringService.ScoreCompetition(competition, entries);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void A_deduct_points_penalty_lands_on_its_subject_only()
    {
        var (competition, entries, competitors) = BuildCompetitionWithPenalties(2, AggregatePenaltyDefs);
        competition = competition with
        {
            Penalties = [new Penalty { InfractionType = "safetyZone", Scope = PenaltyScope.Competition, CompetitorRef = competitors[0] }],
        };

        var result = Score(competition, entries);

        result.Scores[competitors[0].ToString()].Score.Should().Be(100m - 300m);
        result.Scores[competitors[0].ToString()].Disqualified.Should().BeFalse();
        // The other competitor's total is untouched: 100, not -200.
        result.Scores[competitors[1].ToString()].Score.Should().Be(100m);
        result.Scores[competitors[1].ToString()].Disqualified.Should().BeFalse();
    }

    [Fact]
    public void A_disqualify_penalty_flags_only_its_subject()
    {
        var (competition, entries, competitors) = BuildCompetitionWithPenalties(2, AggregatePenaltyDefs);
        competition = competition with
        {
            Penalties = [new Penalty { InfractionType = "disqualify", Scope = PenaltyScope.Competition, CompetitorRef = competitors[0] }],
        };

        var result = Score(competition, entries);

        result.Scores[competitors[0].ToString()].Disqualified.Should().BeTrue();
        result.Scores[competitors[1].ToString()].Disqualified.Should().BeFalse();
    }

    // ------------------------------------------------------- P4
    // Subject isolation (partition invariance): for any set of competition-stream
    // penalties with arbitrary subjects, each competitor's aggregate deduction is
    // identical to the deduction computed from that competitor's own penalties
    // alone. The invariant decision 1 exists to make true — before the subject
    // filter, every penalty hit every competitor (finding 2).

    [Fact]
    public void Aggregate_penalties_are_subject_isolated()
    {
        AggregatePenaltyFactGen.Array[1, 6].Sample(facts =>
        {
            var (competition, entries, competitors) = BuildCompetitionWithPenalties(3, AggregatePenaltyDefs);

            // Fold the generated penalties onto the competition aggregate.
            var penalties = ImmutableArray.CreateBuilder<Penalty>(facts.Length);
            foreach (var (infractionType, subjectIndex) in facts)
            {
                penalties.Add(new Penalty
                {
                    InfractionType = infractionType,
                    Scope = PenaltyScope.Competition,
                    CompetitorRef = competitors[subjectIndex],
                });
            }

            competition = competition with { Penalties = penalties.ToImmutable() };
            var result = Score(competition, entries);

            // Partition oracle: for each competitor, the deduction is exactly what
            // their own penalties alone would produce.
            for (var i = 0; i < competitors.Count; i++)
            {
                var subject = competitors[i];
                var ownPenalties = penalties
                    .Where(p => p.CompetitorRef == subject)
                    .GroupBy(p => p.InfractionType)
                    .Select(g => new RecordedPenalty(g.Key, g.Count()))
                    .ToImmutableArray();

                var expected = PenaltyEngine.ApplyAggregatePenalties(100m, ownPenalties, AggregatePenaltyDefs);

                var score = result.Scores[subject.ToString()];
                score.Score.Should().Be(100m - expected.Deduction);
                score.Disqualified.Should().Be(expected.Disqualified);
            }
        });
    }

    // ============================================================ WI-3 properties
    // P-ZeroRoutingEquivalence —
    // kanban/in-progress/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-3,
    // invariant named at planning. The definition used below is deliberately
    // non-grouped (D-B4): adoption check 16 admits only all-DeductPoints
    // definitions into exclusion groups
    // (ClassDefinitionValidation.CheckExclusionGroupsAreDeductOnly), so
    // exclusion suppression can never hide a Disqualify flag and this
    // property exercises the un-suppressible shape.
    //
    // P-FlagOrAccumulation (the story's other WI-3 property) is below. It
    // found the one wiring defect of WI-2 as first landed — ScoreGroup step 2c
    // unpacked only applied.Result, dropping RawPenaltyApplication.Disqualified,
    // so an entry-scoped Disqualify reached FinalCompetitorScore.Disqualified
    // as false — fixed in-story by threading the flag at the unpack (D-B1).

    // ------------------------------------------------- P-ZeroRoutingEquivalence

    // D-A4: a PURE-Zero definition — no DeductPoints co-effect. A DeductPoints
    // half acts at the aggregate stage for aggregate-scoped records but at the
    // raw stage for entry-scoped ones, so mixed definitions are NOT
    // scope-equivalent by design; the equivalence property must not use one.
    private static readonly ImmutableArray<PenaltyDefinition> PureZeroDefs =
    [
        new PenaltyDefinition
        {
            InfractionType = "motorRestart",
            Effects = [new PenaltyEffectSpec(PenaltyEffect.ZeroFlight)],
        },
    ];

    // Winner-finding only happens with normalisation configured
    // (NormalisationEngine step 4) — the winner-finding-exclusion half of the
    // property below needs a WinnerRef to read.
    private static TaskDefinition MakeNormalisingTask() => MakeTask() with
    {
        Normalise = new Normalisation
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,
        },
    };

    /// <summary>
    /// P-ZeroRoutingEquivalence
    /// (kanban/in-progress/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-3):
    /// a Zero* infraction recorded at TaskRound scope naming coordinate (0,1,1)
    /// produces, for the subject, the same observable task-round outcome —
    /// NoResult state, RawScore 0, Selection null, excluded from
    /// winner-finding — as the identical infraction recorded entry-scoped on an
    /// entry in that task-round: the two scopes are routes to ONE engine path
    /// (D-A1), never two behaviours. The subject flies the better raw, so a
    /// silent routing failure would flip the group winner to the subject, not
    /// merely move a number.
    /// </summary>
    [Fact]
    public void P_ZeroRoutingEquivalence_task_round_scoped_and_entry_scoped_zero_records_produce_one_task_round_outcome()
    {
        (from occurrences in Gen.Int[1, 3]
         from subjectMultiplier in Gen.Int[2, 4]
         select (occurrences, subjectMultiplier))
        .Sample(t =>
        {
            var task = MakeNormalisingTask();
            var classDef = MakeClassDefinition(task) with { Penalties = PureZeroDefs };
            var coordinate = new TaskRoundCoordinate(0, 1, 1);
            var groupRef = GroupId.New();
            var subject = CompetitorId.New();
            var other = CompetitorId.New();

            // Route A: the record sits at TaskRound scope naming (0,1,1),
            // routed by the production helper into ScoreGroup's map (D-A2).
            var routed = ScoringService.GetTaskRoundZeroPenalties(
                [.. Enumerable.Range(0, t.occurrences).Select(_ => new Penalty
                {
                    InfractionType = "motorRestart",
                    Scope = PenaltyScope.TaskRound,
                    CompetitorRef = subject,
                    TaskRound = coordinate,
                })],
                classDef,
                coordinate);
            routed.IsSuccess.Should().BeTrue();

            var cleanEntries = ImmutableDictionary<string, Entry>.Empty
                .Add(subject.ToString(), CapturedEntryFor(subject, t.subjectMultiplier))
                .Add(other.ToString(), CapturedEntryFor(other, 1));

            var viaTaskRound = ScoringService.ScoreGroup(
                groupRef.ToString(), task, classDef, cleanEntries, EmptyBindings, routed.Value);

            // Route B: the identical infraction, occurrence for occurrence,
            // recorded entry-scoped on the subject's Entry.
            var penalisedSubject = CapturedEntryFor(subject, t.subjectMultiplier);
            for (var i = 0; i < t.occurrences; i++)
            {
                var decided = penalisedSubject.RecordPenalty(
                    new Penalty { InfractionType = "motorRestart", Scope = PenaltyScope.Entry },
                    classDef.Penalties);
                decided.IsSuccess.Should().BeTrue();
                penalisedSubject = penalisedSubject.Apply(decided.Value);
            }

            var penalisedEntries = ImmutableDictionary<string, Entry>.Empty
                .Add(subject.ToString(), penalisedSubject)
                .Add(other.ToString(), CapturedEntryFor(other, 1));

            var viaEntry = ScoringService.ScoreGroup(
                groupRef.ToString(), task, classDef, penalisedEntries, EmptyBindings);

            foreach (var (route, group) in new[] { ("taskRound", viaTaskRound), ("entry", viaEntry) })
            {
                var subjectRow = group.Results[subject.ToString()];
                subjectRow.State.Should().Be(TaskResultState.NoResult, $"subject via {route}");
                subjectRow.RawScore.Should().Be(0m, $"subject via {route}");
                subjectRow.Selection.Should().BeNull($"subject via {route}");
                subjectRow.Disqualified.Should().BeFalse($"subject via {route}");

                // Excluded from winner-finding: the same non-subject winner
                // both ways, despite the subject's higher raw.
                group.WinnerRef.Should().Be(other.ToString(), $"winner via {route}");

                var otherRow = group.Results[other.ToString()];
                otherRow.State.Should().Be(TaskResultState.Valid, $"other via {route}");
                otherRow.Disqualified.Should().BeFalse($"other via {route}");
            }
        });
    }

    private static Entry CapturedEntryFor(CompetitorId competitorRef, int multiplier)
    {
        var groupRef = GroupId.New();
        var entry = Entry.Create(new EntryOpened(
            EntryId.New(),
            CompetitionId.New(), 0, 1, 1,
            groupRef, competitorRef, ReflightRole.Original, Now)).Apply(new FlightOpened(1, Now));

        foreach (var metric in MetricNames)
        {
            var value = (Array.IndexOf(MetricNames, metric) + 1) * 10m * multiplier;
            var captured = entry.CaptureMeasurement(1, metric, MeasuredValue.Of(value), Now, MetricDefs);
            captured.IsSuccess.Should().BeTrue();
            entry = entry.Apply(captured.Value);
        }

        return entry;
    }

    // ------------------------------------------------- P-FlagOrAccumulation

    // Pure-effect definitions only, and deliberately non-grouped (D-B4):
    // adoption check 16 admits only all-DeductPoints definitions into
    // exclusion groups (ClassDefinitionValidation.CheckExclusionGroupsAreDeductOnly),
    // so a Disqualify-carrying definition can never be suppressed out of
    // flagging — these generators exercise the un-suppressible shape.
    private static readonly ImmutableArray<PenaltyDefinition> FlagOrAccumulationDefs =
    [
        new()
        {
            InfractionType = "lateLanding",
            Accrual = PenaltyAccrual.PerOccurrence,
            Effects = [new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 100)],
        },
        new()
        {
            InfractionType = "grossMisconduct",
            Effects = [new PenaltyEffectSpec(PenaltyEffect.Disqualify)],
        },
    ];

    private static readonly Gen<(string InfractionType, PenaltyScope Scope)> FlagOrAccumulationFactGen =
        from infractionType in Gen.OneOfConst("lateLanding", "grossMisconduct")
        from scope in Gen.OneOfConst(PenaltyScope.Entry, PenaltyScope.Competition)
        select (infractionType, scope);

    /// <summary>
    /// Score the synthetic 100-raw field with the given records folded in list
    /// order — entry-scoped onto the subject's Entry via its decide function,
    /// aggregate-scoped onto the Competition — and return the subject's final
    /// score row. Rebuilt fresh for each call so permutations start from
    /// identical state (folding mutates both aggregates).
    /// </summary>
    private static FinalCompetitorScore ScoreSubjectWithRecords(
        List<(string InfractionType, PenaltyScope Scope)> records)
    {
        var (competition, entries, competitors) = BuildCompetitionWithPenalties(2, FlagOrAccumulationDefs);
        var subject = competitors[0];
        var definitions = competition.AdoptedRules.Definition.Penalties;

        var aggregateRecords = new List<Penalty>();

        foreach (var (infractionType, scope) in records)
        {
            if (scope == PenaltyScope.Entry)
            {
                var entryId = entries.Keys.Single(id => entries[id].CompetitorRef == subject);
                var decided = entries[entryId].RecordPenalty(
                    new Penalty { InfractionType = infractionType, Scope = PenaltyScope.Entry }, definitions);
                decided.IsSuccess.Should().BeTrue();
                entries[entryId] = entries[entryId].Apply(decided.Value);
            }
            else
            {
                aggregateRecords.Add(new Penalty
                {
                    InfractionType = infractionType,
                    Scope = PenaltyScope.Competition,
                    CompetitorRef = subject,
                });
            }
        }

        competition = competition with { Penalties = aggregateRecords.ToImmutableArray() };

        var result = ScoringService.ScoreCompetition(competition, entries);
        result.IsSuccess.Should().BeTrue();
        return result.Value.Scores[subject.ToString()];
    }

    /// <summary>
    /// P-FlagOrAccumulation
    /// (kanban/in-progress/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-3):
    /// the final <see cref="FinalCompetitorScore.Disqualified"/> equals the OR
    /// over every recorded entry- AND aggregate-scoped Disqualify effect for
    /// that competitor — a Disqualify at either scope flags, and none leaves
    /// the score untouched (D-B2 flag-only). Invariant under permutation of the
    /// recorded penalties (order-independence, mirroring P-RawOrderIndependence
    /// of PenaltyEnginePropertyTests): two re-folded permutations of the same
    /// record set must produce identical Disqualified and Score.
    /// </summary>
    [Fact]
    public void P_FlagOrAccumulation_final_disqualified_is_the_order_independent_or_over_both_scopes()
    {
        FlagOrAccumulationFactGen.Array[0, 5].Sample(records =>
        {
            var list = records.ToList();

            var expectedDisqualified = list.Any(r => r.InfractionType == "grossMisconduct");
            var entryDeduct = 100m * list.Count(r => r is ("lateLanding", PenaltyScope.Entry));
            var aggregateDeduct = 100m * list.Count(r => r is ("lateLanding", PenaltyScope.Competition));

            // Score oracle: the 100-raw subject's DeductPoints halves act at
            // the stage their scope names — entry-scoped pre-normalisation
            // (floored at zero, D4), aggregate-scoped flat after drops. A
            // pure-Disqualify record changes no arithmetic.
            var expectedScore = Math.Max(0m, 100m - entryDeduct) - aggregateDeduct;

            var original = ScoreSubjectWithRecords(list);
            original.Disqualified.Should().Be(expectedDisqualified);
            original.Score.Should().Be(expectedScore);

            // Permutation invariance: the OR and the sum do not care about
            // the order the records were folded in.
            var reversed = ScoreSubjectWithRecords(list.AsEnumerable().Reverse().ToList());
            reversed.Disqualified.Should().Be(original.Disqualified);
            reversed.Score.Should().Be(original.Score);

            var rotated = list.Skip(list.Count / 2).Concat(list.Take(list.Count / 2)).ToList();
            var rotatedScore = ScoreSubjectWithRecords(rotated);
            rotatedScore.Disqualified.Should().Be(original.Disqualified);
            rotatedScore.Score.Should().Be(original.Score);
        });
    }
}
