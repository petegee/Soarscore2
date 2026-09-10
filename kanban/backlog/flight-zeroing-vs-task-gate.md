# Story — Resolve FlightSelector's task gate vs FlightInterpreter's per-flight zeroing

**Status:** Backlog · **Raised:** 2026-09-09 (from `kanban/tech-debt.md`'s
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
