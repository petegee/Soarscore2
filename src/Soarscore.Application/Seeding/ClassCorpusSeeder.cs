// Deployment seeding — kanban/in-progress/seed-class-corpus-at-startup.md WI-1.
// A freshly created store has no published class definitions, so a new
// deployment cannot adopt a class into a competition until the catalogue
// exists. This is the mechanics half of startup seeding: read the frozen
// canonical JSON corpus (tools/Soarscore.SeedData/json — the sixteen FAI and
// NZ class definitions the seed tool emits and byte-verifies), and publish
// each one through IDispatcher — LADR-0002 §1's "seed classes must enter
// through the same door as user classes" (SeedCorpusIngestionTests's proof,
// now applied at deployment time rather than only in tests).
//
// What this file deliberately does NOT do:
//   - No logging: Application carries no Microsoft.Extensions.Logging
//     dependency; SeedAsync returns a ClassCorpusSeedReport and the host
//     (Soarscore.Api's ClassCorpusSeederHost) logs it.
//   - No configuration, no hosted-service plumbing: policy lives with the
//     host; this file only walks a directory and dispatches.
//   - No idempotency guard of its own: PublishClassDefinitionHandler is
//     idempotent by design (the stream id IS the content hash and
//     "eventStore.streamAlreadyExists" returns success), so re-seeding on
//     every boot is a safe no-op at the command — re-running this seeder
//     against a seeded store re-sends every file and appends nothing.
//   - No tape ingestion: json/tapes/ are test fixtures for the property
//     suites (CatalogueDrawPropertyTests et al); no ingest command exists
//     for TapeDefinition and none belongs in a deployment store. Enumerating
//     top-level "*.json" only is what keeps them out.
//
// Failure semantics: the corpus is frozen and test-verified byte-exact by the
// seed tool, so a file that fails to deserialise or publish is a programming
// error, not a deployment condition — SeedAsync throws (naming the file)
// rather than degrading the catalogue. The host's StartAsync propagates the
// throw, failing fast before the API accepts a request. This does not touch
// NFR-4, which forbids imposed ordering on score capture, not on catalog
// bootstrap.

using System.Text.Json;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Seeding;

/// <summary>One corpus file that was sent through the publish command.</summary>
public sealed record ClassCorpusSeedEntry(string FileName, string ContentHash);

/// <summary>What one seeding pass sent. Empty means nothing was seeded.</summary>
public sealed record ClassCorpusSeedReport(IReadOnlyList<ClassCorpusSeedEntry> Published)
{
    public static readonly ClassCorpusSeedReport Empty = new([]);

    /// <summary>How many definitions were dispatched — 16 for the shipped corpus.</summary>
    public int Count => Published.Count;
}

public static class ClassCorpusSeeder
{
    /// <summary>
    /// Publishes every top-level <c>*.json</c> file in <paramref name="directory"/>
    /// as a <see cref="PublishClassDefinition"/> command, in file-name order so a
    /// seeding log is reproducible. Subdirectories (the tapes catalogue) are not
    /// enumerated; see the file header.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">
    /// <paramref name="directory"/> does not exist — the host decides skip-vs-seed
    /// by checking first, so reaching here without a directory is a misconfiguration.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A file is malformed JSON, deserialises to null, or its publish command
    /// returns a failure. Each names the offending file.
    /// </exception>
    public static async Task<ClassCorpusSeedReport> SeedAsync(
        IDispatcher dispatcher, string directory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Class corpus directory '{directory}' does not exist — the host checks before calling SeedAsync, so this is a wiring error.");
        }

        var published = new List<ClassCorpusSeedEntry>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(file);
            var json = await File.ReadAllTextAsync(file, cancellationToken);

            ClassDefinition? definition;
            try
            {
                definition = JsonSerializer.Deserialize<ClassDefinition>(json, ClassDefinitionIngestion.Options);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    $"Seed corpus file '{fileName}' is not valid ClassDefinition JSON: {exception.Message}", exception);
            }

            if (definition is null)
            {
                throw new InvalidOperationException($"Seed corpus file '{fileName}' deserialised to null.");
            }

            var result = await dispatcher.SendAsync(new PublishClassDefinition(definition), cancellationToken);
            if (result.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed corpus file '{fileName}' failed to publish ({result.Code}): {result.Message}. Defects: " +
                    string.Join("; ", result.Defects.Select(d => $"{d.Code} at {d.Path}")));
            }

            published.Add(new ClassCorpusSeedEntry(fileName, result.Value));
        }

        return new ClassCorpusSeedReport(published);
    }
}
