// kanban/completed/create-competition-steel-thread-plan.md WI-3. FindCompetitions
// takes no criteria requirement (unlike FindPeople) — both filters are
// optional and "no filters" is a valid, meaningful call (list everything).

using AwesomeAssertions;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;

namespace Soarscore.Application.Tests.Queries.Competitions;

public class FindCompetitionsHandlerTests
{
    private static readonly CompetitionSummary Nationals = new(
        CompetitionId.New(), "Nationals", "Taupo", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13), "F3J", "hash-f3j", "created");

    private static readonly CompetitionSummary ClubDay = new(
        CompetitionId.New(), "Club Day", "Auckland", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1), "F3K", "hash-f3k", "created");

    [Fact]
    public async Task FindCompetitions_with_no_filters_returns_every_competition()
    {
        var query = new FakeCompetitionsQuery();
        query.Seed(Nationals);
        query.Seed(ClubDay);
        var handler = new FindCompetitionsHandler(query);

        var result = await handler.HandleAsync(new FindCompetitions(null, null), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([Nationals, ClubDay]);
    }

    [Fact]
    public async Task FindCompetitions_by_onOrAfter_filters_to_matching_start_dates()
    {
        var query = new FakeCompetitionsQuery();
        query.Seed(Nationals);
        query.Seed(ClubDay);
        var handler = new FindCompetitionsHandler(query);

        var result = await handler.HandleAsync(
            new FindCompetitions(new DateOnly(2026, 8, 1), null), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(Nationals);
    }

    [Fact]
    public async Task FindCompetitions_by_classContentHash_filters_to_matching_class()
    {
        var query = new FakeCompetitionsQuery();
        query.Seed(Nationals);
        query.Seed(ClubDay);
        var handler = new FindCompetitionsHandler(query);

        var result = await handler.HandleAsync(
            new FindCompetitions(null, "hash-f3k"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(ClubDay);
    }

    [Fact]
    public async Task FindCompetitions_with_both_filters_applies_both()
    {
        var query = new FakeCompetitionsQuery();
        query.Seed(Nationals);
        query.Seed(ClubDay);
        var handler = new FindCompetitionsHandler(query);

        var result = await handler.HandleAsync(
            new FindCompetitions(new DateOnly(2026, 8, 1), "hash-f3k"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
