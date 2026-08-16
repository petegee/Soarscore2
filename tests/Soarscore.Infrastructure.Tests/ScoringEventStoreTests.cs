// kanban/completed/scoring-steel-thread-plan.md WI-9 — the store-backed tests for
// the two scoring queries (ScoreTaskRoundHandler, ScoreCompetitionHandler),
// against a real PostgreSQL via Testcontainers. Same style as
// EntryCaptureEventStoreTests.cs / DrawPhaseEventStoreTests.cs: calls the
// real handlers directly against fixture.EventStore/fixture.EntryQuery, no
// dispatcher needed for a store-level test.
//
// End to end through the real handlers — no shortcuts: create competition ->
// register competitors -> draw phase -> open entries -> open flights ->
// capture measurements -> score. This is also the one place EntryCollector's
// fan-out through the real entry_index (IEntryQuery) gets exercised together
// with scoring (WI-6/WI-7's own header notes this) — Payoff_leaderboard...
// below opens entries across 2 rounds x 2 groups so the fan-out is actually
// doing something, not just a single-stream pass-through.
//
// F5J (30-f5j) is used throughout: literal (non-parameterised) MinPerGroup==6
// gives deterministic group membership for a 6- or 12-pilot field, and its
// task has a real Normalise block (WinnerScore 1000, HigherIsBetter, no
// rounding — 5.5.11.12 m states no precision, so none is applied), which is
// what makes normalised scores exactly reproducible by hand in these tests.
//
// Every captured flight fixes startHeight/landingDistance/overflySeconds/
// touchedByCompetitor to values that contribute zero (startHeight 0m inside
// the first band's zero-width interval, overflySeconds 0, touchedByCompetitor
// false, landingDistance beyond the table's last row -> the Rest(0) bucket),
// and startHeightRecorded true (FlightValidWhen requires it). That leaves raw
// score == flightTime exactly (Rate term, rate 1, no cap reached), so the
// normalised-score formula used by NormalisationEngine
// (WinnerScore * raw / winnerRaw) can be replicated in test code and compared
// for exact decimal equality, not just "some number came back".

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

[Trait("Category", "Storage")]
public sealed class ScoringEventStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    private static readonly DateTimeOffset LaunchAt = new(2026, 1, 10, 9, 3, 12, TimeSpan.Zero);

    // ---------------------------------------------------------------- setup

    private static async Task<CompetitionId> CreateCompetitionAsync(PostgresFixture fixture, string name)
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(F5JDefinition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition(name, "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            TestContext.Current.CancellationToken);
        created.IsSuccess.Should().BeTrue();

        return created.Value;
    }

    private static async Task<CompetitorId> RegisterCompetitorAsync(PostgresFixture fixture, CompetitionId competitionId, string email)
    {
        var registerPersonHandler = new RegisterPersonHandler(fixture.EventStore, new SystemClock());
        var person = await registerPersonHandler.HandleAsync(
            new RegisterPerson("Test Pilot", new ContactDetails { Email = email }, Club: null),
            TestContext.Current.CancellationToken);
        person.IsSuccess.Should().BeTrue();

        var registerCompetitorHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var competitor = await registerCompetitorHandler.HandleAsync(
            new RegisterCompetitor(competitionId, person.Value), TestContext.Current.CancellationToken);
        competitor.IsSuccess.Should().BeTrue();

        return competitor.Value;
    }

    /// <summary>
    /// Opens an Entry, opens its one flight, and captures every metric
    /// F5J's task D references — flightTime carries the test's chosen value;
    /// every other metric is fixed to a value that contributes zero to the
    /// raw score (see this file's header), so raw score == flightTime.
    /// </summary>
    private static async Task<EntryId> OpenAndCaptureFlightAsync(
        PostgresFixture fixture,
        CompetitionId competitionId,
        int phaseOrdinal,
        int roundOrdinal,
        int taskRoundOrdinal,
        GroupId groupRef,
        CompetitorId competitorRef,
        decimal flightTime)
    {
        var openEntryHandler = new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
        var opened = await openEntryHandler.HandleAsync(
            new OpenEntry(competitionId, phaseOrdinal, roundOrdinal, taskRoundOrdinal, groupRef, competitorRef),
            TestContext.Current.CancellationToken);
        opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");
        var entryId = opened.Value;

        var openFlightHandler = new OpenFlightHandler(fixture.EventStore, new SystemClock());
        var openedFlight = await openFlightHandler.HandleAsync(new OpenFlight(entryId, LaunchAt), TestContext.Current.CancellationToken);
        openedFlight.IsSuccess.Should().BeTrue();

        var captureHandler = new CaptureMeasurementHandler(fixture.EventStore, new SystemClock());

        async Task CaptureAsync(string metric, MeasuredValue value)
        {
            var captured = await captureHandler.HandleAsync(
                new CaptureMeasurement(entryId, 1, metric, value), TestContext.Current.CancellationToken);
            captured.IsSuccess.Should().BeTrue($"{captured.Code}: {captured.Message}");
        }

        await CaptureAsync("flightTime", MeasuredValue.Of(flightTime));
        await CaptureAsync("startHeight", MeasuredValue.Of(0m));
        await CaptureAsync("startHeightRecorded", MeasuredValue.Of(true));
        await CaptureAsync("overflySeconds", MeasuredValue.Of(0m));
        await CaptureAsync("touchedByCompetitor", MeasuredValue.Of(false));
        await CaptureAsync("landingDistance", MeasuredValue.Of(100m)); // beyond the last row -> Rest(0)

        return entryId;
    }

    // ---- 1. A real normalised group score for a task-round actually flown --

    [Fact]
    public async Task ScoreTaskRoundHandler_returns_a_real_normalised_group_score_with_one_winner()
    {
        var competitionId = await CreateCompetitionAsync(fixture, "Group Score");

        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 6; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-group-{i}@example.com"));
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 1), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        var group1 = fetched.Value.Competition.Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single(g => g.Ordinal == 1);
        group1.CompetitorRefs.Should().HaveCount(6);
        group1.CompetitorRefs.Should().BeEquivalentTo(competitorIds);

        // Distinct flightTime per competitor -> a single, unambiguous winner.
        // Raw score == flightTime (this file's header), so with winner 600 the
        // normalised formula (1000 * raw / winnerRaw) is exact decimal math.
        var flightTimeByCompetitor = competitorIds
            .Select((c, i) => (c, flightTime: 350m + i * 50m)) // 350, 400, 450, 500, 550, 600
            .ToDictionary(x => x.c, x => x.flightTime);

        foreach (var (competitorRef, flightTime) in flightTimeByCompetitor)
        {
            await OpenAndCaptureFlightAsync(fixture, competitionId, 0, 1, 1, group1.Id, competitorRef, flightTime);
        }

        var scoreHandler = new ScoreTaskRoundHandler(fixture.EventStore, fixture.EntryQuery);
        var scored = await scoreHandler.HandleAsync(
            new ScoreTaskRound(competitionId, 0, 1, 1, null), TestContext.Current.CancellationToken);
        scored.IsSuccess.Should().BeTrue($"{scored.Code}: {scored.Message}");

        scored.Value.Should().ContainSingle();
        var view = scored.Value[0];
        view.GroupRef.Should().Be(group1.Id);
        view.ValidCount.Should().Be(6);
        view.IsAnnulled.Should().BeFalse();

        var winner = flightTimeByCompetitor.MaxBy(kv => kv.Value);
        view.WinnerRef.Should().Be(winner.Key);

        view.Results.Should().HaveCount(6);
        foreach (var result in view.Results)
        {
            result.State.Should().Be(TaskResultState.Valid);
            var flightTime = flightTimeByCompetitor[result.CompetitorRef];
            var expectedNormalised = 1000m * flightTime / winner.Value;
            result.RawScore.Should().Be(expectedNormalised);
        }

        // The winner's own normalised score is exactly the class's normalisation
        // target (5.5.11.12 m, WinnerScore 1000) — SeedF5J.cs's own comment.
        view.Results.Single(r => r.CompetitorRef == winner.Key).RawScore.Should().Be(1000m);
    }

    // ---- 2. A real ranked leaderboard, fanning out across 2 rounds x 2 groups

    [Fact]
    public async Task ScoreCompetitionHandler_returns_a_ranked_leaderboard_fanning_out_across_multiple_groups_and_rounds()
    {
        var competitionId = await CreateCompetitionAsync(fixture, "Leaderboard Fan-out");

        // 12 competitors, MinPerGroup 6 -> 2 groups per round (PhaseDraw.BuildGroups:
        // groupCount = max(1, field.Length / minPerGroup) = 2).
        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < 12; i++)
        {
            competitorIds.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-leaderboard-{i}@example.com"));
        }

        // Skill (== flightTime, this file's header) strictly increasing and
        // globally distinct, so within ANY group the winner is whichever
        // member has the highest index — deterministic regardless of how the
        // draw happens to shuffle the two rounds' groups.
        var skillByCompetitor = competitorIds
            .Select((c, i) => (c, skill: 300m + i * 20m)) // 300, 320, ..., 520
            .ToDictionary(x => x.c, x => x.skill);

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 2), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        var rounds = fetched.Value.Competition.Phases.Single().Rounds;
        rounds.Should().HaveCount(2);

        // expectedTotal[competitor] accumulates each round's normalised score
        // (raw == flightTime == skill, per this file's header), computed from
        // the SAME group membership the real draw produced — not an assumed
        // layout — via NormalisationEngine's own formula
        // (WinnerScore * raw / winnerRaw, F5J has no rounding).
        var expectedTotal = competitorIds.ToDictionary(c => c, _ => 0m);

        foreach (var round in rounds)
        {
            var taskRound = round.TaskRounds.Single();
            taskRound.Groups.Should().HaveCount(2);

            foreach (var group in taskRound.Groups)
            {
                group.CompetitorRefs.Should().HaveCount(6);
                var winnerSkill = group.CompetitorRefs.Max(c => skillByCompetitor[c]);

                foreach (var competitorRef in group.CompetitorRefs)
                {
                    var skill = skillByCompetitor[competitorRef];
                    await OpenAndCaptureFlightAsync(
                        fixture, competitionId, 0, round.Ordinal, taskRound.Ordinal, group.Id, competitorRef, skill);

                    expectedTotal[competitorRef] += 1000m * skill / winnerSkill;
                }
            }
        }

        // Only 2 rounds completed; F5J's drop gate is
        // ApplyWhenRoundsCompletedAtLeast 5 (SeedF5J.cs), so no drop fires —
        // expectedTotal above is a plain sum, matching PhaseAggregator's own
        // "no policy matched" fallback.
        var scoreHandler = new ScoreCompetitionHandler(fixture.EventStore, fixture.EntryQuery);
        var scored = await scoreHandler.HandleAsync(new ScoreCompetition(competitionId), TestContext.Current.CancellationToken);
        scored.IsSuccess.Should().BeTrue($"{scored.Code}: {scored.Message}");

        scored.Value.Scores.Should().HaveCount(12);

        foreach (var score in scored.Value.Scores)
        {
            score.Disqualified.Should().BeFalse();
            score.Score.Should().Be(expectedTotal[score.CompetitorRef]);
        }

        var expectedPlacings = ComputeExpectedPlacings(expectedTotal);
        foreach (var score in scored.Value.Scores)
        {
            score.Placing.Should().Be(expectedPlacings[score.CompetitorRef]);
        }

        // The single highest-skill competitor wins every group they are drawn
        // into across both rounds, so they place first outright.
        var topCompetitor = competitorIds.MaxBy(c => skillByCompetitor[c]);
        scored.Value.Scores.Single(s => s.CompetitorRef == topCompetitor).Placing.Should().Be(1);
    }

    /// <summary>Mirrors RankingEngine.Rank's tie-group placement (descending score, equal scores share a placing).</summary>
    private static Dictionary<CompetitorId, int> ComputeExpectedPlacings(Dictionary<CompetitorId, decimal> totals)
    {
        var ranked = totals.OrderByDescending(kv => kv.Value).ToList();
        var placings = new Dictionary<CompetitorId, int>();
        var place = 1;
        var i = 0;

        while (i < ranked.Count)
        {
            var score = ranked[i].Value;
            var j = i + 1;
            while (j < ranked.Count && ranked[j].Value == score)
                j++;

            for (var k = i; k < j; k++)
                placings[ranked[k].Key] = place;

            place += j - i;
            i = j;
        }

        return placings;
    }
}
