// The Fisher/SQLite fixture — kanban/completed/multi-backend-deployment.md
// WI-6. The exact counterpart of PostgresFixture, and deliberately the same
// shape: the same AddSoarscoreInfrastructure call with a different
// SoarscoreStore, so what the tests exercise is the composition the running Api
// would use, not a hand-assembled store.
//
// What it does NOT need is the point. No Docker, no container start, no image
// pull, no port wait — one file in the OS temp directory, created on
// InitializeAsync and deleted on dispose. That is why the subclasses that use
// this fixture carry no Trait("Category", "Storage"): the trait exists so a fast
// local loop can filter out tests that need a container, and these do not. It is
// the "second, nearer prize" the story's Why names — the whole store-backed
// suite becomes part of `dotnet test` on a machine with no Docker at all.
//
// One file per test class, not per test method, mirroring PostgresFixture's one
// container per class. Each class's tests still use distinct emails and stream
// ids to stay independent of each other and of test order — the isolation
// discipline is the fixture's lifetime, and it is the same on both backends.
//
// A unique file name per fixture instance matters more here than the equivalent
// does on Postgres: SQLite's one-writer-per-file ceiling (LADR-0001 §6, and the
// story's "Before starting") is a real constraint, and xunit runs test classes
// in parallel by default. Sharing one file across classes would be testing
// SQLite's lock contention rather than Soarscore.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Soarscore.Application;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.People;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public sealed class SqliteFixture : IStoreFixture, IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"soarscore-tests-{Guid.NewGuid():N}.db");
    private ServiceProvider? _provider;

    public IEventStore EventStore => _provider!.GetRequiredService<IEventStore>();

    public IPeopleQuery PeopleQuery => _provider!.GetRequiredService<IPeopleQuery>();

    public IClassLibraryQuery ClassLibraryQuery => _provider!.GetRequiredService<IClassLibraryQuery>();

    public ICompetitionsQuery CompetitionsQuery => _provider!.GetRequiredService<ICompetitionsQuery>();

    public IEntryQuery EntryQuery => _provider!.GetRequiredService<IEntryQuery>();

    private Fisher.IDocumentStore DocumentStore => _provider!.GetRequiredService<Fisher.IDocumentStore>();

    /// <summary>Fisher's <c>CleanAsync&lt;T&gt;</c> — the counterpart of Marten's
    /// <c>DeleteDocumentsByTypeAsync</c>, and the only place the two fixtures'
    /// admin APIs differ in more than a type name.</summary>
    public Task DropDocumentsAsync<TDocument>(CancellationToken cancellationToken)
        where TDocument : notnull =>
        DocumentStore.Advanced.Clean.CleanAsync<TDocument>(cancellationToken);

    public async Task RebuildProjectionAsync(string projectionName, CancellationToken cancellationToken)
    {
        using var daemon = await DocumentStore.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync(projectionName, cancellationToken);
    }

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Soarscore"] = $"Data Source={_path}",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSoarscoreInfrastructure(configuration, SoarscoreStore.Sqlite);
        _provider = services.BuildServiceProvider();

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        // -wal and -shm are SQLite's write-ahead-log sidecars; deleting the main
        // file without them leaves litter in the temp directory on every run.
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
