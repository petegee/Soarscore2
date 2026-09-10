// kanban/in-progress/operational-tie-break-resolution.md WI-4 ("Acceptance") —
// step definitions for Features/ResolvingATieBreak.feature. Every step drives
// real HTTP against the real Soarscore.Api (AcceptanceFixture.Client), the
// same discipline RecordingAReflightRulingSteps.cs established.
//
// A self-contained [Binding] class with its own Given/When/Then phrasing:
// Reqnroll binds step regexes assembly-wide, so a regex shared verbatim across
// two Binding classes is an ambiguous match. Nothing here reuses another Steps
// class's phrasing (notably "the leaderboard is requested" and "the contest
// director finalises the competition").
//
// Definition provisioning through Support/Gliderscore/SeedDefinitionLoader —
// the canonical seed JSON via the same ClassDefinitionIngestion.Options a
// human POST to /publish-class-definition binds through — following the
// parallel-run feature's precedent. A missing or stale JSON surfaces here as
// the loader's InvalidOperationException naming the absent file (run
// `dotnet run --project tools/Soarscore.SeedData` to regenerate).
//
// F5L (60-f5l) for the ruling scenarios: UndefinedRequiresRuling on both
// phases, so ANY Score tie pends with no fly-off needed. Raw == 2 * flightTime
// exactly: flightTime stays below the 390 s full-rate band and every flight
// lands 100 m out, past the landing table's last row for zero landing points.
// F3K (10-f3k) for the flown scenario: TieBreakFlyoff after BestDroppedScore,
// a single-drop class (drop gate: 6 completed rounds) where two competitors
// tying on Score AND dropped cell reach the fly-off rung. Six distinct
// single-flight catalogue tasks (A, B, F, G, I, J — no declared metrics, no
// target assignment, no multi-flight selection), every flightTime below every
// task's cap, so raw == flightTime and every normalised score is exact.

using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Acceptance.Tests.Support.Gliderscore;
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

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class ResolvingATieBreakSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private static readonly string[] F3KTieBreakTasks = ["A", "B", "F", "G", "I", "J"];

    private CompetitionId _competitionId;
    private readonly List<CompetitorId> _competitors = [];

    /// <summary>The two tied competitors — the first two registered — flown identically every round.</summary>
    private List<CompetitorId> _tiedPair = [];

    /// <summary>Populated by the pending-query When step, read by its Then steps.</summary>
    private PendingTieBreaksView? _pending;

    /// <summary>Populated by each refusing When step, read by its Then.</summary>
    private HttpResponseMessage? _refusedOutcome;

    /// <summary>The competitor recorded ahead by the most recent outcome — the order the leaderboard must show.</summary>
    private CompetitorId _recordedFirst;

    /// <summary>Outsider placings snapshotted before the outcome is recorded (scenario 2).</summary>
    private readonly Dictionary<CompetitorId, int?> _outsiderPlacings = new();

    /// <summary>Leaderboard snapshot (score + placing per competitor) taken when the tie is engineered.</summary>
    private readonly Dictionary<CompetitorId, (decimal Score, int? Placing)> _baseline = new();

    // ---------------------------------------------------------------- Given

    [Given(@"^an F5L tie-break competition of (\d+) competitors drawn for (one|four) rounds?$")]
    public async Task GivenAnF5LTieBreakCompetitionDrawnForRounds(int competitorCount, string roundsWord)
    {
        var rounds = roundsWord == "four" ? 4 : 1;
        await SetupCompetitionAsync("60-f5l.json", competitorCount, "tiebreak-f5l", $"Tie-Break F5L {Guid.NewGuid():N}");

        // F5L leaves groupSize to the CD (SeedF5L.cs): bound to the whole
        // field so every round draws as one group holding everyone.
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/bind-parameter",
            new BindParameter(_competitionId, "groupSize", MeasuredValue.Of((decimal)competitorCount), "The contest director"));

        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, rounds));
        // D4: flights require an accepted draw.
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/accept-draw", new AcceptDraw(_competitionId));

        var group = await ResolveGroupAsync(roundOrdinal: 1);
        group.CompetitorRefs.Should().HaveCount(competitorCount);
    }

    [Given(@"^an F3K tie-break competition of (\d+) competitors drawn for six catalogue rounds$")]
    public async Task GivenAnF3KTieBreakCompetitionDrawnForSixCatalogueRounds(int competitorCount)
    {
        await SetupCompetitionAsync("10-f3k.json", competitorCount, "tiebreak-f3k", $"Tie-Break F3K {Guid.NewGuid():N}");

        // Catalogue-choice phase (SeedF3K.cs): the CD names the task for every
        // round as part of the draw. F3K's MinPerGroup is the literal 5, so a
        // 5-pilot field draws to exactly one group per round.
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/draw-phase", new DrawPhase(_competitionId, F3KTieBreakTasks.Length, F3KTieBreakTasks));
        // D4: flights require an accepted draw.
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/accept-draw", new AcceptDraw(_competitionId));

        var group = await ResolveGroupAsync(roundOrdinal: 1);
        group.CompetitorRefs.Should().HaveCount(competitorCount);
    }

    [Given(@"^the round (\d+) field flies with the first two tied on top$")]
    public async Task GivenTheRoundFieldFliesWithTheFirstTwoTiedOnTop(int roundOrdinal)
    {
        await FlyF5LRoundAsync(roundOrdinal);
        await SnapshotLeaderboardAsync();
    }

    [Given(@"^rounds 1 to 3 are flown with the first two tied on top$")]
    public async Task GivenRounds1To3AreFlownWithTheFirstTwoTiedOnTop()
    {
        for (var roundOrdinal = 1; roundOrdinal <= 3; roundOrdinal++)
        {
            await FlyF5LRoundAsync(roundOrdinal);
        }

        await SnapshotLeaderboardAsync();
    }

    [Given(@"^every round is flown with the first two tied on top$")]
    public async Task GivenEveryRoundIsFlownWithTheFirstTwoTiedOnTop()
    {
        // Six rounds (the F3K drop gate: ApplyWhenRoundsCompletedAtLeast 6)
        // flown identically: the pair ties on Score AND on the dropped cell,
        // so BestDroppedScore cannot separate them and the ladder halts on
        // the TieBreakFlyoff rung.
        for (var roundOrdinal = 1; roundOrdinal <= F3KTieBreakTasks.Length; roundOrdinal++)
        {
            await FlyF3KRoundAsync(roundOrdinal);
        }

        await SnapshotLeaderboardAsync();
    }

    // ----------------------------------------------------------------- When

    [When(@"^the pending tie-breaks are requested$")]
    public async Task WhenThePendingTieBreaksAreRequested()
    {
        _pending = await ApiClient.GetAsync<PendingTieBreaksView>(
            Client, $"/competition-pending-tie-breaks?competitionRef={_competitionId.Value}");
    }

    [When(@"^the CD records the fly-off ordering with the first tied competitor ahead$")]
    public async Task WhenTheCDRecordsTheFlyOffOrderingWithTheFirstTiedCompetitorAhead()
    {
        await SnapshotOutsidersAsync();
        await RecordOutcomeAsync(new TieBreakFlyoff(), _tiedPair[0], _tiedPair[1], "One-task fly-off flown, scoresheet with the CD");
    }

    [When(@"^the CD records a ruling putting the (first|second) tied competitor ahead$")]
    public async Task WhenTheCDRecordsARulingPuttingATiedCompetitorAhead(string which)
    {
        var first = which == "first" ? _tiedPair[0] : _tiedPair[1];
        var second = which == "first" ? _tiedPair[1] : _tiedPair[0];
        await RecordOutcomeAsync(new UndefinedRequiresRuling(), first, second, "The rulebook states classification and stops; the CD rules");
    }

    [When(@"^the CD attempts to record a tie-break outcome for an unregistered competitor$")]
    public async Task WhenTheCDAttemptsToRecordATieBreakOutcomeForAnUnregisteredCompetitor() =>
        _refusedOutcome = await ApiClient.PostCommandRawAsync(
            Client,
            "/record-tie-break-outcome",
            new RecordTieBreakOutcome(
                _competitionId,
                0,
                new UndefinedRequiresRuling(),
                new[] { _tiedPair[0], CompetitorId.New() }.Select((c, i) => new TieBreakOutcomePlacing(c, i + 1)).ToImmutableArray(),
                "Typo protection probe",
                "the contest director"));

    [When(@"^the CD attempts to record a tie-break outcome with gapped placings$")]
    public async Task WhenTheCDAttemptsToRecordATieBreakOutcomeWithGappedPlacings() =>
        _refusedOutcome = await ApiClient.PostCommandRawAsync(
            Client,
            "/record-tie-break-outcome",
            new RecordTieBreakOutcome(
                _competitionId,
                0,
                new UndefinedRequiresRuling(),
                [new TieBreakOutcomePlacing(_tiedPair[0], 1), new TieBreakOutcomePlacing(_tiedPair[1], 3)],
                "A gap where dense skip-ahead belongs",
                "the contest director"));

    [When(@"^the CD attempts to record a fly-off outcome where the class states only a ruling$")]
    public async Task WhenTheCDAttemptsToRecordAFlyOffOutcomeWhereTheClassStatesOnlyARuling() =>
        _refusedOutcome = await ApiClient.PostCommandRawAsync(
            Client,
            "/record-tie-break-outcome",
            new RecordTieBreakOutcome(
                _competitionId,
                0,
                new TieBreakFlyoff(),
                new[] { _tiedPair[0], _tiedPair[1] }.Select((c, i) => new TieBreakOutcomePlacing(c, i + 1)).ToImmutableArray(),
                "A fly-off the adopted rules never state",
                "the contest director"));

    [When(@"^round (\d+) is flown normally with the tie still pending$")]
    public async Task WhenRoundIsFlownNormallyWithTheTieStillPending(int roundOrdinal)
    {
        // NFR-4's own sentence: entries open and scores are captured exactly
        // as if no tie were pending — the write model does not care.
        await FlyF5LRoundAsync(roundOrdinal);
    }

    [When(@"^all four rounds are closed$")]
    public async Task WhenAllFourRoundsAreClosed()
    {
        // F5L Validity.MinRounds is 4: finalisation counts rounds flown to a
        // result, i.e. closed task-rounds (the ClosingACompetition precedent).
        // Closing gates nothing about the tie — the pending pair stays pending.
        for (var roundOrdinal = 1; roundOrdinal <= 4; roundOrdinal++)
        {
            await ApiClient.PostCommandAsync<CompetitionId>(
                Client, "/complete-task-round", new CompleteTaskRound(_competitionId, 0, roundOrdinal, 1));
        }
    }

    [When(@"^the CD finalises the competition$")]
    public async Task WhenTheCDFinalisesTheCompetition() =>
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/finalise-competition", new FinaliseCompetition(_competitionId, "CD Jane"));

    // ----------------------------------------------------------------- Then

    [Then(@"^one tie pends over the tied pair sharing place (\d+) under a (ruling|fly-off) directive$")]
    public void ThenOneTiePendsOverTheTiedPairSharingPlaceUnderADirective(int sharedPlace, string kind)
    {
        _pending.Should().NotBeNull();
        _pending!.Ties.Should().ContainSingle();

        var tie = _pending.Ties.Single();
        tie.PhaseOrdinal.Should().Be(0);
        tie.CompetitorRefs.Should().BeEquivalentTo(_tiedPair);
        tie.SharedPlace.Should().Be(sharedPlace);

        if (kind == "ruling")
        {
            tie.Directive.Should().BeOfType<UndefinedRequiresRuling>();
        }
        else
        {
            tie.Directive.Should().BeOfType<TieBreakFlyoff>();
        }
    }

    [Then(@"^the leaderboard places the tied pair first and second in the recorded order$")]
    public async Task ThenTheLeaderboardPlacesTheTiedPairFirstAndSecondInTheRecordedOrder()
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        var first = view.Scores.Single(s => s.CompetitorRef == _recordedFirst);
        var second = view.Scores.Single(s => s.CompetitorRef == _tiedPair.Single(c => c != _recordedFirst));

        first.Disqualified.Should().BeFalse();
        second.Disqualified.Should().BeFalse();
        first.Placing.Should().Be(1);
        second.Placing.Should().Be(2);
    }

    [Then(@"^the recorded order is reversed on the leaderboard$")]
    public async Task ThenTheRecordedOrderIsReversedOnTheLeaderboard()
    {
        // The second ruling superseded the first (last matching record wins):
        // the same assertion as the recorded order, now under the new record.
        await ThenTheLeaderboardPlacesTheTiedPairFirstAndSecondInTheRecordedOrder();
    }

    [Then(@"^no tie pends any longer$")]
    public async Task ThenNoTiePendsAnyLonger()
    {
        var pending = await ApiClient.GetAsync<PendingTieBreaksView>(
            Client, $"/competition-pending-tie-breaks?competitionRef={_competitionId.Value}");
        pending.Ties.Should().BeEmpty();
    }

    [Then(@"^every outsider keeps their place$")]
    public async Task ThenEveryOutsiderKeepsTheirPlace()
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        foreach (var (competitor, placing) in _outsiderPlacings)
        {
            view.Scores.Single(s => s.CompetitorRef == competitor).Placing.Should().Be(placing);
        }
    }

    [Then(@"^the tie-break outcome is refused with (.+)$")]
    public async Task ThenTheTieBreakOutcomeIsRefusedWith(string code)
    {
        _refusedOutcome.Should().NotBeNull();
        _refusedOutcome!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await _refusedOutcome.Content.ReadFromJsonAsync<ProblemDetails>(ApiClient.Options);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be(code);
    }

    [Then(@"^the leaderboard is unchanged and the tie still pends$")]
    public async Task ThenTheLeaderboardIsUnchangedAndTheTieStillPends()
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        view.Scores.Should().HaveCount(_baseline.Count);
        foreach (var score in view.Scores)
        {
            var expected = _baseline[score.CompetitorRef];
            score.Score.Should().Be(expected.Score);
            score.Placing.Should().Be(expected.Placing);
        }

        var pending = await ApiClient.GetAsync<PendingTieBreaksView>(
            Client, $"/competition-pending-tie-breaks?competitionRef={_competitionId.Value}");
        pending.Ties.Should().ContainSingle();
        pending.Ties.Single().CompetitorRefs.Should().BeEquivalentTo(_tiedPair);
    }

    [Then(@"^the tie-break leaderboard computes$")]
    public async Task ThenTheTieBreakLeaderboardComputes()
    {
        var response = await Client.GetAsync($"/competition-result?competitionRef={_competitionId.Value}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Then(@"^the tie still pends over the same pair$")]
    public async Task ThenTheTieStillPendsOverTheSamePair()
    {
        var pending = await ApiClient.GetAsync<PendingTieBreaksView>(
            Client, $"/competition-pending-tie-breaks?competitionRef={_competitionId.Value}");
        pending.Ties.Should().ContainSingle();
        pending.Ties.Single().CompetitorRefs.Should().BeEquivalentTo(_tiedPair);
    }

    [Then(@"^finalisation succeeds declaring the tied pair shared at place (\d+)$")]
    public async Task ThenFinalisationSucceedsDeclaringTheTiedPairSharedAtPlace(int sharedPlace)
    {
        // Finalisation declares what the engine derived — the shared places,
        // exactly as derived (D5): no pending-tie check gates it.
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var finalisation = view.Competition.Finalisations.Should().ContainSingle().Subject;

        finalisation.By.Should().Be("CD Jane");

        var leaderboard = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        foreach (var declared in finalisation.DeclaredResults)
        {
            var derived = leaderboard.Scores.Single(s => s.CompetitorRef == declared.CompetitorRef);
            declared.Aggregate.Should().Be(derived.Score);
            declared.Placing.Should().Be(derived.Placing);
        }

        foreach (var member in _tiedPair)
        {
            finalisation.DeclaredResults.Single(d => d.CompetitorRef == member).Placing.Should().Be(sharedPlace);
        }
    }

    // --------------------------------------------------------------- helpers

    private async Task SetupCompetitionAsync(string seedFileName, int competitorCount, string emailPrefix, string competitionName)
    {
        // The corpus seed class through the same ingestion options a human
        // POST uses (SeedDefinitionLoader) — the parallel-run precedent.
        var definition = SeedDefinitionLoader.Load(seedFileName);
        var contentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(definition));

        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition(competitionName, "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), contentHash));

        for (var i = 0; i < competitorCount; i++)
        {
            var email = $"pilot-{emailPrefix}-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }

        _tiedPair = [_competitors[0], _competitors[1]];
    }

    /// <summary>
    /// One F5L round flown with the tied pair identical on top (300 s each)
    /// and two distinct outsiders below (270 s, 240 s) — raw 600/540/480,
    /// normalised 1000/900/800, the pair tied on Score with no comparator to
    /// separate them (this file's header).
    /// </summary>
    private async Task FlyF5LRoundAsync(int roundOrdinal)
    {
        var group = await ResolveGroupAsync(roundOrdinal);
        group.CompetitorRefs.Should().HaveCount(4);

        var times = new Dictionary<CompetitorId, decimal>
        {
            [_tiedPair[0]] = 300m,
            [_tiedPair[1]] = 300m,
            [_competitors[2]] = 270m,
            [_competitors[3]] = 240m,
        };

        foreach (var competitorRef in group.CompetitorRefs)
        {
            await CaptureF5LFlightAsync(roundOrdinal, group.Id, competitorRef, times[competitorRef]);
        }
    }

    /// <summary>
    /// One F3K round flown with the tied pair identical on top (100 s each)
    /// and three distinct outsiders below (80/60/40 s) — under every flown
    /// task's cap (this file's header), so raw == flightTime and every
    /// normalised score is exact.
    /// </summary>
    private async Task FlyF3KRoundAsync(int roundOrdinal)
    {
        var group = await ResolveGroupAsync(roundOrdinal);
        group.CompetitorRefs.Should().HaveCount(5);

        var times = new Dictionary<CompetitorId, decimal>
        {
            [_tiedPair[0]] = 100m,
            [_tiedPair[1]] = 100m,
            [_competitors[2]] = 80m,
            [_competitors[3]] = 60m,
            [_competitors[4]] = 40m,
        };

        foreach (var competitorRef in group.CompetitorRefs)
        {
            await CaptureF3KFlightAsync(roundOrdinal, group.Id, competitorRef, times[competitorRef]);
        }
    }

    private async Task RecordOutcomeAsync(TieBreakDirective directive, CompetitorId first, CompetitorId second, string reason)
    {
        var response = await ApiClient.PostCommandRawAsync(
            Client,
            "/record-tie-break-outcome",
            new RecordTieBreakOutcome(
                _competitionId,
                0,
                directive,
                [new TieBreakOutcomePlacing(first, 1), new TieBreakOutcomePlacing(second, 2)],
                reason,
                "the contest director"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _recordedFirst = first;
    }

    private async Task SnapshotOutsidersAsync()
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        _outsiderPlacings.Clear();
        foreach (var score in view.Scores.Where(s => !_tiedPair.Contains(s.CompetitorRef)))
        {
            _outsiderPlacings[score.CompetitorRef] = score.Placing;
        }
    }

    private async Task SnapshotLeaderboardAsync()
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        _baseline.Clear();
        foreach (var score in view.Scores)
        {
            _baseline[score.CompetitorRef] = (score.Score, score.Placing);
        }
    }

    private async Task<Group> ResolveGroupAsync(int roundOrdinal)
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var round = view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == roundOrdinal);
        return round.TaskRounds.Single().Groups.Single();
    }

    /// <summary>
    /// Opens an entry, opens its one flight, and captures the two F5L task D
    /// metrics without a when-not-recorded default — flightTime and
    /// landingDistance. Every other metric resolves to its recorded-exception
    /// absence default (this file's header).
    /// </summary>
    private async Task<EntryId> CaptureF5LFlightAsync(int roundOrdinal, GroupId groupRef, CompetitorId competitorRef, decimal flightTime)
    {
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, roundOrdinal, 1, groupRef, competitorRef));

        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));

        await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(flightTime));
        await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(100m)); // beyond the last row -> Rest(0)

        return entryId;
    }

    /// <summary>
    /// Opens an entry, opens its one flight, and captures flightTime plus the
    /// two F3K class-wide flight voids as compliance (landed within the
    /// window, launched in working time) — one flight suffices for every
    /// flown task's selection (Last/LastN/BestN all take what is there).
    /// </summary>
    private async Task<EntryId> CaptureF3KFlightAsync(int roundOrdinal, GroupId groupRef, CompetitorId competitorRef, decimal flightTime)
    {
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, roundOrdinal, 1, groupRef, competitorRef));

        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));

        await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(flightTime));
        await CaptureAsync(entryId, "landedWithinWindow", MeasuredValue.Of(true));
        await CaptureAsync(entryId, "launchedInWorkingTime", MeasuredValue.Of(true));

        return entryId;
    }

    private static async Task CaptureAsync(EntryId entryId, string metric, MeasuredValue value) =>
        await ApiClient.PostCommandAsync<EntryId>(Client, "/capture-measurement", new CaptureMeasurement(entryId, 1, metric, value));
}
