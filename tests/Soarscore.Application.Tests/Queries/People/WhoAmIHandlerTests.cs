// authentication-and-authorisation.md WI-7 (D9). Covers WhoAmIHandler against
// the Auth + People fakes — the three shapes the front-end flow walks:
// unauthenticated, authenticated-but-unlinked (nobody yet, but the token's
// name claim still says who is asking), and linked (the person's id, roles
// and name — roles never live in tokens, D2).

using AwesomeAssertions;
using Soarscore.Application.Auth;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;
using Xunit;

using Soarscore.Application.Tests.Auth;
using Soarscore.Application.Tests.Shared.People;

namespace Soarscore.Application.Tests.Queries.People;

public class WhoAmIHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_unauthenticated_caller_gets_the_unauthenticated_shape()
    {
        var handler = new WhoAmIHandler(new AnonymousCurrentUser(), new FakePeopleQuery());

        var result = await handler.HandleAsync(new WhoAmI(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new CurrentUserView(false, null, [], null));
    }

    [Fact]
    public async Task An_authenticated_unlinked_caller_gets_no_PersonId_no_roles_and_the_token_name()
    {
        var user = new FakeCurrentUser(
            IsAuthenticated: true, Provider: "auth0", Subject: "sub-7", Email: "tama@example.org", Name: "Tama");
        var handler = new WhoAmIHandler(user, new FakePeopleQuery());

        var result = await handler.HandleAsync(new WhoAmI(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new CurrentUserView(true, null, [], "Tama"));
    }

    [Fact]
    public async Task A_linked_caller_gets_their_PersonId_roles_and_name()
    {
        var personId = PersonId.New();
        var people = new FakePeopleQuery();
        people.SeedIdentity("auth0", "sub-7", personId, PersonRole.Competitor, PersonRole.Organiser);
        people.Seed(new PersonSummary(personId, "Tama Pilot", "tama@example.org", null, null, null, [PersonRole.Competitor, PersonRole.Organiser]));
        var user = new FakeCurrentUser(
            IsAuthenticated: true, Provider: "auth0", Subject: "sub-7", Email: "tama@example.org", Name: "Tama");
        var handler = new WhoAmIHandler(user, people);

        var result = await handler.HandleAsync(new WhoAmI(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(
            new CurrentUserView(true, personId, [PersonRole.Competitor, PersonRole.Organiser], "Tama Pilot"));
    }

    [Fact]
    public async Task A_linked_caller_with_no_summary_yet_falls_back_to_the_token_name()
    {
        var personId = PersonId.New();
        var people = new FakePeopleQuery();
        people.SeedIdentity("auth0", "sub-7", personId, PersonRole.Competitor);
        var user = new FakeCurrentUser(
            IsAuthenticated: true, Provider: "auth0", Subject: "sub-7", Email: "tama@example.org", Name: "Tama");
        var handler = new WhoAmIHandler(user, people);

        var result = await handler.HandleAsync(new WhoAmI(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.PersonId.Should().Be(personId);
        result.Value.Name.Should().Be("Tama");
    }
}