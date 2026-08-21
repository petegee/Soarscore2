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

using Soarscore.Domain.Competitions;

namespace Soarscore.Domain;

/// <summary>
/// Marker implemented by the four event-union bases (PersonEvent,
/// CompetitionEvent, EntryEvent, ClassDefinitionEvent) so that
/// <c>IEventStore</c> (Soarscore.Application, WI-3) has a typed signature
/// instead of <see cref="object"/>. No members: it exists only to close the
/// port's generic surface over "one of our event types", and touches none of
/// the `[JsonPolymorphic]`/`$kind` machinery those four bases already carry.
/// </summary>
public interface IDomainEvent;

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

    /// <summary>
    /// Who recorded the infraction, when the client supplies it — optional,
    /// never required (clients are not forced to collect it; recorded, never
    /// enforced on anyone's role). Mirrors <see cref="Entries.Annulment.By"/> /
    /// <see cref="Entries.Amendment.By"/> precedent.
    /// </summary>
    public string? By { get; init; }

    /// <summary>
    /// The competitor the penalty is against — meaningful only at
    /// TaskRound/Competition scope (the Entry aggregate's own penalties are
    /// already scoped to the Entry that holds them). Enforced by the decide
    /// functions: <c>RecordPenalty</c> on Competition requires it, on Entry
    /// forbids it.
    /// </summary>
    public CompetitorId? CompetitorRef { get; init; }

    /// <summary>
    /// The reporting coordinate the rules ask to list the deduction against
    /// (F3B.1.7.b: "listed on the score sheet of the round in which the
    /// penalisation was applied"). Recorded when supplied, validated for
    /// existence, and read by nothing yet — no score-sheet report exists.
    /// </summary>
    public TaskRoundCoordinate? TaskRound { get; init; }
}

public enum PenaltyScope { Flight, Entry, TaskRound, Competition }

/// <summary>
/// The same ordinal triple <see cref="Entries.EntryOpened"/> already carries,
/// addressing a task-round without a reference into the Competition aggregate.
/// A value object, not a domain concept (soaring-domain-glossary.md names
/// nothing for it); PhaseOrdinal is 0-based at draw time, RoundOrdinal and
/// TaskRoundOrdinal 1-based, matching <see cref="Entries.Entry"/>'s own fields.
/// </summary>
public sealed record TaskRoundCoordinate(int PhaseOrdinal, int RoundOrdinal, int TaskRoundOrdinal);
