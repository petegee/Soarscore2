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
using Soarscore.Domain.Competitions;

namespace Soarscore.Domain.Entries;

public readonly record struct EntryId(Guid Value)
{
    public static EntryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

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

    /// <summary>The creation event. Every stream begins with exactly one of these.</summary>
    public static Entry Create(EntryOpened @event) => new()
    {
        Id = @event.Id,
        WorkingTime = @event.WorkingTime,
        GroupRef = @event.GroupRef,
        CompetitorRef = @event.CompetitorRef,
        Role = @event.Role,
        Annulment = null,
        Flights = [],
        Penalties = [],
    };

    /// <summary>Appends a new, initially empty Flight at the event's sequence.</summary>
    public Entry Apply(FlightOpened @event)
    {
        var flight = new Flight
        {
            Sequence = @event.Sequence,
            LaunchAt = @event.LaunchAt,
            Measurements = [],
        };

        return this with { Flights = Flights.Add(flight) };
    }

    /// <summary>Appends a raw Measurement to the Flight matching <see cref="MeasurementCaptured.FlightSequence"/>.</summary>
    public Entry Apply(MeasurementCaptured @event) =>
        this with
        {
            Flights = ReplaceFlight(
                Flights,
                @event.FlightSequence,
                flight => flight with { Measurements = flight.Measurements.Add(@event.Measurement) }),
        };

    /// <summary>Appends an Amendment to the Measurement matching <see cref="MeasurementAmended.Metric"/> within the matching Flight.</summary>
    public Entry Apply(MeasurementAmended @event) =>
        this with
        {
            Flights = ReplaceFlight(
                Flights,
                @event.FlightSequence,
                flight => flight with
                {
                    Measurements = ReplaceMeasurement(
                        flight.Measurements,
                        @event.Metric,
                        measurement => measurement with { Amendments = measurement.Amendments.Add(@event.Amendment) }),
                }),
        };

    /// <summary>Records a ruling that this Entry does not count.</summary>
    public Entry Apply(EntryAnnulled @event) => this with { Annulment = @event.Annulment };

    /// <summary>Appends a Flight/Entry-scoped Penalty.</summary>
    public Entry Apply(PenaltyRecorded @event) => this with { Penalties = Penalties.Add(@event.Penalty) };

    /// <summary>Finds the Flight matching <paramref name="sequence"/> and replaces it via <paramref name="update"/>.</summary>
    private static ImmutableArray<Flight> ReplaceFlight(
        ImmutableArray<Flight> flights,
        int sequence,
        Func<Flight, Flight> update) =>
        flights.Select(flight => flight.Sequence == sequence ? update(flight) : flight).ToImmutableArray();

    /// <summary>Finds the Measurement matching <paramref name="metric"/> and replaces it via <paramref name="update"/>.</summary>
    private static ImmutableArray<Measurement> ReplaceMeasurement(
        ImmutableArray<Measurement> measurements,
        string metric,
        Func<Measurement, Measurement> update) =>
        measurements.Select(measurement => measurement.Metric == metric ? update(measurement) : measurement).ToImmutableArray();

    /// <summary>
    /// Generic replay entry point — same signature <c>EntryProjection.Apply</c>
    /// had, so <c>stream.Events.Aggregate(...)</c>-style callers barely change.
    /// Not what Marten calls (Marten calls the typed overloads above via its own
    /// conventional-method discovery); this is for code that only has the closed
    /// union type, not the concrete event type.
    /// </summary>
    public static Entry? Apply(Entry? current, EntryEvent @event) =>
        @event switch
        {
            EntryOpened opened => Create(opened),
            FlightOpened e => Require(current, e).Apply(e),
            MeasurementCaptured e => Require(current, e).Apply(e),
            MeasurementAmended e => Require(current, e).Apply(e),
            EntryAnnulled e => Require(current, e).Apply(e),
            PenaltyRecorded e => Require(current, e).Apply(e),
            _ => throw new ArgumentException($"Unknown EntryEvent subtype: {@event.GetType().Name}"),
        };

    private static Entry Require(Entry? current, EntryEvent @event) =>
        current ?? throw new ArgumentException(
            $"{@event.GetType().Name} folded with no current Entry — every stream must begin with EntryOpened.");
}
