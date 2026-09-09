// WI-2 item 7 of kanban/in-progress/f5j-christchurch-parallel-run-witness.md —
// fixture example tests over the parent story's landed contract
// (tape-points-landing-seeds.md WI-0–WI-5: POST /declare-instruments,
// CaptureMeasurement's optional Instrument, TapeComposition.Compose).
//
// These are integration examples, not a mapping table: no GS scheme is
// re-transcribed into harness code. The scheme-11 award expectations below are
// the story's own verified decision-2 table (GS scheme 11 is 5.5.11.12 h
// pre-composed on the NZ F3J-side tape — verified row for row by the parent's
// TapeCompositionTests.F3J_side_tape_reproduces_F5J_enter_landing_row_for_row),
// asserted here per recorded mark through the real pipeline: declare the tape,
// submit the reading verbatim, read the composed award off the group view.
//
// What each fact pins:
//   1. every distinct recorded landing value is a mark on tape-nz-f3j-side
//      (each 91–100 individually, plus the off-tape 0);
//   2. the submitted measurement is the reading verbatim with its scale, and
//      the composed award equals GS's scheme-11 cell — for every recorded
//      mark, through declare → capture → score;
//   3. no double conversion (one landingDistance capture per entry) and the
//      retained eligibility gates (touch forfeits the bonus, the flight stands);
//   4. loud rejections: a reading absent from the declared tape, an undeclared
//      instrument, and a wrongly declared tape.
// Unchanged parity numbers and the inert-for-ales widenings are pinned by the
// full suite staying green, not by a fact here.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Acceptance.Tests.Support.Gliderscore;
using Xunit;

namespace Soarscore.Acceptance.Tests;

public sealed class F5JChristchurchTapeReadingExamplesTests
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private const string FixtureSlug = "f5j-christchurch-2019";
    private const string Instrument = "nz-f3j-side";

    /// <summary>
    /// The story's decision-2 table: GS scheme 11 IS 5.5.11.12 h composed on
    /// the NZ F3J-side tape, so each recorded reading's composed award is this
    /// cell. Cited, not transcribed from ladder.py into harness code — these
    /// are the verification targets the parent's composition must hit.
    /// </summary>
    private static readonly IReadOnlyDictionary<decimal, decimal> Scheme11AwardByReading =
        new Dictionary<decimal, decimal>
        {
            [0m] = 0m,
            [60m] = 10m, [65m] = 15m, [70m] = 20m, [75m] = 25m, [80m] = 30m,
            [85m] = 35m, [90m] = 40m,
            [91m] = 45m, [92m] = 45m, [93m] = 45m, [94m] = 45m, [95m] = 45m,
            [96m] = 50m, [97m] = 50m, [98m] = 50m, [99m] = 50m, [100m] = 50m,
        };

    private static ReadingScale NzF3JSideScale() =>
        TapeCorpus.All.First(t => t.FileName == "tape-nz-f3j-side").Tape.ToReadingScale();

    [Fact]
    public void EveryRecordedLandingValueIsAMarkOnTheDeclaredTape()
    {
        // WI-0 verified this at planning; the fact pins it so a fixture-data
        // change that breaks the declared-scale run fails HERE, naming the
        // value, rather than as a mysterious raw-grain mismatch in WI-3.
        var fixture = FixtureLoader.Load(FixtureSlug);
        var tape = NzF3JSideScale();

        var recorded = fixture.ScoresRaw.Rows
            .Where(r => r.Updated == "True")
            .Select(r => r.Landing)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        recorded.Should().BeEquivalentTo(
            Scheme11AwardByReading.Keys,
            "the witness pair's recorded universe is exactly the 18 values the story triaged "
            + "(marks 60–100 plus the off-tape 0); a new value is a re-triage, never a silent pass.");

        foreach (var reading in recorded)
        {
            tape.ContainsReading(reading).Should().BeTrue(
                $"recorded landing {reading} must be a mark on tape-nz-f3j-side (or its off-tape reading) — "
                + "the fixture declares that tape and submits readings verbatim.");
        }

        tape.OffScaleReading.Should().Be(0m, "the off-tape reading is 0 — the 24 zero rows are readings, not distances.");
    }

    [Fact]
    public async Task EachRecordedMarkScoresItsScheme11AwardThroughThePipeline()
    {
        // Every recorded mark, through the real pipeline: declare the tape,
        // submit the reading verbatim naming it, and read the composed award
        // off the group view (flightTime 400 + award − zero height deduction).
        // The entry stream pins the record is self-describing (reading + scale)
        // and single (no double conversion).
        await AcceptanceFixture.EnsureInitializedAsync();

        var groups = new[]
        {
            new[] { 0m, 60m, 65m, 70m, 75m, 80m },
            new[] { 85m, 90m, 91m, 92m, 93m, 94m },
            new[] { 95m, 96m, 97m, 98m, 99m, 100m },
        };

        foreach (var readings in groups)
        {
            var (flown, view) = await FlyReadingsAsync(readings);

            foreach (var (reading, entryId, competitor) in flown)
            {
                var expected = 400m + Scheme11AwardByReading[reading];

                view.Results.Should().ContainSingle(r => r.CompetitorRef == competitor)
                    .Which.PreNormalisationScore.Should().Be(expected,
                        $"reading {reading} composes to GS scheme-11 award {Scheme11AwardByReading[reading]} "
                        + "over the fixed 400 s flight time with no height deduction.");

                var entry = await EntryReader.LoadAsync(
                    AcceptanceFixture.EventStore, entryId, TestContext.Current.CancellationToken);
                entry.Flights.Should().ContainSingle();
                var measurement = entry.Flights[0].Measurements
                    .Should().ContainSingle(m => m.Metric == "landingDistance").Subject;
                measurement.Value.Number.Should().Be(reading,
                    "the submitted measurement is the reading verbatim — no decoding, mapping or conversion.");
                measurement.EffectiveInstrument.Should().Be(Instrument,
                    "the reading is held with its scale for re-scoring.");
            }
        }
    }

    [Fact]
    public async Task TouchRetainsFlightButForfeitsLandingBonus()
    {
        // The retained eligibility gates (story decision 2: the canonical
        // landing eligibility gates still apply): a touched landing keeps its
        // flight (400 s stand) but forfeits the composed bonus, while the same
        // reading untouched scores flight time plus the award.
        await AcceptanceFixture.EnsureInitializedAsync();

        var competitionId = await NewTapeCompetitionAsync("30-f5j.json");
        var group = await DrawSingleGroupAsync(competitionId, pilots: 6);

        var touched = await FlyReadingAsync(competitionId, group, 0, 98m, touched: true);
        var clean = await FlyReadingAsync(competitionId, group, 1, 98m, touched: false);
        var filler = new[] { 91m, 100m, 0m, 60m };

        for (var i = 0; i < filler.Length; i++)
        {
            await FlyReadingAsync(competitionId, group, i + 2, filler[i], touched: false);
        }

        var view = await FetchGroupViewAsync(competitionId);
        view.Results.Should().ContainSingle(r => r.CompetitorRef == touched.Competitor)
            .Which.PreNormalisationScore.Should().Be(400m,
                "touch forfeits the composed landing bonus but the 400 s flight stands.");
        view.Results.Should().ContainSingle(r => r.CompetitorRef == clean.Competitor)
            .Which.PreNormalisationScore.Should().Be(450m,
                "the same reading untouched scores flight time plus the 50 award.");
    }

    [Fact]
    public async Task ReadingAbsentFromTheDeclaredTapeIsRefused()
    {
        // A reading the declared tape has no mark for fails loudly — never
        // rounded into the scale, never a distance fallback.
        await AcceptanceFixture.EnsureInitializedAsync();

        var competitionId = await NewTapeCompetitionAsync("30-f5j.json");
        var group = await DrawSingleGroupAsync(competitionId, pilots: 6);
        var entryId = await OpenFlightAsync(competitionId, group, 0);
        await CaptureNonLandingAsync(entryId);

        var offScale = await ApiClient.PostCommandRawAsync(
            Client, "/capture-measurement",
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(87.5m), Instrument));
        offScale.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(offScale)).Should().Be("captureMeasurement.readingNotOnScale");

        // The refused capture appends no event, so the same flight is still
        // landing-free for the second refusal: an instrument nobody declared.
        var undeclared = await ApiClient.PostCommandRawAsync(
            Client, "/capture-measurement",
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(98m), "nz-f3b-side"));
        undeclared.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(undeclared)).Should().Be("captureMeasurement.instrumentNotDeclared");
    }

    [Fact]
    public async Task WronglyDeclaredTapeIsRefusedAtDeclaration()
    {
        // Declaring the metre-marked ALES side for canonical F3J's landing
        // lookup: the (0, 1] tape band straddles F3J.10.5's 0.2 m boundaries,
        // so the declaration is refused loudly with the composition's stable
        // pass-through code — that side of the tape cannot score this class.
        await AcceptanceFixture.EnsureInitializedAsync();

        var hash = await PublishSeedAsync("50-f3j.json");
        var slug = Guid.NewGuid().ToString("N");
        var competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/create-competition",
            new CreateCompetition($"Wrong Tape {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), hash));

        var alesTape = TapeCorpus.All.First(t => t.FileName == "tape-nz-ales-m-10m").Tape;

        var refused = await ApiClient.PostCommandRawAsync(
            Client, "/declare-instruments",
            new DeclareInstruments(
                competitionId,
                [new DeclaredInstrument
                {
                    Instrument = "nz-ales-m-10m",
                    Metric = "landingDistance",
                    Scale = alesTape.ToReadingScale(),
                }],
                "cd"));

        refused.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(refused)).Should().Be("tapeComposition.straddledBand");
    }

    // ---------------------------------------------------------------- helpers

    private sealed record FlownReading(decimal Reading, EntryId Entry, CompetitorId Competitor);

    private static async Task<(IReadOnlyList<FlownReading> Readings, GroupScoreView View)> FlyReadingsAsync(
        IReadOnlyList<decimal> readings)
    {
        var competitionId = await NewTapeCompetitionAsync("30-f5j.json");
        var group = await DrawSingleGroupAsync(competitionId, readings.Count);

        var flown = new List<FlownReading>();

        for (var i = 0; i < readings.Count; i++)
        {
            var flownReading = await FlyReadingAsync(competitionId, group, i, readings[i], touched: false);
            flown.Add(new FlownReading(readings[i], flownReading.Entry, flownReading.Competitor));
        }

        return (flown, await FetchGroupViewAsync(competitionId));
    }

    private static async Task<string> PublishSeedAsync(string fileName)
    {
        var seed = SeedDefinitionLoader.Load(fileName);
        return await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(seed));
    }

    /// <summary>Canonical 30-f5j adopted, NZ F3J-side tape declared for landingDistance.</summary>
    private static async Task<CompetitionId> NewTapeCompetitionAsync(string seedFileName)
    {
        var hash = await PublishSeedAsync(seedFileName);
        var slug = Guid.NewGuid().ToString("N");
        var competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/create-competition",
            new CreateCompetition($"Tape Examples {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), hash));

        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/declare-instruments",
            new DeclareInstruments(
                competitionId,
                [new DeclaredInstrument
                {
                    Instrument = Instrument,
                    Metric = "landingDistance",
                    Scale = NzF3JSideScale(),
                }],
                "cd"));

        return competitionId;
    }

    private static async Task<Group> DrawSingleGroupAsync(CompetitionId competitionId, int pilots)
    {
        // Six pilots draw one group under F5J's MinPerGroup 6 (the parent
        // feature's proven shape); fewer would refuse the draw.
        var slug = Guid.NewGuid().ToString("N");
        var competitors = new List<CompetitorId>();

        for (var i = 0; i < pilots; i++)
        {
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person",
                new RegisterPerson($"Tape Pilot {i + 1}", new ContactDetails
                {
                    Email = $"tape-{slug}-{i}@example.com".ToLowerInvariant(),
                }, null));
            competitors.Add(await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(competitionId, personId)));
        }

        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(competitionId, 1));
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/accept-draw", new AcceptDraw(competitionId));

        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={competitionId.Value}");
        var groups = view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == 1)
            .TaskRounds.Single().Groups;
        groups.Should().ContainSingle("F5J's MinPerGroup 6 draws one group for six pilots");
        groups.Single().CompetitorRefs.Should().BeEquivalentTo(competitors);
        return groups.Single();
    }

    private static async Task<(EntryId Entry, CompetitorId Competitor)> FlyReadingAsync(
        CompetitionId competitionId, Group group, int pilotIndex, decimal reading, bool touched)
    {
        var entryId = await OpenFlightAsync(competitionId, group, pilotIndex);
        var competitor = group.CompetitorRefs[pilotIndex];

        await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(400m));
        await CaptureAsync(entryId, "startHeight", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "startHeightRecorded", MeasuredValue.Of(true));
        await CaptureAsync(entryId, "overflySeconds", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(touched));
        await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(reading), Instrument);

        return (entryId, competitor);
    }

    private static async Task<EntryId> OpenFlightAsync(CompetitionId competitionId, Group group, int pilotIndex)
    {
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry",
            new OpenEntry(competitionId, 0, 1, 1, group.Id, group.CompetitorRefs[pilotIndex]));
        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));
        return entryId;
    }

    private static async Task CaptureNonLandingAsync(EntryId entryId)
    {
        await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(400m));
        await CaptureAsync(entryId, "startHeight", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "startHeightRecorded", MeasuredValue.Of(true));
        await CaptureAsync(entryId, "overflySeconds", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(false));
    }

    private static async Task CaptureAsync(EntryId entryId, string metric, MeasuredValue value, string? instrument = null) =>
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/capture-measurement", new CaptureMeasurement(entryId, 1, metric, value, instrument));

    private static async Task<GroupScoreView> FetchGroupViewAsync(CompetitionId competitionId)
    {
        var views = await ApiClient.GetAsync<List<GroupScoreView>>(
            Client, $"/task-round-result?competitionRef={competitionId.Value}&phaseOrdinal=0&roundOrdinal=1&taskRoundOrdinal=1");
        return views.Should().ContainSingle().Subject;
    }

    private static async Task<string> ReadProblemTitleAsync(HttpResponseMessage response)
    {
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("title").GetString()!;
    }
}
