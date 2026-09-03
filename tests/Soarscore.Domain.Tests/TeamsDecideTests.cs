using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for the seven team commands — kanban/in-progress/teams-mvp.md
/// WI-3. Mirrors DrawAcceptanceDecideTests's style: the real F3J corpus
/// ClassDefinition (Soarscore.SeedData) rather than a hand-built fixture, one
/// fact per defect code (all fifteen, asserted by stable code) plus the happy
/// paths and the fold half of each emitted event. The phase-gate tests drive
/// the real draw lifecycle — drawn, accepted, rejected — with the draw's own
/// decide functions, exactly as the draw-acceptance tests do.
/// </summary>
public class TeamsDecideTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    private static Competition CompetitionAdopting(ClassDefinition definition, int competitorCount)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = At,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Teams Decide Test Comp", "Nowhere",
            new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 3),
            "1.0.0", adoptedRules, At);

        var competition = Competition.Create(created);

        for (var i = 0; i < competitorCount; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), At);
            competition = competition.Apply(registered.Value);
        }

        return competition;
    }

    /// <summary>F3J adopted, three registered competitors, no phase drawn — the base fixture.</summary>
    private static (Competition Competition, ImmutableArray<CompetitorId> Competitors) UndrawnF3J()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 3);
        return (competition, competition.Competitors.Select(c => c.Id).ToImmutableArray());
    }

    /// <summary>F3J adopted, twelve competitors, one round of the preliminary drawn — the live-phase fixture.</summary>
    private static Competition DrawnF3J()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var drawn = competition.DrawPhase(1, [], At);
        drawn.IsSuccess.Should().BeTrue(drawn.Code ?? "draw succeeded");
        return competition.Apply(drawn.Value);
    }

    private static (Competition Competition, ScoringTeamId TeamRef) WithScoringTeam(
        Competition competition, string name = "Eagles")
    {
        var teamId = ScoringTeamId.New();
        var defined = competition.DefineScoringTeam(teamId, name, At);
        defined.IsSuccess.Should().BeTrue(defined.Code ?? "scoring team defined");
        return (competition.Apply(defined.Value), teamId);
    }

    private static (Competition Competition, ProtectionGroupId GroupRef) WithProtectionGroup(
        Competition competition, string name = "Helpers")
    {
        var groupId = ProtectionGroupId.New();
        var defined = competition.DefineProtectionGroup(groupId, name, At);
        defined.IsSuccess.Should().BeTrue(defined.Code ?? "protection group defined");
        return (competition.Apply(defined.Value), groupId);
    }

    // ------------------------------------------------------- DefineScoringTeam

    [Fact]
    public void DefineScoringTeam_happy_path_folds_the_team_into_ScoringTeams()
    {
        var (competition, _) = UndrawnF3J();

        var teamId = ScoringTeamId.New();
        var result = competition.DefineScoringTeam(teamId, "Eagles", At);

        result.IsSuccess.Should().BeTrue();
        result.Value.Team.Id.Should().Be(teamId);
        result.Value.Team.Name.Should().Be("Eagles");
        result.Value.At.Should().Be(At);

        var updated = competition.Apply(result.Value);
        updated.ScoringTeams.Should().ContainSingle();
        updated.ScoringTeams[0].Id.Should().Be(teamId);
        updated.ScoringTeams[0].Name.Should().Be("Eagles");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DefineScoringTeam_with_a_blank_name_fails_with_a_stable_code(string blankName)
    {
        var (competition, _) = UndrawnF3J();

        var result = competition.DefineScoringTeam(ScoringTeamId.New(), blankName, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("defineScoringTeam.nameBlank");
    }

    [Fact]
    public void DefineScoringTeam_with_a_taken_name_fails_with_a_stable_code_case_insensitively()
    {
        var (competition, _) = UndrawnF3J();
        competition = WithScoringTeam(competition).Competition;

        var result = competition.DefineScoringTeam(ScoringTeamId.New(), "eagles", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("defineScoringTeam.nameTaken");
    }

    [Fact]
    public void DefineScoringTeam_may_share_a_name_with_a_protection_group()
    {
        // Uniqueness is enforced within a kind only — the two kinds are
        // unrelated vocabularies (owner decision 3's separation).
        var (competition, _) = UndrawnF3J();
        (competition, _) = WithProtectionGroup(competition, "Kestrels");

        var result = competition.DefineScoringTeam(ScoringTeamId.New(), "Kestrels", At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "scoring team defined");
    }

    // --------------------------------------------------- DefineProtectionGroup

    [Fact]
    public void DefineProtectionGroup_happy_path_folds_the_group_into_ProtectionGroups()
    {
        var (competition, _) = UndrawnF3J();

        var groupId = ProtectionGroupId.New();
        var result = competition.DefineProtectionGroup(groupId, "Helpers", At);

        result.IsSuccess.Should().BeTrue();
        result.Value.Group.Id.Should().Be(groupId);
        result.Value.Group.Name.Should().Be("Helpers");

        var updated = competition.Apply(result.Value);
        updated.ProtectionGroups.Should().ContainSingle();
        updated.ProtectionGroups[0].Id.Should().Be(groupId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DefineProtectionGroup_with_a_blank_name_fails_with_a_stable_code(string blankName)
    {
        var (competition, _) = UndrawnF3J();

        var result = competition.DefineProtectionGroup(ProtectionGroupId.New(), blankName, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("defineProtectionGroup.nameBlank");
    }

    [Fact]
    public void DefineProtectionGroup_with_a_taken_name_fails_with_a_stable_code_case_insensitively()
    {
        var (competition, _) = UndrawnF3J();
        (competition, _) = WithProtectionGroup(competition, "Helpers");

        var result = competition.DefineProtectionGroup(ProtectionGroupId.New(), "HELPERS", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("defineProtectionGroup.nameTaken");
    }

    [Fact]
    public void DefineProtectionGroup_may_share_a_name_with_a_scoring_team()
    {
        var (competition, _) = UndrawnF3J();
        (competition, _) = WithScoringTeam(competition, "Falcons");

        var result = competition.DefineProtectionGroup(ProtectionGroupId.New(), "falcons", At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "protection group defined");
    }

    // --------------------------------------------- AssignScoringTeamMembership

    [Fact]
    public void AssignScoringTeamMembership_happy_path_folds_the_membership()
    {
        var (competition, competitors) = UndrawnF3J();
        (competition, var teamRef) = WithScoringTeam(competition);

        var result = competition.AssignScoringTeamMembership(competitors[0], teamRef, contributes: true, At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "assignment succeeded");
        result.Value.Membership.CompetitorRef.Should().Be(competitors[0]);
        result.Value.Membership.TeamRef.Should().Be(teamRef);
        result.Value.Membership.Contributes.Should().BeTrue();

        var updated = competition.Apply(result.Value);
        updated.ScoringTeamMemberships.Should().ContainSingle();
        updated.ScoringTeamMemberships[0].CompetitorRef.Should().Be(competitors[0]);
    }

    [Fact]
    public void AssignScoringTeamMembership_to_an_unknown_team_fails_with_a_stable_code()
    {
        var (competition, competitors) = UndrawnF3J();

        var result = competition.AssignScoringTeamMembership(competitors[0], ScoringTeamId.New(), true, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignTeamMembership.teamNotFound");
    }

    [Fact]
    public void AssignScoringTeamMembership_for_an_unknown_competitor_fails_with_a_stable_code()
    {
        var (competition, _) = UndrawnF3J();
        (competition, var teamRef) = WithScoringTeam(competition);

        var result = competition.AssignScoringTeamMembership(CompetitorId.New(), teamRef, true, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignTeamMembership.competitorNotFound");
    }

    [Fact]
    public void AssignScoringTeamMembership_for_a_withdrawn_competitor_fails_with_a_stable_code()
    {
        var (competition, competitors) = UndrawnF3J();
        (competition, var teamRef) = WithScoringTeam(competition);
        competition = competition.Apply(competition.WithdrawCompetitor(competitors[0], At).Value);

        var result = competition.AssignScoringTeamMembership(competitors[0], teamRef, true, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignTeamMembership.competitorWithdrawn");
    }

    [Fact]
    public void AssignScoringTeamMembership_naming_a_different_team_while_assigned_fails_with_a_stable_code()
    {
        var (competition, competitors) = UndrawnF3J();
        (competition, var firstTeam) = WithScoringTeam(competition, "Eagles");
        (competition, var secondTeam) = WithScoringTeam(competition, "Kestrels");

        var assigned = competition.AssignScoringTeamMembership(competitors[0], firstTeam, true, At);
        assigned.IsSuccess.Should().BeTrue();
        competition = competition.Apply(assigned.Value);

        var result = competition.AssignScoringTeamMembership(competitors[0], secondTeam, true, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignTeamMembership.competitorAlreadyAssigned");
    }

    [Fact]
    public void Re_assigning_the_same_team_is_the_eligibility_correction_path_and_replaces_the_record()
    {
        var (competition, competitors) = UndrawnF3J();
        (competition, var teamRef) = WithScoringTeam(competition);

        var first = competition.AssignScoringTeamMembership(competitors[0], teamRef, contributes: true, At);
        first.IsSuccess.Should().BeTrue();
        competition = competition.Apply(first.Value);

        // The defending-champion correction: same team, contribution off.
        var second = competition.AssignScoringTeamMembership(competitors[0], teamRef, contributes: false, At);

        second.IsSuccess.Should().BeTrue(second.Code ?? "re-assignment succeeded");
        second.Value.Membership.Contributes.Should().BeFalse();

        var updated = competition.Apply(second.Value);
        updated.ScoringTeamMemberships.Should().ContainSingle();
        updated.ScoringTeamMemberships[0].Contributes.Should().BeFalse();
    }

    // --------------------------------------------- ClearScoringTeamMembership

    [Fact]
    public void ClearScoringTeamMembership_removes_the_membership()
    {
        var (competition, competitors) = UndrawnF3J();
        (competition, var teamRef) = WithScoringTeam(competition);

        var assigned = competition.AssignScoringTeamMembership(competitors[0], teamRef, true, At);
        competition = competition.Apply(assigned.Value);

        var result = competition.ClearScoringTeamMembership(competitors[0], At);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompetitorRef.Should().Be(competitors[0]);

        var updated = competition.Apply(result.Value);
        updated.ScoringTeamMemberships.Should().BeEmpty();
    }

    [Fact]
    public void ClearScoringTeamMembership_with_no_membership_fails_with_a_stable_code()
    {
        var (competition, competitors) = UndrawnF3J();

        var result = competition.ClearScoringTeamMembership(competitors[0], At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("clearTeamMembership.membershipNotFound");
    }

    // --------------------------------------------------- AddProtectionGroupMember

    [Fact]
    public void AddProtectionGroupMember_happy_path_folds_the_membership()
    {
        var (competition, competitors) = UndrawnF3J();
        (competition, var groupRef) = WithProtectionGroup(competition);

        var result = competition.AddProtectionGroupMember(competitors[0], groupRef, At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "add succeeded");
        result.Value.Membership.CompetitorRef.Should().Be(competitors[0]);
        result.Value.Membership.GroupRef.Should().Be(groupRef);

        var updated = competition.Apply(result.Value);
        updated.ProtectionGroupMemberships.Should().ContainSingle();
        updated.ProtectionGroupMemberships[0].GroupRef.Should().Be(groupRef);
    }

    [Fact]
    public void A_competitor_may_hold_memberships_in_many_protection_groups()
    {
        // Multi-group membership is allowed and expected (owner decision 3) —
        // only a duplicate of THIS group is refused.
        var (competition, competitors) = UndrawnF3J();
        (competition, var firstGroup) = WithProtectionGroup(competition, "Helpers");
        (competition, var secondGroup) = WithProtectionGroup(competition, "Juniors");

        var first = competition.AddProtectionGroupMember(competitors[0], firstGroup, At);
        first.IsSuccess.Should().BeTrue();
        competition = competition.Apply(first.Value);

        var second = competition.AddProtectionGroupMember(competitors[0], secondGroup, At);

        second.IsSuccess.Should().BeTrue(second.Code ?? "second-group add succeeded");
        competition = competition.Apply(second.Value);
        competition.ProtectionGroupMemberships.Should().HaveCount(2);
    }

    [Fact]
    public void AddProtectionGroupMember_is_refused_while_a_phase_is_drawn_but_not_accepted()
    {
        var competition = DrawnF3J();
        (competition, var groupRef) = WithProtectionGroup(competition);

        var result = competition.AddProtectionGroupMember(competition.Competitors[0].Id, groupRef, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("addProtectionMember.drawExists");
        result.Message.Should().Be(
            "Protection membership is frozen once a phase has been drawn; reject the draw first.");
    }

    [Fact]
    public void AddProtectionGroupMember_is_refused_once_the_draw_is_accepted()
    {
        var competition = DrawnF3J();
        competition = competition.Apply(competition.AcceptDraw(At).Value);
        (competition, var groupRef) = WithProtectionGroup(competition);

        var result = competition.AddProtectionGroupMember(competition.Competitors[0].Id, groupRef, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("addProtectionMember.drawExists");
    }

    [Fact]
    public void AddProtectionGroupMember_is_allowed_again_after_the_draw_is_rejected()
    {
        var competition = DrawnF3J();

        var rejected = competition.RejectDraw(phaseHasEntries: false, "A late entrant must be included", At);
        rejected.IsSuccess.Should().BeTrue();
        competition = competition.Apply(rejected.Value);

        (competition, var groupRef) = WithProtectionGroup(competition);

        var result = competition.AddProtectionGroupMember(competition.Competitors[0].Id, groupRef, At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "add after rejection succeeded");
    }

    [Fact]
    public void AddProtectionGroupMember_to_an_unknown_group_fails_with_a_stable_code()
    {
        var (competition, competitors) = UndrawnF3J();

        var result = competition.AddProtectionGroupMember(competitors[0], ProtectionGroupId.New(), At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("addProtectionMember.groupNotFound");
    }

    [Fact]
    public void AddProtectionGroupMember_for_an_unknown_competitor_fails_with_a_stable_code()
    {
        var (competition, _) = UndrawnF3J();
        (competition, var groupRef) = WithProtectionGroup(competition);

        var result = competition.AddProtectionGroupMember(CompetitorId.New(), groupRef, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("addProtectionMember.competitorNotFound");
    }

    [Fact]
    public void AddProtectionGroupMember_for_a_withdrawn_competitor_fails_with_a_stable_code()
    {
        var (competition, competitors) = UndrawnF3J();
        (competition, var groupRef) = WithProtectionGroup(competition);
        competition = competition.Apply(competition.WithdrawCompetitor(competitors[1], At).Value);

        var result = competition.AddProtectionGroupMember(competitors[1], groupRef, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("addProtectionMember.competitorWithdrawn");
    }

    [Fact]
    public void AddProtectionGroupMember_twice_for_the_same_group_fails_with_a_stable_code()
    {
        var (competition, competitors) = UndrawnF3J();
        (competition, var groupRef) = WithProtectionGroup(competition);

        var first = competition.AddProtectionGroupMember(competitors[0], groupRef, At);
        first.IsSuccess.Should().BeTrue();
        competition = competition.Apply(first.Value);

        var result = competition.AddProtectionGroupMember(competitors[0], groupRef, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("addProtectionMember.duplicateMembership");
    }

    // ------------------------------------------------ RemoveProtectionGroupMember

    [Fact]
    public void RemoveProtectionGroupMember_filters_the_matching_pair()
    {
        var (competition, competitors) = UndrawnF3J();
        (competition, var groupRef) = WithProtectionGroup(competition);

        var added = competition.AddProtectionGroupMember(competitors[0], groupRef, At);
        competition = competition.Apply(added.Value);

        var result = competition.RemoveProtectionGroupMember(competitors[0], groupRef, At);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompetitorRef.Should().Be(competitors[0]);
        result.Value.GroupRef.Should().Be(groupRef);

        var updated = competition.Apply(result.Value);
        updated.ProtectionGroupMemberships.Should().BeEmpty();
    }

    [Fact]
    public void RemoveProtectionGroupMember_is_refused_while_a_phase_exists()
    {
        var competition = DrawnF3J();
        (competition, var groupRef) = WithProtectionGroup(competition);

        var result = competition.RemoveProtectionGroupMember(competition.Competitors[0].Id, groupRef, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("removeProtectionMember.drawExists");
        result.Message.Should().Be(
            "Protection membership is frozen once a phase has been drawn; reject the draw first.");
    }

    [Fact]
    public void RemoveProtectionGroupMember_with_no_matching_membership_fails_with_a_stable_code()
    {
        var (competition, competitors) = UndrawnF3J();
        (competition, var groupRef) = WithProtectionGroup(competition);

        var result = competition.RemoveProtectionGroupMember(competitors[0], groupRef, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("removeProtectionMember.membershipNotFound");
    }

    // ------------------------------------------- ConfigureTeamClassification

    [Fact]
    public void ConfigureTeamClassification_happy_path_folds_the_configuration()
    {
        var (competition, _) = UndrawnF3J();

        var result = competition.ConfigureTeamClassification(enabled: true, "CD", At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "configuration succeeded");
        result.Value.Configuration.Enabled.Should().BeTrue();

        // The MVP's closed vocabulary has exactly one member (owner decision 7).
        result.Value.Configuration.Method.Should().Be("bestThreeScoreSum");

        var updated = competition.Apply(result.Value);
        updated.TeamClassification.Should().NotBeNull();
        updated.TeamClassification!.Enabled.Should().BeTrue();
        updated.TeamClassification.Method.Should().Be("bestThreeScoreSum");
    }

    [Fact]
    public void ConfigureTeamClassification_reconfiguration_is_allowed_and_replaces_last_wins()
    {
        var (competition, _) = UndrawnF3J();

        var first = competition.ConfigureTeamClassification(enabled: true, "CD", At);
        first.IsSuccess.Should().BeTrue();
        competition = competition.Apply(first.Value);

        var second = competition.ConfigureTeamClassification(enabled: false, "CD", At);

        second.IsSuccess.Should().BeTrue(second.Code ?? "reconfiguration succeeded");
        var updated = competition.Apply(second.Value);
        updated.TeamClassification.Should().NotBeNull();
        updated.TeamClassification!.Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConfigureTeamClassification_with_a_blank_by_fails_with_a_stable_code(string blankBy)
    {
        var (competition, _) = UndrawnF3J();

        var result = competition.ConfigureTeamClassification(enabled: true, blankBy, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("configureTeamClassification.byBlank");
    }
}
