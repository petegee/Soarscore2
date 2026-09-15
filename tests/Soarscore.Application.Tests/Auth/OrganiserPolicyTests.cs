// authentication-and-authorisation.md WI-5 — OrganiserPolicy's truth table.
// An unauthenticated caller is denied auth.notAuthenticated (401) — the mode
// table's "anonymous callers get 401s, unauthorised ones 403s", D10's step-1
// shape; every authenticated principal without the Organiser role is denied
// auth.forbidden (WI-9 maps it to 403). The command in each case is a real
// O-mapped command.

using AwesomeAssertions;
using Soarscore.Application.Auth;
using Soarscore.Application.Auth.Policies;
using Soarscore.Application.Commands.People;
using Soarscore.Domain;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Application.Tests.Auth;

public class OrganiserPolicyTests
{
    private static readonly RegisterPerson Command =
        new("Tama Ropata", new ContactDetails { Email = "tama@example.org" }, null);

    private static readonly OrganiserPolicy Policy = new();

    [Fact]
    public async Task Anonymous_is_denied_notAuthenticated()
    {
        var outcome = await Policy.AuthorizeAsync(Command, new FakeCurrentUser(), null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.notAuthenticated");
        outcome.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task An_authenticated_identity_with_no_linked_person_is_denied_forbidden()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, Provider: "auth0", Subject: "sub-1");

        var outcome = await Policy.AuthorizeAsync(Command, user, null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.forbidden");
    }

    [Fact]
    public async Task A_competitor_is_denied_forbidden()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: PersonId.New(), HeldRoles: [PersonRole.Competitor]);

        var outcome = await Policy.AuthorizeAsync(Command, user, null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.forbidden");
    }

    [Fact]
    public async Task An_organiser_is_allowed()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: PersonId.New(), HeldRoles: [PersonRole.Organiser]);

        var outcome = await Policy.AuthorizeAsync(Command, user, null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
        outcome.Code.Should().BeNull();
    }
}
