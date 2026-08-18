// kanban/completed/task-round-lifecycle.md WI-10 ("Store-backed") — the
// store-backed tests for the four events this thread makes reachable:
// TaskRoundCompleted, TaskRoundAnnulled, TaskRoundReopened and Finalised.
//
// This is the test the plan names as "the test that would have caught an
// unregistered event type" (WI-7): appending any of the four without its line
// in SoarscoreEventTypes.All fails at runtime on BOTH backends per LADR-0001
// §4.8, and nothing below can pass without a real append-and-re-fold through a
// real store. Every assertion therefore reads back through
// GetCompetitionHandler, which folds the stream fresh, rather than through the
// in-memory Competition the command handler happened to build.
//
// kanban/completed/multi-backend-deployment.md WI-6's shape, same as
// BindParameterEventStoreTests.cs / ScoringEventStoreTests.cs: written once
// against IStoreFixture, with one concrete subclass per backend at the foot of
// the file. Only the Postgres subclass keeps Trait("Category", "Storage");
// EventStoreTests.cs's header says why.
//
// F5J (30-f5j) throughout, for the two reasons this thread needs:
//   - literal MinPerGroup 6, so a 6-pilot field draws to exactly one group per
//     round and every ordinal below is deterministic; and
//   - Validity.MinRounds == 4 (5.5.11.5 a, SeedF5J.cs), a literal rather than a
//     parameter, so Competition.Finalise's class-driven gate has a concrete
//     number to be tested against without a BindParameter detour.
// Its drop gate is ApplyWhenRoundsCompletedAtLeast 5, so four flown rounds
// never trigger a drop and a declared aggregate is a plain sum.
//
// Every captured flight fixes startHeight/landingDistance/overflySeconds/
// touchedByCompetitor to values contributing zero to the raw score, exactly as
// ScoringEventStoreTests.cs's header describes, so raw score == flightTime.

using AwesomeAssertions;
using Soarscore.Application;
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
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class TaskRoundLifecycleEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    private static readonly DateTimeOffset LaunchAt = new(2026, 1, 10, 9, 3, 12, TimeSpan.Zero);

    /// <summary>F5J's Validity.MinRounds for the qualification phase (5.5.11.5 a).</summary>
    private const int F5JMinRounds = 4;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------- setup

    private static async Task<CompetitionId> CreateCompetitionAsync(IStoreFixture fixture, string name)
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(F5JDefinition), Ct);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition(name, "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            Ct);
        created.IsSuccess.Should().BeTrue($"{created.Code}: {created.Message}");

        return created.Value;
    }

    private static async Task<CompetitorId> RegisterCompetitorAsync(IStoreFixture fixture, CompetitionId competitionId, string email)
    {
        var registerPersonHandler = new RegisterPersonHandler(fixture.EventStore, new SystemClock());
        var person = await registerPersonHandler.HandleAsync(
            new RegisterPerson("Test Pilot", new ContactDetails { Email = email }, Club: null), Ct);
        person.IsSuccess.Should().BeTrue();

        var registerCompetitorHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var competitor = await registerCompetitorHandler.HandleAsync(
            new RegisterCompetitor(competitionId, person.Value), Ct);
        competitor.IsSuccess.Should().BeTrue();

        return competitor.Value;
    }

    private static async Task<Competition> LoadAsync(IStoreFixture fixture, CompetitionId competitionId)
    {
        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), Ct);
        fetched.IsSuccess.Should().BeTrue($"{fetched.Code}: {fetched.Message}");
        return fetched.Value.Competition;
    }

    private static async Task<TaskRoundState> StateOfAsync(IStoreFixture fixture, CompetitionId competitionId, int roundOrdinal)
    {
        var competition = await LoadAsync(fixture, competitionId);
        return competition.Phases.Single().Rounds.Single(r => r.Ordinal == roundOrdinal).TaskRounds.Single().State;
    }

    private static async Task<string> ReadModelStateAsync(IStoreFixture fixture, CompetitionId competitionId)
    {
        var rows = await fixture.CompetitionsQuery.SearchAsync(null, null, Ct);
        return rows.Single(c => c.Id == competitionId).State;
    }

    /// <summary>Opens an Entry, opens its one flight, and captures every metric F5J's task D references.</summary>
    private static async Task OpenAndCaptureFlightAsync(
        IStoreFixture fixture,
        CompetitionId competitionId,
        int roundOrdinal,
        GroupId groupRef,
        CompetitorId competitorRef,
        decimal flightTime)
    {
        var opened = await OpenEntryAsync(fixture, competitionId, roundOrdinal, groupRef, competitorRef);
        opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");
        var entryId = opened.Value;

        var openFlightHandler = new OpenFlightHandler(fixture.EventStore, new SystemClock());
        var openedFlight = await openFlightHandler.HandleAsync(new OpenFlight(entryId), Ct);
        openedFlight.IsSuccess.Should().BeTrue();

        var captureHandler = new CaptureMeasurementHandler(fixture.EventStore, new SystemClock());

        async Task CaptureAsync(string metric, MeasuredValue value)
        {
            var captured = await captureHandler.HandleAsync(new CaptureMeasurement(entryId, 1, metric, value), Ct);
            captured.IsSuccess.Should().BeTrue($"{captured.Code}: {captured.Message}");
        }

        await CaptureAsync("flightTime", MeasuredValue.Of(flightTime));
        await CaptureAsync("startHeight", MeasuredValue.Of(0m));
        await CaptureAsync("startHeightRecorded", MeasuredValue.Of(true));
        await CaptureAsync("overflySeconds", MeasuredValue.Of(0m));
        await CaptureAsync("touchedByCompetitor", MeasuredValue.Of(false));
        await CaptureAsync("landingDistance", MeasuredValue.Of(100m)); // beyond the last row -> Rest(0)
    }

    private static Task<Result<EntryId>> OpenEntryAsync(
        IStoreFixture fixture, CompetitionId competitionId, int roundOrdinal, GroupId groupRef, CompetitorId competitorRef) =>
        new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock())
            .HandleAsync(new OpenEntry(competitionId, 0, roundOrdinal, 1, groupRef, competitorRef), Ct);

    /// <summary>
    /// Creates the competition, registers <paramref name="fieldSize"/> pilots and
    /// draws the qualification phase for <paramref name="rounds"/> rounds. F5J's
    /// literal MinPerGroup 6 means a 6-pilot field is exactly one group per round.
    /// </summary>
    private static async Task<(CompetitionId CompetitionId, List<CompetitorId> Competitors)> DrawnCompetitionAsync(
        IStoreFixture fixture, string name, string emailSlug, int rounds, int fieldSize = 6)
    {
        var competitionId = await CreateCompetitionAsync(fixture, name);

        var competitors = new List<CompetitorId>();
        for (var i = 0; i < fieldSize; i++)
        {
            competitors.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-{emailSlug}-{i}@example.com"));
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, rounds), Ct);
        drawn.IsSuccess.Should().BeTrue($"{drawn.Code}: {drawn.Message}");

        return (competitionId, competitors);
    }

    // ---- 1. All four new events append and re-fold through the real store --

    [Fact]
    public async Task All_four_lifecycle_events_round_trip_through_the_real_store_and_the_read_model_reaches_finalised()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(
            fixture, "Lifecycle Round Trip", "lifecycle", F5JMinRounds);

        // The read model's first two states, before any lifecycle event exists
        // — the third ("finalised") is what this test is really after.
        (await ReadModelStateAsync(fixture, competitionId)).Should().Be("drawn");

        // Fly every round first: closing a task-round closes score capture for
        // it (Competition.cs's openEntry.taskRoundClosed), so capture comes
        // before completion, never after.
        var competition = await LoadAsync(fixture, competitionId);
        foreach (var round in competition.Phases.Single().Rounds)
        {
            var group = round.TaskRounds.Single().Groups.Single();
            group.CompetitorRefs.Should().HaveCount(6);

            for (var i = 0; i < group.CompetitorRefs.Length; i++)
            {
                await OpenAndCaptureFlightAsync(
                    fixture, competitionId, round.Ordinal, group.Id, group.CompetitorRefs[i], 350m + i * 50m);
            }
        }

        var completeHandler = new CompleteTaskRoundHandler(fixture.EventStore, new SystemClock());
        var annulHandler = new AnnulTaskRoundHandler(fixture.EventStore, new SystemClock());
        var reopenHandler = new ReopenTaskRoundHandler(fixture.EventStore, new SystemClock());

        // TaskRoundCompleted.
        var completed = await completeHandler.HandleAsync(new CompleteTaskRound(competitionId, 0, 1, 1), Ct);
        completed.IsSuccess.Should().BeTrue($"{completed.Code}: {completed.Message}");
        (await StateOfAsync(fixture, competitionId, 1)).Should().Be(TaskRoundState.Complete);

        // TaskRoundAnnulled — on a different round, so the two folds are seen
        // not to disturb each other across an append/re-read boundary.
        var annulled = await annulHandler.HandleAsync(
            new AnnulTaskRound(competitionId, 0, 2, 1, "Winch failure mid-group"), Ct);
        annulled.IsSuccess.Should().BeTrue($"{annulled.Code}: {annulled.Message}");
        (await StateOfAsync(fixture, competitionId, 2)).Should().Be(TaskRoundState.Annulled);
        (await StateOfAsync(fixture, competitionId, 1)).Should().Be(TaskRoundState.Complete);

        // TaskRoundReopened — Annulled -> Drawn, the correction of a ruling
        // made in error (Competition.ReopenTaskRound's doc comment).
        var reopened = await reopenHandler.HandleAsync(
            new ReopenTaskRound(competitionId, 0, 2, 1, "Annulment withdrawn — the winch was fine"), Ct);
        reopened.IsSuccess.Should().BeTrue($"{reopened.Code}: {reopened.Message}");
        (await StateOfAsync(fixture, competitionId, 2)).Should().Be(TaskRoundState.Drawn);

        // Four rounds fully flown is exactly F5J's MinRounds, so finalisation
        // becomes possible only once the last of them is Complete.
        foreach (var roundOrdinal in new[] { 2, 3, 4 })
        {
            var later = await completeHandler.HandleAsync(new CompleteTaskRound(competitionId, 0, roundOrdinal, 1), Ct);
            later.IsSuccess.Should().BeTrue($"{later.Code}: {later.Message}");
        }

        // Finalised.
        var finaliseHandler = new FinaliseCompetitionHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
        var finalised = await finaliseHandler.HandleAsync(new FinaliseCompetition(competitionId, "CD Jane"), Ct);
        finalised.IsSuccess.Should().BeTrue($"{finalised.Code}: {finalised.Message}");

        var afterFinalise = await LoadAsync(fixture, competitionId);
        afterFinalise.Phases.Single().Rounds.Should().OnlyContain(r => r.IsFullyFlown);

        var finalisation = afterFinalise.Finalisations.Should().ContainSingle().Subject;
        finalisation.Scope.Should().Be(FinalisationScope.Competition);
        finalisation.Revision.Should().Be(1);
        finalisation.By.Should().Be("CD Jane");
        finalisation.DeclaredResults.Should().HaveCount(competitors.Count);
        finalisation.DeclaredResults.Select(r => r.CompetitorRef).Should().BeEquivalentTo(competitors);
        finalisation.DeclaredResults.Should().OnlyContain(r => r.Promoted == false);

        // The DeclaredResults survived a JSON round-trip through the store with
        // their decimals intact, and still agree with what the leaderboard
        // derives from the same log (the plan's invariant B, end to end).
        var scoreHandler = new ScoreCompetitionHandler(fixture.EventStore, fixture.EntryQuery);
        var scored = await scoreHandler.HandleAsync(new ScoreCompetition(competitionId), Ct);
        scored.IsSuccess.Should().BeTrue($"{scored.Code}: {scored.Message}");

        foreach (var declared in finalisation.DeclaredResults)
        {
            var derived = scored.Value.Scores.Single(s => s.CompetitorRef == declared.CompetitorRef);
            declared.Aggregate.Should().Be(derived.Score);
            declared.Placing.Should().Be(derived.Placing);
        }

        // WI-8: the read model's third state, folded from Finalised by
        // CompetitionProjection through the real Inline projection.
        (await ReadModelStateAsync(fixture, competitionId)).Should().Be("finalised");
    }

    // ---- 2. Closure really closes capture, and reopening really restores it -

    [Fact]
    public async Task Completing_a_task_round_closes_score_capture_through_the_real_store_and_reopening_restores_it()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Closure", "closure", rounds: 1);

        var competition = await LoadAsync(fixture, competitionId);
        var group = competition.Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single();

        // Five of six fly; the sixth is the late score the plan's governing
        // principle says must never be permanently locked out.
        var latecomer = group.CompetitorRefs[5];
        for (var i = 0; i < 5; i++)
        {
            await OpenAndCaptureFlightAsync(fixture, competitionId, 1, group.Id, group.CompetitorRefs[i], 350m + i * 50m);
        }

        var completeHandler = new CompleteTaskRoundHandler(fixture.EventStore, new SystemClock());
        var completed = await completeHandler.HandleAsync(new CompleteTaskRound(competitionId, 0, 1, 1), Ct);
        completed.IsSuccess.Should().BeTrue($"{completed.Code}: {completed.Message}");

        // The dead check at Competition.cs's openEntry.taskRoundClosed, now
        // reachable — and reached through a real store round-trip, so it is the
        // re-folded state doing the refusing.
        var refused = await OpenEntryAsync(fixture, competitionId, 1, group.Id, latecomer);
        refused.IsFailure.Should().BeTrue();
        refused.Code.Should().Be("openEntry.taskRoundClosed");

        var reopenHandler = new ReopenTaskRoundHandler(fixture.EventStore, new SystemClock());
        var reopened = await reopenHandler.HandleAsync(
            new ReopenTaskRound(competitionId, 0, 1, 1, "Late score from the timing sheet"), Ct);
        reopened.IsSuccess.Should().BeTrue($"{reopened.Code}: {reopened.Message}");

        await OpenAndCaptureFlightAsync(fixture, competitionId, 1, group.Id, latecomer, 600m);

        var scoreHandler = new ScoreCompetitionHandler(fixture.EventStore, fixture.EntryQuery);
        var scored = await scoreHandler.HandleAsync(new ScoreCompetition(competitionId), Ct);
        scored.IsSuccess.Should().BeTrue($"{scored.Code}: {scored.Message}");
        scored.Value.Scores.Should().HaveCount(competitors.Count);
        scored.Value.Scores.Should().Contain(s => s.CompetitorRef == latecomer);
    }

    // ---- 3. The State column survives a read-model drop and full replay ----

    [Fact]
    public async Task Competitions_read_model_dropped_and_fully_replayed_lands_back_on_finalised()
    {
        var (competitionId, _) = await DrawnCompetitionAsync(fixture, "State Replay", "state-replay", F5JMinRounds);

        var competition = await LoadAsync(fixture, competitionId);
        foreach (var round in competition.Phases.Single().Rounds)
        {
            var group = round.TaskRounds.Single().Groups.Single();
            for (var i = 0; i < group.CompetitorRefs.Length; i++)
            {
                await OpenAndCaptureFlightAsync(
                    fixture, competitionId, round.Ordinal, group.Id, group.CompetitorRefs[i], 350m + i * 50m);
            }

            var completed = await new CompleteTaskRoundHandler(fixture.EventStore, new SystemClock())
                .HandleAsync(new CompleteTaskRound(competitionId, 0, round.Ordinal, 1), Ct);
            completed.IsSuccess.Should().BeTrue($"{completed.Code}: {completed.Message}");
        }

        var finaliseHandler = new FinaliseCompetitionHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
        var finalised = await finaliseHandler.HandleAsync(new FinaliseCompetition(competitionId, "CD Jane"), Ct);
        finalised.IsSuccess.Should().BeTrue($"{finalised.Code}: {finalised.Message}");

        var rowsBefore = await fixture.CompetitionsQuery.SearchAsync(null, null, Ct);
        var rowBefore = rowsBefore.Single(c => c.Id == competitionId);
        rowBefore.State.Should().Be("finalised");

        // Drop the read model's data only — the event log is untouched
        // (LADR-0001 §4.10).
        await fixture.DropDocumentsAsync<CompetitionSummary>(Ct);

        var afterDrop = await fixture.CompetitionsQuery.SearchAsync(null, null, Ct);
        afterDrop.Should().NotContain(c => c.Id == competitionId);

        // Replay the whole log — now including all four lifecycle events —
        // through the same Inline projection, on demand, never the async
        // daemon (LADR-0001 §2).
        await fixture.RebuildProjectionAsync("CompetitionSummaryProjection", Ct);

        var rowsAfter = await fixture.CompetitionsQuery.SearchAsync(null, null, Ct);
        var rowAfter = rowsAfter.Single(c => c.Id == competitionId);
        rowAfter.Should().BeEquivalentTo(rowBefore);
        rowAfter.State.Should().Be("finalised");
    }

    // ---- 4. The validity gate is class-driven, not hard-coded --------------

    [Fact]
    public async Task Finalise_is_refused_through_the_real_store_until_the_classs_minimum_rounds_are_fully_flown()
    {
        var (competitionId, _) = await DrawnCompetitionAsync(fixture, "Validity Gate", "validity", F5JMinRounds);

        var competition = await LoadAsync(fixture, competitionId);
        var rounds = competition.Phases.Single().Rounds;

        var finaliseHandler = new FinaliseCompetitionHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
        var completeHandler = new CompleteTaskRoundHandler(fixture.EventStore, new SystemClock());

        foreach (var round in rounds)
        {
            var group = round.TaskRounds.Single().Groups.Single();
            for (var i = 0; i < group.CompetitorRefs.Length; i++)
            {
                await OpenAndCaptureFlightAsync(
                    fixture, competitionId, round.Ordinal, group.Id, group.CompetitorRefs[i], 350m + i * 50m);
            }
        }

        // Flown but not closed: rounds only count towards validity once the CD
        // has asserted their scores are in (Round.IsFullyFlown).
        var tooSoon = await finaliseHandler.HandleAsync(new FinaliseCompetition(competitionId, "CD Jane"), Ct);
        tooSoon.IsFailure.Should().BeTrue();
        tooSoon.Code.Should().Be("finalise.notEnoughRounds");

        // Three complete and one annulled is four rounds with nothing left to
        // fly, and still not four rounds that produced a result.
        foreach (var roundOrdinal in new[] { 1, 2, 3 })
        {
            var completed = await completeHandler.HandleAsync(new CompleteTaskRound(competitionId, 0, roundOrdinal, 1), Ct);
            completed.IsSuccess.Should().BeTrue($"{completed.Code}: {completed.Message}");
        }

        var annulled = await new AnnulTaskRoundHandler(fixture.EventStore, new SystemClock())
            .HandleAsync(new AnnulTaskRound(competitionId, 0, 4, 1, "Thermal cycle collapsed for the second group"), Ct);
        annulled.IsSuccess.Should().BeTrue($"{annulled.Code}: {annulled.Message}");

        var stillShort = await finaliseHandler.HandleAsync(new FinaliseCompetition(competitionId, "CD Jane"), Ct);
        stillShort.IsFailure.Should().BeTrue();
        stillShort.Code.Should().Be("finalise.notEnoughRounds");
        (await ReadModelStateAsync(fixture, competitionId)).Should().Be("drawn");

        // Reopen the annulled round and complete it: now four rounds are fully
        // flown and the class's own gate opens.
        var reopened = await new ReopenTaskRoundHandler(fixture.EventStore, new SystemClock())
            .HandleAsync(new ReopenTaskRound(competitionId, 0, 4, 1, "Re-ruled: the round stands"), Ct);
        reopened.IsSuccess.Should().BeTrue($"{reopened.Code}: {reopened.Message}");

        var lastCompleted = await completeHandler.HandleAsync(new CompleteTaskRound(competitionId, 0, 4, 1), Ct);
        lastCompleted.IsSuccess.Should().BeTrue($"{lastCompleted.Code}: {lastCompleted.Message}");

        var finalised = await finaliseHandler.HandleAsync(new FinaliseCompetition(competitionId, "CD Jane"), Ct);
        finalised.IsSuccess.Should().BeTrue($"{finalised.Code}: {finalised.Message}");

        // And a second competition-scope finalisation is refused, from the
        // re-folded stream rather than from anything held in memory.
        var again = await finaliseHandler.HandleAsync(new FinaliseCompetition(competitionId, "CD Jane"), Ct);
        again.IsFailure.Should().BeTrue();
        again.Code.Should().Be("finalise.alreadyFinalised");
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresTaskRoundLifecycleEventStoreTests(PostgresFixture fixture)
    : TaskRoundLifecycleEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteTaskRoundLifecycleEventStoreTests(SqliteFixture fixture)
    : TaskRoundLifecycleEventStoreTests<SqliteFixture>(fixture);
