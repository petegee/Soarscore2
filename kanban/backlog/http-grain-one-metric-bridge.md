# Story — Evaluate `scoreNormalised` terms against decoded slot metrics so option-2 fixtures also lose their in-process grain-1 dependency

**Status:** Backlog · **Raised:** 2026-08-29 from
`kanban/completed/pre-normalisation-score-view-field.md` WI-4 (house rule 6)

## What

Extend the Gliderscore comparator so fixtures whose tasks carry
`scoreNormalised` terms (option-2: landing points authored there) can still
take the HTTP grain-1 path — by evaluating those terms against each slot's
decoded flight metrics (read from the replayed entry streams) and adding the
contributions onto the fetched `preNormalisationScore`, replacing
`GsEquivalentRaw`'s in-process recompute with a fetch + a mirror-evaluated
composition.

## Why it matters

`pre-normalisation-score-view-field.md` flipped grain 1 to
`GET /task-round-result`'s `preNormalisationScore` for the nine
`scoreNormalised`-free fixtures. `ales-sample-comp` — the one active fixture
whose task D authors landing points inside `scoreNormalised` — still runs the
full in-process pipeline copy (`CompareRawGrainAsync` + `GsEquivalentRaw` +
`EvaluatePostNormalisationTerm` + `EvaluateLookup`) because HTTP does not
carry per-flight metrics. That keeps a whole second comparison mechanism, and
its term-kind mirror, alive for one fixture.

## Before starting

- `kanban/completed/pre-normalisation-score-view-field.md` as-built notes and
  `Comparator.cs`'s grain-1 header paragraph.
- The mirror (`EvaluatePostNormalisationTerm`) supports only `ConstantTerm`
  and `LookupTerm` and refuses other kinds loudly — decide whether that stays
  sufficient for `ales-sample-comp`'s terms before promising a full flip.
- The engine arithmetic stays untouched; this is comparator-side only.
- The transitional-parity-gate pattern from the completed story (flip behind
  an ` ours == fetched` gate, run green, retire) is the proven way to flip
  without behaviour change.
