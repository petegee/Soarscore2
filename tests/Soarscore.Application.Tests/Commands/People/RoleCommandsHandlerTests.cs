// authentication-and-authorisation.md WI-7. Covers GrantRoleHandler and
// RevokeRoleHandler directly against the People fakes: the WI-3 decides'
// refusals (person.roleAlreadyHeld / person.roleNotHeld) surface as handler
// failures, and the last-organiser guard fires exactly when an Organiser
// revoke the decide accepted would strand the system's only Organiser —
// a cross-stream read (CountByRoleAsync), race-tolerant by design.

using AwesomeAssertions;
using Soarscore.Application.Commands.People;
using Soarscore.Domain;
using Soarscore.Domain.People;
using Xunit;

using Soarscore.Application.Tests.Shared.People;

namespace Soarscore.Application.Tests.Commands.People;

public class RoleCommandsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    private static (FakeEventStore Store, PersonId PersonId, FakePeopleQuery People) SeedPerson(
        params PersonEvent[] afterRegistration)
    {
        var store = new FakeEventStore();
        var id = PersonId.New();
        var events = new List<IDomainEvent>
        {
            new PersonRegistered(id, "Pete Moss", new ContactDetails { Email = "pete@example.org" }, null, Now),
        };
        events.AddRange(afterRegistration);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, events).GetAwaiter().GetResult();
        return (store, id, new FakePeopleQuery());
    }

    private static IReadOnlyList<IDomainEvent> Stream(FakeEventStore store, Guid streamId) =>
        store.ReadStreamAsync(streamId, 0).GetAwaiter().GetResult().Value;

    // ---- GrantRole ----------------------------------------------------------

    [Fact]
    public async Task GrantRole_appends_RoleGranted_at_the_next_version()
    {
        var (store, personId, _) = SeedPerson();
        var handler = new GrantRoleHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new GrantRole(personId, PersonRole.Competitor), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(personId);

        var stream = Stream(store, personId.Value);
        stream.Should().HaveCount(2);
        stream[1].Should().BeOfType<RoleGranted>().Which.Role.Should().Be(PersonRole.Competitor);
    }

    [Fact]
    public async Task GrantRole_of_an_already_held_role_fails_roleAlreadyHeld_and_appends_nothing()
    {
        var (store, personId, _) = SeedPerson(new RoleGranted(PersonRole.Competitor, Now));
        var handler = new GrantRoleHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(new GrantRole(personId, PersonRole.Competitor), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.roleAlreadyHeld");
        Stream(store, personId.Value).Should().HaveCount(2);
    }

    [Fact]
    public async Task GrantRole_for_an_unknown_person_fails_notFound()
    {
        var handler = new GrantRoleHandler(new FakeEventStore(), new FakeClock(Now));

        var result = await handler.HandleAsync(new GrantRole(PersonId.New(), PersonRole.Competitor), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.notFound");
    }

    // ---- RevokeRole ---------------------------------------------------------

    [Fact]
    public async Task RevokeRole_removes_a_held_role()
    {
        var (store, personId, people) = SeedPerson(new RoleGranted(PersonRole.Competitor, Now));
        var handler = new RevokeRoleHandler(store, people, new FakeClock(Now));

        var result = await handler.HandleAsync(new RevokeRole(personId, PersonRole.Competitor), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(personId);

        var stream = Stream(store, personId.Value);
        stream.Should().HaveCount(3);
        stream[2].Should().BeOfType<RoleRevoked>().Which.Role.Should().Be(PersonRole.Competitor);
    }

    [Fact]
    public async Task RevokeRole_of_an_unheld_role_fails_roleNotHeld_and_never_consults_the_organiser_count()
    {
        var (store, personId, people) = SeedPerson();
        var handler = new RevokeRoleHandler(store, people, new FakeClock(Now));

        var result = await handler.HandleAsync(new RevokeRole(personId, PersonRole.Competitor), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.roleNotHeld");
        Stream(store, personId.Value).Should().HaveCount(1);
    }

    // ---- The last-organiser guard -------------------------------------------

    [Fact]
    public async Task Revoking_the_only_organiser_fails_lastOrganiser_and_appends_nothing()
    {
        var (store, personId, people) = SeedPerson(new RoleGranted(PersonRole.Organiser, Now));
        people.SeedRoleCount(PersonRole.Organiser, 1);
        var handler = new RevokeRoleHandler(store, people, new FakeClock(Now));

        var result = await handler.HandleAsync(new RevokeRole(personId, PersonRole.Organiser), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.lastOrganiser");
        Stream(store, personId.Value).Should().HaveCount(2);
    }

    [Fact]
    public async Task Revoking_an_organiser_when_others_remain_succeeds()
    {
        var (store, personId, people) = SeedPerson(new RoleGranted(PersonRole.Organiser, Now));
        people.SeedRoleCount(PersonRole.Organiser, 2);
        var handler = new RevokeRoleHandler(store, people, new FakeClock(Now));

        var result = await handler.HandleAsync(new RevokeRole(personId, PersonRole.Organiser), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        Stream(store, personId.Value).Should().HaveCount(3);
    }

    [Fact]
    public async Task Revoking_Competitor_never_pays_for_the_organiser_count()
    {
        var (store, personId, people) = SeedPerson(new RoleGranted(PersonRole.Competitor, Now));
        people.SeedRoleCount(PersonRole.Organiser, 1);   // would refuse, if the guard consulted it for non-Organiser roles
        var handler = new RevokeRoleHandler(store, people, new FakeClock(Now));

        var result = await handler.HandleAsync(new RevokeRole(personId, PersonRole.Competitor), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }
}