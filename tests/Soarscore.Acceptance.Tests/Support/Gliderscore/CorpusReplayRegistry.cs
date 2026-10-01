// GS 04 Step 2 — the executable-corpus replay registry loader. Fixture-
// specific replay declarations (round parameter binds, synthetic slots,
// parallel-run scored-window/tape/skip flags) live in validated fixture data
// (tests/GliderscoreFixtures/corpus-registry.json, schema v1) and this is the
// single reader. Generic mechanisms (DeriveParallelRunBindings, F3KSlotMap,
// capture maps, TaskByRound, team decision-8 mapping) stay in ReplayDriver.
//
// Authoritative-with-fallback discipline: when corpus-registry.json is present
// the registry governs and every declaration is cross-checked against the
// verbatim-migrated fallback tables below (a mismatch throws, naming the slug
// and field — the no-behavior-change proof). When the file is absent the
// fallbacks govern, so strict runs stay green on checkouts predating the
// registry. No class-specific branches anywhere: the tables are data keyed by
// slug, and unknown slugs read as empty (the parity default).
//
// Class arithmetic stays in class-definition.json; expected values stay in
// expected-*.json — the registry holds references and replay scope only.

using System.Text.Json;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

/// <summary>
/// GS 04 Step 3 — one registry manifest row: fixture identity, lifecycle
/// status and the executable modes the coverage gate requires scenarios for.
/// Data only; never branched on by class.
/// </summary>
public sealed record CorpusManifestEntry(
    string Slug,
    string Status,
    IReadOnlyList<CorpusManifestMode> Modes);

/// <summary>One executable mode: "parity", or "parallel-run" with its seed-class slug.</summary>
public sealed record CorpusManifestMode(string Mode, string? Seed);

public static class CorpusReplayRegistry
{
    private static readonly JsonSerializerOptions RegistryJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ------------------------------------------------------------------ data
    // Verbatim-migrated fallbacks: the exact contents of ReplayDriver's six
    // pre-registry dictionaries (see the basisRef on each registry entry).
    // Compared element-for-element against the registry on load; never edited
    // independently — a fixture change lands in corpus-registry.json and a
    // mismatch here fails loudly until the fallback is re-migrated.
    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<(string Parameter, int RoundNo, decimal Value)>>
        FallbackRoundParameterBinds = new Dictionary<string, IReadOnlyList<(string, int, decimal)>>
        {
            ["f3j-international"] = [("targetTime", 1, 540m)],
        };

    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<(int RoundNo, int GroupNo, long PilotNo)>>
        FallbackPrescriptionOnlySlots = new Dictionary<string, IReadOnlyList<(int, int, long)>>
        {
            ["jerilderie-2010"] = [(12, 1, 29)],
            ["f5j-hawkes-bay-trials"] =
                [(1, 2, 128), (2, 1, 128), (3, 2, 128), (4, 1, 128)],
            ["f5k-ni-round-2"] =
                [(6, 2, 88), (7, 2, 88), (8, 2, 88), (9, 2, 88), (10, 2, 88)],
        };

    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<(int RoundNo, int GroupNo, long PilotNo)>>
        FallbackFlightLessSlots = new Dictionary<string, IReadOnlyList<(int, int, long)>>
        {
            ["f3k-southern-fling"] =
                [(9, 3, 89), (10, 3, 89), (11, 3, 89), (12, 3, 89), (13, 3, 89), (14, 3, 89), (15, 3, 89)],
        };

    internal static readonly IReadOnlyDictionary<string, int> FallbackScoredWindowRounds =
        new Dictionary<string, int>
        {
            ["f5j-christchurch-2019"] = 11,
        };

    internal static readonly IReadOnlyDictionary<string, (string Instrument, string TapeFileName)> FallbackLandingTapes =
        new Dictionary<string, (string Instrument, string TapeFileName)>
        {
            ["f5j-christchurch-2019"] = ("nz-f3j-side", "tape-nz-f3j-side"),
        };

    internal static readonly IReadOnlySet<string> FallbackSkipParityRoundBinds =
        new HashSet<string> { "f3j-international" };

    // ---------------------------------------------------------------- shapes
    private sealed record RegistryDocument(int SchemaVersion, List<RegistryEntry> Entries);

    private sealed record RegistryEntry(
        string Slug,
        string Status,
        List<RegistryMode>? Modes,
        RegistryReplay? Replay);

    private sealed record RegistryMode(string Mode, string? Seed);

    private sealed record RegistryReplay(
        List<RegistryRoundBind>? RoundParameterBinds = null,
        RegistrySyntheticSlots? SyntheticSlots = null,
        RegistryParallelRun? ParallelRun = null);

    private sealed record RegistryRoundBind(string Parameter, int RoundNo, decimal Value);

    private sealed record RegistrySyntheticSlots(
        List<RegistrySlot>? PrescriptionOnly = null,
        List<RegistrySlot>? FlightLess = null);

    private sealed record RegistrySlot(int RoundNo, int GroupNo, long PilotNo);

    private sealed record RegistryParallelRun(
        RegistryScoredWindow? ScoredWindowAssertion = null,
        RegistryLandingInstrument? LandingInstrument = null,
        RegistrySkipBinds? SkipParityRoundBinds = null);

    private sealed record RegistryScoredWindow(int Rounds);

    private sealed record RegistryLandingInstrument(string Instrument, string TapeFile);

    private sealed record RegistrySkipBinds(bool Value);

    private sealed record ResolvedTables(
        IReadOnlyDictionary<string, IReadOnlyList<(string Parameter, int RoundNo, decimal Value)>> RoundBinds,
        IReadOnlyDictionary<string, IReadOnlyList<(int RoundNo, int GroupNo, long PilotNo)>> PrescriptionOnly,
        IReadOnlyDictionary<string, IReadOnlyList<(int RoundNo, int GroupNo, long PilotNo)>> FlightLess,
        IReadOnlyDictionary<string, int> ScoredWindows,
        IReadOnlyDictionary<string, (string Instrument, string TapeFileName)> LandingTapes,
        IReadOnlySet<string> SkipParityBinds);

    private static readonly Lazy<ResolvedTables> Tables = new(Load, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>True when corpus-registry.json was present and governs.</summary>
    public static bool RegistryGoverns => WasRegistryPresent.Value;
    private static readonly Lazy<bool> WasRegistryPresent = new(
        () => File.Exists(FixtureLoader.CorpusRegistryPath()),
        System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// GS 04 Step 3 — the registry's fixture identity/status/mode manifest:
    /// one entry per registry slug, in registry order. Schema v1, no values —
    /// modes name the executable comparisons (parity scenarios, parallel-run
    /// seed pairs); class arithmetic and expected values stay in their own
    /// files. Throws naming the slug and field when the registry is present
    /// but malformed; throws when the registry file is absent (callers that
    /// tolerate pre-registry checkouts check <see cref="RegistryGoverns"/>
    /// first). No class-specific branches: entries are data, never switched
    /// on by class.
    /// </summary>
    public static IReadOnlyList<CorpusManifestEntry> ManifestEntries() => Manifest.Value;

    private static readonly Lazy<IReadOnlyList<CorpusManifestEntry>> Manifest = new(
        LoadManifest, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    private static IReadOnlyList<CorpusManifestEntry> LoadManifest()
    {
        var path = FixtureLoader.CorpusRegistryPath();

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Corpus registry '{path}' is absent — ManifestEntries requires it; "
                + "check RegistryGoverns first when a pre-registry fallback applies.");
        }

        var document = JsonSerializer.Deserialize<RegistryDocument>(File.ReadAllText(path), RegistryJson)
            ?? throw new InvalidOperationException($"Corpus registry '{path}' deserialised to null.");

        if (document.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"Corpus registry '{path}' declares schemaVersion {document.SchemaVersion}; this harness reads 1.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<CorpusManifestEntry>();

        foreach (var entry in document.Entries ?? [])
        {
            if (string.IsNullOrWhiteSpace(entry.Slug))
            {
                throw new InvalidOperationException($"Corpus registry '{path}' holds an entry with no slug.");
            }

            if (!seen.Add(entry.Slug))
            {
                throw new InvalidOperationException(
                    $"Corpus registry '{path}' declares slug '{entry.Slug}' twice (field entries.slug).");
            }

            if (entry.Status is not ("active" or "skipped"))
            {
                throw new InvalidOperationException(
                    $"Corpus registry '{path}' entry '{entry.Slug}' declares status '{entry.Status}' "
                    + "(field status; expected 'active' or 'skipped').");
            }

            var modes = (entry.Modes ?? [])
                .Select(m => new CorpusManifestMode(
                    m.Mode ?? throw new InvalidOperationException(
                        $"Corpus registry '{path}' entry '{entry.Slug}' declares a mode with no name (field modes.mode)."),
                    m.Seed))
                .ToList();

            if (modes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Corpus registry '{path}' entry '{entry.Slug}' declares no modes (field modes).");
            }

            entries.Add(new CorpusManifestEntry(entry.Slug, entry.Status, modes));
        }

        return entries;
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<(string Parameter, int RoundNo, decimal Value)>>
        RoundParameterBinds() => Tables.Value.RoundBinds;

    public static IReadOnlyDictionary<string, IReadOnlyList<(int RoundNo, int GroupNo, long PilotNo)>>
        PrescriptionOnlySlots() => Tables.Value.PrescriptionOnly;

    public static IReadOnlyDictionary<string, IReadOnlyList<(int RoundNo, int GroupNo, long PilotNo)>>
        FlightLessSlots() => Tables.Value.FlightLess;

    public static IReadOnlyDictionary<string, int> ScoredWindowRounds() => Tables.Value.ScoredWindows;

    public static IReadOnlyDictionary<string, (string Instrument, string TapeFileName)>
        LandingTapes() => Tables.Value.LandingTapes;

    public static IReadOnlySet<string> SkipParityRoundBinds() => Tables.Value.SkipParityBinds;

    private static ResolvedTables Load()
    {
        var path = FixtureLoader.CorpusRegistryPath();

        if (!File.Exists(path))
        {
            return Fallbacks();
        }

        var document = JsonSerializer.Deserialize<RegistryDocument>(File.ReadAllText(path), RegistryJson)
            ?? throw new InvalidOperationException($"Corpus registry '{path}' deserialised to null.");

        if (document.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"Corpus registry '{path}' declares schemaVersion {document.SchemaVersion}; this harness reads 1.");
        }

        var roundBinds = new Dictionary<string, IReadOnlyList<(string, int, decimal)>>();
        var prescriptionOnly = new Dictionary<string, IReadOnlyList<(int, int, long)>>();
        var flightLess = new Dictionary<string, IReadOnlyList<(int, int, long)>>();
        var scoredWindows = new Dictionary<string, int>();
        var landingTapes = new Dictionary<string, (string, string)>();
        var skipBinds = new HashSet<string>();

        foreach (var entry in document.Entries)
        {
            var replay = entry.Replay ?? new RegistryReplay();

            if (replay.RoundParameterBinds is { Count: > 0 })
            {
                roundBinds[entry.Slug] = replay.RoundParameterBinds
                    .Select(b => (b.Parameter, b.RoundNo, b.Value))
                    .ToList();
            }

            if (replay.SyntheticSlots?.PrescriptionOnly is { Count: > 0 })
            {
                prescriptionOnly[entry.Slug] = replay.SyntheticSlots.PrescriptionOnly
                    .Select(s => (s.RoundNo, s.GroupNo, s.PilotNo))
                    .ToList();
            }

            if (replay.SyntheticSlots?.FlightLess is { Count: > 0 })
            {
                flightLess[entry.Slug] = replay.SyntheticSlots.FlightLess
                    .Select(s => (s.RoundNo, s.GroupNo, s.PilotNo))
                    .ToList();
            }

            if (replay.ParallelRun?.ScoredWindowAssertion is { } window)
            {
                scoredWindows[entry.Slug] = window.Rounds;
            }

            if (replay.ParallelRun?.LandingInstrument is { } tape)
            {
                landingTapes[entry.Slug] = (tape.Instrument, tape.TapeFile);
            }

            if (replay.ParallelRun?.SkipParityRoundBinds is { Value: true })
            {
                skipBinds.Add(entry.Slug);
            }
        }

        var resolved = new ResolvedTables(
            roundBinds, prescriptionOnly, flightLess, scoredWindows, landingTapes, skipBinds);

        VerifyAgainstFallbacks(resolved, path);

        return resolved;
    }

    private static ResolvedTables Fallbacks() => new(
        FallbackRoundParameterBinds,
        FallbackPrescriptionOnlySlots,
        FallbackFlightLessSlots,
        FallbackScoredWindowRounds,
        FallbackLandingTapes,
        FallbackSkipParityRoundBinds);

    /// <summary>
    /// The verbatim-migration proof: every fallback declaration must read back
    /// identical from the registry, and the registry must carry nothing the
    /// fallbacks do not know. A mismatch names the slug and field.
    /// </summary>
    private static void VerifyAgainstFallbacks(ResolvedTables resolved, string path)
    {
        RequireEqual(
            FallbackRoundParameterBinds, resolved.RoundBinds,
            (list) => string.Join(",", list.Select(b => $"{b.Parameter}@{b.RoundNo}={b.Value}")),
            "roundParameterBinds", path);
        RequireEqual(
            FallbackPrescriptionOnlySlots, resolved.PrescriptionOnly,
            (list) => string.Join(",", list.Select(s => $"{s.RoundNo}/{s.GroupNo}/{s.PilotNo}")),
            "syntheticSlots.prescriptionOnly", path);
        RequireEqual(
            FallbackFlightLessSlots, resolved.FlightLess,
            (list) => string.Join(",", list.Select(s => $"{s.RoundNo}/{s.GroupNo}/{s.PilotNo}")),
            "syntheticSlots.flightLess", path);
        RequireEqual(
            FallbackScoredWindowRounds, resolved.ScoredWindows,
            (value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "parallelRun.scoredWindowAssertion", path);
        RequireEqual(
            FallbackLandingTapes, resolved.LandingTapes,
            (value) => $"{value.Instrument}/{value.TapeFileName}",
            "parallelRun.landingInstrument", path);

        var fallbackSkips = FallbackSkipParityRoundBinds.OrderBy(s => s).ToList();
        var resolvedSkips = resolved.SkipParityBinds.OrderBy(s => s).ToList();

        if (!fallbackSkips.SequenceEqual(resolvedSkips))
        {
            throw new InvalidOperationException(
                $"Corpus registry '{path}' field parallelRun.skipParityRoundBinds "
                + $"is [{string.Join(",", resolvedSkips)}] but the migrated code tables declare "
                + $"[{string.Join(",", fallbackSkips)}] — update corpus-registry.json and the fallback together.");
        }
    }

    private static void RequireEqual<T>(
        IReadOnlyDictionary<string, T> expected,
        IReadOnlyDictionary<string, T> actual,
        Func<T, string> render,
        string field,
        string path)
    {
        var problems = expected.Keys.Union(actual.Keys)
            .Where(slug => !expected.TryGetValue(slug, out var want)
                || !actual.TryGetValue(slug, out var got)
                || render(want) != render(got))
            .OrderBy(slug => slug)
            .Select(slug =>
            {
                var want = expected.TryGetValue(slug, out var w) ? render(w) : "<absent>";
                var got = actual.TryGetValue(slug, out var g) ? render(g) : "<absent>";
                return $"{slug}: code=[{want}] registry=[{got}]";
            })
            .ToList();

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Corpus registry '{path}' field {field} diverges from the migrated code tables: "
                + string.Join("; ", problems));
        }
    }
}
