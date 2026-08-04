// Shared-kernel value types — docs/aggregate-roots.md,
// docs/soaring-domain-class-diagram.md §1.
//
// Everything that identifies one aggregate root (PersonId, CompetitionId,
// EntryId) or one of its own entities (CompetitorId, GroupId — both inside
// Competition) now lives beside that aggregate's own type, not here — see
// People/Person.cs, Competitions/Competition.cs, Entries/Entry.cs. What's
// left is Penalty/PenaltyScope: a value shape embedded, unchanged, by TWO
// aggregates (Competition, scoped to TaskRound/Competition, and Entry, scoped
// to Flight/Entry — aggregate-roots.md §3/§4), so it belongs to neither one
// exclusively. LADR-0003 "Domain primitives" still governs the id shape
// wherever an id is minted: `readonly record struct XId(Guid)`,
// `Guid.CreateVersion7()`.

namespace Soarscore.Domain;

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
