# Plan — Catalogue-choice draws: the CD picks each round's task

**Status:** Completed · **Date:** 2026-08-16

Work items are numbered `WI-n`, scoped to *this* plan document (see
`command-side-steel-thread-plan.md`'s numbering note — WI numbers reset per plan).

## Context

`phase-drawn-steel-thread-plan.md` delivered a draw that schedules **one task, repeated
every round**. `bind-parameter-steel-thread-plan.md` and `capture-a-score-steel-thread-plan.md`
then both hit the same wall from different directions, each recording it as out of scope:
two of the eleven corpus classes — **F3K and F5K** — declare rounds whose task is *chosen from
a catalogue*, and the draw refuses them outright:

```
Competition.cs (DrawPhase) → Result<PhaseDrawn>.Failure("drawPhase.unsupportedRoundComposition", …)
```

The check that refuses them is one condition covering **three different situations**:

```csharp
if (phaseDefinition.Tasks.Length != 1
    || phaseDefinition.Rounds.Kind != CompositionKind.FixedSequence
    || phaseDefinition.Rounds.TasksPerRound != 1)
```

This thread splits that condition, and admits exactly one of the three: a
`ChooseFromCatalogue` phase with `TasksPerRound == 1`, where **the CD names the task for
each round as part of the draw**. `TasksPerRound > 1` (F3B's multi-task round) stays
refused, and stays deferred.

**Consequences today, all recorded elsewhere and all discharged by this thread:**

- F5K's draw is blocked *after* `BindParameter` fixed its other blocker — see
  `kanban/completed/bind-parameter-steel-thread-plan.md`.
- The acceptance suite cannot use the real F3K. It publishes a hand-authored
  single-task stand-in, `tests/Soarscore.Acceptance.Tests/Support/AcceptanceF3KShape.cs`,
  tracked in `kanban/tech-debt.md` for retargeting "once catalogue-choice draws land".
- `ScoringCorpusPropertyTests` — the test that asserts the pipeline is class-agnostic —
  runs over 8 of 11 corpus classes, excluding F3K, F5K and F3B for this reason.

### The decision this thread implements (taken 2026-08-08, not re-opened)

`bind-parameter-steel-thread-plan.md`, finding 1: **for catalogue-choice classes the
tasks are set at draw time.** The rules agree — F3K.11's preamble: *"Detailed
specifications including the tasks to be flown for the day must be announced by the
organiser before the start of the contest."* The task for each round is therefore known
before anyone flies, which is exactly when the draw happens; a separate later
"choose task for round n" event would model a decision the rulebook does not permit to be
made that late.

### What is already true, and must not be rebuilt

This is the pleasant surprise of the thread, and it is worth stating before any work
begins, because it removes most of what a reader would expect to be in scope:

- **`PhaseDrawn` needs no payload change.** `TaskRound.TaskRef` is already a per-task-round
  task code (`Competition.cs`, `TaskRound`'s doc comment: *"Code is the only stable handle
  to reference"*). The event can already express a different task in every round; nothing
  has ever written one. **No new event type, no `MapEventType`, no Marten change, no
  event-JSON change.** The set of unreachable event types is untouched
  by this thread.
- **Everything downstream of the draw already reads `TaskRef` per task-round**, and
  therefore already handles a heterogeneous phase: `Competition.OpenEntry` scans every
  declared task for `taskRound.TaskRef`; `TaskResolver` and `ScoringService` do the same,
  then call `ParameterResolver.ResolveTask` per task-round. Scoring, drops
  (`DropDimension.ByRound` / `ByTask`) and normalisation are all per-task-round already.
- **No read-model change.** `GET /competition` returns the folded aggregate including
  `AdoptedRules.Definition`, so the phase's catalogue — the codes a CD must choose from —
  is already visible to a caller, as is the drawn selection.

**What is genuinely missing:** a task selection on the way in, its validation, per-round
group sizing in the draw's builder, and the tests.

### The one non-obvious piece of real work: group sizing is now per round

`GroupConstraint` hangs off `TaskDefinition`, not off the phase. Today `DrawPhase`
resolves **one** `minPerGroup` and hands it to `PhaseDraw.BuildGroups(field, minPerGroup,
rounds)`, which uses it for every round. With a different task per round, `minPerGroup`
is a **per-round** quantity — and `MinPerGroup` may be a `ParameterRef`, so two rounds can
resolve to two different sizes.

In today's corpus they happen to agree (F3K's catalogue tasks all inherit task A's
`MinPerGroup = 5` through `with`; F5K's all inherit `param("minPerGroup")`), but relying
on that would be exactly the class-specific assumption CLAUDE.md's core architectural law
forbids: the model permits per-task group constraints, so the draw must honour them. This
also matters for the fairness invariant — repeat pairings are minimised *across* rounds
(`00-general-rules.md#1`), so the builder must keep its cross-round pairing state while
the group shape changes underneath it.

### What the rules say (checked, not assumed)

| Question | Answer | Source |
|---|---|---|
| Who picks the tasks, and when? | The organiser, announced before the contest starts | `F3K.11` preamble |
| May a task repeat across rounds? | **F3K preliminary: no** — "minimum 5 rounds (each a different task)" | `F3K.10`, modelled as `RequireDistinctTaskPerRound = true` |
| F3K fly-off? | Catalogue is A–N **plus task M**; 3–6 rounds; no distinctness rule stated | `F3K.10.3`, `F3K.11` |
| F5K? | One task per round from A–E; no distinctness rule stated | `5.5.10.2` |

`RequireDistinctTaskPerRound` already exists on `RoundComposition` and is set by exactly
one definition (F3K's preliminary phase). **Nothing in `src/` reads it today** — this
thread is its first consumer, which is the point: the flag was authored from the rulebook
and the core system is only now generic enough to interpret it.

### Out of scope (deliberately)

- **Per-round parameter bindings** (`ParameterBindingPoint.PerRound`; F3K's
  `workingTime.A/B/E/L`, `maxFlight.B/L`). `bind-parameter-steel-thread-plan.md` finding 1
  deferred these *into* this thread on the grounds that "binding the working time for
  round 3 is meaningless until round 3 has a task". Round 3 now has a task — so the
  deferral's precondition is discharged and the work becomes *possible* here, but it is
  **decided (2026-08-16) to be a separate follow-on thread**, for two reasons:
  1. It unblocks nothing. Since `ParameterResolver`'s default fallback landed, all six
     F3K per-round parameters resolve to their declared values (600 s, 240 s, 599 s). F3K
     is fully drawable, floppable and scorable this thread without them; they buy the CD
     an **override**, e.g. dropping task E's working time to 900 s for one round.
  2. It is a different change, in a different place: `ParameterBinding` grows a round
     scope, and `BindParameter`, `ParameterResolver`, `TaskResolver` and
     `Competition.OpenEntry` all grow round context. Landing it alongside a draw change
     would put two unrelated diffs in one review.
  The follow-on thread's shape is recorded in **Appendix A** so it is not re-derived.
- **Multi-task rounds (F3B)** — `FixedSequence` with `tasksPerRound: 3`. A third, separate
  deferral; this thread keeps refusing it, with a *narrower* and clearer failure code.
- **Flyoff-phase draws.** `DrawPhase` still draws only the first phase
  (`drawPhase.alreadyDrawn` on any second call). F3K's and F5K's fly-off catalogues are
  therefore not exercised — that is the flyoff thread's job, and this thread's validation
  is written against "the phase being drawn", so it needs no change when that lands.
- **Redraw / draw acceptance**, and **`RequireDistinctTaskPerRound` for a re-flight
  group** — untouched.
- **A "suggest a task order" helper.** The CD names them; the system does not choose.

### Governing documents

- CLAUDE.md's core architectural law. The draw must interpret `RoundComposition.Kind`,
  `TasksPerRound` and `RequireDistinctTaskPerRound` generically. **The test:** F3K and
  F5K become drawable without either name appearing anywhere in `src/`
  (`ClassAgnosticismTests` enforces this mechanically).
- `docs/aggregate-roots.md` §3 — the schedule is "created up front" and appended
  atomically as one `PhaseDrawn`. The task selection is part of that schedule, not a
  later amendment.
- `docs/ladr/ladr-0001-event-store.md` §4.4 — `ExpectedVersion.Exact(version)` already
  guards the draw; unchanged.
- `docs/rules/f3k.md` §5, `docs/rules/f5k.md` §5 and the `fai-rules` skill for anything
  further.

---

## Phase A — Domain

### WI-1 — `PhaseDraw.BuildGroups` accepts per-round group sizing

`src/Soarscore.Domain/Competitions/PhaseDraw.cs`. Today:

```csharp
public static ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> BuildGroups(
    ImmutableArray<CompetitorId> field, int minPerGroup, int roundCount)
```

becomes, in effect, one `minPerGroup` **per round**:

```csharp
public static ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> BuildGroups(
    ImmutableArray<CompetitorId> field, ImmutableArray<int> minPerGroupByRound)
```

`roundCount` disappears into `minPerGroupByRound.Length` rather than being passed
alongside it — two values that must agree, when one already implies the other, is a
defect waiting to be written. Keep the existing two-argument overload as a thin
forwarder (`Enumerable.Repeat(minPerGroup, roundCount)`) so the eight FixedSequence
classes, `DrawPhaseEventStoreTests` and the existing `PhaseDrawPropertyTests` keep their
call shape.

The change inside is small and local: `groupCount`/`GroupSizes` move **into** the
per-round loop, computed from that round's `minPerGroup`. Everything else — the
`pairCount` dictionary, `BuildOneRound`'s iterative-deepening ceiling, `TryBuildRound`'s
backtracking, `RecordPairings` — is untouched and keeps its cross-round state. That is
the property to protect: changing group *shape* between rounds must not reset pairing
history.

**Watch:** `BuildOneRound`'s ceiling starts at `max(1, currentMax)`. With varying sizes a
round may be unsatisfiable at that ceiling and satisfiable one above; the existing
escalation loop already handles this, but it is the reason the loop must stay
open-ended rather than being bounded by a constant.

**Tests** (`tests/Soarscore.Domain.Tests/PhaseDrawTests.cs` and the existing
`PhaseDrawPropertyTests.cs`): every existing test stands unchanged via the forwarder;
add the heterogeneous cases in WI-3.

### WI-2 — `Competition.DrawPhase` takes a task selection

`src/Soarscore.Domain/Competitions/Competition.cs`. New signature:

```csharp
public Result<PhaseDrawn> DrawPhase(int rounds, ImmutableArray<string> taskRefs, DateTimeOffset at)
```

`taskRefs` empty means "the class leaves no choice" — the FixedSequence path, exactly
today's behaviour. Keep the three-argument shape only if a call site genuinely needs it;
prefer updating the four or five call sites over carrying an overload that makes the
empty case implicit.

**Replace the single composition check with a split.** The current condition refuses
three situations with one code; after this thread there are two distinct outcomes:

| Condition | Outcome |
|---|---|
| `Rounds.TasksPerRound != 1` | `drawPhase.unsupportedRoundComposition` — F3B's multi-task round, still deferred. Message names *multi-task rounds* only; catalogue choice is no longer part of that sentence. |
| `Rounds.Kind == FixedSequence` | Requires `Tasks.Length == 1`; a `FixedSequence` phase declaring several tasks with `tasksPerRound: 1` is a definition the model permits and the draw has no rule for choosing within — refuse with `drawPhase.unsupportedRoundComposition`. No corpus class is in this state. |
| `Rounds.Kind == ChooseFromCatalogue` | The new path — validate `taskRefs` below. |

New failure codes, all `drawPhase.*`, in this order:

| Code | Condition |
|---|---|
| `drawPhase.taskSelectionRequired` | `ChooseFromCatalogue` and `taskRefs` is empty |
| `drawPhase.taskSelectionNotPermitted` | `FixedSequence` and `taskRefs` is **non-**empty — the class leaves no choice to make |
| `drawPhase.taskSelectionCountMismatch` | `taskRefs.Length != rounds` |
| `drawPhase.taskNotInCatalogue` | a code is not the `Code` of any task in **this phase's** `Tasks` |
| `drawPhase.taskSelectionNotDistinct` | `RequireDistinctTaskPerRound` and the selection repeats a code |

Order matters: check count before contents, so a caller sending three codes for five
rounds gets the count error rather than an arbitrary first-code error. Validate
`rounds` (`drawPhase.roundsInvalid`, existing) before any of them — an out-of-range round
count makes the selection meaningless.

Note `drawPhase.taskSelectionNotDistinct` is reachable in two ways and both are the CD's
error: a genuine repeat, and asking for more rounds than the catalogue has distinct tasks
(F3K's 13-task preliminary catalogue at `rounds: 14`). One code, and a message that
states the catalogue size, is enough — a second code for the pigeonhole case would be a
distinction without a difference at the API.

**Then, per round** (this is the body of the change):

1. `task = phaseTasks.First(t => t.Code == taskRefs[i])` — for FixedSequence, the single
   task repeated, exactly as today.
2. Resolve that task's `Group.MinPerGroup` through `ParameterResolver` with the
   flattened last-write-wins bindings, as today. `drawPhase.parameterUnbound` and
   `drawPhase.fieldTooSmall` keep their codes and now name the offending **round and
   task** in the message. Absent `GroupConstraint` still means `field.Length` — one
   whole-field group (NZ N/P).
3. Collect into `minPerGroupByRound`, call `PhaseDraw.BuildGroups` **once** for the whole
   phase (WI-1), and build each `Round` with its own `TaskRound.TaskRef = taskRefs[i]`.

Resolution happens for **every** round before the builder runs, not lazily inside it: the
draw is atomic, so an unbound parameter on round 5 must fail the whole draw rather than
emit a partial schedule.

**Tests** (`tests/Soarscore.Domain.Tests/PhaseDrawnDecideTests.cs`): one per new code,
plus a real-corpus F3K draw of 5 distinct tasks succeeding with the right `TaskRef` per
round, plus a real-corpus F5K draw (needing `BindParameter` for `minPerGroup` first),
plus the existing FixedSequence tests unchanged with an empty selection, plus F3B still
refused with `drawPhase.unsupportedRoundComposition`. The two existing assertions on that
code (`PhaseDrawnDecideTests.cs:131,143`) need their fixtures checked: one of them is a
catalogue case today and must move to the new codes.

### WI-3 — Property tests

`tests/Soarscore.Domain.Tests/CatalogueDrawPropertyTests.cs`, CsCheck. Named invariants,
per CLAUDE.md's testing approach:

1. **Selection round-trips.** For any valid selection, the drawn phase's task-rounds
   carry exactly that sequence of codes, in round order. *The property that proves the
   thread did its job.*
2. **Distinctness is honoured, both ways.** For a phase with
   `RequireDistinctTaskPerRound`, every accepted draw has all-distinct task-rounds, and
   every selection containing a repeat is refused with
   `drawPhase.taskSelectionNotDistinct`. For a phase without it, a repeat is accepted.
3. **The field partition invariant survives heterogeneous sizing.** For any per-round
   sequence of group sizes, in every round each competitor appears in exactly one group,
   groups are non-empty, and the group count for round *i* is
   `max(1, field.Length / minPerGroup[i])` — the phase-drawn plan's WI-2 invariants, now
   asserted round by round rather than once.
4. **Pairing fairness is not degraded by varying group shape.** With uniform sizes the
   draw is byte-identical to the pre-change algorithm (the regression guard on WI-1's
   refactor); with varying sizes, the maximum pair co-occurrence count is no worse than
   the trivial lower bound the phase-drawn plan's brute-force property already checks at
   small field/round counts.
5. **Determinism.** Same field, same selection, same bindings ⇒ identical groups and
   identical task-rounds (ids excepted). The draw stays a pure function of its inputs.
6. **Corpus-generic.** Every corpus class whose first phase is `ChooseFromCatalogue` with
   `tasksPerRound: 1` can be drawn given a valid selection — the set discovered by
   scanning `tools/Soarscore.SeedData/json/`, never hard-coded. Today that finds F3K and
   F5K; a future class must be picked up without editing this test.

## Phase B — Application

### WI-4 — `DrawPhase` command carries the selection

`src/Soarscore.Application/Commands/Competitions/DrawPhase.cs`:

```csharp
public sealed record DrawPhase(
    CompetitionId CompetitionId, int Rounds, IReadOnlyList<string>? TaskRefs = null) : ICommand<CompetitionId>;
```

Two deliberate choices:

- **`IReadOnlyList<string>?`, not `ImmutableArray<string>`.** This is the first command in
  the repo to carry a collection, so the convention is being set here. An omitted
  `ImmutableArray<T>` property deserialises to `default` — an *uninitialised* struct that
  throws on enumeration rather than reading as empty. `IReadOnlyList<string>?` makes the
  omitted case a plain `null`. Convert at the handler boundary:
  `command.TaskRefs?.ToImmutableArray() ?? []`.
- **Optional, defaulted.** Every existing caller of `/draw-phase` — the eight
  FixedSequence classes, the acceptance suite, the `.http` verification files — keeps
  working untouched.

The handler is otherwise unchanged: load, fold, decide, append with
`ExpectedVersion.Exact(version)`.

**Tests** (`tests/Soarscore.Application.Tests/...DrawPhaseHandlerTests.cs`): a catalogue
draw succeeds and appends one `PhaseDrawn` carrying the per-round task refs; each new
decide failure is surfaced faithfully with its code; a null `TaskRefs` still draws a
FixedSequence class.

### WI-5 — Wiring: confirm there is none

An explicit work item because its absence is the surprising part, and because
`bind-parameter`'s WI-5 warned that missed Marten wiring fails at *runtime*:

- `PhaseDrawn` is already registered in `MartenConfig`; its payload is unchanged.
- `CompetitionEventJsonTests` already round-trips `PhaseDrawn`. **Extend the existing
  case** to a two-round, two-distinct-task fixture, so the per-round `TaskRef` is
  actually covered by the serialisation test rather than incidentally by a uniform one.
- `CompetitionProjection` and `CompetitionSummary` need no change.
- No new endpoint. `/draw-phase` gains a body field; `RouteShapeTests` and
  `HandlerRegistrationTests` cover it as-is.

## Phase C — Verification and payoff

### WI-6 — Store-backed tests

`tests/Soarscore.Infrastructure.Tests/CatalogueDrawEventStoreTests.cs`, tagged
`Trait("Category", "Storage")`:

1. **The F3K payoff.** Publish the **real corpus** F3K (`Corpus.All`, `10-f3k`,
   `SeedF3K.Definition`) — no stand-in, no isolation helper — create a competition,
   register a field, draw 5 rounds with 5 distinct catalogue tasks. Succeeds, with the
   right task on each round. *This is the test that fails before the thread and passes
   after it.*
2. **The F5K payoff.** Real corpus F5K, `BindParameter minPerGroup`, then a catalogue
   draw. This is the leg `BindParameterEventStoreTests` had to abandon to NZ Class M —
   cross-reference that file's comment and note it is now covered.
3. **Replay.** Drop the read model, replay from the log; the drawn schedule including
   every `TaskRef` is identical.
4. **Capture and score through the catalogue phase.** Open entries and capture
   measurements against two rounds with *different* tasks, then `ScoreCompetition` —
   proving the per-task-round path through `TaskResolver` and `ScoringService` is real
   and not merely believed to work.

### WI-7 — Retire the acceptance stand-in, and add a catalogue scenario

Two things, in this order:

1. **Retarget `Features/CapturingAScore.feature`'s finding-3 scenario at the real corpus
   F3K** and **delete `tests/Soarscore.Acceptance.Tests/Support/AcceptanceF3KShape.cs`**.
   Tick the item in `kanban/tech-debt.md`. The scenario's step definitions will need the draw
   step to supply a task selection; prefer a Gherkin table of round → task over a magic
   default, since the scenario is about F3K.7's launch-before-working-time rule and the
   task it picks is now visible and meaningful.
2. **A new scenario, `Features/DrawingACatalogueChoicePhase.feature`**, Given/When/Then
   over real HTTP:
   - *Given* a published class whose rounds are chosen from a catalogue and a registered
     field, *When* the CD draws N rounds naming a task for each, *Then* each round is
     scheduled with the named task.
   - *And* naming the same task twice where the rules require distinct tasks is refused,
     with the CD told which task repeated.

### WI-8 — Widen the corpus property test

`tests/Soarscore.Domain.Tests/ScoringCorpusPropertyTests.cs` hard-codes eight
`DrawableFileNames` and asserts `drawable.Length == 8`. After this thread the set is
**ten** — everything but F3B.

Do not simply edit the literal list. **Derive it**: a class is drawable when its first
phase has `TasksPerRound == 1`, supplying a task selection when the phase is
`ChooseFromCatalogue`. Keep a sanity floor (`Should().Be(10)`) so a corpus change still
fails loudly, and keep the doc comment's explanation of *why* F3B is out — but the
membership test itself should follow the code's own rule, not a copy of it. This is the
same "scan the corpus, don't hard-code the list" discipline `BindParameterPropertyTests`
property 5 already applies.

Also update `RequiredBindings` in that file: F5K needs `minPerGroup` bound; F3K needs
nothing (its parameters all carry defaults).

### WI-9 — End-to-end verification

`docs/verification/catalogue-choice-draw-e2e.http`, executed and captured, following
`bind-parameter-e2e.http`'s precedent (that plan's WI-8 note: do not leave another
undocumented manual step). Two legs:

- **F3K:** publish, create, register, draw 5 rounds naming 5 tasks → success; then a
  draw naming a repeated task → `drawPhase.taskSelectionNotDistinct`.
- **F5K:** publish, create, register, draw without binding → `drawPhase.parameterUnbound`;
  bind `minPerGroup`; draw naming tasks → success. This is `bind-parameter-e2e.http`'s
  F5K leg, finally completing.

---

## Dependency order

```
WI-1 ──> WI-2 ──┬──> WI-3
                ├──> WI-4 ──> WI-5 ──> WI-6 ──> WI-7 ──> WI-9
                └──> WI-8
```

WI-1 before WI-2: the draw cannot resolve per-round sizes until the builder can consume
them. WI-3, WI-4 and WI-8 are independent of one another once WI-2 lands.

## Acceptance

- **F3K and F5K can be drawn**, from their real corpus definitions, through the real API,
  against a real store. Before this thread, neither could.
- Each round of a catalogue phase carries the task the CD named, in the order named.
- A repeated task where the rules require distinct tasks is refused, and the message says
  which and why.
- **F3B is still refused**, now with a message that names multi-task rounds and does not
  mention catalogue choice.
- The eight already-drawable classes draw exactly as before — same groups, same
  task-rounds, no call-site changes outside the ones listed here.
- **No new domain concept, no glossary change, no class-model change, no new event type,
  no event payload change, no Marten registration.**
- No class name appears anywhere in the new code (`ClassAgnosticismTests`).
- `RequireDistinctTaskPerRound` has its first production reader.
- `AcceptanceF3KShape.cs` is deleted and its `kanban/tech-debt.md` item is ticked.
- `ScoringCorpusPropertyTests` covers 10 of 11 corpus classes, by derivation not by list.

## What this unlocks

The corpus stops being partly aspirational. Ten of eleven seed classes go all the way
from publish → create → register → draw → capture → ranked result, which is the strongest
available evidence for CLAUDE.md's core architectural law: the two classes with the most
unusual round structure in the corpus become drawable without the core system learning
either name.

It also **discharges the precondition** on per-round parameter bindings (Appendix A) —
that thread was blocked on rounds not having tasks, and now they do.

**Still gated, and not by this thread:** multi-task rounds (F3B), flyoff-phase draws,
redraw / draw acceptance, and the second Entry thread (`MeasurementAmended`,
`EntryAnnulled`, `PenaltyRecorded`).

---

## Appendix A — the deferred follow-on: per-round parameter bindings

Recorded so it is not re-derived. Scope: the six `ParameterBindingPoint.PerRound`
parameters in the corpus, all F3K's — `workingTime.A/B/E/L`, `maxFlight.B/L`. All carry
declared defaults, so this is an **override** capability, not a blocker.

The shape, as far as this thread's design settles it:

- `ParameterBinding` grows an optional round scope. Since a `PerRound` parameter is named
  per *task* (`workingTime.A`) and — after this thread — each round has exactly one task,
  a round ordinal is sufficient scope; a phase ordinal comes with it once flyoff draws
  land, and adding both at once is cheaper than adding one twice.
- Resolution order becomes: round-scoped binding → unscoped binding → declared default →
  throw. `ParameterResolver` takes the round as context; `TaskResolver` and
  `Competition.OpenEntry` already know the round ordinal and pass it through.
- `Competition.BindParameter` validates that a round-scoped binding names a round that
  exists and whose task actually consumes that parameter — a check only possible after
  this thread, and the reason for the original deferral.
- The freeze rule needs a decision that this plan does not take: a `PerRound` binding is
  legitimately made after the draw (unlike `CompetitionSetup`) but presumably not after
  that round's first flight. `ValidateParameterNotFrozen` currently keys on `BoundAt`
  alone.
