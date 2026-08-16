// docs/plans/capture-a-score-steel-thread-plan.md WI-13. One PostgreSQL
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
//     step asserting an Entry's full folded state (a flight, a measurement,
//     a null TimeWindow.End) has no HTTP surface to read it from and falls
//     back to the same raw-stream-read-and-fold
//     EntryCaptureEventStoreTests.cs's LoadEntryAsync already uses.
//
// Scenarios share this one container and are not isolated from each other by
// database — each creates its own competition with a name/emails unique to
// that scenario (Steps/CapturingAScoreSteps.cs), the same discipline
// EntryCaptureEventStoreTests.cs's several [Fact]s already use against one
// shared PostgresFixture instance.

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
    private static WebApplicationFactory<Program>? _factory;
    private static ServiceProvider? _storeProvider;

    public static HttpClient Client { get; private set; } = null!;

    public static IEventStore EventStore { get; private set; } = null!;

    public static IEntryQuery EntryQuery { get; private set; } = null!;

    [BeforeTestRun]
    public static async Task BeforeTestRunAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await _container.StartAsync();

        var connectionString = _container.GetConnectionString();

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
        // separator for ConnectionStrings:Soarscore.
        Environment.SetEnvironmentVariable("ConnectionStrings__Soarscore", connectionString);

        _factory = new WebApplicationFactory<Program>();
        Client = _factory.CreateClient();

        // Same AddSoarscoreInfrastructure wiring PostgresFixture.cs uses,
        // pointed at the same container — a second, independent connection to
        // the one the Api's own WebApplicationFactory-owned DocumentStore
        // holds, exactly as two real processes sharing one database would be.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Soarscore"] = connectionString,
            })
            .Build();
        services.AddSoarscoreInfrastructure(configuration);
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
    }
}
