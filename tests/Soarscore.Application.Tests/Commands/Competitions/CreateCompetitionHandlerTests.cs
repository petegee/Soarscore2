// kanban/completed/create-competition-steel-thread-plan.md WI-3. Covers
// CreateCompetitionHandler directly against a FakeEventStore, no dispatcher
// needed — same style as PublishClassDefinitionPropertyTests.cs.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Shared.CompetitionClasses;
using Soarscore.Application.Tests.Shared.CompetitionClasses;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class CreateCompetitionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly StartDate = new(2026, 9, 12);
    private static readonly DateOnly EndDate = new(2026, 9, 13);

    // Returns the exact same ClassDefinition instance that was published, not
    // a fresh call to ClassDefinitionFixtures.Minimal(): ClassDefinition's
    // ImmutableArray-typed properties compare by underlying-array reference,
    // not by element content, so two separately constructed but
    // content-identical definitions are not AwesomeAssertions .Be()-equal —
    // only the same instance (or an explicit content-hash comparison) is.
    private static (FakeEventStore Store, string Hash, ClassDefinition Definition) SeedPublishedClassDefinition()
    {
        var definition = ClassDefinitionFixtures.Minimal();
        var hash = ClassDefinitionHashing.ComputeContentHash(definition);
        var streamId = ClassDefinitionStreamId.From(hash);

        var store = new FakeEventStore();
        var published = new ClassDefinitionPublished(hash, definition, Now);
        store.AppendAsync(streamId, ExpectedVersion.NoStream, [published]).GetAwaiter().GetResult();

        return (store, hash, definition);
    }

    [Fact]
    public async Task Creating_against_a_known_active_class_definition_succeeds_with_a_full_adopted_rules_copy()
    {
        var (store, hash, definition) = SeedPublishedClassDefinition();
        var handler = new CreateCompetitionHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new CreateCompetition("Nationals", "Taupo", StartDate, EndDate, hash), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();

        var stream = store.Streams[result.Value.Value];
        stream.Should().ContainSingle();
        var created = stream[0].Should().BeOfType<CompetitionCreated>().Subject;
        created.AdoptedRules.Definition.Should().Be(definition);
        created.AdoptedRules.SourceClassId.Should().Be(hash);
    }

    [Fact]
    public async Task Creating_against_an_unknown_hash_fails_with_classDefinitionNotFound()
    {
        var store = new FakeEventStore();
        var handler = new CreateCompetitionHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new CreateCompetition("Nationals", "Taupo", StartDate, EndDate, "00" + new string('0', 62)),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("createCompetition.classDefinitionNotFound");
    }

    [Fact]
    public async Task Creating_against_a_retired_class_definition_fails_with_classDefinitionRetired()
    {
        var (store, hash, _) = SeedPublishedClassDefinition();
        var streamId = ClassDefinitionStreamId.From(hash);
        await store.AppendAsync(
            streamId, ExpectedVersion.Exact(1), [new ClassDefinitionRetired("superseded", Now)], TestContext.Current.CancellationToken);

        var handler = new CreateCompetitionHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new CreateCompetition("Nationals", "Taupo", StartDate, EndDate, hash), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("createCompetition.classDefinitionRetired");
    }

    [Fact]
    public async Task Blank_name_fails_via_CompetitionDecide()
    {
        var (store, hash, _) = SeedPublishedClassDefinition();
        var handler = new CreateCompetitionHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new CreateCompetition(" ", "Taupo", StartDate, EndDate, hash), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.name.blank");
    }

    [Fact]
    public async Task Blank_location_fails_via_CompetitionDecide()
    {
        var (store, hash, _) = SeedPublishedClassDefinition();
        var handler = new CreateCompetitionHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new CreateCompetition("Nationals", " ", StartDate, EndDate, hash), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.location.blank");
    }

    [Fact]
    public async Task Start_date_after_end_date_fails_via_CompetitionDecide()
    {
        var (store, hash, _) = SeedPublishedClassDefinition();
        var handler = new CreateCompetitionHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new CreateCompetition("Nationals", "Taupo", EndDate, StartDate, hash), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.dates.invalid");
    }
}
