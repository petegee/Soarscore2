// The Person aggregate — docs/aggregate-roots.md §2, cross-checked against
// docs/soaring-domain-class-diagram.md §1.
//
// Anyone known to the system: identity, contact details, club affiliation.
// Deliberately free of contest data — a Competition's Competitor records
// reference this by PersonId and own nothing here in return (aggregate-roots.md
// §3). Registering is a one-off event that lets an organiser build a
// competition's field from known people.

namespace Soarscore.Domain.People;

public readonly record struct PersonId(Guid Value)
{
    public static PersonId New() => new(Guid.CreateVersion7());

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
/// The aggregate root. Referenced by id from Competitor records inside each
/// Competition (aggregate-roots.md §3) — nothing downstream of registration
/// lives here.
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
            _ => throw new ArgumentException($"Unknown PersonEvent subtype: {@event.GetType().Name}"),
        };

    private static Person Require(Person? current, PersonEvent @event) =>
        current ?? throw new ArgumentException($"{@event.GetType().Name} folded with no current projection — a change event can never be first in the stream.");
}
