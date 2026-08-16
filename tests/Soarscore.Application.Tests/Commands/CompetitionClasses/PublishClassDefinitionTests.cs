// docs/plans/class-definition-adoption-steel-thread-plan.md WI-4. Application
// tests via IDispatcher, a fake IEventStore, a fake clock — mirrors
// People/PersonCommandsTests.cs's shape.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.CompetitionClasses;

using Soarscore.Application.Shared.CompetitionClasses;
using Soarscore.Application.Queries.CompetitionClasses;
namespace Soarscore.Application.Tests.Commands.CompetitionClasses;

public class PublishClassDefinitionTests
{
    private static readonly ClassDefinition ValidDefinition = Corpus.All[0].Definition;

    private static IDispatcher BuildDispatcher(FakeEventStore eventStore, FakeClock clock)
    {
        var services = new Dictionary<Type, object>
        {
            [typeof(ICommandHandler<PublishClassDefinition, string>)] = new PublishClassDefinitionHandler(eventStore, clock),
            [typeof(IQueryHandler<GetClassDefinition, ClassDefinition>)] = new GetClassDefinitionHandler(eventStore),
        };
        return new Dispatcher(new FakeServiceProvider(services));
    }

    [Fact]
    public async Task A_valid_definition_publishes_once_and_returns_its_content_hash()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));

        var result = await dispatcher.SendAsync(new PublishClassDefinition(ValidDefinition), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ClassDefinitionHashing.ComputeContentHash(ValidDefinition));

        var streamId = ClassDefinitionStreamId.From(result.Value);
        eventStore.Streams[streamId].Should().ContainSingle().Which.Should().BeOfType<ClassDefinitionPublished>();
    }

    [Fact]
    public async Task Publishing_the_same_definition_twice_returns_the_same_hash_and_appends_only_one_event()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));

        var first = await dispatcher.SendAsync(new PublishClassDefinition(ValidDefinition), TestContext.Current.CancellationToken);
        var second = await dispatcher.SendAsync(new PublishClassDefinition(ValidDefinition), TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().Be(first.Value);

        var streamId = ClassDefinitionStreamId.From(first.Value);
        eventStore.Streams[streamId].Should().ContainSingle();
    }

    [Fact]
    public async Task An_invalid_definition_fails_and_carries_every_defect_Validate_found()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));

        // Two independent violations at once: check 12 (multi-phase, no finalRanking)
        // and check 14 (normalised terms with no normalise stage) — proves the
        // handler surfaces every defect Validate() found, not just the first.
        var phase = ValidDefinition.Phases[0];
        var brokenTask = phase.Tasks[0] with { ScoreNormalised = [new ConstantTerm { Value = 1 }], Normalise = null };
        var brokenPhase = phase with { Tasks = [brokenTask] };
        var invalidDefinition = ValidDefinition with { FinalRanking = null, Phases = [brokenPhase, brokenPhase with { Ordinal = 2 }] };

        var result = await dispatcher.SendAsync(new PublishClassDefinition(invalidDefinition), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("class-definition.invalid");
        result.Defects.Should().Contain(d => d.Code == "class-definition.check-12.missing-final-ranking");
        result.Defects.Should().Contain(d => d.Code == "class-definition.check-14.normalised-terms-without-normalisation");
        result.Defects.Count.Should().BeGreaterThanOrEqualTo(2);

        eventStore.Streams.Should().BeEmpty();
    }

    [Fact]
    public async Task A_definition_over_an_ingestion_limit_fails_before_Validate_runs()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));
        var tooManyParameters = ValidDefinition with
        {
            Parameters = Enumerable.Range(0, ClassDefinitionIngestion.MaxParametersPerDefinition + 1)
                .Select(i => new Parameter { Name = $"p{i}" })
                .ToImmutableArray(),
        };

        var result = await dispatcher.SendAsync(new PublishClassDefinition(tooManyParameters), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("class-definition.ingestion.limitsExceeded");
        eventStore.Streams.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClassDefinition_after_publish_returns_the_folded_definition()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));
        var published = await dispatcher.SendAsync(new PublishClassDefinition(ValidDefinition), TestContext.Current.CancellationToken);

        var fetched = await dispatcher.QueryAsync(new GetClassDefinition(published.Value), TestContext.Current.CancellationToken);

        fetched.IsSuccess.Should().BeTrue();
        fetched.Value.Should().Be(ValidDefinition);
    }

    [Fact]
    public async Task GetClassDefinition_for_an_unknown_hash_fails_with_not_found()
    {
        var eventStore = new FakeEventStore();
        var dispatcher = BuildDispatcher(eventStore, new FakeClock(DateTimeOffset.UtcNow));

        var result = await dispatcher.QueryAsync(
            new GetClassDefinition(new string('0', 64)), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("classDefinition.notFound");
    }
}
