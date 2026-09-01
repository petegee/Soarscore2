using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.AssignGroupSpots"/> —
/// kanban/in-progress/lane-assignment.md WI-6 (Stage 1). One fact per defect
/// code (all nine, asserted by stable code) plus the happy paths, mirroring
/// AppendReflightGroupDecideTests's style. The draw itself is bypassed — F3K
/// is ChooseFromCatalogue and DrawPhase is not needed for the decide checks —
/// so the Phase/Round/TaskRound/Group shape is hand-built directly; the
/// reject-draw lifecycle test uses the real DrawPhase/RejectDraw decide
/// functions for the redraw half. The class is the real F3K corpus one; the
/// rules check (fai-rules, 2026-08-31) found no per-class variation in spot
/// data shape, so the class under the rules is irrelevant to these checks.
/// </summary>
public class GroupSpotsDecideTests
{
    private static readonly DateTimeOffset At = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// F3K, <paramref name="competitorCount"/> registered competitors, one
    /// hand-built task-round (ordinal 1, task A) in the requested state, whose
    /// first group holds the first <paramref name="groupSize"/> competitors.
    /// </summary>
    private static (Competition Competition, ImmutableArray<CompetitorId> Competitors, GroupId GroupRef)
        BuildDrawnCompetition(
            int competitorCount = 8,
            int groupSize = 8,
            Soarscore.Domain.Competitions.TaskRoundState taskRoundState =
                Soarscore.Domain.Competitions.TaskRoundState.Drawn)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = SeedF3K.Definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SeedF3K.Definition.Version,
            AdoptedAt = At,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Group Spots Decide Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, At);

        var competition = Competition.Create(created);

        var competitors = ImmutableArray.CreateBuilder<CompetitorId>();
        for (var i = 0; i < competitorCount; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), At);
            competition = competition.Apply(registered.Value);
            competitors.Add(registered.Value.Competitor.Id);
        }

        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [.. competitors.Take(groupSize)] };
        var taskRound = new TaskRound
        {
            Ordinal = 1,
            State = taskRoundState,
            TaskRef = "A",
            Groups = [group],
        };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = At, Status = "drawn" };
        competition = competition.Apply(new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], At));

        return (competition, competitors.ToImmutable(), group.Id);
    }

    private static IReadOnlyList<GroupSpot> Coverage(IEnumerable<CompetitorId> members, int baseSpot = 1) =>
        [.. members.Select((member, index) => new GroupSpot(member, baseSpot + index))];

    [Fact]
    public void AssignGroupSpots_against_ordinals_that_name_no_task_round_fails_with_a_stable_code()
    {
        var (competition, competitors, groupRef) = BuildDrawnCompetition();

        var result = competition.AssignGroupSpots(99, 1, 1, groupRef, Coverage(competitors.Take(2)), At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.taskRoundNotFound");
    }

    [Fact]
    public void AssignGroupSpots_against_an_annulled_task_round_fails_with_a_stable_code()
    {
        var (competition, competitors, groupRef) = BuildDrawnCompetition();
        competition = competition.Apply(new TaskRoundAnnulled(0, 1, 1, "Winch failure", At));

        var result = competition.AssignGroupSpots(0, 1, 1, groupRef, Coverage(competitors), At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.taskRoundAnnulled");
    }

    [Fact]
    public void Complete_InProgress_and_Drawn_task_rounds_all_accept_assignments()
    {
        // The AppendReflightGroup precedent — annulment is the only state gate
        // (D3): spots are operational configuration, assignable while the
        // round lives.
        foreach (var state in new[]
                 {
                     Soarscore.Domain.Competitions.TaskRoundState.Drawn,
                     Soarscore.Domain.Competitions.TaskRoundState.InProgress,
                     Soarscore.Domain.Competitions.TaskRoundState.Complete,
                 })
        {
            var (competition, competitors, groupRef) = BuildDrawnCompetition(taskRoundState: state);

            var result = competition.AssignGroupSpots(0, 1, 1, groupRef, Coverage(competitors), At);

            result.IsSuccess.Should().BeTrue($"{state} task-rounds allow assignment (got {result.Code})");
        }
    }

    [Fact]
    public void AssignGroupSpots_to_a_group_not_in_this_task_round_fails_with_a_stable_code()
    {
        var (competition, competitors, _) = BuildDrawnCompetition();

        var result = competition.AssignGroupSpots(0, 1, 1, GroupId.New(), Coverage(competitors), At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.groupNotFound");
    }

    [Fact]
    public void AssignGroupSpots_with_an_empty_list_fails_with_a_stable_code()
    {
        var (competition, _, groupRef) = BuildDrawnCompetition();

        var result = competition.AssignGroupSpots(0, 1, 1, groupRef, [], At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.assignmentsEmpty");
    }

    [Fact]
    public void AssignGroupSpots_to_a_competitor_not_drawn_into_the_group_fails_with_a_stable_code()
    {
        var (competition, _, groupRef) = BuildDrawnCompetition();

        var spots = new List<GroupSpot> { new(CompetitorId.New(), 1) };

        var result = competition.AssignGroupSpots(0, 1, 1, groupRef, spots, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.competitorNotInGroup");
    }

    [Fact]
    public void AssignGroupSpots_to_a_withdrawn_competitor_fails_with_a_stable_code()
    {
        var (competition, competitors, groupRef) = BuildDrawnCompetition();
        competition = competition.Apply(competition.WithdrawCompetitor(competitors[0], At).Value);

        // The withdrawn id is still drawn into the group — live membership is
        // the decide function's derivation (drawn ∧ ¬withdrawn), not the
        // caller's.
        var result = competition.AssignGroupSpots(
            0, 1, 1, groupRef, Coverage(competitors), At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.competitorNotInGroup");
    }

    [Fact]
    public void AssignGroupSpots_naming_a_competitor_twice_fails_with_a_stable_code()
    {
        var (competition, competitors, groupRef) = BuildDrawnCompetition();

        var spots = new List<GroupSpot>
        {
            new(competitors[0], 1),
            new(competitors[0], 2),
            new(competitors[1], 3),
        };

        var result = competition.AssignGroupSpots(0, 1, 1, groupRef, spots, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.competitorRepeated");
    }

    [Fact]
    public void AssignGroupSpots_giving_a_spot_number_twice_fails_with_a_stable_code()
    {
        var (competition, competitors, groupRef) = BuildDrawnCompetition();

        var spots = new List<GroupSpot>
        {
            new(competitors[0], 1),
            new(competitors[1], 1),
            new(competitors[2], 2),
        };

        var result = competition.AssignGroupSpots(0, 1, 1, groupRef, spots, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.spotDuplicated");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AssignGroupSpots_with_a_spot_number_below_one_fails_with_a_stable_code(int spot)
    {
        var (competition, competitors, groupRef) = BuildDrawnCompetition();

        var spots = new List<GroupSpot> { new(competitors[0], spot), new(competitors[1], 2) };

        var result = competition.AssignGroupSpots(0, 1, 1, groupRef, spots, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.spotInvalid");
    }

    [Fact]
    public void AssignGroupSpots_that_leaves_a_live_member_unassigned_fails_with_a_stable_code()
    {
        var (competition, competitors, groupRef) = BuildDrawnCompetition();

        // Full coverage (D4): seven of eight live members assigned.
        var result = competition.AssignGroupSpots(0, 1, 1, groupRef, Coverage(competitors.Take(7)), At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.memberMissing");
    }

    [Fact]
    public void AssignGroupSpots_happy_path_folds_Group_Spots_to_exactly_the_commanded_set_as_given()
    {
        var (competition, competitors, groupRef) = BuildDrawnCompetition();

        // Distinct positive integers, deliberately non-contiguous (D1: a
        // broken lane skipped is the ordinary case), in a scrambled order —
        // the fold must store them as given, no reordering.
        var commanded = new List<GroupSpot>
        {
            new(competitors[0], 3),
            new(competitors[1], 1),
            new(competitors[2], 7),
            new(competitors[3], 2),
            new(competitors[4], 13),
            new(competitors[5], 5),
            new(competitors[6], 21),
            new(competitors[7], 8),
        };

        var result = competition.AssignGroupSpots(0, 1, 1, groupRef, commanded, At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "assignment succeeded");
        result.Value.PhaseOrdinal.Should().Be(0);
        result.Value.RoundOrdinal.Should().Be(1);
        result.Value.TaskRoundOrdinal.Should().Be(1);
        result.Value.GroupRef.Should().Be(groupRef);
        result.Value.At.Should().Be(At);
        result.Value.Spots.ToArray().Should().Equal(commanded);

        var updated = competition.Apply(result.Value);
        var group = updated.Phases[0].Rounds[0].TaskRounds[0].Groups.Single(g => g.Id == groupRef);
        group.Spots.ToArray().Should().Equal(commanded);
    }

    [Fact]
    public void Re_assignment_replaces_the_previous_assignment_in_its_entirety()
    {
        // The story's A→1,B→2 then B→2,C→1 shorthand, made whole-mapping
        // legal under D4 (every live member covered by both commands).
        var (competition, competitors, groupRef) = BuildDrawnCompetition(competitorCount: 3, groupSize: 3);

        var first = new List<GroupSpot>
        {
            new(competitors[0], 1),
            new(competitors[1], 2),
            new(competitors[2], 3),
        };
        var second = new List<GroupSpot>
        {
            new(competitors[2], 2),
            new(competitors[0], 1),
            new(competitors[1], 3),
        };

        var firstResult = competition.AssignGroupSpots(0, 1, 1, groupRef, first, At);
        firstResult.IsSuccess.Should().BeTrue(firstResult.Code ?? "first assignment succeeded");
        competition = competition.Apply(firstResult.Value);

        var secondResult = competition.AssignGroupSpots(0, 1, 1, groupRef, second, At);
        secondResult.IsSuccess.Should().BeTrue(secondResult.Code ?? "second assignment succeeded");
        competition = competition.Apply(secondResult.Value);

        var group = competition.Phases[0].Rounds[0].TaskRounds[0].Groups.Single(g => g.Id == groupRef);
        group.Spots.ToArray().Should().Equal(second);
        group.Spots.Length.Should().Be(3);
    }

    [Fact]
    public void Withdrawal_after_assignment_leaves_Spots_intact_reading_as_vacant()
    {
        var (competition, competitors, groupRef) = BuildDrawnCompetition(competitorCount: 3, groupSize: 3);

        var assigned = competition.AssignGroupSpots(0, 1, 1, groupRef, Coverage(competitors), At);
        assigned.IsSuccess.Should().BeTrue();
        competition = competition.Apply(assigned.Value);

        competition = competition.Apply(competition.WithdrawCompetitor(competitors[2], At).Value);

        var group = competition.Phases[0].Rounds[0].TaskRounds[0].Groups.Single(g => g.Id == groupRef);
        group.Spots.ToArray().Should().Equal(Coverage(competitors));
    }

    [Fact]
    public void Rejecting_the_draw_removes_the_phase_and_every_assignment_and_the_redraw_starts_unassigned()
    {
        var (competition, competitors, groupRef) = BuildDrawnCompetition();

        var assigned = competition.AssignGroupSpots(0, 1, 1, groupRef, Coverage(competitors), At);
        assigned.IsSuccess.Should().BeTrue();
        competition = competition.Apply(assigned.Value);

        var rejected = competition.RejectDraw(phaseHasEntries: false, "A late entrant must be included", At);
        rejected.IsSuccess.Should().BeTrue();
        competition = competition.Apply(rejected.Value);

        competition.Phases.Should().BeEmpty();

        // Redraw: eligible field still ≥ F3K's minPerGroup (5), so the draw
        // succeeds and mints fresh groups — which start unassigned (D3's
        // dies-with-the-draw claim, asserted concretely).
        var redrawn = competition.DrawPhase(1, ["A"], At);
        redrawn.IsSuccess.Should().BeTrue(redrawn.Code ?? "redraw succeeded");
        competition = competition.Apply(redrawn.Value);

        var redrawnGroups = competition.Phases[0].Rounds[0].TaskRounds[0].Groups;
        foreach (var group in redrawnGroups)
        {
            group.Spots.IsEmpty.Should().BeTrue();
        }

        // And the replacement draw's group accepts an assignment like any
        // other — with the mapping over its own live members.
        var newGroup = redrawnGroups[0];
        var reassign = competition.AssignGroupSpots(0, 1, 1, newGroup.Id, Coverage(newGroup.CompetitorRefs), At);
        reassign.IsSuccess.Should().BeTrue(reassign.Code ?? "re-assignment on the redraw succeeded");
    }

    [Fact]
    public void An_appended_reflight_group_accepts_an_assignment_like_any_drawn_group()
    {
        // Rules check: re-flight priority 1's "additional launch spots" —
        // appended reflight groups are spot consumers too.
        var (competition, competitors, _) = BuildDrawnCompetition();

        var appended = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0], competitors[1], competitors[2], competitors[3]], "Mid-air collision", At);
        appended.IsSuccess.Should().BeTrue();
        competition = competition.Apply(appended.Value);

        var reflightGroup = appended.Value.Group;
        var result = competition.AssignGroupSpots(
            0, 1, 1, reflightGroup.Id, Coverage(reflightGroup.CompetitorRefs), At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "assignment to the reflight group succeeded");

        var updated = competition.Apply(result.Value);
        var folded = updated.Phases[0].Rounds[0].TaskRounds[0].Groups.Single(g => g.Id == reflightGroup.Id);
        folded.Spots.Length.Should().Be(4);
    }
}
