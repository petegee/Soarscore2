// The Person aggregate — docs/aggregate-roots.md §2, cross-checked against
// docs/soaring-domain-class-diagram.md §1.
//
// Anyone known to the system: identity, contact details, club affiliation.
// Deliberately free of contest data — a Competition's Competitor records
// reference this by PersonId and own nothing here in return (aggregate-roots.md
// §3). Registering is a one-off event that lets an organiser build a
// competition's field from known people.

namespace Soarscore.Domain.People;

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
/// </summary>
public sealed record Person
{
    public required PersonId Id { get; init; }

    public required string Name { get; init; }

    public required ContactDetails Contact { get; init; }

    public ClubAffiliation? Club { get; init; }
}
