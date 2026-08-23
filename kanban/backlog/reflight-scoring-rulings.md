# Reflight-scoring rulings

**Status:** Backlog · **Raised:** 2026-08-24

## What

A competition-time mechanism for recording a CD ruling of which score counts
for a re-flight, for the classes where the rulebook is silent. `reflight-groups.md`
(decision 4) records `ReflightSelection.UndefinedRequiresRuling` (F3B Task C,
F5L, NZ Class M) and the scoring pipeline fails those honestly with
`score.refLightRequiresRuling` rather than assuming. But the group physically
flew; a ruling is the ordinary, witnessed resolution — a CD decides Replacement
or BetterOf for a given re-flight, and the system has nowhere to put it.

## Why it matters

`UndefinedRequiresRuling` keeps the class model honest (the silent rulebook is
not silently assumed), but an honest refusal with no way forward strands a
real contest: the group exists, scores are captured, and `score.reflightRequiresRuling`
blocks them indefinitely. A ruling mechanism is what turns "the rules are silent"
from a dead end into a recorded decision.

## Before starting

- **Cite the decision**: `kanban/completed/reflight-groups.md` decision 4 and the
  `score.refLightRequiresRuling` code, and the class-rule docs it points at
  (F3B Task C — F3B.1.5 e names Tasks A/B only; F5L 5.5.12.9; NZ.3.12.5 l:
  NZ Class M grants a re-flight and never says which score counts).
- **Design the shape.** The ruling is per-reflight (one particular competitor /
  group, one of Replacement / BetterOf), not class-wide — the natural home is a
  new Competition-level value recorded as its own event so the log keeps the
  decision. Does it extend the existing reflight-role model or sit beside it?
- **Reconcile with scoring.** `ReflightSelector.Select` currently fails on
  `UndefinedRequiresRuling`; a recorded ruling must be handed to the selection
  the same way the class rule is, only at runtime instead of at adoption. This
  is data, never a branch on class (CLAUDE.md's core law).
- **Check NFR-4** — the ruling is a recorded fact, not a gate; capturing scores
  around an unresolved reflight must not be refused.