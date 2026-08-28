# Story — Model tie-break policy as class data

**Status:** Backlog · **Raised:** 2026-08-29 (board discussion alongside
`kanban/in-progress/ranking-secondary-rawscore-key.md`; the gap is already
recorded in `docs/soaring-domain-class-diagram.md`, closing note "Tie-breaking
is not yet modelled")

## What

Tie-breaking becomes a property of the Competition Class: an ordered list of
tie-break directives that the engine reads generically, as the policy layer
above the hardcoded two-rung display ladder (Score, PreDropScore). The corpus
shows the mechanisms genuinely vary per class: comparators (best dropped
score — `F3K.10`, `5.5.10.16–10.18`; qualifying position — `F3J.11`,
`5.5.11.13`) and operational directives (an additional full round —
`F3B.2.8`; a one-task tie-break fly-off — `F3K.10`), with rulebook silence
for F5L and every NZ class (CD decision).

## Why it matters

The rule map's Tie-break row varies across every class — per the core
architectural law that makes it a field of the class model, and today it is
modelled nowhere. The ranking story hardcodes rungs 1–2 in the engine as a
class-agnostic display ladder, which is correct as far as it goes but cannot
express F3B's extra round, a tie-break fly-off, or qualifying position.

## Before starting

- The class diagram's closing note is the design constraint: two kinds of
  directive — comparison against a figure *and* scheduling more flying. The
  engine evaluates comparator rungs; an unsatisfied operational rung surfaces
  to contest flow ("class policy requires a tie-break fly-off") rather than
  resolving anything.
- Silence handling: mirror the re-flight entitlement enum's
  `UndefinedRequiresRuling` precedent (notation doc, re-flight block) for
  F5L/NZ; shared places remain the display default while a tie stands.
- Multi-drop classes: F3F drops two rounds (`F3F.1.13`) — "best single
  dropped score" and the sum of dropped (PreDropScore) diverge there. Decide
  from the source text which the comparator means.
- Naming: `RawScore` is taken (per-flight, pre-normalisation); a
  normalised-scale dropped-score rung must not reuse it.
- Team-classification tie-breaks (`C.15.6.2`) are a different ranking
  (teams, not competitors) — likely out of scope; decide early.
- Glossary / class diagram / notation changes are approval-gated (house
  rule 4); this story needs at least the diagram's unmodelled-gap note
  retired.
- Relationship to the ranking story: its D2 deferred-decisions entry (rung 3)
  and the F3J/F5J qualifying-position rung defer *to this policy layer*;
  this story absorbs that entry when it lands.
