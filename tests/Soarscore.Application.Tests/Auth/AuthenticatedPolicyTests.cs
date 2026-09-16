// authentication-and-authorisation.md WI-5 — AuthenticatedPolicy's truth
// table. One branch, four principal shapes: authentication is the only
// question this policy answers (D4 makes it the default for every query);
// roles and capture policy are other policies' business. The command in each
// case is a real A-mapped query.

using AwesomeAssertions;
using Soarscore.Application.Auth;
using Soarscore.Application.Auth.Policies;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Application.Tests.Auth;

public class AuthenticatedPolicyTests
{
    private static readonly WhoAmI Query = new();

    private static readonly AuthenticatedPolicy Policy = new();

    [Fact]
    public async Task Anonymous_is_denied_notAuthenticated()
    {
        var outcome = await Policy.AuthorizeAsync(Query, new FakeCurrentUser(), null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.notAuthenticated");
        outcome.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task An_authenticated_identity_with_no_linked_person_is_allowed()
    {
        var outcome = await Policy.AuthorizeAsync(
            Query, new FakeCurrentUser(IsAuthenticated: true, Provider: "auth0", Subject: "sub-1"), null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
        outcome.Code.Should().BeNull();
    }

    [Fact]
    public async Task A_competitor_is_allowed()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: PersonId.New(), HeldRoles: [PersonRole.Competitor]);

        var outcome = await Policy.AuthorizeAsync(Query, user, null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task An_organiser_is_allowed()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: PersonId.New(), HeldRoles: [PersonRole.Organiser]);

        var outcome = await Policy.AuthorizeAsync(Query, user, null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }
}
