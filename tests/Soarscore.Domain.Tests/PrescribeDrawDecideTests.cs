using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.PrescribeDraw"/> —
/// kanban/in-progress/prescribed-draw-import.md WI-2. Mirrors
/// DrawAcceptanceDecideTests / PhaseDrawnDecideTests's style: real
/// seed-corpus ClassDefinitions (Soarscore.SeedData) rather than hand-built
/// fixtures, one case per defect code in WI-1's table, plus the reject →
/// re-prescribe lifecycle and the score-parity happy path that gives the
/// story its reason for existing. The fold half of the happy-path event is
/// asserted too, by applying the emitted event directly.
/// </summary>
public class PrescribeDrawDecideTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);

    private static Competition CompetitionAdopting(ClassDefinition definition, int competitorCount)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Prescribed Draw Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);

        for (var i = 0; i < competitorCount; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now);
            competition = competition.Apply(registered.Value);
        }

        return competition;
    }

    private static ImmutableArray<CompetitorId> EligibleField(Competition competition) =>
        [.. competition.Competitors.Where(c => c.WithdrawnAt is null).Select(c => c.Id)];

    private static PrescribedRound MakeRound(string? taskRef, params IReadOnlyList<CompetitorId>[] groups) =>
        new(taskRef, [.. groups.Select(g => new PrescribedGroup(g))]);

    private static IReadOnlyList<IReadOnlyList<CompetitorId>> Partition(ImmutableArray<CompetitorId> field, int groupSize)
    {
        var groups = new List<IReadOnlyList<CompetitorId>>();
        for (var i = 0; i < field.Length; i += groupSize)
        {
            groups.Add(field.Skip(i).Take(groupSize).ToArray());
        }

        return groups;
    }

    // ------------------------------------------------------------- happy path

    [Fact]
    public void PrescribeDraw_happy_path_emits_the_supplied_groups_in_the_supplied_flying_order()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12); // F3J.6.1 MinPerGroup 6, literal
        var field = EligibleField(competition);

        // Deliberately NOT field order: member order is stored as given
        // (story decision 4), so the assertion below is load-bearing.
        var flyingOrder1 = new[] { field[5], field[0], field[9], field[2], field[7], field[1] };
        var flyingOrder2 = new[] { field[11], field[4], field[8], field[3], field[10], field[6] };

        var result = competition.PrescribeDraw(
            [MakeRound(null, flyingOrder1, flyingOrder2)], "CD", Now);

        result.IsSuccess.Should().BeTrue(result.Code ?? "prescription succeeded");

        var @event = result.Value;
        @event.PhaseOrdinal.Should().Be(0);
        @event.Type.Should().Be(PhaseType.Preliminary);
        @event.Draw.CreatedAt.Should().Be(Now);
        @event.Draw.Status.Should().Be("drawn");
        @event.PrescribedBy.Should().Be("CD");

        @event.Rounds.Length.Should().Be(1);
        var round = @event.Rounds[0];
        round.Ordinal.Should().Be(1);
        round.TaskRounds.Length.Should().Be(1);

        var taskRound = round.TaskRounds[0];
        taskRound.Ordinal.Should().Be(1);
        taskRound.State.Should().Be(Competitions.TaskRoundState.Drawn);
        taskRound.TaskRef.Should().Be("D"); // FixedSequence: resolved from the phase definition

        taskRound.Groups.Length.Should().Be(2);
        taskRound.Groups.Select(g => g.Ordinal).Should().Equal(1, 2);

        // Minted GroupIds are well-formed and unique within the event.
        var minted = taskRound.Groups.Select(g => g.Id).ToArray();
        minted.Select(g => g.Value).Should().OnlyContain(value => value != Guid.Empty);
        minted.Select(g => g.Value).Distinct().Should().HaveCount(minted.Length);

        // Flying order survives the event build — finding 6, load-bearing.
        taskRound.Groups[0].CompetitorRefs.Should().Equal(flyingOrder1);
        taskRound.Groups[1].CompetitorRefs.Should().Equal(flyingOrder2);

        // And survives the fold.
        var folded = competition.Apply(@event);
        folded.Phases.Length.Should().Be(1);
        folded.Phases[0].Ordinal.Should().Be(0);
        folded.Phases[0].Rounds[0].TaskRounds[0].Groups[0].CompetitorRefs.Should().Equal(flyingOrder1);
        folded.Phases[0].Rounds[0].TaskRounds[0].Groups[1].CompetitorRefs.Should().Equal(flyingOrder2);
    }

    [Fact]
    public void PrescribeDraw_real_corpus_F3K_with_distinct_catalogue_tasks_succeeds_and_names_each_rounds_task()
    {
        var competition = CompetitionAdopting(SeedF3K.Definition, 10); // F3K.9.1 MinPerGroup 5, literal
        var field = EligibleField(competition);
        var half1 = field.Take(5).ToArray();
        var half2 = field.Skip(5).Take(5).ToArray();

        var result = competition.PrescribeDraw(
            [MakeRound("A", half1, half2), MakeRound("B", half2, half1)], "CD", Now);

        result.IsSuccess.Should().BeTrue(result.Code ?? "prescription succeeded");
        result.Value.Rounds.Select(r => r.TaskRounds[0].TaskRef).Should().Equal("A", "B");
        foreach (var round in result.Value.Rounds)
        {
            var placed = round.TaskRounds[0].Groups.SelectMany(g => g.CompetitorRefs).ToArray();
            placed.Length.Should().Be(10);
            placed.Distinct().Count().Should().Be(10);
        }
    }

    // ----------------------------------------------- shared-schedule defects

    [Fact]
    public void PrescribeDraw_against_an_already_drawn_phase_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var drawn = competition.DrawPhase(1, [], Now);
        competition = competition.Apply(drawn.Value);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw(
            [MakeRound(null, Partition(field, 6).ToArray())], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.alreadyDrawn");
    }

    [Fact]
    public void PrescribeDraw_with_no_rounds_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);

        var result = competition.PrescribeDraw([], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.roundsInvalid");
    }

    [Fact]
    public void PrescribeDraw_over_the_class_maximum_fails_with_a_stable_code()
    {
        // NZ N: MaxRounds = 3 (NZ.3.13.1 k), single fixed-sequence task,
        // no GroupConstraint — one whole-field group per round.
        var competition = CompetitionAdopting(SeedNzNAles123.Definition, 6);
        var wholeField = EligibleField(competition).ToArray();

        var result = competition.PrescribeDraw(
            [MakeRound(null, wholeField), MakeRound(null, wholeField), MakeRound(null, wholeField), MakeRound(null, wholeField)],
            "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.roundsInvalid");
    }

    [Fact]
    public void PrescribeDraw_against_a_multi_task_per_round_composition_fails_with_a_stable_code()
    {
        // F3B: TasksPerRound = 3 (a round is one flight each of A, B and C).
        var competition = CompetitionAdopting(SeedF3B.Definition, 12);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw([MakeRound(null, field.ToArray())], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.unsupportedRoundComposition");
    }

    [Fact]
    public void PrescribeDraw_with_a_task_selection_for_a_fixed_sequence_phase_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw([MakeRound("D", field.ToArray())], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.taskSelectionNotPermitted");
    }

    [Fact]
    public void PrescribeDraw_against_a_catalogue_choice_phase_with_no_selection_fails_with_a_stable_code()
    {
        // F3K's preliminary phase: ChooseFromCatalogue, task selection required.
        var competition = CompetitionAdopting(SeedF3K.Definition, 10);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw([MakeRound(null, field.ToArray())], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.taskSelectionRequired");
    }

    [Fact]
    public void PrescribeDraw_with_a_task_selection_count_not_matching_rounds_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3K.Definition, 10);
        var field = EligibleField(competition);
        var halves = Partition(field, 5).ToArray();

        // Five rounds requested but only three tasks named — the mismatch is
        // caught by the shared task resolution, which counts NAMED tasks
        // against the round count.
        var result = competition.PrescribeDraw(
        [
            MakeRound("A", halves[0], halves[1]),
            MakeRound("B", halves[1], halves[0]),
            MakeRound("C", halves[0], halves[1]),
            MakeRound(null, halves[0], halves[1]),
            MakeRound(null, halves[0], halves[1]),
        ], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.taskSelectionCountMismatch");
    }

    [Fact]
    public void PrescribeDraw_with_a_repeated_task_where_the_phase_requires_distinct_tasks_fails_with_a_stable_code()
    {
        // F3K's preliminary phase: RequireDistinctTaskPerRound (F3K.10).
        var competition = CompetitionAdopting(SeedF3K.Definition, 10);
        var field = EligibleField(competition);
        var halves = Partition(field, 5).ToArray();

        var result = competition.PrescribeDraw(
        [
            MakeRound("A", halves[0], halves[1]),
            MakeRound("A", halves[1], halves[0]),
            MakeRound("B", halves[0], halves[1]),
            MakeRound("C", halves[1], halves[0]),
            MakeRound("D", halves[0], halves[1]),
        ], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.taskSelectionNotDistinct");
    }

    [Fact]
    public void PrescribeDraw_with_a_task_code_not_in_the_catalogue_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3K.Definition, 10);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw([MakeRound("Z", field.ToArray())], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.taskNotInCatalogue");
    }

    [Fact]
    public void PrescribeDraw_real_corpus_F5K_without_binding_minPerGroup_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF5K.Definition, 10);
        var field = EligibleField(competition);
        var halves = Partition(field, 5).ToArray();

        var result = competition.PrescribeDraw(
        [
            MakeRound("A", halves[0], halves[1]),
            MakeRound("B", halves[1], halves[0]),
            MakeRound("C", halves[0], halves[1]),
        ], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.parameterUnbound");
    }

    [Fact]
    public void PrescribeDraw_with_a_field_smaller_than_minPerGroup_fails_with_a_stable_code()
    {
        // F3J.6.1 minimum is 6; five eligible pilots fail the shared resolution
        // before any prescription-specific partition check runs.
        var competition = CompetitionAdopting(SeedF3J.Definition, 5);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw([MakeRound(null, field.ToArray())], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.fieldTooSmall");
    }

    [Fact]
    public void PrescribeDraw_against_an_empty_field_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 0);

        var result = competition.PrescribeDraw([MakeRound(null, [])], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.fieldEmpty");
    }

    // ------------------------------------------ prescription-only partition defects

    [Fact]
    public void PrescribeDraw_with_an_unregistered_grouped_id_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var field = EligibleField(competition);
        var stranger = CompetitorId.New();

        var result = competition.PrescribeDraw(
            [MakeRound(null, field.Take(6).Append(stranger).ToArray(), field.Skip(6).Take(5).ToArray())],
            "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.competitorNotInField");
    }

    [Fact]
    public void PrescribeDraw_with_a_withdrawn_grouped_id_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var withdrawn = competition.WithdrawCompetitor(competition.Competitors[11].Id, Now);
        withdrawn.IsSuccess.Should().BeTrue(withdrawn.Code ?? "withdrawal succeeded");
        competition = competition.Apply(withdrawn.Value);
        var field = EligibleField(competition);

        // The prescription covers the ORIGINAL twelve — the withdrawn pilot's
        // presence is a malformed input, not a variation (story D2).
        var originalTwelve = competition.Competitors.Select(c => c.Id).ToArray();

        var result = competition.PrescribeDraw(
            [MakeRound(null, originalTwelve.Take(6).ToArray(), originalTwelve.Skip(6).Take(6).ToArray())],
            "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.competitorNotInField");
    }

    [Fact]
    public void PrescribeDraw_with_a_competitor_in_two_groups_of_one_round_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw(
            [
                MakeRound(null, field.Take(6).ToArray(), field.Take(1).Concat(field.Skip(6).Take(5)).ToArray()),
            ],
            "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.competitorRepeated");
    }

    [Fact]
    public void PrescribeDraw_that_leaves_a_registered_competitor_unplaced_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var field = EligibleField(competition);

        // Eleven placed once each — the twelfth eligible pilot is in no group.
        var result = competition.PrescribeDraw(
            [MakeRound(null, field.Take(6).ToArray(), field.Skip(6).Take(5).ToArray())],
            "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.competitorMissing");
    }

    [Fact]
    public void PrescribeDraw_with_a_singleton_group_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw(
            [MakeRound(null, field.Take(1).ToArray(), field.Skip(1).Take(11).ToArray())],
            "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.groupTooSmall");
    }

    [Fact]
    public void PrescribeDraw_with_a_group_below_the_class_minimum_fails_with_a_stable_code()
    {
        // Both groups clear the 2-member floor; the five-member one is below
        // F3J.6.1's six.
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw(
            [MakeRound(null, field.Take(7).ToArray(), field.Skip(7).Take(5).ToArray())],
            "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.groupBelowClassMinimum");
    }

    // ------------------------------------------------------------ lifecycle

    [Fact]
    public void A_rejected_draw_can_be_replaced_by_a_prescription_which_addresses_the_preliminary_again()
    {
        // Decision 7: reject→re-prescribe is legal exactly as reject→redraw is
        // (D2 removal semantics); Phases.Length is again the preliminary's ordinal.
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var drawn = competition.DrawPhase(1, [], Now);
        competition = competition.Apply(drawn.Value);
        competition = competition.Apply(competition.RejectDraw(phaseHasEntries: false, "Wrong groups", Now).Value);
        competition.Phases.Should().BeEmpty();

        var field = EligibleField(competition);
        var result = competition.PrescribeDraw(
            [MakeRound(null, field.Take(6).ToArray(), field.Skip(6).Take(6).ToArray())], "CD", Now);

        result.IsSuccess.Should().BeTrue(result.Code ?? "re-prescription succeeded");
        result.Value.PhaseOrdinal.Should().Be(0);

        var folded = competition.Apply(result.Value);
        folded.Phases.Length.Should().Be(1);
        folded.Phases.Single().Ordinal.Should().Be(0);
        folded.Phases.Single().Draw.Status.Should().Be("drawn");
        folded.Phases.Single().Draw.CreatedAt.Should().Be(Now);
    }

    // ------------------------------------- score parity — the story's purpose

    private const string Metric = "flightTime";

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [new MetricDefinition { Name = Metric, Kind = MeasuredKind.Number }];

    private static TaskDefinition MakeParityTask() => new()
    {
        Code = "T",
        Name = "Prescription parity task",
        Metrics = MetricDefs,
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Group = new GroupConstraint { MinPerGroup = 3 },
        Normalise = new Normalisation { Direction = NormalisationDirection.HigherIsBetter, WinnerScore = 1000 },
        Score = [new RateTerm { MetricRef = Metric, Rate = 1 }],
    };

    private static ClassDefinition MakeParityClassDefinition(TaskDefinition task) => new()
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

    /// <summary>A competition adopting <paramref name="definition"/>, registering exactly <paramref name="ids"/>.</summary>
    private static Competition Adopting(ClassDefinition definition, IReadOnlyList<CompetitorId> ids)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Parity Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);

        foreach (var id in ids)
        {
            var registered = competition.RegisterCompetitor(id, PersonId.New(), Now);
            registered.IsSuccess.Should().BeTrue();
            competition = competition.Apply(registered.Value);
        }

        return competition;
    }

    private static Dictionary<EntryId, Entry> FlyEveryGroup(Competition competition, IReadOnlyDictionary<CompetitorId, decimal> timeFor)
    {
        var entries = new Dictionary<EntryId, Entry>();

        var round = competition.Phases[0].Rounds[0];
        var taskRound = round.TaskRounds[0];

        foreach (var group in taskRound.Groups)
        {
            foreach (var competitorRef in group.CompetitorRefs)
            {
                var opened = competition.OpenEntry(
                    EntryId.New(), 0, round.Ordinal, taskRound.Ordinal, group.Id, competitorRef, ReflightRole.Original, Now);
                opened.IsSuccess.Should().BeTrue(opened.Code ?? "entry opened");

                var entry = Entry.Create(opened.Value).Apply(new FlightOpened(1, Now));

                var captured = entry.CaptureMeasurement(
                    1, Metric, MeasuredValue.Of(timeFor[competitorRef]), Now, MetricDefs);
                captured.IsSuccess.Should().BeTrue(captured.Code ?? "measurement captured");
                entry = entry.Apply(captured.Value);

                entries[entry.Id] = entry;
            }
        }

        return entries;
    }

    [Fact]
    public void A_generated_comp_and_a_prescribed_comp_handed_identical_groups_score_identically()
    {
        // Normalisation scales by GROUP WINNER, so identical scores require
        // identical group membership — which is why prescription exists at all
        // (the story's Why it matters). Both comps share the competitor ids and
        // the prescription feeds the generated groups back verbatim.
        var definition = MakeParityClassDefinition(MakeParityTask());
        var sharedField = Enumerable.Range(0, 6).Select(_ => CompetitorId.New()).ToArray();

        var generated = Adopting(definition, sharedField);
        var drawn = generated.DrawPhase(1, [], Now);
        drawn.IsSuccess.Should().BeTrue(drawn.Code ?? "generation succeeded");

        var prescribed = Adopting(definition, sharedField);
        var fedBack = drawn.Value.Rounds[0].TaskRounds[0].Groups
            .Select(g => new PrescribedGroup([.. g.CompetitorRefs]))
            .ToArray();
        var prescribedResult = prescribed.PrescribeDraw([new PrescribedRound(null, fedBack)], "CD", Now);
        prescribedResult.IsSuccess.Should().BeTrue(prescribedResult.Code ?? "prescription succeeded");

        generated = generated.Apply(drawn.Value);
        generated = generated.Apply(generated.AcceptDraw(Now).Value);
        prescribed = prescribed.Apply(prescribedResult.Value);
        prescribed = prescribed.Apply(prescribed.AcceptDraw(Now).Value);

        decimal[] rawTimes = [120, 200, 90, 300, 150, 260];
        var timeFor = sharedField.Select((id, i) => (id, time: rawTimes[i])).ToDictionary(t => t.id, t => t.time);

        var scoredGenerated = ScoringService.ScoreCompetition(generated, FlyEveryGroup(generated, timeFor));
        scoredGenerated.IsSuccess.Should().BeTrue(scoredGenerated.Code ?? "scoring succeeded");
        var scoredPrescribed = ScoringService.ScoreCompetition(prescribed, FlyEveryGroup(prescribed, timeFor));
        scoredPrescribed.IsSuccess.Should().BeTrue(scoredPrescribed.Code ?? "scoring succeeded");

        foreach (var id in sharedField)
        {
            var key = id.ToString();
            scoredGenerated.Value.Scores[key].Score.Should().Be(scoredPrescribed.Value.Scores[key].Score);
            scoredGenerated.Value.Placings[key].Should().Be(scoredPrescribed.Value.Placings[key]);
        }

        // Sanity: two groups, so exactly two 1000-point winners — the parity
        // above means something only if normalisation actually ran per group.
        scoredGenerated.Value.Scores.Values.Count(s => s.Score == 1000m).Should().Be(2);
    }
}
