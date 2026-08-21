// kanban/in-progress/annul-and-penalise-the-second-entry-thread.md WI-7. Covers
// RecordCompetitionPenaltyHandler directly against a FakeEventStore — the plain
// BindParameter template (CompetitionLoader -> decide -> append), same style as
// WithdrawCompetitorHandlerTests.cs. The adopted F5K definition declares
// safetyZone (DeductPoints 300). Covers the payload round-trip, the domain
// codes surfaced unchanged, and the concurrency append.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class RecordCompetitionPenaltyHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F5K = SeedF5K.Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F5K,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F5K.Version,
            AdoptedAt = Now,
        };

    /// <summary>CompetitionCreated → CompetitorRegistered → PhaseDrawn, one competitor in one group.</summary>
    private static (FakeEventStore Store, CompetitionId CompetitionId, CompetitorId CompetitorId) SeedCompetition()
    {
        var store = new FakeEventStore();
        var competitionId = CompetitionId.New();
        var created = new CompetitionCreated(
            competitionId, "Club Champs 2026", "Auckland",
            new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();

        var competitor = new Competitor
        {
            Id = CompetitorId.New(), PersonRef = PersonId.New(), CompetitorNumber = 1, RegisteredAt = Now,
        };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(1), [new CompetitorRegistered(competitor, Now)]).GetAwaiter().GetResult();

        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [competitor.Id] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(2),
            [new PhaseDrawn(0, PhaseType.Preliminary, new Draw { CreatedAt = Now, Status = "drawn" }, [round], Now)])
            .GetAwaiter().GetResult();

        return (store, competitionId, competitor.Id);
    }

    [Fact]
    public async Task Recording_a_competition_penalty_appends_a_PenaltyRecorded_with_the_payload()
    {
        var (store, competitionId, competitorId) = SeedCompetition();
        var handler = new RecordCompetitionPenaltyHandler(store);

        var result = await handler.HandleAsync(
            new RecordCompetitionPenalty(competitionId, "safetyZone", PenaltyScope.Competition,
                competitorId, new TaskRoundCoordinate(0, 1, 1), "the contest director"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(competitionId);

        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(4); // Created + Registered + Drawn + PenaltyRecorded
        var recorded = stream[3].Should().BeOfType<Domain.Competitions.PenaltyRecorded>().Subject;
        recorded.Penalty.InfractionType.Should().Be("safetyZone");
        recorded.Penalty.Scope.Should().Be(PenaltyScope.Competition);
        recorded.Penalty.CompetitorRef.Should().Be(competitorId);
        recorded.Penalty.TaskRound.Should().Be(new TaskRoundCoordinate(0, 1, 1));
        recorded.Penalty.By.Should().Be("the contest director");
    }

    [Fact]
    public async Task Recording_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var handler = new RecordCompetitionPenaltyHandler(store);

        var result = await handler.HandleAsync(
            new RecordCompetitionPenalty(CompetitionId.New(), "safetyZone", PenaltyScope.Competition,
                CompetitorId.New(), null, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Recording_against_an_unknown_competitor_fails_with_the_domain_code_surfaced_unchanged()
    {
        var (store, competitionId, _) = SeedCompetition();
        var handler = new RecordCompetitionPenaltyHandler(store);

        var result = await handler.HandleAsync(
            new RecordCompetitionPenalty(competitionId, "safetyZone", PenaltyScope.Competition,
                CompetitorId.New(), null, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.competitor.notFound");
    }

    [Fact]
    public async Task Recording_an_undeclared_infraction_type_fails_with_the_domain_code_surfaced_unchanged()
    {
        var (store, competitionId, competitorId) = SeedCompetition();
        var handler = new RecordCompetitionPenaltyHandler(store);

        var result = await handler.HandleAsync(
            new RecordCompetitionPenalty(competitionId, "madeUp", PenaltyScope.Competition, competitorId, null, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.infractionTypeNotDeclared");
    }

    [Fact]
    public async Task A_stale_read_version_fails_with_eventStore_concurrencyConflict_on_append()
    {
        var (store, competitionId, competitorId) = SeedCompetition();

        // Another mutation landed for real between this handler's read and its
        // append.
        await store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(3),
            [new TaskRoundCompleted(0, 1, 1, Now)], TestContext.Current.CancellationToken);

        var staleReadStore = new StaleReadEventStore(store, competitionId.Value, visibleCount: 3);
        var handler = new RecordCompetitionPenaltyHandler(staleReadStore);

        var result = await handler.HandleAsync(
            new RecordCompetitionPenalty(competitionId, "safetyZone", PenaltyScope.Competition, competitorId, null, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("eventStore.concurrencyConflict");
    }

    private sealed class StaleReadEventStore(IEventStore inner, Guid staleStreamId, int visibleCount) : IEventStore
    {
        public Task<Result<long>> AppendAsync(
            Guid streamId, ExpectedVersion expected, IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default) =>
            inner.AppendAsync(streamId, expected, events, cancellationToken);

        public async Task<Result<IReadOnlyList<IDomainEvent>>> ReadStreamAsync(
            Guid streamId, long fromVersion, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadStreamAsync(streamId, fromVersion, cancellationToken);
            if (read.IsFailure || streamId != staleStreamId)
            {
                return read;
            }

            return Result<IReadOnlyList<IDomainEvent>>.Success(read.Value.Take(visibleCount).ToList());
        }

        public Task<Result<IReadOnlyList<RecordedEvent>>> ReadAllAsync(
            long fromPosition, int batchSize, CancellationToken cancellationToken = default) =>
            inner.ReadAllAsync(fromPosition, batchSize, cancellationToken);
    }
}