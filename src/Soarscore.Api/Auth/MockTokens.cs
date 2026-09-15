// The mock persona token minter — authentication-and-authorisation.md WI-9
// step 5 (D11). HS256, signed with the config-pinned static SigningKey,
// issuer/audience per MockAuthOptions, sub = "mock|<slug>" so the standard
// provider|subject parsing rule resolves the seeded identity link unchanged.
// Only sub/email/name ride the token — roles never do (D2), in mock just as
// in production.

using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Soarscore.Api.Auth;

internal static class MockTokens
{
    public static string Mint(MockAuthOptions options, MockPersona persona, DateTimeOffset now) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = MockAuthOptions.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            Expires = now.UtcDateTime.AddDays(30),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(options.SigningKey),
                SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", $"mock|{persona.Slug}"),
                new Claim("email", persona.Email),
                new Claim("name", persona.Name),
            ]),
        });
}
