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
    /// stream), and hands back the folded aggregate plus what the query needs.
    /// The per-competitor Entry/Flight work is the callers'.</summary>
    private static (
        Competition Competition,
        CompetitionId CompetitionId,
        Group Group,
        ImmutableArray<CompetitorId> Competitors)
        SeedDrawnGroup(
            FakeEventStore store,
            FakeEntryQuery entryQuery,
            ClassDefinition definition,
            int competitorCount)
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

        var competitors = ImmutableArray.CreateBuilder<CompetitorId>(competitorCount);
        for (var i = 0; i < competitorCount; i++)
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

        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, competitionEvents)
            .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

        return (competition, competitionId, group, competitors.MoveToImmutable());
    }

    /// <summary>Opens one flown Entry (its first flight, sequence 1) for the
    /// competitor and captures exactly the given (metric, value) pairs,
    /// appending the entry stream and seeding the entry read model.</summary>
    private static void OpenFlownEntry(
        FakeEventStore store,
        FakeEntryQuery entryQuery,
        ClassDefinition definition,
        Competition competition,
        CompetitionId competitionId,
        Group group,
        CompetitorId competitorRef,
        IReadOnlyList<(string Metric, MeasuredValue Value)> captures)
    {
        var taskDefinition = definition.Phases[0].Tasks[0];

        var opened = competition.OpenEntry(
            EntryId.New(), PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            group.Id, competitorRef, ReflightRole.Original, Now);
        opened.IsSuccess.Should().BeTrue();

        var entry = Entry.Create(opened.Value);

        var flightOpened = entry.OpenFlight(1, null, Now);
        flightOpened.IsSuccess.Should().BeTrue();
        var entryEvents = new List<IDomainEvent> { opened.Value, flightOpened.Value };
        entry = entry.Apply(flightOpened.Value);

        foreach (var (metric, value) in captures)
        {
            var captured = entry.CaptureMeasurement(
                1, metric, value, Now, taskDefinition.Metrics);
            captured.IsSuccess.Should().BeTrue();
            entryEvents.Add(captured.Value);
            entry = entry.Apply(captured.Value);
        }

        store.AppendAsync(entry.Id.Value, ExpectedVersion.NoStream, entryEvents)
            .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

        entryQuery.Seed(new EntrySummary(
            entry.Id, competitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            group.Id, competitorRef, ReflightRole.Original));
    }

    /// <summary>The one-competition seeding plus one flown Entry per competitor,
    /// each capturing flightTime at the correspondingly positioned element of
    /// flightTimes.</summary>
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
        return SeedGroupWithCaptures(
            store, entryQuery, definition,
            [.. flightTimes.Select(t => new[] { ("flightTime", MeasuredValue.Of(t)) })]);
    }

    /// <summary>The one-competition seeding plus one flown Entry per competitor,
    /// each capturing exactly its own (metric, value) pairs — the absence shapes
    /// need per-competitor capture plans
    /// (kanban/in-progress/metric-absence-semantics.md WI-3).</summary>
    private static (
        FakeEventStore Store,
        CompetitionId CompetitionId,
        GroupId GroupRef,
        ImmutableArray<CompetitorId> Competitors)
        SeedGroupWithCaptures(
            FakeEventStore store,
            FakeEntryQuery entryQuery,
            ClassDefinition definition,
            params IReadOnlyList<(string Metric, MeasuredValue Value)>[] capturesPerCompetitor)
    {
        var (competition, competitionId, group, competitors) =
            SeedDrawnGroup(store, entryQuery, definition, capturesPerCompetitor.Length);

        for (var i = 0; i < competitors.Length; i++)
        {
            OpenFlownEntry(store, entryQuery, definition, competition, competitionId, group, competitors[i], capturesPerCompetitor[i]);
        }

        return (store, competitionId, group.Id, competitors);
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

    [Fact]
    public async Task A_flight_missing_an_unassumed_metric_carries_the_awaited_metric_on_its_row()
    {
        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();

        // Competitor 0 flies the clean-flight shape — the time recorded only,
        // landedOut's absence resolving to its assumed value (tier 1), so
        // nothing is awaited. Competitor 1 records the exception only —
        // flightTime is declared without an assumption, so the flight pends
        // naming it (tier 2) and the row says so.
        var (_, competitionId, _, competitors) = SeedGroupWithCaptures(
            store, entryQuery, ClassDefinitionFixtures.AbsenceSemantics(),
            [("flightTime", MeasuredValue.Of(600m))],
            [("landedOut", MeasuredValue.Of(true))]);

        var views = await ScoreSeededGroup(store, entryQuery, competitionId);

        var view = views.Should().ContainSingle().Subject;
        // The pending flight is not selectable — score what it can.
        view.ValidCount.Should().Be(1);

        var clean = view.Results.Single(r => r.CompetitorRef == competitors[0]);
        clean.State.Should().Be(TaskResultState.Valid);
        // landedOut IS missing on this flight — assumed, so not awaited.
        clean.AwaitingCapture.Should().BeEmpty();

        var awaiting = view.Results.Single(r => r.CompetitorRef == competitors[1]);
        awaiting.State.Should().Be(TaskResultState.NoResult);
        awaiting.RawScore.Should().Be(0m);
        awaiting.AwaitingCapture.Should().Equal(new PendingFlightDiagnostic(1, "flightTime"));
    }

    [Fact]
    public async Task An_entry_without_flights_renders_an_empty_awaitingCapture_that_serialises()
    {
        // The engine's no-flight NoResult path leaves AwaitingCapture a default
        // ImmutableArray (TaskResult's own doc: "IsEmpty is true") — but a
        // default array cannot be enumerated, so a row carrying one killed
        // response serialisation mid-stream (Gliderscore parity harness). The
        // view hands the boundary a real empty array instead.
        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var (competition, competitionId, group, competitors) =
            SeedDrawnGroup(store, entryQuery, ClassDefinitionFixtures.Minimal(), 1);

        var opened = competition.OpenEntry(
            EntryId.New(), PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            group.Id, competitors[0], ReflightRole.Original, Now);
        opened.IsSuccess.Should().BeTrue();
        var entry = Entry.Create(opened.Value);
        (await store.AppendAsync(entry.Id.Value, ExpectedVersion.NoStream, [opened.Value], TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();
        entryQuery.Seed(new EntrySummary(
            entry.Id, competitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            group.Id, competitors[0], ReflightRole.Original));

        var views = await ScoreSeededGroup(store, entryQuery, competitionId);

        var row = views.Should().ContainSingle().Subject
            .Results.Should().ContainSingle().Subject;
        row.State.Should().Be(TaskResultState.NoResult);
        row.AwaitingCapture.IsEmpty.Should().BeTrue();

        // The actual failure mode was serialisation, not the view itself —
        // camelCase, the Api's response shape.
        var camelCase = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        System.Text.Json.JsonSerializer.Serialize(views, camelCase)
            .Should().Contain("\"awaitingCapture\":[]");
    }

    // ------------------------------------------------- per-term breakdown
    // (kanban/in-progress/per-term-score-breakdown.md WI-4): the view carries
    // the engine's own per-term awards per flight, verbatim — the client
    // renders String(value), never a reading→points lookup of its own.

    private static ClassDefinition RatePlusLandingDefinition() => ClassDefinitionFixtures.WithSingleTask(
        ClassDefinitionFixtures.Minimal(),
        new TaskDefinition
        {
            Code = "A",
            Name = "Task A",
            Metrics =
            [
                new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" },
                new MetricDefinition { Name = "landingDistance", Kind = MeasuredKind.Number, Unit = "m" },
            ],
            Flights = new LastFlight(),
            Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
            Score =
            [
                new RateTerm { MetricRef = "flightTime", Rate = 1 },
                new LookupTerm
                {
                    MetricRef = "landingDistance",
                    Rows = [new LookupRow(5, 50), new LookupRow(10, 25), new LookupRow(null, 0)],
                },
            ],
        });

    private static ClassDefinition GatedLandingDefinition() => ClassDefinitionFixtures.WithSingleTask(
        ClassDefinitionFixtures.Minimal(),
        new TaskDefinition
        {
            Code = "A",
            Name = "Task A",
            Metrics =
            [
                new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" },
                new MetricDefinition { Name = "overflySeconds", Kind = MeasuredKind.Number, Unit = "s" },
                new MetricDefinition { Name = "landingDistance", Kind = MeasuredKind.Number, Unit = "m" },
            ],
            Flights = new LastFlight(),
            Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
            Score =
            [
                new RateTerm { MetricRef = "flightTime", Rate = 1 },
                // F3J.10.9's shape in miniature: the landing lookup is awarded
                // only when nothing overflew; otherwise the term is absent
                // (no Else — a closed gate contributes 0).
                new ConditionalTerm
                {
                    When = new Comparison
                    {
                        LeftMetricRef = "overflySeconds",
                        Op = Comparator.EqualTo,
                        RightValue = MeasuredValue.Of(0m),
                    },
                    Then = new LookupTerm
                    {
                        MetricRef = "landingDistance",
                        Rows = [new LookupRow(5, 50), new LookupRow(null, 0)],
                    },
                },
            ],
        });

    /// <summary>Opens one Entry with the given per-flight capture plans —
    /// the multi-flight companion to <see cref="OpenFlownEntry"/> (one
    /// inner list per flight sequence, 1-based in order).</summary>
    private static void OpenMultiFlightEntry(
        FakeEventStore store,
        FakeEntryQuery entryQuery,
        ClassDefinition definition,
        Competition competition,
        CompetitionId competitionId,
        Group group,
        CompetitorId competitorRef,
        IReadOnlyList<IReadOnlyList<(string Metric, MeasuredValue Value)>> capturesPerFlight)
    {
        var taskDefinition = definition.Phases[0].Tasks[0];

        var opened = competition.OpenEntry(
            EntryId.New(), PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            group.Id, competitorRef, ReflightRole.Original, Now);
        opened.IsSuccess.Should().BeTrue();

        var entry = Entry.Create(opened.Value);
        var entryEvents = new List<IDomainEvent> { opened.Value };

        for (var f = 0; f < capturesPerFlight.Count; f++)
        {
            var flightOpened = entry.OpenFlight(f + 1, null, Now);
            flightOpened.IsSuccess.Should().BeTrue();
            entryEvents.Add(flightOpened.Value);
            entry = entry.Apply(flightOpened.Value);

            foreach (var (metric, value) in capturesPerFlight[f])
            {
                var captured = entry.CaptureMeasurement(
                    f + 1, metric, value, Now, taskDefinition.Metrics);
                captured.IsSuccess.Should().BeTrue();
                entryEvents.Add(captured.Value);
                entry = entry.Apply(captured.Value);
            }
        }

        store.AppendAsync(entry.Id.Value, ExpectedVersion.NoStream, entryEvents)
            .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

        entryQuery.Seed(new EntrySummary(
            entry.Id, competitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            group.Id, competitorRef, ReflightRole.Original));
    }

    [Fact]
    public async Task Per_term_breakdown_surfaces_each_terms_verbatim_award_per_flight()
    {
        // WI-4(a): the single-flight landing shape — the landing term's Points
        // is the engine's own lookup award, keyed by metricRef, beside the
        // flight's sequence (not a selection rank).
        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var (_, competitionId, _, competitors) = SeedGroupWithCaptures(
            store, entryQuery, RatePlusLandingDefinition(),
            [("flightTime", MeasuredValue.Of(300m)), ("landingDistance", MeasuredValue.Of(7m))]);

        var views = await ScoreSeededGroup(store, entryQuery, competitionId);

        var row = views.Should().ContainSingle().Subject
            .Results.Should().ContainSingle().Subject;
        row.CompetitorRef.Should().Be(competitors[0]);
        row.State.Should().Be(TaskResultState.Valid);
        row.RawScore.Should().Be(325m);
        row.PreNormalisationScore.Should().Be(325m);

        var flight = row.Flights.Should().ContainSingle().Subject;
        flight.Sequence.Should().Be(1);
        flight.Terms.Should().HaveCount(2);

        var time = flight.Terms.Single(t => t.TermIndex == 0);
        time.MetricRef.Should().Be("flightTime");
        time.MetricConsumed.Should().Be(300m);
        time.Points.Should().Be(300m);

        var landing = flight.Terms.Single(t => t.TermIndex == 1);
        landing.MetricRef.Should().Be("landingDistance");
        landing.MetricConsumed.Should().Be(7m);
        landing.Points.Should().Be(25m);

        // The pre-cap, pre-rounding sum relationship the record docs state.
        flight.Terms.Sum(t => t.Points).Should().Be(325m);
    }

    [Fact]
    public async Task Per_term_breakdown_carries_the_tape_composed_award_verbatim()
    {
        // WI-4(a), second half: a mark-mode landing's breakdown value is the
        // composed award, not a distance fallback, with zero extra work —
        // reading 22 on the test tape denotes (5, 10] which the class table
        // awards 25; as a distance 22m would fall through to 0.
        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var definition = RatePlusLandingDefinition();
        var (competition, competitionId, group, competitors) =
            SeedDrawnGroup(store, entryQuery, definition, 1);

        var declared = new DeclaredInstrument
        {
            Instrument = "test-tape",
            Metric = "landingDistance",
            Scale = new ReadingScale
            {
                Unit = "m",
                Marks = [new ScaleMark(5, 11), new ScaleMark(10, 22)],
                OffScaleReading = 0,
            },
        };
        var declaredResult = competition.DeclareInstruments([declared], "cd", Now);
        declaredResult.IsSuccess.Should().BeTrue();
        competition = competition.Apply(declaredResult.Value);
        (await store.AppendAsync(
            competitionId.Value,
            ExpectedVersion.Exact(store.Streams[competitionId.Value].Count),
            [declaredResult.Value],
            TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();

        var taskDefinition = definition.Phases[0].Tasks[0];
        var opened = competition.OpenEntry(
            EntryId.New(), PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            group.Id, competitors[0], ReflightRole.Original, Now);
        opened.IsSuccess.Should().BeTrue();
        var entry = Entry.Create(opened.Value);
        var entryEvents = new List<IDomainEvent> { opened.Value };

        var flightOpened = entry.OpenFlight(1, null, Now);
        flightOpened.IsSuccess.Should().BeTrue();
        entryEvents.Add(flightOpened.Value);
        entry = entry.Apply(flightOpened.Value);

        var timed = entry.CaptureMeasurement(
            1, "flightTime", MeasuredValue.Of(300m), Now, taskDefinition.Metrics);
        timed.IsSuccess.Should().BeTrue();
        entryEvents.Add(timed.Value);
        entry = entry.Apply(timed.Value);

        var marked = entry.CaptureMeasurement(
            1, "landingDistance", MeasuredValue.Of(22m), Now, taskDefinition.Metrics,
            instrument: "test-tape", declaredInstruments: [declared]);
        marked.IsSuccess.Should().BeTrue();
        entryEvents.Add(marked.Value);
        entry = entry.Apply(marked.Value);

        (await store.AppendAsync(entry.Id.Value, ExpectedVersion.NoStream, entryEvents, TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();
        entryQuery.Seed(new EntrySummary(
            entry.Id, competitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            group.Id, competitors[0], ReflightRole.Original));

        var views = await ScoreSeededGroup(store, entryQuery, competitionId);

        var row = views.Should().ContainSingle().Subject
            .Results.Should().ContainSingle().Subject;
        row.State.Should().Be(TaskResultState.Valid);
        row.RawScore.Should().Be(325m);

        var landing = row.Flights.Should().ContainSingle().Subject
            .Terms.Single(t => t.TermIndex == 1);
        landing.MetricRef.Should().Be("landingDistance");
        // The contribution consumes the READING (decision 4), and the award
        // is the class table's for the denoted band — never a distance
        // fallback (22m as a distance would score 0).
        landing.MetricConsumed.Should().Be(22m);
        landing.Points.Should().Be(25m);
    }

    [Fact]
    public async Task Per_term_breakdown_unwraps_a_conditional_landing_gate()
    {
        // WI-4(c): the F3J-style touch/overfly gate. Open gate awards the
        // lookup; closed gate contributes its else-branch value (0, no Else)
        // while the term keeps the branch metric's identity.
        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var (_, competitionId, _, competitors) = SeedGroupWithCaptures(
            store, entryQuery, GatedLandingDefinition(),
            [("flightTime", MeasuredValue.Of(300m)), ("overflySeconds", MeasuredValue.Of(0m)), ("landingDistance", MeasuredValue.Of(3m))],
            [("flightTime", MeasuredValue.Of(300m)), ("overflySeconds", MeasuredValue.Of(5m)), ("landingDistance", MeasuredValue.Of(3m))]);

        var views = await ScoreSeededGroup(store, entryQuery, competitionId);
        var view = views.Should().ContainSingle().Subject;

        var open = view.Results.Single(r => r.CompetitorRef == competitors[0]);
        open.State.Should().Be(TaskResultState.Valid);
        open.RawScore.Should().Be(350m);
        var openLanding = open.Flights.Should().ContainSingle().Subject
            .Terms.Single(t => t.TermIndex == 1);
        openLanding.MetricRef.Should().Be("landingDistance");
        openLanding.MetricConsumed.Should().Be(3m);
        openLanding.Points.Should().Be(50m);

        var closed = view.Results.Single(r => r.CompetitorRef == competitors[1]);
        closed.State.Should().Be(TaskResultState.Valid);
        closed.RawScore.Should().Be(300m);
        var closedLanding = closed.Flights.Should().ContainSingle().Subject
            .Terms.Single(t => t.TermIndex == 1);
        closedLanding.MetricRef.Should().Be("landingDistance");
        closedLanding.Points.Should().Be(0m);
    }

    [Fact]
    public async Task Per_term_breakdown_reports_one_entry_per_flight_for_multi_flight_rows()
    {
        // WI-4(b): the multi-flight `all` shape (F3K/F5K) — one breakdown per
        // flight with its own sequence; aggregation stays server-side
        // (RawScore is the sum, the view never asks the client to add).
        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var definition = ClassDefinitionFixtures.WithSingleTask(
            ClassDefinitionFixtures.Minimal(),
            new TaskDefinition
            {
                Code = "A",
                Name = "Task A",
                Metrics = [new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" }],
                Flights = new AllFlights(),
                Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
                Score = [new RateTerm { MetricRef = "flightTime", Rate = 1 }],
            });

        var (competition, competitionId, group, competitors) =
            SeedDrawnGroup(store, entryQuery, definition, 1);
        OpenMultiFlightEntry(
            store, entryQuery, definition, competition, competitionId, group, competitors[0],
            [[("flightTime", MeasuredValue.Of(120m))], [("flightTime", MeasuredValue.Of(200m))]]);

        var views = await ScoreSeededGroup(store, entryQuery, competitionId);

        var row = views.Should().ContainSingle().Subject
            .Results.Should().ContainSingle().Subject;
        row.State.Should().Be(TaskResultState.Valid);
        row.RawScore.Should().Be(320m);

        row.Flights.Should().HaveCount(2);
        row.Flights.Select(f => f.Sequence).Should().Equal(1, 2);
        row.Flights.Select(f => f.Terms.Should().ContainSingle().Subject.Points)
            .Should().Equal(120m, 200m);
        row.Flights.Select(f => f.Terms.Single().MetricRef).Should().OnlyContain(r => r == "flightTime");
    }

    [Fact]
    public async Task Per_term_breakdown_carries_absence_never_zero()
    {
        // WI-4(d) + WI-3: NoResult rows carry an empty breakdown (never a
        // zero); pending flights are omitted with the diagnostics unchanged;
        // flight-gate-zeroed flights carry the interpreter's own zeroing
        // (present flight, empty terms).
        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var definition = ClassDefinitionFixtures.AbsenceSemantics();

        var (competition, competitionId, group, competitors) =
            SeedDrawnGroup(store, entryQuery, definition, 3);

        // Competitor 0: the pending shape — the exception only, so flightTime
        // is awaited (tier 2) and the row is NoResult.
        OpenFlownEntry(store, entryQuery, definition, competition, competitionId, group, competitors[0],
            [("landedOut", MeasuredValue.Of(true))]);
        // Competitor 1: gate-zeroed — full capture, but landed out, so the
        // flight stays selected at 0 with the interpreter's empty terms.
        OpenFlownEntry(store, entryQuery, definition, competition, competitionId, group, competitors[1],
            [("flightTime", MeasuredValue.Of(600m)), ("landedOut", MeasuredValue.Of(true))]);
        // Competitor 2: clean.
        OpenFlownEntry(store, entryQuery, definition, competition, competitionId, group, competitors[2],
            [("flightTime", MeasuredValue.Of(600m))]);

        var views = await ScoreSeededGroup(store, entryQuery, competitionId);
        var view = views.Should().ContainSingle().Subject;

        var awaiting = view.Results.Single(r => r.CompetitorRef == competitors[0]);
        awaiting.State.Should().Be(TaskResultState.NoResult);
        awaiting.Flights.Should().BeEmpty();
        awaiting.AwaitingCapture.Should().Equal(new PendingFlightDiagnostic(1, "flightTime"));

        var zeroed = view.Results.Single(r => r.CompetitorRef == competitors[1]);
        zeroed.State.Should().Be(TaskResultState.Valid);
        zeroed.RawScore.Should().Be(0m);
        var zeroedFlight = zeroed.Flights.Should().ContainSingle().Subject;
        zeroedFlight.Sequence.Should().Be(1);
        zeroedFlight.Terms.Should().BeEmpty();

        var clean = view.Results.Single(r => r.CompetitorRef == competitors[2]);
        clean.State.Should().Be(TaskResultState.Valid);
        clean.AwaitingCapture.Should().BeEmpty();
        var cleanTerm = clean.Flights.Should().ContainSingle().Subject
            .Terms.Should().ContainSingle().Subject;
        cleanTerm.MetricRef.Should().Be("flightTime");
        cleanTerm.Points.Should().Be(600m);

        // The wire stays additive and serialisable: the NoResult row renders
        // an empty flights array, never null.
        var camelCase = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        System.Text.Json.JsonSerializer.Serialize(views, camelCase)
            .Should().Contain("\"flights\":[]");
    }
}
