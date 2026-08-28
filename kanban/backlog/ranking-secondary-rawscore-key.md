# Story — Ranking's secondary key: RawScore tie-break

**Status:** Backlog · **Raised:** 2026-08-26 (found by
`kanban/completed/gliderscore-replay-and-compare-harness.md` — story trap 3,
fired for real on `jerilderie-2010`)

## What

`RankingEngine.Rank` sorts by Score only
(`src/Soarscore.Domain/Scoring/RankingEngine.cs:47`). GliderScore's ladder is
**Score DESC, RawScore DESC** (`Rpt_Results_Calculations_MOD.vb:748`; arithmetic
story, *Ranking & tie-breaks*), where its `RawScore` is the pre-drop normalised
total. Where two pilots tie on Score with different raw sums, GS displays
distinct ranks and we display a shared place.

Witness: jerilderie-2010 pilots P4/P21 tie at Score 11784; GS separates them
8/9 via RawScore 9283 vs 9257; we place them jointly 8th. Ledgered in the
fixture's `divergences.json` citing trap 3.

## Why it matters

Every fixture with a Score tie and distinct raw totals will flag grain 3. The
harness works as intended — but the divergence is an engine gap, not a
rulebook conflict: FAI/NZ ladders also break Score ties before sharing ranks.

## Before starting

- Decide the secondary key's definition for us (our analogue of GS's pre-drop
  total) and whether F3K's further rescue chain (arithmetic story rung 3) stays
  out of scope.
- Property-test candidate: ranking must be a total preorder consistent with a
  stated key ladder — name the invariant explicitly per CLAUDE.md.
- The jerilderie ledger entry should be discharged by this story.
