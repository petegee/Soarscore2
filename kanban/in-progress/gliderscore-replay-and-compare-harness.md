# Story — GliderScore replay-and-compare harness

**Status:** In progress · **Raised:** 2026-08-25 · **Fleshed out:** 2026-08-26 ·
**Implementation started:** 2026-08-26

## What

The golden-path test itself. For each active fixture in
`tests/GliderscoreFixtures/index.md` (five today):

1. **Replay** it into Soarscore through the public command surface only — publish the
   fixture-authored class definition, create competition, register competitors,
   prescribe the realised draw (`/prescribe-draw`), accept, open entries/flights,
   capture measurements and record penalties from the raw `Scores` half, complete
   task rounds, finalise.
2. **Score** via `/competition-result` (+ `/task-round-result` per round).
3. **Compare** against the fixture's persisted oracle at three grains:
   raw flight score · per-round normalised score · final ranking — exact, no
   tolerance, at GS's decimal setting (comparator strategy:
   `kanban/completed/resolve-gliderscore-scoring-arithmetic.md`, Handoff §2).
4. **Report** a diff table (per pilot × round × group) on any mismatch and triage
   every difference: *importer/authoring bug* · *our engine defect* · *intentional
   divergence* (GS breaks the rules; we keep ours and record it in the fixture's
   divergence ledger).

Placement decided below (**D1**): features inside `Soarscore.Acceptance.Tests`,
reusing its `Support/`; the fixture corpus stays where it is.

## Why it matters

This is the gap-hunting engine: real completed competitions become regression tests,
and every new export probes the model where the prior art actually varied.

## Before starting — all discharged

- ~~arithmetic resolved~~ — `resolve-gliderscore-scoring-arithmetic.md` (completed
  2026-08-25); its Handoff notes are this story's source of truth for what the numbers
  mean.
- ~~first fixture committed~~ — `ales-sample-comp` plus four more;
  `grow-gliderscore-fixture-corpus.md` completed 2026-08-26.
- ~~prescribed draw available~~ — `prescribed-draw-import.md` completed 2026-08-26.

Scope guard v1: single-class, no-team, no-series, no-merged/prelim comps. The
team/series flags themselves are satisfied by sound `triageJustification` in every
active fixture; merged/prelim stays an unconditional skip. Fixtures whose *scoring*
needs concepts the model lacks stay skip-listed.

**Settled before code starts** (asked 2026-08-26, Pete answered same day):

- **Q1 — grain-1 exposure — DECIDED: in-process service calls.** Grain 1 compares
  GS's persisted `RawScore` (unnormalised) against our engine's pre-normalisation
  score. No HTTP view exposes that today — `/task-round-result` returns only the
  post-normalisation value (`CompetitorTaskResultView.RawScore`,
  `ScoreTaskRound.cs:30`). Approved: the harness calls the public static granular
  pipeline in-process (`ScoringService.InterpretFlight` → `SelectFlights` → raw
  `TaskResult.RawScore`) using the `Support/AcceptanceFixture` direct provider —
  zero production change. The alternative (additive `PreNormalisationScore` on
  `GroupResult`/`CompetitorTaskResultView`) was declined; if HTTP exposure is later
  wanted it becomes a new backlog stub, not a silent addition here.
- **Q2 — jerilderie fallback — PRE-AUTHORISED.** `jerilderie-2010` carries the
  corpus's only re-flight row, which cannot go through base-draw prescription (its
  pilot appears twice in R13; `prescribeDraw.competitorRepeated` refuses). WI-6
  designs the reflight-group mapping; if it cannot reproduce GS's normalisation basis
  faithfully, the fixture is skip-listed for the harness and the reasoning recorded in
  `deferred-decisions.md`. Approved 2026-08-26: the implementing agent may take that
  fallback without stopping to ask.

---

# Plan

## Pipeline shape (one feature per fixture, shared machinery)

```
index.md ──> FixtureLoader (parses active slugs; rule-5 tokenisation)
             └─> per fixture:
                  competition.json ──> ClassDefinitionAuthor ──> POST /publish-class-definition
                  entries.json     ──> register persons/competitors (PilotNo → CompetitorId map)
                  scores-raw.json  ──> DrawDeriver ──> POST /prescribe-draw ──> /accept-draw
                                   └─> CaptureDriver: /open-entry, /open-flight,
                                       /capture-measurement, /record-entry-penalty,
                                       /record-competition-penalty
                                       /complete-task-round … /finalise-competition
                  GET /task-round-result, /competition-result
                  comparator ⇄ expected-scores.json, expected-result.json, divergences.json
```

All writes go over HTTP via `ApiClient.PostCommandAsync` (same JSON options as the
Api — `Support/ApiClient.cs`). Reads likewise. The only non-HTTP surface is Q1's
in-process grain-1 call.

## Decisions settled during planning (2026-08-26)

1. **D1 — Placement: features inside `Soarscore.Acceptance.Tests`.** One Reqnroll
   feature + steps per concern (`ReplayingAGliderscoreFixture`), sharing
   `AcceptanceFixture` (one store per run, `SOARSCORE_TEST_STORE`) verbatim — the
   sibling-project option buys nothing but a duplicated bootstrap, because the
   harness needs exactly what the acceptance suite already has: WebApplicationFactory
   + Testcontainers + the direct provider. Run scoping via a `@gliderscore` feature
   tag so a fast loop can filter. Fixtures stay in `tests/GliderscoreFixtures/`
   (moving them breaks `validate.py --index` paths and every provenance.json's
   repo-relative citation for zero gain); the harness resolves the directory from the
   assembly location (`…/Soarscore.Acceptance.Tests/../../../../GliderscoreFixtures`).
   This retires the parent story's "fixtures move by `git mv` when that decision
   lands" — they don't move.
2. **D2 — Every fixture gets its own authored class definition; corpus definitions
   are never reused.** The corpus F3J is FAI-shaped (total-raw normalisation, FAI
   landing table, −30 overfly conditional) while GS fixtures embed GS arithmetic
   (per-fixture landing schemes, symmetric over-target decay, option-2 time-basis
   normalisation). The whole point is that the class model expresses a foreign
   rulebook as data — the core-system law paying off. Each authored definition is a
   committed, reviewed artefact `<slug>/class-definition.json` (generated once by the
   implementer, checked into the fixture dir beside its inputs; the harness posts it
   verbatim). Nothing in `src/` learns the word GliderScore.
3. **D3 — Config→definition mapping rules** (from `competition.json`; formulas cited
   to the arithmetic story):
   - **Normalisation basis.** `GroupScoreOption=2` (time basis, idx 0 — the corpus's
     only witness is ales-sample-comp): `Score` carries the time term only,
     `ScoreNormalised` carries the landing lookup — GS/NZ-M add landing after
     normalisation (arithmetic story D1; winner >1000 is *expected* and matches
     `expected-scores.json`). `GroupScoreOption=1` (points — jerilderie, and f3j /
     flyoff per their curation notes): landing goes in `Score`, normalisation scales
     the total. Verify each fixture's actual option from its `competition.json` —
     do not assume from class name.
   - **Duration curve.** GS's symmetric decay `TS -= 2(TS−Target)` before ×PPS
     (`Scoring_MOD.vb:651-658`) is exactly a `PiecewiseTerm`: band `[0,target]` rate
     `DurPointsPerSecond`, band `[target,∞]` rate `-DurPointsPerSecond` — cumulative
     bands give `r·(2·target − T)` for T > target, identical to GS at any PPS. Never
     use a plain capped `RateTerm` (that is FAI cap-without-deduction, wrong beyond
     target).
   - **Landing.** `LndgPoints` rows → `LookupTerm` ascending `upTo` rows. GS looks up
     exact-match with silent 0 on miss, but validator rule 2 guarantees every
     non-zero fixture `Landing` is on-table, where banded and exact agree. The
     +half-grid nudge before rounding affects off-grid values only — unreachable in a
     validated fixture. (If rule 2 ever fires, the fixture is broken; do not soften
     the lookup.)
   - **Rounding grid.** `GroupScoreDecimals`/`RoundOrTruncate` → `normalise.round`
     `{mode: HalfUp|Truncate, precision: 10^-Decs}`. GS half-up is `Int(x+half)`
     binary-fp; our decimal HalfUp agrees on-grid, which is all the oracle contains.
   - **Drops.** `PhaseAggregator` applies the FIRST matching policy only
     (`PhaseAggregator.cs:133-157`), while GS stages accumulate (jerilderie: Drop1@6
     AND Drop2@12 → every pilot loses two cells). Authoring rule: collapse the
     fixture's final activated state into ONE `DropPolicy(dropCount = thresholds
     crossed, applyWhenRoundsCompletedAtLeast = lowest crossed threshold)`.
     Correct for a finished comp (final totals are what we compare; per-round cells —
     grain 2 — involve no drops). Cumulative staged-drop policies in the engine would
     be a separate engine story; record if ever wanted (see WI-8).
   - **Groups.** `group.minPerGroup = 2` always — GS decides grouping; the floor
     exists only so prescription validates (prescribe-story D2 anticipated exactly
     this). Single task per phase, `FixedSequence`, `tasksPerRound: 1`; F3K's
     per-round task schedule prescribes a named task per round instead
     (`PrescribeDraw` catalogue form, exercised by `PrescribingADraw.feature`).
   - **Penalty definitions.** Two GS columns, two scopes:
     `Scores.FlightScoreDeduction` (late-landing, enters raw pre-normalisation —
     f3j-international's eleven −30 rows) → entry-scoped penalty definition
     (`DeductPoints 30`), replayed via `/record-entry-penalty`;
     `Scores.Penalty` (post-sum per-pilot — jerilderie's −100, f3k's 100s) →
     competition-scoped definition, replayed via `/record-competition-penalty` with
     the competitor as subject. `PenaltyEngine.ApplyRawPenalties` runs before
     normalisation and `ApplyAggregatePenalties` after aggregation
     (`ScoringService.cs:111,357`) — same placement as GS.
   - **Timing decimals, negation markers, percent:** UI-only, never modelled
     (Handoff §4).
4. **D4 — Cell universe: every `scores-raw` row gets an Entry.** Open an entry for
   every (round, group, pilot) slot present in `scores-raw.json` — including
   all-zero placeholder rows. An opened entry with no captures yields `NoResult` →
   cell 0, which reproduces GS's placeholder-zero cells sitting in the drop-candidate
   pool (decisive for f3k-sample-comp, whose dropped cell IS a placeholder zero;
   without the entries our drop would land on a real worst score and totals would
   diverge). Absent-from-aggregate behaviour never comes into play because the draw
   prescribes exactly these slots. Capture rule: decode and capture only non-zero
   inputs — packed-mmss times (`500.0` = 300 s, Handoff §3; `Fix`-truncation decode),
   `Landing > 0`, task-specific columns per the slot map (below). Everything else
   stays uncaptured. `Updated` is inert evidence for us (it gates GS rescoring, not
   scoring truth).
5. **D5 — Draw derivation from `scores-raw`.** Rounds ascending `RoundNo`, groups
   ascending `GroupNo`, members in `SeqNo` order (prescribe-story decision 4 —
   preserved as list order). Filters, in order:
   1. drop re-flight rows (`ReFlightNo > 0 || OriginalRoundNo != RoundNo`) — v1 has
      no prescription path for them (deferred-decisions.md, Draw);
   2. deduplicate a pilot appearing more than once in a round (GS phantom groups —
      f3j-international R1 group 5): keep the row with the highest
      `expected-scores` NormalisedScore for that (pilot, OriginalRoundNo), drop the
      rest — mirrors GS's best-per-original-round aggregation, which is what makes
      the phantom neutral;
   3. assert the survivor set partitions cleanly (each remaining pilot once per
      round) — a violation is a derivation bug, fail loudly.
6. **D6 — Comparator: exact, ledgered divergences.** Compare decimals exactly after
   mapping both sides onto GS's grid (our outputs are decimal; oracle values are
   binary32-widened doubles that repr clean at their decimal count — parse the JSON
   literal as decimal, compare `==`). No tolerance anywhere (Handoff §2: a tolerance
   big enough to absorb float32 noise masks exactly the bugs this harness exists to
   catch). Grains:
   - *raw* — GS `RawScore` vs our pre-normalisation score (Q1 mechanism), keyed
     `(round, group, pilot)`;
   - *normalised* — GS `NormalisedScore` vs `/task-round-result` per-group results,
     keyed `(round, group, pilot)`;
   - *ranking* — `expected-result.json` rank strings vs `/competition-result`
     placings: our placing `n` matches `"n"` and `"=n"` alike (our RankingEngine
     shares the numeric place among Score-ties, which is what `"=n"` records);
     `"=n"` groups must contain exactly the pilots we place at `n`.
   Mismatch output: one diff table (pilot × round × grain, ours / expected /
   delta) in the assertion message. A per-fixture `divergences.json` (committed test
   data, schema: grain, round, group, pilotNo-or-`*`, reason citing the arithmetic
   story's divergence IDs D1/D3/D5/D6) lists accepted differences; the comparator
   subtracts ledgered entries and fails only on the remainder. Ledger starts empty;
   an entry lands only after human triage.
7. **D7 — Fixture execution order** = increasing mechanical difficulty, each WI
   widening the mapping table: ales-sample-comp → f3j-international-flyoff →
   f3j-international → f3k-sample-comp → jerilderie-2010 (design-gated, Q2).

## Known traps (pre-answered by planning — verified against the tree)

1. **Absent ≠ zero is already handled by D4**, but know why:
   `PhaseAggregator.Aggregate` treats a missing score as 0 *only within rounds it
   sees* (`PhaseAggregator.cs:83-92`), and `ScoringService` omits task-rounds with no
   entries entirely (`ScoringService.cs:171`). Opening entries for placeholder slots
   is what puts GS's zero cells into the drop pool.
2. **Drop tie-break differs, harmlessly.** GS drops the latest equally-bad cell; our
   stable `OrderBy` drops the earliest (`PhaseAggregator.cs:198`). Equal totals ⇒
   equal contribution removed ⇒ aggregates identical; the dropped-*set* identity is
   not one of the three grains. Do not "fix" this.
3. **Ranking secondary key risk (surfaced, not pre-resolved).** GS's ladder orders
   Score DESC then RawScore DESC; `RankingEngine.Rank` sorts by Score only
   (`RankingEngine.cs:47`). Where two pilots tie on Score with different raw sums and
   GS displays distinct ranks, we will show a tie and the ranking grain will flag it.
   That is the harness working as intended: triage, then either a small engine change
   (RawScore secondary key) as its own story or a ledgered divergence. Do not change
   the engine inside this story.
4. **F3K window reduction (arithmetic-story D5).** GS shortens the working window
   1 s per flown flight and treats landing/deduction slots as flights; not
   expressible in our timing model. Expect ledgered divergences on f3k-sample-comp
   wherever a window violation bites; everything else should match. Same for GS
   counting all seven slots in sum-tasks.
5. **Two timekeepers** (`durNumberOfTimekeepers=2`): absent from the corpus ("Still
   open", index.md). Loader refuses such a fixture with a clear skip message rather
   than mis-modelling the average.
6. **F3K column packing.** F3K slots decode as `ScrArr(0..6)` =
   `Laps, Time1Mins, Time1Secs, Time2Mins, Time2Secs, Landing,
   FlightScoreDeduction` — six potential *flight-time* columns plus landing plus
   deduction (arithmetic story, F3K section). The capture map is per-GS-task-code
   (e.g. round-2 task A(1): result lives in `Laps`). Derive each fixture's map from
   its `scheduleTables` task list and prove it against `expected-scores.json` before
   trusting replays. Duration-family fixtures ignore `Laps`.
7. **Zero-max guard equivalence.** All-NoResult group: our `winnerRaw == 0` branch
   zeroes everyone (`NormalisationEngine.cs:91-95`); GS's zero-max guard writes 0.
   Same. No action.
8. **Double-round identity.** Our normaliser rounds again after adding
   `ScoreNormalised` terms (`NormalisationEngine.cs:147-149`); GS leaves the sum
   unre-rounded. Invisible while landing points are integers (every active fixture) —
   noted so a future fractional-points scheme trips the right wire.
9. **Registration mapping.** Register persons/competitors in `entries.json`
   `compPilots` row order; the returned ids build the `PilotNo → CompetitorId` map.
   Person emails slug-unique per run (existing acceptance pattern). GS `StartNo`
   numbering is irrelevant — keys are internal ids.

## Work items

Each WI lands compiling with the suites named in its checkpoint green. WIs 1–6 are
strictly sequential (one deep compile unit; parallelism buys nothing). WI-7–8 close
out.

### WI-0 — Board

Take into `in-progress/` (`git mv`, status header same commit).

### WI-1 — Harness steel thread: ales-sample-comp green

New `Features/ReplayingAGliderscoreFixture.feature` (+ tag) and
`Steps/ReplaySteps.cs`, plus `Support/Gliderscore/` holding:

- `FixtureLoader` — locates `tests/GliderscoreFixtures/`, parses `index.md` active
  slugs (tokenisation per its header contract), loads/deserialises the six fixture
  files + `class-definition.json` (ClassDefinition via
  `ClassDefinitionIngestion.Options`).
- `ClassDefinitionAuthor` — for WI-1, produces `ales-sample-comp/class-definition.json`
  by hand-following D3 (write the JSON, review it, commit it; a generator is *not*
  built this WI — five fixtures do not justify one, and hand-authored definitions are
  reviewable). Definition sketch: one Preliminary phase, `FixedSequence` 1 task,
  3 rounds validity `minRounds 1`, no drops, `PiecewiseTerm` duration curve
  (target 600, PPS 1), landing `LookupTerm` from scheme 6, `GroupScoreOption=2`
  arrangement (time in `Score`, landing in `ScoreNormalised`), norm round
  `HalfUp 1`, `minPerGroup 2`.
- `ReplayDriver` — publish → create → register (D-trap 9) → derive draw (D5) →
  prescribe → accept → per-slot open entry → open flight → capture (D4) → complete
  each task-round → finalise. Returns the ids/maps the comparator needs.
- `Comparator` — D6, all three grains (grain 1 via the Q1 in-process call),
  empty-ledger pass/fail, diff-table message.

Scenario: replay `ales-sample-comp`, expect all three grains exact, ledger empty.

**Checkpoint:** `dotnet build Soarscore.sln`;
`SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests` green
(includes existing features); postgres leg wherever Docker exists. First green here
is the story's headline milestone.

### WI-2 — `f3j-international-flyoff`

Widens: nothing structurally new (single group, no drops, integral) — proves the
steel thread wasn't shaped to one fixture and exercises a `gs-report-transcript`
ranking oracle (transcript CSV present). Author its `class-definition.json`
(DurGeneral, option per its `competition.json`).

**Checkpoint:** both fixtures green, sqlite + postgres.

### WI-3 — `f3j-international`

Widens: multi-group normalisation (16 × 4), Drop1@8 collapsed policy (D3), Decs=1
grid, `FlightScoreDeduction` → entry-scoped penalty mapping, phantom-group dedupe in
D5 (step 2 fires for real), 30-person scale.

**Checkpoint:** three fixtures green, both stores.

### WI-4 — `f3k-sample-comp`

Widens: F3K family authoring (per-round task catalogue — prescribe names tasks G,
A(1), F, D, C(3), X×4), slot-column capture map incl. `Laps`-as-time (trap 6),
placeholder `NoTaskSet` rounds 6–9 as NoResult entries (D4 decisive case), option-0
drop-count nuance — verify the collapsed single-drop authoring reproduces the
transcript (it must: the drop removes a 0 cell either way), expected D5-window
divergences triaged into `divergences.json`.

**Checkpoint:** four fixtures green, both stores. Any divergence ledgered here cites
D5 and states the affected cells.

### WI-5 — Comparator hardening

Property-flavoured self-checks for the harness itself (example-based; no CsCheck —
nothing here has a genuine input-class invariant that example coverage misses, and
CLAUDE.md's PBT clause is aimed at algorithms/invariants, not file-driven replays):

- replay determinism: running a fixture twice in one run produces identical command
  counts (guards accidental cross-run bleed through the shared store);
- conservation: Σ kept per-round cells − ledgered aggregate divergences == Σ final
  scores (catches a silently dropped replay slot — the failure mode that would
  otherwise masquerade as a scoring diff);
- ledger strictness: a seeded fake mismatch fails when unledgered and passes when
  ledgered (negative self-test, task-round-lifecycle WI-10 discipline).

**Checkpoint:** suite green including self-tests, both stores.

### WI-6 — `jerilderie-2010` (design-gated by Q2)

63 pilots × 14 rounds × 5 groups, two-drop collapse, −100 aggregate penalty, and the
re-flight row. Design the reflight mapping first, in this story file, before code:
which task-round hosts the appended group, how the GS basis (normalise within
R13/G1, aggregate keyed to orig-12) maps onto `AppendReflightGroup` +
`ReflightSelector` semantics, and whether the shape guard accepts the resulting
roles. If the mapping is faithful → implement and go green (largest fixture; watch
runtime). If not → mark the fixture skip-listed-for-harness in `index.md` wording
(status token unchanged — `validate.py` must still pass), record the finding in
`deferred-decisions.md`, and close this WI with the other four fixtures as the
delivered corpus — the fallback is pre-authorised (Q2); no need to stop and ask.

**Checkpoint:** five green or documented fallback; both stores.

### WI-7 — Runbook

`tests/GliderscoreFixtures/extract/README.md` (or a sibling harness README there —
fixtures dir is the neutral home, and it is not `/docs`) gains: how the harness
consumes the corpus, the `@gliderscore` filter tag, the two-store run commands, and
the "add a fixture → author definition → replay → triage → ledger" loop for future
exports. Board-facing only; no `/docs` edits without approval.

### WI-8 — Board reconciliation

`git mv` to `completed/`, status header same commit. Reconcile inventories:

- `tech-debt.md`: add the cumulative-staged-drop-policy limitation (engine applies
  first-match-only; fixtures are authored around it) if jerilderie or a later export
  makes the workaround insufficient — otherwise note nothing;
- `deferred-decisions.md`: discharge or sharpen the reflight-replay scoping note per
  WI-6's outcome; record the ranking-secondary-key finding if it fired (trap 3);
- any newly identified feature → `kanban/backlog/` stub (house rule 6), e.g. a
  staged-drop engine story, or the Q1-declined additive pre-normalisation view field
  if HTTP exposure of the raw grain is later wanted.

## Execution plan

Sequential, one implementer, checkpoints at every WI boundary (each ends compiling,
suite green, safe to park):

1. WI-0 → WI-1 (steel thread; Q1/Q2 already settled — no user input outstanding).
2. WI-2 → WI-3 → WI-4 (corpus widening; each fixture is one session-sized unit).
3. WI-5 (hardening).
4. WI-6 (design-gated; may end in the documented fallback).
5. WI-7 → WI-8 (close-out).

**Full-suite finish line:** `dotnet test Soarscore.sln`, then
`tests/Soarscore.Acceptance.Tests` under both `SOARSCORE_TEST_STORE` values. Known
flake: solution-wide Marten migration race (`tech-debt.md` last item) — re-run the
project alone before diagnosing.

**Story invariant for sign-off:** every active fixture replays through public
commands only and compares exact at all three grains, modulo ledgered divergences
that each cite an arithmetic-story divergence ID; no `src/` file mentions
GliderScore; no glossary/docs change without an approval on record; both stores pass.

## Out of scope (deferrals restated)

- Fly-off mechanics (promotion into a second phase) — no active fixture exercises a
  real fly-off; `f3j-international-flyoff` is fly-off-*shaped*, standalone.
- Reflight-row draw prescription (base-draw path) — standing deferral; WI-6 covers
  only the score-side replay.
- Team scoring, series, merged/prelim comps — concept gaps; skip-listed.
- Speed/Distance/F5B/F5K/F5J families — no active fixture; the mapping table will
  grow with the corpus (`grow-gliderscore-fixture-corpus.md`).
- Two-timekeeper averaging, float32-artifact witnessing — corpus lacks witnesses
  (index.md *Still open*).
- Engine changes: staged-drop accumulation, ranking secondary key — surfaced here,
  delivered (if at all) by their own stories.
