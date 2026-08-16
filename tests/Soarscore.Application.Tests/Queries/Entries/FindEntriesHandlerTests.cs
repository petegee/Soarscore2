// docs/plans/capture-a-score-steel-thread-plan.md WI-7. Mirrors
// Competitions/FindCompetitionsHandlerTests.cs / People/PersonQueriesTests.cs's
// style for a query handler resolving through a fake read-model query.

using AwesomeAssertions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Xunit;

using Soarscore.Application.Tests.Shared.Entries;

namespace Soarscore.Application.Tests.Queries.Entries;

public class FindEntriesHandlerTests
{
    private static readonly CompetitionId Competition = CompetitionId.New();
    private static readonly GroupId Group1 = GroupId.New();
    private static readonly GroupId Group2 = GroupId.New();
    private static readonly CompetitorId Competitor1 = CompetitorId.New();
    private static readonly CompetitorId Competitor2 = CompetitorId.New();

    private static readonly EntrySummary EntryInGroup1 = new(
        EntryId.New(), Competition, 1, 1, 1, Group1, Competitor1, ReflightRole.Original);

    private static readonly EntrySummary EntryInGroup2 = new(
        EntryId.New(), Competition, 1, 1, 1, Group2, Competitor2, ReflightRole.Original);

    private static readonly EntrySummary EntryInAnotherCompetition = new(
        EntryId.New(), CompetitionId.New(), 1, 1, 1, GroupId.New(), Competitor1, ReflightRole.Original);

    [Fact]
    public async Task FindEntries_scoped_to_a_competition_excludes_every_other_competition()
    {
        var query = new FakeEntryQuery();
        query.Seed(EntryInGroup1);
        query.Seed(EntryInGroup2);
        query.Seed(EntryInAnotherCompetition);
        var handler = new FindEntriesHandler(query);

        var result = await handler.HandleAsync(
            new FindEntries(Competition, null, null, null, null, null), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([EntryInGroup1, EntryInGroup2]);
    }

    [Fact]
    public async Task FindEntries_by_groupRef_narrows_to_the_matching_group()
    {
        var query = new FakeEntryQuery();
        query.Seed(EntryInGroup1);
        query.Seed(EntryInGroup2);
        var handler = new FindEntriesHandler(query);

        var result = await handler.HandleAsync(
            new FindEntries(Competition, null, null, null, Group1, null), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(EntryInGroup1);
    }

    [Fact]
    public async Task FindEntries_by_competitorRef_narrows_to_the_matching_competitor()
    {
        var query = new FakeEntryQuery();
        query.Seed(EntryInGroup1);
        query.Seed(EntryInGroup2);
        var handler = new FindEntriesHandler(query);

        var result = await handler.HandleAsync(
            new FindEntries(Competition, null, null, null, null, Competitor2), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(EntryInGroup2);
    }

    [Fact]
    public async Task FindEntries_by_full_coordinate_narrows_to_the_one_task_round()
    {
        var query = new FakeEntryQuery();
        query.Seed(EntryInGroup1);
        query.Seed(EntryInGroup2);
        var handler = new FindEntriesHandler(query);

        var result = await handler.HandleAsync(
            new FindEntries(Competition, 1, 1, 1, null, null), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([EntryInGroup1, EntryInGroup2]);
    }

    [Fact]
    public async Task FindEntries_with_no_matches_returns_an_empty_list()
    {
        var handler = new FindEntriesHandler(new FakeEntryQuery());

        var result = await handler.HandleAsync(
            new FindEntries(Competition, null, null, null, null, null), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
