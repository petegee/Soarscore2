// kanban/in-progress/reflight-aggregate-destination.md WI-3 ("Acceptance BDD,
// parallel with WI-4") — step definitions for
// Features/ReflightingForAMissedRound.feature. Every step drives real HTTP
// against the real Soarscore.Api (AcceptanceFixture.Client), the same
// discipline ClosingACompetitionSteps.cs established and
// ReflightingAGroupSteps.cs follows.
//
// A self-contained [Binding] class with its own Given/When/Then phrasing:
// Reqnroll binds step regexes assembly-wide, so a regex shared verbatim across
// two Binding classes is an ambiguous match (ClosingACompetitionSteps.cs's own
// header). Nothing here reuses another Steps class's phrasing — the ordinary-
// reflight scenario deliberately mirrors ReflightingAGroupSteps's shape with
// different words, and even "the open is refused because ..." had to be
// reworded (CapturingAScoreSteps.cs owns that literal).
//
// Two corpus classes, each where it belongs:
//   - 30-f5j hosts the scored make-up scenarios. A make-up's cell keys to the
//     destination round's walked slot at the HOSTING task-round's
//     (ordinal, task code) — D7's score.reflightDestinationTaskMismatch — and
//     F5J is a fixed-sequence single-task class, so every round's task-round
//     is (1, "D") and a make-up across rounds resolves. 10-f3k cannot host a
//     scored make-up: its catalogue-choice draw requires a different task
//     every round (RequireDistinctTaskPerRound, F3K.10), so no destination
//     round ever holds the hosting task's slot and the law refuses the shape.
//     F5J is also the class of the story's richer corpus witness
//     (f5j-hawkes-bay-trials, comp 135). The scored scenarios assert the
//     leaderboard total, which is exact only when every round cell is: the
//     make-up's normalised score lands in the missed round's slot (not a
//     synthesised zero), the hosting-round cells are untouched, and no drop
//     fires (F5J's discard gate needs 5 completed rounds; 3 are flown).
//   - 10-f3k hosts the write-side refusals and the ordinary-reflight
//     regression. The distinct-task draw makes the handler's D8 destination
//     lookups skip (the destination round's task-round carries a different
//     task code), so each refusal code fires deterministically from the
//     decide; scenario 4 needs no flights at all. Scenario 7 re-proves the
//     same-round Original+Entitled collapse (F3K.9.6 Replacement) that
//     ReflightingAGroup has always scored, now under the destination-aware
//     law with no counts-for anywhere.
//
// Flight-time conventions, identical to those files':
//   - F5J (ClosingACompetitionSteps.cs's): constant time per competitor
//     across rounds (250..500 by group position), every metric but flightTime
//     contributing zero, so raw == flightTime and each round cell is exactly
//     2 * flightTime (winner 500). A make-up's flight is captured in the
//     hosting round's own group below the 500 s winner, so the group's
//     normalisation basis — and every other competitor's cell — is untouched.
//   - F3K (ReflightingAGroupSteps.cs's): winner 300, every time divides 300,
//     so each normalised score is the whole number 1000 * raw / 300.

using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class ReflightingForAMissedRoundSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;
    private static readonly ClassDefinition F3KDefinition = Corpus.All.Single(c => c.FileName == "10-f3k").Definition;

    private CompetitionId _competitionId;
    private readonly List<CompetitorId> _competitors = [];

    /// <summary>The competitor whose missed round the make-up fills.</summary>
    private CompetitorId _makeUpPilot;

    /// <summary>Flight time == raw score, constant per competitor across rounds (this file's header).</summary>
    private readonly Dictionary<CompetitorId, decimal> _flightTimeByCompetitor = [];

    /// <summary>Which metric set a captured flight needs — the two classes' tasks declare different ones.</summary>
    private bool _isF3K;

    private HttpResponseMessage? _refusedResponse;

    // ---------------------------------------------------------------- Given

    [Given(@"^an F5J competition of (\d+) competitors is under way with (\d+) drawn rounds?$")]
    public async Task GivenAnF5JCompetitionIsUnderWay(int competitorCount, int rounds) =>
        await CreateCompetitionAsync(F5JDefinition, isF3K: false, "Makeup F5J", competitorCount, rounds, taskRefs: null, firstTime: 250m, step: 50m);

    [Given(@"^an F3K competition of (\d+) competitors has (\d+) rounds drawn for tasks A, B and D$")]
    public async Task GivenAnF3KCompetitionHasRoundsDrawnForTasksABAndD(int competitorCount, int rounds)
    {
        // The drawn round count must equal the task list below: F3K's
        // catalogue-choice draw demands a distinct task per round
        // (drawPhase.taskSelectionNotDistinct otherwise).
        rounds.Should().Be(3);
        await CreateCompetitionAsync(F3KDefinition, isF3K: true, "Makeup F3K", competitorCount, rounds, ["A", "B", "D"], firstTime: 300m, step: -30m);
    }

    [Given(@"^an F3K competition of (\d+) competitors has a single round drawn for task A$")]
    public async Task GivenAnF3KCompetitionHasASingleRoundDrawnForTaskA(int competitorCount) =>
        await CreateCompetitionAsync(F3KDefinition, isF3K: true, "Makeup F3K", competitorCount, 1, ["A"], firstTime: 300m, step: -30m);

    [Given(@"^every competitor but the make-up pilot has flown round (\d+)$")]
    public async Task GivenEveryCompetitorButTheMakeUpPilotHasFlownRound(int roundOrdinal) =>
        await FlyRoundAsync(roundOrdinal, skipTheMakeUpPilot: true);

    [Given(@"^every competitor has flown round (\d+)$")]
    public async Task GivenEveryCompetitorHasFlownRound(int roundOrdinal) =>
        await FlyRoundAsync(roundOrdinal, skipTheMakeUpPilot: false);

    [Given(@"^the whole field flies the drawn group with distinct flight times$")]
    public async Task GivenTheWholeFieldFliesTheDrawnGroup()
    {
        var group = await SingleGroupAsync(1);
        foreach (var competitorRef in group.CompetitorRefs)
        {
            await CaptureFlightAsync(1, group.Id, competitorRef, _flightTimeByCompetitor[competitorRef], ReflightRole.Original, null, null);
        }
    }

    [Given(@"^the make-up pilot has flown a make-up in round (\d+)'s group counting for round (\d+)$")]
    public async Task GivenTheMakeUpPilotHasFlownAMakeUp(int hostingRoundOrdinal, int countsForRoundOrdinal) =>
        await FlyMakeUpAsync(hostingRoundOrdinal, countsForRoundOrdinal);

    // ----------------------------------------------------------------- When

    [When(@"^the make-up pilot flies a make-up in round (\d+)'s group counting for round (\d+)$")]
    public async Task WhenTheMakeUpPilotFliesAMakeUp(int hostingRoundOrdinal, int countsForRoundOrdinal) =>
        await FlyMakeUpAsync(hostingRoundOrdinal, countsForRoundOrdinal);

    [When(@"^the make-up pilot flies two make-ups in round (\d+)'s group, counting for rounds (\d+) and (\d+)$")]
    public async Task WhenTheMakeUpPilotFliesTwoMakeUps(int hostingRoundOrdinal, int firstDestination, int secondDestination)
    {
        var group = await SingleGroupAsync(hostingRoundOrdinal);

        // The comp-135 shape: the pilot's Original plus two Entitled make-ups
        // live in one task-round, each destination slot holding exactly one
        // candidate. 200 s -> 400 and 300 s -> 600 against the 500 s winner —
        // both below the winner's raw, so the group's basis is untouched.
        await CaptureFlightAsync(
            hostingRoundOrdinal, group.Id, _makeUpPilot, 200m,
            ReflightRole.Entitled, firstDestination, $"Round {firstDestination} launch equipment fault");
        await CaptureFlightAsync(
            hostingRoundOrdinal, group.Id, _makeUpPilot, 300m,
            ReflightRole.Entitled, secondDestination, $"Round {secondDestination} launch equipment fault");
    }

    [When(@"^the make-up pilot attempts a make-up in round (\d+)'s group counting for round (\d+)$")]
    public async Task WhenTheMakeUpPilotAttemptsAMakeUp(int hostingRoundOrdinal, int countsForRoundOrdinal)
    {
        var group = await SingleGroupAsync(hostingRoundOrdinal);
        _refusedResponse = await ApiClient.PostCommandRawAsync(
            Client,
            "/open-entry",
            new OpenEntry(_competitionId, 0, hostingRoundOrdinal, 1, group.Id, _makeUpPilot, ReflightRole.Entitled, countsForRoundOrdinal, $"Round {countsForRoundOrdinal} launch equipment fault"));
    }

    [When(@"^the CD attempts to open an original entry in round (\d+) counting for round (\d+)$")]
    public async Task WhenTheCDAttemptsToOpenAnOriginalEntryCountingForRound(int roundOrdinal, int countsForRoundOrdinal)
    {
        var group = await SingleGroupAsync(roundOrdinal);
        _refusedResponse = await ApiClient.PostCommandRawAsync(
            Client,
            "/open-entry",
            new OpenEntry(_competitionId, 0, roundOrdinal, 1, group.Id, _makeUpPilot, ReflightRole.Original, countsForRoundOrdinal, "Round 1 launch equipment fault"));
    }

    [When(@"^the CD attempts to open a make-up in round (\d+) counting for round (\d+)$")]
    public async Task WhenTheCDAttemptsToOpenAMakeUpCountingForRound(int roundOrdinal, int countsForRoundOrdinal)
    {
        var group = await SingleGroupAsync(roundOrdinal);
        _refusedResponse = await ApiClient.PostCommandRawAsync(
            Client,
            "/open-entry",
            new OpenEntry(_competitionId, 0, roundOrdinal, 1, group.Id, _makeUpPilot, ReflightRole.Entitled, countsForRoundOrdinal, $"Round {countsForRoundOrdinal} launch equipment fault"));
    }

    [When(@"^the CD attempts to open a make-up in round (\d+) counting for round (\d+) without a reason$")]
    public async Task WhenTheCDAttemptsToOpenAMakeUpWithoutReason(int roundOrdinal, int countsForRoundOrdinal)
    {
        var group = await SingleGroupAsync(roundOrdinal);
        _refusedResponse = await ApiClient.PostCommandRawAsync(
            Client,
            "/open-entry",
            new OpenEntry(_competitionId, 0, roundOrdinal, 1, group.Id, _makeUpPilot, ReflightRole.Entitled, countsForRoundOrdinal, null));
    }

    [When(@"^the CD attempts to open an original entry for the drawn pilot into the other group$")]
    public async Task WhenTheCDAttemptsToOpenAnOriginalEntryIntoTheOtherGroup()
    {
        // A 12-competitor F5J field draws to two groups of 6 — the drawn-check
        // relaxation (D5) lifts the requirement for reflight-role entries
        // only, so an Original into the group the pilot was not drawn into
        // must still refuse exactly as before.
        var groups = await ResolveGroupsAsync(1);
        groups.Should().HaveCount(2);

        _refusedResponse = await ApiClient.PostCommandRawAsync(
            Client,
            "/open-entry",
            new OpenEntry(_competitionId, 0, 1, 1, groups[1].Id, groups[0].CompetitorRefs[0]));
    }

    [When(@"^the group's winner re-flies with the same group, flying a shorter time$")]
    public async Task WhenTheGroupsWinnerRefliestWithTheSameGroup()
    {
        var group = await SingleGroupAsync(1);
        var winner = _flightTimeByCompetitor.MaxBy(kv => kv.Value).Key;

        // The ordinary reflight shape: role Entitled, NO counts-for, NO
        // reason — the destination datum is absent, so the entry counts for
        // its own round and the collapse is the class rule verbatim.
        await CaptureFlightAsync(1, group.Id, winner, 150m, ReflightRole.Entitled, null, null);
    }

    // ----------------------------------------------------------------- Then

    [Then(@"^the make-up pilot's missed round is scored at their make-up's normalised score, not a zero$")]
    public async Task ThenTheMakeUpPilotsMissedRoundIsScoredAtTheirMakeUpsNormalisedScore()
    {
        var view = await LeaderboardAsync();
        var score = view.Scores.Single(s => s.CompetitorRef == _makeUpPilot);
        score.Disqualified.Should().BeFalse();

        // The make-up normalises within its HOSTING group (round 2's, against
        // its 500 s winner): 300 s -> 600. The pilot's own rounds 2 and 3
        // score 2 * 250 = 500 each. A synthesised zero in the missed slot (or
        // the make-up cell silently dropped) would total 1000; the make-up
        // wrongly collapsing into its hosting round would total 1100. Only
        // the faithful 600 + 500 + 500 proves the destination slot filled.
        var expected = 1000m * 300m / 500m + 2 * (2m * _flightTimeByCompetitor[_makeUpPilot]);
        score.Score.Should().Be(expected);
        score.Score.Should().Be(1600m);
    }

    [Then(@"^a competitor who flew every round keeps the sum of their three round scores$")]
    public async Task ThenACompetitorWhoFlewEveryRoundKeepsTheSumOfTheirThreeRoundScores()
    {
        var view = await LeaderboardAsync();
        var control = _flightTimeByCompetitor.MaxBy(kv => kv.Value).Key; // the 500 s winner

        var score = view.Scores.Single(s => s.CompetitorRef == control);
        score.Disqualified.Should().BeFalse();

        // Three rounds at 2 * 500 = 1000 each, no drop (F5J's discard gate
        // needs 5 completed rounds) — untouched by the make-up's presence.
        score.Score.Should().Be(3 * (2m * _flightTimeByCompetitor[control]));
        score.Score.Should().Be(3000m);
    }

    [Then(@"^the make-up pilot's two missed rounds are scored at their make-ups' normalised scores, not zeros$")]
    public async Task ThenTheMakeUpPilotsTwoMissedRoundsAreScoredAtTheirMakeUpsNormalisedScores()
    {
        var view = await LeaderboardAsync();
        var score = view.Scores.Single(s => s.CompetitorRef == _makeUpPilot);
        score.Disqualified.Should().BeFalse();

        // Round 3 hosts the pilot's Original and both make-ups, every entry
        // normalising against the group's 500 s winner: 200 s -> 400 into
        // round 1's slot, 300 s -> 600 into round 2's slot, and the Original
        // 250 s -> 500 in round 3. Zeros in the missed slots would total 500;
        // only 400 + 600 + 500 proves both destination slots filled.
        var expected = 1000m * 200m / 500m + 1000m * 300m / 500m + 2m * _flightTimeByCompetitor[_makeUpPilot];
        score.Score.Should().Be(expected);
        score.Score.Should().Be(1500m);
    }

    [Then(@"^the attempt is refused with (.+)$")]
    public async Task ThenTheAttemptIsRefusedWith(string expectedCode)
    {
        _refusedResponse.Should().NotBeNull();
        _refusedResponse!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await _refusedResponse.Content.ReadFromJsonAsync<ProblemDetails>(ApiClient.Options);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be(expectedCode);
    }

    [Then(@"^the same pilot's original entry into their own drawn group is accepted$")]
    public async Task ThenTheSamePilotsOriginalEntryIntoTheirOwnDrawnGroupIsAccepted()
    {
        var groups = await ResolveGroupsAsync(1);
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client,
            "/open-entry",
            new OpenEntry(_competitionId, 0, 1, 1, groups[0].Id, groups[0].CompetitorRefs[0]));

        entryId.Value.Should().NotBe(Guid.Empty);
    }

    [Then(@"^the re-flying competitor is scored on their re-flight's normalised score$")]
    public async Task ThenTheReflyingCompetitorIsScoredOnTheirReflightsNormalisedScore()
    {
        var view = await LeaderboardAsync();
        var winner = _flightTimeByCompetitor.MaxBy(kv => kv.Value).Key;

        var score = view.Scores.Single(s => s.CompetitorRef == winner);
        score.Disqualified.Should().BeFalse();

        // EntitledScores is Replacement (F3K.9.6): the re-flight's 500
        // (1000 * 150 / 300) is official even though the original scored a
        // better 1000 — the no-destination law the counts-for datum changed
        // nothing about.
        score.Score.Should().Be(1000m * 150m / 300m);
        score.Score.Should().Be(500m);
    }

    [Then(@"^a competitor who did not re-fly keeps their original normalised score$")]
    public async Task ThenACompetitorWhoDidNotReFlyKeepsTheirOriginalNormalisedScore()
    {
        var view = await LeaderboardAsync();
        var winner = _flightTimeByCompetitor.MaxBy(kv => kv.Value).Key;
        var control = _flightTimeByCompetitor.Where(kv => kv.Key != winner).MaxBy(kv => kv.Value).Key; // the 270 s pilot

        var score = view.Scores.Single(s => s.CompetitorRef == control);
        score.Disqualified.Should().BeFalse();

        // One round, one normalisation: 1000 * 270 / 300 — the re-flight's
        // presence moved nobody outside the collapse.
        score.Score.Should().Be(1000m * _flightTimeByCompetitor[control] / 300m);
        score.Score.Should().Be(900m);
    }

    // --------------------------------------------------------------- helpers

    private async Task CreateCompetitionAsync(
        ClassDefinition definition,
        bool isF3K,
        string label,
        int competitorCount,
        int rounds,
        IReadOnlyList<string>? taskRefs,
        decimal firstTime,
        decimal step)
    {
        _isF3K = isF3K;

        var contentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(definition));

        // A hyphen-free GUID slug — the same discipline every Steps class uses
        // to keep scenarios sharing one database from colliding
        // (Person.IsPlausibleEmail rejects whitespace).
        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition($"{label} Acceptance {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), contentHash));

        for (var i = 0; i < competitorCount; i++)
        {
            var email = $"pilot-makeup-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }

        if (taskRefs is null)
        {
            // F5J is a fixed-sequence phase: the class names its one task, the
            // draw takes no task choice (ClosingACompetitionSteps.cs).
            await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, rounds));
        }
        else
        {
            // F3K is a catalogue-choice phase: the CD names each round's task
            // as part of the draw itself (DrawingACatalogueChoiceSteps.cs).
            await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, rounds, taskRefs));
        }

        // D4: flights require an accepted draw.
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/accept-draw", new AcceptDraw(_competitionId));

        // Constant flight time per competitor, assigned by group position. A
        // 6-competitor field draws exactly one group per round here (F5J's
        // MinPerGroup 6; F3K's MinPerGroup 5), so position i flies
        // firstTime + step * i and the last position is the group winner; a
        // 12-competitor F5J field draws two groups of 6 with the same time
        // spread per group (scenario 5 never flies, so only the shape
        // matters there).
        var groups = await ResolveGroupsAsync(1);
        foreach (var group in groups)
        {
            for (var i = 0; i < group.CompetitorRefs.Length; i++)
            {
                _flightTimeByCompetitor[group.CompetitorRefs[i]] = firstTime + step * i;
            }
        }

        _makeUpPilot = groups[0].CompetitorRefs[0];
    }

    private async Task FlyRoundAsync(int roundOrdinal, bool skipTheMakeUpPilot)
    {
        var group = await SingleGroupAsync(roundOrdinal);

        foreach (var competitorRef in group.CompetitorRefs)
        {
            if (skipTheMakeUpPilot && competitorRef == _makeUpPilot)
            {
                continue;
            }

            await CaptureFlightAsync(roundOrdinal, group.Id, competitorRef, _flightTimeByCompetitor[competitorRef], ReflightRole.Original, null, null);
        }
    }

    /// <summary>
    /// Opens the make-up (role Entitled, counts-for the missed round, the
    /// recorded entitlement reason) into the hosting round's own group and
    /// captures its 300 s flight — 600 normalised against the 500 s winner,
    /// below every other raw in the group. The hosting round's Original must
    /// already be open: the write side refuses any open once a live entry
    /// exists, so a make-up opened first would strand the Original behind
    /// openEntry.alreadyOpen (the story's trap 2 — GS's own flying order).
    /// </summary>
    private async Task FlyMakeUpAsync(int hostingRoundOrdinal, int countsForRoundOrdinal)
    {
        var group = await SingleGroupAsync(hostingRoundOrdinal);
        await CaptureFlightAsync(
            hostingRoundOrdinal, group.Id, _makeUpPilot, 300m,
            ReflightRole.Entitled, countsForRoundOrdinal, $"Round {countsForRoundOrdinal} launch equipment fault");
    }

    /// <summary>The one group drawn into the round's one task-round (6-competitor layouts).</summary>
    private async Task<Group> SingleGroupAsync(int roundOrdinal) =>
        (await ResolveGroupsAsync(roundOrdinal)).Should().ContainSingle().Subject;

    private async Task<IReadOnlyList<Group>> ResolveGroupsAsync(int roundOrdinal)
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var round = view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == roundOrdinal);
        return round.TaskRounds.Single().Groups.OrderBy(g => g.Ordinal).ToList();
    }

    private async Task<CompetitionScoreView> LeaderboardAsync() =>
        await ApiClient.GetAsync<CompetitionScoreView>(Client, $"/competition-result?competitionRef={_competitionId.Value}");

    /// <summary>
    /// Opens an Entry (carrying the make-up datum when present), opens its one
    /// flight, and captures every metric the scenario's class references, so
    /// raw == flightTime throughout (this file's header).
    /// </summary>
    private async Task<EntryId> CaptureFlightAsync(
        int roundOrdinal, GroupId groupRef, CompetitorId competitorRef, decimal flightTime,
        ReflightRole role, int? countsForRoundOrdinal, string? reason)
    {
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client,
            "/open-entry",
            new OpenEntry(_competitionId, 0, roundOrdinal, 1, groupRef, competitorRef, role, countsForRoundOrdinal, reason));

        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));

        if (_isF3K)
        {
            await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(flightTime));
            await CaptureAsync(entryId, "landedWithinWindow", MeasuredValue.Of(true));
            await CaptureAsync(entryId, "launchedInWorkingTime", MeasuredValue.Of(true));
        }
        else
        {
            await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(flightTime));
            await CaptureAsync(entryId, "startHeight", MeasuredValue.Of(0m));
            await CaptureAsync(entryId, "startHeightRecorded", MeasuredValue.Of(true));
            await CaptureAsync(entryId, "overflySeconds", MeasuredValue.Of(0m));
            await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(false));
            await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(100m)); // beyond the last row -> Rest(0)
        }

        return entryId;
    }

    private static async Task CaptureAsync(EntryId entryId, string metric, MeasuredValue value) =>
        await ApiClient.PostCommandAsync<EntryId>(Client, "/capture-measurement", new CaptureMeasurement(entryId, 1, metric, value));
}
