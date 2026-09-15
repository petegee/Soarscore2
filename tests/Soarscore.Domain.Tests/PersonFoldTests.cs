using AwesomeAssertions;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Domain.Tests;

public class PersonFoldTests
{
    private static readonly ContactDetails SampleContact = new()
    {
        Email = "pilot@example.com",
        Phone = "021 555 0100",
        HomeCity = "Auckland",
    };

    private static readonly ClubAffiliation SampleClub = new()
    {
        ClubName = "Auckland Soaring Club",
        MembershipNumber = "ASC-042",
    };

    [Fact]
    public void Registered_creates_the_projection_from_an_empty_stream()
    {
        var id = PersonId.New();
        var registeredAt = DateTimeOffset.UtcNow;
        var @event = new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, registeredAt);

        var person = Person.Create(@event);

        person.Should().NotBeNull();
        person.Id.Should().Be(id);
        person.Name.Should().Be("Alex Pilot");
        person.Contact.Should().Be(SampleContact);
        person.Club.Should().Be(SampleClub);
    }

    [Fact]
    public void ContactDetailsChanged_replaces_contact_and_leaves_everything_else_untouched()
    {
        var id = PersonId.New();
        var registered = Person.Create(new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));
        var newContact = SampleContact with { Phone = "021 555 0199" };

        var changed = registered.Apply(new ContactDetailsChanged(newContact, DateTimeOffset.UtcNow));

        changed.Contact.Should().Be(newContact);
        changed.Id.Should().Be(registered.Id);
        changed.Name.Should().Be(registered.Name);
        changed.Club.Should().Be(registered.Club);
    }

    [Fact]
    public void ClubAffiliationChanged_can_clear_the_club_by_folding_null()
    {
        var id = PersonId.New();
        var registered = Person.Create(new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var cleared = registered.Apply(new ClubAffiliationChanged(null, DateTimeOffset.UtcNow));

        cleared.Club.Should().BeNull();
    }

    [Fact]
    public void PersonRenamed_replaces_the_name()
    {
        var id = PersonId.New();
        var registered = Person.Create(new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var renamed = registered.Apply(new PersonRenamed("Alexandra Pilot", DateTimeOffset.UtcNow));

        renamed.Name.Should().Be("Alexandra Pilot");
    }

    [Fact]
    public void ContactDetailsChanged_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            Person.Apply(null, new ContactDetailsChanged(SampleContact, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClubAffiliationChanged_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            Person.Apply(null, new ClubAffiliationChanged(SampleClub, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PersonRenamed_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            Person.Apply(null, new PersonRenamed("Alexandra Pilot", DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_full_event_stream_folds_in_order_to_the_expected_final_state()
    {
        var id = PersonId.New();
        var registeredAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var renamedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var contactChangedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var clubClearedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var newContact = SampleContact with { Phone = "021 555 0199" };

        PersonEvent[] stream =
        [
            new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, registeredAt),
            new PersonRenamed("Alexandra Pilot", renamedAt),
            new ContactDetailsChanged(newContact, contactChangedAt),
            new ClubAffiliationChanged(null, clubClearedAt),
        ];

        var final = stream.Aggregate((Person?)null, Person.Apply);

        final.Should().NotBeNull();
        final!.Id.Should().Be(id);
        final.Name.Should().Be("Alexandra Pilot");
        final.Contact.Should().Be(newContact);
        final.Club.Should().BeNull();
    }

    [Fact]
    public void RoleGranted_folds_the_role_into_the_set()
    {
        var registered = Person.Create(new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var granted = registered.Apply(new RoleGranted(PersonRole.Organiser, DateTimeOffset.UtcNow));

        granted.Roles.Should().ContainSingle().Which.Should().Be(PersonRole.Organiser);
        granted.Id.Should().Be(registered.Id);
        granted.Name.Should().Be(registered.Name);
        granted.Contact.Should().Be(registered.Contact);
        granted.Identities.Should().BeEmpty();
    }

    [Fact]
    public void RoleGranted_of_an_already_held_role_folds_to_no_change()
    {
        var registered = Person.Create(new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));
        var granted = registered.Apply(new RoleGranted(PersonRole.Competitor, DateTimeOffset.UtcNow));

        var grantedAgain = granted.Apply(new RoleGranted(PersonRole.Competitor, DateTimeOffset.UtcNow));

        grantedAgain.Roles.Should().ContainSingle().Which.Should().Be(PersonRole.Competitor);
    }

    [Fact]
    public void RoleRevoked_folds_the_role_out_of_the_set()
    {
        var at = DateTimeOffset.UtcNow;
        var registered = Person.Create(new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, at));
        var granted = registered.Apply(new RoleGranted(PersonRole.Competitor, at));

        var revoked = granted.Apply(new RoleRevoked(PersonRole.Competitor, at));

        revoked.Roles.Should().BeEmpty();
    }

    [Fact]
    public void RoleRevoked_of_a_role_not_held_folds_to_no_change()
    {
        var registered = Person.Create(new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var revoked = registered.Apply(new RoleRevoked(PersonRole.Organiser, DateTimeOffset.UtcNow));

        revoked.Roles.Should().BeEmpty();
    }

    [Fact]
    public void IdentityLinked_folds_the_link_into_the_identities()
    {
        var registered = Person.Create(new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var linked = registered.Apply(new IdentityLinked("auth0", "auth0|12345", DateTimeOffset.UtcNow));

        linked.Identities.Should().ContainSingle().Which.Should().Be(new IdentityLink("auth0", "auth0|12345"));
        linked.Id.Should().Be(registered.Id);
        linked.Roles.Should().BeEmpty();
    }

    [Fact]
    public void A_duplicated_IdentityLinked_event_folds_to_a_single_link()
    {
        var registered = Person.Create(new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));
        var at = DateTimeOffset.UtcNow;

        var linked = registered
            .Apply(new IdentityLinked("auth0", "auth0|12345", at))
            .Apply(new IdentityLinked("auth0", "auth0|12345", at));

        linked.Identities.Should().ContainSingle().Which.Should().Be(new IdentityLink("auth0", "auth0|12345"));
    }

    [Fact]
    public void RoleGranted_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            Person.Apply(null, new RoleGranted(PersonRole.Competitor, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RoleRevoked_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            Person.Apply(null, new RoleRevoked(PersonRole.Competitor, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IdentityLinked_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            Person.Apply(null, new IdentityLinked("auth0", "auth0|12345", DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_stream_with_roles_and_identity_links_folds_in_order_to_the_expected_final_state()
    {
        var id = PersonId.New();
        var registeredAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var organiserAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var competitorAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var linkedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var revokedAt = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        PersonEvent[] stream =
        [
            new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, registeredAt),
            new RoleGranted(PersonRole.Organiser, organiserAt),
            new RoleGranted(PersonRole.Competitor, competitorAt),
            new IdentityLinked("auth0", "auth0|12345", linkedAt),
            new IdentityLinked("google-oauth2", "google|67890", linkedAt),
            new RoleRevoked(PersonRole.Competitor, revokedAt),
        ];

        var final = stream.Aggregate((Person?)null, Person.Apply);

        final.Should().NotBeNull();
        final!.Id.Should().Be(id);
        final.Roles.Should().BeEquivalentTo([PersonRole.Organiser]);
        final.Identities.Should().HaveCount(2);
        final.Identities.Should().Contain(new IdentityLink("auth0", "auth0|12345"));
        final.Identities.Should().Contain(new IdentityLink("google-oauth2", "google|67890"));
    }
}
