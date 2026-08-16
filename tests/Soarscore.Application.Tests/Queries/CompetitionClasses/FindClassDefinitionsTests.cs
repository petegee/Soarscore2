// kanban/completed/class-definition-adoption-steel-thread-plan.md WI-4. Mirrors
// People/PersonQueriesTests.cs's shape.

using AwesomeAssertions;
using Soarscore.Application.Queries.CompetitionClasses;
using Xunit;

using Soarscore.Application.Tests.Shared.CompetitionClasses;

namespace Soarscore.Application.Tests.Queries.CompetitionClasses;

public class FindClassDefinitionsTests
{
    private static readonly ClassDefinitionSummary Active = new(Guid.NewGuid(), "hash-1", "F3K", "F3K", "v1", DateTimeOffset.UtcNow, null);
    private static readonly ClassDefinitionSummary Retired = new(Guid.NewGuid(), "hash-2", "F3K Legacy", "F3K", "v0", DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow);

    private static IDispatcher BuildDispatcher(FakeClassLibraryQuery classLibraryQuery)
    {
        var services = new Dictionary<Type, object>
        {
            [typeof(IQueryHandler<FindClassDefinitions, IReadOnlyList<ClassDefinitionSummary>>)] = new FindClassDefinitionsHandler(classLibraryQuery),
        };
        return new Dispatcher(new FakeServiceProvider(services));
    }

    [Fact]
    public async Task FindClassDefinitions_by_name_returns_every_substring_match()
    {
        var query = new FakeClassLibraryQuery();
        query.Seed(Active);
        query.Seed(Retired);
        var dispatcher = BuildDispatcher(query);

        var result = await dispatcher.QueryAsync(new FindClassDefinitions("F3K", ActiveOnly: false), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([Active, Retired]);
    }

    [Fact]
    public async Task FindClassDefinitions_activeOnly_excludes_retired_definitions()
    {
        var query = new FakeClassLibraryQuery();
        query.Seed(Active);
        query.Seed(Retired);
        var dispatcher = BuildDispatcher(query);

        var result = await dispatcher.QueryAsync(new FindClassDefinitions(Name: null, ActiveOnly: true), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(Active);
    }
}
