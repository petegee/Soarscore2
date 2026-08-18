// The `competitions` read model's fold — kanban/completed/create-competition-steel-thread-plan.md
// WI-1, LADR-0001 §4.3. Plain static function, portable if the store is ever
// swapped; the Marten IProjection shim wrapping it is Infrastructure's
// concern (CompetitionSummaryProjection.cs).

using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Queries.Competitions;

public static class CompetitionProjection
{
    /// <summary>
    /// Folds one <see cref="CompetitionEvent"/> onto the current summary, or
    /// creates it from <see cref="CompetitionCreated"/>, or advances its
    /// <see cref="CompetitionSummary.State"/>.
    ///
    /// Only competition-scope <see cref="Finalised"/> moves the state:
    /// phase-scope finalisation freezes one phase and names who was promoted,
    /// which is not the competition being over.
    ///
    /// The default arm is deliberately <c>_ =&gt; current</c> (pass-through),
    /// not <c>throw</c>, unlike PeopleProjection's and ClassDefinitionProjection's
    /// exhaustive switches: a real deployment's event log will start
    /// accumulating those other ten event types as soon as their commands
    /// land, and each will land on its own thread, independently of this one.
    /// An Inline projection that throws on an event type it doesn't yet
    /// recognise would crash for every competition that later gets one
    /// appended to its stream — so unrecognised-yet events must be tolerated
    /// and simply leave the summary unchanged.
    /// </summary>
    public static CompetitionSummary? Apply(CompetitionSummary? current, CompetitionEvent @event) =>
        @event switch
        {
            CompetitionCreated e => new CompetitionSummary(
                e.Id, e.Name, e.Location, e.StartDate, e.EndDate,
                e.AdoptedRules.Definition.Name, e.AdoptedRules.SourceClassId, "created"),
            PhaseDrawn when current is not null => current with { State = "drawn" },
            Finalised e when current is not null && e.Finalisation.Scope == FinalisationScope.Competition =>
                current with { State = "finalised" },
            _ => current,
        };
}
