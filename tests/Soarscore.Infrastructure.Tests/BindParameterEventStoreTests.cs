// kanban/completed/bind-parameter-steel-thread-plan.md WI-7 — the store-backed
// tests for BindParameter, against real PostgreSQL via Testcontainers rather
// than the FakeEventStore double the Application-layer handler tests use.
// Same style as DrawPhaseEventStoreTests.cs: calls the real handlers
// directly against fixture.EventStore, no dispatcher needed for a
// store-level test.
//
// Test 3 is the payoff test the whole plan is building towards: F5K, F5L and
// NZ Class M ALES 200 could not be drawn before this thread because their
// minPerGroup/groupSize parameter has no default (see the plan's Context
// table). The plan's own WI-3 property tests
// (BindParameterPropertyTests.IsolatedToFirstTask) found that F5K's real
// first phase is ChooseFromCatalogue — out of scope for Competition.DrawPhase
// regardless of binding (drawPhase.unsupportedRoundComposition) — and worked
// around it with a synthetically isolated FixedSequence/single-task fixture.
// Checking the other two real seed definitions here (SeedF5L.cs and
// SeedNzMAles200.cs) shows neither needs that workaround: neither's first
// phase sets `Rounds` at all, so both default to
// `RoundComposition { Kind = FixedSequence, TasksPerRound = 1 }`
// (ClassDefinition.cs:93-95), and both declare exactly one task on that
// phase (`Tasks = [TaskD]`). That is precisely the shape
// Competition.DrawPhase requires (Competition.cs:602-604) — so NZ Class M
// ALES 200's real, unmodified definition is used directly below, no
// isolation fixture needed. It is the simpler of the two genuinely-unblocked
// classes (one phase, not two), and it is one of the three classes the plan
// names as blocked before this thread.

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
public sealed class BindParameterEventStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // F3J: literal (non-parameterised) MinPerGroup, single FixedSequence
    // task per phase — used for tests 1, 2 and 4, which do not depend on a
    // parameterised group size, only on ParameterBound round-tripping.
    // flyoffMinRounds is F3J's CompetitionSetup Number parameter with no
    // default and no AllowedValues (SeedF3J.cs) — the same one
    // BindParameterDecideTests.cs and BindParameterHandlerTests.cs already
    // exercise, so a plain MeasuredValue.Of(decimal) bind succeeds cleanly.
    private static readonly ClassDefinition F3JDefinition = Corpus.All.Single(c => c.FileName == "50-f3j").Definition;

    // The payoff test's class — see the file-header comment for why NZ-M
    // ALES 200's real definition needs no isolation fixture, unlike F5K's.
    private static readonly ClassDefinition NzMAles200Definition = Corpus.All.Single(c => c.FileName == "80-nz-m-ales200").Definition;

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
    public async Task BindParameter_survives_an_append_read_round_trip_through_postgres()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F3JDefinition, "Bind Round Trip");

        var bindHandler = new BindParameterHandler(fixture.EventStore, new SystemClock());
        var bound = await bindHandler.HandleAsync(
            new BindParameter(competitionId, "flyoffMinRounds", MeasuredValue.Of(3m), "CD Jane"),
            TestContext.Current.CancellationToken);
        bound.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        fetched.Value.Competition.ParameterBindings.Should().ContainSingle();
        var binding = fetched.Value.Competition.ParameterBindings.Single();
        binding.ParameterName.Should().Be("flyoffMinRounds");
        binding.BoundValue.Should().Be(MeasuredValue.Of(3m));
        binding.By.Should().Be("CD Jane");
    }

    [Fact]
    public async Task Two_bindings_of_one_parameter_both_persist_in_order()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F3JDefinition, "Bind Twice");

        var bindHandler = new BindParameterHandler(fixture.EventStore, new SystemClock());
        var first = await bindHandler.HandleAsync(
            new BindParameter(competitionId, "flyoffMinRounds", MeasuredValue.Of(3m), "CD Jane"),
            TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        var second = await bindHandler.HandleAsync(
            new BindParameter(competitionId, "flyoffMinRounds", MeasuredValue.Of(5m), "CD Jane"),
            TestContext.Current.CancellationToken);
        second.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        // Re-binding before the draw is allowed and the fold only ever
        // appends — last-write-wins is a resolution-time behaviour
        // (Competition.cs:631-633), not a fold-time one, so both entries
        // must remain, in append order.
        fetched.Value.Competition.ParameterBindings.Should().HaveCount(2);
        fetched.Value.Competition.ParameterBindings.Select(b => b.BoundValue)
            .Should().Equal(MeasuredValue.Of(3m), MeasuredValue.Of(5m));
    }

    [Fact]
    public async Task BindParameter_unblocks_DrawPhase_for_NZ_Class_M_ALES_200_and_the_draw_produces_groups_of_the_bound_size()
    {
        var competitionId = await CreateCompetitionAsync(fixture, NzMAles200Definition, "NZ M Payoff");

        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 12; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-nzm-{i}@example.com"));
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());

        // Before this thread this would have failed the same way for good —
        // nothing in the system could clear drawPhase.parameterUnbound.
        var blocked = await drawHandler.HandleAsync(new DrawPhase(competitionId, 2), TestContext.Current.CancellationToken);
        blocked.IsFailure.Should().BeTrue();
        blocked.Code.Should().Be("drawPhase.parameterUnbound");

        var bindHandler = new BindParameterHandler(fixture.EventStore, new SystemClock());
        var bound = await bindHandler.HandleAsync(
            new BindParameter(competitionId, "groupSize", MeasuredValue.Of(6m), "CD Jane"),
            TestContext.Current.CancellationToken);
        bound.IsSuccess.Should().BeTrue();

        // The payoff: real F5K/F5L/NZ-M definition, real store, real draw —
        // must now succeed, with groups of the bound size.
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 2), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        var phase = fetched.Value.Competition.Phases.Single();
        phase.Rounds.Should().HaveCount(2);

        foreach (var round in phase.Rounds)
        {
            var taskRound = round.TaskRounds.Single();
            taskRound.Groups.Should().HaveCount(2);
            taskRound.Groups.Should().OnlyContain(g => g.CompetitorRefs.Length == 6);

            var placed = taskRound.Groups.SelectMany(g => g.CompetitorRefs).ToList();
            placed.Should().HaveCount(12);
            placed.Should().OnlyHaveUniqueItems();
            placed.Should().BeEquivalentTo(competitorIds);
        }
    }

    [Fact]
    public async Task Competitions_read_model_dropped_and_fully_replayed_with_bindings_and_draw_present_lands_identical()
    {
        var competitionId = await CreateCompetitionAsync(fixture, NzMAles200Definition, "Replay With Binding");

        for (var i = 0; i < 10; i++)
        {
            await RegisterCompetitorAsync(fixture, competitionId, $"pilot-replay-bind-{i}@example.com");
        }

        var bindHandler = new BindParameterHandler(fixture.EventStore, new SystemClock());
        var bound = await bindHandler.HandleAsync(
            new BindParameter(competitionId, "groupSize", MeasuredValue.Of(5m), "CD Jane"),
            TestContext.Current.CancellationToken);
        bound.IsSuccess.Should().BeTrue();

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 2), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var before = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        before.IsSuccess.Should().BeTrue();
        before.Value.Competition.ParameterBindings.Should().ContainSingle();
        before.Value.Competition.Phases.Single().Rounds.Should().HaveCount(2);

        // Also capture the CompetitionSummary row: CompetitionProjection's
        // pass-through default arm is what this test really targets —
        // ParameterBound and PhaseDrawn must fold through the Inline
        // projection without being recognised, leaving the summary's own
        // fields unchanged either side of the drop/rebuild.
        var summaryBefore = await fixture.CompetitionsQuery.SearchAsync(null, null, TestContext.Current.CancellationToken);
        var rowBefore = summaryBefore.Single(c => c.Id == competitionId);

        // Drop the read model's data only — the event log, and therefore
        // GetCompetition's fold, is untouched (LADR-0001 §4.10).
        await fixture.DocumentStore.Advanced.Clean.DeleteDocumentsByTypeAsync(typeof(CompetitionSummary), TestContext.Current.CancellationToken);

        var afterDrop = await fixture.CompetitionsQuery.SearchAsync(null, null, TestContext.Current.CancellationToken);
        afterDrop.Should().NotContain(c => c.Id == competitionId);

        // Replay the whole log — now including ParameterBound and PhaseDrawn
        // — through the same Inline projection, on demand, never the async
        // daemon (LADR-0001 §2).
        using var daemon = await fixture.DocumentStore.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync("CompetitionSummaryProjection", TestContext.Current.CancellationToken);

        var summaryAfter = await fixture.CompetitionsQuery.SearchAsync(null, null, TestContext.Current.CancellationToken);
        var rowAfter = summaryAfter.Single(c => c.Id == competitionId);
        rowAfter.Should().BeEquivalentTo(rowBefore);

        // The bindings and the draw themselves survive replay because they
        // live in the event log, re-folded fresh by GetCompetitionHandler;
        // unaffected by the read-model drop, but asserted here to show both
        // round-trip through a real replay once real instances of them exist
        // in the log.
        var after = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        after.IsSuccess.Should().BeTrue();
        after.Value.Should().BeEquivalentTo(before.Value);
    }
}
