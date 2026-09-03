using Soarscore.Api.Routing;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.People;
using Soarscore.Application.Queries.Scoring;
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

        app.MapQuery<GetTaskRoundRecording, TaskRoundRecordingView>("/task-round-recording");

        app.MapQuery<ScoreTaskRound, IReadOnlyList<GroupScoreView>>("/task-round-result");
        app.MapQuery<ScoreCompetition, CompetitionScoreView>("/competition-result");

        app.MapQuery<GetTeamRosters, TeamRostersView>("/competition-teams");
        app.MapQuery<ScoreTeamStandings, TeamStandingsView>("/competition-team-result");
        app.MapQuery<GetDrawProtectionDiagnostics, DrawProtectionDiagnosticsView>("/draw-diagnostics");

        return app;
    }
}
