using Soarscore.Api.Routing;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Application.Competitions;
using Soarscore.Application.Entries;
using Soarscore.Application.People;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Api.Queries;

public static class Queries
{
    // Verbs, never nouns (high-level-architecture.md "intent-based").
    public static WebApplication MapQueries(this WebApplication app)
    {
        app.MapQuery<FindPeople, IReadOnlyList<PersonSummary>>("/people");
        app.MapQuery<GetPerson, Person>("/person");

        app.MapQuery<FindClassDefinitions, IReadOnlyList<ClassDefinitionSummary>>("/class-definitions");
        app.MapQuery<GetClassDefinition, ClassDefinition>("/class-definition");

        app.MapQuery<FindCompetitions, IReadOnlyList<CompetitionSummary>>("/competitions");
        app.MapQuery<GetCompetition, CompetitionView>("/competition");

        app.MapQuery<FindEntries, IReadOnlyList<EntrySummary>>("/entries");

        return app;
    }
}
