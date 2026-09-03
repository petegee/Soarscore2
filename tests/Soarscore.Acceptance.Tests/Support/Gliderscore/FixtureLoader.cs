// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-1 — loads
// fixtures out of tests/GliderscoreFixtures/ for the replay harness (D1: the
// corpus stays where it is; the harness resolves the directory from the test
// assembly's location by walking up, so no build-output depth is hardcoded).
//
// index.md is the manifest and its header carries the tokenisation contract:
// each competition is one `- <slug> — <status> — …` bullet, and a slug counts
// as SKIP-LISTED when its line contains "skipped" anywhere. Bullets wrap over
// several lines; continuation lines never start with "- " and are ignored.

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

    /// <summary>Every slug the manifest lists as active (not skip-listed).</summary>
    public static IReadOnlyList<string> ActiveSlugs()
    {
        var corpus = ResolveCorpusDirectory();
        var index = File.ReadAllLines(Path.Combine(corpus, "index.md"));

        return index
            .Where(line => line.StartsWith("- "))
            .Select(line => line[2..].Split(' ')[0])
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Where(slug => !index.First(line => line.StartsWith($"- {slug} ")).Contains("skipped"))
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
