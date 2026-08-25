// kanban/in-progress/prescribed-draw-import.md WI-5 — the store-backed proof
// of the prescription lifecycle end to end: create -> register -> PRESCRIBE
// -> accept / reject -> re-prescribe -> enter, plus the two properties the
// story adds beyond DrawAcceptanceEventStoreTests' lifecycle: the appended
// PhaseDrawn carries PrescribedBy out of a real store (P1's runtime half —
// WI-3's JSON contract test is the contract half), and a refused prescription
// appends nothing. Full cycles through the DISPATCHER, never hand-appended
// events: a real Dispatcher over exactly the handler registrations the Api's
// composition would build for these commands (reflective resolution included),
// against the fixture's real store.
//
// kanban/completed/multi-backend-deployment.md WI-6's shape, same as
// DrawAcceptanceEventStoreTests.cs: written once against IStoreFixture, so the
// file runs unchanged against every backend Soarscore supports —
// Marten/PostgreSQL and Fisher/SQLite — one concrete subclass per backend at
// the foot of the file. Only the Postgres subclass keeps
// Trait("Category", "Storage"); EventStoreTests.cs's header says why.
//
// F5J (30-f5j) throughout — the corpus class the other drawing store tests all
// adopt (DrawAcceptanceEventStoreTests.cs, DrawPhaseEventStoreTests.cs,
// ScoringEventStoreTests.cs): literal MinPerGroup 6 makes a six-pilot field
// legal for a single whole-field group per round (the group meets both the >=2
// floor and the resolved minimum), and a single FixedSequence/
// TasksPerRound==1 task per phase means every round's TaskRef is null — the
// payload shape an importer sends for such classes.

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

public abstract class PrescribeDrawEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    private const string CdName = "Imported Comp CD";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ------------------------------------------------------------- dispatcher

    // The handlers the four cycles dispatch to, registered against the
    // fixture's real store exactly as Composition.cs registers them — the
    // Dispatcher resolves each command's handler reflectively, so what runs is
    // production's dispatch path, not a hand-wired handler call. DrawPhase is
    // here only for the generated-draw contrast leg of the provenance test.
    private static IDispatcher CreateDispatcher(IStoreFixture fixture)
    {
        var clock = new SystemClock();
        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandler<PublishClassDefinition, string>>(new PublishClassDefinitionHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<CreateCompetition, CompetitionId>>(new CreateCompetitionHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<RegisterPerson, PersonId>>(new RegisterPersonHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<RegisterCompetitor, CompetitorId>>(new RegisterCompetitorHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<DrawPhase, CompetitionId>>(new DrawPhaseHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<PrescribeDraw, CompetitionId>>(new PrescribeDrawHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<AcceptDraw, CompetitionId>>(new AcceptDrawHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<RejectDraw, CompetitionId>>(new RejectDrawHandler(fixture.EventStore, fixture.EntryQuery, clock));
        services.AddSingleton<ICommandHandler<OpenEntry, EntryId>>(new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, clock));
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

    private static async Task<CompetitorId> RegisterCompetitorAsync(IDispatcher dispatcher, CompetitionId competitionId, string email)
    {
        var registeredPerson = await dispatcher.SendAsync(
            new RegisterPerson("Test Pilot", new ContactDetails { Email = email }, Club: null),
            Ct);
        registeredPerson.IsSuccess.Should().BeTrue();

        var registered = await dispatcher.SendAsync(new RegisterCompetitor(competitionId, registeredPerson.Value), Ct);
        registered.IsSuccess.Should().BeTrue($"{registered.Code}: {registered.Message}");

        return registered.Value;
    }

    private static async Task<IReadOnlyList<CompetitorId>> RegisterFieldAsync(IDispatcher dispatcher, CompetitionId competitionId, string emailPrefix)
    {
        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 6; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(dispatcher, competitionId, $"{emailPrefix}-{i}@example.com"));
        }

        return competitorIds;
    }

    // One round, one whole-field group, members listed in the supplied flying
    // order — TaskRef null because F5J's phase is FixedSequence. Returns the
    // prescribed flying order for the caller to assert against the fold.
    private static async Task<IReadOnlyList<CompetitorId>> PrescribeWholeFieldAsync(
        IDispatcher dispatcher, CompetitionId competitionId, IReadOnlyList<CompetitorId> flyingOrder)
    {
        var prescribed = await dispatcher.SendAsync(
            new PrescribeDraw(competitionId, [new PrescribedRound(null, [new PrescribedGroup(flyingOrder)])], CdName),
            Ct);
        prescribed.IsSuccess.Should().BeTrue($"{prescribed.Code}: {prescribed.Message}");

        return flyingOrder;
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

    // ---- 1. prescribe -> accept -> open entry succeeds ----------------------

    [Fact]
    public async Task PrescribeDraw_round_trips_through_the_real_store_and_opening_an_entry_then_succeeds()
    {
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Prescribe Then Enter");
        var competitorIds = await RegisterFieldAsync(dispatcher, competitionId, "pilot-prescribe-enter");

        // Deliberately NOT field order: member order is stored as given (story
        // decision 4 — GliderScore's SeqNo), so the assertion below proves the
        // order survives a real store round trip, not just the decide/fold.
        var flyingOrder = competitorIds.Reverse().ToList();
        await PrescribeWholeFieldAsync(dispatcher, competitionId, flyingOrder);

        await AcceptAsync(dispatcher, competitionId);

        var competition = await LoadCompetitionAsync(fixture, competitionId);
        competition.Phases.Single().Draw.Status.Should().Be("accepted");

        var group = competition.Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single();
        group.Ordinal.Should().Be(1);
        group.CompetitorRefs.Should().Equal(flyingOrder); // stored in supplied flying order, verbatim

        // D4: with the prescribed draw accepted, the competition can begin —
        // the entry opens against a PRESCRIBED GroupId, proving the whole
        // downstream contract sees prescription as an ordinary draw.
        var opened = await dispatcher.SendAsync(
            new OpenEntry(competitionId, 0, 1, 1, group.Id, flyingOrder[0]),
            Ct);
        opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");
    }

    // ---- 2. reject -> re-prescribe -> accept ---------------------------------

    [Fact]
    public async Task A_rejected_prescription_is_re_prescribed_and_accepted_and_the_stream_keeps_both_draws_and_the_rejection()
    {
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Reject Represcribe Accept");
        var competitorIds = await RegisterFieldAsync(dispatcher, competitionId, "pilot-prescribe-cycle");

        await PrescribeWholeFieldAsync(dispatcher, competitionId, competitorIds);

        const string reason = "Imported draw had the latecomer's group wrong";
        var rejected = await dispatcher.SendAsync(new RejectDraw(competitionId, reason), Ct);
        rejected.IsSuccess.Should().BeTrue($"{rejected.Code}: {rejected.Message}");

        // D2: Phases holds only live phases — the rejected prescription is gone
        // from the fold entirely, which is why the re-prescription needs no edit.
        (await LoadCompetitionAsync(fixture, competitionId)).Phases.Should().BeEmpty();

        // The corrected prescription lists members in a different flying order,
        // then accepts and enters — prescription inherits reject/re-draw
        // semantics wholesale because it emits the same PhaseDrawn.
        var correctedOrder = competitorIds.Skip(3).Concat(competitorIds.Take(3)).ToList();
        await PrescribeWholeFieldAsync(dispatcher, competitionId, correctedOrder);
        await AcceptAsync(dispatcher, competitionId);

        var livePhase = (await LoadCompetitionAsync(fixture, competitionId)).Phases.Should().ContainSingle().Subject;
        livePhase.Ordinal.Should().Be(0); // the re-prescription re-addresses phase definition 0, not a flyoff
        livePhase.Draw.Status.Should().Be("accepted");
        livePhase.Rounds.Single().TaskRounds.Single().Groups.Single().CompetitorRefs.Should().Equal(correctedOrder);

        var opened = await dispatcher.SendAsync(
            new OpenEntry(competitionId, 0, 1, 1,
                livePhase.Rounds.Single().TaskRounds.Single().Groups.Single().Id,
                correctedOrder[0]),
            Ct);
        opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");

        // The audit trail is the stream (D2's stated consequence): the fold
        // forgot the first prescription, but the log must hold both PhaseDrawn
        // events plus the rejection — asserted against raw stream events here.
        var stream = await fixture.EventStore.ReadStreamAsync(competitionId.Value, 0, Ct);
        stream.IsSuccess.Should().BeTrue();

        var types = stream.Value.Select(e => e.GetType()).ToList();
        types.Count(t => t == typeof(PhaseDrawn)).Should().Be(2);
        types.Count(t => t == typeof(DrawAccepted)).Should().Be(1);

        var rejection = stream.Value.OfType<DrawRejected>().Should().ContainSingle().Subject;
        rejection.PhaseOrdinal.Should().Be(0);
        rejection.Reason.Should().Be(reason);

        // Both prescriptions carry their provenance — the audit trail records
        // that these were prescribed, not generated (P1).
        stream.Value.OfType<PhaseDrawn>().Should().OnlyContain(drawn => drawn.PrescribedBy == CdName);

        // ...and in log order: first prescription -> reject -> second -> accept.
        types.IndexOf(typeof(PhaseDrawn)).Should().BeLessThan(types.IndexOf(typeof(DrawRejected)));
        types.LastIndexOf(typeof(PhaseDrawn)).Should().BeGreaterThan(types.IndexOf(typeof(DrawRejected)));
        types.IndexOf(typeof(DrawAccepted)).Should().BeGreaterThan(types.LastIndexOf(typeof(PhaseDrawn)));
    }

    // ---- 3. PrescribedBy survives the store round trip -----------------------

    [Fact]
    public async Task PrescribedBy_survives_the_store_round_trip_while_a_generated_draw_stays_null()
    {
        // P1's runtime half: the folded Phase drops PrescribedBy (audit-only),
        // so this reads the raw stream back from each real store and checks the
        // property the log exists to keep — and its null on a generated draw,
        // which is what makes prescribed distinguishable from generated.
        var dispatcher = CreateDispatcher(fixture);
        var prescribedCompetitionId = await CreateCompetitionAsync(dispatcher, "Provenance Round Trip");
        var competitorIds = await RegisterFieldAsync(dispatcher, prescribedCompetitionId, "pilot-prescribe-provenance");

        await PrescribeWholeFieldAsync(dispatcher, prescribedCompetitionId, competitorIds);

        var prescribedStream = await fixture.EventStore.ReadStreamAsync(prescribedCompetitionId.Value, 0, Ct);
        prescribedStream.IsSuccess.Should().BeTrue();

        var prescribedDrawn = prescribedStream.Value.OfType<PhaseDrawn>().Should().ContainSingle().Subject;
        prescribedDrawn.PrescribedBy.Should().Be(CdName);

        // Contrast leg: a GENERATED draw on the same store logs PrescribedBy
        // null — the same PhaseDrawn contract, both values exercised for real.
        var generatedCompetitionId = await CreateCompetitionAsync(dispatcher, "Generated Contrast");
        await RegisterFieldAsync(dispatcher, generatedCompetitionId, "pilot-generated-provenance");

        var drawn = await dispatcher.SendAsync(new DrawPhase(generatedCompetitionId, 1), Ct);
        drawn.IsSuccess.Should().BeTrue($"{drawn.Code}: {drawn.Message}");

        var generatedStream = await fixture.EventStore.ReadStreamAsync(generatedCompetitionId.Value, 0, Ct);
        generatedStream.IsSuccess.Should().BeTrue();

        generatedStream.Value.OfType<PhaseDrawn>().Should().ContainSingle().Which.PrescribedBy.Should().BeNull();
    }

    // ---- 4. missing competitor refused, nothing appended ---------------------

    [Fact]
    public async Task A_prescription_missing_a_registered_competitor_is_refused_and_nothing_is_appended()
    {
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Missing Competitor Refused");
        var competitorIds = await RegisterFieldAsync(dispatcher, competitionId, "pilot-prescribe-missing");

        // Baseline: the stream holds the creation plus one CompetitorRegistered
        // per pilot, and nothing else — captured before the attempt under test.
        var baseline = await fixture.EventStore.ReadStreamAsync(competitionId.Value, 0, Ct);
        baseline.IsSuccess.Should().BeTrue();
        baseline.Value.Select(e => e.GetType()).Should().NotContain(typeof(PhaseDrawn));
        var baselineLength = baseline.Value.Count;

        // Five of the six placed — the partition invariant (every eligible
        // competitor in exactly one group) must refuse this at the decide, and
        // the handler must therefore append nothing.
        var incomplete = competitorIds.Take(5).ToList();
        var refused = await dispatcher.SendAsync(
            new PrescribeDraw(competitionId, [new PrescribedRound(null, [new PrescribedGroup(incomplete)])], CdName),
            Ct);
        refused.IsFailure.Should().BeTrue();
        refused.Code.Should().Be("prescribeDraw.competitorMissing");

        var after = await fixture.EventStore.ReadStreamAsync(competitionId.Value, 0, Ct);
        after.IsSuccess.Should().BeTrue();
        after.Value.Should().HaveCount(baselineLength);
        after.Value.Select(e => e.GetType()).Should().NotContain(typeof(PhaseDrawn));

        // And the competition remains prescribable: the corrected full-field
        // prescription lands cleanly afterwards.
        await PrescribeWholeFieldAsync(dispatcher, competitionId, competitorIds);
        (await LoadCompetitionAsync(fixture, competitionId))
            .Phases.Should().ContainSingle()
            .Which.Rounds.Single().TaskRounds.Single().Groups.Single().CompetitorRefs
            .Should().Equal(competitorIds);
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresPrescribeDrawEventStoreTests(PostgresFixture fixture)
    : PrescribeDrawEventStoreTests<PostgresFixture>(fixture);

public sealed class SqlitePrescribeDrawEventStoreTests(SqliteFixture fixture)
    : PrescribeDrawEventStoreTests<SqliteFixture>(fixture);
