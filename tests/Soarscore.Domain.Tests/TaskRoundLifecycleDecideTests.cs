using System.Linq;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.CompleteTaskRound"/>,
/// <see cref="Competition.AnnulTaskRound"/> and
/// <see cref="Competition.ReopenTaskRound"/> —
/// kanban/completed/task-round-lifecycle.md WI-1/WI-2/WI-2b, one case per
/// defect code plus the success cases. Mirrors BindParameterDecideTests's
/// style: a real seed-corpus ClassDefinition (SeedF3J, the corpus's simplest
/// drawable phase) taken through the real DrawPhase, so the phase/round/
/// task-round shape under test is the one the draw actually produces rather
/// than a hand-built approximation of it.
/// </summary>
public class TaskRoundLifecycleDecideTests
{
    private static readonly DateTimeOffset At = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    /// <summary>F3J, 12 competitors, <paramref name="rounds"/> rounds drawn — each round one task-round (ordinal 1, task D) in Drawn.</summary>
    private static Competition DrawnF3J(int rounds = 2)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = SeedF3J.Definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SeedF3J.Definition.Version,
            AdoptedAt = At,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Task-Round Lifecycle Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, At);

        var competition = Competition.Create(created);

        for (var i = 0; i < 12; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), At);
            competition = competition.Apply(registered.Value);
        }

        var drawn = competition.DrawPhase(rounds, [], At);
        drawn.IsSuccess.Should().BeTrue();
        return competition.Apply(drawn.Value);
    }

    private static TaskRoundState StateOf(Competition competition, int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal) =>
        competition.Phases.Single(p => p.Ordinal == phaseOrdinal)
            .Rounds.Single(r => r.Ordinal == roundOrdinal)
            .TaskRounds.Single(tr => tr.Ordinal == taskRoundOrdinal)
            .State;

    // ------------------------------------------------------------ CompleteTaskRound (WI-1)

    [Theory]
    [InlineData(99, 1, 1)]
    [InlineData(0, 99, 1)]
    [InlineData(0, 1, 99)]
    public void CompleteTaskRound_against_ordinals_that_name_no_task_round_fails_with_a_stable_code(
        int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal)
    {
        var competition = DrawnF3J();

        var result = competition.CompleteTaskRound(phaseOrdinal, roundOrdinal, taskRoundOrdinal, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("completeTaskRound.taskRoundNotFound");
    }

    [Fact]
    public void CompleteTaskRound_against_an_already_complete_task_round_fails_with_a_stable_code()
    {
        var competition = DrawnF3J();
        competition = competition.Apply(new TaskRoundCompleted(0, 1, 1, At));

        var result = competition.CompleteTaskRound(0, 1, 1, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("completeTaskRound.alreadyComplete");
    }

    [Fact]
    public void CompleteTaskRound_against_an_annulled_task_round_fails_with_a_stable_code()
    {
        // An annulment is a resolution, not a way-station: it is reopened
        // first, never completed in place.
        var competition = DrawnF3J();
        competition = competition.Apply(new TaskRoundAnnulled(0, 1, 1, "Launch line collapsed", At));

        var result = competition.CompleteTaskRound(0, 1, 1, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("completeTaskRound.annulled");
    }

    [Fact]
    public void CompleteTaskRound_succeeds_carries_the_ordinals_through_and_folds_the_task_round_to_Complete()
    {
        var competition = DrawnF3J();

        var result = competition.CompleteTaskRound(0, 1, 1, At);

        result.IsSuccess.Should().BeTrue();
        result.Value.PhaseOrdinal.Should().Be(0);
        result.Value.RoundOrdinal.Should().Be(1);
        result.Value.TaskRoundOrdinal.Should().Be(1);
        result.Value.At.Should().Be(At);

        competition = competition.Apply(result.Value);
        StateOf(competition, 0, 1, 1).Should().Be(TaskRoundState.Complete);
    }

    [Fact]
    public void CompleteTaskRound_imposes_no_ordering_across_rounds()
    {
        // NFR-4: rounds are not required to complete in order, or at all —
        // completing round 2 with round 1 untouched is an ordinary act.
        var competition = DrawnF3J();

        var result = competition.CompleteTaskRound(0, 2, 1, At);

        result.IsSuccess.Should().BeTrue();
        competition = competition.Apply(result.Value);
        StateOf(competition, 0, 1, 1).Should().Be(TaskRoundState.Drawn);
        StateOf(competition, 0, 2, 1).Should().Be(TaskRoundState.Complete);
    }

    // ------------------------------------------------------------ AnnulTaskRound (WI-2)

    [Theory]
    [InlineData(99, 1, 1)]
    [InlineData(0, 99, 1)]
    [InlineData(0, 1, 99)]
    public void AnnulTaskRound_against_ordinals_that_name_no_task_round_fails_with_a_stable_code(
        int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal)
    {
        var competition = DrawnF3J();

        var result = competition.AnnulTaskRound(phaseOrdinal, roundOrdinal, taskRoundOrdinal, "Weather", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("annulTaskRound.taskRoundNotFound");
    }

    [Fact]
    public void AnnulTaskRound_against_an_already_annulled_task_round_fails_with_a_stable_code()
    {
        var competition = DrawnF3J();
        competition = competition.Apply(new TaskRoundAnnulled(0, 1, 1, "Weather", At));

        var result = competition.AnnulTaskRound(0, 1, 1, "Weather again", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("annulTaskRound.alreadyAnnulled");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnnulTaskRound_with_a_blank_reason_fails_with_a_stable_code(string reason)
    {
        // Validated in the decide function, not the handler: unlike
        // BindParameter's By, the reason is a substantive record of a ruling.
        var competition = DrawnF3J();

        var result = competition.AnnulTaskRound(0, 1, 1, reason, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("annulTaskRound.reasonRequired");
    }

    [Fact]
    public void AnnulTaskRound_succeeds_carries_the_reason_through_and_folds_the_task_round_to_Annulled()
    {
        var competition = DrawnF3J();

        var result = competition.AnnulTaskRound(0, 1, 1, "Winch failure part-way through the group", At);

        result.IsSuccess.Should().BeTrue();
        result.Value.PhaseOrdinal.Should().Be(0);
        result.Value.RoundOrdinal.Should().Be(1);
        result.Value.TaskRoundOrdinal.Should().Be(1);
        result.Value.Reason.Should().Be("Winch failure part-way through the group");
        result.Value.At.Should().Be(At);

        competition = competition.Apply(result.Value);
        StateOf(competition, 0, 1, 1).Should().Be(TaskRoundState.Annulled);
    }

    [Fact]
    public void A_Complete_task_round_may_be_annulled()
    {
        // The reverse of CompleteTaskRound's rule: a round read out and then
        // found faulty is the ordinary case.
        var competition = DrawnF3J();
        competition = competition.Apply(new TaskRoundCompleted(0, 1, 1, At));

        var result = competition.AnnulTaskRound(0, 1, 1, "Timing sheet found to be for the wrong group", At);

        result.IsSuccess.Should().BeTrue();
        competition = competition.Apply(result.Value);
        StateOf(competition, 0, 1, 1).Should().Be(TaskRoundState.Annulled);
    }

    // ------------------------------------------------------------ ReopenTaskRound (WI-2b)

    [Theory]
    [InlineData(99, 1, 1)]
    [InlineData(0, 99, 1)]
    [InlineData(0, 1, 99)]
    public void ReopenTaskRound_against_ordinals_that_name_no_task_round_fails_with_a_stable_code(
        int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal)
    {
        var competition = DrawnF3J();
        competition = competition.Apply(new TaskRoundCompleted(0, 1, 1, At));

        var result = competition.ReopenTaskRound(phaseOrdinal, roundOrdinal, taskRoundOrdinal, "A late score", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("reopenTaskRound.taskRoundNotFound");
    }

    [Fact]
    public void ReopenTaskRound_against_a_task_round_that_is_still_Drawn_fails_with_a_stable_code()
    {
        var competition = DrawnF3J();

        var result = competition.ReopenTaskRound(0, 1, 1, "A late score", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("reopenTaskRound.notClosed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReopenTaskRound_with_a_blank_reason_fails_with_a_stable_code(string reason)
    {
        var competition = DrawnF3J();
        competition = competition.Apply(new TaskRoundCompleted(0, 1, 1, At));

        var result = competition.ReopenTaskRound(0, 1, 1, reason, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("reopenTaskRound.reasonRequired");
    }

    [Fact]
    public void ReopenTaskRound_from_Complete_succeeds_carries_the_reason_through_and_folds_back_to_Drawn()
    {
        var competition = DrawnF3J();
        competition = competition.Apply(new TaskRoundCompleted(0, 1, 1, At));

        var result = competition.ReopenTaskRound(0, 1, 1, "Score sheet handed in that evening", At);

        result.IsSuccess.Should().BeTrue();
        result.Value.PhaseOrdinal.Should().Be(0);
        result.Value.RoundOrdinal.Should().Be(1);
        result.Value.TaskRoundOrdinal.Should().Be(1);
        result.Value.Reason.Should().Be("Score sheet handed in that evening");
        result.Value.At.Should().Be(At);

        competition = competition.Apply(result.Value);
        StateOf(competition, 0, 1, 1).Should().Be(TaskRoundState.Drawn);
    }

    [Fact]
    public void ReopenTaskRound_from_Annulled_succeeds_and_folds_back_to_Drawn()
    {
        // An annulment made in error is as correctable as a premature
        // completion; refusing this would reintroduce the dead end
        // TaskRoundReopened exists to remove.
        var competition = DrawnF3J();
        competition = competition.Apply(new TaskRoundAnnulled(0, 1, 1, "Annulled in haste", At));

        var result = competition.ReopenTaskRound(0, 1, 1, "Annulment withdrawn on protest", At);

        result.IsSuccess.Should().BeTrue();
        competition = competition.Apply(result.Value);
        StateOf(competition, 0, 1, 1).Should().Be(TaskRoundState.Drawn);
    }

    [Fact]
    public void A_reopened_task_round_can_be_completed_again()
    {
        // Closure stays meaningful precisely because it is revocable: the
        // full round-trip has to be available, not just the reopening.
        var competition = DrawnF3J();
        competition = competition.Apply(new TaskRoundCompleted(0, 1, 1, At));
        competition = competition.Apply(new TaskRoundReopened(0, 1, 1, "Late score", At));

        var result = competition.CompleteTaskRound(0, 1, 1, At);

        result.IsSuccess.Should().BeTrue();
        competition = competition.Apply(result.Value);
        StateOf(competition, 0, 1, 1).Should().Be(TaskRoundState.Complete);
    }
}
