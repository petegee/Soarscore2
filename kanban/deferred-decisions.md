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

- **Fisher/SQLite is built and tested, but not yet *announced* as a supported
  deployment.** **Decided 2026-08-16** (`kanban/completed/multi-backend-deployment.md`).
  The code ships it: `Soarscore:Store=sqlite` composes the whole system on a SQLite file,
  and every store-backed test plus the whole BDD acceptance suite passes on it. What is
  deliberately not done is the *claim*, because Fisher is **0.7.1** — pre-1.0, where a
  minor bump may still be a breaking one, which is also why the package version is pinned
  rather than floated. The story's own "Before starting" proposed exactly this split:
  gate a deployment claim on 1.0, allow the test-store use immediately. Nothing in the
  code changes when Fisher reaches 1.0; this is a README/§Decision sentence and a version
  bump. LADR-0001 §8's "gated on Fisher reaching 1.0" still stands and is the reason.

- **Polecat / SQL Server is not built.** **Decided 2026-08-16**
  (`kanban/completed/multi-backend-deployment.md`). The story was raised for three
  stores and shipped two. The shape it set out to prove — one shared adapter body plus a
  thin composition root per backend — is proved by a *second* store; a third adds cost
  without adding evidence until someone actually wants SQL Server. Nothing about the
  code blocks it: `JasperFxEventStore` has four abstract members, and answering them for
  Polecat is the whole job (`FisherEventStore.cs` is 140 lines including its comments,
  most of them explaining findings that a third backend would not have to rediscover).
  The one thing a Polecat author must not assume is that `AppendExpectedVersion` can be
  inherited — see the note there; Marten and Fisher disagree, so a third store must be
  measured, not guessed.

- **`Soarscore.Infrastructure` is one assembly for both stores.** **Decided 2026-08-16**,
  same story. A SQLite deployment therefore carries an Npgsql it never loads, and vice
  versa. Splitting into `Soarscore.Infrastructure.Marten` / `.Fisher` costs a project per
  store plus edits to the layer rules, the Api and three test projects, and buys a few
  hundred KB at a scale where the whole database is a file on a laptop. Revisit if
  Polecat lands, or if a deployment ever cares about assembly count.

- **The JasperFx compliance suite is not enrolled.** **Decided 2026-08-16**, same story.
  `JasperFx.Events.ComplianceTests` proves *Fisher* correct, which is JasperFx's job and
  already done. Our suites' job is to prove *Soarscore* correct on Fisher — a different
  question, answered by running every existing store-backed test and the whole BDD
  acceptance suite against both backends.

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

## Task-round lifecycle and finalisation

Deferred by `kanban/completed/task-round-lifecycle.md` (2026-08-18).

- **Phase-scope finalisation and `PromotionRule`.** `Finalisation.Scope` discriminates
  Phase and Competition; only Competition is built. Phase scope exists to name who was
  *promoted* into the next phase, and no second phase can be drawn
  (`Competition.DrawPhase` fails on `!Phases.IsEmpty`), so `PromotionRule` has nothing to
  promote into. `DeclaredResult.Promoted` is written `false` throughout.
- **Reopening a finalisation (revision ≥ 2).** The model supports it
  (`Finalisation.Revision`, "nothing is overwritten") and `Competition.Finalise` computes
  `Revision` generically, but no read model or query surfaces a finalisation, so a second
  revision would be write-only. Becomes a backlog stub if a CD asks for it.
- **Blocking score capture on `Finalised`.** `OpenEntry` closes on task-round state,
  which is the right granularity. A competition-level write lock is a separate concern
  and no rule requires one.
- **`TaskRoundState.InProgress` stays unreachable.** A `TaskRoundInProgress` event
  emitted on the first Entry opened was considered and rejected: it would make
  `OpenEntryHandler` append to two streams on the highest-volume write path, for a signal
  that is already derivable and that nothing reads. The debt it was meant to close was
  closed differently — see `kanban/tech-debt.md`'s round-scoped-rebind item.
- **Automatic completion, and deriving completion from recorded data.** *Not* deferrals
  but a standing stance, recorded here so it is not mistaken for an unfinished item:
  nothing infers "the round is done because everyone flew" — the CD says so (NFR-4).
  Presence of data can prove a task-round is *not* ready; absence can never prove it is,
  because a pilot who took three of five allowed launches, a metric legitimately absent,
  and a `NoResult` all look identical to "not typed in yet". A later thread proposing to
  infer completion should reopen that plan's governing-principle section rather than
  treat this as an open box. The read-side indicator that shows without deciding is
  `kanban/backlog/entry-completeness-indicator.md`.

## Competition class model

- **The `.class` notation parser** (`docs/competition-class-notation.md` is a writing
  notation, not an input format) and **class-definition drift detection** — both settled
  out of scope.

## Score capture and corrections

- **Correcting a flight's launch time is not built, and `Flight.LaunchAt` is not a
  scoring input.** **Decided 2026-08-18** (`kanban/in-progress/amend-a-measurement.md`,
  working WI-6). The thread began intending to close `FlightOpened`'s uncorrectable
  `launchAt` alongside `MeasurementAmended`. A rule check reversed that: **no rule in
  either rulebook requires the clock time at which a flight was launched.** The rule
  map's "What the timer records" table lists flight-time precision, landing distance
  and launch height across all seven FAI classes — durations, distances and heights,
  no wall-clock instant. Every timing-validity rule is a captured *flag* instead:
  `launchedInWorkingTime` (`F3K.7`), `landedWithinWindow` (`F3K.9.3`),
  `launchedOnSignal` (`F3K.11.3`), `launchedWithin30s` (F3F). F3B Task C records
  elapsed leg time; F5J's AMRT is a motor-run duration; reflight adjudication turns
  on an official witnessing the hindrance.

  It could not have been otherwise: deriving flight validity in the core system by
  comparing `LaunchAt` against the working-time window is exactly the class-specific
  leak `TimeWindow`'s doc comment (`src/Soarscore.Domain/Entries/Entry.cs:32-45`) and
  CLAUDE.md's core architectural law forbid — the class model owns it as data, via
  `TaskDefinition.FlightValidWhen`. So the scoring-relevant fact about a launch is a
  `Measurement`, corrected by `MeasurementAmended` like any other, and a mistyped
  `LaunchAt` has no scoring consequence now or later.

  Do not reopen this as "the launch amendment we forgot". The follow-on question —
  whether the field should exist at all — is a story, not a deferral:
  `kanban/backlog/remove-flight-launchat.md`.

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
