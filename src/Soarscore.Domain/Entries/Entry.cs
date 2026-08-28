// The Entry aggregate — docs/aggregate-roots.md §4, cross-checked against
// docs/soaring-domain-class-diagram.md §1.
//
// One competitor's record for one task-round and everything captured in it:
// an ordered list of Flights, each with raw Measurements (append-only,
// corrected by Amendments, never overwritten). Isolating this as its own
// aggregate is what keeps concurrent scorer writes — the live capture path —
// from contending with the rest of the Competition.
//
// Flight times are NOT checked against any working time at capture: `F3K.7`
// is explicit that a launch before the working time begins is scored zero,
// not refused, and the flight runs "until a landing … or the working time
// expires" — the working time is a scoring input, not a capture gate. The
// class model owns launch-timing rules as data via
// `TaskDefinition.FlightValidWhen` (CLAUDE.md's core architectural law). The
// system no longer stores a window at all (remove-stored-working-time.md), so
// there is nothing for capture to check against — the strongest form of the
// same argument (kanban/completed/remove-flight-launchat.md).
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
/// One attempt within the task-round's working time. Carries no launch timestamp:
/// see <see cref="Entry.OpenFlight"/> for why the rules never want one.
/// </summary>
public sealed record Flight
{
    /// <summary>
    /// The `flight.sequence` intrinsic the scoring pipeline reads directly —
    /// see FlightInterpreter, which builds a metric dictionary entry of that
    /// name from this property rather than from any captured Measurement.
    /// </summary>
    public required int Sequence { get; init; }

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
/// The aggregate root: one competitor's live flying record for one task-round. <see cref="CompetitorRef"/> identifies the Competitor registration
/// inside the Competition aggregate — the record that carries the competitor
/// number scorers name/id captures with, and the link back to the Person.
/// Referencing an internal entity of another aggregate by id is legal here
/// (same precedent as <see cref="GroupRef"/>) because Entry only ever holds the
/// id; any mutation of a Competitor still goes through the Competition root.
/// </summary>
public sealed record Entry
{
    public required EntryId Id { get; init; }

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
    /// The round whose ladder slot this Entry's score aggregates into
    /// (reflight-aggregate-destination.md D1) — GS's OriginalRoundNo analogue.
    /// Null means the entry's own round, so ordinary scoring is untouched.
    /// Normalisation is unaffected either way: the score is always computed
    /// within the group that hosted the flight.
    /// </summary>
    public int? CountsForRoundOrdinal { get; init; }

    /// <summary>
    /// The entitlement basis recorded when the entry was opened — required
    /// exactly when <see cref="CountsForRoundOrdinal"/> is set (D4), audit-only
    /// like <see cref="Annulment"/>'s reason.
    /// </summary>
    public string? Reason { get; init; }

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
        CompetitionRef = @event.CompetitionRef,
        PhaseOrdinal = @event.PhaseOrdinal,
        RoundOrdinal = @event.RoundOrdinal,
        TaskRoundOrdinal = @event.TaskRoundOrdinal,
        GroupRef = @event.GroupRef,
        CompetitorRef = @event.CompetitorRef,
        Role = @event.Role,
        CountsForRoundOrdinal = @event.CountsForRoundOrdinal,
        Reason = @event.Reason,
        Annulment = null,
        Flights = [],
        Penalties = [],
    };

    /// <summary>Inserts a new, initially empty Flight at the event's sequence.</summary>
    public Entry Apply(FlightOpened @event)
    {
        var flight = new Flight
        {
            Sequence = @event.Sequence,
            Measurements = [],
        };

        // Flights is always ascending by Sequence — insertion in place, not
        // append — which is what makes positional flight selection
        // (FlightSelector.SelectLast etc.) mean launch position under any
        // capture order. A contiguous log always inserts at the end, which is
        // byte-identical to what append did.
        var index = 0;
        while (index < Flights.Length && Flights[index].Sequence < @event.Sequence)
        {
            index++;
        }

        return this with { Flights = Flights.Insert(index, flight) };
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
    // sequence is the stable launch label — "which launch this was" on the
    // field, chosen by whoever records the flight (out-of-order-flight-entry.md
    // decision 1). It is scoring-relevant data (the `flight.sequence`
    // intrinsic feeds lookup terms and launch-penalty rules) but a label, not
    // a claim about when it was typed: flights may be opened in any order, so
    // gaps are legal (a gap means "not entered yet") while duplicates and
    // non-positive values are not (decision 2). No rule in either rulebook
    // requires flights to be entered in launch order; order-sensitive scoring
    // reads launch chronology from this label, which the fold preserves by
    // keeping Flights ascending (Apply(FlightOpened)).
    //
    // A Flight carries no launch timestamp — kanban/completed/remove-flight-launchat.md.
    // It used to, unchecked, so that F3K.7's "an early launch scores zero, it is
    // not refused" stayed a scoring rule rather than a capture gate. The rule
    // check that story ran found the timestamp was never the right carrier of
    // that fact: no rule in either rulebook wants a launch instant, and the
    // classes that care about launch timing declare a metric instead —
    // `launchedInWorkingTime` (F3K.7), `launchedOnSignal` (F3K.11.3),
    // `launchedWithin30s` (F3F). Those are Measurements, captured and amended
    // like any other, which is what keeps the rule in the class model where
    // CLAUDE.md's core architectural law puts it.
    public Result<FlightOpened> OpenFlight(int sequence, int? maxLaunches, DateTimeOffset at)
    {
        if (Annulment is not null)
        {
            return Result<FlightOpened>.Failure(
                "entry.annulled", "This Entry has been annulled and cannot record further flights.");
        }

        if (sequence < 1)
        {
            return Result<FlightOpened>.Failure(
                "openFlight.sequenceNotPositive",
                $"Flight sequence must be a positive launch number; got {sequence}.");
        }

        if (Flights.Any(f => f.Sequence == sequence))
        {
            return Result<FlightOpened>.Failure(
                "openFlight.duplicateSequence", $"Launch {sequence} has already been opened.");
        }

        if (maxLaunches is { } max && Flights.Length >= max)
        {
            return Result<FlightOpened>.Failure(
                "openFlight.maxLaunchesExceeded", $"This task allows at most {max} launches.");
        }

        return Result<FlightOpened>.Success(new FlightOpened(sequence, at));
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

        // A second value for the same metric is a correction, which
        // AmendMeasurement (below) emits as a MeasurementAmended rather than a
        // second capture. This is what makes the aggregate's append-only
        // promise enforceable rather than aspirational: a first value is a
        // capture, every subsequent one is an amendment, and neither can
        // impersonate the other.
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

    // Instance decide function — WI-1 (kanban/completed/amend-a-measurement.md).
    // The correcting counterpart to CaptureMeasurement above: a metric already
    // captured on a
    // flight is amended (appends a MeasurementAmended) rather than captured
    // again. metrics arrives already resolved from the task's declared
    // MetricDefinitions, exactly as it does for capture — Entry never learns
    // which class it is flying under. Reason and By are validated here, not in
    // the handler, following Competition.AnnulTaskRound's ReasonGiven and
    // Competition.Finalise's byRequired rather than BindParameter's
    // handler-side By: an amendment's justification is a substantive record of
    // a correction, not an audit breadcrumb (amend-a-measurement.md, decision
    // 1). At comes from IClock in the handler; the write path enforces nobody's
    // role — the corrector may be anyone, recorded rather than refused.
    public Result<MeasurementAmended> AmendMeasurement(
        int flightSequence,
        string metric,
        MeasuredValue newValue,
        string reason,
        string by,
        DateTimeOffset at,
        ImmutableArray<MetricDefinition> metrics)
    {
        if (Annulment is not null)
        {
            return Result<MeasurementAmended>.Failure(
                "entry.annulled", "This Entry has been annulled and cannot record further measurements.");
        }

        var flight = Flights.FirstOrDefault(f => f.Sequence == flightSequence);
        if (flight is null)
        {
            return Result<MeasurementAmended>.Failure(
                "amendMeasurement.flightNotFound", $"No flight with sequence {flightSequence} has been opened.");
        }

        var measurement = flight.Measurements.FirstOrDefault(m => m.Metric == metric);
        if (measurement is null)
        {
            return Result<MeasurementAmended>.Failure(
                "amendMeasurement.notCaptured",
                $"'{metric}' has not been captured for flight {flightSequence}; there is nothing to amend. " +
                "A first value is a capture, not an amendment.");
        }

        var metricDefinition = metrics.FirstOrDefault(m => m.Name == metric);
        if (metricDefinition is null)
        {
            return Result<MeasurementAmended>.Failure(
                "amendMeasurement.metricNotDeclared", $"'{metric}' is not a metric declared by this task.");
        }

        if (newValue.Kind != metricDefinition.Kind)
        {
            return Result<MeasurementAmended>.Failure(
                "amendMeasurement.kindMismatch",
                $"'{metric}' is a {metricDefinition.Kind} metric; the amended value is a {newValue.Kind}.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<MeasurementAmended>.Failure(
                "amendMeasurement.reasonRequired",
                "A reason is required — it is the recorded justification for the correction, not an audit breadcrumb.");
        }

        if (string.IsNullOrWhiteSpace(by))
        {
            return Result<MeasurementAmended>.Failure(
                "amendMeasurement.byRequired", "By is required — a self-declared corrector's name, not an authorisation claim.");
        }

        // Round the corrected value per the metric's declared precision,
        // identical to CaptureMeasurement's stored-value rule (finding 5): the
        // stored value IS the raw observation, so a correction carries no
        // precision a capture cannot.
        var correctedValue = metricDefinition.Precision is { } precision && newValue.Number is { } number
            ? newValue with { Number = RoundingSupport.ApplyRounding(number, precision) }
            : newValue;

        var amendment = new Amendment
        {
            NewValue = correctedValue,
            Reason = reason,
            By = by,
            At = at,
        };

        return Result<MeasurementAmended>.Success(new MeasurementAmended(flightSequence, metric, amendment));
    }

    // Instance decide function — WI-2 (kanban/in-progress/annul-and-penalise-the-second-entry-thread.md).
    // A ruling, not an infraction with a modelled cost: there is nothing to
    // validate against the class definition, and deliberately no gate on
    // task-round state (an annulment is a ruling *about* recorded data, and the
    // ordinary case is a protest after the round looked finished — NFR-4's
    // world). Re-annulment is allowed: the fold overwrites, which is the right
    // semantics for a jury revising a ruling (P2 holds it true). Reason and By
    // are validated here, not in the handler, exactly as AmendMeasurement does:
    // a ruling's justification is a substantive record, not an audit breadcrumb.
    public Result<EntryAnnulled> AnnulEntry(string reason, string by, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<EntryAnnulled>.Failure(
                "annulEntry.reasonRequired",
                "A reason is required — it is the recorded ruling, not an audit breadcrumb.");
        }

        if (string.IsNullOrWhiteSpace(by))
        {
            return Result<EntryAnnulled>.Failure(
                "annulEntry.byRequired", "By is required — a self-declared jury name, not an authorisation claim.");
        }

        return Result<EntryAnnulled>.Success(new EntryAnnulled(new Annulment
        {
            Reason = reason,
            By = by,
            At = at,
        }));
    }

    // Instance decide function — WI-3 (kanban/in-progress/annul-and-penalise-the-second-entry-thread.md).
    // penaltyDefinitions arrives already resolved from the Competition's
    // AdoptedRules.Definition.Penalties — Entry never learns which class it is
    // flying under, exactly as CaptureMeasurement's metrics parameter. The
    // write path rejects an infraction type the class does not declare
    // (recordPenalty.infractionTypeNotDeclared) rather than letting
    // PenaltyEngine silently skip it at read time — the CD believes they
    // penalised someone and nothing happens. The engine's read-side tolerance
    // stays: it is the safety net for events already in a log.
    public Result<PenaltyRecorded> RecordPenalty(
        Penalty penalty, ImmutableArray<PenaltyDefinition> penaltyDefinitions)
    {
        if (Annulment is not null)
        {
            return Result<PenaltyRecorded>.Failure(
                "entry.annulled", "This Entry has been annulled and cannot record a penalty.");
        }

        if (penalty.Scope is not (PenaltyScope.Flight or PenaltyScope.Entry))
        {
            return Result<PenaltyRecorded>.Failure(
                "recordPenalty.wrongScope",
                $"A penalty recorded against an Entry must be Flight or Entry scoped; got {penalty.Scope}.");
        }

        if (penalty.CompetitorRef is not null || penalty.TaskRound is not null)
        {
            return Result<PenaltyRecorded>.Failure(
                "recordPenalty.subjectNotAllowed",
                "The Entry is its own subject and coordinate; an Entry-scoped penalty must not carry a CompetitorRef or a TaskRound.");
        }

        if (!penaltyDefinitions.Any(d => d.InfractionType == penalty.InfractionType))
        {
            return Result<PenaltyRecorded>.Failure(
                "recordPenalty.infractionTypeNotDeclared",
                $"'{penalty.InfractionType}' is not an infraction type declared by the adopted class definition.");
        }

        if (penalty.By is not null && string.IsNullOrWhiteSpace(penalty.By))
        {
            return Result<PenaltyRecorded>.Failure(
                "recordPenalty.byBlank",
                "By, when supplied, must not be blank — an absent By is fine, a blank one is a typo.");
        }

        return Result<PenaltyRecorded>.Success(new PenaltyRecorded(penalty));
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
