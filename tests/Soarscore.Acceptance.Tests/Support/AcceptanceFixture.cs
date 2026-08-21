// kanban/completed/capture-a-score-steel-thread-plan.md WI-13. One PostgreSQL
// container, one WebApplicationFactory<Program> and one direct-to-store
// IServiceProvider for the whole test run — mirroring
// tests/Soarscore.Infrastructure.Tests/PostgresFixture.cs's "one container
// per test class, not per test method" choice (this project has exactly one
// feature/class-equivalent), via Reqnroll's [BeforeTestRun]/[AfterTestRun]
// rather than xunit's IClassFixture, which Reqnroll step-definition classes
// do not participate in directly.
//
// Two service surfaces are built against the SAME connection string:
//   - Client: real HTTP against the real Soarscore.Api, via
//     WebApplicationFactory<Program> — what every Given/When step drives.
//   - EventStore/EntryQuery: a direct AddSoarscoreInfrastructure
//     ServiceProvider, exactly PostgresFixture's own shape — needed only
//     because no GetEntry HTTP query exists yet (EntrySummary.cs's doc
//     comment: "a future work item, mirroring GetCompetition"), so a Then
//     step asserting an Entry's full folded state (a flight, a measurement)
//     has no HTTP surface to read it from and falls
//     back to the same raw-stream-read-and-fold
//     EntryCaptureEventStoreTests.cs's LoadEntryAsync already uses.
//
// Scenarios share this one container and are not isolated from each other by
// database — each creates its own competition with a name/emails unique to
// that scenario (Steps/CapturingAScoreSteps.cs), the same discipline
// EntryCaptureEventStoreTests.cs's several [Fact]s already use against one
// shared PostgresFixture instance.
//
// kanban/completed/multi-backend-deployment.md WI-7: which store backs the
// run is now chosen by the SOARSCORE_TEST_STORE environment variable —
// `postgres` (the default, so an existing invocation behaves exactly as before)
// or `sqlite`. CLAUDE.md's testing approach says the BDD suite is what covers a
// real user-facing workflow end to end, so a support claim for a backend that
// has not run these scenarios is unbacked; CI runs the suite once per store.
//
// This is a per-RUN switch, not a per-class one, and that is forced rather than
// chosen: everything here hangs off Reqnroll's [BeforeTestRun], which fires once
// for the whole assembly. tests/Soarscore.Infrastructure.Tests does better —
// both backends in a single run, via IStoreFixture — because xunit's
// IClassFixture gives it a per-class seam that Reqnroll step definitions do not
// have.

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Soarscore.Application;
using Soarscore.Application.Queries.Entries;
using Soarscore.Infrastructure;
using Testcontainers.PostgreSql;

namespace Soarscore.Acceptance.Tests.Support;

[Binding]
public static class AcceptanceFixture
{
    private static PostgreSqlContainer? _container;
    private static string? _sqlitePath;
    private static WebApplicationFactory<Program>? _factory;
    private static ServiceProvider? _storeProvider;

    public static HttpClient Client { get; private set; } = null!;

    public static IEventStore EventStore { get; private set; } = null!;

    public static IEntryQuery EntryQuery { get; private set; } = null!;

    [BeforeTestRun]
    public static async Task BeforeTestRunAsync()
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
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            connectionString = _container.GetConnectionString();
        }
        else
        {
            // A file, not :memory: — an in-memory SQLite database is scoped to
            // the connection that opened it, and this suite deliberately runs
            // TWO independent stores against one database (see below). The file
            // is what makes them see each other, which is the whole point of
            // the second surface.
            _sqlitePath = Path.Combine(Path.GetTempPath(), $"soarscore-acceptance-{Guid.NewGuid():N}.db");
            connectionString = $"Data Source={_sqlitePath}";
        }

        // Composition.Build(args) runs via top-level statements (Program.cs),
        // not the IHostBuilder/IWebHostBuilder pipeline — WebApplicationFactory
        // hosts it through HostFactoryResolver's deferred-build mechanism, and
        // a ConfigureAppConfiguration callback registered on WithWebHostBuilder
        // does not reliably run before Composition.Build reads
        // builder.Configuration for a WebApplication.CreateBuilder app (verified
        // empirically: the connection string was still missing at that point).
        // The environment variable IS read by WebApplicationBuilder's default
        // configuration sources unconditionally, so it is set before the
        // factory ever builds the host — ASP.NET Core's standard "__" nesting
        // separator for ConnectionStrings:Soarscore. Soarscore__Store reaches
        // AddSoarscoreInfrastructure's own selector the same way, so the hosted
        // Api and the direct provider below agree on which store they are on.
        Environment.SetEnvironmentVariable("ConnectionStrings__Soarscore", connectionString);
        Environment.SetEnvironmentVariable("Soarscore__Store", storeName);

        _factory = new WebApplicationFactory<Program>();
        Client = _factory.CreateClient();

        // Same AddSoarscoreInfrastructure wiring the Infrastructure fixtures
        // use, pointed at the same database — a second, independent connection
        // to the one the Api's own WebApplicationFactory-owned store holds,
        // exactly as two real processes sharing one database would be. On SQLite
        // that is also, incidentally, a live exercise of the concurrent-reader
        // side of LADR-0001 §6's one-writer ceiling: this provider only ever
        // reads, and the Api is the sole writer.
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
        EntryQuery = _storeProvider.GetRequiredService<IEntryQuery>();
    }

    [AfterTestRun]
    public static async Task AfterTestRunAsync()
    {
        Client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_storeProvider is not null)
        {
            await _storeProvider.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        if (_sqlitePath is not null)
        {
            // -wal and -shm are SQLite's write-ahead-log sidecars; deleting the
            // main file alone leaves litter in the temp directory every run.
            foreach (var file in new[] { _sqlitePath, _sqlitePath + "-wal", _sqlitePath + "-shm" })
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }
}
