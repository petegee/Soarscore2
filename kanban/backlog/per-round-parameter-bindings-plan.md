# Plan — Per-round parameter bindings

**Status:** Backlog · **Date:** 2026-08-16

## What

`ParameterBinding` grows an optional round (and, once flyoff draws land, phase)
scope, so a CD can override a `ParameterBindingPoint.PerRound` parameter for one
specific round rather than only setting it once for the whole competition.

## Why it matters

The six `ParameterBindingPoint.PerRound` parameters in the corpus — all F3K's,
`workingTime.A/B/E/L` and `maxFlight.B/L` — all carry declared defaults, so this
is an **override** capability, not a blocker: F3K is fully drawable, floppable
and scorable without it (see `kanban/completed/catalogue-choice-draws-plan.md`).
It buys the CD the ability to, for example, drop task E's working time to 900 s
for one round.

This was deferred *into* `catalogue-choice-draws-plan.md` by
`bind-parameter-steel-thread-plan.md` finding 1, on the grounds that "binding
the working time for round 3 is meaningless until round 3 has a task." That
precondition is now discharged — rounds have tasks — but
`catalogue-choice-draws-plan.md` (2026-08-16) decided this stays a **separate**
follow-on thread rather than landing alongside the draw change, because (1) it
unblocks nothing today, and (2) it is a different change in a different place:
`ParameterBinding` grows a round scope, and `BindParameter`, `ParameterResolver`,
`TaskResolver` and `Competition.OpenEntry` all grow round context — landing it
alongside a draw change would put two unrelated diffs in one review.

## Shape, as far as the prior thread's design settled it

Recorded in `kanban/completed/catalogue-choice-draws-plan.md`, Appendix A, so it
is not re-derived. Summary:

- `ParameterBinding` grows an optional round scope. Since a `PerRound`
  parameter is named per *task* (`workingTime.A`) and each round has exactly
  one task, a round ordinal is sufficient scope; add a phase ordinal at the
  same time (cheaper than adding one twice once flyoff draws land).
- Resolution order: round-scoped binding → unscoped binding → declared default
  → throw. `ParameterResolver` takes the round as context; `TaskResolver` and
  `Competition.OpenEntry` already know the round ordinal and pass it through.
- `Competition.BindParameter` validates that a round-scoped binding names a
  round that exists and whose task actually consumes that parameter — only
  possible now that rounds have tasks.
- **Open question, not settled by the prior thread:** the freeze rule. A
  `PerRound` binding is legitimately made after the draw (unlike
  `CompetitionSetup`) but presumably not after that round's first flight.
  `ValidateParameterNotFrozen` currently keys on `BoundAt` alone.

## Before starting

- Read `kanban/completed/catalogue-choice-draws-plan.md` Appendix A in full.
- Cross-reference `kanban/deferred-decisions.md`'s "Per-round parameter
  bindings" entry.
- Check whether any other class in the corpus has grown `PerRound` parameters
  since this stub was written — today it is F3K only.
