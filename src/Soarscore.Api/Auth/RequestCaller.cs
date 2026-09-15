// The request-scope carrier for ICurrentUser under mock/oidc —
// authentication-and-authorisation.md WI-9 step 6. Registered as a factory
// over it (services.AddScoped<ICurrentUser>(sp =>
// sp.GetRequiredService<RequestCaller>().User)) because standard DI cannot
// re-register a service per scope: the seeder hosts (ClassCorpusSeederHost,
// MockPersonaSeederHost) resolve IDispatcher from scopes that never run the
// current-principal middleware, and there the default SystemCurrentUser is
// exactly right — system seeding is not a user request, so it must not
// inherit (nor be mistaken for) a caller's authority (D1/D3). The middleware
// rebinds User to the request's bound HttpCurrentUser once per request; under
// none-mode this type is not registered at all.

using Soarscore.Application.Auth;

namespace Soarscore.Api.Auth;

public sealed class RequestCaller
{
    public ICurrentUser User { get; set; } = new SystemCurrentUser();
}
