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

        Assert.NotNull(person);
        Assert.Equal(id, person.Id);
        Assert.Equal("Alex Pilot", person.Name);
        Assert.Equal(SampleContact, person.Contact);
        Assert.Equal(SampleClub, person.Club);
    }

    [Fact]
    public void ContactDetailsChanged_replaces_contact_and_leaves_everything_else_untouched()
    {
        var id = PersonId.New();
        var registered = Person.Create(new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));
        var newContact = SampleContact with { Phone = "021 555 0199" };

        var changed = registered.Apply(new ContactDetailsChanged(newContact, DateTimeOffset.UtcNow));

        Assert.Equal(newContact, changed.Contact);
        Assert.Equal(registered.Id, changed.Id);
        Assert.Equal(registered.Name, changed.Name);
        Assert.Equal(registered.Club, changed.Club);
    }

    [Fact]
    public void ClubAffiliationChanged_can_clear_the_club_by_folding_null()
    {
        var id = PersonId.New();
        var registered = Person.Create(new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var cleared = registered.Apply(new ClubAffiliationChanged(null, DateTimeOffset.UtcNow));

        Assert.Null(cleared.Club);
    }

    [Fact]
    public void PersonRenamed_replaces_the_name()
    {
        var id = PersonId.New();
        var registered = Person.Create(new PersonRegistered(id, "Alex Pilot", SampleContact, SampleClub, DateTimeOffset.UtcNow));

        var renamed = registered.Apply(new PersonRenamed("Alexandra Pilot", DateTimeOffset.UtcNow));

        Assert.Equal("Alexandra Pilot", renamed.Name);
    }

    [Fact]
    public void ContactDetailsChanged_against_no_current_projection_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Person.Apply(null, new ContactDetailsChanged(SampleContact, DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void ClubAffiliationChanged_against_no_current_projection_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Person.Apply(null, new ClubAffiliationChanged(SampleClub, DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void PersonRenamed_against_no_current_projection_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Person.Apply(null, new PersonRenamed("Alexandra Pilot", DateTimeOffset.UtcNow)));
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

        Assert.NotNull(final);
        Assert.Equal(id, final!.Id);
        Assert.Equal("Alexandra Pilot", final.Name);
        Assert.Equal(newContact, final.Contact);
        Assert.Null(final.Club);
    }
}
