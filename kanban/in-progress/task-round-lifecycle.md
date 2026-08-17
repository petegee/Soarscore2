# Plan — Task-round lifecycle: `TaskRoundCompleted` / `TaskRoundAnnulled` / `Finalised`

**Status:** In progress — planned, not yet built · **Raised:** 2026-08-16 · **Planned:** 2026-08-17

## What

Three mapped, folded, unreachable `CompetitionEvent` types get decide functions,
commands, handlers and endpoints: `TaskRoundCompleted`, `TaskRoundAnnulled` and
`Finalised` (competition scope only). Nothing transitions a task-round off
`Drawn` today, so a task-round's state is inferred rather than recorded, and a
competition cannot be closed at all.

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

## Two decisions taken up front

### 1. `TaskRoundState.InProgress` stays unreachable — no twelfth event

`InProgress` is a folded enum member (`Competition.cs:105`) nothing can produce.
The rejected alternative was a `TaskRoundInProgress` event emitted on the first
Entry opened in a task-round. Declined because:

- `CompetitionEvents.cs:7` states **eleven events, mirroring
  `docs/aggregate-roots.md` §3's mutation list one-for-one**. A twelfth is a
  `/docs` change needing approval (CLAUDE.md house-keeping rule 4), for a state
  with no consumer.
- It would make `OpenEntryHandler` append to two streams — the Entry stream and
  the Competition stream — for a signal that is already derivable.

The debt it was meant to close is closed differently and better, in **WI-9**:
`kanban/tech-debt.md`'s round-scoped-rebind item wants "has this round actually
started flying", and the honest answer is "does an Entry exist for it", which the
**handler** can ask `IEntryQuery` directly. `Competition` itself still holds no
live flight data — its documented boundary (`Competition.cs:7-10`,
`docs/aggregate-roots.md` §3) is preserved, because the handler resolves the fact
and passes a bool into the decide function.

`InProgress` is therefore left in the enum and documented as reachable by no
event, the same way `Draw.Status` carries an undefined vocabulary.

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
data, and the CD is the authority on when a round is done. Deliberately **no**
ordering check across rounds — rounds are not required to complete in order.

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
2. Count rounds where **every** task-round is `Complete`. `Annulled` task-rounds
   do **not** count toward validity — an annulled round was not flown to a result,
   and `Round.IsComplete` (`Competition.cs:284`) deliberately treats
   Complete-or-Annulled as "not blocking", which is a different question. This
   distinction is the single most important line in the story; it is why
   `Round.IsComplete` is *not* reused here.
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

Three, in `src/Soarscore.Application/Commands/Competitions/`:

- `CompleteTaskRound(CompetitionRef, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal)`
  → `ICommand<CompetitionId>`. Plain `BindParameter` template:
  `CompetitionLoader.LoadAsync` → decide → `AppendAsync` at `ExpectedVersion.Exact`.
- `AnnulTaskRound(..., Reason)` → same.
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
app.MapCommand<AnnulTaskRound,       CompetitionId>("/annul-task-round");
app.MapCommand<FinaliseCompetition,  CompetitionId>("/finalise-competition");
```

Three matching `AddScoped<ICommandHandler<,>>` lines in `Composition.cs`. Note the
sanity floor in `tests/Soarscore.Architecture.Tests/HandlerRegistrationTests.cs`
goes 13 → 16 commands; its stale "ten commands and four queries" comment is
corrected in the same edit, discharging one `kanban/backlog/smaller-items.md` bullet
opportunistically (rule: take one when a thread is already touching that file).

### WI-7 — Event-type registration (Infrastructure)

Three lines in `SoarscoreEventTypes.All` with the aliases the JSON contracts
already declare (`CompetitionEvents.cs:28-32`): `taskRoundCompleted`,
`taskRoundAnnulled`, `finalised`. The comment at `:50-53` narrows from six
unregistered subtypes to three (`ReflightGroupAppended`, `RulesAmended`,
`PenaltyRecorded`). One list, both backends — no `MartenConfig`/`FisherConfig` edit.

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
`TaskRoundLifecycleDecideTests.cs` — one case per defect code above (nine), plus
success cases; and `FinaliseDecideTests.cs` for the validity gate, including the
F3B `MinTasks` case and the F5K unbound-`minRounds` case. Extend
`OpenEntryDecideTests.cs` to assert `openEntry.taskRoundClosed` now actually fires
after a completion and after an annulment — the dead check at `Competition.cs:871`
gets its first real test.

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
- **Invariant C — completion closes capture.** For any drawn shape and any
  task-round, after `TaskRoundCompleted` or `TaskRoundAnnulled` folds, `OpenEntry`
  into that task-round fails for every competitor drawn into it, and `OpenEntry`
  into every *other* task-round is unaffected.

The fold-side "mutates exactly one node" invariant is **already covered** by
`CompetitionReplaceTaskRoundPropertyTests.cs`, which generates the shape and the
target ordinal across all three `ReplaceTaskRound` events — not duplicated here.

**Serialization**: three cases in `CompetitionEventJsonTests.cs`.
`Finalised` already has one (`:223-242`) covering the decimal-as-string aggregate;
add `TaskRoundCompleted` and `TaskRoundAnnulled` round-trips.

**Store-backed** (`tests/Soarscore.Infrastructure.Tests/`): a new
`TaskRoundLifecycleEventStoreTests.cs` written once against `IStoreFixture`, so it
runs on **both** Postgres (Testcontainers, `Trait("Category","Storage")`) and
Fisher/SQLite. This is the test that would have caught an unregistered event type.

**Acceptance** (`tests/Soarscore.Acceptance.Tests/`): a new
`ClosingACompetition.feature`, run twice (`SOARSCORE_TEST_STORE=postgres|sqlite`):

```gherkin
Scenario: A round is closed and no further scores can be captured for it
Scenario: An annulled round is excluded from the leaderboard
Scenario: A competition cannot be finalised before its class's minimum rounds are flown
Scenario: A finalised competition declares the same results the leaderboard shows
```

The third scenario is the one that proves `ValidityRule` is live and class-driven;
the fourth is invariant B at the workflow level.

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
  flew". The CD says so. Revisit only if the field asks.

## Risks

- **WI-4 is the regression surface.** Every scoring and acceptance test runs
  through that mapping. The plan keeps provisional behaviour byte-identical and
  changes only the comment and the exhaustiveness of the switch; if a scoring test
  moves, the change was wider than intended.
- **WI-9 touches a shipped command.** `BindParameter` grows a parameter and a
  defect code. The new argument defaults to `false`, so every existing call site
  and test compiles unchanged — but `BindParameterHandlerTests` needs an
  `IEntryQuery` test double it does not have today.
