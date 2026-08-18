// kanban/completed/scoring-steel-thread-plan.md WI-10 — step definitions for
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
// F5J only (30-f5j) — literal MinPerGroup 6, so a 6-competitor field draws to
// exactly one group per round and a 12-competitor field to two
// (kanban/completed/multi-group-normalisation-coverage.md), and a real
// Normalise block (WinnerScore 1000, HigherIsBetter, no rounding). Every flight fixes
// startHeight/landingDistance/overflySeconds/touchedByCompetitor to values
// that contribute zero to the raw score (see ScoringEventStoreTests.cs's
// header, WI-9, for the identical convention), so raw score == flightTime
// exactly and every expected value below is computed from that one fact.

using AwesomeAssertions;
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

    // Populated by the two-group scenario's When step, read by its Then steps:
    // groupRef → (competitorRef → flight time), so each group's own winner is
    // recoverable without reference to the other group's.
    private readonly Dictionary<GroupId, Dictionary<CompetitorId, decimal>> _flightTimesByGroup = new();

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

    /// <summary>
    /// Makes the scenario's one-group setup explicit rather than implicit in
    /// the helpers: F5J's literal MinPerGroup 6 (SeedF5J.cs) means a 6-pilot
    /// field draws to exactly one group, which is what makes "the group's
    /// winner" and "the round's winner" indistinguishable in this scenario.
    /// Normalisation is per *group* (docs/rules/f5j.md, 5.5.11.12) — a field
    /// large enough to draw two groups produces two competitors on 1000.
    /// </summary>
    [Given(@"^round (\d+) is drawn as a single group holding all (\d+) competitors$")]
    public async Task GivenRoundIsDrawnAsASingleGroupHoldingAllCompetitors(int roundOrdinal, int count)
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var round = view.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == roundOrdinal);

        round.TaskRounds.Should().ContainSingle();
        round.TaskRounds.Single().Groups.Should().ContainSingle();
        round.TaskRounds.Single().Groups.Single().CompetitorRefs.Should().HaveCount(count);
    }

    /// <summary>
    /// The multi-group counterpart: 12 competitors against F5J's MinPerGroup 6
    /// draw to two groups of 6 (PhaseDraw.BuildGroups: groupCount =
    /// field / minPerGroup, sizes evenly split). Asserted rather than assumed,
    /// because every later step in this scenario is about the boundary
    /// between the two groups — if the draw ever produced one group of 12 the
    /// scenario would still pass while testing nothing.
    /// </summary>
    [Given(@"^round (\d+) is drawn as (\d+) groups of (\d+) competitors$")]
    public async Task GivenRoundIsDrawnAsGroupsOfCompetitors(int roundOrdinal, int groupCount, int perGroup)
    {
        var groups = await ResolveGroupsAsync(roundOrdinal);

        groups.Should().HaveCount(groupCount);
        groups.Should().AllSatisfy(g => g.CompetitorRefs.Should().HaveCount(perGroup));
    }

    // ----------------------------------------------------------------- When

    [When(@"^every competitor in that group flies with a distinct flight time$")]
    public async Task WhenEveryCompetitorInThatGroupFliesWithADistinctFlightTime()
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

    /// <summary>
    /// Two groups flying in different air: group 1's times 300..400, group 2's
    /// 480..600. Every value is a whole multiple of its own group's winner
    /// over 1000 (750, 800, 850, 900, 950, 1000 and 800, 840, 880, 920, 960,
    /// 1000), so the expected scores are exact decimals; and the two groups'
    /// ranges do not overlap, so normalising the whole round against one
    /// winner would move every score in the other group.
    /// </summary>
    [When(@"^every competitor flies, one group flying markedly longer times than the other$")]
    public async Task WhenEveryCompetitorFliesOneGroupFlyingMarkedlyLongerTimes()
    {
        var groups = await ResolveGroupsAsync(roundOrdinal: 1);

        // Group ordinal 1 → 300, 320, 340, 360, 380, 400 (winner 400).
        // Group ordinal 2 → 480, 504, 528, 552, 576, 600 (winner 600).
        var timesByGroupOrdinal = new Dictionary<int, decimal[]>
        {
            [1] = [300m, 320m, 340m, 360m, 380m, 400m],
            [2] = [480m, 504m, 528m, 552m, 576m, 600m],
        };

        foreach (var group in groups)
        {
            var times = timesByGroupOrdinal[group.Ordinal];
            var byCompetitor = new Dictionary<CompetitorId, decimal>();

            for (var i = 0; i < group.CompetitorRefs.Length; i++)
            {
                byCompetitor[group.CompetitorRefs[i]] = times[i];
                await CaptureFlightAsync(1, group.Id, group.CompetitorRefs[i], times[i]);
            }

            _flightTimesByGroup[group.Id] = byCompetitor;
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

    [Then(@"^the group's result holds a normalised score for each of its 6 competitors$")]
    public async Task ThenTheGroupsResultHoldsANormalisedScoreForEachOfIts6Competitors()
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

    [Then(@"^the competitor with the longest flight time in the group is that group's winner, scoring the class's normalisation target of 1000$")]
    public async Task ThenTheCompetitorWithTheLongestFlightTimeInTheGroupIsThatGroupsWinner()
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

    [Then(@"^each competitor's score is their flight time relative to their own group's winner$")]
    public async Task ThenEachCompetitorsScoreIsRelativeToTheirOwnGroupsWinner()
    {
        var views = await FetchAllGroupViewsAsync(roundOrdinal: 1);
        views.Should().HaveCount(2);

        foreach (var view in views)
        {
            var times = _flightTimesByGroup[view.GroupRef];
            var groupWinnerTime = times.Values.Max();

            view.ValidCount.Should().Be(6);
            view.IsAnnulled.Should().BeFalse();
            view.WinnerRef.Should().Be(times.MaxBy(kv => kv.Value).Key);

            foreach (var result in view.Results)
            {
                result.RawScore.Should().Be(1000m * times[result.CompetitorRef] / groupWinnerTime);
            }
        }
    }

    [Then(@"^exactly one competitor in each group scores the class's normalisation target of 1000$")]
    public async Task ThenExactlyOneCompetitorInEachGroupScores1000()
    {
        var views = await FetchAllGroupViewsAsync(roundOrdinal: 1);

        foreach (var view in views)
        {
            view.Results.Count(r => r.RawScore == 1000m).Should().Be(1);
        }

        // The whole point of the scenario: a round of two groups has two
        // competitors on 1000, not one. A pipeline that normalised per round
        // would leave exactly one across the pair.
        views.SelectMany(v => v.Results).Count(r => r.RawScore == 1000m).Should().Be(2);
    }

    /// <summary>
    /// The explicit counterfactual. Normalising the whole round against its
    /// single best flight time is the plausible wrong implementation; this
    /// step computes what every score WOULD be under it and requires the
    /// slower group's actual scores to differ, so the assertion fails loudly
    /// rather than coincidentally agreeing.
    /// </summary>
    [Then(@"^nobody is normalised against the best flight time in the other group$")]
    public async Task ThenNobodyIsNormalisedAgainstTheOtherGroupsBestFlightTime()
    {
        var views = await FetchAllGroupViewsAsync(roundOrdinal: 1);

        var roundBestTime = _flightTimesByGroup.Values.SelectMany(t => t.Values).Max();

        var slowerGroup = views.Single(v => _flightTimesByGroup[v.GroupRef].Values.Max() != roundBestTime);

        foreach (var result in slowerGroup.Results)
        {
            var ifNormalisedAcrossTheRound =
                1000m * _flightTimesByGroup[slowerGroup.GroupRef][result.CompetitorRef] / roundBestTime;

            result.RawScore.Should().NotBe(ifNormalisedAcrossTheRound);
        }
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

    private async Task<Group> ResolveGroupAsync(int roundOrdinal) =>
        (await ResolveGroupsAsync(roundOrdinal)).Single(g => g.Ordinal == 1);

    /// <summary>Every group drawn into the round's one task-round, in draw order.</summary>
    private async Task<IReadOnlyList<Group>> ResolveGroupsAsync(int roundOrdinal)
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var phase = view.Competition.Phases.Single();
        var round = phase.Rounds.Single(r => r.Ordinal == roundOrdinal);
        return round.TaskRounds.Single().Groups;
    }

    /// <summary>
    /// The task-round result for every group at once — the query's groupRef is
    /// optional, and omitting it scores every group in the task-round
    /// (ScoreTaskRound.cs), which is the read a scorer actually does when a
    /// whole round is on the board.
    /// </summary>
    private async Task<List<GroupScoreView>> FetchAllGroupViewsAsync(int roundOrdinal)
    {
        var url = $"/task-round-result?competitionRef={_competitionId.Value}&phaseOrdinal=0&roundOrdinal={roundOrdinal}&taskRoundOrdinal=1";
        return await ApiClient.GetAsync<List<GroupScoreView>>(Client, url);
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

        await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));

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
