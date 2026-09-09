// kanban/backlog/tape-points-landing-seeds.md WI-3. The capture/amendment
// handler loop with a declared tape, against a FakeEventStore on the real F3J
// seed: a reading and a distance mix in one metric across flights, an
// amendment retains/switches/clears the instrument, and every refusal appends
// nothing. Mirrors CaptureMeasurementHandlerTests.cs's seeding (drawn and
// accepted competition, entry opened through the decide function).

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Entries;

namespace Soarscore.Application.Tests.Commands.Entries;

public class TapeCaptureHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F3J = SeedF3J.Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F3J,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F3J.Version,
            AdoptedAt = Now,
        };

    private static ReadingScale F3JSideScale() =>
        TapeCorpus.All.First(t => t.FileName == "tape-nz-f3j-side").Tape.ToReadingScale();

    private static ImmutableArray<DeclaredInstrument> DeclaredNzF3JSide() =>
        [new DeclaredInstrument { Instrument = "nz-f3j-side", Metric = "landingDistance", Scale = F3JSideScale() }];

    private static (FakeEventStore Store, CompetitionId CompetitionId, EntryId EntryId) SeedOpenEntryWithFlight()
    {
        var store = new FakeEventStore();
        var competitionId = CompetitionId.New();
        var created = new CompetitionCreated(
            competitionId, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();

        var competitor = new Competitor { Id = CompetitorId.New(), PersonRef = PersonId.New(), CompetitorNumber = 1, RegisteredAt = Now };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(1), [new CompetitorRegistered(competitor, Now)]).GetAwaiter().GetResult();

        var groupRef = GroupId.New();
        var group = new Group { Id = groupRef, Ordinal = 1, CompetitorRefs = [competitor.Id] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "D", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = Now, Status = "drawn" };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(2),
            [new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], Now), new DrawAccepted(0, Now)])
            .GetAwaiter().GetResult();

        var competitionEvents = store.Streams[competitionId.Value];
        var competition = competitionEvents.Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
        var opened = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitor.Id, ReflightRole.Original, Now).Value;
        store.AppendAsync(opened.Id.Value, ExpectedVersion.NoStream, [opened]).GetAwaiter().GetResult();

        var flightOpened = new FlightOpened(1, Now.AddMinutes(1));
        store.AppendAsync(opened.Id.Value, ExpectedVersion.Exact(1), [flightOpened]).GetAwaiter().GetResult();

        return (store, competitionId, opened.Id);
    }

    private static async Task DeclareNzF3JSideAsync(FakeEventStore store, CompetitionId competitionId)
    {
        var declared = await new DeclareInstrumentsHandler(store, new FakeClock(Now)).HandleAsync(
            new DeclareInstruments(competitionId, DeclaredNzF3JSide(), "CD Jane"),
            TestContext.Current.CancellationToken);
        declared.IsSuccess.Should().BeTrue($"{declared.Code}: {declared.Message}");
    }

    [Fact]
    public async Task Reading_and_distance_mix_in_one_metric_and_round_trip_with_their_instruments()
    {
        var (store, competitionId, entryId) = SeedOpenEntryWithFlight();
        var ct = TestContext.Current.CancellationToken;
        await DeclareNzF3JSideAsync(store, competitionId);

        var captureHandler = new CaptureMeasurementHandler(store, new FakeClock(Now));

        // Flight 1: a reading off the declared tape — stored verbatim.
        var reading = await captureHandler.HandleAsync(
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(96m), "nz-f3j-side"), ct);
        reading.IsSuccess.Should().BeTrue($"{reading.Code}: {reading.Message}");

        // Flight 2: a distance naming none — the existing path, Truncate 0.1.
        await store.AppendAsync(entryId.Value, ExpectedVersion.Exact(3),
            [new FlightOpened(2, Now.AddMinutes(10))], ct);
        var distance = await captureHandler.HandleAsync(
            new CaptureMeasurement(entryId, 2, "landingDistance", MeasuredValue.Of(1.57m)), ct);
        distance.IsSuccess.Should().BeTrue();

        var stream = store.Streams[entryId.Value];
        var read = stream.OfType<MeasurementCaptured>().First(m => m.FlightSequence == 1);
        read.Measurement.Value.Number.Should().Be(96m);
        read.Measurement.Instrument.Should().Be("nz-f3j-side");
        var distanced = stream.OfType<MeasurementCaptured>().First(m => m.FlightSequence == 2);
        distanced.Measurement.Value.Number.Should().Be(1.5m, "distance capture keeps the metric's declared precision");
        distanced.Measurement.Instrument.Should().BeNull();
    }

    [Fact]
    public async Task Capturing_against_a_competition_with_no_declaration_names_no_instrument()
    {
        // No declaration at all: distances only, exactly as today.
        var (store, _, entryId) = SeedOpenEntryWithFlight();
        var handler = new CaptureMeasurementHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(1.57m)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var captured = store.Streams[entryId.Value].OfType<MeasurementCaptured>().Single();
        captured.Measurement.Value.Number.Should().Be(1.5m);
        captured.Measurement.Instrument.Should().BeNull();
    }

    [Fact]
    public async Task Capturing_naming_an_undeclared_instrument_fails_and_appends_nothing()
    {
        var (store, competitionId, entryId) = SeedOpenEntryWithFlight();
        await DeclareNzF3JSideAsync(store, competitionId);
        var before = store.Streams[entryId.Value].Count;
        var handler = new CaptureMeasurementHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(96m), "nz-f3b-side"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("captureMeasurement.instrumentNotDeclared");
        store.Streams[entryId.Value].Should().HaveCount(before, "an invalid capture appends no event");
    }

    [Fact]
    public async Task Amend_retains_switches_and_clears_the_instrument()
    {
        var (store, competitionId, entryId) = SeedOpenEntryWithFlight();
        var ct = TestContext.Current.CancellationToken;
        await DeclareNzF3JSideAsync(store, competitionId);

        var captureHandler = new CaptureMeasurementHandler(store, new FakeClock(Now));
        var amendHandler = new AmendMeasurementHandler(store, new FakeClock(Now));

        // Capture a distance naming none.
        var captured = await captureHandler.HandleAsync(
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(1.57m)), ct);
        captured.IsSuccess.Should().BeTrue();

        // Omitted instrument retains: still a distance, re-rounded.
        var retained = await amendHandler.HandleAsync(
            new AmendMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(2.22m), "re-taped", "the scorer"), ct);
        retained.IsSuccess.Should().BeTrue();
        var afterRetain = store.Streams[entryId.Value].OfType<MeasurementAmended>().Single();
        afterRetain.Amendment.Instrument.Should().BeNull();
        afterRetain.Amendment.NewValue.Number.Should().Be(2.2m);

        // Named instrument switches to a reading.
        var switched = await amendHandler.HandleAsync(
            new AmendMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(91m),
                "was read off the tape", "the scorer", "nz-f3j-side", ChangeInstrument: true), ct);
        switched.IsSuccess.Should().BeTrue();
        store.Streams[entryId.Value].OfType<MeasurementAmended>().Last().Amendment.Instrument.Should().Be("nz-f3j-side");

        // Explicit clear back to none: a distance again.
        var cleared = await amendHandler.HandleAsync(
            new AmendMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(3.33m),
                "was taped after all", "the scorer", null, ChangeInstrument: true), ct);
        cleared.IsSuccess.Should().BeTrue();
        var clearAmendment = store.Streams[entryId.Value].OfType<MeasurementAmended>().Last().Amendment;
        clearAmendment.Instrument.Should().BeNull();
        clearAmendment.NewValue.Number.Should().Be(3.3m);

        // The folded entry's correction history is retained whole.
        var entry = store.Streams[entryId.Value]
            .Aggregate((Entry?)null, (current, e) => Entry.Apply(current, (EntryEvent)e))!;
        var measurement = entry.Flights.Single().Measurements.Single(m => m.Metric == "landingDistance");
        measurement.Amendments.Should().HaveCount(3);
        measurement.EffectiveInstrument.Should().BeNull();
    }

    [Fact]
    public async Task Amend_naming_an_undeclared_instrument_fails_and_appends_nothing()
    {
        var (store, competitionId, entryId) = SeedOpenEntryWithFlight();
        var ct = TestContext.Current.CancellationToken;
        await DeclareNzF3JSideAsync(store, competitionId);

        var captured = await new CaptureMeasurementHandler(store, new FakeClock(Now)).HandleAsync(
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(96m), "nz-f3j-side"), ct);
        captured.IsSuccess.Should().BeTrue();
        var before = store.Streams[entryId.Value].Count;

        var result = await new AmendMeasurementHandler(store, new FakeClock(Now)).HandleAsync(
            new AmendMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(96m),
                "guess", "the scorer", "nz-f3b-side", ChangeInstrument: true), ct);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("amendMeasurement.instrumentNotDeclared");
        store.Streams[entryId.Value].Should().HaveCount(before, "an invalid amendment appends no event");
    }
}
