// authentication-and-authorisation.md WI-10 — Roles.feature's steps. Every
// acting principal is a persona token; every target is a PersonId resolved by
// a sign-in (D2: roles and personhood come from the read model, and this
// feature proves a revoke bites on the next call, with the same token).

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.People;
using Soarscore.Domain.People;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class RolesSteps
{
    private readonly Dictionary<AuthPersona, PersonId> _personIds = new();
    private HttpResponseMessage? _response;

    private async Task<PersonId> PersonIdOfAsync(AuthPersona persona)
    {
        if (_personIds.TryGetValue(persona, out var known))
        {
            return known;
        }

        var response = await AuthApi.PostAsync(TestJwt.ForPerson(persona), "/link-sign-in", new LinkSignIn());
        response.EnsureSuccessStatusCode();
        var personId = (await AuthApi.ReadAsync<LinkSignInView>(response)).PersonId;
        _personIds[persona] = personId;
        return personId;
    }

    // ---------------------------------------------------------------- Given

    [Given(@"^(Tama|FieldRig|Nova) has signed in$")]
    public async Task GivenPersonaHasSignedIn(string persona) =>
        await PersonIdOfAsync(persona switch
        {
            "Tama" => AuthActors.Competitor,
            "FieldRig" => AuthActors.Unlinked,
            _ => AuthActors.Bootstrap,
        });

    // ----------------------------------------------------------------- When

    [When(@"^Pete grants (Tama|FieldRig) the Competitor role$")]
    public async Task WhenPeteGrantsTheCompetitorRole(string persona)
    {
        var personId = await PersonIdOfAsync(persona == "Tama" ? AuthActors.Competitor : AuthActors.Unlinked);
        _response = await AuthApi.PostAsync(
            TestJwt.ForPerson(AuthActors.Organiser), "/grant-role", new GrantRole(personId, PersonRole.Competitor));
        _response.EnsureSuccessStatusCode();
    }

    [When(@"^Pete revokes (Tama|FieldRig|Nova)'s Competitor role$")]
    public async Task WhenPeteRevokesTheCompetitorRole(string persona)
    {
        var personId = await PersonIdOfAsync(persona switch
        {
            "Tama" => AuthActors.Competitor,
            "FieldRig" => AuthActors.Unlinked,
            _ => AuthActors.Bootstrap,
        });
        _response = await AuthApi.PostAsync(
            TestJwt.ForPerson(AuthActors.Organiser), "/revoke-role", new RevokeRole(personId, PersonRole.Competitor));
        _response.EnsureSuccessStatusCode();
    }

    [When(@"^Pete revokes Nova's Organiser role$")]
    public async Task WhenPeteRevokesNovasOrganiserRole()
    {
        var personId = await PersonIdOfAsync(AuthActors.Bootstrap);
        _response = await AuthApi.PostAsync(
            TestJwt.ForPerson(AuthActors.Organiser), "/revoke-role", new RevokeRole(personId, PersonRole.Organiser));
        _response.EnsureSuccessStatusCode();
    }

    [When(@"^Pete attempts to revoke his own Organiser role$")]
    public async Task WhenPeteAttemptsToRevokeHisOwnOrganiserRole()
    {
        var personId = await PersonIdOfAsync(AuthActors.Organiser);
        _response = await AuthApi.PostAsync(
            TestJwt.ForPerson(AuthActors.Organiser), "/revoke-role", new RevokeRole(personId, PersonRole.Organiser));
    }

    [When(@"^Tama attempts to grant FieldRig the Competitor role$")]
    public async Task WhenTamaAttemptsToGrantTheCompetitorRole()
    {
        var personId = await PersonIdOfAsync(AuthActors.Unlinked);
        _response = await AuthApi.PostAsync(
            TestJwt.ForPerson(AuthActors.Competitor), "/grant-role", new GrantRole(personId, PersonRole.Competitor));
    }

    [When(@"^FieldRig renames herself$")]
    public async Task WhenFieldRigRenamesHerself() => await RenameRigAsync();

    [When(@"^FieldRig renames herself again$")]
    public async Task WhenFieldRigRenamesHerselfAgain() => await RenameRigAsync();

    private async Task RenameRigAsync()
    {
        var personId = await PersonIdOfAsync(AuthActors.Unlinked);
        _response = await AuthApi.PostAsync(
            TestJwt.ForPerson(AuthActors.Unlinked), "/rename-person", new RenamePerson(personId, $"Field Rig {Guid.NewGuid():N}"));
    }

    [When(@"^FieldRig renames Tama$")]
    public async Task WhenFieldRigRenamesTama()
    {
        var personId = await PersonIdOfAsync(AuthActors.Competitor);
        _response = await AuthApi.PostAsync(
            TestJwt.ForPerson(AuthActors.Unlinked), "/rename-person", new RenamePerson(personId, "Not Tama"));
    }

    // ----------------------------------------------------------------- Then

    [Then(@"^the rename is accepted$")]
    public void ThenTheRenameIsAccepted() => _response!.IsSuccessStatusCode.Should().BeTrue();

    [Then(@"^the response is 403 refusing with auth\.forbidden$")]
    public async Task ThenTheResponseIs403RefusingWithForbidden()
    {
        _response!.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AuthApi.ProblemTitleAsync(_response)).Should().Be("auth.forbidden");
    }

    [Then(@"^the response is 409 refusing with person\.lastOrganiser$")]
    public async Task ThenTheResponseIs409RefusingWithLastOrganiser()
    {
        _response!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AuthApi.ProblemTitleAsync(_response)).Should().Be("person.lastOrganiser");
    }

    [Then(@"^/who-am-i shows (Tama|FieldRig) holding (the Competitor role|no roles)$")]
    public async Task ThenWhoAmIShowsThePersonaHolding(string persona, string holding)
    {
        var actor = persona == "Tama" ? AuthActors.Competitor : AuthActors.Unlinked;
        var view = await AuthApi.ReadAsync<WhoAmIView>(
            await AuthApi.GetAsync(TestJwt.ForPerson(actor), "/who-am-i"));

        if (holding == "the Competitor role")
        {
            view.Roles.Should().Contain(PersonRole.Competitor);
        }
        else
        {
            view.Roles.Should().BeEmpty();
        }
    }

    private sealed record LinkSignInView(PersonId PersonId, bool PersonCreated);

    private sealed record WhoAmIView(bool IsAuthenticated, PersonId? PersonId, IReadOnlyList<PersonRole> Roles, string? Name);
}
