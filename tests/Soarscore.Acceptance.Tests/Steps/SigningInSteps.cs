// authentication-and-authorisation.md WI-10 — SigningIn.feature's steps, all
// through the mock-mode factory over real HTTP. The sign-in command carries
// no body fields at all (WI-7: identity comes from the validated token, never
// request JSON), so every When is the same POST differing only in token.
//
// The creation scenarios mint a per-scenario identity (never seeded, never
// seen by an earlier scenario): xunit.v3 orders facts by method name, not
// declaration order, so a fixed "newcomer" persona could already exist by the
// time "a first sign-in creates the person" runs. Nova — the bootstrap-listed
// persona — is the one shared identity here, and her scenario is
// order-independent by construction: her sign-in re-grants the Organiser role
// idempotently (D3), however many times a run revokes it elsewhere.

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.People;
using Soarscore.Domain.People;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class SigningInSteps
{
    // Per-scenario identity: fresh every scenario, so "a first sign-in
    // creates the person" is true no matter which order the facts run in.
    private readonly string _newcomerSub = $"mock|aroha-{Guid.NewGuid():N}";
    private readonly string _secondProviderSub;
    private readonly string _newcomerEmail;

    private PersonId _newcomerPersonId;
    private HttpResponseMessage? _response;

    public SigningInSteps()
    {
        var id = Guid.NewGuid().ToString("N");
        _secondProviderSub = $"google-oauth2|aroha-{id}-alt";
        _newcomerEmail = $"aroha-{id}@example.org";
    }

    private static async Task<HttpResponseMessage> SignInAsync(string token) =>
        await AuthApi.PostAsync(token, "/link-sign-in", new LinkSignIn());

    // ----------------------------------------------------------------- When

    [When(@"^an anonymous caller posts to /link-sign-in$")]
    public async Task WhenAnAnonymousCallerPostsToLinkSignIn()
    {
        _response = await AuthApi.PostAnonymousAsync("/link-sign-in", new LinkSignIn());
    }

    [When(@"^(Aroha|Nova) signs in$")]
    public async Task WhenThePersonaSignsIn(string persona)
    {
        _response = await SignInAsync(persona == "Aroha"
            ? TestJwt.ForIdentity(_newcomerSub, _newcomerEmail, "Aroha")
            : TestJwt.ForPerson(AuthActors.Bootstrap));
        _response.EnsureSuccessStatusCode();

        if (persona == "Aroha")
        {
            _newcomerPersonId = (await AuthApi.ReadAsync<LinkSignInView>(_response)).PersonId;
        }
    }

    [Given(@"^Aroha has signed in$")]
    public async Task GivenArohaHasSignedIn() => await WhenThePersonaSignsIn("Aroha");

    [When(@"^Aroha signs in again$")]
    public async Task WhenArohaSignsInAgain() => await WhenThePersonaSignsIn("Aroha");

    [When(@"^the same email signs in through google-oauth2$")]
    public async Task WhenTheSameEmailSignsInThroughASecondProvider()
    {
        _response = await SignInAsync(TestJwt.ForIdentity(_secondProviderSub, _newcomerEmail, "Aroha"));
        _response.EnsureSuccessStatusCode();
    }

    [When(@"^a caller whose token email is not verified signs in$")]
    public async Task WhenAnUnverifiedEmailCallerSignsIn()
    {
        // The per-scenario newcomer identity, minted with email_verified =
        // false: no person exists under its email, so this is the create arm
        // the verification gate refuses (security review 2026-09-16).
        _response = await SignInAsync(
            TestJwt.ForIdentityWithoutVerifiedEmail(_newcomerSub, _newcomerEmail, "Aroha"));
    }

    // ----------------------------------------------------------------- Then

    [Then(@"^the response is 401 refusing with auth\.notAuthenticated$")]
    public async Task ThenTheResponseIs401RefusingWithNotAuthenticated()
    {
        _response!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await AuthApi.ProblemTitleAsync(_response)).Should().Be("auth.notAuthenticated");
    }

    [Then(@"^the response is 403 refusing with auth\.signIn\.emailNotVerified$")]
    public async Task ThenTheResponseIs403RefusingWithEmailNotVerified()
    {
        _response!.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AuthApi.ProblemTitleAsync(_response)).Should().Be("auth.signIn.emailNotVerified");
    }

    // The response carries no created/linked distinction — an account-existence
    // oracle removed by the security review 2026-09-16; the who-am-i and
    // "returns the same person" steps carry the identity resolution coverage.
    [Then(@"^the sign-in returns the person$")]
    public async Task ThenTheSignInReturnsThePerson()
    {
        var result = await AuthApi.ReadAsync<LinkSignInView>(_response!);
        result.PersonId.Should().NotBeNull();
        _newcomerPersonId = result.PersonId;
    }

    [Then(@"^returns the same person$")]
    public void ThenTheSignInReturnsTheSamePerson()
    {
        var result = AuthApi.ReadAsync<LinkSignInView>(_response!).GetAwaiter().GetResult();
        result.PersonId.Should().Be(_newcomerPersonId);
    }

    [Then(@"^/who-am-i resolves the newcomer to that person holding no roles$")]
    public async Task ThenWhoAmIResolvesTheNewcomerToThatPersonHoldingNoRoles()
    {
        var view = await AuthApi.ReadAsync<WhoAmIView>(
            await AuthApi.GetAsync(TestJwt.ForIdentity(_newcomerSub, _newcomerEmail, "Aroha"), "/who-am-i"));

        view.IsAuthenticated.Should().BeTrue();
        view.PersonId.Should().Be(_newcomerPersonId);
        view.Roles.Should().BeEmpty();
    }

    [Then(@"^/who-am-i shows Nova holding the Organiser role$")]
    public async Task ThenWhoAmIShowsNovaHoldingTheOrganiserRole()
    {
        var view = await AuthApi.ReadAsync<WhoAmIView>(
            await AuthApi.GetAsync(TestJwt.ForPerson(AuthActors.Bootstrap), "/who-am-i"));

        view.IsAuthenticated.Should().BeTrue();
        view.PersonId.Should().NotBeNull();
        view.Roles.Should().Contain(PersonRole.Organiser);
    }

    // The wire shapes — read with the server's own serialiser options.
    private sealed record LinkSignInView(PersonId PersonId);

    private sealed record WhoAmIView(bool IsAuthenticated, PersonId? PersonId, IReadOnlyList<PersonRole> Roles, string? Name);
}
