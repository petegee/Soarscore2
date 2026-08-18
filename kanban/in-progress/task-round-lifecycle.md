# Plan — Task-round lifecycle: `TaskRoundCompleted` / `TaskRoundReopened` / `TaskRoundAnnulled` / `Finalised`

**Status:** In progress — planned, not yet built · **Raised:** 2026-08-16 · **Planned:** 2026-08-17 · **Revised:** 2026-08-18

## What

Three mapped, folded, unreachable `CompetitionEvent` types get decide functions,
commands, handlers and endpoints — `TaskRoundCompleted`, `TaskRoundAnnulled` and
`Finalised` (competition scope only) — plus one new event, `TaskRoundReopened`.
Nothing transitions a task-round off `Drawn` today, so a task-round's state is
inferred rather than recorded, and a competition cannot be closed at all.

## Why it matters

The leaderboard cannot distinguish **not flown** from **flown, no result** — it
infers "provisional, over rounds flown so far" from Entry presence alone
(`ScoringService.cs:161-166`, finding 5). `kanban/tech-debt.md`'s `TaskRoundState`
item records that `Drawn`/`InProgress`/`Complete` all collapse to
`Scoring.TaskRoundState.Complete` *precisely because* nothing can emit
`TaskRoundCompleted` (`ScoringService.cs:220-222`).

Two further consequences resolve here:

- **`Competition.OpenEntry`'s closure check is dead code.** `Competition.cs:871`
  already rejects an Entry into a `Complete` or `Annulled` task-round. Nothing
  can reach either state, so the check has never fired. Completing a round is
  what makes "the round is closed, stop capturing scores" real.
- **`ValidityRule` is authored, validated and consumed by nothing.**
  `PhaseDefinition.Validity` (`ClassDefinition.cs:123-133`, `:214`) is checked at
  adoption (`ClassDefinitionValidation.cs:148`) and carried by every seed class,
  but no code has ever read it. `Finalised` is where it becomes live — and it is
  the class-agnostic gate this story is built around.

Annulment and re-flights (`ReflightGroupAppended`,
`kanban/backlog/reflight-groups.md`) hang off this same lifecycle, which is why
this thread goes first.

## Before starting — done

- **`kanban/tech-debt.md`'s `TaskRoundState` mapping note.** Read. It records the
  exact semantics the current inference has; WI-4 replaces the inference rather
  than reopening it.
- **Runtime trap, restated correctly.** The stub said `MartenConfig.cs` registers
  event types. That is **stale** — `kanban/completed/multi-backend-deployment.md`
  WI-1 moved the registry to `src/Soarscore.Infrastructure/SoarscoreEventTypes.cs`,
  one list read by both `MartenConfig` and `FisherConfig`. Appending any of these
  three events without adding its line to `SoarscoreEventTypes.All` fails at
  runtime on **both** backends, per LADR-0001 §4.8. That is WI-7, and its comment
  at `SoarscoreEventTypes.cs:50-53` naming the six unregistered subtypes drops to
  three.
- **Rules cross-check (`fai-rules` skill).** Minimum-rounds-for-validity is a
  per-class datum that genuinely varies — F3J 4 (`F3J.3.1 a`), F3K 5
  (`F3K.10`), F5J 4 (`5.5.11.5 a`), F5L 4 (`5.5.12.4`), F3B "1 round + 1 task"
  (`F3B.2.1 b`, the only class populating `MinTasks`), F5K **none defined** →
  CD decision, authored as `param("minRounds")`. This is exactly the shape
  CLAUDE.md's core architectural law demands: a row that varies across classes is
  a field of the class model, never a branch in core code. It already is one.
  The corpus states **no rule at all** on who may annul a task-round or on what
  grounds — per the skill's standing instruction, a rule that is not there is a
  Contest Director decision, not an inference borrowed from another class. So
  `AnnulTaskRound` carries a free-text `Reason` for audit and imposes no
  eligibility test of its own.

## The governing principle: the system does not order score capture

Raised by the user 2026-08-18 and binding on everything below. On-field
proceedings are chaotic by nature: pilots fly back-to-back across rounds
(R1 G3, then R2 G1), get pulled away to time for someone else, and enter their
scores when they can — retrospectively, in bulk, or not until the evening. How
scores reach the system is **not this system's business**: it may be a connected
field-board and timer rig, pen and paper transcribed at the end of the day, or
twenty phones trickling in at random. Soarscore must not dictate any of it.

The write model already honours this and it must stay that way:

- A phase draws **all** its rounds at once (`drawPhase.alreadyDrawn`,
  `Competition.cs:645`), so every task-round and group exists from the moment of
  the draw. There is no "open round N" step that round N+1 waits on.
- `OpenEntry`'s checks are **structural, not temporal** (`Competition.cs:857-895`):
  does this coordinate exist, was this competitor drawn into it, have they
  withdrawn. Nothing asks what happened in any other round.
- The leaderboard is derived from **what is present, not what is expected** —
  `ScoringService`'s entries-present filter (`ScoringService.cs:161-166`) omits an
  unentered task-round rather than scoring it zero. Partial data yields a partial
  result, never an error.

This story is the first that could take that away, because
`openEntry.taskRoundClosed` (`Competition.cs:883`) is dormant only for want of
something to emit `TaskRoundCompleted`. Hence the stance, carried into WI-1/WI-2b:

> **Completion is the CD asserting that a task-round's scores are in and settled
> — not a statement about the field.** Only a human knows the difference between
> "15 of 20 entered" and "5 people haven't got round to it", which is why the
> assertion is worth having and why it cannot be inferred. And because it is an
> assertion about *data*, it must be **reversible** and never automatic: nothing
> completes a task-round as a side effect, and a score arriving late reopens the
> round rather than being refused.

Recorded in `docs/aggregate-roots.md` §3 (user-approved 2026-08-18), so the next
thread inherits the reasoning rather than rediscovering it.

**Two constraints found during this audit are out of scope here and raised as
their own stories** — both pre-date this work, and both bite retrospective entry
harder than anything in this thread: `kanban/backlog/amend-a-measurement.md` and
`kanban/backlog/out-of-order-flight-entry.md`.

## Three decisions taken up front

### 1. `TaskRoundState.InProgress` stays unreachable — but `TaskRoundReopened` is added

`InProgress` is a folded enum member (`Competition.cs:105`) nothing can produce.
The rejected alternative was a `TaskRoundInProgress` event emitted on the first
Entry opened in a task-round. Declined because:

- It would make `OpenEntryHandler` append to two streams — the Entry stream and
  the Competition stream — on the highest-volume write path in the system, for a
  signal that is already derivable.
- The state has no consumer: nothing reads `InProgress` and nothing would.

The debt it was meant to close is closed differently and better, in **WI-9**:
`kanban/tech-debt.md`'s round-scoped-rebind item wants "has this round actually
started flying", and the honest answer is "does an Entry exist for it", which the
**handler** can ask `IEntryQuery` directly. `Competition` itself still holds no
live flight data — its documented boundary (`Competition.cs:7-10`,
`docs/aggregate-roots.md` §3) is preserved, because the handler resolves the fact
and passes a bool into the decide function.

`InProgress` is therefore left in the enum and documented as reachable by no
event, the same way `Draw.Status` carries an undefined vocabulary.

**A twelfth event is added, but a different one.** `CompetitionEvents.cs:7`
states eleven events mirroring `docs/aggregate-roots.md` §3's mutation list
one-for-one, so growing the list is a `/docs` change needing approval (CLAUDE.md
house-keeping rule 4). Approval given 2026-08-18 for **`TaskRoundReopened`**, and
§3 updated in the same breath. The alternative considered and rejected was to
delete the `openEntry.taskRoundClosed` check outright and make completion purely
informational — cheaper, and it needs no event, but then a genuinely finished
round silently accepts a late write that changes an already-declared result.
Reopening keeps closure meaningful *and* keeps the late score possible, at the
cost of one event; the reopening is an auditable act rather than a silent write,
which is the whole reason to prefer it. The `Reason`-carrying shape of
`TaskRoundAnnulled` is the precedent.

### 2. Finalisation is competition-scope only this thread

`Finalisation.Scope` discriminates Phase and Competition (`Competition.cs:186`).
Only `FinalisationScope.Competition` is built. Phase-scope finalisation exists to
name who was **promoted** into the next phase, and no second phase can be drawn
(`Competition.DrawPhase` fails on `!Phases.IsEmpty`, `Competition.cs:633`) — so
`PromotionRule` has nothing to promote into. `DeclaredResult.Promoted` is written
`false` throughout, with a comment saying why.

Reopening (a second `Finalised` at `Revision` 2) is **out of scope**: nothing
reads a finalisation back yet. See "Out of scope" below.

## Work items

### WI-0 — Split the two round-completion questions (Domain) — **done**

`Round.IsComplete` was named for one question and answered another: it is true
when every task-round is Complete **or** Annulled, which means "nothing left to
fly", not "this round produced a result". WI-3's validity gate wants the second,
and a property named `IsComplete` sitting right next to it was a trap. Renamed
and split before any lifecycle code lands, while it still had zero production
callers:

- `Round.IsCompleteOrAnnulled` — the existing predicate, renamed.
- `Round.IsFullyFlown` — new; every task-round `Complete`. What WI-3 counts.

`docs/soaring-domain-class-diagram.md` updated with the user's approval (the
`Round` class members, and the "Round completion is derived" design note gains
one sentence on why there are now two). `CompetitionTests.Round_completion_predicates_reflect_taskRound_states`
asserts both predicates in the same theory case, since the point of the pair is
that they diverge exactly when a task-round is `Annulled`.

### WI-1 — `Competition.CompleteTaskRound` (Domain)

Instance decide function on `Competition`, returning `Result<TaskRoundCompleted>`.
Defect-chain style (`RegisterCompetitor`/`WithdrawCompetitor`), not `DrawPhase`'s
early-return style — no later check needs a value computed by an earlier one.

| Code | Condition |
|---|---|
| `completeTaskRound.taskRoundNotFound` | no phase/round/task-round at these ordinals |
| `completeTaskRound.alreadyComplete` | state is already `Complete` |
| `completeTaskRound.annulled` | state is `Annulled` — an annulment is a resolution, not a way-station |

Deliberately **no** "every group has flown" check: `Competition` holds no Entry
data, and the CD is the authority on when a round's scores are in. Deliberately
**no** ordering check across rounds — rounds are not required to complete in
order, or at all. Per the governing principle, this is never emitted as a side
effect of anything; only the explicit command produces it.

### WI-2 — `Competition.AnnulTaskRound` (Domain)

Same shape, returning `Result<TaskRoundAnnulled>`, plus the `Reason` string.

| Code | Condition |
|---|---|
| `annulTaskRound.taskRoundNotFound` | no phase/round/task-round at these ordinals |
| `annulTaskRound.alreadyAnnulled` | state is already `Annulled` |
| `annulTaskRound.reasonRequired` | `Reason` blank |

A `Complete` task-round **may** be annulled (the reverse of WI-1's rule): a round
read out and then found faulty is the ordinary case. `Reason` is carried for
audit only and is not folded into the aggregate — `TaskRound` has no `Reason`
field, per `CompetitionEvents.cs:90-93`. `Reason` is validated in the decide
function, not the handler, because unlike `BindParameter`'s `By` it is a
substantive record of a ruling rather than an audit breadcrumb.

### WI-2b — `Competition.ReopenTaskRound` (Domain) — **new**

The event the governing principle requires, and the reason completion is allowed
to close capture at all. A new `CompetitionEvent` subtype,
`TaskRoundReopened(int PhaseOrdinal, int RoundOrdinal, int TaskRoundOrdinal,
string Reason, DateTimeOffset At)`, JSON discriminator `taskRoundReopened`,
folded through the same `ReplaceTaskRound` helper back to `Drawn`.

| Code | Condition |
|---|---|
| `reopenTaskRound.taskRoundNotFound` | no phase/round/task-round at these ordinals |
| `reopenTaskRound.notClosed` | state is `Drawn` — nothing to reopen |
| `reopenTaskRound.reasonRequired` | `Reason` blank |

`Complete → Drawn` and `Annulled → Drawn` are **both** permitted: an annulment
made in error is as correctable as a premature completion, and refusing the
second would reintroduce exactly the dead end this event exists to remove.
`Reason` is carried for audit only, not folded — `TaskRoundAnnulled`'s precedent.

Reopening a task-round does **not** touch any `Finalisation`. A competition
finalised and then reopened at the round level will have a declared result that
no longer matches the derived one — which is invariant B's business to detect,
not this decide function's to prevent. That divergence being *visible* is the
entire point of storing `DeclaredResults` (`Competition.cs:198-202`).

### WI-3 — `Competition.Finalise` (Domain)

Instance decide function returning `Result<Finalised>`, taking the already-computed
`ImmutableArray<DeclaredResult>` plus `by` and `at`. `DrawPhase`'s early-return
style: the validity check needs the resolved `MinRounds` and the per-phase
completed-round counts.

The gate, entirely data-driven off `PhaseDefinition.Validity`, for **every** phase
drawn:

1. Resolve `Validity.MinRounds` (a `NumberOrParam`) through
   `ParameterResolver.Resolve(minRounds, bindings, AdoptedRules.Definition.Parameters)`
   with `ScoringService.FlattenParameterBindings(ParameterBindings)` — no round
   context, exactly as `DrawPhase` does for `MinPerGroup` (`Competition.cs:738-760`).
   An unbound `param("minRounds")` (F5K, NZ Class M) fails
   `finalise.parameterUnbound`, mirroring `drawPhase.parameterUnbound`.
2. Count rounds where `Round.IsFullyFlown` — every task-round `Complete`.
   `Annulled` task-rounds do **not** count toward validity: an annulled round
   resolved the competition's progress but produced no result. That is a
   different question from `Round.IsCompleteOrAnnulled`, which asks only whether
   anything is left to fly; WI-0 gave the two questions two names so this gate
   cannot pick up the wrong one by reading the wrong word.
3. `Validity.MinTasks`, when populated (F3B only), counts **distinct `TaskRef`s**
   across those complete rounds.

| Code | Condition |
|---|---|
| `finalise.parameterUnbound` | `MinRounds` is an unbound parameter ref |
| `finalise.notEnoughRounds` | complete rounds < resolved `MinRounds` |
| `finalise.notEnoughTasks` | distinct complete task codes < `MinTasks` |
| `finalise.noResults` | `DeclaredResults` empty — `Finalisation.DeclaredResults` is 1..* |
| `finalise.alreadyFinalised` | a competition-scope `Finalisation` already exists |
| `finalise.byRequired` | `by` blank |

`Revision` is `Finalisations.Count(competition-scope) + 1`, so it is always 1 this
thread and needs no revisiting when reopening lands.

**Not** a `Phases.IsEmpty` check of its own: an undrawn competition has zero
complete rounds and fails `notEnoughRounds` with a truthful message.

### WI-4 — `ScoringService` stops inferring (Domain)

`ScoringService.ScoreCompetition` (`:220-222`) currently maps
`Competitions.TaskRoundState.Annulled → Annulled` and *everything else* →
`Complete`. With WI-1 landed, `Drawn` is a real, distinguishable state and that
`else` is a lie.

The finding-5 entries-present filter (`:161-166`) stays — it is what makes the
leaderboard provisional over rounds flown so far, and removing it would hand a
drop-worst policy a zero for a round nobody has flown. What changes is that
**`Drawn` with entries** is now honestly a partially-flown round rather than a
completed one. The mapping becomes explicit and total:

- `Annulled` → `Scoring.TaskRoundState.Annulled`
- `Complete` → `Scoring.TaskRoundState.Complete`
- `Drawn` / `InProgress` → included as `Complete` **only** while the competition
  is not finalised (the provisional leaderboard's existing meaning), with the
  comment updated to say this is now a deliberate provisional-view choice rather
  than an artefact of `TaskRoundCompleted` being unreachable.

This keeps every existing scoring test green — the provisional leaderboard's
behaviour is unchanged — while removing the `else` that only existed because the
event was unreachable. `kanban/tech-debt.md`'s `TaskRoundState` item is ticked
here, and its replacement note recorded.

### WI-5 — Commands and handlers (Application)

Four, in `src/Soarscore.Application/Commands/Competitions/`:

- `CompleteTaskRound(CompetitionRef, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal)`
  → `ICommand<CompetitionId>`. Plain `BindParameter` template:
  `CompetitionLoader.LoadAsync` → decide → `AppendAsync` at `ExpectedVersion.Exact`.
- `AnnulTaskRound(..., Reason)` → same.
- `ReopenTaskRound(..., Reason)` → same.
- `FinaliseCompetition(CompetitionRef, By)` → the one non-trivial handler. It must
  compute `DeclaredResults`, which means doing what `ScoreCompetitionHandler` does:
  `CompetitionLoader.LoadAsync` → `EntryCollector.CollectAsync` →
  `ScoringService.ScoreCompetition` → map `CompetitionResult.Scores`/`.Placings`
  into `DeclaredResult` (`Promoted = false`, per decision 2) → `competition.Finalise(...)`
  → append. Precedented: `CreateCompetitionHandler` already does a cross-aggregate
  read, and `OpenEntryHandler` reads the Competition to decide an Entry event.

  The scoring call happens **before** the decide, so a scoring failure surfaces as
  its own code (`score.reflightNotSupported`, `score.taskNotDeclared`) rather than
  as a finalisation defect.

### WI-6 — Routes and composition (Api)

`src/Soarscore.Api/Commands/Commands.cs` — verbs, never nouns:

```
app.MapCommand<CompleteTaskRound,    CompetitionId>("/complete-task-round");
app.MapCommand<ReopenTaskRound,      CompetitionId>("/reopen-task-round");
app.MapCommand<AnnulTaskRound,       CompetitionId>("/annul-task-round");
app.MapCommand<FinaliseCompetition,  CompetitionId>("/finalise-competition");
```

Four matching `AddScoped<ICommandHandler<,>>` lines in `Composition.cs`. Note the
sanity floor in `tests/Soarscore.Architecture.Tests/HandlerRegistrationTests.cs`
goes 13 → 17 commands; its stale "ten commands and four queries" comment is
corrected in the same edit, discharging one `kanban/backlog/smaller-items.md` bullet
opportunistically (rule: take one when a thread is already touching that file).

### WI-7 — Event-type registration (Infrastructure)

Four lines in `SoarscoreEventTypes.All`: three with the aliases the JSON
contracts already declare (`CompetitionEvents.cs:28-32`) — `taskRoundCompleted`,
`taskRoundAnnulled`, `finalised` — plus `taskRoundReopened`, whose discriminator
WI-2b adds alongside them. The comment at `:50-53` narrows from six unregistered
subtypes to three (`ReflightGroupAppended`, `RulesAmended`, `PenaltyRecorded`),
and the header's "eleven events" count goes to twelve. One list, both backends —
no `MartenConfig`/`FisherConfig` edit.

### WI-8 — The `State` column on `competitions` (Application/Infrastructure)

`CompetitionSummary` (`:6-9`) says a `State` column was excluded because "every
row's state is identically 'created' right now". `Finalised` is the second state,
so the reason expires. Add `State` and fold it in `CompetitionProjection`:
`CompetitionCreated` → `"created"`, `PhaseDrawn` → `"drawn"`, competition-scope
`Finalised` → `"finalised"`. The `_ => current` default arm stays, for the reason
its doc comment already gives.

This discharges the second bullet of `kanban/backlog/smaller-items.md`, which
`create-competition-steel-thread-plan.md` predicted would arrive with `PhaseDrawn`
and which did not.

### WI-9 — Close the round-scoped-rebind debt properly (Application/Domain)

Per decision 1. `Competition.ValidateRoundScope`'s freeze test
(`Competition.cs:1067`) currently keys on `taskRound.State != Drawn`, which
`per-round-parameter-bindings-plan.md` explicitly recorded as an approximation
with a real gap: a rebind mid-round, after a flight has opened but before the
round is marked complete, is silently accepted.

`Competition.BindParameter` gains a trailing `bool roundHasEntries = false`
threaded into `ValidateRoundScope`; the freeze becomes
`taskRound.State != Drawn || roundHasEntries`, with a new defect code
`competition.parameter.roundInProgress`. `BindParameterHandler` resolves it by
calling `IEntryQuery.FindAsync(competitionRef, phaseOrdinal, roundOrdinal, …)`
and testing for any row — only when the command is round-scoped, so the unscoped
path takes no extra query.

The aggregate boundary holds: `Competition` receives an already-resolved fact,
exactly as it receives an already-resolved `AdoptedRules` (`Competition.cs:502-508`).

### WI-10 — Tests

**Domain example-based** (`tests/Soarscore.Domain.Tests/`): one new file,
`TaskRoundLifecycleDecideTests.cs` — one case per defect code above (twelve), plus
success cases; and `FinaliseDecideTests.cs` for the validity gate, including the
F3B `MinTasks` case and the F5K unbound-`minRounds` case. Extend
`OpenEntryDecideTests.cs` to assert `openEntry.taskRoundClosed` now actually fires
after a completion and after an annulment — the dead check at `Competition.cs:883`
gets its first real test — and that it stops firing after a reopen.

**Property-based** (CsCheck), with the invariants named up front per CLAUDE.md:

- **Invariant A — the validity gate counts completed rounds, never annulled ones.**
  For any drawn phase of *n* rounds and any assignment of each round to
  {left `Drawn`, `Complete`, `Annulled`}, `Competition.Finalise` succeeds iff
  `count(rounds where every task-round is Complete) >= resolved MinRounds` and
  the distinct-task count meets `MinTasks`. Generated over the shape and the
  assignment, so no fixed fixture can hide a case. This is the invariant that
  makes annulment-vs-completion meaningful rather than a naming difference.
- **Invariant B — a declared result is always re-derivable.** For any competition
  and entry set that finalises successfully, the `DeclaredResult` set in the
  emitted `Finalised` equals, competitor for competitor, what
  `ScoringService.ScoreCompetition` returns for the same inputs — score, placing
  and disqualification. This is `DeclaredResult`'s own documented contract
  (`Competition.cs:198-202`: "Answers 'what was declared', never 'what is the
  score' … can always be re-derived and compared against what was published"),
  turned into an executable claim.
- **Invariant C — closure is scoped, and always revocable.** For any drawn shape
  and any task-round: after `TaskRoundCompleted` or `TaskRoundAnnulled` folds,
  `OpenEntry` into that task-round fails for every competitor drawn into it,
  while `OpenEntry` into every *other* task-round is unaffected — and after
  `TaskRoundReopened` folds, `OpenEntry` succeeds again for exactly the
  competitors it would have accepted before the closure. Generated over the shape,
  the target ordinal, and the close/reopen sequence, so no arrangement of
  closures can strand a task-round. The second half is the governing principle
  expressed as a test: a late score is never permanently locked out.

The fold-side "mutates exactly one node" invariant is **already covered** by
`CompetitionReplaceTaskRoundPropertyTests.cs`, which generates the shape and the
target ordinal across all three `ReplaceTaskRound` events — not duplicated here.

**Serialization**: three new cases in `CompetitionEventJsonTests.cs`.
`Finalised` already has one (`:223-242`) covering the decimal-as-string aggregate;
add `TaskRoundCompleted`, `TaskRoundAnnulled` and `TaskRoundReopened` round-trips.

**Store-backed** (`tests/Soarscore.Infrastructure.Tests/`): a new
`TaskRoundLifecycleEventStoreTests.cs` written once against `IStoreFixture`, so it
runs on **both** Postgres (Testcontainers, `Trait("Category","Storage")`) and
Fisher/SQLite. This is the test that would have caught an unregistered event type.

**Acceptance** (`tests/Soarscore.Acceptance.Tests/`): a new
`ClosingACompetition.feature`, run twice (`SOARSCORE_TEST_STORE=postgres|sqlite`):

```gherkin
Scenario: Scores are captured out of round order, and every one is accepted
Scenario: A round is closed and no further scores can be captured for it
Scenario: A late score is entered after its round was closed, by reopening it
Scenario: An annulled round is excluded from the leaderboard
Scenario: A competition cannot be finalised before its class's minimum rounds are flown
Scenario: A finalised competition declares the same results the leaderboard shows
```

The first is the governing principle at the workflow level — a pilot flies R1 G3
then R2 G1 and enters both later, in the wrong order — and is the scenario that
should fail loudly if a future thread adds sequencing. The third is its
companion, and the reason `TaskRoundReopened` exists. The fifth proves
`ValidityRule` is live and class-driven; the sixth is invariant B end-to-end.

### WI-11 — Board reconciliation

On completion: `git mv` to `kanban/completed/`, set the status header, tick
`kanban/tech-debt.md`'s `TaskRoundState` item and its round-scoped-rebind item,
tick two `kanban/backlog/smaller-items.md` bullets (`State` column, stale sanity
floor), and record what this thread defers (below) in
`kanban/deferred-decisions.md`.

## Out of scope — deliberately

- **Phase-scope finalisation and `PromotionRule`.** Decision 2. Nothing to promote
  into until a flyoff draw exists.
- **Reopening a finalisation (revision ≥ 2).** The model supports it
  (`Finalisation.Revision`, "nothing is overwritten") and `Finalise` computes
  `Revision` generically, but no read model or query surfaces a finalisation, so a
  second revision would be write-only. Becomes a backlog stub if a CD asks for it.
- **A `GetFinalisation` query / finalisation on `CompetitionView`.** `GetCompetition`
  folds the stream and so already returns `Competition.Finalisations` — no new
  query needed.
- **Blocking score capture on `Finalised`.** `OpenEntry` closes on task-round
  state, which is the right granularity; a competition-level write lock is a
  separate concern and no rule requires one.
- **`ReflightGroupAppended`.** `kanban/backlog/reflight-groups.md`, unblocked by
  this thread.
- **Automatic completion.** Nothing infers "the round is done because everyone
  flew". The CD says so. Per the governing principle this is not a deferral — it
  is a standing stance, and a later thread proposing to infer it should reopen
  that section rather than treat this bullet as an unfinished item.
- **Deriving completion from recorded data.** Asked directly (user, 2026-08-18):
  once every metric for a task-round's groups is entered, is it complete? No —
  presence of data can prove a task-round is *not* ready, but absence can never
  prove it is, because a pilot who took three of five allowed launches, a metric
  legitimately absent, and a `NoResult` all look identical to "not typed in yet".
  What *is* worth building is a read-side indicator that shows the CD what is
  recorded without ever deciding: `kanban/backlog/entry-completeness-indicator.md`.
- **Correcting a captured measurement, and out-of-order flight entry.** Found by
  this thread's ordering audit, raised as `kanban/backlog/amend-a-measurement.md`
  and `kanban/backlog/out-of-order-flight-entry.md` (house-keeping rule 6). Both
  live in the `Entry` aggregate, not `Competition`, and neither blocks this work.

## Risks

- **WI-4 is the regression surface.** Every scoring and acceptance test runs
  through that mapping. The plan keeps provisional behaviour byte-identical and
  changes only the comment and the exhaustiveness of the switch; if a scoring test
  moves, the change was wider than intended.
- **WI-9 touches a shipped command.** `BindParameter` grows a parameter and a
  defect code. The new argument defaults to `false`, so every existing call site
  and test compiles unchanged — but `BindParameterHandlerTests` needs an
  `IEntryQuery` test double it does not have today.
