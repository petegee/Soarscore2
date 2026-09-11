// kanban/in-progress/competition-event-log-endpoint.md WI-1/WI-2/WI-4.
// Mirrors GetCompetitionHandlerTests.cs's fake-driven style: fake event store,
// entry_index and people query, no store, no HTTP. (The repo's doubles are
// namespace-local by precedent, so the aliases below disambiguate them.)
//
// The property test pins the story's named invariant — log round-trip
// completeness and order: for any generated competition log, the view holds
// every appended event exactly once, in stream version order within each
// stream, with the competition stream first and entries in contest order.

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.People;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

using EntryDoubles = Soarscore.Application.Tests.Shared.Entries;
using PeopleDoubles = Soarscore.Application.Tests.Shared.People;

namespace Soarscore.Application.Tests.Queries.Competitions;

// A stand-in for an event kind a newer binary appended — named by its own
// union declaration (the same reflection path every real event takes) but
// unknown to the summariser, proving the graceful-degradation rule.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(FutureEvent), "futureEvent")]
public abstract record FutureEventBase : IDomainEvent
{
    private protected FutureEventBase() { }
}

public sealed record FutureEvent : FutureEventBase;

public class GetCompetitionEventLogHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetCompetitionEventLog_for_an_unknown_id_fails_with_competition_notFound()
    {
        var handler = new GetCompetitionEventLogHandler(
            new EntryDoubles.FakeEventStore(), new EntryDoubles.FakeEntryQuery(), new PeopleDoubles.FakePeopleQuery());

        var result = await handler.HandleAsync(new GetCompetitionEventLog(CompetitionId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Compact_log_lists_the_competition_stream_first_with_versions_and_no_payloads()
    {
        var (store, entries, people, id) = Scenario();

        var handler = new GetCompetitionEventLogHandler(store, entries, people);
        var result = await handler.HandleAsync(new GetCompetitionEventLog(id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var view = result.Value;
        view.Name.Should().Be("Nationals");
        view.Streams.Should().HaveCount(1);

        var competition = view.Streams[0];
        competition.Kind.Should().Be("competition");
        competition.Label.Should().BeNull();
        competition.StreamId.Should().Be(id.Value);
        competition.Events.Select(e => e.Version).Should().Equal([1L, 2L, 3L]);
        competition.Events.Select(e => e.Name).Should().Equal(
            ["competitionCreated", "competitorRegistered", "taskRoundCompleted"]);
        competition.Events.Should().OnlyContain(e => e.Payload == null);
        competition.Events[1].Summary.Should().Be("John Smith (#3) registered into the field");
        competition.Events[2].Summary.Should().Be("Task-round 0.1.1 completed");
    }

    [Fact]
    public async Task Entry_streams_merge_in_contest_order_regardless_of_capture_order()
    {
        var (store, entries, people, id, first, second) = ScenarioWithTwoEntries();

        var handler = new GetCompetitionEventLogHandler(store, entries, people);
        var result = await handler.HandleAsync(new GetCompetitionEventLog(id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Streams.Should().HaveCount(3);
        result.Value.Streams.Skip(1).Select(s => s.StreamId).Should().Equal([first.Id.Value, second.Id.Value]);
        result.Value.Streams[1].Label.Should().Be("John Smith (#3) — task-round 0.1.1");
        result.Value.Streams[1].Events.Select(e => e.Name).Should().Equal(
            ["entryOpened", "flightOpened", "measurementCaptured"]);
        result.Value.Streams[1].Events[2].Summary.Should().Be("Flight 1: flightTime = 47.3 (on stopwatch)");
    }

[Fact]
    public async Task A_competitor_absent_from_the_fold_degrades_in_the_label()
    {
        var (store, entries, people, id, _, second) = ScenarioWithTwoEntries();
        var entryQuery = new EntryDoubles.FakeEntryQuery();
        entryQuery.Seed(second);

        var handler = new GetCompetitionEventLogHandler(store, entryQuery, people);
        var result = await handler.HandleAsync(new GetCompetitionEventLog(id), TestContext.Current.CancellationToken);

        result.Value.Streams.Should().HaveCount(2);
        result.Value.Streams[1].Label.Should().StartWith($"competitor {second.CompetitorRef.Value}");
    }

    [Fact]
    public async Task A_missing_person_degrades_to_the_competitor_number()
    {
        var (store, entries, _, id) = Scenario();
        // personRef is deliberately not in the read model this time.

        var handler = new GetCompetitionEventLogHandler(store, entries, new PeopleDoubles.FakePeopleQuery());
        var result = await handler.HandleAsync(new GetCompetitionEventLog(id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Streams[0].Events[1].Summary.Should().Be("competitor #3 registered into the field");
    }

    [Fact]
    public async Task IncludePayload_adds_the_serialised_event_whose_discriminator_matches_the_name()
    {
        var (store, entries, people, id) = Scenario();

        var handler = new GetCompetitionEventLogHandler(store, entries, people);
        var result = await handler.HandleAsync(new GetCompetitionEventLog(id, IncludePayload: true), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var everyEvent = result.Value.Streams.SelectMany(s => s.Events).ToList();
        everyEvent.Should().OnlyContain(e => e.Payload != null);
        everyEvent.All(e =>
        {
            var kind = e.Payload!.Value.GetProperty("$kind");
            return kind.GetString() == e.Name;
        }).Should().BeTrue();
    }

    [Fact]
    public async Task An_event_kind_outside_the_known_contracts_degrades_to_name_only()
    {
        var id = CompetitionId.New();
        var store = new EntryDoubles.FakeEventStore();
        await store.AppendAsync(id.Value, ExpectedVersion.NoStream, [new CompetitionCreated(
            id, "Nationals", "Taupo", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13), "1",
            BuildAdoptedRules(), Now)], TestContext.Current.CancellationToken);

        // The realistic shape of a future event: appended by a newer binary on
        // an entry stream, which this reader loads raw. (The competition fold
        // casts to CompetitionEvent exactly as CompetitionLoader does — a
        // foreign event there is out of this story's scope.)
        var entryId = new EntryId(Guid.CreateVersion7());
        var summary = new EntrySummary(
            entryId, id, 0, 1, 1, GroupId.New(), CompetitorId.New(), ReflightRole.Original);
        await store.AppendAsync(entryId.Value, ExpectedVersion.NoStream,
        [
            new EntryOpened(entryId, id, 0, 1, 1, summary.GroupRef, summary.CompetitorRef, ReflightRole.Original, Now),
            new FutureEvent(),
        ], TestContext.Current.CancellationToken);
        var entryQuery = new EntryDoubles.FakeEntryQuery();
        entryQuery.Seed(summary);

        var handler = new GetCompetitionEventLogHandler(store, entryQuery, new PeopleDoubles.FakePeopleQuery());
        var result = await handler.HandleAsync(new GetCompetitionEventLog(id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var future = result.Value.Streams[1].Events[1];
        future.Name.Should().Be("futureEvent");
        future.Summary.Should().BeNull();
    }

    // WI-1's named invariant, as a property: log round-trip completeness and
    // order. Any generated log — arbitrary competition events, arbitrary
    // entry streams at arbitrary contest coordinates — comes back complete,
    // exactly once per event, in stream version order, competition first,
    // entries in the handler's contest order.
    [Fact]
    public void Property_every_generated_event_appears_exactly_once_in_stream_order()
    {
        var gen =
            from competitionStream in CompetitionStream
            from entryStreams in EntryStreams
            select (competitionStream, entryStreams);

        gen.Sample(generated =>
        {
            var id = CompetitionId.New();
            var store = new EntryDoubles.FakeEventStore();
            store.AppendAsync(id.Value, ExpectedVersion.NoStream, generated.competitionStream, TestContext.Current.CancellationToken).Wait();
            var entryQuery = new EntryDoubles.FakeEntryQuery();
            foreach (var (summary, events) in generated.entryStreams)
            {
                var scoped = summary with { CompetitionRef = id };
                entryQuery.Seed(scoped);
                store.AppendAsync(summary.Id.Value, ExpectedVersion.NoStream, events, TestContext.Current.CancellationToken).Wait();
            }

            var people = new PeopleDoubles.FakePeopleQuery();
            foreach (var personRef in generated.competitionStream.OfType<CompetitorRegistered>().Select(e => e.Competitor.PersonRef).Distinct())
            {
                people.Seed(new PersonSummary(personRef, "John Smith", "john@test", null, null, null));
            }

            var handler = new GetCompetitionEventLogHandler(store, entryQuery, people);
            var result = handler.HandleAsync(new GetCompetitionEventLog(id), TestContext.Current.CancellationToken).Result;
            if (result.IsFailure)
            {
                return false;
            }

            var view = result.Value;
            if (view.Streams[0].Kind != "competition" || view.Streams[0].Events.Length != generated.competitionStream.Length)
            {
                return false;
            }

            for (var i = 0; i < generated.competitionStream.Length; i++)
            {
                var actual = view.Streams[0].Events[i];
                if (actual.Name != EventName.Of(generated.competitionStream[i]) || actual.Version != i + 1)
                {
                    return false;
                }
            }

            // The handler's contest order — phase, round, task-round, then the
            // competitor number where the fold knows it (none of these are in
            // the fold, so it never bites here), then the entry id.
            var ordered = generated.entryStreams
                .OrderBy(e => e.Summary.PhaseOrdinal)
                .ThenBy(e => e.Summary.RoundOrdinal)
                .ThenBy(e => e.Summary.TaskRoundOrdinal)
                .ThenBy(e => e.Summary.Id.Value)
                .ToList();
            for (var s = 0; s < ordered.Count; s++)
            {
                var (summary, events) = ordered[s];
                var stream = view.Streams[s + 1];
                if (stream.StreamId != summary.Id.Value || stream.Events.Length != events.Length)
                {
                    return false;
                }

                for (var i = 0; i < events.Length; i++)
                {
                    if (stream.Events[i].Name != EventName.Of(events[i]) || stream.Events[i].Version != i + 1)
                    {
                        return false;
                    }
                }
            }

            return true;
        });
    }

    // A competition stream: CompetitionCreated first (the fold requires it),
    // then a pool of CompetitorRegistered at varied lengths.
    private static Gen<ImmutableArray<CompetitionEvent>> CompetitionStream =>
        from extra in Gen.Int[0, 6]
        select ImmutableArray.CreateRange<CompetitionEvent>(
        [
            new CompetitionCreated(
                CompetitionId.New(), "Nationals", "Taupo", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13), "1",
                BuildAdoptedRules(), Now),
            ..Enumerable.Range(0, extra).Select(i => (CompetitionEvent)new CompetitorRegistered(
                new Competitor
                {
                    Id = CompetitorId.New(),
                    PersonRef = PersonId.New(),
                    CompetitorNumber = i + 1,
                    RegisteredAt = Now,
                }, Now)),
        ]);

    // Entry streams at varied task-round coordinates, each opening and then
    // either capturing a flight or annulling.
    private static readonly Gen<(int Phase, int Round, int TaskRound, int Kind)> Shape =
        from phase in Gen.Int[0, 1]
        from taskRound in Gen.Int[1, 2]
        from kind in Gen.Int[0, 1]
        select (Phase: phase, Round: 1, TaskRound: taskRound, Kind: kind);

    private static Gen<ImmutableArray<(EntrySummary Summary, ImmutableArray<EntryEvent> Events)>> EntryStreams =>
        from shapes in Shape.Array[0, 4]
        select ImmutableArray.CreateRange(
            shapes.Select(shape =>
            {
                var entryId = new EntryId(Guid.CreateVersion7());
                var summary = new EntrySummary(
                    entryId, CompetitionId.New(), shape.Phase, shape.Round, shape.TaskRound,
                    GroupId.New(), CompetitorId.New(), ReflightRole.Original);
                var events = shape.Kind == 0
                    ? ImmutableArray.CreateRange<EntryEvent>(
                    [
                        new EntryOpened(entryId, summary.CompetitionRef, shape.Phase, shape.Round, shape.TaskRound,
                            summary.GroupRef, summary.CompetitorRef, ReflightRole.Original, Now),
                        new FlightOpened(1, Now),
                    ])
                    : ImmutableArray.CreateRange<EntryEvent>(
                    [
                        new EntryOpened(entryId, summary.CompetitionRef, shape.Phase, shape.Round, shape.TaskRound,
                            summary.GroupRef, summary.CompetitorRef, ReflightRole.Original, Now),
                        new EntryAnnulled(new Annulment { Reason = "mid-air", By = "CD", At = Now }),
                    ]);
                return (summary, events);
            }));

    private static AdoptedRules BuildAdoptedRules() =>
        new()
        {
            Definition = Soarscore.Application.Tests.Shared.CompetitionClasses.ClassDefinitionFixtures.Minimal(),
            SourceClassId = "test-hash",
            SourceVersion = "v1",
            AdoptedAt = Now,
        };

    private static (EntryDoubles.FakeEventStore Store, EntryDoubles.FakeEntryQuery Entries,
        PeopleDoubles.FakePeopleQuery People, CompetitionId Id) Scenario()
    {
        var id = CompetitionId.New();
        var competitorRef = CompetitorId.New();
        var personRef = PersonId.New();
        var store = new EntryDoubles.FakeEventStore();
        store.AppendAsync(id.Value, ExpectedVersion.NoStream,
        [
            new CompetitionCreated(
                id, "Nationals", "Taupo", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13), "1",
                BuildAdoptedRules(), Now),
            new CompetitorRegistered(
                new Competitor { Id = competitorRef, PersonRef = personRef, CompetitorNumber = 3, RegisteredAt = Now }, Now),
            new TaskRoundCompleted(0, 1, 1, Now),
        ], TestContext.Current.CancellationToken).Wait();
        var people = new PeopleDoubles.FakePeopleQuery();
        people.Seed(new PersonSummary(personRef, "John Smith", "john@test", null, null, null));
        return (store, new EntryDoubles.FakeEntryQuery(), people, id);
    }

private static (EntryDoubles.FakeEventStore Store, EntryDoubles.FakeEntryQuery Entries,
        PeopleDoubles.FakePeopleQuery People, CompetitionId Id, EntrySummary First, EntrySummary Second)
        ScenarioWithTwoEntries()
    {
        var (store, _, people, id) = Scenario();
        var competitorRef = store.Streams[id.Value].OfType<CompetitorRegistered>().Single().Competitor.Id;
        var first = new EntrySummary(
            new EntryId(Guid.CreateVersion7()), id, 0, 1, 1, GroupId.New(), competitorRef, ReflightRole.Original);
        var secondCompetitor = CompetitorId.New();
        var second = new EntrySummary(
            new EntryId(Guid.CreateVersion7()), id, 0, 1, 1, GroupId.New(), secondCompetitor, ReflightRole.Filler);
        store.AppendAsync(first.Id.Value, ExpectedVersion.NoStream,
        [
            new EntryOpened(first.Id, id, 0, 1, 1, first.GroupRef, competitorRef, ReflightRole.Original, Now),
            new FlightOpened(1, Now),
            new MeasurementCaptured(1, new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(47.3m), Instrument = "stopwatch", CapturedAt = Now }),
        ], TestContext.Current.CancellationToken).Wait();
        store.AppendAsync(second.Id.Value, ExpectedVersion.NoStream,
        [
            new EntryOpened(second.Id, id, 0, 1, 1, second.GroupRef, secondCompetitor, ReflightRole.Filler, Now),
            new EntryAnnulled(new Annulment { Reason = "mid-air", By = "CD", At = Now }),
        ], TestContext.Current.CancellationToken).Wait();
        var entries = new EntryDoubles.FakeEntryQuery();
        entries.Seed(first);
        entries.Seed(second);
        return (store, entries, people, id, first, second);
    }
}