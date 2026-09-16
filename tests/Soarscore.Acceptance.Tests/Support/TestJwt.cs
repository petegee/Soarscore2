// authentication-and-authorisation.md WI-10 — the acceptance suite's token
// minter (D11's "TestJwt" helper): HS256 against the same static signing key
// and issuer the mock-mode host validates with, so the suite and the shipped
// mock path are one mechanism. Only sub/email/name/email_verified ride the
// token — roles never do (D2), exactly the Api's own MockTokens shape.
// email_verified is minted true so every persona exercises the verified path
// the email-born decisions require; ForIdentityWithoutVerifiedEmail mints the
// unverified shape the security review of 2026-09-16 gate refuses. People
// carry sub = "mock|<slug>" (matching the seeded identity link); the machine
// actor carries a bare client id with no | — D12's client-credentials rule.
//
// MockAuthOptions.Issuer is internal to the Api, so the one string it pins is
// restated here and both files say so — WI-9's MockAuthOptions comment
// anticipates exactly this constant.

using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Soarscore.Acceptance.Tests.Support;

/// <summary>The personas the auth-mode fixture seeds, and the machine actor.</summary>
public sealed record AuthPersona(string Slug, string Name, string Email)
{
    public override string ToString() => Slug;
}

public static class AuthActors
{
    /// <summary>D12: the machine actor's sub is the bare client id — no | separator.</summary>
    public const string MachineClientId = "field-clock-rig";

    public static readonly AuthPersona Organiser = new("pete", "Pete", "pete@example.org");
    public static readonly AuthPersona Competitor = new("tama", "Tama", "tama@example.org");
    public static readonly AuthPersona Unlinked = new("fieldrig", "FieldRig", "rig@example.org");
    public static readonly AuthPersona Bootstrap = new("nova", "Nova", "nova@example.org");
}

public static class TestJwt
{
    // MockAuthOptions.Issuer (Soarscore.Api.Auth, internal) pins this exact
    // string; the mock-mode host and these tokens agree by construction.
    public const string Issuer = "https://soarscore.mock";

    public const string Audience = "soarscore-acceptance";

    /// <summary>A fixed >= 32-byte key, base64 — the same value the fixture config carries.</summary>
    public static readonly string SigningKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(1, 64).Select(i => (byte)i).ToArray());

    public static string ForPerson(AuthPersona persona) => Mint($"mock|{persona.Slug}", persona.Email, persona.Name);

    /// <summary>
    /// A token for an ad-hoc identity the seeder has never seen — the
    /// SigningIn scenarios' creation assertions need a caller no earlier
    /// scenario can have made a person, so each scenario mints its own
    /// unique (provider, subject) pair.
    /// </summary>
    public static string ForIdentity(string sub, string? email, string? name) => Mint(sub, email, name);

    /// <summary>A second IdP's token for the same human: different provider/subject, same email (D5's email-match arm).</summary>
    public static string ForSecondProvider(AuthPersona persona) =>
        Mint($"google-oauth2|{persona.Slug}-alt", persona.Email, persona.Name);

    /// <summary>
    /// A token whose email claim is present but NOT verified (security review
    /// 2026-09-16): the IdP has not vouched for the address, so every
    /// email-born decision — email-matched linking, creation, the bootstrap
    /// grant — must refuse it with auth.signIn.emailNotVerified.
    /// </summary>
    public static string ForIdentityWithoutVerifiedEmail(string sub, string? email, string? name) =>
        Mint(sub, email, name, emailVerified: false);

    public static string ForMachine() => Mint(AuthActors.MachineClientId, null, AuthActors.MachineClientId);

    /// <summary>A machine token for an ad-hoc client id — sub without | (D12).</summary>
    public static string ForMachine(string clientId) => Mint(clientId, null, clientId);

    private static string Mint(string sub, string? email, string? name, bool emailVerified = true)
    {
        var claims = new List<Claim> { new("sub", sub) };
        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        claims.Add(new Claim("email_verified", emailVerified ? "true" : "false"));

        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddDays(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(SigningKeyBase64)),
                SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(claims),
        });
    }
}
