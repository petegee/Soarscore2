# High Level Architecture

## Core Principles
 - System is headless only
 - Hexagonal architecture
 - SOLID
 - Domain Driven Design
 - Lean and Focused
 - Testable by design
 - System assumes no specific external UX requirements
 - Access is strictly via an REST based API
 - The API interaction is "intent-based" 
 - Append only immutable log as state storage (Event Sourced)
 - CQRS pattern to cleanly seperate Reads from Writes
 - Commands and Queries only
 - Core-owned invariants
 - Functional-Like as a Core Princple

 ### Headless
The system must be headless in nature. It must not offer a UI or alternative interaction.

### Hexagonal architecture
At its simplest form, the application should be onion layered with dependencies pointing inwards only.
The layers should be:
 1, API: an http adapter, 
 2, Application: exposes Ports, contains domain services, and use cases, state management
 3, Domain: Contains the aggregate roots, and their entities/value objects

### SOLID
It should try to maintain SOLID principes, but we care more about (SLID): 
 - Single Responsibilty - Single reason to change.
 - Liskov Substitution Principle - Subtypes must be substitutable for their base types without altering the correctness of the program.
 - Interface Segregation Principle - Clients should not be forced to depend on methods they do not use.
 - Dependency Inversion Principle - "Dont ask, we'll tell you"

### Domain Driven Design
Domain-Driven Design (DDD) is a software development approach that aligns code structure with 
real business needs by modeling software around a specific domain using a shared vocabulary. The 
Domain layer must have **NO** dependencies.

### Lean and Focused
This system is to be as light weight and as un-opinionated as possible to allow maximum 
integrator flexibility while covering no more than the core domain.

### Testable by design
All functionality must be verifiable by test which are:
 - fast, 
 - independent
 - reliable 
 - automated 
 - isolated - Tests must not depend on each other
 - Be able to be driven without HTTP testing tools
 - NO UI TESTING

 Tests must not have unessessary knowledge of the structure of the code they're testing. 
 Eg Black-Box sociable style tests.

#### Overlapping Socialable Testing is preferred
We prefer to minimise solitary style white-box testing and mocking, and instead choose to 
do overlapping sociable black-box style tests verifing behaviour without "double-encoding"
the structure and dependencies of the application.

### System assumes no specific external UX requirements
We do not let external parties shape this system. It is the core kernel of a wider integrated 
competition system (which may have displays, sounds, announcements, score gathering systems etc).

### Access is strictly via an REST based API
The one and only adapter provided by this solution is an HTTP RESTfull API.

### The API interaction is "intent-based" 
We must only use "intent-based" interactions over HTTP. This means
 - only accept POSTs (a Command), and GETs (a Query)
 - POSTs paths are to verbs not nouns - to decouple consumers from the domain structures. 
 - There are NEVER any PUTs, PATCHES, DELETES, or OPTIONS
 - the new HTTP QUERY will not be supported just yet. 
 - Queries must send parameters on the query string - NEVER in the body.

### Append only immutable log as state storage (Event Sourced)
 - The system is Event-Sourced. 
 - Events are the state. 
 - Events are immutable. 
 - Read-model projections are a necessary evil, but should only be used when doing cross-stream 
   queries or query by something other than an aggregate root's ID
 - If querying by ID, then you must use load the stream.

### CQRS pattern to cleanly seperate Reads from Writes
The mechanisms to do queries must be separate from the mechanism which does state changes.

### Commands and Queries only
The application layer only supports either Commands or Queries. These are dispatched via a 
mediator pattern. This dove-tails nicely with intent-based APIs.

### Core-owned invariants

A handful of rules are true of *every* competition class and so are owned by the
core rather than written into a Competition Class definition. Each one below was
reached by trying to express an FAI rule as class data and concluding it did not
belong there. This list is deliberately short: anything that varies between
classes is class data by the law in `CLAUDE.md`, and an entry here must be
defensible as universal, not merely as convenient.

**Flight times within an Entry cannot exceed that Entry's working time.**
Enforced at capture. `F3K.7` states a stricter form of this — the sum of scored
flight times may not exceed the working time minus one second per scored flight —
but that arithmetic is F3K-specific and is a knowingly accepted deviation, not an
oversight; see §6 of `competition-class-notation.md`.

**A field smaller than a task's `minPerGroup` flies as one group.**
`F3B.1.8 b` writes the escape hatch out loud ("a minimum of eight competitors *or
all competitors*") and F3K does not, but F3K's minimum of five is equally
unsatisfiable in a four-pilot event and nobody would call that contest
impossible. It belongs to the draw, not to `GroupConstraint`.

**The scoring pipeline is flight-local up to flight selection.**
`interpret flight` sees one Flight's Measurements and the `flight.sequence`
intrinsic — never sibling flights, never task-level values, and never arithmetic
between them. `select flights` is the first stage that sees the whole Entry.
Holding this line is what keeps a class definition statically validatable at
adoption, so it is a constraint on the scoring implementation and not just a
description of it.

**Validated at adoption, before a Competition may hold a rulebook.**

A class definition is checked once, on adoption, and a Competition cannot come
into existence holding one that fails (`aggregate-roots.md` §3). The list below
is the **complete inventory** of those checks and is maintained as one:
**anything that introduces an adoption check adds a line here.** Each line is
the check only. The rule citation and the reason the check exists stay where
they are stated — in `competition-class-notation.md` and
`soaring-domain-class-diagram.md`, next to the construct they guard — because
that reasoning is the valuable half and flattening it into a list would lose it.

*References resolve.*

1. Every metric named by a `ScoreTerm` or a `Predicate` resolves to a
   `MetricDefinition` declared on that task — notation §9.
2. `FlightSelection.rankByMetric` resolves to a metric declared on that task —
   diagram §3.
3. Every `ParameterRef` resolves to a declared `Parameter`, and every referenced
   parameter is bound before the pipeline stage that reads it — notation §3.
4. A `ParameterRef` occurs only in the thirteen slots that permit one, and
   nowhere a numeric literal would otherwise sit — notation §3, diagram §2.

*Structures are well formed.*

5. Adjacent piecewise bands meet: where one band's `to` and the next band's
   `from` are both `ParameterRef`s, they name the *same* parameter (F27) —
   notation §3, diagram §2. A gap or an overlap is a silent mis-score.
6. `lookup` rows ascend, at most one row is unbounded, and an unbounded row is
   last (F9) — notation §5, diagram §3.
7. Exactly one of {leaf comparison, `allOf`} is populated on a `Predicate` —
   notation §5, diagram §3.
8. A phase's ordered `DropPolicy` list has strictly descending gates (F22) —
   notation §4. Both orderings produce a plausible number, so the writer does
   not get to rely on remembering.

*A slot's presence agrees with the rest of the definition.* These are the checks
optional multiplicities buy: where a slot may be absent, something has to reject
the combinations absence makes incoherent.

9. `finalRanking` written as `SinglePhase` on a class with more than one
   `PhaseDefinition` is rejected — notation §3, diagram §2.
10. A class with more than one `PhaseDefinition` and no `finalRanking` is
    rejected — notation §3, diagram §2. The omission is available only where the
    phase list forces the value.
11. `ReflightRule.minNewGroupSize` populated while both selections are
    `NotPermitted` is rejected — notation §3, diagram §2. The rules have already
    ruled out the group the number would size.
12. A `ScoreTerm` with `applyAt = Normalised` on a task with no `Normalisation`
    is rejected (F24) — notation §5, diagram §3. There is no stage for it to
    land at.
13. A task whose terms are *all* `Normalised` is rejected (F24) — notation §5,
    diagram §3. Nothing is left for normalisation to consume.
14. A task with a `Normalisation` and no `GroupConstraint` is rejected —
    notation §5, diagram §3. Normalisation is defined against the best score in
    the group, so a class that normalises has to say how groups are formed.
15. A `PenaltyDefinition.exclusionGroup` contains only `DeductPoints` effects —
    notation §3, diagram §2, since "the largest applies" is undefined across
    effect kinds.

One candidate is deliberately **not** here: `zeroFlight` and `zeroRound` are
unusable in a `LowerIsBetter` task, because a raw zero is the fastest time in
the group. Notation §11 records it as a candidate and no definition in
`seed-data/` needs it enforced; it joins the list with the first class that
does.

### Functional-Like as a Core Princple
The system code should borrow core concepts of functional programming (but not necessarily 
use a functional language). The principles we choose to apply are:
 - Immutability. Data is immutable, meaning once a value is assigned, it cannot be changed.
 - Pure functions where possible. A pure function always produces the same output for the 
   same input. It does not have side effects.
 - Function Composition. Functions can be combined to build more complex functions.
 - Declarative Style. The focus is on what to solve rather than how to solve it.
 - Avoiding Side Effects. Functions should not produce side effects that affect 
   the program's state outside their scope
