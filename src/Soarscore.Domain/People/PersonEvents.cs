// Event contracts for the Person aggregate — docs/aggregate-roots.md §2.
//
// Person is anyone known to the system: identity, contact details, and club
// affiliation. Registering happens once and mints the stream; everything
// after that is a mutation of one of the three fields Person owns. Unlike
// CompetitionClass, Person has a minted id (PersonId, Domain/Shared.cs) and is
// never conceptually deleted — there is no retirement event, and every event
// after PersonRegistered requires a non-null current projection to fold onto
// (Person.cs).

using System.Text.Json.Serialization;

namespace Soarscore.Domain.People;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(PersonRegistered), "personRegistered")]
[JsonDerivedType(typeof(ContactDetailsChanged), "contactDetailsChanged")]
[JsonDerivedType(typeof(ClubAffiliationChanged), "clubAffiliationChanged")]
[JsonDerivedType(typeof(PersonRenamed), "personRenamed")]
public abstract record PersonEvent
{
    private protected PersonEvent() { }
}

/// <summary>The creation event — mints the stream at <see cref="Id"/>.</summary>
public sealed record PersonRegistered(
    PersonId Id,
    string Name,
    ContactDetails Contact,
    ClubAffiliation? Club,
    DateTimeOffset At) : PersonEvent;

public sealed record ContactDetailsChanged(
    ContactDetails Contact,
    DateTimeOffset At) : PersonEvent;

/// <summary>Nullable so a club affiliation can be cleared, not just changed.</summary>
public sealed record ClubAffiliationChanged(
    ClubAffiliation? Club,
    DateTimeOffset At) : PersonEvent;

public sealed record PersonRenamed(
    string Name,
    DateTimeOffset At) : PersonEvent;
