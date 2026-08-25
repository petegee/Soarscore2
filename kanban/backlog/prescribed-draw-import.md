# Story — Prescribed-draw import capability

**Status:** Backlog · **Raised:** 2026-08-25

## What

A way to set a phase's groups explicitly — which pilots in which group, and fly order —
instead of only drawing fresh. Needed so an imported GliderScore competition can
reproduce the *realised* draw recorded in its `Scores` rows (`GroupNo`, `SeqNo`).

Open design questions:

1. Command shape: extend `DrawPhase`/`AcceptDraw` (`Competition.cs:743`) or a separate
   command (e.g. set-groups-on-phase)? Import-only affordance vs general feature?
   Real events do run manual draws, so a general capability is defensible — but that is
   a domain decision to make explicitly.
2. Validation: what must still hold (all competitors placed exactly once per round,
   group-size limits from the class definition)?
3. Glossary check: does this reuse existing draw vocabulary or introduce a concept?
   House rule — no new glossary entries without approval.

## Why it matters

Normalisation is winner-per-group (`NormalisationEngine` scales by group winner), so
group membership changes normalised scores. Re-drawing an imported comp produces
different groups and hence different numbers than the oracle — the comparison would be
noise. Identical group composition is a precondition for meaningful score comparison.

## Before starting

Read `kanban/deferred-decisions.md`'s Draw section before designing; nothing there
covers prescription today, but adjacent settled decisions may constrain the shape.
Prerequisite for `gliderscore-replay-and-compare-harness.md`; independent of the
fixture pipeline.
