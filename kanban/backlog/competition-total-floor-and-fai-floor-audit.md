# Story — Competition-grain score floor (5.5.11.12 n), and the FAI-class floor audit

**Status:** Backlog (raised 2026-09-27 as a split from
`kanban/completed/minimum-score-floor.md` — owner decision 2026-09-27, design
question 2: keep that story the round-grain fix).

## What

Two related residuals of the score floor, deliberately not landed with the
task-round floor:

1. **The competition total.** FAI F5J `5.5.11.12 n`
   (`docs/rules/source-docs/f5-electric-2026.md:1007-1011`) — "All penalties
   are cumulative and will be deducted from the competitor's total score at
   the end of the preliminary rounds … In case the total score after
   deduction of the penalties is negative, a zero (0) score will be
   recorded." The engine subtracts aggregate penalties with no clamp
   (`src/Soarscore.Domain/Scoring/ScoringService.cs` final assembly,
   `Score: totalScore - penaltyResult.Deduction`), and the D4-era "Aggregate
   floor ≥ 0" deferral (`kanban/completed/normalisation-lower-clamp.md` D4)
   rested on "no fixture evidence" — but the RULEBOOK evidence exists for the
   F5J family (carried by the F5J NDC definition via NZ.2.4(c), which
   disregards only 5.5.11.12.m). A 5.5.11.12-f-floored round (recorded 0)
   plus an aggregate penalty lands below zero today.
2. **The FAI-class floor audit.** `minimum-score-floor.md` seeded the datum
   only where a class's own clause states it (85c, 85d, 30-f5j). The
   remaining normalising FAI classes — 20-f3b, 40-f5k, 50-f3j, 60-f5l — carry
   negative-rate terms and rely on `NormalisationEngine`'s normalised-grain
   clamp, which covers the common case (a positive winner) but NOT an
   **all-negative group**: there the winning raw is itself negative and the
   ratio formula inverts, so a loser can outscore the winner with normalised
   scores above 1000 (the clamp only fixes `≤ 0`). This is exactly why
   30-f5j was seeded in the floor story. Examine each class's own clause —
   seed the datum where stated; where the rulebook is silent, record it as a
   Contest-Director question, not an inference (the fai-rules skill's rule).

## Why it matters

The floor story fixed the round grain the NdcScore sheet flagged. The
competition grain is the same defect one grain later, with explicit rulebook
wording, and the all-negative-group anomaly is a correctness hole in the
normalising branch that no current fixture exercises — both silently wrong
today for classes the rulebook does state a floor for.

## Cross-reference (house rule 2)

- **D4-era deferral** (`kanban/deferred-decisions.md`, "Aggregate floor ≥ 0",
  amended 2026-09-27 by the floor story landing): its "no fixture evidence"
  basis weakens here — the floor story's amendment notes the rulebook-stated
  route; this story is where the aggregate grain actually lands.
- **NFR-1/NFR-2** — same mechanism as the round floor: definition data
  (class- or phase-level datum), additive, omitted when absent.
- **NFR-4** — no capture gating; scoring interpretation only.
- **Drop/PreDrop interaction** — `ScoringService`'s final assembly computes
  `Score` and `PreDropScore` separately from the same total; settle whether
  the floor applies to both (the D4-era note has GS flooring the final score
  after penalties, `Rpt_Results_Overall_MOD.vb:2690-2712`).

## Before starting

- Settle the datum grain from the wording: `n` says "at the end of the
  preliminary rounds" — per phase, with penalties NOT carried into fly-offs;
  the fly-off scoring per `5.5.11.12` (`f5-electric-2026.md:1033`). Check the
  NZ classes' own wording too (NZ-M NDC states a max, not a floor:
  NZ.7.4(h)(v) "Max NDC score is 2600 points").
- Confirm the per-class seed list against each class's own clause.
- Property test invariant (CsCheck): for any competitor with any round
  scores and any aggregate penalties, under a class that states the
  competition floor, the final score is `max(0, sum − penalties)` and ≥ 0.

## Sibling

`landing-zero-and-flyaway-encoding.md` shares nothing mechanically with this
story, but if it lands first the same drift-guard run should cover both
(they both edit seed definitions).