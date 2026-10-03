// ss_entry-measurements-read: GetEntry folds one stream and projects
// flights/measurements with current values and amendments. Mirrors
// Competitions/GetCompetitionHandlerTests.cs's found/not-found style.

using AwesomeAssertions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

using Soarscore.Application.Tests.Shared.Entries;

namespace Soarscore.Application.Tests.Queries.Entries;

public class GetEntryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private static EntryOpened SampleOpened(EntryId id) =>
        new(
            id,
            CompetitionId.New(),
            0,
            5,
            1,
            GroupId.New(),
            CompetitorId.New(),
            ReflightRole.Original,
            Now);

    private static Measurement SampleMeasurement(string metric, decimal number, DateTimeOffset at) =>
        new()
        {
            Metric = metric,
            Value = MeasuredValue.Of(number),
            Instrument = null,
            CapturedAt = at,
        };

    [Fact]
    public async Task GetEntry_for_an_existing_stream_returns_coordinate_and_flights()
    {
        var id = EntryId.New();
        var store = new FakeEventStore();
        await store.AppendAsync(id.Value, ExpectedVersion.NoStream, [SampleOpened(id)], TestContext.Current.CancellationToken);
        await store.AppendAsync(
            id.Value,
            ExpectedVersion.Exact(1),
            [new FlightOpened(1, Now.AddMinutes(1))],
            TestContext.Current.CancellationToken);
        await store.AppendAsync(
            id.Value,
            ExpectedVersion.Exact(2),
            [new MeasurementCaptured(1, SampleMeasurement("flightTime", 512, Now.AddMinutes(2)))],
            TestContext.Current.CancellationToken);
        var handler = new GetEntryHandler(store);

        var result = await handler.HandleAsync(new GetEntry(id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(id);
        result.Value.Annulment.Should().BeNull();
        result.Value.Flights.Should().ContainSingle().Which.Sequence.Should().Be(1);
        var measurement = result.Value.Flights[0].Measurements.Should().ContainSingle().Which;
        measurement.Metric.Should().Be("flightTime");
        measurement.Value.Number.Should().Be(512);
        measurement.Original.Number.Should().Be(512);
        measurement.Amendments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntry_for_an_unknown_id_fails_with_entry_notFound()
    {
        var handler = new GetEntryHandler(new FakeEventStore());

        var result = await handler.HandleAsync(new GetEntry(EntryId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.notFound");
    }

    [Fact]
    public async Task GetEntry_shows_amended_measurement_with_current_value_and_history()
    {
        var id = EntryId.New();
        var store = new FakeEventStore();
        await store.AppendAsync(id.Value, ExpectedVersion.NoStream, [SampleOpened(id)], TestContext.Current.CancellationToken);
        await store.AppendAsync(
            id.Value, ExpectedVersion.Exact(1), [new FlightOpened(1, Now)], TestContext.Current.CancellationToken);
        await store.AppendAsync(
            id.Value,
            ExpectedVersion.Exact(2),
            [new MeasurementCaptured(1, SampleMeasurement("flightTime", 215, Now.AddMinutes(1)))],
            TestContext.Current.CancellationToken);
        var amendment = new Amendment
        {
            NewValue = MeasuredValue.Of(512),
            Instrument = null,
            Reason = "Mis-keyed",
            By = "Pete",
            At = Now.AddMinutes(2),
        };
        await store.AppendAsync(
            id.Value,
            ExpectedVersion.Exact(3),
            [new MeasurementAmended(1, "flightTime", amendment)],
            TestContext.Current.CancellationToken);
        var handler = new GetEntryHandler(store);

        var result = await handler.HandleAsync(new GetEntry(id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var measurement = result.Value.Flights.Should().ContainSingle().Which
            .Measurements.Should().ContainSingle().Which;
        measurement.Value.Number.Should().Be(512);
        measurement.Original.Number.Should().Be(215);
        measurement.Amendments.Should().ContainSingle().Which.Should().Be(amendment);
    }

    [Fact]
    public async Task GetEntry_includes_empty_flights_and_annulment()
    {
        var id = EntryId.New();
        var store = new FakeEventStore();
        await store.AppendAsync(id.Value, ExpectedVersion.NoStream, [SampleOpened(id)], TestContext.Current.CancellationToken);
        await store.AppendAsync(
            id.Value, ExpectedVersion.Exact(1), [new FlightOpened(2, Now)], TestContext.Current.CancellationToken);
        await store.AppendAsync(
            id.Value, ExpectedVersion.Exact(2), [new FlightOpened(1, Now.AddMinutes(1))], TestContext.Current.CancellationToken);
        var annulment = new Annulment { Reason = "Protest upheld", By = "Jury", At = Now.AddMinutes(2) };
        await store.AppendAsync(
            id.Value, ExpectedVersion.Exact(3), [new EntryAnnulled(annulment)], TestContext.Current.CancellationToken);
        var handler = new GetEntryHandler(store);

        var result = await handler.HandleAsync(new GetEntry(id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Annulment.Should().Be(annulment);
        result.Value.Flights.Select(f => f.Sequence).Should().Equal(1, 2);
        result.Value.Flights.Should().OnlyContain(f => f.Measurements.IsEmpty);
    }
}
