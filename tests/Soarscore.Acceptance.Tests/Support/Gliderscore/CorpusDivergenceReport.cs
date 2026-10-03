// kanban/in-progress/gs-ledger-modes.md WI-5 — the run-level corpus report:
// one artifact answering "where and how does SoarScore diverge from
// GliderScore", green or red, strict or ledgered. Written after the test run
// walks every active fixture's divergences.json and every parallel-run pair
// ledger. Generated output under TestResults/ — never a docs file, never a
// status document (board rule 7); the write is best-effort because report
// generation must never fail the run it summarises.
//
// GS 05 — the header now pins the run identity (store + ledger mode) and the
// corpus snapshot (commit + registry active/skipped sets), and the tail names
// the fixtures with no ledgered divergences and the skipped fixtures, so the
// report distinguishes compared fixtures from never-compared ones. Compared-
// cell totals are referenced (the generated index.md block), never copied.
// Ledger visibility only: per-scenario outcomes live in the TRX, and a red
// run claims no coverage from this file.

using Reqnroll;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

public static class CorpusDivergenceReport
{
    public static string Write()
    {
        var corpus = FixtureLoader.ResolveCorpusDirectory();
        var mode = GsLedgerModeReader.FromEnvironment();
        var store = Environment.GetEnvironmentVariable("SOARSCORE_TEST_STORE") ?? "postgres";
        var invariant = System.Globalization.CultureInfo.InvariantCulture;

        var lines = new List<string>
        {
            "# SoarScore ↔ GliderScore divergence report",
            "",
            $"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm}UTC by the @gliderscore acceptance run — "
            + $"SOARSCORE_TEST_STORE={store}, {GsLedgerModeReader.EnvironmentVariableName}={mode.ToString().ToLowerInvariant()}.",
            "",
            SnapshotIdentityLine(corpus),
            "",
            "Ledger visibility only: this file lists committed ledger entries, never the run's verdict. "
            + "Which assertions ran, passed, failed or skipped lives in the test results (one TRX per backend "
            + "in CI); a run that did not finish green claims no coverage from this file. Unsupported "
            + "comparisons (grains the harness never runs) appear below as documentary entries; skipped "
            + "fixtures are listed at the end and are never compared.",
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
                "grain       | round | group | pilot | kind                  | expectation                    | disposition | reason");
            lines.Add(
                "------------|-------|-------|-------|-----------------------|--------------------------------|-------------|-------");

            foreach (var d in fixture.Divergences
                .OrderBy(d => d.Grain, StringComparer.Ordinal)
                .ThenBy(d => d.Round).ThenBy(d => d.Group).ThenBy(d => d.PilotToken()))
            {
                lines.Add(string.Format(
                    invariant,
                    "{0,-11} | {1,5} | {2,5} | {3,5} | {4,-21} | {5,-30} | {6,-11} | {7}",
                    d.Grain, d.Round?.ToString(invariant) ?? "*", d.Group?.ToString(invariant) ?? "*",
                    d.PilotToken(), RenderKind(d), RenderExpectation(d),
                    d.Disposition ?? "(pending)", d.Reason));
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
                    "kind | grain       | round/group/pilot | cells | disposition | difference | citation");
                lines.Add(
                    "-----|-------------|-------------------|-------|-------------|------------|---------");

                foreach (var e in ledger.TriagedDifferences
                    .OrderBy(e => e.Grain, StringComparer.Ordinal)
                    .ThenBy(e => e.Round).ThenBy(e => e.Group).ThenBy(e => e.PilotToken()))
                {
                    lines.Add($"{e.TriageKind,4} | {e.Grain,-11} | r{e.Round?.ToString() ?? "*"}/g{e.Group?.ToString() ?? "*"} "
                        + $"p{e.PilotToken(),-3} | {(e.Cells?.Count ?? 0),5} | {e.Disposition ?? "(pending)",-11} | {e.Difference} | {e.Citation}");

                    // gs_03 — the declared exact set beside the prose: every
                    // declared cell with both pinned values, so the report
                    // answers "which cells, which values" structurally.
                    foreach (var cell in (e.Cells ?? []).OrderBy(c => c.Round).ThenBy(c => c.Group).ThenBy(c => c.PilotNo))
                    {
                        lines.Add($"         |             |   r{cell.Round}/g{cell.Group} p{cell.PilotNo}: "
                            + $"seed-run {cell.Seed?.ToString(invariant) ?? "(none)"} vs GS oracle "
                            + $"{cell.Gs?.ToString(invariant) ?? "(none)"}");
                    }
                }

                lines.Add("");
                pairEntryTotal += ledger.TriagedDifferences.Count;
            }
        }

        lines.Add(
            $"Totals: parity {fixtureEntryTotal} entr"
            + (fixtureEntryTotal == 1 ? "y" : "ies") + $", parallel-run {pairEntryTotal} entr"
            + (pairEntryTotal == 1 ? "y" : "ies") + ".");
        lines.Add("");

        // GS 05 — compared vs never-compared, explicitly: fixtures with an
        // empty ledger compare exact on every grain when their scenarios pass
        // (TRX), while skipped registry fixtures never replay at all.
        if (CorpusReplayRegistry.RegistryGoverns)
        {
            var exactSlugs = new List<string>();

            foreach (var slug in FixtureLoader.ActiveSlugs())
            {
                if (!Directory.Exists(Path.Combine(corpus, slug)))
                {
                    continue;
                }

                if (FixtureLoader.Load(slug).Divergences.Count == 0)
                {
                    exactSlugs.Add(slug);
                }
            }

            lines.Add("## Fixtures with no ledgered divergences");
            lines.Add("");
            lines.Add(exactSlugs.Count == 0
                ? "None — every active fixture carries ledgered entries above."
                : string.Join(", ", exactSlugs)
                    + " — exact on every compared grain when their scenarios pass (see the TRX).");
            lines.Add("");

            var skipped = CorpusReplayRegistry.ManifestEntries()
                .Where(e => e.Status == "skipped")
                .Select(e => e.Slug)
                .ToList();

            lines.Add("## Skipped fixtures (never compared)");
            lines.Add("");
            lines.Add(skipped.Count == 0
                ? "None."
                : string.Join(", ", skipped)
                    + " — skip-listed in index.md; no replay runs and no comparison is claimed.");
            lines.Add("");
        }

        var path = PathFor();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);

        return path;
    }

    /// <summary>GS 05 — the corpus snapshot identity: which commit and which
    /// registry set this run's ledgers were read from. Compared-cell totals
    /// are referenced (the generated index.md block), never copied, so this
    /// line cannot drift from the summaries `corpus.py regen --check` gates.
    /// Best-effort like the rest of the report: the hook swallows failures.
    /// </summary>
    private static string SnapshotIdentityLine(string corpus)
    {
        var sha = Environment.GetEnvironmentVariable("GITHUB_SHA");
        var commit = string.IsNullOrWhiteSpace(sha)
            ? "working tree (no GITHUB_SHA)"
            : $"commit {sha[..Math.Min(12, sha.Length)]}";

        if (!CorpusReplayRegistry.RegistryGoverns)
        {
            return $"Corpus snapshot: {commit}; corpus-registry.json absent (pre-registry checkout) — "
                + "the index alone governs the fixture set.";
        }

        var manifest = CorpusReplayRegistry.ManifestEntries();
        var active = manifest.Where(e => e.Status == "active").Select(e => e.Slug).ToList();
        var skipped = manifest.Where(e => e.Status == "skipped").Select(e => e.Slug).ToList();

        return $"Corpus snapshot: {commit}; corpus-registry.json schema v1 — "
            + $"{active.Count} active ({string.Join(", ", active)}), "
            + $"{skipped.Count} skipped ({string.Join(", ", skipped)}). "
            + "Compared-cell totals per fixture: tests/GliderscoreFixtures/index.md "
            + "(generated oracle-coverage block, `corpus.py regen --check` gated); "
            + "seed coverage: tests/GliderscoreFixtures/parallel-run-mapping.md.";
    }

    /// <summary>gs_03 — the structured kind without throwing: the report is
    /// best-effort visibility, so an unknown token prints instead of
    /// breaking the run (the compare itself fails it loudly).</summary>
    private static string RenderKind(DivergenceEntry entry)
    {
        try
        {
            return entry.KindNormalized;
        }
        catch (InvalidOperationException)
        {
            return $"unknown-kind:{entry.Kind}";
        }
    }

    /// <summary>gs_03 — the structured expectation one-liner beside the
    /// prose: pinned ours-vs-expected values for numeric entries, the
    /// documentary scope otherwise, with the evidence reference where one
    /// is declared.</summary>
    private static string RenderExpectation(DivergenceEntry entry)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;

        string expectation;
        try
        {
            expectation = entry.KindNormalized switch
            {
                "numeric" => entry.Ours is { } ours && entry.Expected is { } expected
                    ? $"ours {ours.ToString(invariant)} vs GS {expected.ToString(invariant)}"
                    : "(pins missing)",
                "excludedOracleCell" => "oracle cell never replayed",
                "syntheticSlot" => "our-only slot, no oracle cell",
                "unsupportedComparison" => "team comparison does not run",
                _ => "(unknown kind)",
            };
        }
        catch (InvalidOperationException)
        {
            expectation = "(unknown kind)";
        }

        return entry.Evidence is { } evidence
            ? $"{expectation} [{evidence}]"
            : expectation;
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
