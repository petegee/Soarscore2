# Story — Grow the Gliderscore fixture corpus

**Status:** Backlog · **Raised:** 2026-08-25

## What

Carry out the remainder of WI-4 of
`kanban/completed/gliderscore-golden-fixture-pipeline.md`: add more real
GliderScore competitions as fixtures. Each addition is small — extract → curate →
validate → index — using the tooling already in
`tests/GliderscoreFixtures/extract/`.

The corpus manifest `tests/GliderscoreFixtures/index.md` records what to hunt
(diversity wanted) and the standing skip reasons. Current state: one active
fixture (`ales-sample-comp`), which exercises none of the diversity targets.

## Why it matters

Corpus diversity (classes, multi-group rounds, re-flights, drop thresholds,
`Decs ≥ 1`) is what makes the replay/compare harness's gap-hunt valuable — see
the parent story's *Why it matters*.

## Before starting

- Blocked on source material: GliderScore exports must come from Pete's install
  (*Export Competition(s)* produces a Jet `.mdb`; the shipped example comps are
  candidates). Nothing further exists on this machine — the only other database
  found is a SQL Server NTbackup of the eScoring web DB, unreadable by the Jet
  extractor.
- PII check at curation is mandatory before committing any source or entries
  (real comps carry members' personal data — blank or redact).
- Respect the standing skip reasons in `index.md` (§6 concept gaps; F3B-style
  multi-task-per-round comps).
- Update `index.md` with every addition, active or skipped.
