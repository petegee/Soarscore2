// kanban/in-progress/pre-normalisation-score-view-field.md WI-2. Covers
// ScoreTaskRoundHandler directly against a FakeEventStore + FakeEntryQuery,
// same style as Queries/Entries/FindEntriesHandlerTests.cs. The handler runs
// the REAL pipeline — CompetitionLoader -> EntryCollector -> ScoringService
// .ScoreGroup -> MapGroupResult — never a stubbed ScoringService: the
// competition is built through its decide functions (FinaliseCompetition
// PropertyTests's seeding precedent) and the emitted events appended, so the
// fold sees exactly what a real run would. The assertion is that each row's
// PreNormalisationScore is the engine-input raw while RawScore stays the
// post-normalisation value (trap 5 — the two coexisting is the point).

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Application.Tests.Shared.CompetitionClasses;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

// Shared/Competitions has a FakeEventStore of its own (BindParameterHandler
// Tests.cs's precedent) — alias the Entries copy this file seeds with.
using Soarscore.Application.Tests.Shared.Entries;
using FakeEventStore = Soarscore.Application.Tests.Shared.Entries.FakeEventStore;

namespace Soarscore.Application.Tests.Queries.Scoring;

public class ScoreTaskRoundHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);

    private const int PhaseOrdinal = 0;
    private const int RoundOrdinal = 1;
    private const int TaskRoundOrdinal = 1;

    /// <summary>The Minimal fixture's task "A" with a HigherIsBetter normalisation
    /// to 1000, rounded to whole points — so the engine's post-normalisation
    /// values are exact integers (1000 and 833 for raws 600/500).</summary>
    private static ClassDefinition NormalisedDefinition() => ClassDefinitionFixtures.WithSingleTask(
        ClassDefinitionFixtures.Minimal(),
        new TaskDefinition
        {
            Code = "A",
            Name = "Task A",
            Metrics = [new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" }],
            Flights = new LastFlight(),
            Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
            Score = [new RateTerm { MetricRef = "flightTime", Rate = 1 }],
            Normalise = new Normalisation
            {
                Direction = NormalisationDirection.HigherIsBetter,
                WinnerScore = 1000,
                Round = new Rounding(RoundingMode.HalfUp, 1m),
            },
        });

    /// <summary>Builds a one-phase/one-round/one-task-round/one-group competition
    /// through its decide functions, appends the emitted events (CompetitionCreated
    /// → CompetitorRegistered… → PhaseDrawn → DrawAccepted on the competition
    /// stream; EntryOpened → FlightOpened → MeasurementCaptured per entry stream),
    /// seeds the entry read model, and hands back what the query needs. Each
    /// competitor flies one flight whose flightTime measurement is the
    /// correspondingly positioned element of flightTimes.</summary>
    private static (
        FakeEventStore Store,
        CompetitionId CompetitionId,
        GroupId GroupRef,
        ImmutableArray<CompetitorId> Competitors)
        SeedScoredGroup(
            FakeEventStore store,
            FakeEntryQuery entryQuery,
            ClassDefinition definition,
            params decimal[] flightTimes)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = definition.Version!,
            AdoptedAt = Now,
        };

        var competitionId = CompetitionId.New();
        var created = new CompetitionCreated(
            competitionId, "Pre-Normalisation View Comp", "Nowhere",
            new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 28),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);
        var competitionEvents = new List<IDomainEvent> { created };

        var competitors = ImmutableArray.CreateBuilder<CompetitorId>(flightTimes.Length);
        for (var i = 0; i < flightTimes.Length; i++)
        {
            var competitorId = CompetitorId.New();
            var registered = competition.RegisterCompetitor(competitorId, PersonId.New(), Now);
            registered.IsSuccess.Should().BeTrue();
            competitionEvents.Add(registered.Value);
            competition = competition.Apply(registered.Value);
            competitors.Add(competitorId);
        }

        // task.Group is null (whole-field, one group), so DrawPhase needs no
        // parameter binding — see Competition.DrawPhase's minPerGroup default.
        var drawn = competition.DrawPhase(1, ImmutableArray<string>.Empty, Now);
        drawn.IsSuccess.Should().BeTrue();
        competitionEvents.Add(drawn.Value);
        competition = competition.Apply(drawn.Value);

        // Entries open only against an accepted draw (D4).
        var accepted = competition.AcceptDraw(Now);
        accepted.IsSuccess.Should().BeTrue();
        competitionEvents.Add(accepted.Value);
        competition = competition.Apply(accepted.Value);

        var group = competition.Phases[PhaseOrdinal].Rounds[0].TaskRounds[0].Groups.Single();
        var taskDefinition = definition.Phases[0].Tasks[0];

        for (var i = 0; i < group.CompetitorRefs.Length; i++)
        {
            var opened = competition.OpenEntry(
                EntryId.New(), PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
                group.Id, group.CompetitorRefs[i], ReflightRole.Original, Now);
            opened.IsSuccess.Should().BeTrue();

            var entry = Entry.Create(opened.Value);

            var flightOpened = entry.OpenFlight(1, null, Now);
            flightOpened.IsSuccess.Should().BeTrue();
            var entryEvents = new List<IDomainEvent> { opened.Value, flightOpened.Value };
            entry = entry.Apply(flightOpened.Value);

            var captured = entry.CaptureMeasurement(
                1, "flightTime", MeasuredValue.Of(flightTimes[i]), Now, taskDefinition.Metrics);
            captured.IsSuccess.Should().BeTrue();
            entryEvents.Add(captured.Value);
            entry = entry.Apply(captured.Value);

            store.AppendAsync(entry.Id.Value, ExpectedVersion.NoStream, entryEvents)
                .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

            entryQuery.Seed(new EntrySummary(
                entry.Id, competitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
                group.Id, group.CompetitorRefs[i], ReflightRole.Original));
        }

        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, competitionEvents)
            .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

        return (store, competitionId, group.Id, competitors.MoveToImmutable());
    }

    private static async Task<IReadOnlyList<GroupScoreView>> ScoreSeededGroup(
        FakeEventStore store, FakeEntryQuery entryQuery, CompetitionId competitionId)
    {
        var handler = new ScoreTaskRoundHandler(store, entryQuery);

        var result = await handler.HandleAsync(
            new ScoreTaskRound(competitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal, null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task A_normalised_group_exposes_the_engine_input_raws_alongside_the_post_normalisation_scores()
    {
        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var (_, competitionId, groupRef, competitors) =
            SeedScoredGroup(store, entryQuery, NormalisedDefinition(), 600m, 500m);

        var views = await ScoreSeededGroup(store, entryQuery, competitionId);

        var view = views.Should().ContainSingle().Subject;
        view.GroupRef.Should().Be(groupRef);
        view.WinnerRef.Should().Be(competitors[0]);
        view.ValidCount.Should().Be(2);
        view.IsAnnulled.Should().BeFalse();

        view.Results.Should().HaveCount(2);

        var winner = view.Results.Single(r => r.CompetitorRef == competitors[0]);
        winner.State.Should().Be(TaskResultState.Valid);
        winner.Role.Should().Be(ReflightRole.Original);
        winner.PreNormalisationScore.Should().Be(600m);
        winner.RawScore.Should().Be(1000m);

        var runnerUp = view.Results.Single(r => r.CompetitorRef == competitors[1]);
        runnerUp.State.Should().Be(TaskResultState.Valid);
        runnerUp.PreNormalisationScore.Should().Be(500m);
        // 1000 * 500 / 600 = 833.333…, rounded HalfUp to whole points by the
        // task's Normalisation.Round.
        runnerUp.RawScore.Should().Be(833m);
    }

    [Fact]
    public async Task A_pass_through_task_reports_identical_pre_and_post_normalisation_scores()
    {
        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var (_, competitionId, groupRef, competitors) =
            SeedScoredGroup(store, entryQuery, ClassDefinitionFixtures.Minimal(), 600m, 500m);

        var views = await ScoreSeededGroup(store, entryQuery, competitionId);

        var view = views.Should().ContainSingle().Subject;
        view.GroupRef.Should().Be(groupRef);
        view.ValidCount.Should().Be(2);
        // The pass-through branch names no winner.
        view.WinnerRef.Should().BeNull();

        var first = view.Results.Single(r => r.CompetitorRef == competitors[0]);
        first.PreNormalisationScore.Should().Be(600m);
        first.RawScore.Should().Be(600m);

        var second = view.Results.Single(r => r.CompetitorRef == competitors[1]);
        second.PreNormalisationScore.Should().Be(500m);
        second.RawScore.Should().Be(500m);
    }
}
