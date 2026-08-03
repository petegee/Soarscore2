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
 - Lightweight

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

### Lightweight  
This application will be open source for people to host their own instances, as well as 
myself hosting a shared instance in a cloud (TBC). A Competition will likely never have 
more than 30-40 competitorsover two days but more likley less. Entries are in real-time 
as the competition progresses. It is anticipated an extremely low transaction/sec rate 
even at its highest load. The application must remain as lightweight as possible and
not be over-engineered to handle large load (this will never happen).

### Open Source 
This application must be open source and its dependencies be open source so people 
can freely use, fork, extend this application as they wish.

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

**An Entry annulled by ruling has no result.**
Applied at `select flights`, alongside the class's own `validWhen`. Every class
can produce an attempt that a ruling voids — `F3F.1.5`'s provisional re-flight,
flown under protest with the jury deciding afterwards which of the two attempts
counts, is the case that needed it — and no rulebook makes it class data. See
the Entry aggregate in `aggregate-roots.md` §4.

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
2. `BestNFlights.rankByMetric` resolves to a metric declared on that task —
   diagram §3.
3. Every `ParameterRef` resolves to a declared `Parameter`, and every referenced
   parameter is bound before the pipeline stage that reads it — notation §3.
   One way only: a declared `Parameter` no ref names is legal, because a
   rulebook can require a CD choice no scoring stage reads (`F3F.1.5`). There is
   deliberately no `Parameter` analogue of check 6 — notation §3.
4. A `ParameterRef` occurs only in the thirteen slots that permit one, and
   nowhere a numeric literal would otherwise sit — notation §3, diagram §2.
5. Every `use` names a declared class-scope group of the kind its site requires
   — a `metricSet` in a task, a `rows` list on a `lookup`, a `bands` list on a
   `piecewise` — notation §7.1. The groups are notation sugar and are expanded
   away before the rest of this list runs, so checks 8 and 9 below see the
   copied rows and need no variant for a shared list.
6. Every declared class-scope group is named by at least one `use` — notation
   §7.1. An orphan is a cited scoring table the class no longer scores against.
7. Where a slot that consumes a `ParameterRef` has a unit of its own, the
   `Parameter` states that same unit — notation §3, diagram §2. A parameter
   written `m` and referenced from `TaskTiming.workingTime` is otherwise a
   defect nothing detects. Slots with no unit (counts, group sizes, round
   counts) require none, and a `Flag` parameter has no unit to state; ten of the
   corpus's parameters write one and all ten land in a unit-bearing slot.

*Structures are well formed.*

8. Adjacent piecewise bands meet: where one band's `to` and the next band's
   `from` are both `ParameterRef`s, they name the *same* parameter (F27) —
   notation §3, diagram §2. A gap or an overlap is a silent mis-score.
9. `lookup` rows ascend, at most one row is unbounded, and an unbounded row is
   last (F9) — notation §5, diagram §3.
10. A phase's ordered `DropPolicy` list has strictly descending gates (F22) —
    notation §4. Both orderings produce a plausible number, so the writer does
    not get to rely on remembering.

Two checks *left* this list rather than being added to it. "Exactly one of {leaf
comparison, `allOf`} is populated on a `Predicate`" was checkable only at
adoption while both lived on one class; `Comparison` and `AllOf` are now two
subtypes (diagram §3), so the combination it rejected has become unrepresentable
and there is nothing left to check. "A task whose terms are *all* `Normalised`
is rejected" went the same way when the stage moved off `ScoreTerm` onto the two
Task term lists (diagram §3): the raw list is `1..*`, so a task with nothing for
normalisation to consume is now unstorable. A constraint a type states is better than a
constraint this list states, and the same reasoning retired the `ScoreTerm` and
`FlightSelection` "exactly one of" invariants before either was ever written
down here.

*A slot's presence agrees with the rest of the definition.* These are the checks
optional multiplicities buy: where a slot may be absent, something has to reject
the combinations absence makes incoherent.

11. `finalRanking` written as `SinglePhase` on a class with more than one
    `PhaseDefinition` is rejected — notation §3, diagram §2.
12. A class with more than one `PhaseDefinition` and no `finalRanking` is
    rejected — notation §3, diagram §2. The omission is available only where the
    phase list forces the value.
13. `ReflightRule.minNewGroupSize` populated while both selections are
    `NotPermitted` is rejected — notation §3, diagram §2. The rules have already
    ruled out the group the number would size.
14. A task with a normalised term list and no `Normalisation` is rejected (F24)
    — notation §5, diagram §3. There is no stage for those terms to land at.
15. A task with a `Normalisation` and no `GroupConstraint` is rejected —
    notation §5, diagram §3. Normalisation is defined against the best score in
    the group, so a class that normalises has to say how groups are formed.
16. Each group named in a `PenaltyDefinition.exclusionGroups` contains only
    `DeductPoints` effects — notation §3, diagram §2, since "the largest
    applies" is undefined across effect kinds. A penalty may name several groups
    (F28); the check is per group.

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
