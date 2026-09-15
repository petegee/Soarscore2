// authentication-and-authorisation.md WI-5 — the default for every query (D4:
// default secure; per-query public reads such as a public leaderboard are a
// later per-query opt-out, stubbed as a backlog story) and, when WI-7 lands,
// LinkSignIn and WhoAmI. The whole policy is one branch: a validated token is
// the only requirement. Roles and capture policy are never consulted here —
// a principal's authority beyond authentication is the other policies' job.

namespace Soarscore.Application.Auth.Policies;

public sealed class AuthenticatedPolicy : ICommandPolicy
{
    public Task<AuthzOutcome> AuthorizeAsync(object command, ICurrentUser user, IServiceProvider services, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated
            ? AuthzOutcome.Allow()
            : AuthzOutcome.Deny("auth.notAuthenticated", "An authenticated principal is required."));
}
