using Soarscore.Api.Routing;
using Soarscore.Application.People;
using Soarscore.Domain.People;

namespace Soarscore.Api.Queries;

public static class Queries
{
    // Verbs, never nouns (high-level-architecture.md "intent-based").
    public static WebApplication MapQueries(this WebApplication app)
    {
        app.MapQuery<FindPeople, IReadOnlyList<PersonSummary>>("/people");
        app.MapQuery<GetPerson, Person>("/person");

        return app;
    }
}
