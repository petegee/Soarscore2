using AwesomeAssertions;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Domain.Tests;

public class PersonTests
{
    [Fact]
    public void Person_with_club_affiliation_round_trips_all_values()
    {
        var id = PersonId.New();
        var person = new Person
        {
            Id = id,
            Name = "Alex Pilot",
            Contact = new ContactDetails
            {
                Email = "alex@example.com",
                Phone = "021 555 1234",
                HomeCity = "Auckland",
            },
            Club = new ClubAffiliation
            {
                ClubName = "Auckland Soaring Club",
                MembershipNumber = "ASC-42",
            },
        };

        person.Id.Should().Be(id);
        person.Name.Should().Be("Alex Pilot");
        person.Contact.Email.Should().Be("alex@example.com");
        person.Contact.Phone.Should().Be("021 555 1234");
        person.Contact.HomeCity.Should().Be("Auckland");
        person.Club.Should().NotBeNull();
        person.Club!.ClubName.Should().Be("Auckland Soaring Club");
        person.Club!.MembershipNumber.Should().Be("ASC-42");
    }

    [Fact]
    public void Person_without_club_affiliation_has_null_club()
    {
        var person = new Person
        {
            Id = PersonId.New(),
            Name = "Jordan Novice",
            Contact = new ContactDetails { Email = "jordan@example.com" },
        };

        person.Club.Should().BeNull();
    }

    [Fact]
    public void ContactDetails_optional_fields_default_to_null()
    {
        var contact = new ContactDetails { Email = "min@example.com" };

        contact.Email.Should().Be("min@example.com");
        contact.Phone.Should().BeNull();
        contact.HomeCity.Should().BeNull();
    }

    [Fact]
    public void Person_records_with_same_values_are_equal()
    {
        var id = PersonId.New();
        var contact = new ContactDetails { Email = "same@example.com" };

        var first = new Person { Id = id, Name = "Same Person", Contact = contact };
        var second = new Person { Id = id, Name = "Same Person", Contact = contact };

        second.Should().Be(first);
    }
}
