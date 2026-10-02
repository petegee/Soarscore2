// ui_production-oidc-sign-in.md (owner decision ii): the /userinfo fallback in
// LinkSignInHandler. A real Auth0 access token carries no email claim, so the
// handler recovers it through IUserInfoProvider — but ONLY when the token
// email is missing; a token that already has one never pays for the call.
// Covers the fallback against the People fakes: verified recovery proceeds
// (create and email-match arms), unverified recovery keeps emailNotVerified,
// a lookup with no email keeps emailRequired, and an unreachable IdP fails
// as auth.signIn.identityLookupFailed. Streams are read back through
// ReadStreamAsync — the port, not a store internals peek.

using AwesomeAssertions;
using Soarscore.Application.Auth;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.People;
using Soarscore.Domain;
using Soarscore.Domain.People;
using Xunit;

using Soarscore.Application.Tests.Auth;
using Soarscore.Application.Tests.Shared.People;

namespace Soarscore.Application.Tests.Commands.People;

public sealed class LinkSignInUserInfoFallbackTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    // The stub the port's contract needs: a canned lookup plus a call count,
    // so the "only when the token email is missing" rule is pinned, not
    // merely implied.
    private sealed class FakeUserInfo(UserInfoResult? result = null, Exception? failure = null) : IUserInfoProvider
    {
        public int Calls { get; private set; }

        public Task<UserInfoResult?> LookupAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return failure is not null
                ? Task.FromException<UserInfoResult?>(failure)
                : Task.FromResult(result);
        }
    }

    private static LinkSignInHandler Handler(
        IEventStore store, IPeopleQuery people, FakeCurrentUser user, IUserInfoProvider? userInfo, AuthBootstrap? bootstrap = null) =>
        new(store, people, user, new FakeClock(Now), bootstrap ?? new AuthBootstrap([]), userInfo);

    // A token shaped like a real Auth0 access token: provider + subject, no
    // email claim, no name claim.
    private static FakeCurrentUser TokenWithoutEmail(string provider = "auth0", string subject = "sub-9") =>
        new(IsAuthenticated: true, Provider: provider, Subject: subject, Email: null, Name: null);

    [Fact]
    public async Task A_missing_token_email_recovered_verified_creates_the_person_from_the_userinfo_profile()
    {
        var store = new FakeEventStore();
        var userInfo = new FakeUserInfo(new UserInfoResult("whetu@example.org", EmailVerified: true, Name: "Whetu"));
        var handler = Handler(store, new FakePeopleQuery(), TokenWithoutEmail(), userInfo);

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        userInfo.Calls.Should().Be(1);
        var stream = (await store.ReadStreamAsync(result.Value.PersonId.Value, 0, TestContext.Current.CancellationToken)).Value;
        stream.Should().HaveCount(2);
        stream[0].Should().BeOfType<PersonRegistered>().Which.Should().Match<PersonRegistered>(e =>
            e.Contact.Email == "whetu@example.org" && e.Name == "Whetu");
        stream[1].Should().BeOfType<IdentityLinked>().Which.Should().Match<IdentityLinked>(e =>
            e.Provider == "auth0" && e.Subject == "sub-9");
    }

    [Fact]
    public async Task A_missing_token_email_recovered_verified_links_the_pre_registered_person()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        var id = PersonId.New();
        await store.AppendAsync(id.Value, ExpectedVersion.NoStream,
            [new PersonRegistered(id, "Pete Moss", new ContactDetails { Email = "pete@example.org" }, null, Now)],
            TestContext.Current.CancellationToken);
        people.Seed(new PersonSummary(id, "Pete Moss", "pete@example.org", null, null, null, []));
        var userInfo = new FakeUserInfo(new UserInfoResult("pete@example.org", EmailVerified: true, Name: null));
        var handler = Handler(store, people, TokenWithoutEmail(), userInfo);

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new LinkSignInResult(id));
    }

    [Fact]
    public async Task A_recovered_bootstrap_email_is_born_with_the_Organiser_grant()
    {
        var store = new FakeEventStore();
        var userInfo = new FakeUserInfo(new UserInfoResult("nova@example.org", EmailVerified: true, Name: "Nova"));
        var handler = Handler(store, new FakePeopleQuery(), TokenWithoutEmail(), userInfo, new AuthBootstrap(["nova@example.org"]));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var stream = (await store.ReadStreamAsync(result.Value.PersonId.Value, 0, TestContext.Current.CancellationToken)).Value;
        stream.Should().HaveCount(3);
        stream[2].Should().BeOfType<RoleGranted>().Which.Role.Should().Be(PersonRole.Organiser);
    }

    [Fact]
    public async Task No_fallback_call_happens_when_the_token_already_has_an_email()
    {
        var store = new FakeEventStore();
        var userInfo = new FakeUserInfo(failure: new UserInfoLookupException("must never be called"));
        var user = new FakeCurrentUser(
            IsAuthenticated: true, Provider: "auth0", Subject: "sub-9",
            Email: "pete@example.org", Name: "Pete Moss", EmailVerified: true);
        var handler = Handler(store, new FakePeopleQuery(), user, userInfo);

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        userInfo.Calls.Should().Be(0);
    }

    [Fact]
    public async Task No_fallback_call_happens_when_the_identity_is_already_linked()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        var personId = PersonId.New();
        await store.AppendAsync(personId.Value, ExpectedVersion.NoStream,
            [new PersonRegistered(personId, "Pete Moss", new ContactDetails { Email = "pete@example.org" }, null, Now)],
            TestContext.Current.CancellationToken);
        people.SeedIdentity("auth0", "sub-9", personId);
        var userInfo = new FakeUserInfo(failure: new UserInfoLookupException("must never be called"));
        var handler = Handler(store, people, TokenWithoutEmail(), userInfo);

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new LinkSignInResult(personId));
        userInfo.Calls.Should().Be(0);
    }

    [Fact]
    public async Task A_recovered_but_unverified_email_keeps_emailNotVerified_and_appends_nothing()
    {
        var store = new FakeEventStore();
        var userInfo = new FakeUserInfo(new UserInfoResult("pete@example.org", EmailVerified: false, Name: "Pete"));
        var handler = Handler(store, new FakePeopleQuery(), TokenWithoutEmail(), userInfo);

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.emailNotVerified");
        (await store.ReadAllAsync(0, 100, TestContext.Current.CancellationToken)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task A_lookup_with_no_profile_keeps_emailRequired()
    {
        var store = new FakeEventStore();
        var userInfo = new FakeUserInfo(result: null);
        var handler = Handler(store, new FakePeopleQuery(), TokenWithoutEmail(), userInfo);

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.emailRequired");
        (await store.ReadAllAsync(0, 100, TestContext.Current.CancellationToken)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task A_lookup_whose_profile_has_no_email_keeps_emailRequired()
    {
        var store = new FakeEventStore();
        var userInfo = new FakeUserInfo(new UserInfoResult(Email: null, EmailVerified: true, Name: "No Email"));
        var handler = Handler(store, new FakePeopleQuery(), TokenWithoutEmail(), userInfo);

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.emailRequired");
    }

    [Fact]
    public async Task An_unreachable_IdP_fails_as_identityLookupFailed_and_appends_nothing()
    {
        var store = new FakeEventStore();
        var userInfo = new FakeUserInfo(failure: new UserInfoLookupException("The identity provider could not be reached."));
        var handler = Handler(store, new FakePeopleQuery(), TokenWithoutEmail(), userInfo);

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.identityLookupFailed");
        (await store.ReadAllAsync(0, 100, TestContext.Current.CancellationToken)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_a_provider_a_missing_token_email_still_fails_as_emailRequired()
    {
        // mock/none compositions register no IUserInfoProvider — the optional
        // dependency stays null and the pre-fallback semantics are untouched.
        var store = new FakeEventStore();
        var handler = Handler(store, new FakePeopleQuery(), TokenWithoutEmail(), userInfo: null);

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.emailRequired");
    }
}
