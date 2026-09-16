// authentication-and-authorisation.md WI-5 — self-service for one's own
// record, organiser for anyone's (§Per-command policy table's S kind: the
// person commands). "Requires an authenticated principal" takes D10's step-1
// shape: an unauthenticated caller is a 401, never a 403. Self is the
// principal's PersonId — D2-resolved from the read model, never a token
// claim — equal to the command's PersonRef; an authenticated-but-unlinked
// identity has no PersonId, so the self-half can never match it and only the
// organiser half can still pass.

using Soarscore.Domain.People;

namespace Soarscore.Application.Auth.Policies;

public sealed class SelfOrOrganiserPolicy : ICommandPolicy
{
    public Task<AuthzOutcome> AuthorizeAsync(object command, ICurrentUser user, IServiceProvider services, CancellationToken ct)
    {
        if (!user.IsAuthenticated)
        {
            return Task.FromResult(AuthzOutcome.Deny("auth.notAuthenticated", "An authenticated principal is required."));
        }

        if (command is ISelfPersonCommand self && user.PersonId == self.PersonRef)
        {
            return Task.FromResult(AuthzOutcome.Allow());
        }

        return Task.FromResult(user.HasRole(PersonRole.Organiser)
            ? AuthzOutcome.Allow()
            : AuthzOutcome.Deny("auth.forbidden", "Only the person themself or an organiser may do this."));
    }
}
