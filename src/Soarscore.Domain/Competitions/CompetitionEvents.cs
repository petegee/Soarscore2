// Event contracts for the Competition aggregate — docs/aggregate-roots.md §3.
//
// The setup and shape of one event: its field, its rulebook copy, and its
// phase/round/task-round/group schedule. Created up front (CompetitionCreated
// + PhaseDrawn) and only lightly mutated afterwards — register or withdraw a
// competitor, accept or reject the draw, append a reflight group, complete /
// annul / reopen a task-round, amend the rules, bind a parameter, finalise,
// record a penalty, record a reflight ruling. Fifteen events total, mirroring
// aggregate-roots.md §3's mutation list one-for-one.
//
// Event payloads reuse Domain's own value-object records (AdoptedRules,
// RulesAmendment, ParameterBinding, Finalisation, Competitor, Group, Round,
// Penalty, ReflightRuling) directly rather than redefining their shapes —
// those types are already immutable value objects with no Marten dependency,
// so wrapping them again here would just be duplication (LADR-0001 §4).

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Competitions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(CompetitionCreated), "competitionCreated")]
[JsonDerivedType(typeof(CompetitorRegistered), "competitorRegistered")]
[JsonDerivedType(typeof(CompetitorWithdrawn), "competitorWithdrawn")]
[JsonDerivedType(typeof(PhaseDrawn), "phaseDrawn")]
[JsonDerivedType(typeof(ReflightGroupAppended), "reflightGroupAppended")]
[JsonDerivedType(typeof(TaskRoundCompleted), "taskRoundCompleted")]
[JsonDerivedType(typeof(TaskRoundAnnulled), "taskRoundAnnulled")]
[JsonDerivedType(typeof(TaskRoundReopened), "taskRoundReopened")]
[JsonDerivedType(typeof(DrawAccepted), "drawAccepted")]
[JsonDerivedType(typeof(DrawRejected), "drawRejected")]
[JsonDerivedType(typeof(RulesAmended), "rulesAmended")]
[JsonDerivedType(typeof(ParameterBound), "parameterBound")]
[JsonDerivedType(typeof(Finalised), "finalised")]
[JsonDerivedType(typeof(PenaltyRecorded), "penaltyRecorded")]
[JsonDerivedType(typeof(ReflightRulingRecorded), "reflightRulingRecorded")]
public abstract record CompetitionEvent : IDomainEvent
{
    private protected CompetitionEvent() { }
}

/// <summary>
/// The creation event: field, rounds and adopted rulebook are set up
/// separately (CompetitorRegistered, PhaseDrawn), so this event alone folds to
/// a Competition with an empty field and no phases yet — a transient state a
/// command handler, not this fold, is responsible for not leaving exposed.
/// </summary>
public sealed record CompetitionCreated(
    CompetitionId Id,
    string Name,
    string Location,
    DateOnly StartDate,
    DateOnly EndDate,
    string EvaluatorVersion,
    AdoptedRules AdoptedRules,
    DateTimeOffset At) : CompetitionEvent;

/// <summary>One person's registration into the field (aggregate-roots.md §3's field-freeze note).</summary>
public sealed record CompetitorRegistered(
    Competitor Competitor,
    DateTimeOffset At) : CompetitionEvent;

/// <summary>Leaves the draw intact — the competitor's entries simply never occur.</summary>
public sealed record CompetitorWithdrawn(
    CompetitorId CompetitorRef,
    DateTimeOffset At) : CompetitionEvent;

/// <summary>
/// The whole round/task-round/group schedule for one phase, drawn atomically
/// and appended — "created up front", per aggregate-roots.md §3.
/// </summary>
public sealed record PhaseDrawn(
    int PhaseOrdinal,
    PhaseType Type,
    Draw Draw,
    ImmutableArray<Round> Rounds,
    DateTimeOffset At) : CompetitionEvent;

/// <summary>
/// A reflight group appended to an existing task-round.
/// <paramref name="Reason"/> records the entitlement basis (collision,
/// hindrance, timing failure — F5J 5.5.11.6 b); audit-only, exactly like
/// TaskRoundAnnulled's.
/// </summary>
public sealed record ReflightGroupAppended(
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    Group Group,
    string Reason,
    DateTimeOffset At) : CompetitionEvent;

/// <summary>
/// The CD asserting that this task-round's scores are in and settled — never a
/// side effect of anything, and never inferred from the field
/// (kanban/completed/task-round-lifecycle.md, "The governing principle").
/// Reversible by <see cref="TaskRoundReopened"/>, which is what lets it close
/// score capture without ever locking a late score out for good.
/// </summary>
public sealed record TaskRoundCompleted(
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    DateTimeOffset At) : CompetitionEvent;

/// <summary>
/// <paramref name="Reason"/> is carried for audit only — TaskRound itself has
/// no Reason field, this is not folded into the aggregate's shape.
/// </summary>
public sealed record TaskRoundAnnulled(
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    string Reason,
    DateTimeOffset At) : CompetitionEvent;

/// <summary>
/// Returns a Complete or Annulled task-round to Drawn, so a score that arrives
/// late is accepted rather than refused — NFR-4, and the reason completion is
/// allowed to close capture at all. <paramref name="Reason"/> is carried for
/// audit only, not folded: TaskRoundAnnulled's precedent, so a reopening is an
/// auditable act rather than a silent write.
/// </summary>
public sealed record TaskRoundReopened(
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    string Reason,
    DateTimeOffset At) : CompetitionEvent;

/// <summary>
/// The CD standing behind the drawn schedule — glossary: "once accepted, the
/// competition can begin". Moves the named phase's Draw.Status to "accepted";
/// the field freeze (D6) and CompetitionSetup parameter freeze (D7) key off
/// this state, and Entry opening gates on it (D4).
/// </summary>
public sealed record DrawAccepted(int PhaseOrdinal, DateTimeOffset At) : CompetitionEvent;

/// <summary>
/// The CD sending the draw back — a rejected phase is removed from the fold
/// (Phases holds only live phases, decision D2), which is what lets DrawPhase
/// address the replacement draw correctly without any edit. Reason is carried
/// for audit only — TaskRoundAnnulled's precedent; the log keeps every
/// rejected draw even though the fold forgets them.
/// </summary>
public sealed record DrawRejected(int PhaseOrdinal, string Reason, DateTimeOffset At) : CompetitionEvent;

/// <summary>Appends a corrected definition, applied retroactively across the whole competition.</summary>
public sealed record RulesAmended(RulesAmendment Amendment) : CompetitionEvent;

/// <summary>Records one choice the class left open, when it was made and by whom.</summary>
public sealed record ParameterBound(ParameterBinding Binding) : CompetitionEvent;

/// <summary>
/// Covers both phase- and competition-scope finalisation — Finalisation.Scope
/// already discriminates, so no need for two event types.
/// </summary>
public sealed record Finalised(Finalisation Finalisation) : CompetitionEvent;

/// <summary>TaskRound/Competition scope only — Flight/Entry-scoped penalties live on the Entry aggregate.</summary>
public sealed record PenaltyRecorded(Penalty Penalty) : CompetitionEvent;

/// <summary>
/// The CD settling which score counts where the class rulebook is silent
/// (<see cref="ReflightSelection.UndefinedRequiresRuling"/> — F3B Task C, F5L,
/// NZ.3.12.5 l; reflight-scoring-rulings.md). Superseding rulings accumulate —
/// the log keeps every decision, and last-logged wins at lookup time (RR3).
/// </summary>
public sealed record ReflightRulingRecorded(ReflightRuling Ruling) : CompetitionEvent;
