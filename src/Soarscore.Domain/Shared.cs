// Cross-aggregate identity and value types — docs/aggregate-roots.md,
// docs/soaring-domain-class-diagram.md §1.
//
// Every id that is referenced *across* an aggregate boundary lives here, so
// the three aggregates built from this file (Person, Competition, Entry)
// share one definition of each rather than each minting its own. LADR-0003
// "Domain primitives": `readonly record struct XId(Guid)`, `Guid.CreateVersion7()`.
//
// CompetitionClass is deliberately absent: ADR-0002 §5 makes its identity a
// content hash over the canonical serialisation, not a minted id, and
// AdoptedRules.SourceClassId already carries that hash as a plain string —
// there is no ClassDefinitionId to mint.
//
// CompetitorId and GroupId identify *entities*, not aggregate roots — both
// live inside the Competition aggregate — but Entry references them by id
// from across the boundary (aggregate-roots.md §4), which is what puts them
// here rather than in Competitions/Competition.cs.

namespace Soarscore.Domain;

public readonly record struct PersonId(Guid Value)
{
    public static PersonId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct CompetitionId(Guid Value)
{
    public static CompetitionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

/// <summary>An entity inside the Competition aggregate; referenced by id from Entry.</summary>
public readonly record struct CompetitorId(Guid Value)
{
    public static CompetitorId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

/// <summary>
/// An entity inside the Competition aggregate; referenced by id from Entry.
/// Group membership is not stored on the Group itself — it is the set of
/// Entries whose GroupRef points at it (aggregate-roots.md §3).
/// </summary>
public readonly record struct GroupId(Guid Value)
{
    public static GroupId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct EntryId(Guid Value)
{
    public static EntryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

/// <summary>
/// A recorded infraction, not a derived deduction (high-level-architecture.md,
/// "Penalties are recorded infractions only"). The same shape is held on
/// Competition, scoped to TaskRound/Competition, and on Entry, scoped to
/// Flight/Entry (aggregate-roots.md §3/§4) — PenaltyScope's four values say
/// which pairing a given instance uses, not which aggregate it lives in.
/// </summary>
public sealed record Penalty
{
    public required string InfractionType { get; init; }

    public required PenaltyScope Scope { get; init; }
}

public enum PenaltyScope { Flight, Entry, TaskRound, Competition }
