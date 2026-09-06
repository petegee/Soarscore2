# Story — f3j-international parallel-run re-triage

**Status:** Backlog · **Raised:** 2026-09-06 (decision 2 of
`kanban/completed/seed-definition-parallel-run.md`: the pair's original
guaranteed-divergence claim was withdrawn; this stub carries the corrected
difference list)

## What

Run `f3j-international` under the seed class
`tools/Soarscore.SeedData/json/50-f3j.json` through the parallel-run harness
and ledger against the **corrected** difference list. The story's original
claim — guaranteed drop-policy divergence from GS's local `Drop1AtRound=8` —
is **withdrawn**: GS's `Drop1AtRound=8` equals the seed's
`applyWhenRoundsCompletedAtLeast: 8` and the FAI rule (drop beyond 7
qualification rounds, `docs/rules/f3j.md:99`). The witnessed candidate is
**normalise rounding** — GS's proven HalfUp-1dp grid vs the rulebook's
Truncate-0.1 — the same shape the ales pair disclosed as an unwitnessed
candidate. Carried alongside it: the overfly-semantics `fai-rules` question
and the promotion/fly-off spike (below).

## Why it matters

The withdrawn claim is exactly the failure mode the parallel-run discipline
exists to catch — a predicted difference that curation measurement dissolved.
The re-triage is the honest version: run the pair, witness what actually
differs, and let the rounding candidate either materialise (first sighting of
the HalfUp-vs-Truncate split the ales pair predicted but never showed) or join
the unwitnessed-candidate disclosures.

## Before starting

- **Overfly semantics (`fai-rules` question):** is GS's F3J late-landing −30
  payload the same offence the rulebook's overfly gate penalises? Settle with
  the skill before mapping the metric — the mapping policy (parent story
  decision 1) only fixes *values*, not offences.
- **Promotion / fly-off spike:** the seed's never-flown fly-off phase under
  `SplitByPromotion` — the same open question the F5J witness stub carries.
- **Anti-goal guard:** the seed class is never tuned to GS; a rounding-mode
  disagreement is reported as a triaged difference (kind 1), never reconciled
  by re-rounding either side.
