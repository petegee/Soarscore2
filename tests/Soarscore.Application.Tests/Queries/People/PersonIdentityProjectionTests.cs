using AwesomeAssertions;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Application.Tests.Queries.People;

public class PersonIdentityProjectionTests
{
    private static readonly PersonId Person = PersonId.New();

    private static readonly PersonRegistered Registration =
        new(Person, "Alex Pilot", new ContactDetails { Email = "pilot@example.com" }, null, DateTimeOffset.UtcNow);

    private static readonly IdentityLinked Link =
        new("google-oauth2", "google-sub-1", DateTimeOffset.UtcNow);

    [Fact]
    public void IdentityLinked_mints_the_row_for_the_stream_person()
    {
        var row = PersonIdentityProjection.Apply(null, Link, Person);

        row.Should().NotBeNull();
        row!.Provider.Should().Be("google-oauth2");
        row.Subject.Should().Be("google-sub-1");
        row.PersonId.Should().Be(Person);
    }

    [Fact]
    public void A_duplicated_IdentityLinked_folds_to_an_identical_row()
    {
        // Upsert tolerance, the PeopleProjection creation-event precedent:
        // the minting arm creates unconditionally, ignoring current, so a
        // replayed or duplicated link folds to the same row and the unique
        // compound index (WI-8) stays the arbiter of conflicting ones.
        var first = PersonIdentityProjection.Apply(null, Link, Person);

        var second = PersonIdentityProjection.Apply(first, Link, Person);

        second.Should().Be(first);
    }

    [Fact]
    public void PersonRegistered_does_not_mint_a_row()
    {
        // The stream's creation event carries no identity link, so there is
        // nothing to mint — and being a creation event it never trips the
        // change-event Require (PeopleProjection's precedent: creation events
        // mint only what their payload carries).
        var row = PersonIdentityProjection.Apply(null, Registration, Person);

        row.Should().BeNull();
    }

    [Fact]
    public void RoleGranted_against_no_current_row_throws_the_Require_error()
    {
        FluentActions.Invoking(() =>
            PersonIdentityProjection.Apply(null, new RoleGranted(PersonRole.Organiser, DateTimeOffset.UtcNow), Person))
            .Should().Throw<ArgumentException>()
            .WithMessage("RoleGranted projected with no current identity row — a change event can never be first in the stream.");
    }

    [Fact]
    public void RoleRevoked_against_no_current_row_throws_the_Require_error()
    {
        FluentActions.Invoking(() =>
            PersonIdentityProjection.Apply(null, new RoleRevoked(PersonRole.Competitor, DateTimeOffset.UtcNow), Person))
            .Should().Throw<ArgumentException>()
            .WithMessage("RoleRevoked projected with no current identity row — a change event can never be first in the stream.");
    }

    [Fact]
    public void A_non_role_change_event_against_no_current_row_throws_the_Require_error_too()
    {
        FluentActions.Invoking(() =>
            PersonIdentityProjection.Apply(null, new PersonRenamed("Alexandra Pilot", DateTimeOffset.UtcNow), Person))
            .Should().Throw<ArgumentException>()
            .WithMessage("*a change event can never be first in the stream.");
    }

    [Fact]
    public void A_change_event_against_an_existing_row_leaves_it_unchanged()
    {
        var row = PersonIdentityProjection.Apply(null, Link, Person);

        var after = PersonIdentityProjection.Apply(row, new RoleGranted(PersonRole.Organiser, DateTimeOffset.UtcNow), Person);

        after.Should().Be(row);
    }
}
