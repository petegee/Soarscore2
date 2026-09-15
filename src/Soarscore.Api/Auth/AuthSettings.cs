// Soarscore:Auth — authentication-and-authorisation.md WI-9, steps 1–2. The
// mode selection is validated HERE, once, at Composition.Build time, because
// D8's promise is a loud crash at boot rather than a silently open (or
// mock-keyed) API:
//
//   - Production refuses every mode but "oidc" (none/unset/mock throw,
//     naming Soarscore:Auth:Mode).
//   - "mock" demands Soarscore:Auth:Mock:SigningKey (base64, ≥ 32 bytes —
//     HS256's 256-bit floor) and Soarscore:Auth:Mock:Audience.
//   - "oidc" demands Soarscore:Auth:Domain and Soarscore:Auth:Audience. The
//     Mock block's static SigningKey is honoured under "oidc" TOO (step 1):
//     when present, TokenValidationParameters are pinned to issuer + audience
//     + key instead of Authority metadata retrieval — the same mechanism
//     mock uses, which is what makes mock and WI-10's acceptance suite one
//     thing rather than two.
//   - An unknown mode value is a config typo, thrown at boot.
//   - Development + "none" (the default) validates to nothing — the
//     byte-identical composition every existing test runs under.
//
// Api-internal: only Composition consumes this; the story's guard tests
// exercise it through Build's args.

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Soarscore.Domain.People;

namespace Soarscore.Api.Auth;

internal enum AuthMode
{
    None,
    Mock,
    Oidc,
}

internal sealed record AuthSettings
{
    public required AuthMode Mode { get; init; }

    // D3: the config-listed emails whose holders become Organiser at
    // sign-in/link time. Bound in every mode (LinkSignIn's handler is
    // registered in every mode); empty by default.
    public required IReadOnlyList<string> BootstrapOrganisers { get; init; }

    public string? Domain { get; init; }

    public string? Audience { get; init; }

    // The static signing key (base64-decoded), honoured under "oidc" too.
    public byte[]? StaticSigningKey { get; init; }

    // Non-null only under "mock": the persona set the seeder provisions.
    public MockAuthOptions? Mock { get; init; }

    public static AuthSettings From(IConfiguration configuration, bool isProduction)
    {
        var rawMode = configuration["Soarscore:Auth:Mode"];
        var mode = rawMode switch
        {
            null or "" => AuthMode.None,
            "none" => AuthMode.None,
            "mock" => AuthMode.Mock,
            "oidc" => AuthMode.Oidc,
            _ => throw new InvalidOperationException(
                $"Soarscore:Auth:Mode '{rawMode}' is not one of none | mock | oidc — fix the configuration; the composition refuses to guess."),
        };

        if (isProduction && mode != AuthMode.Oidc)
        {
            throw new InvalidOperationException(
                $"Soarscore:Auth:Mode is {(rawMode is null or "" ? "not set" : $"'{rawMode}'")} — "
                + "Production refuses every mode but \"oidc\" (authentication-and-authorisation.md D8): "
                + "a release deployment that wanted to dodge auth gets this loud crash, not a silently open or mock-keyed API.");
        }

        var bootstrap = configuration.GetSection("Soarscore:Auth:BootstrapOrganisers").Get<string[]>() ?? [];

        return mode switch
        {
            AuthMode.None => new AuthSettings { Mode = mode, BootstrapOrganisers = bootstrap },
            AuthMode.Mock => MockAuth(configuration, bootstrap),
            AuthMode.Oidc => OidcAuth(configuration, bootstrap),
            _ => throw new UnreachableException(),
        };
    }

    private static AuthSettings MockAuth(IConfiguration configuration, IReadOnlyList<string> bootstrap)
    {
        var signingKey = ParseSigningKey(
            configuration["Soarscore:Auth:Mock:SigningKey"],
            "Soarscore:Auth:Mock:SigningKey is missing — mock mode validates tokens with this static key "
            + "(authentication-and-authorisation.md D8/D11).");
        var audience = RequireValue(
            configuration["Soarscore:Auth:Mock:Audience"],
            "Soarscore:Auth:Mock:Audience is missing — mock mode pins the persona tokens' audience to it (D11).");

        return new AuthSettings
        {
            Mode = AuthMode.Mock,
            BootstrapOrganisers = bootstrap,
            Audience = audience,
            StaticSigningKey = signingKey,
            Mock = new MockAuthOptions(audience, signingKey, ParsePersonas(configuration)),
        };
    }

    private static AuthSettings OidcAuth(IConfiguration configuration, IReadOnlyList<string> bootstrap)
    {
        var domain = RequireValue(
            configuration["Soarscore:Auth:Domain"],
            "Soarscore:Auth:Domain is missing — under mode \"oidc\" the API validates tokens against the "
            + "identity provider's domain (authentication-and-authorisation.md D8).");
        var audience = RequireValue(
            configuration["Soarscore:Auth:Audience"],
            "Soarscore:Auth:Audience is missing — under mode \"oidc\" the API validates the token's "
            + "audience claim against it (authentication-and-authorisation.md D8).");

        // The Mock block's key is honoured under "oidc" too: present, the
        // validation parameters are pinned (no metadata retrieval); absent, the
        // JwtBearer handler performs normal Authority discovery.
        var rawStaticKey = configuration["Soarscore:Auth:Mock:SigningKey"];
        var staticKey = string.IsNullOrWhiteSpace(rawStaticKey)
            ? null
            : ParseSigningKey(rawStaticKey, "Soarscore:Auth:Mock:SigningKey is not a usable static key");

        return new AuthSettings
        {
            Mode = AuthMode.Oidc,
            BootstrapOrganisers = bootstrap,
            Domain = domain,
            Audience = audience,
            StaticSigningKey = staticKey,
        };
    }

    private static byte[] ParseSigningKey(string? raw, string failure)
    {
        if (raw is not { Length: > 0 })
        {
            throw new InvalidOperationException(failure);
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(raw);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{failure}: the value is not valid base64.");
        }

        return key.Length >= 32
            ? key
            : throw new InvalidOperationException(
                $"{failure}: the decoded key is {key.Length} bytes — HS256 needs at least 32 (256 bits).");
    }

    // D11's persona block. Every persona is validated here (name, email, known
    // roles, unique slug) so a config typo fails at boot rather than mid-seed.
    private static IReadOnlyList<MockPersona> ParsePersonas(IConfiguration configuration)
    {
        var section = configuration.GetSection("Soarscore:Auth:Mock:Personas");
        var personas = new List<MockPersona>();

        foreach (var persona in section.GetChildren())
        {
            var name = RequireValue(persona["Name"], $"Soarscore:Auth:Mock:Personas[{persona.Key}].Name is missing.");
            var email = RequireValue(persona["Email"], $"Soarscore:Auth:Mock:Personas[{persona.Key}].Email is missing.");

            var roles = new List<PersonRole>();
            foreach (var role in persona.GetSection("Roles").GetChildren())
            {
                if (!Enum.TryParse(role.Value, ignoreCase: true, out PersonRole parsed))
                {
                    throw new InvalidOperationException(
                        $"Soarscore:Auth:Mock:Personas[{persona.Key}].Roles has '{role.Value}' — not a PersonRole "
                        + "(Competitor | Organiser).");
                }

                roles.Add(parsed);
            }

            personas.Add(new MockPersona(name, email, roles));
        }

        var duplicated = personas.Select(p => p.Slug).GroupBy(slug => slug).FirstOrDefault(group => group.Count() > 1);
        if (duplicated is { } clash)
        {
            throw new InvalidOperationException(
                $"Two configured personas derive the same slug '{clash.Key}' — the identity link (provider \"mock\", "
                + "subject = slug) must be unique. Rename one of the personas.");
        }

        return personas;
    }

    private static string RequireValue(string? value, string failure) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(failure) : value;
}
