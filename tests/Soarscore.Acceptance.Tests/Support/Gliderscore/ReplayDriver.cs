// kanban/in-progress/gliderscore-replay-and-compare-harness.md WI-1 — the
// replay half of the steel thread. Drives one fixture through the PUBLIC
// command surface only (every write goes over AcceptanceFixture.Client via
// ApiClient, same JSON options as the Api): publish class definition, create
// competition, register pilots, prescribe the realised draw (D5), accept,
// open an Entry for every scores-raw slot (D4), capture decoded measurements,
// complete each task-round, finalise. The only non-HTTP scoring surface in
// the whole harness is Comparator's Q1 grain, not this file.
//
// Decisions implemented here, cited by their story numbers:
//   D4 — every scores-raw row gets an Entry, including all-zero placeholder
//        rows; a flight-less entry yields NoResult ⇒ cell 0, which is what puts
//        GS's placeholder zeros into the drop-candidate pool. Only non-zero
//        inputs are captured: packed-mmss times decoded Fix-style ("500.0" =
//        300 s, arithmetic story Handoff §3), Landing > 0 as landingDistance
//        metres. Laps is ignored for duration-family fixtures (trap 6).
//   D5 — draw derivation: drop re-flight rows, dedupe phantom repeats keeping
//        the highest oracle NormalisedScore per (pilot, original round), then
//        fail loudly on any partition violation. Rounds ascending RoundNo,
//        groups ascending GroupNo, members in SeqNo order (prescribe-story
//        decision 4 — list order IS the flying order).
//   Trap 5 — a fixture with two timekeepers is refused loudly rather than
//        mis-modelled as a single-timekeeper decode.

using System.Net.Http.Json;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

/// <summary>Everything the comparator needs to find its way around the replay.</summary>
public sealed record ReplayOutcome(
    CompetitionId CompetitionId,
    int PhaseOrdinal,
    string TaskCode,
    IReadOnlyDictionary<int, int> RoundOrdinalByRoundNo,
    IReadOnlyDictionary<(int RoundNo, int GroupNo), GroupId> GroupIdByRoundAndGroup,
    IReadOnlyDictionary<(int RoundNo, int GroupNo, long PilotNo), EntryId> EntryIdBySlot,
    IReadOnlyDictionary<long, CompetitorId> CompetitorByPilotNo);

public sealed class ReplayDriver(HttpClient client)
{
    private const string CdName = "Gliderscore replay harness";

    public async Task<ReplayOutcome> ReplayAsync(GliderscoreFixture fixture)
    {
        // ------------------------------------------------------------ publish
        // Fails loudly here if the authored definition does not pass adoption
        // validation — which is how WI-1 proves it does.
        var contentHash = await PostAsync<string>(
            "/publish-class-definition", new PublishClassDefinition(fixture.Definition));

        // ------------------------------------------------------------- create
        // Name/emails carry a run slug so scenarios sharing one store never
        // collide (the CapturingAScoreSteps discipline). CompDate gives the dates.
        var slug = Guid.NewGuid().ToString("N");
        var compDate = DateOnly.Parse(
            fixture.Competition.Identity.CompDate.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);

        var competitionId = await PostAsync<CompetitionId>(
            "/create-competition",
            new CreateCompetition(
                $"{fixture.Competition.Identity.CompName} ({slug})",
                "Gliderscore replay",
                compDate,
                compDate,
                contentHash));

        // ----------------------------------------------------------- register
        // D-trap 9: compPilots row order; names joined from the pilots table by
        // PilotNo; emails slug-unique per run (Person.IsPlausibleEmail forbids
        // whitespace). GS StartNo numbering is irrelevant — keys are internal ids.
        var pilotNames = fixture.Entries.Pilots.Rows.ToDictionary(p => p.PilotNo, p => $"{p.FirstName} {p.LastName}");
        var competitorByPilotNo = new Dictionary<long, CompetitorId>();

        foreach (var row in fixture.Entries.CompPilots.Rows)
        {
            var personId = await PostAsync<PersonId>(
                "/register-person",
                new RegisterPerson(
                    pilotNames.GetValueOrDefault(row.PilotNo, $"Pilot {row.PilotNo}"),
                    new ContactDetails { Email = $"gliderscore-{slug}-pilot-{row.PilotNo}@example.com".ToLowerInvariant() },
                    null));
            competitorByPilotNo[row.PilotNo] = await PostAsync<CompetitorId>(
                "/register-competitor", new RegisterCompetitor(competitionId, personId));
        }

        // -------------------------------------------------------------- draw
        var keptRows = DeriveDrawRows(fixture);

        var prescribedRounds = keptRows
            .GroupBy(r => r.RoundNo)
            .OrderBy(g => g.Key)
            .Select(roundRows =>
            {
                var groups = roundRows
                    .GroupBy(r => r.GroupNo)
                    .OrderBy(g => g.Key)
                    .Select(g => new PrescribedGroup(
                        g.OrderBy(r => r.SeqNo).Select(r => competitorByPilotNo[r.PilotNo]).ToList()))
                    .ToList();

                return new PrescribedRound(TaskRef: null, Groups: groups);
            })
            .ToList();

        await PostAsync<CompetitionId>("/prescribe-draw", new PrescribeDraw(competitionId, prescribedRounds, CdName));
        await PostAsync<CompetitionId>("/accept-draw", new AcceptDraw(competitionId));

        // --------------------------------------------- read back drawn structure
        // Ordinals are assigned by position at prescription time (Competition.
        // PrescribeDraw): Phase.Ordinal is Phases.Length at draw time — 0 for a
        // first phase — while Round/TaskRound/Group ordinals are 1-based. Read
        // them back rather than assume, and key everything the comparator needs
        // by FIXTURE coordinates (RoundNo/GroupNo/PilotNo), not engine ordinals.
        var view = await ApiClient.GetAsync<CompetitionView>(client, $"/competition?id={competitionId.Value}");
        var phase = view.Competition.Phases.Single();
        var roundsAscending = phase.Rounds.OrderBy(r => r.Ordinal).ToList();

        var roundNosAscending = keptRows.Select(r => r.RoundNo).Distinct().OrderBy(n => n).ToList();
        var roundOrdinalByRoundNo = roundNosAscending
            .Select((roundNo, index) => (roundNo, ordinal: index + 1))
            .ToDictionary(pair => pair.roundNo, pair => pair.ordinal);

        var groupIdByRoundAndGroup = new Dictionary<(int RoundNo, int GroupNo), GroupId>();

        foreach (var roundNo in roundNosAscending)
        {
            var taskRound = roundsAscending[roundOrdinalByRoundNo[roundNo] - 1].TaskRounds.Single();
            var groupNosAscending = keptRows
                .Where(r => r.RoundNo == roundNo)
                .Select(r => r.GroupNo).Distinct().OrderBy(n => n).ToList();

            for (var i = 0; i < groupNosAscending.Count; i++)
            {
                groupIdByRoundAndGroup[(roundNo, groupNosAscending[i])] =
                    taskRound.Groups.OrderBy(g => g.Ordinal).ElementAt(i).Id;
            }
        }

        var taskCode = roundsAscending[0].TaskRounds.Single().TaskRef;

        // --------------------------------------------------- D4 cell universe
        var entryIdBySlot = new Dictionary<(int RoundNo, int GroupNo, long PilotNo), EntryId>();

        foreach (var group in keptRows
            .GroupBy(r => (r.RoundNo, r.GroupNo))
            .OrderBy(g => g.Key.RoundNo).ThenBy(g => g.Key.GroupNo))
        {
            var roundOrdinal = roundOrdinalByRoundNo[group.Key.RoundNo];
            var groupId = groupIdByRoundAndGroup[(group.Key.RoundNo, group.Key.GroupNo)];

            foreach (var row in group.OrderBy(r => r.SeqNo))
            {
                var entryId = await PostAsync<EntryId>(
                    "/open-entry",
                    new OpenEntry(
                        competitionId, phase.Ordinal, roundOrdinal, 1,
                        groupId, competitorByPilotNo[row.PilotNo]));

                entryIdBySlot[(row.RoundNo, row.GroupNo, row.PilotNo)] = entryId;

                // Placeholder rows stay flight-less ⇒ NoResult ⇒ cell 0 (D4);
                // flown slots open exactly one flight and capture decoded values.
                var captures = CaptureInputs(fixture, row);

                if (captures.Count > 0)
                {
                    await PostAsync<EntryId>("/open-flight", new OpenFlight(entryId));

                    foreach (var (metric, value) in captures)
                    {
                        await PostAsync<EntryId>(
                            "/capture-measurement",
                            new CaptureMeasurement(entryId, 1, metric, MeasuredValue.Of(value)));
                    }
                }
            }

            await PostAsync<CompetitionId>(
                "/complete-task-round",
                new CompleteTaskRound(competitionId, phase.Ordinal, roundOrdinal, 1));
        }

        await PostAsync<CompetitionId>("/finalise-competition", new FinaliseCompetition(competitionId, CdName));

        return new ReplayOutcome(
            CompetitionId: competitionId,
            PhaseOrdinal: phase.Ordinal,
            TaskCode: taskCode,
            RoundOrdinalByRoundNo: roundOrdinalByRoundNo,
            GroupIdByRoundAndGroup: groupIdByRoundAndGroup,
            EntryIdBySlot: entryIdBySlot,
            CompetitorByPilotNo: competitorByPilotNo);
    }

    // ------------------------------------------------------------- D5 draw

    /// <summary>The scores-raw rows that form the realised draw, after D5's filters.</summary>
    private static List<ScoresRow> DeriveDrawRows(GliderscoreFixture fixture)
    {
        // Step 1 — re-flight rows have no base-draw prescription path yet
        // (deferred-decisions.md "Draw"); WI-6 designs that mapping.
        var rows = fixture.ScoresRaw.Rows
            .Where(r => !(r.ReFlightNo > 0 || r.OriginalRoundNo != r.RoundNo))
            .ToList();

        // Step 2 — phantom repeats (GS phantom groups, f3j-international R1/G5):
        // a pilot twice in one round keeps the row whose oracle NormalisedScore
        // is highest for that (pilot, original round) — GS's best-per-original-
        // round aggregation, which is what makes the phantom neutral.
        var kept = new List<ScoresRow>();

        foreach (var group in rows.GroupBy(r => (r.RoundNo, r.PilotNo)))
        {
            kept.Add(group.Count() == 1
                ? group.First()
                : group.OrderByDescending(row => OracleNormalisedScore(fixture, row)).First());
        }

        // Step 3 — the survivor set must partition cleanly; anything else is a
        // derivation bug and fails loudly here rather than as a score diff.
        var duplicated = kept
            .GroupBy(r => (r.RoundNo, r.PilotNo))
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicated is not null)
        {
            throw new InvalidOperationException(
                $"Fixture '{fixture.Slug}': draw derivation failed — pilot {duplicated.Key.PilotNo} still appears "
                + $"{duplicated.Count()} times in round {duplicated.Key.RoundNo} after deduplication.");
        }

        return kept;
    }

    /// <summary>
    /// Oracle lookup for D5 step 2, keyed exactly as expected-scores.json's
    /// keyFormat states: {"TaskNo"}/{"RoundNo"}/{"GroupNo"}/{"ReFlightNo"}/{"PilotNo"}.
    /// A missing oracle cell ranks lowest rather than throwing — deduplication
    /// needs an ordering, not a verdict.
    /// </summary>
    private static decimal OracleNormalisedScore(GliderscoreFixture fixture, ScoresRow row) =>
        fixture.ExpectedScores.Scores.TryGetValue(
                $"{row.TaskNo}/{row.OriginalRoundNo}/{row.GroupNo}/{row.ReFlightNo}/{row.PilotNo}",
                out var cell)
            ? cell.NormalisedScore
            : 0m;

    // ---------------------------------------------------------- D4 capture

    /// <summary>
    /// The (metric, value) pairs this row contributes, per D4's capture rule:
    /// non-zero inputs only. Duration family — Time1Mins is the packed-mmss
    /// flight time, Landing the landing-distance bucket; Laps ignored (trap 6),
    /// FlightScoreDeduction is WI-3's widening, Time2* would mean two
    /// timekeepers (trap 5) and is refused rather than averaged away.
    /// </summary>
    private static List<(string Metric, decimal Value)> CaptureInputs(GliderscoreFixture fixture, ScoresRow row)
    {
        if (DurFamilyRow.Of(fixture.Competition).DurNumberOfTimekeepers != 1)
        {
            throw new NotSupportedException(
                $"Fixture '{fixture.Slug}': durNumberOfTimekeepers = "
                + $"{DurFamilyRow.Of(fixture.Competition).DurNumberOfTimekeepers}. Two-timekeeper fixtures are not "
                + "supported by the harness yet (index.md 'Still open'; story trap 5).");
        }

        var captures = new List<(string, decimal)>();

        if (row.Time1Mins > 0m)
        {
            captures.Add(("flightTime", DecodePackedMinutesSeconds(row.Time1Mins)));
        }

        if (row.Landing > 0m)
        {
            captures.Add(("landingDistance", row.Landing));
        }

        return captures;
    }

    /// <summary>
    /// GS packed mmss.s decode with Fix-truncation toward zero
    /// (Scoring_MOD.vb GetTimeInSeconds via arithmetic story Handoff §3):
    /// seconds = Fix(v/100)·60 + (v − 100·Fix(v/100)). "500.0" → 300 s.
    /// </summary>
    private static decimal DecodePackedMinutesSeconds(decimal packed)
    {
        var minutes = Math.Truncate(packed / 100m);

        return minutes * 60m + (packed - 100m * minutes);
    }

    // ---------------------------------------------------------------- misc

    private async Task<T> PostAsync<T>(string path, object command)
    {
        using var response = await client.PostAsJsonAsync(path, command, ApiClient.Options);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Replay POST {path} returned {(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(body, ApiClient.Options)!;
    }
}
