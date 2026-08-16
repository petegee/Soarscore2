using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Soarscore.Application;
using Soarscore.Application.Queries.People;
using Soarscore.Infrastructure.CompetitionClasses;
using Soarscore.Infrastructure.Competitions;
using Soarscore.Infrastructure.Entries;
using Soarscore.Infrastructure.People;
using Weasel.Core;

namespace Soarscore.Infrastructure;

public static class MartenConfig
{
    public static DocumentStore ConfigureDocumentStore(string connectionString)
    {
        var store = Marten.DocumentStore.For(opts =>
        {
            opts.Connection(connectionString);

            // LADR-0001 §4.4/§4.8, MartenEventStore.cs: Rich mode is required for the
            // version-checked Append overload; Quick (the Marten 9 default) does not
            // support it.
            opts.Events.AppendMode = EventAppendMode.Rich;

            // The alias table lives in SoarscoreEventTypes.cs — one list, read by
            // every store's composition root, because these aliases are the on-disk
            // event-type names and LADR-0001 §5's store-to-store migration is a
            // replay that reads one store's and writes the other's. What stays here
            // is Marten's own registration call.
            foreach (var (type, alias) in SoarscoreEventTypes.All)
            {
                opts.Events.MapEventType(type, alias);
            }

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

            // jasperfx-shared-store-contracts.md WI-5: what gets registered here is
            // the per-store shim (Marten*Projection), not the store-agnostic fold it
            // derives from — Marten's registration API wants its own IProjection
            // marker, which is the one thing JasperFx does not share.
            //
            // The third argument matters and is not decoration. Marten derives a
            // projection's registered name from the instance's type name, so the shim
            // would silently re-register `PersonSummaryProjection` as
            // `MartenPersonSummaryProjection`. That name is the async-daemon
            // progression key and the handle RebuildProjectionAsync takes, so letting
            // it drift would rename existing progression rows and break every
            // rebuild-by-name call site. Pinning it keeps the name a property of the
            // read model rather than of whichever store-specific shim is wiring it.
            opts.Projections.Add(new MartenPersonSummaryProjection(), ProjectionLifecycle.Inline, "PersonSummaryProjection");

            // WI-5 (class-definition-adoption-steel-thread-plan.md): no unique index —
            // the content hash's uniqueness is already the Marten stream key
            // (ExistingStreamIdCollisionException, handled in PublishClassDefinition.cs),
            // not a document-level constraint. Inline for read-your-own-writes, not for
            // an invariant class_library enforces.
            opts.Projections.Add(new MartenClassDefinitionSummaryProjection(), ProjectionLifecycle.Inline, "ClassDefinitionSummaryProjection");

            // WI-4 (create-competition-steel-thread-plan.md): no unique index —
            // nothing about CompetitionSummary's fields is unique the way
            // PersonSummary.Email is.
            opts.Projections.Add(new MartenCompetitionSummaryProjection(), ProjectionLifecycle.Inline, "CompetitionSummaryProjection");

            // WI-9 (capture-a-score-steel-thread-plan.md): no unique index — two
            // Entries can legitimately share a (task-round, competitor) pair once
            // reflights exist (EntrySummary.cs), so nothing here is unique the way
            // PersonSummary.Email is. Inline for read-your-own-writes on the
            // openEntry.alreadyOpen check (WI-8), not for an invariant entry_index
            // itself enforces.
            opts.Projections.Add(new MartenEntryIndexProjection(), ProjectionLifecycle.Inline, "EntryIndexProjection");
        });
        return store;
    }
}