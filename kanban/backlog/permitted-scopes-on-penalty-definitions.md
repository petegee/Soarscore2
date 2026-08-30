# Story — Permitted scopes on PenaltyDefinition (adoption-time scope hardening)

**Status:** Backlog · **Raised:** 2026-08-30 — WI-6 of
`kanban/completed/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md`,
spun out at that story's closeout by user approval.

## What

Let a `PenaltyDefinition` optionally declare the scopes it may be recorded at:
an optional `PenaltyScope[]? PermittedScopes` field (null ⇒ any scope, so every
existing class definition is untouched). The write side enforces it —
`Competition.RecordPenalty` and `Entry.RecordPenalty` gain a check after the
infraction-type lookup that refuses a record whose scope is not permitted,
defect `recordPenalty.scopeNotAllowed` — plus an adoption check and unit tests,
and the glossary / class-diagram `PenaltyDefinition` note.

## Why it matters

A definition today may be recorded at any scope the write side allows, and the
parent story proved scope+effect *combinations* cannot be judged at adoption
because the definition carries no scope knowledge. This field lets a class
author say "this infraction is a flight-level fact" and get
`recordPenalty.scopeNotAllowed` at recording time instead of discovering at
score time that the record landed where its effects cannot act. It is the
adoption-time mirror of that story's D-A3 record-time completeness check, and
it is data-driven (NFR-1) — the core system reads the list generically, never
branching on a class.

## Before starting

- **/docs approval: GRANTED 2026-08-30** (housekeeping rule 4) — the user
  approved exactly the argument in the parent story's WI-6, which is the basis
  of this story. When the field lands, make the glossary and class-diagram
  `PenaltyDefinition` notes in the same change.
- Green-field: no events to preserve, no migration (CLAUDE.md project status).
- Scope discipline: one optional field, two decide-function checks, one
  adoption check, unit tests. No engine changes — the read path already routes
  correctly (D-A1/D-A2 of the parent story); this only refuses bad records
  earlier.
- Cross-reference NFR-1/NFR-2 (additive-only, class-model-owned) and NFR-4
  (record-time validation constrains payload correctness, not when a record
  may be made) before starting — housekeeping rule 2.
