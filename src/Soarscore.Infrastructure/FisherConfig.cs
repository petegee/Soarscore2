// The Fisher/SQLite composition root — kanban/completed/multi-backend-deployment.md
// WI-1. The deliberate mirror of MartenConfig.cs: same event-type aliases, same
// serialization conventions, same unique index, same four Inline projections
// under the same four names. Read the two files side by side; where they differ,
// the difference is Fisher's API and is commented as such.
//
// Per LADR-0001 §4.1-3 and the story's shape, a composition root is exactly what
// is NOT shared between backends. AddFisher/Fisher.StoreOptions and
// AddMarten/Marten.StoreOptions are different APIs by JasperFx's own design, and
// trying to unify them behind one parameterised builder buys nothing and hides
// which store is being configured.

using JasperFx.Events.Projections;
using Soarscore.Application;
using Soarscore.Application.Queries.People;
using Soarscore.Infrastructure.CompetitionClasses;
using Soarscore.Infrastructure.Competitions;
using Soarscore.Infrastructure.Entries;
using Soarscore.Infrastructure.People;
using Weasel.Core;

namespace Soarscore.Infrastructure;

public static class FisherConfig
{
    public static Fisher.DocumentStore ConfigureDocumentStore(string connectionString)
    {
        var store = Fisher.DocumentStore.For(opts =>
        {
            opts.Connection(connectionString);

            // No AppendMode here, and none needed: Fisher has no Rich/Quick
            // distinction — the version-checked Append(id, expectedVersion, events)
            // overload is simply how appending works, and its guard runs inside the
            // write transaction. MartenConfig.cs's `opts.Events.AppendMode = Rich`
            // has no counterpart because there is no wrong setting to avoid.
            // Confirmed by the WI-6 suite: the same stale-version test that proved
            // Rich mode necessary on Marten passes here unconfigured.

            // The alias table lives in SoarscoreEventTypes.cs — one list, read by
            // every store's composition root, because these aliases are the on-disk
            // event-type names and LADR-0001 §5's store-to-store migration is a
            // replay that reads one store's and writes the other's. What stays here
            // is Fisher's own registration call, which is the one thing that differs:
            // Fisher takes the alias as a settable EventTypeName on the mapping
            // rather than as an argument to Marten's MapEventType(type, alias).
            foreach (var (type, alias) in SoarscoreEventTypes.All)
            {
                opts.Events.AddEventType(type);
                opts.EventGraph.EventMappingFor(type).EventTypeName = alias;
            }

            // Fisher's serialization API is a mutate-in-place callback over its own
            // JsonSerializerOptions instance, where Marten's takes an options object
            // and rebuilds from it. So the conventions are copied onto Fisher's
            // instance rather than handed over wholesale — every one of them, from
            // the single source (SoarscoreEventJson.Options), so the two stores
            // cannot drift. Casing and enum storage are passed as the dedicated
            // parameters for the same reason MartenConfig.cs passes them explicitly.
            opts.ConfigureSerialization(
                EnumStorage.AsString,
                Casing.CamelCase,
                CollectionStorage.Default,
                NonPublicMembersStorage.Default,
                json =>
                {
                    json.PropertyNamingPolicy = SoarscoreEventJson.Options.PropertyNamingPolicy;
                    json.DefaultIgnoreCondition = SoarscoreEventJson.Options.DefaultIgnoreCondition;
                    json.AllowOutOfOrderMetadataProperties = SoarscoreEventJson.Options.AllowOutOfOrderMetadataProperties;
                    foreach (var converter in SoarscoreEventJson.Options.Converters)
                    {
                        json.Converters.Add(converter);
                    }
                });

            // LADR-0001 §2/§3 — the sole cross-stream index, and the invariant the
            // whole Inline-vs-async decision rests on. Same call shape as Marten's.
            opts.Schema.For<PersonSummary>().UniqueIndex(x => x.Email);

            // The per-store shims, with their names pinned for exactly the reason
            // MartenConfig.cs pins them: a projection's registered name is derived
            // from the instance's type, so the shim would otherwise re-register
            // `PersonSummaryProjection` as `FisherPersonSummaryProjection`. That name
            // is the handle RebuildProjectionAsync takes, and it must mean the same
            // thing on both backends.
            opts.Projections.Add(new FisherPersonSummaryProjection(), ProjectionLifecycle.Inline, "PersonSummaryProjection");
            opts.Projections.Add(new FisherClassDefinitionSummaryProjection(), ProjectionLifecycle.Inline, "ClassDefinitionSummaryProjection");
            opts.Projections.Add(new FisherCompetitionSummaryProjection(), ProjectionLifecycle.Inline, "CompetitionSummaryProjection");
            opts.Projections.Add(new FisherEntryIndexProjection(), ProjectionLifecycle.Inline, "EntryIndexProjection");
        });

        // Fisher does not build its schema lazily on first use the way Marten
        // does. `AutoCreateSchemaObjects` defaults to CreateOrUpdate on both, but
        // on Fisher that is the policy applied WHEN a migration runs, not a
        // trigger for running one: against a fresh database the first append
        // fails with "no such table: fi_streams" from inside
        // SaveChangesAsync — found empirically by the WI-6 suite, which failed
        // exactly that way on all four EventStoreTests before this line existed.
        //
        // So the schema is applied here, once, while the store is being built.
        // Blocking on an async call is not something to do lightly, but this is
        // the composition root running before the first request, on a local
        // file, with nothing else contending for the connection — and the
        // alternative (an async initialisation step every caller must remember)
        // would put the failure back in front of the user at their first append
        // rather than at startup. Marten needs no counterpart, which is why this
        // has no MartenConfig.cs twin.
        store.ApplyAllConfiguredChangesToDatabaseAsync().GetAwaiter().GetResult();

        return store;
    }
}
