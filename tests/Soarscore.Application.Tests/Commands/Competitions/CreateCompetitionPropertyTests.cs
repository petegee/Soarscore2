// kanban/completed/create-competition-steel-thread-plan.md WI-3. The fake-store
// analogue of WI-6's real-Postgres round-trip proof: for any corpus
// definition published into the fake store, and any valid name/location/date
// input, CreateCompetitionHandler always succeeds and the AdoptedRules it
// copies into the new stream always hashes back to the exact class-definition
// hash used to look it up.

using CsCheck;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Shared.CompetitionClasses;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class CreateCompetitionPropertyTests
{
    private static readonly Gen<ClassDefinition> AnyCorpusDefinition =
        Gen.OneOfConst(Corpus.All.Select(c => c.Definition).ToArray());

    private static readonly Gen<(string Name, string Location, int OffsetDays, int DurationDays)> AnyValidCreation =
        from name in Gen.OneOfConst("Nationals", "Club Champs", "Regionals", "Test Fly")
        from location in Gen.OneOfConst("Taupo", "Auckland", "Wellington", "Christchurch")
        from offsetDays in Gen.Int[0, 365]
        from durationDays in Gen.Int[0, 5]
        select (name, location, offsetDays, durationDays);

    [Fact]
    public void Creating_against_a_published_corpus_definition_always_succeeds_and_round_trips_the_content_hash()
    {
        (from definition in AnyCorpusDefinition from creation in AnyValidCreation select (definition, creation))
        .Sample(t =>
        {
            var eventStore = new FakeEventStore();
            var expectedHash = ClassDefinitionHashing.ComputeContentHash(t.definition);
            var classStreamId = ClassDefinitionStreamId.From(expectedHash);
            var published = new ClassDefinitionPublished(expectedHash, t.definition, DateTimeOffset.UtcNow);
            eventStore.AppendAsync(classStreamId, ExpectedVersion.NoStream, [published]).GetAwaiter().GetResult();

            var handler = new CreateCompetitionHandler(eventStore, new FakeClock(DateTimeOffset.UtcNow));

            var start = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(t.creation.OffsetDays);
            var end = start.AddDays(t.creation.DurationDays);

            var result = handler.HandleAsync(
                new CreateCompetition(t.creation.Name, t.creation.Location, start, end, expectedHash),
                CancellationToken.None).GetAwaiter().GetResult();

            if (!result.IsSuccess)
            {
                return false;
            }

            var stream = eventStore.Streams[result.Value.Value];
            var created = stream.Single() as CompetitionCreated;
            if (created is null)
            {
                return false;
            }

            var roundTrippedHash = ClassDefinitionHashing.ComputeContentHash(created.AdoptedRules.Definition);
            return roundTrippedHash == expectedHash && created.AdoptedRules.SourceClassId == expectedHash;
        });
    }
}
