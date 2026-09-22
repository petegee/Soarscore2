// authentication-and-authorisation.md WI-5 — SelfOrOrganiserPolicy's truth
// table: authenticated first (401, D10's step-1 shape), then self = the
// principal's D2-resolved PersonId equals the command's PersonRef, then the
// organiser half. The command in each case is a real S-mapped command.

using AwesomeAssertions;
using Soarscore.Application.Auth;
using Soarscore.Application.Auth.Policies;
using Soarscore.Application.Commands.People;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Application.Tests.Auth;

public class SelfOrOrganiserPolicyTests
{
    private static readonly PersonId SomePerson = PersonId.New();
    private static readonly PersonId OtherPerson = PersonId.New();

    private static readonly RenamePerson Command = new(SomePerson, "New Name");

    private static readonly SelfOrOrganiserPolicy Policy = new();

    [Fact]
    public async Task Anonymous_is_denied_notAuthenticated()
    {
        var outcome = await Policy.AuthorizeAsync(Command, new FakeCurrentUser(), null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.notAuthenticated");
    }

    [Fact]
    public async Task An_authenticated_unlinked_identity_is_denied_forbidden()
    {
        // No PersonId, no role: the self-half can never match (null ≠ PersonRef)
        // and the organiser-half is unreachable.
        var user = new FakeCurrentUser(IsAuthenticated: true, Provider: "auth0", Subject: "sub-1");

        var outcome = await Policy.AuthorizeAsync(Command, user, null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.forbidden");
    }

    [Fact]
    public async Task A_competitor_editing_their_own_record_is_allowed()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, HeldRoles: [PersonRole.Competitor]);

        var outcome = await Policy.AuthorizeAsync(Command, user, null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task A_competitor_editing_someone_else_is_denied_forbidden()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: OtherPerson, HeldRoles: [PersonRole.Competitor]);

        var outcome = await Policy.AuthorizeAsync(Command, user, null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.forbidden");
    }

    [Fact]
    public async Task An_organiser_editing_someone_else_is_allowed()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: OtherPerson, HeldRoles: [PersonRole.Organiser]);

        var outcome = await Policy.AuthorizeAsync(Command, user, null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task An_organiser_editing_their_own_record_is_allowed()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, HeldRoles: [PersonRole.Organiser]);

        var outcome = await Policy.AuthorizeAsync(Command, user, null!, TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }
}
