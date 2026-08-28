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
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;

namespace Soarscore.Domain.Competitions;

/// <summary>
/// <see cref="IParsable{TSelf}"/> so ASP.NET's Minimal API parameter binding
/// (<c>[AsParameters]</c> query records, e.g. GetCompetition) can bind this
/// straight from a query-string value — no Api-layer converter needed.
/// Mirrors People/Person.cs's PersonId.
/// </summary>
public readonly record struct CompetitionId(Guid Value) : IParsable<CompetitionId>
{
    public static CompetitionId New() => new(Guid.CreateVersion7());

    public static CompetitionId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s, provider));

    public static bool TryParse(string? s, IFormatProvider? provider, out CompetitionId result)
    {
        if (Guid.TryParse(s, provider, out var value))
        {
            result = new CompetitionId(value);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>
/// An entity inside this aggregate; referenced by id from the Entry aggregate.
/// <see cref="IParsable{TSelf}"/> so ASP.NET's Minimal API parameter binding
/// (<c>[AsParameters]</c> query records, e.g. FindEntries) can bind this
/// straight from a query-string value — mirrors CompetitionId above.
/// </summary>
public readonly record struct CompetitorId(Guid Value) : IParsable<CompetitorId>
{
    public static CompetitorId New() => new(Guid.CreateVersion7());

    public static CompetitorId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s, provider));

    public static bool TryParse(string? s, IFormatProvider? provider, out CompetitorId result)
    {
        if (Guid.TryParse(s, provider, out var value))
        {
            result = new CompetitorId(value);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>
/// An entity inside this aggregate; referenced by id from the Entry aggregate.
/// Group membership is not stored on the Group itself — it is the set of
/// Entries whose GroupRef points at it (aggregate-roots.md §3).
/// <see cref="IParsable{TSelf}"/> so ASP.NET's Minimal API parameter binding
/// (<c>[AsParameters]</c> query records, e.g. FindEntries) can bind this
/// straight from a query-string value — mirrors CompetitionId above.
/// </summary>
public readonly record struct GroupId(Guid Value) : IParsable<GroupId>
{
    public static GroupId New() => new(Guid.CreateVersion7());

    public static GroupId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s, provider));

    public static bool TryParse(string? s, IFormatProvider? provider, out GroupId result)
    {
        if (Guid.TryParse(s, provider, out var value))
        {
            result = new GroupId(value);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

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

    /// <summary>
    /// Both-or-neither with <see cref="RoundOrdinal"/>. Null means this binding is
    /// unscoped — it applies wherever no round-scoped binding of the same
    /// parameter wins (kanban/completed/catalogue-choice-draws-plan.md Appendix A's
    /// resolution order). 0-based, matching <see cref="Phase.Ordinal"/>.
    /// </summary>
    public int? PhaseOrdinal { get; init; }

    /// <summary>
    /// Both-or-neither with <see cref="PhaseOrdinal"/>. 1-based, matching
    /// <see cref="Round.Ordinal"/> — NOT <see cref="TaskRound.Ordinal"/>, which
    /// indexes the (currently always single) task within the round.
    /// </summary>
    public int? RoundOrdinal { get; init; }
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
/// <see cref="CompetitorRefs"/> is the *drawn* allocation — who this draw put
/// in the group, fixed at draw time (aggregate-roots.md §3 callout). It is
/// not "who flew": after reflights, fillers and annulments, who a scoring
/// pass actually counts for the group remains an Entry-derived query (Entries
/// whose GroupRef points here), not duplicated on this list.
/// </summary>
public sealed record Group
{
    public required GroupId Id { get; init; }

    public required int Ordinal { get; init; }

    /// <summary>2..* — the drawn allocation. See the type doc comment.</summary>
    public required ImmutableArray<CompetitorId> CompetitorRefs { get; init; }
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
    /// Derived, not stored: nothing left to fly here, because every TaskRound is
    /// Complete or Annulled — so partial annulment is handled by filtering rather
    /// than by mutating a completion flag (soaring-domain-class-diagram.md,
    /// "Round completion is derived"). An annulled task-round is a resolution,
    /// not a block. Answers "may the competition move on", *not* "did this round
    /// produce a result" — see <see cref="IsFullyFlown"/>.
    /// </summary>
    public bool IsCompleteOrAnnulled => TaskRounds.All(tr => tr.State is TaskRoundState.Complete or TaskRoundState.Annulled);

    /// <summary>
    /// Derived, not stored: every TaskRound was flown to a result, with none
    /// annulled. The stricter of the pair, and the one a validity rule counts —
    /// an annulled round resolved the competition's progress but produced no
    /// result, so it cannot make a contest valid.
    /// </summary>
    public bool IsFullyFlown => TaskRounds.All(tr => tr.State is TaskRoundState.Complete);
}

/// <summary>
/// The diagram types Status as a bare string rather than a lifecycle enum —
/// kept that way deliberately. Vocabulary in folded state is
/// <c>"drawn" | "accepted"</c>: a rejected draw's phase is REMOVED from Phases
/// entirely (Phases holds only live phases — decision D2,
/// kanban/in-progress/draw-acceptance-redraw.md), so no "rejected" value ever
/// appears here.
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
/// Input to <see cref="Competition.PrescribeDraw"/>: one group's membership,
/// listed in flying order (SeqNo for an imported realised draw) and stored
/// exactly as given.
/// </summary>
public sealed record PrescribedGroup(IReadOnlyList<CompetitorId> Competitors);

/// <summary>
/// Input to <see cref="Competition.PrescribeDraw"/>: one round's task choice —
/// null only where the phase is FixedSequence and takes no choice — and its
/// groups, whose ordinals are assigned by position, never supplied.
/// </summary>
public sealed record PrescribedRound(string? TaskRef, IReadOnlyList<PrescribedGroup> Groups);

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

    /// <summary>
    /// The CD's recorded answers to the class rulebook's silences
    /// (<see cref="ReflightSelection.UndefinedRequiresRuling"/>) — accumulate,
    /// never replace: a superseding ruling is a new entry, and last-logged wins
    /// at lookup time (RR3), exactly as ParameterBindings accumulates.
    /// </summary>
    public ImmutableArray<ReflightRuling> Rulings { get; init; } = [];

    /// <summary>The creation event. Every stream begins with exactly one of these.</summary>
    public static Competition Create(CompetitionCreated @event) =>
        new()
        {
            Id = @event.Id,
            Name = @event.Name,
            Location = @event.Location,
            StartDate = @event.StartDate,
            EndDate = @event.EndDate,
            EvaluatorVersion = @event.EvaluatorVersion,
            Competitors = [],
            Phases = [],
            AdoptedRules = @event.AdoptedRules,
            RulesAmendments = [],
            ParameterBindings = [],
            Finalisations = [],
            Penalties = [],
            Rulings = [],
        };

    // One overload per non-creation event — both the domain's own fold-by-type
    // API *and*, unchanged from today's Infrastructure shim, exactly what
    // Marten's conventional-method discovery on SingleStreamProjection<TDoc,TId>
    // matches on.
    public Competition Apply(CompetitorRegistered @event) =>
        this with { Competitors = Competitors.Add(@event.Competitor) };

    public Competition Apply(CompetitorWithdrawn @event)
    {
        var competitors = Competitors
            .Select(c => c.Id == @event.CompetitorRef ? c with { WithdrawnAt = @event.At } : c)
            .ToImmutableArray();

        return this with { Competitors = competitors };
    }

    public Competition Apply(PhaseDrawn @event)
    {
        var phase = new Phase
        {
            Type = @event.Type,
            Ordinal = @event.PhaseOrdinal,
            Draw = @event.Draw,
            Rounds = @event.Rounds,
        };

        return this with { Phases = Phases.Add(phase) };
    }

    public Competition Apply(ReflightGroupAppended @event) =>
        ReplaceTaskRound(
            @event.PhaseOrdinal,
            @event.RoundOrdinal,
            @event.TaskRoundOrdinal,
            taskRound => taskRound with { Groups = taskRound.Groups.Add(@event.Group) });

    public Competition Apply(TaskRoundCompleted @event) =>
        ReplaceTaskRound(
            @event.PhaseOrdinal,
            @event.RoundOrdinal,
            @event.TaskRoundOrdinal,
            taskRound => taskRound with { State = TaskRoundState.Complete });

    public Competition Apply(TaskRoundAnnulled @event) =>
        ReplaceTaskRound(
            @event.PhaseOrdinal,
            @event.RoundOrdinal,
            @event.TaskRoundOrdinal,
            taskRound => taskRound with { State = TaskRoundState.Annulled });

    public Competition Apply(TaskRoundReopened @event) =>
        ReplaceTaskRound(
            @event.PhaseOrdinal,
            @event.RoundOrdinal,
            @event.TaskRoundOrdinal,
            taskRound => taskRound with { State = TaskRoundState.Drawn });

    public Competition Apply(DrawAccepted @event)
    {
        var phases = Phases
            .Select(phase => phase.Ordinal == @event.PhaseOrdinal
                ? phase with { Draw = phase.Draw with { Status = "accepted" } }
                : phase)
            .ToImmutableArray();

        return this with { Phases = phases };
    }

    // Removal, not a status write: Phases holds only live phases (decision D2,
    // kanban/in-progress/draw-acceptance-redraw.md), which is what lets the
    // draw decide functions (DrawPhase, PrescribeDraw) address the replacement
    // draw correctly with no edit and what reopens registration and unscoped
    // parameter binds automatically. Reason is audit-only — TaskRoundAnnulled's
    // precedent; the log keeps every rejected draw even though this fold
    // forgets them.
    public Competition Apply(DrawRejected @event) =>
        this with { Phases = Phases.Where(p => p.Ordinal != @event.PhaseOrdinal).ToImmutableArray() };

    public Competition Apply(RulesAmended @event) =>
        this with { RulesAmendments = RulesAmendments.Add(@event.Amendment) };

    public Competition Apply(ParameterBound @event) =>
        this with { ParameterBindings = ParameterBindings.Add(@event.Binding) };

    public Competition Apply(Finalised @event) =>
        this with { Finalisations = Finalisations.Add(@event.Finalisation) };

    public Competition Apply(PenaltyRecorded @event) =>
        this with { Penalties = Penalties.Add(@event.Penalty) };

    // Accumulate, never replace: the log keeps every ruling (RR3's fold half).
    public Competition Apply(ReflightRulingRecorded @event) =>
        this with { Rulings = Rulings.Add(@event.Ruling) };

    /// <summary>
    /// Shared navigation for ReflightGroupAppended, TaskRoundCompleted,
    /// TaskRoundAnnulled and TaskRoundReopened: find the Phase/Round/TaskRound
    /// by ordinal — never by
    /// array index — rebuild the three containing arrays with the mutated
    /// TaskRound in place, and leave everything else untouched.
    /// </summary>
    private Competition ReplaceTaskRound(
        int phaseOrdinal,
        int roundOrdinal,
        int taskRoundOrdinal,
        Func<TaskRound, TaskRound> mutate)
    {
        var phases = Phases
            .Select(phase =>
            {
                if (phase.Ordinal != phaseOrdinal)
                {
                    return phase;
                }

                var rounds = phase.Rounds
                    .Select(round =>
                    {
                        if (round.Ordinal != roundOrdinal)
                        {
                            return round;
                        }

                        var taskRounds = round.TaskRounds
                            .Select(taskRound => taskRound.Ordinal == taskRoundOrdinal ? mutate(taskRound) : taskRound)
                            .ToImmutableArray();

                        return round with { TaskRounds = taskRounds };
                    })
                    .ToImmutableArray();

                return phase with { Rounds = rounds };
            })
            .ToImmutableArray();

        return this with { Phases = phases };
    }

    /// <summary>
    /// Generic replay entry point, folding a closed <see cref="CompetitionEvent"/>
    /// union rather than requiring callers to hold the concrete event type. Not
    /// what Marten calls (Marten calls the typed overloads above via its own
    /// conventional-method discovery); this is for generic replay code and tests.
    /// </summary>
    public static Competition? Apply(Competition? current, CompetitionEvent @event) =>
        @event switch
        {
            CompetitionCreated created => Create(created),
            CompetitorRegistered e => Require(current, e).Apply(e),
            CompetitorWithdrawn e => Require(current, e).Apply(e),
            PhaseDrawn e => Require(current, e).Apply(e),
            ReflightGroupAppended e => Require(current, e).Apply(e),
            TaskRoundCompleted e => Require(current, e).Apply(e),
            TaskRoundAnnulled e => Require(current, e).Apply(e),
            TaskRoundReopened e => Require(current, e).Apply(e),
            DrawAccepted e => Require(current, e).Apply(e),
            DrawRejected e => Require(current, e).Apply(e),
            RulesAmended e => Require(current, e).Apply(e),
            ParameterBound e => Require(current, e).Apply(e),
            Finalised e => Require(current, e).Apply(e),
            PenaltyRecorded e => Require(current, e).Apply(e),
            ReflightRulingRecorded e => Require(current, e).Apply(e),
            _ => throw new ArgumentException($"Unknown CompetitionEvent subtype: {@event.GetType().Name}"),
        };

    private static Competition Require(Competition? current, CompetitionEvent @event) =>
        current ?? throw new ArgumentException($"{@event.GetType().Name} folded with no current projection — a Competition must begin with CompetitionCreated.");

    // Decide functions — WI-2 (kanban/completed/create-competition-steel-thread-plan.md).
    // Named Decide, not Create, because Create is already taken by the fold
    // above. Unlike Person.Register, this does not mint its own id: WI-3's
    // handler needs the id before calling Decide, in order to also construct
    // AdoptedRules from a cross-aggregate read. AdoptedRules is deliberately
    // not (re-)validated here — by the time this runs, the caller has already
    // resolved it from an already-validated, immutable PublishedClassDefinition
    // stream (LADR-0002 §5). A decide function takes already-resolved value
    // objects as input, the same way Person.Register takes an
    // already-constructed ContactDetails rather than reaching out to check
    // anything about it itself.
    public static Result<CompetitionCreated> Decide(
        CompetitionId id,
        string name,
        string location,
        DateOnly startDate,
        DateOnly endDate,
        string evaluatorVersion,
        AdoptedRules adoptedRules,
        DateTimeOffset at)
    {
        var defect = ValidateName(name) ?? ValidateLocation(location) ?? ValidateDates(startDate, endDate);
        return defect is not null
            ? Result<CompetitionCreated>.Failure(defect.Code, defect.Message)
            : Result<CompetitionCreated>.Success(
                new CompetitionCreated(id, name, location, startDate, endDate, evaluatorVersion, adoptedRules, at));
    }

    private static Defect? ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? new Defect("competition.name.blank", "$.name", "Name must not be blank.")
            : null;

    private static Defect? ValidateLocation(string location) =>
        string.IsNullOrWhiteSpace(location)
            ? new Defect("competition.location.blank", "$.location", "Location must not be blank.")
            : null;

    private static Defect? ValidateDates(DateOnly startDate, DateOnly endDate) =>
        startDate > endDate
            ? new Defect("competition.dates.invalid", "$.startDate", "Start date must not be after end date.")
            : null;

    // Instance decide functions — WI-1 (kanban/completed/register-competitor-steel-thread-plan.md).
    // Instance, unlike Decide above: deciding whether a registration or
    // withdrawal is valid needs the current field, and the aggregate is what
    // already holds it. CompetitorId is minted by the caller (handler), not
    // here — same reason Decide above does not mint CompetitionId: a decide
    // function stays deterministic on already-resolved inputs, which is what
    // lets WI-2's property tests compare an expected event against an actual
    // one.

    public Result<CompetitorRegistered> RegisterCompetitor(CompetitorId id, PersonId personRef, DateTimeOffset at)
    {
        var defect = ValidateNotAlreadyRegistered(personRef) ?? ValidateFieldNotFrozen();
        if (defect is not null)
        {
            return Result<CompetitorRegistered>.Failure(defect.Code, defect.Message);
        }

        // max+1, not Count+1: they agree today only because withdrawal never
        // removes a record. Numbers are never reused — a withdrawn
        // competitor's number stays retired, already written on score sheets.
        var competitor = new Competitor
        {
            Id = id,
            PersonRef = personRef,
            CompetitorNumber = Competitors.Select(c => c.CompetitorNumber).DefaultIfEmpty(0).Max() + 1,
            RegisteredAt = at,
            WithdrawnAt = null,
        };

        return Result<CompetitorRegistered>.Success(new CompetitorRegistered(competitor, at));
    }

    public Result<CompetitorWithdrawn> WithdrawCompetitor(CompetitorId competitorRef, DateTimeOffset at)
    {
        // Deliberately no field-freeze check: aggregate-roots.md:330-333 closes
        // *registration* at the draw, not withdrawal — "a withdrawal is
        // recorded but leaves the draw intact." Registration closes at the
        // draw; withdrawal never closes. This looks like an asymmetric
        // oversight; it is the rule.
        var defect = ValidateCompetitorExists(competitorRef) ?? ValidateNotAlreadyWithdrawn(competitorRef);
        return defect is not null
            ? Result<CompetitorWithdrawn>.Failure(defect.Code, defect.Message)
            : Result<CompetitorWithdrawn>.Success(new CompetitorWithdrawn(competitorRef, at));
    }

    // Draw-lifecycle instance decide functions — WI-2
    // (kanban/in-progress/draw-acceptance-redraw.md). Defect-chain style, like
    // RegisterCompetitor/WithdrawCompetitor above: no later check needs an
    // earlier check's value beyond the live phase they all share. Both act on
    // THE LIVE PHASE — the single element of Phases (the story's P1 proves
    // there is at most one) — and the emitted event carries that phase's
    // Ordinal.
    //
    // Accept requires status "drawn" (D4/D6/D7 all key off acceptance:
    // entries open, the field freezes and CompetitionSetup parameters freeze
    // only once the CD has stood behind the draw). Reject deliberately has no
    // alreadyAccepted code: rejecting an accepted draw nobody has flown
    // against is the ordinary correction path, and D2's phase removal is what
    // reopens registration and unscoped binds — only entries block it (D5).
    //
    // Reason is validated here rather than in the handler because it is a
    // substantive CD ruling record, not an audit breadcrumb — AnnulTaskRound's
    // recorded reasoning (story decision F2). phaseHasEntries is an
    // already-resolved fact supplied by the handler from IEntryQuery, with NO
    // default: unlike BindParameter.roundHasEntries, a wrong default here
    // would silently orphan entries.
    public Result<DrawAccepted> AcceptDraw(DateTimeOffset at)
    {
        var phase = LivePhase();

        var defect = DrawnPhaseFound(phase, "acceptDraw")
            ?? (phase!.Draw.Status == "accepted"
                ? new Defect("acceptDraw.alreadyAccepted", "$.competitionId", "This draw has already been accepted.")
                : null);

        return defect is not null
            ? Result<DrawAccepted>.Failure(defect.Code, defect.Message)
            : Result<DrawAccepted>.Success(new DrawAccepted(phase!.Ordinal, at));
    }

    public Result<DrawRejected> RejectDraw(bool phaseHasEntries, string reason, DateTimeOffset at)
    {
        var phase = LivePhase();

        var defect = DrawnPhaseFound(phase, "rejectDraw")
            ?? ReasonGiven(reason, "rejectDraw")
            ?? (phaseHasEntries
                ? new Defect(
                    "rejectDraw.entriesExist", "$.competitionId",
                    "Entries already exist against this phase's draw; rejecting it would orphan them.")
                : null);

        return defect is not null
            ? Result<DrawRejected>.Failure(defect.Code, defect.Message)
            : Result<DrawRejected>.Success(new DrawRejected(phase!.Ordinal, reason, at));
    }

    // Instance decide function — WI-1 (kanban/completed/bind-parameter-steel-thread-plan.md),
    // round scope added by kanban/completed/per-round-parameter-bindings-plan.md.
    // Defect-chain style, like RegisterCompetitor/WithdrawCompetitor above —
    // unlike DrawPhase below, no later check needs a value computed by an
    // earlier one, so each validator re-resolves the named Parameter itself
    // rather than threading it through. Re-binding before the draw is
    // deliberately allowed and not deduped here: last-write-wins is resolved
    // at the draw's call site (Competition.cs, DrawPhase), not here.
    //
    // roundHasEntries is an already-resolved fact the handler supplies from
    // IEntryQuery — the aggregate boundary holds exactly as it does for
    // AdoptedRules: Competition still holds no live flight data, it is simply
    // told one bool about it (kanban/completed/task-round-lifecycle.md WI-9).
    // Defaulted so unscoped binds, which cannot be round-frozen anyway, take
    // no extra query.
    public Result<ParameterBound> BindParameter(
        string parameterName, MeasuredValue value, string by, DateTimeOffset at,
        int? phaseOrdinal = null, int? roundOrdinal = null, bool roundHasEntries = false)
    {
        var defect = ValidateParameterDeclared(parameterName)
            ?? ValidateParameterKind(parameterName, value)
            ?? ValidateParameterValueAllowed(parameterName, value)
            ?? ValidateParameterNotFrozen(parameterName)
            ?? ValidateRoundScope(parameterName, phaseOrdinal, roundOrdinal, roundHasEntries);

        if (defect is not null)
        {
            return Result<ParameterBound>.Failure(defect.Code, defect.Message);
        }

        return Result<ParameterBound>.Success(
            new ParameterBound(new ParameterBinding
            {
                ParameterName = parameterName,
                BoundValue = value,
                By = by,
                At = at,
                PhaseOrdinal = phaseOrdinal,
                RoundOrdinal = roundOrdinal,
            }));
    }

    // Instance decide function — WI-1 (kanban/completed/phase-drawn-steel-thread-plan.md).
    // Not the Defect-chain style RegisterCompetitor/WithdrawCompetitor use:
    // later checks need values (phaseDefinition, the eligible field,
    // resolved MinPerGroup) computed by earlier ones, and the happy path
    // needs them again to build the event, so this reads top-to-bottom with
    // early returns instead.
    public Result<PhaseDrawn> DrawPhase(int rounds, ImmutableArray<string> taskRefs, DateTimeOffset at)
    {
        var resolved = ResolveSchedule(rounds, taskRefs, "drawPhase");
        if (resolved.IsFailure)
        {
            return Result<PhaseDrawn>.Failure(resolved.Code!, resolved.Message!);
        }

        var (phaseDefinition, resolvedTaskRefs, field, minPerGroupByRound) = resolved.Value;

        var groupedRounds = PhaseDraw.BuildGroups(field, minPerGroupByRound);

        var rows = groupedRounds
            .Select((groups, roundIndex) => new Round
            {
                Ordinal = roundIndex + 1,
                TaskRounds =
                [
                    new TaskRound
                    {
                        Ordinal = 1,
                        State = TaskRoundState.Drawn,
                        TaskRef = resolvedTaskRefs[roundIndex],
                        Groups = groups
                            .Select((members, groupIndex) => new Group
                            {
                                Id = GroupId.New(),
                                Ordinal = groupIndex + 1,
                                CompetitorRefs = members,
                            })
                            .ToImmutableArray(),
                    },
                ],
            })
            .ToImmutableArray();

        var @event = new PhaseDrawn(
            PhaseOrdinal: Phases.Length,
            Type: phaseDefinition.Type,
            Draw: new Draw { CreatedAt = at, Status = "drawn" },
            Rounds: rows,
            At: at);

        return Result<PhaseDrawn>.Success(@event);
    }

    /// <summary>
    /// Sets the live phase's schedule explicitly — the prescribed-draw path
    /// beside <see cref="DrawPhase"/>
    /// (kanban/in-progress/prescribed-draw-import.md WI-1). Emits the same
    /// <see cref="PhaseDrawn"/> event, with provenance in
    /// <see cref="PhaseDrawn.PrescribedBy"/>; group and round ordinals are
    /// assigned by position and members are stored in the supplied flying
    /// order, preserved as-is. Validation is exactly what generation
    /// guarantees — the shared schedule checks, then each round partitions
    /// the eligible field exactly once with groups of at least two and no
    /// smaller than the round's resolved minimum — and nothing stricter:
    /// fairness, group counts and pairing minimisation are the generator's
    /// business (D2), so a realised draw can be reproduced verbatim.
    /// </summary>
    public Result<PhaseDrawn> PrescribeDraw(IReadOnlyList<PrescribedRound> rounds, string by, DateTimeOffset at)
    {
        var resolved = ResolveSchedule(
            rounds.Count,
            [.. rounds.Where(r => r.TaskRef is not null).Select(r => r.TaskRef!)],
            "prescribeDraw");

        if (resolved.IsFailure)
        {
            return Result<PhaseDrawn>.Failure(resolved.Code!, resolved.Message!);
        }

        var (phaseDefinition, resolvedTaskRefs, field, minPerGroupByRound) = resolved.Value;

        for (var roundIndex = 0; roundIndex < rounds.Count; roundIndex++)
        {
            var round = rounds[roundIndex];
            var placed = new HashSet<CompetitorId>();

            foreach (var group in round.Groups)
            {
                foreach (var member in group.Competitors)
                {
                    var competitor = Competitors.FirstOrDefault(c => c.Id == member);
                    if (competitor is null || competitor.WithdrawnAt is not null)
                    {
                        return Result<PhaseDrawn>.Failure(
                            "prescribeDraw.competitorNotInField",
                            $"Round {roundIndex + 1}: a grouped id is not an eligible competitor in this competition.");
                    }

                    if (!placed.Add(member))
                    {
                        return Result<PhaseDrawn>.Failure(
                            "prescribeDraw.competitorRepeated",
                            $"Round {roundIndex + 1}: the same competitor appears more than once.");
                    }
                }
            }

            foreach (var eligible in field)
            {
                if (!placed.Contains(eligible))
                {
                    return Result<PhaseDrawn>.Failure(
                        "prescribeDraw.competitorMissing",
                        $"Round {roundIndex + 1}: an eligible competitor appears in no group.");
                }
            }

            for (var groupIndex = 0; groupIndex < round.Groups.Count; groupIndex++)
            {
                var size = round.Groups[groupIndex].Competitors.Count;

                if (size < 2)
                {
                    return Result<PhaseDrawn>.Failure(
                        "prescribeDraw.groupTooSmall",
                        $"Round {roundIndex + 1}, group {groupIndex + 1}: a group needs at least 2 members; got {size}.");
                }

                if (size < minPerGroupByRound[roundIndex])
                {
                    return Result<PhaseDrawn>.Failure(
                        "prescribeDraw.groupBelowClassMinimum",
                        $"Round {roundIndex + 1}, group {groupIndex + 1}: the group has {size} member(s), smaller than the class's minimum group size ({minPerGroupByRound[roundIndex]}).");
                }
            }
        }

        var rows = rounds
            .Select((round, roundIndex) => new Round
            {
                Ordinal = roundIndex + 1,
                TaskRounds =
                [
                    new TaskRound
                    {
                        Ordinal = 1,
                        State = TaskRoundState.Drawn,
                        TaskRef = resolvedTaskRefs[roundIndex],
                        Groups = round.Groups
                            .Select((group, groupIndex) => new Group
                            {
                                Id = GroupId.New(),
                                Ordinal = groupIndex + 1,
                                CompetitorRefs = [.. group.Competitors],
                            })
                            .ToImmutableArray(),
                    },
                ],
            })
            .ToImmutableArray();

        var @event = new PhaseDrawn(
            PhaseOrdinal: Phases.Length,
            Type: phaseDefinition.Type,
            Draw: new Draw { CreatedAt = at, Status = "drawn" },
            Rounds: rows,
            At: at,
            PrescribedBy: by);

        return Result<PhaseDrawn>.Success(@event);
    }

    private sealed record ResolvedSchedule(
        PhaseDefinition Phase,
        ImmutableArray<string> ResolvedTaskRefs,
        ImmutableArray<CompetitorId> Field,
        ImmutableArray<int> MinPerGroupByRound);

    // The schedule validation shared by DrawPhase and PrescribeDraw — guards,
    // task resolution, eligible field, per-round minPerGroup resolution. The
    // defect-code prefix is a parameter so each command keeps its own codes
    // for identical conditions: a caller can tell which command rejected
    // without reading the message (TaskRoundFound's rule). Every message is
    // verbatim the one DrawPhase has always emitted.
    private Result<ResolvedSchedule> ResolveSchedule(int rounds, ImmutableArray<string> taskRefs, string codePrefix)
    {
        // The guard is "no live phase", not "never drawn": rejecting a draw
        // removes its phase (D2), so a redraw or re-prescription after a
        // rejection is legal and Phases.Length is again the preliminary's
        // ordinal. Flyoff-phase draws stay deferred (deferred-decisions.md).
        if (!Phases.IsEmpty)
        {
            return Result<ResolvedSchedule>.Failure(
                $"{codePrefix}.alreadyDrawn", "A phase has already been drawn for this competition.");
        }

        // Positional index into the class's ordered phase list — NOT a
        // lookup by PhaseDefinition.Ordinal, which is the rulebook's own
        // (often 1-based) numbering and a different scheme entirely.
        var phaseDefinition = AdoptedRules.Definition.Phases[Phases.Length];

        if (rounds < 1)
        {
            return Result<ResolvedSchedule>.Failure($"{codePrefix}.roundsInvalid", "Rounds must be at least 1.");
        }

        if (phaseDefinition.Rounds.MaxRounds is { } maxRounds && rounds > maxRounds)
        {
            return Result<ResolvedSchedule>.Failure(
                $"{codePrefix}.roundsInvalid", $"Rounds must not exceed the class's maximum of {maxRounds}.");
        }

        // Multi-task rounds (F3B) — a real algorithmic gap this thread does
        // not build, see catalogue-choice-draws-plan.md's Out of scope. The
        // message names multi-task rounds only; catalogue choice has its own
        // codes below.
        if (phaseDefinition.Rounds.TasksPerRound != 1)
        {
            return Result<ResolvedSchedule>.Failure(
                $"{codePrefix}.unsupportedRoundComposition",
                "This phase schedules more than one task per round, which is not yet supported by the draw.");
        }

        ImmutableArray<string> resolvedTaskRefs;

        if (phaseDefinition.Rounds.Kind == CompositionKind.FixedSequence)
        {
            if (!taskRefs.IsEmpty)
            {
                return Result<ResolvedSchedule>.Failure(
                    $"{codePrefix}.taskSelectionNotPermitted",
                    "This phase's rounds follow a fixed sequence; the class leaves no task choice to make.");
            }

            // A FixedSequence phase declaring several tasks with
            // tasksPerRound: 1 is a definition the model permits and the
            // draw has no rule for choosing within — no corpus class is in
            // this state today.
            if (phaseDefinition.Tasks.Length != 1)
            {
                return Result<ResolvedSchedule>.Failure(
                    $"{codePrefix}.unsupportedRoundComposition",
                    "This phase declares more than one task for a fixed sequence; the draw has no rule for choosing within it.");
            }

            resolvedTaskRefs = [.. Enumerable.Repeat(phaseDefinition.Tasks[0].Code, rounds)];
        }
        else
        {
            if (taskRefs.IsEmpty)
            {
                return Result<ResolvedSchedule>.Failure(
                    $"{codePrefix}.taskSelectionRequired",
                    "This phase's rounds are chosen from a catalogue; a task must be named for every round.");
            }

            if (taskRefs.Length != rounds)
            {
                return Result<ResolvedSchedule>.Failure(
                    $"{codePrefix}.taskSelectionCountMismatch",
                    $"{rounds} round(s) were requested but {taskRefs.Length} task(s) were named.");
            }

            var catalogueCodes = phaseDefinition.Tasks.Select(t => t.Code).ToImmutableArray();
            var unknownCode = taskRefs.FirstOrDefault(code => !catalogueCodes.Contains(code));
            if (unknownCode is not null)
            {
                return Result<ResolvedSchedule>.Failure(
                    $"{codePrefix}.taskNotInCatalogue",
                    $"'{unknownCode}' is not one of this phase's catalogue task codes ({string.Join(", ", catalogueCodes)}).");
            }

            if (phaseDefinition.Rounds.RequireDistinctTaskPerRound && taskRefs.Distinct().Count() != taskRefs.Length)
            {
                return Result<ResolvedSchedule>.Failure(
                    $"{codePrefix}.taskSelectionNotDistinct",
                    $"This phase requires a different task every round; the catalogue offers {catalogueCodes.Length} distinct task(s).");
            }

            resolvedTaskRefs = taskRefs;
        }

        var field = Competitors
            .Where(c => c.WithdrawnAt is null)
            .Select(c => c.Id)
            .ToImmutableArray();

        if (field.IsEmpty)
        {
            return Result<ResolvedSchedule>.Failure($"{codePrefix}.fieldEmpty", "No eligible competitors to draw.");
        }

        // No round context: a round-scoped binding can only name a round that
        // already exists (ValidateRoundScope), and the rounds this draw is
        // about to create do not exist yet — only unscoped bindings can apply.
        var bindings = ScoringService.FlattenParameterBindings(ParameterBindings);

        // Resolved for every round before the builder runs, not lazily
        // inside it: the draw is atomic, so an unbound parameter on round 5
        // must fail the whole draw rather than emit a partial schedule.
        var minPerGroupByRound = ImmutableArray.CreateBuilder<int>(rounds);

        for (var i = 0; i < rounds; i++)
        {
            var task = phaseDefinition.Tasks.First(t => t.Code == resolvedTaskRefs[i]);

            // Absent GroupConstraint means the class does not group-score at
            // all (NZ N/P) — one whole-field group, every round; minPerGroup
            // == field.Length makes PhaseDraw.BuildGroups's groupCount
            // formula produce exactly one group without a special case.
            var minPerGroup = field.Length;

            if (task.Group is not null)
            {
                decimal resolvedMinPerGroup;
                try
                {
                    resolvedMinPerGroup = ParameterResolver.Resolve(task.Group.MinPerGroup, bindings, AdoptedRules.Definition.Parameters);
                }
                catch (UnresolvedParameterException ex)
                {
                    return Result<ResolvedSchedule>.Failure(
                        $"{codePrefix}.parameterUnbound", $"Round {i + 1} ('{task.Code}'): {ex.Message}");
                }

                minPerGroup = (int)resolvedMinPerGroup;

                if (minPerGroup > field.Length)
                {
                    return Result<ResolvedSchedule>.Failure(
                        $"{codePrefix}.fieldTooSmall",
                        $"Round {i + 1} ('{task.Code}'): the eligible field ({field.Length}) is smaller than the class's minimum group size ({minPerGroup}).");
                }
            }

            minPerGroupByRound.Add(minPerGroup);
        }

        return Result<ResolvedSchedule>.Success(new ResolvedSchedule(
            phaseDefinition, resolvedTaskRefs, field, minPerGroupByRound.MoveToImmutable()));
    }

    // Instance decide function — WI-2 (kanban/completed/capture-a-score-steel-thread-plan.md).
    // Returns an event for the ENTRY aggregate's stream, not this one — new to
    // the repo and deliberate: the Competition is the sole authority on
    // whether an Entry may exist and what working time it gets (does this
    // coordinate exist, was this competitor drawn into this group, is the
    // field still valid), and Entry itself has no state yet to check any of
    // that against. DrawPhase's early-return style above, not
    // RegisterCompetitor's Defect-chain style: later checks need values (the
    // task-round, the group, the resolved working time) computed by earlier
    // ones, and the happy path needs them again to build the event.
    //
    // Deliberately absent: an "already open" check (a read-model concern —
    // see the plan's WI-8) and any PreparationTime handling (no domain
    // representation for a preparation window exists yet — out of scope).
    // The entry's ReflightRole is data, not a ruling this aggregate makes:
    // whether a competitor is entitled or a filler is a witnessed CD ruling
    // recorded as the role at open and as Reason on the reflight append
    // (kanban/in-progress/reflight-groups.md WI-3) — Competition cannot
    // adjudicate it, any more than it can hold Entry data.
    //
    // countsForRoundOrdinal/reason (reflight-aggregate-destination.md WI-2)
    // carry the make-up datum — the round whose ladder slot the score
    // aggregates into, and its required entitlement basis. Their write-side
    // rules live here, in the story's order: destinationOnOriginalRole (D6
    // bullet 1 — an Original counts for its own round, always),
    // destinationNotFound, destinationNotEarlier (an earlier round of the
    // same phase), reasonRequired (D4, the AppendReflightGroup parity). The
    // drawn-check is relaxed for reflight-role entries only (D5): the CD's
    // allocation is the act, so a reflight-role entry may be opened into any
    // group of the addressed task-round for a registered, non-withdrawn
    // competitor, and Group.CompetitorRefs stays "the drawn allocation, not
    // who flew" (reflight-groups finding 4). The event carries the datum
    // verbatim; the scoring side's destination-aware law
    // (ScoringService/ReflightSelector) is the belt.
    public Result<EntryOpened> OpenEntry(
        EntryId id,
        int phaseOrdinal,
        int roundOrdinal,
        int taskRoundOrdinal,
        GroupId groupRef,
        CompetitorId competitorRef,
        ReflightRole role,
        DateTimeOffset at,
        int? countsForRoundOrdinal = null,
        string? reason = null)
    {
        var phase = Phases.FirstOrDefault(p => p.Ordinal == phaseOrdinal);
        if (phase is null)
        {
            return Result<EntryOpened>.Failure(
                "openEntry.phaseNotDrawn", "No phase has been drawn with this ordinal.");
        }

        // D4 (kanban/in-progress/draw-acceptance-redraw.md): the competition
        // begins at acceptance, not at the draw — glossary "once accepted, the
        // competition can begin". Gating on the REFERENCED phase's status (not
        // "any accepted draw exists") is equivalent under P1 and truthful per
        // entry; ordering above is preserved, so an undrawn competition still
        // answers openEntry.phaseNotDrawn first.
        if (phase.Draw.Status != "accepted")
        {
            return Result<EntryOpened>.Failure(
                "entry.drawNotAccepted",
                "The draw has not been accepted — the competition cannot begin yet.");
        }

        var round = phase.Rounds.FirstOrDefault(r => r.Ordinal == roundOrdinal);
        if (round is null)
        {
            return Result<EntryOpened>.Failure(
                "openEntry.roundNotFound", "No round with this ordinal in this phase.");
        }

        var taskRound = round.TaskRounds.FirstOrDefault(tr => tr.Ordinal == taskRoundOrdinal);
        if (taskRound is null)
        {
            return Result<EntryOpened>.Failure(
                "openEntry.taskRoundNotFound", "No task-round with this ordinal in this round.");
        }

        var group = taskRound.Groups.FirstOrDefault(g => g.Id == groupRef);
        if (group is null)
        {
            return Result<EntryOpened>.Failure(
                "openEntry.groupNotFound", "No group with this id in this task-round.");
        }

        if (taskRound.State is TaskRoundState.Complete or TaskRoundState.Annulled)
        {
            return Result<EntryOpened>.Failure(
                "openEntry.taskRoundClosed", "This task-round is complete or annulled.");
        }

        // D5 (reflight-aggregate-destination.md): enforced for Original-role
        // opens only. A reflight-role entry may be opened into any group of
        // the addressed task-round — the CD's allocation is the act, and
        // Group.CompetitorRefs remains "the drawn allocation, not who flew".
        if (role == ReflightRole.Original && !group.CompetitorRefs.Contains(competitorRef))
        {
            return Result<EntryOpened>.Failure(
                "openEntry.competitorNotDrawn", "This competitor was not drawn into this group.");
        }

        // Registration is now checked explicitly, for every role: the
        // relaxation above removed the drawn-check that implicitly guaranteed
        // it for reflight-role opens.
        var competitor = Competitors.FirstOrDefault(c => c.Id == competitorRef);
        if (competitor is null)
        {
            return Result<EntryOpened>.Failure(
                "openEntry.competitorNotRegistered", "This competitor is not registered in this competition.");
        }

        if (competitor.WithdrawnAt is not null)
        {
            return Result<EntryOpened>.Failure(
                "openEntry.competitorWithdrawn", "This competitor has withdrawn.");
        }

        // TaskRound.TaskRef is the task's Code, "the only stable handle" to
        // reference it (Competition.cs, TaskRound's doc comment) — so the
        // task is found by scanning every phase's declared tasks, not by
        // indexing into AdoptedRules.Definition.Phases with this runtime
        // phaseOrdinal: the two numbering schemes are unrelated (see
        // DrawPhase's own note on PhaseDefinition.Ordinal above).
        var task = AdoptedRules.Definition.Phases
            .SelectMany(p => p.Tasks)
            .First(t => t.Code == taskRound.TaskRef);

        if (task.Timing.Kind == WorkingTimeKind.Fixed)
        {
            if (task.Timing.WorkingTime is not { } declaredWorkingTime)
            {
                return Result<EntryOpened>.Failure(
                    "openEntry.workingTimeUndeclared",
                    "This task's timing is Fixed but declares no WorkingTime — a definition defect.");
            }

            // Round-scoped binding for THIS round wins over an unscoped one,
            // per kanban/completed/per-round-parameter-bindings-plan.md.
            var bindings = ScoringService.FlattenParameterBindings(ParameterBindings, phaseOrdinal, roundOrdinal);

            try
            {
                // Validation only, deliberately: the resolved seconds are discarded. The
                // call exists for its two failure modes — openEntry.workingTimeUndeclared
                // above, and openEntry.parameterUnbound here, which is what forces a
                // CD-parameter working time (F5K) to be bound before entries can open.
                // No window is stored: remove-stored-working-time.md.
                _ = ParameterResolver.Resolve(
                    declaredWorkingTime, bindings, AdoptedRules.Definition.Parameters);
            }
            catch (UnresolvedParameterException ex)
            {
                return Result<EntryOpened>.Failure("openEntry.parameterUnbound", ex.Message);
            }
        }

        // The make-up validations (reflight-aggregate-destination.md WI-2,
        // D6/D4), in the story's order, only when a counts-for round is
        // supplied — null means the entry's own round and none of this fires.
        if (countsForRoundOrdinal is { } countsFor)
        {
            if (role == ReflightRole.Original)
            {
                return Result<EntryOpened>.Failure(
                    "openEntry.destinationOnOriginalRole",
                    "An Original entry always counts for its own round; a counts-for round belongs to a reflight-role entry.");
            }

            if (!phase.Rounds.Any(r => r.Ordinal == countsFor))
            {
                return Result<EntryOpened>.Failure(
                    "openEntry.destinationNotFound",
                    $"No round with ordinal {countsFor} exists in this phase — the counts-for round must be a drawn round of the same phase.");
            }

            if (countsFor < 1 || countsFor >= roundOrdinal)
            {
                return Result<EntryOpened>.Failure(
                    "openEntry.destinationNotEarlier",
                    $"The counts-for round ({countsFor}) must be an earlier round than this entry's own ({roundOrdinal}) — a make-up counts for a round the competitor missed.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return Result<EntryOpened>.Failure(
                    "openEntry.reasonRequired",
                    "A reason is required — it is the recorded entitlement ruling, not an audit breadcrumb.");
            }
        }

        return Result<EntryOpened>.Success(new EntryOpened(
            id,
            Id,
            phaseOrdinal,
            roundOrdinal,
            taskRoundOrdinal,
            groupRef,
            competitorRef,
            role,
            at,
            countsForRoundOrdinal,
            reason));
    }

    // Task-round lifecycle decide functions — WI-1/WI-2/WI-2b
    // (kanban/completed/task-round-lifecycle.md). Defect-chain style, like
    // RegisterCompetitor/WithdrawCompetitor: no later check needs a value an
    // earlier one computed, only the task-round they all share.
    //
    // Deliberately absent from all three: any "has every group flown" check
    // (Competition holds no Entry data, and the CD is the authority on when a
    // round's scores are in) and any ordering check across rounds — rounds are
    // not required to complete in order, or at all (NFR-4). None of the three
    // is ever emitted as a side effect of anything; only its own command
    // produces it.

    public Result<TaskRoundCompleted> CompleteTaskRound(
        int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal, DateTimeOffset at)
    {
        var taskRound = FindTaskRound(phaseOrdinal, roundOrdinal, taskRoundOrdinal);

        var defect = TaskRoundFound(taskRound, "completeTaskRound")
            ?? (taskRound!.State switch
            {
                TaskRoundState.Complete => new Defect(
                    "completeTaskRound.alreadyComplete", "$.taskRoundOrdinal",
                    "This task-round is already complete."),

                // An annulment is a resolution, not a way-station: an annulled
                // task-round is reopened first, never completed in place.
                TaskRoundState.Annulled => new Defect(
                    "completeTaskRound.annulled", "$.taskRoundOrdinal",
                    "This task-round is annulled; reopen it before completing it."),
                _ => null,
            });

        return defect is not null
            ? Result<TaskRoundCompleted>.Failure(defect.Code, defect.Message)
            : Result<TaskRoundCompleted>.Success(
                new TaskRoundCompleted(phaseOrdinal, roundOrdinal, taskRoundOrdinal, at));
    }

    /// <summary>
    /// A Complete task-round MAY be annulled — the reverse of
    /// <see cref="CompleteTaskRound"/>'s rule: a round read out and then found
    /// faulty is the ordinary case. <paramref name="reason"/> is validated here
    /// rather than in the handler (unlike BindParameter's <c>By</c>) because it
    /// is a substantive record of a ruling, not an audit breadcrumb.
    /// </summary>
    public Result<TaskRoundAnnulled> AnnulTaskRound(
        int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal, string reason, DateTimeOffset at)
    {
        var taskRound = FindTaskRound(phaseOrdinal, roundOrdinal, taskRoundOrdinal);

        var defect = TaskRoundFound(taskRound, "annulTaskRound")
            ?? (taskRound!.State is TaskRoundState.Annulled
                ? new Defect("annulTaskRound.alreadyAnnulled", "$.taskRoundOrdinal", "This task-round is already annulled.")
                : null)
            ?? ReasonGiven(reason, "annulTaskRound");

        return defect is not null
            ? Result<TaskRoundAnnulled>.Failure(defect.Code, defect.Message)
            : Result<TaskRoundAnnulled>.Success(
                new TaskRoundAnnulled(phaseOrdinal, roundOrdinal, taskRoundOrdinal, reason, at));
    }

    /// <summary>
    /// Complete → Drawn and Annulled → Drawn are BOTH permitted: an annulment
    /// made in error is as correctable as a premature completion, and refusing
    /// the second would reintroduce exactly the dead end this event exists to
    /// remove.
    /// <para>
    /// Reopening does not touch any <see cref="Finalisation"/>. A competition
    /// finalised and then reopened at the round level will have a declared
    /// result that no longer matches the derived one — that divergence being
    /// visible is the entire point of storing <see cref="DeclaredResult"/>s.
    /// </para>
    /// </summary>
    public Result<TaskRoundReopened> ReopenTaskRound(
        int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal, string reason, DateTimeOffset at)
    {
        var taskRound = FindTaskRound(phaseOrdinal, roundOrdinal, taskRoundOrdinal);

        var defect = TaskRoundFound(taskRound, "reopenTaskRound")
            ?? (taskRound!.State is TaskRoundState.Complete or TaskRoundState.Annulled
                ? null
                : new Defect("reopenTaskRound.notClosed", "$.taskRoundOrdinal", $"This task-round is {taskRound.State}; there is nothing to reopen."))
            ?? ReasonGiven(reason, "reopenTaskRound");

        return defect is not null
            ? Result<TaskRoundReopened>.Failure(defect.Code, defect.Message)
            : Result<TaskRoundReopened>.Success(
                new TaskRoundReopened(phaseOrdinal, roundOrdinal, taskRoundOrdinal, reason, at));
    }

    // Instance decide function — WI-2 (kanban/in-progress/reflight-groups.md).
    // Early-return style, like DrawPhase/OpenEntry: later checks need the rule
    // and the resolved MinNewGroupSize that earlier ones computed, and the
    // happy path needs the resolved rule again to build the event.
    //
    // Deliberately absent (mirroring the lifecycle functions' own omissions):
    // any check that a member already flew this task-round, that an entitled
    // member exists, or that a member already holds a reflight entry —
    // Competition holds no Entry data (its own design note above, OpenEntry);
    // entitlement and membership are CD rulings recorded as `Reason` and as
    // Entry `Role`s, and the duplicate-reflight guard is WI-5's, on the Entry
    // side, where the data lives.
    public Result<ReflightGroupAppended> AppendReflightGroup(
        int phaseOrdinal,
        int roundOrdinal,
        int taskRoundOrdinal,
        ImmutableArray<CompetitorId> members,
        string reason,
        DateTimeOffset at)
    {
        var taskRound = FindTaskRound(phaseOrdinal, roundOrdinal, taskRoundOrdinal);
        if (taskRound is null)
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.taskRoundNotFound",
                "No task-round at these phase/round/task-round ordinals.");
        }

        // Annulled refuses; Complete/Drawn/InProgress all allow — the
        // protest-driven reflight after a round is read out is the ordinary
        // late case (planner's call, reflight-groups.md).
        if (taskRound.State is TaskRoundState.Annulled)
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.taskRoundAnnulled",
                "This task-round is annulled; reopen it before appending a reflight group.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.reasonRequired",
                "A reason is required — it is the recorded entitlement ruling, not an audit breadcrumb.");
        }

        // TaskRound.TaskRef is the task's Code — the one stable handle (see
        // OpenEntry's own note). The class-level default, overridden per-task
        // where the task declares one (F19).
        var rule = ResolveReflightRule(taskRound.TaskRef);

        if (rule.MinNewGroupSize is null)
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.newGroupNeverFormed",
                "This class never forms new reflight groups; the re-flyer rejoins the running order (F26).");
        }

        // Belt and braces: a hypothetical class declaring both a non-null min
        // and a NotPermitted selection. The seed corpus's null-min classes
        // (F3F.1.5, NZ N/P) ordinarily refuse above already.
        if (rule.EntitledScores is ReflightSelection.NotPermitted
            || rule.OthersScore is ReflightSelection.NotPermitted)
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.notPermitted",
                "This class permits no re-flight at all.");
        }

        var bindings = ScoringService.FlattenParameterBindings(ParameterBindings, phaseOrdinal, roundOrdinal);

        decimal resolvedMin;
        try
        {
            resolvedMin = ParameterResolver.Resolve(rule.MinNewGroupSize, bindings, AdoptedRules.Definition.Parameters);
        }
        catch (UnresolvedParameterException ex)
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.parameterUnbound", ex.Message);
        }

        if (members.IsEmpty)
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.membersEmpty",
                "A reflight group must name at least one member.");
        }

        if (members.Any(m => !Competitors.Any(c => c.Id == m)))
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.memberNotRegistered",
                "A reflight group member is not a registered competitor in this competition.");
        }

        if (members.Any(m => Competitors.First(c => c.Id == m).WithdrawnAt is not null))
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.memberWithdrawn",
                "A reflight group member has withdrawn.");
        }

        if (members.Distinct().Count() != members.Length)
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.memberDuplicated",
                "A reflight group names the same competitor more than once.");
        }

        var minNewGroupSize = (int)resolvedMin;
        if (members.Length < minNewGroupSize)
        {
            return Result<ReflightGroupAppended>.Failure(
                "appendReflightGroup.groupTooSmall",
                $"A reflight group needs at least {minNewGroupSize} members; got {members.Length}.");
        }

        var group = new Group
        {
            Id = GroupId.New(),
            Ordinal = taskRound.Groups.Length + 1,
            CompetitorRefs = members,
        };

        return Result<ReflightGroupAppended>.Success(
            new ReflightGroupAppended(phaseOrdinal, roundOrdinal, taskRoundOrdinal, group, reason, at));
    }

    private TaskRound? FindTaskRound(int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal) =>
        Phases.FirstOrDefault(p => p.Ordinal == phaseOrdinal)
            ?.Rounds.FirstOrDefault(r => r.Ordinal == roundOrdinal)
            ?.TaskRounds.FirstOrDefault(tr => tr.Ordinal == taskRoundOrdinal);

    // Both draw-lifecycle commands act on THE LIVE PHASE — the single element
    // of Phases (kanban/in-progress/draw-acceptance-redraw.md, P1: at most one).
    private Phase? LivePhase() =>
        Phases.IsEmpty ? null : Phases.Single();

    // One code per command rather than one shared code, so a caller can tell
    // which command rejected without reading the message — TaskRoundFound's
    // rule.
    private static Defect? DrawnPhaseFound(Phase? phase, string command) =>
        phase is null
            ? new Defect($"{command}.noDrawnPhase", "$.competitionId", "No phase has been drawn for this competition.")
            : null;

    // The task-scan + class-default fallback shared by AppendReflightGroup and
    // RecordReflightRuling (reflight-scoring-rulings.md WI-2): the class-level
    // ReflightRule, overridden per-task where the task declares one (F19).
    private ReflightRule ResolveReflightRule(string taskCode) =>
        AdoptedRules.Definition.Phases
            .SelectMany(p => p.Tasks)
            .First(t => t.Code == taskCode)
            .Reflight ?? AdoptedRules.Definition.Reflight;

    // One code per command rather than one shared code, so a caller can tell
    // which command rejected without reading the message.
    private static Defect? TaskRoundFound(TaskRound? taskRound, string command) =>
        taskRound is null
            ? new Defect($"{command}.taskRoundNotFound", "$.taskRoundOrdinal", "No task-round at these phase/round/task-round ordinals.")
            : null;

    private static Defect? ReasonGiven(string reason, string command) =>
        string.IsNullOrWhiteSpace(reason)
            ? new Defect($"{command}.reasonRequired", "$.reason", "A reason is required — it is the recorded ruling, not an audit breadcrumb.")
            : null;

    // Instance decide function — WI-3 (kanban/completed/task-round-lifecycle.md).
    // DrawPhase's early-return style, not the Defect chain above: the validity
    // check needs the resolved MinRounds and the per-phase completed-round
    // counts, and the happy path needs neither again but the checks build on
    // each other.
    //
    // The gate is entirely data-driven off PhaseDefinition.Validity — the row
    // that varies across classes (F3J 4, F3K 5, F5J 4, F5L 4, F3B "1 round +
    // 1 task", F5K a CD parameter) is a field of the class model, never a
    // branch here (CLAUDE.md's core architectural law).
    //
    // DeclaredResults arrives already computed: the handler scores the
    // competition and maps the result, exactly as this aggregate receives an
    // already-resolved AdoptedRules rather than reaching out for one.
    public Result<Finalised> Finalise(ImmutableArray<DeclaredResult> declaredResults, string by, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(by))
        {
            return Result<Finalised>.Failure(
                "finalise.byRequired", "By is required — a self-declared CD name, not an authorisation claim.");
        }

        // Finalisation.DeclaredResults is 1..*.
        if (declaredResults.IsEmpty)
        {
            return Result<Finalised>.Failure(
                "finalise.noResults", "A finalisation must declare at least one result.");
        }

        if (Finalisations.Any(f => f.Scope == FinalisationScope.Competition))
        {
            return Result<Finalised>.Failure(
                "finalise.alreadyFinalised", "This competition has already been finalised.");
        }

        // No round context: MinRounds is a phase-level datum, exactly as
        // DrawPhase resolves MinPerGroup with no round context.
        var bindings = ScoringService.FlattenParameterBindings(ParameterBindings);

        // The plan (WI-3) predicted this needed no Phases.IsEmpty check —
        // "an undrawn competition has zero complete rounds and fails
        // notEnoughRounds". That reasoning was wrong and the tests caught it:
        // the gate below is a loop over Phases, so with no phases the body
        // never runs and the class's MinRounds is never consulted. Same defect
        // code, because it is the same fact about the same competition: it has
        // flown nothing.
        if (Phases.IsEmpty)
        {
            return Result<Finalised>.Failure(
                "finalise.notEnoughRounds", "No phase has been drawn, so no round has been flown to a result.");
        }

        foreach (var phase in Phases)
        {
            // Positional index, matching ScoringService — Phase.Ordinal is the
            // 0-based position PhaseDrawn assigned it, NOT a lookup by
            // PhaseDefinition.Ordinal, which is the rulebook's own (often
            // 1-based) numbering and an unrelated scheme. See DrawPhase's
            // fuller note on the same distinction.
            var validity = AdoptedRules.Definition.Phases[phase.Ordinal].Validity;

            decimal minRounds;
            try
            {
                minRounds = ParameterResolver.Resolve(validity.MinRounds, bindings, AdoptedRules.Definition.Parameters);
            }
            catch (UnresolvedParameterException ex)
            {
                return Result<Finalised>.Failure("finalise.parameterUnbound", $"Phase {phase.Ordinal}: {ex.Message}");
            }

            // IsFullyFlown, not IsCompleteOrAnnulled: an annulled round
            // resolved the competition's progress but produced no result, so
            // it cannot make a contest valid. WI-0 gave the two questions two
            // names so this gate cannot pick up the wrong one.
            var flownRounds = phase.Rounds.Where(r => r.IsFullyFlown).ToImmutableArray();

            if (flownRounds.Length < minRounds)
            {
                return Result<Finalised>.Failure(
                    "finalise.notEnoughRounds",
                    $"Phase {phase.Ordinal}: {flownRounds.Length} round(s) flown to a result, but the class requires {minRounds}.");
            }

            if (validity.MinTasks is { } minTasks)
            {
                var distinctTasks = flownRounds
                    .SelectMany(r => r.TaskRounds)
                    .Select(tr => tr.TaskRef)
                    .Distinct()
                    .Count();

                if (distinctTasks < minTasks)
                {
                    return Result<Finalised>.Failure(
                        "finalise.notEnoughTasks",
                        $"Phase {phase.Ordinal}: {distinctTasks} distinct task(s) flown to a result, but the class requires {minTasks}.");
                }
            }
        }

        var finalisation = new Finalisation
        {
            Scope = FinalisationScope.Competition,
            // Always 1 this thread — alreadyFinalised above rejects a second.
            // Written generically so reopening (revision >= 2) needs no change
            // here when it lands.
            Revision = Finalisations.Count(f => f.Scope == FinalisationScope.Competition) + 1,
            By = by,
            At = at,
            DeclaredResults = declaredResults,
        };

        return Result<Finalised>.Success(new Finalised(finalisation));
    }

    // Instance decide function — WI-4 (kanban/in-progress/annul-and-penalise-the-second-entry-thread.md).
    // Defect-chain style, like WithdrawCompetitor: no later check needs a value
    // an earlier one computed. Reads AdoptedRules.Definition.Penalties from its
    // own state — the same self-service read OpenEntry makes of AdoptedRules;
    // unlike Entry, Competition holds the adopted rules. Deliberately no
    // finalisation gate (an aggregate penalty is a ruling about recorded
    // results, and the ordinary case is a protest after the contest looked
    // finished — NFR-4's world, the same stance as decision 6's annulment
    // note) and no withdrawn check (decision 8: withdrawal leaves scores
    // intact, so an aggregate deduction against a withdrawn competitor's
    // accumulated score still deducts).
    public Result<PenaltyRecorded> RecordPenalty(Penalty penalty)
    {
        var defect = ValidatePenaltyScope(penalty)
            ?? ValidateCompetitorSubject(penalty)
            ?? ValidateCompetitorExists(penalty.CompetitorRef!.Value)
            ?? ValidateTaskRoundCoordinate(penalty.TaskRound, "recordPenalty")
            ?? ValidateInfractionType(penalty.InfractionType)
            ?? ValidateByNotBlank(penalty.By, "recordPenalty");

        return defect is not null
            ? Result<PenaltyRecorded>.Failure(defect.Code, defect.Message)
            : Result<PenaltyRecorded>.Success(new PenaltyRecorded(penalty));
    }

    private static Defect? ValidatePenaltyScope(Penalty penalty) =>
        penalty.Scope is PenaltyScope.TaskRound or PenaltyScope.Competition
            ? null
            : new Defect("recordPenalty.wrongScope", "$.scope",
                $"A penalty recorded against a Competition must be TaskRound or Competition scoped; got {penalty.Scope}.");

    private static Defect? ValidateCompetitorSubject(Penalty penalty) =>
        penalty.CompetitorRef is null
            ? new Defect("recordPenalty.competitorRequired", "$.competitorRef",
                "A TaskRound/Competition-scoped penalty must name the competitor it is against.")
            : null;

    // Code-prefix parameter, not a hardcoded "recordPenalty.*": the coordinate
    // navigation is shared with RecordReflightRuling
    // (reflight-scoring-rulings.md WI-2), and each command keeps its own code.
    private Defect? ValidateTaskRoundCoordinate(TaskRoundCoordinate? coordinate, string command)
    {
        if (coordinate is null)
        {
            return null;
        }

        var phase = Phases.FirstOrDefault(p => p.Ordinal == coordinate.PhaseOrdinal);
        if (phase is null)
        {
            return new Defect($"{command}.taskRoundNotFound", "$.taskRound",
                $"No phase has been drawn with ordinal {coordinate.PhaseOrdinal}.");
        }

        var round = phase.Rounds.FirstOrDefault(r => r.Ordinal == coordinate.RoundOrdinal);
        if (round is null)
        {
            return new Defect($"{command}.taskRoundNotFound", "$.taskRound",
                $"No round with ordinal {coordinate.RoundOrdinal} in phase {coordinate.PhaseOrdinal}.");
        }

        var taskRound = round.TaskRounds.FirstOrDefault(tr => tr.Ordinal == coordinate.TaskRoundOrdinal);
        if (taskRound is null)
        {
            return new Defect($"{command}.taskRoundNotFound", "$.taskRound",
                $"No task-round with ordinal {coordinate.TaskRoundOrdinal} in round {coordinate.RoundOrdinal}.");
        }

        return null;
    }

    private Defect? ValidateInfractionType(string infractionType) =>
        AdoptedRules.Definition.Penalties.Any(d => d.InfractionType == infractionType)
            ? null
            : new Defect("recordPenalty.infractionTypeNotDeclared", "$.infractionType",
                $"'{infractionType}' is not an infraction type declared by the adopted class definition.");

    private static Defect? ValidateByNotBlank(string? by, string command) =>
        by is not null && string.IsNullOrWhiteSpace(by)
            ? new Defect($"{command}.byBlank", "$.by",
                "By, when supplied, must not be blank — an absent By is fine, a blank one is a typo.")
            : null;

    // Instance decide function — WI-2 (reflight-scoring-rulings.md). Defect-chain
    // style, like RecordPenalty beside it: no later check needs a value an
    // earlier one computed beyond what FindTaskRound already returns. Validates
    // the ruling against the adopted class's RESOLVED ReflightRule — data, never
    // a branch on class (CLAUDE.md's core architectural law).
    //
    // Deliberately absent (lifecycle-function style):
    //   - No entry-existence or pair-shape check (planner's call 3, NFR-4): a
    //     ruling may precede capture (the CD rules at the incident) or follow
    //     it (scoring recomputes per query). A ruling whose pair of entries
    //     never materialises simply never matches a candidate pair.
    //   - No uniqueness check (decision 2): re-recording for the same
    //     (task-round, competitor) supersedes — the most recently logged ruling
    //     is the effective one, and the log keeps every decision.
    //   - No per-role necessity check in mixed classes (planner's call 4): where
    //     exactly one slot is silent (F3F), Competition holds no entry data, so
    //     it cannot know the competitor's role; such a ruling may turn out inert,
    //     and scoring ignores it by RR1.
    public Result<ReflightRulingRecorded> RecordReflightRuling(ReflightRuling ruling)
    {
        var taskRound = FindTaskRound(
            ruling.TaskRound.PhaseOrdinal, ruling.TaskRound.RoundOrdinal, ruling.TaskRound.TaskRoundOrdinal);

        var defect = ValidateSelectionIsAResolution(ruling.Selection)
            ?? ReasonGiven(ruling.Reason, "recordReflightRuling")
            ?? ValidateByNotBlank(ruling.By, "recordReflightRuling")
            ?? ValidateTaskRoundCoordinate(ruling.TaskRound, "recordReflightRuling")
            ?? ValidateTaskRoundNotAnnulled(taskRound)
            ?? ValidateRulingCompetitorRegistered(ruling.CompetitorRef)
            ?? ValidateClassRuleSilent(taskRound);

        return defect is not null
            ? Result<ReflightRulingRecorded>.Failure(defect.Code, defect.Message)
            : Result<ReflightRulingRecorded>.Success(new ReflightRulingRecorded(ruling));
    }

    private static Defect? ValidateSelectionIsAResolution(ReflightSelection selection) =>
        selection is ReflightSelection.Replacement or ReflightSelection.BetterOf
            ? null
            : new Defect("recordReflightRuling.selectionNotAResolution", "$.selection",
                $"A ruling must decide: Replacement or BetterOf. '{selection}' asserts what the " +
                "rulebook forbids or where it is silent — neither is a decision.");

    private Defect? ValidateTaskRoundNotAnnulled(TaskRound? taskRound) =>
        taskRound is { State: TaskRoundState.Annulled }
            ? new Defect("recordReflightRuling.taskRoundAnnulled", "$.taskRound",
                "This task-round is annulled; nothing scores there, so there is nothing to rule on.")
            : null;

    // Typo protection only: a ruling keyed to nobody would silently never
    // apply. Withdrawal is NOT checked — AppendReflightGroup refuses withdrawn
    // members because it forms a group; a ruling does not, and a moot ruling
    // for a withdrawn competitor is inert, not harmful (planner's call 2).
    private Defect? ValidateRulingCompetitorRegistered(CompetitorId competitorRef) =>
        Competitors.Any(c => c.Id == competitorRef)
            ? null
            : new Defect("recordReflightRuling.competitorNotFound", "$.competitorRef",
                "No such competitor in this competition.");

    // Decision 3: where BOTH resolved slots are concrete — Replacement,
    // BetterOf or NotPermitted, e.g. F3K, F5J — the rulebook governs and there
    // is nothing to fill. Accepting a ruling there would let a CD believe they
    // settled something that had no effect. Classes with at least one silent
    // slot stay acceptable; a ruling against the one speaking slot in a mixed
    // class (F3F) is accepted here and ignored at scoring by RR1.
    private Defect? ValidateClassRuleSilent(TaskRound? taskRound)
    {
        if (taskRound is null)
        {
            return null;
        }

        var rule = ResolveReflightRule(taskRound.TaskRef);
        return rule.EntitledScores is ReflightSelection.UndefinedRequiresRuling
               || rule.OthersScore is ReflightSelection.UndefinedRequiresRuling
            ? null
            : new Defect("recordReflightRuling.classRuleSpeaks", "$.selection",
                "The adopted class rules state which of a competitor's attempts counts here; " +
                "there is no silence for a ruling to fill.");
    }

    // Compares against *all* competitors, including withdrawn ones — a
    // withdrawal is not a re-entry ticket (invariant 1, the plan's Context).
    private Defect? ValidateNotAlreadyRegistered(PersonId personRef) =>
        Competitors.Any(c => c.PersonRef == personRef)
            ? new Defect("competition.competitor.alreadyRegistered", "$.personRef", "This person is already registered in this competition.")
            : null;

    // Frozen means the live draw is ACCEPTED, not merely drawn (D6,
    // kanban/in-progress/draw-acceptance-redraw.md): a competitor who turns up
    // after the draw can still be registered until the CD stands behind it.
    // Withdrawal stays ungated forever (WithdrawCompetitor's note above;
    // aggregate-roots.md §3's field-freeze note unchanged). Shares
    // HasAnAcceptedDraw with ValidateParameterNotFrozen so the two cannot
    // drift; they stay separate checks — they ask different questions (is the
    // field closed, vs is this parameter settled) and keep separate codes.
    private Defect? ValidateFieldNotFrozen() =>
        HasAnAcceptedDraw()
            ? new Defect("competition.field.frozen", "$.personRef", "The field is frozen: the draw has been accepted.")
            : null;

    private Defect? ValidateCompetitorExists(CompetitorId competitorRef) =>
        Competitors.Any(c => c.Id == competitorRef)
            ? null
            : new Defect("competition.competitor.notFound", "$.competitorRef", "No such competitor in this competition.");

    private Defect? ValidateNotAlreadyWithdrawn(CompetitorId competitorRef) =>
        Competitors.Single(c => c.Id == competitorRef).WithdrawnAt is not null
            ? new Defect("competition.competitor.alreadyWithdrawn", "$.competitorRef", "This competitor has already withdrawn.")
            : null;

    private Defect? ValidateParameterDeclared(string parameterName) =>
        AdoptedRules.Definition.Parameters.Any(p => p.Name == parameterName)
            ? null
            : new Defect("competition.parameter.notDeclared", "$.parameterName", $"'{parameterName}' is not a parameter of the adopted class.");

    private Defect? ValidateParameterKind(string parameterName, MeasuredValue value)
    {
        var parameter = AdoptedRules.Definition.Parameters.FirstOrDefault(p => p.Name == parameterName);
        return parameter is not null && value.Kind != parameter.Kind
            ? new Defect("competition.parameter.kindMismatch", "$.value", $"'{parameterName}' expects a {parameter.Kind} value, not {value.Kind}.")
            : null;
    }

    private Defect? ValidateParameterValueAllowed(string parameterName, MeasuredValue value)
    {
        var parameter = AdoptedRules.Definition.Parameters.FirstOrDefault(p => p.Name == parameterName);
        return parameter is not null && !parameter.AllowedValues.IsEmpty && !parameter.AllowedValues.Contains(value)
            ? new Defect("competition.parameter.valueNotAllowed", "$.value", $"The bound value is not one of '{parameterName}''s allowed values.")
            : null;
    }

    // See ValidateFieldNotFrozen above for the other consumer of
    // HasAnAcceptedDraw — this asks whether THIS PARAMETER is settled, not
    // whether the field is closed, and keeps its own code for it. Frozen at
    // acceptance too (D7, kanban/in-progress/draw-acceptance-redraw.md):
    // rebinding minPerGroup between reject and redraw may be precisely why
    // the CD rejected. Scoped to CompetitionSetup only: a BeforeFlying
    // parameter (e.g. F5K's nlh) is legitimately bound even after acceptance.
    private Defect? ValidateParameterNotFrozen(string parameterName)
    {
        var parameter = AdoptedRules.Definition.Parameters.FirstOrDefault(p => p.Name == parameterName);
        return parameter is not null && parameter.BoundAt == ParameterBindingPoint.CompetitionSetup && HasAnAcceptedDraw()
            ? new Defect("competition.parameter.frozen", "$.parameterName", $"'{parameterName}' is bound at competition setup and cannot be changed once the draw has been accepted.")
            : null;
    }

    // One accepted-draw check shared by both freeze validators (D6/D7) so the
    // two cannot drift — kanban/in-progress/draw-acceptance-redraw.md.
    private bool HasAnAcceptedDraw() =>
        Phases.Any(p => p.Draw.Status == "accepted");

    // kanban/completed/per-round-parameter-bindings-plan.md. phaseOrdinal/roundOrdinal
    // null-null means an unscoped bind — every parameter, PerRound or not, may
    // still be bound unscoped (Appendix A's resolution order falls back to it).
    // Only a genuinely round-scoped bind (both given) triggers the checks below.
    private Defect? ValidateRoundScope(string parameterName, int? phaseOrdinal, int? roundOrdinal, bool roundHasEntries)
    {
        if (phaseOrdinal is null && roundOrdinal is null)
        {
            return null;
        }

        if (phaseOrdinal is null || roundOrdinal is null)
        {
            return new Defect(
                "competition.parameter.roundScopeIncomplete", "$.roundOrdinal",
                "A round-scoped binding must name both a phase and a round.");
        }

        var parameter = AdoptedRules.Definition.Parameters.FirstOrDefault(p => p.Name == parameterName);
        if (parameter is not null && parameter.BoundAt != ParameterBindingPoint.PerRound)
        {
            return new Defect(
                "competition.parameter.roundScopeNotPermitted", "$.roundOrdinal",
                $"'{parameterName}' is not bound per round; a round-scoped binding is not permitted.");
        }

        var round = Phases.FirstOrDefault(p => p.Ordinal == phaseOrdinal)?.Rounds.FirstOrDefault(r => r.Ordinal == roundOrdinal);
        if (round is null)
        {
            return new Defect(
                "competition.parameter.roundNotFound", "$.roundOrdinal",
                $"No round {roundOrdinal} in phase {phaseOrdinal} has been drawn.");
        }

        // Single(): every round has exactly one task-round today — multi-task
        // rounds (F3B) are a separate, still-deferred thread (DrawPhase's
        // drawPhase.unsupportedRoundComposition).
        var taskRound = round.TaskRounds.Single();
        var task = AdoptedRules.Definition.Phases.SelectMany(p => p.Tasks).First(t => t.Code == taskRound.TaskRef);

        if (!ParameterResolver.TaskReferencesParameter(task, parameterName))
        {
            return new Defect(
                "competition.parameter.notConsumedByTask", "$.parameterName",
                $"Round {roundOrdinal}'s task ('{task.Code}') does not consume '{parameterName}'.");
        }

        // The freeze rule, in the two halves it was always meant to have:
        // the round is closed (per-round-parameter-bindings-plan.md's original
        // approximation), or flying has actually started in it. The second
        // half closed that plan's recorded gap — a rebind after a flight had
        // opened but before the round was marked complete used to be silently
        // accepted (task-round-lifecycle.md WI-9). Two codes, not one: they
        // are different situations and a CD can act on the difference.
        if (taskRound.State != TaskRoundState.Drawn)
        {
            return new Defect(
                "competition.parameter.roundFrozen", "$.roundOrdinal",
                $"Round {roundOrdinal} is {taskRound.State}; a round-scoped binding can no longer be made.");
        }

        if (roundHasEntries)
        {
            return new Defect(
                "competition.parameter.roundInProgress", "$.roundOrdinal",
                $"Round {roundOrdinal} has flights already opened; a round-scoped binding can no longer be made.");
        }

        return null;
    }
}
