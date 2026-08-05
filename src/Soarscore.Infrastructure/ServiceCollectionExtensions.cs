// Composition root for Soarscore.Infrastructure —
// docs/plans/command-side-steel-thread-plan.md WI-7. Wires the Marten/PostgreSQL
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
using Soarscore.Application.People;
using Soarscore.Domain.People;
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

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(connectionString);

            // LADR-0001 §4.4/§4.8, MartenEventStore.cs: Rich mode is required for the
            // version-checked Append overload; Quick (the Marten 9 default) does not
            // support it.
            opts.Events.AppendMode = EventAppendMode.Rich;

            opts.Events.MapEventType<PersonRegistered>("personRegistered");
            opts.Events.MapEventType<PersonRenamed>("personRenamed");
            opts.Events.MapEventType<ContactDetailsChanged>("contactDetailsChanged");
            opts.Events.MapEventType<ClubAffiliationChanged>("clubAffiliationChanged");

            // Casing and enum storage must be passed explicitly here even though
            // SoarscoreEventJson.Options already sets them: Marten's overload rebuilds
            // its own options from these two parameters and silently discards the
            // PropertyNamingPolicy already on the options instance if they're omitted
            // (verified empirically — the default resets every event payload to
            // PascalCase).
            opts.UseSystemTextJsonForSerialization(
                SoarscoreEventJson.Options,
                enumStorage: EnumStorage.AsString,
                casing: Casing.CamelCase);

            // LADR-0001 §2/§3: the sole cross-stream index this thread builds, and the
            // one invariant the whole Inline-vs-async decision rests on.
            opts.Schema.For<PersonSummary>().UniqueIndex(x => x.Email);

            opts.Projections.Add(new PersonSummaryProjection(), ProjectionLifecycle.Inline);
        });

        services.AddSingleton<IDocumentStore>(store);
        services.AddSingleton<Soarscore.Application.IEventStore, MartenEventStore>();
        services.AddSingleton<IPeopleQuery, MartenPeopleQuery>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
