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
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;

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

/// <summary>An entity inside this aggregate; referenced by id from the Entry aggregate.</summary>
public readonly record struct CompetitorId(Guid Value)
{
    public static CompetitorId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

/// <summary>
/// An entity inside this aggregate; referenced by id from the Entry aggregate.
/// Group membership is not stored on the Group itself — it is the set of
/// Entries whose GroupRef points at it (aggregate-roots.md §3).
/// </summary>
public readonly record struct GroupId(Guid Value)
{
    public static GroupId New() => new(Guid.CreateVersion7());

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

    public Competition Apply(RulesAmended @event) =>
        this with { RulesAmendments = RulesAmendments.Add(@event.Amendment) };

    public Competition Apply(ParameterBound @event) =>
        this with { ParameterBindings = ParameterBindings.Add(@event.Binding) };

    public Competition Apply(Finalised @event) =>
        this with { Finalisations = Finalisations.Add(@event.Finalisation) };

    public Competition Apply(PenaltyRecorded @event) =>
        this with { Penalties = Penalties.Add(@event.Penalty) };

    /// <summary>
    /// Shared navigation for ReflightGroupAppended, TaskRoundCompleted and
    /// TaskRoundAnnulled: find the Phase/Round/TaskRound by ordinal — never by
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
            RulesAmended e => Require(current, e).Apply(e),
            ParameterBound e => Require(current, e).Apply(e),
            Finalised e => Require(current, e).Apply(e),
            PenaltyRecorded e => Require(current, e).Apply(e),
            _ => throw new ArgumentException($"Unknown CompetitionEvent subtype: {@event.GetType().Name}"),
        };

    private static Competition Require(Competition? current, CompetitionEvent @event) =>
        current ?? throw new ArgumentException($"{@event.GetType().Name} folded with no current projection — a Competition must begin with CompetitionCreated.");

    // Decide functions — WI-2 (docs/plans/create-competition-steel-thread-plan.md).
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

    // Instance decide functions — WI-1 (docs/plans/register-competitor-steel-thread-plan.md).
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

    // Compares against *all* competitors, including withdrawn ones — a
    // withdrawal is not a re-entry ticket (invariant 1, the plan's Context).
    private Defect? ValidateNotAlreadyRegistered(PersonId personRef) =>
        Competitors.Any(c => c.PersonRef == personRef)
            ? new Defect("competition.competitor.alreadyRegistered", "$.personRef", "This person is already registered in this competition.")
            : null;

    // Unreachable this thread — Phases is always empty because no command
    // produces PhaseDrawn yet. Written anyway, the same way CreateCompetition's
    // retirement check was written against a state nothing could yet produce.
    // "Accepted" currently means "any phase drawn" because Draw.Status carries
    // no defined value set (Competition.cs:230-234) — revisit this check once
    // it does.
    private Defect? ValidateFieldNotFrozen() =>
        !Phases.IsEmpty
            ? new Defect("competition.field.frozen", "$.personRef", "The field is frozen: a phase has already been drawn.")
            : null;

    private Defect? ValidateCompetitorExists(CompetitorId competitorRef) =>
        Competitors.Any(c => c.Id == competitorRef)
            ? null
            : new Defect("competition.competitor.notFound", "$.competitorRef", "No such competitor in this competition.");

    private Defect? ValidateNotAlreadyWithdrawn(CompetitorId competitorRef) =>
        Competitors.Single(c => c.Id == competitorRef).WithdrawnAt is not null
            ? new Defect("competition.competitor.alreadyWithdrawn", "$.competitorRef", "This competitor has already withdrawn.")
            : null;
}
