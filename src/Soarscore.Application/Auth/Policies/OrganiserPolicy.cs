// authentication-and-authorisation.md WI-5 — the Organiser role (glossary
// "Role"): contest structure, rulings and CD authority for v1, the role the
// trust model grants "can do everything" reach. An unauthenticated caller is
// a 401 (mode table: anonymous callers get 401s, unauthorised ones 403s) —
// D10's step-1 shape, same as SelfOrOrganiserPolicy. The role check is the
// rest of the policy — auth.forbidden maps to 403 (WI-9's status-code table).

using Soarscore.Domain.People;

namespace Soarscore.Application.Auth.Policies;

public sealed class OrganiserPolicy : ICommandPolicy
{
    public Task<AuthzOutcome> AuthorizeAsync(object command, ICurrentUser user, IServiceProvider services, CancellationToken ct)
    {
        if (!user.IsAuthenticated)
        {
            return Task.FromResult(AuthzOutcome.Deny("auth.notAuthenticated", "An authenticated principal is required."));
        }

        return Task.FromResult(user.HasRole(PersonRole.Organiser)
            ? AuthzOutcome.Allow()
            : AuthzOutcome.Deny("auth.forbidden", "The Organiser role is required."));
    }
}
