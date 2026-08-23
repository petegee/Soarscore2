using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.AppendReflightGroup"/> —
/// kanban/in-progress/reflight-groups.md WI-2. One fact per defect code plus
/// the happy path, mirroring TaskRoundLifecycleDecideTests's style. The
/// draw itself is bypassed — F3K is ChooseFromCatalogue and DrawPhase does not
/// support every corpus composition — so the Phase/Round/TaskRound/Group shape
/// is hand-built directly, the same way OpenEntryDecideTests does for its own
/// tests. The class rule under test is the real F3K corpus one (F3K.9.6:
/// Replacement/BetterOf, min new group size 4); the parameterised-min and
/// NotPermitted shapes are hand-built from the same corpus base.
/// </summary>
public class AppendReflightGroupDecideTests
{
    private static readonly DateTimeOffset At = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// F3K, <paramref name="competitorCount"/> registered competitors, one
    /// hand-built task-round (ordinal 1, task A) in the requested state.
    /// </summary>
    private static (Competition Competition, ImmutableArray<CompetitorId> Competitors) BuildDrawnCompetition(
        ClassDefinition definition,
        string taskCode,
        int competitorCount,
        Soarscore.Domain.Competitions.TaskRoundState taskRoundState =
            Soarscore.Domain.Competitions.TaskRoundState.Drawn)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = At,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Reflight Append Test Comp", "Nowhere",
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

        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [competitors[0]] };
        var taskRound = new TaskRound { Ordinal = 1, State = taskRoundState, TaskRef = taskCode, Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = At, Status = "drawn" };
        competition = competition.Apply(new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], At));

        return (competition, competitors.ToImmutable());
    }

    /// <summary>
    /// Swaps the class's Reflight rule — F3K's own corpus rule (min 4) and a
    /// hand-built one for the cases the corpus does not contain.
    /// </summary>
    private static ClassDefinition WithReflightRule(ClassDefinition definition, ReflightRule rule) =>
        definition with { Reflight = rule };

    /// <summary>F3K with the reflight minimum swapped to an unbound Param — isolates the parameter-resolution path.</summary>
    private static ClassDefinition WithUnboundMinNewGroup(ClassDefinition definition)
    {
        var reflight = definition.Reflight with
        {
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),
        };
        var parameters = definition.Parameters.Add(new Parameter
        {
            Name = "minNewGroup",
            Kind = MeasuredKind.Number,
            BoundAt = ParameterBindingPoint.BeforeFlying,
        });
        return definition with { Reflight = reflight, Parameters = parameters };
    }

    [Fact]
    public void AppendReflightGroup_against_ordinals_that_name_no_task_round_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition, "A", 8);

        var result = competition.AppendReflightGroup(
            99, 1, 1, [competitors[0], competitors[1], competitors[2], competitors[3]], "Mid-air collision", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.taskRoundNotFound");
    }

    [Fact]
    public void AppendReflightGroup_against_an_annulled_task_round_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition, "A", 8);
        competition = competition.Apply(new TaskRoundAnnulled(0, 1, 1, "Winch failure", At));

        var result = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0], competitors[1], competitors[2], competitors[3]], "Mid-air collision", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.taskRoundAnnulled");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AppendReflightGroup_with_a_blank_reason_fails_with_a_stable_code(string reason)
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition, "A", 8);

        var result = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0], competitors[1], competitors[2], competitors[3]], reason, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.reasonRequired");
    }

    [Fact]
    public void AppendReflightGroup_under_a_class_that_never_forms_new_groups_fails_with_a_stable_code()
    {
        // F3F.1.5 re-flies one pilot into the running order — MinNewGroupSize
        // is null on the corpus class, so no new group is ever formed (F26).
        var (competition, competitors) = BuildDrawnCompetition(SeedF3F.Definition, "S", 1);

        var result = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0]], "Faulty launcher", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.newGroupNeverFormed");
    }

    [Fact]
    public void AppendReflightGroup_under_a_class_forbidding_reflights_fails_with_a_stable_code()
    {
        // Belt-and-braces shape the corpus does not contain: a non-null min
        // paired with a NotPermitted selection (NZ N/P declare null min, so
        // they ordinarily refuse at newGroupNeverFormed first).
        var definition = WithReflightRule(SeedF3K.Definition, new ReflightRule
        {
            EntitledScores = ReflightSelection.NotPermitted,
            OthersScore = ReflightSelection.NotPermitted,
            MinNewGroupSize = 4,
        });
        var (competition, competitors) = BuildDrawnCompetition(definition, "A", 8);

        var result = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0], competitors[1], competitors[2], competitors[3]], "Mid-air collision", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.notPermitted");
    }

    [Fact]
    public void AppendReflightGroup_under_an_unbound_parameterised_minimum_fails_with_a_stable_code()
    {
        var definition = WithUnboundMinNewGroup(SeedF3K.Definition);
        var (competition, competitors) = BuildDrawnCompetition(definition, "A", 8);

        var result = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0], competitors[1], competitors[2], competitors[3]], "Mid-air collision", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.parameterUnbound");
    }

    [Fact]
    public void AppendReflightGroup_with_an_unbound_minimum_is_unblocked_by_BindParameter()
    {
        var definition = WithUnboundMinNewGroup(SeedF3K.Definition);
        var (competition, competitors) = BuildDrawnCompetition(definition, "A", 8);

        var bound = competition.BindParameter("minNewGroup", MeasuredValue.Of(4m), "CD", At);
        bound.IsSuccess.Should().BeTrue();
        competition = competition.Apply(bound.Value);

        var result = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0], competitors[1], competitors[2], competitors[3]], "Mid-air collision", At);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AppendReflightGroup_with_an_empty_member_list_fails_with_a_stable_code()
    {
        var (competition, _) = BuildDrawnCompetition(SeedF3K.Definition, "A", 8);

        var result = competition.AppendReflightGroup(0, 1, 1, [], "Mid-air collision", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.membersEmpty");
    }

    [Fact]
    public void AppendReflightGroup_with_an_unregistered_member_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition, "A", 8);

        var result = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0], competitors[1], competitors[2], CompetitorId.New()], "Mid-air collision", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.memberNotRegistered");
    }

    [Fact]
    public void AppendReflightGroup_with_a_withdrawn_member_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition, "A", 8);
        competition = competition.Apply(competition.WithdrawCompetitor(competitors[0], At).Value);

        var result = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0], competitors[1], competitors[2], competitors[3]], "Mid-air collision", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.memberWithdrawn");
    }

    [Fact]
    public void AppendReflightGroup_with_a_duplicated_member_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition, "A", 8);

        var result = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0], competitors[0], competitors[1], competitors[2]], "Mid-air collision", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.memberDuplicated");
    }

    [Fact]
    public void AppendReflightGroup_below_the_class_minimum_fails_with_a_stable_code()
    {
        // F3K.9.6 requires at least 4 in a new reflight group.
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition, "A", 8);

        var result = competition.AppendReflightGroup(
            0, 1, 1, [competitors[0], competitors[1], competitors[2]], "Mid-air collision", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("appendReflightGroup.groupTooSmall");
    }

    [Fact]
    public void AppendReflightGroup_succeeds_mints_a_new_group_and_folds_at_most_one_group_on()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition, "A", 8);
        var pre = competition.Phases[0].Rounds[0].TaskRounds[0];

        var members = ImmutableArray.Create(competitors[0], competitors[1], competitors[2], competitors[3]);
        var result = competition.AppendReflightGroup(0, 1, 1, members, "Mid-air collision", At);

        result.IsSuccess.Should().BeTrue();
        result.Value.PhaseOrdinal.Should().Be(0);
        result.Value.RoundOrdinal.Should().Be(1);
        result.Value.TaskRoundOrdinal.Should().Be(1);
        result.Value.Reason.Should().Be("Mid-air collision");
        result.Value.At.Should().Be(At);
        result.Value.Group.CompetitorRefs.Should().Equal(members);

        // A fresh group id — never one already present in the task-round.
        pre.Groups.Should().NotContain(g => g.Id == result.Value.Group.Id);
        result.Value.Group.Ordinal.Should().Be(pre.Groups.Length + 1);

        var updated = competition.Apply(result.Value);
        var post = updated.Phases[0].Rounds[0].TaskRounds[0];
        post.Groups.Length.Should().Be(pre.Groups.Length + 1);
        post.Groups.Should().Contain(g => g.Id == result.Value.Group.Id);
    }
}