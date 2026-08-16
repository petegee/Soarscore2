using System.Linq;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.DrawPhase"/> —
/// kanban/completed/phase-drawn-steel-thread-plan.md WI-1. Mirrors
/// CompetitionDecideTests's style; drives real seed-corpus ClassDefinitions
/// (Soarscore.SeedData) rather than hand-built fixtures wherever the corpus
/// already has the shape a case needs — see the plan's "checked, not assumed"
/// rules note for why F3J/F3K/F5J/F3F, not F5K, run the happy path today.
/// </summary>
public class PhaseDrawnDecideTests
{
    private static Competition CompetitionAdopting(ClassDefinition definition, int competitorCount)
    {
        var at = DateTimeOffset.UtcNow;
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = at,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Draw Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, at);

        var competition = Competition.Create(created);

        for (var i = 0; i < competitorCount; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), at);
            competition = competition.Apply(registered.Value);
        }

        return competition;
    }

    /// <summary>F3J's TaskD (Group.MinPerGroup = 6) with the constraint swapped to an unbound Param — an F5K-shaped fixture, isolated from F5K's own catalogue-choice composition so this exercises only the parameter-resolution path.</summary>
    private static ClassDefinition WithUnboundMinPerGroup(ClassDefinition definition)
    {
        var phase = definition.Phases[0];
        var task = phase.Tasks[0] with { Group = new GroupConstraint { MinPerGroup = NumberOrParam.Param("minPerGroup") } };
        return definition with { Phases = [phase with { Tasks = [task] }] };
    }

    [Fact]
    public void DrawPhase_with_12_competitors_minPerGroup_6_over_3_rounds_produces_3_rounds_of_2_groups_of_6()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);

        var result = competition.DrawPhase(3, [], DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.PhaseOrdinal.Should().Be(0);
        result.Value.Rounds.Length.Should().Be(3);
        result.Value.Rounds.Select(r => r.Ordinal).Should().Equal(1, 2, 3);

        foreach (var round in result.Value.Rounds)
        {
            round.TaskRounds.Length.Should().Be(1);
            var taskRound = round.TaskRounds[0];
            taskRound.Ordinal.Should().Be(1);
            taskRound.TaskRef.Should().Be("D");
            taskRound.State.Should().Be(TaskRoundState.Drawn);
            taskRound.Groups.Length.Should().Be(2);
            taskRound.Groups.Select(g => g.Ordinal).Should().Equal(1, 2);
            taskRound.Groups.Should().OnlyContain(g => g.CompetitorRefs.Length == 6);

            var placed = taskRound.Groups.SelectMany(g => g.CompetitorRefs).ToArray();
            placed.Length.Should().Be(12);
            placed.Distinct().Count().Should().Be(12);
        }
    }

    [Fact]
    public void DrawPhase_against_an_already_drawn_phase_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 6);
        var first = competition.DrawPhase(1, [], DateTimeOffset.UtcNow);
        competition = competition.Apply(first.Value);

        var result = competition.DrawPhase(1, [], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.alreadyDrawn");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DrawPhase_with_zero_or_negative_rounds_fails_with_a_stable_code(int rounds)
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 6);

        var result = competition.DrawPhase(rounds, [], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.roundsInvalid");
    }

    [Fact]
    public void DrawPhase_with_rounds_over_the_class_maximum_fails_with_a_stable_code()
    {
        // NZ N: MaxRounds = 3 (NZ.3.13.1 k), single fixed-sequence task, no GroupConstraint.
        var competition = CompetitionAdopting(SeedNzNAles123.Definition, 6);

        var result = competition.DrawPhase(4, [], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.roundsInvalid");
    }

    [Fact]
    public void DrawPhase_against_a_multi_task_per_round_composition_fails_with_a_stable_code()
    {
        // F3B: TasksPerRound = 3 (a round is one flight each of A, B and C).
        var competition = CompetitionAdopting(SeedF3B.Definition, 12);

        var result = competition.DrawPhase(1, [], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.unsupportedRoundComposition");
    }

    [Fact]
    public void DrawPhase_against_a_catalogue_choice_phase_with_no_selection_fails_with_a_stable_code()
    {
        // F3K's preliminary phase: ChooseFromCatalogue, task selection required.
        var competition = CompetitionAdopting(SeedF3K.Definition, 10);

        var result = competition.DrawPhase(1, [], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.taskSelectionRequired");
    }

    [Fact]
    public void DrawPhase_with_a_task_selection_for_a_fixed_sequence_phase_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);

        var result = competition.DrawPhase(1, ["D"], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.taskSelectionNotPermitted");
    }

    [Fact]
    public void DrawPhase_with_a_task_selection_count_not_matching_rounds_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3K.Definition, 10);

        var result = competition.DrawPhase(5, ["A", "B", "C"], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.taskSelectionCountMismatch");
    }

    [Fact]
    public void DrawPhase_with_a_task_code_not_in_the_catalogue_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3K.Definition, 10);

        var result = competition.DrawPhase(1, ["Z"], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.taskNotInCatalogue");
    }

    [Fact]
    public void DrawPhase_with_a_repeated_task_where_the_phase_requires_distinct_tasks_fails_with_a_stable_code()
    {
        // F3K's preliminary phase: RequireDistinctTaskPerRound (F3K.10).
        var competition = CompetitionAdopting(SeedF3K.Definition, 10);

        var result = competition.DrawPhase(5, ["A", "A", "B", "C", "D"], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.taskSelectionNotDistinct");
    }

    [Fact]
    public void DrawPhase_real_corpus_F3K_with_5_distinct_catalogue_tasks_succeeds_with_the_named_task_per_round()
    {
        var competition = CompetitionAdopting(SeedF3K.Definition, 10); // F3K.9.1 minPerGroup 5, literal
        var selection = new[] { "A", "B", "C", "D", "E" };

        var result = competition.DrawPhase(5, [.. selection], DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue(result.Code);
        result.Value.Rounds.Length.Should().Be(5);
        result.Value.Rounds.Select(r => r.TaskRounds[0].TaskRef).Should().Equal(selection);
        foreach (var round in result.Value.Rounds)
        {
            var placed = round.TaskRounds[0].Groups.SelectMany(g => g.CompetitorRefs).ToArray();
            placed.Length.Should().Be(10);
            placed.Distinct().Count().Should().Be(10);
        }
    }

    [Fact]
    public void DrawPhase_real_corpus_F5K_after_binding_minPerGroup_with_catalogue_tasks_succeeds()
    {
        var competition = CompetitionAdopting(SeedF5K.Definition, 10);
        var bound = competition.BindParameter("minPerGroup", MeasuredValue.Of(5m), "CD", DateTimeOffset.UtcNow);
        bound.IsSuccess.Should().BeTrue(bound.Code);
        competition = competition.Apply(bound.Value);

        var selection = new[] { "A", "B", "C" };
        var result = competition.DrawPhase(3, [.. selection], DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue(result.Code);
        result.Value.Rounds.Select(r => r.TaskRounds[0].TaskRef).Should().Equal(selection);
    }

    [Fact]
    public void DrawPhase_real_corpus_F5K_without_binding_minPerGroup_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF5K.Definition, 10);

        var result = competition.DrawPhase(3, ["A", "B", "C"], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.parameterUnbound");
    }

    [Fact]
    public void DrawPhase_against_an_empty_field_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 0);

        var result = competition.DrawPhase(1, [], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.fieldEmpty");
    }

    [Fact]
    public void DrawPhase_with_a_field_smaller_than_minPerGroup_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 5); // F3J.6.1 minimum is 6

        var result = competition.DrawPhase(1, [], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.fieldTooSmall");
    }

    [Fact]
    public void DrawPhase_against_an_unbound_parameterised_minPerGroup_fails_with_a_stable_code()
    {
        var definition = WithUnboundMinPerGroup(SeedF3J.Definition);
        var competition = CompetitionAdopting(definition, 12);

        var result = competition.DrawPhase(1, [], DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.parameterUnbound");
    }

    [Fact]
    public void DrawPhase_excludes_withdrawn_competitors_from_every_group()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 8);
        var toWithdraw = competition.Competitors.Take(2).Select(c => c.Id).ToArray();
        foreach (var id in toWithdraw)
        {
            var withdrawn = competition.WithdrawCompetitor(id, DateTimeOffset.UtcNow);
            competition = competition.Apply(withdrawn.Value);
        }

        var result = competition.DrawPhase(2, [], DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        foreach (var round in result.Value.Rounds)
        {
            var placed = round.TaskRounds[0].Groups.SelectMany(g => g.CompetitorRefs).ToArray();
            placed.Length.Should().Be(6);
            placed.Should().NotContain(toWithdraw);
        }
    }

    [Fact]
    public void DrawPhase_against_a_task_with_no_GroupConstraint_puts_the_whole_field_in_one_group_every_round()
    {
        var competition = CompetitionAdopting(SeedNzNAles123.Definition, 5);

        var result = competition.DrawPhase(3, [], DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        foreach (var round in result.Value.Rounds)
        {
            var taskRound = round.TaskRounds[0];
            taskRound.Groups.Length.Should().Be(1);
            taskRound.Groups[0].CompetitorRefs.Length.Should().Be(5);
        }
    }
}
