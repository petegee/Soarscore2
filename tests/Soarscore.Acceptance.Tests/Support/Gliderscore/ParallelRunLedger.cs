// kanban/in-progress/seed-definition-parallel-run.md WI-2 item 1 — the
// parallel-run LEDGER's deserialised shape and its loader.
//
// The ledger is a DIFFERENT CONTRACT from a fixture's divergences.json
// (story law, "Ledger semantics"): distinct schema, distinct comparator, never
// merged. Where divergences.json holds the parity harness's accepted
// D-number/trap/R1/T1 divergences, a parallel-run ledger holds one
// (fixture, seed-class) pair's claim — *the differences are exactly the
// triaged set* — as three blocks:
//
//   provenance          — the P4 disclosure block: the parameter bindings
//                         with their derivations (what DeriveParallelRunBindings
//                         bound and why) and the P1/P2 metric-mapping
//                         disclosures (each seed-declared whenNotRecorded
//                         resolution, justified against docs/rules/nz/ — never
//                         against GliderScore). The harness emits NO assumed
//                         values; this block discloses what the seed class
//                         itself declares and the engine resolves. Unwitnessed
//                         triaged candidates live here as NOTES, never as
//                         difference entries: a triaged difference that fails
//                         to appear fails the scenario (WI-2.3), so anything
//                         not actually witnessed on the pair's data may only
//                         be provenance.
//   triagedDifferences — the measured, witnessed difference set. Each entry:
//                         triage kind (story What: 1 local variation / 2
//                         seed-class authoring gap / 3 engine divergence),
//                         grain ("raw" | "normalised" | "ranking" — the
//                         comparator's grains; WI-2.2's product grain "final
//                         placings" is the comparator's "ranking"), the
//                         difference in words, and the rulebook or
//                         local-practice citation.
//
// pilotNo arrives as either a number or "*" (the divergences.json precedent,
// DivergenceEntry) — the wildcard is a matching convenience for entries whose
// difference is genuinely per-cell anonymous; witnessed entries should name
// their cells, because set-equality against the comparator's computed set is
// the whole verification.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

/// <summary>One (fixture, seed class) pair's parallel-run ledger.</summary>
public sealed record ParallelRunLedger(
    ParallelRunPair Pair,
    ParallelRunSeedClass SeedClass,
    ParallelRunProvenance Provenance,
    IReadOnlyList<ParallelRunDifferenceEntry> TriagedDifferences);

/// <summary>Pair identity: fixture slug + seed slug (the seed JSON file name without extension).</summary>
public sealed record ParallelRunPair(string Fixture, string Seed);

/// <summary>The seed class's own name/version — the published competition carries them
/// (ReplayDriver WI-1 naming), so parallel-run artifacts are self-describing
/// against the fixture's GS-mirrored twin.</summary>
public sealed record ParallelRunSeedClass(string Name, string Version);

/// <summary>The P4 disclosure block (decision 1).</summary>
public sealed record ParallelRunProvenance(
    IReadOnlyList<ParallelRunParameterBinding> ParameterBindings,
    IReadOnlyList<ParallelRunMetricMapping> MetricMappings,
    IReadOnlyList<string> Notes);

/// <summary>One seed parameter bound from the comp's actual config, with its derivation.</summary>
public sealed record ParallelRunParameterBinding(string Parameter, decimal Value, string Derivation);

/// <summary>
/// One seed-declared metric the foil never recorded, and the value it resolves
/// to when unrecorded. <paramref name="resolved"/> is the seed-declared
/// whenNotRecorded FLAG value the engine resolves (P1); the comparator verifies
/// it against the ADOPTED definition, so the disclosure and the seed cannot
/// drift apart silently.
/// </summary>
public sealed record ParallelRunMetricMapping(
    string Metric,
    bool Resolved,
    string Mechanism,
    string Justification);

/// <summary>
/// One triaged, witnessed difference. Round/group are null when the entry is
/// not scoped to a score cell (the ranking grain); pilotNo is a number or "*".
/// </summary>
public sealed record ParallelRunDifferenceEntry(
    int TriageKind,
    string Grain,
    int? Round,
    int? Group,
    JsonElement? PilotNo,
    string Difference,
    string Citation)
{
    /// <summary>True when this entry names the computed mismatch's cell: same
    /// grain, the entry's round/group scope (null = any), and the entry's pilot
    /// (number or "*"). The set-equality verdict is this predicate in both
    /// directions — every computed mismatch covered, every entry witnessed.</summary>
    public bool Covers(GrainMismatch mismatch) =>
        Grain.Equals(mismatch.Grain, StringComparison.OrdinalIgnoreCase)
        && (Round is null || Round == mismatch.RoundNo)
        && (Group is null || Group == mismatch.GroupNo)
        && PilotNo is { } pilot && (
            pilot.ValueKind == JsonValueKind.Number && pilot.TryGetInt64(out var n) && n == mismatch.PilotNo
            || pilot.ValueKind == JsonValueKind.String && pilot.GetString() == "*");

    /// <summary>The pilot token as written ("13" or "*"), for report lines.</summary>
    public string PilotToken() => PilotNo is { } pilot && pilot.ValueKind == JsonValueKind.Number
        ? pilot.TryGetInt64(out var n) ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : pilot.GetRawText()
        : PilotNo?.GetString() ?? "(none)";
}

/// <summary>
/// Loads one pair's parallel-run ledger from
/// tests/GliderscoreFixtures/&lt;fixture-slug&gt;/parallel-run/&lt;seed-slug&gt;.json —
/// an ADDITION beside the fixture corpus (WI-3.4), never a corpus file change.
/// Case-insensitive options, matching the fixture JSON conventions.
/// </summary>
public static class ParallelRunLedgerLoader
{
    private static readonly JsonSerializerOptions LedgerJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ParallelRunLedger Load(string fixtureSlug, string seedSlug)
    {
        var path = PathFor(fixtureSlug, seedSlug);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Parallel-run ledger for pair ({fixtureSlug}, {seedSlug}) has no file at {path}.");
        }

        return JsonSerializer.Deserialize<ParallelRunLedger>(File.ReadAllText(path), LedgerJson)
            ?? throw new InvalidOperationException($"Parallel-run ledger '{path}' deserialised to null.");
    }

    public static string PathFor(string fixtureSlug, string seedSlug) =>
        Path.Combine(FixtureLoader.ResolveCorpusDirectory(), fixtureSlug, "parallel-run", $"{seedSlug}.json");
}
