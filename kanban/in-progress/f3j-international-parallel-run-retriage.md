# Story — f3j-international parallel-run re-triage

**Status:** Backlog · **Raised:** 2026-09-06 (decision 2 of
`kanban/completed/seed-definition-parallel-run.md`: the pair's original
guaranteed-divergence claim was withdrawn; this stub carries the corrected
difference list) · **Plan written:** 2026-09-10 — the fixture data was
measured during planning, not estimated: both sides' arithmetic was simulated
cell-by-cell over all 485 oracle rows and the GS raw/normalisation models
verify with **zero mismatches** (see *Verified ground truth*). Counts below
are planning measurements; WI-2 re-pins them from the measured run. Decisions
1–8 are **proposed — owner confirmation gates WI-1**.

## What

Run `f3j-international` (GS comp 1: 30 pilots, 16 scored rounds × 4 groups,
485 rows) under the seed class `tools/Soarscore.SeedData/json/50-f3j.json`
("RC Thermal Duration Gliders", FAI F3 Soaring 2025 ed.2) through the
parallel-run harness and ledger the differences against the triaged set.

The story's original claim — guaranteed drop-policy divergence from GS's local
`Drop1AtRound=8` — **stands withdrawn**: GS's `Drop1AtRound=8` equals the
seed's `applyWhenRoundsCompletedAtLeast: 8` and the FAI rule (drop beyond 7
qualification rounds, `docs/rules/f3j.md:99`). Drop policies **agree**; both
sides drop each pilot's single worst round score from all 16.

The re-triage's witnessed candidates, all measured on the fixture's own data
(four difference classes, each rulebook-vs-local-practice — triage kind 1):

1. **The rounding grid — the stub's central candidate, and it materialises.**
   GS normalises `1000·Raw/max` **HalfUp to 1 dp** (`GroupScoreDecimals=1`,
   `RoundOrTruncate=0`) while the rulebook states the corrected score is
   "recorded (truncated) to one place after the decimal point" (`F3J.10.11`)
   — the seed's `normalise.round` is Truncate-0.1. This is the first sighting
   of the HalfUp-vs-Truncate split the ales pair predicted but never showed:
   **174 normalised cells** differ on identical raws and unshifted winners
   (witness cell `1/1/4/0/11`: exact ratio 715.986… → GS 716.0 vs seed 715.9).
2. **The landing-0 sentinel.** GS records "no landing bonus" as `Landing=0.0`
   and its exact-match scheme awards 0; the seed's rulebook table
   (`F3J.10.5`) has no 0-row, so a recorded 0.0 lands in the first band
   ("up to 0.2" → 100). **34 flown cells** differ by +100 on the seed side.
3. **The −30 late-landing deduction.** `durFlightPenalty=1` puts GS's
   late-landing deduction mode in force: 11 rows carry
   `FlightScoreDeduction=30`, deducted pre-normalisation. The seed declares no
   such metric (its −30 is `F3J.10.3`'s overfly offence, which never fires —
   all 11 rows sit inside the 600 s working time). **11 cells** differ
   (all ⊂ the sentinel cells — every deduction row also carries `Landing=0.0`).
4. **The R1 over-target decay.** GS scored round 1 against a **540 s target**
   with linear over-target decay (oracle-reconciled — the export carries no
   `DurTargetTimeByRound` rows; 22 flights over 540 s imply it exactly); the
   seed's working time is a fixed 600 s (`F3J.6.2 b`) with 1 pt/s capped at
   600 and no decay clause. **22 R1 cells** differ.

The classes cascade: **54 raw mismatches** (20 pure decay + 22 pure sentinel +
10 sentinel+deduction + 1 decay+sentinel + 1 all-three), **264 normalised
mismatches** (54 raw-carriers + 90 winner-shift cascade cells in the 10 groups
whose seed winner ≠ GS winner + 174 pure rounding-grid cells), and a **20-pilot
final-placing split** — the product grain, fully enumerated per pilot.

Carried alongside: the overfly-semantics `fai-rules` question (**answered in
planning** — decision 3) and the promotion/fly-off spike (**dormant, verified**
— decision 5; the seed's fly-off phase never draws, same resolution as the F5J
witness).

## Why it matters

The withdrawn claim is exactly the failure mode the parallel-run discipline
exists to catch — a predicted difference that curation measurement dissolved.
The re-triage is the honest version: run the pair, witness what actually
differs, and let each candidate either materialise or join the
unwitnessed-candidate disclosures. Here **three of the four candidates
materialise**, including the first sighting of the HalfUp-vs-Truncate
normalisation split, and the pair delivers the parallel-run claim's product at
scale: a 20-pilot placing split the club would have seen on the day. It is
also the first pair whose **raw grain itself splits** (ales and christchurch
were raw-exact) — the ledger's raw grain carries witnessed entries for the
first time, exercising the comparator's raw walk against a non-empty triaged
set.

## Settled decisions (2026-09-10, proposed — owner may veto any before WI-1)

1. **Ledger shape: class entries + count pins + fully-enumerated ranking
   grain** (the christchurch decision-4 shape). The ales discipline
   ("witnessed entries name their cells") would make this ledger ~300
   hand-authored entries; instead: **three wildcard raw entries** (one per
   difference class, `pilotNo: "*"`, kind 1), **two wildcard normalised
   entries** (the rounding grid; the composition/winner-shift cascade), and
   the **ranking grain fully enumerated per pilot** (20 entries, GS rank vs
   seed-run placing, each cited). The scenario pins the exact computed counts
   per grain (raw 54, normalised 264) — the wildcards cover any cell, so the
   count is what makes each entry exact.
2. **The landing-0 sentinel is captured verbatim and triaged — never mapped
   away.** `row.Landing` is submitted as recorded (0.0 included); the seed's
   table then awards 100 where GS awards 0. The alternatives are refused:
   skipping the capture is impossible (the seed declares `landingDistance` a
   demanded observation with **no** `whenNotRecorded` — an uncaptured flight
   runs Pending forever), and rewriting 0.0 to ">15 m" would be a
   harness-authored interpretation of the foil's sentinel — tuning, not
   mapping. Triage kind 1: the club's record convention (0.0 = no bonus) vs
   the rulebook's measurement semantics (a true 0.0 m nose-to-spot distance
   scores 100 per `F3J.10.5`'s first band). The seed is rulebook-faithful;
   nothing is fixed.
3. **The −30 payload is not mapped — the offences differ (`fai-rules`
   answered in planning).** All 11 `FlightScoreDeduction=30` rows carry flight
   times of 182–596 s, **inside** the 600 s working time, so `F3J.10.3`'s
   overfly offence (flying past the end of working time) cannot describe them;
   GS's `durFlightPenalty=1` late-landing scheme is a local GS construct the
   rulebook does not state. Mapping the payload onto the seed's overfly
   conditional would misauthor the rulebook (the mapping policy fixes
   *values*, not offences). The harness captures nothing for the column under
   this seed (the capture gate reads the published definition —
   `lateLandingDeduction` is not declared), so the seed-run scores those rows
   +30. Triaged kind 1, disclosed in provenance, never reconciled.
4. **The R1 540 target is never bound under the seed — and the parity
   round-bind refusal guard widens to say so explicitly.** The fixture is in
   `ReplayDriver.RoundParameterBindings` (parity-only: `targetTime` R1 → 540),
   and parallel-run mode refuses any fixture in that map
   (`ReplayDriver.cs:616-622`) — correct in spirit (binding GS-oracle
   knowledge under a seed would tune the seed to GS) and here in **letter**
   too: the seed declares no `targetTime` parameter at all, so there is
   nothing to bind. WI-1 adds an explicit per-pair skip set (decision 6)
   so the pair proceeds with **no** binds while the 540 knowledge stays
   parity-only and the decay difference is triaged kind 1.
5. **No scored window, no tape, no seed change, no `src/` change.** All 16
   rounds are scored (`MAX(RoundNo where Updated='True')` = 16 = the full
   fixture) — no `ParallelRunScoredWindowRounds` entry. Landings are
   entered distances (`durLndg=2`, scheme values 0.2–14.0 m) — the G5
   distance path, no `ParallelRunLandingTapes` entry, no instrument
   declaration. `50-f3j.json` is already rulebook-faithful (landing table,
   drop threshold, fixed working time, Truncate-0.1 normalisation all cited);
   WI-1 confirms it round-trips and changes nothing. No core code is touched.
6. **Parameter binds: one bind, one disclosed skip.** The seed declares three
   CompetitionSetup parameters: `flyoffSize` (defaulted 9 — the defaulted
   arm binds nothing, christchurch decision-8 precedent), `flyoffMinRounds`
   (**no default** → bound **1**, the least-constraining rulebook-consistent
   choice — F3J.11 states no fly-off minimum (F12); the ales
   `minNewGroup=1` precedent; never exercised — the fly-off never draws) and
   `carryPenalties` (**no default, Flag kind** → **not bound**: a Flag bind
   would need the decimal-typed bind pipeline widened for a parameter that is
   never read — the fly-off phase never draws, so promotion's
   `CarryPenalties` never resolves; F3J states nothing about carry-over
   (F12)). `DeriveParallelRunBindings` gains an explicit known-unexercised
   skip arm for `carryPenalties` (loud comment naming this story), and the
   ledger's provenance notes disclose the choice. Unbound-but-never-read is
   safe: the engine resolves parameters per use (`parameterUnbound` at
   prescription/finalise — `Competition.cs:1620/:2346`), and phase-2's
   parameters are never reached.
7. **Phantom exclusion: a provenance-level declared scope.** R1 carries GS's
   phantom group 5 — five all-zero rows (pilots 7, 11, 17, 39, 56) whose
   pilots also fly real flights in the same round; the draw derivation keeps
   the real flight and drops the phantoms (the parity `divergences.json` D5
   entries), so those 5 oracle cells are never replayed or compared. The
   parallel ledger is a different contract from `divergences.json` (never
   merged), and a never-compared cell is not a difference — so the exclusion
   is **declared scope in provenance**, exactly the christchurch
   scored-window discipline: `ParallelRunProvenance` gains
   `ExcludedOracleCells` (null-tolerant; round/group/pilot-or-"*"/reason),
   the comparator's coverage universe excludes declared cells, and
   `CheckProvenance` verifies each declared cell exists in the oracle
   (anti-typo, anti-silent-shrink). The ales/christchurch ledgers carry none
   and deserialise unchanged.
8. **Scenario shape: reuse the three generic steps verbatim, add one count
   step.** The f3j scenario reuses `the parallel-run verdict is exactly the
   triaged differences`, `the final placings split from the GliderScore
   oracle exactly as the ledger triages` and `every ledgered difference is a
   triaged rulebook-vs-local-practice difference with a citation` unchanged;
   christchurch's `the raw grain is exact…` step does **not** apply (the raw
   grain splits here — that is the product). One new step:
   `the witnessed split counts match the ledger's pins` — raw and normalised
   computed counts equal the ledger's pinned counts and both grains are
   non-empty (mirroring christchurch's pin parsing; the existing
   `PinnedNormalisedCount` helper and its `Pinned normalised-cell count: N`
   marker are reused; a parallel `Pinned raw-mismatch count: N` marker is
   added). The ales scenario is untouched.

## Verified ground truth (2026-09-10, planning simulation — re-verify counts at curation)

Measured by simulating both sides over the fixture; **re-pin every count from
the measured run in WI-2** (float-repr arithmetic could shift a borderline
cell by ±1; the engine's decimal arithmetic is authoritative).

**Fixture** (`tests/GliderscoreFixtures/f3j-international/`, comp 1):

- 30 pilots; 16 scored rounds × 4 groups; 485 rows = 480 real (464 flown +
  16 zero-time flight-less) + 5 phantom R1/G5 rows. Post-dedup group sizes
  are 6–8 — the seed's `minPerGroup: 6` (SHOULD, `F3J.6.1`) is met
  everywhere; no warning fires (no christchurch R5 repeat).
- No re-flights (`OriginalRoundNo == RoundNo`, `ReFlightNo` 0 everywhere);
  `Penalty` 0 everywhere (G4 moot); `LandingOver75m` false everywhere (the
  seed's `restedWithin75m` whenNotRecorded-true never contradicted); no
  per-fixture harness maps beyond `RoundParameterBindings`.
- 11 rows carry `FlightScoreDeduction=30`: (R1G1P44, R5G4P7, R6G3P2, R6G4P63,
  R10G3P2, R12G2P63, R13G1P42, R13G4P54, R14G2P25, R14G3P44, R14G4P7) — all
  with `Landing=0.0`, all times ≤ 596 s.
- 34 flown rows carry `Landing=0.0`; 22 R1 rows carry t > 540 s; max decoded
  flight 599 s (nobody overflew the 600 s working time — the seed's
  `overflySeconds`→0 assumption is also what the foil's data implies).
- Teams: `UseTeams=true` with 8 teams (sizes 4,3,4,4,4,3,4,4) — populated
  honestly but reaching no persisted score (provenance
  `triageJustification.teams`); the parallel comparator's deliberate
  no-team-grain stance holds (the ales precedent).

**GS arithmetic** (verified cell-exact over all 485 oracle rows, 0
mismatches): raw = flight points + landing award − `FlightScoreDeduction`,
where flight points = t for t ≤ target else target − (t − target) with target
540 in R1 and 600 in R2–16, and the GS landing award for a recorded 0.0 is
**0** (exact-match miss on the scheme's rows, which start at 0.2 m).
Normalisation = HalfUp-1dp of `1000·Raw/max` over the group (verified 0
mismatches); zero rows normalise to 0.0. Drop: exactly one worst cell per
pilot wherever it sits (`DropScoreOption=0`, `Drop1AtRound=8`, 16 ≥ 8);
aggregate = sum of 15; sort Score DESC then RawScore DESC; display ties `=n`
(no aggregate ties exist on this oracle — every rank distinct).

**Seed-run arithmetic** (50-f3j, simulated): raw = min(t, 600)·1 +
seedLookup(Landing) — the rate term capped PerFlight at 600, no −30
(overflySeconds unrecorded → 0, so the `F3J.10.3` conditional never fires and
the landing conditional's gate passes), and `seedLookup(0.0) = 100` (first
band `upTo: 0.2`). Normalisation = **Truncate**-1dp of `1000·raw/max` per
`F3J.10.11`. Drop: ByRound, count 1, `applyWhenRoundsCompletedAtLeast: 8`
(16 completed — fires), `tieBreak: "Latest"` (equal-lowest ties drop the
latest — aggregate-neutral, never exercised differently here: no
aggregate-relevant drop ties).

**Measured difference set** (provisional pins):

| grain | count | decomposition |
|---|---|---|
| raw | 54 | 22 R1-decay (20 pure + 2 sharing cells below) · 34 landing-0 sentinel (22 pure + 10 with deduction + 2 with decay + 1 with both) · 11 deduction (all ⊂ sentinel cells) |
| normalised | 264 | 54 raw-carrier cells · 90 winner-shift cascade cells (10 groups: R1G1, R1G2, R1G3, R5G4, R6G3, R7G2, R8G1, R13G1, R13G3, R15G4) · 174 pure rounding-grid cells |
| ranking | 20 moves | fully enumerated at curation; planning expectation: p54 6→3, p13 8→4, p56 3→5, p21 4→6, p47 5→7, p26 7→8, p12 11→9, p30 12→10, p17 9→11, p22 10→12, p25 17→16, p42 19→17, p29 16→18, p2 24→19, p50 18→20, p32 20→21, p52 21→23, p64 23→24, p39 26→25, p43 25→26 (all others hold) |
| excluded | 5 cells | oracle keys `1/1/5/0/{7,11,17,39,56}` — the D5 phantom group, declared scope, never compared |

Compared universes: raw 480 cells, normalised 480 cells, ranking 30 pilots
(485 oracle keys − 5 declared exclusions). No GS-side and no seed-side
aggregate ties exist in the simulation — tie-group membership lines should
compare exact; if the measured run contradicts that, enumerate what it
actually shows (the escalation law governs).

**Harness as-built facts the pair reuses, never re-shapes** (re-verify line
refs before relying on one):

- `DeriveParallelRunBindings` (`ReplayDriver.cs:348-388`): the switch's
  unknown-no-default arm throws — the two new arms land beside it (WI-1).
- The parallel round-bind refusal (`ReplayDriver.cs:616-622`) fires for this
  fixture — the skip set lands there (WI-1).
- The capture gate reads the published definition
  (`ReplayDriver.cs:683`); `CaptureDurationInputs` captures `flightTime`
  (always for a flown slot) and `landingDistance` (always, including 0.0,
  no instrument); `lateLandingDeduction`/`launchHeight`/`startHeight` arms
  are definition-gated and this seed declares none of them — nothing new to
  capture.
- `ParallelRunComparator`: raw walk as-produced
  (`CompareRawGrainAsProducedAsync`), coverage universe
  (`ParallelRunComparator.cs:239-246` — widens for exclusions in WI-1),
  `CheckProvenance` (`:353+`).
- `ParallelRunSteps`: `PinnedNormalisedCount` parses
  `Pinned normalised-cell count: N` (`ParallelRunSteps.cs:~245`) — reused by
  the new count-pins step; the verdict/split/citation steps are reused
  verbatim.

## Before starting

- **fai-rules answers (settled in planning; re-confirm citations with the
  skill before they enter the ledger):**
  - *Overfly vs late-landing:* answered — decision 3. Cite `F3J.10.3`
    (`docs/rules/f3j.md:34`) for the overfly offence and
    `competition.json familyRows.Dur.durFlightPenalty` = 1 +
    `provenance.json` note ("11 rows carry FlightScoreDeduction=30") for the
    local scheme.
  - *Drop threshold:* the withdrawal re-cited — `F3J.3.1 a`
    (`docs/rules/f3j.md:99`), seed `50-f3j.json`
    `phases[0].drops[0].applyWhenRoundsCompletedAtLeast` = 8, GS
    `Drop1AtRound` = 8. The drop **agrees**; the ranking moves come from the
    score cells, not the drop policy.
  - *Normalised precision:* `F3J.10.11` states "recorded (truncated) to one
    place after the decimal point" (`docs/rules/f3j.md:86`) — the seed's
    Truncate-0.1 is the cited rulebook position and GS's HalfUp-1dp
    (`RoundOrTruncate=0`) is the local grid. (Unlike christchurch, where the
    rulebook was silent, here the rulebook speaks — the citation is stronger.)
  - *Landing table:* `F3J.10.5` (`docs/rules/f3j.md:38-55`) — first band
    "up to 0.2 → 100", no 0-row, "over 15 → 0".
- **Board check:** `kanban/in-progress/tape-points-landing-seeds.md` is
  in-progress but owns nothing here (no tape, no instrument, no composition —
  G5's distance path); `kanban/in-progress/operational-tie-break-resolution.md`
  is in-flight on `master` with uncommitted `src/` work — this story is
  planned against committed `master` (596bede), touches no `src/` code, and
  must not build on that story's uncommitted state; the seed has no phase-1
  tie-breaks and the pair shows no aggregate ties, so the two stories do not
  collide.
- **House rule 2 cross-reference (done at planning):** `docs/users.md`'s
  "parallel" is role separation — unrelated; NFR-1/NFR-2 are *supported*
  (the seed expresses the comp faithfully; every difference is the club's
  local practice, not a model gap); no new domain concepts — "sentinel",
  "excluded oracle cell" and the count pins are harness/corpus vocabulary
  (the "record scenario", "parallel-run ledger" and scored-window
  precedents); nothing in `/docs` changes (house rules 3–4).

## Plan

### WI-1 — Harness widening (`tests/Soarscore.Acceptance.Tests`)

All additive and per-pair-keyed; the parity path and the ales/christchurch
pairs are inert proofs (the full suite stays green unchanged except the new
scenario, which lands in WI-2).

1. **Binding arms** in `DeriveParallelRunBindings`
   (`ReplayDriver.cs:348-388`):
   - `"flyoffMinRounds"` → `1m` — bound before `/prescribe-draw`
     (CompetitionSetup, no default). Derivation text for the ledger: F3J.11
     states no fly-off minimum (F12 — silence is a parameter); 1 is the
     least-constraining rulebook-consistent choice (the ales
     `minNewGroup=1` precedent); never exercised — the fly-off never draws.
   - `"carryPenalties"` → **skip, loudly documented**: an explicit
     known-unexercised arm (checked before the unknown-parameter throw) that
     binds nothing and carries a comment naming this story and decision 6
     (Flag kind; the decimal-typed bind pipeline would need widening for a
     parameter that is never read — the fly-off phase never draws, so
     promotion's `CarryPenalties` never resolves; F3J states nothing (F12)).
     The ledger's provenance notes disclose it; the binding block stays
     what was actually bound.
   - `flyoffSize` already falls through the defaulted arm — no change.
2. **Round-bind skip set** at the refusal (`ReplayDriver.cs:616-622`): a
   `ParallelRunSkipParityRoundBinds` set `{"f3j-international"}` consulted
   before throwing; a skipped fixture binds nothing from
   `RoundParameterBindings` (parity unchanged) and the comment cites this
   story's decision 4 (the seed has no `targetTime` parameter; the R1 540
   knowledge is triaged, never applied).
3. **Ledger schema widening** (`ParallelRunLedger.cs`): new record
   `ParallelRunExcludedCell(int Round, int Group, JsonElement? PilotNo,
   string Reason)` and `ParallelRunProvenance.ExcludedOracleCells`
   (`IReadOnlyList<ParallelRunExcludedCell>?`, null-tolerant — the ales and
   christchurch ledgers deserialise unchanged).
4. **Coverage-universe exclusion** in `ParallelRunComparator`: after the
   scored-window filter, remove oracle keys whose parsed
   (round, group, pilot) is covered by a declared excluded cell (pilot
   number or `"*"`; the key format is
   `{TaskNo}/{RoundNo}/{GroupNo}/{ReFlightNo}/{PilotNo}`). Applied to the
   universe **before** `EnsureOracleCoverage` walks it, for both grains.
5. **`CheckProvenance` widening**: every declared excluded cell must match
   ≥1 actual oracle key (parse the keys; a declared cell matching nothing is
   a typo — break loudly). The reason string rides unverified (the ledger
   review covers its honesty), exactly like provenance notes.

**Done-when:** the full acceptance suite is green unchanged on sqlite (the
new scenario lands in WI-2) — proving every widening inert for the ales and
christchurch pairs on the grains their scenarios exercise.

### WI-2 — Ledger, scenario, measured-first run

1. **Author the ledger pre-argued spine**
   `tests/GliderscoreFixtures/f3j-international/parallel-run/50-f3j.json`:
   - `pair`: (`f3j-international`, `50-f3j`); `seedClass`: "RC Thermal
     Duration Gliders" / "FAI F3 Soaring 2025 ed.2".
   - Provenance: **no** `scoredWindowRounds`; `parameterBindings` =
     `[{flyoffMinRounds, 1, derivation}]` (decision 6 text);
     `metricMappings` = the three seed-declared `whenNotRecorded`
     disclosures — `overflySeconds` → 0 (`F3J.10.3`/`F3J.10.4`; the
     exception-recording policy P1; corroborated: max decoded flight 599 s),
     `touchedByCompetitor` → false (`F3J.10.8`), `restedWithin75m` → true
     (`F3J.5.1 e`) — each "seed-declared, engine-resolved, the harness emits
     nothing"; **no** `derivedMetrics`; **no** `declaredInstruments`;
     `excludedOracleCells` = the five D5 phantom cells
     (`round 1, group 5, pilotNo "*", reason` citing the dedup
     (`ReplayDriver.cs:1005-1029`) and GS's best-per-original-round
     aggregation, `provenance.json` phantom note); `notes` = the
     unwitnessed/inert list — the withdrawn drop claim (thresholds agree,
     `F3J.3.1 a`), the −30 offence distinction (decision 3), the teams
     reach-no-persisted-score note, the `=n` tie-display semantics vs the
     engine's exact-decimal ties (inert — no aggregate ties, but a rounding-
     cascade disclosure), the unexercised parameters (`flyoffSize` defaulted
     9, `carryPenalties` unbound per decision 6, `flyoffMinRounds` bound but
     never read), and the R1 540 target's oracle-reconciled provenance.
   - `triagedDifferences` (the pre-argued spine — counts re-pinned from the
     measured run before the scenario goes green):
     1. raw wildcard, kind 1 — **landing-0 sentinel**: GS records "no
        landing bonus" as 0.0 and its exact-match scheme awards 0; the
        rulebook table's first band scores a recorded 0.0 at 100. Citations:
        local practice — `competition.json lookups.landingSchemes[0]`
        (scheme 2 exact-match) + the fixture-authored definition's explicit
        `upTo: 0 → 0` row (`class-definition.json`) + the ReplayDriver
        capture comment; rulebook — `F3J.10.5`. Carries
        `Pinned raw-mismatch count: 54` (the designated raw pin).
     2. raw wildcard, kind 1 — **the −30 late-landing deduction**: 11 rows,
        GS deducts the payload pre-normalisation, the seed has no such term;
        the offences differ (`F3J.10.3` overfly vs GS's
        `durFlightPenalty=1` scheme) so the payload is not mapped
        (decision 3).
     3. raw wildcard, kind 1 — **R1 over-target decay**: GS scored R1
        against 540 s with linear decay (22 flights over target;
        oracle-reconciled, `ReplayDriver.cs:185-203`); the seed's fixed
        600 s working time (`F3J.6.2 b`) with 1 pt/s capped 600
        (`F3J.10.1 c`) has no decay clause.
     4. normalised wildcard, kind 1 — **the rounding grid**: GS HalfUp-1dp
        (`GroupScoreDecimals=1`, `RoundOrTruncate=0`) vs the rulebook's
        Truncate-0.1 (`F3J.10.11`; the seed's `normalise.round`). Names its
        witness cell (`1/1/4/0/11`: exact 715.986… → GS 716.0 vs seed
        715.9). Carries `Pinned normalised-cell count: 264` (the designated
        normalised pin, parsed by the existing helper).
     5. normalised wildcard, kind 1 — **the composition/renormalisation
        cascade**: the 144 non-rounding normalised mismatches — cells
        carrying a raw-mismatched value (54) and cells whose own raw is
        unchanged but whose group winner shifted (90, the 10 groups listed
        in ground truth). Cites the three raw classes as its causes.
     6.–25. **ranking, per pilot** — the 20 moves, enumerated from the
        measured run (step 3), each: GS rank vs seed-run placing, the
        contributing classes (sentinel +100s dominating; the −30 absence;
        the R1 decay; the rounding cascade), the drop-agreement citation
        (`F3J.3.1 a`; GS `Drop1AtRound=8` == the seed's
        `applyWhenRoundsCompletedAtLeast: 8` — the withdrawn claim), and the
        `tieBreak: "Latest"` arm note where a pilot's dropped round ties.
   - **Anti-goal guard:** the ledger is authored from the rulebook-vs-GS
     reading above and the measured run — never re-authored to make a
     failing comparison pass (the escalation law).
2. **Add the scenario** to `Features/ParallelRunningAGliderscoreFixture.feature`
   (the `@gliderscore` feature) and one step to `Steps/ParallelRunSteps.cs`:

   ```gherkin
   Scenario: The f3j-international parallel run under the F3J seed class reports exactly the triaged differences
     Given the fixture corpus manifest
     When the harness parallel-runs the GliderScore fixture "f3j-international" under the seed class "50-f3j"
     Then the parallel-run verdict is exactly the triaged differences
     And the final placings split from the GliderScore oracle exactly as the ledger triages
     And the witnessed split counts match the ledger's pins
     And every ledgered difference is a triaged rulebook-vs-local-practice difference with a citation
   ```

   The reused steps are verbatim (their christchurch-specific failure-message
   wording is tolerated — the assertions are grain-generic). The new
   `the witnessed split counts match the ledger's pins` step asserts: raw
   computed mismatches **non-empty** (the sentinel split is this pair's
   raw-grain product — an empty raw grain is a loud failure) and equal to
   the ledger's `Pinned raw-mismatch count`; normalised computed count equal
   to the ledger's `Pinned normalised-cell count` (via the existing
   `PinnedNormalisedCount` parser over the designated entry). The ranking
   count needs no pin — its grain is fully enumerated per pilot, and the
   split step already asserts coverage in both directions.
3. **Measured-first curation:** run `@gliderscore` on sqlite with the
   pre-argued spine — the verdict will be Mismatch until the ranking grain
   is enumerated. Read the report: enumerate the ranking-grain mismatches
   into per-pilot entries (the planning expectation lists 20 moves with no
   ties — verify against the run and record what it actually shows, moving
   the per-pilot entries to the measured set); re-pin the raw and normalised
   counts; confirm the three raw classes and the two normalised classes
   decompose exactly as pinned (a class with no cells is an unwitnessed
   candidate → provenance note, never an entry); confirm the 5 exclusions
   held. Re-run to green. **Escalation law:** a computed difference outside
   the triaged set after curation, or a triaged difference that fails to
   appear, is triaged kind 1/2 with citations or escalated kind 3 as a
   defect — never a ledger edit to fit.

**Done-when:** the f3j scenario passes on sqlite; the verdict is
`MatchesTriagedSet` (Exact is a loud failure — a ledgered pair with a
non-empty triaged set); raw 54 / normalised 264 / ranking 20 as measured
(re-pinned); every entry kind ∈ {1} with a citation.

### WI-3 — Full verification

1. `@gliderscore` and the full acceptance suite on **both stores**
   (`SOARSCORE_TEST_STORE=sqlite` fast loop; postgres via Testcontainers) —
   a backend Soarscore claims to support passes this suite unchanged.
2. Domain, Application, Architecture suites green (no-core-change proof —
   the story touches no `src/` code).
3. Corpus discipline: **no corpus file changes** — the ledger and scenario
   are additions beside the fixture; `validate.py --index` untouched (11/11);
   `Corpus.ExpectedCount` stays 16; the parity path's
   `divergences.json`, `RoundParameterBindings` and every existing scenario
   are byte-unchanged.

### WI-4 — Board reconciliation

1. **Mapping-table flip** (the update contract,
   `tests/GliderscoreFixtures/parallel-run-mapping.md`): the
   `f3j-international` row → `done`; `why` folds the measured outcome
   (the rounding grid materialised — 174 cells; the landing-0 sentinel — 34;
   the −30 absence — 11; the R1 540 decay — 22; a 20-pilot placing split;
   5 declared phantom exclusions; drops agree — the original claim stays
   withdrawn). Seed coverage: the `50-f3j.json` line records
   f3j-international (witness, done) alongside f3j-international-flyoff
   (planned) and the jerilderie refusal (unchanged).
2. Board reconciliation: `tech-debt.md` / `deferred-decisions.md` only for
   what the run actually surfaced (expected: nothing new — the fly-off
   promotion/tie-break dormancy deferral already stands in
   `deferred-decisions.md` §Draw; the `carryPenalties` unbound choice is
   ledger provenance, not a deferred decision). Move this story to
   `completed/` with `git mv`, set the status header; `graphify update .`.

## Testing approach

- **Pair comparator remains example-driven.** The product is set-equality
  between a computed difference set and a curated triaged set, per
  hand-curated pair; the invariant ("the report is exactly the triaged
  differences") is structural and example-asserted per pair. No new CsCheck
  property: generating class/fixture mutations would test the comparator,
  not the product.
- **The genuine invariants stay where they are.** The rounding-grid
  behaviour (normalise truncation per the class definition) and the drop
  mechanics are engine-level and covered by the existing scoring corpus
  tests; the pair witnesses them on real data rather than re-proving them.
- **What each failure must name:** an untriaged raw mismatch → investigate
  the capture or the seed's score terms (authoring bug or kind-3,
  escalate); a normalised count off its pin → re-triage; a ranking mismatch
  outside the enumerated set → re-triage or kind-3 escalation with the full
  `Render()`; a provenance break → the run did not run under the disclosed
  bindings/exclusions.

## Scope guards and standing constraints

- **Anti-goal, stated hard:** the seed classes are never tuned to GS. The
  seed is published as authored; the 540 target is never bound; the sentinel
  is never mapped away; the −30 payload is never synthesised. Every
  difference is triaged kind 1 with citations — reported, never reconciled.
- **Ledger semantics:** the parallel-run ledger is a different contract from
  the fixture divergence ledger — distinct schema, distinct comparator,
  never merged. The phantom exclusion is provenance (declared scope), never
  a triaged entry (a triaged difference that fails to appear fails the
  scenario); unwitnessed candidates live in provenance notes, never as
  entries.
- **No `src/` changes, no seed changes.** A contradiction found in the run
  escalates as a finding, not a local core or seed implementation. Any
  genuinely new feature found during implementation becomes a backlog stub
  under house rule 6 — never silent scope growth.
- **Regression proof:** the ales and christchurch pairs are inert proofs.
  Every widening is per-pair-keyed or null-tolerant; the full suite green on
  both stores is the discipline.
