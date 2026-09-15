// authentication-and-authorisation.md WI-5 — the Organiser role (glossary
// "Role"): contest structure, rulings and CD authority for v1, the role the
// trust model grants "can do everything" reach. The role check is the whole
// policy — WI-9's status-code table maps its one code, auth.forbidden, to 403.

using Soarscore.Domain.People;

namespace Soarscore.Application.Auth.Policies;

public sealed class OrganiserPolicy : ICommandPolicy
{
    public Task<AuthzOutcome> AuthorizeAsync(object command, ICurrentUser user, IServiceProvider services, CancellationToken ct) =>
        Task.FromResult(user.HasRole(PersonRole.Organiser)
            ? AuthzOutcome.Allow()
            : AuthzOutcome.Deny("auth.forbidden", "The Organiser role is required."));
}
