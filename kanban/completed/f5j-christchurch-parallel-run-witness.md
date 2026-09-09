# Story — F5J Christchurch parallel-run witness (the guaranteed divergence)

**Status:** Completed 2026-09-10 — canonical `30-f5j` witness landed:
rulebook drop-from-5 moves 7 pilots over R1–11 (p79 12→13, p82 7→6, p83 6→8,
p94 13→11, p129 8→9, p131 9→7, p133 11→12), raw grain exact on the declared NZ
F3J-side tape, normalised grid pinned 127 (WI-4: 86/86 acceptance both stores;
Domain 783, Application 310, Architecture 7).
**Raised:** 2026-09-06 · **Plan** written 2026-09-07 (fixture
data measured, GS arithmetic and engine mechanics checked during planning;
decisions 1 and 3–8 owner-confirmed same day; **decision 2 rewritten
2026-09-08 (second revision)** when the owner supplied the actual NZ landing-tape
practice — the earlier "importer decodes the scheme" route is superseded) ·
**Re-verified and citation-corrected 2026-09-09:** the parent prerequisite's
WI-1–WI-5 code is landed and green (commit `68c3cd1` — declaration, capture,
composition, tape seeds, both BDD scenarios; Domain 758 / Application 305 /
Architecture 7 / Infrastructure non-Storage 76 / Acceptance 78 on sqlite,
build clean, zero skips), every code and seed citation below was re-checked
against the tree and corrected, and this story's WI-0 is a verification, not a
wait. The parent's outstanding WI-0 paperwork (owner-approved
glossary/class-diagram wording, two owner questions) is owner-gated and gates
the parent's completion, not this story's WI-1. · **Raised:** 2026-09-06
(decision 2 of
`kanban/completed/seed-definition-parallel-run.md`: the guaranteed-divergence
witness role moved here from the withdrawn f3j-international claim)

## What

Run `f5j-christchurch-2019` under **canonical `30-f5j.json`**, with its landing
definition unchanged, through the parallel-run harness and ledger the
differences against the triaged set. First, `kanban/in-progress/tape-points-landing-seeds.md`
WI-0 through WI-5 must deliver the landing-tape model: the tape as a declared
reading scale, and the composition of that scale with the class's own rulebook
landing table. This story then owns only the canonical 75 m seed fix and the
harness/ledger work. The fixture **declares the NZ F3J-side tape** and submits
`row.Landing` as a reading, verbatim; it neither invents metres nor decodes the
scheme in harness code.

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

The **landing side is expected cell-exact, and now provably so**: GS scheme 11
"F5J Enter Landing" *is* the seed's own `5.5.11.12 h` table pre-composed on the
F3J side of the tape — verified row for row during the 2026-09-08 replan (the
parent story's arithmetic section). Both sides therefore run identical
arithmetic (min(time, 600)·1 + the composed landing award − two-rate height
deduction), with the canonical landing eligibility gates still applied. Any
raw-grain mismatch is a declaration/authoring bug or a kind-3 engine
divergence, escalated — never triaged. See **Settled decision 2**.

Two planning findings reshaped the stub, both verified against the tree:

- **The drop split is conditional on the run's prescription shape** (decision
  1). The fixture carries 18 rounds of rows — R12–18 are wholly-unflown GS
  placeholder rows — and the engine's `PhaseAggregator` treats a round with no
  score as **0** and pools it into the ByRound drop candidates
  (`PhaseAggregator.cs:83-92` "No score recorded — treat as 0";
     `ApplyByRoundDrop` `:191-216`, ordering candidates by total then the
   `DropTieBreak` — Latest default, owner decision 2026-09-08). A full-fixture run lets the drop eat a
  phantom zero and the aggregate split vanishes. The run therefore replays the
  comp GS actually scored: **rounds 1–11** (the rollup window,
  `ladder.py:201` `TaskLastRound = MAX(RoundNo where Updated='True')` = 11).
- **The landing scale is an instrument, not a different class and not an
  importer concern** (decision 2, rewritten 2026-09-08). Every recorded landing
  value hits the scheme's exact-match rows or is 0 (verified); the recorded
  number cannot honestly be read as metres-from-spot (the scheme rewards larger
  readings). Feeding it into `30-f5j` as metres would fabricate an observation
  the club never made (the 24 zero readings would become perfect spot landings,
  +50 each). Nor is it points: mark 55 is an award of 5. It is a **reading on
  the F3J side of the standard NZ landing tape**, whose scale is `NZ.2.4.4`, and
  scheme 11 is exactly that scale composed with `5.5.11.12 h`. F5J's own
  `5.5.11.12 i` corroborates it: "A dedicated non-elastic tape marked in bonus
  (landing) points is the means, by which this distance is measured" - the rule
  mandates a tape reading, so this fixture's data is what the rulebook asks for,
  not a local shortcut. The fixture declares the tape and submits the reading;
  the parent story's composition produces the award. Neither physical tape
  calibration nor rescoring against a
  different table is needed for this run.

The driver already captures F5J launch height (parity
`launchHeight` arm, `ReplayDriver.cs:1066-1078`) — the pair needs the P2
derivation re-expressed under the seed's metric names (decision 5), plus the
tape declaration and verbatim reading capture through the parent's contract. No
declaration, capture or composition plumbing belongs to this story.

## Why it matters

The ales pair — the parallel-run machinery's proof pair — came out near-exact:
the witnessed difference set was three raw-grain entries with identical final
placings. The parallel-run claim has therefore never yet been seen producing a
*visible* split on the day, which is the product's whole point ("what would
have differed"). This pair guarantees one — a drop-policy split moving final
placings, the first time the ledger's ranking grain carries triaged entries at
all. It also exercises canonical F5J scored from tape readings,
without changing its landing definition, and is the first sighting of
the HalfUp-1dp-vs-exact rounding split the f3j re-triage predicted but could
not confirm. The fly-off/promotion spike the stub carried is answered by
verification, not new machinery (decision 6).

## Settled decisions (2026-09-07, Pete; decision 2 rewritten 2026-09-08)

1. **Scored-window prescription: rounds 1–11 only.** The per-pair scored-window
   map lives in the driver, applied in parallel-run mode only (the ales pair's
   shape is untouched — regression-pinned by its scenario and ledger
   provenance). The ledger provenance declares the window
   (`scoredWindowRounds: 11`); the comparator scopes its oracle-coverage
   universe to the declared window and verifies the run's prescribed round
   count equals it. The R12–18 oracle cells (persisted 0.0 placeholders) are
   deliberately outside the comparison universe — a declared scope, recorded in
   the ledger, never a silent shrink.
2. **Canonical landing definition; declare the tape, submit the reading
   (owner decision 2026-09-08, second revision).** This supersedes both earlier
   routes — the derived tape-variant plans (`31-f5j-tape-points`,
   `SeedF5JTapePoints`, a new `landingTapeReading`/`landingPoints` metric,
   shared-seed lifting, corpus-count growth) **and** the "importer decodes
   scheme 11 into awarded points" plan that replaced them. Use `30-f5j`
   unchanged in its landing definition; decision 3 remains the only seed delta.
   The parent story delivers the landing-tape model: a tape is a declared
   reading scale, a competition declares which tape was used for which metric,
   capture records the reading together with its scale, and the core composes
   the scale with the class's own `LookupTerm` rows to get the award — refusing
   loudly if a tape band straddles two awards. Only the parent owns that work.
   **This fixture declares the NZ F3J-side tape (`NZ.2.4.4`) and submits
   `row.Landing` verbatim, including the off-the-tape zeros.** No decoding, no
   mapping table, no invented metres, no second conversion in harness code, and
   the parity branch stays numerically unchanged. GS scheme 11 is `5.5.11.12 h`
   pre-composed on that scale — verified row for row — so it is a **verification
   target for the parent's composition**, not adapter logic:

   | reading | band | `5.5.11.12 h` | GS scheme 11 |
   |---|---|---|---|
   | 96–100 | ≤1 m | 50 | 50 |
   | 91–95 | (1,2] | 45 | 45 |
   | 90 / 85 / 80 / 75 | (2,3] … (5,6] | 40 / 35 / 30 / 25 | same |
   | 70 / 65 / 60 / 55 | (6,7] … (9,10] | 20 / 15 / 10 / 5 | same |
   | ≤50, off-tape | >10 m | 0 | absent ⇒ 0 |

   A reading the declared tape has no mark for fails loudly. Retain the
   readings and the declared scale in fixture and ledger provenance; the core
   event honestly records the reading and the scale it was read on. A valid
   reading is not a claim about the physical tape's calibration; calibration and
   rescoring against a different table are outside this witness, not
   prerequisites.
3. **Fix the seed's 75 m authoring gap now.** The F5J seed cannot express
   `5.5.11.7 d` ("nose not at rest within 75 m of the designated landing spot"
   → flight = 0, `docs/rules/f5j.md:37`): it declares no within-75 m flag and
   its `flightValidWhen` gates only overfly ≤ 60 s and `startHeightRecorded`
   (`30-f5j.json:241-263`). Add a `landedWithin75m` Flag metric with
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
   mismatches (the declared-scale and canonical-scoring exactness claim).
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
    scores only **drawn** phases (`ScoringService.cs:226` iterates
    `competition.Phases`; `Finalise` iterates drawn phases —
    `Competition.cs:2192`, drawn-phase loop `:2250`); a second phase
    cannot be drawn today (the `!Phases.IsEmpty` refusal lives in the shared
    `ResolveSchedule`, `Competition.cs:1430`, reached by both `DrawPhase`
    (`:1212`) and `PrescribeDraw`; `kanban/deferred-decisions.md` §Draw);
    `RankingEngine.Rank` receives
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
   mappings only (`ParallelRunComparator.cs:416-425`) — the number-valued
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
  ≤ 599 s (nobody overflew); landing values ∈ scheme-11 rows ∪ {0} — i.e.
  every one is a mark on the F3J side of the tape or the off-tape reading (24
  rows recorded 0 — no bonus, GS's exact-match short-circuit `ladder.py:142-145`);
  `Penalty` all 0 (G4 moot — the harness's loud infraction-mapping refusal at
  `ReplayDriver.cs:724-739` never fires); `F5JMotorReStarted` true on 22 rows
  (see Before-starting); `LandingOver75m` true only on the two cancelled rows.
- Two rule-driven cancelled flights — R8/G3 P82, R11/G1 P104 (time 0, height 0,
  `LandingOver75m: true`): GS persisted 0.0 cells; the harness's zero-row rule
  (`Time1Mins <= 0` → flight-less, `ReplayDriver.cs:1035-1038`) makes them
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
  P2 height derivation and the tape declaration plus verbatim reading capture
  for this parallel-run pair, using the parent's contract, inert for existing
  pairs.
- Naming (`ReplayDriver.cs:383-390`): the competition carries canonical
  `30-f5j`'s own name/version automatically.
- Comparator (`Comparator.cs`): `RecordCell`/`AddIfDifferent`/
  `EnsureOracleCoverage` (`:1507/:1518/:1534`) are internal and shared;
  `CompareRankingGrain` (`:689`) compares engine placings against the oracle's
  `=n` ranks **and** tie-group membership. Ledger `Covers` supports
  `pilotNo: "*"` and null round/group scope (`ParallelRunLedger.cs:98-104`).

**Seed class** (`30-f5j.json`): drops `applyWhenRoundsCompletedAtLeast: 5`,
dropCount 1, ByRound, `tieBreak: "Latest"` (`:153-160` — `DropPolicy.TieBreak`,
owner decision 2026-09-08 in
`kanban/completed/literal-record-f3k-sample-comp.md` WI-4b: equally-lowest
rounds drop latest-first; it names WHICH round drops when a pilot's minimum
ties, relevant to the ledger's per-pilot ranking citations); phase-2 fly-off
(`TopPercent` 30, `qualifyingPosition` tie-breaks, `:366-398`) never drawn;
both parameters defaulted (`:6-36`); `startHeightRecorded` has no
`whenNotRecorded` (`:187-191`); `overflySeconds` → 0 and
`touchedByCompetitor` → false declared (`:211-223`); landing lookup
conditional on overfly == 0 ∧ !touched, table ≤ 1 m → 50 … > 10 m → 0
(`:312-360`).

**Raw-grain exactness claim** (the declared-scale run's testable spine):
both sides compute min(t, 600)·1 + the landing award − two-rate height, identical band
arithmetic, no floor exercised, no overfly recorded → every raw cell in the
window must compare exact (162 flown + 34 flight-less 0.0 + the 2 cancelled
0.0). The scenario pins this at zero. The landing term is exact **by
derivation**, not by adapter examples: GS scheme 11 and the composed
`5.5.11.12 h` are the same function (decision 2).

## Cross-story contract — `kanban/in-progress/tape-points-landing-seeds.md`
(owner decision 2026-09-08, second revision; supersedes both the shared-variant
plan and the importer-decoding plan)

- **Prerequisite and ownership.** Parent WI-0 through WI-5 owns the landing-tape
  concept and its glossary approval, the tape catalogue and its seeds, the
  per-competition declaration of the instruments in use and its correction path,
  the reading-to-band inversion and band-to-award composition with its straddle
  refusal, reading-set validation, capture/amendment naming a declared
  instrument or none, the unchanged distance path and mixed-instrument
  equivalence, and the retained eligibility gates. This F5J story starts its code
  work only after that prerequisite — landed and verified 2026-09-09 (commit
   `68c3cd1`; see WI-0) — and owns only seed75m + harness/ledger.
- **Declare the scale, submit the reading.** Consume the parent's declaration
  and capture contract with the NZ F3J-side tape and canonical
  `landingDistance`. No new landing metric, class variant, shared-seed lift,
  corpus entry, or decoding table anywhere in this story. The parent owns the
  composition-fidelity property; this story owns the example-driven pair.
- **No rulebook table in harness code.** GS scheme 11 is `5.5.11.12 h`
  composed on `NZ.2.4.4`. Reproducing that mapping in an adapter would put a
  rulebook table in test code, which is the duplication the parent story exists
  to remove. Scheme 11 is a verification target for the parent's composition;
  this story asserts the fixture's readings are all on the declared tape and
  keeps the readings and scale traceable in fixture/ledger provenance.
- **Path obligation.** Track the parent's actual lane. Once it moves, update
  this story's prerequisite citations to its actual path. Use
  `kanban/completed/tape-points-landing-seeds.md` only once completed, citing
  its verified WI-0 through WI-5 contract. If Jerilderie WI-6 remains parked,
  cite the parent's blocked path instead; WI-6 is not an F5J prerequisite.
  Do not edit a completed parent story; it is history.

## Before starting

- **Check the board first — ANSWERED 2026-09-09:** `tape-points-landing-seeds.md`
  WI-1 through WI-5 are landed and green (commit `68c3cd1`); read the landed
  contract as recorded in this story's WI-0 before WI-1. The parent's approved
  glossary/class-diagram wording does not exist yet (its WI-0 paperwork is
  owner-gated); it does not gate this story's code work, and nothing here
  changes when it lands. Update this story's citations to the parent's
  completed path when it completes, without editing the completed story.
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
  row `tests/GliderscoreFixtures/parallel-run-mapping.md:98`). The row's
  **seed remains `30-f5j.json`**; WI-1 updates its rationale for the declared
  tape scale and the drop/rounding witness, per the table's update contract.
  The parent's WI-5 already rewrote the table's G5 gate and the Jerilderie row
  (now `:101`) for the declared-instrument model — discharged, do not
  duplicate.

## Plan

### WI-0 — Landing-tape prerequisite (verified 2026-09-09; blocks WI-1)

1. **Parent WI-1 through WI-5 verified landed** (commit `68c3cd1`; Domain 758 /
   Application 305 / Architecture 7 / Infrastructure non-Storage 76 /
   Acceptance 78 on sqlite, build clean, zero skips). The landed contract this
   story consumes — do not implement, duplicate or shortcut any of it:
   - **Declare:** `POST /declare-instruments` — `DeclareInstruments(
     CompetitionRef, ImmutableArray<DeclaredInstrument>, By)` where
     `DeclaredInstrument` is `{ Instrument, Metric, Scale }` with the full
     `ReadingScale` inlined; `Instrument` is a caller-chosen name (no catalogue
     slug field — the harness picks one, e.g. `nz-f3j-side`, and records it in
     provenance). Correction path: `POST /correct-instrument-declaration`.
     Events `InstrumentsDeclared` / `InstrumentDeclarationCorrected`, store
     aliases `instrumentsDeclared` / `instrumentDeclarationCorrected`.
   - **Capture:** `CaptureMeasurement(EntryRef, FlightSequence, Metric, Value,
     string? Instrument = null)`; `AmendMeasurement(..., string? Instrument,
     bool ChangeInstrument)` — `ChangeInstrument` is the switch/clear
     discriminator; default retains the effective instrument.
   - **Refusals this story's tests name:**
     `captureMeasurement.readingNotOnScale`,
     `captureMeasurement.instrumentNotDeclared`,
     `tapeComposition.straddledBand` (surfaced at declaration time as a
     pass-through code under the `declareInstruments.` message wrapper).
   - **Composition:** `TapeComposition.Compose`
     (`src/Soarscore.Domain/PublishedClassDefinition/ReadingScale.cs:131`),
     declaration-time gate `Competition.ValidateInstrumentSet`
     (`Competition.cs:1147`), scoring-time
     `FlightInterpreter.EvaluateComposedLookup` (`FlightInterpreter.cs:203`).
   - **The tape:** `tape-nz-f3j-side` (`SeedTapeNzF3JSide.cs`, scale
     `NZ.2.4.4` — 23 marks `0.2→100 … 15→30`, off-tape reading `0`). In the
     harness, build the scale via
     `TapeCorpus.All.First(t => t.FileName == "tape-nz-f3j-side").Tape.ToReadingScale()`
     (the acceptance project already references `Soarscore.SeedData`; `src/`
     does not, and needs nothing here).
   Cite the parent's actual path (`kanban/in-progress/`), switching to
   `kanban/completed/tape-points-landing-seeds.md` when it completes, without
   editing a completed story.
2. **Verified against the fixture.** All 18 distinct recorded landing values
   are marks on `tape-nz-f3j-side` or the off-tape `0` ({60, 65, 70, 75, 80,
   85, 90, 91–100} ∪ {0}); the parent's composition reproduces GS scheme 11
   row for row — pinned by
   `TapeCompositionTests.F3J_side_tape_reproduces_F5J_enter_landing_row_for_row`
   and the parent's BDD composed-award assertions on canonical F5J. Decision
   2's route and decision 3's seed fix stand without reconfirmation; physical
   tape calibration is not a gate.

### WI-1 — Seed work (`tools/Soarscore.SeedData`)

1. **The 75 m fix** (decision 3): add `landedWithin75m` (Flag,
   `whenNotRecorded: true`) to both `SeedF5J` tasks and extend both tasks'
   `flightValidWhen` with the `== true` gate; header comment cites
   `5.5.11.7 d` and this story. Copy the in-tree precedent:
   `SeedF5jNdc.cs:57` declares the Flag (`M.Flag("landedWithin75m",
   whenNotRecorded: true)`) and `:107` the gate (`P.Is("landedWithin75m",
   true)`), cited `NZ.0.3 h; FAI 5.5.11.7 d`. Domain test first if a test seam
   exists for `flightValidWhen` gates (the metric-absence-semantics property
   tests are the precedent); otherwise pin via the
   seed-arithmetic/ingestion suites.
2. **Keep canonical landing unchanged.** No landing metric/table/identity
   changes, new seed, shared-seed lifting or `Corpus.All` count changes.
   Preserve all other canonical parameters, reflight, penalties, phases,
   drops, promotion, timing, group and normalisation data.
3. Regenerate: `dotnet run --project tools/Soarscore.SeedData` — all integrity
   checks pass; the emitted `json/30-f5j.json` shows **exactly** the 75 m delta
   (metric + gate, both tasks) and nothing else (`tools/Soarscore.SeedData/json/`
   is untracked — diff the regenerated file, not git).
4. **Update the mapping-table row** (`parallel-run-mapping.md:98`): seed stays
   `30-f5j.json`, `why` → the drop split + rounding grid, landing read on the
   declared NZ F3J-side tape and composed with canonical `landingDistance`
   (cite this story); status stays `stubbed` until WI-5.

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
2. **Oracle-coverage universe scoped to the window.** `ParallelRunComparator`'s
   `EnsureOracleCoverage` walk currently runs over ALL of
   `fixture.ExpectedScores.Scores.Keys` (`ParallelRunComparator.cs:231-234`;
   324 cells here). Under decision 1's window it must scope the coverage
   universe to `scoredWindowRounds`: oracle cells outside the window are
   deliberately uncompared (a declared scope, recorded in the ledger, never a
   silent shrink), and decision 1's check that the run's prescribed round
   count equals the declared window lives here. Without this the 11-round
   window produces 126 spurious "never compared" mismatches per grain (252
   total).
3. **P2 height arm** in `CaptureDurationInputs`
   (`ReplayDriver.cs:1021-1082`; the `launchHeight` arm is `:1076`), beside it:
   `declared.Contains("startHeight")` → capture `startHeight`
   (`row.FlightScoreDeduction`) and `startHeightRecorded` (Flag true) for every
   flown row with a positive payload; a flown row with a zero payload captures
   `startHeightRecorded` = false (decision 5's rule — never exercised here).
4. **Tape declaration and verbatim reading capture, parallel-run only:** for
   this fixture, declare the NZ F3J-side tape for `landingDistance` through the
   parent's landed declaration contract (WI-0: `POST /declare-instruments`,
   scale built via `TapeCorpus…ToReadingScale()`, harness-chosen instrument
   name), then capture `row.Landing` **as recorded and naming that tape** via
   `CaptureMeasurement`'s optional `Instrument` parameter, including 0 for
   off-the-tape, with no decoding, mapping, interpolation, fallback, fabricated
   metres or second conversion. Today `ReplayDriver`'s capture call passes no
   instrument and `SlotCapture` carries none (`ReplayDriver.cs:634-638`) — the
   widening is the `SlotCapture` field, the capture arm, and the declaration
   POST in parallel-run mode. Every one of this fixture's landing rows is a
   tape reading, so it needs no mixed-instrument arm; a reading the declared
   tape has no mark for fails loudly. Preserve the readings and the declared
   instrument in ledger provenance. Keep parity numerically unchanged.
5. **Ledger schema widening** (`ParallelRunLedger.cs` — `Resolved` is still
   `bool` at `:75-79`, and `ParallelRunProvenance` still carries only
   `ParameterBindings`/`MetricMappings`/`Notes` at `:60-63`):
   `ParallelRunMetricMapping.Resolved`: `bool` → `JsonElement` — Flag (bool)
   and Number (decimal) `whenNotRecorded` values both expressible; the ales
   ledger deserialises unchanged. `ParallelRunProvenance` gains
   `ScoredWindowRounds` (`int?`, null-tolerant — the ales ledger carries none),
   `DerivedMetrics` (`IReadOnlyList<ParallelRunDerivedMetric>?` — `Metric`,
   `Source`, `Derivation`, `Justification`; null-tolerant empty) and
   `DeclaredInstruments` (`IReadOnlyList<ParallelRunDeclaredInstrument>?` —
   `Instrument` (the harness-chosen name), `Metric`, `TapeSlug`, `Clause`;
   here `nz-f3j-side` / `landingDistance` / `tape-nz-f3j-side` / `NZ.2.4.4`;
   null-tolerant empty — the ales pair declares none). The parent's
   declaration shape is landed (WI-0), so "whatever shape" is resolved.
6. **`CheckProvenance` widening** (`ParallelRunComparator.cs`): verify
   number-valued metric mappings against the adopted seed's `whenNotRecorded`
   number; verify each derived metric is declared by the adopted seed (name
   match, exactly once across phases); verify `ScoredWindowRounds` (when set)
   equals the outcome's prescribed round count; verify a declared reading scale
   matches the competition's actual declaration.
7. **Fixture example tests:** assert every recorded landing value in the
   fixture is a mark on the declared tape (or the off-tape reading), that the
   submitted measurement is the reading verbatim with its scale, and that the
   composed award equals GS's scheme-11 cell for each of them — every mark
   91–100 individually, plus 0. Cover a reading absent from the declared tape
   and a wrongly declared tape as loud rejections, and pin no double conversion
   and unchanged parity numbers. These are integration examples over the
   parent's contract; the composition property itself is the parent's, and no
   mapping table is authored here.

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
   (the declared-scale/canonical exactness claim; failure = authoring bug or
   kind-3, escalate, never a ledger edit); **`the final placings split…`** — ranking-grain mismatches
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
   (a pilot's seed-run aggregate = his Σ11 minus his lowest cell; where a
   pilot's minimum ties across rounds, the dropped round is the LATEST such
   round — `tieBreak: "Latest"`, `30-f5j.json:158`). Re-run to
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
  `Scores.FlightScoreDeduction`, `5.5.11.12 d` / `5.5.11.7 e`). Landing is
  **not** a derived metric: `landingDistance` carries the reading exactly as
  recorded (`Scores.Landing`), and provenance instead declares the **reading
  scale** — the NZ F3J-side tape, `NZ.2.4.4` — under which the award is composed
  from the seed's own `5.5.11.12 h` rows. Note that GS scheme 11 is that same
  composition, cite its rows, and do not describe the reading as points or as a
  claim about the physical tape's calibration.
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
   thresholds, plus the rounding cascade; where a pilot's minimum ties across
   rounds, also cite the seed's `tieBreak: "Latest"` (`30-f5j.json:158`),
   which selects which equally-lowest round drops) are **enumerated from the
   measured run** in step 3.

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
   `Corpus.ExpectedCount` stays 16 (`tools/Soarscore.SeedData/Corpus.cs:25`)
   and `TapeCorpus.ExpectedCount` stays 2 (counted separately — no tape work
   in this story); the mapping-table row is a curation edit, not a corpus
   file.
4. Fixture examples from WI-2 green, including every recorded mark, the
   off-tape reading, loud rejection cases, reading/scale preservation and
   retained eligibility. Cite the parent's declaration, capture and
   composition-fidelity coverage (its properties 1–2; see the Testing-approach
   caveat on 3–4) rather than moving that responsibility into
   this story. Verify canonical landing data and the class corpus count remain
   unchanged.

### WI-5 — Flips, coverage, board

1. **Mapping-table flip** (D6 of the mapping-table story): the christchurch
   row → `done`, `why` folds the measured outcome (the split shape, the
   declared-scale landing's cell-exactness, the window scope). Seed coverage
   records christchurch under `30-f5j.json`, alongside hawkes-bay/south-island;
   no variant line or seed-count growth.
2. **Cross-story citations** per the contract above: cite the parent's actual
   lane and verified WI-0 through WI-5 contract. Its Jerilderie WI-6 may still
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
- **Generic property testing belongs to the parent — with a recorded caveat.**
  The parent's landed properties 1–2 (composition fidelity; refinement is
  exactly the condition) are genuine CsCheck over corpus and generated tapes
  (`TapeCompositionPropertyTests.cs`, `TapeLandingScaleProofTests.cs`
  properties a/b). Properties 3–4 as built are weaker than the parent's plan
  words them: instrument equivalence is example-based and asserted at
  flight-interpretation grain, and the eligibility-invariance property never
  varies the instrument. Strengthening those two stays the parent's work
  (`kanban/in-progress/tape-points-landing-seeds.md` WI-5); until it lands,
  this story's per-mark fixture examples (WI-2) are the scoring-grain witness
  for this pair, and this story must not cite the parent's fairness proof as
  generic.
- **Fixture examples enforce the declaration, not a mapping.** Assert every
  recorded landing value is a mark on the declared tape (each 91–100
  individually, plus the off-tape 0), that capture stores the reading verbatim
  with its scale, and that the composed award equals GS's scheme-11 cell.
  Reject a reading absent from the declared tape and a wrongly declared tape.
  Pin readings/scale provenance, no decoding table, no fallback or double
  conversion, unchanged parity numbers, and retained landing and flight
  eligibility gates. Consume the parent's contract; no core work or property
  suite here.
- **What each failure must name:** a raw-grain mismatch → investigate the tape
  declaration, capture or canonical scoring (authoring bug or kind-3, escalate,
  escalate, not a ledger edit); a normalised mismatch outside the measured
  count → re-triage; a ranking mismatch outside
  the enumerated set → re-triage or kind-3 escalation with the full
  `Render()`; a provenance break → the run did not run under the disclosed
  window/bindings/derivations.

## Blocker (2026-09-10 — parks the story; WI-3 measured-first run)

**Finding:** the story's verified ground truth ("11 scored rounds × 3 drawn
groups of 6/6/6") is wrong for R5. Re-verified against
`tests/GliderscoreFixtures/f5j-christchurch-2019/scores-raw.json`: the drawn
partition (keptRows, flown + placeholder) is 6/6/6 in R1–R4 and R6–R11 but
**5/6/7 in R5** (G1: pilots 132/75/86/84/131; G2: 6; G3: 7; all 18 pilots
exactly once). The seed-run fails at `/prescribe-draw` with
`prescribeDraw.groupBelowClassMinimum — Round 5, group 1: the group has 5
member(s), smaller than the class's minimum group size (6)`
(`Competition.cs:1371`, seed `MinPerGroup = 6`, `SeedF5J.cs:91` citing
5.5.11.8) — before any grain compares. Parity replay of christchurch passes
13/13 (that twin's `minPerGroup: 2`), isolating the refusal to the canonical
seed's minimum.

**Rulebook position (fai-rules, WI-3):** 5.5.11.8.1 a) min-6 is SHOULD-level,
and 5.5.11.14.1 d)–e) is explicitly "Advisory Information" — worse, for this
18-pilot (≤30) contest clause e) triggers move-up/cancel-refill only at
4-or-fewer, which R5's 5-group doesn't trip. So the seed encodes a SHOULD as
a hard class minimum, the engine enforces it as a hard refusal, and the club
flew a 5-group nobody had to repair.

**What would unblock it (owner pick):** (1) replan around the draw-grain
finding (harness has no draw-grain vocabulary — likely a new backlog stub);
(2) a separate story recalibrating the engine's SHOULD-vs-shall hardness at
prescription; (3) rule the R5 5-group out of witness scope with a re-triaged
window (against the current window declaration). Out of scope per the story's
own guards: repartitioning R5 (fabricates a draw), relaxing the seed minimum
(tunes the seed to GS — anti-goal), an engine change (no `src/` changes),
dropping R5 silently (guts the drop witness).

**Owner decision 2026-09-10 (Pete):** rules that read as "SHOULD" are
non-terminal — a SHOULD-level breach (R5's 5-group against F5J's min-6) must
not refuse; it should surface as a warning somewhere/somehow. The warning
mechanism itself is a deferred decision (recorded in
`kanban/deferred-decisions.md` §Draw) so this story can move forward. This
selects route (2): new backlog stub
`kanban/backlog/should-level-minima-warn-dont-refuse.md` owns the hardness
recalibration; routes (1) and (3) fall away — R5 stays in the window, and
there is no refusal left to witness (a warning-grain, if the mechanism story
creates one, is that story's business). This story stays parked here until
the stub lands, then returns to `in-progress/` with `git mv`.

**Landed alongside the finding (uncommitted):** WI-1 75 m fix + domain tests;
WI-2 harness widening + 5 tape-reading example facts; WI-3 ledger spine
(`f5j-christchurch-2019/parallel-run/30-f5j.json`), scenario + two witness
steps, stale-expectation fixes (`SeeingWhatIsRecorded`, landing-tape capture
sites — WI-1 metric now enumerated), `CheckProvenance` cross-phase-duplicate
fix, fai-rules resolutions (motor-restart note; overfly 5.5.11.12 g+k,
touched 5.5.11.12 j).

## Scope guards and standing constraints

- **Anti-goal, stated hard:** the seed classes are never tuned to GS. The
  rulebook-faithful `30-f5j` stays canonical and untouched except the 75 m
  rulebook fix; its landing definition is unchanged. The reading scale is
  declared equipment, and the award is composed from the class's own rulebook
  rows via the parent's generic contract — no scale mapping is authored in this
  story. A GS-vs-rulebook disagreement (the drop thresholds are one) is
  reported, never reconciled.
- **Ledger semantics:** the parallel-run ledger is a different contract from
  the fixture divergence ledger — distinct schema, distinct comparator, never
  merged. Unwitnessed candidates live in provenance notes, never as entries
  (a triaged difference that fails to appear fails the scenario).
- **No `src/` changes in this story.** Drop mechanics, dormancy and absence
  semantics are landed; the landing-tape concept, the declaration, capture with
  its scale, the composition and its refusals must land in parent WI-0 through
  WI-5 first — including the parent's glossary/class-diagram approval, which
  this story neither seeks nor pre-empts. This story is seed75m +
  harness/ledger only; a contradiction found in the run escalates as a finding,
  not a local core implementation.
- **No new domain concepts here** — "scored window" and "count pins" are
  harness/corpus vocabulary (the "record scenario" and "parallel-run ledger"
  precedents); the landing-tape concept is the parent's, under owner approval.
  House rule 2 cross-reference (done at planning): `docs/users.md`'s "parallel"
  is role separation, unrelated; NFR-1/NFR-2 are *supported* (canonical class
  data stays unchanged in landing, and no scale mapping enters harness code);
  `metric-absence-semantics` supplies the assumption machinery; nothing in
  `/docs` changes from this story (house rules 3–4).
- **No physical-calibration or different-table rescore scope.** Do not infer
  anything about the physical tape's accuracy from a valid reading, invent
  metres, author a mapping table, or reopen a variant-route approval gate. Both
  the geometry-dependent follow-on plan and the importer-decoding plan are
  superseded by the 2026-09-08 decisions; no such stub is required by this
  story. Any genuinely new feature found during implementation needs a separate
   backlog stub under house rule 6, never silent scope growth here.

## WI-5 completion (2026-09-10)

1. **Mapping-table flip** (D6 of the mapping-table story):
   `tests/GliderscoreFixtures/parallel-run-mapping.md:98` christchurch row →
   `done`; `why` folds the measured outcome (7-pilot drop split with per-pilot
   moves, raw-grain exactness on the declared scale, normalised grid pinned
   127, R1–11 window scope with R5 5-group prescribing under SHOULD warning).
   Seed coverage records christchurch under `30-f5j.json` as done alongside
   hawkes-bay/south-island; no variant line, no seed-count growth.
2. **Cross-story citations:** parent `kanban/in-progress/tape-points-landing-seeds.md`
   still in-progress — cited at its actual lane with its verified WI-0–WI-5
   contract (commit `68c3cd1`; see WI-0); completion not required, file not
   edited. R5 warn-through cites completed
   `kanban/completed/should-level-minima-warn-dont-refuse.md` (WI-1–WI-5).
3. **Board reconciliation:** verified, no new entries — the fly-off draw
   deferral stands (`deferred-decisions.md` §Draw; its SHOULD-mechanism half
   already records Landed by the hardness story); the run surfaced nothing new
   (all unwitnessed candidates live in ledger provenance notes, never as
   entries). `tech-debt.md` untouched.
