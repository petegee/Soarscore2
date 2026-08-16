// docs/plans/class-definition-adoption-steel-thread-plan.md WI-3. Pure fold
// tests, no store needed — mirrors PeopleProjectionTests.cs's shape.

using AwesomeAssertions;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Shared.CompetitionClasses;
namespace Soarscore.Application.Tests.Queries.CompetitionClasses;

public class ClassDefinitionProjectionTests
{
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;
    private const string SampleHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcd";

    [Fact]
    public void Published_creates_the_summary_from_an_empty_projection()
    {
        var publishedAt = DateTimeOffset.UtcNow;
        var @event = new ClassDefinitionPublished(SampleHash, SampleDefinition, publishedAt);

        var summary = ClassDefinitionProjection.Apply(null, @event);

        summary.Should().NotBeNull();
        summary!.Id.Should().Be(ClassDefinitionStreamId.From(SampleHash));
        summary.ContentHash.Should().Be(SampleHash);
        summary.Name.Should().Be(SampleDefinition.Name);
        summary.FaiDesignation.Should().Be(SampleDefinition.FaiDesignation);
        summary.Version.Should().Be(SampleDefinition.Version);
        summary.PublishedAt.Should().Be(publishedAt);
        summary.RetiredAt.Should().BeNull();
    }

    [Fact]
    public void Retired_sets_RetiredAt_and_leaves_everything_else_untouched()
    {
        var published = ClassDefinitionProjection.Apply(null, new ClassDefinitionPublished(SampleHash, SampleDefinition, DateTimeOffset.UtcNow));
        var retiredAt = DateTimeOffset.UtcNow.AddDays(1);

        var retired = ClassDefinitionProjection.Apply(published, new ClassDefinitionRetired("superseded", retiredAt));

        retired.Should().NotBeNull();
        retired!.RetiredAt.Should().Be(retiredAt);
        retired.ContentHash.Should().Be(published!.ContentHash);
        retired.Name.Should().Be(published.Name);
    }

    [Fact]
    public void Retired_against_no_current_summary_throws()
    {
        var act = () => ClassDefinitionProjection.Apply(null, new ClassDefinitionRetired("n/a", DateTimeOffset.UtcNow));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_full_event_stream_folds_in_order_to_the_expected_final_state()
    {
        var publishedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var retiredAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        ClassDefinitionEvent[] stream =
        [
            new ClassDefinitionPublished(SampleHash, SampleDefinition, publishedAt),
            new ClassDefinitionRetired("library cleanup", retiredAt),
        ];

        var final = stream.Aggregate((ClassDefinitionSummary?)null, ClassDefinitionProjection.Apply);

        final.Should().NotBeNull();
        final!.ContentHash.Should().Be(SampleHash);
        final.RetiredAt.Should().Be(retiredAt);
    }
}
