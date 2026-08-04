// Event contracts for the Competition aggregate — docs/aggregate-roots.md §3.
//
// The setup and shape of one event: its field, its rulebook copy, and its
// phase/round/task-round/group schedule. Created up front (CompetitionCreated
// + PhaseDrawn) and only lightly mutated afterwards — register or withdraw a
// competitor, append a reflight group, annul a task-round, amend the rules,
// bind a parameter, finalise, record a penalty. Eleven events total, mirroring
// aggregate-roots.md §3's mutation list one-for-one.
//
// Event payloads reuse Domain's own value-object records (AdoptedRules,
// RulesAmendment, ParameterBinding, Finalisation, Competitor, Group, Round,
// Penalty) directly rather than redefining their shapes — those types are
// already immutable value objects with no Marten dependency, so wrapping them
// again here would just be duplication (LADR-0001 §4).

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.Domain.Competitions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(CompetitionCreated), "competitionCreated")]
[JsonDerivedType(typeof(CompetitorRegistered), "competitorRegistered")]
[JsonDerivedType(typeof(CompetitorWithdrawn), "competitorWithdrawn")]
[JsonDerivedType(typeof(PhaseDrawn), "phaseDrawn")]
[JsonDerivedType(typeof(ReflightGroupAppended), "reflightGroupAppended")]
[JsonDerivedType(typeof(TaskRoundCompleted), "taskRoundCompleted")]
[JsonDerivedType(typeof(TaskRoundAnnulled), "taskRoundAnnulled")]
[JsonDerivedType(typeof(RulesAmended), "rulesAmended")]
[JsonDerivedType(typeof(ParameterBound), "parameterBound")]
[JsonDerivedType(typeof(Finalised), "finalised")]
[JsonDerivedType(typeof(PenaltyRecorded), "penaltyRecorded")]
public abstract record CompetitionEvent
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

/// <summary>A reflight group appended to an existing task-round.</summary>
public sealed record ReflightGroupAppended(
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    Group Group,
    DateTimeOffset At) : CompetitionEvent;

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
