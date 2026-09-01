// kanban/in-progress/lane-assignment.md WI-8. The scenario-scoped state that
// lets Features/AssigningSpots.feature reuse the shared draw-acceptance Givens
// verbatim ("a published F5J rulebook for draw acceptance", "a
// draw-acceptance competition with N registered competitors", the drawn-and-
// accepted / drawn-for-review variants) instead of growing a fifth private
// copy of that composition — the story's finding 8: "the new feature's Givens
// compose with them unchanged".
//
// Reqnroll binds step regexes assembly-wide and instantiates each Binding
// class per scenario with no shared instance state (the reason every Steps
// class here rewords its neighbours' phrasing), so the competition and
// competitor list the shared Givens create travel across the two Binding
// classes through context injection — the documented "Sharing Data between
// Bindings" mechanism: one POCO per scenario, resolved lazily into the
// scenario container and therefore the SAME instance inside
// AcceptingTheDrawSteps (which writes it in
// GivenADrawAcceptanceCompetitionWithRegisteredCompetitors) and
// AssigningSpotsSteps (which reads it). Nothing else may write it.
//
// Competitors is the live list reference, not a copy: the latecomer step
// appends to it, so any consumer always sees the field as it now stands.

using Soarscore.Domain.Competitions;

namespace Soarscore.Acceptance.Tests.Support;

public sealed class DrawAcceptanceState
{
    public CompetitionId CompetitionId { get; set; } = default!;

    public IReadOnlyList<CompetitorId> Competitors { get; set; } = [];
}
