# Story — Aggregate-scoped Zero* records zero nothing; entry-scoped Disqualify does nothing

**Status:** Backlog · **Raised:** 2026-08-27 — mirrors of the no-ops left open
by `kanban/in-progress/entry-scoped-deduct-points-penalties-inert.md` (WI-0),
which wired the first direction (entry-scoped `DeductPoints` → raw stage) but
deliberately narrowed rather than solved its siblings.

## What

Two gaps in `PenaltyEngine` routing, plus one hardening idea:

1. **(a) TaskRound/Competition-scoped record of a Zero*-carrying definition
   zeroes nothing.** `ScoringService.GetAggregatePenalties`
   (`src/Soarscore.Domain/Scoring/ScoringService.cs`) feeds aggregate-scoped
   records only into `ApplyAggregatePenalties`, which honours
   `DeductPoints`/`Disqualify` and ignores `ZeroFlight`/`ZeroRound`/`ZeroTask`.
   Live example: F3B's own `nonConformingWinch` is ZeroFlight + DeductPoints
   1000 (`tools/Soarscore.SeedData/SeedF3B.cs`); recorded at TaskRound or
   Competition scope, its zeroing half silently does nothing. Fix shape: route
   Zero* effects into the group walk using `Penalty.TaskRound` / subject
   filters to find which task-round(s) the record names.
2. **(b) `Disqualify` on an entry-scoped record sets no flag.** Entry-penalty
   records reach only `ApplyRawPenalties`; a Disqualify effect there has no
   action today. Fix shape: carry an aggregate-stage action out of a raw-owned
   record (the reverse direction of what
   entry-scoped-deduct-points-penalties-inert did).
3. **(c) Hardening idea:** let a `PenaltyDefinition` optionally declare its
   permitted recording scopes, so mis-scopeable definitions are caught at
   adoption instead of at scoring time. **New field on the class model —
   glossary-gated:** argue it in `docs/soaring-domain-glossary.md` terms and get
   approval before adding anything to `/docs`.

## Why it matters

Both (a) and (b) are CD-visible no-ops on an immutable audit trail — the same
trust-model objection that motivated the parent story. The decision there
(scoping argument, "option 2 rejected") covers why *routing*, not adoption-time
refusal, is the preferred family of fix; re-read it before contradicting.

## Before starting

- Re-read the parent story's D1 ("stage follows recorded scope") — these gaps
  are its deliberate residuals; any fix must stay consistent with it.
- Check whether any fixture/seed class actually records Zero*-carrying or
  Disqualify-carrying definitions at aggregate scope / entry scope
  respectively; if none does, consider whether the stub stays parked in
  `deferred-decisions.md` territory.
- (c) requires glossary approval BEFORE any `/docs` edit (housekeeping rule 4).
