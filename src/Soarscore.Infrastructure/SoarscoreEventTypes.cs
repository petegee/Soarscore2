// The event-type alias table — one list, read by every store's composition root.
// kanban/completed/multi-backend-deployment.md WI-1.
//
// These aliases are the on-disk event-type names. They must be identical on
// every backend, and not as a matter of tidiness: LADR-0001 §5's store-to-store
// migration is a replay that reads one store's names and writes the other's, so
// a single alias that drifted between MartenConfig.cs and FisherConfig.cs would
// make the two stores mutually unreadable — and would do it silently, at the
// moment someone actually needed to migrate.
//
// The first draft of the second backend copied this list into FisherConfig.cs
// and left a tech-debt note asking for a test that the two agreed. One list read
// twice is strictly better than two lists checked against each other: there is
// nothing left to disagree. What stays per-store is the *registration call* —
// Marten's MapEventType<T>(alias) and Fisher's AddEventType<T>() plus a settable
// EventTypeName are different APIs, and neither store is trying to share them.
//
// A thread that adds a command appending a new event type adds ONE line here.
// The discipline the two configs used to state is unchanged and now lives with
// the list: a type is registered only once a command actually appends it. The
// JSON `$kind` discriminators for every unregistered sibling already exist on
// the event contracts and compile fine either way; only the registry is
// per-command.

using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Infrastructure;

internal static class SoarscoreEventTypes
{
    /// <summary>CLR event type to its on-disk alias, in registration order.</summary>
    public static readonly IReadOnlyList<(Type Type, string Alias)> All =
    [
        // command-side-steel-thread-plan.md WI-7.
        (typeof(PersonRegistered), "personRegistered"),
        (typeof(PersonRenamed), "personRenamed"),
        (typeof(ContactDetailsChanged), "contactDetailsChanged"),
        (typeof(ClubAffiliationChanged), "clubAffiliationChanged"),

        // class-definition-adoption-steel-thread-plan.md WI-5.
        (typeof(ClassDefinitionPublished), "classDefinitionPublished"),
        (typeof(ClassDefinitionRetired), "classDefinitionRetired"),

        // create-competition-steel-thread-plan.md WI-4 registered CompetitionCreated;
        // register-competitor-steel-thread-plan.md WI-5 the next two;
        // phase-drawn-steel-thread-plan.md WI-5 PhaseDrawn;
        // bind-parameter-steel-thread-plan.md WI-5 ParameterBound;
        // task-round-lifecycle.md WI-7 the last four. The remaining three
        // CompetitionEvent subtypes (ReflightGroupAppended, RulesAmended,
        // PenaltyRecorded) are absent because nothing appends them yet.
        (typeof(CompetitionCreated), "competitionCreated"),
        (typeof(CompetitorRegistered), "competitorRegistered"),
        (typeof(CompetitorWithdrawn), "competitorWithdrawn"),
        (typeof(PhaseDrawn), "phaseDrawn"),
        (typeof(ParameterBound), "parameterBound"),
        (typeof(TaskRoundCompleted), "taskRoundCompleted"),
        (typeof(TaskRoundAnnulled), "taskRoundAnnulled"),
        (typeof(TaskRoundReopened), "taskRoundReopened"),
        (typeof(Finalised), "finalised"),

        // capture-a-score-steel-thread-plan.md WI-9 — three of the six EntryEvent
        // subtypes, the narrow capture slice that thread scoped itself to. The other
        // three (MeasurementAmended, EntryAnnulled, PenaltyRecorded) are the
        // corrections-and-rulings thread, deliberately out of scope for capture.
        (typeof(EntryOpened), "entryOpened"),
        (typeof(FlightOpened), "flightOpened"),
        (typeof(MeasurementCaptured), "measurementCaptured"),
    ];
}
