# Story — GliderScore replay-and-compare harness

**Status:** Backlog · **Raised:** 2026-08-25

## What

The golden-path test itself. For each committed fixture
(`gliderscore-golden-fixture-pipeline.md`):

1. **Replay** it into Soarscore through the public command surface only — publish the
   mapped class definition, create competition, register competitors, prescribed draw
   (`prescribed-draw-import.md`), open entries/flights, capture measurements and
   penalties from the raw `Scores` half, complete task rounds, finalise.
2. **Score** via `/competition-result` (+ `/task-round-result` per round).
3. **Compare** against GliderScore's persisted oracle at three grains:
   raw flight score · per-round normalised score · final ranking — exact at GS's
   decimal/round setting.
4. **Report** a diff table (per pilot × round × group) and triage every mismatch:
   *importer bug* / *our engine defect* / *intentional divergence* (GS breaks the
   rules; we keep ours and record it).

Placement: reuse the acceptance harness pattern (`SOARSCORE_TEST_STORE` +
`WebApplicationFactory` + Testcontainers) — either a tagged feature inside
`Soarscore.Acceptance.Tests` or a sibling `Compatibility.Tests` project sharing its
`Support/`; decide when the first fixture exists.

## Why it matters

This is the gap-hunting engine: real completed competitions become regression tests,
and every new export probes the model where the prior art actually varied. First green
case = the ALES sample comp already analysed.

## Before starting

Blocked on: arithmetic resolved, first fixture committed, ~~prescribed draw available~~ (discharged 2026-08-26 by `kanban/in-progress/prescribed-draw-import.md`).
Scope guard v1: single-class, no-team, no-series, no-merged/prelim comps — fixtures
touching the §6 concept gaps stay skip-listed until those concepts exist.
