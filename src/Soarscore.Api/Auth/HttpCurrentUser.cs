// The Api's ICurrentUser implementation — authentication-and-authorisation.md
// WI-9 step 4 (D2). One instance per request, registered scoped; the
// current-principal middleware (Composition) binds the JwtBearer-validated
// ClaimsPrincipal to it and resolves the identity in the same pass.
//
// The token supplies sub/email/name plus email_verified — the last is the
// IdP's vouching for the address, and it is the only claim beyond sub that
// an email-born decision (D5's email-match link and create arms, D3's
// bootstrap grant) is allowed to act on (security review 2026-09-16).
//
// The sub claim is parsed on the FIRST '|': "provider|subject" is an
// interactive identity; a sub WITHOUT '|' is a client-credentials (machine)
// token — the client itself is the actor, provider "client-credentials",
// subject = the client id (D12). A sub that parses to empty halves (or is
// missing entirely) leaves the user unauthenticated so the pipeline answers
// auth.notAuthenticated (401) instead of guessing at an identity.
//
// D2: PersonId and roles are never claims. They resolve from the people read
// model — one indexed IPeopleQuery.FindIdentityAsync per request, resolved in
// the middleware and cached here for the request's lifetime — so a role grant
// takes effect on the caller's next request with no token-refresh dance.

using System.Security.Claims;
using Soarscore.Application.Auth;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;

namespace Soarscore.Api.Auth;

public sealed class HttpCurrentUser(IPeopleQuery peopleQuery) : ICurrentUser
{
    // D12: machine tokens carry sub without the '|' separator.
    private const string ClientCredentialsProvider = "client-credentials";

    private ClaimsPrincipal? principal;
    private string? provider;
    private string? subject;
    private PersonId? personId;
    private IReadOnlyList<PersonRole> roles = [];

    /// <summary>
    /// Binds the validated principal and resolves the identity (one indexed
    /// read-model lookup per request, D2). Called once, by the middleware,
    /// before the endpoint — everything downstream (policies, handlers)
    /// reads the cached values.
    /// </summary>
    public async Task BindAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        this.principal = principal;

        if (principal.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var sub = principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
        {
            return;
        }

        var separator = sub.IndexOf('|');
        if (separator < 0)
        {
            // D12: no '|' ⇒ the sub is a client id — the client is the actor.
            provider = ClientCredentialsProvider;
            subject = sub;
        }
        else
        {
            var parsedProvider = sub[..separator];
            var parsedSubject = sub[(separator + 1)..];
            if (parsedProvider.Length == 0 || parsedSubject.Length == 0)
            {
                // Malformed — the principal stays unauthenticated so the
                // pipeline 401s (step 4). No invented identity.
                return;
            }

            provider = parsedProvider.ToLowerInvariant();
            subject = parsedSubject;
        }

        if (await peopleQuery.FindIdentityAsync(provider, subject, cancellationToken) is { } match)
        {
            personId = match.PersonId;
            roles = match.Roles;
        }
    }

    // Authenticated means: a validated principal whose sub parsed into a
    // (provider, subject) pair. An unlinked identity is still authenticated
    // (nobody yet — PersonId null); a missing or malformed sub is not.
    public bool IsAuthenticated => subject is not null;

    public string? Provider => provider;

    public string? Subject => subject;

    public string? Email => principal?.FindFirstValue("email");

    // MapInboundClaims is false, so the wire claim name is "email_verified".
    // JwtBearer surfaces claim values as strings and an IdP boolean may
    // arrive as "True"/"true" — bool.TryParse handles both; a missing or
    // unparseable claim reads as unverified (fail closed, security review
    // 2026-09-16).
    public bool EmailVerified => bool.TryParse(principal?.FindFirstValue("email_verified"), out var verified) && verified;

    public string? Name => principal?.FindFirstValue("name");

    public PersonId? PersonId => personId;

    public IReadOnlyList<PersonRole> Roles => roles;

    public bool HasRole(PersonRole role) => roles.Contains(role);
}
