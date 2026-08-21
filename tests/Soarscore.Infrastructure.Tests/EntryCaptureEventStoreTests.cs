// kanban/completed/capture-a-score-steel-thread-plan.md WI-12 — the store-backed
// tests for the Entry write path and `entry_index`, against a real
// PostgreSQL via Testcontainers rather than the FakeEventStore double the
// Application-layer handler tests (tests/Soarscore.Application.Tests/Entries/)
// use. Same style as DrawPhaseEventStoreTests.cs / BindParameterEventStoreTests.cs:
// calls the real handlers directly against fixture.EventStore/fixture.EntryQuery,
// no dispatcher needed for a store-level test.
//
// kanban/completed/multi-backend-deployment.md WI-6 made these generic over
// the fixture, so they now run unchanged against every backend Soarscore
// supports — Marten/PostgreSQL and Fisher/SQLite — one concrete subclass per
// backend at the foot of the file. Only the Postgres subclass keeps
// Trait("Category", "Storage"); EventStoreTests.cs's header says why.
//
// No GetEntry query exists yet (EntrySummary.cs's doc comment: "a future work
// item, mirroring GetCompetition") — every assertion about a folded Entry's
// full state reads the raw stream via fixture.EventStore.ReadStreamAsync and
// folds it with the public Entry.Apply, the same shape EntryLoader uses
// internally (that type is `internal` to Soarscore.Application and not
// visible here).
//
// Tests 3 and 4 are the payoff test the whole plan is building towards, run
// twice: once under a Fixed-timing class (F5J) and once under NZ Class M
// ALES 200 — UntilAllFlightsComplete AND parameter-bound (groupSize). Scenario
// 4's point survives the removal of the stored window (remove-stored-working-time.md
// WI-3): the draw resolves the groupSize binding, so groupSize must be bound
// before any entry can even be drawn and opened, and once it is, the open
// succeeds under UntilAllFlightsComplete timing (whose WorkingTime is null —
// no Fixed-time arm fires for it).

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

public abstract class EntryCaptureEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    // F5J: Fixed timing, literal (non-parameterised) Group.MinPerGroup == 6,
    // single FixedSequence/TasksPerRound==1 task per phase — the shape
    // Competition.DrawPhase requires, same choice DrawPhaseEventStoreTests
    // makes. Used for the entry-stream round-trip, the entry_index filter
    // test, the replay test and payoff scenario 3 (Fixed timing).
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    // NZ Class M ALES 200 — UntilAllFlightsComplete timing (WorkingTime is
    // null, not a class datum) AND groupSize is a bound parameter with no
    // default (bind-parameter-steel-thread-plan.md). Payoff scenario 4.
    private static readonly ClassDefinition NzMAles200Definition = Corpus.All.Single(c => c.FileName == "80-nz-m-ales200").Definition;

    private static async Task<CompetitionId> CreateCompetitionAsync(IStoreFixture fixture, ClassDefinition definition, string name)
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(definition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition(name, "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            TestContext.Current.CancellationToken);
        created.IsSuccess.Should().BeTrue();

        return created.Value;
    }

    private static async Task<PersonId> RegisterPersonAsync(IStoreFixture fixture, string email)
    {
        var registerHandler = new RegisterPersonHandler(fixture.EventStore, new SystemClock());
        var registered = await registerHandler.HandleAsync(
            new RegisterPerson("Test Pilot", new ContactDetails { Email = email }, Club: null),
            TestContext.Current.CancellationToken);
        registered.IsSuccess.Should().BeTrue();

        return registered.Value;
    }

    private static async Task<CompetitorId> RegisterCompetitorAsync(IStoreFixture fixture, CompetitionId competitionId, string email)
    {
        var personId = await RegisterPersonAsync(fixture, email);
        var registerHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var registered = await registerHandler.HandleAsync(
            new RegisterCompetitor(competitionId, personId), TestContext.Current.CancellationToken);
        registered.IsSuccess.Should().BeTrue();

        return registered.Value;
    }

    /// <summary>
    /// Reads the raw Entry stream and folds it with the public <see cref="Entry.Apply(Entry?, EntryEvent)"/> —
    /// the same shape Application's internal EntryLoader uses, inlined here
    /// because that type is not visible outside Soarscore.Application and no
    /// GetEntry query exists yet.
    /// </summary>
    private static async Task<Entry> LoadEntryAsync(IStoreFixture fixture, EntryId id)
    {
        var read = await fixture.EventStore.ReadStreamAsync(id.Value, 0, TestContext.Current.CancellationToken);
        read.IsSuccess.Should().BeTrue();
        return read.Value.Aggregate((Entry?)null, (current, e) => Entry.Apply(current, (EntryEvent)e))!;
    }

    // ---- 1. An Entry stream round-trips through PostgreSQL ------------------

    [Fact]
    public async Task EntryStream_open_three_flights_measurements_on_each_round_trips_and_folds_back_identical()
    {
        var entryId = EntryId.New();
        var competitionRef = CompetitionId.New();
        var groupRef = GroupId.New();
        var competitorRef = CompetitorId.New();
        var openedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

        var opened = new EntryOpened(
            entryId,
            competitionRef,
            1,
            1,
            1,
            groupRef,
            competitorRef,
            ReflightRole.Original,
            openedAt);

        var openAppend = await fixture.EventStore.AppendAsync(
            entryId.Value, ExpectedVersion.NoStream, [opened], TestContext.Current.CancellationToken);
        openAppend.IsSuccess.Should().BeTrue();

        var flight1 = new FlightOpened(1, openedAt.AddMinutes(1));
        var measurement1 = new MeasurementCaptured(
            1, new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(180m), CapturedAt = openedAt.AddMinutes(2) });
        var flight2 = new FlightOpened(2, openedAt.AddMinutes(3));
        var measurement2 = new MeasurementCaptured(
            2, new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(210m), CapturedAt = openedAt.AddMinutes(5) });
        var flight3 = new FlightOpened(3, openedAt.AddMinutes(6));
        var measurement3 = new MeasurementCaptured(
            3, new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(240m), CapturedAt = openedAt.AddMinutes(8) });

        var restAppend = await fixture.EventStore.AppendAsync(
            entryId.Value,
            ExpectedVersion.Exact(1),
            [flight1, measurement1, flight2, measurement2, flight3, measurement3],
            TestContext.Current.CancellationToken);
        restAppend.IsSuccess.Should().BeTrue();

        var read = await fixture.EventStore.ReadStreamAsync(entryId.Value, 0, TestContext.Current.CancellationToken);
        read.IsSuccess.Should().BeTrue();
        read.Value.Should().Equal(opened, flight1, measurement1, flight2, measurement2, flight3, measurement3);

        var entry = await LoadEntryAsync(fixture, entryId);
        entry.Id.Should().Be(entryId);
        entry.CompetitionRef.Should().Be(competitionRef);
        entry.GroupRef.Should().Be(groupRef);
        entry.CompetitorRef.Should().Be(competitorRef);
        entry.Role.Should().Be(ReflightRole.Original);
        entry.Annulment.Should().BeNull();

        entry.Flights.Should().HaveCount(3);
        entry.Flights.Select(f => f.Sequence).Should().Equal(1, 2, 3);

        foreach (var flight in entry.Flights)
        {
            flight.Measurements.Should().ContainSingle(m => m.Metric == "flightTime");
        }

        entry.Flights[0].Measurements[0].Value.Should().Be(MeasuredValue.Of(180m));
        entry.Flights[1].Measurements[0].Value.Should().Be(MeasuredValue.Of(210m));
        entry.Flights[2].Measurements[0].Value.Should().Be(MeasuredValue.Of(240m));
    }

    // ---- 2. entry_index queryable by every filter IEntryQuery exposes -------

    [Fact]
    public async Task EntryIndex_is_populated_inline_and_queryable_by_every_filter_combination()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F5JDefinition, "Entry Index Filters");

        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 12; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-index-{i}@example.com"));
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 2), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        var rounds = fetched.Value.Competition.Phases.Single().Rounds;
        var round1 = rounds.Single(r => r.Ordinal == 1);
        var round2 = rounds.Single(r => r.Ordinal == 2);
        var round1Group1 = round1.TaskRounds.Single().Groups.Single(g => g.Ordinal == 1);
        var round1Group2 = round1.TaskRounds.Single().Groups.Single(g => g.Ordinal == 2);
        var round2Group1 = round2.TaskRounds.Single().Groups.Single(g => g.Ordinal == 1);

        var openEntryHandler = new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());

        // Round 1 and round 2 are drawn independently, so a round2Group1
        // competitor can coincidentally be the very pilot already opened as
        // entryA/entryB in round 1 — legitimate (a different task-round is a
        // different coordinate) but it would make the "competitorRef alone"
        // filter below ambiguous between two real entries. Picking a
        // round2Group1 competitor distinct from entryA's and entryB's keeps
        // that assertion deterministic; round2Group1 has 6 members, so at
        // least 4 remain after excluding two.
        var entryACompetitor = round1Group1.CompetitorRefs[0];
        var entryBCompetitor = round1Group2.CompetitorRefs[0];
        var entryCCompetitor = round2Group1.CompetitorRefs.First(c => c != entryACompetitor && c != entryBCompetitor);

        // Phase.Ordinal is Phases.Length AT THE TIME OF THE DRAW (Competition.cs's
        // DrawPhase), so the first (and, in this thread, only) phase drawn is
        // Ordinal 0 — unlike Round/TaskRound/Group, which are 1-based.
        var entryA = await openEntryHandler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, round1Group1.Id, entryACompetitor), TestContext.Current.CancellationToken);
        entryA.IsSuccess.Should().BeTrue($"{entryA.Code}: {entryA.Message}");

        var entryB = await openEntryHandler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, round1Group2.Id, entryBCompetitor), TestContext.Current.CancellationToken);
        entryB.IsSuccess.Should().BeTrue();

        var entryC = await openEntryHandler.HandleAsync(
            new OpenEntry(competitionId, 0, 2, 1, round2Group1.Id, entryCCompetitor), TestContext.Current.CancellationToken);
        entryC.IsSuccess.Should().BeTrue();

        // CompetitionId only — every entry opened under this competition.
        var all = await fixture.EntryQuery.FindAsync(competitionId, null, null, null, null, null, TestContext.Current.CancellationToken);
        all.Select(e => e.Id).Should().BeEquivalentTo([entryA.Value, entryB.Value, entryC.Value]);

        // + phaseOrdinal — every entry is in phase 0, so still all three.
        var byPhase = await fixture.EntryQuery.FindAsync(competitionId, 0, null, null, null, null, TestContext.Current.CancellationToken);
        byPhase.Select(e => e.Id).Should().BeEquivalentTo([entryA.Value, entryB.Value, entryC.Value]);

        // + roundOrdinal — narrows to round 1's two entries.
        var byRound = await fixture.EntryQuery.FindAsync(competitionId, 0, 1, null, null, null, TestContext.Current.CancellationToken);
        byRound.Select(e => e.Id).Should().BeEquivalentTo([entryA.Value, entryB.Value]);

        // + taskRoundOrdinal on top of phase/round — same two, proves the filter is honoured.
        var byTaskRound = await fixture.EntryQuery.FindAsync(competitionId, 0, 1, 1, null, null, TestContext.Current.CancellationToken);
        byTaskRound.Select(e => e.Id).Should().BeEquivalentTo([entryA.Value, entryB.Value]);

        // roundOrdinal alone (no phase) — round 2's single entry.
        var byRoundOnly = await fixture.EntryQuery.FindAsync(competitionId, null, 2, null, null, null, TestContext.Current.CancellationToken);
        byRoundOnly.Select(e => e.Id).Should().BeEquivalentTo([entryC.Value]);

        // + groupRef, fully coordinate-qualified — exactly entry A.
        var byGroup = await fixture.EntryQuery.FindAsync(
            competitionId, 0, 1, 1, round1Group1.Id, null, TestContext.Current.CancellationToken);
        byGroup.Select(e => e.Id).Should().BeEquivalentTo([entryA.Value]);

        // groupRef alone (no phase/round/taskRound) — still exactly entry B.
        var byGroupOnly = await fixture.EntryQuery.FindAsync(
            competitionId, null, null, null, round1Group2.Id, null, TestContext.Current.CancellationToken);
        byGroupOnly.Select(e => e.Id).Should().BeEquivalentTo([entryB.Value]);

        // + competitorRef alone — exactly the entry opened for that competitor.
        var byCompetitor = await fixture.EntryQuery.FindAsync(
            competitionId, null, null, null, null, entryCCompetitor, TestContext.Current.CancellationToken);
        byCompetitor.Select(e => e.Id).Should().BeEquivalentTo([entryC.Value]);

        // Every filter set at once — the shape OpenEntryHandler's own
        // openEntry.alreadyOpen check uses.
        var fullyQualified = await fixture.EntryQuery.FindAsync(
            competitionId, 0, 1, 1, round1Group1.Id, entryACompetitor, TestContext.Current.CancellationToken);
        fullyQualified.Select(e => e.Id).Should().BeEquivalentTo([entryA.Value]);

        // A competitor never opened returns nothing — deliberately excludes
        // all three opened competitors (not just entryA's/entryB's) in case
        // round 1 and round 2's independent draws happened to reuse one.
        var openedCompetitors = new HashSet<CompetitorId> { entryACompetitor, entryBCompetitor, entryCCompetitor };
        var neverOpenedCompetitor = round1Group1.CompetitorRefs
            .Concat(round1Group2.CompetitorRefs)
            .First(c => !openedCompetitors.Contains(c));
        var none = await fixture.EntryQuery.FindAsync(
            competitionId, null, null, null, null, neverOpenedCompetitor, TestContext.Current.CancellationToken);
        none.Should().BeEmpty();
    }

    // ---- 3 & 4. The payoff test, run under Fixed timing and under NZ-M -----

    /// <summary>
    /// Adopt a real seed definition, register a field, draw, open an Entry
    /// per drawn competitor in group 1, capture a flightTime for each —
    /// against a real store, end to end. Shared by both payoff scenarios;
    /// <paramref name="bindGroupSizeTo"/> is non-null only for NZ Class M
    /// ALES 200, whose groupSize parameter has no default.
    /// </summary>
    private static async Task RunPayoffCaptureScenarioAsync(
        IStoreFixture fixture,
        ClassDefinition definition,
        string competitionName,
        decimal? bindGroupSizeTo)
    {
        var competitionId = await CreateCompetitionAsync(fixture, definition, competitionName);

        if (bindGroupSizeTo is { } groupSize)
        {
            var bindHandler = new BindParameterHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
            var bound = await bindHandler.HandleAsync(
                new BindParameter(competitionId, "groupSize", MeasuredValue.Of(groupSize), "CD Jane"),
                TestContext.Current.CancellationToken);
            bound.IsSuccess.Should().BeTrue();
        }

        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 6; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-payoff-{competitionName.Replace(" ", "-")}-{i}@example.com"));
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 1), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        var taskRound = fetched.Value.Competition.Phases.Single().Rounds.Single().TaskRounds.Single();
        var group1 = taskRound.Groups.Single(g => g.Ordinal == 1);
        group1.CompetitorRefs.Should().HaveCount(6);
        group1.CompetitorRefs.Should().BeEquivalentTo(competitorIds);

        var openEntryHandler = new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
        var openFlightHandler = new OpenFlightHandler(fixture.EventStore, new SystemClock());
        var captureHandler = new CaptureMeasurementHandler(fixture.EventStore, new SystemClock());

        foreach (var competitorRef in group1.CompetitorRefs)
        {
            // Phase.Ordinal is Phases.Length at draw time — 0 for the first
            // (and, in this thread, only) phase drawn, unlike the 1-based
            // Round/TaskRound/Group ordinals.
            var opened = await openEntryHandler.HandleAsync(
                new OpenEntry(competitionId, 0, 1, 1, group1.Id, competitorRef), TestContext.Current.CancellationToken);
            opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");
            var entryId = opened.Value;

            var openedFlight = await openFlightHandler.HandleAsync(
                new OpenFlight(entryId), TestContext.Current.CancellationToken);
            openedFlight.IsSuccess.Should().BeTrue();

            // flightTime is truncated to the nearest whole second on both
            // seed classes (RoundingMode.Truncate, precision 1) — 412.9
            // proves finding 4's capture-time rounding is applied, not just
            // that a raw value was stored unchanged.
            var captured = await captureHandler.HandleAsync(
                new CaptureMeasurement(entryId, 1, "flightTime", MeasuredValue.Of(412.9m)),
                TestContext.Current.CancellationToken);
            captured.IsSuccess.Should().BeTrue();

            var entry = await LoadEntryAsync(fixture, entryId);
            entry.CompetitorRef.Should().Be(competitorRef);
            entry.Flights.Should().ContainSingle();
            entry.Flights[0].Measurements.Should().ContainSingle(m => m.Metric == "flightTime");
            entry.Flights[0].Measurements[0].Value.Should().Be(MeasuredValue.Of(412m));

            var indexed = await fixture.EntryQuery.FindAsync(
                competitionId, 0, 1, 1, group1.Id, competitorRef, TestContext.Current.CancellationToken);
            indexed.Should().ContainSingle(e => e.Id == entryId);
        }
    }

    [Fact]
    public async Task Payoff_capture_end_to_end_under_Fixed_timing_F5J()
    {
        await RunPayoffCaptureScenarioAsync(
            fixture, F5JDefinition, "Payoff F5J", bindGroupSizeTo: null);
    }

    [Fact]
    public async Task Payoff_capture_end_to_end_under_NZ_Class_M_ALES_200_UntilAllFlightsComplete_and_parameter_bound()
    {
        await RunPayoffCaptureScenarioAsync(
            fixture, NzMAles200Definition, "Payoff NZ M", bindGroupSizeTo: 6m);
    }

    // ---- 5. entry_index dropped and fully replayed lands identical ---------

    [Fact]
    public async Task EntryIndex_dropped_and_fully_replayed_lands_identical()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F5JDefinition, "Entry Index Replay");

        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 6; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-replay-index-{i}@example.com"));
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 1), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();
        var group1 = fetched.Value.Competition.Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single(g => g.Ordinal == 1);

        var openEntryHandler = new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
        var openedIds = new List<EntryId>();
        foreach (var competitorRef in group1.CompetitorRefs)
        {
            var opened = await openEntryHandler.HandleAsync(
                new OpenEntry(competitionId, 0, 1, 1, group1.Id, competitorRef), TestContext.Current.CancellationToken);
            opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");
            openedIds.Add(opened.Value);
        }

        var before = await fixture.EntryQuery.FindAsync(competitionId, null, null, null, null, null, TestContext.Current.CancellationToken);
        before.Select(e => e.Id).Should().BeEquivalentTo(openedIds);

        // Drop the read model's data only — the event log, and therefore a
        // fresh fold of any Entry stream, is untouched (LADR-0001 §4.10).
        await fixture.DropDocumentsAsync<EntrySummary>(TestContext.Current.CancellationToken);

        var afterDrop = await fixture.EntryQuery.FindAsync(competitionId, null, null, null, null, null, TestContext.Current.CancellationToken);
        afterDrop.Should().BeEmpty();

        // Replay the whole log through the same Inline projection, on demand
        // — never the continuously-running async daemon (LADR-0001 §2).
        await fixture.RebuildProjectionAsync("EntryIndexProjection", TestContext.Current.CancellationToken);

        var afterRebuild = await fixture.EntryQuery.FindAsync(competitionId, null, null, null, null, null, TestContext.Current.CancellationToken);
        afterRebuild.Should().BeEquivalentTo(before);

        // Each Entry's own stream, unaffected by the read-model drop, still
        // folds to the same state as before — the replay only touched
        // entry_index, never the log.
        foreach (var entryId in openedIds)
        {
            var entry = await LoadEntryAsync(fixture, entryId);
            entry.Id.Should().Be(entryId);
            entry.CompetitionRef.Should().Be(competitionId);
        }
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresEntryCaptureEventStoreTests(PostgresFixture fixture) : EntryCaptureEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteEntryCaptureEventStoreTests(SqliteFixture fixture) : EntryCaptureEventStoreTests<SqliteFixture>(fixture);
