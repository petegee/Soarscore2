// Event contracts for the Person aggregate — docs/aggregate-roots.md §2.
//
// Person is anyone known to the system: identity, contact details, and club
// affiliation. Registering happens once and mints the stream; everything
// after that is a mutation of one of the fields Person owns: contact details,
// club affiliation, name — and, since authentication-and-authorisation.md
// WI-3, the roles and identity links that carry system-level authority (never
// contest data). Unlike
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
[JsonDerivedType(typeof(RoleGranted), "roleGranted")]
[JsonDerivedType(typeof(RoleRevoked), "roleRevoked")]
[JsonDerivedType(typeof(IdentityLinked), "identityLinked")]
public abstract record PersonEvent : IDomainEvent
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

// Roles and identity links — WI-3 (kanban/in-progress/authentication-and-authorisation.md).
// Stream-scoped change events like PersonRenamed — no Id. Role changes are
// organiser actions; IdentityLinked is appended at first sign-in or by an
// organiser binding an external account (D5/D12).
public sealed record RoleGranted(
    PersonRole Role,
    DateTimeOffset At) : PersonEvent;

public sealed record RoleRevoked(
    PersonRole Role,
    DateTimeOffset At) : PersonEvent;

/// <summary>
/// One external identity-provider account: the (Provider, Subject) pair the
/// IdP guarantees. Unique across people is the store's unique index's job,
/// never this aggregate's.
/// </summary>
public sealed record IdentityLinked(
    string Provider,
    string Subject,
    DateTimeOffset At) : PersonEvent;

/// <summary>
/// System-level authority held by a Person, independent of any competition —
/// glossary "Role" (approved 2026-09-15). For v1, Competitor and Organiser
/// only: the Contest Director's officiating authority folds into Organiser.
/// Plain enum, MinEnforcement precedent. Who may enter a given score is never
/// answered here — that is per-competition capture policy (D7/D10).
/// </summary>
public enum PersonRole { Competitor, Organiser }
