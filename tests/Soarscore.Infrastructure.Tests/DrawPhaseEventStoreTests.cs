// docs/plans/phase-drawn-steel-thread-plan.md WI-7 — the store-backed tests
// for DrawPhase, against real PostgreSQL via Testcontainers rather than the
// FakeEventStore double the Application-layer handler tests use. Same style
// as CompetitorEventStoreTests.cs: calls the real handlers directly against
// fixture.EventStore, no dispatcher needed for a store-level test.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

[Trait("Category", "Storage")]
public sealed class DrawPhaseEventStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Distinct FileName per test, the same reason CompetitorEventStoreTests
    // gives: nothing here inspects AdoptedRules content, only avoids
    // ClassDefinition content-hash collisions across the shared fixture.
    // F3J and F5J both carry a literal (non-parameterised) MinPerGroup, and a
    // single FixedSequence/TasksPerRound==1 task per phase — the shape
    // Competition.DrawPhase requires. F3F is used for the replay scenario,
    // which never calls DrawPhase, so its shape does not matter there.
    private static readonly ClassDefinition F3JDefinition = Corpus.All.Single(c => c.FileName == "50-f3j").Definition;
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;
    private static readonly ClassDefinition F3FDefinition = Corpus.All.Single(c => c.FileName == "70-f3f").Definition;

    private static async Task<CompetitionId> CreateCompetitionAsync(PostgresFixture fixture, ClassDefinition definition, string name)
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

    private static async Task<PersonId> RegisterPersonAsync(PostgresFixture fixture, string email)
    {
        var registerHandler = new RegisterPersonHandler(fixture.EventStore, new SystemClock());
        var registered = await registerHandler.HandleAsync(
            new RegisterPerson("Test Pilot", new ContactDetails { Email = email }, Club: null),
            TestContext.Current.CancellationToken);
        registered.IsSuccess.Should().BeTrue();

        return registered.Value;
    }

    private static async Task<CompetitorId> RegisterCompetitorAsync(PostgresFixture fixture, CompetitionId competitionId, string email)
    {
        var personId = await RegisterPersonAsync(fixture, email);
        var registerHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var registered = await registerHandler.HandleAsync(
            new RegisterCompetitor(competitionId, personId), TestContext.Current.CancellationToken);
        registered.IsSuccess.Should().BeTrue();

        return registered.Value;
    }

    [Fact]
    public async Task DrawPhase_twelve_competitors_three_rounds_folds_to_three_rounds_of_two_groups_of_six()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F3JDefinition, "Draw Of Twelve");

        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 12; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-draw-{i}@example.com"));
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 3), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        var phase = fetched.Value.Competition.Phases.Single();
        phase.Rounds.Should().HaveCount(3);

        foreach (var round in phase.Rounds)
        {
            var taskRound = round.TaskRounds.Single();
            taskRound.Groups.Should().HaveCount(2);
            taskRound.Groups.Should().OnlyContain(g => g.CompetitorRefs.Length == 6);

            // Every competitor placed exactly once per round, no duplicates.
            var placed = taskRound.Groups.SelectMany(g => g.CompetitorRefs).ToList();
            placed.Should().HaveCount(12);
            placed.Should().OnlyHaveUniqueItems();
            placed.Should().BeEquivalentTo(competitorIds);
        }
    }

    [Fact]
    public async Task DrawPhase_a_second_time_is_rejected_against_real_postgres()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F5JDefinition, "Redraw Rejected");

        for (var i = 0; i < 6; i++)
        {
            await RegisterCompetitorAsync(fixture, competitionId, $"pilot-redraw-{i}@example.com");
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var first = await drawHandler.HandleAsync(new DrawPhase(competitionId, 2), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        // The point of this test: "only the first, unconditional draw" is a
        // decide-function check over the folded stream, not a unique index —
        // this proves it survives a real append/reload round trip, not just
        // FakeEventStore holding events in memory.
        var second = await drawHandler.HandleAsync(new DrawPhase(competitionId, 2), TestContext.Current.CancellationToken);
        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("drawPhase.alreadyDrawn");
    }

    [Fact]
    public async Task DrawPhase_freezes_the_field_registration_rejected_withdrawal_still_succeeds()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F3JDefinition, "Field Freeze");

        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 6; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-freeze-{i}@example.com"));
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 1), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        // The first real exercise of RegisterCompetitor's ValidateFieldNotFrozen
        // check — "unreachable this thread" in the previous plan, reachable for
        // the first time here, now against a real PostgreSQL round trip.
        var latePerson = await RegisterPersonAsync(fixture, "pilot-freeze-late@example.com");
        var registerHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var lateRegistration = await registerHandler.HandleAsync(
            new RegisterCompetitor(competitionId, latePerson), TestContext.Current.CancellationToken);
        lateRegistration.IsFailure.Should().BeTrue();
        lateRegistration.Code.Should().Be("competition.field.frozen");

        // Withdrawing, by contrast, still succeeds — the draw records who was
        // put in a group at draw time; it does not block a competitor leaving.
        var withdrawHandler = new WithdrawCompetitorHandler(fixture.EventStore, new SystemClock());
        var withdrawn = await withdrawHandler.HandleAsync(
            new WithdrawCompetitor(competitionId, competitorIds[0]), TestContext.Current.CancellationToken);
        withdrawn.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Competitions_read_model_dropped_and_fully_replayed_with_phase_drawn_present_lands_identical()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F3FDefinition, "Replay With Phase Drawn");

        for (var i = 0; i < 10; i++)
        {
            await RegisterCompetitorAsync(fixture, competitionId, $"pilot-replay-drawn-{i}@example.com");
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 2), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var before = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        before.IsSuccess.Should().BeTrue();
        before.Value.Competition.Phases.Single().Rounds.Should().HaveCount(2);

        // Also capture the CompetitionSummary row: CompetitionProjection's
        // pass-through default arm is what this test really targets —
        // PhaseDrawn must fold through the Inline projection without being
        // recognised, leaving the summary's own fields unchanged either side
        // of the drop/rebuild.
        var summaryBefore = await fixture.CompetitionsQuery.SearchAsync(null, null, TestContext.Current.CancellationToken);
        var rowBefore = summaryBefore.Single(c => c.Id == competitionId);

        // Drop the read model's data only — the event log, and therefore
        // GetCompetition's fold, is untouched (LADR-0001 §4.10).
        await fixture.DocumentStore.Advanced.Clean.DeleteDocumentsByTypeAsync(typeof(CompetitionSummary), TestContext.Current.CancellationToken);

        var afterDrop = await fixture.CompetitionsQuery.SearchAsync(null, null, TestContext.Current.CancellationToken);
        afterDrop.Should().NotContain(c => c.Id == competitionId);

        // Replay the whole log — now including PhaseDrawn — through the same
        // Inline projection, on demand, never the async daemon (LADR-0001 §2).
        using var daemon = await fixture.DocumentStore.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync("CompetitionSummaryProjection", TestContext.Current.CancellationToken);

        var summaryAfter = await fixture.CompetitionsQuery.SearchAsync(null, null, TestContext.Current.CancellationToken);
        var rowAfter = summaryAfter.Single(c => c.Id == competitionId);
        rowAfter.Should().BeEquivalentTo(rowBefore);

        // The drawn field itself survives replay because it lives in the
        // event log, re-folded fresh by GetCompetitionHandler; unaffected by
        // the read-model drop, but asserted here to show PhaseDrawn
        // round-trips through a real replay once a real instance of it
        // exists in the log.
        var after = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        after.IsSuccess.Should().BeTrue();
        after.Value.Should().BeEquivalentTo(before.Value);
    }
}
