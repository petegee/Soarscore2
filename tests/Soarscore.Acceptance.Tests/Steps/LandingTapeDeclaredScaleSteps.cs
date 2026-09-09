// WI-5 of kanban/backlog/tape-points-landing-seeds.md — BDD proof for the
// landing tape as a declared reading scale, on the unchanged canonical F3J
// (50-f3j) and F5J (30-f5j) classes. Step phrasing is unique to this feature
// (the per-feature self-contained Steps discipline
// ScoringACompetitionSteps.cs's header records); the scale snapshot is mapped
// from the WI-2 catalogue in-test (finding F-MAP-1 in
// TapeLandingScaleProofTests.cs).
//
// Scenario (i) proves the base case is the existing behaviour: no instrument
// declared, distances only, nothing changed. Scenario (ii) is the field's
// not-enough-tapes shape — one group mixing readings on the declared NZ
// F3J-side tape with tape-measure distances — through declaration, capture,
// refusal, amendment, declaration correction, eligibility correction, audit
// and completeness. Where WI-4 (composition at resolution) is absent, the
// steps pin the current raw-number scoring as F-WI4-1 gap pins, exactly like
// the Domain proof's WI4Gap_ tests.

using System.Collections.Immutable;
using System.Globalization;
using AwesomeAssertions;
using Reqnroll;
using Xunit;
using Soarscore.Acceptance.Tests.Support;
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
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class LandingTapeDeclaredScaleSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private static readonly ClassDefinition F3JDefinition = Corpus.All.Single(c => c.FileName == "50-f3j").Definition;
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    private string? _classContentHash;
    private CompetitionId _competitionId;
    private readonly List<CompetitorId> _competitors = [];
    private readonly Dictionary<int, EntryId> _entryByPilot = [];

    /// <summary>Catalogue-to-snapshot mapping, in-test (finding F-MAP-1).</summary>
    private static ReadingScale NzF3JSideScale()
    {
        var tape = TapeCorpus.All.First(t => t.FileName == "tape-nz-f3j-side").Tape;
        return new ReadingScale
        {
            Unit = tape.Unit,
            Marks = tape.Marks.Select(m => new ScaleMark(m.UpTo!.Value, m.Reading)).ToImmutableArray(),
            OffScaleReading = tape.OffTapeReading,
        };
    }

    private static ImmutableArray<DeclaredInstrument> DeclaredSet() =>
        [new DeclaredInstrument { Instrument = "nz-f3j-side", Metric = "landingDistance", Scale = NzF3JSideScale() }];

    // ------------------------------------------------------------------ Given

    [Given(@"^the canonical F3J class is published for the landing proof$")]
    public async Task GivenTheCanonicalF3JClassIsPublishedForTheLandingProof()
    {
        _classContentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(F3JDefinition));
    }

    [Given(@"^the canonical F5J class is published for the landing proof$")]
    public async Task GivenTheCanonicalF5JClassIsPublishedForTheLandingProof()
    {
        _classContentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(F5JDefinition));
    }

    [Given(@"^a landing proof competition adopting it with (\d+) registered competitors$")]
    public async Task GivenALandingProofCompetitionAdoptingItWithRegisteredCompetitors(int count)
    {
        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition($"Landing Proof {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), _classContentHash!));

        for (var i = 0; i < count; i++)
        {
            var email = $"pilot-landing-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }
    }

    [Given(@"^its preliminary phase is drawn for (\d+) rounds?$")]
    public async Task GivenItsPreliminaryPhaseIsDrawnForRounds(int rounds)
    {
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, rounds));
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/accept-draw", new AcceptDraw(_competitionId));
    }

    [Given(@"^the CD declares the NZ F3J-side tape for landingDistance$")]
    public async Task GivenTheCdDeclaresTheNzF3JSideTapeForLandingDistance()
    {
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/declare-instruments",
            new DeclareInstruments(_competitionId, DeclaredSet(), "cd"));

        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var declared = view.Competition.DeclaredInstruments;
        declared.Should().NotBeNull();
        declared!.Instruments.Should().ContainSingle().Which.Instrument.Should().Be("nz-f3j-side");
    }

    // ------------------------------------------------------------------- When

    [When(@"^each pilot flies with a 500 second flight time and these landing distances$")]
    public async Task WhenEachPilotFliesWithA500SecondFlightTimeAndTheseLandingDistances(Table table)
    {
        var group = await ResolveGroupAsync();
        group.CompetitorRefs.Should().HaveCount(6, "F3J's MinPerGroup 6 draws one group for six pilots");

        foreach (var row in table.Rows)
        {
            var pilot = int.Parse(row["pilot"], CultureInfo.InvariantCulture);
            var landing = decimal.Parse(row["landing"], CultureInfo.InvariantCulture);
            var entryId = await OpenFlightAsync(group.Id, group.CompetitorRefs[pilot - 1]);
            _entryByPilot[pilot] = entryId;

            await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(500m));
            await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(landing));
            await CaptureAsync(entryId, "overflySeconds", MeasuredValue.Of(0m));
            await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(false));
            await CaptureAsync(entryId, "restedWithin75m", MeasuredValue.Of(true));
        }
    }

    [When(@"^three pilots read their landings off the declared tape and two tape-measure theirs$")]
    public async Task WhenThreePilotsReadTheirLandingsOffTheDeclaredTapeAndTwoTapeMeasureTheirs(Table table)
    {
        var group = await ResolveGroupAsync();
        group.CompetitorRefs.Should().HaveCount(6, "F5J's MinPerGroup 6 draws one group for six pilots");

        foreach (var row in table.Rows)
        {
            var pilot = int.Parse(row["pilot"], CultureInfo.InvariantCulture);
            var landing = decimal.Parse(row["landing"], CultureInfo.InvariantCulture);
            var instrument = row["form"] == "reading" ? "nz-f3j-side" : null;
            var entryId = await OpenFlightAsync(group.Id, group.CompetitorRefs[pilot - 1]);
            _entryByPilot[pilot] = entryId;

            await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(400m));
            await CaptureAsync(entryId, "startHeight", MeasuredValue.Of(0m));
            await CaptureAsync(entryId, "startHeightRecorded", MeasuredValue.Of(true));
            await CaptureAsync(entryId, "overflySeconds", MeasuredValue.Of(0m));
            await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(false));
            await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(landing), instrument);
        }
    }

    [When(@"^the sixth pilot's entry is opened with a flight but no landing$")]
    public async Task WhenTheSixthPilotsEntryIsOpenedWithAFlightButNoLanding()
    {
        // Kept as pilot 6's entry: the refusal Then lands here, and the
        // tape-measure When captures its real landing onto the same flight
        // (a refused capture appends no event, so it stays landing-free).
        var group = await ResolveGroupAsync();
        _entryByPilot[6] = await OpenFlightAsync(group.Id, group.CompetitorRefs[5]);
    }

    [When(@"^the sixth pilot tape-measures 12\.0 metres$")]
    public async Task WhenTheSixthPilotTapeMeasures120Metres()
    {
        var entryId = _entryByPilot[6];

        await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(400m));
        await CaptureAsync(entryId, "startHeight", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "startHeightRecorded", MeasuredValue.Of(true));
        await CaptureAsync(entryId, "overflySeconds", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(false));
        await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(12.0m));
    }

    [When(@"^the scorer amends a reading and a measurement's instrument$")]
    public async Task WhenTheScorerAmendsAReadingAndAMeasurementsInstrument()
    {
        // Pilot 1's reading 98 -> 90: the instrument is retained (the handler
        // restates the effective instrument when no change is named), and the
        // composed award moves bands (50 -> 40).
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/amend-measurement",
            new AmendMeasurement(_entryByPilot[1], 1, "landingDistance", MeasuredValue.Of(90m),
                "misread the tape", "scorer"));

        // Pilot 4's distance 0.5 -> reading 98 naming the tape: the instrument
        // change is explicit.
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/amend-measurement",
            new AmendMeasurement(_entryByPilot[4], 1, "landingDistance", MeasuredValue.Of(98m),
                "tape freed up", "scorer", "nz-f3j-side", true));
    }

    [When(@"^the scorer touches down and then corrects the touch flag$")]
    public async Task WhenTheScorerTouchesDownAndThenCorrectsTheTouchFlag()
    {
        // Pilot 5's tape-measured landing loses its bonus while touched, and
        // regains it when the flag is corrected — eligibility, corrected.
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/amend-measurement",
            new AmendMeasurement(_entryByPilot[5], 1, "touchedByCompetitor", MeasuredValue.Of(true),
                "touched on landing", "scorer"));

        var touched = (await FetchGroupViewAsync()).Results.Single(r => r.CompetitorRef == _competitors[4]);
        touched.PreNormalisationScore.Should().Be(400m, "touch forfeits the bonus");

        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/amend-measurement",
            new AmendMeasurement(_entryByPilot[5], 1, "touchedByCompetitor", MeasuredValue.Of(false),
                "no touch after all", "scorer"));
    }

    [When(@"^the CD corrects the declaration record$")]
    public async Task WhenTheCdCorrectsTheDeclarationRecord()
    {
        // Same set, corrected record (a tape swapped for its identical spare):
        // the correction replaces the declaration whole and re-scores
        // retroactively by re-derivation. Correcting to a set that strands the
        // captured readings would fail loudly at resolution instead.
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/correct-instrument-declaration",
            new CorrectInstrumentDeclaration(_competitionId, DeclaredSet(), "tape swapped for identical spare", "cd2"));

        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var declared = view.Competition.DeclaredInstruments;
        declared.Should().NotBeNull();
        declared!.Instruments.Should().ContainSingle().Which.Instrument.Should().Be("nz-f3j-side");
        declared.By.Should().Be("cd2", "the correction visibly replaced the record");
    }

    // ------------------------------------------------------------------- Then

    [Then(@"^the group's pre-normalisation scores are flight time plus the rulebook landing award$")]
    public async Task ThenTheGroupsPreNormalisationScoresAreFlightTimePlusTheRulebookLandingAward()
    {
        // F3J.10.5 through the real pipeline: 0.0->100, 0.5->98, 2.5->90,
        // 5.5->75, 12.5->40, 100->0, each over the fixed 500 s flight time.
        var expected = new Dictionary<int, decimal>
        {
            [1] = 600m, [2] = 598m, [3] = 590m, [4] = 575m, [5] = 540m, [6] = 500m,
        };
        var view = (await FetchGroupViewAsync());
        foreach (var (pilot, pre) in expected)
        {
            var result = view.Results.Single(r => r.CompetitorRef == _competitors[pilot - 1]);
            result.State.Should().Be(TaskResultState.Valid);
            result.PreNormalisationScore.Should().Be(pre, $"pilot {pilot}");
            result.AwaitingCapture.Should().BeEmpty();
        }
    }

    [Then(@"^the group's normalised scores follow the class target of 1000$")]
    public async Task ThenTheGroupsNormalisedScoresFollowTheClassTargetOf1000()
    {
        // F3J.10.11 Truncate-0.1 over the 600 winner: exact decimals.
        var expected = new Dictionary<int, decimal>
        {
            [1] = 1000m, [2] = 996.6m, [3] = 983.3m, [4] = 958.3m, [5] = 900m, [6] = 833.3m,
        };
        var view = await FetchGroupViewAsync();
        view.WinnerRef.Should().Be(_competitors[0]);
        foreach (var (pilot, normalised) in expected)
            view.Results.Single(r => r.CompetitorRef == _competitors[pilot - 1]).RawScore.Should().Be(normalised);
    }

    [Then(@"^the landing recording reports no gaps$")]
    public async Task ThenTheLandingRecordingReportsNoGaps()
    {
        var recording = await ApiClient.GetAsync<TaskRoundRecordingView>(
            Client, $"/task-round-recording?competitionRef={_competitionId.Value}&phaseOrdinal=0&roundOrdinal=1&taskRoundOrdinal=1");
        var group = recording.Groups.Should().ContainSingle().Subject;
        group.MetricGaps.Should().BeEmpty("a valid reading or a valid distance satisfies the landing input");
    }

    [Then(@"^the leaderboard totals the group's normalised scores$")]
    public async Task ThenTheLeaderboardTotalsTheGroupsNormalisedScores()
    {
        // One round, and F3J drops only from eight rounds — totals are the
        // normalised group scores.
        var view = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");
        view.Scores.Should().HaveCount(6);
        var group = await FetchGroupViewAsync();
        foreach (var score in view.Scores)
        {
            score.Disqualified.Should().BeFalse();
            score.Score.Should().Be(group.Results.Single(r => r.CompetitorRef == score.CompetitorRef).RawScore);
        }
    }

    [Then(@"^an off-scale reading is refused and an undeclared instrument is refused$")]
    public async Task ThenAnOffScaleReadingIsRefusedAndAnUndeclaredInstrumentIsRefused()
    {
        // On pilot 6's still landing-free flight, with the two stable codes.
        var entryId = _entryByPilot[6];

        var offScale = await ApiClient.PostCommandRawAsync(
            Client, "/capture-measurement",
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(87.5m), "nz-f3j-side"));
        offScale.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(offScale)).Should().Be("captureMeasurement.readingNotOnScale");

        var undeclared = await ApiClient.PostCommandRawAsync(
            Client, "/capture-measurement",
            new CaptureMeasurement(entryId, 1, "landingDistance", MeasuredValue.Of(98m), "nz-f3b-side"));
        undeclared.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadProblemTitleAsync(undeclared)).Should().Be("captureMeasurement.instrumentNotDeclared");
    }

    [Then(@"^each distance landing scores flight time plus the rulebook award$")]
    public async Task ThenEachDistanceLandingScoresFlightTimePlusTheRulebookAward()
    {
        // 5.5.11.12 h through the real pipeline over the fixed 400 s flight
        // time: 0.5->50, 2.5->40, 12.0->0.
        var expected = new Dictionary<int, decimal> { [4] = 450m, [5] = 440m, [6] = 400m };
        var view = await FetchGroupViewAsync();
        foreach (var (pilot, pre) in expected)
        {
            var result = view.Results.Single(r => r.CompetitorRef == _competitors[pilot - 1]);
            result.State.Should().Be(TaskResultState.Valid);
            result.PreNormalisationScore.Should().Be(pre, $"pilot {pilot}");
        }
    }

    [Then(@"^each reading is held with its instrument for re-scoring$")]
    public async Task ThenEachReadingIsHeldWithItsInstrumentForReScoring()
    {
        // Composition live (owner decision 5): 91 -> (1.8, 2.0] -> 45,
        // 98 -> (0.4, 0.6] -> 50, off-scale 0 -> (15, inf) -> 0, each over the
        // fixed 400 s flight time. The record is self-describing: the reading
        // and its instrument are what re-scoring composes.
        var expectedPre = new Dictionary<int, decimal> { [1] = 450m, [2] = 445m, [3] = 400m };
        var expectedReading = new Dictionary<int, decimal> { [1] = 98m, [2] = 91m, [3] = 0m };
        var view = await FetchGroupViewAsync();
        foreach (var pilot in new[] { 1, 2, 3 })
        {
            view.Results.Single(r => r.CompetitorRef == _competitors[pilot - 1]).PreNormalisationScore
                .Should().Be(expectedPre[pilot], $"pilot {pilot}");

            var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryByPilot[pilot],
                TestContext.Current.CancellationToken);
            var measurement = entry.Flights.Should().ContainSingle().Subject.Measurements
                .Single(m => m.Metric == "landingDistance");
            measurement.Value.Number.Should().Be(expectedReading[pilot]);
            measurement.EffectiveInstrument.Should().Be("nz-f3j-side");
        }
    }

    [Then(@"^a reading and a tape-measure of the same landing agree$")]
    public async Task ThenAReadingAndATapeMeasureOfTheSameLandingAgree()
    {
        // Property (c) at the BDD grain: pilot 1's reading 98 denotes the
        // (0.4, 0.6] band and pilot 4 tape-measured 0.5 m inside it — the same
        // landing scores identically in both forms.
        var view = await FetchGroupViewAsync();
        var reading = view.Results.Single(r => r.CompetitorRef == _competitors[0]);
        var measured = view.Results.Single(r => r.CompetitorRef == _competitors[3]);
        reading.PreNormalisationScore.Should().Be(450m);
        measured.PreNormalisationScore.Should().Be(450m);
    }

    [Then(@"^the re-score is visible and the audit history is retained$")]
    public async Task ThenTheReScoreIsVisibleAndTheAuditHistoryIsRetained()
    {
        // Every correction re-derived through the pipeline: pilot 1's reading
        // is 90 (440), pilot 4's distance is now reading 98 on the tape
        // (450), pilot 5's touch stands corrected (440) — and pilot 4, the
        // sole 450, is the group's winner on 1000.
        var view = await FetchGroupViewAsync();
        view.Results.Should().HaveCount(6);
        view.Results.Single(r => r.CompetitorRef == _competitors[0]).PreNormalisationScore.Should().Be(440m);
        view.Results.Single(r => r.CompetitorRef == _competitors[3]).PreNormalisationScore.Should().Be(450m);
        view.Results.Single(r => r.CompetitorRef == _competitors[4]).PreNormalisationScore.Should().Be(440m);
        view.WinnerRef.Should().Be(_competitors[3]);
        view.Results.Single(r => r.CompetitorRef == _competitors[3]).RawScore.Should().Be(1000m);

        var pilot1 = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryByPilot[1],
            TestContext.Current.CancellationToken);
        var landing1 = pilot1.Flights[0].Measurements.Single(m => m.Metric == "landingDistance");
        landing1.Value.Number.Should().Be(98m, "the original reading survives — append-only");
        var amendment1 = landing1.Amendments.Should().ContainSingle().Subject;
        amendment1.NewValue.Number.Should().Be(90m);
        amendment1.Instrument.Should().Be("nz-f3j-side", "the instrument is retained when no change is named");
        amendment1.Reason.Should().Be("misread the tape");

        var pilot4 = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryByPilot[4],
            TestContext.Current.CancellationToken);
        var landing4 = pilot4.Flights[0].Measurements.Single(m => m.Metric == "landingDistance");
        landing4.Value.Number.Should().Be(0.5m, "the original distance survives — append-only");
        var amendment4 = landing4.Amendments.Should().ContainSingle().Subject;
        amendment4.NewValue.Number.Should().Be(98m);
        amendment4.Instrument.Should().Be("nz-f3j-side");

        var pilot5 = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, _entryByPilot[5],
            TestContext.Current.CancellationToken);
        pilot5.Flights[0].Measurements.Single(m => m.Metric == "touchedByCompetitor").Amendments
            .Should().HaveCount(2, "touch on, then corrected off");
    }

    [Then(@"^no landing contributes twice$")]
    public async Task ThenNoLandingContributesTwice()
    {
        // One flight, one landingDistance measurement per entry, and the
        // pre-normalisation score is exactly flight time plus the single
        // award it decides — no form contributes twice, including amended ones.
        foreach (var (pilot, entryId) in _entryByPilot)
        {
            var entry = await EntryReader.LoadAsync(AcceptanceFixture.EventStore, entryId,
                TestContext.Current.CancellationToken);
            entry.Flights.Should().ContainSingle();
            entry.Flights[0].Measurements.Count(m => m.Metric == "landingDistance").Should().Be(1, $"pilot {pilot}");
        }

        var view = await FetchGroupViewAsync();
        view.Results.Single(r => r.CompetitorRef == _competitors[0]).PreNormalisationScore.Should().Be(440m);
        view.Results.Single(r => r.CompetitorRef == _competitors[3]).PreNormalisationScore.Should().Be(450m);
        view.Results.Single(r => r.CompetitorRef == _competitors[4]).PreNormalisationScore.Should().Be(440m);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Group> ResolveGroupAsync()
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        return view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == 1)
            .TaskRounds.Single().Groups.Should().ContainSingle().Subject;
    }

    private async Task<GroupScoreView> FetchGroupViewAsync()
    {
        var views = await ApiClient.GetAsync<List<GroupScoreView>>(
            Client, $"/task-round-result?competitionRef={_competitionId.Value}&phaseOrdinal=0&roundOrdinal=1&taskRoundOrdinal=1");
        return views.Should().ContainSingle().Subject;
    }

    private async Task<EntryId> OpenFlightAsync(GroupId groupRef, CompetitorId competitorRef)
    {
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, 1, 1, groupRef, competitorRef));
        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));
        return entryId;
    }

    private static async Task CaptureAsync(EntryId entryId, string metric, MeasuredValue value, string? instrument = null) =>
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/capture-measurement", new CaptureMeasurement(entryId, 1, metric, value, instrument));

    private static async Task<string> ReadProblemTitleAsync(HttpResponseMessage response)
    {
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("title").GetString()!;
    }
}
