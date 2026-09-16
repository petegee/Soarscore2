// The current-user port — authentication-and-authorisation.md WI-5 (D2).
// Application's one seam to "who is acting": the Api layer's JwtBearer
// middleware (WI-9) validates the token and implements this port as
// HttpCurrentUser, resolving PersonId + roles from the read model per request
// (IPeopleQuery.FindIdentityAsync). Roles never live in tokens (D2) — they
// ride the port so a grant takes effect on the next request with no
// token-refresh dance. The Dispatcher's pipeline (D1) is the only consumer
// the core has; nothing else in Application reads it.

using Soarscore.Domain.People;

namespace Soarscore.Application.Auth;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    string? Provider { get; }      // "google-oauth2", "auth0", …

    string? Subject { get; }       // IdP user id

    string? Email { get; }

    // The token's display name — WI-7's LinkSignIn create path registers the
    // person from it (defaulting to the email local-part). D11/D12 keep the
    // claim on the token; D2 keeps authority out of it: a name is never
    // authorisation, so it rides the port without threatening the
    // roles-never-live-in-tokens rule.
    string? Name { get; }

    // Security review 2026-09-16: the port's email-born decisions — D5's
    // email-match link arm and create arm in LinkSignIn, and D3's bootstrap
    // grant on the config-listed emails — act on the token's email, so the
    // IdP must vouch for the address (its email_verified claim). Roles and
    // identity never come from claims, but email-matching does, and an
    // unverified email is an attacker-chosen string: anyone can register it
    // at the IdP and present it as someone else's address.
    bool EmailVerified { get; }

    PersonId? PersonId { get; }    // null until linked

    IReadOnlyList<PersonRole> Roles { get; }

    bool HasRole(PersonRole role);
}

/// <summary>
/// The system actor — WI-9's seeder host only (D11's persona seeding and
/// ClassCorpusSeeder both dispatch through the pipeline at startup, and
/// neither is a user request). Registered into the seeder scope, never the
/// request scope: system seeding is not a user, so it must not inherit a
/// caller's authority. IsAuthenticated and the Organiser role are the whole
/// authority; Provider/Subject/Email/PersonId stay null — the system is
/// nobody's identity link.
/// </summary>
public sealed record SystemCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => true;

    public string? Provider => null;

    public string? Subject => null;

    public string? Email => null;

    public string? Name => null;

    // No email-born decisions run as the system actor, but it is not an
    // attacker either — there is no unverified claim to distrust.
    public bool EmailVerified => true;

    public PersonId? PersonId => null;

    public IReadOnlyList<PersonRole> Roles { get; init; } = [PersonRole.Organiser];

    public bool HasRole(PersonRole role) => Roles.Contains(role);
}

/// <summary>
/// The stock principal registered under none-mode (D1/D8): the trust model
/// the club tool shipped with. Unauthenticated and unlinked, so every
/// non-Organiser check denies it — but under none-mode no pipeline is
/// registered at all, so nothing actually consults it. It exists so
/// compositions (and the WI-10 acceptance suite's none-mode factory instance)
/// always have a current user to resolve, keeping the port total.
/// </summary>
public sealed record AnonymousCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public string? Provider => null;

    public string? Subject => null;

    public string? Email => null;

    public string? Name => null;

    public bool EmailVerified => false;

    public PersonId? PersonId => null;

    public IReadOnlyList<PersonRole> Roles { get; } = [];

    public bool HasRole(PersonRole role) => false;
}
