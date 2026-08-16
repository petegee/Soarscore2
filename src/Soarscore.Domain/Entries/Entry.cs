// The Entry aggregate — docs/aggregate-roots.md §4, cross-checked against
// docs/soaring-domain-class-diagram.md §1.
//
// One competitor's working-time window and everything captured in it: an
// ordered list of Flights, each with raw Measurements (append-only, corrected
// by Amendments, never overwritten). Isolating this as its own aggregate is
// what keeps concurrent scorer writes — the live capture path — from
// contending with the rest of the Competition.
//
// CompetitionRef, GroupRef and CompetitorRef reach across the aggregate
// boundary into the Competition aggregate by id only (CompetitionId, GroupId,
// CompetitorId — Shared.cs, Competition.cs); nothing here holds a direct
// reference to a Competition, a Group or a Competitor. PhaseOrdinal,
// RoundOrdinal and TaskRoundOrdinal are the same ordinal-addressing idiom
// Competition uses internally to reach a task-round, carried here so the
// write path never has to scan the Competition to find its own task.

using System.Collections.Immutable;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;

namespace Soarscore.Domain.Entries;

public readonly record struct EntryId(Guid Value)
{
    public static EntryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

/// <summary>
/// The working-time window a competitor flies within. Not expanded in either
/// diagram — abbreviated away the same as several of ClassDefinition's own
/// value objects. Flight times captured within the owning Entry are NOT
/// checked against this window at capture. `F3K.7` is explicit that a launch
/// before the working time begins is scored zero, not refused — and it makes
/// the working time a scoring input, not a capture gate, in the other
/// direction too: the flight runs "until a landing … or the working time
/// expires". Encoding a reject-outside-window check in
/// <see cref="Entry.OpenFlight"/> would put a scoring rule into the core
/// system (CLAUDE.md's core architectural law); the class model already owns
/// this as data, via `TaskDefinition.FlightValidWhen`
/// (kanban/completed/capture-a-score-steel-thread-plan.md, finding 3).
/// </summary>
public sealed record TimeWindow
{
    public required DateTimeOffset Start { get; init; }

    /// <summary>
    /// Null under WorkingTimeKind.UntilAllFlightsComplete: the working time
    /// is not a class datum at all, the round ends when the last flight does
    /// (ScoringVocabulary.cs, TaskTiming.WorkingTime). Absence is the only
    /// truthful encoding — the same rule absent Normalisation and absent
    /// GroupConstraint follow.
    /// </summary>
    public DateTimeOffset? End { get; init; }
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
/// (kanban/completed/scoring-service-plan.md WI-9).
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

    /// <summary>The Competition this Entry belongs to.</summary>
    public required CompetitionId CompetitionRef { get; init; }

    /// <summary>The Phase.Ordinal of the task-round this Entry was opened against.</summary>
    public required int PhaseOrdinal { get; init; }

    /// <summary>The Round.Ordinal of the task-round this Entry was opened against.</summary>
    public required int RoundOrdinal { get; init; }

    /// <summary>The TaskRound.Ordinal this Entry was opened against.</summary>
    public required int TaskRoundOrdinal { get; init; }

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
        CompetitionRef = @event.CompetitionRef,
        PhaseOrdinal = @event.PhaseOrdinal,
        RoundOrdinal = @event.RoundOrdinal,
        TaskRoundOrdinal = @event.TaskRoundOrdinal,
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

    // Instance decide function — WI-3 (kanban/completed/capture-a-score-steel-thread-plan.md).
    // maxLaunches arrives already resolved from the task's MaxLaunches, not
    // read from a ClassDefinition here — the same reasoning finding 1 applies
    // to the coordinate keeps Entry free of any dependency on the class
    // definition's task shape; the handler does that resolution. Null means
    // the task limits launches not at all (half the corpus).
    //
    // Nothing is checked about launchAt — see TimeWindow's doc comment above
    // and finding 3: F3K.7 scores an early launch rather than refusing it, so
    // gating OpenFlight on the working-time window would put a scoring rule
    // into the core system. A mistyped launch time cannot be corrected in
    // this slice; FlightOpened has no amendment event (deferred, not missed).
    public Result<FlightOpened> OpenFlight(int sequence, DateTimeOffset launchAt, int? maxLaunches, DateTimeOffset at)
    {
        if (Annulment is not null)
        {
            return Result<FlightOpened>.Failure(
                "entry.annulled", "This Entry has been annulled and cannot record further flights.");
        }

        if (sequence != Flights.Length + 1)
        {
            return Result<FlightOpened>.Failure(
                "openFlight.sequenceOutOfOrder",
                $"Flight sequence must be {Flights.Length + 1}; got {sequence}.");
        }

        if (maxLaunches is { } max && Flights.Length >= max)
        {
            return Result<FlightOpened>.Failure(
                "openFlight.maxLaunchesExceeded", $"This task allows at most {max} launches.");
        }

        return Result<FlightOpened>.Success(new FlightOpened(sequence, launchAt, at));
    }

    // Instance decide function — WI-4 (kanban/completed/capture-a-score-steel-thread-plan.md).
    // metrics arrives already resolved from the task's declared
    // MetricDefinitions, for the same reason maxLaunches does above: Entry
    // never learns which class it is flying under.
    public Result<MeasurementCaptured> CaptureMeasurement(
        int flightSequence,
        string metric,
        MeasuredValue value,
        DateTimeOffset capturedAt,
        ImmutableArray<MetricDefinition> metrics)
    {
        if (Annulment is not null)
        {
            return Result<MeasurementCaptured>.Failure(
                "entry.annulled", "This Entry has been annulled and cannot record further measurements.");
        }

        var flight = Flights.FirstOrDefault(f => f.Sequence == flightSequence);
        if (flight is null)
        {
            return Result<MeasurementCaptured>.Failure(
                "captureMeasurement.flightNotFound", $"No flight with sequence {flightSequence} has been opened.");
        }

        var metricDefinition = metrics.FirstOrDefault(m => m.Name == metric);
        if (metricDefinition is null)
        {
            return Result<MeasurementCaptured>.Failure(
                "captureMeasurement.metricNotDeclared", $"'{metric}' is not a metric declared by this task.");
        }

        if (value.Kind != metricDefinition.Kind)
        {
            return Result<MeasurementCaptured>.Failure(
                "captureMeasurement.kindMismatch",
                $"'{metric}' is a {metricDefinition.Kind} metric; the captured value is a {value.Kind}.");
        }

        // A second value for the same metric is a correction, MeasurementAmended's
        // job — which does not exist yet (out of scope, see the plan's Scope
        // section). This is what makes the aggregate's append-only promise
        // enforceable rather than aspirational.
        if (flight.Measurements.Any(m => m.Metric == metric))
        {
            return Result<MeasurementCaptured>.Failure(
                "captureMeasurement.alreadyCaptured",
                $"'{metric}' was already captured for flight {flightSequence}. " +
                "Correcting a captured value is MeasurementAmended's job, not a second capture.");
        }

        // Round per the metric's declared precision (finding 4) — the stored
        // value IS the raw observation, not a derivation from it. A Flag-kind
        // metric has nothing to round and Precision is null there by
        // construction.
        var storedValue = metricDefinition.Precision is { } precision && value.Number is { } number
            ? value with { Number = RoundingSupport.ApplyRounding(number, precision) }
            : value;

        var measurement = new Measurement
        {
            Metric = metric,
            Value = storedValue,
            CapturedAt = capturedAt,
        };

        return Result<MeasurementCaptured>.Success(new MeasurementCaptured(flightSequence, measurement));
    }

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
