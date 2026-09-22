// authentication-and-authorisation.md WI-10 — the auth-enabled acceptance
// fixture (D11): a second WebApplicationFactory<Program> whose host runs in
// Soarscore:Auth:Mode=mock, against its OWN store, next to the none-mode
// factory AcceptanceFixture owns. Existing scenarios stay on the none-mode
// instance untouched — zero churn — while the four auth features drive the
// shipped enforcement path: same JwtBearer validation, same pipeline, same
// policy table, only the issuer/signing key local (D11's point — the suite
// proves what ships).
//
// The mock config travels the only seam that reliably reaches
// Composition.Build for a WebApplication-hosted WebApplication.CreateBuilder
// app: environment variables, read by the default configuration sources when
// the deferred host build fires (AcceptanceFixture.cs's header records the
// empirical finding; this fixture sets the mock block the same way, builds
// the host by creating the client, then RESTORES every variable it touched —
// the none-mode host has long since built, and the direct-store provider
// below reads an explicit ConfigurationBuilder, so restoring is safe).
//
// The persona seeder runs at the mock host's startup, seeding each persona
// through real commands into the real store — that is the point (D11): the
// suite exercises the real grant → resolve → enforce chain. Lazy initialisa-
// tion mirrors AcceptanceFixture.EnsureInitializedAsync: whichever auth step
// runs first builds the factory, gated by a semaphore; plain facts and later
// steps reuse it.

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Soarscore.Application;
using Soarscore.Infrastructure;
using Testcontainers.PostgreSql;

namespace Soarscore.Acceptance.Tests.Support;

[Binding]
public static class AuthAcceptanceFixture
{
    private static PostgreSqlContainer? _container;
    private static string? _sqlitePath;
    private static WebApplicationFactory<Program>? _factory;
    private static ServiceProvider? _storeProvider;

    public static HttpClient Client { get; private set; } = null!;

    public static IEventStore EventStore { get; private set; } = null!;

    private static readonly SemaphoreSlim InitGate = new(1, 1);

    public static async Task EnsureInitializedAsync()
    {
        if (Client is not null)
        {
            return;
        }

        await InitGate.WaitAsync();

        try
        {
            if (Client is null)
            {
                await InitialiseAsync();
            }
        }
        finally
        {
            InitGate.Release();
        }
    }

    private static async Task InitialiseAsync()
    {
        var storeName = Environment.GetEnvironmentVariable("SOARSCORE_TEST_STORE") ?? "postgres";
        var store = storeName.ToLowerInvariant() switch
        {
            "postgres" => SoarscoreStore.Postgres,
            "sqlite" => SoarscoreStore.Sqlite,
            _ => throw new InvalidOperationException(
                $"Unknown SOARSCORE_TEST_STORE '{storeName}'. Valid values are 'postgres' and 'sqlite'."),
        };

        string connectionString;
        if (store == SoarscoreStore.Postgres)
        {
            // A second container, not a second database on AcceptanceFixture's:
            // that container is private to its class and its lifetime, and the
            // auth store must outlive no scenario the none-mode store does not.
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            connectionString = _container.GetConnectionString();
        }
        else
        {
            // A file, not :memory: — same reasoning as AcceptanceFixture: the
            // Api host and the direct read-side provider below must see the
            // same data, and an in-memory SQLite database is scoped to the
            // connection that opened it.
            _sqlitePath = Path.Combine(Path.GetTempPath(), $"soarscore-acceptance-auth-{Guid.NewGuid():N}.db");
            connectionString = $"Data Source={_sqlitePath}";
        }

        var touched = CaptureEnvironment(
        [
            "ConnectionStrings__Soarscore",
            "Soarscore__Store",
            "Soarscore__SeedCorpusDirectory",
            "Soarscore__Auth__Mode",
            "Soarscore__Auth__BootstrapOrganisers__0",
            "Soarscore__Auth__Mock__Audience",
            "Soarscore__Auth__Mock__SigningKey",
            "Soarscore__Auth__Mock__Personas__0__Name",
            "Soarscore__Auth__Mock__Personas__0__Email",
            "Soarscore__Auth__Mock__Personas__0__Roles__0",
            "Soarscore__Auth__Mock__Personas__1__Name",
            "Soarscore__Auth__Mock__Personas__1__Email",
            "Soarscore__Auth__Mock__Personas__1__Roles__0",
            "Soarscore__Auth__Mock__Personas__2__Name",
            "Soarscore__Auth__Mock__Personas__2__Email",
            "Soarscore__Auth__Mock__Personas__3__Name",
            "Soarscore__Auth__Mock__Personas__3__Email",
        ]);

        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Soarscore", connectionString);
            Environment.SetEnvironmentVariable("Soarscore__Store", storeName);
            Environment.SetEnvironmentVariable("Soarscore__SeedCorpusDirectory", FindSeedJsonDirectory());

            // D11's config shape, exercised for real: mode mock, a local
            // audience, the static signing key, and the persona set. Pete is
            // the working organiser; Tama the working competitor; FieldRig a
            // roleless person (and the email-match path); Nova is
            // bootstrap-listed with no seeded role — her first sign-in must
            // land her the Organiser role (D3), and re-signing re-grants it
            // idempotently, which keeps the Roles.feature scenarios
            // order-independent. AuthActors.Newcomer is deliberately NOT a
            // seeded persona: SigningIn.feature's creation scenarios need a
            // caller no seeder has ever turned into a person.
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mode", "mock");
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Audience", TestJwt.Audience);
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__SigningKey", TestJwt.SigningKeyBase64);
            Environment.SetEnvironmentVariable("Soarscore__Auth__BootstrapOrganisers__0", AuthActors.Bootstrap.Email);
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Personas__0__Name", "Pete");
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Personas__0__Email", AuthActors.Organiser.Email);
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Personas__0__Roles__0", "Organiser");
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Personas__1__Name", "Tama");
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Personas__1__Email", AuthActors.Competitor.Email);
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Personas__1__Roles__0", "Competitor");
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Personas__2__Name", "FieldRig");
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Personas__2__Email", AuthActors.Unlinked.Email);
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Personas__3__Name", "Nova");
            Environment.SetEnvironmentVariable("Soarscore__Auth__Mock__Personas__3__Email", AuthActors.Bootstrap.Email);

            _factory = new WebApplicationFactory<Program>();
            Client = _factory.CreateClient();
        }
        finally
        {
            RestoreEnvironment(touched);
        }

        // The same read-side surface AcceptanceFixture builds: a second,
        // independent connection to the one database the Api owns, so Then
        // steps can fold an entry's stream (no GetEntry query exists yet —
        // EntryReader.cs's header). Reads only; the Api is the sole writer.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Soarscore"] = connectionString,
            })
            .Build();
        services.AddSoarscoreInfrastructure(configuration, store);
        _storeProvider = services.BuildServiceProvider();

        EventStore = _storeProvider.GetRequiredService<IEventStore>();
    }

    [AfterTestRun]
    public static async Task AfterTestRunAsync()
    {
        Client?.Dispose();
        Client = null!;

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }

        if (_storeProvider is not null)
        {
            await _storeProvider.DisposeAsync();
            _storeProvider = null;
        }

        EventStore = null!;

        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
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

    /// <summary>The repo's frozen canonical corpus — AcceptanceFixture's copy, same mechanism.</summary>
    private static string FindSeedJsonDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !IsRepositoryRoot(directory.FullName))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not find the repository root from the test's base directory.")
            : Path.Combine(directory.FullName, "tools", "Soarscore.SeedData", "json");
    }

    private static bool IsRepositoryRoot(string path) =>
        Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git"));
}
