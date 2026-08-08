using Soarscore.Api.Routing;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Application.Competitions;
using Soarscore.Application.Entries;
using Soarscore.Application.People;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
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

        app.MapCommand<CreateCompetition, CompetitionId>("/create-competition");
        app.MapCommand<RegisterCompetitor, CompetitorId>("/register-competitor");
        app.MapCommand<WithdrawCompetitor, CompetitorId>("/withdraw-competitor");
        app.MapCommand<DrawPhase, CompetitionId>("/draw-phase");
        app.MapCommand<BindParameter, CompetitionId>("/bind-parameter");

        app.MapCommand<OpenEntry, EntryId>("/open-entry");
        app.MapCommand<OpenFlight, EntryId>("/open-flight");
        app.MapCommand<CaptureMeasurement, EntryId>("/capture-measurement");

        return app;
    }
}
