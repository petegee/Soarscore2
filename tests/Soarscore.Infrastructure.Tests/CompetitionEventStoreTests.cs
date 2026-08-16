// kanban/completed/create-competition-steel-thread-plan.md WI-6 — the three
// store-backed tests that carry real weight for CreateCompetition, against a
// real PostgreSQL via Testcontainers rather than the FakeEventStore double
// the Application-layer handler tests use. Calls the real handlers directly
// against fixture.EventStore/fixture.CompetitionsQuery — same style as
// ClassDefinitionEventStoreTests.cs, no dispatcher needed for a store-level
// test.
//
// kanban/completed/multi-backend-deployment.md WI-6 made these generic over
// the fixture, so they now run unchanged against every backend Soarscore
// supports — Marten/PostgreSQL and Fisher/SQLite — one concrete subclass per
// backend at the foot of the file. Only the Postgres subclass keeps
// Trait("Category", "Storage"); EventStoreTests.cs's header says why.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Shared.CompetitionClasses;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class CompetitionEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    // F5K: the richest payload the event log holds for this thread — same
    // choice as ClassDefinitionEventStoreTests.cs's RichestDefinition.
    private static readonly ClassDefinition RichestDefinition = Corpus.All.Single(c => c.FileName == "40-f5k").Definition;

    // A distinct definition (different content hash) for the retirement test:
    // all three tests in this class share one PostgresFixture/container
    // (IClassFixture is one instance per test class), so retiring
    // RichestDefinition's hash here would silently poison the other two
    // tests, which expect that hash to stay adoptable.
    private static readonly ClassDefinition RetirementCandidateDefinition = Corpus.All.Single(c => c.FileName == "10-f3k").Definition;

    [Fact]
    public async Task CreateCompetition_adopting_a_published_definition_round_trips_the_full_definition()
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(RichestDefinition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition("Nationals", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            TestContext.Current.CancellationToken);
        created.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(created.Value), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        // Compares by content hash rather than record equality: ImmutableArray<T>.Equals
        // is reference-based (LADR-0003), same gotcha ClassDefinitionEventStoreTests.cs
        // notes for the class-definition round trip.
        ClassDefinitionHashing.ComputeContentHash(fetched.Value.Competition.AdoptedRules.Definition).Should().Be(published.Value);
    }

    [Fact]
    public async Task CreateCompetition_against_a_retired_definition_is_rejected()
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(RetirementCandidateDefinition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        // RetireClassDefinition the command does not exist yet (out of scope
        // elsewhere) — append ClassDefinitionRetired directly, same approach
        // the plan specifies.
        var streamId = ClassDefinitionStreamId.From(published.Value);
        var retire = await fixture.EventStore.AppendAsync(
            streamId,
            ExpectedVersion.Exact(1),
            [new ClassDefinitionRetired("test", DateTimeOffset.UtcNow)],
            TestContext.Current.CancellationToken);
        retire.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition("Nationals", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            TestContext.Current.CancellationToken);

        created.IsFailure.Should().BeTrue();
        created.Code.Should().Be("createCompetition.classDefinitionRetired");
    }

    [Fact]
    public async Task Competitions_read_model_dropped_and_fully_replayed_lands_identical()
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(RichestDefinition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition("Nationals", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            TestContext.Current.CancellationToken);
        created.IsSuccess.Should().BeTrue();

        var before = await fixture.CompetitionsQuery.SearchAsync(null, published.Value, TestContext.Current.CancellationToken);
        before.Should().ContainSingle(c => c.Id == created.Value);

        // Drop the read model's data only — the event log is untouched (LADR-0001 §4.10).
        await fixture.DropDocumentsAsync<CompetitionSummary>(TestContext.Current.CancellationToken);

        var afterDrop = await fixture.CompetitionsQuery.SearchAsync(null, published.Value, TestContext.Current.CancellationToken);
        afterDrop.Should().BeEmpty();

        // Replay the whole log through the same Inline projection, on demand —
        // never the continuously-running async daemon (LADR-0001 §2).
        await fixture.RebuildProjectionAsync("CompetitionSummaryProjection", TestContext.Current.CancellationToken);

        var afterRebuild = await fixture.CompetitionsQuery.SearchAsync(null, published.Value, TestContext.Current.CancellationToken);
        afterRebuild.Should().BeEquivalentTo(before);
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresCompetitionEventStoreTests(PostgresFixture fixture) : CompetitionEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteCompetitionEventStoreTests(SqliteFixture fixture) : CompetitionEventStoreTests<SqliteFixture>(fixture);
