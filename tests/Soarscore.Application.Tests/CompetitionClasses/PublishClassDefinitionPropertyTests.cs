// docs/plans/class-definition-adoption-steel-thread-plan.md WI-4's idempotency
// claim ("republishing identical content targets the same stream and is a
// safe no-op"), checked as a property across a random republish count and a
// random corpus definition, complementing PublishClassDefinitionTests.cs's
// fixed two-calls example.

using CsCheck;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests.CompetitionClasses;

public class PublishClassDefinitionPropertyTests
{
    private static readonly Gen<ClassDefinition> AnyCorpusDefinition =
        Gen.OneOfConst(Corpus.All.Select(c => c.Definition).ToArray());

    [Fact]
    public void Republishing_the_same_definition_any_number_of_times_always_returns_the_same_hash_and_appends_once()
    {
        (from definition in AnyCorpusDefinition from republishCount in Gen.Int[1, 5] select (definition, republishCount))
        .Sample(t =>
        {
            var eventStore = new FakeEventStore();
            var handler = new PublishClassDefinitionHandler(eventStore, new FakeClock(DateTimeOffset.UtcNow));
            var expectedHash = ClassDefinitionHashing.ComputeContentHash(t.definition);

            var results = Enumerable.Range(0, t.republishCount)
                .Select(_ => handler.HandleAsync(new PublishClassDefinition(t.definition), CancellationToken.None).GetAwaiter().GetResult())
                .ToList();

            var streamId = ClassDefinitionStreamId.From(expectedHash);

            return results.All(r => r.IsSuccess && r.Value == expectedHash)
                && eventStore.Streams[streamId].Count == 1;
        });
    }
}
