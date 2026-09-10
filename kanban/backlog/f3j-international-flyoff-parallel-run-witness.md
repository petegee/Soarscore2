# Story stub - f3j-international-flyoff parallel-run witness

**Status:** Backlog
**Raised:** 2026-09-10 — from the `f3j-international` re-triage work: the
fly-off phase is dormant on the 16-round fixture (the fly-off never draws), so
the seed's phase-2 behaviour went unexercised; the mapping table's
`f3j-international-flyoff` row (`parallel-run-mapping.md:100`) records the
planned witness and its measured-during-planning candidates.

## What

Run the `f3j-international-flyoff` fixture under the seed
`tools/Soarscore.SeedData/json/50-f3j.json` through the parallel-run harness
and ledger the differences — the fly-off-phase twin of the completed
`f3j-international` witness (decision 1 of
`kanban/completed/seed-definition-parallel-run.md` line still cites the fly-off
as planned).

Planning-time candidates to re-measure at curation (from the mapping row):

1. **Target/cap split** — GS scored all 4 rounds to the **900 s** target
   (flights to 898 s, R1 raw 996) vs the seed Preliminary's 600-point cap;
   provable at the raw grain.
2. **Integer rounding grid** — GS HalfUp on an integer scale vs the rulebook
   Truncate-0.1 (981 vs 980.9).
3. Drops agree at 4 rounds — re-verify; the withdrawn drop claim precedent
   applies until it doesn't.

## Why it matters

The f3j-international witness showed all four candidates materialising but
exercised **no fly-off**: phase 2's parameters, promotion and tie-breaks
remain dormant deferrals. This fixture is all fly-off shape, so it is the
first pair that actually exercises the seed's phase-2 machinery — including
the parameters the completed story deliberately left unexercised
(`flyoffMinRounds` bound-but-never-read, `carryPenalties` unbound, decision-6
precedent). If the fly-off really draws here, those choices come off the
dormant list and get witnessed for real.

## Before starting

- **Deferred decision first:** `kanban/deferred-decisions.md` §Draw holds the
  fly-off-phase-draws deferral and the promotion/tie-break dormancy — read it
  and resolve what changes if this fixture's fly-off actually draws
  (promotion, `CarryPenalties` resolution, the tie-break interplay with
  `kanban/completed/operational-tie-break-resolution.md`'s landed work). The
  completed story's decision 5/6 reasoning assumed the fly-off never draws;
  that assumption may be false here and must be re-planned, not assumed.
- The `carryPenalties` bind question returns: if phase 2 is reached, the
  Flag-bind pipeline widening deferred there may become real work (decision 6
  refused widening for a never-read parameter).
- Check `kanban/in-progress/` collisions (tape story owns landing semantics,
  not this; check for parallel-run harness changes it may carry).
- The mapping row's cited configs and rule clauses are planning measurements —
  re-verify (`config`/`rule` citations in `parallel-run-mapping.md:100`)
  before trusting any count.
- A 900 s target may not be expressible as a round bind under the seed (the
  seed declares no `targetTime` parameter — the completed story's decision-4
  refusal); the 540/900 class of difference is triaged kind 1, never bound.
