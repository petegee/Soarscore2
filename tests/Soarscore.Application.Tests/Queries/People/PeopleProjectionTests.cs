using AwesomeAssertions;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Application.Tests.Queries.People;

public class PeopleProjectionTests
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
    public void PersonRegistered_creates_the_summary_from_an_empty_projection()
    {
        var id = PersonId.New();
        var @event = new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow);

        var summary = PeopleProjection.Apply(null, @event);

        summary.Should().NotBeNull();
        summary!.Id.Should().Be(id);
        summary.Name.Should().Be("Alex Pilot");
        summary.Email.Should().Be("pilot@example.com");
        summary.Phone.Should().Be("021 555 0100");
        summary.HomeCity.Should().Be("Auckland");
        summary.ClubName.Should().Be("Auckland Soaring Club");
        summary.Roles.Should().BeEmpty();
    }

    [Fact]
    public void PersonRegistered_with_no_club_leaves_ClubName_null()
    {
        var summary = PeopleProjection.Apply(null, new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, null, DateTimeOffset.UtcNow));

        summary!.ClubName.Should().BeNull();
    }

    [Fact]
    public void PersonRenamed_replaces_the_name_and_leaves_everything_else_untouched()
    {
        var registered = PeopleProjection.Apply(null, new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var renamed = PeopleProjection.Apply(registered, new PersonRenamed("Alexandra Pilot", DateTimeOffset.UtcNow));

        renamed!.Name.Should().Be("Alexandra Pilot");
        renamed.Id.Should().Be(registered!.Id);
        renamed.Email.Should().Be(registered.Email);
        renamed.ClubName.Should().Be(registered.ClubName);
    }

    [Fact]
    public void ContactDetailsChanged_replaces_email_phone_and_home_city()
    {
        var registered = PeopleProjection.Apply(null, new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));
        var newContact = new ContactDetails { Email = "alexandra@example.com", Phone = "021 555 0199", HomeCity = "Wellington" };

        var changed = PeopleProjection.Apply(registered, new ContactDetailsChanged(newContact, DateTimeOffset.UtcNow));

        changed!.Email.Should().Be("alexandra@example.com");
        changed.Phone.Should().Be("021 555 0199");
        changed.HomeCity.Should().Be("Wellington");
    }

    [Fact]
    public void ClubAffiliationChanged_to_null_clears_ClubName()
    {
        var registered = PeopleProjection.Apply(null, new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var cleared = PeopleProjection.Apply(registered, new ClubAffiliationChanged(null, DateTimeOffset.UtcNow));

        cleared!.ClubName.Should().BeNull();
    }

    [Fact]
    public void ClubAffiliationChanged_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            PeopleProjection.Apply(null, new ClubAffiliationChanged(SampleClub, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ContactDetailsChanged_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            PeopleProjection.Apply(null, new ContactDetailsChanged(SampleContact, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PersonRenamed_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            PeopleProjection.Apply(null, new PersonRenamed("Alexandra Pilot", DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_full_event_stream_folds_in_order_to_the_expected_final_summary()
    {
        var id = PersonId.New();
        var newContact = SampleContact with { Phone = "021 555 0199" };

        PersonEvent[] stream =
        [
            new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow),
            new PersonRenamed("Alexandra Pilot", DateTimeOffset.UtcNow),
            new ContactDetailsChanged(newContact, DateTimeOffset.UtcNow),
            new ClubAffiliationChanged(null, DateTimeOffset.UtcNow),
        ];

        var final = stream.Aggregate((PersonSummary?)null, PeopleProjection.Apply);

        final.Should().NotBeNull();
        final!.Id.Should().Be(id);
        final.Name.Should().Be("Alexandra Pilot");
        final.Email.Should().Be(newContact.Email);
        final.Phone.Should().Be(newContact.Phone);
        final.ClubName.Should().BeNull();
    }

    // Roles — authentication-and-authorisation.md WI-6. The fold mirrors
    // Person.Apply's set semantics (ImmutableHashSet.Add/Remove): idempotent
    // under replay in both directions, with the decides refusing what the
    // fold tolerates (person.roleAlreadyHeld / person.roleNotHeld).

    [Fact]
    public void RoleGranted_adds_the_role_and_leaves_everything_else_untouched()
    {
        var registered = PeopleProjection.Apply(null, new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var granted = PeopleProjection.Apply(registered, new RoleGranted(PersonRole.Competitor, DateTimeOffset.UtcNow));

        granted!.Roles.Should().Equal(PersonRole.Competitor);
        granted.Name.Should().Be(registered!.Name);
        granted.Email.Should().Be(registered.Email);
        granted.ClubName.Should().Be(registered.ClubName);
    }

    [Fact]
    public void A_second_RoleGranted_of_an_already_held_role_does_not_duplicate_it()
    {
        var registered = PeopleProjection.Apply(null, new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));
        var granted = PeopleProjection.Apply(registered, new RoleGranted(PersonRole.Competitor, DateTimeOffset.UtcNow));

        var regranted = PeopleProjection.Apply(granted, new RoleGranted(PersonRole.Competitor, DateTimeOffset.UtcNow));

        regranted!.Roles.Should().ContainSingle().Which.Should().Be(PersonRole.Competitor);
    }

    [Fact]
    public void RoleRevoked_removes_only_that_role()
    {
        var registered = PeopleProjection.Apply(null, new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));
        var competitor = PeopleProjection.Apply(registered, new RoleGranted(PersonRole.Competitor, DateTimeOffset.UtcNow));
        var organiser = PeopleProjection.Apply(competitor, new RoleGranted(PersonRole.Organiser, DateTimeOffset.UtcNow));

        var revoked = PeopleProjection.Apply(organiser, new RoleRevoked(PersonRole.Competitor, DateTimeOffset.UtcNow));

        revoked!.Roles.Should().Equal(PersonRole.Organiser);
    }

    [Fact]
    public void RoleRevoked_of_a_role_not_held_leaves_the_summary_unchanged()
    {
        var registered = PeopleProjection.Apply(null, new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var revoked = PeopleProjection.Apply(registered, new RoleRevoked(PersonRole.Competitor, DateTimeOffset.UtcNow));

        revoked.Should().Be(registered);
        revoked!.Roles.Should().BeEmpty();
    }

    [Fact]
    public void RoleGranted_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            PeopleProjection.Apply(null, new RoleGranted(PersonRole.Organiser, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RoleRevoked_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            PeopleProjection.Apply(null, new RoleRevoked(PersonRole.Competitor, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IdentityLinked_leaves_the_summary_unchanged()
    {
        var registered = PeopleProjection.Apply(null, new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var linked = PeopleProjection.Apply(registered, new IdentityLinked("google-oauth2", "google-sub-1", DateTimeOffset.UtcNow));

        linked.Should().Be(registered);
    }

    [Fact]
    public void IdentityLinked_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            PeopleProjection.Apply(null, new IdentityLinked("google-oauth2", "google-sub-1", DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_stream_with_roles_and_an_identity_link_folds_to_the_expected_final_summary()
    {
        var id = PersonId.New();

        PersonEvent[] stream =
        [
            new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow),
            new RoleGranted(PersonRole.Competitor, DateTimeOffset.UtcNow),
            new IdentityLinked("google-oauth2", "google-sub-1", DateTimeOffset.UtcNow),
            new RoleGranted(PersonRole.Organiser, DateTimeOffset.UtcNow),
            new RoleRevoked(PersonRole.Competitor, DateTimeOffset.UtcNow),
        ];

        var final = stream.Aggregate((PersonSummary?)null, PeopleProjection.Apply);

        final.Should().NotBeNull();
        final!.Id.Should().Be(id);
        final.Name.Should().Be("Alex Pilot");
        final.Roles.Should().Equal(PersonRole.Organiser);
        final.ClubName.Should().Be("Auckland Soaring Club");
    }
}
