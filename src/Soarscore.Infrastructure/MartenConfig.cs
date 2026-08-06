using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Soarscore.Application;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Application.People;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure.CompetitionClasses;
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

            opts.Events.MapEventType<PersonRegistered>("personRegistered");
            opts.Events.MapEventType<PersonRenamed>("personRenamed");
            opts.Events.MapEventType<ContactDetailsChanged>("contactDetailsChanged");
            opts.Events.MapEventType<ClubAffiliationChanged>("clubAffiliationChanged");

            opts.Events.MapEventType<ClassDefinitionPublished>("classDefinitionPublished");
            opts.Events.MapEventType<ClassDefinitionRetired>("classDefinitionRetired");

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

            // WI-5 (class-definition-adoption-steel-thread-plan.md): no unique index —
            // the content hash's uniqueness is already the Marten stream key
            // (ExistingStreamIdCollisionException, handled in PublishClassDefinition.cs),
            // not a document-level constraint. Inline for read-your-own-writes, not for
            // an invariant class_library enforces.
            opts.Projections.Add(new ClassDefinitionSummaryProjection(), ProjectionLifecycle.Inline);
        });
        return store;
    }
}