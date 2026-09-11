// kanban/in-progress/competition-event-log-endpoint.md WI-4 — the merged-log
// read against a real store, through the real Inline projections and the real
// Document* adapters (fixture.EventStore/PeopleQuery/EntryQuery), generic over
// the fixture so it runs unchanged on every backend — Marten/PostgreSQL and
// Fisher/SQLite — one concrete subclass per backend at the foot of the file
// (CompetitionEventStoreTests.cs's header explains the Storage-trait split).
//
// Events are appended directly rather than driven through commands: this test
// pins the reader's merge/order/labelling behaviour, not the command paths the
// command-side and capture suites already prove. The appended layout is a
// faithful miniature of the real stream graph — one competition stream, one
// person stream per competitor, one stream per entry.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class CompetitionEventLogEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

    // 10-f3k — the same retirement-candidate choice CompetitionEventStoreTests
    // makes: shared container, so a definition no other test in the suite
    // expects to stay unretired.
    private static readonly AdoptedRules Adopted = BuildAdoptedRules();

    [Fact]
    public async Task The_merged_log_returns_the_competition_stream_then_entry_streams_in_contest_order()
    {
        var (competitionId, alice, bob, entryOne, entryTwo) = await AppendScenarioAsync();

        var handler = new GetCompetitionEventLogHandler(fixture.EventStore, fixture.EntryQuery, fixture.PeopleQuery);
        var view = await handler.HandleAsync(new GetCompetitionEventLog(competitionId), TestContext.Current.CancellationToken);
        view.IsSuccess.Should().BeTrue();

        view.Value.Name.Should().Be("Nationals");
        view.Value.Streams.Length.Should().Be(3);
        view.Value.Streams[0].Kind.Should().Be("competition");
        view.Value.Streams[0].Events.Select(e => e.Name).Should().Equal(
            ["competitionCreated", "competitorRegistered", "competitorRegistered", "phaseDrawn", "drawAccepted", "taskRoundCompleted"]);
        view.Value.Streams.Skip(1).Select(s => s.StreamId).Should().Equal([entryOne.Value, entryTwo.Value]);
        view.Value.Streams[1].Label.Should().Be($"Alice Atkins (#1) — task-round 0.1.1");
        view.Value.Streams[2].Label.Should().Be($"Bob Brown (#2) — task-round 0.1.2");
        view.Value.Streams[1].Events.Select(e => e.Name).Should().Equal(
            ["entryOpened", "flightOpened", "measurementCaptured"]);
        view.Value.Streams[1].Events[0].Summary.Should().Be("Alice Atkins (#1) entered task-round 0.1.1");
        view.Value.Streams[1].Events[2].Summary.Should().Be("Flight 1: flightTime = 47.3 (on stopwatch)");
    }

    [Fact]
    public async Task Compact_by_default_and_full_record_with_includePayload()
    {
        var (competitionId, _, _, _, _) = await AppendScenarioAsync();

        var handler = new GetCompetitionEventLogHandler(fixture.EventStore, fixture.EntryQuery, fixture.PeopleQuery);

        var compact = await handler.HandleAsync(new GetCompetitionEventLog(competitionId), TestContext.Current.CancellationToken);
        compact.Value.Streams.SelectMany(s => s.Events).Should().OnlyContain(e => e.Payload == null);

        var full = await handler.HandleAsync(new GetCompetitionEventLog(competitionId, IncludePayload: true), TestContext.Current.CancellationToken);
        var events = full.Value.Streams.SelectMany(s => s.Events).ToList();
        events.Should().OnlyContain(e => e.Payload != null);
        events.All(e => e.Payload!.Value.GetProperty("$kind").GetString() == e.Name).Should().BeTrue();
    }

    [Fact]
    public async Task An_unknown_competition_id_fails_with_competition_notFound()
    {
        var handler = new GetCompetitionEventLogHandler(fixture.EventStore, fixture.EntryQuery, fixture.PeopleQuery);

        var result = await handler.HandleAsync(new GetCompetitionEventLog(CompetitionId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    // One competition, two competitors, a drawn and accepted phase with two
    // task-rounds, one entry each, one captured measurement. Returns the ids
    // the assertions address.
    private async Task<(CompetitionId CompetitionId, CompetitorId Alice, CompetitorId Bob, EntryId EntryOne, EntryId EntryTwo)>
        AppendScenarioAsync()
    {
        var at = DateTimeOffset.UtcNow;
        var alicePerson = PersonId.New();
        var bobPerson = PersonId.New();
        var alice = CompetitorId.New();
        var bob = CompetitorId.New();
        var groupOne = GroupId.New();
        var groupTwo = GroupId.New();
        var competitionId = CompetitionId.New();

        await AppendAsync(alicePerson.Value, [new PersonRegistered(
            alicePerson, "Alice Atkins", new ContactDetails { Email = $"alice-{Guid.NewGuid():N}@test" }, null, DateTimeOffset.UtcNow)]);
        await AppendAsync(bobPerson.Value, [new PersonRegistered(
            bobPerson, "Bob Brown", new ContactDetails { Email = $"bob-{Guid.NewGuid():N}@test" }, null, DateTimeOffset.UtcNow)]);
        await AppendAsync(competitionId.Value,
        [
            new CompetitionCreated(competitionId, "Nationals", "Taupo", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13), "1", Adopted, at),
            new CompetitorRegistered(new Competitor { Id = alice, PersonRef = alicePerson, CompetitorNumber = 1, RegisteredAt = at }, at),
            new CompetitorRegistered(new Competitor { Id = bob, PersonRef = bobPerson, CompetitorNumber = 2, RegisteredAt = at }, at),
            new PhaseDrawn(0, PhaseType.Preliminary, new Draw { CreatedAt = at, Status = "drawn" },
                [new Round
                {
                    Ordinal = 1,
                    TaskRounds =
                    [
                        new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [new Group { Id = groupOne, Ordinal = 1, CompetitorRefs = [alice] }] },
                        new TaskRound { Ordinal = 2, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [new Group { Id = groupTwo, Ordinal = 1, CompetitorRefs = [bob] }] },
                    ],
                }], at),
            new DrawAccepted(0, at),
            new TaskRoundCompleted(0, 1, 1, at),
        ]);

        var entryOne = new EntryId(Guid.CreateVersion7());
        var entryTwo = new EntryId(Guid.CreateVersion7());
        await AppendAsync(entryOne.Value,
        [
            new EntryOpened(entryOne, competitionId, 0, 1, 1, groupOne, alice, ReflightRole.Original, at),
            new FlightOpened(1, at),
            new MeasurementCaptured(1, new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(47.3m), Instrument = "stopwatch", CapturedAt = at }),
        ]);
        await AppendAsync(entryTwo.Value,
        [
            new EntryOpened(entryTwo, competitionId, 0, 1, 2, groupTwo, bob, ReflightRole.Original, at),
        ]);

        return (competitionId, alice, bob, entryOne, entryTwo);
    }

    private async Task AppendAsync(Guid streamId, IReadOnlyList<IDomainEvent> events)
    {
        var append = await fixture.EventStore.AppendAsync(streamId, ExpectedVersion.Any, events, TestContext.Current.CancellationToken);
        append.IsSuccess.Should().BeTrue(append.Message);
    }

    private static AdoptedRules BuildAdoptedRules() =>
        new()
        {
            Definition = Corpus.All.Single(c => c.FileName == "10-f3k").Definition,
            SourceClassId = "f3k-content-hash",
            SourceVersion = "v1",
            AdoptedAt = DateTimeOffset.UtcNow,
        };
}

[Trait("Category", "Storage")]
public sealed class PostgresCompetitionEventLogEventStoreTests(PostgresFixture fixture) : CompetitionEventLogEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteCompetitionEventLogEventStoreTests(SqliteFixture fixture) : CompetitionEventLogEventStoreTests<SqliteFixture>(fixture);