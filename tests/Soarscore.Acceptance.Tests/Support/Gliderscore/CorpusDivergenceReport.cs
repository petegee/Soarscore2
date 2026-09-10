// kanban/in-progress/gs-ledger-modes.md WI-5 — the run-level corpus report:
// one artifact answering "where and how does SoarScore diverge from
// GliderScore", green or red, strict or ledgered. Written after the test run
// walks every active fixture's divergences.json and every parallel-run pair
// ledger. Generated output under TestResults/ — never a docs file, never a
// status document (board rule 7); the write is best-effort because report
// generation must never fail the run it summarises.

using Reqnroll;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

public static class CorpusDivergenceReport
{
    public static string Write()
    {
        var corpus = FixtureLoader.ResolveCorpusDirectory();
        var mode = GsLedgerModeReader.FromEnvironment();
        var invariant = System.Globalization.CultureInfo.InvariantCulture;

        var lines = new List<string>
        {
            "# SoarScore ↔ GliderScore divergence report",
            "",
            $"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm}UTC by the @gliderscore acceptance run — "
            + $"{GsLedgerModeReader.EnvironmentVariableName}={mode.ToString().ToLowerInvariant()}.",
            "",
            "Every ledgered, triaged difference between SoarScore and GliderScore across the corpus. "
            + "`pending` entries are actionable debt (strict mode fails them); `permanent` entries are decided "
            + "laws or structural facts (kanban/deferred-decisions.md) — reported, never failed. "
            + "The full reason text lives in the fixture's divergences.json / parallel-run/<seed>.json.",
            "",
        };

        var fixtureEntryTotal = 0;

        lines.Add("## Parity fixtures (replay)");
        lines.Add("");

        foreach (var slug in FixtureLoader.ActiveSlugs())
        {
            // index.md's bullets include non-fixture sections ("§6 concept
            // gaps", open-work headers) — ActiveSlugs tokenises every bullet;
            // only slugs with a fixture directory can carry a ledger.
            if (!Directory.Exists(Path.Combine(corpus, slug)))
            {
                continue;
            }

            var fixture = FixtureLoader.Load(slug);

            if (fixture.Divergences.Count == 0)
            {
                continue;
            }

            var permanent = fixture.Divergences.Count(d => d.Permanent);

            lines.Add($"### {slug} — {fixture.Divergences.Count} "
                + (fixture.Divergences.Count == 1 ? "entry" : "entries") + $", {permanent} permanent");
            lines.Add(
                "grain       | round | group | pilot | disposition | reason");
            lines.Add(
                "------------|-------|-------|-------|-------------|-------");

            foreach (var d in fixture.Divergences
                .OrderBy(d => d.Grain, StringComparer.Ordinal)
                .ThenBy(d => d.Round).ThenBy(d => d.Group).ThenBy(d => d.PilotToken()))
            {
                lines.Add(string.Format(
                    invariant,
                    "{0,-11} | {1,5} | {2,5} | {3,5} | {4,-11} | {5}",
                    d.Grain, d.Round?.ToString(invariant) ?? "*", d.Group?.ToString(invariant) ?? "*",
                    d.PilotToken(), d.Disposition ?? "(pending)", d.Reason));
            }

            lines.Add("");
            fixtureEntryTotal += fixture.Divergences.Count;
        }

        var pairEntryTotal = 0;

        lines.Add("## Parallel-run pairs (seed class vs GS on the day)");
        lines.Add("");

        foreach (var slug in FixtureLoader.ActiveSlugs())
        {
            var pairDirectory = Path.Combine(FixtureLoader.ResolveCorpusDirectory(), slug, "parallel-run");

            if (!Directory.Exists(pairDirectory))
            {
                continue;
            }

            foreach (var ledgerPath in Directory.GetFiles(pairDirectory, "*.json").Order())
            {
                var seedSlug = Path.GetFileNameWithoutExtension(ledgerPath);
                var ledger = ParallelRunLedgerLoader.Load(slug, seedSlug);

                if (ledger.TriagedDifferences.Count == 0)
                {
                    continue;
                }

                var permanent = ledger.TriagedDifferences.Count(e => e.Permanent);

                lines.Add($"### {slug} under {seedSlug} — {ledger.TriagedDifferences.Count} "
                    + (ledger.TriagedDifferences.Count == 1 ? "entry" : "entries") + $", {permanent} permanent");
                lines.Add(
                    "kind | grain       | round/group/pilot | disposition | difference | citation");
                lines.Add(
                    "-----|-------------|-------------------|-------------|------------|---------");

                foreach (var e in ledger.TriagedDifferences
                    .OrderBy(e => e.Grain, StringComparer.Ordinal)
                    .ThenBy(e => e.Round).ThenBy(e => e.Group).ThenBy(e => e.PilotToken()))
                {
                    lines.Add($"{e.TriageKind,4} | {e.Grain,-11} | r{e.Round?.ToString() ?? "*"}/g{e.Group?.ToString() ?? "*"} "
                        + $"p{e.PilotToken(),-3} | {e.Disposition ?? "(pending)",-11} | {e.Difference} | {e.Citation}");
                }

                lines.Add("");
                pairEntryTotal += ledger.TriagedDifferences.Count;
            }
        }

        lines.Add(
            $"Totals: parity {fixtureEntryTotal} entr"
            + (fixtureEntryTotal == 1 ? "y" : "ies") + $", parallel-run {pairEntryTotal} entr"
            + (pairEntryTotal == 1 ? "y" : "ies") + ".");

        var path = PathFor();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);

        return path;
    }

    /// <summary>TestResults/ off the repository root — the same walk-up
    /// FixtureLoader uses, so the path follows the tree, not the runner.</summary>
    private static string PathFor()
    {
        var corpus = FixtureLoader.ResolveCorpusDirectory();

        return Path.Combine(
            Directory.GetParent(corpus)!.Parent!.FullName, "TestResults", "gs-divergences.md");
    }
}

/// <summary>The Reqnroll hook — one report per test run, after everything.</summary>
[Binding]
public sealed class CorpusDivergenceReportHook
{
    [AfterTestRun]
    public static void AfterTestRun()
    {
        // Best-effort: the report is visibility, not a gate — a write failure
        // (read-only CI volume, path policy) must not fail the run.
        try
        {
            var path = CorpusDivergenceReport.Write();
            Console.WriteLine($"GS divergence report: {path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GS divergence report could not be written: {ex.Message}");
        }
    }
}
