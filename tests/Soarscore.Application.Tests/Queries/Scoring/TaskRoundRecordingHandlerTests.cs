// kanban/in-progress/metric-absence-semantics.md WI-3. Covers
// GetTaskRoundRecordingHandler directly against a FakeEventStore +
// FakeEntryQuery, same style as ScoreTaskRoundHandlerTests — the handler runs
// the REAL walk (CompetitionLoader -> entry_index slice -> EntryLoader ->
// RecordingCore) with the referenced-metric set taken straight from the
// Domain's FlightMetricResolution. The assertions are the two WI-3 behaviours
// RecordingCore's own property tests cannot see: the task's declared metrics
// ride the view with their optional assumed values, and the awaited/assumed
// distinction is wired from the Domain walk, not re-derived here.

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
using Xunit;

// Shared/Competitions has a FakeEventStore of its own (BindParameterHandler
// Tests.cs's precedent) — alias the Entries copy this file seeds with.
using Soarscore.Application.Tests.Shared.Entries;
using FakeEventStore = Soarscore.Application.Tests.Shared.Entries.FakeEventStore;

namespace Soarscore.Application.Tests.Queries.Scoring;

public class TaskRoundRecordingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 9, 0, 0, TimeSpan.Zero);

    private const int PhaseOrdinal = 0;
    private const int RoundOrdinal = 1;
    private const int TaskRoundOrdinal = 1;

    /// <summary>Seeds the one-group competition through its decide functions
    /// (ScoreTaskRoundHandlerTests's precedent), opens one flown Entry
    /// capturing exactly the given (metric, value) pairs, appends every stream,
    /// and hands back what the query needs.</summary>
    private static (
        FakeEventStore Store,
        FakeEntryQuery EntryQuery,
        CompetitionId CompetitionId,
        GroupId GroupRef,
        CompetitorId CompetitorRef)
        SeedOneFlownEntry(
            ClassDefinition definition,
            params (string Metric, MeasuredValue Value)[] captures)
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
            competitionId, "Recording Absence Comp", "Nowhere",
            new DateOnly(2026, 9, 6), new DateOnly(2026, 9, 7),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);
        var competitionEvents = new List<IDomainEvent> { created };
        var store = new FakeEventStore();

        var competitorId = CompetitorId.New();
        var registered = competition.RegisterCompetitor(competitorId, PersonId.New(), Now);
        registered.IsSuccess.Should().BeTrue();
        competitionEvents.Add(registered.Value);
        competition = competition.Apply(registered.Value);

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

        var taskDefinition = definition.Phases[0].Tasks[0];

        var opened = competition.OpenEntry(
            EntryId.New(), PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            group.Id, competitorId, ReflightRole.Original, Now);
        opened.IsSuccess.Should().BeTrue();

        var entry = Entry.Create(opened.Value);

        var flightOpened = entry.OpenFlight(1, null, Now);
        flightOpened.IsSuccess.Should().BeTrue();
        var entryEvents = new List<IDomainEvent> { opened.Value, flightOpened.Value };
        entry = entry.Apply(flightOpened.Value);

        foreach (var (metric, value) in captures)
        {
            var captured = entry.CaptureMeasurement(1, metric, value, Now, taskDefinition.Metrics);
            captured.IsSuccess.Should().BeTrue();
            entryEvents.Add(captured.Value);
            entry = entry.Apply(captured.Value);
        }

        store.AppendAsync(entry.Id.Value, ExpectedVersion.NoStream, entryEvents)
            .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

        var entryQuery = new FakeEntryQuery();
        entryQuery.Seed(new EntrySummary(
            entry.Id, competitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
            group.Id, competitorId, ReflightRole.Original));

        return (store, entryQuery, competitionId, group.Id, competitorId);
    }

    private static TaskRoundRecordingView AskAsync(
        FakeEventStore store, FakeEntryQuery entryQuery, CompetitionId competitionId)
    {
        var handler = new GetTaskRoundRecordingHandler(store, entryQuery);

        var result = handler.HandleAsync(
            new GetTaskRoundRecording(competitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal, null),
            TestContext.Current.CancellationToken);

        result.GetAwaiter().GetResult().IsSuccess.Should().BeTrue();
        return result.GetAwaiter().GetResult().Value;
    }

    private static GroupRecordingView TheGroup(TaskRoundRecordingView view) =>
        view.Groups.Should().ContainSingle().Subject;

    [Fact]
    public void The_declared_metrics_carry_their_assumed_values_and_a_missing_assumed_metric_is_not_awaited()
    {
        // The clean-flight shape: the time recorded only. landedOut is absent
        // but its absence resolves to the declared assumption — a recorded
        // fact on the gap list, never an awaited metric.
        var (store, entryQuery, competitionId, _, _) = SeedOneFlownEntry(
            ClassDefinitionFixtures.AbsenceSemantics(),
            ("flightTime", MeasuredValue.Of(600m)));

        var view = AskAsync(store, entryQuery, competitionId);

        view.Metrics.Select(m => m.Name).Should().Equal(["flightTime", "landedOut"]);
        view.Metrics[0].WhenNotRecorded.Should().BeNull();
        view.Metrics[1].WhenNotRecorded.Should().Be(MeasuredValue.Of(false));

        var flightGaps = TheGroup(view).MetricGaps.Should().ContainSingle().Subject
            .Flights.Should().ContainSingle().Subject;
        flightGaps.MissingMetrics.Should().Equal(["landedOut"]);
        flightGaps.AwaitingCapture.Should().BeEmpty();
    }

    [Fact]
    public void A_flight_missing_an_unassumed_referenced_metric_reads_as_awaiting_capture()
    {
        // The exception shape: landedOut recorded, the time still to come.
        // flightTime is referenced (score term) and declared without an
        // assumption, so the recording view says what scoring says: awaiting.
        var (store, entryQuery, competitionId, _, _) = SeedOneFlownEntry(
            ClassDefinitionFixtures.AbsenceSemantics(),
            ("landedOut", MeasuredValue.Of(true)));

        var view = AskAsync(store, entryQuery, competitionId);

        var flightGaps = TheGroup(view).MetricGaps.Should().ContainSingle().Subject
            .Flights.Should().ContainSingle().Subject;
        flightGaps.Sequence.Should().Be(1);
        flightGaps.MissingMetrics.Should().Equal(["flightTime"]);
        flightGaps.AwaitingCapture.Should().Equal(["flightTime"]);
    }
}
