# Story — F5J Christchurch parallel-run witness (the guaranteed divergence)

**Status:** Backlog — plan written 2026-09-07 (fixture
data measured, GS arithmetic and engine mechanics checked during planning;
decisions 1–4 owner-confirmed same day; landing route superseded by the
2026-09-08 owner decision; implementation awaits the generic prerequisite in
WI-0) · **Raised:** 2026-09-06 (decision 2 of
`kanban/completed/seed-definition-parallel-run.md`: the guaranteed-divergence
witness role moved here from the withdrawn f3j-international claim)

## What

Run `f5j-christchurch-2019` under **canonical `30-f5j.json`**, with its landing
definition unchanged, through the parallel-run harness and ledger the
differences against the triaged set. First, `kanban/backlog/tape-points-landing-seeds.md`
WI-0 through WI-3 must deliver generic unit-aware capture and amendment. This
story then owns only the canonical 75 m seed fix and the harness/ledger work.
The fixture adapter decodes GS scheme 11 "F5J Enter Landing" source marks into
actual awarded points and submits `landingDistance` with explicit unit `pts`;
it neither invents metres nor submits the source marks as points.

Two witnessed difference classes are expected:

1. **The drop split — the story's guarantee, at the final-aggregate/ranking
   grain.** The fixture's GS config carries **no drop thresholds** (verified:
   `Drop1AtRound`/`Drop2AtRound` unset, `Drop3–5AtRound` 99,
   `competition.json configProvenance` "no drop-worst can activate within
   rounds 1-11") against the rulebook's drop-from-5 (`docs/rules/f5j.md:88-90`,
   the seed's `applyWhenRoundsCompletedAtLeast: 5`, `30-f5j.json:153-159`) over
   the 11 scored rounds — GS summed everything; the seed drops the lowest round
   score. Every pilot who flew all 11 rounds with a positive lowest cell
   provably aggregates lower under the seed (Σ best-10 < Σ 11; 12 of the 16
   flying pilots qualify); the placings split is measured, not predicted.
2. **The normalisation grid.** GS normalises `1000·Raw/max` **HalfUp to 1 dp**
   (effective `GroupScoreDecimals` 1, arithmetic-proven,
   `competition.json configProvenance.knobs`) while the seed leaves the
   rulebook's values exact — the same rounding candidate the f3j re-triage
   carries, and here it materialises: GS's normalised cells span all ten
   tenths (verified over the 162 scored cells). It cascades into the ranking
   grain through the aggregate.

The **landing side is expected cell-exact**: after source-scheme decoding,
both sides run identical arithmetic (min(time, 600)·1 + awarded landing points
− two-rate height deduction), with the canonical landing eligibility gates
still applied. Any raw-grain mismatch is an adapter/authoring bug or a kind-3
engine divergence, escalated — never triaged. See **Settled decision 2** for
the 2026-09-08 owner decision superseding all derived tape-variant plans.

Two planning findings reshaped the stub, both verified against the tree:

- **The drop split is conditional on the run's prescription shape** (decision
  1). The fixture carries 18 rounds of rows — R12–18 are wholly-unflown GS
  placeholder rows — and the engine's `PhaseAggregator` treats a round with no
  score as **0** and pools it into the ByRound drop candidates
  (`PhaseAggregator.cs:83-92` "No score recorded — treat as 0";
  `ApplyByRoundDrop` `:191-214`). A full-fixture run lets the drop eat a
  phantom zero and the aggregate split vanishes. The run therefore replays the
  comp GS actually scored: **rounds 1–11** (the rollup window,
  `ladder.py:201` `TaskLastRound = MAX(RoundNo where Updated='True')` = 11).
- **The landing scale needs source decoding, not a different class** (decision
  2, updated 2026-09-08). Every recorded landing value hits the scheme's
  exact-match rows or is 0 (verified); the recorded number cannot honestly be
  read as metres-from-spot (the scheme rewards larger readings). Feeding it
  into `30-f5j` as metres would fabricate an observation the club never made
  (the 24 zero readings would become perfect spot landings, +50 each).
  Scheme-11 marks 55–100 are **not actual points**: the fixture adapter must
  decode the source scheme to awarded points, with 0 decoding to 0, before
  capture. Membership in the canonical points value-set validates the submitted
  award; it does not prove physical landing-band equivalence. Neither physical
  tape geometry nor rescoring against different tables is needed for this run.

The driver already captures F5J launch height (parity
`launchHeight` arm, `ReplayDriver.cs:1066-1078`) — the pair needs the P2
derivation re-expressed under the seed's metric names (decision 5), plus the
source landing adapter using the parent's unit-aware capture contract. No
generic capture plumbing belongs to this story.

## Why it matters

The ales pair — the parallel-run machinery's proof pair — came out near-exact:
the witnessed difference set was three raw-grain entries with identical final
placings. The parallel-run claim has therefore never yet been seen producing a
*visible* split on the day, which is the product's whole point ("what would
have differed"). This pair guarantees one — a drop-policy split moving final
placings, the first time the ledger's ranking grain carries triaged entries at
all. It also exercises canonical F5J with importer-decoded awarded points,
without changing its landing definition, and is the first sighting of
the HalfUp-1dp-vs-exact rounding split the f3j re-triage predicted but could
not confirm. The fly-off/promotion spike the stub carried is answered by
verification, not new machinery (decision 6).

## Settled decisions (2026-09-07, Pete; landing decision superseded 2026-09-08)

1. **Scored-window prescription: rounds 1–11 only.** The per-pair scored-window
   map lives in the driver, applied in parallel-run mode only (the ales pair's
   shape is untouched — regression-pinned by its scenario and ledger
   provenance). The ledger provenance declares the window
   (`scoredWindowRounds: 11`); the comparator scopes its oracle-coverage
   universe to the declared window and verifies the run's prescribed round
   count equals it. The R12–18 oracle cells (persisted 0.0 placeholders) are
   deliberately outside the comparison universe — a declared scope, recorded in
   the ledger, never a silent shrink.
2. **Canonical landing definition; decode the source scheme in the adapter
   (owner decision 2026-09-08).** This supersedes all derived tape-variant
   plans: no `31-f5j-tape-points`, no `SeedF5JTapePoints`, no new
   `landingTapeReading`/`landingPoints` metric, no shared-seed lifting or
   corpus-count growth. Use `30-f5j` unchanged in its landing definition;
   decision 3 remains the only seed delta. The generic prerequisite accepts
   the same metric `landingDistance` with explicit `pts` or declared `m`,
   preserving submitted value/unit in capture and amendment. Points must
   exactly match a value in the applicable `LookupTerm.Rows.Points`; unmatched
   values and missing/unsupported units are rejected, and eligibility gates
   still apply. Only the parent story owns this generic core work. The F5J
   fixture adapter decodes the source landing scheme: 55→5, 60→10, 65→15,
   70→20, 75→25, 80→30, 85→35, 90→40, 91–95→45, 96–100→50, and 0→0.
   Submit the decoded value as `landingDistance`, unit `pts`, never the raw
   mark and never invented metres. Unknown marks or decoded awards absent
   from the canonical table fail loudly, without fallback or double
   conversion; the parity branch stays numerically unchanged. Retain source
   marks and scheme traceability in fixture/ledger provenance; the core event
   honestly records the importer-submitted points. Valid value-set membership
   is not proof of physical band equivalence; tape geometry and rescoring
   different tables are outside this witness, not prerequisites.
3. **Fix the seed's 75 m authoring gap now.** The F5J seed cannot express
   `5.5.11.7 d` ("nose not at rest within 75 m of the designated landing spot"
   → flight = 0, `docs/rules/f5j.md:37`): it declares no within-75 m flag and
   its `flightValidWhen` gates only overfly ≤ 60 s and `startHeightRecorded`
   (`30-f5j.json:240-262`). Add a `landedWithin75m` Flag metric with
   `whenNotRecorded: true` (the exception-recording policy P1 — the club
   records the exception; GS's own `LandingOver75m` column is the same shape)
   plus the `flightValidWhen` gate, to **both** of `SeedF5J`'s tasks, justified
   against `docs/rules/f5j.md` — never against GS. Numerically inert on this
   pair (the only two 75 m-flagged rows are the rule-driven zeroed flights,
   flight-less on both sides) but rulebook-faithful for the later F5J pairs
   (hawkes-bay, south-island).
4. **Ledger shape: class entries + count pins + fully-enumerated ranking
   grain.** The ales discipline ("witnessed entries should name their cells")
   would make this ledger ~300 hand-authored entries; instead: one triaged
   entry per difference class per grain (wildcarded `pilotNo: "*"`, the
   `ParallelRunLedger` wildcard the schema already supports) **with the
   scenario pinning the exact computed counts per grain**, and the **ranking
   grain — the product — fully enumerated per pilot** (18 entries, GS rank vs
   seed-run placing, each cited). The raw grain is pinned at **zero**
   mismatches (the source-decoding and canonical-scoring exactness claim).
5. **`startHeight`/`startHeightRecorded` are derived, not assumed (P2).** GS's
   `FlightScoreDeduction` column carries the start height ("misleadingly
   named", `ladder.py:26-27`; the F5J parity definitions consume it as
   `launchHeight`). The derivation rule: a flown row with a positive payload
   captures `startHeight = payload` and `startHeightRecorded = true`; a flown
   row with a zero payload would capture `startHeightRecorded = false` —
   the rulebook zeroes such a flight (`5.5.11.7 e`, `docs/rules/f5j.md:33`) and
   the seed's `flightValidWhen` gate implements exactly that outcome. Verified:
   **all 162 flown rows carry heights 66–271 m** — the zero-payload case never
   occurs here (a provenance note). The seed declares **no** `whenNotRecorded`
   for `startHeightRecorded` (metric-absence-semantics WI-4's F5J case: an
   unrecorded height is not assumed valid) — the derivation covers every flown
   row, so no flight runs Pending.
6. **The fly-off/promotion spike resolves as dormancy, with existing acceptance
   coverage; this pair still needs its run.** The engine
   scores only **drawn** phases (`ScoringService.cs:213` iterates
   `competition.Phases`; `Finalise` iterates drawn phases —
   `Competition.cs:1964`, cited by tape-points' ground truth); a second phase
   cannot be drawn today (`Competition.DrawPhase` refuses on `!Phases.IsEmpty`,
   `kanban/deferred-decisions.md` §Draw); `RankingEngine.Rank` receives
   `promotion`/`finalRanking` but never reads them (params
   `RankingEngine.cs:62-63`, absent from the body) — the qualifying-only
   ranking is the dormant path. `ClosingACompetitionSteps` uses canonical
   `30-f5j` (`F5JDefinition`) and covers finalisation without drawing a fly-off.
   The run therefore prescribes phase 1's 11 rounds and finalises; no engine
   change; the seed's fly-off phase and its promotion/tie-break machinery stay
   untouched future work (`kanban/deferred-decisions.md` "Phase-scope
   finalisation and PromotionRule"). If the run contradicts any of this, it
   escalates as a finding — never a harness workaround.
7. **`overflySeconds` is P1-assumed and disclosed; the ledger schema widens to
   say so.** The seed declares `whenNotRecorded: 0`
   (`30-f5j.json:210-214`); the engine resolves it; the harness emits nothing.
   The data corroborates: max decoded flight 599 s — nobody overflew the 600 s
   working time, so the assumption is also what the foil's data implies.
   `ParallelRunComparator.CheckProvenance` currently verifies **Flag** metric
   mappings only (`ParallelRunComparator.cs:413-423`) — the number-valued
   assumption needs the widening in WI-2, additive, with the ales ledger
   untouched (its `true` deserialises into the widened type unchanged).
8. **No parameter bindings.** The seed's two parameters (`flyoffMaxGroup` 14,
   `flyoffMinRounds` 3) both carry declared defaults bound at
   `CompetitionSetup` and neither is ever exercised (the fly-off never
   draws) — `DeriveParallelRunBindings`' documented arm: a defaulted parameter
   is the class author's rulebook-faithful choice. The ledger's
   `parameterBindings` is empty with a note saying why.

## Verified ground truth (2026-09-07, planning — re-verify line refs before relying on one)

**Fixture** (`tests/GliderscoreFixtures/f5j-christchurch-2019/`, comp 45):

- 18 pilots (`entries.json`), 11 scored rounds × 3 drawn groups of **6/6/6**
  (the seed's `minPerGroup: 6` is met exactly; drawn groups include every
  registered pilot — `prescribeDraw.competitorMissing` demands it), 324 rows =
  164 flown (`Updated='True'`, all within R1–11) + 160 placeholders (34
  scattered unflown slots inside R1–11 + all of R12–18).
- Flown-row facts (measured over the 162 non-cancelled rows): start heights
  66–271 m, every row carries one; packed-mmss flight times decode to
  ≤ 599 s (nobody overflew); landing values ∈ scheme-11 rows ∪ {0} (24 rows
  recorded 0 — no bonus, GS's exact-match short-circuit `ladder.py:144-145`);
  `Penalty` all 0 (G4 moot — the harness's loud infraction-mapping refusal at
  `ReplayDriver.cs:724-739` never fires); `F5JMotorReStarted` true on 22 rows
  (see Before-starting); `LandingOver75m` true only on the two cancelled rows.
- Two rule-driven cancelled flights — R8/G3 P82, R11/G1 P104 (time 0, height 0,
  `LandingOver75m: true`): GS persisted 0.0 cells; the harness's zero-row rule
  (`Time1Mins <= 0` → flight-less, `ReplayDriver.cs:1034-1037`) makes them
  flight-less in the seed-run too → cell 0 both sides. Equivalent; a provenance
  note.
- Teams: `UseTeams=true`/`UseTeamProtection=true` are app defaults — **all 18
  CompPilots carry `Team='0'`** and `MapGliderscoreTeamsAsync` maps only
  `Team > 0` (`ReplayDriver.cs:819-822`), so the mapping no-ops and the
  parallel comparator's deliberate no-team-grain stance holds (the ales
  precedent).
- No re-flights (`OriginalRoundNo == RoundNo`, `ReFlightNo` 0 everywhere);
  no per-fixture harness maps touch this slug (`RoundParameterBindings`,
  `SyntheticPrescriptionOnlySlots`, `SyntheticFlightLessSlots` — verified
  absent), so the parallel-run round-bind refusal guard never trips.

**GS arithmetic** (all cited to `ladder.py`, cell-exact vs
`expected-scores.json`): raw = min(t, 600)·1.0 + scheme-11 landing (+0.005
nudge, half-up 1 dp — integer points, so exact) − two-rate height deduction
(≤ 200 m: 0.5/m; above: 100 + 3/m — byte-identical to the seed's piecewise
bands); floor ≥ 0 never exercised on a flown row (min flown raw 123.0);
normalisation `RoundNumber(1000·Raw/max, 1)` HalfUp-1dp over the group
including placeholder 0-rows; ladder window R1–11, **no drops** (Score ==
RawScore), re-rounded to 1 dp **before** comparing, sort Score DESC then
RawScore DESC, ties display `=n`.

**Engine** — cited in decisions 1 and 6. One addition: the drop gate counts
completed rounds (`PhaseAggregator.cs:97-99`, `:136-137`) — 11 completed
rounds ≥ 5, the drop fires on real rounds only under the scored-window
prescription.

**Harness as-built** (the pair reuses, never re-shapes):

- `DeriveParallelRunBindings` (`ReplayDriver.cs:314-354`): binds no-default
  parameters and `targetTime` only — the F5J seed's parameters are all
  defaulted, so it binds nothing; `ParallelRunBindings` ends up an empty
  dictionary (not null), which the provenance verification accepts against an
  empty ledger binding block.
- The capture gate reads the **published** definition
  (`ReplayDriver.cs:602-609`): canonical F5J declares `startHeight`/
  `startHeightRecorded`/`landingDistance`; `launchHeight`/
  `lateLandingDeduction` are parity-metric arms. WI-2 adds the definition-gated
  P2 height derivation and source-scheme decoding for this parallel-run pair,
  using the parent's unit-aware capture path, inert for existing pairs.
- Naming (`ReplayDriver.cs:383-390`): the competition carries canonical
  `30-f5j`'s own name/version automatically.
- Comparator (`Comparator.cs`): `RecordCell`/`AddIfDifferent`/
  `EnsureOracleCoverage` (`:1380/:1391/:1407`) are internal and shared;
  `CompareRankingGrain` (`:687`) compares engine placings against the oracle's
  `=n` ranks **and** tie-group membership. Ledger `Covers` supports
  `pilotNo: "*"` and null round/group scope (`ParallelRunLedger.cs:98-104`).

**Seed class** (`30-f5j.json`): drops `applyWhenRoundsCompletedAtLeast: 5`,
dropCount 1, ByRound (`:153-159`); phase-2 fly-off (`TopPercent` 30,
`qualifyingPosition` tie-breaks, `:366-398`) never drawn; both parameters
defaulted (`:6-36`); `startHeightRecorded` has no `whenNotRecorded`
(`:187-190`); `overflySeconds` → 0 and `touchedByCompetitor` → false declared
(`:210-223`); landing lookup conditional on overfly == 0 ∧ !touched, table
≤ 1 m → 50 … > 10 m → 0 (`:312-359`).

**Raw-grain exactness claim** (the adapter/canonical run's testable spine):
both sides compute min(t, 600)·1 + club-landing-points − two-rate height, identical band
arithmetic, no floor exercised, no overfly recorded → every raw cell in the
window must compare exact (162 flown + 34 flight-less 0.0 + the 2 cancelled
0.0). The scenario pins this at zero.

## Cross-story contract — `kanban/backlog/tape-points-landing-seeds.md`
(owner decision 2026-09-08; supersedes the former shared-variant plan)

- **Prerequisite and ownership.** Parent WI-0 through WI-3 owns all generic
  unit-aware capture/amendment, value/unit preservation, exact applicable
  `LookupTerm.Rows.Points` membership validation, missing/unsupported-unit
  rejection and retained eligibility gates. This F5J story starts its code
  work only after that prerequisite, and owns only seed75m + harness/ledger.
- **Same metric, two explicit units.** Consume `landingDistance` with `pts`
  for decoded source awards or the declared `m` for distance observations.
  No new landing metric, class variant, shared-seed lift or corpus entry.
  The parent owns the generic distance-vs-points property invariant; this
  story owns source-scheme adapter examples and the example-driven pair.
- **Source boundary.** Scheme-11 source marks require decoding in the fixture
  adapter; the core sees awarded points, not GS marks or source-scheme logic.
  Keep the original marks and scheme traceable in fixture/ledger provenance.
  Neither story needs a physical-geometry answer for this witness.
- **Path obligation.** Track the parent's actual lane. Once it moves, update
  this story's prerequisite citations to its actual path. Use
  `kanban/completed/tape-points-landing-seeds.md` only once completed, citing
  its verified WI-0 through WI-3 contract. If Jerilderie WI-4 remains parked,
  cite the parent's blocked path instead; WI-4 is not an F5J prerequisite.
  Do not edit a completed parent story; it is history.

## Before starting

- **Check the board first:** has `tape-points-landing-seeds.md` WI-0 through
  WI-3 landed? Read its landed unit-aware capture/amendment and validation
  contract before WI-1; update this story's citations to the moved completed
  path when applicable, without editing the completed story.
- **Motor-restart semantics (`fai-rules` question):** 22 flown rows carry
  `F5JMotorReStarted=true`; GS applies no scoring effect
  (`F5JMotorRestartOption` null, `ladder.py:35`), the seed declares no such
  metric, and the harness maps no such flag — the two sides' **numbers agree
  by construction**. Resolve with the skill what `5.5.11.7 g` ("propeller
  turning after the 30-second motor-run period") does with a restart and
  whether GS's flag is that offence; the answer is a **provenance note**
  either way — a GS-vs-rulebook disagreement is reported, never reconciled,
  and never a harness-emitted zeroing.
- **Rule citations via the `fai-rules` skill:** the overfly rule and the
  touched-by-competitor landing condition in the F5J/F5-general docs carry
  prose refs here (`docs/rules/f5j.md:36-40`); resolve the exact `5.5.11.x`
  numbers with the skill before they go into the ledger's citations.
- **Pairing:** owner-confirmed (parent story decision 2; the mapping table's
  row `tests/GliderscoreFixtures/parallel-run-mapping.md:91`). The row's
  **seed remains `30-f5j.json`**; WI-1 updates its rationale for source-scheme
  decoding and the drop/rounding witness, per the table's update contract.

## Plan

### WI-0 — Generic prerequisite (blocks WI-1)

1. **Verify parent WI-0 through WI-3 has landed.** Consume its unit-aware
   capture/amendment API: preserve value/unit; accept the same `landingDistance`
   metric with explicit `pts` or declared `m`; reject points not exactly in
   applicable `LookupTerm.Rows.Points` and missing/unsupported units; retain
   eligibility gates. Do not implement or duplicate this core work here.
2. **Pin the landed contract and source scheme.** Cite the parent's actual
   path, switching to `kanban/completed/tape-points-landing-seeds.md` when
   moved, without editing that completed story. Re-verify the fixture's
   scheme-11 rows for the adapter examples and provenance. The 2026-09-08
   canonical route and decision-3 seed fix need no reconfirmation; physical
   tape geometry is not a gate.

### WI-1 — Seed work (`tools/Soarscore.SeedData`)

1. **The 75 m fix** (decision 3): add `landedWithin75m` (Flag,
   `whenNotRecorded: true`) to both `SeedF5J` tasks and extend both tasks'
   `flightValidWhen` with the `== true` gate; header comment cites
   `5.5.11.7 d` and this story. Domain test first if a test seam exists for
   `flightValidWhen` gates (the metric-absence-semantics property tests are
   the precedent); otherwise pin via the seed-arithmetic/ingestion suites.
2. **Keep canonical landing unchanged.** No landing metric/table/identity
   changes, new seed, shared-seed lifting or `Corpus.All` count changes.
   Preserve all other canonical parameters, reflight, penalties, phases,
   drops, promotion, timing, group and normalisation data.
3. Regenerate: `dotnet run --project tools/Soarscore.SeedData` — all integrity
   checks pass; `git diff tools/Soarscore.SeedData/json/30-f5j.json` shows
   **exactly** the 75 m delta (metric + gate, both tasks) and nothing else.
4. **Update the mapping-table row** (`parallel-run-mapping.md:91`): seed stays
   `30-f5j.json`, `why` → the drop split + rounding grid, source scheme decoded
   to awarded `pts` on canonical `landingDistance` (cite this story); status
   stays `stubbed` until WI-5.

**Done-when:** tool green; `SeedCorpusIngestionTests` green; 30-f5j's diff is
exactly the 75 m delta; corpus count unchanged; the row rationale updated.

### WI-2 — Harness widening (`tests/Soarscore.Acceptance.Tests`)

All cited to this story; the parity path and the ales pair are inert proofs
(the full suite stays green unchanged except the new scenario).

1. **Scored-window map** — a per-pair map keyed by fixture slug, parallel-run
   mode only: `["f5j-christchurch-2019"] = 11`, applied to `keptRows` before
   prescription/entry-opening, with a loud assertion that the fixture's
   `MAX(RoundNo where Updated=='True')` equals the declared window. A fixture
   in parallel-run mode with no map entry behaves exactly as today (ales
   untouched).
2. **P2 height arm** in `CaptureDurationInputs`, beside the `launchHeight` arm:
   `declared.Contains("startHeight")` → capture `startHeight`
   (`row.FlightScoreDeduction`) and `startHeightRecorded` (Flag true) for every
   flown row with a positive payload; a flown row with a zero payload captures
   `startHeightRecorded` = false (decision 5's rule — never exercised here).
3. **Source landing adapter, parallel-run only:** for this fixture and declared
   `landingDistance`, decode `row.Landing` using the fixture's source scheme 11
   into actual awarded points, including 0→0, then capture `landingDistance`
   with unit `pts` through the parent's API. Exact source-row matching only:
   55→5, 60→10, 65→15, 70→20, 75→25, 80→30, 85→35, 90→40, 91–95→45,
   96–100→50. Unknown marks and decoded awards absent from the applicable
   canonical `LookupTerm.Rows.Points` fail loudly; no interpolation, fallback,
   fabricated metres or second conversion. Do not send marks as `pts`.
   Preserve fixture mark/scheme traceability in ledger provenance; core events
   retain the submitted points value/unit. Keep parity numerically unchanged.
4. **Ledger schema widening** (`ParallelRunLedger.cs`):
   `ParallelRunMetricMapping.Resolved`: `bool` → `JsonElement` — Flag (bool)
   and Number (decimal) `whenNotRecorded` values both expressible; the ales
   ledger deserialises unchanged. `ParallelRunProvenance` gains
   `ScoredWindowRounds` (`int?`, null-tolerant — the ales ledger carries none)
   and `DerivedMetrics` (`IReadOnlyList<ParallelRunDerivedMetric>?` — `Metric`,
   `Source`, `Derivation`, `Justification`; null-tolerant empty).
5. **`CheckProvenance` widening** (`ParallelRunComparator.cs`): verify
   number-valued metric mappings against the adopted seed's `whenNotRecorded`
   number; verify each derived metric is declared by the adopted seed (name
   match, exactly once across phases); verify `ScoredWindowRounds` (when set)
   equals the outcome's prescribed round count.
6. **Adapter example tests:** cover every scheme-11 row (each mark 91–100
   individually), 0, unknown marks, and a decoded award absent from the
   canonical table. Assert submitted metric/value/unit and retained source
   provenance; pin no double conversion and unchanged parity numbers.
   Exercise the parent's rejection of missing/unsupported units and unmatched
   points through capture and amendment, preservation of value/unit, and
   retained landing eligibility (overfly/touched) and flight-validity gates.
   These are integration examples, not duplicate generic core implementation.

**Done-when:** the full acceptance suite is green unchanged (the new scenario
lands in WI-3) — proving every widening inert for existing pairs on both
grains the ales scenario exercises.

### WI-3 — Ledger, scenario, measured-first run

1. **Author the ledger pre-argued spine**
   `tests/GliderscoreFixtures/f5j-christchurch-2019/parallel-run/30-f5j.json`.
   `pair` is (`f5j-christchurch-2019`, `30-f5j`); `seedClass` records canonical
   Name/Version. Provenance and triage contents are specified below.
2. **Add the scenario** to `Features/ParallelRunningAGliderscoreFixture.feature`
   and the witness steps to `Steps/ParallelRunSteps.cs` (scenario below).
   The existing When/verdict/citation steps are reused verbatim; the ales
   scenario and its `final placings match` step are untouched. Two new Then
   steps: **`the raw grain is exact…`** — computed raw-grain mismatches empty
   (the adapter/canonical exactness claim; failure = authoring bug or kind-3,
   escalate, never a ledger edit); **`the final placings split…`** — ranking-grain mismatches
   **non-empty** (the guarantee, loud) and each covered by a triaged ranking
   entry, none missing. Count pins (decision 4) in the raw-exact step or a
   third step: computed normalised count == the ledger's measured count.
3. **Measured-first curation:** run `@gliderscore` on sqlite with the ledger's
   pre-argued spine — the verdict will be Mismatch (ranking entries not yet
   enumerated). Read the report: enumerate the ranking-grain mismatches into
   the ledger's per-pilot entries (GS rank vs seed-run placing, each citing
   the drop rule + the unset-threshold config and, where the rounding cascade
   contributed, the rounding citation); pin the measured normalised count;
   confirm the raw grain was exact; confirm the drop fired on real rounds
   (a pilot's seed-run aggregate = his Σ11 minus his lowest cell). Re-run to
   green. **Escalation law:** a computed difference outside the triaged set
   after curation, or a triaged difference that fails to appear, is triaged
   kind 1/2 with citations or escalated kind 3 as a defect — never a ledger
   edit to fit.

**Ledger spine (step 1):**

- Provenance: `scoredWindowRounds: 11`; `parameterBindings: []` + the
  decision-8 note; metricMappings: `overflySeconds` → 0,
  `touchedByCompetitor` → false (seed-declared `whenNotRecorded`, the engine
  resolves, the harness emits nothing — each with its `fai-rules` citation).
- DerivedMetrics: `startHeight` + `startHeightRecorded` (source
  `Scores.FlightScoreDeduction`, `5.5.11.12 d` / `5.5.11.7 e`);
  `landingDistance` (source `Scores.Landing` plus the fixture's scheme-11
  definition, decoded in the importer to actual awarded points, submitted unit
  `pts`). Cite the source scheme rows and retain per-cell source-mark
  traceability through the unchanged fixture, including 0→0. The core event
  records the importer-submitted points value/unit, not the original mark;
  do not describe points membership as proof of physical band equivalence.
- Notes: the unwitnessed list — the raw floor (min flown raw 123), the
  motor-restart rows (Before-starting answer), the 75 m rows (flight-less both
  sides), the overfly corroboration (max 599 s), the `+0.005` nudge
  (integer-exact), GS's report-time re-round before comparing (the `=n`
  tie-display semantics vs the engine's exact-decimal ties — a rounding-cascade
  disclosure), the defaulted parameters, and the R12–18
  declared-outside-window scope.
- `triagedDifferences`: pre-argued — one **normalised**-grain class entry
  (`pilotNo: "*"`, kind 1: GS HalfUp-1dp grid vs the seed's exact values;
  citation: `configProvenance.knobs.GroupScoreDecimals` basis vs the rulebook's
  silence on normalised rounding precision, the f3j re-triage framing). The
  **ranking**-grain entries (per-pilot, kind 1: the drop split — Σ best-10 vs
  Σ 11, the seed's `applyWhenRoundsCompletedAtLeast: 5` vs the club's unset
  thresholds, plus the rounding cascade) are **enumerated from the measured
  run** in step 3.

**Scenario (step 2):**

```gherkin
Scenario: The f5j-christchurch-2019 parallel run under canonical F5J reports exactly the triaged differences
  Given the fixture corpus manifest
  When the harness parallel-runs the GliderScore fixture "f5j-christchurch-2019" under the seed class "30-f5j"
  Then the parallel-run verdict is exactly the triaged differences
  And the raw grain is exact against the GliderScore oracle
  And the final placings split from the GliderScore oracle exactly as the ledger triages
  And every ledgered difference is a triaged rulebook-vs-local-practice difference with a citation
```

**Done-when:** the christchurch scenario passes on sqlite; the verdict is
`MatchesTriagedSet` (Exact is a loud failure — a witness pair with a non-empty
ledger); the raw grain exact; ranking mismatches witnessed per pilot.

### WI-4 — Full verification

1. `@gliderscore` and the full acceptance suite on **both stores**
   (`SOARSCORE_TEST_STORE=sqlite` fast loop; postgres via Testcontainers) — a
   backend Soarscore claims to support passes this suite unchanged.
2. Domain, Application, Architecture suites green (no-core-change proof — the
   story touches no `src/` code).
3. Corpus discipline: **no corpus file changes** — the ledger and scenario are
   additions beside the fixture; `validate.py --index` untouched (11/11);
   the mapping-table row is a curation edit, not a corpus file.
4. Adapter examples from WI-2 green, including all source-scheme rows, zero,
   loud rejection cases, value/unit preservation and retained eligibility.
   Cite the parent's generic capture/amendment and distance-vs-points property
   coverage rather than moving that responsibility into this story. Verify
   canonical landing data and corpus count remain unchanged.

### WI-5 — Flips, coverage, board

1. **Mapping-table flip** (D6 of the mapping-table story): the christchurch
   row → `done`, `why` folds the measured outcome (the split shape, the
   decoded landing points' cell-exactness, the window scope). Seed coverage
   records christchurch under `30-f5j.json`, alongside hawkes-bay/south-island;
   no variant line or seed-count growth.
2. **Cross-story citations** per the contract above: cite the parent's actual
   lane and verified WI-0 through WI-3 contract. Its Jerilderie WI-4 may still
   be parked; do not require a completed parent or edit one that is completed.
3. Board reconciliation: `tech-debt.md` / `deferred-decisions.md` only for
   what the run actually surfaced (expected: the fly-off draw deferral already
   stands; nothing new anticipated); move this story to `completed/` with
   `git mv`, set the status header; `graphify update .`.

## Testing approach

- **Pair comparator remains example-driven.** The product is set-equality
  between a computed difference set and a curated triaged set, per hand-curated
  pair; the invariant ("the report is exactly the triaged differences") is
  structural and example-asserted per pair, not a new property test here.
- **Generic property testing belongs to the parent.** For a valid declared-m
  distance and its applicable lookup award, submitting that award as explicit
  `pts` must yield the same landing contribution under identical eligibility
  inputs; ineligible landing/flight inputs must not gain points by changing
  unit. This is distance-vs-points scoring equivalence, not evidence that the
  fixture's tape marks correspond to physical distance bands.
- **Adapter examples enforce decoding.** Test all scheme-11 rows, including
  every 91–95→45 and 96–100→50 mark, plus 0→0 and unknown marks; reject decoded
  awards outside the canonical table, missing/unsupported units and unmatched
  points. Pin `landingDistance`/`pts` capture and amendment value/unit
  preservation, source-mark/scheme provenance, no fallback or double conversion,
  unchanged parity numbers, and retained landing and flight eligibility gates.
  Consume the parent's contract; no generic core work or property suite here.
- **What each failure must name:** a raw-grain mismatch → investigate source
  decoding/capture or canonical scoring (adapter/authoring bug or kind-3,
  escalate, not a ledger edit); a normalised mismatch outside the measured
  count → re-triage; a ranking mismatch outside
  the enumerated set → re-triage or kind-3 escalation with the full
  `Render()`; a provenance break → the run did not run under the disclosed
  window/bindings/derivations.

## Scope guards and standing constraints

- **Anti-goal, stated hard:** the seed classes are never tuned to GS. The
  rulebook-faithful `30-f5j` stays canonical and untouched except the 75 m
  rulebook fix; its landing definition is unchanged. Source-scheme decoding is
  an importer concern, with actual awarded points submitted on the existing
  metric via the parent's generic unit contract. A GS-vs-rulebook disagreement
  (the drop thresholds are one) is reported, never reconciled.
- **Ledger semantics:** the parallel-run ledger is a different contract from
  the fixture divergence ledger — distinct schema, distinct comparator, never
  merged. Unwitnessed candidates live in provenance notes, never as entries
  (a triaged difference that fails to appear fails the scenario).
- **No `src/` changes in this story.** Drop mechanics, dormancy and absence
  semantics are landed; generic unit-aware capture/amendment, validation and
  eligibility preservation must land in parent WI-0 through WI-3 first. This
  story is seed75m + harness/ledger only; a contradiction found in the run
  escalates as a finding, not a local core implementation.
- **No new domain concepts** — "scored window", "source-scheme decoding",
  "count pins" are harness/corpus vocabulary (the "record scenario" and
  "parallel-run ledger" precedents). House rule 2 cross-reference (done at
  planning): `docs/users.md`'s "parallel" is role separation, unrelated;
  NFR-1/NFR-2 are *supported* (canonical class data stays unchanged in landing,
  generic core contains no source-scheme branches); `metric-absence-semantics`
  supplies the assumption machinery; nothing in `/docs` changes (house rules 3–4).
- **No physical-tape or different-table rescore scope.** Do not infer physical
  band equivalence from a valid points value-set, invent metres, or reopen a
  variant-route approval gate. The former geometry-dependent follow-on plan is
  superseded by the 2026-09-08 decision; no such stub is required by this story.
  Any genuinely new feature found during implementation needs a separate
  backlog stub under house rule 6, never silent scope growth here.
