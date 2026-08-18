// kanban/completed/task-round-lifecycle.md WI-10 ("Acceptance") — step
// definitions for Features/ClosingACompetition.feature. Every step drives real
// HTTP against the real Soarscore.Api (AcceptanceFixture.Client), the same
// discipline CapturingAScoreSteps.cs established and ScoringACompetitionSteps.cs
// and DrawingACatalogueChoicePhaseSteps.cs follow.
//
// A self-contained [Binding] class with its own Given/When/Then phrasing:
// Reqnroll binds step regexes assembly-wide, so a regex shared verbatim across
// two Binding classes is an ambiguous match. Nothing here reuses
// ScoringACompetitionSteps' "the F5J class is published" even though the
// setup underneath is the same, for that reason alone.
//
// F5J (30-f5j) throughout, for two reasons this feature needs:
//   - literal MinPerGroup 6 (SeedF5J.cs), so a 6-pilot field draws to exactly
//     one group per round and every round has the same membership; and
//   - Validity.MinRounds == 4 (5.5.11.5 a), a literal rather than a parameter,
//     so the fifth scenario's gate is class-driven with a concrete number and
//     needs no BindParameter detour. Its drop policy needs at least 5 completed
//     rounds, so no scenario below ever trips a drop and every expected
//     aggregate is a plain sum.
//
// Flight-time convention, identical to ScoringACompetitionSteps.cs's: every
// metric but flightTime is fixed to a value contributing zero to the raw score,
// so raw == flightTime; and each competitor's flight time is constant across
// rounds (250, 300, ..., 500), so every round's winner scores 500 raw and every
// normalised round score is exactly 2 * flightTime. No repeating decimals.

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
public sealed class ClosingACompetitionSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;
    private static readonly DateTimeOffset LaunchAt = new(2026, 1, 10, 9, 3, 12, TimeSpan.Zero);

    private CompetitionId _competitionId;

    /// <summary>Flight time == raw score, constant per competitor across rounds (this file's header).</summary>
    private readonly Dictionary<CompetitorId, decimal> _flightTimeByCompetitor = [];

    /// <summary>The one competitor deliberately left unflown, so a late score has somewhere to arrive from.</summary>
    private CompetitorId _latecomer;

    private HttpResponseMessage? _refusedResponse;

    // ---------------------------------------------------------------- Given

    [Given(@"^an F5J competition is under way, with (\d+) competitors and (\d+) drawn rounds$")]
    public async Task GivenAnF5JCompetitionIsUnderWay(int competitorCount, int rounds)
    {
        var contentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(F5JDefinition));

        // Person.IsPlausibleEmail rejects whitespace, so a hyphen-free GUID
        // slug — the same discipline the other Steps classes use to keep
        // scenarios sharing one database from colliding.
        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition($"Closing Acceptance {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), contentHash));

        for (var i = 0; i < competitorCount; i++)
        {
            var email = $"pilot-closing-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
        }

        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, rounds));

        // F5J's literal MinPerGroup 6 means a 6-pilot field is exactly one
        // group, with the same membership every round. Asserted rather than
        // assumed: if the draw ever produced two groups, every expected value
        // below (normalisation is per group) would quietly change meaning.
        var group = await ResolveGroupAsync(roundOrdinal: 1);
        group.CompetitorRefs.Should().HaveCount(competitorCount);

        for (var i = 0; i < group.CompetitorRefs.Length; i++)
        {
            _flightTimeByCompetitor[group.CompetitorRefs[i]] = 250m + i * 50m; // 250 .. 500, winner 500
        }
    }

    [Given(@"^every competitor but one has flown round (\d+)$")]
    public async Task GivenEveryCompetitorButOneHasFlownRound(int roundOrdinal)
    {
        var group = await ResolveGroupAsync(roundOrdinal);

        // The latecomer is the fastest of the field (500), so once their score
        // does arrive it is unambiguously the group's winner — a late score
        // that changes the result is the case worth proving, not one that
        // quietly agrees with what was already there.
        _latecomer = _flightTimeByCompetitor.MaxBy(kv => kv.Value).Key;

        foreach (var competitorRef in group.CompetitorRefs.Where(c => c != _latecomer))
        {
            await CaptureFlightAsync(roundOrdinal, group.Id, competitorRef, _flightTimeByCompetitor[competitorRef]);
        }
    }

    [Given(@"^every competitor has flown rounds 1 and 2$")]
    public async Task GivenEveryCompetitorHasFlownRounds1And2()
    {
        await FlyRoundAsync(1);
        await FlyRoundAsync(2);
    }

    [Given(@"^every competitor has flown all (\d+) rounds$")]
    public async Task GivenEveryCompetitorHasFlownAllRounds(int rounds)
    {
        for (var roundOrdinal = 1; roundOrdinal <= rounds; roundOrdinal++)
        {
            await FlyRoundAsync(roundOrdinal);
        }
    }

    [Given(@"^the contest director closes round (\d+)$")]
    [When(@"^the contest director closes round (\d+)$")]
    public async Task CloseRound(int roundOrdinal) =>
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/complete-task-round", new CompleteTaskRound(_competitionId, 0, roundOrdinal, 1));

    [Given(@"^the contest director closes rounds 1, 2 and 3$")]
    public async Task GivenTheContestDirectorClosesRounds1To3()
    {
        foreach (var roundOrdinal in new[] { 1, 2, 3 })
        {
            await CloseRound(roundOrdinal);
        }
    }

    [Given(@"^the contest director closes all (\d+) rounds$")]
    public async Task GivenTheContestDirectorClosesAllRounds(int rounds)
    {
        for (var roundOrdinal = 1; roundOrdinal <= rounds; roundOrdinal++)
        {
            await CloseRound(roundOrdinal);
        }
    }

    // ----------------------------------------------------------------- When

    /// <summary>
    /// The governing principle at the workflow level: the field flew round 1
    /// then round 2, and the scores reach the system the other way round —
    /// every one of round 2's before any of round 1's. Nothing in the write
    /// model may care. This is the scenario that should fail loudly if a future
    /// thread ever adds sequencing.
    /// </summary>
    [When(@"^every competitor flies rounds 1 and 2, and round 2's scores are all entered before round 1's$")]
    public async Task WhenRound2ScoresAreEnteredBeforeRound1s()
    {
        await FlyRoundAsync(2);
        await FlyRoundAsync(1);
    }

    [When(@"^the contest director reopens round (\d+) and the late score is entered$")]
    public async Task WhenTheContestDirectorReopensRoundAndTheLateScoreIsEntered(int roundOrdinal)
    {
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/reopen-task-round",
            new ReopenTaskRound(_competitionId, 0, roundOrdinal, 1, "A late score arrived from the timing sheet"));

        var group = await ResolveGroupAsync(roundOrdinal);
        await CaptureFlightAsync(roundOrdinal, group.Id, _latecomer, _flightTimeByCompetitor[_latecomer]);
    }

    [When(@"^the contest director annuls round (\d+)$")]
    public async Task WhenTheContestDirectorAnnulsRound(int roundOrdinal) =>
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/annul-task-round",
            new AnnulTaskRound(_competitionId, 0, roundOrdinal, 1, "The winch failed part-way through the group"));

    [When(@"^the contest director tries to finalise the competition$")]
    public async Task WhenTheContestDirectorTriesToFinaliseTheCompetition() =>
        _refusedResponse = await ApiClient.PostCommandRawAsync(
            Client, "/finalise-competition", new FinaliseCompetition(_competitionId, "CD Jane"));

    [When(@"^the contest director finalises the competition$")]
    public async Task WhenTheContestDirectorFinalisesTheCompetition() =>
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/finalise-competition", new FinaliseCompetition(_competitionId, "CD Jane"));

    // ----------------------------------------------------------------- Then

    [Then(@"^every score is accepted, and the leaderboard counts both rounds for everyone$")]
    public async Task ThenEveryScoreIsAcceptedAndBothRoundsCount()
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        view.Scores.Should().HaveCount(_flightTimeByCompetitor.Count);

        foreach (var score in view.Scores)
        {
            // Two rounds, each normalised to 2 * flight time (winner 500), no
            // drop — so a leaderboard that had silently dropped or refused the
            // out-of-order round would land on half this.
            score.Disqualified.Should().BeFalse();
            score.Score.Should().Be(2 * (2m * _flightTimeByCompetitor[score.CompetitorRef]));
        }
    }

    [Then(@"^the last competitor's round (\d+) score is refused because the round is closed$")]
    public async Task ThenTheLastCompetitorsScoreIsRefused(int roundOrdinal)
    {
        var group = await ResolveGroupAsync(roundOrdinal);
        var response = await ApiClient.PostCommandRawAsync(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, roundOrdinal, 1, group.Id, _latecomer));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ApiClient.Options);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("openEntry.taskRoundClosed");
    }

    [Then(@"^the late score is accepted and counts towards the leaderboard$")]
    public async Task ThenTheLateScoreIsAcceptedAndCounts()
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        view.Scores.Should().HaveCount(_flightTimeByCompetitor.Count);

        // The latecomer flew the longest time of the field, so their arrival
        // makes them the group's winner on the class's normalisation target of
        // 1000 — the whole group's scores are relative to a flight that was
        // entered after the round had been closed and reopened.
        var late = view.Scores.Single(s => s.CompetitorRef == _latecomer);
        late.Disqualified.Should().BeFalse();
        late.Score.Should().Be(1000m);

        foreach (var score in view.Scores)
        {
            score.Score.Should().Be(1000m * _flightTimeByCompetitor[score.CompetitorRef] / _flightTimeByCompetitor[_latecomer]);
        }
    }

    [Then(@"^the leaderboard scores every competitor on round (\d+) alone$")]
    public async Task ThenTheLeaderboardScoresEveryCompetitorOnRoundAlone(int roundOrdinal)
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        view.Scores.Should().HaveCount(_flightTimeByCompetitor.Count);

        foreach (var score in view.Scores)
        {
            // One round's worth, not two: the annulled round's flights are all
            // still recorded and still normalise, but contribute nothing to the
            // aggregate. A pipeline that ignored annulment would double this.
            var oneRound = 2m * _flightTimeByCompetitor[score.CompetitorRef];
            score.Score.Should().Be(oneRound);
            score.Score.Should().NotBe(2 * oneRound);
        }
    }

    [Then(@"^finalisation is refused because the class requires more rounds flown to a result$")]
    public async Task ThenFinalisationIsRefusedForTooFewRounds()
    {
        _refusedResponse.Should().NotBeNull();
        _refusedResponse!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await _refusedResponse.Content.ReadFromJsonAsync<ProblemDetails>(ApiClient.Options);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("finalise.notEnoughRounds");

        // The number comes from PhaseDefinition.Validity, not from anything in
        // the core system — F5J's own 5.5.11.5 a minimum of four. That the
        // message can state it at all is what makes ValidityRule live.
        problem.Detail.Should().Contain("3 round(s) flown to a result");
        problem.Detail.Should().Contain("requires 4");
    }

    [Then(@"^the declared results match the leaderboard, competitor for competitor$")]
    public async Task ThenTheDeclaredResultsMatchTheLeaderboard()
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var finalisation = view.Competition.Finalisations.Should().ContainSingle().Subject;

        finalisation.By.Should().Be("CD Jane");
        finalisation.Revision.Should().Be(1);
        finalisation.DeclaredResults.Should().HaveCount(_flightTimeByCompetitor.Count);

        var leaderboard = await ApiClient.GetAsync<CompetitionScoreView>(
            Client, $"/competition-result?competitionRef={_competitionId.Value}");

        foreach (var declared in finalisation.DeclaredResults)
        {
            var derived = leaderboard.Scores.Single(s => s.CompetitorRef == declared.CompetitorRef);
            declared.Aggregate.Should().Be(derived.Score);
            declared.Placing.Should().Be(derived.Placing);
        }

        // Four rounds at 2 * flight time each, no drop (F5J's gate needs 5).
        foreach (var declared in finalisation.DeclaredResults)
        {
            declared.Aggregate.Should().Be(4 * (2m * _flightTimeByCompetitor[declared.CompetitorRef]));
        }
    }

    [Then(@"^the competition is listed as finalised$")]
    public async Task ThenTheCompetitionIsListedAsFinalised()
    {
        var rows = await ApiClient.GetAsync<List<CompetitionSummary>>(Client, "/competitions");
        rows.Single(c => c.Id == _competitionId).State.Should().Be("finalised");
    }

    // --------------------------------------------------------------- helpers

    /// <summary>The one group drawn into the round's one task-round — see this file's header.</summary>
    private async Task<Group> ResolveGroupAsync(int roundOrdinal)
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var round = view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == roundOrdinal);
        return round.TaskRounds.Single().Groups.Single();
    }

    private async Task FlyRoundAsync(int roundOrdinal)
    {
        var group = await ResolveGroupAsync(roundOrdinal);

        foreach (var competitorRef in group.CompetitorRefs)
        {
            await CaptureFlightAsync(roundOrdinal, group.Id, competitorRef, _flightTimeByCompetitor[competitorRef]);
        }
    }

    /// <summary>
    /// Opens an Entry, opens its one flight, and captures every metric F5J's
    /// task D references. Every metric but flightTime is fixed to a value that
    /// contributes zero to the raw score (this file's header), so raw score ==
    /// flightTime.
    /// </summary>
    private async Task CaptureFlightAsync(int roundOrdinal, GroupId groupRef, CompetitorId competitorRef, decimal flightTime)
    {
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, roundOrdinal, 1, groupRef, competitorRef));

        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));

        await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(flightTime));
        await CaptureAsync(entryId, "startHeight", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "startHeightRecorded", MeasuredValue.Of(true));
        await CaptureAsync(entryId, "overflySeconds", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(false));
        await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(100m)); // beyond the last row -> Rest(0)
    }

    private static async Task CaptureAsync(EntryId entryId, string metric, MeasuredValue value) =>
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/capture-measurement", new CaptureMeasurement(entryId, 1, metric, value));
}
