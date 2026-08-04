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

No application code yet.

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




!!!CHECK
## Pointers


- Start any domain question at `docs/soaring-domain-glossary.md`.
- "Who needs this / why" → `docs/users.md`.
- "What's the actual rule / number" → the `fai-rules` skill (class doc first,
  then family, then general; verbatim source text on demand).
