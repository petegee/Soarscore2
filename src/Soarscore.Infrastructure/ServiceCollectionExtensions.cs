// Composition root for Soarscore.Infrastructure —
// kanban/completed/command-side-steel-thread-plan.md WI-7. Wires the Marten/PostgreSQL
// adapter: event-type mapping (LADR-0001 §4.8), the SoarscoreEventJson
// conventions (LADR-0001 §4.5-6), the Inline `people` projection with its
// unique email index (LADR-0001 §2/§3), and Rich append mode (see
// MartenEventStore.cs for why). No async daemon is ever registered or started.

using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Soarscore.Application;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;
using Soarscore.Infrastructure.CompetitionClasses;
using Soarscore.Infrastructure.Competitions;
using Soarscore.Infrastructure.Entries;
using Soarscore.Infrastructure.People;
using Weasel.Core;

namespace Soarscore.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSoarscoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Soarscore")
            ?? configuration["SOARSCORE_CONNECTION_STRING"]
            ?? throw new InvalidOperationException(
                "No Soarscore PostgreSQL connection string configured (ConnectionStrings:Soarscore or SOARSCORE_CONNECTION_STRING).");

        var store = MartenConfig.ConfigureDocumentStore(connectionString);

        services.AddSingleton<IDocumentStore>(store);
        services.AddSingleton<Application.IEventStore, MartenEventStore>();
        services.AddSingleton<IPeopleQuery, MartenPeopleQuery>();
        services.AddSingleton<IClassLibraryQuery, MartenClassLibraryQuery>();
        services.AddSingleton<ICompetitionsQuery, MartenCompetitionsQuery>();
        services.AddSingleton<IEntryQuery, MartenEntryQuery>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
