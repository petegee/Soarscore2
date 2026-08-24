# Story — Accepting or rejecting the draw, and redrawing

**Status:** In progress · **Raised:** 2026-08-24 · **Fleshed out:** 2026-08-24

## What

The glossary promises it (`docs/soaring-domain-glossary.md:51`): *"A draw is
produced at the start of the competition from the registered competitors, and
can be accepted or rejected and redrawn. Once accepted, the competition can
begin."* Today only the first half exists — `DrawPhase` is unconditional
(`!Phases.IsEmpty` → `drawPhase.alreadyDrawn`, `Competition.cs:672-676`), there
is no accept, no reject, no redraw, and `Draw.Status` carries the stable
literal `"drawn"` with no defined value set (`Competition.cs:297-305`,
`:846-852`). This story adds `AcceptDraw` and `RejectDraw` commands, gives
`Draw.Status` its vocabulary, moves the field freeze onto *acceptance*, and
lets a rejected draw be superseded by a new one.

## Why it matters

A draw is a CD judgement, not a computation: the pairing algorithm minimises
repeat pairings, but only the CD can look at the groups — who got stuck
together, whether a late registrant is missing — and say "run it again".
`GET /competition` already exposes the folded draw plus
`PairwiseCoOccurrence`, built for exactly this review
(`PairwiseCoOccurrence.cs:1-8`: *"judge whether to accept it (once the
'Redrawing' thread the plan defers exists)"*). Without accept/reject that
review dead-ends: the CD can see a bad draw and cannot do anything about it.
And because the field freezes on *any* draw today
(`ValidateFieldNotFrozen`, `Competition.cs:1527-1530`), a competitor who
turns up after the draw can never be registered at all — the workflow the
glossary describes (draw → someone arrives → reject → register → redraw →
accept) is impossible end to end.

This discharges the recorded deferral in `kanban/deferred-decisions.md`
(Draw §"Redraw / draw acceptance"), whose acceptance criteria were drafted at
`kanban/completed/phase-drawn-steel-thread-plan.md:110-121`.

## Before starting

- Flyoff-phase draws and multi-task rounds stay deferred
  (`deferred-decisions.md`) — this story touches neither; it must leave
  `DrawPhase`'s rejections for them intact.
- Three decisions below deviate from or sharpen the drafted criteria; all are
  flagged inline (**D2**, **D4**, and the new **F2**) so they can be vetoed
  before code starts.
- **F1 — a `/docs` change rides along and needs explicit approval
  (house-keeping rule 4).** `CompetitionEvents.cs`' header states the event
  set mirrors `aggregate-roots.md` §3's mutation list *one-for-one*
  (`:8-9`), so growing thirteen events to fifteen makes §3's prose list stale
  (`:199-207` — "register or withdraw a competitor, append a reflight group,
  annul a task-round, record a reflight ruling"). WI-1 adds accept/reject of
  the draw to that sentence. Draft wording is at WI-1 below; **ask again at
  implementation time** — a fresh confirmation separate from this planning
  conversation — exactly as `phase-drawn-steel-thread-plan.md` WI-0 required.
  No other doc moves: the class diagram already types `Draw.Status` as a bare
  string (`soaring-domain-class-diagram.md:83-86`) so the vocabulary lands in
  the record's doc comment only (D3); the glossary already promises the whole
  workflow (`:51`, and `:31`'s Competitor entry already says registration
  closes at acceptance) — nothing there to change.
- **F2 — flagged for veto: `rejectDraw.reasonRequired`.** The drafted defect
  set (WI-2) has no Reason validation. But `Reason` here is a substantive CD
  ruling record, not an audit breadcrumb — exactly the distinction
  `task-round-lifecycle.md` WI-2 drew when it validated
  `annulTaskRound.reasonRequired` in the decide function rather than the
  handler. This fleshing-out adds the fourth reject code on that precedent;
  strike it if you disagree and `Reason` becomes free-text-unvalidated like
  `BindParameter.By`.
- Anchors were re-verified against the tree on 2026-08-24. Two claims in the
  earlier draft moved: the handler-registration sanity-floor numbers
  (corrected in finding 5) and nothing else.

---

# Plan

## Decisions settled during planning (2026-08-24)

1. **Two events: `DrawAccepted(int PhaseOrdinal, DateTimeOffset At)` and
   `DrawRejected(int PhaseOrdinal, string Reason, DateTimeOffset At)`.**
   `Reason` is audit-only, never folded — the `TaskRoundAnnulled` precedent
   (`CompetitionEvents.cs:106-115`). No payload beyond that: the fold already
   holds the phase; these events only move its `Draw.Status` (or remove it,
   D2).

2. **Rejection removes the phase from the fold — `Phases` holds only live
   phases.** *(Deviation from the drafted criteria, deliberate.)* The draft
   gave `Draw.Status` a `rejected` value; that value never appears in folded
   state under this design. Removal is what makes everything else fall out
   unchanged:
   - `DrawPhase`'s precondition (`!Phases.IsEmpty`) permits the redraw with
     no edit;
   - `PhaseOrdinal = Phases.Length` and the positional
     `AdoptedRules.Definition.Phases[Phases.Length]` lookup
     (`Competition.cs:681`, `:850`) stay correct for the replacement draw —
     keeping a rejected phase in `Phases` would address phase definition 1
     (the flyoff) when drawing the preliminary a second time;
   - registration and unscoped-parameter freeze checks (D5/D6) reopen
     automatically, because they key off the same `Phases`.
   The log retains every rejected draw — the audit trail is the stream, per
   house style. Consequence surfaced honestly: prior rejected draws are
   invisible through `GET /competition`; a draw-history read surface is a new
   deferral (WI-9), not an omission here.

3. **`Draw.Status` vocabulary is `"drawn" | "accepted"` in folded state** —
   string values, not an enum; the diagram types it as a bare string and
   `"drawn"` set the precedent (`Competition.cs:846-852`). The `Draw` record's
   doc comment states the vocabulary and D2's removal semantics.

4. **Opening an Entry requires an accepted draw** — new defect
   `entry.drawNotAccepted` in `Competition.OpenEntry`. Glossary-faithful
   (*"Once accepted, the competition can begin"*); without it, flights could
   be captured against a draw the CD has not stood behind, and rejection
   would orphan entries referencing dead GroupIds. *(Blast radius flagged:*
   every acceptance-test Given that ends "...drawn" gains an accept call —
   WI-7.)* Capture is otherwise untouched: NFR-4 governs score freshness, not
   pre-contest setup, and this gate sits before any flying exists.

5. **RejectDraw refuses when entries exist against the phase**
   (`rejectDraw.entriesExist`). Under D4 this should be unreachable through
   the API, but the decide function does not trust that: entries reference
   the doomed draw's GroupIds. The handler supplies `phaseHasEntries` as an
   already-resolved fact from `IEntryQuery.FindAsync(competitionRef,
   phaseOrdinal, …)` — `BindParameterHandler`'s `roundHasEntries` precedent
   (`per-round-parameter-bindings-plan.md` WI-9, live at
   `BindParameter.cs:44-57`). Aggregate boundary holds.

6. **Field freeze moves from "any phase drawn" to "the current phase's draw
   is accepted"** — `ValidateFieldNotFrozen` (`Competition.cs:1518-1530`)
   re-points at `Draw.Status == "accepted"`, exactly as the drafted criteria
   specified; its own doc comment has been waiting for this. Withdrawal stays
   ungated forever (`WithdrawCompetitor`'s comment, `Competition.cs:607-611`);
   aggregate-roots.md:340-343 unchanged.

7. **CompetitionSetup parameter freeze moves to acceptance too** —
   `ValidateParameterNotFrozen` (`Competition.cs:1563-1574`) swaps
   `!Phases.IsEmpty` for the same accepted check. Rationale: rebinding
   `minPerGroup` between reject and redraw may be precisely why the CD
   rejected; freezing it at draw time would make rejection useless for its
   most plausible purpose.

8. **Read model:** `CompetitionProjection` adds
   `DrawAccepted → State = "accepted"`; `DrawRejected` reverts `State` to
   `"created"` (with a live phase removed, the summary is back where
   `CompetitionCreated` left it). Both arms carry comments; the `_ => current`
   default stays for the reason it already documents. (The revert-to-created
   arm can theoretically overwrite `"finalised"` — unreachable: finalisation
   requires complete rounds, which require entries, and `rejectDraw.entriesExist`
   refuses rejection of any competition that far along. Say so in the arm's
   comment.)

## Findings from reading the tree (re-verified 2026-08-24)

1. **No store change anywhere.** New event types need only
   `[JsonDerivedType]` entries (`CompetitionEvents.cs:23-36`) and
   `SoarscoreEventTypes.cs` pairs (the `phaseDrawn` precedent, line 62);
   Marten's conventional discovery picks up the typed `Apply` overloads the
   same way it picks up every existing one (`Competition.cs:385-456` — one
   overload per non-creation event, both the domain's own fold-by-type API
   *and* what Marten matches on), and Fisher shares the store-agnostic
   adapter body.
2. **The review surface already exists.** `GET /competition` returns the
   folded aggregate — including each phase's `Draw` — plus
   `PairwiseCoOccurrence`; nothing read-side needs building for the CD to
   judge a draw.
3. **Four test files encode the old approximation** "frozen = anything
   drawn":
   - `tests/Soarscore.Domain.Tests/CompetitionDecideTests.cs:186-193`
     (`RegisterCompetitor_against_a_field_with_a_drawn_phase_fails_with_a_stable_code`);
   - `tests/Soarscore.Infrastructure.Tests/DrawPhaseEventStoreTests.cs`
     (~`:145-170`, late registration refused after draw-phase);
   - `tests/Soarscore.Domain.Tests/BindParameterDecideTests.cs:115-126` and
     `:160-173` (`parameter.frozen` after draw-phase, two tests);
   - `tests/Soarscore.Application.Tests/Commands/Competitions/
     BindParameterHandlerTests.cs` (~`:150-172`).
   Each asserted behaviour changes: frozen only after *accept*. They are
   rewritten, not deleted — the freeze rules themselves remain.
4. **`alreadyDrawn` tests survive** (`PhaseDrawnDecideTests.cs:94`,
   `DrawPhaseHandlerTests.cs:187`, `DrawPhaseEventStoreTests.cs:135`) — a
   *drawn* phase still blocks a second draw; only rejection reopens it.
5. **Routing surface grows by two POSTs**: `/accept-draw`, `/reject-draw`.
   Route-shape test unaffected (POST allowed). The handler-registration
   sanity floor (`HandlerRegistrationTests.cs:72`,
   `HaveCountGreaterThanOrEqualTo(24)`) rises by two — **note: the earlier
   draft said "seventeen commands → nineteen"; the true count today is
   twenty-three commands + ten queries mapped** (23 `MapCommand` lines in
   `Commands.cs`, confirmed by grep), so after this story it is twenty-five,
   the floor literal goes to `≥26`, and the test's own count comment — which
   already says "twenty-four commands" against twenty-three — is corrected to
   match reality while we are in there.
6. **Nothing else consumes `Phases.IsEmpty` as a proxy** — grep confirms
   `ValidateFieldNotFrozen` and `ValidateParameterNotFrozen` are the only two,
   and both are covered above. `ScoringService` walks live phases; a removed
   phase contributes nothing, which is correct.
7. **Exactly six step-definition files open entries or flights**, so exactly
   six gain an accept call (WI-7): CapturingAScore, ScoringACompetition,
   ClosingACompetition, SeeingWhatIsRecorded, ReflightingAGroup,
   RecordingAReflightRuling. `DrawingACatalogueChoicePhaseSteps.cs` draws and
   asserts on `GET /competition` output only — no accept needed there, which
   keeps that feature an honest regression guard for "a drawn-but-not-
   accepted competition still reads correctly".

## Work items

Each WI is small enough for one agent session and lands compiling. Tests green
at each stage boundary per the execution plan below — note the four rewritten
test files span three test projects, which is why stage 1 sweeps them up with
the domain work instead of leaving the suite red behind itself.

### WI-1 — Events and fold (Domain: `CompetitionEvents.cs`, `Competition.cs`)

Two records appended after `TaskRoundReopened` (keeping the lifecycle events
together), discriminators `drawAccepted` / `drawRejected` added to the
`[JsonDerivedType]` block (`:27` area), header comment updated *thirteen →
fifteen* and the mutation sentence gains "accept or reject the draw":

```csharp
/// <summary>
/// The CD standing behind the drawn schedule — glossary: "once accepted, the
/// competition can begin". Moves the named phase's Draw.Status to "accepted";
/// the field freeze (D6) and CompetitionSetup parameter freeze (D7) key off
/// this state, and Entry opening gates on it (D4).
/// </summary>
public sealed record DrawAccepted(int PhaseOrdinal, DateTimeOffset At) : CompetitionEvent;

/// <summary>
/// The CD sending the draw back — a rejected phase is removed from the fold
/// (Phases holds only live phases, decision D2), which is what lets DrawPhase
/// address the replacement draw correctly without any edit. Reason is carried
/// for audit only — TaskRoundAnnulled's precedent; the log keeps every
/// rejected draw even though the fold forgets them.
/// </summary>
public sealed record DrawRejected(int PhaseOrdinal, string Reason, DateTimeOffset At) : CompetitionEvent;
```

Fold arms beside the existing overloads (`:401-456` region), following the
`CompetitorWithdrawn` select-and-rebuild pattern, navigating by Ordinal —
never array index, `ReplaceTaskRound`'s rule (`:458-469`):

```csharp
public Competition Apply(DrawAccepted @event) { /* rebuild Phases with the named
    phase's Draw replaced: Draw.Status -> "accepted" */ }

public Competition Apply(DrawRejected @event) =>
    // RemoveAll / Where(p => p.Ordinal != @event.PhaseOrdinal) — removal, not
    // a status write; D2 is why this is safe for everything downstream.
```

Plus the F1 wording in `docs/aggregate-roots.md` §3 (`:200-203`): extend the
mutation sentence to "…register or withdraw a competitor, accept or reject the
draw and redraw a rejected one, append a reflight group, complete / annul /
reopen a task-round, record a reflight ruling" — **only after the fresh
user confirmation F1 requires.**

### WI-2 — Decide functions (Domain: `Competition.cs`)

Two instance decide functions in the `RegisterCompetitor`/`WithdrawCompetitor`
defect-chain style (no later check needs an earlier check's value), placed
after `WithdrawCompetitor` / before `BindParameter`:

| Function | Code | Condition |
|---|---|---|
| `AcceptDraw(DateTimeOffset at)` | `acceptDraw.noDrawnPhase` | `Phases.IsEmpty` |
| | `acceptDraw.alreadyAccepted` | live phase's `Draw.Status == "accepted"` |
| `RejectDraw(bool phaseHasEntries, string reason, DateTimeOffset at)` | `rejectDraw.noDrawnPhase` | `Phases.IsEmpty` |
| | `rejectDraw.reasonRequired` *(F2)* | `reason` blank |
| | `rejectDraw.entriesExist` | `phaseHasEntries` |

Semantics spelled out so no implementer guesses:

- Both act on **the live phase** — the single element of `Phases` (P1 proves
  there is at most one). The emitted event carries that phase's `Ordinal`.
- **Accept requires status `"drawn"`** — accepting twice fails
  `alreadyAccepted`.
- **Reject applies whatever the live phase's status** — drawn *or* accepted.
  Rejecting an accepted draw nobody has flown against is the ordinary
  correction path (CD accepted, then spotted the problem before flying
  began); only entries block it. There is deliberately no
  `rejectDraw.alreadyAccepted`.
- `phaseHasEntries` defaults are **not** used here — unlike
  `BindParameter.roundHasEntries` (which defaults `false` because unscoped
  binds can never be round-frozen), a wrong default on reject would let a
  caller silently orphan entries. `RejectDraw` takes it as a plain required
  parameter; only the handler ever calls this function anyway.
- `Reason` validated in the decide, not the handler — `AnnulTaskRound`'s
  recorded reasoning (F2).
- Doc comments cite D2/D4/D6/D7 and this story's path.

Three edits elsewhere in the same file:

1. **D4 gate in `OpenEntry`** (`:888-893`): directly after the existing
   `phase is null → openEntry.phaseNotDrawn` check, insert:

   ```csharp
   if (phase.Draw.Status != "accepted")
       return Result<EntryOpened>.Failure(
           "entry.drawNotAccepted",
           "The draw has not been accepted — the competition cannot begin yet.");
   ```

   Gating on the *referenced phase's* status (not "any accepted draw exists")
   is equivalent under P1 and truthful per-entry. Existing failure ordering
   is preserved: an undrawn competition still answers `openEntry.phaseNotDrawn`
   first, so every existing test that relies on that code is unaffected.

2. **D6/D7 validator re-points** (`:1518-1530`, `:1563-1574`): replace
   `!Phases.IsEmpty` with `Phases.Any(p => p.Draw.Status == "accepted")` —
   extract one private helper (`HasAnAcceptedDraw()` or similar) so the two
   validators cannot drift, keeping their separate defect codes and messages
   (they ask different questions and stay unmerged for the reason their
   comments give; update those comments — the "unreachable this thread"
   preamble at `:1518-1520` and the "will diverge once Draw.Status is
   defined" promise at `:1521-1526` are discharged by exactly this edit).

3. **D3 + stale-comment cleanups**: `Draw` record doc comment (`:296-306`)
   states the vocabulary `"drawn" | "accepted"` and D2's removal semantics;
   delete the now-false "Only the first, unconditional draw" comment above
   `DrawPhase` (`:668-671` — replacement text: the precondition means "no
   live phase", i.e. a redraw after rejection is legal, flyoffs still
   deferred) and the `"drawn" is a stable literal` note at `:846-848`.

### WI-3 — Commands and handlers (Application)

Two files in `src/Soarscore.Application/Commands/Competitions/`, on the
`DrawPhase.cs` template (`:25-53`):

```csharp
// AcceptDraw.cs
public sealed record AcceptDraw(CompetitionId CompetitionId) : ICommand<CompetitionId>;

public sealed class AcceptDrawHandler(IEventStore eventStore, IClock clock) : ICommandHandler<AcceptDraw, CompetitionId>
{ /* load -> decide -> append at ExpectedVersion.Exact(version) */ }
```

```csharp
// RejectDraw.cs
public sealed record RejectDraw(CompetitionId CompetitionRef, string Reason) : ICommand<CompetitionId>;

public sealed class RejectDrawHandler(IEventStore eventStore, IEntryQuery entryQuery, IClock clock)
    : ICommandHandler<RejectDraw, CompetitionId>
{ /* load -> resolve fact -> decide -> append */ }
```

- Naming follows whichever template each file copies (`DrawPhase` spells the
  id property `CompetitionId`; `BindParameter` spells it `CompetitionRef` —
  the repo carries both spellings; do not "fix" either).
- `RejectDrawHandler` mirrors `BindParameterHandler`'s fact resolution
  (`:44-57`): if the loaded competition has a live phase, call
  `entryQuery.FindAsync(command.CompetitionRef, <livePhase.Ordinal>, null,
  null, null, null, cancellationToken)` and pass `entries.Count > 0`;
  if `Phases.IsEmpty`, skip the query entirely and pass `false` (the decide's
  `noDrawnPhase` fires first regardless). Comment cites D5.
- `AcceptDrawHandler` needs no port beyond `IEventStore`/`IClock`.
- DI: two lines in `src/Soarscore.Api/Composition.cs` beside `:90`.

### WI-4 — Routes (Api: `src/Soarscore.Api/Commands/Commands.cs`)

Beside the other competition verbs (`:27-35`):

```csharp
app.MapCommand<AcceptDraw, CompetitionId>("/accept-draw");
app.MapCommand<RejectDraw, CompetitionId>("/reject-draw");
```

Route-shape test unaffected (verbs GET/POST only, POST allowed). Bump
`HandlerRegistrationTests.cs:72` to `HaveCountGreaterThanOrEqualTo(26)` and
correct its count comment (finding 5).

### WI-5 — Event type registry (Infrastructure: `SoarscoreEventTypes.cs`)

Two pairs appended inside the competition block, after
`ReflightRulingRecorded` (`:70`), with a comment citing this story:

```csharp
(typeof(DrawAccepted), "drawAccepted"),
(typeof(DrawRejected), "drawRejected"),
```

One list, both backends — no `MartenConfig`/`FisherConfig` edit (LADR-0001
§4.8: missing these lines fails at runtime on both stores, which WI-8's
store-backed suite is the net for). The block's running commentary
(`:47-58`) stays accurate: `RulesAmended` remains the only registered-nothing
sibling.

### WI-6 — Unit-level tests (Domain.Tests + Application.Tests)

**Rewrites (behavioural flips, finding 3):**

- `CompetitionDecideTests.cs` frozen-registration test: arrange draw +
  hand-folded `DrawAccepted` (apply the event directly — no handler needed at
  this layer), assert `field.frozen`; add a companion assertion that between
  draw and accept registration *succeeds* (that success is the point of D6).
- `BindParameterDecideTests.cs` both tests: same technique — CompetitionSetup
  bind fails only once accepted, BeforeFlying bind unaffected throughout;
  keep asserting the pair's divergence (their whole point).
- `BindParameterHandlerTests.cs`: seed the `FakeEventStore` with a
  hand-built `DrawAccepted` in the stream, assert the frozen refusal; the
  pre-accept case asserts success.

**New example-based file `DrawAcceptanceDecideTests.cs`** (Domain.Tests), one
case per code plus the happy paths:

- accept happy path → `DrawAccepted` folds `Status` to `"accepted"`;
- accept twice → `acceptDraw.alreadyAccepted`;
- accept/reject with nothing drawn → `*.noDrawnPhase`;
- reject blank reason → `rejectDraw.reasonRequired` (if F2 survives veto);
- reject with entries → `rejectDraw.entriesExist` (pass `phaseHasEntries: true`);
- **the story's core cycle**: draw → reject → register latecomer → redraw
  succeeds, and the redraw's `PhaseOrdinal` is again `0` with groups drawn
  from a field that includes the latecomer (asserts D2's ordinal-correctness
  claim concretely — the second `AdoptedRules.Definition.Phases[Phases.Length]`
  lookup addresses definition 0, not the flyoff);
- entry opened before acceptance → `entry.drawNotAccepted`; after acceptance
  → succeeds; withdraw-after-accept leaves the draw intact and the withdrawal
  honoured by `openEntry.competitorWithdrawn`.

**Serialization:** two round-trip cases in
`tests/Soarscore.Application.Tests/CompetitionEventJsonTests.cs` (one per new
event, `$kind` discriminator asserted, `TaskRoundCompleted`-style) — the
store-agnostic JSON contract is what both stores actually persist.

**Property-based (CsCheck), invariant named up front per CLAUDE.md:**

> **P1 — the draw-lifecycle state machine.** For any generated sequence over
> the alphabet {register person, withdraw competitor, draw, accept, reject}
> driven through the real decide functions and folded via `Apply`: (a)
> `Phases` holds at most one phase; (b) `DrawPhase` succeeds iff `Phases`
> is empty; (c) `RegisterCompetitor` succeeds iff no live phase is
> `"accepted"`; (d) every successful accept partitions exactly the eligible
> field — each non-withdrawn competitor appears in exactly one group per
> round of the live phase; (e) reject always leaves `Phases` empty and the
> next draw legal. A small mutable reference model tracks {registered,
> withdrawn, live?, accepted?} in lockstep — the
> `EntryModelBasedFoldTests`/`CompetitionFieldPropertyTests` shape, extended
> into `CompetitionFieldPropertyTests.cs` (or its own file citing P1).
> Generate field sizes within a literal-`MinPerGroup` corpus class
> (`Corpus.All[0]`, the file's existing fixture) so draws succeed whenever
> the model predicts they must. Mutation-check non-vacuity (the
> task-round-lifecycle WI-10 discipline): weakening (b) or (c) must fail.

### WI-7 — Acceptance tests (Acceptance.Tests)

**Existing features — mechanical accept insertion (six files, finding 7).**
Every shared Given whose scenario later opens an entry/flight appends the
accept POST immediately after its `/draw-phase` call:

- `CapturingAScoreSteps.cs:98-102` and `:108-114` (both draw-Givens);
- `ScoringACompetitionSteps.cs:109-113` and the `:123`/`:142` round-shape
  Givens;
- `ClosingACompetitionSteps.cs:68` ("under way", `:92` draw call);
- `SeeingWhatIsRecordedSteps.cs:58` (`:80`);
- `ReflightingAGroupSteps.cs:74` (`:99`);
- `RecordingAReflightRulingSteps.cs:67` (`:99`).

One-line bodies calling `/accept-draw` with `new AcceptDraw(_competitionId)`;
no step wording changes, no `.feature` churn for these.

**New feature `Features/AcceptingTheDraw.feature` + steps**, scenarios:

```gherkin
Scenario: A CD reviews the drawn groups and accepts the draw
Scenario: A rejected draw is redrawn after a latecomer registers, then accepted
Scenario: A draw cannot be rejected once flights are recorded against it
Scenario: An entry cannot be opened before the draw is accepted
Scenario: Withdrawing a competitor after acceptance leaves the draw intact
```

The first asserts `GET /competition` shows the accepted state (read-model
arm, D8); the second is the glossary sentence end-to-end and the scenario
most likely to fail loudly if anyone reintroduces a draw-time field freeze;
the third exercises D5 through the real handler + entry index. Run against
both stores per CLAUDE.md (`SOARSCORE_TEST_STORE=postgres|sqlite`).

### WI-8 — Store-backed tests (Infrastructure.Tests)

One new class `DrawAcceptanceEventStoreTests.cs` written once against
`IStoreFixture` — runs on Postgres/Testcontainers (`Trait("Category",
"Storage")`) and Fisher/SQLite automatically. Full cycles through the
dispatcher, not hand-appended events:

1. create → register ×N → draw → accept → open entry succeeds;
2. create → register → draw → reject → register latecomer → redraw →
   accept → open entry succeeds (and the stream holds both draws plus the
   rejection — the audit-trail-is-the-stream claim, asserted against a real
   store);
3. draw → accept → open entry → capture flight → reject refused with
   `rejectDraw.entriesExist`;
4. rewrite of `DrawPhaseEventStoreTests`' late-registration scenario
   (finding 3): registration refused only after *accept*, succeeding between
   draw and accept — the first real-store exercise of the re-pointed
   `ValidateFieldNotFrozen`.

### WI-9 — Board reconciliation

Move the story to `completed/` (`git mv`, status header in the same commit);
delete the Redraw/draw-acceptance entry from `deferred-decisions.md`; add two
new deferrals there:

- **Draw-history read surface** — prior rejected draws live in the log, not
  in any query (D2's stated consequence);
- **Whether `AppendReflightGroup` should also require an accepted draw** —
  unreachable-through-API today under D4 (a reflight group needs a task-round,
  which needs an entry), left ungated in the decide function.

Nothing to tick in `smaller-items.md` or `tech-debt.md` — verified
2026-08-24 against both files. F1's `aggregate-roots.md` edit lands in the
same commit as WI-1, carrying its fresh approval.

---

## Execution plan — how an agent (or agents) runs this

**Sequential by necessity, parallel-safe nowhere profitable.** Everything
downstream compiles against WI-1/WI-2's types, the rewritten unit tests seed
events only Domain knows, and the acceptance suite cannot go green until the
routes exist. A sub-agent split would add hand-off cost across one deep
compile unit. Recommended: **one implementer, four staged checkpoints** —
each ends compiling with its layer's suites green, so a crash or park leaves
clean ground. (If sub-agents are preferred anyway: map one agent per stage
below, in order, passing the tree forward — the split is the staging, not
concurrency.)

**Stage 1 — Domain core + its unit mirror** (WI-1 minus the F1 docs line,
WI-2, WI-6's rewrites/new file/property test/JSON cases).
Checkpoint: `dotnet build Soarscore.sln`; `dotnet test
tests/Soarscore.Domain.Tests tests/Soarscore.Application.Tests` green.
(Application.Tests passes here because the handler-level rewrite seeds a
hand-built `DrawAccepted` — no new handler needed.) Ask the F1 question and
land the approved `aggregate-roots.md` sentence in this stage's commit.

**Stage 2 — Wiring** (WI-3, WI-4, WI-5).
Checkpoint: build green; `dotnet test
tests/Soarscore.Architecture.Tests` green (floor bump included); API boots.

**Stage 3 — Store-backed proof** (WI-8 + the `DrawPhaseEventStoreTests`
rewrite if not already swept into stage 1's sweep of finding 3 — it belongs
here; it needs real handlers).
Checkpoint: `dotnet test tests/Soarscore.Infrastructure.Tests` — SQLite leg
always; Postgres leg wherever Docker exists. This stage is the LADR-0001 §4.8
net: an unregistered event type fails here loudly.

**Stage 4 — Acceptance + close-out** (WI-7, then WI-9).
Checkpoint: `SOARSCORE_TEST_STORE=postgres dotnet test
tests/Soarscore.Acceptance.Tests` and `SOARSCORE_TEST_STORE=sqlite dotnet
test tests/Soarscore.Acceptance.Tests`, both green; then the board move and
deferral edits. Known flake: the solution-wide Marten migration race
(`tech-debt.md` last item) — if a red first-scenario names schema migration,
re-run the project alone before diagnosing.

**Full-suite finish line** (all stages done):
`dotnet test Soarscore.sln` once for the record, accepting the known
migration flake caveat, then the two store-tagged acceptance runs above.

**Story invariant for sign-off:** the four rewritten freeze tests and the
`alreadyDrawn` trio are green and changed *only* in arrangement (freeze
semantics moved, codes unchanged); the new feature's reject→redraw→accept
scenario is green on both stores; no scoring test moved (NFR-4 untouched —
capture paths gained no new gate).

## Out of scope (deferrals restated, untouched)

Flyoff-phase draws; multi-task rounds (F3B); catalogue-choice round *re*-choice
on redraw (the redraw re-runs the same `taskRefs` contract — the CD names tasks
again, which `DrawPhase` already handles); mid-round regroup floors; team
separation / frequency management (`C.16.2.6` scope note).
