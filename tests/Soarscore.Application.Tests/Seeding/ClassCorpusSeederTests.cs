// kanban/in-progress/seed-class-corpus-at-startup.md WI-4. ClassCorpusSeeder's
// own tests: it is the deployment-time replay of SeedCorpusIngestionTests's
// proof — the corpus enters through the same door (IDispatcher → real
// PublishClassDefinitionHandler → fake store), and the seeder adds nothing
// above the command except directory mechanics and fail-fast on defect.

using System.Text.Json;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Seeding;
using Soarscore.Application.Shared.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

using Soarscore.Application.Tests.Shared.CompetitionClasses;

namespace Soarscore.Application.Tests.Seeding;

public class ClassCorpusSeederTests
{
    // ------------------------------------------------------------ the corpus

    [Fact]
    public async Task Publishes_every_corpus_file_through_the_dispatcher()
    {
        var (dispatcher, eventStore) = BuildDispatcher();
        var corpus = FindSeedJsonDirectory();

        var report = await ClassCorpusSeeder.SeedAsync(dispatcher, corpus, TestContext.Current.CancellationToken);

        var files = Directory.GetFiles(corpus, "*.json");
        report.Count.Should().Be(files.Length,
            "every top-level corpus file is a class definition that must be published");
        report.Count.Should().BePositive("the corpus is the deployment's class catalogue");

        foreach (var entry in report.Published)
        {
            eventStore.Streams
                .Should().ContainKey(ClassDefinitionStreamId.From(entry.ContentHash),
                    $"{entry.FileName} published through the same door as a user class must land on its content-hash stream");
        }
    }

    [Fact]
    public async Task Reports_the_content_hash_of_each_published_definition()
    {
        var (dispatcher, _) = BuildDispatcher();
        var corpus = FindSeedJsonDirectory();

        var report = await ClassCorpusSeeder.SeedAsync(dispatcher, corpus, TestContext.Current.CancellationToken);

        foreach (var (file, entry) in Directory.GetFiles(corpus, "*.json").Order(StringComparer.Ordinal)
                     .Zip(report.Published))
        {
            var definition = JsonSerializer.Deserialize<ClassDefinition>(
                await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken), ClassDefinitionIngestion.Options)!;

            entry.FileName.Should().Be(Path.GetFileName(file));
            entry.ContentHash.Should().Be(ClassDefinitionHashing.ComputeContentHash(definition),
                "the report carries the same identity the publish command derived");
        }
    }

    [Fact]
    public async Task Re_seeding_an_already_seeded_store_is_a_safe_no_op()
    {
        var (dispatcher, eventStore) = BuildDispatcher();
        var corpus = FindSeedJsonDirectory();

        var first = await ClassCorpusSeeder.SeedAsync(dispatcher, corpus, TestContext.Current.CancellationToken);
        var streamsAfterFirst = eventStore.Streams.Count;

        var second = await ClassCorpusSeeder.SeedAsync(dispatcher, corpus, TestContext.Current.CancellationToken);

        second.Count.Should().Be(first.Count,
            "the seeder sends every file again and relies on the publish command's own idempotency");
        eventStore.Streams.Should().HaveCount(streamsAfterFirst,
            "republishing identical content appends nothing — no new streams after the second pass");
  }

    // -------------------------------------------------------- failure paths

    [Fact]
    public async Task A_definition_that_fails_to_publish_stops_the_seeder_and_names_the_file()
    {
        var (dispatcher, _) = BuildDispatcher();
        var directory = TemporaryCorpus(("broken-class.json", InvalidDefinition));

        var seed = () => ClassCorpusSeeder.SeedAsync(dispatcher, directory, TestContext.Current.CancellationToken);

        (await seed.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*broken-class.json*class-definition.invalid*");
    }

    [Fact]
    public async Task Malformed_json_names_the_file()
    {
        var (dispatcher, _) = BuildDispatcher();
        var directory = TemporaryCorpus(("garbage.json", "{ not json"));

        var seed = () => ClassCorpusSeeder.SeedAsync(dispatcher, directory, TestContext.Current.CancellationToken);

        (await seed.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*garbage.json*");
    }

    [Fact]
    public async Task A_missing_directory_is_a_wiring_error()
    {
        var (dispatcher, _) = BuildDispatcher();
        var missing = Path.Combine(Path.GetTempPath(), "soarscore-no-such-corpus-" + Guid.NewGuid().ToString("N"));

        var seed = () => ClassCorpusSeeder.SeedAsync(dispatcher, missing);

        (await seed.Should().ThrowAsync<DirectoryNotFoundException>())
            .WithMessage("*" + missing + "*");
    }

    [Fact]
    public async Task Only_top_level_json_files_are_enumerated_so_tapes_stay_out()
    {
        var (dispatcher, _) = BuildDispatcher();
        var directory = TemporaryCorpus(("a-class.json", CorpusFile("20-f3b")));
        Directory.CreateDirectory(Path.Combine(directory, "tapes"));
        await File.WriteAllTextAsync(
            Path.Combine(directory, "tapes", "junk.json"), "{ not json", TestContext.Current.CancellationToken);

        var report = await ClassCorpusSeeder.SeedAsync(dispatcher, directory, TestContext.Current.CancellationToken);

        report.Count.Should().Be(1,
            "the tapes subdirectory is test-fixture territory, never deployment data; if it were enumerated the garbage file would have thrown");
    }

    // -------------------------------------------------------------- plumbing

    private static (IDispatcher Dispatcher, FakeEventStore EventStore) BuildDispatcher()
    {
        var eventStore = new FakeEventStore();
        var services = new Dictionary<Type, object>
        {
            [typeof(ICommandHandler<PublishClassDefinition, string>)] =
                new PublishClassDefinitionHandler(eventStore, new FakeClock(DateTimeOffset.UtcNow)),
        };
        return (new Dispatcher(new FakeServiceProvider(services)), eventStore);
    }

    /// <summary>A definition that deserialises cleanly but fails adoption check 1:
    /// its score term names a metric the task never declared.</summary>
    private static string InvalidDefinition
    {
        get
        {
            var baseline = ClassDefinitionFixtures.Minimal();
            var task = baseline.Phases[0].Tasks[0] with
            {
                Score = [new RateTerm { MetricRef = "no-such-metric", Rate = 1 }],
            };
            var invalid = baseline with { Phases = [baseline.Phases[0] with { Tasks = [task] }] };
            return JsonSerializer.Serialize(invalid, ClassDefinitionIngestion.Options);
        }
    }

    /// <summary>One named corpus file's content, by class file name stem (e.g. "f3b").</summary>
    private static string CorpusFile(string stem)
    {
        var path = Path.Combine(FindSeedJsonDirectory(), stem + ".json");
        return File.Exists(path) ? File.ReadAllText(path) : throw new InvalidOperationException($"No corpus file '{stem}.json'.");
    }

    private static string TemporaryCorpus(params (string FileName, string Content)[] files)
    {
        var directory = Path.Combine(Path.GetTempPath(), "soarscore-corpus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (var (fileName, content) in files)
        {
            File.WriteAllText(Path.Combine(directory, fileName), content);
        }

        return directory;
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
