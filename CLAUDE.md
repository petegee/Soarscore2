# CLAUDE.md — Soarscore

Master context for all Claude sessions/agents on this repo. Keep it succinct;
only add fundamentals here; grow it as the project matures but be mindful of 
size and each addition should be justifiable. If something here goes stale, fix it. 

## What this is

Soarscore is a **scoring and running system for radio-control glider
competitions** (FAI classes F3B, F3J, F3K, F5J, F5K, F5L). It manages master
data, competition setup, fair round-by-round draws, raw score capture, score calculation, 
and results reporting.

The fundamental domain concept glossary is here: docs/soaring-domain-glossary.md 
No new concepts should be added; always stop and surface to the user any new
concepts and justification for adding them. Do not add to the glossary without
explicit approval.

## Project status
Currently under active green-field development. There are no current users, 
and no existing real data that needs to be preserved, or migrated.

## Repository map

- `src/Soarscore.Domain` — the aggregates as immutable state + `Apply` folds
  (`Person`, `Competition`, `Entry`, `PublishedClassDefinition`), decide
  functions returning `Result<T>`, and the scoring engine. No dependency
  outside the BCL — enforced at build by `Soarscore.Architecture.Tests`.
- `src/Soarscore.Application` — the hexagonal core: `IDispatcher`,
  `ICommand`/`IQuery` handlers, the `IEventStore` / `IPeopleQuery` / `IClock`
  ports, and read-model projection functions (e.g. `PeopleProjection`).
  Depends on Domain only — never Marten.
- `src/Soarscore.Infrastructure` — the only project allowed to reference
  Marten: `MartenEventStore` (event append/read plus Inline projections over
  PostgreSQL, never the async daemon — see `docs/ladr/ladr-0001-event-store.md`)
  and the `AddSoarscoreInfrastructure` DI wiring.
- `src/Soarscore.Api` — ASP.NET Core Minimal API front door.
  `MapCommand`/`MapQuery` are the only routing surface — verbs, never nouns.
- `tests/Soarscore.Domain.Tests`, `tests/Soarscore.Application.Tests` — unit
  and property-based (CsCheck) tests driven through fakes; no store, no HTTP.
- `tests/Soarscore.Architecture.Tests` — ArchUnitNET layer rules plus the
  route-shape reflection test (endpoints must be GET/POST only).
- `tests/Soarscore.Infrastructure.Tests` — store-backed tests against a real
  PostgreSQL via Testcontainers, tagged `Trait("Category", "Storage")` so a
  fast local loop can filter them out.
- `kanban/` — the work board. Stories live in one of four lane folders and their
  plans are cited from code as `WI-n`. See "Working the board" below.
- `docs/ladr/` — architecture decision records binding the choices above
  (`ladr-0001-event-store.md`, `ladr-0003-library-choices.md`).
- `docs/rules/` — the rule knowledge base. `source-docs/` is the verbatim
  official text; the files above it are condensed, software-relevant summaries.
  See House-keeping rule 1.
- `docs/rules/nz/` — the **NZMAA** New Zealand rules, same structure and same
  read-only discipline. A *separate rulebook by a separate body*, not FAI
  variations; refs are written `NZ.3.12.3`. The FAI cross-class invariants do
  not hold for these classes — the `fai-rules` skill lists how.
- `docs/soaring-domain-glossary.md`, `docs/soaring-domain-class-diagram.md` —
  the domain concepts and their relationships. Approval required to change.
- `docs/competition-class-notation.md` — the notation for writing a Competition
  Class by hand, isomorphic to the class diagram.
- `tools/Soarscore.SeedData/` — seven FAI classes and the NZ national ALES
  classes, authored in C#. They are the model's test: anything they cannot
  express is a gap in the model. The NZ classes are there because they are a
  *different* rulebook — they found four gaps the FAI corpus could not.
  Canonical JSON is emitted into `tools/Soarscore.SeedData/json/`.
- `docs/high-level-architecture.md`, `docs/aggregate-roots.md` — intended shape
  of the system.
- `docs/non-functional-requirements.md` — NFR-1 … NFR-3.
- `docs/users.md` — who needs this and why.
- `.claude/skills/fai-rules/` — agent skill for looking up, citing and
  compliance-checking the rules — FAI and NZMAA — without loading whole rule
  volumes. (Named for the FAI; the corpus is now broader than the name.)


## Testing approach

- Unit and property-based (CsCheck) tests per layer remain the default —
  see Repository map.
- As features reach true end-to-end, user-centric workflows, cover them with
  **BDD/Gherkin-style acceptance tests** (Given/When/Then) exercising the
  workflow end-to-end. This is a routine part of the testing approach once a
  feature has a real user-facing workflow to cover, not an optional extra.
- During planning, identify where **property-based testing** (CsCheck) will
  add value and be appropriate — a genuine invariant, algorithm, or class of
  input where example-based tests would leave gaps (e.g. draw fairness, score
  calculation, drop-worst rules, normalisation) — and **articulate the
  invariant explicitly** as part of the plan. A named invariant is what makes
  the property test meaningful; identifying it during planning, not
  implementation, is what ensures it stays held true rather than getting
  discovered after the fact.

## Domain in one screen
Domain diagram is here: docs/soaring-domain-class-diagram.md - as with the glossary
this cannot be changed without approval.


## Core architectural law: Competition Class model vs. core system

**The core system must not know about any specific competition class.** All
variance between classes (F3B, F3J, F3K, F5J, F5K, F5L, and any future class)
is encapsulated in a **Competition Class** — a data-driven definition
(group-score basis, drop-worst rule, tasks, metrics, penalties, landing
table). The core system reads and interprets class models generically; it
never branches on discipline.

**The test:** if adding or changing a competition class requires editing code
outside that class's own model/definition, the core system has leaked a
class-specific assumption and the design is wrong — not the class.

This is not a style preference! Treat it as a constraint on
every design and code decision.

!!!CHECK
Backing detail: [NFR-1](docs/non-functional-requirements.md#nfr-1--one-centralised-flexible-competition-class-model)
(one place that knows a class's shape), [NFR-2](docs/non-functional-requirements.md#nfr-2--additive-only-extensibility-for-new-competition-types)
(extension is additive-only).

## Key constraints

- **Trust model:** club-level tool for a small, trusted NZ group. No auth, no
  score sign-off; an **immutable event log of all mutations** provides
  auditability instead.
- **Scale:** ≤ 20 pilots, ≤ 8 rounds/day, 1–2 day events;

## House-keeping rules

1. **`docs/rules/` is derived from the official FAI rules 
   (`docs/rules/source-docs`) and is read-only to the 
   software process.** Do **not** edit it to fit new software
   requirements or MVP-scoping decisions — it tracks the sport, not the product.
   Any requirement or software we generate **must not contravene** these rules
2. **Every new requirement must be cross-referenced against the existing
   requirements before it lands.** Check `docs/users.md`,
   `docs/non-functional-requirements.md`, and the rule docs for anything the
   addition contradicts or duplicates. If you find an inconsistency, **flag it
   and propose a fix, then ask the user before applying it** — surface the
   conflict with a recommended resolution rather than silently reconciling it
   yourself.
3. Agents must not add transient information to anything /docs
4. Agents must ask the user before adding anything in /docs
5. Any residual technical debt identified or intentionally deferred during
   implementing a feature can go into `kanban/tech-debt.md` as a checklist item
   `[ ] tech debt heading. Description`
6. Any newly identified feature identified during implementing a feature, which
   would be out of scope of the current feature, becomes a new story stub in
   `kanban/backlog/` — never a silent scope increase on the story in hand.

## Working the board

`kanban/` is the single source of truth for what is planned, in flight and done.
Four lanes — `backlog/`, `in-progress/`, `completed/`, `blocked/` — plus two
standing inventories at `kanban/` root: `tech-debt.md` (deferred debt, rule 5)
and `deferred-decisions.md` (things decided *not* to do yet, with the reasoning —
read it before "fixing" something that looks missing).

1. **One story = one markdown file**, named for the work (`catalogue-choice-draws-plan.md`).
   It starts as a short stub in `backlog/` — What, Why it matters, Before starting —
   and grows its plan (`WI-n` work items) in place. Code cites work items by path
   and number; keep the filename stable so those citations survive.
2. **Move the file between lane folders as the work progresses** — `git mv`, so
   history follows. That move *is* the status change; the lane, not prose, is the
   truth. Update the `**Status:**` header line in the same commit.
3. **Take a story into `in-progress/` before writing code**, and keep the lane
   thin — finish or park before pulling another.
4. **Park to `blocked/`** when something outside the story stops it, recording the
   blocker and what would unblock it in the file.
5. **On completion**, move to `completed/`, set the status header, and reconcile
   `tech-debt.md` and `deferred-decisions.md` — tick what the story discharged,
   add what it deferred. Completed stories are the historical record: never edit
   them to match later reality, and never delete one.
6. **Plans in `completed/` describe the tree as built.** Read them as history, not
   instructions; where two disagree, the newer one wins and says so. They cite
   `file:line` and test counts that drift — re-verify before acting on one.
7. **State lives in the tree, not in a status document.** Do not write a
   point-in-time audit of "what the code does not do" — it is stale on the next
   commit and outlives its accuracy. Open work is a `backlog/` story; a settled
   non-decision is a `deferred-decisions.md` entry.



!!!CHECK
## Pointers

- Start any domain question at `docs/soaring-domain-glossary.md`.
- "Who needs this / why" → `docs/users.md`.
- "What's the actual rule / number" → the `fai-rules` skill (class doc first,
  then family, then general; verbatim source text on demand).
- "What's the actual API / current syntax" for a chosen library (Marten,
  xunit.v3, CsCheck, ArchUnitNET, AwesomeAssertions) → the `context7` MCP
  server, rather than answering from training memory.
