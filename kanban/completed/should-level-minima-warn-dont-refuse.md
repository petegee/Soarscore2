# Story stub — SHOULD-level minima warn, don't refuse

**Status:** Completed 2026-09-10 (WI-1–WI-5 landed and verified, both stores) · **Raised:** 2026-09-10 from the R5 finding in
`kanban/blocked/f5j-christchurch-parallel-run-witness.md` (owner decision
2026-09-10, Pete)

## What

Recalibrate the engine's SHOULD-vs-shall hardness at draw prescription: a
breach of a SHOULD-level rulebook minimum (the case in hand: R5's drawn
5-group against F5J's min-6, `5.5.11.8.1 a)` — "SHOULD be scheduled") must
not refuse with `prescribeDraw.groupBelowClassMinimum`; it must prescribe
and surface as a warning somewhere/somehow. The warning mechanism is itself
deferred (`kanban/deferred-decisions.md` §Draw) — this story designs it.

## Why it matters

The witness run proved the current hardness wrong against real club practice:
the club flew a 5-group nobody had to repair (for an 18-pilot contest,
`5.5.11.14.1 e)` triggers move-up/cancel-refill only at 4-or-fewer), yet the
seed encodes the SHOULD as a hard class minimum and the engine enforces it
as a terminal refusal — so a faithfully-drawn round cannot be scored. The
f5j-christchurch witness stays parked in `blocked/` until this lands; R5
stays in its window (no re-triage, no repartition).

## Before starting

- Read the Blocker section of
  `kanban/blocked/f5j-christchurch-parallel-run-witness.md` (verified R5
  5/6/7 partition, rulebook position via the `fai-rules` skill: SHOULD-level
  `5.5.11.8.1 a)`, advisory `5.5.11.14.1 d)–e)`, 4-or-fewer trigger).
- The seed side is rulebook-faithful as a *minimum* (`SeedF5J.cs:91` citing
  5.5.11.8) — the question is refusal-vs-warning hardness in the engine, not
  the number. Do not "fix" by lowering minima (tuning the seed to GS is the
  witness story's anti-goal).
- No warning machinery exists in `src/` today (verified 2026-09-10 — only
  unrelated comments). The `Result<T>`/defect vocabulary, the event log
  (auditability is the trust model), and read-model surfacing are all in
  play; decide together, not piecemeal.
- Scope check: which other minima are SHOULD-level vs mandatory (draw field
  minima, group minima across the corpus — F3J 6 / prefer 8–10, F3B, F3F 10,
  F5K/F5L/NZ-M none stated)? The `fai-rules` skill's "The draw / group
  sizes" table is the starting point. Decide whether the recalibration is
  one gate (`groupBelowClassMinimum` at `Competition.cs:1371` via the shared
  `ResolveSchedule`) or a general SHOULD principle — per the core law, it
  must be generic, never per-class branching.
- Unblocks: on landing, move the witness story back to `in-progress/` and
  re-run its WI-3 measured-first run (R5 prescribes with a warning; drop +
  rounding witness proceeds).

## Plan

### Gate inventory (verified against the tree 2026-09-10; cite before relying)

All draw/prescription minima flow through one shared validator,
`ResolveSchedule` (`src/Soarscore.Domain/Competitions/Competition.cs:1424-1580`),
reached by `DrawPhase` (`:1212`, prefix `drawPhase`) and `PrescribeDraw`
(`:1309`, prefix `prescribeDraw`):

| # | Gate (`Competition.cs`) | Codes reached | Command path(s) | Proposed hardness |
|---|---|---|---|---|
| G1 | `alreadyDrawn` (`:1430-1434`) | `drawPhase.` / `prescribeDraw.` `alreadyDrawn` | both | Refusal always — not a minimum, untouched |
| G2 | `fieldEmpty` (`:1527-1530`) | `drawPhase.` / `prescribeDraw.` `fieldEmpty` | both | Refusal always — zero eligible, nothing to schedule |
| G3 | `parameterUnbound` (`:1559-1563`) | `drawPhase.` / `prescribeDraw.` `parameterUnbound` | both | Refusal always — the minimum cannot even be evaluated |
| G4 | `fieldTooSmall` (`:1567-1572`): eligible field < resolved round minimum | `drawPhase.` / `prescribeDraw.` `fieldTooSmall` | both | SHOULD → warn-through (see WI-2); shall → refusal (unchanged) |
| G5 | prescribed group < 2 (`:1361-1366`) | `prescribeDraw.groupTooSmall` | prescribe only | Refusal always — engine constant, class-independent (a lone pilot cannot group-score; cf. `ScoringVocabulary.cs:271-276`: "`minPerGroup 1` is a fabricated rule") |
| G6 | prescribed group in `[2, min)` (`:1368-1373`) | `prescribeDraw.groupBelowClassMinimum` | prescribe only | SHOULD → prescribe + recorded warning (the R5 case); shall → refusal (unchanged) |

`DrawPhase` can never trip G5/G6 — the generator derives
`groupCount = max(1, field / minPerGroup)` (`PhaseDraw.cs:91`), so generated
groups always clear the minimum except via G4 (field < min → single
below-minimum group). Out of scope but classified, unchanged: `fieldTooSmall`
is the only DrawPhase minimum refusal; `appendReflightGroup.groupTooSmall`
(`Competition.cs:1989-1994`, reflight new-group minima — F5J 6 / F3J, F3K,
F5K 4 — all mandatory-verb placement rules) stays a refusal; the
scoring-side annulment `MinValidResults` (`NormalisationEngine.cs:57-60`,
F3B.1.8 c) is not a refusal at all; `finalise.notEnoughRounds`
(`Competition.cs:2275-2280`, validity minima) stays a refusal.

### SHOULD-vs-shall classification (via the `fai-rules` skill; verbatim verbs)

| Class | Rulebook minimum | Modal verb | Hardness | Seed today |
|---|---|---|---|---|
| F5J | `5.5.11.8.1 a)` "A minimum of six (6) competitors **should** be scheduled"; repair rule `5.5.11.14.1 d)–e)` is headed "**Advisory** Information" ("should move up" at 5-or-fewer; **4-or-fewer** for ≤30-pilot contests) | SHOULD / advisory | **SHOULD — warn** | `SeedF5J.cs:91` literal 6, citing 5.5.11.8 |
| F3J | `F3J.6.1 a)` "A minimum of 6 and preferably 8 to 10 competitors **should** be scheduled"; `F3J.13.1 c)` advisory (fairness floor 4, repair at 3-or-fewer) | SHOULD / advisory | **SHOULD — warn** | `SeedF3J.cs:77` literal 6 |
| F3K | `F3K.9.1` "A group **must** consist of at least 5 competitors" | must | **shall — refuse** (unchanged) | `SeedF3K.cs:38` literal 5 |
| F3B | `F3B.1.8 b)` "there **must** be a minimum of five / three / eight competitors" per task (C: "or all competitors"); `F3B.1.8 c)` annulment rule | must | **shall — refuse** (unchanged) | `SeedF3B.cs:38,97,128` literals 5/3/8 + `MinValidResults` 2 |
| F3F | `F3F.1.7` "with at least ten (10) competitors in one group" — scoped to the **weather-contingency provisional division** ("This division will be used if weather conditions require"), not the scored draw | at-least (contingency scope) | **shall — refuse** (unchanged); record the scope note beside the seed | `SeedF3F.cs:49` literal 10 |
| F5K | **No group minimum stated** (organisation `5.5.2.4` numbers nothing; seed comment "F5K states no group minimum (F12)") | — | nothing to warn on | `SeedF5K.cs:111` `Param("minPerGroup")`, CD-bound |
| F5L | **No group size fixed** (`5.5.12.4` divides into flight groups, no number; fly-off group = preliminary group size is an equality, not a minimum) | — | nothing to warn on | `SeedF5L.cs:54` `Param("groupSize")`, CD-bound |
| NZ-M | "Man-On-Man (Group scored), and that is all §3.12 says" (`docs/rules/nz/class-m-ales200.md:15-17`) — **no size stated** | — | nothing to warn on | `SeedNzMAles200.cs:55` `Param("groupSize")`, CD-bound |
| F5J-NZ NDC variant | `SeedF5jNdc.cs:100` carries `5.5.11.8` "per NZ.0.3 c … governs the DRAW only" | SHOULD (carried) | **SHOULD — warn** (same flag as F5J) | literal 6 |

The seed carries only a number (`GroupConstraint.MinPerGroup`,
`ScoringVocabulary.cs:277-283`) — the modal verb has nowhere to live. Per
the core law the hardness **must be data on the class definition, never a
per-class branch**: WI-1 adds it as an optional datum on `GroupConstraint`
(default = today's hard refusal, so the change is additive-only per NFR-2),
set by the seed author with the rulebook citation beside the number.
Generic inference ("all minima warn") is rejected — it would soften F3K/F3B
`must` minima; seed-comments-only is rejected — the engine cannot read a
comment.

### Warning-carriage design (recommended; alternatives rejected below)

Recommended — **event-carried warning + synchronous advisory, read-model for
free**:

1. **Record:** `PhaseDrawn` gains an optional `Warnings` (audit-only
   `ImmutableArray`, default empty — `CompetitionEvents.cs:89-95`; precedent:
   `PrescribedBy` "audit-only, never branched on", `DrawRejected.Reason`,
   `TaskRoundAnnulled.Reason`). The decide computes one entry per breached
   SHOULD gate (round/group identity, the minimum, the actual size, the
   rulebook ref from the seed). The fold retains them on the phase; the log
   keeps the trace — auditability is the trust model, and the owner framing
   says "prescribes with a **recorded** warning".
2. **Synchronous channel:** `Result<T>` gains an additive advisories list
   (empty on every existing path — `Result.cs:33-71` stays binary-compatible:
   success/failure shapes unchanged). `ToHttpResult`
   (`EndpointRouteBuilderExtensions.cs:49-58`) keeps 200 on success and
   surfaces advisories as a response extension (same `defects`-style
   mechanism). The R5 caller sees the warning without a second round-trip;
   the acceptance harness asserts on it directly.
3. **Read surface:** none new — `GET /competition` already returns the folded
   `Competition` (`GetCompetition.cs:21`), so retained phase warnings ride
   the existing query (the `PairwiseCoOccurrence` precedent: derive/surface
   on read, no sibling endpoint; verbs-only routing untouched).

Alternatives rejected: lowering the seed minimum (the witness story's
explicit anti-goal — tunes the seed to GS); per-class branching in the
decide (violates the core law — the test "adding/changing a class must not
touch code outside that class's definition" fails); warning-without-record
(transient-only advisory violates the trust model — nothing in the log);
new endpoint/query for warnings (routing surface grows for what
`GET /competition` already carries); reusing `Defects` on success
(`Defects` is documented "Failure only", `Result.cs:56` — overloading it
lies about the outcome).

### WI-1 — Hardness datum in Domain vocabulary

Scope: `GroupConstraint` gains the optional hardness datum (e.g.
`MinEnforcement: Shall | Should`, default `Shall` = today's behaviour);
`ParameterResolver` carries it through resolution alongside `MinPerGroup`
(`ScoringResultTypes.cs:325-326` shape); `ClassDefinitionValidation.cs:178`
checks the new slot; JSON serialisation round-trips absent (old payloads) →
default-hard. No decide change here.
Done-when: absent datum behaves exactly as today (all existing Domain tests
green unchanged); present datum resolves and validates; `NumberOrParam`
`Ref` minima (F5K/F5L/NZ-M) carry hardness identically — the CD's bound
number inherits the class's hardness.
Testing: Domain unit (validation + resolution, literal + param), ArchUnitNET
clean (Domain still references BCL only).

### WI-2 — Decide recalibration + `PhaseDrawn` warning carriage

Scope: `ResolveSchedule` G4 and `PrescribeDraw` G6 consult resolved
hardness — SHOULD breach: proceed, collecting one warning entry per
breach (round/group, minimum, actual, rulebook ref); shall breach: the
byte-identical current refusals. G1/G2/G3/G5 refusals untouched on both
paths. `PhaseDrawn.Warnings` added (optional, default empty); fold retains
on phase; `Result<T>` advisories carry the same entries synchronously;
`ToHttpResult` surfaces them on the 200. DrawPhase on a SHOULD class with
field < min generates the single-group draw with a warning (the generator
itself is untouched — it already emits that shape; only the G4 refusal is
lifted for SHOULD).
Done-when: R5's 5/6/7 prescription against canonical F5J succeeds with one
warning naming R5/G1 (5 vs 6, `5.5.11.8.1 a)`); every shall-class refusal
(F3K 4-group, F3B task minima, singleton floor) is byte-identical.
Testing: Domain decide tests (`PrescribeDrawDecideTests`,
`PhaseDrawnDecideTests` + handler tests), event JSON round-trip
(`CompetitionEventJsonTests` — old payloads without the field still read),
**CsCheck property** with the named invariant: *"every SHOULD breach in
`[2, min)` prescribes + warns naming the group; every shall breach refuses;
no group `< 2` ever prescribes"* — generated over corpus definitions ×
small fields/partitions (extends `PrescribeDrawPropertyTests`' mutation
table: `SplitOffSingleton → groupTooSmall`, below-minimum donor →
`groupBelowClassMinimum`-or-warning by hardness). DrawPhase property:
SHOULD + field < min → success + warning; shall + field < min →
`fieldTooSmall`.

### WI-3 — Seed hardness authoring (rulebook-faithful numbers untouched)

Scope: mark SHOULD on `SeedF3J` (`F3J.6.1 a)` citation), `SeedF5J` and
`SeedF5jNdc` (`5.5.11.8.1 a)` + `5.5.11.14.1` citations); F3K/F3B/F3F stay
default-hard with their existing citations; F5K/F5L/NZ-M untouched (no
minimum to harden). Regenerate (`dotnet run --project
tools/Soarscore.SeedData`); emitted JSON diff is exactly the hardness
datum, no number moves; corpus counts unchanged.
Done-when: seed-arithmetic/ingestion suites green; `30-f5j.json` diff shows
only the added datum; re-verified: no `MinPerGroup` number changed in any
seed (anti-goal guard).
Testing: `SeedCorpusIngestionTests`, seed-arithmetic suites; example test
that F5J's 6 carries SHOULD and F3K's 5 carries shall (pins the
classification table in code).

### WI-4 — Acceptance: prescribe-with-warning BDD + API surfacing

Scope: BDD scenario (Gherkin, `Drawing…`/`Prescribing…` feature area):
prescribe a below-SHOULD-minimum draw (the R5 5/6/7 shape against canonical
F5J) → 200 with the advisory + `GET /competition` shows the retained phase
warning; triage: shall-class below-minimum prescription still 400s with the
stable code. Handler-level: advisory extension present on success,
absent (empty) on all pre-existing success paths. The witness story owns
its own WI-3 re-run — this story asserts the mechanism, not the
christchurch ledger.
Done-when: new scenario green on sqlite; full acceptance suite otherwise
unchanged (proves inertness for existing pairs).
Testing: Acceptance BDD + handler tests; no property work here (WI-2 owns
the invariant).

### WI-5 — Full verification + board reconciliation

Scope: Domain + Application + Architecture green; Infrastructure
non-Storage + Acceptance on **both stores** (`SOARSCORE_TEST_STORE=sqlite`
then postgres via Testcontainers — a backend Soarscore claims to support
passes unchanged); build 0 warnings; `graphify update .`. Reconcile
`tech-debt.md` / `deferred-decisions.md` (draw §Draw "How SHOULD-level
breaches surface" bullet: mechanism now landed — update, don't delete
history); leave the witness story parked (its own move back to
`in-progress/` is that story's business on landing this one).
Done-when: both-store green, board reconciled, no lane move here.
