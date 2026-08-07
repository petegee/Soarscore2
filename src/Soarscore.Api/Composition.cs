// Wiring factored out of Program.cs so the WI-2 route-shape reflection test
// (Soarscore.Architecture.Tests) can build the same WebApplication in-memory
// and enumerate its EndpointDataSource without starting Kestrel or opening an
// HTTP client — "driven without HTTP testing tools" (LADR-0003).

using Microsoft.AspNetCore.Http.Features;
using Soarscore.Api.Commands;
using Soarscore.Api.Queries;
using Soarscore.Application;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Application.Competitions;
using Soarscore.Application.People;
using Soarscore.Domain.Competitions;
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
        builder.Services.AddSingleton<IDispatcher, Dispatcher>();

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
        builder.Services.AddScoped<IQueryHandler<FindCompetitions, IReadOnlyList<CompetitionSummary>>, FindCompetitionsHandler>();
        builder.Services.AddScoped<IQueryHandler<GetCompetition, CompetitionView>, GetCompetitionHandler>();

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
                context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize = ClassDefinitionIngestion.MaxPayloadBytes;
            }

            await next();
        });

        app.MapOpenApi();

        app.MapCommands();
        app.MapQueries();

        return app;
    }
}
