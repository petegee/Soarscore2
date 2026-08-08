// Test fixture for WI-9 (docs/plans/command-side-steel-thread-plan.md): a
// real PostgreSQL container wired up exactly the way WI-7's
// AddSoarscoreInfrastructure does, so these tests exercise the same
// composition as the running Api rather than a hand-assembled DocumentStore.
//
// One container per test class (IClassFixture), not per test method — the
// four tests in MartenEventStoreTests share it and use distinct emails/stream
// ids to stay independent of each other and of test order.

using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Soarscore.Application;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Application.Competitions;
using Soarscore.Application.Entries;
using Soarscore.Application.People;
using Testcontainers.PostgreSql;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private ServiceProvider? _provider;

    public IEventStore EventStore => _provider!.GetRequiredService<IEventStore>();

    public IPeopleQuery PeopleQuery => _provider!.GetRequiredService<IPeopleQuery>();

    public IClassLibraryQuery ClassLibraryQuery => _provider!.GetRequiredService<IClassLibraryQuery>();

    public ICompetitionsQuery CompetitionsQuery => _provider!.GetRequiredService<ICompetitionsQuery>();

    /// <summary>capture-a-score-steel-thread-plan.md WI-12 — the entry_index query port.</summary>
    public IEntryQuery EntryQuery => _provider!.GetRequiredService<IEntryQuery>();

    /// <summary>Exposed only for test 4's read-model drop/rebuild — no port on IEventStore/IPeopleQuery covers it.</summary>
    public IDocumentStore DocumentStore => _provider!.GetRequiredService<IDocumentStore>();

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
        services.AddSoarscoreInfrastructure(configuration);
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
