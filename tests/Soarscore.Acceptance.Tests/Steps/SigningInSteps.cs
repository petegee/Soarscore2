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
    private readonly string _thirdProviderSub;
    private readonly string _newcomerEmail;

    private PersonId _newcomerPersonId;
    private PersonId _preRegisteredPersonId;
    private HttpResponseMessage? _response;

    public SigningInSteps()
    {
        var id = Guid.NewGuid().ToString("N");
        _secondProviderSub = $"google-oauth2|aroha-{id}-alt";
        _thirdProviderSub = $"microsoft|aroha-{id}-third";
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

    [Given(@"^an organiser has pre-registered a person under Aroha's email$")]
    public async Task GivenAnOrganiserHasPreRegisteredAPersonUnderArohasEmail()
    {
        // D5 arm 2's legitimate half: organiser pre-registration (RegisterPerson
        // is organiser-only), the person holding no identity links — the shape
        // the email-match arm still links on first sign-in.
        // /register-person returns the bare PersonId — {"value": "<guid>"} on
        // the wire (LinkSignInResult's nested shape is why its own view reads
        // PersonId directly).
        var registered = await AuthApi.PostCommandAsync<PersonIdView>(
            TestJwt.ForPerson(AuthActors.Organiser),
            "/register-person",
            new RegisterPerson("Pre Registered", new ContactDetails { Email = _newcomerEmail }, null));
        _preRegisteredPersonId = new PersonId(registered.Value);
    }

    [When(@"^Aroha signs in again$")]
    public async Task WhenArohaSignsInAgain() => await WhenThePersonaSignsIn("Aroha");

    [When(@"^the same email signs in through google-oauth2$")]
    public async Task WhenTheSameEmailSignsInThroughASecondProvider()
    {
        // Refused under secure-automatic-identity-linking.md: the person the
        // email matches already holds Aroha's first sign-in — the Then asserts
        // the 409, so no EnsureSuccessStatusCode here.
        _response = await SignInAsync(TestJwt.ForIdentity(_secondProviderSub, _newcomerEmail, "Aroha"));
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

    [When(@"^Aroha sets their contact email to the bootstrap organiser's address$")]
    public async Task WhenArohaSetsTheirContactEmailToTheBootstrapOrganisersAddress()
    {
        // The attack shape (secure-automatic-identity-linking.md): the
        // contact email is D5's email-match key and D3's bootstrap key, so a
        // self-edit claiming Nova's address is exactly what must refuse.
        _response = await AuthApi.PostAsync(
            TestJwt.ForIdentity(_newcomerSub, _newcomerEmail, "Aroha"),
            "/change-person-contact-details",
            new ChangePersonContactDetails(_newcomerPersonId, new ContactDetails { Email = AuthActors.Bootstrap.Email }));
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

    [Then(@"^the response is 403 refusing with auth\.contact\.emailOwnership$")]
    public async Task ThenTheResponseIs403RefusingWithEmailOwnership()
    {
        _response!.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AuthApi.ProblemTitleAsync(_response)).Should().Be("auth.contact.emailOwnership");
    }

    [Then(@"^the response is 409 refusing with auth\.signIn\.explicitLinkRequired$")]
    public async Task ThenTheResponseIs409RefusingWithExplicitLinkRequired()
    {
        _response!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AuthApi.ProblemTitleAsync(_response)).Should().Be("auth.signIn.explicitLinkRequired");
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

    [Then(@"^the sign-in returns the pre-registered person$")]
    public void ThenTheSignInReturnsThePreRegisteredPerson()
    {
        var result = AuthApi.ReadAsync<LinkSignInView>(_response!).GetAwaiter().GetResult();
        result.PersonId.Should().Be(_preRegisteredPersonId);
    }

    [Then(@"^/who-am-i resolves the refused identity to no person$")]
    public async Task ThenWhoAmIResolvesTheRefusedIdentityToNoPerson()
    {
        // The 409 linked nothing: the refused identity still resolves to an
        // authenticated-but-unlinked caller — nobody's person, no roles.
        var view = await AuthApi.ReadAsync<WhoAmIView>(
            await AuthApi.GetAsync(TestJwt.ForIdentity(_secondProviderSub, _newcomerEmail, "Aroha"), "/who-am-i"));

        view.IsAuthenticated.Should().BeTrue();
        view.PersonId.Should().BeNull();
        view.Roles.Should().BeEmpty();
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

    private sealed record PersonIdView(Guid Value);
}
