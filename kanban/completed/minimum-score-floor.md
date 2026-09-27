# Story — Minimum score floor: a negative raw score records zero where the class states it

**Status:** Completed 2026-09-27 (raised 2026-09-27, unblocking NdcScore
`kanban/blocked/ss_minimum-score-zero.md` — first real-use F5J NDC feedback,
tester item #5). Sibling story:
`landing-zero-and-flyaway-encoding.md` — the three scoring fixes it shares
the F5J score sum with are to land in one review, one seed drift-guard run.
Owner decisions 2026-09-27: datum is a `bool?` (revised from the initial
record-with-citation shape later the same day — nothing consumed the citation
and the corpus convention cites rules in comments, not data); grain is the
task-round score; competition grain (5.5.11.12 n) split to a new
backlog story; PreNormalisationScores preserves the floored zero; seeds are
85c + 85d **and 30-f5j**; this story lands alone.

## As built (2026-09-27)

Landed alone (owner decision): the sibling story remains in backlog and will
run its own drift guard; the three fixes it shares the F5J score sum with
were not touched here.

**WI-1 — the datum.** `FloorAtZero` is a `bool?` on `TaskDefinition`
(`src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs`, beside
`RawScore`), threaded as-is through
`ParameterResolver.ResolveTask` (`src/Soarscore.Domain/Scoring/ParameterResolver.cs:127`)
onto `ResolvedTask` (`src/Soarscore.Domain/Scoring/ScoringResultTypes.cs`,
positional with a null default so every construction site compiles). True =
the class states the floor; absent = D4's identity pass-through. The
rulebook citations live in the seeds' C# comments (the corpus's
"cite what you encode" convention) — an initial record-with-Citation shape
was replaced the same day when nothing consumed it. Canonical JSON omits it
when null (`Json.cs` `WhenWritingNull`) and emits `"floorAtZero": true` when
set — proven by emitting the corpus from a pristine HEAD worktree and
diffing: exactly three files differ (85c, 85d, 30-f5j), the other 13 classes
and 3 tapes are byte-identical.

**WI-2 — the floor, once.** `ScoringService.ScoreGroup` step 2d
(`src/Soarscore.Domain/Scoring/ScoringService.cs:170-189`): after the raw
penalties, before `NormalisationEngine.Normalise`, gated on
`resolvedTask.FloorAtZero`, Valid rows only, `RawScore <= 0m` → literal `0m`
(the clamp's −0.0 lesson, normalisation-lower-clamp.md D3). The Disqualified
flag rides on. `NormalisationEngine` untouched — the normalised-grain clamp
and the pass-through identity stand byte for byte.

**WI-3 — seeds, as verified from each class's own rulebook** (citations in
the seed comments; the wire carries only `"floorAtZero": true`).
- **85c F5J NDC** — `5.5.11.12 f` via NZ.2.4(f) (`SeedF5jNdc.cs:122`).
- **85d NZ Class Q F5K** — NZ.7.8(c)(iv) (`SeedF5kNdc.cs:178`, TaskA; B/C/E
  inherit through the `with`). NOTE: the story's original "NZ-M 3.16.1 b) iv"
  citation was a misattribution — that clause is Class Q's (March 2024's
  3.16.x renumbered into 7.8's letters per `SeedF5kNdc.cs:4-5`); NZ-M (81)
  is silent and is NOT seeded.
- **30 international F5J** — `5.5.11.12 f` (`SeedF5J.cs:117`, TaskD; the
  fly-off task inherits) — owner decision 2026-09-27, added beyond the story's
  original list: the normalised-grain clamp alone does not cover F5J's own
  stated floor (see the anomaly note below).
- **81/83/85/86** — examined; their rulebooks (NZ.7.4, NZ.7.5, NZ.7.6,
  NZ.7.7) state no floor anywhere (the corpus-wide "negative" scan of the NZ
  volume hits only 7.8(c)(iv)) → not seeded, per "seed only where stated".
- **85b** — cannot go negative (no negative-rate terms) → not seeded.
- **80** — normalises and its rulebook is silent → not seeded.
- Corpus regenerated (`dotnet run --project tools/Soarscore.SeedData`; all
  integrity gates green) and `85c-nz-f5j-ndc.json` mirrored into the NdcScore
  fixture (`~/source/NZ-NDC-Score/src/test/fixtures/85c-nz-f5j-ndc.json`),
  preserving that fixture's pre-existing "March 2024" version line — the
  fixture's only other delta from the corpus, not reconciled here (flagged to
  the owner).

**WI-4 — fixture silence.** Full acceptance suite (which carries the
Gliderscore replay and parallel-run oracles) green on sqlite: 116/116. All
eleven GS fixtures use bespoke "Gliderscore …" definitions — none derived
from seeds — so no oracle could move; the suite proves it. The two
`CorsPreflightSmokeTests` failures seen en route were the untracked local
`appsettings.Development.json` leaking CORS origins into the test host —
pre-existing environment sensitivity, unrelated (they pass with it aside and
at HEAD-without-it). Postgres-side runs were not possible here (no Docker).

**WI-5 — tests.** `tests/Soarscore.Domain.Tests/ScoreFloorTests.cs`, 10 tests
through `ScoringService.ScoreGroup`: (a) datum floors a negative to 0 and
leaves non-negative untouched, PreNormalisationScores = floored zero; (b) no
datum → negative passes through (comp-121 shape); (c) penalty composition —
floor after penalties in both arrival orders (score→penalty→floor;
penalty-dragging-positive-below), a non-reaching penalty untouched; the
Disqualified flag survives; the 30-f5j shape (normalising + datum) floors
before the ratio with the final score unchanged vs today and the consumed
value floored. **Property tests (CsCheck)** — the named invariant: for any
task with `FloorAtZero` and any metric inputs the task-round score is exactly
`max(0, unfloored sum)` and ≥ 0; and for two competitors with unfloored sums
a < b ≤ 0 both record 0 — the sub-zero tail merges into ties, pinned together
with the aggregator fact that a drop policy still selects exactly one cell
(`A_drop_policy_still_selects_exactly_one_cell_when_floored_zeros_tie`).
Domain suite: 884 passing.

**Split out.** The competition-grain floor (`5.5.11.12 n`, carried by 85c) and
the remaining-FAI-class floor audit (20-f3b/40-f5k/50-f3j/60-f5l carry
negative-rate terms behind normalisation; an all-negative group there can
still invert through the ratio — the reason 30-f5j was seeded here) → new
backlog stub `kanban/backlog/competition-total-floor-and-fai-floor-audit.md`
(owner decision 2026-09-27).

**House-keeping.** The D4 deferral bullet in `kanban/deferred-decisions.md`
amended with the datum as the rulebook-stated route (identity default
stands). `docs/rules/` untouched; no glossary concept added — the floor was
already an FAI invariant in the fai-rules skill.

## What

An F5J NDC flight with a very high launch and a modest flight time scores a
**negative** round score: flight points minus the start-height deduction
comes out below zero, and the negative is summed into the four-round NDC
total. The rulebook is explicit that this cannot happen: `5.5.11.12 f`
— *"Where the score is negative (below zero), a zero score will be recorded.
Note that any penalty points applied in the round will remain effective."*

The engine implements that floor on exactly one grain — inside the
normalisation branch — which the no-normalise NDC classes never reach. The
fix: a **task-level definition datum** (`FloorAtZero`) that gates the floor
per class, applied at the end of the raw stage after raw penalties, and
seeded only into the classes whose rules state it.

## Verified facts (SoarScore2, traced 2026-09-27 from the NdcScore blocked story)

**The rules.**

- FAI F5J `5.5.11.12 f` (`docs/rules/source-docs/f5-electric-2026.md:962-963`)
  — quoted above, verbatim.
- NZ NDC adopts it whole: NZMAA S5 §0.3 c) "Contest rules as per FAI Section
  4 … Volume F5" and f) "Scoring as per 5.5.11.12"
  (`docs/rules/nz/source-docs/nzmaa-s5-soaring-2024.md:187-188`), while
  §0.3 d) "Disregard 5.5.11.12.m and score the sum of the Raw Scores from
  the four rounds" (:183) is precisely why the NDC definitions carry no
  `normalise`. Digest one-liner: `docs/rules/f5j.md:73`.
- NZ-M states it in its own words — 3.16.1 b) iv: "If the total of all
  points is negative, the score is zero (0)"
  (`nzmaa-s5-soaring-2024.md:1880`) — the F5K/NZ pattern is per-class
  wording, so seeding must follow each class's own text, not a blanket
  adoption.

**The engine, grain by grain — the floor exists only on the normalised
grain:**

- Per flight: `FlightInterpreter.Interpret` sums term contributions verbatim
  (`src/Soarscore.Domain/Scoring/FlightInterpreter.cs:92-99`). No floor.
- Per task: `FlightSelector.SelectAndScore` sums selected flight scores,
  then PerTask caps and optional rounding
  (`Scoring/FlightSelector.cs:117-125`). No clamp.
- Per group: the pass-through branch is the identity, negatives included
  (`Scoring/NormalisationEngine.cs:63-84`); the lower clamp exists at
  `:185-192`, citing `5.5.11.12 f` by name — **only inside the
  has-normalisation branch**. The rule the comment claims is exactly the one
  the NDC path cannot reach.
- Recorded penalties are floored at the raw stage
  (`Scoring/PenaltyEngine.cs:139-141`, `Math.Max(0m, …)` after deduction) —
  but the start-height deduction is a **score term** (a negative-rate
  piecewise), not a RecordedPenalty; no penalty is recorded on a clean
  flight, so that floor never sees the deficit.
- Competition grain: phase aggregates sum plainly
  (`Scoring/ScoringService.cs:565-573`) and the final assembly subtracts
  aggregate penalties with no clamp (`:586-599`, the subtraction at `:591`).
- Wire: `GET /task-round-result` and `GET /competition-result` carry bare
  numbers with no floor metadata — nothing for NdcScore to key a display
  clamp from (NdcScore law 2 forbids client arithmetic; today it renders the
  negative verbatim, `src/scoring/ScoreTable.tsx` `Verbatim`).

**The definition** (`tools/Soarscore.SeedData/json/85c-nz-f5j-ndc.json`,
mirrored by the NdcScore fixture `src/test/fixtures/85c-nz-f5j-ndc.json`):
one Preliminary phase, 4 rounds, no drops, no `normalise` key anywhere; task
D scores rate (`flightTime × 1`, cap 600 PerFlight, :236-243) **+** piecewise
(`startHeight`: −0.5/m to 200 m, −3/m above, :244-258) **+** conditional
landing lookup (:259-333). Worked example: 240 s from 300 m →
240 − (0.5×200 + 3×100) = **−160**.

**Blast radius:** the seven NZ classes all pass through the pass-through
branch; six carry negative-rate terms (only 85b F3K NDC cannot go
negative). 80 NZ-M ALES200 and international F5J (`30-f5j.json`) normalise,
so the existing clamp covers them — which is why the bug surfaced on the NDC
sheet first.

## Why it matters

First real-use feedback on the F5J NDC sheet, verbatim:

> "#5 "Minimum score should be zero... F5J calcs negative score with a very
> high launch." … "i assume this is a soar-score bug?"

Yes. A 300 m launch costs 400 points — more than many flights earn — so the
defect band is the *normal shape* of a poor flight on a windy day, not an
exotic corner. Today the engine records −160 where the rulebook mandates 0,
the standings sum the negative, and the displayed score contradicts the
paper scoresheet the organiser is transcribing from.

## Cross-reference (house rule 2)

- **Deferred decision D4** (`kanban/deferred-decisions.md`, "Pass-through …
  stays an identity on raws, including negative ones — GS's option-0 floor
  is not replicated", `normalisation-lower-clamp.md` D4, 2026-08-28) refuses
  an **unconditional** engine floor in the pass-through branch. This story
  does not reopen it: the proposed mechanism is definition data, not engine
  policy — a task without `FloorAtZero` keeps D4's identity pass-through
  byte for byte, and comp 121's negative-raw fixture oracle (−2026) is
  untouched because its class states no floor. Where D4 governed what the
  engine may do by itself, this datum governs what a class may state. On
  landing, that deferral bullet gets an amendment noting the datum as the
  rulebook-stated route (not a deletion — the identity default stands).
- **NFR-1/NFR-2** (`docs/non-functional-requirements.md`): the variance is
  definition data; the datum is additive and omitted by canonical JSON so
  every existing payload, seed and parallel-run fixture is unchanged.
- **NFR-4**: no capture gating — the reject/clamp-at-capture alternative was
  set aside (see below); nothing here makes capture conditional on scoring
  state.

## Plan

- **WI-1 — the datum.** Add `FloorAtZero` (bool, plus a short rulebook
  citation string — decide the exact shape with the owner) to
  `TaskDefinition`
  (`src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs:183-225`;
  natural slot beside `RawScore` at `:206`). Additive (NFR-2). Confirm the
  canonical-JSON emitter omits the default so `tools/Soarscore.SeedData/json/`
  and every NdcScore fixture diff is empty until a seed actually flips it.
  Thread it through `ParameterResolver.ResolveTask` so `ResolvedTask`
  carries it.
- **WI-2 — the floor, once.** Apply at the end of the raw stage: a step in
  `ScoringService.ScoreGroup` between the raw-penalty application
  (`:158-159`) and `NormalisationEngine.Normalise` (`:174`), gated on the
  resolved task's datum — after raw penalties, mirroring
  `ApplyRawPenalties`'s existing `Math.Max(0m, …)` placement
  (`PenaltyEngine.cs:139-141`), so "penalties remain effective": a recorded
  penalty that pushed the score to the floor is embodied in the recorded 0,
  not cancelled. Engine floor exists *only* where the datum says so —
  NormalisationEngine's existing normalised-grain clamp and D4's
  pass-through identity are untouched.
- **WI-3 — seeds.** 85c F5J NDC first (`5.5.11.12 f` via S5 §0.3 f). Then
  examine 81, 83, 85, 85d, 86 for their own wording — seed only where the
  class's rules state the floor, citing each class's own clause (the
  NZ-M 3.16.1 b) iv pattern). Regenerate the corpus with the SeedData tool
  (integrity checks green), and mirror `85c-nz-f5j-ndc.json` into the
  NdcScore fixture. Drift-guard discipline per
  `kanban/backlog/ci-seed-corpus-drift-guard.md` (check in / regenerate in
  the same commit).
- **WI-4 — golden/parallel-run fixtures.** Run the drift guard over
  `tests/GliderscoreFixtures` and the parallel-run oracles: the change must
  not move any fixture whose class has no datum. Ledger nothing — silence is
  the assertion.
- **WI-5 — tests.** Unit tests pinning: (a) a task with the datum floors a
  negative sum to 0 and leaves a non-negative sum untouched; (b) a task
  without the datum is byte-identical to today (the comp-121 shape); (c) the
  floor composes with recorded penalties in both orders (score → penalty →
  floor; floor applied after penalty only). **Property test (CsCheck)** —
  named invariant: *for any task with `FloorAtZero` and any metric inputs,
  the emitted task-round score equals `max(0, unfloored sum)` and satisfies
  `score ≥ 0` — and for two competitors with unfloored sums a < b ≤ 0 both
  record 0* (the floor merges the sub-zero tail into ties; drop policy must
  still select exactly one cell — pinned).

## Design questions to settle

- **Grain:** per task-round score (the FAI clause sits in the group-score
  section `5.5.11.12`), or per flight for multi-flight tasks? 85c is
  single-flight so the two coincide; F3K-family tasks never go negative, so
  no corpus case distinguishes them — pick one, state it in the datum's doc
  comment.
- **Competition grain:** `ScoringService.cs:591` subtracts aggregate
  penalties from the total with no clamp, and `5.5.11.12 n`
  (`f5-electric-2026.md:1007-1011`) floors the *after-penalty* total at zero
  at the competition grain, with the fly-off scoring per `5.5.11.12`
  (`:1033`). Does the floor apply there too, under a phase- or
  class-level datum, or only per round? Settle from the wording, not
  symmetry.
- **`PreNormalisationScores`** (`kanban/completed/pre-normalisation-score-view-field.md`):
  for a floored row, is the preserved pre-normalisation value the true
  negative or the floored zero? The view field exists to show what
  normalisation consumed — decide which truth it owes before touching
  `NormalisationEngine`'s pre-score capture.
- **Drops:** floored-zero cells change which cell a drop policy selects in
  classes that drop. 85c drops none; verify no NDC class drops before
  assuming no impact.

## Before starting

- Settle the grain and competition-grain questions from the FAI/NZ wording
  (not symmetry), with the owner.
- Confirm the seed list against each class's own rulebook clause — seed only
  where stated.
- Land together with `landing-zero-and-flyaway-encoding.md` (same review;
  one seed drift-guard run — all three fixes edit the same F5J score sum).

## Alternative homes considered and set aside

- **Unconditional floor in `NormalisationEngine`'s pass-through branch** —
  the one-line fix, but unstated engine policy for any class whose rules are
  silent, and exactly what deferred-decision D4 refuses; it would also
  rewrite raw-grain semantics the GS fixture oracles pin.
- **Capture-time reject/clamp** — needs new `MetricDefinition` range data on
  the wire plus a capture gate (NFR-4 tension), for a problem that is purely
  one of *scoring interpretation*, not capture validity.

## NdcScore's part once it lands

Nothing structural. The client renders the engine's number verbatim
(`String(value)`, no arithmetic), so once the engine floors, the display
corrects itself with no client change. Today's negative shows as `-160` in
Group scores and Provisional standings; after, `0`.
