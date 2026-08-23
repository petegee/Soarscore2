// kanban/in-progress/reflight-groups.md WI-10 ("Acceptance") — step definitions
// for Features/ReflightingAGroup.feature. Every step drives real HTTP against
// the real Soarscore.Api (AcceptanceFixture.Client), the same discipline
// ClosingACompetitionSteps.cs established.
//
// A self-contained [Binding] class with its own Given/When/Then phrasing:
// Reqnroll binds step regexes assembly-wide, so a regex shared verbatim across
// two Binding classes is an ambiguous match (ClosingACompetitionSteps.cs's own
// header, lines 7-11). Nothing here reuses another Steps class's phrasing.
//
// F3K (10-f3k) throughout. The class is drawn as a catalogue-choice phase
// naming its tasks via a Gherkin table, exactly like
// Features/DrawingACatalogueChoicePhase.feature's own pattern. F3K.9.6 is the
// reflight rule under test (Replacement for the entitled, BetterOf for fillers,
// a new group of at least 4). Task A's metrics are flightTime (truncated to
// 0.1 s, raw == flightTime below its 300 s cap), landedWithinWindow and
// launchedInWorkingTime (flags that must be true), and its normalisation is
// WinnerScore 1000, HalfUp to 0.1 (F3K.9.1) — so the flight times below are
// chosen to make every normalised expectation exact decimal math, never a
// repeating fraction.
//
// The original group is scored with a winner at 300 s, and every competitor's
// flight time divides 300 exactly (300, 270, 240, 210, 180, 150), so each
// normalised score is 1000 * raw / 300 — all whole numbers. The reflight group
// flies to a 240 s winner, and its times (150, 240, 210, 180) likewise divide
// 240 exactly (625, 1000, 875, 750).

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
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class ReflightingAGroupSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private static readonly ClassDefinition F3KDefinition = Corpus.All.Single(c => c.FileName == "10-f3k").Definition;

    private CompetitionId _competitionId;
    private readonly List<CompetitorId> _competitors = [];

    /// <summary>Flight time == raw score for the ORIGINAL group, per competitor.</summary>
    private readonly Dictionary<CompetitorId, decimal> _originalTimeByCompetitor = [];

    /// <summary>The members of the appended reflight group, in order (entitled first).</summary>
    private List<CompetitorId> _reflightMembers = [];

    /// <summary>Flight time == raw score for the REFLIGHT group, per member.</summary>
    private readonly Dictionary<CompetitorId, decimal> _reflightTimeByCompetitor = [];

    private GroupId _reflightGroupId;

    private HttpResponseMessage? _refusedAppend;

    // ---------------------------------------------------------------- Given

    [Given(@"^an F3K competition of (\d+) competitors has a preliminary round drawn for task A$")]
    public async Task GivenAnF3KCompetitionHasAPreliminaryRoundDrawnForTaskA(int competitorCount)
    {
        var contentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(F3KDefinition));

        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition($"Reflight Acceptance {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), contentHash));

        for (var i = 0; i < competitorCount; i++)
        {
            var email = $"pilot-reflight-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }

        // F3K is a catalogue-choice phase: the CD names the task for the round
        // as part of the draw itself (DrawingACatalogueChoicePhase.feature).
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/draw-phase", new DrawPhase(_competitionId, 1, ["A"]));
    }

    [Given(@"^every competitor flies the original group with a distinct flight time$")]
    public async Task GivenEveryCompetitorFliesTheOriginalGroupWithADistinctFlightTime()
    {
        var group = await ResolveOriginalGroupAsync();

        // Winner at 300 s; every time divides 300 exactly, so every normalised
        // score is the whole number 1000 * raw / 300. F3K's cap is 300, so raw
        // == flightTime throughout (this file's header).
        var times = new decimal[] { 300m, 270m, 240m, 210m, 180m, 150m };

        for (var i = 0; i < group.CompetitorRefs.Length; i++)
        {
            var flightTime = times[i];
            _originalTimeByCompetitor[group.CompetitorRefs[i]] = flightTime;
            await CaptureFlightAsync(roundOrdinal: 1, groupRef: group.Id, competitorRef: group.CompetitorRefs[i], flightTime);
        }
    }

    [Given(@"^the CD appends a reflight group holding the entitled competitor and (\d+) fillers$")]
    public async Task GivenTheCDAppendsAReflightGroupHoldingTheEntitledCompetitorAndFillers(int fillerCount)
    {
        var group = await ResolveOriginalGroupAsync();

        // The entitled competitor is the group's winner and the first of the
        // field; the fillers are the next <fillerCount> pilots.
        var entitled = group.CompetitorRefs.OrderByDescending(c => _originalTimeByCompetitor[c]).First();
        _reflightMembers = new List<CompetitorId> { entitled };
        _reflightMembers.AddRange(group.CompetitorRefs.Where(c => c != entitled).Take(fillerCount));

        _reflightGroupId = await ApiClient.PostCommandAsync<GroupId>(
            Client,
            "/append-reflight-group",
            new AppendReflightGroup(_competitionId, 0, 1, 1, _reflightMembers, "Mid-air collision"));
    }

    [Given(@"^the entitled competitor's re-flight is worse than their original, and the fillers fly again$")]
    public async Task GivenTheEntitledCompetitorsReflightIsWorseAndTheFillersFlyAgain()
    {
        // The reflight group flies to a 240 s winner; every time divides 240
        // exactly, so every normalised score is the exact 1000 * raw / 240. The
        // entitled competitor's 150 s re-flight is worse than their 300 s
        // original — the governing principle is that it still counts.
        var times = new decimal[] { 150m, 240m, 210m, 180m };
        for (var i = 0; i < _reflightMembers.Count; i++)
        {
            _reflightTimeByCompetitor[_reflightMembers[i]] = times[i];
            var role = i == 0 ? ReflightRole.Entitled : ReflightRole.Filler;
            await CaptureFlightAsync(1, _reflightGroupId, _reflightMembers[i], times[i], role);
        }
    }

    [Given(@"^the contest director completes the task-round$")]
    public async Task GivenTheContestDirectorCompletesTheTaskRound() =>
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/complete-task-round", new CompleteTaskRound(_competitionId, 0, 1, 1));

    // ----------------------------------------------------------------- When

    [When(@"^the entitled competitor opens an entitled re-flight into the original group, flying worse$")]
    public async Task WhenTheEntitledCompetitorOpensAnEntitledReflightIntoTheOriginalGroupFlyingWorse()
    {
        // Priority (c): the entitled competitor re-flies with their ORIGINAL
        // group. Their Entitled entry is opened against the same GroupId; at
        // 150 s it is worse than their 300 s original but, under F3K.9.6's
        // Replacement rule, is the official score.
        var group = await ResolveOriginalGroupAsync();
        var entitled = group.CompetitorRefs.OrderByDescending(c => _originalTimeByCompetitor[c]).First();

        await CaptureFlightAsync(1, group.Id, entitled, 150m, ReflightRole.Entitled);
        _reflightTimeByCompetitor[entitled] = 150m;
    }

    [When(@"^the CD attempts to append a reflight group of only (\d+) members$")]
    public async Task WhenTheCDAttemptsToAppendAReflightGroupOfOnlyMembers(int memberCount)
    {
        _refusedAppend = await ApiClient.PostCommandRawAsync(
            Client,
            "/append-reflight-group",
            new AppendReflightGroup(_competitionId, 0, 1, 1, _competitors.Take(memberCount).ToList(), "Mid-air collision"));
    }

    [When(@"^the contest director appends a reflight group of the entitled competitor and (\d+) fillers$")]
    public async Task WhenTheContestDirectorAppendsAReflightGroupOfTheEntitledCompetitorAndFillers(int fillerCount)
    {
        var group = await ResolveOriginalGroupAsync();
        var entitled = group.CompetitorRefs.OrderByDescending(c => _originalTimeByCompetitor[c]).First();
        var members = new List<CompetitorId> { entitled };
        members.AddRange(group.CompetitorRefs.Where(c => c != entitled).Take(fillerCount));

        _reflightGroupId = await ApiClient.PostCommandAsync<GroupId>(
            Client,
            "/append-reflight-group",
            new AppendReflightGroup(_competitionId, 0, 1, 1, members, "Mid-air collision"));
    }

    // ----------------------------------------------------------------- Then

    [Then(@"^the entitled competitor is scored on their re-flight, not their better original$")]
    public async Task ThenTheEntitledCompetitorIsScoredOnTheirReflightNotTheirBetterOriginal()
    {
        var view = await LeaderboardAsync();
        var entitled = _reflightMembers[0];

        var score = view.Scores.Single(s => s.CompetitorRef == entitled);
        score.Disqualified.Should().BeFalse();

        // EntitledScores is Replacement (F3K.9.6): the re-flight's normalised
        // score of 625 (1000 * 150 / 240) is official even though the original
        // scored a better 1000 (1000 * 300 / 300).
        score.Score.Should().Be(625m);
        score.Score.Should().NotBe(1000m);
    }

    [Then(@"^the filler is scored on the better of their two normalised scores$")]
    public async Task ThenTheFillerIsScoredOnTheBetterOfTheirTwoNormalisedScores()
    {
        var view = await LeaderboardAsync();
        var filler = _reflightMembers[1];

        var score = view.Scores.Single(s => s.CompetitorRef == filler);
        score.Disqualified.Should().BeFalse();

        // OthersScore is BetterOf (F3K.9.6). For the first filler: original at
        // 270 → 900, re-flight at 240 → 1000 — the re-flight is the better.
        score.Score.Should().Be(1000m);
    }

    [Then(@"^a competitor outside the reflight group keeps their original score$")]
    public async Task ThenACompetitorOutsideTheReflightGroupKeepsTheirOriginalScore()
    {
        var view = await LeaderboardAsync();

        // A competitor not in the reflight group (R3): their score is exactly
        // the original group's single normalisation of their flight time
        // (winner 300 → 1000 * raw / 300).
        var untouched = _competitors.First(c => !_reflightMembers.Contains(c));
        var expected = 1000m * _originalTimeByCompetitor[untouched] / 300m;

        var score = view.Scores.Single(s => s.CompetitorRef == untouched);
        score.Disqualified.Should().BeFalse();
        score.Score.Should().Be(expected);
    }

    [Then(@"^the entitled competitor's leaderboard score equals their re-flight's normalised score$")]
    public async Task ThenTheEntitledCompetitorsLeaderboardScoreEqualsTheirReflightsNormalisedScore()
    {
        var view = await LeaderboardAsync();
        var entitled = _reflightTimeByCompetitor.Keys.Single(c => _originalTimeByCompetitor.ContainsKey(c) && _reflightTimeByCompetitor[c] == 150m);

        var score = view.Scores.Single(s => s.CompetitorRef == entitled);
        score.Disqualified.Should().BeFalse();

        // Priority (c): both entries in the original group (winner 300). The
        // Entitled re-flight normalises to 1000 * 150 / 300 = 500, and that is
        // what Replacement keeps.
        score.Score.Should().Be(500m);
    }

    [Then(@"^the append is refused because the group is below the class's minimum of (\d+)$")]
    public async Task ThenTheAppendIsRefusedBecauseTheGroupIsBelowTheClasssMinimum(int minimum)
    {
        _refusedAppend.Should().NotBeNull();
        _refusedAppend!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await _refusedAppend.Content.ReadFromJsonAsync<ProblemDetails>(ApiClient.Options);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("appendReflightGroup.groupTooSmall");
        problem.Detail.Should().Contain($"at least {minimum} members");
    }

    [Then(@"^the competition shows the appended reflight group$")]
    public async Task ThenTheCompetitionShowsTheAppendedReflightGroup()
    {
        // The protest shape is now real (entry-completeness-indicator.md's fourth
        // reason): a task-round the CD has closed can still gain a reflight group.
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var taskRound = view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == 1).TaskRounds.Single();

        taskRound.State.Should().Be(Soarscore.Domain.Competitions.TaskRoundState.Complete);
        taskRound.Groups.Should().Contain(g => g.Id == _reflightGroupId && g.Ordinal == 2);
    }

    // --------------------------------------------------------------- helpers

    private async Task<Group> ResolveOriginalGroupAsync()
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        return view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == 1).TaskRounds.Single().Groups.Single(g => g.Ordinal == 1);
    }

    private async Task<CompetitionScoreView> LeaderboardAsync() =>
        await ApiClient.GetAsync<CompetitionScoreView>(Client, $"/competition-result?competitionRef={_competitionId.Value}");

    /// <summary>Opens an Entry, opens its one flight, and captures every metric F3K's task A references (raw == flightTime below the 300 s cap).</summary>
    private async Task<EntryId> CaptureFlightAsync(
        int roundOrdinal, GroupId groupRef, CompetitorId competitorRef, decimal flightTime,
        ReflightRole role = ReflightRole.Original)
    {
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, roundOrdinal, 1, groupRef, competitorRef, role));

        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));

        await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(flightTime));
        await CaptureAsync(entryId, "landedWithinWindow", MeasuredValue.Of(true));
        await CaptureAsync(entryId, "launchedInWorkingTime", MeasuredValue.Of(true));

        return entryId;
    }

    private static async Task CaptureAsync(EntryId entryId, string metric, MeasuredValue value) =>
        await ApiClient.PostCommandAsync<EntryId>(Client, "/capture-measurement", new CaptureMeasurement(entryId, 1, metric, value));
}