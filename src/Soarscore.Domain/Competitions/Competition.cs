// The Competition aggregate — docs/aggregate-roots.md §3, cross-checked
// against docs/soaring-domain-class-diagram.md §1.
//
// The setup and shape of one event: its field, its rulebook copy, and its
// phase/round/task-round/group schedule. Created up front and only lightly
// mutated afterwards (register or withdraw a competitor, append a re-flight
// group, annul a task-round). It holds no live flight data — Entry
// (a separate aggregate, built elsewhere) references Group and Competitor by
// id from outside, which is what keeps high-concurrency scorer writes off
// this root.

using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.Domain.Competitions;

public enum FinalisationScope { Phase, Competition }

public enum TaskRoundState { Drawn, InProgress, Complete, Annulled }

/// <summary>
/// A complete copy of the class definition, taken at creation, with the
/// source class and version it came from and the evaluator version that
/// interprets it. This is what scoring reads — never the library
/// CompetitionClass — so editing or retiring a library class cannot disturb
/// a running or finished event (soaring-domain-class-diagram.md, "The
/// Competition owns its rulebook").
/// </summary>
public sealed record AdoptedRules
{
    public required ClassDefinition Definition { get; init; }

    /// <summary>
    /// The content hash identifying the library ClassDefinition this was
    /// copied from (ADR-0002 §5) — a plain string, not a typed id, because
    /// ClassDefinition has no minted identity to reference.
    /// </summary>
    public required string SourceClassId { get; init; }

    public required string SourceVersion { get; init; }

    public required DateTimeOffset AdoptedAt { get; init; }
}

/// <summary>
/// Appends a corrected definition and applies to the whole competition
/// retroactively — because results are derived, that costs nothing but a
/// re-query (aggregate-roots.md §3).
/// </summary>
public sealed record RulesAmendment
{
    public required ClassDefinition Definition { get; init; }

    public required string Reason { get; init; }

    public required string By { get; init; }

    public required DateTimeOffset At { get; init; }
}

/// <summary>
/// Records one choice the class left open, when it was made and by whom.
/// Bindings are events rather than configuration precisely because
/// re-scoring must reproduce the decisions as they were actually taken
/// (aggregate-roots.md §3).
/// </summary>
public sealed record ParameterBinding
{
    public required string ParameterName { get; init; }

    public required MeasuredValue BoundValue { get; init; }

    public required string By { get; init; }

    public required DateTimeOffset At { get; init; }
}

/// <summary>
/// Freezes results for a phase (naming who was promoted) or the whole
/// competition. Reopening after an error appends a further revision and
/// keeps the earlier one — nothing is overwritten.
/// </summary>
public sealed record Finalisation
{
    public required FinalisationScope Scope { get; init; }

    public required int Revision { get; init; }

    public required string By { get; init; }

    public required DateTimeOffset At { get; init; }

    /// <summary>1..*.</summary>
    public required ImmutableArray<DeclaredResult> DeclaredResults { get; init; }
}

/// <summary>
/// Answers "what was declared", never "what is the score" — raw measurements
/// plus the adopted rules remain the sole source of truth, so a declared
/// result can always be re-derived and compared against what was published.
/// </summary>
public sealed record DeclaredResult
{
    public required CompetitorId CompetitorRef { get; init; }

    public required decimal Aggregate { get; init; }

    public required int Placing { get; init; }

    public required bool Promoted { get; init; }
}

/// <summary>
/// One person's registration into this event. Lives inside the Competition
/// aggregate (rather than being pushed out like Entry) because the draw's
/// fairness invariant needs the field as one consistent set, and
/// registration writes are low-volume — the contention argument that pushes
/// Entry out does not apply here. References the system-wide Person by id
/// only (aggregate-roots.md's Law of Demeter / aggregate boundary rule).
/// </summary>
public sealed record Competitor
{
    public required CompetitorId Id { get; init; }

    public required PersonId PersonRef { get; init; }

    public required int CompetitorNumber { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Null while still fielded. Withdrawing leaves the draw intact — see aggregate-roots.md §3's field-freeze note.</summary>
    public DateTimeOffset? WithdrawnAt { get; init; }
}

/// <summary>
/// group membership is NOT stored here — deliberately no competitor/member
/// list. "Who is in Group C" is a query over the Entry aggregate (which
/// Entries have GroupRef pointing here), not a list held on Group
/// (aggregate-roots.md §3 callout). Do not add one back.
/// </summary>
public sealed record Group
{
    public required GroupId Id { get; init; }

    public required int Ordinal { get; init; }
}

/// <summary>
/// References the class's TaskDefinition by its Code, not by a typed id —
/// TaskDefinition (nested inside AdoptedRules.Definition) has no synthetic
/// id of its own, only a Code, so Code is the only stable handle to reference.
/// </summary>
public sealed record TaskRound
{
    public required int Ordinal { get; init; }

    public required TaskRoundState State { get; init; }

    public required string TaskRef { get; init; }

    /// <summary>1..*.</summary>
    public required ImmutableArray<Group> Groups { get; init; }
}

public sealed record Round
{
    public required int Ordinal { get; init; }

    /// <summary>1..*.</summary>
    public required ImmutableArray<TaskRound> TaskRounds { get; init; }

    /// <summary>
    /// Derived, not stored: complete when every TaskRound is Complete or
    /// Annulled, so partial annulment is handled by filtering rather than by
    /// mutating a completion flag (soaring-domain-class-diagram.md,
    /// "Round completion is derived"). An annulled task-round is a
    /// resolution, not a block.
    /// </summary>
    public bool IsComplete => TaskRounds.All(tr => tr.State is TaskRoundState.Complete or TaskRoundState.Annulled);
}

/// <summary>
/// The diagram types Status as a bare string rather than a lifecycle enum —
/// kept that way deliberately, since neither doc source states the set of
/// draw statuses.
/// </summary>
public sealed record Draw
{
    public required DateTimeOffset CreatedAt { get; init; }

    public required string Status { get; init; }
}

public sealed record Phase
{
    public required PhaseType Type { get; init; }

    public required int Ordinal { get; init; }

    public required Draw Draw { get; init; }

    /// <summary>1..*.</summary>
    public required ImmutableArray<Round> Rounds { get; init; }
}

/// <summary>
/// The aggregate root. Identity per LADR-0003 (`readonly record struct XId(Guid)`)
/// even though the abbreviated §3 diagram omits it, the same abbreviation
/// pattern ClassDefinition's diagram uses elsewhere — Person's diagram shows
/// `+id id` explicitly, confirming every aggregate root carries one.
/// </summary>
public sealed record Competition
{
    public required CompetitionId Id { get; init; }

    public required string Name { get; init; }

    public required string Location { get; init; }

    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public required string EvaluatorVersion { get; init; }

    /// <summary>0..* — the field. See aggregate-roots.md §3's field-freeze note.</summary>
    public ImmutableArray<Competitor> Competitors { get; init; } = [];

    /// <summary>1..*.</summary>
    public required ImmutableArray<Phase> Phases { get; init; }

    public required AdoptedRules AdoptedRules { get; init; }

    public ImmutableArray<RulesAmendment> RulesAmendments { get; init; } = [];

    public ImmutableArray<ParameterBinding> ParameterBindings { get; init; } = [];

    public ImmutableArray<Finalisation> Finalisations { get; init; } = [];

    /// <summary>TaskRound / Competition scope only — Flight / Entry scoped penalties live on the Entry aggregate.</summary>
    public ImmutableArray<Penalty> Penalties { get; init; } = [];
}
