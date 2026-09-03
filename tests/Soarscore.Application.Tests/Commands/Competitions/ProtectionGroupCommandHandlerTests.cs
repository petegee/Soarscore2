// kanban/in-progress/teams-mvp.md WI-6. Covers the protection-side team command
// handlers (DefineProtectionGroup, AddProtectionGroupMember,
// RemoveProtectionGroupMember) directly against a FakeEventStore — the
// ScoringTeamCommandHandlerTests.cs style. Beyond defect propagation, the
// owner-decision-3 shape is asserted through the handlers: multi-group
// membership succeeds, a duplicate of THIS group does not, names may coincide
// across kinds (uniqueness is enforced within a kind only), and the phase gate
// (addProtectionMember.drawExists / removeProtectionMember.drawExists)
// surfaces through load→decide→append. The draw arming the gate uses the
// whole-field Minimal definition (ScoreTaskRoundHandlerTests's fixture) so no
// parameter binding is needed.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Tests.Shared.CompetitionClasses;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;
using FakeClock = Soarscore.Application.Tests.Shared.Competitions.FakeClock;
using FakeEventStore = Soarscore.Application.Tests.Shared.Competitions.FakeEventStore;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class ProtectionGroupCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    private static (FakeEventStore Store, CompetitionId CompetitionId) SeedCompetition()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var definition = ClassDefinitionFixtures.Minimal();
        var created = new CompetitionCreated(
            id, "Teams Comp 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", new AdoptedRules
            {
                Definition = definition,
                SourceClassId = "content-hash-synthetic",
                SourceVersion = definition.Version!,
                AdoptedAt = Now,
            }, Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return (store, id);
    }

    private static CompetitorId SeedRegisteredCompetitor(FakeEventStore store, CompetitionId competitionId)
    {
        var read = store.ReadStreamAsync(competitionId.Value, 0).GetAwaiter().GetResult();
        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = read.Value.Count,
            RegisteredAt = Now,
        };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(read.Value.Count), [new CompetitorRegistered(competitor, Now)])
            .GetAwaiter().GetResult();
        return competitor.Id;
    }

    private static void SeedWithdrawnCompetitor(FakeEventStore store, CompetitionId competitionId, CompetitorId competitorId)
    {
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(store.Streams[competitionId.Value].Count),
            [new CompetitorWithdrawn(competitorId, Now)]).GetAwaiter().GetResult();
    }

    private static async Task<ProtectionGroupId> SeedProtectionGroup(FakeEventStore store, CompetitionId competitionId, string name)
    {
        var handler = new DefineProtectionGroupHandler(store, new FakeClock(Now));
        var result = await handler.HandleAsync(
            new DefineProtectionGroup(competitionId, name), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    /// <summary>Draws the whole-field one-round phase through the decide — the
    /// live phase that arms the protection-membership gate.</summary>
    private static void SeedDraw(FakeEventStore store, CompetitionId competitionId)
    {
        SeedRegisteredCompetitor(store, competitionId);
        SeedRegisteredCompetitor(store, competitionId);

        var read = store.ReadStreamAsync(competitionId.Value, 0).GetAwaiter().GetResult();
        var competition = read.Value.Aggregate(
            (Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;

        var drawn = competition.DrawPhase(1, ImmutableArray<string>.Empty, Now);
        drawn.IsSuccess.Should().BeTrue();
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(read.Value.Count), [drawn.Value]).GetAwaiter().GetResult();
    }

    // ------------------------------------------------------ DefineProtectionGroup

    [Fact]
    public async Task Defining_a_protection_group_appends_one_event_and_returns_the_minted_id()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new DefineProtectionGroupHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DefineProtectionGroup(competitionId, "Helpers"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().NotBeEmpty();

        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(2);
        stream[1].Should().BeOfType<ProtectionGroupDefined>()
            .Which.Group.Name.Should().Be("Helpers");
    }

    [Fact]
    public async Task Defining_a_protection_group_with_a_blank_name_fails_with_defineProtectionGroup_nameBlank()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new DefineProtectionGroupHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DefineProtectionGroup(competitionId, ""), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("defineProtectionGroup.nameBlank");
    }

    [Fact]
    public async Task Defining_a_protection_group_with_a_duplicate_name_fails_with_defineProtectionGroup_nameTaken()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new DefineProtectionGroupHandler(store, new FakeClock(Now));
        await SeedProtectionGroup(store, competitionId, "Helpers");

        var result = await handler.HandleAsync(
            new DefineProtectionGroup(competitionId, "HELPERS"), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("defineProtectionGroup.nameTaken");
    }

    [Fact]
    public async Task A_protection_group_may_share_its_name_with_a_scoring_team()
    {
        var (store, competitionId) = SeedCompetition();
        var scoringHandler = new DefineScoringTeamHandler(store, new FakeClock(Now));
        var scoring = await scoringHandler.HandleAsync(
            new DefineScoringTeam(competitionId, "Eagles"), TestContext.Current.CancellationToken);
        scoring.IsSuccess.Should().BeTrue();

        var protectionHandler = new DefineProtectionGroupHandler(store, new FakeClock(Now));
        var protection = await protectionHandler.HandleAsync(
            new DefineProtectionGroup(competitionId, "Eagles"), TestContext.Current.CancellationToken);

        // Uniqueness is enforced within a kind only — the two vocabularies are
        // unrelated (teams-mvp.md §Decide functions).
        protection.IsSuccess.Should().BeTrue();
    }

    // --------------------------------------------------- AddProtectionGroupMember

    [Fact]
    public async Task Adding_a_member_to_two_groups_succeeds_but_a_duplicate_of_one_group_does_not()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId);
        var helpersId = await SeedProtectionGroup(store, competitionId, "Helpers");
        var juniorsId = await SeedProtectionGroup(store, competitionId, "Juniors");
        var handler = new AddProtectionGroupMemberHandler(store, new FakeClock(Now));

        var first = await handler.HandleAsync(
            new AddProtectionGroupMember(competitionId, competitorId, helpersId), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        // Many-to-many protection groups (owner decision 3) — a second group is
        // allowed and expected.
        var second = await handler.HandleAsync(
            new AddProtectionGroupMember(competitionId, competitorId, juniorsId), TestContext.Current.CancellationToken);
        second.IsSuccess.Should().BeTrue();

        var duplicate = await handler.HandleAsync(
            new AddProtectionGroupMember(competitionId, competitorId, helpersId), TestContext.Current.CancellationToken);

        duplicate.IsFailure.Should().BeTrue();
        duplicate.Code.Should().Be("addProtectionMember.duplicateMembership");

        // created, registered, two groups defined, added (helpers), added (juniors).
        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(6);
        stream[4].Should().BeOfType<ProtectionGroupMemberAdded>()
            .Which.Membership.GroupRef.Should().Be(helpersId);
        stream[5].Should().BeOfType<ProtectionGroupMemberAdded>()
            .Which.Membership.GroupRef.Should().Be(juniorsId);
    }

    [Fact]
    public async Task Adding_a_member_to_an_unknown_group_fails_with_addProtectionMember_groupNotFound()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId);
        var handler = new AddProtectionGroupMemberHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AddProtectionGroupMember(competitionId, competitorId, ProtectionGroupId.New()),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("addProtectionMember.groupNotFound");
    }

    [Fact]
    public async Task Adding_an_unknown_competitor_fails_with_addProtectionMember_competitorNotFound()
    {
        var (store, competitionId) = SeedCompetition();
        var helpersId = await SeedProtectionGroup(store, competitionId, "Helpers");
        var handler = new AddProtectionGroupMemberHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AddProtectionGroupMember(competitionId, CompetitorId.New(), helpersId),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("addProtectionMember.competitorNotFound");
    }

    [Fact]
    public async Task Adding_a_withdrawn_competitor_fails_with_addProtectionMember_competitorWithdrawn()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId);
        var helpersId = await SeedProtectionGroup(store, competitionId, "Helpers");
        SeedWithdrawnCompetitor(store, competitionId, competitorId);
        var handler = new AddProtectionGroupMemberHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AddProtectionGroupMember(competitionId, competitorId, helpersId), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("addProtectionMember.competitorWithdrawn");
    }

    [Fact]
    public async Task Adding_a_member_once_a_phase_is_drawn_fails_with_addProtectionMember_drawExists()
    {
        var (store, competitionId) = SeedCompetition();
        var lateCompetitorId = SeedRegisteredCompetitor(store, competitionId);
        var helpersId = await SeedProtectionGroup(store, competitionId, "Helpers");
        SeedDraw(store, competitionId);
        var handler = new AddProtectionGroupMemberHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AddProtectionGroupMember(competitionId, lateCompetitorId, helpersId), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("addProtectionMember.drawExists");
    }

    // ------------------------------------------------ RemoveProtectionGroupMember

    [Fact]
    public async Task Removing_a_member_appends_one_event()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId);
        var helpersId = await SeedProtectionGroup(store, competitionId, "Helpers");
        var addHandler = new AddProtectionGroupMemberHandler(store, new FakeClock(Now));
        var added = await addHandler.HandleAsync(
            new AddProtectionGroupMember(competitionId, competitorId, helpersId), TestContext.Current.CancellationToken);
        added.IsSuccess.Should().BeTrue();

        var handler = new RemoveProtectionGroupMemberHandler(store, new FakeClock(Now));
        var result = await handler.HandleAsync(
            new RemoveProtectionGroupMember(competitionId, competitorId, helpersId), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        // created, registered, group defined, added, removed.
        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(5);
        stream[4].Should().BeOfType<ProtectionGroupMemberRemoved>().Subject.CompetitorRef.Should().Be(competitorId);
    }

    [Fact]
    public async Task Removing_a_membership_that_does_not_exist_fails_with_removeProtectionMember_membershipNotFound()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId);
        var helpersId = await SeedProtectionGroup(store, competitionId, "Helpers");
        var handler = new RemoveProtectionGroupMemberHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new RemoveProtectionGroupMember(competitionId, competitorId, helpersId), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("removeProtectionMember.membershipNotFound");
    }

    [Fact]
    public async Task Removing_a_member_once_a_phase_is_drawn_fails_with_removeProtectionMember_drawExists()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId);
        var helpersId = await SeedProtectionGroup(store, competitionId, "Helpers");
        var addHandler = new AddProtectionGroupMemberHandler(store, new FakeClock(Now));
        var added = await addHandler.HandleAsync(
            new AddProtectionGroupMember(competitionId, competitorId, helpersId), TestContext.Current.CancellationToken);
        added.IsSuccess.Should().BeTrue();

        SeedDraw(store, competitionId);

        var handler = new RemoveProtectionGroupMemberHandler(store, new FakeClock(Now));
        var result = await handler.HandleAsync(
            new RemoveProtectionGroupMember(competitionId, competitorId, helpersId), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("removeProtectionMember.drawExists");
    }
}
