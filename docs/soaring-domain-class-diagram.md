# RC Soaring Competitions — Domain Class Diagram

Class diagrams for the RC soaring timing and scoring domain model. Three views:
the compositional spine (Competition down to Measurement), the Competition Class
structure (phases and tasks), and the scoring vocabulary a class composes its
arithmetic from.

The class model carries all per-class variance (NFR-1). Nothing in the spine
branches on which class is being run.

---

## 1. The competition spine

```mermaid
classDiagram
    direction TB

    class Competition {
        <<aggregate root>>
        +string name
        +string location
        +date startDate
        +date endDate
        +string evaluatorVersion
    }

    class AdoptedRules {
        <<value object>>
        +ClassDefinition definition
        +string sourceClassId
        +string sourceVersion
        +timestamp adoptedAt
    }

    class RulesAmendment {
        +ClassDefinition definition
        +string reason
        +string by
        +timestamp at
    }

    class ParameterBinding {
        +string parameterName
        +MeasuredValue boundValue
        +string by
        +timestamp at
    }

    class Finalisation {
        +FinalisationScope scope
        +int revision
        +string by
        +timestamp at
    }

    class DeclaredResult {
        +competitorId competitorRef
        +decimal aggregate
        +int placing
        +bool promoted
    }

    class Person {
        <<aggregate root>>
        +id id
        +string name
        +ContactDetails contact
    }

    class Competitor {
        +personId personRef
        +int competitorNumber
        +timestamp registeredAt
        +timestamp withdrawnAt
    }

    class Phase {
        +PhaseType type
        +int ordinal
    }

    class Draw {
        +timestamp createdAt
        +string status
    }

    class Round {
        +int ordinal
        +isComplete() bool
    }

    class TaskRound {
        +int ordinal
        +TaskRoundState state
        +taskId taskRef
    }

    class Group {
        +int ordinal
    }

    class Entry {
        <<aggregate root>>
        +TimeWindow workingTime
        +ReflightRole role
    }

    class Flight {
        +int sequence
        +timestamp launchAt
    }

    class Measurement {
        +string metric
        +MeasuredValue value
        +timestamp capturedAt
    }

    class MeasuredValue {
        <<value object>>
        +MeasuredKind kind
        +decimal number
        +bool flag
    }

    class Amendment {
        +MeasuredValue newValue
        +string reason
        +string by
        +timestamp at
    }

    class Penalty {
        +string infractionType
        +PenaltyScope scope
    }

    class ClubAffiliation {
        <<value object>>
        +string clubName
        +string membershipNumber
    }

    class CompetitionClass {
        <<aggregate root>>
    }

    class PhaseType {
        <<enumeration>>
        Preliminary
        Flyoff
    }

    class TaskRoundState {
        <<enumeration>>
        Drawn
        InProgress
        Complete
        Annulled
    }

    class PenaltyScope {
        <<enumeration>>
        Flight
        Entry
        TaskRound
        Competition
    }

    class MeasuredKind {
        <<enumeration>>
        Number
        Flag
    }

    class ReflightRole {
        <<enumeration>>
        Original
        Entitled
        Filler
    }

    class FinalisationScope {
        <<enumeration>>
        Phase
        Competition
    }

    Competition "1" *-- "1" AdoptedRules : rulebook snapshot
    Competition "1" *-- "0..*" RulesAmendment : corrections
    Competition "1" *-- "0..*" ParameterBinding : choices made
    Competition "1" *-- "0..*" Finalisation : declared results
    Finalisation "1" *-- "1..*" DeclaredResult
    Competition "1" *-- "0..*" Competitor : field
    Competition "1" *-- "1..*" Phase : has
    Competition "1" *-- "0..*" Penalty : records
    Phase "1" *-- "1..*" Round : has
    Round "1" *-- "1..*" TaskRound : has
    TaskRound "1" *-- "1..*" Group : divided into
    Entry "1" *-- "1..*" Flight : contains
    Flight "1" *-- "1..*" Measurement : captures
    Measurement "1" *-- "1" MeasuredValue
    Measurement "1" *-- "0..*" Amendment : corrected by
    Entry "1" *-- "0..*" Penalty : flight / entry scope

    Competitor "*" --> "1" Person : registration of
    Person "1" *-- "0..1" ClubAffiliation : club
    Phase "1" --> "1" Draw : organised by
    Draw "1" --> "2..*" Competitor : allocates
    Draw ..> Group : produces initial
    Entry "*" --> "1" Group : flown in
    Entry "*" --> "1" Competitor : flown by
    TaskRound ..> AdoptedRules : task by id
    DeclaredResult ..> Competitor : for
    AdoptedRules ..> CompetitionClass : adopted from

    note for AdoptedRules "The whole rulebook, copied in at creation. Scoring reads this, never the library class."
    note for Measurement "Raw and append-only; corrections recorded as Amendments"
    note for Entry "A reflight is a second Entry; role decides which one counts"
    note for Finalisation "Captures what was declared; the raw record stays authoritative"

    classDef aggregateRoot fill:#FFE873,stroke:#E5B700,stroke-width:2px,color:#1A1A1A
    classDef external fill:#EEEEEE,stroke:#BDBDBD,stroke-width:1px,color:#555
    cssClass "Competition,Person,Entry" aggregateRoot
    cssClass "CompetitionClass" external
```

---

## 2. Competition Class — structure

The rulebook. An ordered list of phase definitions, each owning its own tasks
and aggregation; the class owns only what is genuinely true of the whole event.

```mermaid
classDiagram
    direction TB

    class CompetitionClass {
        <<aggregate root>>
        +id id
        +string name
        +string faiDesignation
        +string version
    }

    class Parameter {
        +string name
        +MeasuredKind kind
        +MeasuredValue defaultValue
        +MeasuredValue[] allowedValues
        +ParameterBindingPoint boundAt
    }

    class ParameterRef {
        <<value object>>
        +string parameterName
    }
    %% A ParameterRef may stand in for a literal in exactly these thirteen slots,
    %% and nowhere else (validated at adoption):
    %%   TaskTiming.workingTime, TaskTiming.maxLaunches
    %%   ScoreTerm.cap, ScoreTerm.origin
    %%   Band.from, Band.to
    %%   GroupConstraint.minPerGroup
    %%   ValidityRule.minRounds
    %%   PromotionRule.topN, .minGroupSize, .maxGroupSize, .carryPenalties
    %%   ReflightRule.minNewGroupSize
    %% Band.from/.to were added for NZ Class M (F27), whose +1/-1 turning point
    %% is the CD-announced target time rather than a rule constant. Adjacent
    %% bands must still meet, so a parameter bound on one band's `to` and the
    %% next band's `from` must be the SAME parameter — checked at adoption.

    class PhaseDefinition {
        +int ordinal
        +PhaseType type
        +bool mandatory
    }

    class RoundComposition {
        <<value object>>
        +CompositionKind kind
        +int tasksPerRound
        +bool requireDistinctTaskPerRound
        +int maxRounds
    }
    %% maxRounds is nullable; unset means the rules state no ceiling. It bounds
    %% what may be scheduled, which is why it is not on ValidityRule: a phase
    %% over its ceiling is not "invalid" in the sense minRounds means.

    class Task {
        +string code
        +string name
    }

    class DropPolicy {
        <<value object>>
        +DropDimension dimension
        +int dropCount
        +int applyWhenRoundsCompletedAtLeast
        +int applyWhenResultsAtLeast
    }
    %% Both gates are nullable and conjunctive: a drop applies only when every
    %% populated gate holds. F3B.2.8 states two — more than five complete rounds
    %% AND a task with more than five results — and they diverge whenever a
    %% group is annulled under F3B.1.8 c.
    %% A phase holds an ORDERED list of policies and the first whose gates all
    %% hold is the one that applies (F22). F3F.1.13 tiers the discard: one round
    %% at four or more, two rounds above fourteen. A single-element list is the
    %% case in all six original classes.

    class ValidityRule {
        <<value object>>
        +int minRounds
        +int minTasks
    }

    class PromotionRule {
        <<value object>>
        +PromotionKind kind
        +int topN
        +decimal topPercent
        +int minGroupSize
        +int maxGroupSize
        +bool carryPenalties
    }

    class FinalRankingRule {
        <<value object>>
        +FinalRankingKind kind
    }

    class ReflightRule {
        <<value object>>
        +ReflightSelection entitledScores
        +ReflightSelection othersScore
        +int minNewGroupSize
    }

    class PenaltyCatalogue {
        <<value object>>
    }

    class PenaltyDefinition {
        +string infractionType
        +string exclusionGroup
        +PenaltyAccrual accrual
    }
    %% accrual defaults to OncePerAttempt, which is what F3K.4.3 and F3J.2.4 c
    %% require literally ("each flight attempt may only incur a single penalty")
    %% and what all six original classes assume. PerOccurrence multiplies the
    %% deduction by the number of recorded occurrences: F3F.1.10 penalises a
    %% safety-plane crossing "by 100 points each" (F23).

    class PenaltyEffectSpec {
        <<value object>>
        +PenaltyEffect effect
        +decimal points
        +PenaltyApplication appliedAt
    }
    %% One infraction may carry several effects at different pipeline points:
    %% F3B.2.2 p zeroes the flight AND deducts 1000 from the final score;
    %% F3K.4.1 deducts AND zeroes the whole round.
    %% exclusionGroup is nullable. Within one flight attempt at most one penalty
    %% from a group applies, the largest winning (F3K.4.3, F3J). A group may
    %% contain only DeductPoints effects — "largest" is undefined across effect
    %% kinds — and that is validated at adoption.
    %% "Largest" compares each definition's accrued contribution, not its
    %% points: two PerOccurrence crossings contribute 200, and F3F.1.10's 1000
    %% point person-contact still supersedes them. With every member
    %% OncePerAttempt the contribution IS the points, so F3K and F3J are
    %% unchanged by the refinement.

    class CompositionKind {
        <<enumeration>>
        FixedSequence
        ChooseFromCatalogue
    }

    class DropDimension {
        <<enumeration>>
        ByRound
        ByTask
    }

    class PromotionKind {
        <<enumeration>>
        TopN
        TopPercent
    }

    class FinalRankingKind {
        <<enumeration>>
        SinglePhase
        LastPhaseReplaces
        SplitByPromotion
    }

    class ReflightSelection {
        <<enumeration>>
        Replacement
        BetterOf
        NotPermitted
        UndefinedRequiresRuling
    }
    %% NotPermitted (F26) is a rule that DEFINITELY grants no re-flight — NZ
    %% 3.13.1 h and 3.15.1 h, "no re-flights are permitted". Distinct from
    %% UndefinedRequiresRuling, which asserts the rulebook is silent and a CD
    %% must decide. Conflating them would put a ruling in front of the CD that
    %% the rules have already made.

    class PenaltyEffect {
        <<enumeration>>
        DeductPoints
        ZeroFlight
        ZeroRound
        ZeroTask
        Disqualify
    }

    class PenaltyApplication {
        <<enumeration>>
        RawScore
        FinalAggregate
    }

    class PenaltyAccrual {
        <<enumeration>>
        OncePerAttempt
        PerOccurrence
    }

    class ParameterBindingPoint {
        <<enumeration>>
        CompetitionSetup
        BeforeFlying
        PerRound
    }

    CompetitionClass "1" *-- "1..*" PhaseDefinition : ordered
    CompetitionClass "1" *-- "0..*" Parameter : declares
    ParameterRef ..> Parameter : resolves to (validated at adoption)
    CompetitionClass "1" *-- "1" FinalRankingRule
    CompetitionClass "1" *-- "1" ReflightRule
    CompetitionClass "1" *-- "1" PenaltyCatalogue
    PenaltyCatalogue "1" *-- "0..*" PenaltyDefinition
    PenaltyDefinition "1" *-- "1..*" PenaltyEffectSpec
    PhaseDefinition "1" *-- "1" RoundComposition
    PhaseDefinition "1" *-- "1..*" Task : catalogue
    Task "1" *-- "0..1" ReflightRule : overrides the class default
    PhaseDefinition "1" *-- "1..*" DropPolicy : ordered, first match wins
    PhaseDefinition "1" *-- "1" ValidityRule
    PhaseDefinition "1" *-- "0..1" PromotionRule : entry criteria

    note for PhaseDefinition "A flyoff changes working times, caps, available tasks and penalty carry-over. Those rules live here, not on the class."
    note for ReflightRule "Two roles, one event: the entitled competitor takes the reflight; everyone else in the group takes the better of two. The class states the default; a Task overrides it where its rules differ."

    classDef aggregateRoot fill:#FFE873,stroke:#E5B700,stroke-width:2px,color:#1A1A1A
    cssClass "CompetitionClass" aggregateRoot
```

---

## 3. Competition Class — the scoring vocabulary

What a Task composes to turn measurements into points. This is the closed
vocabulary NFR-2 refers to: the scoring process interprets these generically and
has no notion of landings, launch heights, laps or motor runs.

```mermaid
classDiagram
    direction TB

    class Task {
        +string code
        +string name
    }

    class MetricDefinition {
        +string name
        +MeasuredKind kind
        +string unit
        +bool declaredBeforeLaunch
    }

    class FlightSelection {
        <<value object>>
        +SelectionKind kind
        +int count
        +string rankByMetric
        +TargetAssignment targets
        +decimal[] targetValues
    }
    %% targetValues are in the UNITS OF THE METRIC the task's scoring term
    %% consumes, not in points. Each selected flight's metric is clamped to its
    %% assigned target, then scored.
    %% rankByMetric is nullable and only meaningful to BestN. Null ranks the
    %% candidate flights by score (F3K.11.5, Poker: an achieved target credits
    %% the target, so score is the only ordering that means anything). Set,
    %% it ranks by that metric's raw value — F3K.11.8 assigns targets to the
    %% four longest FLIGHTS, and no flight has a score until a target has been
    %% assigned to it, so ranking by score there is circular.

    class ScoreTerm {
        +ScoreTermKind kind
        +string metricRef
        +decimal rate
        +decimal cap
        +CapScope capScope
        +decimal origin
        +decimal value
        +ScoreStage applyAt
    }
    %% applyAt defaults to RawScore: the term contributes to the value that
    %% normalisation consumes, which is what all seven FAI classes want — F5J
    %% and F5L normalise their landing bonus along with the flight time.
    %% Normalised terms are added AFTER normalisation and are not scaled by it
    %% (F24). NZ Class M 3.12.1 e states it outright: "landing points will be
    %% added to the normalized flight score". The two orders give different
    %% scores and, in a close group, a different ORDER — so this is not a
    %% rounding difference.
    %% A Normalised term is meaningless on a task with no Normalisation, and a
    %% task whose ONLY terms are Normalised has no raw score to normalise.
    %% Both are rejected at adoption.

    class Band {
        <<value object>>
        +decimal from
        +decimal to
        +decimal ratePerUnit
    }
    %% Bands are cumulative and are evaluated over (metric - ScoreTerm.origin).
    %% from and to accept a ParameterRef (F27).

    class LookupRow {
        <<value object>>
        +decimal upTo
        +decimal points
    }
    %% upTo is nullable; null means unbounded. Legal only on the last row; validated at
    %% adoption (rows ascending, at most one unbounded, and it must be last).

    class Predicate {
        <<value object>>
        +string leftMetricRef
        +Comparator op
        +string rightMetricRef
        +decimal rightValue
        +Predicate[] allOf
    }
    %% Exactly one of {leaf comparison, allOf} is populated. There is no anyOf:
    %% every multi-condition site in the six FAI classes is a conjunction.

    class TaskTiming {
        <<value object>>
        +WorkingTimeKind kind
        +duration workingTime
        +duration preparationTime
        +int maxLaunches
    }

    class GroupConstraint {
        <<value object>>
        +int minPerGroup
        +int minValidResults
    }
    %% minValidResults is nullable; unset means the class states no annulment
    %% threshold, and no group is annulled for want of valid results.

    class Normalisation {
        <<value object>>
        +NormalisationDirection direction
        +int winnerScore
    }
    %% Optional on Task (F25). Absent means the task does not normalise at all:
    %% the raw score IS the task result and rounds aggregate raw points. Every
    %% FAI class normalises, so this was 1..1 until the NZ classes; ALES 123 and
    %% ALES Radian say "each flight counts, the final score is the total of all
    %% points over three flights" (3.13.1 i, 3.15.1 i). There is no identity
    %% value for normalisation, so absence is the only truthful encoding.

    class Rounding {
        <<value object>>
        +RoundingMode mode
        +decimal precision
    }

    class SelectionKind {
        <<enumeration>>
        Last
        LastN
        BestN
        All
        ExactlyNInOrder
    }

    class TargetAssignment {
        <<enumeration>>
        None
        AnyOrder
        InOrder
    }

    class CapScope {
        <<enumeration>>
        PerFlight
        PerTask
    }

    class ScoreTermKind {
        <<enumeration>>
        Rate
        Lookup
        Piecewise
        Constant
        Conditional
    }

    class ScoreStage {
        <<enumeration>>
        RawScore
        Normalised
    }

    class Comparator {
        <<enumeration>>
        LessThan
        LessOrEqual
        GreaterThan
        GreaterOrEqual
        EqualTo
    }

    class WorkingTimeKind {
        <<enumeration>>
        Fixed
        UntilAllFlightsComplete
    }

    class NormalisationDirection {
        <<enumeration>>
        HigherIsBetter
        LowerIsBetter
    }

    class RoundingMode {
        <<enumeration>>
        Truncate
        HalfUp
        Ceiling
    }

    Task "1" *-- "1..*" MetricDefinition : records
    Task "1" *-- "1" FlightSelection : which flights count
    Task "1" *-- "1..*" ScoreTerm : staged by applyAt
    Task "1" *-- "1" TaskTiming
    Task "1" *-- "1" GroupConstraint
    Task "1" *-- "0..1" Normalisation
    Task "1" *-- "0..1" Predicate : validWhen
    Task "1" *-- "0..1" Predicate : flightValidWhen
    Task "1" *-- "0..1" Rounding : of the raw score
    MetricDefinition "1" *-- "1" Rounding : capture precision
    Normalisation "1" *-- "0..1" Rounding : of the normalised score
    ScoreTerm "1" *-- "0..*" Band : piecewise
    ScoreTerm "1" *-- "0..*" LookupRow : lookup
    ScoreTerm "1" *-- "0..1" Predicate : conditional
    ScoreTerm "1" *-- "0..2" ScoreTerm : then / else
    ScoreTerm ..> MetricDefinition : reads
    Predicate ..> MetricDefinition : compares

    note for ScoreTerm "A landing table and a launch-height penalty are the same term reading different metrics."
    note for Band "Bands are cumulative: 1 pt/s to 600 s then -1 pt/s scores 599 at 601 s."
    note for Normalisation "Direction is per task: F3B Speed inverts, because the lowest time wins."
    note for Predicate "Two gates, different outcomes: validWhen decides whether the TASK has a result at all; flightValidWhen zeroes one flight while leaving it selectable."

    classDef aggregateRoot fill:#FFE873,stroke:#E5B700,stroke-width:2px,color:#1A1A1A
```

---

## 4. Scoring

`ScoringService` is a domain service, not an aggregate. It reads the rulebook
from the Competition's own copy, structure from the Competition, and raw data
from the Entries.

```mermaid
classDiagram
    direction LR

    class ScoringService {
        <<domain service>>
        +interpretFlight(Flight) FlightResult
        +selectFlights(Entry) TaskResult
        +normaliseGroup(Group) GroupResult
        +aggregate(Competitor, Phase) PhaseResult
        +rank(Competition) ScoreResult
    }

    class TaskResult {
        <<derived>>
        +ResultState state
        +decimal rawScore
    }

    class ScoreResult {
        <<derived>>
        +decimal normalisedScore
        +int placing
    }

    class ResultState {
        <<enumeration>>
        Valid
        NoResult
    }

    ScoringService ..> TaskResult : produces
    ScoringService ..> ScoreResult : produces
    TaskResult "1" *-- "1" ResultState

    note for ResultState "NoResult is not a score of zero. It is excluded when finding the group winner."
```

**The pipeline is fixed and core-owned; every stage takes class data.**

```
capture → interpret flight → select flights → assemble raw → clamp → round
  → normalise → add normalised terms → round → aggregate phase → drop
  → apply penalties → rank
```

Classes disagree about *where* in this sequence things happen, which is why the
order is explicit: F3J subtracts penalties before normalising, F5J deducts them
from the final aggregate; F5K truncates the raw score to whole points, then
normalises, then rounds again.

Two stages are skipped rather than parameterised when a class is silent.
`normalise` is a no-op where the task has no `Normalisation` — the NZ ALES
classes aggregate raw points — and `add normalised terms` is a no-op wherever no
term sets `applyAt Normalised`, which is every FAI class. NZ Class M is the only
definition in `seed-data/` that uses the stage, and it is the reason the stage
is named separately rather than folded into `assemble raw`.

**Every stage is flight-local or later.** `interpret flight` sees one `Flight`'s
`Measurement`s and the `flight.sequence` intrinsic — never its siblings, never
task-level values, and never arithmetic between them. `select flights` is the
first stage that sees the whole `Entry`, and it is the only one that needs to.
Three FAI rules push on that boundary and all three are answered without moving
it: `F3K.11.8` by `FlightSelection.rankByMetric` (selection legitimately sees
every flight already), `F3K.9.3` by a captured flag read through
`Task.flightValidWhen`, and `F3K.7`'s per-task sum limit by a core invariant on
capture rather than by class data. See `high-level-architecture.md`.

---

## Modelling notes

- **Aggregate roots are shaded yellow** — CompetitionClass, Competition, Person
  and Entry. Per-aggregate boundary diagrams are in `aggregate-roots.md`.
- **The Competition owns its rulebook.** `AdoptedRules` is the whole class
  definition copied in at creation, with provenance (which library class and
  version it came from) and the evaluator version that interprets it. Scoring
  never reads the library `CompetitionClass`, so editing or deleting a class in
  the library cannot perturb a running or finished event. The version pin it
  replaces only worked if nobody ever edited seed data in place.
- **Rulebook corrections are retroactive.** A `RulesAmendment` applies to the
  whole competition, not from a point in time. This costs nothing because
  results are derived — the next query simply returns corrected numbers — and it
  is what you want when a transcription error is found at round 3.
- **Parameters vs bindings.** The class declares what may vary (`Parameter`);
  the Competition records what was chosen and when (`ParameterBinding`). They
  are bound at different moments — F5K's height reference from the measured
  wind the day before, F3K's task selection per round, a flyoff cut at the end
  of qualifying. Bindings must be recorded as they happen or re-scoring is not
  reproducible. A `ParameterRef` is how a definition *consumes* one, and it is
  legal only in thirteen slots — a working time, a
  launch cap, a band origin, a band bound, a group minimum, a minimum-rounds
  rule, a promotion size, penalty carry-over, a reflight group minimum.
  `allowedValues`
  records the bounds where the rules state them, so a binding can be validated
  rather than trusted.
- **Rule-silence is a parameter, not a default.** Where a rulebook simply does
  not say — whether penalties carry into a flyoff, how large the flyoff is —
  the class declares a parameter with *no default*, so the CD chooses at setup
  and the choice enters the event log. The alternative, a sensible default, is
  the software inventing a rule and hiding it. Reading a definition for `no
  default` finds every place its rulebook is silent.
- **Phases own their scoring rules.** A flyoff is not a shorter preliminary: it
  changes working times, points caps, which tasks are available, and whether
  penalties carry over. `PhaseDefinition` holds all of it; `FinalRankingRule`
  says how phases combine, and its three variants exist because the classes
  genuinely disagree — a single phase, a flyoff replacing preliminary points, or
  qualifiers and non-qualifiers ranked on different bases.
- **Tasks own everything that varies per task.** The test applied throughout:
  *if it varies between tasks, it belongs on the Task.* F3B settles this on its
  own — within one class its group minima, flight-time precision, landing table
  and normalisation direction all differ per task.
- **Score terms replace named rule slots.** There is no `landingPoints` or
  `heightBonusPenalty` attribute, because naming them puts a discipline concept
  in the core. A landing table is `Lookup` over a distance metric; a launch
  height penalty is `Piecewise` over a height metric; F3B's −1 pt/s beyond 600 s
  is a second cumulative band on the same term as the flight points. A term's
  `cap` clamps the *metric consumed*, not the points produced, and `capScope`
  says whether it clamps each flight or their sum — F5K caps total flight time
  across a task while leaving the launch-height bonus outside the cap.
- **Bands are cumulative.** A piecewise term applies each band's rate to the
  portion of the measurement falling inside it. This is what lets one term type
  express both F3B's overtime deduction and F5J's two-slope height penalty.
  Bands are measured from `ScoreTerm.origin`, which defaults to zero: F5K's
  launch points are per metre *relative to an announced height*, so its bands
  read from a parameter rather than from nothing. A band *bound* may also read a
  parameter: NZ Class M's +1/−1 turning point is the target time the CD
  announces on the day, not a rule constant.
- **A score term names the stage it lands at.** `ScoreTerm.applyAt` is
  `RawScore` by default — the term feeds the value normalisation consumes, and
  that is what every FAI class wants, including F5J and F5L, which deliberately
  normalise their landing bonus along with the flight time. NZ Class M states
  the other order: "landing points will be added to the *normalized* flight
  score". The distinction is not cosmetic. Two pilots in one group, target 600 s
  — A flies 600 s and lands 9 m out (bonus 10), B flies 500 s and lands 1 m out
  (bonus 50). Added after normalising: A 1010, B 883. Folded into the raw score
  and normalised: A 1000, B 902. Different scores and a different order, from
  the same rulebook, with nothing in the definition to say which was meant.
- **Normalisation is optional, and its absence is a statement.** A task with no
  `Normalisation` does not normalise: its raw score *is* its result and rounds
  aggregate raw points. Every FAI class normalises, which is why this was
  mandatory for the first seven; the NZ ALES classes do not — "each flight
  counts, the final score is the total of all points over three flights". There
  is no normalisation that leaves scores unchanged, so writing one to satisfy a
  multiplicity would have been a fabricated rule, not a harmless default.
- **A zeroed flight is still a flight.** `Task.flightValidWhen` is the per-flight
  gate — `F3K.9.3`'s late landing, `F3K.11.3`'s launch outside the three-second
  signal, `F3K.7`'s launch before the working time. It zeroes that flight's
  contribution and nothing else: the flight is still selected, because
  `F3K.11.1` scores *the last flight* and a late-landing last flight does not
  promote its predecessor. Voiding it on the `Flight` itself would, which is why
  the gate lives on the Task and is read at `interpret flight`, not at capture.
  It is distinct from `Task.validWhen`, which decides whether the task has a
  result at all.
- **`NoResult` is not zero.** A flight that never validly completed has no
  result. This matters wherever the lowest value wins: in F3B Speed a raw zero
  would otherwise be the fastest time in the group. Competitors with `NoResult`
  score nothing and are ignored when finding the group winner; a group is
  annulled when fewer than `GroupConstraint.minValidResults` remain, where the
  class states such a threshold. `Task.validWhen` is what decides validity: a
  rule saying an incomplete course "scores zero" means *no result*, and writing
  it as a zero would hand the group to whoever failed.
- **Measurements are not all numbers.** `MeasuredValue` carries a number or a
  flag, because the rules require plain observations (landed in the defined
  area, model touched during landing) as well as quantities. `declaredBeforeLaunch` marks
  metrics the pilot nominates *before* flying — a Poker target — which the
  scoring rules then compare the flight against.
- **Penalties are recorded infractions only.** Deductions the system *derives*
  from what was measured — an overfly deduction, a per-launch penalty — are
  score terms, not Penalties, and nobody records them as infractions. The
  distinction matches how the data is captured: launch counts are inferred from
  flight records, never entered, and a term reads which launch a flight was via
  the reserved intrinsic `flight.sequence` rather than by anyone writing it on a
  card. A `PenaltyDefinition` carries an *effect*, not
  merely a cost, because zeroing a flight, a round or a task are all real
  outcomes in the rules and none of them is a point deduction. It carries
  *several*, because one infraction can do two things at two points in the
  pipeline: `F3B.2.2 p` zeroes the flight and deducts 1000 from the final score,
  `F3K.4.1` deducts and zeroes the whole round. `exclusionGroup` is the other
  half — `F3K.4.3` and `F3J` both say a flight attempt may incur only one
  penalty, the largest applying, so penalties do not simply sum. `accrual` is
  the third: those two rules mean it *literally* ("each flight attempt may only
  incur a single penalty"), but `F3F.1.10` deducts 100 points per safety-plane
  crossing, so how many times an infraction counts is class data rather than a
  universal. `OncePerAttempt` is the default and the case in all six original
  classes; the exclusion group then compares accrued contributions, which
  leaves those six scoring exactly as before.
- **Reflight rules default at the class and override at the task.** `F3B.1.5 e`
  scopes its better-of rule to Tasks A and B by name; Task C's is unstated, and
  `UndefinedRequiresRuling` can only say so if a Task can hold a rule of its own.
  Five of the six classes never override.
- **Reflight scoring is per role, not per class.** In one reflight group the
  entitled competitor's new attempt is official even if worse, while every other
  pilot takes the better of their two — so `Entry` carries the role.
  `UndefinedRequiresRuling` exists because at least one class defines no rule at
  all, and the system must be able to say so and record the Contest Director's
  decision rather than invent one.
- **Finalisation captures, it does not compute.** Results are derived on demand
  while a competition is live. Finalising a phase freezes its results and names
  who was promoted — which is where a flyoff cut decision is recorded — and
  finalising the competition captures the final classification. A declared
  result answers "what was declared"; asking "what is the score" still derives
  from raw data, so the two can always be compared. Reopening appends a new
  revision; nothing is overwritten.
- **Round completion is derived** from the state of its TaskRounds, so partial
  annulment is handled by filtering rather than by mutating a completion flag.
- **Predicates combine, but only by conjunction.** `Predicate.allOf` exists
  because several rules gate one outcome on many observations at once — F5L
  voids its landing bonus on any of seven. There is deliberately no `anyOf`:
  no FAI class in the corpus needs disjunction, and the vocabulary admits a
  construct only when a rule requires it. This is not an expression language —
  no arithmetic, no functions — so a definition stays statically validatable at
  adoption.
- **Tie-breaking is not yet modelled**, and when it is it will need two kinds,
  not one: comparison against another figure (a best dropped score, a qualifying
  position) and *scheduling more flying* (an additional full round, a one-task
  tie-break flyoff). An ordered list of comparators cannot express the second.
- **Two rule exceptions remain unwritable.** `F3B.2.3 b` and `F3B.2.4 f` zero a
  flight that misses the landing area *"except in the case of midair collision"*
  — an exception to a score term, which no predicate over measurements reaches.
  `F3K.9.3`'s Task C cascade — still airborne in the sixty-second preparation
  period zeroes the *next* attempt — is cross-flight, and the flight-local
  boundary above refuses it: the timekeeper records the next attempt's flag
  directly, so the system honours a judgement rather than deriving one.
- **The notation is the model's test.** `docs/competition-class-notation.md`
  defines a hand-writing notation for a class definition, and `seed-data/`
  holds seven FAI classes and three NZ national classes written in it. The
  notation is deliberately isomorphic to this diagram — one keyword per model
  element, no keyword that is not one — so anything a class cannot express is a
  gap here, not there. Every change above came from writing them, and the last
  four came from the three NZ classes, which is the point of keeping a corpus
  the model was not designed against.
