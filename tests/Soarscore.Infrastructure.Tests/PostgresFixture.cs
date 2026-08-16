// The Marten/PostgreSQL fixture — WI-9 (kanban/completed/command-side-steel-thread-plan.md),
// generalised to IStoreFixture by kanban/completed/multi-backend-deployment.md
// WI-6. A real PostgreSQL container wired up exactly the way
// AddSoarscoreInfrastructure does, so these tests exercise the same composition
// as the running Api rather than a hand-assembled DocumentStore.
//
// One container per test class (IClassFixture), not per test method — the tests
// sharing it use distinct emails/stream ids to stay independent of each other
// and of test order.
//
// The store is selected in code (SoarscoreStore.Postgres) rather than through
// configuration, so that one `dotnet test` run can host this fixture and
// SqliteFixture side by side without a single process-wide setting deciding for
// both.

using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Soarscore.Application;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.People;
using Testcontainers.PostgreSql;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public sealed class PostgresFixture : IStoreFixture, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private ServiceProvider? _provider;

    public IEventStore EventStore => _provider!.GetRequiredService<IEventStore>();

    public IPeopleQuery PeopleQuery => _provider!.GetRequiredService<IPeopleQuery>();

    public IClassLibraryQuery ClassLibraryQuery => _provider!.GetRequiredService<IClassLibraryQuery>();

    public ICompetitionsQuery CompetitionsQuery => _provider!.GetRequiredService<ICompetitionsQuery>();

    /// <summary>capture-a-score-steel-thread-plan.md WI-12 — the entry_index query port.</summary>
    public IEntryQuery EntryQuery => _provider!.GetRequiredService<IEntryQuery>();

    private IDocumentStore DocumentStore => _provider!.GetRequiredService<IDocumentStore>();

    public Task DropDocumentsAsync<TDocument>(CancellationToken cancellationToken)
        where TDocument : notnull =>
        DocumentStore.Advanced.Clean.DeleteDocumentsByTypeAsync(typeof(TDocument), cancellationToken);

    public async Task RebuildProjectionAsync(string projectionName, CancellationToken cancellationToken)
    {
        using var daemon = await DocumentStore.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync(projectionName, cancellationToken);
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Soarscore"] = _container.GetConnectionString(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSoarscoreInfrastructure(configuration, SoarscoreStore.Postgres);
        _provider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}
