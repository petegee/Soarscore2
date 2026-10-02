// The raw bearer-token carrier for the userinfo fallback —
// ui_production-oidc-sign-in.md. ICurrentUser exposes only parsed claims, and
// the JwtBearer middleware binds the ClaimsPrincipal, not the token, so the
// Infrastructure /userinfo implementation needs the token from somewhere
// else: the Api middleware captures the request's Authorization header here
// once per request, and the provider reads it. Scoped (per request), set by
// middleware, read by the provider — the same carrier shape as the Api's
// RequestCaller, but living in Application so Infrastructure can depend on it
// without depending back on Api (LayerRuleTests). A null token means no
// bearer arrived; the provider then reports null, never a lookup failure.
namespace Soarscore.Application.Auth;

public sealed class CallerAccessToken
{
    public string? Token { get; set; }
}
