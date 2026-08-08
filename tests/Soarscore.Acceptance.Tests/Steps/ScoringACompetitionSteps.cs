// docs/plans/scoring-steel-thread-plan.md WI-10 — step definitions for
// Features/ScoringACompetition.feature. Every step drives real HTTP against
// the real Soarscore.Api (AcceptanceFixture.Client), the same discipline
// CapturingAScoreSteps.cs (WI-13 of the earlier plan) established.
//
// A self-contained Steps class per feature file, exactly that earlier
// precedent's shape: Reqnroll binds step regexes assembly-wide, and its own
// scenario state lives in plain private fields (not ScenarioContext), so two
// [Binding] classes sharing an identical regex would collide as an ambiguous
// match. This class deliberately phrases its Given steps differently from
// CapturingAScoreSteps' ("the F5J class is published" vs. "a published F5J
// class definition") rather than attempting to share state across classes.
//
// F5J only (30-f5j) — literal MinPerGroup 6, so a 6-competitor field always
// draws to exactly one group per round, and a real Normalise block
// (WinnerScore 1000, HigherIsBetter, no rounding). Every flight fixes
// startHeight/landingDistance/overflySeconds/touchedByCompetitor to values
// that contribute zero to the raw score (see ScoringEventStoreTests.cs's
// header, WI-9, for the identical convention), so raw score == flightTime
// exactly and every expected value below is computed from that one fact.

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Application.Competitions;
using Soarscore.Application.Entries;
using Soarscore.Application.People;
using Soarscore.Application.Scoring;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class ScoringACompetitionSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;
    private static readonly DateTimeOffset LaunchAt = new(2026, 1, 10, 9, 3, 12, TimeSpan.Zero);

    private string? _classContentHash;
    private CompetitionId _competitionId;
    private readonly List<CompetitorId> _competitors = [];

    // Populated by scenario 1's When step, read by its Then steps.
    private readonly Dictionary<CompetitorId, decimal> _scenario1FlightTimes = new();

    // Populated by scenario 2's When step, read by its Then step.
    private CompetitorId _competitor1;
    private decimal _expectedWithDrop;
    private decimal _expectedWithoutDrop;

    // Populated by scenario 3's When step, read by its Then step.
    private readonly Dictionary<CompetitorId, decimal> _scenario3ExpectedTotals = new();

    // ---------------------------------------------------------------- Given

    [Given(@"^the F5J class is published$")]
    public async Task GivenTheF5JClassIsPublished()
    {
        _classContentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(F5JDefinition));
    }

    [Given(@"^a competition is created adopting it, with (\d+) registered competitors$")]
    public async Task GivenACompetitionIsCreatedAdoptingItWithRegisteredCompetitors(int count)
    {
        // Person.IsPlausibleEmail rejects whitespace (Person.cs), same
        // discipline CapturingAScoreSteps.cs's own slug uses.
        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition($"Scoring Acceptance {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), _classContentHash!));

        for (var i = 0; i < count; i++)
        {
            var email = $"pilot-scoring-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
        }
    }

    [Given(@"^the preliminary phase is drawn for (\d+) rounds?$")]
    public async Task GivenThePreliminaryPhaseIsDrawnForRounds(int rounds)
    {
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, rounds));
    }

    // ----------------------------------------------------------------- When

    [When(@"^every competitor in round 1 flies with a distinct flight time$")]
    public async Task WhenEveryCompetitorInRound1FliesWithADistinctFlightTime()
    {
        var group = await ResolveGroupAsync(roundOrdinal: 1);
        group.CompetitorRefs.Should().HaveCount(6);

        // Distinct, ascending — an unambiguous single winner (the highest).
        for (var i = 0; i < group.CompetitorRefs.Length; i++)
        {
            var flightTime = 350m + i * 50m; // 350, 400, 450, 500, 550, 600
            _scenario1FlightTimes[group.CompetitorRefs[i]] = flightTime;
            await CaptureFlightAsync(1, group.Id, group.CompetitorRefs[i], flightTime);
        }
    }

    [When(@"^every competitor flies every round, competitor 1 flying a deliberately short flight time in round 3$")]
    public async Task WhenEveryCompetitorFliesEveryRoundCompetitor1FlyingAShortFlightTimeInRound3()
    {
        var group = await ResolveGroupAsync(roundOrdinal: 1); // membership is the same 6 people every round (one group, 6-pilot field)
        group.CompetitorRefs.Should().HaveCount(6);

        // Skill constant per competitor across rounds (raw == flightTime, this
        // file's header), except competitor 1's deliberately low round 3 —
        // 100, well below their own normal 250, so round 3 is unambiguously
        // their worst round. Winner is always competitor index 5 (500,
        // constant), so every round's winnerRaw is 500 and normalised score
        // is exactly 2 * flightTime — no repeating decimals to compare.
        var skillByCompetitor = group.CompetitorRefs
            .Select((c, i) => (c, skill: 250m + i * 50m)) // 250, 300, 350, 400, 450, 500
            .ToDictionary(x => x.c, x => x.skill);

        _competitor1 = group.CompetitorRefs[0];
        const decimal shortFlightTime = 100m;

        for (var roundOrdinal = 1; roundOrdinal <= 5; roundOrdinal++)
        {
            var roundGroup = roundOrdinal == 1 ? group : await ResolveGroupAsync(roundOrdinal);

            foreach (var competitorRef in roundGroup.CompetitorRefs)
            {
                var flightTime = (roundOrdinal == 3 && competitorRef == _competitor1)
                    ? shortFlightTime
                    : skillByCompetitor[competitorRef];

                await CaptureFlightAsync(roundOrdinal, roundGroup.Id, competitorRef, flightTime);
            }
        }

        // competitor 1's 5 normalised round scores: 500, 500, 200, 500, 500
        // (round 3 is 2 * 100). Without the drop: sum of all 5. With the
        // drop (F5J: ByRound, DropCount 1, ApplyWhenRoundsCompletedAtLeast 5
        // — SeedF5J.cs — exactly met by 5 flown rounds): the lowest round
        // (round 3's 200) is removed.
        _expectedWithoutDrop = (4 * (2m * skillByCompetitor[_competitor1])) + (2m * shortFlightTime);
        _expectedWithDrop = 4 * (2m * skillByCompetitor[_competitor1]);
    }

    [When(@"^every competitor flies rounds 1 and 2, and nobody flies round 3$")]
    public async Task WhenEveryCompetitorFliesRounds1And2AndNobodyFliesRound3()
    {
        var group = await ResolveGroupAsync(roundOrdinal: 1);
        group.CompetitorRefs.Should().HaveCount(6);

        // Skill constant per competitor across the two rounds actually flown
        // — winner is always competitor index 5 (500), so normalised == 2 *
        // flightTime exactly, same reasoning as the drop scenario above.
        // Round 3 is drawn (it exists in the competition's structure) but no
        // Entry is ever opened for it, for anyone — finding 5's case.
        var skillByCompetitor = group.CompetitorRefs
            .Select((c, i) => (c, skill: 250m + i * 50m))
            .ToDictionary(x => x.c, x => x.skill);

        foreach (var roundOrdinal in new[] { 1, 2 })
        {
            var roundGroup = roundOrdinal == 1 ? group : await ResolveGroupAsync(roundOrdinal);

            foreach (var competitorRef in roundGroup.CompetitorRefs)
            {
                await CaptureFlightAsync(roundOrdinal, roundGroup.Id, competitorRef, skillByCompetitor[competitorRef]);
            }
        }

        foreach (var (competitorRef, skill) in skillByCompetitor)
        {
            // Two rounds flown, each normalised to 2 * skill, no drop (only
            // 2 completed rounds — F5J's drop gate needs at least 5).
            _scenario3ExpectedTotals[competitorRef] = 2 * (2m * skill);
        }
    }

    // ----------------------------------------------------------------- Then

    [Then(@"^the task-round result for round 1 holds a normalised score for all 6 competitors$")]
    public async Task ThenTheTaskRoundResultForRound1HoldsANormalisedScoreForAll6Competitors()
    {
        var group = await ResolveGroupAsync(roundOrdinal: 1);
        var url = $"/task-round-result?competitionRef={_competitionId.Value}&phaseOrdinal=0&roundOrdinal=1&taskRoundOrdinal=1&groupRef={group.Id.Value}";
        var views = await ApiClient.GetAsync<List<GroupScoreView>>(Client, url);

        views.Should().ContainSingle();
        var view = views[0];
        view.GroupRef.Should().Be(group.Id);
        view.ValidCount.Should().Be(6);
        view.IsAnnulled.Should().BeFalse();
        view.Results.Should().HaveCount(6);

        var winnerFlightTime = _scenario1FlightTimes.Values.Max();
        foreach (var result in view.Results)
        {
            var expected = 1000m * _scenario1FlightTimes[result.CompetitorRef] / winnerFlightTime;
            result.RawScore.Should().Be(expected);
        }
    }

    [Then(@"^the competitor with the longest flight time is the sole winner with the class's normalisation target of 1000$")]
    public async Task ThenTheCompetitorWithTheLongestFlightTimeIsTheSoleWinnerWithTheNormalisationTargetOf1000()
    {
        var group = await ResolveGroupAsync(roundOrdinal: 1);
        var url = $"/task-round-result?competitionRef={_competitionId.Value}&phaseOrdinal=0&roundOrdinal=1&taskRoundOrdinal=1&groupRef={group.Id.Value}";
        var views = await ApiClient.GetAsync<List<GroupScoreView>>(Client, url);
        var view = views.Single();

        var winner = _scenario1FlightTimes.MaxBy(kv => kv.Value).Key;
        view.WinnerRef.Should().Be(winner);
        view.Results.Count(r => r.RawScore == 1000m).Should().Be(1);
        view.Results.Single(r => r.CompetitorRef == winner).RawScore.Should().Be(1000m);
    }

    [Then(@"^the competition leaderboard excludes competitor 1's round 3 score from their final aggregate$")]
    public async Task ThenTheCompetitionLeaderboardExcludesCompetitor1sRound3ScoreFromTheirFinalAggregate()
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(Client, $"/competition-result?competitionRef={_competitionId.Value}");
        var score = view.Scores.Single(s => s.CompetitorRef == _competitor1);

        score.Disqualified.Should().BeFalse();
        score.Score.Should().Be(_expectedWithDrop);
        score.Score.Should().NotBe(_expectedWithoutDrop);
    }

    [Then(@"^the competition leaderboard scores every competitor as the sum of rounds 1 and 2 only$")]
    public async Task ThenTheCompetitionLeaderboardScoresEveryCompetitorAsTheSumOfRounds1And2Only()
    {
        var view = await ApiClient.GetAsync<CompetitionScoreView>(Client, $"/competition-result?competitionRef={_competitionId.Value}");
        view.Scores.Should().HaveCount(6);

        foreach (var score in view.Scores)
        {
            score.Disqualified.Should().BeFalse();
            score.Score.Should().Be(_scenario3ExpectedTotals[score.CompetitorRef]);
        }
    }

    // --------------------------------------------------------------- helpers

    private async Task<Group> ResolveGroupAsync(int roundOrdinal)
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var phase = view.Competition.Phases.Single();
        var round = phase.Rounds.Single(r => r.Ordinal == roundOrdinal);
        return round.TaskRounds.Single().Groups.Single(g => g.Ordinal == 1);
    }

    /// <summary>
    /// Opens an Entry, opens its one flight, and captures every metric F5J's
    /// task D references. Every metric but flightTime is fixed to a value
    /// that contributes zero to the raw score (this file's header), so raw
    /// score == flightTime.
    /// </summary>
    private async Task<EntryId> CaptureFlightAsync(int roundOrdinal, GroupId groupRef, CompetitorId competitorRef, decimal flightTime)
    {
        var entryId = await ApiClient.PostCommandAsync<EntryId>(
            Client, "/open-entry", new OpenEntry(_competitionId, 0, roundOrdinal, 1, groupRef, competitorRef));

        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId, LaunchAt));

        await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(flightTime));
        await CaptureAsync(entryId, "startHeight", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "startHeightRecorded", MeasuredValue.Of(true));
        await CaptureAsync(entryId, "overflySeconds", MeasuredValue.Of(0m));
        await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(false));
        await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(100m)); // beyond the last row -> Rest(0)

        return entryId;
    }

    private static async Task CaptureAsync(EntryId entryId, string metric, MeasuredValue value) =>
        await ApiClient.PostCommandAsync<EntryId>(Client, "/capture-measurement", new CaptureMeasurement(entryId, 1, metric, value));
}
