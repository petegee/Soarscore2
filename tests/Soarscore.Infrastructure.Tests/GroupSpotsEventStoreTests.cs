// kanban/in-progress/lane-assignment.md WI-7 — the store-backed proof of the
// field-spot lifecycle end to end: create -> register -> draw -> assign (->
// accept) -> re-assign / reject -> redraw -> withdraw. Full cycles through the
// DISPATCHER, never hand-appended events, against the fixture's real store —
// the same discipline as DrawAcceptanceEventStoreTests.cs, written once against
// IStoreFixture so the file runs unchanged against every backend Soarscore
// supports — Marten/PostgreSQL and Fisher/SQLite — one concrete subclass per
// backend at the foot of the file. Only the Postgres subclass keeps
// Trait("Category", "Storage"); EventStoreTests.cs's header says why.
//
// F5J (30-f5j) throughout, for the same reason the other drawing store tests
// give: literal MinPerGroup 6 makes a six-pilot field draw to exactly one group
// per round (and a twelve-pilot field to exactly two), which pins the group
// coordinates (0/1/1) without hunting the fold.
//
// Cycle 3 is LADR-0001 §4.8's net for lane-assignment.md WI-4: the
// "groupSpotsAdded" alias registered in SoarscoreEventTypes is the only thing
// that makes GroupSpotsAssigned readable on EITHER backend, so assign -> reject
// -> redraw -> re-assign through the real store fails loudly on both if that
// registration line goes missing.
//
// Reads stay on the handlers directly (GetCompetitionHandler — the GET
// /competition read path; GetTaskRoundRecordingHandler — the GET
// /task-round-recording read path), as in every other store-level test file;
// only the write cycles go through the dispatcher.

using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class GroupSpotsEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
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
        services.AddSingleton<ICommandHandler<WithdrawCompetitor, CompetitorId>>(new WithdrawCompetitorHandler(fixture.EventStore, clock));
        services.AddSingleton<ICommandHandler<AssignGroupSpots, GroupId>>(new AssignGroupSpotsHandler(fixture.EventStore, clock));
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

    private static async Task<List<CompetitorId>> RegisterCompetitorsAsync(
        IDispatcher dispatcher, CompetitionId competitionId, int count, string emailStem)
    {
        var list = new List<CompetitorId>();
        for (var i = 0; i < count; i++)
        {
            var person = await dispatcher.SendAsync(
                new RegisterPerson(
                    "Test Pilot",
                    new ContactDetails { Email = $"{emailStem}-{i}@example.com".ToLowerInvariant() },
                    Club: null),
                Ct);
            person.IsSuccess.Should().BeTrue();

            var registered = await dispatcher.SendAsync(new RegisterCompetitor(competitionId, person.Value), Ct);
            registered.IsSuccess.Should().BeTrue($"{registered.Code}: {registered.Message}");
            list.Add(registered.Value);
        }

        return list;
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

    private static async Task AssignAsync(
        IDispatcher dispatcher, CompetitionId competitionId, GroupId groupRef, IReadOnlyList<GroupSpot> spots)
    {
        var assigned = await dispatcher.SendAsync(
            new AssignGroupSpots(competitionId, 0, 1, 1, groupRef, spots), Ct);
        assigned.IsSuccess.Should().BeTrue($"{assigned.Code}: {assigned.Message}");
        assigned.Value.Should().Be(groupRef); // ICommand<GroupId>: names the group it assigned
    }

    private static async Task<Competition> LoadCompetitionAsync(IStoreFixture fixture, CompetitionId competitionId)
    {
        var fetched = await new GetCompetitionHandler(fixture.EventStore).HandleAsync(new GetCompetition(competitionId), Ct);
        fetched.IsSuccess.Should().BeTrue($"{fetched.Code}: {fetched.Message}");
        return fetched.Value.Competition;
    }

    private static async Task<Group> SingleGroupOfRound1Async(IStoreFixture fixture, CompetitionId competitionId)
    {
        var competition = await LoadCompetitionAsync(fixture, competitionId);
        return competition.Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single();
    }

    private static async Task<TaskRoundRecordingView> AskRecordingAsync(IStoreFixture fixture, CompetitionId competitionId)
    {
        var handler = new GetTaskRoundRecordingHandler(fixture.EventStore, fixture.EntryQuery);
        var asked = await handler.HandleAsync(
            new GetTaskRoundRecording(competitionId, 0, 1, 1, GroupRef: null), Ct);
        asked.IsSuccess.Should().BeTrue($"{asked.Code}: {asked.Message}");
        return asked.Value;
    }

    // ---- 1. draw -> accept -> assign -> the fold round-trips the real store --

    [Fact]
    public async Task AssignGroupSpots_round_trips_through_the_real_store_and_both_reads_carry_the_assignment()
    {
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Assign Then Read");

        await RegisterCompetitorsAsync(dispatcher, competitionId, 6, "pilot-spots-accept");
        await DrawAsync(dispatcher, competitionId);
        await AcceptAsync(dispatcher, competitionId);

        var group = await SingleGroupOfRound1Async(fixture, competitionId);

        // As given, deliberately NOT the drawn order and NOT spot == sequence
        // position (design principle 10): the fold stores exactly what was
        // commanded — no reordering, no normalisation; the read view sorts.
        var spots = group.CompetitorRefs.Select((c, i) => new GroupSpot(c, group.CompetitorRefs.Length - i)).ToList();
        await AssignAsync(dispatcher, competitionId, group.Id, spots);

        var stored = await SingleGroupOfRound1Async(fixture, competitionId);
        stored.Spots.Should().HaveCount(6);
        stored.Spots.Select(s => s.CompetitorRef).Should().Equal(group.CompetitorRefs); // as given
        stored.Spots.Select(s => s.Spot).Should().Equal([6, 5, 4, 3, 2, 1]);

        // The capture-time read (GET /task-round-recording, WI-5) states the
        // same assignment, spot-ordered: spot 1 first — the drawn order,
        // reversed by the as-given mapping above.
        var view = await AskRecordingAsync(fixture, competitionId);
        var g = view.Groups.Should().ContainSingle().Subject;
        g.GroupRef.Should().Be(group.Id);
        g.Spots.Select(s => s.Spot).Should().Equal([1, 2, 3, 4, 5, 6]);
        g.Spots.Select(s => s.CompetitorRef).Should().Equal(group.CompetitorRefs.Reverse());
    }

    // ---- 2. re-assign replaces whole (D3) -----------------------------------

    [Fact]
    public async Task Re_assigning_replaces_the_previous_assignment_whole_and_the_reads_show_only_the_second_list()
    {
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Reassign Replaces");

        await RegisterCompetitorsAsync(dispatcher, competitionId, 6, "pilot-spots-reassign");
        await DrawAsync(dispatcher, competitionId);
        await AcceptAsync(dispatcher, competitionId);

        var group = await SingleGroupOfRound1Async(fixture, competitionId);

        var first = group.CompetitorRefs.Select((c, i) => new GroupSpot(c, i + 1)).ToList();
        await AssignAsync(dispatcher, competitionId, group.Id, first);

        // Broken winch, shift lanes — ordinary field operations (D3). The
        // second mapping is complete and non-contiguous (distinct positive
        // integers, not required contiguous — D1), in a different order again.
        var second = group.CompetitorRefs.Select((c, i) => new GroupSpot(c, 60 - i * 10)).ToList();
        await AssignAsync(dispatcher, competitionId, group.Id, second);

        var stored = await SingleGroupOfRound1Async(fixture, competitionId);
        stored.Spots.Should().HaveCount(6);
        stored.Spots.Select(s => s.CompetitorRef).Should().Equal(group.CompetitorRefs); // as given: drawn order
        stored.Spots.Select(s => s.Spot).Should().Equal([60, 50, 40, 30, 20, 10]);      // no trace of 1..6

        var g = (await AskRecordingAsync(fixture, competitionId)).Groups.Should().ContainSingle().Subject;
        g.Spots.Select(s => s.Spot).Should().Equal([10, 20, 30, 40, 50, 60]);           // read sorts
        g.Spots.Select(s => s.CompetitorRef).Should().Equal(group.CompetitorRefs.Reverse());
    }

    // ---- 3. reject -> redraw discards the assignments (D3) — LADR-0001 §4.8 --

    [Fact]
    public async Task A_rejected_draw_discards_its_assignments_and_the_redraw_starts_unassigned_and_re_assigns()
    {
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Reject Redraw Reassign");

        await RegisterCompetitorsAsync(dispatcher, competitionId, 12, "pilot-spots-reject");

        await DrawAsync(dispatcher, competitionId);

        // Assignment needs no acceptance (D3 — assignable from draw time
        // onward): both groups of the round get spots while the draw is merely
        // drawn.
        var drawn = (await LoadCompetitionAsync(fixture, competitionId))
            .Phases.Should().ContainSingle().Subject
            .Rounds.Single().TaskRounds.Single().Groups;
        drawn.Should().HaveCount(2); // MinPerGroup 6 with a 12-pilot field

        foreach (var group in drawn)
        {
            await AssignAsync(
                dispatcher, competitionId, group.Id,
                group.CompetitorRefs.Select((c, i) => new GroupSpot(c, i + 1)).ToList());
        }

        const string reason = "Spots were marked out on the wrong paddock";
        var rejected = await dispatcher.SendAsync(new RejectDraw(competitionId, reason), Ct);
        rejected.IsSuccess.Should().BeTrue($"{rejected.Code}: {rejected.Message}");

        // D2: the rejected phase is gone from the fold entirely — and every
        // assignment on it died with it (D3).
        (await LoadCompetitionAsync(fixture, competitionId)).Phases.Should().BeEmpty();

        await DrawAsync(dispatcher, competitionId);

        // The redraw mints fresh GroupIds whose groups read unassigned — a
        // fact (D2), never a zombie of the rejected draw's assignments.
        var redrawn = (await LoadCompetitionAsync(fixture, competitionId))
            .Phases.Should().ContainSingle().Subject
            .Rounds.Single().TaskRounds.Single().Groups;
        redrawn.Should().HaveCount(2);
        redrawn.Select(g => g.Id).Should().NotIntersectWith(drawn.Select(g => g.Id));
        foreach (var group in redrawn)
        {
            group.Spots.Should().BeEmpty();
        }

        // Re-assigning the redraw's groups succeeds — nothing gates on the
        // old assignment's ghost.
        foreach (var group in redrawn)
        {
            await AssignAsync(
                dispatcher, competitionId, group.Id,
                group.CompetitorRefs.Select((c, i) => new GroupSpot(c, (i + 1) * 10)).ToList());
        }

        var after = (await LoadCompetitionAsync(fixture, competitionId))
            .Phases.Should().ContainSingle().Subject
            .Rounds.Single().TaskRounds.Single().Groups;
        foreach (var group in after)
        {
            group.Spots.Select(s => s.Spot).Should().Equal([10, 20, 30, 40, 50, 60]);
        }

        // The audit trail is the stream (D2's stated consequence): the fold
        // forgot the first draw, but the log holds both draws, the rejection
        // and all four assignments — first draw's two, then the redraw's two.
        // This read is the WI-4 net itself: the "groupSpotsAdded" alias is
        // what makes these events deserialise on this backend.
        var stream = await fixture.EventStore.ReadStreamAsync(competitionId.Value, 0, Ct);
        stream.IsSuccess.Should().BeTrue();

        var types = stream.Value.Select(e => e.GetType()).ToList();
        types.Count(t => t == typeof(PhaseDrawn)).Should().Be(2);
        types.Count(t => t == typeof(DrawRejected)).Should().Be(1);
        types.Count(t => t == typeof(GroupSpotsAssigned)).Should().Be(4);

        // ...and in log order: draw -> assign x2 -> reject -> redraw -> assign x2.
        types.IndexOf(typeof(GroupSpotsAssigned)).Should().BeGreaterThan(types.IndexOf(typeof(PhaseDrawn)));
        types.IndexOf(typeof(GroupSpotsAssigned)).Should().BeLessThan(types.IndexOf(typeof(DrawRejected)));
        types.LastIndexOf(typeof(GroupSpotsAssigned)).Should().BeGreaterThan(types.LastIndexOf(typeof(PhaseDrawn)));

        var assignments = stream.Value.OfType<GroupSpotsAssigned>().ToList();
        assignments[0].GroupRef.Should().Be(drawn[0].Id);
        assignments[1].GroupRef.Should().Be(drawn[1].Id);
        assignments.Skip(2).Select(a => a.GroupRef).Should().Equal(redrawn[0].Id, redrawn[1].Id);
    }

    // ---- 4. withdrawal after assignment leaves the spot recorded (D4) -------

    [Fact]
    public async Task A_competitor_who_withdraws_after_assignment_leaves_the_spot_recorded_and_exits_the_expected_list()
    {
        var dispatcher = CreateDispatcher(fixture);
        var competitionId = await CreateCompetitionAsync(dispatcher, "Withdraw After Assign");

        var competitorIds = await RegisterCompetitorsAsync(dispatcher, competitionId, 6, "pilot-spots-withdraw");
        await DrawAsync(dispatcher, competitionId);
        await AcceptAsync(dispatcher, competitionId);

        var group = await SingleGroupOfRound1Async(fixture, competitionId);

        var spots = group.CompetitorRefs.Select((c, i) => new GroupSpot(c, i + 1)).ToList();
        await AssignAsync(dispatcher, competitionId, group.Id, spots);

        var withdrawn = await dispatcher.SendAsync(new WithdrawCompetitor(competitionId, competitorIds[3]), Ct);
        withdrawn.IsSuccess.Should().BeTrue($"{withdrawn.Code}: {withdrawn.Message}");

        // D4: the assignment stays recorded on the fold — the spot still names
        // them; vacancy is the consumer's derivation, never an edit.
        var stored = await SingleGroupOfRound1Async(fixture, competitionId);
        stored.Spots.Should().HaveCount(6);
        stored.Spots.Select(s => s.CompetitorRef).Should().Equal(group.CompetitorRefs);
        stored.Spots.Select(s => s.Spot).Should().Equal([1, 2, 3, 4, 5, 6]);

        // The capture-time read states both facts: the recorded assignment
        // untouched (spot 4 now derivably vacant), and Expected — drawn minus
        // withdrawn — without them.
        var g = (await AskRecordingAsync(fixture, competitionId)).Groups.Should().ContainSingle().Subject;
        g.Spots.Should().HaveCount(6);
        g.Spots.Select(s => s.Spot).Should().Equal([1, 2, 3, 4, 5, 6]);
        g.Spots.Select(s => s.CompetitorRef).Should().Equal(group.CompetitorRefs);
        g.ExpectedCompetitorRefs.Should().Equal(group.CompetitorRefs.Where(c => c != competitorIds[3]));
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresGroupSpotsEventStoreTests(PostgresFixture fixture)
    : GroupSpotsEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteGroupSpotsEventStoreTests(SqliteFixture fixture)
    : GroupSpotsEventStoreTests<SqliteFixture>(fixture);
