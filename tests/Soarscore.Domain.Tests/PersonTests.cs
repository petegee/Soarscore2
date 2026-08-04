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

        Assert.Equal(id, person.Id);
        Assert.Equal("Alex Pilot", person.Name);
        Assert.Equal("alex@example.com", person.Contact.Email);
        Assert.Equal("021 555 1234", person.Contact.Phone);
        Assert.Equal("Auckland", person.Contact.HomeCity);
        Assert.NotNull(person.Club);
        Assert.Equal("Auckland Soaring Club", person.Club!.ClubName);
        Assert.Equal("ASC-42", person.Club!.MembershipNumber);
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

        Assert.Null(person.Club);
    }

    [Fact]
    public void ContactDetails_optional_fields_default_to_null()
    {
        var contact = new ContactDetails { Email = "min@example.com" };

        Assert.Equal("min@example.com", contact.Email);
        Assert.Null(contact.Phone);
        Assert.Null(contact.HomeCity);
    }

    [Fact]
    public void Person_records_with_same_values_are_equal()
    {
        var id = PersonId.New();
        var contact = new ContactDetails { Email = "same@example.com" };

        var first = new Person { Id = id, Name = "Same Person", Contact = contact };
        var second = new Person { Id = id, Name = "Same Person", Contact = contact };

        Assert.Equal(first, second);
    }
}
