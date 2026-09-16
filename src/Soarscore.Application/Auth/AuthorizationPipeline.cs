// The authorization pipeline — authentication-and-authorisation.md WI-5 (D1),
// the owner-decided enforcement point (2026-09-14). One behaviour step in the
// Dispatcher: resolve IAuthorizationPipeline, consult it before resolving any
// handler, map a denial straight to Result<T>.Failure. Registered only under
// oidc/mock (WI-9); under none-mode nothing resolves it and the Dispatcher
// behaves byte-identically to its pre-auth shape.
//
// The pipeline reads its table from CommandPolicyTable and FAILS CLOSED: a
// message type absent from the table denies with auth.policyMissing. It never
// throws — WI-10's totality property pins that — so a wiring bug surfaces as
// a loud 500-shaped failure, not an unhandled exception.

namespace Soarscore.Application.Auth;

/// <summary>One policy verdict — allowed, or denied with a stable code and a human message (the Result failure's shape).</summary>
public readonly record struct AuthzOutcome(bool Allowed, string? Code, string? Message)
{
    public static AuthzOutcome Allow() => new(true, null, null);

    public static AuthzOutcome Deny(string code, string message) => new(false, code, message);
}

/// <summary>
/// Per-kind policy for one slice of the §Per-command policy table. Receives
/// the current principal and the service provider so a policy that needs a
/// read-model lookup (the capture policy reads ICompetitionsQuery and
/// IEntryQuery, D10) resolves them per call — policies stay stateless
/// singletons.
/// </summary>
public interface ICommandPolicy
{
    Task<AuthzOutcome> AuthorizeAsync(object command, ICurrentUser user, IServiceProvider services, CancellationToken ct);
}

public interface IAuthorizationPipeline
{
    Task<AuthzOutcome> AuthorizeAsync(object message, CancellationToken cancellationToken);
}

/// <summary>
/// Message type → policy, per CommandPolicyTable, with the current principal
/// attached. Constructor-injected ICurrentUser mirrors every handler's
/// port-injection shape; the IServiceProvider is what stateless policies
/// resolve their read-model lookups through.
/// </summary>
public sealed class AuthorizationPipeline(ICurrentUser currentUser, IServiceProvider services) : IAuthorizationPipeline
{
    public async Task<AuthzOutcome> AuthorizeAsync(object message, CancellationToken cancellationToken)
    {
        if (!CommandPolicyTable.Table.TryGetValue(message.GetType(), out var policy))
        {
            return AuthzOutcome.Deny(
                "auth.policyMissing",
                $"No policy is registered for {message.GetType().Name} — the pipeline fails closed.");
        }

        return await policy.AuthorizeAsync(message, currentUser, services, cancellationToken);
    }
}
