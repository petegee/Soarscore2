// kanban/completed/create-competition-steel-thread-plan.md WI-3. Mirrors
// People/PersonQueriesTests.cs's found/not-found style for GetPersonHandler.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;

namespace Soarscore.Application.Tests.Queries.Competitions;

public class GetCompetitionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetCompetition_for_an_existing_stream_returns_the_folded_competition()
    {
        var id = CompetitionId.New();
        var store = new FakeEventStore();
        var created = new CompetitionCreated(
            id,
            "Nationals",
            "Taupo",
            new DateOnly(2026, 9, 12),
            new DateOnly(2026, 9, 13),
            "1",
            BuildAdoptedRules(),
            Now);
        await store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created], TestContext.Current.CancellationToken);

        var handler = new GetCompetitionHandler(store);

        var result = await handler.HandleAsync(new GetCompetition(id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Competition.Id.Should().Be(id);
        result.Value.Competition.Name.Should().Be("Nationals");
        result.Value.PairwiseCoOccurrence.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompetition_for_an_unknown_id_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var handler = new GetCompetitionHandler(store);

        var result = await handler.HandleAsync(new GetCompetition(CompetitionId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    private static AdoptedRules BuildAdoptedRules() =>
        new()
        {
            Definition = Soarscore.Application.Tests.Shared.CompetitionClasses.ClassDefinitionFixtures.Minimal(),
            SourceClassId = "test-hash",
            SourceVersion = "v1",
            AdoptedAt = Now,
        };
}
