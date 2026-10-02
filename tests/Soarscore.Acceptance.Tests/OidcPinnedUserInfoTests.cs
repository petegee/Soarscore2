// ui_production-oidc-sign-in.md (owner decision ii): the /userinfo fallback
// over real HTTP on the oidc-pinned path — Development + oidc + the static
// signing key, which is exactly the mechanism the shipped mock path and the
// WI-10 suite share, minus the personas. The IdP itself is stubbed: the real
// OidcUserInfoProvider is replaced with a stub behind the same port, so these
// facts prove the composition wiring (token capture, registration, status
// mapping) without a live tenant. The mock-mode factory cannot cover this —
// it registers no provider at all.
//
// A class of its own, not a Reqnroll feature: this needs a second factory
// with its own store (a sqlite file, no container) and its own env block.
// Both shared fixtures are ensured first so their env-mutating lazy inits
// cannot interleave with this one's (the AuthAcceptanceFixture header records
// why env is the only seam that reaches Composition.Build).

using System.Net.Http.Json;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Auth;
using Soarscore.Domain.People;
using Xunit;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Soarscore.Acceptance.Tests;

public sealed class OidcPinnedUserInfoTests : IAsyncLifetime
{
    private const string Domain = "oidc-pinned-test.example.com";
    private const string Audience = "soarscore-oidc-pinned";

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string? _sqlitePath;
    private readonly StubUserInfo _userInfo = new();

    private sealed class StubUserInfo : IUserInfoProvider
    {
        public Func<Task<UserInfoResult?>> Next { get; set; } = () => Task.FromResult<UserInfoResult?>(null);

        public Task<UserInfoResult?> LookupAsync(CancellationToken cancellationToken) => Next();
    }

    public async ValueTask InitializeAsync()
    {
        // Serialize after the shared fixtures' lazy inits (env-mutating, see
        // above): once they have built, later tests never touch env again.
        await AcceptanceFixture.EnsureInitializedAsync();
        await AuthAcceptanceFixture.EnsureInitializedAsync();

        _sqlitePath = Path.Combine(Path.GetTempPath(), $"soarscore-acceptance-oidc-{Guid.NewGuid():N}.db");

        var touched = CaptureEnvironment(
        [
            "ConnectionStrings__Soarscore",
            "Soarscore__Store",
            "Soarscore__SeedCorpus",
            "Soarscore__Auth__Mode",
            "Soarscore__Auth__Domain",
            "Soarscore__Auth__Audience",
            "Soarscore__Auth__Mock__SigningKey",
        ]);

        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Soarscore", $"Data Source={_sqlitePath}");
            Environment.SetEnvironmentVariable("Soarscore__Store", "sqlite");
            Environment.SetEnvironmentVariable("Soarscore__SeedCorpus", "false");
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mode", "oidc");
            Environment.SetEnvironmentVariable("Soarscore__Auth__Domain", Domain);
            Environment.SetEnvironmentVariable("Soarscore__Auth__Audience", Audience);
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__SigningKey", TestJwt.SigningKeyBase64);

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                    services.AddScoped<IUserInfoProvider>(_ => _userInfo)));
            _client = _factory.CreateClient();
        }
        finally
        {
            RestoreEnvironment(touched);
        }

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _client = null;

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }

        if (_sqlitePath is not null)
        {
            foreach (var file in new[] { _sqlitePath, _sqlitePath + "-wal", _sqlitePath + "-shm" })
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            _sqlitePath = null;
        }
    }

    // A token shaped like a real Auth0 access token on the pinned path: the
    // pinned issuer/audience/key the composition validates with, sub only —
    // no email claim for the fallback to recover.
    private static string MintWithoutEmail(string sub) => Mint(sub, email: null);

    private static string MintWithEmail(string sub, string email) => Mint(sub, email);

    private static string Mint(string sub, string? email)
    {
        var claims = new List<Claim> { new("sub", sub) };
        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        claims.Add(new Claim("email_verified", "true"));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = $"https://{Domain}/",
            Audience = Audience,
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddDays(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwt.SigningKeyBase64)),
                SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(claims),
        });
    }

    private async Task<HttpResponseMessage> PostLinkSignInAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/link-sign-in")
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Authorization = new("Bearer", token);
        return await _client!.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_brand_new_user_without_a_token_email_signs_in_via_userinfo()
    {
        var id = Guid.NewGuid().ToString("N");
        _userInfo.Next = () => Task.FromResult<UserInfoResult?>(
            new UserInfoResult($"newcomer-{id}@example.org", EmailVerified: true, Name: "Newcomer"));

        using var response = await PostLinkSignInAsync(MintWithoutEmail($"auth0|newcomer-{id}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = (await AuthApi.ReadAsync<LinkSignInView>(response)).PersonId;
        created.Should().NotBeNull();

        using var whoRequest = new HttpRequestMessage(HttpMethod.Get, "/who-am-i");
        whoRequest.Headers.Authorization = new("Bearer", MintWithoutEmail($"auth0|newcomer-{id}"));
        using var who = await _client!.SendAsync(whoRequest, TestContext.Current.CancellationToken);
        (await AuthApi.ReadAsync<WhoAmIView>(who)).PersonId.Should().Be(created);
    }

    [Fact]
    public async Task A_token_email_never_touches_userinfo()
    {
        var id = Guid.NewGuid().ToString("N");
        _userInfo.Next = () => throw new UserInfoLookupException("must never be called");

        using var response = await PostLinkSignInAsync(MintWithEmail($"auth0|present-{id}", $"present-{id}@example.org"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unreachable_IdP_maps_to_502_identityLookupFailed()
    {
        var id = Guid.NewGuid().ToString("N");
        _userInfo.Next = () => throw new UserInfoLookupException("The identity provider could not be reached.");

        using var response = await PostLinkSignInAsync(MintWithoutEmail($"auth0|stranded-{id}"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await AuthApi.ProblemTitleAsync(response)).Should().Be("auth.signIn.identityLookupFailed");
    }

    private sealed record LinkSignInView(PersonId PersonId);

    private sealed record WhoAmIView(bool IsAuthenticated, PersonId? PersonId, IReadOnlyList<PersonRole> Roles, string? Name);

    private static Dictionary<string, string?> CaptureEnvironment(IReadOnlyList<string> keys)
    {
        var captured = new Dictionary<string, string?>();
        foreach (var key in keys)
        {
            captured[key] = Environment.GetEnvironmentVariable(key);
        }

        return captured;
    }

    private static void RestoreEnvironment(Dictionary<string, string?> captured)
    {
        foreach (var (key, value) in captured)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
