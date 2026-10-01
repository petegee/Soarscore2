// GS 04 Step 3 — the explicit scenario-coverage gate: the registry declares
// every active fixture and every parallel-run pair, and these tests prove an
// executable scenario exists for each. An active fixture without its replay
// scenario (or a parallel-run mode without its parallel-run scenario) fails
// here, naming the slug — the story's acceptance criterion, kept as data:
// the .feature files stay literal-record scenarios (additional workflows are
// untouched) and this gate reads them as text, never by executing them.
//
// Pure unit tests: they read the registry, index.md and the two .feature
// sources from the repo layout beside the corpus, so no store, HTTP or replay
// is involved. Deterministic: registry order, ordinal string comparison.

using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;
using Soarscore.Acceptance.Tests.Support.Gliderscore;

namespace Soarscore.Acceptance.Tests;

public sealed class CorpusCoverageTests
{
    [Fact]
    public void ActiveSlugs_follow_the_registry_with_the_index_agreeing()
    {
        // Step 3 migration proof: the loader's active set IS the registry's
        // active set, in registry order — index.md agrees (ActiveSlugs throws
        // on any drift, so reaching the assertion already proves it).
        var manifest = CorpusReplayRegistry.ManifestEntries();
        var registryActive = manifest.Where(e => e.Status == "active").Select(e => e.Slug).ToList();

        FixtureLoader.ActiveSlugs().Should().Equal(registryActive);
    }

    [Fact]
    public void Every_active_fixture_has_its_replay_scenario()
    {
        var replayed = ScenarioSlugs(
            FeatureText("ReplayingAGliderscoreFixture.feature"),
            @"replays the GliderScore fixture ""([^""]+)""");

        var missing = FixtureLoader.ActiveSlugs().Where(slug => !replayed.Contains(slug)).ToList();

        missing.Should().BeEmpty(
            "every active registry fixture requires its executable replay scenario — "
            + "add the literal-record scenario, never delete the registry entry to fit.");
    }

    [Fact]
    public void Every_registry_parallel_run_mode_has_its_parallel_run_scenario()
    {
        var pairs = ScenarioPairs(FeatureText("ParallelRunningAGliderscoreFixture.feature"));

        var missing = CorpusReplayRegistry.ManifestEntries()
            .SelectMany(e => e.Modes
                .Where(m => m.Mode == "parallel-run")
                .Select(m => (Slug: e.Slug, Seed: m.Seed ?? "<no seed declared>")))
            .Where(want => !pairs.Contains((want.Slug, want.Seed ?? string.Empty)) || want.Seed is null)
            .Select(want => want.Seed is null
                ? $"{want.Slug} declares a parallel-run mode with no seed"
                : $"{want.Slug} under seed {want.Seed}")
            .ToList();

        missing.Should().BeEmpty(
            "every registry parallel-run mode requires its executable parallel-run scenario — "
            + "add the literal-record scenario, never delete the registry mode to fit.");
    }

    [Fact]
    public void No_scenario_names_a_slug_outside_the_registry()
    {
        var known = CorpusReplayRegistry.ManifestEntries().Select(e => e.Slug).ToHashSet(StringComparer.Ordinal);

        var orphaned = ScenarioSlugs(
            FeatureText("ReplayingAGliderscoreFixture.feature"),
            @"replays the GliderScore fixture ""([^""]+)""")
            .Concat(ScenarioPairs(FeatureText("ParallelRunningAGliderscoreFixture.feature"))
                .Select(p => p.Slug))
            .Where(slug => !known.Contains(slug))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(slug => slug, StringComparer.Ordinal)
            .ToList();

        orphaned.Should().BeEmpty(
            "a scenario naming a slug the registry does not list is an orphaned reference — "
            + "register the fixture or drop the scenario.");
    }

    private static string FeatureText(string fileName)
    {
        var corpus = FixtureLoader.ResolveCorpusDirectory();
        var path = Path.GetFullPath(Path.Combine(
            corpus, "..", "Soarscore.Acceptance.Tests", "Features", fileName));

        return File.ReadAllText(path);
    }

    private static HashSet<string> ScenarioSlugs(string feature, string pattern) =>
        Regex.Matches(feature, pattern, RegexOptions.CultureInvariant)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<(string Slug, string Seed)> ScenarioPairs(string feature) =>
        Regex.Matches(
                feature,
                @"parallel-runs the GliderScore fixture ""([^""]+)"" under the seed class ""([^""]+)""",
                RegexOptions.CultureInvariant)
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value))
            .ToHashSet();
}
