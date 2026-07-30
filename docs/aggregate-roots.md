# RC Soaring — Aggregate Boundaries

One diagram per aggregate root. Each shows the root (**yellow**), the entities
and value objects that live *inside* its consistency boundary (filled diamonds),
and everything it reaches across the boundary **by id** (grey, dashed arrows) —
other roots, or entities that live inside another aggregate. Nothing inside one
aggregate holds a direct object reference into another — across a boundary you
reference by id only.

## A note on how many roots there are

You marked three roots: **CompetitionClass**, **Competition**, **Competitor**
(the last has since been reworked: the system-wide root is now **Person**, and
*Competitor* names a person's registration inside a Competition — see §2–§3 and
`soaring-registration.md`).
Drawing them per-aggregate exposes a problem the whole-model diagram hides: the
composition spine is ten deep, and if all of it lives inside **Competition**,
then the entire event — every flight, every raw measurement — is one aggregate.
Its lower half (**Entry → Flight → Measurement → Amendment**) is written *live*
by the scorers, at high concurrency. Keeping that inside
Competition means every score update must load and lock the whole event.

So the model pushes toward a **fourth root: Entry** — the unit of live capture.
Split out and referenced from Group by id, each pilot's working-time record
becomes independent, which is what a real-time capture system needs. This is the
standard "small aggregates, reference across boundaries by id" pattern. The
diagrams below assume this split. If you'd rather keep one large Competition
aggregate for simplicity, fold Entry back in and accept the write-contention
cost — but for live hardware capture I'd keep them separate.

The penalty scope enum maps cleanly onto the boundary: **Flight** and **Entry**
scoped penalties live in the Entry aggregate; **TaskRound** and **Competition**
scoped penalties live in the Competition aggregate.

---

## 1. CompetitionClass — the rulebook

Pure reference data. It defines the tasks and every rule used to turn raw numbers
into scores, and it references nothing else. Versioned, and pinned by a
Competition at creation so historical results stay reproducible.

```mermaid
classDiagram
    direction TB
    class CompetitionClass {
        +string faiDesignation
        +TaskMode taskMode
        +string version
    }
    class Task {
        +string name
        +string kind
        +MetricDefinition metrics
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
    class MetricSchema {
        <<value object>>
    }
    class HeightTable {
        <<value object>>
    }
    class LandingPointsTable {
        <<value object>>
    }
    class FlightTimeRules {
        <<value object>>
    }
    class PenaltyCatalogue {
        <<value object>>
    }

    CompetitionClass "1" *-- "1..*" Task
    CompetitionClass "1" *-- "1" DropPolicy
    CompetitionClass "1" *-- "1" ReflightRule
    CompetitionClass "1" *-- "1" MetricSchema
    CompetitionClass "1" *-- "1" HeightTable
    CompetitionClass "1" *-- "1" LandingPointsTable
    CompetitionClass "1" *-- "1" FlightTimeRules
    CompetitionClass "1" *-- "1" PenaltyCatalogue

    classDef root fill:#FFE873,stroke:#E5B700,stroke-width:2px,color:#1A1A1A
    cssClass "CompetitionClass" root
```

---

## 2. Person — a registered person

Anyone known to the system: identity, contact details, and club affiliation.
Registering with the system happens once, and is what lets an organiser build a
competition's field from known people. Deliberately free of contest data: it is
referenced by id from each Competition's Competitor records and owns nothing
downstream of that. Email is unique system-wide (enforced at the repository /
unique-index level — the standard cross-aggregate uniqueness answer).

```mermaid
classDiagram
    direction TB
    class Person {
        +id id
        +string name
    }
    class ContactDetails {
        <<value object>>
        +string email
        +string phone
        +string homeCity
    }
    class ClubAffiliation {
        <<value object>>
        +string clubName
        +string membershipNumber
    }

    Person "1" *-- "1" ContactDetails
    Person "1" *-- "0..1" ClubAffiliation

    classDef root fill:#FFE873,stroke:#E5B700,stroke-width:2px,color:#1A1A1A
    cssClass "Person" root
```

---

## 3. Competition — the event structure, field and schedule

The setup and shape of one event: its phases, rounds, task-rounds, groups, the
draw — and now the field itself. Each **Competitor** is one person's
registration into this event (competitor number, registered/withdrawn
timestamps), referencing the system-wide Person by id. Registrations live
inside this aggregate because the draw's fairness invariant needs the field as
one consistent set, and registration writes are low-volume — the contention
argument that pushes Entry out does not apply here. Created up front and only
lightly mutated afterwards (register or withdraw a competitor, append a
reflight group, annul a task-round). It holds no live flight data — Entries reference their Group and
Competitor by id from outside. Task-round completion and annulment live here;
scoring reads this structure but writes nothing back to it.

```mermaid
classDiagram
    direction TB
    class Competition {
        +string name
        +string location
        +date startDate
        +date endDate
        +string pinnedRuleVersion
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
        +taskId taskRef
    }
    class Group {
        +int ordinal
    }
    class Penalty {
        +string infractionType
        +PenaltyScope scope
    }
    class CompetitionClass {
        <<external root>>
    }
    class Person {
        <<external root>>
    }

    Competition "1" *-- "0..*" Competitor : field
    Competition "1" *-- "1..*" Phase
    Phase "1" *-- "1" Draw
    Phase "1" *-- "1..*" Round
    Round "1" *-- "1..*" TaskRound
    TaskRound "1" *-- "1..*" Group
    Competition "1" *-- "0..*" Penalty : task-round / competition scope

    Draw "1" --> "0..*" Competitor : allocates
    Competition ..> CompetitionClass : pins version
    Competitor ..> Person : registration of
    TaskRound ..> CompetitionClass : task by id

    classDef root fill:#FFE873,stroke:#E5B700,stroke-width:2px,color:#1A1A1A
    classDef external fill:#EEEEEE,stroke:#BDBDBD,stroke-width:1px,color:#555
    cssClass "Competition" root
    cssClass "CompetitionClass,Person" external
```

> **Group membership** is not stored here — it is the set of Entries whose
> `groupRef` points at a given Group. "Who is in Group C" is a query over the
> Entry aggregate, not a list held on Group. Field membership *is* stored here —
> the Competitor records. Two different things: the field is who is in the
> competition; group membership is who flew where.

> **Field freeze:** competitors are added or removed only until the draw is
> accepted. After that a withdrawal is recorded but leaves the draw intact —
> the competitor's entries simply never occur — and changing the field means
> redrawing.

---

## 4. Entry — the live flying record

One competitor's working-time window and everything captured in it. This is what
the scorers directly update: an ordered list of Flights, each with raw
Measurements (append-only, corrected by Amendments, never overwritten). A
reflight is simply a second Entry pointing at whichever Group flew it. Isolating
this as its own aggregate is what keeps concurrent scorer writes from contending.
`competitorRef` identifies the Competitor registration inside the Competition
aggregate — the record that carries the competitor number the scorers name/id
captures with, and the link back to the Person. Referencing an internal entity
of another aggregate by id is fine here (precedent: `groupRef`) because Entry
only ever holds the id — any mutation of a Competitor still goes through the
Competition root.

```mermaid
classDiagram
    direction TB
    class Entry {
        +TimeWindow workingTime
        +groupId groupRef
        +competitorId competitorRef
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
    class Group {
        <<Competition aggregate>>
    }
    class Competitor {
        <<Competition aggregate>>
    }

    Entry "1" *-- "1..*" Flight
    Flight "1" *-- "1..*" Measurement
    Measurement "1" *-- "0..*" Amendment
    Entry "1" *-- "0..*" Penalty : flight / entry scope

    Entry ..> Group : belongs to
    Entry ..> Competitor : flown by

    classDef root fill:#FFE873,stroke:#E5B700,stroke-width:2px,color:#1A1A1A
    classDef external fill:#EEEEEE,stroke:#BDBDBD,stroke-width:1px,color:#555
    cssClass "Entry" root
    cssClass "Group,Competitor" external
```

---

## Scoring is cross-aggregate (not a root)

The scoring service is a domain service, not an aggregate. It *reads* rules from
CompetitionClass, structure from Competition, and raw data from the Entries, and
*produces* a ScoreResult that is derived on demand and never persisted as source
of truth. It has no consistency boundary of its own — it spans all of them.

```mermaid
classDiagram
    direction LR
    class ScoringService {
        <<domain service>>
        +interpretEntry(Entry) TaskResult
        +selectScoringEntry() Entry
        +normaliseGroup() GroupResult
        +aggregate() ScoreResult
    }
    class ScoreResult {
        <<derived, not persisted>>
        +decimal normalisedScore
        +int placing
    }
    class CompetitionClass {
        <<root>>
    }
    class Competition {
        <<root>>
    }
    class Entry {
        <<root>>
    }

    ScoringService ..> CompetitionClass : rules
    ScoringService ..> Competition : structure
    ScoringService ..> Entry : raw data
    ScoringService ..> ScoreResult : produces

    classDef external fill:#EEEEEE,stroke:#BDBDBD,stroke-width:1px,color:#555
    cssClass "CompetitionClass,Competition,Entry" external
```
