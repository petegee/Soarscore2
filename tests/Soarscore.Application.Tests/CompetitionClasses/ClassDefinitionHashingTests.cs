using Soarscore.Application.CompetitionClasses;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests.CompetitionClasses;

public class ClassDefinitionHashingTests
{
    [Fact]
    public void ComputeContentHash_is_deterministic_and_content_sensitive()
    {
        var hashA = ClassDefinitionHashing.ComputeContentHash(Corpus.All[0].Definition);
        var hashAAgain = ClassDefinitionHashing.ComputeContentHash(Corpus.All[0].Definition);
        var hashB = ClassDefinitionHashing.ComputeContentHash(Corpus.All[1].Definition);

        Assert.Equal(hashA, hashAAgain);
        Assert.NotEqual(hashA, hashB);
        Assert.Equal(64, hashA.Length); // full SHA-256 hex digest, not the seed tool's 16-char console truncation
    }
}
