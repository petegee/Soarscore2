// docs/plans/class-definition-adoption-steel-thread-plan.md WI-8. The proof
// LADR-0002 §1 demands: "seed classes must enter through the same door as
// user classes." Drives every checked-in seed JSON file — not the C# Corpus
// objects — through the real deserialise -> PublishClassDefinitionHandler
// pipeline (Validate() included) with a fake store, exactly the path a user's
// POST /publish-class-definition body would take.

using System.Text.Json;
using AwesomeAssertions;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Application.Tests.CompetitionClasses;

public class SeedCorpusIngestionTests
{
    [Fact]
    public async Task Every_seed_json_file_publishes_clean_through_the_real_ingestion_path()
    {
        var jsonDirectory = FindSeedJsonDirectory();
        var files = Directory.GetFiles(jsonDirectory, "*.json");
        files.Should().NotBeEmpty("the seed corpus JSON must have already been emitted by the seed tool");

        var eventStore = new FakeEventStore();
        var handler = new PublishClassDefinitionHandler(eventStore, new FakeClock(DateTimeOffset.UtcNow));

        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
            var definition = JsonSerializer.Deserialize<ClassDefinition>(json, ClassDefinitionIngestion.Options);
            definition.Should().NotBeNull($"{Path.GetFileName(file)} must deserialise through the ingestion options");

            var result = await handler.HandleAsync(new PublishClassDefinition(definition!), TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue($"{Path.GetFileName(file)} is part of the model's own test corpus and must publish clean: " +
                string.Join("; ", result.Defects.Select(d => $"{d.Code} at {d.Path}")));
        }
    }

    private static string FindSeedJsonDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not find the repository root from the test's base directory.");
        }

        return Path.Combine(directory.FullName, "tools", "Soarscore.SeedData", "json");
    }
}
