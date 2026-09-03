using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Fold-semantics tests for the seven team events — kanban/in-progress/teams-mvp.md
/// WI-3, mirroring CompetitionFoldTests's style: events applied directly, fold
/// effects asserted on the projection. The story's fold contract: membership
/// Assigned REPLACES any existing record for that competitor; Cleared removes
/// all records for the competitor; MemberAdded adds (the decide function
/// prevents duplicates); MemberRemoved filters the matching pair;
/// TeamClassificationConfigured replaces last-wins — the log is the audit
/// trail, the ParameterBindings precedent.
/// </summary>
public class TeamsFoldTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static CompetitionCreated SampleCreatedEvent() =>
        new(
            CompetitionId.New(),
            "Club Champs 2026",
            "Auckland",
            new DateOnly(2026, 9, 2),
            new DateOnly(2026, 9, 3),
            "1.0.0",
            new AdoptedRules
            {
                Definition = SampleDefinition,
                SourceClassId = "content-hash-abc123",
                SourceVersion = SampleDefinition.Version,
                AdoptedAt = At,
            },
            At);

    [Fact]
    public void Created_leaves_the_team_state_default_empty_and_unconfigured()
    {
        var competition = Competition.Create(SampleCreatedEvent());

        competition.ScoringTeams.Should().BeEmpty();
        competition.ProtectionGroups.Should().BeEmpty();
        competition.ScoringTeamMemberships.Should().BeEmpty();
        competition.ProtectionGroupMemberships.Should().BeEmpty();
        competition.TeamClassification.Should().BeNull();
    }

    [Fact]
    public void ScoringTeamMembershipAssigned_replaces_the_existing_record_for_that_competitor()
    {
        var competition = Competition.Create(SampleCreatedEvent());

        var firstTeam = new ScoringTeam { Id = ScoringTeamId.New(), Name = "Eagles" };
        var secondTeam = new ScoringTeam { Id = ScoringTeamId.New(), Name = "Kestrels" };
        competition = competition.Apply(new ScoringTeamDefined(firstTeam, At));
        competition = competition.Apply(new ScoringTeamDefined(secondTeam, At));

        var competitorA = CompetitorId.New();
        var competitorB = CompetitorId.New();

        competition = competition.Apply(new ScoringTeamMembershipAssigned(
            new ScoringTeamMembership { CompetitorRef = competitorA, TeamRef = firstTeam.Id, Contributes = true }, At));
        competition = competition.Apply(new ScoringTeamMembershipAssigned(
            new ScoringTeamMembership { CompetitorRef = competitorB, TeamRef = firstTeam.Id, Contributes = true }, At));

        // The eligibility correction: the same competitor re-assigned — the
        // record is replaced, not appended.
        competition = competition.Apply(new ScoringTeamMembershipAssigned(
            new ScoringTeamMembership { CompetitorRef = competitorA, TeamRef = secondTeam.Id, Contributes = false }, At));

        competition.ScoringTeamMemberships.Should().HaveCount(2);
        var replaced = competition.ScoringTeamMemberships.Single(m => m.CompetitorRef == competitorA);
        replaced.TeamRef.Should().Be(secondTeam.Id);
        replaced.Contributes.Should().BeFalse();
        competition.ScoringTeamMemberships.Single(m => m.CompetitorRef == competitorB).TeamRef.Should().Be(firstTeam.Id);
    }

    [Fact]
    public void ScoringTeamMembershipCleared_removes_all_records_for_the_competitor()
    {
        var competition = Competition.Create(SampleCreatedEvent());

        var competitorA = CompetitorId.New();
        var competitorB = CompetitorId.New();

        competition = competition.Apply(new ScoringTeamMembershipAssigned(
            new ScoringTeamMembership { CompetitorRef = competitorA, TeamRef = ScoringTeamId.New(), Contributes = true }, At));
        competition = competition.Apply(new ScoringTeamMembershipAssigned(
            new ScoringTeamMembership { CompetitorRef = competitorA, TeamRef = ScoringTeamId.New(), Contributes = false }, At));
        competition = competition.Apply(new ScoringTeamMembershipAssigned(
            new ScoringTeamMembership { CompetitorRef = competitorB, TeamRef = ScoringTeamId.New(), Contributes = true }, At));

        competition = competition.Apply(new ScoringTeamMembershipCleared(competitorA, At));

        competition.ScoringTeamMemberships.Select(m => m.CompetitorRef).Should().Equal([competitorB]);
    }

    [Fact]
    public void ProtectionGroupMemberAdded_appends_and_ProtectionGroupMemberRemoved_filters_the_matching_pair_only()
    {
        var competition = Competition.Create(SampleCreatedEvent());

        var competitorA = CompetitorId.New();
        var firstGroup = ProtectionGroupId.New();
        var secondGroup = ProtectionGroupId.New();

        competition = competition.Apply(new ProtectionGroupMemberAdded(
            new ProtectionGroupMembership { CompetitorRef = competitorA, GroupRef = firstGroup }, At));
        competition = competition.Apply(new ProtectionGroupMemberAdded(
            new ProtectionGroupMembership { CompetitorRef = competitorA, GroupRef = secondGroup }, At));

        competition.ProtectionGroupMemberships.Should().HaveCount(2);

        competition = competition.Apply(new ProtectionGroupMemberRemoved(competitorA, firstGroup, At));

        competition.ProtectionGroupMemberships.Should().ContainSingle();
        competition.ProtectionGroupMemberships[0].GroupRef.Should().Be(secondGroup);
    }

    [Fact]
    public void TeamClassificationConfigured_replaces_last_wins()
    {
        var competition = Competition.Create(SampleCreatedEvent());

        competition = competition.Apply(new TeamClassificationConfigured(
            new TeamClassificationConfiguration { Enabled = true, Method = "bestThreeScoreSum" }, At));
        competition = competition.Apply(new TeamClassificationConfigured(
            new TeamClassificationConfiguration { Enabled = false, Method = "bestThreeScoreSum" }, At.AddMinutes(1)));

        competition.TeamClassification.Should().NotBeNull();
        competition.TeamClassification!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void A_team_event_stream_folds_in_order_to_the_expected_final_state()
    {
        var created = SampleCreatedEvent();
        var team = new ScoringTeam { Id = ScoringTeamId.New(), Name = "Eagles" };
        var group = new ProtectionGroup { Id = ProtectionGroupId.New(), Name = "Helpers" };
        var competitorA = CompetitorId.New();
        var competitorB = CompetitorId.New();

        CompetitionEvent[] stream =
        [
            created,
            new ScoringTeamDefined(team, At),
            new ProtectionGroupDefined(group, At),
            new ScoringTeamMembershipAssigned(
                new ScoringTeamMembership { CompetitorRef = competitorA, TeamRef = team.Id, Contributes = true }, At),
            new ScoringTeamMembershipAssigned(
                new ScoringTeamMembership { CompetitorRef = competitorB, TeamRef = team.Id, Contributes = false }, At),
            new ProtectionGroupMemberAdded(
                new ProtectionGroupMembership { CompetitorRef = competitorA, GroupRef = group.Id }, At),
            new TeamClassificationConfigured(
                new TeamClassificationConfiguration { Enabled = true, Method = "bestThreeScoreSum" }, At),
            new ScoringTeamMembershipCleared(competitorB, At),
            new ProtectionGroupMemberRemoved(competitorA, group.Id, At),
        ];

        var final = stream.Aggregate((Competition?)null, Competition.Apply);

        final.Should().NotBeNull();
        final.ScoringTeams.Should().ContainSingle().Which.Id.Should().Be(team.Id);
        final.ProtectionGroups.Should().ContainSingle().Which.Id.Should().Be(group.Id);
        final.ScoringTeamMemberships.Should().ContainSingle().Which.CompetitorRef.Should().Be(competitorA);
        final.ProtectionGroupMemberships.Should().BeEmpty();
        final.TeamClassification!.Enabled.Should().BeTrue();
    }

    [Fact]
    public void ProtectedPair_canonicalises_so_both_naming_orders_are_one_equal_pair()
    {
        // The draw engine's entire view of protection — dedup by plain record
        // equality must be correct regardless of the order the pair was named.
        var a = CompetitorId.New();
        var b = CompetitorId.New();

        var first = new ProtectedPair(a, b);
        var second = new ProtectedPair(b, a);

        first.A.Should().Be(second.A);
        first.B.Should().Be(second.B);
        first.Should().Be(second);
        new HashSet<ProtectedPair> { first, second }.Should().ContainSingle();
    }

    [Fact]
    public void Non_creation_team_events_against_no_current_projection_throw()
    {
        FluentActions.Invoking(() =>
            Competition.Apply(null, new ScoringTeamDefined(
                new ScoringTeam { Id = ScoringTeamId.New(), Name = "Eagles" }, At)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() =>
            Competition.Apply(null, new TeamClassificationConfigured(
                new TeamClassificationConfiguration { Enabled = true, Method = "bestThreeScoreSum" }, At)))
            .Should().Throw<ArgumentException>();
    }
}
