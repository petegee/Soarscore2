// kanban/in-progress/gs-ledger-modes.md WI-3 — the rendered explanations the
// step classes assert with. The GATE policy lives in the steps (strict →
// fail pending entries at the When tail, after the full compare, so a red run
// still reports whether everything else was exact; any mode → fail unwitnessed
// pending entries); this file only renders the evidence. Everything here is
// corpus-side: no store, no HTTP.

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

public static class LedgerGate
{
    // --------------------------------------------------------------- parity

    /// <summary>The strict-mode explanation for a fixture carrying pending
    /// divergences: where and how SoarScore splits from GS, one line per
    /// entry, plus the remainder status (whether everything else is exact).
    /// Permanent entries are counted for context, never listed — the corpus
    /// report is their home.
    /// <para>
    /// gs_03 — rendered from the entries' structured fields (kind, pinned
    /// values, evidence), not from prose: a numeric line pins both values, a
    /// documentary line names its scope. The full reason prose stays as the
    /// one-glance context; the JSON carries the whole triage.
    /// </para>
    /// </summary>
    public static string FixtureDivergenceExplanation(
        GliderscoreFixture fixture, IReadOnlyList<DivergenceEntry> pending, ComparisonReport report)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;

        var lines = new List<string>
        {
            $"SOARSCORE DIVERGES FROM GLIDERSCORE on '{fixture.Slug}' — {pending.Count} pending ledgered "
            + "difference(s) in strict mode. A pending divergence is actionable debt: discharge it at source, "
            + "or re-triage it to \"disposition\": \"permanent\" with a citation if it is a decided law or a "
            + "structural fact (gs-ledger-modes.md):",
            "grain       | round | group | pilot | kind                  | expectation                    | disposition | reason",
            "------------|-------|-------|-------|-----------------------|--------------------------------|-------------|-------",
        };

        lines.AddRange(pending
            .OrderBy(d => d.Grain, StringComparer.Ordinal)
            .ThenBy(d => d.Round).ThenBy(d => d.Group).ThenBy(d => d.PilotToken())
            .Select(d => string.Format(
                invariant,
                "{0,-11} | {1,5} | {2,5} | {3,5} | {4,-21} | {5,-30} | {6,-11} | {7}",
                d.Grain, d.Round?.ToString(invariant) ?? "*", d.Group?.ToString(invariant) ?? "*",
                d.PilotToken(), SafeKind(d), Expectation(d), d.Disposition ?? "(pending)", FirstSentence(d.Reason))));

        lines.Add($"full reasons: tests/GliderscoreFixtures/{fixture.Slug}/divergences.json");

        var permanent = fixture.Divergences.Count(d => d.Permanent);
        if (permanent > 0)
        {
            lines.Add(
                $"(the fixture also carries {permanent} permanent divergence(s) — reported, never failed; "
                + "see TestResults/gs-divergences.md)");
        }

        lines.Add(report.AllGrainsExact
            ? $"Every other cell compared exact: raw {report.RawCellsCompared}/{report.OracleCells}, "
                + $"normalised {report.NormalisedCellsCompared}/{report.OracleCells}, ranking "
                + $"{report.RankingPilotsCompared} pilots."
            : $"AND {report.RawMismatches.Count + report.NormalisedMismatches.Count + report.RankingMismatches.Count} "
                + "UNLEDGERED mismatch(es) beyond the ledger — a new divergence, never a ledger edit:"
                + $"{Environment.NewLine}{report.DiffTable()}");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>The unwitnessed-entry explanation — a pending entry whose
    /// divergence no longer fires is a discharged divergence the ledger still
    /// carries; it fails in every mode (WI-2's hygiene arm).
    /// <para>
    /// gs_03 — every observed-difference entry (any disposition) reports
    /// here with its structured expectation, so a stale pin names the value
    /// the run no longer produces.
    /// </para>
    /// </summary>
    public static string UnwitnessedExplanation(
        GliderscoreFixture fixture, IReadOnlyList<DivergenceEntry> unwitnessed)
    {
        var lines = new List<string>
        {
            $"STALE LEDGER on '{fixture.Slug}' — {unwitnessed.Count} pending ledgered divergence(s) cover NO "
            + "computed mismatch: the divergence they triage no longer fires, so the entry is discharged and "
            + "must be removed from divergences.json, never silently carried (gs-ledger-modes.md WI-2):",
        };

        lines.AddRange(unwitnessed.Select(d =>
            $"  {d.Grain} r{d.Round?.ToString() ?? "*"}/g{d.Group?.ToString() ?? "*"} p{d.PilotToken()} "
            + $"[{SafeKind(d)}; expects {Expectation(d)}]: "
            + FirstSentence(d.Reason)));

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>gs_03 — the documentary scope-break explanation: an
    /// unsupported-comparison entry whose declared method now runs (or never
    /// applied) fails in every mode, like a stale entry.</summary>
    public static string ScopeBreakExplanation(
        GliderscoreFixture fixture, IReadOnlyList<string> breaks)
    {
        var lines = new List<string>
        {
            $"STALE DOCUMENTARY LEDGER on '{fixture.Slug}' — {breaks.Count} unsupported-comparison entr(ies) "
            + "no longer describe the run: the team comparison they excuse now runs, or the declared method "
            + "never applied. Remove or re-triage the entry, never silently carry it "
            + "(gs_03_exact-divergence-contracts.md):",
        };

        lines.AddRange(breaks.Select(b => $"  {b}"));

        return string.Join(Environment.NewLine, lines);
    }

    // ---------------------------------------------------------- parallel run

    /// <summary>The strict-mode explanation for a pair carrying pending triaged
    /// differences: the parallel-run split is actionable debt (discharge the
    /// seed class, or triage to permanent with its citation).
    /// <para>
    /// gs_03 — rendered from structured fields: each entry's declared cell
    /// count (identity + pinned values live in the ledger JSON), never a
    /// prose-parsed count.
    /// </para>
    /// </summary>
    public static string PairDivergenceExplanation(IReadOnlyList<ParallelRunDifferenceEntry> pending)
    {
        var lines = new List<string>
        {
            $"PARALLEL RUN DIVERGES FROM GLIDERSCORE — {pending.Count} pending triaged difference(s) in strict "
            + "mode. A pending split is actionable debt: fix the seed class and discharge the entry, or re-triage "
            + "it to \"disposition\": \"permanent\" with its citation (gs-ledger-modes.md):",
            "kind | grain       | round/group/pilot | cells | disposition | difference | citation",
            "-----|-------------|-------------------|-------|-------------|------------|---------",
        };

        lines.AddRange(pending
            .OrderBy(e => e.Grain, StringComparer.Ordinal)
            .ThenBy(e => e.Round).ThenBy(e => e.Group).ThenBy(e => e.PilotToken())
            .Select(e => $"{e.TriageKind,4} | {e.Grain,-11} | r{e.Round?.ToString() ?? "*"}/g{e.Group?.ToString() ?? "*"} "
                + $"p{e.PilotToken(),-3} | {(e.Cells?.Count ?? 0),5} | {e.Disposition ?? "(pending)",-11} | {FirstSentence(e.Difference)} | "
                + FirstSentence(e.Citation)));

        return string.Join(Environment.NewLine, lines);
    }

    // -------------------------------------------------------------- plumbing

    /// <summary>gs_03 — the entry's kind without throwing: the strict-mode
    /// table renders ledgers that may still be mid-migration, so an unknown
    /// token prints instead of throwing (the compare itself still fails it
    /// loudly via KindNormalized).</summary>
    private static string SafeKind(DivergenceEntry entry)
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

    /// <summary>gs_03 — the structured expectation one-liner: pinned
    /// ours-vs-expected values for numeric entries, the documentary scope
    /// otherwise, with the evidence reference where one is declared.</summary>
    private static string Expectation(DivergenceEntry entry)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var expectation = entry.KindNormalized switch
        {
            "numeric" => entry.Ours is { } ours && entry.Expected is { } expected
                ? $"ours {ours.ToString(invariant)} vs GS {expected.ToString(invariant)}"
                : "(pins missing)",
            "excludedOracleCell" => "oracle cell never replayed",
            "syntheticSlot" => "our-only slot, no oracle cell",
            "unsupportedComparison" => "team comparison does not run",
            _ => "(unknown kind)",
        };

        return entry.Evidence is { } evidence
            ? $"{expectation} [{evidence}]"
            : expectation;
    }

    /// <summary>The first sentence of a long reason — the one-glance form;
    /// the corpus report and the JSON carry the full text.</summary>
    private static string FirstSentence(string text)
    {
        var cut = text.IndexOf(". ", StringComparison.Ordinal);
        var sentence = cut < 0 ? text : text[..(cut + 1)];

        return sentence.Length <= 160 ? sentence : sentence[..157] + "...";
    }
}
