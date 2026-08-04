using Soarscore.Domain.CompetitionClasses;
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

        var projection = PublishedClassDefinition.Apply(null, @event);

        Assert.NotNull(projection);
        Assert.Equal(hash, projection!.ContentHash);
        Assert.Same(SampleDefinition, projection.Definition);
        Assert.Equal(publishedAt, projection.PublishedAt);
        Assert.Null(projection.RetiredAt);
    }

    [Fact]
    public void Retired_sets_RetiredAt_and_leaves_everything_else_untouched()
    {
        var hash = SampleHash;
        var published = PublishedClassDefinition.Apply(null, new ClassDefinitionPublished(hash, SampleDefinition, DateTimeOffset.UtcNow));
        var retiredAt = DateTimeOffset.UtcNow.AddDays(1);

        var retired = PublishedClassDefinition.Apply(published, new ClassDefinitionRetired("superseded", retiredAt));

        Assert.NotNull(retired);
        Assert.Equal(retiredAt, retired!.RetiredAt);
        Assert.Equal(published!.ContentHash, retired.ContentHash);
        Assert.Equal(published.PublishedAt, retired.PublishedAt);
    }

    [Fact]
    public void Retired_against_no_current_projection_folds_to_null()
    {
        var result = PublishedClassDefinition.Apply(null, new ClassDefinitionRetired("n/a", DateTimeOffset.UtcNow));

        Assert.Null(result);
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

        var final = stream.Aggregate((PublishedClassDefinition?)null, PublishedClassDefinition.Apply);

        Assert.NotNull(final);
        Assert.Equal(hash, final!.ContentHash);
        Assert.Equal(retiredAt, final.RetiredAt);
    }
}
