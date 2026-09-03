// kanban/in-progress/teams-mvp.md WI-6. Covers GetTeamRostersHandler directly
// against a FakeEventStore, driven through the real team command handlers so
// the views are built from exactly the events the API writes. The two
// assertions the story names: the scoring/protection separation is visibly
// structural in the view (a competitor's scoring membership and their
// protection memberships are independent sections), and assignment / clear /
// correction sequences replay to accurate views.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;

namespace Soarscore.Application.Tests.Queries.Competitions;

public class GetTeamRostersHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    private sealed record Wired(
        FakeEventStore Store,
        CompetitionId CompetitionId,
        GetTeamRostersHandler Rosters,
        DefineScoringTeamHandler DefineScoringTeam,
        DefineProtectionGroupHandler DefineProtectionGroup,
        AssignScoringTeamMembershipHandler Assign,
        ClearScoringTeamMembershipHandler Clear,
        AddProtectionGroupMemberHandler AddProtection,
        RemoveProtectionGroupMemberHandler RemoveProtection);

    private static Wired SeedWired()
    {
        var store = new FakeEventStore();
        var competitionId = CompetitionId.New();
        var definition = Corpus.All[0].Definition;
        var created = new CompetitionCreated(
            competitionId, "Teams Comp 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", new AdoptedRules
            {
                Definition = definition,
                SourceClassId = "content-hash-abc123",
                SourceVersion = definition.Version,
                AdoptedAt = Now,
            }, Now);
        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();

        return new Wired(
            store,
            competitionId,
            new GetTeamRostersHandler(store),
            new DefineScoringTeamHandler(store, new FakeClock(Now)),
            new DefineProtectionGroupHandler(store, new FakeClock(Now)),
            new AssignScoringTeamMembershipHandler(store, new FakeClock(Now)),
            new ClearScoringTeamMembershipHandler(store, new FakeClock(Now)),
            new AddProtectionGroupMemberHandler(store, new FakeClock(Now)),
            new RemoveProtectionGroupMemberHandler(store, new FakeClock(Now)));
    }

    private static CompetitorId SeedRegisteredCompetitor(FakeEventStore store, CompetitionId competitionId)
    {
        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = store.Streams[competitionId.Value].Count,
            RegisteredAt = Now,
        };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(store.Streams[competitionId.Value].Count),
            [new CompetitorRegistered(competitor, Now)]).GetAwaiter().GetResult();
        return competitor.Id;
    }

    private static async Task<ScoringTeamId> DefineTeamAsync(Wired wired, string name)
    {
        var result = await wired.DefineScoringTeam.HandleAsync(
            new DefineScoringTeam(wired.CompetitionId, name), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static async Task<ProtectionGroupId> DefineGroupAsync(Wired wired, string name)
    {
        var result = await wired.DefineProtectionGroup.HandleAsync(
            new DefineProtectionGroup(wired.CompetitionId, name), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static async Task<TeamRostersView> RostersAsync(Wired wired)
    {
        var result = await wired.Rosters.HandleAsync(
            new GetTeamRosters(wired.CompetitionId), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task A_competition_with_no_teams_returns_two_empty_sections()
    {
        var wired = SeedWired();

        var view = await RostersAsync(wired);

        view.ScoringTeams.Should().BeEmpty();
        view.ProtectionGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Scoring_and_protection_memberships_remain_distinct_in_the_view()
    {
        var wired = SeedWired();
        var scorer1 = SeedRegisteredCompetitor(wired.Store, wired.CompetitionId);
        var scorer2 = SeedRegisteredCompetitor(wired.Store, wired.CompetitionId);
        var protector = SeedRegisteredCompetitor(wired.Store, wired.CompetitionId);
        var hawksId = await DefineTeamAsync(wired, "Hawks");
        var helpersId = await DefineGroupAsync(wired, "Helpers");
        var juniorsId = await DefineGroupAsync(wired, "Juniors");

        // scorer1 sits in the scoring team AND two protection groups — the
        // F5J-junior-with-a-helper-pair shape (owner decision 3).
        (await wired.Assign.HandleAsync(
            new AssignScoringTeamMembership(wired.CompetitionId, scorer1, hawksId, Contributes: true),
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();
        (await wired.Assign.HandleAsync(
            new AssignScoringTeamMembership(wired.CompetitionId, scorer2, hawksId, Contributes: false),
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();
        (await wired.AddProtection.HandleAsync(
            new AddProtectionGroupMember(wired.CompetitionId, scorer1, helpersId), TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();
        (await wired.AddProtection.HandleAsync(
            new AddProtectionGroupMember(wired.CompetitionId, scorer1, juniorsId), TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();
        (await wired.AddProtection.HandleAsync(
            new AddProtectionGroupMember(wired.CompetitionId, protector, helpersId), TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();

        var view = await RostersAsync(wired);

        var hawks = view.ScoringTeams.Should().ContainSingle().Subject;
        hawks.TeamRef.Should().Be(hawksId);
        hawks.Name.Should().Be("Hawks");
        hawks.Members.Should().HaveCount(2);
        hawks.Members.Single(m => m.CompetitorRef == scorer1).Contributes.Should().BeTrue();
        hawks.Members.Single(m => m.CompetitorRef == scorer2).Contributes.Should().BeFalse();

        view.ProtectionGroups.Select(g => g.GroupRef).Should().Equal(new ProtectionGroupId[] { helpersId, juniorsId });

        var helpers = view.ProtectionGroups.Single(g => g.GroupRef == helpersId);
        helpers.Name.Should().Be("Helpers");
        helpers.Members.Should().HaveCount(2);
        helpers.Members.Should().Contain(scorer1);
        helpers.Members.Should().Contain(protector);

        view.ProtectionGroups.Single(g => g.GroupRef == juniorsId).Members.Should().ContainSingle()
            .Which.Should().Be(scorer1);
    }

    [Fact]
    public async Task Assignment_correction_clear_and_reassignment_sequences_replay_to_accurate_views()
    {
        var wired = SeedWired();
        var competitorId = SeedRegisteredCompetitor(wired.Store, wired.CompetitionId);
        var hawksId = await DefineTeamAsync(wired, "Hawks");
        var falconsId = await DefineTeamAsync(wired, "Falcons");
        var helpersId = await DefineGroupAsync(wired, "Helpers");

        (await wired.Assign.HandleAsync(
            new AssignScoringTeamMembership(wired.CompetitionId, competitorId, hawksId, Contributes: true),
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();

        // Correction on the same team flips the eligibility flag.
        (await wired.Assign.HandleAsync(
            new AssignScoringTeamMembership(wired.CompetitionId, competitorId, hawksId, Contributes: false),
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();
        (await RostersAsync(wired)).ScoringTeams.Single(t => t.TeamRef == hawksId).Members.Should().ContainSingle()
            .Which.Contributes.Should().BeFalse();

        // Clear empties the roster, then a different team's assignment lands there.
        (await wired.Clear.HandleAsync(
            new ClearScoringTeamMembership(wired.CompetitionId, competitorId), TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();
        (await RostersAsync(wired)).ScoringTeams.Single(t => t.TeamRef == hawksId).Members.Should().BeEmpty();

        (await wired.Assign.HandleAsync(
            new AssignScoringTeamMembership(wired.CompetitionId, competitorId, falconsId, Contributes: true),
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();

        // Protection: add then remove replays to the remaining membership.
        (await wired.AddProtection.HandleAsync(
            new AddProtectionGroupMember(wired.CompetitionId, competitorId, helpersId), TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();
        (await wired.RemoveProtection.HandleAsync(
            new RemoveProtectionGroupMember(wired.CompetitionId, competitorId, helpersId), TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();

        var view = await RostersAsync(wired);

        var falcons = view.ScoringTeams.Single(t => t.TeamRef == falconsId);
        falcons.Members.Should().ContainSingle();
        falcons.Members[0].CompetitorRef.Should().Be(competitorId);
        falcons.Members[0].Contributes.Should().BeTrue();
        view.ScoringTeams.Single(t => t.TeamRef == hawksId).Members.Should().BeEmpty();
        view.ProtectionGroups.Single().Members.Should().BeEmpty();
    }

    [Fact]
    public async Task Rosters_against_an_unknown_competition_fail_with_competition_notFound()
    {
        var handler = new GetTeamRostersHandler(new FakeEventStore());

        var result = await handler.HandleAsync(
            new GetTeamRosters(CompetitionId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }
}
