using AwesomeAssertions;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Domain.Tests;

public class PersonDecideTests
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

    private static Person Registered() =>
        Person.Create(new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

    [Fact]
    public void Register_with_valid_input_succeeds_with_the_expected_event()
    {
        var id = PersonId.New();
        var at = DateTimeOffset.UtcNow;

        var result = Person.Register(id, "Alex Pilot", SampleContact, SampleClub, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, at));
    }

    [Fact]
    public void Register_with_no_club_affiliation_succeeds()
    {
        var id = PersonId.New();
        var at = DateTimeOffset.UtcNow;

        var result = Person.Register(id, "Alex Pilot", SampleContact, null, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Club.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_with_a_blank_name_fails_with_a_stable_code(string blankName)
    {
        var result = Person.Register(PersonId.New(), blankName, SampleContact, null, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.name.blank");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    [InlineData("no-at-sign.example.com")]
    [InlineData("no-dot-in-domain@example")]
    [InlineData("has whitespace@example.com")]
    public void Register_with_an_implausible_email_fails_with_a_stable_code(string badEmail)
    {
        var result = Person.Register(PersonId.New(), "Alex Pilot", SampleContact with { Email = badEmail }, null, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.email.invalid");
    }

    [Fact]
    public void Register_with_a_null_contact_fails_with_a_stable_code()
    {
        // Reference-type non-null parameters are only a compile-time hint — a
        // client that omits "contact" from the JSON body binds one straight
        // through to null, so the domain must reject it rather than NRE.
        var result = Person.Register(PersonId.New(), "Alex Pilot", null!, null, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.contact.missing");
    }

    [Fact]
    public void Rename_with_a_valid_name_succeeds_with_the_expected_event()
    {
        var person = Registered();
        var at = DateTimeOffset.UtcNow;

        var result = person.Rename("Alexandra Pilot", at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new PersonRenamed("Alexandra Pilot", at));
    }

    [Fact]
    public void Rename_with_a_blank_name_fails_with_a_stable_code()
    {
        var result = Registered().Rename("   ", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.name.blank");
    }

    [Fact]
    public void ChangeContactDetails_with_a_plausible_email_succeeds_with_the_expected_event()
    {
        var person = Registered();
        var newContact = SampleContact with { Phone = "021 555 0199" };
        var at = DateTimeOffset.UtcNow;

        var result = person.ChangeContactDetails(newContact, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new ContactDetailsChanged(newContact, at));
    }

    [Fact]
    public void ChangeContactDetails_with_an_implausible_email_fails_with_a_stable_code()
    {
        var result = Registered().ChangeContactDetails(SampleContact with { Email = "not-an-email" }, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.email.invalid");
    }

    [Fact]
    public void ChangeContactDetails_with_a_null_contact_fails_with_a_stable_code()
    {
        var result = Registered().ChangeContactDetails(null!, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("person.contact.missing");
    }

    [Fact]
    public void ChangeClubAffiliation_to_a_new_club_succeeds_with_the_expected_event()
    {
        var person = Registered();
        var newClub = new ClubAffiliation { ClubName = "Wellington Soaring Club" };
        var at = DateTimeOffset.UtcNow;

        var result = person.ChangeClubAffiliation(newClub, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new ClubAffiliationChanged(newClub, at));
    }

    [Fact]
    public void ChangeClubAffiliation_to_null_clears_the_club_and_still_succeeds()
    {
        var person = Registered();
        var at = DateTimeOffset.UtcNow;

        var result = person.ChangeClubAffiliation(null, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Club.Should().BeNull();
    }
}
