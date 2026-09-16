// Mock persona seeding — authentication-and-authorisation.md WI-9 step 5
// (D11). Registered ONLY under Soarscore:Auth:Mode=mock, starting before the
// web host like ClassCorpusSeederHost (registered hosted services run before
// GenericWebHostService), so the personas exist — and their bearer tokens are
// printed — before the API accepts a request.
//
// Seeding walks the REAL command path through IDispatcher: RegisterPerson
// (the organiser's manual pre-registration), BindIdentity (provider "mock",
// subject = the persona slug), then GrantRole per configured role. The
// system actor (SystemCurrentUser, which carries Organiser) is bound
// EXPLICITLY to the scope below — the middleware never runs in this scope
// and RequestCaller's default fails closed to AnonymousCurrentUser — so the
// authorization pipeline allows exactly what an organiser would have
// appended. Real store events; roles come from the seeded store data, never
// claims.
//
// Re-running is a no-op for existing personas: an identity link already
// seeded short-circuits registration, and a role already held is not
// re-granted. A seeding failure is a bug or a config error — StartAsync
// propagates and the host fails fast (the ClassCorpusSeederHost precedent).

using Soarscore.Application.Auth;
using Soarscore.Application;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.People;
using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Api.Auth;

internal sealed class MockPersonaSeederHost(
    MockAuthOptions options,
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<MockPersonaSeederHost> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        // System seeding is not a user request (D1/D3): bind the system actor
        // explicitly — the carrier's default is anonymous, not authority.
        // (The carrier exists only under mock/oidc; none-mode has no pipeline.)
        if (scope.ServiceProvider.GetService<RequestCaller>() is { } caller)
        {
            caller.User = new SystemCurrentUser();
        }
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var people = scope.ServiceProvider.GetRequiredService<IPeopleQuery>();

        foreach (var persona in options.Personas)
        {
            var personId = await EnsurePersonAsync(dispatcher, people, persona, cancellationToken);
            await EnsureRolesAsync(dispatcher, people, persona, personId, cancellationToken);

            // D11's token distribution is the console: paste into Swagger UI's
            // Authorize button or a curl header. No mock-token endpoint, no UI
            // (NFR-3 headless); picking a persona for a run = choosing the
            // config set before start, then picking the printed token.
            logger.LogInformation(
                "Mock persona \"{Name}\" <{Email}> — Authorization: Bearer {Token}",
                persona.Name,
                persona.Email,
                MockTokens.Mint(options, persona, clock.UtcNow));
        }

        logger.LogInformation(
            "Mock personas seeded: {Count} configured — sign in with POST /link-sign-in, resolve with GET /who-am-i.",
            options.Personas.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Identity link first (D5's ordering — the unique (provider, subject)
    // index is the arbiter), then the persona's email (the person may already
    // be registered through the API — bind the mock identity to THAT person,
    // the same second arm LinkSignIn walks), then create.
    private static async Task<PersonId> EnsurePersonAsync(
        IDispatcher dispatcher, IPeopleQuery people, MockPersona persona, CancellationToken cancellationToken)
    {
        if (await people.FindIdentityAsync("mock", persona.Slug, cancellationToken) is { } match)
        {
            return match.PersonId;
        }

        if (await people.FindByEmailAsync(persona.Email, cancellationToken) is { } existing)
        {
            var bound = await RequireAsync(
                dispatcher.SendAsync(new BindIdentity(existing.Id, "mock", persona.Slug), cancellationToken),
                $"binding the mock identity of persona \"{persona.Name}\"");
            return bound;
        }

        var registered = await RequireAsync(
            dispatcher.SendAsync(
                new RegisterPerson(persona.Name, new ContactDetails { Email = persona.Email }, null),
                cancellationToken),
            $"registering persona \"{persona.Name}\"");

        return await RequireAsync(
            dispatcher.SendAsync(new BindIdentity(registered, "mock", persona.Slug), cancellationToken),
            $"binding the mock identity of persona \"{persona.Name}\"");
    }

    private static async Task EnsureRolesAsync(
        IDispatcher dispatcher, IPeopleQuery people, MockPersona persona, PersonId personId, CancellationToken cancellationToken)
    {
        var summaries = await people.FindByIdsAsync([personId], cancellationToken);
        var held = summaries.Count > 0 ? summaries[0].Roles : [];

        foreach (var role in persona.Roles.Where(role => !held.Contains(role)))
        {
            await RequireAsync(
                dispatcher.SendAsync(new GrantRole(personId, role), cancellationToken),
                $"granting {role} to persona \"{persona.Name}\"");
        }
    }

    // A dispatcher failure here is a wiring bug or a config error — both fail
    // the boot (Value throws on failure; IsSuccess checked first).
    private static async Task<T> RequireAsync<T>(Task<Result<T>> result, string what)
    {
        var outcome = await result;
        if (!outcome.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Mock persona seeding failed while {what}: {outcome.Code} — {outcome.Message}");
        }

        return outcome.Value;
    }
}
