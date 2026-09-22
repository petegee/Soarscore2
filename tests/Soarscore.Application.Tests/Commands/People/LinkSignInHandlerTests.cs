// authentication-and-authorisation.md WI-7. Covers LinkSignInHandler directly
// against the People fakes — all four resolution paths (identity hit, email
// hit, create, no email claim), the D3 bootstrap grant on each arm that can
// carry it, D5's bounded retry: the (provider, subject) unique index is
// the arbiter, so a violation means a concurrent sign-in won the race and the
// resolution re-runs exactly once before propagating. Streams are read back
// through ReadStreamAsync — the port, not a store internals peek. The
// verification gate (security review 2026-09-16) is covered on the arms it
// guards: an unverified email blocks the email-match link arm, the create
// arm, and the bootstrap grant.

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

public class LinkSignInHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly ContactDetails SampleContact = new() { Email = "pete@example.org" };

    private static LinkSignInHandler Handler(
        IEventStore store, IPeopleQuery people, FakeCurrentUser user, AuthBootstrap? bootstrap = null) =>
        new(store, people, user, new FakeClock(Now), bootstrap ?? new AuthBootstrap([]));

    private static IReadOnlyList<IDomainEvent> Stream(FakeEventStore store, Guid streamId) =>
        store.ReadStreamAsync(streamId, 0).GetAwaiter().GetResult().Value;

    private static PersonId SeedPerson(FakeEventStore store, string email, params PersonEvent[] afterRegistration)
    {
        var id = PersonId.New();
        var events = new List<IDomainEvent>
        {
            new PersonRegistered(id, "Pete Moss", SampleContact with { Email = email }, null, Now),
        };
        events.AddRange(afterRegistration);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, events).GetAwaiter().GetResult();
        return id;
    }

    private static FakeCurrentUser SignedIn(
        string email,
        string provider = "google-oauth2",
        string subject = "sub-1",
        string? name = "Pete Moss",
        bool emailVerified = true) =>
        new(IsAuthenticated: true, Provider: provider, Subject: subject, Email: email, Name: name, EmailVerified: emailVerified);

    // ---- Arm 3: create ------------------------------------------------------

    [Fact]
    public async Task First_sign_in_creates_the_person_and_links_the_identity_in_one_NoStream_append()
    {
        var store = new FakeEventStore();
        var handler = Handler(store, new FakePeopleQuery(), SignedIn("pete@example.org"));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();

        var stream = Stream(store, result.Value.PersonId.Value);
        stream.Should().HaveCount(2);
        var registered = stream[0].Should().BeOfType<PersonRegistered>().Subject;
        registered.Name.Should().Be("Pete Moss");              // name from the token's name claim
        registered.Contact.Email.Should().Be("pete@example.org");
        stream[1].Should().BeOfType<IdentityLinked>().Which
            .Should().Match<IdentityLinked>(e => e.Provider == "google-oauth2" && e.Subject == "sub-1");
    }

    [Fact]
    public async Task A_token_without_a_name_claim_defaults_the_name_to_the_email_local_part()
    {
        var store = new FakeEventStore();
        var handler = Handler(store, new FakePeopleQuery(), SignedIn("jane.doe@example.org", name: null));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        Stream(store, result.Value.PersonId.Value)[0]
            .Should().BeOfType<PersonRegistered>().Which.Name.Should().Be("jane.doe");
    }

    [Fact]
    public async Task Signing_in_without_an_email_claim_fails_with_auth_signIn_emailRequired_and_appends_nothing()
    {
        var store = new FakeEventStore();
        var handler = Handler(store, new FakePeopleQuery(), SignedIn(email: null!));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.emailRequired");
        (await store.ReadAllAsync(0, 100, TestContext.Current.CancellationToken)).Value.Should().BeEmpty();
    }

    // ---- Arm 1: identity hit ------------------------------------------------

    [Fact]
    public async Task A_known_identity_resolves_to_its_person_and_creates_nothing()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        var personId = SeedPerson(store, "pete@example.org");
        people.SeedIdentity("google-oauth2", "sub-1", personId, PersonRole.Competitor);
        var handler = Handler(store, people, SignedIn("pete@example.org"));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new LinkSignInResult(personId));
        Stream(store, personId.Value).Should().HaveCount(1);   // no new events
    }

    [Fact]
    public async Task A_bootstrap_listed_email_gains_Organiser_on_sign_in_when_the_person_lacks_it()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        var personId = SeedPerson(store, "pete@example.org");
        people.SeedIdentity("google-oauth2", "sub-1", personId);
        var handler = Handler(store, people, SignedIn("pete@example.org"), new AuthBootstrap(["Pete@Example.org"]));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var stream = Stream(store, personId.Value);
        stream.Should().HaveCount(2);
        stream[1].Should().BeOfType<RoleGranted>().Which.Role.Should().Be(PersonRole.Organiser); // ordinal-ignore-case against "Pete@Example.org"
    }

    [Fact]
    public async Task A_second_sign_in_of_a_listed_email_does_not_re_grant()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        var personId = SeedPerson(store, "pete@example.org", new RoleGranted(PersonRole.Organiser, Now));
        people.SeedIdentity("google-oauth2", "sub-1", personId, PersonRole.Organiser);
        var handler = Handler(store, people, SignedIn("pete@example.org"), new AuthBootstrap(["pete@example.org"]));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        Stream(store, personId.Value).Should().HaveCount(2);   // registration + the original grant; nothing appended
    }

    // ---- Arm 2: email hit ---------------------------------------------------

    [Fact]
    public async Task A_first_sign_in_with_a_known_email_links_the_identity_to_that_person()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        var personId = SeedPerson(store, "pete@example.org");
        people.Seed(new PersonSummary(personId, "Pete Moss", "pete@example.org", null, null, null, []));
        var handler = Handler(store, people, SignedIn("pete@example.org", provider: "auth0", subject: "sub-9"));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new LinkSignInResult(personId));
        var stream = Stream(store, personId.Value);
        stream.Should().HaveCount(2);
        stream[1].Should().BeOfType<IdentityLinked>().Which
            .Should().Match<IdentityLinked>(e => e.Provider == "auth0" && e.Subject == "sub-9");
    }

    [Fact]
    public async Task An_email_hit_with_a_listed_email_links_then_grants_in_sequence()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        var personId = SeedPerson(store, "pete@example.org");
        people.Seed(new PersonSummary(personId, "Pete Moss", "pete@example.org", null, null, null, []));
        var handler = Handler(store, people, SignedIn("pete@example.org", provider: "auth0", subject: "sub-9"),
            new AuthBootstrap(["pete@example.org"]));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var stream = Stream(store, personId.Value);
        stream.Should().HaveCount(3);                          // registration, the link, then the grant
        stream[1].Should().BeOfType<IdentityLinked>();
        stream[2].Should().BeOfType<RoleGranted>().Which.Role.Should().Be(PersonRole.Organiser);
    }

    // ---- Arm 3 bootstrap: the grant rides the creation append ---------------

    [Fact]
    public async Task A_listed_email_creating_its_person_is_born_with_the_Organiser_grant()
    {
        var store = new FakeEventStore();
        var handler = Handler(store, new FakePeopleQuery(), SignedIn("pete@example.org"), new AuthBootstrap(["pete@example.org"]));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var stream = Stream(store, result.Value.PersonId.Value);
        stream.Should().HaveCount(3);
        stream[0].Should().BeOfType<PersonRegistered>();
        stream[1].Should().BeOfType<IdentityLinked>();
        stream[2].Should().BeOfType<RoleGranted>().Which.Role.Should().Be(PersonRole.Organiser);
    }

    // ---- Verification gate: an unverified email decides nothing -------------
    // (security review 2026-09-16 — every email-born arm refuses first)

    [Fact]
    public async Task An_unverified_email_cannot_link_to_the_person_registered_under_it()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        // Arm 2's shape: a person already registered under the token's email
        // (read-model row only — the guard must fire before any stream I/O).
        people.Seed(new PersonSummary(PersonId.New(), "Pete Moss", "pete@example.org", null, null, null, []));
        var handler = Handler(store, people, SignedIn("pete@example.org", emailVerified: false));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.emailNotVerified");
        (await store.ReadAllAsync(0, 100, TestContext.Current.CancellationToken)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task An_unverified_email_cannot_create_a_person()
    {
        var store = new FakeEventStore();
        var handler = Handler(store, new FakePeopleQuery(), SignedIn("pete@example.org", emailVerified: false));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.emailNotVerified");
        (await store.ReadAllAsync(0, 100, TestContext.Current.CancellationToken)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task An_unverified_bootstrap_listed_email_fails_sign_in_without_granting_the_role()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        var personId = SeedPerson(store, "pete@example.org");
        people.SeedIdentity("google-oauth2", "sub-1", personId);   // arm 1: identity already linked
        var handler = Handler(store, people, SignedIn("pete@example.org", emailVerified: false),
            new AuthBootstrap(["pete@example.org"]));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.emailNotVerified");
        Stream(store, personId.Value).Should().HaveCount(1);       // registration only — no RoleGranted
    }

    // ---- Arm 2 guard: a person that already has a sign-in is not absorbable
    //      by email match (secure-automatic-identity-linking.md) -------------

    [Fact]
    public async Task An_email_matching_a_person_that_already_has_an_identity_refuses_automatic_linking()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        var personId = SeedPerson(store, "pete@example.org", new IdentityLinked("auth0", "existing-sub", Now));
        people.Seed(new PersonSummary(personId, "Pete Moss", "pete@example.org", null, null, null, []));
        var handler = Handler(store, people, SignedIn("pete@example.org", provider: "google-oauth2", subject: "sub-9"));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.explicitLinkRequired");
        Stream(store, personId.Value).Should().HaveCount(2);   // registration + the existing link; nothing appended
    }

    [Fact]
    public async Task The_email_match_refusal_carries_no_bootstrap_grant()
    {
        var store = new FakeEventStore();
        var people = new FakePeopleQuery();
        var personId = SeedPerson(store, "pete@example.org", new IdentityLinked("auth0", "existing-sub", Now));
        people.Seed(new PersonSummary(personId, "Pete Moss", "pete@example.org", null, null, null, []));
        var handler = Handler(store, people, SignedIn("pete@example.org", provider: "google-oauth2", subject: "sub-9"),
            new AuthBootstrap(["pete@example.org"]));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("auth.signIn.explicitLinkRequired");
        Stream(store, personId.Value).Should().NotContain(e => e is RoleGranted);
    }

    // ---- D5: the unique index is the arbiter; retry the resolution once -----

    [Fact]
    public async Task A_unique_violation_reruns_the_resolution_once_and_resolves_to_the_race_winner()
    {
        var inner = new FakeEventStore();
        var winnerId = PersonId.New();
        await inner.AppendAsync(winnerId.Value, ExpectedVersion.NoStream,
        [
            new PersonRegistered(winnerId, "Winner", SampleContact with { Email = "winner@example.org" }, null, Now),
            new IdentityLinked("google-oauth2", "sub-1", Now),
        ], TestContext.Current.CancellationToken);

        var store = new FakeUniqueIndexEventStore(inner, violations: 1);
        var people = new RaceWinnerIdentityQuery(new FakePeopleQuery());
        // The winner's projection row is seeded, but RaceWinnerIdentityQuery's
        // first lookup misses — the row only becomes readable after the loser's
        // append has been rejected, exactly the D5 race.
        people.Inner.SeedIdentity("google-oauth2", "sub-1", winnerId);

        var handler = Handler(store, people, SignedIn("loser@example.org"));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new LinkSignInResult(winnerId)); // resolved, not created
        people.IdentityLookups.Should().Be(2);                  // the resolution re-ran: missed, then hit the winner
        store.AppendCalls.Should().Be(1);                       // only the rejected create — the retry resolved without appending
        Stream(inner, winnerId.Value).Should().HaveCount(2);    // the winner's stream is untouched
    }

    [Fact]
    public async Task A_second_unique_violation_propagates_after_exactly_one_retry()
    {
        var store = new FakeUniqueIndexEventStore(new FakeEventStore(), violations: 2);
        var handler = Handler(store, new FakePeopleQuery(), SignedIn("pete@example.org"));

        var result = await handler.HandleAsync(new LinkSignIn(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("eventStore.uniqueConstraintViolation");
        store.AppendCalls.Should().Be(2);                       // the attempt plus one retry — never a third
    }
}