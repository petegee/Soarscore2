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
        // task-round-lifecycle.md WI-7 the next four;
        // annul-and-penalise-the-second-entry-thread.md WI-9 the Competition-scoped
        // PenaltyRecorded (alias "competitionPenaltyRecorded" — decision 5: it is
        // a different CLR type from the Entry-scoped PenaltyRecorded and cannot
        // share "penaltyRecorded" as an on-disk identity).
        // reflight-groups.md WI-4 registered ReflightGroupAppended — the only
        // CompetitionEvent now still absent is RulesAmended, because nothing
        // appends it yet.
        (typeof(CompetitionCreated), "competitionCreated"),
        (typeof(CompetitorRegistered), "competitorRegistered"),
        (typeof(CompetitorWithdrawn), "competitorWithdrawn"),
        (typeof(PhaseDrawn), "phaseDrawn"),
        (typeof(ParameterBound), "parameterBound"),
        (typeof(TaskRoundCompleted), "taskRoundCompleted"),
        (typeof(TaskRoundAnnulled), "taskRoundAnnulled"),
        (typeof(TaskRoundReopened), "taskRoundReopened"),
        (typeof(Finalised), "finalised"),
        (typeof(Soarscore.Domain.Competitions.PenaltyRecorded), "competitionPenaltyRecorded"),
        (typeof(ReflightGroupAppended), "reflightGroupAppended"),
        (typeof(ReflightRulingRecorded), "reflightRulingRecorded"), // reflight-scoring-rulings.md WI-5 — missing this line fails at runtime on BOTH backends per LADR-0001 §4.8.
        // draw-acceptance-redraw.md WI-5 registered the draw-lifecycle pair;
        // as above, a missing line fails at runtime on both backends per
        // LADR-0001 §4.8. RulesAmended remains the only registered-nothing sibling.
        (typeof(DrawAccepted), "drawAccepted"),
        (typeof(DrawRejected), "drawRejected"),

        // capture-a-score-steel-thread-plan.md WI-9 registered the narrow capture
        // slice: EntryOpened, FlightOpened, MeasurementCaptured.
        // amend-a-measurement.md WI-5 registered MeasurementAmended (the event a
        // correction appends). annul-and-penalise-the-second-entry-thread.md WI-9
        // registered EntryAnnulled and the Entry-scoped PenaltyRecorded (alias
        // "entryPenaltyRecorded" — see the Competition block's note above).
        (typeof(EntryOpened), "entryOpened"),
        (typeof(FlightOpened), "flightOpened"),
        (typeof(MeasurementCaptured), "measurementCaptured"),
        (typeof(MeasurementAmended), "measurementAmended"),
        (typeof(EntryAnnulled), "entryAnnulled"),
        (typeof(Soarscore.Domain.Entries.PenaltyRecorded), "entryPenaltyRecorded"),
    ];
}
