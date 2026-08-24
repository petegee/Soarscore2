// kanban/in-progress/reflight-scoring-rulings.md WI-7 ("Acceptance") — step
// definitions for Features/RecordingAReflightRuling.feature. Every step drives
// real HTTP against the real Soarscore.Api (AcceptanceFixture.Client), the
// same discipline ReflightingAGroupSteps.cs established.
//
// A self-contained [Binding] class with its own Given/When/Then phrasing:
// Reqnroll binds step regexes assembly-wide, so a regex shared verbatim across
// two Binding classes is an ambiguous match. Nothing here reuses another Steps
// class's phrasing.
//
// NZ Class M ALES 200 (80-nz-m-ales200) throughout — the story's named class,
// whose rulebook is silent on BOTH reflight slots (NZ.3.12.5 l grants the
// re-flight and stops). Its task D metrics are flightTime, landingDistance,
// damagedAndNotSafelyFlyable, touchedByCompetitor and landedWithin75m; raw ==
// flightTime below the 600 s target time, and landingDistance of 100 m falls
// past the landing table's last row for zero landing points — so every
// normalised score is exactly 1000 * raw / winnerRaw with no rounding and no
// repeating fractions.
//
// Numbers: the original group flies 300/270/240/210 s (normalised
// 1000/900/800/700 against a 300 s winner). The appended reflight group flies
// to either a 200 s winner (150 s → 750) or, in the filler scenario, the
// winner re-flies best at 200 s while the filler's 150 s normalises to 750.

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
public sealed class RecordingAReflightRulingSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private static readonly ClassDefinition NzMAles200Definition = Corpus.All.Single(c => c.FileName == "80-nz-m-ales200").Definition;

    private CompetitionId _competitionId;
    private readonly List<CompetitorId> _competitors = [];

    /// <summary>The round winner (first member) and the filler (second), in that order.</summary>
    private List<CompetitorId> _reflightMembers = [];

    // Populated by the pre-ruling leaderboard request, read by its refusal Then.
    private HttpResponseMessage? _leaderboardResponse;

    // Populated by each refusing When step, read by its Then.
    private HttpResponseMessage? _refusedRuling;

    // ---------------------------------------------------------------- Given

    [Given(@"^an NZ Class M ALES 200 competition of (\d+) competitors drawn for one round$")]
    public async Task GivenAnNzClassMCompetitionOfCompetitorsDrawnForOneRound(int competitorCount)
    {
        var contentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(NzMAles200Definition));

        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition($"Reflight Ruling {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), contentHash));

        for (var i = 0; i < competitorCount; i++)
        {
            var email = $"pilot-ruling-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }

        // NZ Class M leaves groupSize and minNewGroup to the CD: both must be
        // bound before their consumers run (the draw resolves MinPerGroup from
        // groupSize; AppendReflightGroup resolves its minimum from minNewGroup).
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/bind-parameter",
            new BindParameter(_competitionId, "groupSize", MeasuredValue.Of(4m), "The contest director"));
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/bind-parameter",
            new BindParameter(_competitionId, "minNewGroup", MeasuredValue.Of(2m), "The contest director"));

        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, 1));
    }

    [Given(@"^every competitor flies the original group at their seeded pace$")]
    public async Task GivenEveryCompetitorFliesTheOriginalGroupAtTheirSeededPace()
    {
        // Winner 300 s; every time divides 300 exactly, so every normalised
        // score is the whole number 1000 * raw / 300. Raw == flightTime below
        // the class's 600 s target time (this file's header).
        var times = new decimal[] { 300m, 270m, 240m, 210m };

        var originalGroup = await ResolveOriginalGroupAsync();
        for (var i = 0; i < originalGroup.CompetitorRefs.Length; i++)
        {
            await CaptureFlightAsync(originalGroup.Id, _competitors[i], times[i], ReflightRole.Original);
        }
    }

    [Given(@"^a reflight group holds the round winner and one filler, and both fly again with the winner worse$")]
    public async Task GivenAReflightGroupHoldsTheWinnerAndOneFillerAndBothFlyAgainWithTheWinnerWorse()
    {
        // The winner re-flies 150 s into a 200 s group: their re-flight
        // normalises to 1000 * 150 / 200 = 750, WORSE than their 1000 original,
        // while the filler's 200 s tops the group at 1000.
        await AppendReflightGroupAndFlyAsync(winnerTime: 150m, fillerTime: 200m);
    }

    [Given(@"^a reflight group holds the round winner and one filler, and both fly again with the winner better$")]
    public async Task GivenAReflightGroupHoldsTheWinnerAndOneFillerAndBothFlyAgainWithTheWinnerBetter()
    {
        // The winner's 200 s re-flight tops its group (normalised 1000); the
        // filler's 150 s normalises to 750 — worse than their 900 original,
        // which is exactly what the filler's BetterOf ruling must see past.
        await AppendReflightGroupAndFlyAsync(winnerTime: 200m, fillerTime: 150m);
    }

    // ----------------------------------------------------------------- When

    [When(@"^the leaderboard is requested$")]
    public async Task WhenTheLeaderboardIsRequested()
    {
        _leaderboardResponse = await Client.GetAsync($"/competition-result?competitionRef={_competitionId.Value}");
    }

    [When(@"^the CD rules the winner's re-flight counts outright and the filler takes the better of their two attempts$")]
    public async Task WhenTheCDRulesReplacementForWinnerAndBetterOfForFiller()
    {
        await RecordRulingAsync(_reflightMembers[0], ReflightSelection.Replacement);
        await RecordRulingAsync(_reflightMembers[1], ReflightSelection.BetterOf);
    }

    [When(@"^the CD rules both re-flights by role: Replacement for the winner, BetterOf for the filler$")]
    public async Task WhenTheCDRulesBothReflightsByRole()
    {
        await RecordRulingAsync(_reflightMembers[0], ReflightSelection.Replacement);
        await RecordRulingAsync(_reflightMembers[1], ReflightSelection.BetterOf);
    }

    [When(@"^the CD records a BetterOf ruling for the round winner and the filler$")]
    public async Task WhenTheCDRecordsABetterOfRulingForWinnerAndFiller()
    {
        await RecordRulingAsync(_reflightMembers[0], ReflightSelection.BetterOf);
        await RecordRulingAsync(_reflightMembers[1], ReflightSelection.BetterOf);
    }

    [When(@"^the CD supersedes the winner's ruling with Replacement$")]
    public async Task WhenTheCDSupersedesTheWinnersRulingWithReplacement() =>
        await RecordRulingAsync(_reflightMembers[0], ReflightSelection.Replacement);

    [When(@"^the CD attempts to record a NotPermitted selection as a ruling$")]
    public async Task WhenTheCDAttemptsToRecordANotPermittedSelectionAsARuling() =>
        _refusedRuling = await PostRulingRawAsync(_competitors[0], ReflightSelection.NotPermitted, "No re-flight was ever permitted here");

    [When(@"^the CD attempts to record a ruling for an unregistered competitor$")]
    public async Task WhenTheCDAttemptsToRecordARulingForAnUnregisteredCompetitor() =>
        _refusedRuling = await PostRulingRawAsync(CompetitorId.New(), ReflightSelection.Replacement, "Typo protection probe");

    [When(@"^the CD attempts to record a ruling with a blank reason$")]
    public async Task WhenTheCDAttemptsToRecordARulingWithABlankReason() =>
        _refusedRuling = await PostRulingRawAsync(_competitors[0], ReflightSelection.Replacement, "   ");

    // ----------------------------------------------------------------- Then

    [Then(@"^the leaderboard request is refused with (.+)$")]
    public async Task ThenTheLeaderboardRequestIsRefusedWith(string code)
    {
        _leaderboardResponse!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = System.Text.Json.JsonDocument.Parse(await _leaderboardResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("title").GetString().Should().Be(code);
    }

    [Then(@"^the ruling is refused with (.+)$")]
    public async Task ThenTheRulingIsRefusedWith(string code)
    {
        _refusedRuling.Should().NotBeNull();
        _refusedRuling!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await _refusedRuling.Content.ReadFromJsonAsync<ProblemDetails>(ApiClient.Options);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be(code);
    }

    [Then(@"^the leaderboard computes$")]
    public async Task ThenTheLeaderboardComputes()
    {
        // A fresh request, not the stored one: the point is that scoring now
        // computes where, before the ruling, it refused.
        var response = await Client.GetAsync($"/competition-result?competitionRef={_competitionId.Value}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Then(@"^the (.+) scores exactly (\d+)$")]
    public async Task ThenTheNamedCompetitorScoresExactly(string who, int expected)
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        var competitor = who switch
        {
            "round winner" => _reflightMembers[0],
            "filler" => _reflightMembers[1],
            _ => throw new ArgumentException($"Unknown role '{who}'."),
        };

        var score = view.Scores.Single(s => s.CompetitorRef == competitor);
        score.Disqualified.Should().BeFalse();
        score.Score.Should().Be(expected);
    }

    // --------------------------------------------------------------- helpers

    /// <summary>
    /// Appends the reflight group (winner first — the Entitled role) and
    /// captures both members' re-flights into it.
    /// </summary>
    private async Task AppendReflightGroupAndFlyAsync(decimal winnerTime, decimal fillerTime)
    {
        var originalGroup = await ResolveOriginalGroupAsync();

        _reflightMembers = [_competitors[0], _competitors[1]];
        var reflightGroupId = await ApiClient.PostCommandAsync<GroupId>(
            Client,
            "/append-reflight-group",
            new AppendReflightGroup(_competitionId, 0, 1, 1, _reflightMembers, "Mid-air collision"));

        // Both entries are opened against the REFLIGHT group — normalisation is
        // per group, so the pair's scores come from the re-flight group's own
        // winner (this file's header numbers).
        await CaptureFlightAsync(reflightGroupId, _reflightMembers[0], winnerTime, ReflightRole.Entitled);
        await CaptureFlightAsync(reflightGroupId, _reflightMembers[1], fillerTime, ReflightRole.Filler);
    }

    private async Task RecordRulingAsync(CompetitorId competitor, ReflightSelection selection)
    {
        var response = await PostRulingRawAsync(
            competitor, selection, $"Timing failure unresolved by the rulebook ({Guid.NewGuid():N})");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> PostRulingRawAsync(
        CompetitorId competitor, ReflightSelection selection, string reason) =>
        ApiClient.PostCommandRawAsync(
            Client,
            "/record-reflight-ruling",
            new RecordReflightRuling(_competitionId, 0, 1, 1, competitor, selection, reason, "the contest director"));

    private async Task<Group> ResolveOriginalGroupAsync()
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        return view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == 1).TaskRounds.Single().Groups.Single(g => g.Ordinal == 1);
    }

    /// <summary>
    /// Opens an entry (with the given role), opens its one flight, and captures
    /// every metric NZ Class M's task D references — raw == flightTime below
    /// the 600 s target, landing points zero past the table's last row.
    /// </summary>
    private async Task<EntryId> CaptureFlightAsync(GroupId groupRef, CompetitorId competitorRef, decimal flightTime, ReflightRole role)
    {
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, 1, 1, groupRef, competitorRef, role));

        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));

        await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(flightTime));
        await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(100m));
        await CaptureAsync(entryId, "damagedAndNotSafelyFlyable", MeasuredValue.Of(false));
        await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(false));
        await CaptureAsync(entryId, "landedWithin75m", MeasuredValue.Of(true));

        return entryId;
    }

    private static async Task CaptureAsync(EntryId entryId, string metric, MeasuredValue value) =>
        await ApiClient.PostCommandAsync<EntryId>(Client, "/capture-measurement", new CaptureMeasurement(entryId, 1, metric, value));
}
