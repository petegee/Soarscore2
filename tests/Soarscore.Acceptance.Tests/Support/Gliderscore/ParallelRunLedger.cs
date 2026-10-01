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
//                         difference in words, the rulebook or
//                         local-practice citation, and — gs_03 — the exact
//                         witnessed difference set as cells (stable cell
//                         identity with pinned GS and seed-run values, matched
//                         bidirectionally: a moved cell or an altered
//                         value/delta fails even at the same count).
//
// pilotNo arrives as either a number or "*" (the divergences.json precedent,
// DivergenceEntry) — the scalar scope must contain every declared cell: a
// per-pilot entry names its pilot, a class entry scopes null/"*". There are
// no anonymous wildcards: set-equality against the comparator's computed set
// is the whole verification, and only the declared difference set satisfies it.

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
    IReadOnlyList<string> Notes,
    // f5j-christchurch-parallel-run-witness.md WI-2 item 5 — three additive,
    // null-tolerant widenings. Every ledger authored before them (the ales
    // pair) carries none and deserialises unchanged.
    //
    // ScoredWindowRounds — the fixture's scored rollup window the run
    // prescribed (rounds 1–N); the comparator scopes its oracle-coverage
    // universe to it and verifies the run's prescribed round count equals it.
    // DerivedMetrics — the P2 derivations the harness captured from foil
    // columns under the seed's metric names (never assumptions: each is
    // declared by the adopted seed, verified by name).
    // DeclaredInstruments — the reading instruments the run declared (the
    // harness-chosen name, the metric, the TapeCorpus slug snapshotted into
    // the declaration, and the scale's rule authority); the comparator
    // verifies each against the competition's actual declaration.
    int? ScoredWindowRounds = null,
    IReadOnlyList<ParallelRunDerivedMetric>? DerivedMetrics = null,
    IReadOnlyList<ParallelRunDeclaredInstrument>? DeclaredInstruments = null,
    // f3j-international-parallel-run-retriage.md WI-1 item 3 — a fourth
    // additive, null-tolerant widening: ExcludedOracleCells declares the oracle
    // cells the run deliberately never compares (decision 7 — a provenance-level
    // declared scope, the christchurch scored-window discipline). Every ledger
    // authored before it (the ales and christchurch pairs) carries none and
    // deserialises unchanged.
    IReadOnlyList<ParallelRunExcludedCell>? ExcludedOracleCells = null);

/// <summary>One seed parameter bound from the comp's actual config, with its derivation.</summary>
public sealed record ParallelRunParameterBinding(string Parameter, decimal Value, string Derivation);

/// <summary>
/// One seed-declared metric the foil never recorded, and the value it resolves
/// to when unrecorded. <paramref name="resolved"/> is the seed-declared
/// whenNotRecorded value the engine resolves (P1), serialised verbatim —
/// a Flag as a JSON boolean, a Number as a JSON number — so both shapes the
/// metric-absence machinery declares stay expressible. The comparator verifies
/// it against the ADOPTED definition, so the disclosure and the seed cannot
/// drift apart silently. (Before WI-2 this was bool; the ales ledger's
/// <c>true</c> deserialises into the widened type unchanged.)
/// </summary>
public sealed record ParallelRunMetricMapping(
    string Metric,
    JsonElement Resolved,
    string Mechanism,
    string Justification);

/// <summary>
/// One P2 derived metric: a seed metric the harness captured from a foil
/// column under the seed's own name (e.g. startHeight from
/// Scores.FlightScoreDeduction), with the derivation rule and its
/// rulebook justification. Landing readings are NOT derived metrics — they
/// are recorded verbatim under a declared instrument (DeclaredInstruments).
/// </summary>
public sealed record ParallelRunDerivedMetric(
    string Metric,
    string Source,
    string Derivation,
    string Justification);

/// <summary>
/// One declared reading instrument: the harness-chosen <paramref name="instrument"/>
/// name the captures cite, the <paramref name="metric"/> it was read against,
/// the <paramref name="tapeSlug"/> (TapeCorpus stem) whose scale was
/// snapshotted into the declaration, and the <paramref name="clause"/> — the
/// rule authority for that scale (e.g. NZ.2.4.4).
/// </summary>
public sealed record ParallelRunDeclaredInstrument(
    string Instrument,
    string Metric,
    string TapeSlug,
    string Clause);

/// <summary>
/// One declared excluded oracle cell (f3j-international-parallel-run-retriage.md
/// decision 7): an oracle cell the run deliberately never compares, declared as
/// scope in provenance exactly like the scored window — GS persists cells the
/// seed-run can never produce (its phantom rows the draw derivation drops), and
/// undeclared they would surface as spurious "never compared" mismatches.
/// <paramref name="pilotNo"/> is a number or "*" (any pilot in the group), and
/// <paramref name="reason"/> rides unverified — the ledger review covers its
/// honesty, exactly like the provenance notes.
/// </summary>
public sealed record ParallelRunExcludedCell(
    int Round,
    int Group,
    JsonElement? PilotNo,
    string Reason)
{
    /// <summary>True when this cell declares the given oracle cell's (round,
    /// group, pilot): round and group equal, and the declared pilot is "*" or
    /// the cell's pilot number. A cell with no pilot declaration covers
    /// nothing — it matches no key and the comparator's provenance check
    /// breaks loudly (a typo, not a silent shrink).</summary>
    public bool Covers(int roundNo, int groupNo, long pilotNo) =>
        Round == roundNo && Group == groupNo
        && PilotNo is { } pilot && (
            pilot.ValueKind == JsonValueKind.Number && pilot.TryGetInt64(out var n) && n == pilotNo
            || pilot.ValueKind == JsonValueKind.String && pilot.GetString() == "*");

    /// <summary>The pilot token as written ("13" or "*"), for provenance break lines.</summary>
    public string PilotToken() => PilotNo is { } pilot && pilot.ValueKind == JsonValueKind.Number
        ? pilot.TryGetInt64(out var n) ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : pilot.GetRawText()
        : PilotNo?.GetString() ?? "(none)";
}

/// <summary>
/// gs_03_exact-divergence-contracts.md — one declared computed difference: a
/// single score cell (or ranking line) the seed-run and the GS oracle are
/// triaged to differ on, with both sides pinned exact-decimal. Gs is the GS
/// oracle value (<see cref="GrainMismatch.Expected"/>), Seed is the seed-run
/// value (<see cref="GrainMismatch.Ours"/>); either may be null exactly where
/// the comparator emits null (a rank-placement line carries both, a "=n"
/// tie-group membership line carries null on the side that excludes the
/// pilot). The grain rides the owning entry. Round/Group are the comparator's
/// coordinates (0/0 for ranking lines).
/// </summary>
public sealed record ParallelRunExpectedCell(
    int Round,
    int Group,
    long PilotNo,
    decimal? Gs,
    decimal? Seed);

/// <summary>
/// gs_03_exact-divergence-contracts.md — one declared difference the run no
/// longer produces: a stale expectation. Carries the owning entry's grain
/// (the cell itself carries no grain) and a short excerpt of the entry's
/// difference prose so the failure names the triaged cause that discharged.
/// A resolved difference fails its stale expectation in every mode.
/// </summary>
public sealed record ParallelRunMissingCell(
    string Grain,
    ParallelRunExpectedCell Cell,
    string EntryDifference);

/// <summary>
/// One triaged, witnessed difference. Round/group are null when the entry is
/// not scoped to a score cell (the ranking grain); pilotNo is a number or "*".
/// <para>
/// gs-ledger-modes.md WI-1 — the optional CI disposition, mirroring
/// DivergenceEntry: "permanent" (a cited rulebook-vs-local-practice split the
/// seed class holds by design) is reported but never fails strict mode;
/// anything else is "pending" and fails strict mode.
/// </para>
/// <para>
/// gs_03_exact-divergence-contracts.md — the exact difference set: every
/// entry declares its witnessed differences as <see cref="Cells"/>, each with
/// stable cell identity and both pinned values. Verification is bidirectional
/// over the ledger's whole declared set: a computed difference outside it —
/// a moved cell at the same count, an altered value/delta at the same key —
/// fails, and a declared cell the run no longer produces fails as stale in
/// every mode. No count is parsed from <see cref="Difference"/> prose; the
/// scalar round/group/pilot scope above must contain every declared cell (a
/// class entry scopes null/"*", a per-pilot entry names its pilot).
/// </para>
/// </summary>
public sealed record ParallelRunDifferenceEntry(
    int TriageKind,
    string Grain,
    int? Round,
    int? Group,
    JsonElement? PilotNo,
    string Difference,
    string Citation,
    string? Disposition = null,
    IReadOnlyList<ParallelRunExpectedCell>? Cells = null)
{
    /// <summary>True for a permanent-by-design entry (never fails strict mode);
    /// throws on any token other than pending/permanent — a typo must not
    /// silently demote an entry to pending.</summary>
    public bool Permanent => (Disposition ?? "pending").ToLowerInvariant() switch
    {
        "pending" => false,
        "permanent" => true,
        var d => throw new InvalidOperationException(
            $"Parallel-run ledger entry has unknown disposition '{d}' (valid: pending, permanent): {Difference[..Math.Min(80, Difference.Length)]}"),
    };
    /// <summary>True when this entry names the computed mismatch's cell: same
    /// grain, the entry's round/group scope (null = any), and the entry's pilot
    /// (number or "*"). The set-equality verdict is this predicate in both
    /// directions — every computed mismatch covered, every entry witnessed.
    /// <para>
    /// gs_03 — exact: the entry covers a computed mismatch only when one of
    /// its declared <see cref="Cells"/> matches the mismatch's cell identity
    /// AND both pinned values (a moved cell, an altered value/delta or a
    /// different grain is not covered). An entry declaring no cells covers
    /// nothing — <see cref="ValidateCells"/> fails such entries loudly at
    /// compare time, so this predicate never faces them.
    /// </para>
    /// </summary>
    public bool Covers(GrainMismatch mismatch) =>
        Grain.Equals(mismatch.Grain, StringComparison.OrdinalIgnoreCase)
        && (Cells ?? []).Any(cell =>
            (Round is null || Round == cell.Round)
            && (Group is null || Group == cell.Group)
            && PilotCovers(cell.PilotNo)
            && cell.Round == mismatch.RoundNo
            && cell.Group == mismatch.GroupNo
            && cell.PilotNo == mismatch.PilotNo
            && cell.Gs == mismatch.Expected
            && cell.Seed == mismatch.Ours);

    /// <summary>True when the entry's scalar pilot scope contains the given pilot.</summary>
    private bool PilotCovers(long pilotNo) => PilotNo is not { } pilot
        || (pilot.ValueKind == JsonValueKind.Number && pilot.TryGetInt64(out var n) && n == pilotNo)
        || (pilot.ValueKind == JsonValueKind.String && pilot.GetString() == "*");

    /// <summary>
    /// gs_03 — the structural validation every compare runs before matching:
    /// unknown disposition tokens fail (via <see cref="Permanent"/>), every
    /// entry declares a non-empty exact cell set, and every declared cell
    /// falls inside the entry's scalar round/group/pilot scope (a class entry
    /// scopes null/"*", a per-pilot entry names its pilot — a cell outside
    /// its entry's scope is a typo, not a silent widening). Throws
    /// <see cref="InvalidOperationException"/> — an authoring bug fails
    /// loudly in every mode, never as a quiet mismatch.
    /// </summary>
    public void ValidateCells()
    {
        // Touch the disposition first: an unknown token must fail even on an
        // otherwise well-formed entry.
        _ = Permanent;

        if (Cells is not { Count: > 0 })
        {
            throw new InvalidOperationException(
                $"Parallel-run ledger entry ({Grain} r{Round?.ToString() ?? "*"}/g{Group?.ToString() ?? "*"}) "
                + "declares no expected cells — every triaged difference must declare its exact witnessed "
                + "difference set (cell identities with pinned GS and seed-run values).");
        }

        foreach (var cell in Cells)
        {
            if (Round is not null && Round != cell.Round
                || Group is not null && Group != cell.Group
                || !PilotCovers(cell.PilotNo))
            {
                throw new InvalidOperationException(
                    $"Parallel-run ledger entry ({Grain} r{Round?.ToString() ?? "*"}/g{Group?.ToString() ?? "*"} "
                    + $"p{PilotToken()}) declares cell r{cell.Round}/g{cell.Group} p{cell.PilotNo} outside its own "
                    + "scope — a declared cell outside its entry's scope is a typo; widen the entry's scope "
                    + "or move the cell.");
            }
        }
    }

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
