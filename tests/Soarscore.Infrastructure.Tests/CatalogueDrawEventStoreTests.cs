// kanban/in-progress/catalogue-choice-draws-plan.md WI-6 — the store-backed
// payoff tests for catalogue-choice draws, against a real PostgreSQL via
// Testcontainers. Same style as DrawPhaseEventStoreTests.cs /
// BindParameterEventStoreTests.cs: calls the real handlers directly against
// fixture.EventStore/fixture.EntryQuery, no dispatcher needed for a
// store-level test.
//
// kanban/completed/multi-backend-deployment.md WI-6 made these generic over
// the fixture, so they now run unchanged against every backend Soarscore
// supports — Marten/PostgreSQL and Fisher/SQLite — one concrete subclass per
// backend at the foot of the file. Only the Postgres subclass keeps
// Trait("Category", "Storage"); EventStoreTests.cs's header says why.
//
// F3K (10-f3k) and F5K (40-f5k) are used throughout — the two corpus classes
// this thread's own plan exists to unblock. Both declare
// CompositionKind.ChooseFromCatalogue phases, which Competition.DrawPhase
// rejected outright before this thread. F3K's task A carries a literal
// Group.MinPerGroup == 5 (SeedF3K.cs); task D is its `like`-derived twin,
// same three flight metrics (flightTime, landedWithinWindow,
// launchedInWorkingTime) and Fixed timing, so the two can stand in for
// "different tasks, same shape" without dragging in F3K's more exotic
// catalogue members (Poker targets, UntilAllFlightsComplete, ...).
//
// F5K's Group.MinPerGroup is `param("minPerGroup")` with NO declared default
// (SeedF5K.cs:103,315) — the leg BindParameterEventStoreTests.cs's own
// header comment says it had to abandon to NZ Class M ALES 200 because
// catalogue choice blocked the draw before BindParameter could even be
// exercised for F5K. Test 2 below is that leg, finally completing.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class CatalogueDrawEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly ClassDefinition F3KDefinition = Corpus.All.Single(c => c.FileName == "10-f3k").Definition;
    private static readonly ClassDefinition F5KDefinition = Corpus.All.Single(c => c.FileName == "40-f5k").Definition;

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

    private static async Task<CompetitorId> RegisterCompetitorAsync(IStoreFixture fixture, CompetitionId competitionId, string email)
    {
        var registerPersonHandler = new RegisterPersonHandler(fixture.EventStore, new SystemClock());
        var person = await registerPersonHandler.HandleAsync(
            new RegisterPerson("Test Pilot", new ContactDetails { Email = email }, Club: null),
            TestContext.Current.CancellationToken);
        person.IsSuccess.Should().BeTrue();

        var registerCompetitorHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var competitor = await registerCompetitorHandler.HandleAsync(
            new RegisterCompetitor(competitionId, person.Value), TestContext.Current.CancellationToken);
        competitor.IsSuccess.Should().BeTrue();

        return competitor.Value;
    }

    private static async Task<List<CompetitorId>> RegisterFieldAsync(IStoreFixture fixture, CompetitionId competitionId, string tag, int count)
    {
        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < count; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-{tag}-{i}@example.com"));
        }

        return competitorIds;
    }

    // ---- 1. The F3K payoff ---------------------------------------------------

    [Fact]
    public async Task DrawPhase_real_corpus_F3K_five_distinct_catalogue_tasks_succeeds_with_the_right_task_per_round()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F3KDefinition, "F3K Catalogue Payoff");

        // F3K task A's literal Group.MinPerGroup == 5 (SeedF3K.cs) — every
        // catalogue task inherits it through `with`, so a field of exactly 5
        // gives one group per round regardless of which task is drawn.
        await RegisterFieldAsync(fixture, competitionId, "f3k", 5);

        var taskRefs = new[] { "A", "B", "C", "D", "E" };
        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(
            new DrawPhase(competitionId, 5, taskRefs), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue($"{drawn.Code}: {drawn.Message}");

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        var rounds = fetched.Value.Competition.Phases.Single().Rounds;
        rounds.Should().HaveCount(5);
        rounds.OrderBy(r => r.Ordinal).Select(r => r.TaskRounds.Single().TaskRef).Should().Equal(taskRefs);

        foreach (var round in rounds)
        {
            var taskRound = round.TaskRounds.Single();
            taskRound.Groups.Should().HaveCount(1);
            taskRound.Groups.Single().CompetitorRefs.Should().HaveCount(5);
        }
    }

    // ---- 2. The F5K payoff — the leg BindParameterEventStoreTests abandoned --

    [Fact]
    public async Task DrawPhase_real_corpus_F5K_without_binding_minPerGroup_fails_then_binding_and_naming_tasks_succeeds()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F5KDefinition, "F5K Catalogue Payoff");

        await RegisterFieldAsync(fixture, competitionId, "f5k", 5);

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());

        // Before binding: minPerGroup has no declared default (SeedF5K.cs),
        // so even a valid task selection cannot resolve group sizing.
        var beforeBind = await drawHandler.HandleAsync(
            new DrawPhase(competitionId, 3, ["A", "B", "C"]), TestContext.Current.CancellationToken);
        beforeBind.IsFailure.Should().BeTrue();
        beforeBind.Code.Should().Be("drawPhase.parameterUnbound");

        var bindHandler = new BindParameterHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
        var bound = await bindHandler.HandleAsync(
            new BindParameter(competitionId, "minPerGroup", MeasuredValue.Of(5m), "cd"), TestContext.Current.CancellationToken);
        bound.IsSuccess.Should().BeTrue($"{bound.Code}: {bound.Message}");

        var taskRefs = new[] { "A", "B", "C" };
        var afterBind = await drawHandler.HandleAsync(
            new DrawPhase(competitionId, 3, taskRefs), TestContext.Current.CancellationToken);
        afterBind.IsSuccess.Should().BeTrue($"{afterBind.Code}: {afterBind.Message}");

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        var rounds = fetched.Value.Competition.Phases.Single().Rounds;
        rounds.Should().HaveCount(3);
        rounds.OrderBy(r => r.Ordinal).Select(r => r.TaskRounds.Single().TaskRef).Should().Equal(taskRefs);
        rounds.Should().OnlyContain(r => r.TaskRounds.Single().Groups.Single().CompetitorRefs.Length == 5);
    }

    // ---- 3. Replay: the per-round TaskRef survives a drop and rebuild -------

    [Fact]
    public async Task Competitions_read_model_dropped_and_replayed_with_a_catalogue_draw_lands_identical_including_every_TaskRef()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F3KDefinition, "F3K Catalogue Replay");

        await RegisterFieldAsync(fixture, competitionId, "f3k-replay", 5);

        var taskRefs = new[] { "A", "D", "E" };
        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(
            new DrawPhase(competitionId, 3, taskRefs), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue($"{drawn.Code}: {drawn.Message}");

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var before = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        before.IsSuccess.Should().BeTrue();
        before.Value.Competition.Phases.Single().Rounds
            .OrderBy(r => r.Ordinal).Select(r => r.TaskRounds.Single().TaskRef).Should().Equal(taskRefs);

        // Drop the read model's data only — the event log, and therefore
        // GetCompetition's fold, is untouched (LADR-0001 §4.10), the same
        // pattern DrawPhaseEventStoreTests.cs's replay test uses.
        await fixture.DropDocumentsAsync<CompetitionSummary>(TestContext.Current.CancellationToken);

        await fixture.RebuildProjectionAsync("CompetitionSummaryProjection", TestContext.Current.CancellationToken);

        var after = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        after.IsSuccess.Should().BeTrue();
        after.Value.Should().BeEquivalentTo(before.Value);

        after.Value.Competition.Phases.Single().Rounds
            .OrderBy(r => r.Ordinal).Select(r => r.TaskRounds.Single().TaskRef).Should().Equal(taskRefs);
    }

    // ---- 4. Capture and score through the catalogue phase --------------------

    [Fact]
    public async Task Capturing_and_scoring_two_rounds_with_different_catalogue_tasks_succeeds_end_to_end()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F3KDefinition, "F3K Catalogue Capture");

        var competitorIds = await RegisterFieldAsync(fixture, competitionId, "f3k-capture", 5);

        // Task A and task D: both Fixed timing, both share the same three
        // FlightMetrics (SeedF3K.cs) — "a different task per round" without
        // dragging in Poker targets (E) or UntilAllFlightsComplete (C).
        var taskRefs = new[] { "A", "D" };
        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(
            new DrawPhase(competitionId, 2, taskRefs), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue($"{drawn.Code}: {drawn.Message}");

        // D4 (kanban/in-progress/draw-acceptance-redraw.md): entries need an
        // accepted draw. Tests 1-3 above deliberately never accept — they are
        // the regression guard for a drawn-but-not-accepted competition still
        // reading correctly; this one opens entries, so it accepts.
        var accepted = await new AcceptDrawHandler(fixture.EventStore, new SystemClock())
            .HandleAsync(new AcceptDraw(competitionId), TestContext.Current.CancellationToken);
        accepted.IsSuccess.Should().BeTrue($"{accepted.Code}: {accepted.Message}");

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        var rounds = fetched.Value.Competition.Phases.Single().Rounds.OrderBy(r => r.Ordinal).ToList();
        rounds.Should().HaveCount(2);
        rounds[0].TaskRounds.Single().TaskRef.Should().Be("A");
        rounds[1].TaskRounds.Single().TaskRef.Should().Be("D");

        var openEntryHandler = new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
        var openFlightHandler = new OpenFlightHandler(fixture.EventStore, new SystemClock());
        var captureHandler = new CaptureMeasurementHandler(fixture.EventStore, new SystemClock());

        var launchAt = new DateTimeOffset(2026, 1, 10, 9, 3, 0, TimeSpan.Zero);

        foreach (var round in rounds)
        {
            var taskRound = round.TaskRounds.Single();
            var group = taskRound.Groups.Single();

            foreach (var (competitorRef, index) in group.CompetitorRefs.Select((c, i) => (c, i)))
            {
                var opened = await openEntryHandler.HandleAsync(
                    new OpenEntry(competitionId, 0, round.Ordinal, taskRound.Ordinal, group.Id, competitorRef),
                    TestContext.Current.CancellationToken);
                opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");
                var entryId = opened.Value;

                var openedFlight = await openFlightHandler.HandleAsync(
                    new OpenFlight(entryId), TestContext.Current.CancellationToken);
                openedFlight.IsSuccess.Should().BeTrue($"{openedFlight.Code}: {openedFlight.Message}");

                async Task CaptureAsync(string metric, MeasuredValue value)
                {
                    var captured = await captureHandler.HandleAsync(
                        new CaptureMeasurement(entryId, 1, metric, value), TestContext.Current.CancellationToken);
                    captured.IsSuccess.Should().BeTrue($"{captured.Code}: {captured.Message}");
                }

                await CaptureAsync("flightTime", MeasuredValue.Of(150m + index * 10));
                await CaptureAsync("landedWithinWindow", MeasuredValue.Of(true));
                await CaptureAsync("launchedInWorkingTime", MeasuredValue.Of(true));
            }
        }

        // The payoff: ScoreCompetition drives TaskResolver / ScoringService
        // across BOTH task-rounds — proving the per-task-round path is real,
        // not merely believed to work from the domain-level property tests.
        var scoreHandler = new ScoreCompetitionHandler(fixture.EventStore, fixture.EntryQuery);
        var scored = await scoreHandler.HandleAsync(new ScoreCompetition(competitionId), TestContext.Current.CancellationToken);
        scored.IsSuccess.Should().BeTrue($"{scored.Code}: {scored.Message}");

        scored.Value.Scores.Should().HaveCount(5);
        scored.Value.Scores.Select(s => s.CompetitorRef).Should().BeEquivalentTo(competitorIds);
        scored.Value.Scores.Should().OnlyContain(s => !s.Disqualified && s.Placing != null);
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresCatalogueDrawEventStoreTests(PostgresFixture fixture) : CatalogueDrawEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteCatalogueDrawEventStoreTests(SqliteFixture fixture) : CatalogueDrawEventStoreTests<SqliteFixture>(fixture);
