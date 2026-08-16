// Wiring factored out of Program.cs so the WI-2 route-shape reflection test
// (Soarscore.Architecture.Tests) can build the same WebApplication in-memory
// and enumerate its EndpointDataSource without starting Kestrel or opening an
// HTTP client — "driven without HTTP testing tools" (LADR-0003).

using Microsoft.AspNetCore.Http.Features;
using Soarscore.Api.Commands;
using Soarscore.Api.Queries;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.People;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;

namespace Soarscore.Api;

public static class Composition
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();

        // LADR-0003 "Errors": RFC 9457 ProblemDetails via IProblemDetailsService —
        // Results.Problem() (Routing/EndpointRouteBuilderExtensions.cs) delegates to
        // this once registered, rather than hand-writing the response shape.
        builder.Services.AddProblemDetails();

        // WI-1/WI-6 (class-definition-adoption-steel-thread-plan.md): what
        // POST /publish-class-definition binds its body through, and what every
        // response — including GET /class-definition — is written with. Only
        // adds to ASP.NET's Web defaults (already camelCase); harmless to the
        // Person endpoints, none of which carry a NumberOrParam/FlagOrParam or an
        // enum.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.MaxDepth = ClassDefinitionIngestion.MaxDepth;
            options.SerializerOptions.AllowOutOfOrderMetadataProperties = true;
            foreach (var converter in ClassDefinitionIngestion.Options.Converters)
            {
                options.SerializerOptions.Converters.Add(converter);
            }
        });

        builder.Services.AddSoarscoreInfrastructure(builder.Configuration);
        // capture-a-score-steel-thread-plan.md WI-13: Scoped, not Singleton.
        // Dispatcher.Invoke (Dispatcher.cs) resolves ICommandHandler<,>/
        // IQueryHandler<,> — both registered Scoped below — through the
        // IServiceProvider its constructor captures. A Singleton Dispatcher
        // captures the ROOT provider (a deliberate .NET DI guard against
        // captive dependencies), so every handler resolution was quietly
        // running against the root scope rather than the request's — masked
        // under plain `dotnet run` because Kestrel's default host only
        // validates scopes in the Development environment, and nothing
        // before this thread hosted the app any other way. The acceptance
        // suite's WebApplicationFactory (Development by default) surfaced it
        // immediately: "Cannot resolve scoped service ... from root
        // provider." Dispatcher holds no state, so Scoped costs nothing and
        // gives every request its own handler resolution, which is what was
        // always intended.
        builder.Services.AddScoped<IDispatcher, Dispatcher>();

        // One registration per handler — no assembly scanning (LADR-0003
        // "Command/query dispatch": inspectable over convention-magic).
        builder.Services.AddScoped<ICommandHandler<RegisterPerson, PersonId>, RegisterPersonHandler>();
        builder.Services.AddScoped<ICommandHandler<RenamePerson, PersonId>, RenamePersonHandler>();
        builder.Services.AddScoped<ICommandHandler<ChangePersonContactDetails, PersonId>, ChangePersonContactDetailsHandler>();
        builder.Services.AddScoped<ICommandHandler<ChangePersonClubAffiliation, PersonId>, ChangePersonClubAffiliationHandler>();
        builder.Services.AddScoped<IQueryHandler<FindPeople, IReadOnlyList<PersonSummary>>, FindPeopleHandler>();
        builder.Services.AddScoped<IQueryHandler<GetPerson, Person>, GetPersonHandler>();

        builder.Services.AddScoped<ICommandHandler<PublishClassDefinition, string>, PublishClassDefinitionHandler>();
        builder.Services.AddScoped<IQueryHandler<FindClassDefinitions, IReadOnlyList<ClassDefinitionSummary>>, FindClassDefinitionsHandler>();
        builder.Services.AddScoped<IQueryHandler<GetClassDefinition, ClassDefinition>, GetClassDefinitionHandler>();

        builder.Services.AddScoped<ICommandHandler<CreateCompetition, CompetitionId>, CreateCompetitionHandler>();
        builder.Services.AddScoped<ICommandHandler<RegisterCompetitor, CompetitorId>, RegisterCompetitorHandler>();
        builder.Services.AddScoped<ICommandHandler<WithdrawCompetitor, CompetitorId>, WithdrawCompetitorHandler>();
        builder.Services.AddScoped<ICommandHandler<DrawPhase, CompetitionId>, DrawPhaseHandler>();
        builder.Services.AddScoped<ICommandHandler<BindParameter, CompetitionId>, BindParameterHandler>();
        builder.Services.AddScoped<IQueryHandler<FindCompetitions, IReadOnlyList<CompetitionSummary>>, FindCompetitionsHandler>();
        builder.Services.AddScoped<IQueryHandler<GetCompetition, CompetitionView>, GetCompetitionHandler>();

        builder.Services.AddScoped<ICommandHandler<OpenEntry, EntryId>, OpenEntryHandler>();
        builder.Services.AddScoped<ICommandHandler<OpenFlight, EntryId>, OpenFlightHandler>();
        builder.Services.AddScoped<ICommandHandler<CaptureMeasurement, EntryId>, CaptureMeasurementHandler>();
        builder.Services.AddScoped<IQueryHandler<FindEntries, IReadOnlyList<EntrySummary>>, FindEntriesHandler>();

        builder.Services.AddScoped<IQueryHandler<ScoreTaskRound, IReadOnlyList<GroupScoreView>>, ScoreTaskRoundHandler>();
        builder.Services.AddScoped<IQueryHandler<ScoreCompetition, CompetitionScoreView>, ScoreCompetitionHandler>();

        var app = builder.Build();

        // WI-1/WI-6: the payload-size ceiling, ahead of routing and therefore
        // ahead of model binding — Kestrel enforces it while reading the body
        // stream, before ClassDefinitionIngestion.Options ever parses a byte of
        // an oversized POST. Scoped to this one path; every other endpoint's body
        // is small by construction and keeps the server's ordinary default.
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsPost(context.Request.Method)
                && context.Request.Path.Equals("/publish-class-definition", StringComparison.OrdinalIgnoreCase))
            {
                // capture-a-score-steel-thread-plan.md WI-13: Kestrel always
                // provides IHttpMaxRequestBodySizeFeature, but
                // Microsoft.AspNetCore.Mvc.Testing's WebApplicationFactory hosts
                // this app over TestServer, which does not — the acceptance
                // suite's first HTTP call through this middleware
                // NullReferenceException'd on the `!` below before this null
                // check existed. Null-conditional rather than an added `is null`
                // branch: under real Kestrel this still sets the limit exactly
                // as before, and under TestServer skipping it is correct, not a
                // silent gap — TestServer enforces no body-size limit of its own
                // either, so there is nothing to configure.
                context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize = ClassDefinitionIngestion.MaxPayloadBytes;
            }

            await next();
        });

        app.MapOpenApi();

        app.MapCommands();
        app.MapQueries();

        return app;
    }
}
