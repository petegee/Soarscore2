// docs/plans/bind-parameter-steel-thread-plan.md WI-4. Covers
// BindParameterHandler directly against a FakeEventStore — same style as
// DrawPhaseHandlerTests.cs: no cross-aggregate read, the adopted class
// definition is already sitting in AdoptedRules. Uses SeedF3J's
// flyoffMinRounds — a CompetitionSetup Number parameter with no default and
// no AllowedValues (SeedF3J.cs), the same parameter WI-1's own domain tests
// (BindParameterDecideTests.cs) use for its success/frozen cases — so a plain
// MeasuredValue.Of(decimal) bind succeeds cleanly.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests.Competitions;

public class BindParameterHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F3J = SeedF3J.Definition; // flyoffMinRounds: CompetitionSetup, Number, no default

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F3J,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F3J.Version,
            AdoptedAt = Now,
        };

    private static (FakeEventStore Store, CompetitionId CompetitionId) SeedCompetition()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var created = new CompetitionCreated(
            id, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return (store, id);
    }

    [Fact]
    public async Task Binding_a_declared_parameter_succeeds_and_appends_exactly_one_event_at_the_next_version()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new BindParameterHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new BindParameter(competitionId, "flyoffMinRounds", MeasuredValue.Of(3m), "CD Jane"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(competitionId);

        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(2); // 1 created + 1 bound
        var bound = stream[1].Should().BeOfType<ParameterBound>().Subject;
        bound.Binding.ParameterName.Should().Be("flyoffMinRounds");
        bound.Binding.BoundValue.Should().Be(MeasuredValue.Of(3m));
        bound.Binding.By.Should().Be("CD Jane");

        var folded = Competition.Apply(null, (CompetitionEvent)stream[0])!.Apply(bound);
        folded.ParameterBindings.Should().ContainSingle(b => b.ParameterName == "flyoffMinRounds");
    }

    [Fact]
    public async Task Binding_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var handler = new BindParameterHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new BindParameter(CompetitionId.New(), "flyoffMinRounds", MeasuredValue.Of(3m), "CD"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Binding_an_undeclared_parameter_name_surfaces_the_decide_functions_code_unchanged()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new BindParameterHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new BindParameter(competitionId, "notAThing", MeasuredValue.Of(5m), "CD"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.notDeclared");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Binding_with_an_empty_or_whitespace_By_fails_with_competition_parameter_byRequired_before_the_decide_function_runs(string by)
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new BindParameterHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new BindParameter(competitionId, "flyoffMinRounds", MeasuredValue.Of(3m), by),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.byRequired");

        // Nothing appended — the check runs before BindParameter is even called.
        store.Streams[competitionId.Value].Should().HaveCount(1);
    }

    [Fact]
    public async Task Binding_with_the_wrong_MeasuredKind_surfaces_the_decide_functions_code_unchanged()
    {
        // F3J's carryPenalties is a Flag parameter (SeedF3J.cs).
        var (store, competitionId) = SeedCompetition();
        var handler = new BindParameterHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new BindParameter(competitionId, "carryPenalties", MeasuredValue.Of(5m), "CD"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.kindMismatch");
    }

    [Fact]
    public async Task Binding_a_CompetitionSetup_parameter_after_a_phase_is_drawn_surfaces_frozen_unchanged()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new BindParameterHandler(store, new FakeClock(Now));

        // Enough field to draw. Register 12 competitors then draw.
        var version = 1L;
        for (var i = 0; i < 12; i++)
        {
            var competitor = new Competitor
            {
                Id = CompetitorId.New(),
                PersonRef = Soarscore.Domain.People.PersonId.New(),
                CompetitorNumber = i + 1,
                RegisteredAt = Now,
            };
            await store.AppendAsync(
                competitionId.Value, ExpectedVersion.Exact(version), [new CompetitorRegistered(competitor, Now)],
                TestContext.Current.CancellationToken);
            version++;
        }

        var drawHandler = new DrawPhaseHandler(store, new FakeClock(Now));
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 1), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        var result = await handler.HandleAsync(
            new BindParameter(competitionId, "flyoffMinRounds", MeasuredValue.Of(3m), "CD"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.frozen");
    }

    [Fact]
    public async Task A_stale_read_version_fails_with_eventStore_concurrencyConflict_on_append()
    {
        var (store, competitionId) = SeedCompetition();

        // A rebind lands for real between this handler's read and its
        // append — the append's ExpectedVersion.Exact, computed from the
        // stale one-event read below, no longer matches the store's actual
        // two-event stream.
        await store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(1),
            [new ParameterBound(new ParameterBinding { ParameterName = "flyoffMinRounds", BoundValue = MeasuredValue.Of(4m), By = "Other CD", At = Now })],
            TestContext.Current.CancellationToken);

        var staleReadStore = new StaleReadEventStore(store, competitionId.Value, visibleCount: 1);
        var handler = new BindParameterHandler(staleReadStore, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new BindParameter(competitionId, "flyoffMinRounds", MeasuredValue.Of(3m), "CD"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("eventStore.concurrencyConflict");
    }

    [Fact]
    public async Task Appended_ParameterBound_folds_idempotently_when_applied_twice()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new BindParameterHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new BindParameter(competitionId, "flyoffMinRounds", MeasuredValue.Of(3m), "CD Jane"),
            TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();

        var events = store.Streams[competitionId.Value];
        var priorEvents = events.Take(events.Count - 1);
        var priorState = priorEvents.Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
        var parameterBound = (ParameterBound)events[^1];

        var first = priorState.Apply(parameterBound);
        var second = priorState.Apply(parameterBound);

        second.ParameterBindings.Should().Equal(first.ParameterBindings);
    }

    /// <summary>
    /// Wraps a real FakeEventStore but truncates one stream's ReadStreamAsync
    /// result — standing in for a read that happened before a concurrent
    /// append landed, so the handler under test computes an
    /// ExpectedVersion.Exact that is already stale by the time it appends.
    /// Mirrors DrawPhaseHandlerTests's private double of the same name.
    /// </summary>
    private sealed class StaleReadEventStore(IEventStore inner, Guid staleStreamId, int visibleCount) : IEventStore
    {
        public Task<Result<long>> AppendAsync(
            Guid streamId, ExpectedVersion expected, IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default) =>
            inner.AppendAsync(streamId, expected, events, cancellationToken);

        public async Task<Result<IReadOnlyList<IDomainEvent>>> ReadStreamAsync(
            Guid streamId, long fromVersion, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadStreamAsync(streamId, fromVersion, cancellationToken);
            if (read.IsFailure || streamId != staleStreamId)
            {
                return read;
            }

            return Result<IReadOnlyList<IDomainEvent>>.Success(read.Value.Take(visibleCount).ToList());
        }

        public Task<Result<IReadOnlyList<RecordedEvent>>> ReadAllAsync(
            long fromPosition, int batchSize, CancellationToken cancellationToken = default) =>
            inner.ReadAllAsync(fromPosition, batchSize, cancellationToken);
    }
}
