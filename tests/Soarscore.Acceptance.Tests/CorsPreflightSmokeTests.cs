// kanban/completed/cors-for-ndcscore-spa.md WI-3. CORS smoke tests: the
// NdcScore companion SPA (~/Source/NdcScore) is a pure web client of this API —
// no BFF — so a misconfigured CORS policy is a silent, browser-side failure
// every organiser would hit at home with nothing in any server log. These
// facts pin the contract end to end over real HTTP, each against its own
// standalone SQLite factory (no Docker, no shared AcceptanceFixture state —
// CORS is a per-deployment configuration, and these tests are exactly that
// configuration's behaviour):
//   1. preflight from a configured origin → 204 with the origin echoed;
//   2. a simple GET from a configured origin carries Access-Control-Allow-Origin;
//   3. no origins configured → no CORS headers even with an Origin header
//      (default-off, byte-identical to the pre-CORS behaviour);
//   4. the SOARSCORE_CORS_ORIGINS flat env alias resolves, comma-separated.

using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Soarscore.Acceptance.Tests;

public sealed class CorsPreflightSmokeTests
{
    private const string SpaOrigin = "http://localhost:5173";

    [Fact]
    public async Task Preflight_from_a_configured_origin_is_allowed_and_echoes_the_origin()
    {
        var (factory, dbPath) = Factory(("Soarscore:Cors:Origins:0", SpaOrigin));
        try
        {
            var client = factory.CreateClient();

            var response = await client.SendAsync(Preflight("/class-definitions"), TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle()
                .Which.Should().Be(SpaOrigin);
        }
        finally
        {
            factory.Dispose();
            DeleteDatabase(dbPath);
        }
    }

    [Fact]
    public async Task A_simple_get_from_a_configured_origin_carries_the_allow_origin_header()
    {
        var (factory, dbPath) = Factory(("Soarscore:Cors:Origins:0", SpaOrigin));
        try
        {
            var client = factory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Get, "/class-definitions");
            request.Headers.Add("Origin", SpaOrigin);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle()
                .Which.Should().Be(SpaOrigin);
        }
        finally
        {
            factory.Dispose();
            DeleteDatabase(dbPath);
        }
    }

    [Fact]
    public async Task With_no_origins_configured_no_cors_headers_are_added()
    {
        var (factory, dbPath) = Factory();
        try
        {
            var client = factory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Get, "/class-definitions");
            request.Headers.Add("Origin", SpaOrigin);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        }
        finally
        {
            factory.Dispose();
            DeleteDatabase(dbPath);
        }
    }

    [Fact]
    public async Task The_SOARSCORE_CORS_ORIGINS_alias_is_honoured_comma_separated()
    {
        var otherOrigin = "https://ndcscore.example";
        var (factory, dbPath) = Factory(("SOARSCORE_CORS_ORIGINS", $"{SpaOrigin},{otherOrigin}"));
        try
        {
            var client = factory.CreateClient();

            var allowed = await client.SendAsync(Preflight("/class-definitions", otherOrigin), TestContext.Current.CancellationToken);
            var disallowed = await client.SendAsync(Preflight("/class-definitions", "https://stranger.example"), TestContext.Current.CancellationToken);

            allowed.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle()
                .Which.Should().Be(otherOrigin);
            disallowed.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        }
        finally
        {
            factory.Dispose();
            DeleteDatabase(dbPath);
        }
    }

    private static HttpRequestMessage Preflight(string path, string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin ?? SpaOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }

    /// <summary>
    /// A standalone factory per fact, on its own throwaway SQLite file —
    /// explicit store settings rather than SOARSCORE_TEST_STORE, because these
    /// tests are about composition configuration, not about which store the
    /// scenario suite chose for this run. The seed-corpus hosted service runs
    /// here too and costs a moment per factory; that is the price of testing
    /// the real Build, not a mock of it. The caller owns both the factory and
    /// the file: dispose the factory, then DeleteDatabase.
    /// </summary>
    private static (WebApplicationFactory<Program> Factory, string DbPath) Factory(
        params (string Key, string Value)[] settings)
    {
        var dbPath = Path.Combine(
            Path.GetTempPath(), $"soarscore-cors-{Guid.NewGuid():N}.db");
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Soarscore:Store", "sqlite");
            builder.UseSetting("ConnectionStrings:Soarscore", $"Data Source={dbPath}");
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });
        return (factory, dbPath);
    }

    private static void DeleteDatabase(string dbPath)
    {
        try
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
        catch (IOException)
        {
            // best effort — a leftover temp file is not a test failure
        }
    }
}