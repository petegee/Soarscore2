using Soarscore.Api.Routing;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Application.People;
using Soarscore.Domain.People;

namespace Soarscore.Api.Commands;

public static class Commands
{
    // Verbs, never nouns (high-level-architecture.md "intent-based").
    public static WebApplication MapCommands(this WebApplication app)
    {
        app.MapCommand<RegisterPerson, PersonId>("/register-person");
        app.MapCommand<RenamePerson, PersonId>("/rename-person");
        app.MapCommand<ChangePersonContactDetails, PersonId>("/change-person-contact-details");
        app.MapCommand<ChangePersonClubAffiliation, PersonId>("/change-person-club-affiliation");

        app.MapCommand<PublishClassDefinition, string>("/publish-class-definition");

        return app;
    }
}
