# CLAUDE.md — Soarscore

Master context for all Claude sessions/agents on this repo. Keep it succinct;
only add fundamentals here; grow it as the project matures but be mindful of 
size and each addition should be justifiable. If something here goes stale, fix it. 

## What this is

Soarscore is a **scoring and running system for radio-control glider
competitions** (FAI classes F3B, F3J, F3K, F5J, F5K, F5L). It manages master
data, competition setup, fair round-by-round draws, live on-device score
capture, mid-contest adjustments, and results reporting.

The fundamental domain concept glossary is here: docs/soaring-domain-glossary.md 
No new concepts should be added; always stop and surface to the user any new
concepts and justification for adding them. Do not add to the glossary without
explicit approval.


## Project status

Currently under active green-field development. There are no current users, 
and no existing real data that needs to be preserved, or migrated.

## Repository map
TBC


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
Backing detail: [NFR-1](docs/requirements/non-functional.md#nfr-1--one-centralised-flexible-task-model)
(one place that knows a class's shape), [NFR-2](docs/requirements/non-functional.md#nfr-2--additive-only-extensibility-for-new-competition-types)
(extension is additive-only), [D12](docs/requirements/decisions.md#d12--contest-classes-are-modelled-as-seeded-cloneable-definitions)
(the concrete Contest Class Model shape).

## Key constraints (recorded in docs/requirements/decisions.md)

- **Trust model:** club-level tool for a small, trusted NZ group. No auth, no
  score sign-off; an **immutable event log of all mutations** provides
  auditability instead.
- **Scale:** ≤ 20 pilots, ≤ 8 rounds/day, 1–2 day events;

## Working conventions

- These are **high-level, implementation-agnostic** requirements — they describe
  *what a scoring system must do*, not any specific UI or codebase. 
- Prose is wrapped at ~80 columns. Match the existing markdown style.

## House-keeping rules

1. **`docs/requirements/rules/` is derived from the official FAI rules 
   (`docs/requirements/rules/source-docs`) and is read-only to the 
   software process.** Do **not** edit it to fit new software
   requirements or MVP-scoping decisions — it tracks the sport, not the product.
   Any requirement or software we generate **must not contravene** these rules
2. **Every new requirement must be cross-referenced against the existing
   requirements before it lands.** Check `high-level-requirements.md`,
   `users.md`, and the rule docs for anything the addition contradicts or
   duplicates. If you find an inconsistency, **flag it and propose a fix, then
   ask the user before applying it** — surface the conflict with a recommended
   resolution rather than silently reconciling it yourself.





!!!CHECK
## Pointers


- Start any domain question at `docs/requirements/high-level-requirements.md`.
- "Who needs this / why" → `docs/requirements/users.md`.
- "What's the actual rule / number" → `docs/requirements/rules/` (class doc first,
  then family, then general).
