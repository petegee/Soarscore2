// kanban/completed/entry-completeness-indicator.md WI-4 — store-backed tests
// for GetTaskRoundRecordingHandler, generic over IStoreFixture so they run
// unchanged against PostgreSQL (Testcontainers, Storage trait) and Fisher/
// SQLite. Same discipline as ScoringEventStoreTests.cs: real handlers against
// fixture.EventStore / fixture.EntryQuery, no dispatcher, no HTTP.
//
// F5J (30-f5j) throughout, for the same two reasons the scoring tests give:
// literal MinPerGroup 6 makes a 6-pilot field draw to exactly one group, and
// its task declares six real metrics — so "captures only flightTime" leaves a
// concrete five-metric gap list whose order is the task's declared order:
// startHeight, startHeightRecorded, landingDistance, overflySeconds,
// touchedByCompetitor.

using AwesomeAssertions;
using Soarscore.Application;
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
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class TaskRoundRecordingEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    // SeedF5J's FlightMetrics minus flightTime, in declared order.
    private static readonly string[] NonFlightTimeMetrics =
    [
        "startHeight", "startHeightRecorded", "landingDistance", "overflySeconds", "touchedByCompetitor",
    ];

    // ---------------------------------------------------------------- setup

    private static async Task<(CompetitionId CompetitionId, List<CompetitorId> Competitors)> SetUpAsync(
        IStoreFixture fixture, string name, int competitors, int rounds)
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(F5JDefinition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition(name, "Taupo", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 25), published.Value),
            TestContext.Current.CancellationToken);
        created.IsSuccess.Should().BeTrue();

        var list = new List<CompetitorId>();
        for (var i = 0; i < competitors; i++)
        {
            var registerPersonHandler = new RegisterPersonHandler(fixture.EventStore, new SystemClock());
            var person = await registerPersonHandler.HandleAsync(
                new RegisterPerson(
                    "Test Pilot",
                    new ContactDetails { Email = $"pilot-recording-{name}-{i}@example.com".ToLowerInvariant() },
                    Club: null),
                TestContext.Current.CancellationToken);
            person.IsSuccess.Should().BeTrue();

            var registerCompetitorHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
            var competitor = await registerCompetitorHandler.HandleAsync(
                new RegisterCompetitor(created.Value, person.Value), TestContext.Current.CancellationToken);
            competitor.IsSuccess.Should().BeTrue();
            list.Add(competitor.Value);
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(created.Value, rounds), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue();

        // D4 (kanban/in-progress/draw-acceptance-redraw.md): an entry cannot
        // open against a drawn-but-not-accepted competition — every scenario
        // below records entries, so the CD accepts right after the draw.
        var accepted = await new AcceptDrawHandler(fixture.EventStore, new SystemClock())
            .HandleAsync(new AcceptDraw(created.Value), TestContext.Current.CancellationToken);
        accepted.IsSuccess.Should().BeTrue($"{accepted.Code}: {accepted.Message}");

        return (created.Value, list);
    }

    private static async Task<Group> SingleGroupOfRound1Async(IStoreFixture fixture, CompetitionId competitionId)
    {
        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();
        return fetched.Value.Competition.Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single();
    }

    /// <summary>Opens the Entry and optionally its first flight, capturing either every F5J
    /// metric or flightTime alone (the partial-transcription shape).</summary>
    private static async Task<EntryId> RecordAsync(
        IStoreFixture fixture,
        CompetitionId competitionId,
        GroupId groupRef,
        CompetitorId competitorRef,
        bool fly = true,
        bool fullMetrics = true)
    {
        var openEntryHandler = new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
        var opened = await openEntryHandler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, groupRef, competitorRef),
            TestContext.Current.CancellationToken);
        opened.IsSuccess.Should().BeTrue($"{opened.Code}: {opened.Message}");

        if (!fly)
        {
            return opened.Value;
        }

        var openFlightHandler = new OpenFlightHandler(fixture.EventStore, new SystemClock());
        var openedFlight = await openFlightHandler.HandleAsync(new OpenFlight(opened.Value), TestContext.Current.CancellationToken);
        openedFlight.IsSuccess.Should().BeTrue();

        var captureHandler = new CaptureMeasurementHandler(fixture.EventStore, new SystemClock());

        async Task CaptureAsync(string metric, MeasuredValue value)
        {
            var captured = await captureHandler.HandleAsync(
                new CaptureMeasurement(opened.Value, 1, metric, value), TestContext.Current.CancellationToken);
            captured.IsSuccess.Should().BeTrue($"{captured.Code}: {captured.Message}");
        }

        await CaptureAsync("flightTime", MeasuredValue.Of(300m));
        if (!fullMetrics)
        {
            return opened.Value;
        }

        await CaptureAsync("startHeight", MeasuredValue.Of(0m));
        await CaptureAsync("startHeightRecorded", MeasuredValue.Of(true));
        await CaptureAsync("landingDistance", MeasuredValue.Of(100m));
        await CaptureAsync("overflySeconds", MeasuredValue.Of(0m));
        await CaptureAsync("touchedByCompetitor", MeasuredValue.Of(false));

        return opened.Value;
    }

    private async Task<TaskRoundRecordingView> AskAsync(CompetitionId competitionId, GroupId? groupRef = null)
    {
        var handler = new GetTaskRoundRecordingHandler(fixture.EventStore, fixture.EntryQuery);
        var asked = await handler.HandleAsync(
            new GetTaskRoundRecording(competitionId, 0, 1, 1, groupRef), TestContext.Current.CancellationToken);
        asked.IsSuccess.Should().BeTrue($"{asked.Code}: {asked.Message}");
        return asked.Value;
    }

    // ---- 1. Full house -----------------------------------------------------

    [Fact]
    public async Task A_fully_recorded_task_round_names_everyone_expected_with_no_gaps()
    {
        var (competitionId, competitors) = await SetUpAsync(fixture, "full-house", 6, 1);
        var group = await SingleGroupOfRound1Async(fixture, competitionId);

        foreach (var competitorRef in competitors)
        {
            await RecordAsync(fixture, competitionId, group.Id, competitorRef);
        }

        var view = await AskAsync(competitionId);

        view.TaskRef.Should().Be("D");
        view.Groups.Should().ContainSingle();
        var g = view.Groups[0];
        g.GroupRef.Should().Be(group.Id);

        g.ExpectedCompetitorRefs.Should().Equal(group.CompetitorRefs);
        g.NotRecordedCompetitorRefs.Should().BeEmpty();
        g.RecordedWithoutFlightCompetitorRefs.Should().BeEmpty();
        g.MetricGaps.Should().BeEmpty();
    }

    // ---- 2. The absent and the unflown are named ---------------------------

    [Fact]
    public async Task Competitors_without_entries_are_named_and_an_unflown_entry_is_shown_as_such()
    {
        var (competitionId, competitors) = await SetUpAsync(fixture, "absent-unflown", 6, 1);
        var group = await SingleGroupOfRound1Async(fixture, competitionId);

        foreach (var competitorRef in competitors.Take(4))
        {
            await RecordAsync(fixture, competitionId, group.Id, competitorRef);
        }
        await RecordAsync(fixture, competitionId, group.Id, competitors[4], fly: false); // entered, never flown
        // competitors[5]: no entry at all

        var view = await AskAsync(competitionId);

        var g = view.Groups.Single();
        g.ExpectedCompetitorRefs.Should().HaveCount(6);
        g.NotRecordedCompetitorRefs.Should().Equal([competitors[5]]);
        g.RecordedWithoutFlightCompetitorRefs.Should().Equal([competitors[4]]);
        g.MetricGaps.Should().BeEmpty();
    }

    // ---- 3. Partial transcription names its missing metrics ----------------

    [Fact]
    public async Task A_flight_capturing_only_flightTime_is_reported_missing_the_other_five_metrics_in_declared_order()
    {
        var (competitionId, competitors) = await SetUpAsync(fixture, "partial-capture", 6, 1);
        var group = await SingleGroupOfRound1Async(fixture, competitionId);

        await RecordAsync(fixture, competitionId, group.Id, competitors[0], fullMetrics: false);
        foreach (var competitorRef in competitors.Skip(1))
        {
            await RecordAsync(fixture, competitionId, group.Id, competitorRef);
        }

        var view = await AskAsync(competitionId);

        var g = view.Groups.Single();
        g.NotRecordedCompetitorRefs.Should().BeEmpty();
        g.RecordedWithoutFlightCompetitorRefs.Should().BeEmpty();

        var gaps = g.MetricGaps.Should().ContainSingle().Subject;
        gaps.CompetitorRef.Should().Be(competitors[0]);
        gaps.Role.Should().Be(ReflightRole.Original);

        var flightGaps = gaps.Flights.Should().ContainSingle().Subject;
        flightGaps.Sequence.Should().Be(1);
        flightGaps.MissingMetrics.Should().Equal(NonFlightTimeMetrics);
    }

    // ---- 4. Withdrawal after recording removes them everywhere --------------

    [Fact]
    public async Task A_competitor_who_withdraws_after_being_recorded_disappears_from_every_bucket()
    {
        var (competitionId, competitors) = await SetUpAsync(fixture, "withdraw-late", 6, 1);
        var group = await SingleGroupOfRound1Async(fixture, competitionId);

        foreach (var competitorRef in competitors)
        {
            await RecordAsync(fixture, competitionId, group.Id, competitorRef);
        }

        var withdrawHandler = new WithdrawCompetitorHandler(fixture.EventStore, new SystemClock());
        var withdrawn = await withdrawHandler.HandleAsync(
            new WithdrawCompetitor(competitionId, competitors[5]), TestContext.Current.CancellationToken);
        withdrawn.IsSuccess.Should().BeTrue();

        var view = await AskAsync(competitionId);

        var g = view.Groups.Single();
        g.ExpectedCompetitorRefs.Should().Equal(group.CompetitorRefs.Where(c => c != competitors[5]));
        g.NotRecordedCompetitorRefs.Should().BeEmpty();
        g.RecordedWithoutFlightCompetitorRefs.Should().BeEmpty();
        g.MetricGaps.Should().BeEmpty(); // their recorded flight is noise now, not a gap
    }

    // ---- 5. An annulled entry does not record its competitor ----------------

    [Fact]
    public async Task A_competitor_whose_only_entry_is_annulled_reads_as_not_recorded()
    {
        var (competitionId, competitors) = await SetUpAsync(fixture, "annulled-only", 6, 1);
        var group = await SingleGroupOfRound1Async(fixture, competitionId);

        var annulledEntry = await RecordAsync(fixture, competitionId, group.Id, competitors[3]);
        foreach (var competitorRef in competitors.Where((_, i) => i != 3))
        {
            await RecordAsync(fixture, competitionId, group.Id, competitorRef);
        }

        var annulHandler = new AnnulEntryHandler(fixture.EventStore, new SystemClock());
        var annulled = await annulHandler.HandleAsync(
            new AnnulEntry(annulledEntry, "Landed outside the field — reflight under protest", "CD Jane"),
            TestContext.Current.CancellationToken);
        annulled.IsSuccess.Should().BeTrue($"{annulled.Code}: {annulled.Message}");

        var view = await AskAsync(competitionId);

        var g = view.Groups.Single();
        g.ExpectedCompetitorRefs.Should().Equal(group.CompetitorRefs); // annulment is not withdrawal
        g.NotRecordedCompetitorRefs.Should().Equal([competitors[3]]);
        g.MetricGaps.Should().BeEmpty();
    }

    // ---- 6. The GroupRef filter narrows to exactly that group ---------------

    [Fact]
    public async Task Asking_about_one_group_of_two_returns_exactly_that_group()
    {
        // MinPerGroup 6 with a 12-pilot field draws two groups per round.
        var (competitionId, competitors) = await SetUpAsync(fixture, "group-filter", 12, 1);

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();
        var groups = fetched.Value.Competition.Phases.Single().Rounds.Single().TaskRounds.Single().Groups;
        groups.Should().HaveCount(2);

        foreach (var group in groups)
        {
            foreach (var competitorRef in group.CompetitorRefs)
            {
                await RecordAsync(fixture, competitionId, group.Id, competitorRef);
            }
        }

        var second = groups.Single(g => g.Ordinal == 2);
        var view = await AskAsync(competitionId, second.Id);

        view.Groups.Should().ContainSingle();
        var g = view.Groups[0];
        g.GroupRef.Should().Be(second.Id);
        g.ExpectedCompetitorRefs.Should().Equal(second.CompetitorRefs);
        g.NotRecordedCompetitorRefs.Should().BeEmpty();
        g.MetricGaps.Should().BeEmpty();
    }

    // ---- 7. Unknown coordinates are refused ---------------------------------

    [Fact]
    public async Task An_unknown_task_round_is_refused()
    {
        var (competitionId, _) = await SetUpAsync(fixture, "unknown-round", 6, 1);

        var handler = new GetTaskRoundRecordingHandler(fixture.EventStore, fixture.EntryQuery);
        var asked = await handler.HandleAsync(
            new GetTaskRoundRecording(competitionId, 0, 9, 1, null), TestContext.Current.CancellationToken);

        asked.IsFailure.Should().BeTrue();
        asked.Code.Should().Be("taskRoundRecording.taskRoundNotFound");
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresTaskRoundRecordingEventStoreTests(PostgresFixture fixture)
    : TaskRoundRecordingEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteTaskRoundRecordingEventStoreTests(SqliteFixture fixture)
    : TaskRoundRecordingEventStoreTests<SqliteFixture>(fixture);
