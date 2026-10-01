// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-1 — the
// deserialised shapes of a GliderScore fixture's JSON files. Only the columns
// the harness reads are modelled; System.Text.Json ignores the rest, which is
// the right default for a corpus whose schema block is the verbatim GS table
// (tests/GliderscoreFixtures/index.md rule 5) and whose unused columns (Helper,
// ModelID, …) carry nothing the replay needs.
//
// f5k-fixture-from-server-db.md WI-3 brings the first exception to that rule:
// Scores.Flight1..4 (below) — GS's structured per-flight detail strings, which
// the F5K capture map decodes into multi-flight captures.
//
// These DTOs deliberately use their OWN JsonSerializerOptions (see
// FixtureLoader.Json), never ClassDefinitionIngestion.Options: the fixture
// files are PascalCase/camelCase-mixed GS exports, not Soarscore wire payloads.
// The class definition inside a fixture is the one place the ingestion options
// ARE used — FixtureLoader deserialises it with those, so what gets posted to
// /publish-class-definition is exactly what the Api would bind.

using System.Text.Json;
using System.Text.Json.Serialization;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

// WI-4 widens this file with the two shapes f3k-sample-comp needs: the
// schedule tables (per-round task catalogue) and the optional F3K family row.
// Both are nullable — the duration-family fixtures' competition.json carries
// neither, and System.Text.Json leaves an absent property at its default.

public sealed record CompetitionFile(
    CompetitionIdentity Identity,
    CompetitionScoring Scoring,
    FamilyRowsTable FamilyRows,
    ScheduleTablesTable? ScheduleTables = null,
    TriageTable? Triage = null);

/// <summary>
/// teams-mvp.md WI-9 — the triage block's team switches, the extraction's
/// verbatim record of the GS Comps row's UseTeams / UseTeamProtection /
/// NbrForTeamScore. Only these three are read (decision 8's mapping inputs);
/// the series/prelim/justification siblings carry nothing the replay needs.
/// Nullable as ever: an absent switch never fired, and deserialisation must
/// not falsify committed provenance to load (CompetitionScoring precedent).
/// </summary>
public sealed record TriageTable(bool? UseTeams, bool? UseTeamProtection, int? NbrForTeamScore);

public sealed record FamilyRowsTable(DurFamilyRow? Dur = null, F3KFamilyRow? F3K = null);

/// <summary>
/// The F3K family row — draw/timing state only, no scoring knobs (provenance):
/// the harness reads nothing from it today; its presence is what marks a
/// fixture as F3K-family alongside Identity.GsCompClass.
/// </summary>
public sealed record F3KFamilyRow(int CompNo);

/// <summary>
/// The per-round task schedule tables (competition.json scheduleTables). F3K
/// fixtures carry F3KTaskByRound; F5K fixtures (f5k-fixture-from-server-db.md
/// WI-3) carry F5KTaskandRefHeightByRound — the per-round task AND per-round
/// NLH in one row. F5KDataByRound (task descriptions, max flights/times) stays
/// loader-invisible: curation metadata the replay never reads.
/// </summary>
public sealed record ScheduleTablesTable(
    F3KTaskByRoundTable? F3KTaskByRound = null,
    F5KTaskAndRefHeightByRoundTable? F5KTaskAndRefHeightByRound = null);

public sealed record F3KTaskByRoundTable(F3KTaskRow[] Rows);

/// <summary>One F3KTaskByRound row: round → GS task code ("G", "A(1)", …).</summary>
public sealed record F3KTaskRow(int RoundNo, string Task);

public sealed record F5KTaskAndRefHeightByRoundTable(F5KTaskAndRefHeightRow[] Rows);

/// <summary>
/// One F5KTaskandRefHeightByRound row: round → GS task code ("A"–"E") and that
/// round's nominal launch height in metres — the origin of every launch band
/// that round. (Server-table name has the lowercase "and"; the JSON options are
/// case-insensitive, so the C# name need not reproduce it.)
/// </summary>
public sealed record F5KTaskAndRefHeightRow(int RoundNo, string Task, int RefHeight);

public sealed record CompetitionIdentity(
    int CompNo,
    string CompName,
    string GsCompClass,
    string CompDate);

// nz-fixture-replay-scenarios.md — the five NZ competition.json files carry
// the verbatim stored state for these knobs: GroupScoreOption and
// GroupScoreDecimals are null in ALL FIVE, RoundOrTruncate null in three
// (comps 135/121/17). Nothing in the harness reads them (WI-2 sentinel
// stop-and-triage finding), so the properties widen to int? — deserialisation
// must not falsify committed provenance to load.
public sealed record CompetitionScoring(
    int? GroupScoreOption,
    int? GroupScoreDecimals,
    int? RoundOrTruncate);

/// <summary>The Dur family row — the duration-curve parameters grain 1 needs.</summary>
public sealed record DurFamilyRow(
    decimal DurTargetTime,
    decimal DurPointsPerSecond,
    int DurNumberOfTimekeepers,
    int DurLndg,
    int DurFlightPenalty)
{
    /// <summary>Convenience accessor — competition.json nests family rows under "familyRows". Null when absent (F3K fixtures carry no Dur row).</summary>
    public static DurFamilyRow? Of(CompetitionFile competition) => competition.FamilyRows.Dur;
}

public sealed record EntriesFile(CompPilotsTable CompPilots, PilotsTable Pilots);

public sealed record CompPilotsTable(CompPilotRow[] Rows);

/// <summary>
/// Only PilotNo mattered until teams-mvp.md WI-9: names come from the pilots
/// table by join, and the row's two team columns — the GS team number (0 is
/// GS's own unassigned sentinel) and the per-member OmitFromTeamScore switch —
/// were deliberately ignored. Decision 8's mapping reads both now. Nullable
/// per the CompetitionScoring precedent: a missing column deserialises to
/// null rather than a silently-invented default.
/// </summary>
public sealed record CompPilotRow(int PilotNo, int? Team, bool? OmitFromTeamScore);

public sealed record PilotsTable(PilotRow[] Rows);

public sealed record PilotRow(int PilotNo, string FirstName, string LastName);

public sealed record ScoresRawFile(ScoresRow[] Rows);

/// <summary>
/// One persisted Scores row. Decimals, not doubles: D6 compares decimals
/// exactly, and every value this corpus carries in these columns reprs clean
/// at its written precision (arithmetic story, Precision &amp; storage §6).
/// <para>
/// f5k-fixture-from-server-db.md WI-3 — the four per-flight detail strings the
/// server-path fixtures carry (GS's structured Flight1..4, "" on never-flown
/// rows). Optional with null defaults so every earlier fixture and the
/// driver's synthetic-row constructors keep compiling; the duration/F3K
/// capture maps never read them. Data1..5 (GS's own per-flight point slots)
/// stay loader-invisible: curation-verified duplicates of what the strings'
/// FPT fields hold, and the harness recomputes rather than reads them.
/// </para>
/// <para>
/// f5j-christchurch-parallel-run-witness.md WI-2 — the GS Updated flag
/// ("True"/"False" strings in the export), read only by the parallel-run
/// scored-window assertion (MAX(RoundNo where Updated=='True') must equal the
/// declared window). Optional with a null default so every fixture without the
/// column and every synthetic row keeps deserialising exactly as before.
/// </para>
/// </summary>
public sealed record ScoresRow(
    int TaskNo,
    int RoundNo,
    int GroupNo,
    int ReFlightNo,
    long PilotNo,
    int SeqNo,
    decimal Laps,
    decimal Time1Mins,
    decimal Time1Secs,
    decimal Time2Mins,
    decimal Time2Secs,
    decimal FlightScoreDeduction,
    decimal Landing,
    int Penalty,
    long OriginalRoundNo,
    string? Flight1 = null,
    string? Flight2 = null,
    string? Flight3 = null,
    string? Flight4 = null,
    string? Updated = null);

public sealed record ExpectedScoresFile(Dictionary<string, ExpectedCell> Scores);

/// <summary>Keyed "{TaskNo}/{RoundNo}/{GroupNo}/{ReFlightNo}/{PilotNo}" per keyFormat.</summary>
public sealed record ExpectedCell(decimal RawScore, decimal NormalisedScore);

public sealed record ExpectedResultFile(
    ExpectedRank[] Ranks,
    // gs_01_f5k-snapshot-result-parity.md — the snapshot/result-oracle contract.
    // All optional and null-tolerant: fixtures without a snapshot oracle load
    // exactly as before and the comparator runs its place-only ranking grain.
    // Totals pins the final/progressive totals the snapshot declares per pilot;
    // UnrankedZeroOnly names registered pilots the oracle deliberately omits
    // (never-flew, zero-only) with their expected zero total; ScoredWindow is
    // the source window the replay prescribes entries/completions for and the
    // comparator covers cells for; Lifecycle names how the comparison reads
    // the oracle (a legitimately finalised snapshot, never invented
    // completion); ExcludedRounds discloses the archived rounds no comparison
    // is claimed for.
    //
    // gs_02_complete-result-oracles.md generalises the contract across the
    // corpus: PreDropTotals pins the pre-drop (pre-penalty) aggregate per
    // pilot, Penalties pins the aggregate penalty deduction per pilot
    // (explicit zero where none — omission never means zero), Discards pins
    // the discarded scoring units (round vs task, the fixture's own drop
    // dimension) with their identities and values per pilot, and
    // FieldAvailability declares every result field available, unavailable or
    // inapplicable with a reason — the comparator asserts exactly the
    // available fields, automatically.
    IReadOnlyList<ExpectedPilotTotal>? Totals = null,
    IReadOnlyList<ExpectedUnrankedPilot>? UnrankedZeroOnly = null,
    ExpectedScoredWindow? ScoredWindow = null,
    string? Lifecycle = null,
    IReadOnlyList<int>? ExcludedRounds = null,
    IReadOnlyList<ExpectedPreDropTotal>? PreDropTotals = null,
    IReadOnlyList<ExpectedPilotPenalty>? Penalties = null,
    IReadOnlyList<ExpectedPilotDiscards>? Discards = null,
    IReadOnlyList<ResultFieldAvailability>? FieldAvailability = null);

/// <summary>gs_01 — one pilot's declared final/progressive total, exact-decimal.</summary>
public sealed record ExpectedPilotTotal(long PilotNo, decimal Total);

/// <summary>gs_01 — a registered pilot the oracle deliberately leaves unranked:
/// never flew, zero-only, expected total zero. Explicit so an extra ranked
/// competitor still fails while the witnessed absence does not.</summary>
public sealed record ExpectedUnrankedPilot(long PilotNo, decimal Total);

/// <summary>gs_01 — the source window, inclusive fixture RoundNos.</summary>
public sealed record ExpectedScoredWindow(int FirstRound, int LastRound);

/// <summary>
/// gs_02 — one pilot's pre-drop (pre-penalty) aggregate: the sum of the
/// pilot's best-per-original-round normalised cells as the report rollup
/// states it, before drops and before aggregate penalties. Exact-decimal.
/// </summary>
public sealed record ExpectedPreDropTotal(long PilotNo, decimal Total);

/// <summary>
/// gs_02 — one pilot's aggregate penalty deduction: the total GS subtracts
/// after summing the round cells (always explicit, even when zero — omission
/// never means zero). Exact-decimal, non-negative.
/// </summary>
public sealed record ExpectedPilotPenalty(long PilotNo, decimal Deduction);

/// <summary>
/// gs_02 — one pilot's discarded scoring units: the fixture's own drop
/// dimension as the unit ("round" for a ByRound policy, "task" for a ByTask
/// policy — the source distinction is preserved, never normalised away),
/// with the discarded fixture RoundNos and their values in parallel arrays
/// (both ascending by round; both empty for a proven empty discard set).
/// A discard set whose identity the source does not evidence is never
/// populated here — the field is declared unavailable instead.
/// </summary>
public sealed record ExpectedPilotDiscards(
    long PilotNo,
    string Unit,
    IReadOnlyList<int> DroppedRounds,
    IReadOnlyList<decimal> DroppedValues);

/// <summary>
/// gs_02 — one result field's availability declaration. Field names the
/// "total", "preDropTotal", "penaltyDeduction", "discards", "places" or
/// "population" result field; Status is "available" (asserted automatically),
/// "unavailable" (the source carries no evidence — never asserted, never
/// treated as zero) or "inapplicable" (the concept does not apply to this
/// fixture); Reason records why in the fixture's own evidential terms.
/// </summary>
public sealed record ResultFieldAvailability(string Field, string Status, string Reason);

/// <summary>Rank strings are "n" or "=n" (GS displayed rank, HiddenRanking aside).</summary>
public sealed record ExpectedRank(long PilotNo, string Rank);

/// <summary>
/// grow-corpus-team-parity-fixtures.md WI-1C/WI-1D — the GS team-ladder
/// oracle (expected-teams.json): the reconstructed GliderScore team standings
/// transcribed over the oracle-verified individual result. OPTIONAL as a
/// fixture file — only team-bearing overlap fixtures carry one; whether an
/// overlap fixture HAS its oracle is the comparator's guard, never the
/// loader's business. Ranks are GS's display strings ("n" or "=n");
/// TeamScore is exact-decimal; CountedPilots are the retained member PilotNos
/// in GS trim order (the ladder grain compares them as a set, never as an
/// order — the trim order is an artefact of GS's Team, Score DESC view).
/// </summary>
public sealed record ExpectedTeamsFile(
    string Source,
    string? VerifiedAgainst,
    string KeyFormat,
    IReadOnlyList<string> Notes,
    IReadOnlyList<TeamStandingOracle> Standings);

/// <summary>One GS team-ladder standing, keyed by GS team number (keyFormat).</summary>
public sealed record TeamStandingOracle(
    int Team,
    string Rank,
    decimal TeamScore,
    IReadOnlyList<long> CountedPilots);

/// <summary>
/// One accepted divergence. The ledger starts EMPTY and an entry lands only
/// after human triage (D6); pilotNo-or-"*" arrives as either a number or a
/// string, hence the raw element. Round/group are null for the ranking grain.
/// <para>
/// gs-ledger-modes.md WI-1 — the optional CI disposition: "permanent" (a
/// decided law — deferred-decisions.md R1/T1 — or a structural fact GS rows a
/// replay can never produce) is reported but never fails strict mode;
/// anything else is "pending" (the default when absent) and fails strict
/// mode, and must keep witnessing a live computed mismatch in every mode.
/// Null-tolerant widening precedent: ParallelRunProvenance.
/// </para>
/// <para>
/// gs_03_exact-divergence-contracts.md — the typed expectation: every entry
/// declares its <see cref="Kind"/> and is matched bidirectionally on its
/// structured fields, never on prose. "numeric" pins the observed difference
/// with <see cref="Ours"/> (SoarScore) and <see cref="Expected"/> (the GS
/// oracle) exact-decimal values — a relocated cell, an altered value/delta
/// or a different grain all fail. "excludedOracleCell" / "syntheticSlot" are
/// documentary scope (an oracle cell deliberately never replayed, an our-only
/// replay slot with no oracle counterpart) validated against the oracle and
/// the compared set rather than a numeric witness. "unsupportedComparison"
/// (the T1 team shape) discloses a comparison that does not run and is
/// validated against the fixture's declared team method, never against a
/// computed mismatch. <see cref="Evidence"/> names the evidence reference
/// (oracle note, report, rule anchor) behind the reason.
/// </para>
/// </summary>
public sealed record DivergenceEntry(
    string Grain,
    int? Round,
    int? Group,
    JsonElement? PilotNo,
    string Reason,
    string? Disposition = null,
    string? Kind = null,
    decimal? Ours = null,
    decimal? Expected = null,
    string? Evidence = null)
{
    /// <summary>True when the entry names this pilot, or "*" for all pilots.</summary>
    public bool Covers(long pilotNo) => PilotNo is { } p && (
        p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n) && n == pilotNo
        || p.ValueKind == JsonValueKind.String && p.GetString() == "*");

    /// <summary>
    /// The entry's expectation kind: numeric (an observed value difference
    /// with pinned values), excludedOracleCell, syntheticSlot or
    /// unsupportedComparison (documentary scope, validated — never matched —
    /// against the oracle and the run). Absent reads as numeric for the
    /// ledger-subtraction shape, but a numeric entry without pins is invalid
    /// (see <see cref="RequirePins"/>) — tolerance-by-omission is not an
    /// alternative. Throws on any other token: a typo must not silently
    /// change what the entry claims.
    /// </summary>
    public string KindNormalized => (Kind ?? "numeric").ToLowerInvariant() switch
    {
        "numeric" => "numeric",
        "excludedoraclecell" => "excludedOracleCell",
        "syntheticslot" => "syntheticSlot",
        "unsupportedcomparison" => "unsupportedComparison",
        var k => throw new InvalidOperationException(
            $"Ledger entry has unknown kind '{Kind}' (valid: numeric, excludedOracleCell, syntheticSlot, "
            + $"unsupportedComparison): {Reason[..Math.Min(80, Reason.Length)]}"),
    };

    /// <summary>True for a documentary entry (every kind except numeric).</summary>
    public bool IsDocumentary => KindNormalized is not "numeric";

    /// <summary>
    /// Throws unless a numeric entry pins both sides of the observed
    /// difference exact-decimal. A numeric claim without values cannot be
    /// matched bidirectionally, so it is an authoring bug, not a wildcard.
    /// </summary>
    public void RequirePins()
    {
        if (KindNormalized is not "numeric")
        {
            return;
        }

        if (Ours is null || Expected is null)
        {
            throw new InvalidOperationException(
                $"Ledger entry ({Grain} r{Round?.ToString() ?? "*"}/g{Group?.ToString() ?? "*"} "
                + $"p{PilotToken()}) claims kind 'numeric' but pins no values — a numeric difference "
                + "must pin both SoarScore (ours) and GliderScore (expected) exact-decimal values.");
        }
    }

    /// <summary>
    /// True when this entry names the computed mismatch's cell — the
    /// SubtractLedger predicate, shared with the witnessing arm
    /// (gs-ledger-modes.md). A null pilotNo covers any pilot, as always.
    /// <para>
    /// gs_03 — kind-aware and bidirectional: a numeric entry covers a
    /// mismatch only with equal pinned values on both sides (a moved cell,
    /// an altered value/delta or a different grain is not covered);
    /// documentary entries never cover a computed mismatch here — excluded
    /// and synthetic scope shape the compared universe instead (see
    /// Comparator), and an unsupported comparison by design witnesses
    /// nothing numeric.
    /// </para>
    /// </summary>
    public bool CoversGrainRoundGroupPilot(GrainMismatch mismatch) =>
        CoversMismatch(mismatch);

    /// <summary>
    /// gs_03 — the kind-aware cover predicate replacing bare
    /// identity matching. Numeric: grain, round/group scope, pilot AND both
    /// pinned values equal. Documentary: never (scope is validated, not
    /// subtracted).
    /// </summary>
    public bool CoversMismatch(GrainMismatch mismatch)
    {
        if (KindNormalized is not "numeric")
        {
            return false;
        }

        return Grain.Equals(mismatch.Grain, StringComparison.OrdinalIgnoreCase)
            && (Round is null || Round == mismatch.RoundNo)
            && (Group is null || Group == mismatch.GroupNo)
            && (PilotNo is null || Covers(mismatch.PilotNo))
            && Ours == mismatch.Ours
            && Expected == mismatch.Expected;
    }

    /// <summary>The pilot token as written ("13" or "*"), for report lines.</summary>
    public string PilotToken() => PilotNo is { } pilot && pilot.ValueKind == JsonValueKind.Number
        ? pilot.TryGetInt64(out var n) ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : pilot.GetRawText()
        : PilotNo?.GetString() ?? "(none)";

    /// <summary>True for a permanent-by-design entry (never fails strict mode);
    /// throws on any token other than pending/permanent — a typo must not
    /// silently demote an entry to pending.</summary>
    public bool Permanent => (Disposition ?? "pending").ToLowerInvariant() switch
    {
        "pending" => false,
        "permanent" => true,
        var d => throw new InvalidOperationException(
            $"Ledger entry has unknown disposition '{d}' (valid: pending, permanent): {Reason[..Math.Min(80, Reason.Length)]}"),
    };
}

/// <summary>One loaded fixture — everything the replay and comparison need.</summary>
public sealed record GliderscoreFixture(
    string Slug,
    string Directory,
    CompetitionFile Competition,
    EntriesFile Entries,
    ScoresRawFile ScoresRaw,
    ExpectedScoresFile ExpectedScores,
    ExpectedResultFile ExpectedResult,
    IReadOnlyList<DivergenceEntry> Divergences,
    ClassDefinition Definition,
    // grow-corpus-team-parity-fixtures.md WI-1D — the optional GS team-ladder
    // oracle; null when the fixture carries no expected-teams.json.
    ExpectedTeamsFile? ExpectedTeams = null);
