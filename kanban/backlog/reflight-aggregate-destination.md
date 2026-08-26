# Story — Re-flight scores: aggregate destination ≠ entry's round

**Status:** Backlog · **Raised:** 2026-08-26 (found by
`kanban/completed/gliderscore-replay-and-compare-harness.md` WI-6 — design
recorded in that story file, "WI-6 design" section)

## What

An engine concept for scoring a flight in one task-round but aggregating the
result into **another round's ladder slot**. GliderScore keys report cells by
`OriginalRoundNo` (`Rpt_Results_Overall_MOD.vb:2698-2706`), so jerilderie-2010's
re-flight (pilot 29 flies inside R13/G1, normalises against R13/G1's basis,
but aggregates into his orig-round-12 cell) produces two live aggregate cells
from one task-round. No current aggregate or selector expresses that:
`ReflightSelector` collapses a competitor's task-round entries to ONE score,
and `PhaseAggregator` keys cells by the entry's own round.

## Why it matters

The harness replayed jerilderie-2010 with the re-flight row excluded (D5 step
1) and ledgered every arithmetic consequence (9 entries) — exact modulo the
ledger, but pilot 29's total differs by Δ284 and four pilots' places shift.
A faithful replay needs this concept.

## Before starting

- Read the WI-6 design section first — it rules out three mappings
  (appended-group basis, collapse-to-one, entry-in-R12) with numeric evidence.
- Design must respect the reflight shape guard and not corrupt hosting groups'
  normalisation bases.
- Discharging it should retire jerilderie-2010's D5 ledger entries.
