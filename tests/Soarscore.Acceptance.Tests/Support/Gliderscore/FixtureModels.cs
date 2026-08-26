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
    ScheduleTablesTable? ScheduleTables = null);

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

public sealed record CompetitionScoring(
    int GroupScoreOption,
    int GroupScoreDecimals,
    int RoundOrTruncate);

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

/// <summary>Only PilotNo matters: names come from the pilots table by join.</summary>
public sealed record CompPilotRow(int PilotNo);

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
    ClassDefinition Definition);
