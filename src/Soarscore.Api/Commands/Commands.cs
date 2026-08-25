using Soarscore.Api.Routing;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
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
        app.MapCommand<PrescribeDraw, CompetitionId>("/prescribe-draw");
        app.MapCommand<AcceptDraw, CompetitionId>("/accept-draw");
        app.MapCommand<RejectDraw, CompetitionId>("/reject-draw");
        app.MapCommand<BindParameter, CompetitionId>("/bind-parameter");
        app.MapCommand<CompleteTaskRound, CompetitionId>("/complete-task-round");
        app.MapCommand<ReopenTaskRound, CompetitionId>("/reopen-task-round");
        app.MapCommand<AnnulTaskRound, CompetitionId>("/annul-task-round");
        app.MapCommand<FinaliseCompetition, CompetitionId>("/finalise-competition");
        app.MapCommand<RecordCompetitionPenalty, CompetitionId>("/record-competition-penalty");
        app.MapCommand<AppendReflightGroup, GroupId>("/append-reflight-group");
        app.MapCommand<RecordReflightRuling, CompetitionId>("/record-reflight-ruling");

        app.MapCommand<OpenEntry, EntryId>("/open-entry");
        app.MapCommand<OpenFlight, EntryId>("/open-flight");
        app.MapCommand<CaptureMeasurement, EntryId>("/capture-measurement");
        app.MapCommand<AmendMeasurement, EntryId>("/amend-measurement");
        app.MapCommand<AnnulEntry, EntryId>("/annul-entry");
        app.MapCommand<RecordEntryPenalty, EntryId>("/record-entry-penalty");

        return app;
    }
}
