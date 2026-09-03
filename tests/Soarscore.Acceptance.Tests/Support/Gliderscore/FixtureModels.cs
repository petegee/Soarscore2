// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-1 — the
// deserialised shapes of a GliderScore fixture's JSON files. Only the columns
// the harness reads are modelled; System.Text.Json ignores the rest, which is
// the right default for a corpus whose schema block is the verbatim GS table
// (tests/GliderscoreFixtures/index.md rule 5) and whose unused columns (Helper,
// ModelID, Flight1..4, …) carry nothing the replay needs.
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

/// <summary>The per-round task schedule tables (competition.json scheduleTables).</summary>
public sealed record ScheduleTablesTable(F3KTaskByRoundTable? F3KTaskByRound = null);

public sealed record F3KTaskByRoundTable(F3KTaskRow[] Rows);

/// <summary>One F3KTaskByRound row: round → GS task code ("G", "A(1)", …).</summary>
public sealed record F3KTaskRow(int RoundNo, string Task);

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
    long OriginalRoundNo);

public sealed record ExpectedScoresFile(Dictionary<string, ExpectedCell> Scores);

/// <summary>Keyed "{TaskNo}/{RoundNo}/{GroupNo}/{ReFlightNo}/{PilotNo}" per keyFormat.</summary>
public sealed record ExpectedCell(decimal RawScore, decimal NormalisedScore);

public sealed record ExpectedResultFile(ExpectedRank[] Ranks);

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
/// </summary>
public sealed record DivergenceEntry(
    string Grain,
    int? Round,
    int? Group,
    JsonElement? PilotNo,
    string Reason)
{
    /// <summary>True when the entry names this pilot, or "*" for all pilots.</summary>
    public bool Covers(long pilotNo) => PilotNo is { } p && (
        p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n) && n == pilotNo
        || p.ValueKind == JsonValueKind.String && p.GetString() == "*");
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
