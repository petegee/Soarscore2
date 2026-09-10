# Story — Resolve FlightSelector's task gate vs FlightInterpreter's per-flight zeroing

**Status:** Completed 2026-09-10 · **Raised:** 2026-09-09 (from `kanban/tech-debt.md`'s
`FlightSelector`-vs-`FlightInterpreter` item, found 2026-09-04 during
`kanban/completed/f5k-fixture-from-server-db.md` WI-3)

## What

`FlightInterpreter.Interpret` (`src/Soarscore.Domain/Scoring/FlightInterpreter.cs:72-88`)
zeroes a flight that fails `flightValidWhen` but leaves it selected and
counted (`FlightResultState.Valid`, score 0, empty contributions) — the
"zero points for that flight only" reading of 5.5.10.12 flight penalty b
that `tools/Soarscore.SeedData/SeedF5K.cs:119-125` documents (Task B's last
flight stays last). But `FlightSelector.SelectAndScore`
(`src/Soarscore.Domain/Scoring/FlightSelector.cs:90-106`, step 4)
re-evaluates `task.ValidWhen` over the SELECTED flights — with their
original metrics intact — and returns `NoResult` for the whole cell if ANY
of them fails. The two sites encode different answers to the same question
(per-flight zero vs all-or-nothing gate), and no corpus fixture witnesses
the difference yet: the F5K flight-zeroing flag (`landedOnField`) is never
false in `f5k-ni-round-2`, and `landedInPilotArea` is a scored −10 term, not
a validity flag.

Decide which reading is correct, align the two sites generically, and cover
it with domain tests before a real fixture witnesses a zeroed flight.

## Why it matters

- A single off-field F5K flight today flows through two contradictory
  semantics depending on which predicate a future class definition happens
  to set. The first real OOF flight in a replay will produce a silently
  wrong grain (a zeroed flight vs a voided cell normalise completely
  differently) with no tripwire in place.
- The model already distinguishes the two levels deliberately
  (`ClassDefinition.cs:199-203`: `ValidWhen` "decides whether the TASK has
  a result at all — NoResult, not zero (F2)"; `FlightValidWhen` "zeroes ONE
  FLIGHT while leaving it selected (F17)"). The engine must honour that
  distinction in combination, not just in isolation.
- Verified 2026-09-09: no seed sets BOTH gates on one task (task-level
  `ValidWhen` lives only on `SeedF3B.cs:143` Task C and `SeedF3F.cs:56`;
  every other gate in the corpus is `FlightValidWhen`-only). So the
  conflict is genuinely unwitnessed — this story is cheap now and a
  triage exercise later.

## Before starting

- The rule: 5.5.10.12 flight penalty b, "Landing outside the flying field
  will result in 'zero' points for that flight only". Pull it verbatim via
  the `fai-rules` skill (`.claude/skills/fai-rules/scripts/fai-rule.sh show
  5.5.10.12`) — do not decide from memory or from the condensed doc alone.
  The expected outcome is confirmation of the seed's existing reading, but
  the WI-1 decision must cite the verbatim text, including what "zero" vs
  the safety-penalty "zero for the round" wording commits us to.
- Read `FlightSelector.cs:42-127` (the step 0–8 pipeline, especially step 4)
  and `FlightInterpreter.cs:43-109` (the zero-but-valid return) end to end.
  Note that a zeroed flight keeps its ORIGINAL metrics, so it can still
  trip the task gate downstream.
- Read the neighbouring semantics that constrain the answer: F3K.9.3's
  late-landing zero must NOT promote the predecessor for `LastFlight`
  (`SeedF3K.cs:45-51` — zeroed flight stays selected); F3B Task C's
  `ValidWhen` (`SeedF3B.cs:111`, `SeedF3B.cs:143`) is the one live task-gate
  witness and must keep returning `NoResult` (`FlightSelectorTests.cs:149`
  pins it); `SeedF3F.cs:56` is the second.
- Core architectural law (CLAUDE.md): the fix must be generic engine
  semantics — no `if class == "F5K"` branch anywhere. Both gates are class
  data; their combination rule belongs to the core exactly once.
- Cross-reference (house rule 2): `docs/users.md`, NFRs, rule docs — no
  known contradiction; state the check in WI-1 rather than assuming it.

## Plan

- **WI-1 — Decide, on the record.** (a) Cite verbatim 5.5.10.12(a–c) and the
  safety-penalty wording; confirm flight penalty b is flight-scoped and the
  `SeedF5K.cs` `FlightValidWhen` encoding stands. (b) Decide the combination
  semantics for a task carrying BOTH gates — the question step 4 must
  answer: does a flight already zeroed by `flightValidWhen` participate in
  the `task.ValidWhen` gate? Recommended default: it does not — a zeroed
  flight has already scored its 0 and stays counted; the task gate judges
  the countable flights (so a lone zeroed `LastFlight` still yields a Valid
  0, matching the F3K.9.3 "stays last" precedent, while a genuinely
  task-void condition on a countable flight still voids the cell). Either
  way, write the decision and its justification into this story file before
  touching `src/` — a later reader must be able to reconstruct WHY from
  here, not from a diff. (c) Also decide the BestN interplay explicitly: a
  zeroed flight ranks by score 0 (selected last / dropped first under
  score ranking) — confirm that is intended and say so, or change it with
  a cited reason. Record all three decisions here.
- **WI-2 — Failing tests first.** Domain tests only — no corpus fixture is
  needed (the conflict is unwitnessed BY DESIGN; synthetic tasks are the
  correct witness). Minimum set, all through the real pipeline
  (`FlightInterpreter.Interpret` → `FlightSelector.SelectAndScore`, no
  mocks between stages):
  1. F5K Task A shape: one of four selected flights off-field →
     `Valid` cell, zeroed flight still selected, raw = sum of the other
     three (per-flight zeroing survives selection).
  2. F5K Task B (`LastFlight`) shape: last flight off-field → `Valid`
     with raw 0, NOT `NoResult`, predecessor NOT promoted.
  3. The combination case on a synthetic task carrying BOTH gates with
     disjoint predicates: a flight failing ONLY `flightValidWhen` must not
     trip the `task.ValidWhen` gate (per the WI-1 decision — if WI-1
     decides the other way, this test asserts `NoResult` instead; the
     point is the behaviour is pinned either way).
  4. The task-gate negative control: a countable flight failing
     `task.ValidWhen` still voids the whole cell (`NoResult`, null
     selection) — i.e. WI-1's carve-out does not defang the F3B-C/F3F
     semantics. (Overlaps `ValidWhen_failing_returns_NoResult`; keep both
     — that test guards the single-gate path, this one guards it beside a
     live flight gate.)
  Name the invariant explicitly (testing approach): **a `flightValidWhen`
  failure zeroes exactly that flight's contribution and never changes
  selection count or cell state; a `task.ValidWhen` failure on a countable
  selected flight voids the cell.** If that invariant admits a CsCheck
  property (e.g. over random flag-vectors on a fixed two-gate task:
  raw == sum over countable-passing flights, cell state == NoResult iff any
  countable selected flight fails the task gate), write it as a property
  test alongside the examples; if the generator shape fights back, record
  why in the story and keep the examples.
- **WI-3 — Align the engine.** Implement the WI-1 decision at exactly one
  site (expected: `FlightSelector.cs` step 4 — e.g. evaluate the task gate
  over countable selected flights, where "countable" = selected flights
  that passed `flightValidWhen`; `FlightInterpreter`'s zero-but-valid
  return is already the F17 behaviour and should not move). Keep the
  pending-flight behaviour (`complete` vs `awaiting`, steps 1b/8)
  untouched. No seed changes are expected — if WI-1 concludes a seed
  mis-encodes its rule (e.g. something that should be a flight gate sits
  in `ValidWhen`), that is a seed fix with its own rule citation, done
  here openly, not smuggled in.
- **WI-4 — Verify and close.** Fast loop green (Domain, Application,
  Architecture, Infrastructure non-Storage); BDD acceptance green on BOTH
  stores (`SOARSCORE_TEST_STORE=sqlite` and `postgres`) — the change sits
  in the scoring path every replay exercises. Tick the tech-debt item
  (mark `[x]`, append the discharge note with this story's path, mirroring
  the existing discharged entries' style); reconcile
  `kanban/deferred-decisions.md` only if WI-1 deliberately leaves
  something out; `graphify update .`; move this file to `completed/` with
  the `**Status:**` header updated in the same commit. No glossary or
  class-diagram change is expected (F2/F17 already exist) — if WI-1 finds
  the glossary cannot express the combination rule, STOP and surface it
  per the no-new-concepts rule instead of inventing language here.

## Acceptance

- The four (or more) WI-2 tests fail before WI-3 and pass after; no
  existing test changes meaning (F3B-C/F3F gate tests still assert
  `NoResult`; F3K zero-but-valid interpreter tests untouched).
- Full fast loop + both-store BDD green, recorded in the story's As-built
  section with test counts.
- The tech-debt entry is discharged with a pointer here, and this story's
  WI-1 section contains the verbatim-rule citation plus the three recorded
  decisions, so the combination semantics are reviewable without reading
  code.

## WI-1 decision (2026-09-10)

Decided from the verbatim rule text (`.claude/skills/fai-rules/scripts/fai-rule.sh
show 5.5.10.12`), the engine sources (`FlightSelector.cs`, `FlightInterpreter.cs`)
and the constraining neighbours (`SeedF5K.cs`, `SeedF3K.cs`, `SeedF3B.cs`,
`ClassDefinition.cs`, `FlightSelectorTests.cs`). Decision only — no `src/` or
`tests/` change in this WI.

### (a) Verbatim rule — flight penalty b is flight-scoped; the SeedF5K encoding stands

5.5.10.12, verbatim:

```text
Flight penalty:
   a)    Overfly landing window will result in a 100 points penalty for the flight score
   b)    Landing outside the flying field will result in “zero” points for that flight only
   c)    Motor restart during flight will result in a zero for that flight

Safety penalty – zero for the round:
   a)    Hitting some else than yourself or your timer will result is a zero (0) for the round
   b)    Flying in a no fly or other safety zone will result in a 300 points penalty. The penalty is
   c)    deducted from the final score
```

The section scopes its own penalties: flight penalties b and c are "for that
flight (only)", while the safety block says "zero (0) for the round" and
"deducted from the final score". The rulebook distinguishes flight-scoped zero
from round-scoped zero in adjacent clauses — the engine must not amplify one
into the other. Flight penalty a is a scored penalty ("for the flight score"),
which is why F5K's pilot-area/overfly items are score terms, not validity flags.
The F5L summary agrees in one class: "Landing outside the area → 0 for that
flight; overfly working time by > 30 s → 0 for the whole task"
(`docs/rules/f5l.md:35`) — scope differs rule by rule and is the class data's
job to say.

**SeedF5K confirmed.** `SeedF5K.cs:119-125` encodes flight penalty b as
`FlightValidWhen = Predicate.Is("landedOnField", true)` (F17, zero-but-selected)
with flight penalty a as scored terms — correct against the verbatim text. No
seed correction.

### (b) Combination semantics — the task gate judges countable flights only

**Decision:** on a task carrying BOTH gates, a flight already zeroed by
`flightValidWhen` does NOT participate in the `task.ValidWhen` gate.
"Countable" = a selected flight that passed `flightValidWhen`. The task gate
voids the cell iff some countable selected flight fails it. Consequences: a
lone zeroed `LastFlight` yields `Valid` with raw 0, never `NoResult`; a
task-void condition failing on a countable selected flight still voids the
cell. If every selected flight is zeroed, the gate passes vacuously (same
Valid-0 outcome).

**Justification.** F17's consequence is complete when it lands: the flight is
zeroed and stays selected; its original metrics remain as the record of what
was flown, not as live gate input. Feeding them to the task gate would attach
a second consequence — voiding the task — that no rule text states, and
5.5.10.12 itself shows the rulebook scopes penalties explicitly rather than
letting a flight zero cascade. F2's scope is the task result as a whole: the
corpus witnesses of `ValidWhen` (F3B-C, `SeedF3B.cs:111-112` + `:143-148`;
F3F.1.6's nine conditions, `competition-class-notation.md:1415-1419`) are all
"official but no result" rulings the rulebook scopes to the task — notably the
notation doc records that F3F routes those *zero-score* conditions to
`validWhen` precisely because a raw zero would win the inverted group. Where a
rulebook wants a flight zero, it writes a flight zero (F17); where it wants a
voided task, it says so via F2. The F3K.9.3 precedent (`SeedF3K.cs:45-52`) is
the binding precedent for the both-gate case: a late-landing last flight scores
zero, stays selected, predecessor not promoted, cell Valid — a both-gate task
must not convert that Valid 0 into NoResult. Both existing single-gate
behaviours are preserved exactly: F3B-C/F3F set no `flightValidWhen`, so for
them countable == selected and step 4 is byte-identical
(`FlightSelectorTests.cs:149` stays true).

**Generic-engine check:** the rule is stated once in the core over class data
(two predicates), no per-class branch. **No-new-concepts check:** F2 and F17
already exist (`competition-class-notation.md` F-table lines 1332/1361; class
diagram note "Two gates, different outcomes" line 1147; "A zeroed flight is
still a flight" lines 1358-1366; glossary "no result at all … not the same as a
result of zero" line 105). "Countable" is a predicate over two existing
concepts (selected ∧ passed `flightValidWhen`), not a new concept. **No glossary
change needed — no STOP condition.**

### (c) BestN interplay — a zeroed flight ranks by score 0: confirmed intended

Under score ranking (`SelectBestN`, `FlightSelector.cs:179`), a zeroed flight
ranks at 0 — selected last among candidates, first out. Confirmed: a zeroed
flight must never outrank a scored flight on its zeroed score. Under
`RankByMetric` ranking (F5K Task A, `SeedF5K.cs:100`), the zeroed flight keeps
its ORIGINAL metrics (`FlightInterpreter.cs:72-88`) and competes for a BestN
slot on its measured flight time — also intended: the flight happened and
occupies one of the four slots; only its points are zeroed ("for that flight
only"), which is exactly how an off-field F5K flight must bite.

### Finding for WI-2/WI-3 — ClampAndRecompute can un-zero a flight

`ApplyTargets` → `ClampAndRecompute` (`FlightSelector.cs:293-342`) re-scores
ALL terms on clamped metrics WITHOUT re-checking `flightValidWhen`. A zeroed
flight whose ranking metric exceeds its assigned target (F5K Task A: off-field
120 s clamped to a 60 s target) leaves the clamp with a NON-zero score — the
zeroing silently undone. WI-2 must pin this; WI-3 must preserve the zero
through the clamp (generic — mechanism is WI-3's to choose).

### House-rule 2 cross-reference

Checked `docs/users.md`, NFR-1/2/4 (`docs/non-functional-requirements.md`) and
the rule docs. No contradiction found; nothing to surface to the owner:

- `users.md` (Scorer: the system "applies the scoring rules … consistently from
  the raw data", `docs/users.md:78-83`) — this decision is a consistency fix in
  exactly that spirit.
- NFR-1 (one place knows a class's shape) — the combination rule lives once in
  the core, reading both gates as class data. Reinforced, not touched.
- NFR-2 (additive-only) — no new term type, no edit to existing behaviour;
  single-gate semantics unchanged.
- NFR-4 (no imposed ordering on capture) — untouched; a zeroed flight keeps its
  original metrics, so late capture or amendment re-derives identically.

### WI-3 as-built (2026-09-10)

Both sites in `src/Soarscore.Domain/Scoring/FlightSelector.cs` (`FlightInterpreter`
untouched): step 4 now judges `task.ValidWhen` over countable selected flights
only — countable = passed `flightValidWhen`, re-checked by the new
`IsZeroedByFlightGate` helper on the flight's ORIGINAL metrics (clamping rewrites
scores, never metrics), so an all-zeroed selection passes vacuously and F3B-C/F3F
(set no flight gate) are byte-identical — and `ClampAndRecompute` re-applies the
same gate before re-scoring so a clamp cannot un-zero a zeroed flight. Domain:
779→783 passing, 13→9 failing (all 8 `FlightZeroingTaskGateTests` pass; the 9
residual failures are the pre-existing `CatalogueDrawPropertyTests`, unchanged
name-for-name). Ripple: Architecture 7/0, Infrastructure non-Storage 76/0,
Application 309+1 pre-existing worktree repo-root detection failure (reproduces
on the untouched baseline).

## As-built (2026-09-10)

WI-4 verification for the record, from the worktree
`/home/pete/Source/SoarScore2-flight-zeroing` after WI-1..WI-3. Every count
below is a fresh run; every failure named was also reproduced on the stashed
baseline (`git stash -u` → run → pop) and is therefore unrelated to this
story's diff.

- **Fast loop:** Domain **783 passed / 9 failed** (792); Application **309
  passed / 1 failed** (310); Architecture **7 passed / 0 failed**;
  Infrastructure non-Storage **76 passed / 0 failed**. The 10 failures are the
  documented pre-existing baseline, unchanged name-for-name: 9
  `CatalogueDrawPropertyTests` (Domain) and 1 `SeedCorpusIngestionTests`
  (Application — its worktree repo-root detection). A later reader: these 10
  are NOT this story's doing; the story's 8 `FlightZeroingTaskGateTests` all
  pass and Domain went 779→783 passing.
- **BDD acceptance, both stores:** sqlite **80 passed / 6 failed** (86);
  postgres **80 passed / 6 failed** (86). IMPORTANT — the suite is not green
  in this worktree and was not green before this story either: the same six
  tests fail on both stores and on the stashed baseline — 4×
  `F5JChristchurchTapeReadingExamplesTests` plus the `ales-sample-comp` and
  `f5j-christchurch-2019` parallel-run tests, every one dying in
  `SeedDefinitionLoader.ResolveSeedDirectory`
  (`tests/Soarscore.Acceptance.Tests/Support/Gliderscore/SeedDefinitionLoader.cs:61`)
  with "No tools/Soarscore.SeedData/json directory found above …/bin/…". Same
  root-detection class as the `SeedCorpusIngestionTests` Application failure:
  running the suite from a git worktree, an environment issue, not a scoring
  regression — this story's diff is provably neutral on the suite (identical
  failure set with and without it). These 6 belong in the same bucket as the
  10 documented fast-loop baseline failures.
- **Board reconciliation:** the `FlightSelector`-vs-`FlightInterpreter`
  item in `kanban/tech-debt.md` is ticked with a discharge note citing this
  story; `kanban/deferred-decisions.md` needs **no change** — WI-1 decided
  the verbatim reading, the combination semantics, the BestN interplay and
  the clamp finding completely, with no deliberate leftovers to record.
