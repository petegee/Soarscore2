# Story — F5J Christchurch parallel-run witness (the guaranteed divergence)

**Status:** Backlog · **Raised:** 2026-09-06 (decision 2 of
`kanban/completed/seed-definition-parallel-run.md`: the guaranteed-divergence
witness role moves here from the withdrawn f3j-international claim)

## What

Run `f5j-christchurch-2019` under the seed class
`tools/Soarscore.SeedData/json/30-f5j.json` through the parallel-run harness
(WI-1..WI-3 of the parent story) and ledger the differences against the triaged
set. This pair is expected to split **provably at the final-aggregate grain**:
all three F5J fixtures have **no drop thresholds configured** (verified null
`Drop1AtRound`/`Drop2AtRound`) against the rulebook's drop-from-5
(`docs/rules/f5j.md:90`) over 11 scored rounds — GS's local practice summed
everything; the seed class drops the lowest round score. The driver already
captures F5J launch height, so the pair runs without new capture plumbing.

## Why it matters

The ales pair — the parallel-run machinery's proof pair — came out
near-exact: the witnessed difference set was three raw-grain entries with
identical final placings. The parallel-run claim has therefore never yet been
seen producing a *visible* split on the day, which is the product's whole
point ("what would have differed"). This pair guarantees one, and it is the
first FAI seed run through the harness — the metric-mapping policy (P1/P2)
gets its first real exercise.

## Before starting

- **`startHeightRecorded` (P2 derive vs P1 assume):** GS's
  `FlightScoreDeduction` height payload is the finer fact the seed metric
  follows — derive rather than assume where it exists — and the harder
  question is the sibling story's pending precedent: **is an unrecorded
  height a valid flight?** (`kanban/completed/metric-absence-semantics.md`
  WI-4's F5J case — the seed declares no assumption and such flights run as
  Pending.) Resolve with the `fai-rules` skill, not by symmetry.
- **`overflySeconds` P1/P2:** assumed 0 without a contrary record per the
  parent story's policy, plus whatever the `fai-rules` skill says about the
  payload's semantics (the same family of question the f3j re-triage stub
  carries).
- **The fly-off / `SplitByPromotion` spike:** the seed's fly-off phase never
  flew in any fixture; the promotion machinery is the same open question the
  f3j pair raises. Scope it before scoring, not after.
- **Anti-goal guard:** the seed class is never tuned to GS — a GS-vs-rulebook
  disagreement (the drop thresholds are one) is reported, not reconciled.
- Pairing is owner-confirmed (parent story decision 2); the corpus-wide
  mapping-table stub's per-pair NZ rule governs the *unpaired* fixtures, not
  this one.
