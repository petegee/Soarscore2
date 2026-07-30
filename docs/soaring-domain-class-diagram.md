# RC Soaring Competitions — Domain Class Diagram

Class diagram for the RC soaring timing and scoring domain model. Captures the
compositional spine (Competition down to Measurement), the reference-data side
(CompetitionClass and its rule policies), and the scoring service as pure
derivation over raw data.

```mermaid
classDiagram
    direction TB

    class CompetitionClass {
        <<aggregate root>>
        +string faiDesignation
        +TaskMode taskMode
        +MetricSchema metricsSchema
        +RoundRules roundRules
        +GroupRules groupRules
        +HeightTable heightBonusPenalty
        +LandingPointsTable landingPoints
        +FlightTimeRules flightTimeRules
        +PenaltyCatalogue penaltyCosts
    }

    class Competition {
        <<aggregate root>>
        +string name
        +string location
        +date startDate
        +date endDate
        +string pinnedRuleVersion
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
    }

    class Task {
        +string name
        +string kind
        +MetricDefinition metrics
    }

    class Group {
        +int ordinal
    }

    class Entry {
        +TimeWindow workingTime
    }

    class Flight {
        +int sequence
        +timestamp launchAt
    }

    class Measurement {
        +string type
        +decimal value
        +timestamp capturedAt
    }

    class Amendment {
        +decimal newValue
        +string reason
        +string by
        +timestamp at
    }

    class Penalty {
        +string infractionType
        +PenaltyScope scope
    }

    class DropPolicy {
        <<value object>>
        +DropDimension dimension
        +int threshold
    }

    class ReflightRule {
        <<value object>>
        +ReflightStrategy strategy
    }

    class ClubAffiliation {
        <<value object>>
        +string clubName
        +string membershipNumber
    }

    class ScoringService {
        <<domain service>>
        +interpretEntry(Entry) TaskResult
        +selectScoringEntry(Competitor, TaskRound) Entry
        +normaliseGroup(Group) GroupResult
        +aggregate(Competitor) ScoreResult
    }

    class ScoreResult {
        <<derived>>
        +decimal normalisedScore
        +int placing
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

    class TaskMode {
        <<enumeration>>
        SingleTask
        SimpleMultiTask
        ComplexMultiTask
    }

    class DropDimension {
        <<enumeration>>
        ByRound
        ByTask
    }

    class ReflightStrategy {
        <<enumeration>>
        Replacement
        BetterOfN
    }

    CompetitionClass "1" *-- "1..*" Task : defines
    CompetitionClass "1" *-- "1" DropPolicy : drop rule
    CompetitionClass "1" *-- "1" ReflightRule : reflight rule

    Competition "1" *-- "1..*" Phase : has
    Competition "1" *-- "0..*" Penalty : records
    Phase "1" *-- "1..*" Round : has
    Round "1" *-- "1..*" TaskRound : has
    TaskRound "1" *-- "1..*" Group : divided into
    Group "1" *-- "1..*" Entry : contains
    Entry "1" *-- "1..*" Flight : contains
    Flight "1" *-- "1..*" Measurement : captures
    Measurement "1" *-- "0..*" Amendment : corrected by

    Competition "1..*" --> "1" CompetitionClass : instance of / pins version
    Competition "1" *-- "0..*" Competitor : field
    Competitor "*" --> "1" Person : registration of
    Person "1" *-- "0..1" ClubAffiliation : club
    Phase "1" --> "2..*" Competitor : field
    Phase "1" --> "1" Draw : organised by
    Draw "1" --> "2..*" Competitor : allocates
    Draw ..> Group : produces initial
    TaskRound "*" --> "1" Task : for
    Entry "*" --> "1" Competitor : flown by

    Penalty ..> Flight : may attach to
    Penalty ..> Entry : may attach to
    Penalty ..> TaskRound : may attach to
    Penalty ..> Competition : may attach to

    ScoringService ..> CompetitionClass : applies rules
    ScoringService ..> Entry : reads
    ScoringService ..> Measurement : reads raw
    ScoringService ..> Penalty : reads
    ScoringService ..> ScoreResult : produces

    note for Round "Completion is derived from its TaskRounds"
    note for Measurement "Raw and append-only; corrections recorded as Amendments"
    note for ScoreResult "Computed on demand from raw data + rules; never stored"
    note for Competitor "One per person per competition; created by the organiser from registered Persons"

    classDef aggregateRoot fill:#FFE873,stroke:#E5B700,stroke-width:2px,color:#1A1A1A
    cssClass "CompetitionClass,Competition,Person" aggregateRoot
```

## Modelling notes

- **Aggregate roots are shaded yellow** — CompetitionClass, Competition, and Person. Per-aggregate boundary diagrams (one per root) are in `soaring-aggregates.md`.
- **Filled diamonds are composition, hollow arrows are references.** The whole
  spine Competition → … → Measurement is composition (the parts have no life
  outside their parent). CompetitionClass, Person, and Task are referenced
  by association because they are their own aggregates. The field itself now
  lives inside Competition: each Competitor is one person's registration into
  this one event, referencing the system-wide Person by id.
- **Person vs Competitor:** Person is the system-wide identity — name, contact
  details, club affiliation — created once when someone registers with the
  system. A Competitor is that person's participation in one competition: a
  reference to the Person, a competitor number, and registered/withdrawn
  timestamps. The organiser creates Competitors by picking from registered
  Persons; there is no self-service path. The registration process is in
  `soaring-registration.md`.
- **DropPolicy and ReflightRule are broken out as value objects** rather than
  buried as attributes, because their two dimensions (drop = ByRound vs ByTask;
  reflight = Replacement vs BetterOfN) are class-defined rules that vary between
  formats and deserve to be visible.
- **ScoringService only ever reads.** No arrow writes a score back onto any
  entity; `ScoreResult` is marked «derived» and produced on demand. This is the
  raw-first guarantee made visual — scores are computed from raw measurements
  plus pinned rules, never captured or stored authoritatively.
- **Penalty's four dashed arrows** are the polymorphic scope: a penalty may
  attach to a Flight, an Entry, a TaskRound, or the Competition as a whole. The
  `scope` enum plus a single reference is a valid alternative if polymorphic
  association is undesirable.
- **Round completion is derived** from the state of its TaskRounds, not stored
  as a flag — so partial annulment (weather kills some groups) is handled by
  filtering rather than by mutating a completion field.
- **Rule versioning:** Competition pins the CompetitionClass rule version at
  creation (`pinnedRuleVersion`) so historical results re-score deterministically
  even after the sporting code changes.

Several attributes are typed to value-object names (`MetricSchema`,
`HeightTable`, `LandingPointsTable`, etc.) rather than modelled as full classes,
to keep CompetitionClass legible. Promote any of them to a first-class type if
its internal structure becomes significant.
