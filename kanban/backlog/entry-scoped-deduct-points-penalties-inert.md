# Story — Entry-scoped point-deduction penalties are inert

**Status:** Backlog · **Raised:** 2026-08-26 (found by
`kanban/completed/gliderscore-replay-and-compare-harness.md` WI-3)

## What

`PenaltyEngine.ApplyRawPenalties`
(`src/Soarscore.Domain/Scoring/PenaltyEngine.cs:29-63`) honours only `ZeroFlight`
/ `ZeroRound` / `ZeroTask` effects; `DeductPoints` is aggregate-only
(`ApplyAggregatePenalties`). But `RecordEntryPenalty` accepts any declared
infraction type, and a class definition may declare an entry-scoped
`deduct <pts>` penalty — the command validates it as recorded, then no score
changes anywhere: a CD-visible no-op.

Two candidate resolutions, to be argued in-story:

1. **Wire it** — route Flight/Entry-scoped `DeductPoints` into the pipeline at
   GS's placement: inside raw pre-normalisation
   (`ClassDefinition.cs`'s design comment already anticipates effect-derived
   stages). This is what GliderScore's late-landing deduction does
   (`FltPenalty` subtracted inside `RawScore`), and what harness fixture
   `f3j-international` had to express as class data instead (captured metric +
   rate term) because the penalty route was inert.
2. **Reject it** — refuse a Flight/Entry-scoped declaration whose only effects
   are aggregate-stage (`DeductPoints`) at adoption or record time, so the
   no-op cannot be declared.

## Why it matters

A trust-model audit trail that records a penalty which changes nothing is worse
than refusing it. And until resolved, imported rulebooks whose deductions act
pre-normalisation must be authored around (as f3j-international was), not
declared.

## Before starting

- Read `ScoringService.GetEntryPenalties` / `GetAggregatePenalties`
  (`src/Soarscore.Domain/Scoring/ScoringService.cs:441-464`) and the
  exclusion-group semantics before choosing wire-vs-reject — a wired
  entry-scoped deduction must say how it interacts with exclusion groups.
- Cross-check the FAI/NZ rules for any deduction that acts pre-normalisation
  (the `fai-rules` skill); if none exists in any real rulebook, reject is
  likely right.
