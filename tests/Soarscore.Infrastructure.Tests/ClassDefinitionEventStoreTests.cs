// kanban/completed/class-definition-adoption-steel-thread-plan.md WI-7 — the three
// store-backed tests that carry real weight, against a real PostgreSQL via
// Testcontainers rather than the FakeEventStore double the Application-layer
// handler tests use. Calls the real handlers directly against
// fixture.EventStore/fixture.ClassLibraryQuery — same style as
// MartenEventStoreTests.cs, no dispatcher needed for a store-level test.
//
// kanban/completed/multi-backend-deployment.md WI-6 made these generic over
// the fixture, so they now run unchanged against every backend Soarscore
// supports — Marten/PostgreSQL and Fisher/SQLite — one concrete subclass per
// backend at the foot of the file. Only the Postgres subclass keeps
// Trait("Category", "Storage"); EventStoreTests.cs's header says why.

using AwesomeAssertions;
using System.Collections.Immutable;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Application.Shared.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class ClassDefinitionEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    // F5K: the richest payload the event log holds for this thread — every
    // ScoreTerm subtype (Rate/Lookup/Piecewise/Constant/Conditional), a
    // ParameterRef in a PiecewiseTerm.origin slot (the announced NLH), and
    // cumulative Bands.
    private static readonly ClassDefinition RichestDefinition = Corpus.All.Single(c => c.FileName == "40-f5k").Definition;

    [Fact]
    public async Task Publish_then_GetClassDefinition_round_trips_the_full_definition()
    {
        var handler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await handler.HandleAsync(new PublishClassDefinition(RichestDefinition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var getHandler = new GetClassDefinitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetClassDefinition(published.Value), TestContext.Current.CancellationToken);

        fetched.IsSuccess.Should().BeTrue();

        // Compares by content hash rather than record equality: ImmutableArray<T>.Equals
        // is reference-based (LADR-0003), so round-trip correctness is a canonical-JSON
        // question, not a `.Should().Be()` one.
        ClassDefinitionHashing.ComputeContentHash(fetched.Value).Should().Be(published.Value);
    }

    // kanban/in-progress/metric-absence-semantics.md WI-1: the richest payload
    // with a whenNotRecorded assumption added to the first metric of every
    // task — a Flag metric assumed true, a Number metric assumed 0, exactly as
    // the notation will write it. Round-trips through both stores: the event
    // JSON carries it as the same MeasuredValue shape the predicates'
    // rightValue uses.
    private static readonly ClassDefinition WhenNotRecordedDefinition = WithWhenNotRecorded(RichestDefinition);

    private static ClassDefinition WithWhenNotRecorded(ClassDefinition definition) => definition with
    {
        Phases = definition.Phases
            .Select(p => p with
            {
                Tasks = p.Tasks
                    .Select(t => t with
                    {
                        Metrics = t.Metrics
                            .Select((m, i) => i == 0
                                ? m with { WhenNotRecorded = m.Kind == MeasuredKind.Flag
                                    ? MeasuredValue.Of(true)
                                    : MeasuredValue.Of(0m) }
                                : m)
                            .ToImmutableArray()
                    })
                    .ToImmutableArray()
            })
            .ToImmutableArray()
    };

    [Fact]
    public async Task Publish_then_GetClassDefinition_round_trips_whenNotRecorded_assumed_values()
    {
        var handler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await handler.HandleAsync(new PublishClassDefinition(WhenNotRecordedDefinition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var getHandler = new GetClassDefinitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetClassDefinition(published.Value), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        // Content-hash equality against the ORIGINAL definition proves the
        // serialised shape round-trips every whenNotRecorded field.
        ClassDefinitionHashing.ComputeContentHash(fetched.Value).Should().Be(published.Value);

        // And directly: every task's first metric comes back with the assumed
        // value it was published with.
        var metrics = fetched.Value.Phases.SelectMany(p => p.Tasks).Select(t => t.Metrics[0]);
        metrics.Should().OnlyContain(m => m.WhenNotRecorded != null);
        var publishedMetrics = WhenNotRecordedDefinition.Phases.SelectMany(p => p.Tasks).Select(t => t.Metrics[0]);
        metrics.Should().Equal(publishedMetrics);
    }

    [Fact]
    public async Task Publishing_identical_content_twice_appends_exactly_one_event_and_both_calls_return_the_same_hash()
    {
        var handler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());

        var first = await handler.HandleAsync(new PublishClassDefinition(RichestDefinition), TestContext.Current.CancellationToken);
        var second = await handler.HandleAsync(new PublishClassDefinition(RichestDefinition), TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().Be(first.Value);

        var streamId = ClassDefinitionStreamId.From(first.Value);
        var stream = await fixture.EventStore.ReadStreamAsync(streamId, 0, TestContext.Current.CancellationToken);
        stream.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task Class_library_read_model_dropped_and_fully_replayed_lands_identical()
    {
        var handler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await handler.HandleAsync(new PublishClassDefinition(RichestDefinition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var before = await fixture.ClassLibraryQuery.FindByHashAsync(published.Value, TestContext.Current.CancellationToken);
        before.Should().NotBeNull();

        // Drop the read model's data only — the event log is untouched (LADR-0001 §4.10).
        await fixture.DropDocumentsAsync<ClassDefinitionSummary>(TestContext.Current.CancellationToken);

        var afterDrop = await fixture.ClassLibraryQuery.FindByHashAsync(published.Value, TestContext.Current.CancellationToken);
        afterDrop.Should().BeNull();

        // Replay the whole log through the same Inline projection, on demand —
        // never the continuously-running async daemon (LADR-0001 §2).
        await fixture.RebuildProjectionAsync("ClassDefinitionSummaryProjection", TestContext.Current.CancellationToken);

        var afterRebuild = await fixture.ClassLibraryQuery.FindByHashAsync(published.Value, TestContext.Current.CancellationToken);
        afterRebuild.Should().Be(before);
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresClassDefinitionEventStoreTests(PostgresFixture fixture) : ClassDefinitionEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteClassDefinitionEventStoreTests(SqliteFixture fixture) : ClassDefinitionEventStoreTests<SqliteFixture>(fixture);
