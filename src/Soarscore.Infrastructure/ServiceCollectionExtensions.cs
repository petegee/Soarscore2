// Composition root for Soarscore.Infrastructure —
// kanban/completed/command-side-steel-thread-plan.md WI-7, extended to a second
// backend by kanban/completed/multi-backend-deployment.md WI-4.
//
// Two roots, one per store, and a selector between them. The selector is
// configuration; the roots are not. AddMarten/Marten.StoreOptions and
// AddFisher/Fisher.StoreOptions are different APIs by JasperFx's own design, and
// MartenConfig.cs / FisherConfig.cs stay separate files that each say plainly
// which store they configure. What IS shared is everything below the store: the
// same IEventStore port, the same four query adapters, the same four projection
// folds, the same event-type aliases and JSON conventions.

using JasperFx.Events.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Soarscore.Application;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.People;
using Soarscore.Infrastructure.CompetitionClasses;
using Soarscore.Infrastructure.Competitions;
using Soarscore.Infrastructure.Entries;
using Soarscore.Infrastructure.People;

namespace Soarscore.Infrastructure;

/// <summary>The event stores Soarscore can be composed against.</summary>
public enum SoarscoreStore
{
    /// <summary>Marten on PostgreSQL — LADR-0001's original and default choice.</summary>
    Postgres,

    /// <summary>
    /// Fisher on SQLite — an in-process file, no server, backup is <c>cp</c>.
    /// The club-secretary's-laptop deployment LADR-0001 §6 kept possible.
    /// </summary>
    Sqlite,
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires whichever store <c>Soarscore:Store</c> selects — <c>postgres</c>
    /// (the default, so nothing about an existing deployment changes) or
    /// <c>sqlite</c>. Both read their connection string from the same
    /// <c>ConnectionStrings:Soarscore</c> / <c>SOARSCORE_CONNECTION_STRING</c>
    /// pair; for SQLite that is an ordinary Microsoft.Data.Sqlite connection
    /// string, e.g. <c>Data Source=/home/secretary/soarscore.db</c>.
    /// </summary>
    public static IServiceCollection AddSoarscoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var store = configuration["Soarscore:Store"] ?? configuration["SOARSCORE_STORE"];
        var selected = store switch
        {
            null or "" => SoarscoreStore.Postgres,
            _ when store.Equals("postgres", StringComparison.OrdinalIgnoreCase) => SoarscoreStore.Postgres,
            _ when store.Equals("sqlite", StringComparison.OrdinalIgnoreCase) => SoarscoreStore.Sqlite,
            _ => throw new InvalidOperationException(
                $"Unknown Soarscore store '{store}'. Valid values are 'postgres' and 'sqlite'."),
        };

        return services.AddSoarscoreInfrastructure(configuration, selected);
    }

    /// <summary>
    /// The same wiring with the store chosen in code rather than read from
    /// configuration — what the store-backed test fixtures use, so that one test
    /// run can exercise both backends without one global setting deciding for
    /// the whole process.
    /// </summary>
    public static IServiceCollection AddSoarscoreInfrastructure(
        this IServiceCollection services, IConfiguration configuration, SoarscoreStore store)
    {
        var connectionString = configuration.GetConnectionString("Soarscore")
            ?? configuration["SOARSCORE_CONNECTION_STRING"]
            ?? throw new InvalidOperationException(
                "No Soarscore connection string configured (ConnectionStrings:Soarscore or SOARSCORE_CONNECTION_STRING).");

        // The one store-specific pair of lines in this method. Each concrete
        // store registers itself twice: once under its own type, for the few
        // places that legitimately need it (a read-model rebuild has no port),
        // and once under JasperFx's store-agnostic IDocumentSessionFactory,
        // which is what every adapter below is actually written against. Not a
        // wrapper and not a second store — the same singleton instance under two
        // contracts. Choosing which concrete store fills that slot is exactly
        // the per-backend decision a composition root exists to make.
        switch (store)
        {
            case SoarscoreStore.Postgres:
                var marten = MartenConfig.ConfigureDocumentStore(connectionString);
                services.AddSingleton<Marten.IDocumentStore>(marten);
                services.AddSingleton<IDocumentSessionFactory>(marten);
                services.AddSingleton<IEventStore, MartenEventStore>();
                break;

            case SoarscoreStore.Sqlite:
                var fisher = FisherConfig.ConfigureDocumentStore(connectionString);
                services.AddSingleton<Fisher.IDocumentStore>(fisher);
                services.AddSingleton<IDocumentSessionFactory>(fisher);
                services.AddSingleton<IEventStore, FisherEventStore>();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(store), store, "Unknown store.");
        }

        // Everything below here is store-agnostic and registered identically for
        // every backend — the four query adapters read the JasperFx contracts
        // only (kanban/completed/jasperfx-shared-store-contracts.md WI-2).
        services.AddSingleton<IPeopleQuery, DocumentPeopleQuery>();
        services.AddSingleton<IClassLibraryQuery, DocumentClassLibraryQuery>();
        services.AddSingleton<ICompetitionsQuery, DocumentCompetitionsQuery>();
        services.AddSingleton<IEntryQuery, DocumentEntryQuery>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
