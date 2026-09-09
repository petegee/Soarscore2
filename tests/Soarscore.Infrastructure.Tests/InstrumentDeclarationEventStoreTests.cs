// kanban/backlog/tape-points-landing-seeds.md WI-3 — the store-backed proof
// that declaration, mixed-form capture, amendment and correction round-trip
// with their instruments on BOTH backends. Generic over the fixture
// (IStoreFixture), run unchanged against Marten/PostgreSQL (Trait Category
// Storage) and Fisher/SQLite — the EntryCaptureEventStoreTests pattern: real
// handlers, raw-stream reads folded with the public Apply, no dispatcher.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class InstrumentDeclarationEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly ClassDefinition F3JDefinition = Corpus.All.Single(c => c.FileName == "50-f3j").Definition;

    private static ReadingScale F3JSideScale() =>
        TapeCorpus.All.First(t => t.FileName == "tape-nz-f3j-side").Tape.ToReadingScale();

    private static ReadingScale AlesMetreScale() =>
        TapeCorpus.All.First(t => t.FileName == "tape-nz-ales-m-10m").Tape.ToReadingScale();

    private static async Task<CompetitionId> CreateCompetitionAsync(IStoreFixture fixture)
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(
            new PublishClassDefinition(F3JDefinition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition("Tape WI-3", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            TestContext.Current.CancellationToken);
        created.IsSuccess.Should().BeTrue();

        return created.Value;
    }

    private static async Task<EntryId> SeedEntryWithTwoFlightsAsync(
        IStoreFixture fixture, CompetitionId competitionId, string tag)
    {
        for (var i = 0; i < 6; i++)
        {
            var person = await new RegisterPersonHandler(fixture.EventStore, new SystemClock()).HandleAsync(
                new RegisterPerson("Test Pilot", new ContactDetails { Email = $"pilot-tape-{tag}-{i}@example.com" }, Club: null),
                TestContext.Current.CancellationToken);
            person.IsSuccess.Should().BeTrue();
            var registered = await new RegisterCompetitorHandler(fixture.EventStore, new SystemClock()).HandleAsync(
                new RegisterCompetitor(competitionId, person.Value), TestContext.Current.CancellationToken);
            registered.IsSuccess.Should().BeTrue();
        }

        var drawn = await new DrawPhaseHandler(fixture.EventStore, new SystemClock()).HandleAsync(
            new DrawPhase(competitionId, 1), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue($"{drawn.Code}: {drawn.Message}");
        var accepted = await new AcceptDrawHandler(fixture.EventStore, new SystemClock()).HandleAsync(
            new AcceptDraw(competitionId), TestContext.Current.CancellationToken);
        accepted.IsSuccess.Should().BeTrue();

        var fetched = await new GetCompetitionHandler(fixture.EventStore).HandleAsync(
            new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();
        var group1 = fetched.Value.Competition.Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single();

        var opened = await new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock()).HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, group1.Id, group1.CompetitorRefs[0]),
            TestContext.Current.CancellationToken);
        opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");

        var flights = new OpenFlightHandler(fixture.EventStore, new SystemClock());
        var first = await flights.HandleAsync(new OpenFlight(opened.Value), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();
        var second = await flights.HandleAsync(new OpenFlight(opened.Value), TestContext.Current.CancellationToken);
        second.IsSuccess.Should().BeTrue();

        return opened.Value;
    }

    private static async Task<Entry> LoadEntryAsync(IStoreFixture fixture, EntryId id)
    {
        var read = await fixture.EventStore.ReadStreamAsync(id.Value, 0, TestContext.Current.CancellationToken);
        read.IsSuccess.Should().BeTrue();
        return read.Value.Aggregate((Entry?)null, (current, e) => Entry.Apply(current, (EntryEvent)e))!;
    }

    private static async Task<Competition> LoadCompetitionAsync(IStoreFixture fixture, CompetitionId id)
    {
        var read = await fixture.EventStore.ReadStreamAsync(id.Value, 0, TestContext.Current.CancellationToken);
        read.IsSuccess.Should().BeTrue();
        return read.Value.Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
    }

    [Fact]
    public async Task Declared_reading_and_distance_round_trip_with_instruments_intact()
    {
        var competitionId = await CreateCompetitionAsync(fixture);
        var entryId = await SeedEntryWithTwoFlightsAsync(fixture, competitionId, "roundtrip");
        var ct = TestContext.Current.CancellationToken;

        var declared = await new DeclareInstrumentsHandler(fixture.EventStore, new SystemClock()).HandleAsync(
            new DeclareInstruments(competitionId,
                [new DeclaredInstrument { Instrument = "nz-f3j-side", Metric = "landingDistance", Scale = F3JSideScale() }],
                "CD Jane"),
            ct);
        declared.IsSuccess.Should().BeTrue($"{declared.Code}: {declared.Message}");

        var captureHandler = new CaptureMeasurementHandler(fixture.EventStore, new SystemClock());

        // Flight 1: a reading off the declared tape — stored verbatim, never rounded.
        var reading = await captureHandler.HandleAsync(
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(96m), "nz-f3j-side"), ct);
        reading.IsSuccess.Should().BeTrue($"{reading.Code}: {reading.Message}");

        // Flight 2: a distance naming none — the existing path, precision kept.
        var distance = await captureHandler.HandleAsync(
            new CaptureMeasurement(entryId, 2, "landingDistance", MeasuredValue.Of(1.57m)), ct);
        distance.IsSuccess.Should().BeTrue();

        // An amendment retaining the instrument (the ordinary correction).
        var amended = await new AmendMeasurementHandler(fixture.EventStore, new SystemClock()).HandleAsync(
            new AmendMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(95m), "misread", "the scorer"), ct);
        amended.IsSuccess.Should().BeTrue();

        var entry = await LoadEntryAsync(fixture, entryId);
        var read = entry.Flights.Single(f => f.Sequence == 1).Measurements.Single(m => m.Metric == "landingDistance");
        read.Value.Number.Should().Be(96m);
        read.Instrument.Should().Be("nz-f3j-side");
        read.Amendments.Should().ContainSingle();
        read.Amendments[0].Instrument.Should().Be("nz-f3j-side", "an amendment restates the effective instrument");
        read.Amendments[0].NewValue.Number.Should().Be(95m);
        read.EffectiveInstrument.Should().Be("nz-f3j-side");

        var distanced = entry.Flights.Single(f => f.Sequence == 2).Measurements.Single(m => m.Metric == "landingDistance");
        distanced.Value.Number.Should().Be(1.5m);
        distanced.Instrument.Should().BeNull();

        var competition = await LoadCompetitionAsync(fixture, competitionId);
        competition.DeclaredInstruments.Should().NotBeNull();
        competition.DeclaredInstruments!.Instruments.Should().ContainSingle();

        var indexed = await fixture.EntryQuery.FindAsync(
            competitionId, 0, 1, 1, null, null, ct);
        indexed.Should().ContainSingle(e => e.Id == entryId, "the read models are untouched by instruments");
    }

    [Fact]
    public async Task Corrected_declaration_replaces_the_set_while_measurements_stay_self_describing()
    {
        var competitionId = await CreateCompetitionAsync(fixture);
        var entryId = await SeedEntryWithTwoFlightsAsync(fixture, competitionId, "corrected");
        var ct = TestContext.Current.CancellationToken;

        var declared = await new DeclareInstrumentsHandler(fixture.EventStore, new SystemClock()).HandleAsync(
            new DeclareInstruments(competitionId,
                [new DeclaredInstrument { Instrument = "nz-f3j-side", Metric = "landingDistance", Scale = F3JSideScale() }],
                "CD Jane"),
            ct);
        declared.IsSuccess.Should().BeTrue();

        var captured = await new CaptureMeasurementHandler(fixture.EventStore, new SystemClock()).HandleAsync(
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(96m), "nz-f3j-side"), ct);
        captured.IsSuccess.Should().BeTrue();

        // The tapes never arrived: correct back to the empty set.
        var corrected = await new CorrectInstrumentDeclarationHandler(fixture.EventStore, new SystemClock()).HandleAsync(
            new CorrectInstrumentDeclaration(competitionId, [], "the tapes never arrived", "CD Jane"), ct);
        corrected.IsSuccess.Should().BeTrue();

        // The declaration is replaced whole — retroactive by re-derivation —
        // while the measurement still names the instrument it was read on: an
        // audit never depends on the current declaration.
        var competition = await LoadCompetitionAsync(fixture, competitionId);
        competition.DeclaredInstruments!.Instruments.Should().BeEmpty();

        var entry = await LoadEntryAsync(fixture, entryId);
        entry.Flights.Single(f => f.Sequence == 1).Measurements.Single(m => m.Metric == "landingDistance")
            .Instrument.Should().Be("nz-f3j-side");
    }

    [Fact]
    public async Task Refused_declaration_and_capture_append_nothing()
    {
        var competitionId = await CreateCompetitionAsync(fixture);
        var entryId = await SeedEntryWithTwoFlightsAsync(fixture, competitionId, "refused");
        var ct = TestContext.Current.CancellationToken;

        // ALES M's metre tape cannot score F3J: boundary 0.2 sits inside band
        // (0, 1], straddling 100 vs 99.
        var refused = await new DeclareInstrumentsHandler(fixture.EventStore, new SystemClock()).HandleAsync(
            new DeclareInstruments(competitionId,
                [new DeclaredInstrument { Instrument = "ales-metre", Metric = "landingDistance", Scale = AlesMetreScale() }],
                "CD Jane"),
            ct);
        refused.IsFailure.Should().BeTrue();
        refused.Code.Should().Be("tapeComposition.straddledBand");
        (await LoadCompetitionAsync(fixture, competitionId)).DeclaredInstruments.Should().BeNull();

        // With nothing declared, naming an instrument is refused — and the
        // entry stream is untouched.
        var entryReadBefore = await fixture.EventStore.ReadStreamAsync(
            entryId.Value, 0, TestContext.Current.CancellationToken);
        entryReadBefore.IsSuccess.Should().BeTrue();
        var badCapture = await new CaptureMeasurementHandler(fixture.EventStore, new SystemClock()).HandleAsync(
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(96m), "nz-f3j-side"), ct);
        badCapture.IsFailure.Should().BeTrue();
        badCapture.Code.Should().Be("captureMeasurement.instrumentNotDeclared");
        var entryReadAfter = await fixture.EventStore.ReadStreamAsync(
            entryId.Value, 0, TestContext.Current.CancellationToken);
        entryReadAfter.IsSuccess.Should().BeTrue();
        entryReadAfter.Value.Should().Equal(entryReadBefore.Value, "an invalid capture appends no event");
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresInstrumentDeclarationEventStoreTests(PostgresFixture fixture)
    : InstrumentDeclarationEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteInstrumentDeclarationEventStoreTests(SqliteFixture fixture)
    : InstrumentDeclarationEventStoreTests<SqliteFixture>(fixture);
