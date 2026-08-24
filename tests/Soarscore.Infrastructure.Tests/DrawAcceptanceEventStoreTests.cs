// kanban/in-progress/draw-acceptance-redraw.md WI-8 — the store-backed proof of
// the draw lifecycle end to end: create -> register -> draw -> accept / reject
// -> redraw -> enter. Full cycles through the DISPATCHER, never hand-appended
// events: a real Dispatcher over exactly the handler registrations the Api's
// composition would build for these commands (reflective resolution included),
// against the fixture's real store.
//
// kanban/completed/multi-backend-deployment.md WI-6's shape, same as
// DrawPhaseEventStoreTests.cs: written once against IStoreFixture, so the file
// runs unchanged against every backend Soarscore supports — Marten/PostgreSQL
// and Fisher/SQLite — one concrete subclass per backend at the foot of the
// file. Only the Postgres subclass keeps Trait("Category", "Storage");
// EventStoreTests.cs's header says why.
//
// F5J (30-f5j) throughout — the corpus class the other drawing store tests all
// adopt (DrawPhaseEventStoreTests.cs, ScoringEventStoreTests.cs,
// TaskRoundLifecycleEventStoreTests.cs): literal MinPerGroup 6 makes a
// six-pilot field draw to exactly one group per round, and a single
// FixedSequence/TasksPerRound==1 task per phase is the shape
// Competition.DrawPhase requires.

using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class DrawAcceptanceEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ------------------------------------------------------------- dispatcher

    // The handlers the four cycles dispatch to, registered against the
    // fixture's real store exactly as Composition.cs registers them — the
    // Dispatcher resolves each command's handler reflectively, so what runs is
    // production's dispatch path, not a hand-wired handler call.
    private static IDispatcher CreateDispatcher(IStoreFixture fixture)
    {
        var clock = new SystemClock();
        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandler<PublishClassDefinition, string>>(new PublishClassDefinitionHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<CreateCompetition, CompetitionId>>(new CreateCompetitionHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<RegisterPerson, PersonId>>(new RegisterPersonHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<RegisterCompetitor, CompetitorId>>(new RegisterCompetitorHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<DrawPhase, CompetitionId>>(new DrawPhaseHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<AcceptDraw, CompetitionId>>(new AcceptDrawHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<RejectDraw, CompetitionId>>(new RejectDrawHandler(fixture.EventStore, fixture.EntryQuery, clock));
        services.AddSingleton<ICommandHandler<OpenEntry, EntryId>>(new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, clock));
        services.AddSingleton<ICommandHandler<OpenFlight, EntryId>>(new OpenFlightHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<CaptureMeasurement, EntryId>>(new CaptureMeasurementHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<WithdrawCompetitor, CompetitorId>>(new WithdrawCompetitorHandler(fixture.EventStore, clock));
        return new Dispatcher(services.BuildServiceProvider());
    }

    // ----------------------------------------------------------------- setup

    private static async Task<CompetitionId> CreateCompetitionAsync(IDispatcher dispatcher, string name)
    {
        var published = await dispatcher.SendAsync(new PublishClassDefinition(F5JDefinition), Ct);
        published.IsSuccess.Should().BeTrue();

        var created = await dispatcher.SendAsync(
            new CreateCompetition(name, "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            Ct);
        created.IsSuccess.Should().BeTrue($"{created.Code}: {created.Message}");

        return created.Value;
    }

    private static async Task<PersonId> RegisterPersonAsync(IDispatcher dispatcher, string email)
    {
        var registered = await dispatcher.SendAsync(
            new RegisterPerson("Test Pilot", new ContactDetails { Email = email }, Club: null),
            Ct);
        registered.IsSuccess.Should().BeTrue();

        return registered.Value;
    }

    private static async Task<CompetitorId> RegisterCompetitorAsync(IDispatcher dispatcher, CompetitionId competitionId, string email)
    {
        var personId = await RegisterPersonAsync(dispatcher, email);
        var registered = await dispatcher.SendAsync(new RegisterCompetitor(competitionId, personId), Ct);
        registered.IsSuccess.Should().BeTrue($"{registered.Code}: {registered.Message}");

        return registered.Value;
    }

    private static async Task DrawAsync(IDispatcher dispatcher, CompetitionId competitionId)
    {
        var drawn = await dispatcher.SendAsync(new DrawPhase(competitionId, 1), Ct);
        drawn.IsSuccess.Should().BeTrue($"{drawn.Code}: {drawn.Message}");
    }

    private static async Task AcceptAsync(IDispatcher dispatcher, CompetitionId competitionId)
    {
        var accepted = await dispatcher.SendAsync(new AcceptDraw(competitionId), Ct);
        accepted.IsSuccess.Should().BeTrue($"{accepted.Code}: {accepted.Message}");
    }

    private static async Task<Competition> LoadCompetitionAsync(IStoreFixture fixture, CompetitionId competitionId)
    {
        // Reads stay on GetCompetitionHandler directly, as in every other
        // store-level test file; only the write cycles go through the
        // dispatcher.
        var fetched = await new GetCompetitionHandler(fixture.EventStore).HandleAsync(new GetCompetition(competitionId), Ct);
        fetched.IsSuccess.Should().BeTrue($"{fetched.Code}: {fetched.Message}");
        return fetched.Value.Competition;
    }

    // ---- 1. draw -> accept -> open entry succeeds ---------------------------

    [Fact]
    public async Task AcceptDraw_round_trips_through_the_real_store_and_opening_an_entry_then_succeeds()
    {
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Accept Then Enter");

        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 6; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(dispatcher, competitionId, $"pilot-accept-{i}@example.com"));
        }

        await DrawAsync(dispatcher, competitionId);
        await AcceptAsync(dispatcher, competitionId);

        var competition = await LoadCompetitionAsync(fixture, competitionId);
        competition.Phases.Single().Draw.Status.Should().Be("accepted");

        var group = competition.Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single();
        group.CompetitorRefs.Should().HaveCount(6);

        // D4: with the draw accepted, the competition can begin.
        var opened = await dispatcher.SendAsync(
            new OpenEntry(competitionId, 0, 1, 1, group.Id, competitorIds[0]),
            Ct);
        opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");
    }

    // ---- 2. reject -> register latecomer -> redraw -> accept ----------------

    [Fact]
    public async Task A_rejected_draw_is_redrawn_and_accepted_and_the_stream_keeps_both_draws_and_the_rejection()
    {
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Reject Redraw Accept");

        for (var i = 0; i < 6; i++)
        {
            await RegisterCompetitorAsync(dispatcher, competitionId, $"pilot-cycle-{i}@example.com");
        }

        await DrawAsync(dispatcher, competitionId);

        const string reason = "Latecomer missed the draw";
        var rejected = await dispatcher.SendAsync(new RejectDraw(competitionId, reason), Ct);
        rejected.IsSuccess.Should().BeTrue($"{rejected.Code}: {rejected.Message}");

        // D2: Phases holds only live phases — the rejected one is gone from
        // the fold entirely, which is why the next draw needs no edit.
        (await LoadCompetitionAsync(fixture, competitionId)).Phases.Should().BeEmpty();

        // D6 reopened registration with the phase gone — the point of the
        // whole cycle: the latecomer gets into the field before the redraw.
        await RegisterCompetitorAsync(dispatcher, competitionId, "pilot-cycle-late@example.com");

        await DrawAsync(dispatcher, competitionId);
        await AcceptAsync(dispatcher, competitionId);

        var livePhase = (await LoadCompetitionAsync(fixture, competitionId)).Phases.Should().ContainSingle().Subject;
        livePhase.Ordinal.Should().Be(0); // the redraw re-addresses phase definition 0, not a flyoff
        livePhase.Draw.Status.Should().Be("accepted");

        var opened = await dispatcher.SendAsync(
            new OpenEntry(competitionId, 0, 1, 1, livePhase.Rounds.Single().TaskRounds.Single().Groups.Single().Id,
                livePhase.Rounds.Single().TaskRounds.Single().Groups.Single().CompetitorRefs[0]),
            Ct);
        opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");

        // The audit trail is the stream (D2's stated consequence): the fold
        // forgot the first draw, but the log must hold both PhaseDrawn events
        // plus the rejection — asserted against raw stream events here.
        var stream = await fixture.EventStore.ReadStreamAsync(competitionId.Value, 0, Ct);
        stream.IsSuccess.Should().BeTrue();

        var types = stream.Value.Select(e => e.GetType()).ToList();
        types.Count(t => t == typeof(PhaseDrawn)).Should().Be(2);
        types.Count(t => t == typeof(DrawAccepted)).Should().Be(1);

        var rejection = stream.Value.OfType<DrawRejected>().Should().ContainSingle().Subject;
        rejection.PhaseOrdinal.Should().Be(0);
        rejection.Reason.Should().Be(reason);

        // ...and in log order: first draw -> reject -> second draw -> accept.
        types.IndexOf(typeof(PhaseDrawn)).Should().BeLessThan(types.IndexOf(typeof(DrawRejected)));
        types.LastIndexOf(typeof(PhaseDrawn)).Should().BeGreaterThan(types.IndexOf(typeof(DrawRejected)));
        types.IndexOf(typeof(DrawAccepted)).Should().BeGreaterThan(types.LastIndexOf(typeof(PhaseDrawn)));
    }

    // ---- 3. entries against the phase block rejection -----------------------

    [Fact]
    public async Task RejectDraw_once_flights_are_recorded_against_the_draw_is_refused_with_rejectDraw_entriesExist()
    {
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Reject Refused With Entries");

        for (var i = 0; i < 6; i++)
        {
            await RegisterCompetitorAsync(dispatcher, competitionId, $"pilot-refuse-{i}@example.com");
        }

        await DrawAsync(dispatcher, competitionId);
        await AcceptAsync(dispatcher, competitionId);

        var group = (await LoadCompetitionAsync(fixture, competitionId))
            .Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single();

        var opened = await dispatcher.SendAsync(
            new OpenEntry(competitionId, 0, 1, 1, group.Id, group.CompetitorRefs[0]),
            Ct);
        opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");

        var flightOpened = await dispatcher.SendAsync(new OpenFlight(opened.Value), Ct);
        flightOpened.IsSuccess.Should().BeTrue($"{flightOpened.Code}: {flightOpened.Message}");

        var captured = await dispatcher.SendAsync(
            new CaptureMeasurement(opened.Value, 1, "flightTime", MeasuredValue.Of(300m)),
            Ct);
        captured.IsSuccess.Should().BeTrue($"{captured.Code}: {captured.Message}");

        // D5: the handler resolves entries-exist from the real entry_index and
        // the decide function refuses — rejecting now would orphan an entry
        // referencing the doomed draw's GroupId.
        var rejected = await dispatcher.SendAsync(new RejectDraw(competitionId, "Spotted too late"), Ct);
        rejected.IsFailure.Should().BeTrue();
        rejected.Code.Should().Be("rejectDraw.entriesExist");
    }

    // ---- 4. the field freezes on acceptance, not at the draw ----------------

    [Fact]
    public async Task Registration_succeeds_between_draw_and_accept_and_freezes_only_once_accepted()
    {
        // WI-8 item 4 — the rewrite of DrawPhaseEventStoreTests' late-
        // registration scenario: frozen only after ACCEPT, the first
        // real-store exercise of the re-pointed ValidateFieldNotFrozen (D6).
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Freeze On Accept");

        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 6; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(dispatcher, competitionId, $"pilot-freeze-{i}@example.com"));
        }

        await DrawAsync(dispatcher, competitionId);

        // Between draw and acceptance the field is still open.
        await RegisterCompetitorAsync(dispatcher, competitionId, "pilot-freeze-between@example.com");

        await AcceptAsync(dispatcher, competitionId);

        var latePerson = await RegisterPersonAsync(dispatcher, "pilot-freeze-too-late@example.com");
        var refused = await dispatcher.SendAsync(new RegisterCompetitor(competitionId, latePerson), Ct);
        refused.IsFailure.Should().BeTrue();
        refused.Code.Should().Be("competition.field.frozen");

        // Withdrawing, by contrast, stays ungated forever.
        var withdrawn = await dispatcher.SendAsync(new WithdrawCompetitor(competitionId, competitorIds[0]), Ct);
        withdrawn.IsSuccess.Should().BeTrue($"{withdrawn.Code}: {withdrawn.Message}");
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresDrawAcceptanceEventStoreTests(PostgresFixture fixture)
    : DrawAcceptanceEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteDrawAcceptanceEventStoreTests(SqliteFixture fixture)
    : DrawAcceptanceEventStoreTests<SqliteFixture>(fixture);
