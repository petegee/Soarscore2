using CsCheck;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Model-based property test for Entry's fold (LADR-0003: CsCheck's
/// SampleModelBased) — complements EntryFoldTests's single, hand-written
/// event stream by driving long, randomly-interleaved sequences of
/// FlightOpened / MeasurementCaptured / MeasurementAmended /
/// PenaltyRecorded / EntryAnnulled against the real <see cref="Entry"/>
/// fold and a plain mutable reference model in lockstep, failing (and
/// shrinking to a minimal repro) the moment the two disagree.
///
/// The reference model tracks shape only — which flights exist, how many
/// measurements each has and how many amendments each measurement has —
/// not the captured values themselves, because the fold's only interesting
/// behaviour is which node an event's Sequence/Metric addressing reaches,
/// not what MeasuredValue it carries.
/// </summary>
public class EntryModelBasedFoldTests
{
    private sealed class MeasurementModel
    {
        public required string Metric { get; init; }

        public int AmendmentCount { get; set; }
    }

    private sealed class FlightModel
    {
        public required int Sequence { get; init; }

        public required List<MeasurementModel> Measurements { get; init; }
    }

    private sealed class Model
    {
        public List<FlightModel> Flights { get; } = [];

        public int PenaltyCount { get; set; }

        public string? LastAnnulmentReason { get; set; }
    }

    private sealed class Actual
    {
        public required Entry Value { get; set; }
    }

    private static readonly TimeWindow SampleWorkingTime = new()
    {
        Start = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 1, 10, 9, 10, 0, TimeSpan.Zero),
    };

    // A wide raw index, reduced modulo the *current* live flight count inside
    // each operation — the count isn't known when the Gen is built, only when
    // the operation actually runs against whatever state came before it.
    private static readonly Gen<int> Pick = Gen.Int[0, 999];

    private static readonly Gen<string> Metric = Gen.OneOfConst("flightTime", "landingBonus", "distance");

    private static readonly Gen<decimal> NumericValue = Gen.Int[0, 100_000].Select(i => i / 100m);

    private static readonly Gen<string> AnnulmentReason =
        Gen.OneOfConst("outside course", "late launch", "boundary violation");

    private static readonly GenOperation<Actual, Model> OpenFlight =
        Gen.Operation<Actual, Model>(
            "OpenFlight",
            actual =>
            {
                var sequence = actual.Value.Flights.Length + 1;
                actual.Value = actual.Value.Apply(new FlightOpened(sequence, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            },
            model => model.Flights.Add(new FlightModel { Sequence = model.Flights.Count + 1, Measurements = [] }));

    private static readonly GenOperation<Actual, Model> CaptureMeasurement =
        (from pick in Pick from metric in Metric from value in NumericValue select (pick, metric, value))
        .Operation<Actual, Model>(
            p => $"CaptureMeasurement(#{p.pick}, {p.metric})",
            (actual, p) =>
            {
                if (actual.Value.Flights.Length == 0)
                {
                    return;
                }

                var sequence = (p.pick % actual.Value.Flights.Length) + 1;
                actual.Value = actual.Value.Apply(new MeasurementCaptured(
                    sequence,
                    new Measurement { Metric = p.metric, Value = MeasuredValue.Of(p.value), CapturedAt = DateTimeOffset.UtcNow }));
            },
            (model, p) =>
            {
                if (model.Flights.Count == 0)
                {
                    return;
                }

                var sequence = (p.pick % model.Flights.Count) + 1;
                var flight = model.Flights.Single(f => f.Sequence == sequence);
                flight.Measurements.Add(new MeasurementModel { Metric = p.metric, AmendmentCount = 0 });
            });

    private static readonly GenOperation<Actual, Model> AmendMeasurement =
        (from pick in Pick from metric in Metric from value in NumericValue select (pick, metric, value))
        .Operation<Actual, Model>(
            p => $"AmendMeasurement(#{p.pick}, {p.metric})",
            (actual, p) =>
            {
                if (actual.Value.Flights.Length == 0)
                {
                    return;
                }

                var sequence = (p.pick % actual.Value.Flights.Length) + 1;
                actual.Value = actual.Value.Apply(new MeasurementAmended(
                    sequence,
                    p.metric,
                    new Amendment { NewValue = MeasuredValue.Of(p.value), Reason = "property-test amendment", By = "PBT", At = DateTimeOffset.UtcNow }));
            },
            (model, p) =>
            {
                if (model.Flights.Count == 0)
                {
                    return;
                }

                var sequence = (p.pick % model.Flights.Count) + 1;
                var flight = model.Flights.Single(f => f.Sequence == sequence);
                foreach (var measurement in flight.Measurements.Where(m => m.Metric == p.metric))
                {
                    measurement.AmendmentCount++;
                }
            });

    private static readonly GenOperation<Actual, Model> RecordPenalty =
        Gen.Operation<Actual, Model>(
            "RecordPenalty",
            actual => actual.Value = actual.Value.Apply(new Entries.PenaltyRecorded(new Penalty { InfractionType = "test", Scope = PenaltyScope.Flight })),
            model => model.PenaltyCount++);

    private static readonly GenOperation<Actual, Model> Annul =
        AnnulmentReason.Operation<Actual, Model>(
            reason => $"Annul({reason})",
            (actual, reason) => actual.Value = actual.Value.Apply(new EntryAnnulled(new Annulment { Reason = reason, By = "PBT", At = DateTimeOffset.UtcNow })),
            (model, reason) => model.LastAnnulmentReason = reason);

    private static readonly Gen<(Actual actual, Model model)> Initial =
        Gen.Int[0, 0].Select(_ =>
        {
            var entry = Entry.Create(new EntryOpened(
                EntryId.New(), SampleWorkingTime, GroupId.New(), CompetitorId.New(), ReflightRole.Original, DateTimeOffset.UtcNow));

            return (new Actual { Value = entry }, new Model());
        });

    [Fact]
    public void Random_event_sequences_fold_to_the_structurally_matching_reference_model()
    {
        Check.SampleModelBased(
            Initial,
            [OpenFlight, CaptureMeasurement, AmendMeasurement, RecordPenalty, Annul],
            StructurallyEqual);
    }

    private static bool StructurallyEqual(Actual actual, Model model)
    {
        var flights = actual.Value.Flights;
        if (flights.Length != model.Flights.Count)
        {
            return false;
        }

        for (var i = 0; i < flights.Length; i++)
        {
            var flight = flights[i];
            var flightModel = model.Flights[i];
            if (flight.Sequence != flightModel.Sequence || flight.Measurements.Length != flightModel.Measurements.Count)
            {
                return false;
            }

            for (var j = 0; j < flight.Measurements.Length; j++)
            {
                if (flight.Measurements[j].Metric != flightModel.Measurements[j].Metric
                    || flight.Measurements[j].Amendments.Length != flightModel.Measurements[j].AmendmentCount)
                {
                    return false;
                }
            }
        }

        return actual.Value.Penalties.Length == model.PenaltyCount
            && actual.Value.Annulment?.Reason == model.LastAnnulmentReason;
    }
}
