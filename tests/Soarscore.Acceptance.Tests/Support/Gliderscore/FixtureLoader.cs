// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-1 — loads
// fixtures out of tests/GliderscoreFixtures/ for the replay harness (D1: the
// corpus stays where it is; the harness resolves the directory from the test
// assembly's location by walking up, so no build-output depth is hardcoded).
//
// GS 04 Step 3 — corpus-registry.json is the authoritative manifest; index.md
// stays the human-readable manifest and ActiveSlugs cross-checks the two (a
// drift on either side fails loudly naming the slug).

using System.Text.Json;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

public static class FixtureLoader
{
    // Case-insensitive on purpose: GS exports mix PascalCase columns ("RoundNo")
    // with camelCase family fields ("durTargetTime") in one file.
    private static readonly JsonSerializerOptions FixtureJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Every slug the corpus runs: GS 04 Step 3 — the registry is
    /// authoritative (its "active" entries, in registry order) and index.md is
    /// cross-checked against it: a slug the index lists without a registry
    /// entry, a registry entry without an index bullet, or a status mismatch
    /// on either side throws naming the slug. When corpus-registry.json is
    /// absent (a checkout predating the registry) the index governs alone, so
    /// strict runs stay green there — the same authoritative-with-fallback
    /// discipline as <see cref="CorpusReplayRegistry"/>.
    /// </summary>
    public static IReadOnlyList<string> ActiveSlugs()
    {
        var corpus = ResolveCorpusDirectory();
        var index = IndexSlugs(corpus);

        if (!CorpusReplayRegistry.RegistryGoverns)
        {
            return index.Where(s => !s.Skipped).Select(s => s.Slug).ToList();
        }

        var manifest = CorpusReplayRegistry.ManifestEntries();
        var manifestBySlug = manifest.ToDictionary(e => e.Slug, StringComparer.Ordinal);
        var indexBySlug = index.ToDictionary(s => s.Slug, StringComparer.Ordinal);

        foreach (var slug in indexBySlug.Keys.Union(manifestBySlug.Keys).OrderBy(s => s, StringComparer.Ordinal))
        {
            var inIndex = indexBySlug.TryGetValue(slug, out var indexRow);
            var inRegistry = manifestBySlug.TryGetValue(slug, out var entry);

            if (!inRegistry)
            {
                throw new InvalidOperationException(
                    $"Fixture '{slug}' is listed in index.md but has no corpus-registry.json entry — "
                    + "add the entry (status, modes, provenance refs) or drop the bullet.");
            }

            if (!inIndex)
            {
                throw new InvalidOperationException(
                    $"Fixture '{slug}' has a corpus-registry.json entry but no index.md bullet — "
                    + "the index stays the human-readable manifest; list it there.");
            }

            var indexActive = !indexRow!.Skipped;
            var registryActive = entry!.Status == "active";

            if (indexActive != registryActive)
            {
                throw new InvalidOperationException(
                    $"Fixture '{slug}' status disagrees: index.md says "
                    + (indexActive ? "active" : "skipped") + " but corpus-registry.json status is "
                    + $"'{entry.Status}' — the two must agree.");
            }
        }

        return manifest.Where(e => e.Status == "active").Select(e => e.Slug).ToList();
    }

    private sealed record IndexRow(string Slug, bool Skipped);

    /// <summary>
    /// The index.md tokenisation contract: under the "## Competitions"
    /// heading each competition is one `- &lt;slug&gt; — &lt;status&gt; — …`
    /// bullet, and a slug counts as SKIP-LISTED when its line contains
    /// "skipped" anywhere. Bullets wrap over several lines; continuation
    /// lines never start with "- " and are ignored. Dashes elsewhere in the
    /// file (skip rules, diversity notes) are prose, never manifest entries —
    /// scoping to the Competitions section keeps them out of the corpus.
    /// </summary>
    private static IReadOnlyList<IndexRow> IndexSlugs(string corpus)
    {
        var lines = File.ReadAllLines(Path.Combine(corpus, "index.md"));
        var competitions = lines
            .SkipWhile(line => line.Trim() != "## Competitions")
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("## "))
            .ToList();

        if (competitions.Count == 0)
        {
            throw new InvalidOperationException(
                $"index.md under {corpus} carries no '## Competitions' section — the manifest contract needs it.");
        }

        return competitions
            .Where(line => line.StartsWith("- "))
            .Select(line => line[2..].Split(' ')[0])
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Select(slug => new IndexRow(
                slug,
                competitions.First(line => line.StartsWith($"- {slug} ")).Contains("skipped")))
            .ToList();
    }

    public static GliderscoreFixture Load(string slug)
    {
        var directory = Path.Combine(ResolveCorpusDirectory(), slug);
        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException($"Fixture '{slug}' has no directory under {ResolveCorpusDirectory()}.");
        }

        var competition = Deserialize<CompetitionFile>(directory, "competition.json");
        var entries = Deserialize<EntriesFile>(directory, "entries.json");
        var scoresRaw = Deserialize<ScoresRawFile>(directory, "scores-raw.json");
        var expectedScores = Deserialize<ExpectedScoresFile>(directory, "expected-scores.json");
        var expectedResult = Deserialize<ExpectedResultFile>(directory, "expected-result.json");

        // Absent ⇒ empty ledger (D6): most fixtures have none to accept.
        var divergencesPath = Path.Combine(directory, "divergences.json");
        var divergences = File.Exists(divergencesPath)
            ? JsonSerializer.Deserialize<List<DivergenceEntry>>(
                File.ReadAllText(divergencesPath), FixtureJson)!.AsReadOnly()
            : [];

        // Absent ⇒ null oracle (grow-corpus-team-parity-fixtures.md WI-1D):
        // only team-bearing overlap fixtures carry the GS team ladder, and
        // team-less fixtures must load exactly as before. Whether an overlap
        // fixture HAS its oracle is the comparator's guard, never the
        // loader's business — no content validation happens here.
        var expectedTeamsPath = Path.Combine(directory, "expected-teams.json");
        var expectedTeams = File.Exists(expectedTeamsPath)
            ? JsonSerializer.Deserialize<ExpectedTeamsFile>(
                File.ReadAllText(expectedTeamsPath), FixtureJson)
            : null;

        // The one file deserialised with the Api's own ingestion options —
        // posting the result to /publish-class-definition must round-trip
        // through exactly the binding path a human POST would take.
        var definition = JsonSerializer.Deserialize<ClassDefinition>(
            File.ReadAllText(Path.Combine(directory, "class-definition.json")),
            ClassDefinitionIngestion.Options)
            ?? throw new InvalidOperationException($"Fixture '{slug}': class-definition.json deserialised to null.");

        return new GliderscoreFixture(
            Slug: slug,
            Directory: directory,
            Competition: competition,
            Entries: entries,
            ScoresRaw: scoresRaw,
            ExpectedScores: expectedScores,
            ExpectedResult: expectedResult,
            Divergences: divergences,
            Definition: definition,
            ExpectedTeams: expectedTeams);
    }

    /// <summary>
    /// GS 04 Step 2 — the executable-corpus registry file
    /// (tests/GliderscoreFixtures/corpus-registry.json). Consumed by
    /// <see cref="CorpusReplayRegistry"/>; since Step 3 it also governs
    /// <see cref="ActiveSlugs"/> (index.md is cross-checked, not read alone).
    /// </summary>
    public static string CorpusRegistryPath() =>
        Path.Combine(ResolveCorpusDirectory(), "corpus-registry.json");

    /// <summary>
    /// Walks up from AppContext.BaseDirectory until a tests/GliderscoreFixtures
    /// directory hangs off the current level — the repository root's shape,
    /// not a hardcoded number of ups (bin/Debug/net10.0 today, who knows when).
    /// </summary>
    public static string ResolveCorpusDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "tests", "GliderscoreFixtures");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent!;
        }

        throw new InvalidOperationException(
            $"No tests/GliderscoreFixtures directory found above {AppContext.BaseDirectory}.");
    }

    private static T Deserialize<T>(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), FixtureJson)
            ?? throw new InvalidOperationException($"{path} deserialised to null.");
    }
}
