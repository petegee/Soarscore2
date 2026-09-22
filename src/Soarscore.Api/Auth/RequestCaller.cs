// The request-scope carrier for ICurrentUser under mock/oidc —
// authentication-and-authorisation.md WI-9 step 6. Registered as a factory
// over it (services.AddScoped<ICurrentUser>(sp =>
// sp.GetRequiredService<RequestCaller>().User)) because standard DI cannot
// re-register a service per scope. The middleware rebinds User to the
// request's bound HttpCurrentUser once per request; under none-mode this
// type is not registered at all.
//
// Security review 2026-09-16: the default fails closed — an unbound scope is
// unauthenticated, so the authorization pipeline 401s; it can never default
// to authority. Request scopes are rebound by the current-principal
// middleware, and the seeder scopes (ClassCorpusSeederHost,
// MockPersonaSeederHost) bind the system actor explicitly (D1/D3, system
// seeding is not a user request).

using Soarscore.Application.Auth;

namespace Soarscore.Api.Auth;

public sealed class RequestCaller
{
    public ICurrentUser User { get; set; } = new AnonymousCurrentUser();
}
