# Deferred decisions

Things the project has **decided** not to do yet, with the reasoning. Recorded so
nobody "fixes" them by mistake, and so a thread that needs one reopens it deliberately
rather than rediscovering it.

Not a backlog — a backlog item is work waiting for a turn; an item here is a settled
decision. When one is taken up, move it into a `backlog/` story and delete it from
here, carrying the reasoning across.

Drained from `gap.md` (deleted 2026-08-16); decisions dated where the record has a date.

## Draw

- **Redraw / draw acceptance.** Acceptance criteria are already drafted at
  `kanban/completed/phase-drawn-steel-thread-plan.md:110-121` — the `Draw.Status`
  vocabulary, `AcceptDraw`/`RejectDraw`, and moving `ValidateFieldNotFrozen` off
  `Phases.IsEmpty`.
- **Flyoff-phase draws.** The current draw's field is unconditionally "every
  non-withdrawn Competitor". Flyoff field selection is a different algorithm, not a
  variation on this one.
- **Multi-task rounds (F3B).** `FixedSequence` with `tasksPerRound: 3`, structurally
  rejected by `Competition.DrawPhase` with `drawPhase.unsupportedRoundComposition`. A
  different problem from catalogue choice, refused at the same single check.
- **Per-round parameter bindings.** `ParameterBinding` carries no round or phase
  ordinal, so `ParameterBindingPoint.PerRound` is *unrepresentable*. Six parameters are
  affected, all F3K's. **Decided 2026-08-08: deferred into the catalogue-choice story**
  (`kanban/backlog/catalogue-choice-draws-plan.md`) — binding "the working time for
  round 3" is meaningless until round 3 has a task, and adding the scope alone unblocks
  nothing, because F3K is independently blocked by its round composition. Reasoning at
  `kanban/completed/bind-parameter-steel-thread-plan.md`, finding 1.

## Competition class model

- **The `.class` notation parser** (`docs/competition-class-notation.md` is a writing
  notation, not an input format) and **class-definition drift detection** — both settled
  out of scope.

---

## Decisions that have since been taken up

Kept briefly, because the reasoning still binds the code that resulted.

- **Catalogue-choice rounds.** **Decided 2026-08-08: each round's task is set at draw
  time** — `PhaseDrawn` grows a per-round task selection rather than a separate later
  event. Now a planned story, `kanban/backlog/catalogue-choice-draws-plan.md`.
- **`Parameter.DefaultValue` was inert.** **Decided 2026-08-08: `ParameterResolver`
  falls back to the declared default**, rather than seeding `ParameterBound` events at
  `CreateCompetition`. The objection to a fallback does not hold —
  `AdoptedRules.Definition` is an immutable copy already in the log, so defaults are
  auditable and the effective value is reconstructible — and seeding would silently
  defeat `RulesAmended`'s retroactive intent. Shipped as WI-2 of
  `kanban/completed/bind-parameter-steel-thread-plan.md`.
