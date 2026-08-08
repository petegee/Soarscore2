using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Soarscore.Application;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Application.People;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure.CompetitionClasses;
using Soarscore.Infrastructure.Competitions;
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

            // create-competition-steel-thread-plan.md WI-4 registered CompetitionCreated.
            // register-competitor-steel-thread-plan.md WI-5 adds CompetitorRegistered
            // and CompetitorWithdrawn below. phase-drawn-steel-thread-plan.md WI-5
            // adds PhaseDrawn. bind-parameter-steel-thread-plan.md WI-5 adds
            // ParameterBound. The remaining six CompetitionEvent subtypes
            // (ReflightGroupAppended, TaskRoundCompleted, TaskRoundAnnulled,
            // RulesAmended, Finalised, PenaltyRecorded) are still not registered here
            // because nothing appends them yet. Each future thread that adds a
            // command producing one of them must add its own MapEventType line before
            // that command can append — the JSON $kind discriminators for all eleven
            // already exist on CompetitionEvents.cs and compile fine either way; only
            // the registry is per-command.
            opts.Events.MapEventType<CompetitionCreated>("competitionCreated");
            opts.Events.MapEventType<CompetitorRegistered>("competitorRegistered");
            opts.Events.MapEventType<CompetitorWithdrawn>("competitorWithdrawn");
            opts.Events.MapEventType<PhaseDrawn>("phaseDrawn");
            opts.Events.MapEventType<ParameterBound>("parameterBound");

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

            // WI-4 (create-competition-steel-thread-plan.md): no unique index —
            // nothing about CompetitionSummary's fields is unique the way
            // PersonSummary.Email is.
            opts.Projections.Add(new CompetitionSummaryProjection(), ProjectionLifecycle.Inline);
        });
        return store;
    }
}