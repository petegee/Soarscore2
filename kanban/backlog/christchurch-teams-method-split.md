# Stub — christchurch teams method split (NbrForTeamScore≠3)

**Status:** Backlog · **Raised:** 2026-10-03 (house rule 6 stub from
`kanban/completed/gs_10_teams-grain-parallel-comparison.md` WI-4 — never
silent scope growth)
**Sequence:** unscheduled · after GS 10

## What

Extend the parallel-run teams grain to `NbrForTeamScore≠3` pairs — today the
grain's gate opens only for the MVP's own method (`NbrForTeamScore == 3`,
`Comparator.TeamGrainOverlap`), and a different count is a different method
that is never emulated (standing T1 decision in
`kanban/deferred-decisions.md`).

## Why it matters

`f5j-christchurch-2019` declares `UseTeams=true` with `NbrForTeamScore=2`
(a stored default) but all 18 `CompPilots` carry `Team='0'` — the gate stays
shut on two arms at once (unpopulated AND non-3 method), and the pair's
ledger note discloses exactly that. A future pair with POPULATED teams under
a non-3 count would need a decided comparison shape before any run: the
method split is its own witnessed difference class (GS's N-member sum vs the
MVP's fixed three), not an emulation target.

## Scope

Decide the shape (comparison against what oracle, what rows, what ledger
kind) before any implementation. Never bend `bestThreeScoreSum` to the knob
(R1 discipline). This stub records the obligation; it proposes no design.
