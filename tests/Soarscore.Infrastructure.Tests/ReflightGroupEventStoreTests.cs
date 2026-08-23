// kanban/in-progress/reflight-groups.md WI-9 ("Store-backed") — the store-backed
// test for AppendReflightGroup, mirroring TaskRoundLifecycleEventStoreTests.cs
// exactly: real handlers, read-back through GetCompetitionHandler so the fold
// is fresh (that file's header, lines 5-17, states why).
//
// This is the test the plan names as "the suite that fails at runtime if WI-4's
// registration line is missing": appending ReflightGroupAppended without its
// line in SoarscoreEventTypes.All fails at runtime on BOTH backends per
// LADR-0001 §4.8, and nothing below can pass without a real append-and-re-fold
// through a real store.
//
// kanban/completed/multi-backend-deployment.md WI-6's shape, same as
// BindParameterEventStoreTests.cs / ScoringEventStoreTests.cs: written once
// against IStoreFixture, with one concrete subclass per backend at the foot of
// the file. Only the Postgres subclass keeps Trait("Category", "Storage");
// EventStoreTests.cs's header says why.
//
// F5J (30-f5j): literal MinPerGroup 6 and reflight minimum 6 (SeedF5J.cs:146-150)
// — a 6-pilot field draws to exactly one group, and a 6-member reflight group
// is the minimum the class requires.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class ReflightGroupEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------- setup

    private static async Task<CompetitionId> CreateCompetitionAsync(IStoreFixture fixture, string name)
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(F5JDefinition), Ct);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition(name, "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            Ct);
        created.IsSuccess.Should().BeTrue($"{created.Code}: {created.Message}");

        return created.Value;
    }

    private static async Task<CompetitorId> RegisterCompetitorAsync(IStoreFixture fixture, CompetitionId competitionId, string email)
    {
        var registerPersonHandler = new RegisterPersonHandler(fixture.EventStore, new SystemClock());
        var person = await registerPersonHandler.HandleAsync(
            new RegisterPerson("Test Pilot", new ContactDetails { Email = email }, Club: null), Ct);
        person.IsSuccess.Should().BeTrue();

        var registerCompetitorHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var competitor = await registerCompetitorHandler.HandleAsync(
            new RegisterCompetitor(competitionId, person.Value), Ct);
        competitor.IsSuccess.Should().BeTrue();

        return competitor.Value;
    }

    private static async Task<Competition> LoadAsync(IStoreFixture fixture, CompetitionId competitionId)
    {
        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), Ct);
        fetched.IsSuccess.Should().BeTrue($"{fetched.Code}: {fetched.Message}");
        return fetched.Value.Competition;
    }

    /// <summary>Creates the competition, registers six pilots and draws one round — F5J's literal MinPerGroup 6 is exactly one group.</summary>
    private static async Task<(CompetitionId CompetitionId, List<CompetitorId> Competitors)> DrawnCompetitionAsync(
        IStoreFixture fixture, string name, string emailSlug)
    {
        var competitionId = await CreateCompetitionAsync(fixture, name);

        var competitors = new List<CompetitorId>();
        for (var i = 0; i < 6; i++)
        {
            competitors.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-{emailSlug}-{i}@example.com"));
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 1), Ct);
        drawn.IsSuccess.Should().BeTrue($"{drawn.Code}: {drawn.Message}");

        return (competitionId, competitors);
    }

    // ---- 1. A 6-member reflight group appends and re-folds fresh ---------

    [Fact]
    public async Task AppendReflightGroup_round_trips_through_the_real_store_and_folds_a_second_group_on()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Reflight Round Trip", "reflight");

        var appendHandler = new AppendReflightGroupHandler(fixture.EventStore, new SystemClock());
        var appended = await appendHandler.HandleAsync(
            new AppendReflightGroup(competitionId, 0, 1, 1, competitors, "Mid-air collision"), Ct);
        appended.IsSuccess.Should().BeTrue($"{appended.Code}: {appended.Message}");

        // Read back through the store, so it is the re-folded stream asserting,
        // not anything the handler held in memory.
        var competition = await LoadAsync(fixture, competitionId);
        var taskRound = competition.Phases.Single().Rounds.Single().TaskRounds.Single();
        taskRound.Groups.Should().HaveCount(2);

        var reflightGroup = taskRound.Groups.Single(g => g.Id == appended.Value);
        reflightGroup.Ordinal.Should().Be(2);
        reflightGroup.CompetitorRefs.Should().BeEquivalentTo(competitors);
    }

    // ---- 2. The failure codes survive the JSON round trip ----------------

    [Fact]
    public async Task A_group_below_the_class_minimum_is_refused_through_the_real_store()
    {
        // F5J's reflight minimum is 6 (5.5.11.6); five members must be refused
        // with the domain code, surfaced through the real append path.
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Reflight Too Small", "too-small");

        var appendHandler = new AppendReflightGroupHandler(fixture.EventStore, new SystemClock());
        var appended = await appendHandler.HandleAsync(
            new AppendReflightGroup(competitionId, 0, 1, 1, competitors.Take(5).ToList(), "Mid-air collision"), Ct);

        appended.IsFailure.Should().BeTrue();
        appended.Code.Should().Be("appendReflightGroup.groupTooSmall");
    }

    [Fact]
    public async Task A_blank_reason_is_refused_through_the_real_store()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Reason Required", "reason");

        var appendHandler = new AppendReflightGroupHandler(fixture.EventStore, new SystemClock());
        var appended = await appendHandler.HandleAsync(
            new AppendReflightGroup(competitionId, 0, 1, 1, competitors, "   "), Ct);

        appended.IsFailure.Should().BeTrue();
        appended.Code.Should().Be("appendReflightGroup.reasonRequired");
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresReflightGroupEventStoreTests(PostgresFixture fixture)
    : ReflightGroupEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteReflightGroupEventStoreTests(SqliteFixture fixture)
    : ReflightGroupEventStoreTests<SqliteFixture>(fixture);