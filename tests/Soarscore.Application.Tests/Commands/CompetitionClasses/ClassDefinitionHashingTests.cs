using AwesomeAssertions;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests.Commands.CompetitionClasses;

public class ClassDefinitionHashingTests
{
    [Fact]
    public void ComputeContentHash_is_deterministic_and_content_sensitive()
    {
        var hashA = ClassDefinitionHashing.ComputeContentHash(Corpus.All[0].Definition);
        var hashAAgain = ClassDefinitionHashing.ComputeContentHash(Corpus.All[0].Definition);
        var hashB = ClassDefinitionHashing.ComputeContentHash(Corpus.All[1].Definition);

        hashAAgain.Should().Be(hashA);
        hashB.Should().NotBe(hashA);
        hashA.Length.Should().Be(64); // full SHA-256 hex digest, not the seed tool's 16-char console truncation
    }
}
