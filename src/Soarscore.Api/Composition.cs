// Wiring factored out of Program.cs so the WI-2 route-shape reflection test
// (Soarscore.Architecture.Tests) can build the same WebApplication in-memory
// and enumerate its EndpointDataSource without starting Kestrel or opening an
// HTTP client — "driven without HTTP testing tools" (LADR-0003).

using Soarscore.Api.Routing;
using Soarscore.Application;
using Soarscore.Application.People;
using Soarscore.Domain.People;
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

        var app = builder.Build();

        app.MapOpenApi();

        // Verbs, never nouns (high-level-architecture.md "intent-based").
        app.MapCommand<RegisterPerson, PersonId>("/register-person");
        app.MapCommand<RenamePerson, PersonId>("/rename-person");
        app.MapCommand<ChangePersonContactDetails, PersonId>("/change-person-contact-details");
        app.MapCommand<ChangePersonClubAffiliation, PersonId>("/change-person-club-affiliation");
        app.MapQuery<FindPeople, IReadOnlyList<PersonSummary>>("/people");
        app.MapQuery<GetPerson, Person>("/person");

        return app;
    }
}
