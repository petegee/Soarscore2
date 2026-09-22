// Wiring factored out of Program.cs so the WI-2 route-shape reflection test
// (Soarscore.Architecture.Tests) can build the same WebApplication in-memory
// and enumerate its EndpointDataSource without starting Kestrel or opening an
// HTTP client — "driven without HTTP testing tools" (LADR-0003).

using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using Soarscore.Api.Auth;
using Soarscore.Api.Commands;
using Soarscore.Api.Queries;
using Soarscore.Application;
using Soarscore.Application.Auth;
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

        // authentication-and-authorisation.md WI-9: the auth mode is validated
        // FIRST (D8) — a misconfigured or Production-refused mode is a loud
        // crash at boot, never a silently open or mock-keyed API. Everything
        // auth-shaped below branches on this one value; see the mode table in
        // the story's "Shape of the change".
        var auth = AuthSettings.From(builder.Configuration, builder.Environment.IsProduction());

        builder.Services.AddOpenApi(options => options.AddDocumentTransformer(new BearerSecurityScheme()));

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
        //
        // WI-6 originally also set SerializerOptions.MaxDepth = 24 here — the
        // ingestion nesting ceiling enforced at binding. That broke GET
        // /openapi/v1.json: .NET 10's OpenAPI document generation reuses these
        // shared options, and a type's JSON *schema* is roughly twice as deep
        // as the deepest *instance* it documents (each model level adds a
        // type/properties wrapper), so no options depth that admits legal
        // class definitions can also write ClassDefinition's schema —
        // Utf8JsonWriter threw "CurrentDepth (24) is equal to or larger than
        // the maximum allowed depth of 24". The ceiling therefore lives in the
        // payload middleware below instead, enforced against the raw body with
        // the same STJ parser semantics (LADR-0002 §4's bound, unchanged);
        // these shared options keep the Web default of 64, itself a bound.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
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

        // authentication-and-authorisation.md WI-7: the sign-in/role/capture-
        // policy/identity-binding commands and the /who-am-i identity bridge.
        // Registered in EVERY mode — the routes are mapped in every mode
        // (Commands.cs/Queries.cs) and HandlerRegistrationTests resolves every
        // mapped handler; under none-mode the pipeline is absent (below), so
        // these behave exactly as their plain pre-auth handlers would.
        builder.Services.AddScoped<ICommandHandler<LinkSignIn, LinkSignInResult>, LinkSignInHandler>();
        builder.Services.AddScoped<ICommandHandler<GrantRole, PersonId>, GrantRoleHandler>();
        builder.Services.AddScoped<ICommandHandler<RevokeRole, PersonId>, RevokeRoleHandler>();
        builder.Services.AddScoped<ICommandHandler<ConfigureCapturePolicy, CompetitionId>, ConfigureCapturePolicyHandler>();
        builder.Services.AddScoped<ICommandHandler<BindIdentity, PersonId>, BindIdentityHandler>();
        builder.Services.AddScoped<IQueryHandler<WhoAmI, CurrentUserView>, WhoAmIHandler>();

        builder.Services.AddScoped<ICommandHandler<PublishClassDefinition, string>, PublishClassDefinitionHandler>();
        builder.Services.AddScoped<IQueryHandler<FindClassDefinitions, IReadOnlyList<ClassDefinitionSummary>>, FindClassDefinitionsHandler>();
        builder.Services.AddScoped<IQueryHandler<GetClassDefinition, ClassDefinition>, GetClassDefinitionHandler>();

        builder.Services.AddScoped<ICommandHandler<CreateCompetition, CompetitionId>, CreateCompetitionHandler>();
        builder.Services.AddScoped<ICommandHandler<RegisterCompetitor, CompetitorId>, RegisterCompetitorHandler>();
        builder.Services.AddScoped<ICommandHandler<WithdrawCompetitor, CompetitorId>, WithdrawCompetitorHandler>();
        builder.Services.AddScoped<ICommandHandler<DrawPhase, CompetitionId>, DrawPhaseHandler>();
        builder.Services.AddScoped<ICommandHandler<PrescribeDraw, CompetitionId>, PrescribeDrawHandler>();
        builder.Services.AddScoped<ICommandHandler<AcceptDraw, CompetitionId>, AcceptDrawHandler>();
        builder.Services.AddScoped<ICommandHandler<RejectDraw, CompetitionId>, RejectDrawHandler>();
        builder.Services.AddScoped<ICommandHandler<BindParameter, CompetitionId>, BindParameterHandler>();
        builder.Services.AddScoped<ICommandHandler<DeclareInstruments, CompetitionId>, DeclareInstrumentsHandler>();
        builder.Services.AddScoped<ICommandHandler<CorrectInstrumentDeclaration, CompetitionId>, CorrectInstrumentDeclarationHandler>();
        builder.Services.AddScoped<ICommandHandler<CompleteTaskRound, CompetitionId>, CompleteTaskRoundHandler>();
        builder.Services.AddScoped<ICommandHandler<ReopenTaskRound, CompetitionId>, ReopenTaskRoundHandler>();
        builder.Services.AddScoped<ICommandHandler<AnnulTaskRound, CompetitionId>, AnnulTaskRoundHandler>();
        builder.Services.AddScoped<ICommandHandler<FinaliseCompetition, CompetitionId>, FinaliseCompetitionHandler>();
        builder.Services.AddScoped<ICommandHandler<RecordCompetitionPenalty, CompetitionId>, RecordCompetitionPenaltyHandler>();
        builder.Services.AddScoped<ICommandHandler<AppendReflightGroup, GroupId>, AppendReflightGroupHandler>();
        builder.Services.AddScoped<ICommandHandler<AssignGroupSpots, GroupId>, AssignGroupSpotsHandler>();
        builder.Services.AddScoped<ICommandHandler<RecordReflightRuling, CompetitionId>, RecordReflightRulingHandler>();
        builder.Services.AddScoped<ICommandHandler<RecordTieBreakOutcome, CompetitionId>, RecordTieBreakOutcomeHandler>();
        builder.Services.AddScoped<IQueryHandler<FindCompetitions, IReadOnlyList<CompetitionSummary>>, FindCompetitionsHandler>();
        builder.Services.AddScoped<IQueryHandler<GetCompetition, CompetitionView>, GetCompetitionHandler>();
        builder.Services.AddScoped<IQueryHandler<GetCompetitionEventLog, CompetitionEventLogView>, GetCompetitionEventLogHandler>();

        builder.Services.AddScoped<ICommandHandler<DefineScoringTeam, ScoringTeamId>, DefineScoringTeamHandler>();
        builder.Services.AddScoped<ICommandHandler<DefineProtectionGroup, ProtectionGroupId>, DefineProtectionGroupHandler>();
        builder.Services.AddScoped<ICommandHandler<AssignScoringTeamMembership, CompetitionId>, AssignScoringTeamMembershipHandler>();
        builder.Services.AddScoped<ICommandHandler<ClearScoringTeamMembership, CompetitionId>, ClearScoringTeamMembershipHandler>();
        builder.Services.AddScoped<ICommandHandler<AddProtectionGroupMember, CompetitionId>, AddProtectionGroupMemberHandler>();
        builder.Services.AddScoped<ICommandHandler<RemoveProtectionGroupMember, CompetitionId>, RemoveProtectionGroupMemberHandler>();
        builder.Services.AddScoped<ICommandHandler<ConfigureTeamClassification, CompetitionId>, ConfigureTeamClassificationHandler>();
        builder.Services.AddScoped<IQueryHandler<GetTeamRosters, TeamRostersView>, GetTeamRostersHandler>();
        builder.Services.AddScoped<IQueryHandler<ScoreTeamStandings, TeamStandingsView>, ScoreTeamStandingsHandler>();
        builder.Services.AddScoped<IQueryHandler<GetDrawProtectionDiagnostics, DrawProtectionDiagnosticsView>, GetDrawProtectionDiagnosticsHandler>();

        builder.Services.AddScoped<ICommandHandler<OpenEntry, EntryId>, OpenEntryHandler>();
        builder.Services.AddScoped<ICommandHandler<OpenFlight, EntryId>, OpenFlightHandler>();
        builder.Services.AddScoped<ICommandHandler<CaptureMeasurement, EntryId>, CaptureMeasurementHandler>();
        builder.Services.AddScoped<ICommandHandler<AmendMeasurement, EntryId>, AmendMeasurementHandler>();
        builder.Services.AddScoped<ICommandHandler<AnnulEntry, EntryId>, AnnulEntryHandler>();
        builder.Services.AddScoped<ICommandHandler<RecordEntryPenalty, EntryId>, RecordEntryPenaltyHandler>();
        builder.Services.AddScoped<IQueryHandler<FindEntries, IReadOnlyList<EntrySummary>>, FindEntriesHandler>();

        builder.Services.AddScoped<IQueryHandler<GetTaskRoundRecording, TaskRoundRecordingView>, GetTaskRoundRecordingHandler>();
        builder.Services.AddScoped<IQueryHandler<ScoreTaskRound, IReadOnlyList<GroupScoreView>>, ScoreTaskRoundHandler>();
        builder.Services.AddScoped<IQueryHandler<ScoreCompetition, CompetitionScoreView>, ScoreCompetitionHandler>();
        builder.Services.AddScoped<IQueryHandler<GetPendingTieBreaks, PendingTieBreaksView>, GetPendingTieBreaksHandler>();

        // authentication-and-authorisation.md WI-9 step 6: the mode-dispatched
        // registrations. D1/D8 make enforcement opt-in per composition — under
        // "none" this branch registers NOTHING (no bearer validation, no
        // pipeline, no HttpCurrentUser), which is the byte-identical default
        // every existing test runs under.
        builder.Services.AddSingleton(new AuthBootstrap(auth.BootstrapOrganisers));

        if (auth.Mode is AuthMode.Mock or AuthMode.Oidc)
        {
            // D2: one HttpCurrentUser per request, bound by the middleware
            // below. RequestCaller is the per-scope carrier — its comment
            // explains the indirection (its default fails closed to an
            // unauthenticated actor; the seeder hosts bind the system actor
            // explicitly).
            builder.Services.AddScoped<HttpCurrentUser>();
            builder.Services.AddScoped<RequestCaller>();
            builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<RequestCaller>().User);
            builder.Services.AddScoped<IAuthorizationPipeline, AuthorizationPipeline>();

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => ConfigureJwtBearer(options, auth));

            if (auth.Mode is AuthMode.Mock)
            {
                // D11: the persona set is seeded (and its bearer tokens
                // printed) at startup, before the web host — same
                // hosted-service mechanism as ClassCorpusSeederHost below.
                // Refused in Production (D8), so this never exists in a
                // release deployment.
                builder.Services.AddSingleton(auth.Mock!);
                builder.Services.AddHostedService<MockPersonaSeederHost>();
            }
        }
        else
        {
            // D1/D8: none-mode truly registers nothing auth-shaped — the
            // stock anonymous principal keeps the port total, no
            // IAuthorizationPipeline resolves (Dispatcher.Invoke behaves
            // exactly as it did pre-auth).
            builder.Services.AddSingleton<ICurrentUser, AnonymousCurrentUser>();
        }

        // Deployment seeding — kanban/in-progress/seed-class-corpus-at-startup.md WI-2.
        // Registered hosted services start before the generic web host (Kestrel),
        // so the seed corpus is published before the API accepts a request.
        builder.Services.AddHostedService<Seeding.ClassCorpusSeederHost>();

        // kanban/completed/cors-for-ndcscore-spa.md WI-1: cross-origin access for
        // the NDC companion SPA (~/Source/NdcScore), which is a pure web client of
        // this API — no BFF — so the browser enforces CORS against it. Strictly
        // opt-in: with no origins configured there is no policy, no middleware and
        // no change to today's responses; configuring some turns on a default
        // policy for them. AllowCredentials is still deliberately absent
        // (authentication-and-authorisation.md amended the trust model): the SPA
        // authenticates with an Authorization header, which CORS treats as
        // non-credentialed — no cookies are ever involved.
        var corsOrigins = CorsOrigins(builder.Configuration);
        if (corsOrigins.Length > 0)
        {
            builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
                policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));
        }

        var app = builder.Build();

        // wwwroot/integrators-guide.html — the integrators guide, the single
        // artifact (no markdown source). Middleware, not routing — no endpoint
        // is created, so the WI-2 route-shape reflection test (only GET/POST
        // from MapCommand/MapQuery) is unaffected; it enumerates
        // EndpointDataSource, which static files never add to.
        app.UseStaticFiles();

        // cors-for-ndcscore-spa.md WI-1: only when origins are configured.
        // After UseStaticFiles (CORS is for the API surface, not the static
        // integrators guide) and before any endpoint executes; adds no
        // endpoint, so the WI-2 route-shape reflection test is unaffected.
        if (corsOrigins.Length > 0)
        {
            app.UseCors();
        }

        // authentication-and-authorisation.md WI-9 steps 3–4, under mock/oidc
        // only: UseAuthentication runs the JwtBearer handler (the token is
        // validated, context.User filled); the middleware after it binds that
        // principal to the request scope's ICurrentUser (HttpCurrentUser —
        // sub parsing, D2's per-request identity resolution, D12's machine
        // tokens) and rebinds the scope's RequestCaller. UseAuthorization is
        // deliberately ABSENT: there are no endpoint-level policies — the
        // authorization pipeline (AuthorizationPipeline, resolved by
        // Dispatcher.Invoke before any handler) is the single enforcement
        // point. Under none-mode neither middleware exists.
        if (auth.Mode is AuthMode.Mock or AuthMode.Oidc)
        {
            app.UseAuthentication();
            app.Use(async (context, next) =>
            {
                var caller = context.RequestServices.GetRequiredService<RequestCaller>();
                var user = context.RequestServices.GetRequiredService<HttpCurrentUser>();
                await user.BindAsync(context.User, context.RequestAborted);
                caller.User = user;
                await next();
            });
        }

        // WI-1/WI-6: the payload-size and nesting-depth ceiling, ahead of routing
        // and therefore ahead of model binding — Kestrel enforces the size while
        // reading the body stream, before ClassDefinitionIngestion.Options ever
        // parses a byte of an oversized POST, and the depth walk below enforces
        // LADR-0002 §4's nesting bound (ClassDefinitionIngestion.MaxDepth) with
        // the same parser semantics before binding parses at the shared default.
        // The depth bound lives here rather than in ConfigureHttpJsonOptions —
        // see the comment there for why (OpenAPI schema generation shares those
        // options and needs room for ClassDefinition's schema, which is deeper
        // than any legal instance). Scoped to this one path; every other
        // endpoint's body is small and shallow by construction and keeps the
        // server's ordinary defaults.
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

                // The depth half of the ceiling. The body is buffered (already
                // size-capped above) and walked once with Utf8JsonReader — the
                // same parser binding will use — at the ingestion MaxDepth, so
                // the semantics are STJ's own: a JsonException means malformed
                // JSON or depth beyond the ceiling, both of which binding would
                // otherwise reject with a plain 400. The buffered bytes are put
                // back as the request body for binding to parse normally. Club
                // scale (a ≤ 256 KiB payload, ≤ 8 rounds/day) makes the double
                // parse unmeasurable; doing it here keeps LADR-0002 §4's bound
                // exactly while the shared options stay at the default.
                using var buffered = new MemoryStream();
                await context.Request.Body.CopyToAsync(buffered, context.RequestAborted);
                var body = buffered.ToArray();
                try
                {
                    var reader = new Utf8JsonReader(
                        body, new JsonReaderOptions { MaxDepth = ClassDefinitionIngestion.MaxDepth });
                    while (reader.Read()) { }
                }
                catch (JsonException ex)
                {
                    await Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "class-definition.ingestion.invalid-json",
                        detail: ex.Message).ExecuteAsync(context);
                    return;
                }

                context.Request.Body = new MemoryStream(body);
            }

            await next();
        });

        app.MapOpenApi();

        // Swashbuckle's Swagger UI at /swagger, pointed at the in-box document
        // above — the UI is the only thing Swashbuckle supplies here; the spec
        // still has exactly one generator (LADR-0003 "API documentation", as
        // amended for the hosted UI). Served in every environment: the page's
        // point is poking the deployed API (e.g. on Fly.io). Since
        // authentication-and-authorisation.md the API is token-validated
        // (WI-9) — under oidc/mock the UI's Authorize button (bearer scheme
        // from the BearerSecurityScheme transformer) takes a pasted token, and
        // under mock the persona tokens printed at startup are exactly what to
        // paste. The page itself stays anonymous in v1 (deferred-decisions.md).
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Soarscore v1");
            options.DocumentTitle = "Soarscore API";
        });

        app.MapCommands();
        app.MapQueries();

        return app;
    }

    /// <summary>
    /// authentication-and-authorisation.md WI-9 step 3: the JwtBearer wiring,
    /// one of two shapes per mode. Under "mock" (and under "oidc" when a
    /// static signing key is configured — the same mechanism WI-10's
    /// acceptance suite pins, which is what makes mock and the suite one
    /// thing, D11) the validation parameters are pinned wholesale: issuer +
    /// audience + symmetric key, no Authority metadata retrieval. Under plain
    /// "oidc" the handler discovers from the Auth0 domain at first request.
    /// MapInboundClaims stays off in every authenticated mode (the middleware
    /// reads <c>sub</c>, <c>email</c>, <c>name</c> at their wire names) and
    /// the principal's name claim is <c>sub</c>.
    /// </summary>
    private static void ConfigureJwtBearer(JwtBearerOptions options, AuthSettings auth)
    {
        options.MapInboundClaims = false;

        if (auth.Mode is AuthMode.Mock)
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "sub",
                ValidIssuer = MockAuthOptions.Issuer,
                ValidAudience = auth.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(auth.Mock!.SigningKey),
            };
        }
        else if (auth.StaticSigningKey is { } key)
        {
            // oidc with a pinned static key: the issuer is still the IdP
            // domain — only the signing-key discovery is bypassed.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "sub",
                ValidIssuer = $"https://{auth.Domain}/",
                ValidAudience = auth.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
            };
        }
        else
        {
            options.Authority = $"https://{auth.Domain}/";
            options.Audience = auth.Audience;
            options.TokenValidationParameters.NameClaimType = "sub";
        }
    }

    /// <summary>
    /// cors-for-ndcscore-spa.md WI-1: where allowed origins come from.
    /// <c>Soarscore:Cors:Origins</c> (an array in appsettings) first, then the
    /// flat env alias <c>SOARSCORE_CORS_ORIGINS</c> (comma-separated) — the same
    /// flat-alias convention as <c>SOARSCORE_STORE</c> and
    /// <c>SOARSCORE_CONNECTION_STRING</c>, because the deployments that need this
    /// (Fly.io, the secretary's laptop behind a different dev port) set
    /// configuration as environment variables, not JSON. Empty is the normal
    /// case: same-origin callers need nothing, so nothing is enabled.
    /// </summary>
    private static string[] CorsOrigins(IConfiguration configuration)
    {
        var origins = configuration.GetSection("Soarscore:Cors:Origins").Get<string[]>();
        if (origins is { Length: > 0 })
        {
            return origins;
        }

        var alias = configuration["SOARSCORE_CORS_ORIGINS"];
        return string.IsNullOrWhiteSpace(alias)
            ? []
            : alias.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
