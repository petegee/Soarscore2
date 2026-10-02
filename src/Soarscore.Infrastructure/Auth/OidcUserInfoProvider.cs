// The IUserInfoProvider implementation — ui_production-oidc-sign-in.md.
// Infrastructure owns it (not Application: it does HTTP; not Api: it is a
// port implementation, and this project already holds every other adapter).
// One GET per sign-in that needs it, at https://{Domain}/userinfo with the
// caller's bearer token — the token comes from the scoped CallerAccessToken,
// never from a command body. System.Text.Json and HttpClient are BCL, so no
// new package or layer widening.
//
// Semantics (the port's contract): 401/403 or a profile without an email
// returns null — the token simply lacks the profile scopes, so the handler
// keeps emailRequired. Everything else unusual — unreachable IdP, 5xx,
// timeout, unparseable body — throws UserInfoLookupException, which the
// handler maps to auth.signIn.identityLookupFailed (502). A caller-cancelled
// request is rethrown unwrapped: that is cancellation, not an IdP outage.
// The provider never disposes the HttpClient — the composition owns it as a
// singleton and the provider holds it only for the request's lifetime.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Soarscore.Application.Auth;

namespace Soarscore.Infrastructure.Auth;

public sealed record OidcUserInfoOptions(string Domain);

public sealed class OidcUserInfoProvider(
    HttpClient http,
    OidcUserInfoOptions options,
    CallerAccessToken tokens) : IUserInfoProvider
{
    public async Task<UserInfoResult?> LookupAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokens.Token))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{options.Domain}/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
            using var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UserInfoLookupException(
                    $"The identity provider answered the profile lookup with {(int)response.StatusCode} — sign-in cannot recover the email right now.");
            }

            return Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (UserInfoLookupException)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UserInfoLookupException(
                "The identity provider did not answer the profile lookup in time — sign-in cannot recover the email right now.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new UserInfoLookupException(
                "The identity provider could not be reached for the profile lookup — sign-in cannot recover the email right now.", ex);
        }
        catch (JsonException ex)
        {
            throw new UserInfoLookupException(
                "The identity provider answered the profile lookup unusably — sign-in cannot recover the email right now.", ex);
        }
    }

    // Standard OIDC claims (email, email_verified, name). email_verified may
    // arrive as a JSON boolean or as the "true"/"false" string JwtBearer-style
    // carriers use — anything else reads as unverified (fail closed, the
    // HttpCurrentUser precedent). A missing or blank email yields a result
    // with no email, which the handler treats exactly like a null lookup.
    private static UserInfoResult Parse(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var email = root.TryGetProperty("email", out var emailElement)
            && emailElement.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(emailElement.GetString())
            ? emailElement.GetString()
            : null;

        var verified = root.TryGetProperty("email_verified", out var verifiedElement)
            && verifiedElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(verifiedElement.GetString(), out var parsed) && parsed,
                _ => false,
            };

        var name = root.TryGetProperty("name", out var nameElement)
            && nameElement.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(nameElement.GetString())
            ? nameElement.GetString()
            : null;

        return new UserInfoResult(email, verified, name);
    }
}
