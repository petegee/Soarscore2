// The Entry aggregate — docs/aggregate-roots.md §4, cross-checked against
// docs/soaring-domain-class-diagram.md §1.
//
// One competitor's working-time window and everything captured in it: an
// ordered list of Flights, each with raw Measurements (append-only, corrected
// by Amendments, never overwritten). Isolating this as its own aggregate is
// what keeps concurrent scorer writes — the live capture path — from
// contending with the rest of the Competition.
//
// GroupRef and CompetitorRef reach across the aggregate boundary into the
// Competition aggregate by id only (GroupId, CompetitorId — Shared.cs);
// nothing here holds a direct reference to a Group or a Competitor.

using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.Domain.Entries;

/// <summary>
/// The working-time window a competitor flies within. Not expanded in either
/// diagram — abbreviated away the same as several of ClassDefinition's own
/// value objects. Flight times captured within the owning Entry cannot exceed
/// this window; that invariant is enforced at capture, not here
/// (high-level-architecture.md, "Core-owned invariants").
/// </summary>
public sealed record TimeWindow
{
    public required DateTimeOffset Start { get; init; }

    public required DateTimeOffset End { get; init; }
}

/// <summary>
/// A ruling that this attempt does not count, with the reason, who ruled and
/// when — exactly as an Amendment carries the same three facts about a
/// measurement. `F3F.1.5`'s provisional re-flight is why it exists: the
/// competitor re-flies under protest and the jury afterwards decides which of
/// the two attempts stands, which no `ReflightSelection` value can express
/// because it states one rule for the whole class. An annulled Entry has no
/// result and is skipped at `select flights`.
/// </summary>
public sealed record Annulment
{
    public required string Reason { get; init; }

    public required string By { get; init; }

    public required DateTimeOffset At { get; init; }
}

/// <summary>
/// A correction to a Measurement's value, recorded rather than overwriting it.
/// </summary>
public sealed record Amendment
{
    public required MeasuredValue NewValue { get; init; }

    public required string Reason { get; init; }

    public required string By { get; init; }

    public required DateTimeOffset At { get; init; }
}

/// <summary>
/// One raw observation — a number or a flag, since the rules require plain
/// observations (landed in the defined area, score card signed) alongside
/// quantities (MeasuredValue, CompetitionClasses/ScoringVocabulary.cs).
/// Append-only and corrected by Amendments, never overwritten: there is no
/// setter on <see cref="Value"/>, and resolving the effective value from a
/// Measurement's Amendments is ScoringService's job, not this type's
/// (docs/plans/scoring-service-plan.md WI-9).
/// </summary>
public sealed record Measurement
{
    /// <summary>Matched against MetricDefinition.Name / ScoreTerm.MetricRef in the adopted class.</summary>
    public required string Metric { get; init; }

    public required MeasuredValue Value { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }

    public ImmutableArray<Amendment> Amendments { get; init; } = [];
}

/// <summary>
/// One attempt within an Entry's working time.
/// </summary>
public sealed record Flight
{
    /// <summary>
    /// The `flight.sequence` intrinsic the scoring pipeline reads directly —
    /// see FlightInterpreter, which builds a metric dictionary entry of that
    /// name from this property rather than from any captured Measurement.
    /// </summary>
    public required int Sequence { get; init; }

    public required DateTimeOffset LaunchAt { get; init; }

    public required ImmutableArray<Measurement> Measurements { get; init; }
}

/// <summary>
/// Which of the two reflight rules an Entry is scored under. Within a single
/// reflight group the entitled competitor's new attempt is official even if
/// worse (`Entitled`), while every other pilot flying it — the fillers drawn
/// in to make up the group — takes the better of their two attempts
/// (`Filler`). `Original` is the ordinary case: no reflight involved. One
/// event, two rules, discriminated by role rather than by class
/// (soaring-domain-class-diagram.md, "Reflight scoring is per role, not per
/// class").
/// </summary>
public enum ReflightRole { Original, Entitled, Filler }

/// <summary>
/// The aggregate root: one competitor's live flying record for one working-time
/// window. <see cref="CompetitorRef"/> identifies the Competitor registration
/// inside the Competition aggregate — the record that carries the competitor
/// number scorers name/id captures with, and the link back to the Person.
/// Referencing an internal entity of another aggregate by id is legal here
/// (same precedent as <see cref="GroupRef"/>) because Entry only ever holds the
/// id; any mutation of a Competitor still goes through the Competition root.
/// </summary>
public sealed record Entry
{
    public required EntryId Id { get; init; }

    public required TimeWindow WorkingTime { get; init; }

    /// <summary>The Group this Entry belongs to, inside the Competition aggregate.</summary>
    public required GroupId GroupRef { get; init; }

    /// <summary>The Competitor registration this Entry was flown by, inside the Competition aggregate.</summary>
    public required CompetitorId CompetitorRef { get; init; }

    public required ReflightRole Role { get; init; }

    /// <summary>
    /// A ruling that this Entry does not count. An Entry annulled by ruling has
    /// no result (high-level-architecture.md, "Core-owned invariants").
    /// </summary>
    public Annulment? Annulment { get; init; }

    public required ImmutableArray<Flight> Flights { get; init; }

    /// <summary>Flight/Entry-scoped penalties; TaskRound/Competition-scoped ones live on Competition instead.</summary>
    public ImmutableArray<Penalty> Penalties { get; init; } = [];
}
