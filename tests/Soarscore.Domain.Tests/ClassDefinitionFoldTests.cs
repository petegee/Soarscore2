using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

public class ClassDefinitionFoldTests
{
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    // Fold logic treats ContentHash as an opaque string — no need for a real
    // SHA-256 digest here (that's ClassDefinitionHashing, an Application
    // concern this project doesn't reference).
    private const string SampleHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcd";

    [Fact]
    public void Published_creates_the_projection_from_an_empty_stream()
    {
        var publishedAt = DateTimeOffset.UtcNow;
        var hash = SampleHash;
        var @event = new ClassDefinitionPublished(hash, SampleDefinition, publishedAt);

        var projection = PublishedClassDefinition.PublishedClassDefinition.Apply(null, @event);

        projection.Should().NotBeNull();
        projection!.ContentHash.Should().Be(hash);
        projection.Definition.Should().BeSameAs(SampleDefinition);
        projection.PublishedAt.Should().Be(publishedAt);
        projection.RetiredAt.Should().BeNull();
    }

    [Fact]
    public void Retired_sets_RetiredAt_and_leaves_everything_else_untouched()
    {
        var hash = SampleHash;
        var published = PublishedClassDefinition.PublishedClassDefinition.Apply(null, new ClassDefinitionPublished(hash, SampleDefinition, DateTimeOffset.UtcNow));
        var retiredAt = DateTimeOffset.UtcNow.AddDays(1);

        var retired = PublishedClassDefinition.PublishedClassDefinition.Apply(published, new ClassDefinitionRetired("superseded", retiredAt));

        retired.Should().NotBeNull();
        retired!.RetiredAt.Should().Be(retiredAt);
        retired.ContentHash.Should().Be(published!.ContentHash);
        retired.PublishedAt.Should().Be(published.PublishedAt);
    }

    [Fact]
    public void Retired_against_no_current_projection_folds_to_null()
    {
        var result = PublishedClassDefinition.PublishedClassDefinition.Apply(null, new ClassDefinitionRetired("n/a", DateTimeOffset.UtcNow));

        result.Should().BeNull();
    }

    [Fact]
    public void A_full_event_stream_folds_in_order_to_the_expected_final_state()
    {
        var hash = SampleHash;
        var publishedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var retiredAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        ClassDefinitionEvent[] stream =
        [
            new ClassDefinitionPublished(hash, SampleDefinition, publishedAt),
            new ClassDefinitionRetired("library cleanup", retiredAt),
        ];

        var final = stream.Aggregate((PublishedClassDefinition.PublishedClassDefinition?)null, PublishedClassDefinition.PublishedClassDefinition.Apply);

        final.Should().NotBeNull();
        final!.ContentHash.Should().Be(hash);
        final.RetiredAt.Should().Be(retiredAt);
    }
}
