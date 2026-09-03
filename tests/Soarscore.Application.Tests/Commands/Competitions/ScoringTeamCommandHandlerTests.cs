// kanban/in-progress/teams-mvp.md WI-6. Covers the scoring-side team command
// handlers (DefineScoringTeam, AssignScoringTeamMembership,
// ClearScoringTeamMembership, ConfigureTeamClassification) directly against a
// FakeEventStore — same style as WithdrawCompetitorHandlerTests.cs. The decide
// functions' own semantics have their WI-3 unit tests in
// tests/Soarscore.Domain.Tests; here the assertion is propagation: each
// handler loads, calls its decide, appends exactly the decide's event at
// ExpectedVersion.Exact, and surfaces every defect code intact. The
// eligibility-correction and clear-then-reassign sequences run through the
// handlers end to end, the same replay the rosters view (WI-6) is asserted
// against.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class ScoringTeamCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = SampleDefinition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SampleDefinition.Version,
            AdoptedAt = Now,
        };

    private static (FakeEventStore Store, CompetitionId CompetitionId) SeedCompetition()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var created = new CompetitionCreated(
            id, "Teams Comp 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return (store, id);
    }

    private static CompetitorId SeedRegisteredCompetitor(FakeEventStore store, CompetitionId competitionId, int competitorNumber)
    {
        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = competitorNumber,
            RegisteredAt = Now,
        };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(1 + competitorNumber - 1), [new CompetitorRegistered(competitor, Now)])
            .GetAwaiter().GetResult();
        return competitor.Id;
    }

    private static void SeedWithdrawnCompetitor(FakeEventStore store, CompetitionId competitionId, CompetitorId competitorId)
    {
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(store.Streams[competitionId.Value].Count),
            [new CompetitorWithdrawn(competitorId, Now)]).GetAwaiter().GetResult();
    }

    private static async Task<ScoringTeamId> SeedScoringTeam(FakeEventStore store, CompetitionId competitionId, string name)
    {
        var handler = new DefineScoringTeamHandler(store, new FakeClock(Now));
        var result = await handler.HandleAsync(
            new DefineScoringTeam(competitionId, name), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    // ------------------------------------------------------------- DefineScoringTeam

    [Fact]
    public async Task Defining_a_scoring_team_appends_one_event_and_returns_the_minted_id()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new DefineScoringTeamHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DefineScoringTeam(competitionId, "Hawks"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().NotBeEmpty();

        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(2);
        var defined = stream[1].Should().BeOfType<ScoringTeamDefined>().Subject;
        defined.Team.Id.Should().Be(result.Value);
        defined.Team.Name.Should().Be("Hawks");
    }

    [Fact]
    public async Task Defining_a_scoring_team_with_a_blank_name_fails_with_defineScoringTeam_nameBlank()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new DefineScoringTeamHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DefineScoringTeam(competitionId, "   "), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("defineScoringTeam.nameBlank");
    }

    [Fact]
    public async Task Defining_a_scoring_team_with_a_duplicate_name_fails_with_defineScoringTeam_nameTaken()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new DefineScoringTeamHandler(store, new FakeClock(Now));
        await SeedScoringTeam(store, competitionId, "Hawks");

        var result = await handler.HandleAsync(
            new DefineScoringTeam(competitionId, "hawks"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("defineScoringTeam.nameTaken");
    }

    [Fact]
    public async Task Defining_a_scoring_team_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var handler = new DefineScoringTeamHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DefineScoringTeam(CompetitionId.New(), "Hawks"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    // ------------------------------------------------- AssignScoringTeamMembership

    [Fact]
    public async Task Assigning_a_member_appends_one_event_and_reassignment_on_the_same_team_corrects_eligibility()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId, 1);
        var teamId = await SeedScoringTeam(store, competitionId, "Hawks");
        var handler = new AssignScoringTeamMembershipHandler(store, new FakeClock(Now));

        var assigned = await handler.HandleAsync(
            new AssignScoringTeamMembership(competitionId, competitorId, teamId, Contributes: true),
            TestContext.Current.CancellationToken);
        assigned.IsSuccess.Should().BeTrue();

        // Same-team re-assignment is the eligibility-correction path.
        var corrected = await handler.HandleAsync(
            new AssignScoringTeamMembership(competitionId, competitorId, teamId, Contributes: false),
            TestContext.Current.CancellationToken);

        corrected.IsSuccess.Should().BeTrue();

        // created, registered, team defined, assigned, corrected.
        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(5);
        stream[4].Should().BeOfType<ScoringTeamMembershipAssigned>()
            .Which.Membership.Contributes.Should().BeFalse();
    }

    [Fact]
    public async Task Assigning_to_an_unknown_team_fails_with_assignTeamMembership_teamNotFound()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId, 1);
        var handler = new AssignScoringTeamMembershipHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AssignScoringTeamMembership(competitionId, competitorId, ScoringTeamId.New(), Contributes: true),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignTeamMembership.teamNotFound");
    }

    [Fact]
    public async Task Assigning_an_unknown_competitor_fails_with_assignTeamMembership_competitorNotFound()
    {
        var (store, competitionId) = SeedCompetition();
        var teamId = await SeedScoringTeam(store, competitionId, "Hawks");
        var handler = new AssignScoringTeamMembershipHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AssignScoringTeamMembership(competitionId, CompetitorId.New(), teamId, Contributes: true),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignTeamMembership.competitorNotFound");
    }

    [Fact]
    public async Task Assigning_a_withdrawn_competitor_fails_with_assignTeamMembership_competitorWithdrawn()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId, 1);
        var teamId = await SeedScoringTeam(store, competitionId, "Hawks");
        SeedWithdrawnCompetitor(store, competitionId, competitorId);
        var handler = new AssignScoringTeamMembershipHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AssignScoringTeamMembership(competitionId, competitorId, teamId, Contributes: true),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignTeamMembership.competitorWithdrawn");
    }

    [Fact]
    public async Task Assigning_a_member_of_a_different_team_fails_with_assignTeamMembership_competitorAlreadyAssigned()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId, 1);
        var hawksId = await SeedScoringTeam(store, competitionId, "Hawks");
        var falconsId = await SeedScoringTeam(store, competitionId, "Falcons");
        var handler = new AssignScoringTeamMembershipHandler(store, new FakeClock(Now));

        var first = await handler.HandleAsync(
            new AssignScoringTeamMembership(competitionId, competitorId, hawksId, Contributes: true),
            TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.HandleAsync(
            new AssignScoringTeamMembership(competitionId, competitorId, falconsId, Contributes: true),
            TestContext.Current.CancellationToken);

        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("assignTeamMembership.competitorAlreadyAssigned");
    }

    // --------------------------------------------- ClearScoringTeamMembership

    [Fact]
    public async Task Clearing_then_reassigning_replays_to_the_new_team()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId, 1);
        var hawksId = await SeedScoringTeam(store, competitionId, "Hawks");
        var falconsId = await SeedScoringTeam(store, competitionId, "Falcons");
        var assignHandler = new AssignScoringTeamMembershipHandler(store, new FakeClock(Now));
        var clearHandler = new ClearScoringTeamMembershipHandler(store, new FakeClock(Now));

        var assigned = await assignHandler.HandleAsync(
            new AssignScoringTeamMembership(competitionId, competitorId, hawksId, Contributes: true),
            TestContext.Current.CancellationToken);
        assigned.IsSuccess.Should().BeTrue();

        var cleared = await clearHandler.HandleAsync(
            new ClearScoringTeamMembership(competitionId, competitorId), TestContext.Current.CancellationToken);
        cleared.IsSuccess.Should().BeTrue();

        var reassigned = await assignHandler.HandleAsync(
            new AssignScoringTeamMembership(competitionId, competitorId, falconsId, Contributes: true),
            TestContext.Current.CancellationToken);

        reassigned.IsSuccess.Should().BeTrue();
        // created, registered, two teams defined, assigned, cleared, reassigned.
        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(7);
        stream[5].Should().BeOfType<ScoringTeamMembershipCleared>()
            .Which.CompetitorRef.Should().Be(competitorId);
        stream[6].Should().BeOfType<ScoringTeamMembershipAssigned>()
            .Which.Membership.TeamRef.Should().Be(falconsId);
    }

    [Fact]
    public async Task Clearing_a_membership_that_does_not_exist_fails_with_clearTeamMembership_membershipNotFound()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId, 1);
        var handler = new ClearScoringTeamMembershipHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new ClearScoringTeamMembership(competitionId, competitorId), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("clearTeamMembership.membershipNotFound");
    }

    // --------------------------------------------- ConfigureTeamClassification

    [Fact]
    public async Task Configuring_then_reconfiguring_appends_both_events_last_wins()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new ConfigureTeamClassificationHandler(store, new FakeClock(Now));

        var first = await handler.HandleAsync(
            new ConfigureTeamClassification(competitionId, Enabled: true, By: "CD"), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.HandleAsync(
            new ConfigureTeamClassification(competitionId, Enabled: false, By: "CD"), TestContext.Current.CancellationToken);

        second.IsSuccess.Should().BeTrue();

        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(3);
        stream[1].Should().BeOfType<TeamClassificationConfigured>()
            .Which.Configuration.Enabled.Should().BeTrue();
        stream[2].Should().BeOfType<TeamClassificationConfigured>()
            .Which.Configuration.Should().Match<TeamClassificationConfiguration>(c =>
                !c.Enabled && c.Method == TeamClassificationEngine.MethodBestThreeScoreSum);
    }

    [Fact]
    public async Task Configuring_with_a_blank_by_fails_with_configureTeamClassification_byBlank()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new ConfigureTeamClassificationHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new ConfigureTeamClassification(competitionId, Enabled: true, By: " "), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("configureTeamClassification.byBlank");
    }

    [Fact]
    public async Task Configuring_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var handler = new ConfigureTeamClassificationHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new ConfigureTeamClassification(CompetitionId.New(), Enabled: true, By: "CD"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }
}
