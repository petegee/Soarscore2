using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

public class EntryFoldTests
{
    private static readonly EntryId SampleId = EntryId.New();
    private static readonly GroupId SampleGroup = GroupId.New();
    private static readonly CompetitorId SampleCompetitor = CompetitorId.New();

    private static readonly TimeWindow SampleWorkingTime = new()
    {
        Start = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 1, 10, 9, 10, 0, TimeSpan.Zero),
    };

    private static EntryOpened SampleOpened(DateTimeOffset at) =>
        new(SampleId, SampleWorkingTime, SampleGroup, SampleCompetitor, ReflightRole.Original, at);

    [Fact]
    public void EntryOpened_creates_the_projection_from_an_empty_stream()
    {
        var at = DateTimeOffset.UtcNow;
        var @event = SampleOpened(at);

        var entry = Entry.Create(@event);

        Assert.NotNull(entry);
        Assert.Equal(SampleId, entry.Id);
        Assert.Equal(SampleWorkingTime, entry.WorkingTime);
        Assert.Equal(SampleGroup, entry.GroupRef);
        Assert.Equal(SampleCompetitor, entry.CompetitorRef);
        Assert.Equal(ReflightRole.Original, entry.Role);
        Assert.Null(entry.Annulment);
        Assert.Empty(entry.Flights);
        Assert.Empty(entry.Penalties);
    }

    [Fact]
    public void FlightOpened_appends_an_initially_empty_flight()
    {
        var entry = Entry.Create(SampleOpened(DateTimeOffset.UtcNow));
        var launchAt = DateTimeOffset.UtcNow;

        var updated = entry.Apply(new FlightOpened(1, launchAt, DateTimeOffset.UtcNow));

        var flight = Assert.Single(updated.Flights);
        Assert.Equal(1, flight.Sequence);
        Assert.Equal(launchAt, flight.LaunchAt);
        Assert.Empty(flight.Measurements);
    }

    [Fact]
    public void FlightOpened_against_no_current_entry_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Entry.Apply(null, new FlightOpened(1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void MeasurementCaptured_appends_a_measurement_to_the_matching_flight()
    {
        var entry = Entry.Create(SampleOpened(DateTimeOffset.UtcNow));
        entry = entry.Apply(new FlightOpened(1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        entry = entry.Apply(new FlightOpened(2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var measurement = new Measurement
        {
            Metric = "flightTime",
            Value = MeasuredValue.Of(123.45m),
            CapturedAt = DateTimeOffset.UtcNow,
        };

        var updated = entry.Apply(new MeasurementCaptured(2, measurement));

        var flightOne = updated.Flights.Single(f => f.Sequence == 1);
        var flightTwo = updated.Flights.Single(f => f.Sequence == 2);
        Assert.Empty(flightOne.Measurements);
        var captured = Assert.Single(flightTwo.Measurements);
        Assert.Equal(measurement, captured);
    }

    [Fact]
    public void MeasurementCaptured_against_no_current_entry_throws()
    {
        var measurement = new Measurement
        {
            Metric = "flightTime",
            Value = MeasuredValue.Of(1m),
            CapturedAt = DateTimeOffset.UtcNow,
        };

        Assert.Throws<ArgumentException>(() =>
            Entry.Apply(null, new MeasurementCaptured(1, measurement)));
    }

    [Fact]
    public void MeasurementAmended_appends_an_amendment_to_the_matching_measurement()
    {
        var entry = Entry.Create(SampleOpened(DateTimeOffset.UtcNow));
        entry = entry.Apply(new FlightOpened(1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var timeMeasurement = new Measurement
        {
            Metric = "flightTime",
            Value = MeasuredValue.Of(100m),
            CapturedAt = DateTimeOffset.UtcNow,
        };
        var landingMeasurement = new Measurement
        {
            Metric = "landingBonus",
            Value = MeasuredValue.Of(true),
            CapturedAt = DateTimeOffset.UtcNow,
        };
        entry = entry.Apply(new MeasurementCaptured(1, timeMeasurement));
        entry = entry.Apply(new MeasurementCaptured(1, landingMeasurement));

        var amendment = new Amendment
        {
            NewValue = MeasuredValue.Of(105m),
            Reason = "timing re-read from video",
            By = "CD",
            At = DateTimeOffset.UtcNow,
        };

        var updated = entry.Apply(new MeasurementAmended(1, "flightTime", amendment));

        var flight = updated.Flights.Single(f => f.Sequence == 1);
        var amendedMeasurement = flight.Measurements.Single(m => m.Metric == "flightTime");
        var untouchedMeasurement = flight.Measurements.Single(m => m.Metric == "landingBonus");

        var appendedAmendment = Assert.Single(amendedMeasurement.Amendments);
        Assert.Equal(amendment, appendedAmendment);
        Assert.Equal(MeasuredValue.Of(100m), amendedMeasurement.Value); // original value untouched, correction appended
        Assert.Empty(untouchedMeasurement.Amendments);
    }

    [Fact]
    public void MeasurementAmended_against_no_current_entry_throws()
    {
        var amendment = new Amendment
        {
            NewValue = MeasuredValue.Of(1m),
            Reason = "n/a",
            By = "n/a",
            At = DateTimeOffset.UtcNow,
        };

        Assert.Throws<ArgumentException>(() =>
            Entry.Apply(null, new MeasurementAmended(1, "flightTime", amendment)));
    }

    [Fact]
    public void EntryAnnulled_sets_the_annulment()
    {
        var entry = Entry.Create(SampleOpened(DateTimeOffset.UtcNow));
        var annulment = new Annulment
        {
            Reason = "provisional re-flight ruling, F3F.1.5",
            By = "Jury",
            At = DateTimeOffset.UtcNow,
        };

        var updated = entry.Apply(new EntryAnnulled(annulment));

        Assert.Equal(annulment, updated.Annulment);
    }

    [Fact]
    public void EntryAnnulled_against_no_current_entry_throws()
    {
        var annulment = new Annulment { Reason = "n/a", By = "n/a", At = DateTimeOffset.UtcNow };

        Assert.Throws<ArgumentException>(() => Entry.Apply(null, new EntryAnnulled(annulment)));
    }

    [Fact]
    public void PenaltyRecorded_appends_to_penalties()
    {
        var entry = Entry.Create(SampleOpened(DateTimeOffset.UtcNow));
        var penalty = new Penalty { InfractionType = "late landing", Scope = PenaltyScope.Flight };

        var updated = entry.Apply(new Entries.PenaltyRecorded(penalty));

        var recorded = Assert.Single(updated.Penalties);
        Assert.Equal(penalty, recorded);
    }

    [Fact]
    public void PenaltyRecorded_against_no_current_entry_throws()
    {
        var penalty = new Penalty { InfractionType = "n/a", Scope = PenaltyScope.Entry };

        Assert.Throws<ArgumentException>(() => Entry.Apply(null, new Entries.PenaltyRecorded(penalty)));
    }

    [Fact]
    public void Apply_dispatcher_throws_for_null_current_on_every_non_creation_event()
    {
        // Belt-and-braces on the dispatcher itself (not just the per-event overloads).
        EntryEvent[] mutationEvents =
        [
            new FlightOpened(1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new MeasurementCaptured(1, new Measurement { Metric = "x", Value = MeasuredValue.Of(1m), CapturedAt = DateTimeOffset.UtcNow }),
            new MeasurementAmended(1, "x", new Amendment { NewValue = MeasuredValue.Of(1m), Reason = "n/a", By = "n/a", At = DateTimeOffset.UtcNow }),
            new EntryAnnulled(new Annulment { Reason = "n/a", By = "n/a", At = DateTimeOffset.UtcNow }),
            new Entries.PenaltyRecorded(new Penalty { InfractionType = "n/a", Scope = PenaltyScope.Flight }),
        ];

        foreach (var @event in mutationEvents)
        {
            Assert.Throws<ArgumentException>(() => Entry.Apply(null, @event));
        }
    }

    [Fact]
    public void A_full_event_stream_folds_in_order_to_the_expected_final_state()
    {
        var openedAt = new DateTimeOffset(2026, 1, 10, 8, 55, 0, TimeSpan.Zero);
        var launchAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var capturedAt = new DateTimeOffset(2026, 1, 10, 9, 4, 0, TimeSpan.Zero);
        var amendedAt = new DateTimeOffset(2026, 1, 10, 9, 30, 0, TimeSpan.Zero);
        var annulledAt = new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero);

        var measurement = new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(240m), CapturedAt = capturedAt };
        var amendment = new Amendment { NewValue = MeasuredValue.Of(238m), Reason = "re-timed", By = "CD", At = amendedAt };
        var annulment = new Annulment { Reason = "outside course", By = "Judge", At = annulledAt };
        var penalty = new Penalty { InfractionType = "boundary violation", Scope = PenaltyScope.Flight };

        EntryEvent[] stream =
        [
            SampleOpened(openedAt),
            new FlightOpened(1, launchAt, launchAt),
            new MeasurementCaptured(1, measurement),
            new MeasurementAmended(1, "flightTime", amendment),
            new Entries.PenaltyRecorded(penalty),
            new EntryAnnulled(annulment),
        ];

        var final = stream.Aggregate((Entry?)null, Entry.Apply);

        Assert.NotNull(final);
        Assert.Equal(SampleId, final!.Id);
        var flight = Assert.Single(final.Flights);
        Assert.Equal(1, flight.Sequence);
        var finalMeasurement = Assert.Single(flight.Measurements);
        Assert.Equal("flightTime", finalMeasurement.Metric);
        Assert.Equal(MeasuredValue.Of(240m), finalMeasurement.Value);
        var finalAmendment = Assert.Single(finalMeasurement.Amendments);
        Assert.Equal(amendment, finalAmendment);
        var finalPenalty = Assert.Single(final.Penalties);
        Assert.Equal(penalty, finalPenalty);
        Assert.Equal(annulment, final.Annulment);
    }
}
