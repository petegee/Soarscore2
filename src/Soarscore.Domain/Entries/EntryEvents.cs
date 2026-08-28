// Event contracts for the Entry aggregate — docs/aggregate-roots.md §4.
//
// One competitor's record for one task-round and everything captured in it:
// an ordered list of Flights, each with raw Measurements — append-only,
// corrected by Amendments, never overwritten. Six events, directly derived
// from that shape:
//
//   EntryOpened          — the creation event. Opens one competitor's record
//     for one task-round against one Group, under one ReflightRole.
//   FlightOpened         — appends a new, initially empty Flight.
//   MeasurementCaptured  — appends a raw Measurement to a Flight.
//   MeasurementAmended   — appends an Amendment to an existing Measurement,
//     correcting it in place rather than overwriting it.
//   EntryAnnulled        — a ruling that this Entry does not count (F3F.1.5's
//     provisional re-flight is why it exists).
//   PenaltyRecorded      — a Flight/Entry-scoped Penalty. TaskRound/
//     Competition-scoped penalties belong to the Competition aggregate, not
//     here (aggregate-roots.md §4).
//
// Every payload reuses Domain's own value-object records (Measurement,
// Amendment, Annulment, Penalty) directly rather than redefining
// their shapes — same convention as ClassDefinitionEvents.cs.

using System.Text.Json.Serialization;
using Soarscore.Domain.Competitions;

namespace Soarscore.Domain.Entries;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(EntryOpened), "entryOpened")]
[JsonDerivedType(typeof(FlightOpened), "flightOpened")]
[JsonDerivedType(typeof(MeasurementCaptured), "measurementCaptured")]
[JsonDerivedType(typeof(MeasurementAmended), "measurementAmended")]
[JsonDerivedType(typeof(EntryAnnulled), "entryAnnulled")]
[JsonDerivedType(typeof(PenaltyRecorded), "penaltyRecorded")]
public abstract record EntryEvent : IDomainEvent
{
    private protected EntryEvent() { }
}

/// <summary>
/// The creation event: opens one competitor's record for one task-round.
/// <para>
/// <see cref="CountsForRoundOrdinal"/> (reflight-aggregate-destination.md D1)
/// names the round whose ladder slot the score aggregates into — a make-up
/// flight flown with a later round's group counts for the missed round while
/// still normalising within the group that hosted it. Null means the entry's
/// own round, so every ordinary entry is unchanged.
/// <see cref="Reason"/> is the entitlement basis for that counts-for round
/// (D4 — the AppendReflightGroup.reasonRequired parity), recorded for audit
/// exactly like TaskRoundAnnulled's. Both are additive and optional; the write
/// side owns their validation (WI-2).
/// </para>
/// </summary>
public sealed record EntryOpened(
    EntryId Id,
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    GroupId GroupRef,
    CompetitorId CompetitorRef,
    ReflightRole Role,
    DateTimeOffset At,
    int? CountsForRoundOrdinal = null,
    string? Reason = null) : EntryEvent;

/// <summary>Appends a new, initially empty Flight at the given sequence.</summary>
public sealed record FlightOpened(
    int Sequence,
    DateTimeOffset At) : EntryEvent;

/// <summary>Appends a raw Measurement to the Flight matching <see cref="FlightSequence"/>.</summary>
public sealed record MeasurementCaptured(
    int FlightSequence,
    Measurement Measurement) : EntryEvent;

/// <summary>
/// Appends an Amendment to the Measurement matching <see cref="Metric"/> within
/// the Flight matching <see cref="FlightSequence"/> — a correction recorded
/// alongside the original, never an overwrite of it.
/// </summary>
public sealed record MeasurementAmended(
    int FlightSequence,
    string Metric,
    Amendment Amendment) : EntryEvent;

/// <summary>A ruling that this Entry does not count.</summary>
public sealed record EntryAnnulled(Annulment Annulment) : EntryEvent;

/// <summary>A Flight/Entry-scoped Penalty.</summary>
public sealed record PenaltyRecorded(Penalty Penalty) : EntryEvent;
