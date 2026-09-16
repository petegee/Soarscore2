// The mock issuer + persona set — authentication-and-authorisation.md WI-9
// (D11). Produced by AuthSettings only under Soarscore:Auth:Mode=mock and
// registered as a singleton for the persona seeder host. Production never
// sees one (D8 refuses mock before this type could exist).

using Soarscore.Domain.People;

namespace Soarscore.Api.Auth;

/// <summary>
/// One dev persona: a person, an identity link (provider "mock", subject = the
/// slug) and the roles to grant — seeded at startup via the real command path,
/// so switching personas locally exercises the real grant → resolve → enforce
/// chain (D11). Roles are never claims, not even in mock.
/// </summary>
internal sealed record MockPersona(string Name, string Email, IReadOnlyList<PersonRole> Roles)
{
    // The persona's identity subject: the seeded identity link is
    // (provider "mock", subject = slug) and the persona token's sub is
    // "mock|<slug>" — one derivation feeding both sides of the resolution.
    // Stable across boots, which is what makes re-running the seeder find
    // the personas it seeded last time. Duplicated slugs are refused by
    // AuthSettings at boot.
    public string Slug { get; } = Name.ToLowerInvariant().Replace(' ', '-');
}

internal sealed record MockAuthOptions(string Audience, byte[] SigningKey, IReadOnlyList<MockPersona> Personas)
{
    /// <summary>
    /// The local issuer mock tokens carry and the mock JwtBearer validation
    /// pins — the one string both the seeder's minting and the validation
    /// agree on. WI-10's TestJwt mints against the same constants, which is
    /// what makes mock and the acceptance suite the same mechanism (D11).
    /// </summary>
    public const string Issuer = "https://soarscore.mock";
}
