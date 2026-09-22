// authentication-and-authorisation.md WI-10 — the scenario-scoped state the
// capture-policy and machine-actor features share (the DrawAcceptanceState
// pattern: Reqnroll binds step regexes assembly-wide and instantiates each
// Binding class per scenario with no shared instance state, so the
// competition, entry and capturer ids the shared Givens create travel across
// CapturePolicySteps and IntegrationsSteps through context injection).
//
// PersonIds are resolved by link-sign-in on first need — idempotent (D5), so
// whichever feature asks first, and however the feature scenarios interleave,
// the resolution is the same person.

using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;

namespace Soarscore.Acceptance.Tests.Support;

public sealed class CapturePolicyState
{
    public CompetitionId CompetitionId { get; set; }

    public IReadOnlyList<CompetitorId> Competitors { get; set; } = [];

    public EntryId EntryId { get; set; }

    /// <summary>Persona slug → PersonId, resolved by idempotent link-sign-in.</summary>
    public Dictionary<string, PersonId> PersonIds { get; } = new();

    /// <summary>The machine actor's person, once Pete has bound it (D12).</summary>
    public PersonId? MachinePersonId { get; set; }

    /// <summary>
    /// The most recent raw response, shared across the Binding classes that
    /// serve these features — a When in IntegrationsSteps and its Then in
    /// CapturePolicySteps are the same scenario's state.
    /// </summary>
    public HttpResponseMessage? LastResponse { get; set; }
}
