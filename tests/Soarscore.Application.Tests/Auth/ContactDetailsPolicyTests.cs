// secure-automatic-identity-linking.md (security review 2026-09-17) —
// ContactDetailsPolicy's truth table: authenticated first (401), organiser
// unrestricted, self bounded to the stored address unchanged or the
// IdP-verified token email, everything else denied with
// auth.contact.emailOwnership. Fail-closed arms (no port, wrong command)
// follow the pipeline's auth.policyMissing stance. The command in each case
// is the real ChangePersonContactDetails.

using AwesomeAssertions;
using Soarscore.Application.Auth;
using Soarscore.Application.Auth.Policies;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;
using Xunit;

using Soarscore.Application.Tests.Auth;
using Soarscore.Application.Tests.Shared.People;

namespace Soarscore.Application.Tests.Auth;

public class ContactDetailsPolicyTests
{
    private static readonly PersonId SomePerson = PersonId.New();
    private static readonly PersonId OtherPerson = PersonId.New();

    private const string StoredEmail = "pete@example.org";
    private const string TokenEmail = "tama@example.org";
    private const string VictimEmail = "organiser@example.org";

    private static ChangePersonContactDetails Command(string? email) =>
        new(SomePerson, email is null ? null! : new ContactDetails { Email = email, Phone = "021 555" });

    private static IServiceProvider Services(FakePeopleQuery? people) =>
        people is null
            ? new FakeServiceProvider(new Dictionary<Type, object>())
            : new FakeServiceProvider(new Dictionary<Type, object> { [typeof(IPeopleQuery)] = people });

    private static FakePeopleQuery PeopleWithStoredRow()
    {
        var people = new FakePeopleQuery();
        people.Seed(new PersonSummary(SomePerson, "Pete Moss", StoredEmail, null, null, null, []));
        return people;
    }

    private static readonly ContactDetailsPolicy Policy = new();

    [Fact]
    public async Task Anonymous_is_denied_notAuthenticated()
    {
        var outcome = await Policy.AuthorizeAsync(Command(TokenEmail), new FakeCurrentUser(), Services(null), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.notAuthenticated");
    }

    [Fact]
    public async Task An_organiser_may_set_anyone_s_email_to_anything()
    {
        // The attack shape itself — organiser authority is the repair path.
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: OtherPerson, HeldRoles: [PersonRole.Organiser]);

        var outcome = await Policy.AuthorizeAsync(Command(VictimEmail), user, Services(PeopleWithStoredRow()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task A_person_editing_someone_else_is_denied_forbidden()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: OtherPerson, HeldRoles: [PersonRole.Competitor]);

        var outcome = await Policy.AuthorizeAsync(Command(TokenEmail), user, Services(PeopleWithStoredRow()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.forbidden");
    }

    [Fact]
    public async Task Keeping_the_stored_email_unchanged_is_allowed_even_when_the_token_email_differs()
    {
        // The phone-edit shape: stored pete@, verified token tama@ — the
        // unchanged half allows, the match key is untouched.
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, Email: TokenEmail);

        var outcome = await Policy.AuthorizeAsync(Command(StoredEmail), user, Services(PeopleWithStoredRow()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Setting_the_verified_token_email_is_allowed()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, Email: TokenEmail);

        var outcome = await Policy.AuthorizeAsync(Command(TokenEmail), user, Services(PeopleWithStoredRow()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Setting_a_third_address_is_denied_emailOwnership()
    {
        // The attack shape: the token's verified email is the caller's own —
        // no IdP vouches for a third address, so the change is refused.
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, Email: TokenEmail);

        var outcome = await Policy.AuthorizeAsync(Command(VictimEmail), user, Services(PeopleWithStoredRow()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.contact.emailOwnership");
    }

    [Fact]
    public async Task An_unverified_token_email_cannot_become_the_contact_email()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, Email: TokenEmail, EmailVerified: false);

        var outcome = await Policy.AuthorizeAsync(Command(TokenEmail), user, Services(PeopleWithStoredRow()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.contact.emailOwnership");
    }

    [Fact]
    public async Task A_machine_token_without_an_email_claim_cannot_change_the_address()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, Email: null);

        var outcome = await Policy.AuthorizeAsync(Command(VictimEmail), user, Services(PeopleWithStoredRow()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.contact.emailOwnership");
    }

    [Fact]
    public async Task A_missing_read_model_row_falls_to_the_strict_verified_rule()
    {
        var people = new FakePeopleQuery();   // no row for SomePerson
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, Email: TokenEmail);

        var allowed = await Policy.AuthorizeAsync(Command(TokenEmail), user, Services(people), TestContext.Current.CancellationToken);
        allowed.Allowed.Should().BeTrue("the new email is the verified token email");

        var denied = await Policy.AuthorizeAsync(Command(VictimEmail), user, Services(people), TestContext.Current.CancellationToken);
        denied.Allowed.Should().BeFalse();
        denied.Code.Should().Be("auth.contact.emailOwnership");
    }

    [Fact]
    public async Task A_null_contact_binds_to_a_denial_not_a_crash()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, Email: TokenEmail);

        var outcome = await Policy.AuthorizeAsync(Command(null), user, Services(PeopleWithStoredRow()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.contact.emailOwnership");
    }

    [Fact]
    public async Task A_missing_read_model_port_fails_closed_policyMissing()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, Email: TokenEmail);

        var outcome = await Policy.AuthorizeAsync(Command(TokenEmail), user, Services(null), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.policyMissing");
    }

    [Fact]
    public async Task A_command_of_another_type_fails_closed_policyMissing()
    {
        var user = new FakeCurrentUser(IsAuthenticated: true, PersonId: SomePerson, Email: TokenEmail);

        var outcome = await Policy.AuthorizeAsync(
            new RenamePerson(SomePerson, "New Name"), user, Services(PeopleWithStoredRow()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.policyMissing");
    }
}
