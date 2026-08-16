# Deferred decisions

Things the project has **decided** not to do yet, with the reasoning. Recorded so
nobody "fixes" them by mistake, and so a thread that needs one reopens it deliberately
rather than rediscovering it.

Not a backlog — a backlog item is work waiting for a turn; an item here is a settled
decision. When one is taken up, move it into a `backlog/` story and delete it from
here, carrying the reasoning across.

Drained from `gap.md` (deleted 2026-08-16); decisions dated where the record has a date.

## Event store

- **`IEventStore.ReadAllAsync` stays, and stays per-store.** **Decided 2026-08-16**
  (`kanban/completed/jasperfx-shared-store-contracts.md`). It is the one method on the
  port with no equivalent on the JasperFx shared contracts, and it has zero production
  callers — so the portability refactor had to either drop it or keep it deliberately.
  Kept. `IReadOnlyEventStore.QueryEventsAsync` is not a substitute: flat filters plus
  page-number paging give no sequence cursor, and therefore no replay ordering
  guarantee, which is the entire reason LADR-0001 §4.10's replay path wants the method.
  Deleting the port instead is a larger question — it changes what `IEventStore`
  promises — and should be argued on its own terms rather than as a side effect of a
  type-level refactor. It is `abstract` on `JasperFxEventStore`, so a second backend is
  forced to answer this rather than inherit a silently-wrong implementation.

- **The four query ports are not collapsed into their handlers.** **Decided
  2026-08-16**, same story. A store-agnostic `IDocumentSessionFactory` could now appear
  in `Application`, which would make `IPeopleQuery` / `IClassLibraryQuery` /
  `ICompetitionsQuery` / `IEntryQuery` and their adapters redundant. Refused: LADR-0001
  §4.2's stated reason is hexagonal dependencies pointing inward, *independently* of
  portability, and that reason is untouched by the contracts becoming portable.
  `LayerRuleTests` now excludes `JasperFx` alongside `Marten` so the temptation fails
  the build rather than being rediscovered as a judgement call.

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

## Competition class model

- **The `.class` notation parser** (`docs/competition-class-notation.md` is a writing
  notation, not an input format) and **class-definition drift detection** — both settled
  out of scope.

---

## Decisions that have since been taken up

Kept briefly, because the reasoning still binds the code that resulted.

- **Catalogue-choice rounds.** **Decided 2026-08-08: each round's task is set at draw
  time** — `PhaseDrawn` grows a per-round task selection rather than a separate later
  event. Shipped as `kanban/completed/catalogue-choice-draws-plan.md`.
- **`Parameter.DefaultValue` was inert.** **Decided 2026-08-08: `ParameterResolver`
  falls back to the declared default**, rather than seeding `ParameterBound` events at
  `CreateCompetition`. The objection to a fallback does not hold —
  `AdoptedRules.Definition` is an immutable copy already in the log, so defaults are
  auditable and the effective value is reconstructible — and seeding would silently
  defeat `RulesAmended`'s retroactive intent. Shipped as WI-2 of
  `kanban/completed/bind-parameter-steel-thread-plan.md`.
