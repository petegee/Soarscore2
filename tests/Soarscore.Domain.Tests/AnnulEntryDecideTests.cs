using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Entry.AnnulEntry"/> —
/// kanban/in-progress/annul-and-penalise-the-second-entry-thread.md WI-2. One
/// example per defect code, the happy path asserting the emitted
/// <see cref="Annulment"/> carries reason, by and the caller's instant, and a
/// re-annulment fact. Mirrors AmendMeasurementDecideTests.cs: an annulment is a
/// ruling, like an amendment is a correction, and the two share Reason/By
/// validation.
///
/// Home to the annulment property invariants P1 and P2 (named during planning).
/// P1 pins the read side the write side exists to serve; P2 pins the overwrite
/// fold that decision 7 relies on.
/// </summary>
public class AnnulEntryDecideTests
{
    private static readonly EntryId SampleId = EntryId.New();
    private static readonly CompetitionId SampleCompetition = CompetitionId.New();
    private static readonly GroupId SampleGroup = GroupId.New();
    private static readonly CompetitorId SampleCompetitor = CompetitorId.New();

    private static readonly MetricDefinition FlightTimeMetric = new()
    {
        Name = "flightTime",
        Kind = MeasuredKind.Number,
        Unit = "s",
    };

    private static readonly ImmutableArray<MetricDefinition> SampleMetrics = [FlightTimeMetric];

    private static Entry SampleEntry() =>
        Entry.Create(new EntryOpened(
            SampleId, SampleCompetition, 1, 1, 1,
            SampleGroup, SampleCompetitor, ReflightRole.Original, DateTimeOffset.UtcNow));

    // ------------------------------------------------------------------- FAILURES

    [Fact]
    public void AnnulEntry_with_a_blank_reason_fails_with_a_stable_code()
    {
        var result = SampleEntry().AnnulEntry("   ", "the jury", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("annulEntry.reasonRequired");
    }

    [Fact]
    public void AnnulEntry_with_a_blank_by_fails_with_a_stable_code()
    {
        var result = SampleEntry().AnnulEntry("outside the re-flight window", "  ", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("annulEntry.byRequired");
    }

    // -------------------------------------------------------------------- SUCCESS

    [Fact]
    public void AnnulEntry_succeeds_carrying_reason_by_and_at()
    {
        var at = new DateTimeOffset(2026, 1, 10, 9, 5, 30, TimeSpan.Zero);

        var result = SampleEntry().AnnulEntry("the competitor re-flew under protest", "the jury", at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Annulment.Reason.Should().Be("the competitor re-flew under protest");
        result.Value.Annulment.By.Should().Be("the jury");
        // The instant is the caller's (the handler supplies IClock's), not invented here.
        result.Value.Annulment.At.Should().Be(at);
    }

    [Fact]
    public void Re_annulling_an_annulled_entry_succeeds_with_the_latest_ruling_standing()
    {
        var first = SampleEntry().AnnulEntry("first ruling", "jury", DateTimeOffset.UtcNow).Value;
        var entry = SampleEntry().Apply(first);

        var second = entry.AnnulEntry("ruling revised", "the same jury", DateTimeOffset.UtcNow.AddMinutes(1));

        second.IsSuccess.Should().BeTrue();
        var folded = entry.Apply(second.Value);
        // The fold overwrites: the latest ruling stands (decision 7).
        folded.Annulment.Should().Be(second.Value.Annulment);
        folded.Annulment!.Reason.Should().Be("ruling revised");
    }

    // ======================================================================= PROPERTY TESTS — P1, P2

    private static readonly DateTimeOffset Base = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly Gen<Annulment> AnnulmentGen =
        from i in Gen.Int[0, 1000]
        from offset in Gen.Int[0, 20]
        select new Annulment { Reason = $"reason {i}", By = $"jury {i % 5}", At = Base.AddSeconds(offset) };

    // ------------------------------------------------------- P2
    // The latest ruling stands: for any non-empty sequence of annulments, the
    // folded Entry.Annulment equals the last one's payload — decision 7's
    // overwrite semantics, held true against an adversarial ordering.

    [Fact]
    public void The_latest_ruling_stands()
    {
        AnnulmentGen.Array[1, 5].Sample(annulments =>
        {
            var entry = SampleEntry();

            foreach (var annulment in annulments)
            {
                var decision = entry.AnnulEntry(annulment.Reason, annulment.By, annulment.At);
                decision.IsSuccess.Should().BeTrue();
                entry = entry.Apply(decision.Value);
            }

            entry.Annulment.Should().Be(annulments[^1]);
        });
    }

    // ------------------------------------------------------- P1
    // Annulment dominates capture: for any Entry state — any number of
    // flights, any captured flight times — folding an EntryAnnulled makes
    // FlightSelector.SelectAndScore return NoResult, even where the same
    // Entry without the annulment would score Valid. The invariant
    // FlightSelector.cs:40-42 encodes; this exercises it against adversarial
    // capture density.

    private static readonly ResolvedTask MinimalTask = new(
        Code: "T", Name: "T",
        Metrics: SampleMetrics,
        Flights: new AllFlights(),
        Timing: new ResolvedTiming(WorkingTimeKind.Fixed, 600, null, null),
        Group: null, Normalise: null, ValidWhen: null, FlightValidWhen: null,
        RawScore: null, Reflight: null,
        Score: ImmutableArray.Create<ScoreTerm>(new RateTerm
        {
            MetricRef = "flightTime", Rate = 1, Cap = null, CapScope = CapScope.PerFlight,
        }),
        ScoreNormalised: ImmutableArray<ScoreTerm>.Empty);

    [Fact]
    public void Annulment_dominates_capture()
    {
        (from flightCount in Gen.Int[0, 4]
         from flightTimes in Gen.Int[1, 999].Array[4]
         select (flightCount, flightTimes))
        .Sample(t =>
        {
            var entry = SampleEntry();
            for (var i = 1; i <= t.flightCount; i++)
            {
                entry = entry.Apply(new FlightOpened(i, Base));
                var captured = entry.CaptureMeasurement(
                    i, "flightTime", MeasuredValue.Of((decimal)t.flightTimes[i - 1]), Base, SampleMetrics);
                captured.IsSuccess.Should().BeTrue();
                entry = entry.Apply(captured.Value);
            }

            // Interpreted flights mirroring the captured data — the control:
            // without an annulment, a non-empty capture scores Valid here.
            var interpreted = entry.Flights.Select(f =>
                FlightInterpreter.Interpret(MinimalTask, f.Sequence, new Dictionary<string, MeasuredValue>
                {
                    ["flightTime"] = f.Measurements.Single(m => m.Metric == "flightTime").Value,
                    ["flight.sequence"] = MeasuredValue.Of(f.Sequence),
                })).ToImmutableArray();

            var bindings = new Dictionary<string, MeasuredValue>();

            if (t.flightCount > 0)
            {
                FlightSelector.SelectAndScore(entry, MinimalTask, bindings, interpreted)
                    .State.Should().Be(TaskResultState.Valid);
            }

            var annulled = entry.Apply(entry.AnnulEntry("protest", "jury", Base).Value);
            var result = FlightSelector.SelectAndScore(annulled, MinimalTask, bindings, interpreted);

            result.State.Should().Be(TaskResultState.NoResult);
            result.RawScore.Should().Be(0m);
            result.Selection.Should().BeNull();
        });
    }
}
