# Story — Expose the pre-normalisation score over HTTP

**Status:** Backlog · **Raised:** 2026-08-26 (decision recorded in
`kanban/completed/gliderscore-replay-and-compare-harness.md`, Q1)

## What

An additive `PreNormalisationScore` on `GroupResult` /
`CompetitorTaskResultView` so grain-1 (raw flight score) comparison can run
over HTTP instead of the harness's in-process granular pipeline
(`ScoringService.InterpretFlight` → `SelectFlights` → raw `TaskResult.RawScore`
via the acceptance suite's direct provider).

## Why it matters

Q1 (asked and answered 2026-08-26) chose the in-process mechanism with zero
production change, and explicitly declined the view field *for that story*:
"if HTTP exposure is later wanted it becomes a new backlog stub, not a silent
addition here." This is that stub. Wanted only if a consumer outside the test
suite ever needs the unnormalised score — until then the in-process path works
and the engine stays untouched.

## Before starting

- Re-read Q1's rationale; nothing has changed since.
- Additive-only: existing views keep their shapes (NFR-2).
