using AwesomeAssertions;
using Soarscore.Application.People;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Application.Tests.People;

public class PersonQueriesTests
{
    private static readonly PersonSummary Alex = new(PersonId.New(), "Alex Pilot", "alex@example.com", null, "Auckland", null);
    private static readonly PersonSummary Alexandra = new(PersonId.New(), "Alexandra Novice", "alexandra@example.com", null, "Wellington", null);

    private static IDispatcher BuildDispatcher(FakePeopleQuery peopleQuery)
    {
        var services = new Dictionary<Type, object>
        {
            [typeof(IQueryHandler<FindPeople, IReadOnlyList<PersonSummary>>)] = new FindPeopleHandler(peopleQuery),
        };
        return new Dispatcher(new FakeServiceProvider(services));
    }

    [Fact]
    public async Task FindPeople_by_email_returns_the_single_exact_match()
    {
        var peopleQuery = new FakePeopleQuery();
        peopleQuery.Seed(Alex);
        peopleQuery.Seed(Alexandra);
        var dispatcher = BuildDispatcher(peopleQuery);

        var result = await dispatcher.QueryAsync(new FindPeople("alex@example.com", null), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(Alex);
    }

    [Fact]
    public async Task FindPeople_by_email_with_no_match_returns_an_empty_list()
    {
        var dispatcher = BuildDispatcher(new FakePeopleQuery());

        var result = await dispatcher.QueryAsync(new FindPeople("nobody@example.com", null), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task FindPeople_by_name_returns_every_case_insensitive_substring_match()
    {
        var peopleQuery = new FakePeopleQuery();
        peopleQuery.Seed(Alex);
        peopleQuery.Seed(Alexandra);
        var dispatcher = BuildDispatcher(peopleQuery);

        var result = await dispatcher.QueryAsync(new FindPeople(null, "alex"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([Alex, Alexandra]);
    }

    [Fact]
    public async Task FindPeople_with_neither_email_nor_name_fails()
    {
        var dispatcher = BuildDispatcher(new FakePeopleQuery());

        var result = await dispatcher.QueryAsync(new FindPeople(null, null), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("findPeople.noCriteria");
    }
}
