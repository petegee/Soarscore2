// kanban/in-progress/seed-definition-parallel-run.md WI-1 item 1 — loads the
// seed competition classes out of tools/Soarscore.SeedData/json/ for the
// replay harness's parallel-run mode. The seed JSON is the single source of
// truth — nothing is copied into the test project — and the deserialisation
// goes through the SAME ClassDefinitionIngestion.Options a human POST to
// /publish-class-definition binds through (FixtureLoader's precedent for the
// fixture-authored definitions), so what reaches the Api is byte-faithful to
// the canonical JSON the seed tool emits.
//
// The directory is resolved by the same walk-up arithmetic
// FixtureLoader.ResolveCorpusDirectory uses for tests/GliderscoreFixtures —
// the repository root's shape, not a hardcoded number of ups — with the
// different relative tail this corpus lives at.

using System.Text.Json;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

public static class SeedDefinitionLoader
{
    /// <summary>
    /// One seed class by its JSON file name (e.g. "80-nz-m-ales200.json").
    /// </summary>
    public static ClassDefinition Load(string fileName)
    {
        var path = Path.Combine(ResolveSeedDirectory(), fileName);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Seed definition '{fileName}' has no file at {path}.");
        }

        return JsonSerializer.Deserialize<ClassDefinition>(File.ReadAllText(path), ClassDefinitionIngestion.Options)
            ?? throw new InvalidOperationException($"Seed definition '{path}' deserialised to null.");
    }

    /// <summary>
    /// Walks up from AppContext.BaseDirectory until a tools/Soarscore.SeedData/json
    /// directory hangs off the current level — the same arithmetic
    /// FixtureLoader.ResolveCorpusDirectory applies to tests/GliderscoreFixtures,
    /// different relative tail (the seed corpus lives under tools/, not tests/).
    /// </summary>
    public static string ResolveSeedDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "tools", "Soarscore.SeedData", "json");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent!;
        }

        throw new InvalidOperationException(
            $"No tools/Soarscore.SeedData/json directory found above {AppContext.BaseDirectory}.");
    }
}
