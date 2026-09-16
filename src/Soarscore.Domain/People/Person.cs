// The Person aggregate — docs/aggregate-roots.md §2, cross-checked against
// docs/soaring-domain-class-diagram.md §1.
//
// Anyone known to the system: identity, contact details, club affiliation.
// Deliberately free of contest data — a Competition's Competitor records
// reference this by PersonId and own nothing here in return (aggregate-roots.md
// §3). Registering is a one-off event that lets an organiser build a
// competition's field from known people.

using System.Collections.Immutable;

namespace Soarscore.Domain.People;

/// <summary>
/// <see cref="IParsable{TSelf}"/> so ASP.NET's Minimal API parameter binding
/// (WI-8, <c>[AsParameters]</c> query records) can bind this straight from a
/// query-string value — no Api-layer converter needed.
/// </summary>
public readonly record struct PersonId(Guid Value) : IParsable<PersonId>
{
    public static PersonId New() => new(Guid.CreateVersion7());

    public static PersonId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s, provider));

    public static bool TryParse(string? s, IFormatProvider? provider, out PersonId result)
    {
        if (Guid.TryParse(s, provider, out var value))
        {
            result = new PersonId(value);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Email is unique system-wide (aggregate-roots.md §2, LADR-0001 §2/§3), but
/// that constraint is enforced at the repository / unique-index level — an
/// infrastructure concern, not something this value object can check for
/// itself against the rest of the population.
/// </summary>
public sealed record ContactDetails
{
    public required string Email { get; init; }

    public string? Phone { get; init; }

    public string? HomeCity { get; init; }
}

public sealed record ClubAffiliation
{
    public required string ClubName { get; init; }

    public string? MembershipNumber { get; init; }
}

/// <summary>
/// One external identity-provider account bound to exactly one Person —
/// glossary "Identity link" (approved 2026-09-15). A Person may hold several
/// (several social accounts, or a social account plus a magic-link email); a
/// link belongs to exactly one Person. As with the email uniqueness remark
/// below, the one-link-one-Person arbiter across the population is the
/// store's unique index, not anything this value object can check.
/// </summary>
public sealed record IdentityLink(string Provider, string Subject);

/// <summary>
/// The aggregate root. Referenced by id from Competitor records inside each
/// Competition (aggregate-roots.md §3) — nothing downstream of registration
/// lives here, except the competition-independent system-level authority and
/// account data WI-3 added: Roles and identity links (glossary "Role",
/// "Identity link"), which say who may act system-wide, never what they did
/// in a contest.
///
/// Person is never conceptually deleted (docs/aggregate-roots.md §2), so
/// unlike ClassDefinitionRetired's tolerance of a null current projection,
/// every <c>Apply</c> overload below other than <see cref="Create"/> requires
/// a non-null current instance to fold onto — a change event can never be
/// first in the stream.
/// </summary>
public sealed record Person
{
    public required PersonId Id { get; init; }

    public required string Name { get; init; }

    public required ContactDetails Contact { get; init; }

    public ClubAffiliation? Club { get; init; }

    public ImmutableHashSet<PersonRole> Roles { get; init; } = ImmutableHashSet<PersonRole>.Empty;

    public ImmutableArray<IdentityLink> Identities { get; init; } = ImmutableArray<IdentityLink>.Empty;

    /// <summary>The creation event. Every stream begins with exactly one of these.</summary>
    public static Person Create(PersonRegistered @event) => new()
    {
        Id = @event.Id,
        Name = @event.Name,
        Contact = @event.Contact,
        Club = @event.Club,
    };

    // One overload per non-creation event — both the domain's own fold-by-type
    // API *and* exactly what Marten's conventional-method discovery on a
    // self-aggregating snapshot type matches on (docs/plans/domain-fold-refactor-plan.md,
    // WI-0 finding).
    public Person Apply(ContactDetailsChanged @event) => this with { Contact = @event.Contact };

    public Person Apply(ClubAffiliationChanged @event) => this with { Club = @event.Club };

    public Person Apply(PersonRenamed @event) => this with { Name = @event.Name };

    public Person Apply(RoleGranted @event) => this with { Roles = Roles.Add(@event.Role) };

    public Person Apply(RoleRevoked @event) => this with { Roles = Roles.Remove(@event.Role) };

    // Fold tolerance for a duplicated event (WI-3): an IdentityLinked replay
    // folds to no change instead of a second, identical link.
    public Person Apply(IdentityLinked @event)
    {
        var link = new IdentityLink(@event.Provider, @event.Subject);
        return Identities.Contains(link) ? this : this with { Identities = Identities.Add(link) };
    }

    /// <summary>
    /// Generic replay entry point for code that only has the closed union type,
    /// not the concrete event type (e.g. folding a whole stream). Not what
    /// Marten calls — Marten calls the typed overloads above via its own
    /// conventional-method discovery.
    /// </summary>
    public static Person? Apply(Person? current, PersonEvent @event) =>
        @event switch
        {
            PersonRegistered registered => Create(registered),
            ContactDetailsChanged e => Require(current, e).Apply(e),
            ClubAffiliationChanged e => Require(current, e).Apply(e),
            PersonRenamed e => Require(current, e).Apply(e),
            RoleGranted e => Require(current, e).Apply(e),
            RoleRevoked e => Require(current, e).Apply(e),
            IdentityLinked e => Require(current, e).Apply(e),
            _ => throw new ArgumentException($"Unknown PersonEvent subtype: {@event.GetType().Name}"),
        };

    private static Person Require(Person? current, PersonEvent @event) =>
        current ?? throw new ArgumentException($"{@event.GetType().Name} folded with no current projection — a change event can never be first in the stream.");

    // Decide functions — WI-4 (kanban/completed/command-side-steel-thread-plan.md).
    // They return the event to append; they never mutate this instance and
    // never append it themselves (that is the handler's job, WI-6). Email
    // uniqueness is deliberately not checked here — see the ContactDetails
    // remark above; it is the sole reason the unique index in the Inline
    // projection exists.

    public static Result<PersonRegistered> Register(
        PersonId id, string name, ContactDetails contact, ClubAffiliation? club, DateTimeOffset at)
    {
        var defect = ValidateName(name) ?? ValidateContact(contact);
        return defect is not null
            ? Result<PersonRegistered>.Failure(defect.Code, defect.Message)
            : Result<PersonRegistered>.Success(new PersonRegistered(id, name, contact, club, at));
    }

    public Result<PersonRenamed> Rename(string name, DateTimeOffset at)
    {
        var defect = ValidateName(name);
        return defect is not null
            ? Result<PersonRenamed>.Failure(defect.Code, defect.Message)
            : Result<PersonRenamed>.Success(new PersonRenamed(name, at));
    }

    public Result<ContactDetailsChanged> ChangeContactDetails(ContactDetails contact, DateTimeOffset at)
    {
        var defect = ValidateContact(contact);
        return defect is not null
            ? Result<ContactDetailsChanged>.Failure(defect.Code, defect.Message)
            : Result<ContactDetailsChanged>.Success(new ContactDetailsChanged(contact, at));
    }

    public Result<ClubAffiliationChanged> ChangeClubAffiliation(ClubAffiliation? club, DateTimeOffset at) =>
        Result<ClubAffiliationChanged>.Success(new ClubAffiliationChanged(club, at));

    // Roles and identity links — WI-3 (kanban/in-progress/authentication-and-authorisation.md).
    // Same single-event-or-failure discipline as every decide here. Like email
    // uniqueness, (Provider, Subject) uniqueness is only checkable against the
    // rest of the population — the store's unique index stays the arbiter
    // (IdentityLink remark above); this decide can only refuse a link this
    // person already holds.
    public Result<RoleGranted> GrantRole(PersonRole role, DateTimeOffset at) =>
        Roles.Contains(role)
            ? Result<RoleGranted>.Failure("person.roleAlreadyHeld", $"The {role} role is already held.")
            : Result<RoleGranted>.Success(new RoleGranted(role, at));

    public Result<RoleRevoked> RevokeRole(PersonRole role, DateTimeOffset at) =>
        Roles.Contains(role)
            ? Result<RoleRevoked>.Success(new RoleRevoked(role, at))
            : Result<RoleRevoked>.Failure("person.roleNotHeld", $"The {role} role is not held.");

    public Result<IdentityLinked> LinkIdentity(string provider, string subject, DateTimeOffset at)
    {
        var defect = ValidateIdentityLink(provider, subject);
        return defect is not null
            ? Result<IdentityLinked>.Failure(defect.Code, defect.Message)
            : Identities.Contains(new IdentityLink(provider, subject))
                ? Result<IdentityLinked>.Failure(
                    "person.identityAlreadyLinked", "This identity is already linked to this person.")
                : Result<IdentityLinked>.Success(new IdentityLinked(provider, subject, at));
    }

    private static Defect? ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? new Defect("person.name.blank", "$.name", "Name must not be blank.")
            : null;

    // Accepts null despite the non-nullable parameter type on Register/ChangeContactDetails:
    // nullable reference annotations are compile-time only, and a client that omits "contact"
    // from the request JSON binds one straight through to null here.
    private static Defect? ValidateContact(ContactDetails? contact) =>
        contact is null
            ? new Defect("person.contact.missing", "$.contact", "Contact details are required.")
            : IsPlausibleEmail(contact.Email)
                ? null
                : new Defect("person.email.invalid", "$.contact.email", "Email must be non-blank and structurally plausible.");

    // Accepts null despite the non-nullable parameter types — ValidateContact's
    // reasoning: a client that omits "provider"/"subject" from the request
    // JSON binds one straight through to null here.
    private static Defect? ValidateIdentityLink(string? provider, string? subject) =>
        string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(subject)
            ? new Defect("person.identity.blank", "$.identity", "Provider and subject must not be blank.")
            : null;

    /// <summary>
    /// Not full RFC 5322 validation — one '@', a non-blank local part, a
    /// domain part containing at least one '.', and no whitespace anywhere.
    /// Deliberately loose: the only thing this aggregate can check about an
    /// email is its shape, not whether it is real (that is what registration
    /// itself is for).
    /// </summary>
    private static bool IsPlausibleEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var parts = email.Split('@');
        return parts.Length == 2
            && parts[0].Length > 0
            && parts[1].Contains('.')
            && !parts[1].StartsWith('.')
            && !parts[1].EndsWith('.');
    }
}
