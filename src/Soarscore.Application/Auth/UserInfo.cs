// The userinfo port — ui_production-oidc-sign-in.md (owner decision ii,
// LADR-0004 D2 preserved: no Auth0 Action copies claims onto the access
// token). A real Auth0 custom-API access token carries no email claim, so
// LinkSignIn's email-born arms cannot run on token claims alone; this port is
// the one outbound call per sign-in that recovers them from the IdP's
// /userinfo endpoint with the caller's own bearer token.
//
// Transport-blind: Application names no HTTP type (LADR-0001 §4.2 —
// Application depends on Domain only, both this file's contents are BCL).
// The bearer token arrives through CallerAccessToken (the scoped carrier the
// Api middleware fills), never through a command body.
namespace Soarscore.Application.Auth;

/// <summary>
/// The caller's OIDC profile as the IdP reports it. A userinfo email counts
/// as verified ONLY when <see cref="EmailVerified"/> is true — the handler
/// applies its existing emailNotVerified gate to this value exactly as it
/// does to the token claim.
/// </summary>
public sealed record UserInfoResult(string? Email, bool EmailVerified, string? Name);

public interface IUserInfoProvider
{
    /// <summary>
    /// Looks up the caller's profile at the IdP's /userinfo endpoint.
    /// Returns null when there is no usable email to report — no bearer token
    /// to forward, the token lacks the profile scopes (401/insufficient
    /// scope), or the profile carries no email — so the caller keeps its
    /// existing emailRequired semantics, NOT a 502. Transport failures (the
    /// IdP unreachable, 5xx, timeout, unparseable body) throw
    /// <see cref="UserInfoLookupException"/> instead, which the caller maps
    /// to auth.signIn.identityLookupFailed (502).
    /// </summary>
    Task<UserInfoResult?> LookupAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The port's failure signal: the IdP could not be asked, or answered
/// unusably. Distinct from a null lookup — this one becomes a 502, the null
/// keeps emailRequired. Carries only a message (no status, no body): the
/// caller decides the failure code.
/// </summary>
public sealed class UserInfoLookupException(string message, Exception? inner = null)
    : Exception(message, inner);
