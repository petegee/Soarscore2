using System.Text.Json;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Application.Tests;

public class PersonEventJsonTests
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
    public void Events_round_trip_through_SoarscoreEventJson_byte_for_byte()
    {
        PersonEvent registered = new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(registered, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<PersonEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        Assert.Equal(json, reemitted);
        Assert.IsType<PersonRegistered>(reread);
    }

    [Fact]
    public void Registered_event_serialises_with_the_kind_discriminator()
    {
        PersonEvent registered = new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(registered, SoarscoreEventJson.Options);

        Assert.Contains("\"$kind\":\"personRegistered\"", json);
    }
}
