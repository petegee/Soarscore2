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
/// Property tests for the presence-gated validity (recordedness) semantics —
/// WI-5 of kanban/in-progress/add-recorded-predicate.md, the three invariants
/// named at planning, over one F5J-NDC-shaped task (RecordednessFixtures at the
/// foot of this file, written with the seed authoring factories the corpus
/// uses):
///
/// 1. P-PartialCapture (the metric-absence invariant, amended for the new tier
///    row) — for any capture subset of an entry's flights, the entry's score
///    equals a twin entry's where every flight missing ONLY
///    IsRecorded-referenced metrics is realized as the 5.5.11.7 e flight it is
///    (docs/rules/source-docs/f5-electric-2026.md:847): FLOWN, gate-zeroed
///    (State Valid, score 0) and still selected, consuming its slot. Flights
///    missing ordinary referenced metrics stay REMOVED (pending) on both
///    sides — that half is the metric-absence precedent
///    (MetricAbsenceSemanticsPropertyTests.cs P-PendingArithmeticallyInvisible),
///    unchanged; what changed is that a blank recordedness metric is a zero,
///    never a capture gap.
/// 2. P-DigestEquivalence — IsRecorded(M) evaluates exactly as the
///    amendment-resolved digest (MeasurementDigest.Resolve) contains M:
///    presence by metric name, either input form fulfilling (a Number reading
///    or a Flag — metric-absence-semantics.md owner decision 9's convention),
///    amendments never withdrawing presence. The orchestrator half runs the
///    same digests through FlightMetricResolution.InterpretAllFlights — where
///    the absence semantics live — and demands the pend / zero / score routing
///    the digest dictates.
/// 3. P-PendThenZero — a flight missing BOTH an IsRecorded-referenced metric
///    and an ordinary referenced metric pends on the ordinary one (first in
///    the task's declared order — settled decision 6) and reaches EXACTLY the
///    zeroed state (State Valid, score 0, still selected) once the ordinary
///    capture arrives, whatever the capture order: no order produces a
///    different final state.
///
/// Generator strategy: per-flight capture slots over the shape's three
/// unassumed demands (flightTime, startHeight, landingDistance), integer values
/// so capture precision is identity, with the shape's three exception metrics
/// always captured at their compliant values so the only flight-zeroing arms
/// in play are the recordedness one and the twin's 75 m one. Selection kinds
/// are generated like the precedent's sweep — the zeroed flight's slot
/// consumption is only observable under selection, never in the raw sum alone.
///
/// Mutation-proven teeth (kanban/in-progress/add-recorded-predicate.md WI-5):
/// removing the IsRecorded carve-out from
/// FlightMetricResolution.ResolveAndInterpret's tier-2 loop turns invariants 1
/// and 3 red (a height-missing flight pends instead of zeroing); arming
/// PredicateEvaluator's IsRecorded arm to a constant true turns invariant 2
/// red (the evaluator leg contradicts the digest on every absent metric).
/// Reverted immediately after each run.
/// </summary>
public class IsRecordedPredicatePropertyTests
{
    // ================================================== invariant 1: partial capture

    /// <summary>
    /// P-PartialCapture (amended; kanban/in-progress/add-recorded-predicate.md
    /// WI-5, invariant 1): for any generated selection kind and any generated
    /// per-flight capture subset, scoring the entry AS CAPTURED is
///     observationally identical to scoring the twin entry where every
///     height-only-missing flight is realized as the 5.5.11.7 e flight it is —
///     height captured, gate failed through the 75 m arm — so zeroed, still
///     selected, consuming its slot. The twin equality is what makes
///     "zeroed, not removed" a proved statement rather than an asserted one:
///     a flight that pended would be filtered before selection and its slot
///     would go to a different flight. State, raw score entering
///     normalisation, final score, selected-flight profile and awaited
///     diagnostics all match; and directly at the orchestrator, each
///     height-only-missing flight IS the zeroed state (Valid, 0, no
///     contributions, no awaited diagnostic), while the ordinary gaps name
///     their awaited metric and a blank startHeight contributes none.
/// </summary>
    [Fact]
    public void P_PartialCapture_height_only_missing_flights_zero_and_stay_selected_ordinary_missing_ones_are_removed()
    {
        (from selection in Gen.OneOfConst<FlightSelection>(
             new AllFlights(),
             new LastFlight(),
             new LastNFlights(2),
             new BestNFlights { Count = 2 },
             new ExactlyNInOrder { Count = 2 })
         from flightCount in Gen.Int[1, 3]
         from plans in CaptureSlotGen.Array[flightCount]
         select (selection, flightCount, plans))
        .Sample(t =>
        {
            var task = RecordednessFixtures.Task with { Flights = t.selection };
            var classDef = RecordednessFixtures.Class(task);
            var because = $"selection={t.selection.GetType().Name}, plans={Describe(t.plans)}";

            var asCaptured = EntryFor(t.plans, asTwin: false);
            var gateZeroedTwin = EntryFor(t.plans, asTwin: true);

            var capturedGroup = MetricAbsenceFixtures.Score(task, classDef, asCaptured);
            var twinGroup = MetricAbsenceFixtures.Score(task, classDef, gateZeroedTwin);
            var capturedRow = capturedGroup.Results[MetricAbsenceFixtures.RowKey(0)];
            var twinRow = twinGroup.Results[MetricAbsenceFixtures.RowKey(0)];

            capturedRow.State.Should().Be(twinRow.State, because);
            capturedGroup.PreNormalisationScores[MetricAbsenceFixtures.RowKey(0)]
                .Should().Be(twinGroup.PreNormalisationScores[MetricAbsenceFixtures.RowKey(0)], $"{because}: raw score entering normalisation");
            capturedRow.RawScore.Should().Be(twinRow.RawScore, $"{because}: final score");
            capturedRow.AwaitingCapture.Should().Equal(twinRow.AwaitingCapture, $"{because}: awaited-capture diagnostics");
            SelectedShouldMatch(capturedRow.Selection, twinRow.Selection, $"{because}: selection profile");

            // Each height-only-missing flight IS the zeroed state itself —
            // never pending (the tier-2 carve-out), whatever the selection
            // rank made of the row.
            var interpreted = FlightMetricResolution.InterpretAllFlights(asCaptured, RecordednessFixtures.Resolved());
            foreach (var (slot, index) in t.plans.Select((slot, index) => (slot, index)))
            {
                if (!slot.HeightOnlyMissing)
                    continue;

                var flight = interpreted[index];
                flight.Result.State.Should().Be(FlightResultState.Valid, $"{because}: flight {index + 1} missing only startHeight");
                flight.Score.Should().Be(0m, $"{because}: flight {index + 1} scores the 5.5.11.7 e zero");
                flight.Result.Awaited.Should().BeNull($"{because}: flight {index + 1}'s blank height is the gate's false, never a capture gap");
                flight.TermContributions.Should().BeEmpty($"{because}: flight {index + 1} is cancelled, not scored");
            }

            // The ordinary gaps still name their awaited metric — flightTime
            // first in declared order, landingDistance after it — and a blank
            // startHeight contributes NO diagnostic at all.
            var expectedAwaiting = t.plans
                .Select((slot, index) => (slot, index))
                .Where(x => x.slot.OrdinaryMissing)
                .Select(x => new PendingFlightDiagnostic(
                    x.index + 1,
                    x.slot.Time is null ? "flightTime" : "landingDistance"))
                .ToArray();
            capturedRow.AwaitingCapture.Should().Equal(expectedAwaiting, $"{because}: the awaited diagnostics name the ordinary capture gaps only");
        });
    }

    // ================================================== invariant 2: digest equivalence

    /// <summary>
    /// P-DigestEquivalence (kanban/in-progress/add-recorded-predicate.md WI-5,
    /// invariant 2): over generated measurement/amendment sets,
    /// IsRecorded(M) evaluates exactly as the amendment-resolved digest
    /// contains M — the plain dictionary lookup IS the recordedness fact
    /// (settled decision 5; adoption check 24 is what makes it sound, since
    /// the assumption loop can never insert a value for a metric an IsRecorded
    /// reads, so an inserted assumption is never a recording). Presence is by
    /// metric name, either input form fulfilling — a Number reading or a Flag
    /// (owner decision 9) — and an amendment corrects values, never presence.
    /// The orchestrator half routes the same digests as the digest dictates:
    /// pend on the absent ordinary metric first, zero when ONLY the
    /// recordedness metric is absent, score what the digest says when both are
    /// recorded.
    /// </summary>
    [Fact]
    public void P_DigestEquivalence_IsRecorded_holds_exactly_when_the_amendment_resolved_digest_carries_the_metric()
    {
        (from height in HeightGen
         from time in TimeGen
         select (height, time))
        .Sample(t =>
        {
            var flight = new Flight
            {
                Sequence = 1,
                Measurements = [.. t.height, .. t.time, .. GateCompliantBaseline()],
            };
            var digest = MeasurementDigest.Resolve(flight);

            // The evaluator leg, over every declared metric of the shape: the
            // arm's answer is exactly the digest's presence answer.
            foreach (var name in RecordednessFixtures.Task.Metrics.Select(m => m.Name))
            {
                PredicateEvaluator.Evaluate(new IsRecorded { MetricRef = name }, digest.Metrics)
                    .Should().Be(digest.Metrics.ContainsKey(name),
                        $"IsRecorded('{name}') is presence by metric name (owner decision 9), never a value or a form");
            }

            // The orchestrator half — the interpreter applies the absence
            // semantics to the same digest. Dispatched BEFORE interpreting:
            // the pend and zero paths never reach the score terms, but a
            // digest with both metrics recorded and a Flag height reaches the
            // shape's own kind discipline (a PiecewiseTerm over startHeight),
            // which throws before a score — that case is out of this
            // invariant's orchestrator scope and belongs to the evaluator leg.
            var entry = EntryWithFlight(flight);

            var heightRecorded = t.height.Length > 0;
            var timeRecorded = t.time.Length > 0;

            if (!timeRecorded)
            {
                // Pend first (settled decision 6): the ordinary gap is the
                // capture gap, whatever the recordedness metric's state.
                var pending = FlightMetricResolution.InterpretAllFlights(entry, RecordednessFixtures.Resolved())[0];
                pending.Result.State.Should().Be(FlightResultState.Pending, "an absent ordinary referenced metric pends before any gate is read");
                pending.Result.Awaited.Should().Be(new PendingFlightDiagnostic(1, "flightTime"));
            }
            else if (!heightRecorded)
            {
                // Absent ONLY the recordedness metric: the zeroed state —
                // never pending, never a capture gap.
                var zeroed = FlightMetricResolution.InterpretAllFlights(entry, RecordednessFixtures.Resolved())[0];
                zeroed.Result.State.Should().Be(FlightResultState.Valid, "a blank startHeight is the gate's false, not a pending flight");
                zeroed.Score.Should().Be(0m, "the 5.5.11.7 e zero");
                zeroed.TermContributions.Should().BeEmpty("the flight is cancelled, not scored");
                zeroed.Result.Awaited.Should().BeNull("absence of an IsRecorded-referenced metric is never awaited");
            }
            else if (t.height[0].Value.Kind == MeasuredKind.Flag)
            {
                // Both recorded with a FLAG height: the recordedness fact
                // holds (the evaluator leg above), but this shape's own kind
                // discipline — a PiecewiseTerm over startHeight — stops the
                // pipeline before a score. Out of this invariant's scope; the
                // evaluator leg is the statement for the Flag form.
            }
            else
            {
                // Both recorded (Number height): the flight scores what the
                // digest says — the amendment-resolved values, never the
                // originals.
                var scored = FlightMetricResolution.InterpretAllFlights(entry, RecordednessFixtures.Resolved())[0];
                var effectiveTime = Effective(t.time[0]);
                var effectiveHeight = Effective(t.height[0]);
                scored.Result.State.Should().Be(FlightResultState.Valid);
                scored.Score.Should().Be(
                    RecordednessFixtures.ExpectedFlightScore(effectiveTime, effectiveHeight, LandingBaseline),
                    "the score reads the amendment-resolved digest, and presence held so the gate passed");
                scored.Result.Awaited.Should().BeNull();
            }
        });
    }

    // ================================================== invariant 3: pend-then-zero

    /// <summary>
    /// P-PendThenZero (kanban/in-progress/add-recorded-predicate.md WI-5,
    /// invariant 3 — settled decision 6): a flight missing BOTH an
    /// IsRecorded-referenced metric and an ordinary referenced metric pends on
    /// the ordinary one — flightTime, first in the shape's declared order
    /// (flightTime, startHeight, landingDistance, …) among absent ordinary
    /// referenced metrics — even though the gate is already determinately
    /// false on the blank height. Once the ordinary capture arrives while the
    /// height is still blank, the flight reaches EXACTLY the zeroed state:
    /// State Valid, score 0, no term contributions, no awaited diagnostic, and
    /// still selected (the flight is FLOWN and consumes the slot). Over both
    /// capture orders — time-then-height and height-then-time — the final
    /// state after both captures is identical and is the shape's own
    /// arithmetic: no capture order produces a different final state.
    /// </summary>
    [Fact]
    public void P_PendThenZero_the_both_missing_flight_converges_to_exactly_the_zeroed_state_whatever_the_capture_order()
    {
        (from flightTime in MetricAbsenceFixtures.Number(1, 600)
         from startHeight in MetricAbsenceFixtures.Number(0, 650)
         from landingDistance in MetricAbsenceFixtures.Number(0, 15)
         select (flightTime, startHeight, landingDistance))
        .Sample(t =>
        {
            var resolved = RecordednessFixtures.Resolved();
            var task = RecordednessFixtures.Task;
            var classDef = RecordednessFixtures.Class(task);

            // The departure point both orders share: one flight with only its
            // landing distance recorded — flightTime AND startHeight blank.
            var open = MetricAbsenceFixtures.Capture(
                MetricAbsenceFixtures.OpenEntry(flightCount: 1),
                1, "landingDistance", t.landingDistance, RecordednessFixtures.Task.Metrics);

            var bothMissing = FlightMetricResolution.InterpretAllFlights(open, resolved)[0];
            bothMissing.Result.State.Should().Be(FlightResultState.Pending, "both gaps: pend on the ordinary one first (settled decision 6)");
            bothMissing.Result.Awaited.Should().Be(new PendingFlightDiagnostic(1, "flightTime"));

            // Order 1 — time then height. The ordinary capture arrives while
            // the height is still blank: EXACTLY the zeroed state.
            var timeThenHeight = MetricAbsenceFixtures.Capture(
                open, 1, "flightTime", t.flightTime, RecordednessFixtures.Task.Metrics);

            var zeroed = FlightMetricResolution.InterpretAllFlights(timeThenHeight, resolved)[0];
            zeroed.Result.State.Should().Be(FlightResultState.Valid, "the ordinary capture arrived with the height still blank — the 5.5.11.7 e zero");
            zeroed.Score.Should().Be(0m);
            zeroed.TermContributions.Should().BeEmpty();
            zeroed.Result.Awaited.Should().BeNull("the blank height is the gate's false, never an awaited capture");

            var zeroedRow = MetricAbsenceFixtures.Score(task, classDef, timeThenHeight)
                .Results[MetricAbsenceFixtures.RowKey(0)];
            zeroedRow.State.Should().Be(TaskResultState.Valid, "the zeroed flight is still selected — the flight is FLOWN and consumes the slot");
            zeroedRow.RawScore.Should().Be(0m);
            zeroedRow.Selection.Should().NotBeNull();
            zeroedRow.Selection!.Flights.Should().ContainSingle().Which.Score.Should().Be(0m);
            zeroedRow.AwaitingCapture.Should().BeEmpty();

            var timeThenHeightFinal = MetricAbsenceFixtures.Capture(
                timeThenHeight, 1, "startHeight", t.startHeight, RecordednessFixtures.Task.Metrics);

            // Order 2 — height then time. The recordedness gap closes first:
            // the flight still pends on flightTime, the ordinary capture it is
            // missing — the recordedness gap is not a capture gap and never
            // awaited.
            var heightThenTime = MetricAbsenceFixtures.Capture(
                MetricAbsenceFixtures.OpenEntry(flightCount: 1),
                1, "landingDistance", t.landingDistance, RecordednessFixtures.Task.Metrics);
            heightThenTime = MetricAbsenceFixtures.Capture(
                heightThenTime, 1, "startHeight", t.startHeight, RecordednessFixtures.Task.Metrics);

            var stillPending = FlightMetricResolution.InterpretAllFlights(heightThenTime, resolved)[0];
            stillPending.Result.State.Should().Be(FlightResultState.Pending, "pend first: the ordinary flightTime gap is still open");
            stillPending.Result.Awaited.Should().Be(new PendingFlightDiagnostic(1, "flightTime"));

            var heightThenTimeFinal = MetricAbsenceFixtures.Capture(
                heightThenTime, 1, "flightTime", t.flightTime, RecordednessFixtures.Task.Metrics);

            // The convergence: no capture order produces a different final
            // state — and it is the shape's own arithmetic, the full
            // gate-passing score.
            var expected = RecordednessFixtures.ExpectedFlightScore(
                t.flightTime.Number!.Value, t.startHeight.Number!.Value, t.landingDistance.Number!.Value);

            var orderOne = MetricAbsenceFixtures.Score(task, classDef, timeThenHeightFinal);
            var orderTwo = MetricAbsenceFixtures.Score(task, classDef, heightThenTimeFinal);
            var one = orderOne.Results[MetricAbsenceFixtures.RowKey(0)];
            var two = orderTwo.Results[MetricAbsenceFixtures.RowKey(0)];

            one.State.Should().Be(two.State, "the final state is capture-order independent");
            orderOne.PreNormalisationScores[MetricAbsenceFixtures.RowKey(0)]
                .Should().Be(orderTwo.PreNormalisationScores[MetricAbsenceFixtures.RowKey(0)], "the final raw score is capture-order independent");
            one.RawScore.Should().Be(two.RawScore).And.Be(expected, "the final score is the full gate-passing arithmetic");
            one.AwaitingCapture.Should().Equal(two.AwaitingCapture, "the final diagnostics are capture-order independent");
            one.Selection.Should().NotBeNull();
            two.Selection.Should().NotBeNull();
            one.Selection!.Flights.Single().Score.Should().Be(two.Selection!.Flights.Single().Score);
        });
    }

    // ------------------------------------------------------------- helpers

    private const decimal LandingBaseline = 1m;

    /// <summary>
    /// One generated capture slot over the shape's three unassumed demands.
    /// The height is always generated; <see cref="HeightMissing"/> decides
    /// whether the as-captured entry records it (the twin always does, and
    /// fails the gate through the 75 m arm instead).
    /// </summary>
    private sealed record CaptureSlot(MeasuredValue? Time, MeasuredValue Height, bool HeightMissing, MeasuredValue? Landing)
    {
        public bool HeightOnlyMissing => HeightMissing && Time is not null && Landing is not null;

        public bool OrdinaryMissing => Time is null || Landing is null;
    }

    private static readonly Gen<CaptureSlot> CaptureSlotGen =
        from time in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(1, 600))
        from height in MetricAbsenceFixtures.Number(0, 650)
        from heightMissing in Gen.Bool
        from landing in MetricAbsenceFixtures.Opt(MetricAbsenceFixtures.Number(0, 15))
        select new CaptureSlot(time, height, heightMissing, landing);

    /// <summary>
    /// Fold the entry the sweep compares. As captured, every flight carries
    /// exactly its slot's captures, the shape's three exception metrics at
    /// their compliant values. The twin realizes every height-missing flight
    /// as the 5.5.11.7 e flight it is: the height captured anyway and the gate
    /// failed through the 75 m arm — zeroed, still selected, consuming the
    /// slot — while ordinary-missing flights pend identically on both sides.
    /// </summary>
    private static Entry EntryFor(IReadOnlyList<CaptureSlot> plans, bool asTwin)
    {
        var entry = MetricAbsenceFixtures.OpenEntry(flightCount: plans.Count);

        for (var index = 0; index < plans.Count; index++)
        {
            var sequence = index + 1;
            var slot = plans[index];

            if (slot.Time is { } time)
                entry = MetricAbsenceFixtures.Capture(entry, sequence, "flightTime", time, RecordednessFixtures.Task.Metrics);

            if (asTwin || !slot.HeightMissing)
                entry = MetricAbsenceFixtures.Capture(entry, sequence, "startHeight", slot.Height, RecordednessFixtures.Task.Metrics);

            if (slot.Landing is { } landing)
                entry = MetricAbsenceFixtures.Capture(entry, sequence, "landingDistance", landing, RecordednessFixtures.Task.Metrics);

            entry = MetricAbsenceFixtures.Capture(entry, sequence, "overflySeconds", MeasuredValue.Of(0m), RecordednessFixtures.Task.Metrics);
            entry = MetricAbsenceFixtures.Capture(entry, sequence, "touchedByCompetitor", MeasuredValue.Of(false), RecordednessFixtures.Task.Metrics);
            entry = MetricAbsenceFixtures.Capture(entry, sequence, "landedWithin75m",
                asTwin && slot.HeightMissing ? MeasuredValue.Of(false) : MeasuredValue.Of(true), RecordednessFixtures.Task.Metrics);
        }

        return entry;
    }

    private static string Describe(IReadOnlyList<CaptureSlot> plans) =>
        string.Join("|", plans.Select(p =>
            $"{(p.Time is null ? "·" : "t")}{(p.HeightMissing ? "·" : "h")}{(p.Landing is null ? "·" : "d")}"));

    /// <summary>
    /// The generated startHeight measurement sets: nothing recorded, a Number
    /// reading, a Flag (either input form fulfils presence), or a Number
    /// reading corrected by one amendment (amendments resolve values, never
    /// presence — there is no amendment that withdraws a measurement).
    /// Ascending amendment Ats, so the digest's effective value is the last.
    /// </summary>
    private static readonly Gen<ImmutableArray<Measurement>> HeightGen =
        Gen.OneOf(
        [
            Gen.Const<ImmutableArray<Measurement>>([]),
            MetricAbsenceFixtures.Number(0, 650).Select(h => One("startHeight", h)),
            Gen.Bool.Select(flag => One("startHeight", MeasuredValue.Of(flag))),
            from h in MetricAbsenceFixtures.Number(0, 650)
            from h2 in MetricAbsenceFixtures.Number(0, 650)
            select One("startHeight", h, h2),
        ]);

    private static readonly Gen<ImmutableArray<Measurement>> TimeGen =
        Gen.OneOf(
        [
            Gen.Const<ImmutableArray<Measurement>>([]),
            MetricAbsenceFixtures.Number(1, 600).Select(v => One("flightTime", v)),
            from t in MetricAbsenceFixtures.Number(1, 600)
            from t2 in MetricAbsenceFixtures.Number(1, 600)
            select One("flightTime", t, t2),
        ]);

    /// <summary>
    /// The shape's three exception metrics, always recorded at their compliant
    /// values — the gate's non-recordedness arms pass, so the recordedness arm
    /// is the only flight-zeroing arm in play.
    /// </summary>
    private static ImmutableArray<Measurement> GateCompliantBaseline() =>
    [
        One("landingDistance", MeasuredValue.Of(LandingBaseline))[0],
        One("overflySeconds", MeasuredValue.Of(0m))[0],
        One("touchedByCompetitor", MeasuredValue.Of(false))[0],
        One("landedWithin75m", MeasuredValue.Of(true))[0],
    ];

    private static ImmutableArray<Measurement> One(string metric, MeasuredValue value, params MeasuredValue[] amendments) =>
    [
        new Measurement
        {
            Metric = metric,
            Value = value,
            CapturedAt = RecordednessFixtures.Now,
            Amendments = [.. amendments.Select((v, i) => new Amendment
            {
                NewValue = v,
                Reason = "correction",
                By = "scorer",
                At = RecordednessFixtures.Now.AddSeconds(i + 1),
            })],
        },
    ];

    /// <summary>The amendment-resolved effective Number of a generated measurement (ascending Ats: the last amendment).</summary>
    private static decimal Effective(Measurement measurement) =>
        (measurement.Amendments.IsEmpty ? measurement.Value : measurement.Amendments[^1].NewValue).Number!.Value;

    private static Entry EntryWithFlight(Flight flight) => new()
    {
        Id = EntryId.New(),
        CompetitionRef = CompetitionId.New(),
        PhaseOrdinal = 0,
        RoundOrdinal = 1,
        TaskRoundOrdinal = 1,
        GroupRef = GroupId.New(),
        CompetitorRef = CompetitorId.New(),
        Role = ReflightRole.Original,
        Flights = [flight],
    };

    /// <summary>Selection equivalence for the sweep: same launches (the flight.sequence intrinsic) and same flight scores — or both null.</summary>
    private static void SelectedShouldMatch(SelectedFlights? left, SelectedFlights? right, string because)
    {
        if (left is null)
        {
            right.Should().BeNull(because);
            return;
        }

        right.Should().NotBeNull(because);

        static (int Sequence, decimal Score)[] Profile(SelectedFlights selection) =>
            [.. selection.Flights.Select(f => (
                Sequence: (int)f.Metrics["flight.sequence"].Number!.Value,
                Score: f.Score))];

        Profile(left).Should().Equal(Profile(right!), because);
    }
}

/// <summary>
/// Shared fixture for the recordedness tests — one F5J-NDC-shaped construction
/// for the property tests above and the example-based tests in
/// MetricAbsenceSemanticsTests.cs and FlightZeroingTaskGateTests.cs (the
/// MetricAbsenceFixtures discipline). SeedF5jNdc's task shape written with the
/// seed authoring factories: six metrics in SeedF5J's declared order —
/// flightTime, startHeight, landingDistance, overflySeconds,
/// touchedByCompetitor, landedWithin75m, flightTime BEFORE startHeight, which
/// is what makes the both-missing flight pend on flightTime (settled decision
/// 6) — the 5.5.11.7 e recordedness gate (carried by NZ.0.3 c) beside its two
/// value clauses, and the three score terms of 5.5.11.12 c/e/h. The three
/// exceptions carry their assumptions (absence ⇒ compliance); startHeight
/// declares NONE — check 24 forbids it — so its only absence path is the gate.
/// </summary>
internal static class RecordednessFixtures
{
    public static readonly DateTimeOffset Now = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

    public static TaskDefinition Task { get; } = new()
    {
        Code = "R",
        Name = "Recordedness synthetic",
        Metrics =
        [
            Metric.Number("flightTime", "s", RoundingMode.Truncate, 1),                         // 5.5.11.12 b
            Metric.Number("startHeight", "m", RoundingMode.Truncate, 1),                        // 5.5.11.12 d — no assumption (check 24)
            Metric.Number("landingDistance", "m", RoundingMode.Truncate, 1),                    // 5.5.11.12 i
            Metric.Number("overflySeconds", "s", RoundingMode.Truncate, 1, whenNotRecorded: 0), // 5.5.11.12 g, k (carried by NZ.0.3 c)
            Metric.Flag("touchedByCompetitor", whenNotRecorded: false),                         // 5.5.11.12 j (carried by NZ.0.3 c)
            Metric.Flag("landedWithin75m", whenNotRecorded: true),                              // NZ.0.3 h; FAI 5.5.11.7 d
        ],
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        FlightValidWhen = Predicate.All(
            Predicate.IsRecorded("startHeight"),                                        // 5.5.11.7 e (carried by NZ.0.3 c)
            Predicate.Is("landedWithin75m", true),                                      // NZ.0.3 h; FAI 5.5.11.7 d
            Predicate.LessThanOrEqual("overflySeconds", 60)),                           // 5.5.11.12 g
        Score =
        [
            ScoreTerm.Rate("flightTime", 1, cap: 600),                                  // 5.5.11.12 c
            ScoreTerm.Piecewise("startHeight", Bands.From(0).UpTo(200, -0.5m).Rest(-3)), // 5.5.11.12 e, cumulative
            ScoreTerm.When(
                Predicate.All(Predicate.Equal("overflySeconds", 0), Predicate.Is("touchedByCompetitor", false)),
                ScoreTerm.Lookup("landingDistance", Rows.UpTo(5, 50).Then(10, 25).Rest(0))), // 5.5.11.12 h
        ],
    };

    public static ClassDefinition Class(TaskDefinition task) => new()
    {
        Name = "Synthetic recordedness class",
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

    /// <summary>The shape resolved with no parameters — it declares none.</summary>
    public static ResolvedTask Resolved() =>
        ParameterResolver.ResolveTask(Task, MetricAbsenceFixtures.NoBindings, []);

    /// <summary>
    /// The independent arithmetic oracle for one complete flight of the shape:
    /// 1 pt/s capped at 600, cumulative height bands (−0.5/m to 200 m, −3/m
    /// above), and the landing table (≤5 m → 50, ≤10 → 25, beyond → 0) — the
    /// lookup's condition passes with the shape's fixed compliant exception
    /// captures. Written from 5.5.11.12's numbers, not from the engine.
    /// </summary>
    public static decimal ExpectedFlightScore(decimal flightTime, decimal startHeight, decimal landingDistance)
    {
        var flight = Math.Min(flightTime, 600m);
        var height = startHeight <= 200m ? -0.5m * startHeight : -100m - 3m * (startHeight - 200m);
        var landing = landingDistance <= 5m ? 50m : landingDistance <= 10m ? 25m : 0m;
        return flight + height + landing;
    }
}
