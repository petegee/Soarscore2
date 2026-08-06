// docs/plans/register-competitor-steel-thread-plan.md WI-3. Covers
// WithdrawCompetitorHandler directly against a FakeEventStore — same style as
// RegisterCompetitorHandlerTests.cs, but no person lookup: withdrawal
// addresses a CompetitorId that, by construction, is already in the field.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Competitions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests.Competitions;

public class WithdrawCompetitorHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = SampleDefinition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SampleDefinition.Version,
            AdoptedAt = Now,
        };

    private static (FakeEventStore Store, CompetitionId CompetitionId) SeedCompetition()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var created = new CompetitionCreated(
            id, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return (store, id);
    }

    private static CompetitorId SeedRegisteredCompetitor(FakeEventStore store, CompetitionId competitionId)
    {
        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 1,
            RegisteredAt = Now,
        };
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(1), [new CompetitorRegistered(competitor, Now)]).GetAwaiter().GetResult();
        return competitor.Id;
    }

    [Fact]
    public async Task Withdrawing_a_registered_competitor_succeeds_and_appends_exactly_one_event_at_the_next_version()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId);
        var handler = new WithdrawCompetitorHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new WithdrawCompetitor(competitionId, competitorId), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(competitorId);

        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(3);
        var withdrawn = stream[2].Should().BeOfType<CompetitorWithdrawn>().Subject;
        withdrawn.CompetitorRef.Should().Be(competitorId);
    }

    [Fact]
    public async Task Withdrawing_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var handler = new WithdrawCompetitorHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new WithdrawCompetitor(CompetitionId.New(), CompetitorId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Withdrawing_an_unknown_competitor_fails_with_competition_competitor_notFound()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new WithdrawCompetitorHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new WithdrawCompetitor(competitionId, CompetitorId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.competitor.notFound");
    }

    [Fact]
    public async Task Withdrawing_an_already_withdrawn_competitor_fails_with_competition_competitor_alreadyWithdrawn()
    {
        var (store, competitionId) = SeedCompetition();
        var competitorId = SeedRegisteredCompetitor(store, competitionId);
        var handler = new WithdrawCompetitorHandler(store, new FakeClock(Now));

        var first = await handler.HandleAsync(
            new WithdrawCompetitor(competitionId, competitorId), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.HandleAsync(
            new WithdrawCompetitor(competitionId, competitorId), TestContext.Current.CancellationToken);

        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("competition.competitor.alreadyWithdrawn");
    }
}
