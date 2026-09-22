using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;
using Predicate = Soarscore.SeedData.Predicate;
using ScoreTerm = Soarscore.SeedData.ScoreTerm;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Example-based tests for the metric absence semantics —
/// kanban/in-progress/metric-absence-semantics.md WI-5. Home to the
/// per-pipeline-stage half of invariant 3 (Pending-then-filled): (a) resolution
/// pends the flight naming what it awaits, (b) selection excludes pending
/// flights and yields NoResult-with-AwaitingCapture when nothing else is left,
/// (c) normalisation gives a pending entry the identical arithmetic to a
/// NoResult while the group normalises over what is present, and (d) the
/// awaited capture arriving and rescoring fills the flight in, diagnostics
/// gone. Also home to WI-5 item 4's tolerance-boundary unit tests: missing
/// MEASUREMENTS are tolerated, missing PARAMETER bindings and undeclared
/// referenced metrics (tier 3) are not.
///
/// WI-5 of kanban/in-progress/add-recorded-predicate.md adds the recordedness
/// half: the IsRecorded-referenced carve-out (a blank gate metric is the
/// gate's false — the flight zeroes per 5.5.11.7 e, it never pends), pinned
/// here at the orchestrator (the pending-vs-zeroed distinction only exists
/// where FlightMetricResolution.InterpretAllFlights applies the tier table)
/// over the F5J-NDC-shaped RecordednessFixtures. Its three named CsCheck
/// invariants live in IsRecordedPredicatePropertyTests.cs.
///
/// The three named CsCheck invariants (Transparency, Explicit-wins, and the
/// Pending-then-filled sweep) live in MetricAbsenceSemanticsPropertyTests.cs.
///
/// WI-2's adoption kind-mismatch refusals are already
/// ClassDefinitionValidationTests's Check22 cases
/// (tests/Soarscore.Application.Tests) and WI-1's store round-trips are
/// ClassDefinitionEventStoreTests's whenNotRecorded case
/// (tests/Soarscore.Infrastructure.Tests) — verified, deliberately not
/// duplicated here.
/// </summary>
public class MetricAbsenceSemanticsTests
{
    // ============================ invariant 3, stage (a): resolution

    /// <summary>
    /// Pending-then-filled, stage (a) — resolution
    /// (kanban/in-progress/metric-absence-semantics.md#wi-5, invariant 3):
    /// a flight missing an unassumed declared referenced metric interprets
    /// <see cref="FlightResultState.Pending"/> — never an error — with a
    /// <see cref="PendingFlightDiagnostic"/> naming the flight sequence and the
    /// FIRST absent metric in declared order, while the same pass inserts the
    /// flight's assumed-declared metrics so reporting sees exactly what the
    /// flight carries. A pending flight scores 0 with no term contributions.
    /// </summary>
    [Fact]
    public void Resolution_a_flight_missing_an_unassumed_referenced_metric_interprets_pending_naming_the_first_absent_metric()
    {
        var task = MetricAbsenceFixtures.SyntheticTask;
        var resolvedTask = ParameterResolver.ResolveTask(task, MetricAbsenceFixtures.NoBindings, []);

        // Declared metric order: flightTime, landingDistance, landedOut, overflySeconds.
        // Flight 3 captures flightTime only — still missing landingDistance.
        var entry = MetricAbsenceFixtures.Capture(
            MetricAbsenceFixtures.OpenEntry(flightCount: 3), 3, "flightTime", MeasuredValue.Of(100m));

        var interpreted = FlightMetricResolution.InterpretAllFlights(entry, resolvedTask);

        // Flight 1 captures nothing: flightTime is the first absent metric in
        // declared order, and the flight's assumptions resolve around it.
        var flight1 = interpreted[0];
        flight1.Result.State.Should().Be(FlightResultState.Pending);
        flight1.Result.Awaited.Should().Be(new PendingFlightDiagnostic(1, "flightTime"));
        flight1.Score.Should().Be(0m);
        flight1.TermContributions.Should().BeEmpty();

        // Tier-1 insertion is visible on the pending flight's resolved
        // measurements; the awaited metric itself is not there.
        flight1.Result.Measurements.Metrics["landedOut"].Should().Be(MeasuredValue.Of(false));
        flight1.Result.Measurements.Metrics["overflySeconds"].Should().Be(MeasuredValue.Of(10m));
        flight1.Result.Measurements.Metrics.ContainsKey("flightTime").Should().BeFalse();

        // Declared order decides the awaited metric, not dictionary order:
        // flightTime present, landingDistance absent → landingDistance awaited.
        var flight3 = interpreted[2];
        flight3.Result.State.Should().Be(FlightResultState.Pending);
        flight3.Result.Awaited.Should().Be(new PendingFlightDiagnostic(3, "landingDistance"));

        // Positive control: a flight missing nothing interprets normally.
        var complete = FlightMetricResolution.InterpretAllFlights(
            MetricAbsenceFixtures.CompleteEntry(), resolvedTask);
        complete.Should().ContainSingle().Which.Result.State.Should().Be(FlightResultState.Valid);
    }

    // ============================ invariant 3, stage (b): selection

    /// <summary>
    /// Pending-then-filled, stage (b) — selection: pending flights are not
    /// selectable. An entry whose every flight is pending yields
    /// <see cref="TaskResultState.NoResult"/> carrying
    /// <see cref="TaskResult.AwaitingCapture"/> — arithmetically the
    /// flight-less precedent, distinguishable by its diagnostics from a genuine
    /// no-result, which carries none.
    /// </summary>
    [Fact]
    public void Selection_an_entry_whose_every_flight_is_pending_yields_no_result_awaiting_capture()
    {
        var task = MetricAbsenceFixtures.SyntheticTask with { Flights = new AllFlights() };
        var classDef = MetricAbsenceFixtures.SyntheticClass(task);

        var allPending = MetricAbsenceFixtures.EntryPendingOnFlightTime();
        var group = MetricAbsenceFixtures.Score(task, classDef, allPending);
        var row = group.Results[MetricAbsenceFixtures.RowKey(0)];

        row.State.Should().Be(TaskResultState.NoResult);
        row.RawScore.Should().Be(0m);
        row.Selection.Should().BeNull();
        row.AwaitingCapture.Should().Equal(new PendingFlightDiagnostic(1, "flightTime"));

        // The flight-less precedent it mirrors: a genuine no-result, no diagnostics.
        var flightless = MetricAbsenceFixtures.OpenEntry(flightCount: 0);
        var flightlessGroup = MetricAbsenceFixtures.Score(task, classDef, flightless);
        var flightlessRow = flightlessGroup.Results[MetricAbsenceFixtures.RowKey(0)];

        flightlessRow.State.Should().Be(TaskResultState.NoResult);
        flightlessRow.RawScore.Should().Be(0m);
        flightlessRow.AwaitingCapture.IsDefaultOrEmpty.Should().BeTrue();
    }

    /// <summary>
    /// Pending-then-filled, stage (b) — selection, the partial case: a
    /// partially-pending entry scores from its complete flights ("score what it
    /// can") with the pending flights' awaited-metric diagnostics carried on
    /// the otherwise-Valid result.
    /// </summary>
    [Fact]
    public void Selection_a_partially_pending_entry_scores_from_its_complete_flights_with_diagnostics_on_the_result()
    {
        var task = MetricAbsenceFixtures.SyntheticTask with { Flights = new AllFlights() };
        var classDef = MetricAbsenceFixtures.SyntheticClass(task);

        // Flight 1 complete: 100 flight time + 50 landing table = 150.
        // Flight 2 missing flightTime → pending, contributes nothing.
        var entry = MetricAbsenceFixtures.Capture(
            MetricAbsenceFixtures.WithCompleteFlight(MetricAbsenceFixtures.OpenEntry(flightCount: 2), 1),
            2, "landingDistance", MeasuredValue.Of(3m));

        var row = MetricAbsenceFixtures.Score(task, classDef, entry)
            .Results[MetricAbsenceFixtures.RowKey(0)];

        row.State.Should().Be(TaskResultState.Valid);
        row.RawScore.Should().Be(150m);
        row.Selection.Should().NotBeNull();
        row.Selection!.Flights.Should().ContainSingle().Which.Score.Should().Be(150m);
        row.AwaitingCapture.Should().Equal(new PendingFlightDiagnostic(2, "flightTime"));
    }

    // ============================ invariant 3, stage (c): normalisation

    /// <summary>
    /// Pending-then-filled, stage (c) — normalisation: a pending entry
    /// contributes nothing — the identical arithmetic path to a genuine
    /// NoResult (raw 0 in, zeroed by normalisation, excluded from
    /// winner-finding) — and the group normalises over what is present.
    /// </summary>
    [Fact]
    public void Normalisation_a_pending_entry_contributes_nothing_and_the_group_normalises_over_what_is_present()
    {
        var task = MetricAbsenceFixtures.SyntheticTask with
        {
            Flights = new AllFlights(),
            Normalise = new Normalisation
            {
                Direction = NormalisationDirection.HigherIsBetter,
                WinnerScore = 1000,
            },
        };
        var classDef = MetricAbsenceFixtures.SyntheticClass(task);

        var complete = MetricAbsenceFixtures.CompleteEntry();                       // raw 150
        var allPending = MetricAbsenceFixtures.EntryPendingOnFlightTime();
        var flightless = MetricAbsenceFixtures.OpenEntry(flightCount: 0);

        var group = MetricAbsenceFixtures.Score(task, classDef, complete, allPending, flightless);

        var completeRow = group.Results[MetricAbsenceFixtures.RowKey(0)];
        var pendingRow = group.Results[MetricAbsenceFixtures.RowKey(1)];
        var flightlessRow = group.Results[MetricAbsenceFixtures.RowKey(2)];

        // The group normalises over what is present: the one complete entry is
        // the winner and takes the winner score.
        group.ValidCount.Should().Be(1);
        group.WinnerRef.Should().Be(MetricAbsenceFixtures.RowKey(0));
        completeRow.State.Should().Be(TaskResultState.Valid);
        completeRow.RawScore.Should().Be(1000m);

        // The pending row is the NoResult arithmetic, exactly: raw 0 entering
        // normalisation, 0 out, never the winner — the ONLY difference from the
        // genuine no-result is the awaited-capture diagnostics.
        pendingRow.State.Should().Be(TaskResultState.NoResult);
        pendingRow.RawScore.Should().Be(0m);
        pendingRow.AwaitingCapture.Should().Equal(new PendingFlightDiagnostic(1, "flightTime"));

        flightlessRow.State.Should().Be(TaskResultState.NoResult);
        flightlessRow.RawScore.Should().Be(0m);
        flightlessRow.AwaitingCapture.IsDefaultOrEmpty.Should().BeTrue();

        group.PreNormalisationScores[MetricAbsenceFixtures.RowKey(1)]
            .Should().Be(group.PreNormalisationScores[MetricAbsenceFixtures.RowKey(2)])
            .And.Be(0m);
    }

    // ============================ invariant 3, stage (d): fill-in

    /// <summary>
    /// Pending-then-filled, stage (d) — the awaited metric arriving (captured
    /// via the Entry aggregate's decide function, the capture-a-score
    /// steel-thread path) and rescoring yields the full result with the
    /// diagnostics gone. The filled score also shows tier-1 acting on the
    /// still-absent overflySeconds: its declared assumption (10 s) applies, so
    /// the derived −30 overfly penalty lands — 120 + 50 − 30 = 140.
    /// </summary>
    [Fact]
    public void Rescoring_the_arriving_capture_fills_the_pending_flight_and_clears_the_diagnostics()
    {
        var task = MetricAbsenceFixtures.SyntheticTask;
        var classDef = MetricAbsenceFixtures.SyntheticClass(task);

        var entry = MetricAbsenceFixtures.EntryPendingOnFlightTime();
        var before = MetricAbsenceFixtures.Score(task, classDef, entry);
        before.Results[MetricAbsenceFixtures.RowKey(0)].State.Should().Be(TaskResultState.NoResult);
        before.Results[MetricAbsenceFixtures.RowKey(0)].AwaitingCapture
            .Should().Equal(new PendingFlightDiagnostic(1, "flightTime"));

        var captured = entry.CaptureMeasurement(
            1, "flightTime", MeasuredValue.Of(120m), MetricAbsenceFixtures.Now, task.Metrics);
        captured.IsSuccess.Should().BeTrue();
        var filled = entry.Apply(captured.Value);

        var after = MetricAbsenceFixtures.Score(task, classDef, filled);
        var row = after.Results[MetricAbsenceFixtures.RowKey(0)];

        row.State.Should().Be(TaskResultState.Valid);
        row.RawScore.Should().Be(140m);   // 120 flight time + 50 landing table − 30 assumed-overfly penalty
        row.AwaitingCapture.IsDefaultOrEmpty.Should().BeTrue();
    }

    // ============================================ WI-5 item 4: tolerance boundary

    /// <summary>
    /// The tolerance boundary (kanban/in-progress/metric-absence-semantics.md
    /// WI-5 item 4, the owner's general rule): missing MEASUREMENTS are
    /// tolerated — the flight pends and scoring succeeds — while missing
    /// PARAMETER bindings are not. An unbound no-default parameter is still
    /// refused loudly at scoring time through the pre-existing refusal shape
    /// (<see cref="UnresolvedParameterException"/>, unchanged), even with every
    /// measurement present: parameters are class configuration, not
    /// observations.
    /// </summary>
    [Fact]
    public void Missing_measurements_are_tolerated_while_missing_parameter_bindings_stay_critical()
    {
        // Measurement half: flightTime never captured → the flight pends, no throw.
        var task = MetricAbsenceFixtures.SyntheticTask;
        var classDef = MetricAbsenceFixtures.SyntheticClass(task);

        var tolerated = MetricAbsenceFixtures.Score(
            task, classDef, MetricAbsenceFixtures.EntryPendingOnFlightTime());
        var pendingRow = tolerated.Results[MetricAbsenceFixtures.RowKey(0)];

        pendingRow.State.Should().Be(TaskResultState.NoResult);
        pendingRow.AwaitingCapture.Should().ContainSingle().Which.AwaitedMetric.Should().Be("flightTime");

        // Parameter half: the identical pipeline, but the rate term's cap is an
        // unbound no-default parameter — refused loudly, even with a fully
        // captured entry.
        var parameterised = MetricAbsenceFixtures.SyntheticTask with
        {
            Score =
            [
                ScoreTerm.Rate("flightTime", 1, cap: NumberOrParam.Param("cap")),
                ScoreTerm.Lookup("landingDistance", Rows.UpTo(5, 50).Then(10, 25).Rest(0)),
                ScoreTerm.When(Predicate.GreaterThan("overflySeconds", 0), ScoreTerm.Constant(-30)),
            ],
        };
        var parameterClass = MetricAbsenceFixtures.SyntheticClass(parameterised) with
        {
            Parameters = [Params.Number("cap")],   // declared, no default
        };

        FluentActions.Invoking(() => MetricAbsenceFixtures.Score(
                parameterised, parameterClass, MetricAbsenceFixtures.CompleteEntry()))
            .Should().Throw<UnresolvedParameterException>()
            .Which.ParameterName.Should().Be("cap");
    }

    /// <summary>
    /// The tier-3 boundary (kanban/in-progress/metric-absence-semantics.md WI-5
    /// item 4): a referenced-but-UNDECLARED metric still throws loudly at
    /// resolution — the <see cref="FlightMetricResolution"/> ArgumentException
    /// path. Adoption check 1 (ClassDefinitionValidation.CheckMetricReferencesResolve)
    /// refuses this shape for every adopted definition, so the guard is
    /// unreachable through the write path; it exists so definition integrity can
    /// never degrade into a silently-wrong score. Absence is the trigger: the
    /// same undeclared metric PRESENT in a flight's measurements — an event
    /// already in a log, since the capture write path refuses new ones — is
    /// scored, not thrown.
    /// </summary>
    [Fact]
    public void A_referenced_but_undeclared_metric_still_throws_loudly_at_resolution()
    {
        var task = MetricAbsenceFixtures.SyntheticTask with { Score = [ScoreTerm.Rate("bogus", 1)] };
        var classDef = MetricAbsenceFixtures.SyntheticClass(task);

        FluentActions.Invoking(() => MetricAbsenceFixtures.Score(
                task, classDef, MetricAbsenceFixtures.CompleteEntry()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*bogus*");

        // Absence-triggered, not undeclared-triggered: with the metric's value
        // present there is no capture gap to interpret, and the flight scores.
        var withBogus = MetricAbsenceFixtures.CompleteEntry().Apply(new MeasurementCaptured(
            1, new Measurement { Metric = "bogus", Value = MeasuredValue.Of(50m), CapturedAt = MetricAbsenceFixtures.Now }));

        var group = MetricAbsenceFixtures.Score(task, classDef, withBogus);
        group.Results[MetricAbsenceFixtures.RowKey(0)].RawScore.Should().Be(50m);
    }

    // ============ WI-5 of kanban/in-progress/add-recorded-predicate.md: the recordedness carve-out ============
    // The pending-vs-zeroed distinction only exists at the orchestrator
    // (FlightMetricResolution.InterpretAllFlights applies the tier table);
    // FlightInterpreter alone cannot tell a capture gap from a gate. Shapes
    // live in RecordednessFixtures (IsRecordedPredicatePropertyTests.cs); the
    // three named CsCheck invariants are in IsRecordedPredicatePropertyTests.cs.

    [Fact]
    public void Recordedness_an_absent_IsRecorded_referenced_metric_zeroes_the_flight_and_never_pends()
    {
        // 5.5.11.7 e (carried by NZ.0.3 c): with everything but the start
        // height recorded, the height's absence is the gate's false — the
        // flight is cancelled and recorded as a zero: State Valid, score 0, no
        // term contributions, NO awaited diagnostic. The read-side distinction
        // follows: a Valid-at-zero row with nothing to await — blank ⇒ zero,
        // typed ⇒ valid, one input.
        var task = RecordednessFixtures.Task;
        var entry = RecordednessCapturedFlight();

        var flight = FlightMetricResolution.InterpretAllFlights(entry, RecordednessFixtures.Resolved()).Single();
        flight.Result.State.Should().Be(FlightResultState.Valid, "the blank height is the gate's false, not a capture gap");
        flight.Result.Awaited.Should().BeNull("absence of an IsRecorded-referenced metric is never awaited");
        flight.Score.Should().Be(0m);
        flight.TermContributions.Should().BeEmpty();

        var row = MetricAbsenceFixtures.Score(task, RecordednessFixtures.Class(task), entry)
            .Results[MetricAbsenceFixtures.RowKey(0)];
        row.State.Should().Be(TaskResultState.Valid);
        row.RawScore.Should().Be(0m);
        row.AwaitingCapture.Should().BeEmpty("the read model must not ask for a capture the gate has already judged");
        row.Selection.Should().NotBeNull();
        row.Selection!.Flights.Should().ContainSingle().Which.Score.Should().Be(0m);
    }

    [Fact]
    public void Recordedness_an_absent_ordinary_referenced_metric_still_pends_with_its_awaited_diagnostic()
    {
        // The carve-out is carved OUT of tier 2, not over it: a blank
        // flightTime — an ordinary referenced metric — still pends with its
        // awaited diagnostic, height recorded or not.
        var entry = RecordednessCapturedFlight(withFlightTime: false, withStartHeight: true);

        var flight = FlightMetricResolution.InterpretAllFlights(entry, RecordednessFixtures.Resolved()).Single();
        flight.Result.State.Should().Be(FlightResultState.Pending);
        flight.Result.Awaited.Should().Be(new PendingFlightDiagnostic(1, "flightTime"));
        flight.Score.Should().Be(0m);
    }

    [Fact]
    public void Recordedness_both_missing_pends_on_the_first_declared_order_ordinary_metric_then_zeroes_on_its_arrival()
    {
        // Blank start height AND blank flight time (5.5.11.7 e's shape and
        // settled decision 6 together): pend first on the ordinary metric —
        // flightTime, first in declared order (flightTime, startHeight,
        // landingDistance, …) among absent ORDINARY referenced metrics, the
        // gate being already determinately false — and when that capture
        // arrives, exactly the zeroed state: Valid, 0, no contributions, no
        // awaited diagnostic.
        var pendingEntry = RecordednessCapturedFlight(withFlightTime: false);

        var pending = FlightMetricResolution.InterpretAllFlights(pendingEntry, RecordednessFixtures.Resolved()).Single();
        pending.Result.State.Should().Be(FlightResultState.Pending, "settled decision 6: pend on the ordinary metric first");
        pending.Result.Awaited.Should().Be(new PendingFlightDiagnostic(1, "flightTime"));

        var captured = pendingEntry.CaptureMeasurement(
            1, "flightTime", MeasuredValue.Of(120m), RecordednessFixtures.Now, RecordednessFixtures.Task.Metrics);
        captured.IsSuccess.Should().BeTrue();
        var zeroed = FlightMetricResolution.InterpretAllFlights(pendingEntry.Apply(captured.Value), RecordednessFixtures.Resolved()).Single();

        zeroed.Result.State.Should().Be(FlightResultState.Valid, "the gate was already determinately false — the capture arrival resolves it to the 5.5.11.7 e zero");
        zeroed.Score.Should().Be(0m);
        zeroed.TermContributions.Should().BeEmpty();
        zeroed.Result.Awaited.Should().BeNull();
    }

    [Fact]
    public void Recordedness_the_awaited_walk_skips_the_absent_recordedness_metric()
    {
        // flightTime recorded; startHeight AND landingDistance blank. The
        // tier-2 walk passes OVER the absent startHeight (declared before
        // landingDistance) — its absence is the gate's false, not a gap — and
        // pends on landingDistance. Without the carve-out it would pend on
        // startHeight.
        var entry = RecordednessCapturedFlight(withLandingDistance: false);

        var flight = FlightMetricResolution.InterpretAllFlights(entry, RecordednessFixtures.Resolved()).Single();
        flight.Result.State.Should().Be(FlightResultState.Pending);
        flight.Result.Awaited.Should().Be(new PendingFlightDiagnostic(1, "landingDistance"),
            "the awaited walk skips the IsRecorded-referenced startHeight and names the first absent ordinary metric");
    }

    [Fact]
    public void Recordedness_the_gate_s_other_clauses_still_combine_a_present_height_with_a_false_75m_flag_zeroes_through_that_arm()
    {
        // The recordedness arm is one conjunct among the gate's value clauses:
        // with the height recorded and landedWithin75m false, the flight
        // zeroes through THAT arm — the same zeroed state, different conjunct.
        var entry = RecordednessCapturedFlight(withStartHeight: true, within75m: false);

        var flight = FlightMetricResolution.InterpretAllFlights(entry, RecordednessFixtures.Resolved()).Single();
        flight.Result.State.Should().Be(FlightResultState.Valid);
        flight.Result.Awaited.Should().BeNull("a failed value clause is a gate fact, never a capture gap");
        flight.Score.Should().Be(0m);
        flight.TermContributions.Should().BeEmpty();
    }

    // ------------------------------------------- recordedness fixture helpers

    /// <summary>
    /// One flight of the recordedness shape: the demands are captured at
    /// compliant values (flightTime 120, startHeight 200, landingDistance 1)
    /// unless the corresponding flag turns them off — a blank being exactly
    /// the absence the tier table acts on — and the three exception metrics
    /// are always captured at their compliant values so the only gate arm in
    /// play is the recordedness one unless a test states otherwise.
    /// </summary>
    private static Entry RecordednessCapturedFlight(
        bool withFlightTime = true,
        bool withStartHeight = false,
        bool withLandingDistance = true,
        bool within75m = true)
    {
        var entry = MetricAbsenceFixtures.OpenEntry(flightCount: 1);

        if (withFlightTime)
            entry = MetricAbsenceFixtures.Capture(entry, 1, "flightTime", MeasuredValue.Of(120m), RecordednessFixtures.Task.Metrics);

        if (withStartHeight)
            entry = MetricAbsenceFixtures.Capture(entry, 1, "startHeight", MeasuredValue.Of(200m), RecordednessFixtures.Task.Metrics);

        if (withLandingDistance)
            entry = MetricAbsenceFixtures.Capture(entry, 1, "landingDistance", MeasuredValue.Of(1m), RecordednessFixtures.Task.Metrics);

        entry = MetricAbsenceFixtures.Capture(entry, 1, "overflySeconds", MeasuredValue.Of(0m), RecordednessFixtures.Task.Metrics);
        entry = MetricAbsenceFixtures.Capture(entry, 1, "touchedByCompetitor", MeasuredValue.Of(false), RecordednessFixtures.Task.Metrics);
        entry = MetricAbsenceFixtures.Capture(entry, 1, "landedWithin75m", MeasuredValue.Of(within75m), RecordednessFixtures.Task.Metrics);

        return entry;
    }
}

/// <summary>
/// Shared fixture for the metric absence semantics tests — one construction for
/// both the example-based tests above and the property tests in
/// MetricAbsenceSemanticsPropertyTests.cs (the ClassDefinitionFixtures.cs
/// discipline; the WI-3 <c>AbsenceSemantics()</c> fixture lives in
/// Soarscore.Application.Tests, which this project cannot reference).
///
/// The synthetic task declares two unassumed demanded observations
/// (flightTime, landingDistance), one Flag assumption (landedOut ⇒ false), one
/// Number assumption (overflySeconds ⇒ 10 — deliberately NOT zero, so an
/// engine that defaulted absence to 0 instead of the declared assumption is
/// caught), a flight-void gate reading both assumptions, and a raw score of
/// rate + landing lookup + conditional derived penalty. It is written with the
/// seed authoring helpers (M/P/T/Rows) — the same smart constructors the
/// corpus classes use.
/// </summary>
internal static class MetricAbsenceFixtures
{
    public static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    public static readonly IReadOnlyDictionary<string, MeasuredValue> NoBindings =
        new Dictionary<string, MeasuredValue>();

    public static TaskDefinition SyntheticTask { get; } = new()
    {
        Code = "S",
        Name = "Absence semantics synthetic",
        Metrics =
        [
            Metric.Number("flightTime", "s", RoundingMode.Truncate, 1),                          // demanded observation — no assumption
            Metric.Number("landingDistance", "m", RoundingMode.Truncate, 1),                     // demanded observation — no assumption
            Metric.Flag("landedOut", whenNotRecorded: false),                                    // Flag assumption
            Metric.Number("overflySeconds", "s", RoundingMode.Truncate, 1, whenNotRecorded: 10), // Number assumption, deliberately non-zero
        ],
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        FlightValidWhen = Predicate.All(Predicate.LessThanOrEqual("overflySeconds", 60), Predicate.Is("landedOut", false)),
        Score =
        [
            ScoreTerm.Rate("flightTime", 1),
            ScoreTerm.Lookup("landingDistance", Rows.UpTo(5, 50).Then(10, 25).Rest(0)),
            ScoreTerm.When(Predicate.GreaterThan("overflySeconds", 0), ScoreTerm.Constant(-30)),
        ],
    };

    public static ClassDefinition SyntheticClass(TaskDefinition task) => new()
    {
        Name = "Synthetic absence class",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = ReflightSelection.Replacement,
            OthersScore = ReflightSelection.BetterOf,
        },
        Phases =
        [
            new PhaseDefinition
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new ValidityRule { MinRounds = 1 },
                Tasks = [task],
            },
        ],
    };

    public static Gen<MeasuredValue> Number(int lo, int hi) =>
        Gen.Int[lo, hi].Select(i => MeasuredValue.Of((decimal)i));

    public static Gen<MeasuredValue> Flag => Gen.Bool.Select(MeasuredValue.Of);

    /// <summary>A captured-or-absent slot: the generated capture subsets.</summary>
    public static Gen<MeasuredValue?> Opt(Gen<MeasuredValue> value) =>
        from captured in Gen.Bool
        from v in value
        select captured ? v : null;

    /// <summary>An entry with <paramref name="flightCount"/> open flights, nothing captured.</summary>
    public static Entry OpenEntry(int flightCount)
    {
        var entry = Entry.Create(new EntryOpened(
            EntryId.New(), CompetitionId.New(), 0, 1, 1,
            GroupId.New(), CompetitorId.New(), ReflightRole.Original, Now));

        for (var sequence = 1; sequence <= flightCount; sequence++)
        {
            var opened = entry.OpenFlight(sequence, maxLaunches: null, at: Now);
            opened.IsSuccess.Should().BeTrue();
            entry = entry.Apply(opened.Value);
        }

        return entry;
    }

    /// <summary>Capture via the Entry decide function, folding the event in.
    /// Validated against the synthetic task's declared metrics.</summary>
    public static Entry Capture(Entry entry, int flightSequence, string metric, MeasuredValue value) =>
        Capture(entry, flightSequence, metric, value, SyntheticTask.Metrics);

    /// <summary>Capture via the Entry decide function, folding the event in —
    /// against the caller's task: the property tests capture on seed tasks.</summary>
    public static Entry Capture(
        Entry entry, int flightSequence, string metric, MeasuredValue value,
        ImmutableArray<MetricDefinition> metrics)
    {
        var decided = entry.CaptureMeasurement(flightSequence, metric, value, Now, metrics);
        decided.IsSuccess.Should().BeTrue($"{metric}: {decided.Code}");
        return entry.Apply(decided.Value);
    }

    /// <summary>
    /// One complete flight on the entry: 100 flight time + 50 landing table,
    /// landedOut false and overflySeconds 0 — raw score 150 for that flight.
    /// </summary>
    public static Entry WithCompleteFlight(Entry entry, int flightSequence) =>
        Capture(
            Capture(
                Capture(
                    Capture(entry, flightSequence, "flightTime", MeasuredValue.Of(100m)),
                    flightSequence, "landingDistance", MeasuredValue.Of(3m)),
                flightSequence, "landedOut", MeasuredValue.Of(false)),
            flightSequence, "overflySeconds", MeasuredValue.Of(0m));

    /// <summary>One complete flight — raw score 150.</summary>
    public static Entry CompleteEntry() => WithCompleteFlight(OpenEntry(flightCount: 1), 1);

    /// <summary>
    /// One flight missing only flightTime — pending on the first unassumed
    /// declared referenced metric (landingDistance 3 is captured).
    /// </summary>
    public static Entry EntryPendingOnFlightTime() =>
        Capture(OpenEntry(flightCount: 1), 1, "landingDistance", MeasuredValue.Of(3m));

    /// <summary>Score the entries as one group, keyed <see cref="RowKey"/>.</summary>
    public static GroupResult Score(TaskDefinition task, ClassDefinition classDef, params Entry[] entries)
    {
        var map = ImmutableDictionary.CreateBuilder<string, Entry>();
        for (var i = 0; i < entries.Length; i++)
            map[RowKey(i)] = entries[i];

        return ScoringService.ScoreGroup(GroupId.New().ToString(), task, classDef, map.ToImmutable(), NoBindings);
    }

    public static string RowKey(int index) => $"subject-{index}";
}
