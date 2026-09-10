// kanban/in-progress/seed-class-corpus-at-startup.md WI-5 — the acceptance
// proof of startup seeding. The host under test is the run's one
// WebApplicationFactory, which AcceptanceFixture pointed at the repo's frozen
// corpus before it built (the same thing the Docker image does with /app/seed);
// by the time any scenario runs, the seeder has already published through the
// ordinary PublishClassDefinition door. So the Given here re-states the
// startup wiring, the When reads the catalogue over real HTTP, and the Then
// checks identity the only honest way: recomputing each corpus file's content
// hash and finding it in the projected class_library.

using System.Text.Json;
using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class SeedingTheClassCatalogueSteps
{
    private string _corpusDirectory = null!;
    private IReadOnlyList<ClassDefinitionSummary>? _catalogue;

    [Given(@"^the class corpus the application ships$")]
    public void GivenTheClassCorpusTheApplicationShips()
    {
        _corpusDirectory = FindSeedJsonDirectory();
        Directory.Exists(_corpusDirectory).Should().BeTrue(
            $"{_corpusDirectory} is what AcceptanceFixture pointed the seeder at; without it the host started with an empty catalogue and this scenario is measuring nothing");
    }

    [When(@"^the class catalogue is queried$")]
    public async Task WhenTheClassCatalogueIsQueried()
    {
        _catalogue = await ApiClient.GetAsync<IReadOnlyList<ClassDefinitionSummary>>(
            AcceptanceFixture.Client, "/class-definitions");
    }

    [Then(@"^every seed class is listed under its content hash$")]
    public async Task ThenEverySeedClassIsListedUnderItsContentHash()
    {
        _catalogue.Should().NotBeNull("the When step must have queried the catalogue first");

        var files = Directory.GetFiles(_corpusDirectory, "*.json");
        files.Should().HaveCount(16, "the corpus count is pinned by the seed tool; the catalogue must carry all of it");

        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
            var definition = JsonSerializer.Deserialize<ClassDefinition>(json, ApiClient.Options)!;
            var hash = ClassDefinitionHashing.ComputeContentHash(definition);

            _catalogue.Should().Contain(summary => summary.ContentHash == hash,
                $"{Path.GetFileName(file)} must be in the catalogue after startup seeding");
        }
    }

    private static string FindSeedJsonDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not find the repository root from the test's base directory.")
            : Path.Combine(directory.FullName, "tools", "Soarscore.SeedData", "json");
    }
}
