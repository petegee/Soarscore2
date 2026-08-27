# Story — Harness replay scenarios for the five NZ fixtures

**Status:** In progress · **Raised:** 2026-08-27 · **Planned (fleshed out):** 2026-08-28 ·
**Implementation started:** 2026-08-28 ·
Parent: `kanban/completed/grow-corpus-nz-master-five-fixtures.md` (WI-4 carve-out)

## What

Extend the GliderScore replay/compare harness so its scenarios actually replay
the five NZ fixtures (`f5j-christchurch-2019`, `f5j-hawkes-bay-trials`,
`f3k-southern-fling`, `f5j-nz-south-island`, `f3k-june-2020`) end-to-end against
their reconstructed-ladder oracles. WI-4 proved only that the harness consumes
them via the manifest path (`FixtureLoader.ActiveSlugs`); each needs a
per-fixture `class-definition.json` + scenario before replay/compare covers it.

This plan is written for **parallel sub-agent dispatch**: §Verified ground truth
below pins every data-derived fact (each one re-proved cell-exact against the
committed oracles during planning, so implementers must not re-derive or
relitigate them), and the work items are partitioned so that only WI-1 touches
shared files.

## Why it matters

The five fixtures are exactly what the corpus was missing: first active F5J
family witnesses (incl. the float32 persist-cast cast-residue property and the
−2026→norm-0 clamp), four re-flight cells beyond jerilderie's single witness,
first F3K multi-group per-group normalisation, a mid-comp group re-draw, and a
motor-restart-effect pairing. Until they are replayed, none of that exercises
the engine.

## Before starting

- **Baseline (WI-1's first action):** run the suite once before touching
  anything — `SOARSCORE_TEST_STORE=sqlite dotnet test
  tests/Soarscore.Acceptance.Tests` (avoids Docker) — and record the result.
  Parent as-built recorded 52/52 green on sqlite.
- Read the parent story's *As built* section. Several of its caveats are now
  **resolved with evidence** in §Verified ground truth — notably: comp 17's
  "phantom Landing" is not noise (it is task D's sixth ladder slot), comp 54's
  "five decode deviants" are fully explained by task H's clamp arithmetic, and
  comp 45's committed data is 18 rounds with 12–18 wholly unflown (not "1–11
  ragged partial groups"). Do not re-implement around the stale wording.
- `kanban/backlog/reflight-aggregate-destination.md` stays backlog. Comp 135
  replays under the jerilderie WI-6 mapping (a) precedent (D1 below); nobody
  implements the engine concept here.
- The oracles (`expected-scores.json`, `expected-result.json`, `ladder.py`) are
  frozen outputs of the parent story's WI-2/WI-3. Never edit them; any
  unexpected mismatch is a stop-and-triage event (D7), never a ledger-stretch.
- Housekeeping: nothing in `/docs`; no glossary concepts (everything here is
  harness-side); new knowables → `tech-debt.md` or backlog stubs, never silent
  scope growth.

---

# Verified ground truth (planning-time 2026-08-28)

All numbers below were recomputed from the committed fixture files (not from
prose) and reproduced the oracles cell-exactly. Cite this section, not the
parent story's prose, where the two disagree.

## Per-fixture shape (as committed)

| Fixture | CompNo | Class | Pilots | Rounds | Groups/round | Rows | Oracle keys | Peculiarities |
|---|---|---|---|---|---|---|---|---|
| f5j-christchurch-2019 | 45 | F5J | 18 | 1–18 | 3 (sizes 5/6/7 in flown rounds) | 324 | 324 | Rounds 12–18 **wholly unflown** (126 all-zero cells); 11 flown rounds; GroupScoreOption **effective 1**, Decs **effective 1**; `F5JMotorRestartOption` null; 22 rows carry `F5JMotorReStarted` flag (inert, see below) |
| f5j-hawkes-bay-trials | 135 | F5J | 18 | 1–16 | 3 | 288 | 288 | Four re-flight rows, **all pilot 128** (orig 1/2 in R5, orig 4/3 in R6; `ReFlightNo=0`, identified by `OriginalRoundNo≠RoundNo`); pilot 128 absent R1–R4 (his 4 make-ups replace them); P128 also holds ordinary R5/G1 and R6/G2 rows; R11–16 largely unflown placeholders; `F5JMotorRestartOption=1` + 1 flagged all-zero row (R6/G3 P100) |
| f5j-nz-south-island | 121 | F5J | 13 | 1–16 | 3 | 208 | 208 | **No re-flights** (parent plan correction stands); one negative-raw cell `1/3/3/0/99` (raw −2026.0, NS 0.0); `F5JMotorRestartOption=1` + 1 flagged all-zero row (R6/G1 P131) |
| f3k-southern-fling | 17 | F3K | 15 | 1–15 | 3 | 218 | 218 | 12 distinct GS task codes; pilot 89 `Retired=true` after R8 — absent R9–R15 (7 missing slots); groups 5/5/4 from R9 |
| f3k-june-2020 | 54 | F3K | 15 | 1–13 | 3 (R7 has **4**) | 199 | 199 | TaskNo is **5** (not 1); R7 re-draw: 4 pilots (101, 102, 128, 85) cancelled in G3 (all-zero) and re-flown in new G4; `f3kDecimalsForTiming=1` (tenth-second inputs — 124 fractional packed cells) |

`CompDate` is **empty or null in all five** `competition.json` files — the
driver's `DateOnly.Parse(CompDate.Split(' ')[0])` (`ReplayDriver.cs:150-151`)
throws on every one of them. WI-1 fixes this (D5 item 1).

## F5J arithmetic — proven cell-exact (820/820 across comps 45/135/121)

With `H = Scores.FlightScoreDeduction` (launch height, metres), `T` = packed
mmss decode of `Time1Mins` (`Fix(v/100)·60 + v − 100·Fix(v/100)`), `L` =
`Landing`, scheme-11 exact-match lookup (0 off-table/zero):

```
RawScore      = min(T, 600) − (H ≤ 200 ? 0.5·H : 100 + 3.0·(H − 200)) + Lndg(L)
NormalisedScore = max(0, RoundHalfUp1dp(1000 · RawScore / groupMaxRaw))     (groupMax > 0; else 0)
```

- The time curve is a **cap** at target 600 (`durFlightPenalty=3` ⇒ `varFltDednIdx=3`,
  and F5J ∉ {DurALES, F3G}, so GS caps instead of decaying — arithmetic story,
  Duration curve + `GetTimeScore` Case 1). It is NOT the symmetric decay the
  F3J fixtures author.
- RawScore is **unfloored** — comp 121's −2026 persists (see worked example).
  Our engine composes negative raws unfloored too: `PenaltyEngine` floors only
  when raw-stage penalties exist (`PenaltyEngine.cs:118`) and there are none here.
- NormalisedScore **is floored at 0 by GS** (option-1 branch, arithmetic story
  Normalisation matrix `:310`). Our `NormalisationEngine` has no lower clamp
  (`NormalisationEngine.cs:121`) → the one negative cell diverges → ledgered
  under new citation token **N1** (D2). Exactly one cell corpus-wide:
  `1/3/3/0/99` — worked example: T=714.0→434 s; H=1000 → 100+2400 = 2500;
  L=90 → 40; raw = 434 − 2500 + 40 = **−2026** ✔; GS NS = max(0, …) = **0.0** ✔.
- Scheme-11 (`LndgNo=11` "F5J Enter Landing", committed in all three
  competition.json): exact-match distances 100→50, 99→50, … 96..91→45,
  90→40, 85→35, 80→30, 75→25, 70→20, 65→15, 60→10, 55→5; 0 and off-table → 0.
  All non-zero landings in all three fixtures are on-table (parent WI-2
  validation rule 2 held), so a banded `LookupTerm` with a leading
  `upTo: 0 → 0` row is exact on this data (jerilderie D3 precedent).

## F3K task catalogue — proven cell-exact (417/417 across comps 17 and 54)

GS's `CalcRawScoreF3K` semantics per task code, decoded from the slots and
**verified against every oracle RawScore and NormalisedScore cell of both
fixtures** (218 + 199 = 417/417 exact, including every NS on the 1-dp
half-up grid with the ≥0 floor and zero-max guard — both no-ops here since no
F3K raw is negative and no group is all-zero). The VB source is not available
on this machine; **the committed oracles are the proof of record.**

| Code | Slots read (ScrArr order) | Proven arithmetic | Class-definition shape |
|---|---|---|---|
| A(2) | Laps | 1 flight, cap 300 | `flights last`, maxLaunches 1, score `rate 1 cap 300` |
| B(1) | Laps, Time1Mins | 2 flights, uncapped sum (182 > 180 uncapped ⇒ no cap) | `all`, maxLaunches 2, `rate 1` |
| B(2) | Laps, Time1Mins | 2 flights, uncapped | `all`, 2, `rate 1` |
| C(1) | Laps, Time1Mins, Time1Secs | 3 flights, cap 180 (never bites; AllUp-family convention per C(3) precedent) | `all`, 3, `rate 1 cap 180` |
| D | Laps, Time1Mins, Time1Secs, Time2Mins, Time2Secs, Landing, FlightScoreDeduction | positional ladder, caps 30/45/60/75/90/105/120; Landing and Deduction slots are flight times (comp 17 R9: Landing 145→105 vs the 105 target on 12/14 rows, Deduction 200→120 on 6) | `exactlyN 7, targets InOrder [30,45,60,75,90,105,120]`, `rate 1` (existing) |
| D(1) | Laps, Time1Mins | 2 flights, uncapped | `all`, 2, `rate 1` |
| E | Laps, Time1Mins, Time1Secs, Time2Mins, Time2Secs | uncapped sum (old E — no 2024 window reduction) | `all`, 5, `rate 1` |
| E(1) | Laps, Time1Mins, Time1Secs | 3 flights, uncapped | `all`, 3, `rate 1` |
| F | Laps, Time1Mins, Time1Secs | 3 flights, cap 180 | `all`, 3, `rate 1 cap 180` (existing) |
| G | Laps, Time1Mins, Time1Secs, Time2Mins, Time2Secs | per-slot cap 120 (44/44 incl. rows above 120) | `all`, 5, `rate 1 cap 120` (existing) |
| H | Laps, Time1Mins, Time1Secs, Time2Mins | **sort the slot values descending, then positional caps 240/180/120/60** (44/44; e.g. comp 17 R5 P76 slots [221,60,120,166] → sorted [221,166,120,60] → 567) | `exactlyN 4, targets InOrder [240,180,120,60]`, `rate 1`; **the driver sorts desc before assigning flights** (D5 item 3) |
| I | Laps, Time1Mins, Time1Secs | 3 flights, uncapped (200s witnessed) | `all`, 3, `rate 1` |
| J | Laps, Time1Mins, Time1Secs | 3 flights, uncapped | `all`, 3, `rate 1` |
| K | Laps, Time1Mins, Time1Secs, Time2Mins, Time2Secs | positional caps 60/90/120/150/180 (comp 17 P81 R1 slots [47,49,43,0,180] → 47+49+43+0+180 = 319) | `exactlyN 5, targets InOrder [60,90,120,150,180]`, `rate 1` |
| L | Laps | 1 flight, uncapped | `last`, 1, `rate 1` |
| M | Laps, Time1Mins, Time1Secs | 3 flights, uncapped (309.8 s witnessed) | `all`, 3, `rate 1` |

Task-name strings are free text — names for codes not already named in
`ReplayDriver.cs`'s F3KSlotMap comment are unverified (VB source unavailable);
use neutral descriptive names. The arithmetic lives in the structure, not the
name. Comp 17's definition needs the 12 codes its schedule names (K, I, A(2),
E, H, G, J, C(1), D, F, B(1), B(2)); comp 54's needs its 13 (B(1), D(1), G, H,
M, A(2), E(1), C(1), I, J, K, L, F). Each fixture authors its own definition
(harness-story D2 — definitions are never reused).

Normalisation for both F3K comps: HigherIsBetter, winner 1000, HalfUp **0.1**
(1-dp grid verified cell-exact). Metric precision is a **quantum** (WI-2
as-built lesson): comp 17 `f3kDecimalsForTiming=0` → quantum 1 (all inputs
whole seconds — verified); comp 54 `f3kDecimalsForTiming=1` → quantum **0.1**
(124 tenth-second cells). F5J comps: quantum 1 everywhere (all whole —
verified).

## G4 float32 persist-cast property — pinned 99/162

Comp 45's stored NormalisedScore values are clean exact-1dp values (0 cells off
the 1-dp grid). 324 cells; **162 scored** (NS ≠ 0); emulating the binary32
persist cast — `float f = (float)(double)ns` — leaves **exactly 99** cells with
`f ≠ (double)ns` (cast residue), and **all 162** re-round (HalfUp 1dp) back to
the stored value. Both pins verified in planning. Per the parent's G4
discipline: assert the **cast behaviour**, never literal repr bits (D6).

## Resolutions of the parent story's caveats

- **Comp 45 G4 discipline** → comparator-property step over clean stores, D6.
- **Comp 54's R7 keep-highest dedup** → already the harness's D5 step 2
  behaviour (`ReplayDriver.cs:389-394`, oracle-keyed dedup): the four G4 rows
  (highest NS) win; the cancelled G3 zero-cells are intentionally never
  replayed → 8 ledger entries (D7). No ranking divergence: GS's ladder and our
  aggregate both keep the re-flown cell per pilot.
- **Comp 17's retired-pilot ranking** → pilot 89 absent R9–R15; the driver
  prescribes 7 synthetic flight-less slots (jerilderie WI-6 amendment
  precedent). Zeros contribute nothing, so his total — and expected place —
  match GS's flown-rounds ranking exactly; only "no oracle cell" entries are
  ledgered (D7).
- **Comp 121's unfloored raw** → our engine composes −2026 unfloored (raw grain
  matches); only the NS clamp diverges → N1 ledger (D2/D7).
- **Comp 135's OriginalRoundNo-keyed aggregation** → jerilderie mapping (a)
  precedent: the four re-flight rows are excluded at D5 step 1 (they have no
  base-draw prescription path; P128 also holds *ordinary* R5/G1 and R6/G2 rows,
  which survive step 1 and make step 2 a no-op — verified), synthetic slots
  fill R1–R4, and every arithmetic consequence is ledgered. Closing the gap
  faithfully is `reflight-aggregate-destination.md`'s job, not ours.
- **Comp 17's "phantom-Landing noise"** → **not noise**: R9 is task D, whose
  slot map reads the Landing column as the sixth flight time (145 → 105 s
  against the 105 target). Faithfully replayed; proven by the 417/417 sweep.
  Do not redact or ledger it.
- **Comp 54's "five decode deviants"** → fully explained by task H's
  sort-desc-then-clamp arithmetic (they are the clamp-biting rows). Nothing
  unexplained remains; no divergence-ledger treatment needed for them.
- **Motor-restart-effect pairing** → no mechanism needed: comp 45's 22 flagged
  rows fly with `F5JMotorRestartOption=null` and score normally (e.g. P94 R1:
  596 s − 78 + 50 = 568 ✔); comps 135/121's flagged rows are all-zero rows
  under option=1, which replay as flight-less placeholders (D4 cell 0) exactly
  like any placeholder. The pairing is a data observation for the scenarios'
  provenance notes, not engine work.

---

# Decisions settled during planning (do not relitigate)

## D1 — Replay mechanics per fixture

| Fixture | Draw derivation | Synthetic slots | Ledger needed |
|---|---|---|---|
| f5j-christchurch-2019 | D5 steps 1–3 as-is (no re-flights, no dups) | none | **none** (empty) + G4 property step |
| f5j-hawkes-bay-trials | D5 step 1 drops P128's four re-flight rows; step 2 no-op | R1–R4 for P128 (see D5 item 6) | yes (D7) |
| f5j-nz-south-island | as-is | none | yes — N1 clamp cell + ranking knock-ons (D7) |
| f3k-southern-fling | as-is (P89's absences are step-3-legal: 0 occurrences, not 2) | R9–R15 for P89 | yes — 14 "no oracle cell" entries (D7) |
| f3k-june-2020 | D5 step 2 fires for real: keeps the four G4 rows over the cancelled G3 zeros | none | yes — 8 "never compared" entries (D7) |

## D2 — Divergence citation register (new token N1)

Ledger reasons must cite an accepted token (existing: arithmetic/harness-story
`D1–D6`, `trap 3`). This story adds exactly one:

> **N1 — GS floors NormalisedScore at 0; our NormalisationEngine does not.**
> GS's option-1 branch floors after rounding (arithmetic story, Normalisation
> decision matrix, Duration × Opt 1, `Scoring_MOD.vb:310`); our engine has no
> lower clamp (`NormalisationEngine.cs:121`). Witness: comp 121
> `1/3/3/0/99` — raw −2026 both sides; GS NS 0.0, ours negative. This is an
> engine gap, not a rulebook conflict — the floor-at-zero belongs in a normalisation
> story; WI-7 files the backlog stub. Until then the divergence is ledgered N1.

`ReplaySteps.ThenEveryLedgeredDivergenceCitesAnArithmeticStoryId`'s regex
widens from `\bD[1-6]\b|\btrap\s*3\b` to
`\bD[1-6]\b|\btrap\s*3\b|\bN1\b`, with a comment citing this story's D2
(trap-3 precedent). Anything else uncited still fails.

## D3 — F5J class-definition authoring spec (comps 45, 135, 121)

One phase, one task, authored per fixture (never shared). Skeleton — the three
fixtures differ only in `name`/`version` provenance text:

```json
{
  "name": "Gliderscore F5J (<slug>)",
  "faiDesignation": "",
  "version": "GliderScore NZ master export, authored per kanban/backlog/nz-fixture-replay-scenarios.md D3; arithmetic proven cell-exact against expected-scores.json (820/820 planning sweep)",
  "parameters": [],
  "reflight": { "entitledScores": "UndefinedRequiresRuling",
                "othersScore": "UndefinedRequiresRuling", "minNewGroupSize": 2 },
  "penalties": [],
  "phases": [ {
    "ordinal": 1, "type": "Preliminary",
    "rounds": { "kind": "FixedSequence", "tasksPerRound": 1,
                "requireDistinctTaskPerRound": false },
    "validity": { "minRounds": 1 },
    "drops": [],
    "tasks": [ {
      "code": "F5J",
      "name": "F5J duration, two-rate height penalty, scheme-11 enter landing",
      "metrics": [
        { "name": "flightTime",    "kind": "Number", "unit": "s",
          "declaredBeforeLaunch": false,
          "precision": { "mode": "Truncate", "precision": 1 } },
        { "name": "launchHeight",  "kind": "Number", "unit": "m",
          "declaredBeforeLaunch": false,
          "precision": { "mode": "Truncate", "precision": 1 } },
        { "name": "landingDistance", "kind": "Number", "unit": "m",
          "declaredBeforeLaunch": false,
          "precision": { "mode": "Truncate", "precision": 1 } }
      ],
      "flights": { "$kind": "last" },
      "timing": { "kind": "UntilAllFlightsComplete", "maxLaunches": 1 },
      "group": { "minPerGroup": 2 },
      "normalise": { "direction": "HigherIsBetter", "winnerScore": 1000,
                     "round": { "mode": "HalfUp", "precision": 0.1 } },
      "score": [
        { "$kind": "piecewise", "metricRef": "flightTime", "bands": [
            { "from": 0,   "to": 600, "ratePerUnit": 1 },
            { "from": 600,            "ratePerUnit": 0 } ] },
        { "$kind": "piecewise", "metricRef": "launchHeight", "bands": [
            { "from": 0,   "to": 200, "ratePerUnit": -0.5 },
            { "from": 200,            "ratePerUnit": -3.0 } ] },
        { "$kind": "lookup", "metricRef": "landingDistance", "rows": [
            { "upTo": 0,   "points": 0  }, { "upTo": 55,  "points": 5 },
            { "upTo": 60,  "points": 10 }, { "upTo": 65,  "points": 15 },
            { "upTo": 70,  "points": 20 }, { "upTo": 75,  "points": 25 },
            { "upTo": 80,  "points": 30 }, { "upTo": 85,  "points": 35 },
            { "upTo": 90,  "points": 40 }, { "upTo": 91,  "points": 45 },
            { "upTo": 92,  "points": 45 }, { "upTo": 93,  "points": 45 },
            { "upTo": 94,  "points": 45 }, { "upTo": 95,  "points": 45 },
            { "upTo": 96,  "points": 50 }, { "upTo": 97,  "points": 50 },
            { "upTo": 98,  "points": 50 }, { "upTo": 99,  "points": 50 },
            { "upTo": 100, "points": 50 } ] }
      ],
      "scoreNormalised": []
    } ]
  } ]
}
```

Binding rationale (each verified in the ground-truth sweep):

- **Time term** = cap (band `[600,∞]` rate 0), NOT the F3J symmetric decay —
  `varFltDednIdx=3` caps for non-ALES/F3G classes.
- **Height term** = cumulative two-rate piecewise with negative rates:
  H ≤ 200 → −0.5·H; above → −100 − 3.0·(H−200) (cumulative bands, ales
  precedent). `launchHeight` is captured from `Scores.FlightScoreDeduction`
  (D5 item 5). It is a score term, NOT a penalty — never author
  `lateLandingDeduction` or any penalty definition for F5J (trap 5).
- **Landing lookup inside `score`** (option-1 effective arrangement — landing
  is part of the normalised base, unlike the ales option-2 arrangement), so
  `scoreNormalised` is **empty**. Leading `upTo: 0 → 0` row reproduces GS's
  0/miss→0; on-table validation (rule 2) makes banded ≡ exact on this data.
- **`normalise.round` HalfUp 0.1** — the effective 1-dp grid (both knobs stored
  null; effective values proven in `configProvenance` + the sweep).
- **`drops: []`** — no threshold can fire in any of the three (Drop1/2 unset,
  Drop3–5 = 99; recorded explicitly in comp 45's configProvenance).
- **Self-check rows** before running the harness: comp 45 P94 R1 → raw 568,
  NS 1000; comp 121 P99 R3/G3 → raw −2026, NS 0.0 (N1); comp 135 P128 R6/G1 →
  raw 170.5, NS 492.1.

## D4 — F3K class-definition authoring spec (comps 17, 54)

- One phase, `rounds.kind: ChooseFromCatalogue` (f3k-sample-comp precedent);
  `tasksPerRound: 1`; `validity.minRounds: 1`; **`drops: []`** (no threshold
  fires in either comp).
- One task object per GS code in that fixture's schedule (12 for comp 17, 13
  for comp 54 — see the ground-truth table for each code's shape: flights
  kind, maxLaunches, targets, caps). Single metric `flightTime`
  (`declaredBeforeLaunch: false`), `timing UntilAllFlightsComplete`,
  `group.minPerGroup 2`, `normalise HigherIsBetter 1000 HalfUp 0.1`,
  `scoreNormalised: []`.
- **Metric precision quantum**: comp 17 → `{"mode":"Truncate","precision":1}`;
  comp 54 → `{"mode":"Truncate","precision":0.1}`.
- `reflight` block and `penalties: []` as in D3 (no `Scores.Penalty` rows in
  either comp — verified).
- Task name strings: reuse the GS-style names already recorded in
  `ReplayDriver.cs`'s F3KSlotMap comment (G, A(1), F, D, C(3), X) where the
  code matches; for new codes use neutral names ("GS task K — positional caps
  60/90/120/150/180") and say in `version` that names are unverified.
- Self-check rows before running the harness: comp 17 P81 R1 (task K) → raw
  319; comp 54 P78 R4 (task H) → raw 522; comp 17 P87 R9 (task D) → raw 405.

## D5 — ReplayDriver / ReplaySteps widening (WI-1, exhaustive; shared files)

1. **CompDate fallback** (`ReplayDriver.cs:150-151`): `CompDate` null/empty →
   `new DateOnly(2000, 1, 1)` with a comment naming this story's ground truth
   (all five fixtures lack dates; the replay date is scoring-irrelevant).
   Existing fixtures (real dates) unaffected.
2. **F3KSlotMap** (`ReplayDriver.cs:552-563`): extend to the full 16-code
   catalogue exactly as the ground-truth table's "Slots read" column states
   (`A(2) [Laps]`; `B(1)`, `B(2)`, `D(1) [Laps, Time1Mins]`; `C(1)`, `E(1)`,
   `I`, `J`, `M [Laps, Time1Mins, Time1Secs]`; `E`, `K [Laps … Time2Secs]`;
   `H [Laps, Time1Mins, Time1Secs, Time2Mins]`; `L [Laps]`; keep existing
   G/A(1)/F/D/C(3)/X).
3. **Task H sort**: after decoding H's slots, sort the non-zero values
   **descending** before assigning flights (GS sorts then clamps positionally —
   ground-truth proof). Comment cites the sweep; do not sort for any other code.
4. **Task K zero-slot capture**: K pairs targets to SLOT positions, and comp
   17 has exactly one row with an interior gap (R1 P89, slots [47,49,43,**0**,
   180] → GS raw 319). For K, when a row has ≥1 non-zero slot, capture **all
   five slots as flights, zeros included** (a 0-valued flight pairs with its
   own target and contributes 0, keeping positional alignment). **Spike first**
   (one manual replay or focused test): confirm the engine accepts an
   open-flight + `flightTime 0` capture and `exactlyN` selection scores it
   `min(0, target) = 0`.    If selection drops or rejects zero-time flights, do
   NOT bend the engine — fall back to prefix capture and ledger the single
   affected cell (raw + normalised at comp 17's R1/G3/P89, reason D5) plus any
   ranking knock-ons; note the spike outcome in the fixture's provenance
   notes. Scope the zero-slot rule to **K only** — comp 17's D rows are all
   prefix-shaped, and changing D's capture risks f3k-sample-comp's committed
   green replay (trap 7).
5. **F5J capture** (`CaptureDurationInputs`, `ReplayDriver.cs:469-514`): the
   declared-metric gate already drives per-fixture capture; add
   `launchHeight` ← `FlightScoreDeduction` beside the existing
   `flightTime`/`landingDistance`/`lateLandingDeduction` arms (the F5J
   definitions declare `launchHeight`, so the mutual exclusion with
   `lateLandingDeduction` is by definition content, as today). Keep the
   `Time1Mins ≤ 0 → flight-less` placeholder rule (motor-restart zero rows and
   every unflown placeholder ride it).
6. **SyntheticSlots** (`ReplayDriver.cs:132-136`): add the entries (group =
   the round's smallest, ties → lowest GroupNo — which lands exactly on each
   vacated seat; SeqNo handling is the existing max+1):
   - `f5j-hawkes-bay-trials`: (1, **2**, 128), (2, **1**, 128), (3, **2**,
     128), (4, **1**, 128) — R1/R3 sizes {G1 6, G2 5, G3 6}; R2/R4 {G1 5,
     G2 6, G3 6} (verified).
   - `f3k-southern-fling`: (9, **3**, 89), (10, **3**, 89), (11, **3**, 89),
     (12, **3**, 89), (13, **3**, 89), (14, **3**, 89), (15, **3**, 89) —
     R9–R15 sizes {G1 5, G2 5, G3 4} (verified).
   No entries for the other three fixtures.
7. **ReplaySteps citation regex** per D2 (one-line widen + comment).
8. **Comparator: zero changes.** `OracleCell`'s hardcoded ReFlightNo 0 is fine
   (every oracle key in all five fixtures has ReFlightNo 0 — verified), the
   single-TaskNo guard is fine (each fixture carries exactly one TaskNo: 1 for
   the F5J comps, 5 for both F3K comps — never hardcode either).

WI-1's checkpoint: build clean; full acceptance suite green on sqlite with the
existing scenarios untouched and unchanged diff-table/ledger behaviour.

## D6 — The G4 comparator-property step (new Then, shared file, WI-1)

New step in `ReplaySteps.cs`, referenced only by comp 45's scenario:

```
Then the fixture's float32 persist-cast witness property holds over its scored normalised cells
```

Implementation, over the loaded `Fixture.ExpectedScores` (no comparator
changes, no replay data needed):

- Universe: cells with `NormalisedScore != 0m` — for comp 45 exactly **162**.
- Property per cell: `double exact = (double)ns; float f = (float)exact;`
  assert `Math.Round((decimal)(double)f, 1, MidpointRounding.AwayFromZero) ==
  ns` (GS's persist cast re-rounds to the stored 1-dp value).
- Witness pin: count of cells where `(double)f != exact` must be exactly
  **99** — the property must not be allowed to go silently vacuous.
- Failure messages cite this story's ground-truth section and the parent's G4
  discipline ("assert cast behaviour, never repr bits").

## D7 — Ledger protocol and a-priori expectations

`divergences.json` schema and reason style: copy
`tests/GliderscoreFixtures/jerilderie-2010/divergences.json`
(`DivergenceEntry`: grain, round?, group?, pilotNo?, reason — `FixtureModels.cs:122`).
Reasons cite D5 (harness-story draw-derivation decisions, jerilderie
precedent), N1 (D2), or trap 3 (pre-authorised). Committing a ledger entry is
human-triage, but the entries below are **pre-triaged by this plan** — land
them, then pin the count:

- **f5j-christchurch-2019** — empty ledger (the scenario asserts
  "carries no ledgered divergences" + the D6 property step).
- **f5j-hawkes-bay-trials** — expected **16 raw/normalised + ranking entries**:
  - 8 × "oracle cell never compared" at (5,2), (5,3), (6,1), (6,3) ×
    {raw, normalised} for P128 — the four re-flight cells (raws 225/299/170.5/524,
    NS 400/635.5/492.1/955.3) are intentionally never replayed;
  - 8 × "no oracle cell" at the synthetic slots (1,2), (2,1), (3,2), (4,1)
    P128 — ours alone, cell 0;
  - ranking: P128's total is short by 2482.9 (Σ the four re-flight NS) vs GS —
    his place and every displaced pilot get one entry each, enumerated from the
    first diff-table, reason D5 with the Δ stated;
  - trap-3 entries if a Score-tie/distinct-raw pair fires (check the diff table).
- **f5j-nz-south-island** — expected **1 raw/normalised pair NOT both**: the
  raw grain **matches** (−2026 both sides — no entry); 1 × normalised at
  (3,3,99) citing N1 (ours negative vs GS 0.0); ranking entries for P99 (total
  short by |our NS for R3|, place drops) + every displaced pilot, enumerated
  from the diff table, citing N1; trap-3 if fired.
- **f3k-southern-fling** — expected **14** raw/normalised "no oracle cell"
  entries at (9..15, 3, 89) — the synthetic retired-pilot slots (reason D5,
  citing the jerilderie WI-6 amendment precedent). P89's total and place are
  expected to MATCH (zeros contribute nothing) — if the diff table says
  otherwise, stop and triage (D7 discipline). Trap-3 if fired.
- **f3k-june-2020** — expected **8** raw/normalised "oracle cell never
  compared" entries at (7,3) for P101/P102/P128/P85 — the cancelled re-draw
  cells (reason D5, step-2 keep-highest). Ranking expected clean (GS's ladder
  and our aggregate both keep the re-flown cell). Trap-3 if fired.

Each fixture's scenario pins its final committed count with
`And the fixture ledger records exactly N accepted divergences` (existing
step). **Discipline:** any mismatch NOT predicted above is a stop-and-triage
event — re-derive by hand from the fixture inputs before touching anything;
a ledger entry is written only when the cause is understood and named. Never
widen an entry to make a suite green.

## D8 — Dispatch model

- **WI-1 is one agent** and the only one touching shared files
  (`ReplayDriver.cs`, `ReplaySteps.cs`, and nothing else in `tests/`).
- **WI-2 is one agent (sentinel)** — it proves the F5J authoring pattern and
  the empty-ledger + G4 path before the others fan out.
- **WI-3–WI-6 are four parallel agents** (one per fixture), each touching ONLY:
  its `class-definition.json` (new file), its `divergences.json` (new file,
  where ledgered), and its scenario block appended to
  `Features/ReplayingAGliderscoreFixture.feature` (append-only, in slug order
  after the last fixture scenario and before the WI-5 self-check scenarios).
  Feature-file edits are textually disjoint; the orchestrator lands them in
  slug order. A parallel agent that finds it needs ANY other file changed must
  stop and report back instead of editing shared files.
- **WI-7 is one agent** (close-out) after all fixtures are green.

---

# Known traps (pre-answered)

1. **Metric precision is a quantum, not decimal places** (WI-2 as-built).
   Comp 54 needs 0.1; everything else here is quantum 1. Getting comp 54
   wrong truncates its tenth-second inputs.
2. **Comp 54's TaskNo is 5** — oracle keys embed it (`5/7/4/0/128`). Never
   hardcode TaskNo 1 in driver/comparator code (existing code doesn't).
3. **The D5 dedup oracle key uses `OriginalRoundNo`**
   (`ReplayDriver.cs:418-423`) — correct for comp 54 (orig = round there) and
   for comp 135 (whose re-flight rows step 1 removed first). Do not "fix" it.
4. **Do not author caps the data cannot prove.** B(1)/B(2)/D(1)/E/E(1)/I/J/L/M
   are uncapped (witnessed above-cap slots: B(1) 182, D(1) 300, E(1) 277.5,
   I 200, M 309.8). C(1)'s 180 cap never bites and is AllUp-family convention —
   note it in the definition's `version` text.
5. **F5J's `FlightScoreDeduction` is launch HEIGHT (idx=3), not a deduction
   payload.** Never author `lateLandingDeduction` for an F5J fixture; never
   route it through penalties. The −1-rate-term trick from f3j-international
   does not apply here.
6. **Comp 45 rounds 12–18 are wholly unflown placeholder rounds** (126 cells)
   and comps 135/121 carry unflown placeholder groups too — all-zero rows are
   flight-less entries (cell 0) and all-zero groups hit the engine's
   zero-winner guard (harness trap 7: equivalent to GS's zero-max guard).
   Expect 0-cells, not errors.
7. **Scope the K zero-slot capture to K** (D5 item 4). Comp 17's D rows are
   prefix-shaped; widening D's capture risks regressing f3k-sample-comp's
   committed replay and command counts.
8. **The F3K window reduction and violation-zeroing remain inexpressible**
   (harness trap 4) — the 417/417 sweep proves the recorded data never trips
   them. If a replay still mismatches on a window suspicion, that is a
   stop-and-triage event, not a ledger.
9. **Ranking secondary key (trap 3) can fire in ANY fixture** — GS's ladder is
   Score DESC, RawScore DESC. Check each fixture's diff table for Score ties
   with distinct raw sums; ledger them citing trap 3 (token already accepted),
   and note the finding for `ranking-secondary-rawscore-key.md`.
10. **Runtime**: comp 45 is ~1000+ command POSTs (jerilderie's 63×14 replay is
    the precedent that this is fine). Run fixtures individually while
    iterating (`--filter` on the scenario name), full suite at checkpoints.
11. **Do not re-derive the oracles.** `expected-*.json` and `ladder.py` are
    the parent story's frozen outputs; `validate.py --index` must stay green
    (adding `class-definition.json` files does not affect it — verified: the
    validator's contract covers the schema-v1 files).

---

# Work items

### WI-0 — Board

`git mv kanban/backlog/nz-fixture-replay-scenarios.md kanban/in-progress/`; set
`**Status:** In progress · …` in the same commit.

### WI-1 — Shared machinery (one agent)

Implement D5 items 1–7 and D6 (the G4 step lands here because it is shared
code; it is referenced only by WI-2's scenario). Nothing else changes.

**Checkpoint:** `dotnet build Soarscore.sln`; baseline recorded;
`SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests`
green with all existing scenarios unchanged. Postgres leg wherever Docker
exists.

### WI-2 — Sentinel: f5j-christchurch-2019 (one agent)

Author `class-definition.json` per D3; append the scenario:

```
Scenario: The f5j-christchurch-2019 fixture reproduces GliderScore exactly at all three grains with its float32 persist-cast witness
  Given the fixture corpus manifest
  When the harness replays the GliderScore fixture "f5j-christchurch-2019"
  Then every raw flight score matches the fixture oracle exactly
  And every normalised round score matches the fixture oracle exactly
  And the final ranking matches the fixture oracle exactly
  And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
  And the fixture carries no ledgered divergences
  And the fixture's float32 persist-cast witness property holds over its scored normalised cells
```

Expect green with an empty diff table. Any mismatch → stop and triage per D7
(the F5J authoring is proven; a mismatch means a wiring bug).

**Checkpoint:** suite green on sqlite; WI-3–6 may now dispatch.

### WI-3 — f5j-hawkes-bay-trials (parallel)

Author `class-definition.json` per D3. Replay, expect the D7-predicted
raw/normalised mismatches exactly (16), enumerate the ranking knock-ons from
the diff table, commit `divergences.json`, pin the count in the scenario
(`And the fixture ledger records exactly N accepted divergences`, reason
citations D5). Scenario name:
"The f5j-hawkes-bay-trials fixture reproduces GliderScore exactly at all three
grains modulo its ledgered re-flight cells" — same Then chain as the
jerilderie scenario (exact grains + conservation + citation-check + count).

**Checkpoint:** its scenario green; all others green.

### WI-4 — f5j-nz-south-island (parallel)

Author `class-definition.json` per D3. Replay, expect exactly one normalised
mismatch at (3,3,99) citing N1 (raw matches); enumerate P99's ranking
knock-ons; commit `divergences.json`; pin the count. Scenario name:
"The f5j-nz-south-island fixture reproduces GliderScore exactly at all three
grains modulo its ledgered normalised-clamp cell".

**Checkpoint:** as WI-3.

### WI-5 — f3k-southern-fling (parallel)

Author `class-definition.json` per D4 (12 task codes, quantum 1). Replay,
expect exactly the 14 "no oracle cell" entries at (9..15, 3, 89); commit
`divergences.json`; pin the count. Verify P89's ranking matches the oracle
(flown-rounds semantics — if not, stop and triage). Scenario name:
"The f3k-southern-fling fixture reproduces GliderScore exactly at all three
grains across its twelve-task catalogue modulo its ledgered retired-pilot
slots".

**Checkpoint:** as WI-3.

### WI-6 — f3k-june-2020 (parallel)

Author `class-definition.json` per D4 (13 task codes, quantum 0.1). Replay,
expect exactly the 8 "never compared" entries at (7,3) for P101/P102/P128/P85;
commit `divergences.json`; pin the count. Ranking expected clean. Scenario
name: "The f3k-june-2020 fixture reproduces GliderScore exactly at all three
grains modulo its ledgered cancelled re-draw cells".

**Checkpoint:** as WI-3; all five NZ scenarios green.

### WI-7 — Close-out (one agent)

- `tests/GliderscoreFixtures/extract/validate.py --index` still 10/10 PASS;
  full acceptance suite green on **both** stores
  (`SOARSCORE_TEST_STORE=sqlite` and `=postgres` where Docker exists).
- House rule 6: create `kanban/backlog/normalisation-lower-clamp.md` — a short
  stub (What / Why it matters / Before starting) for the engine's missing
  normalisation floor-at-zero, citing D2/N1 and comp 121's witness cell, and
  noting it would discharge comp 121's N1 ledger entries.
- `deferred-decisions.md`: nothing expected — record only if a triage
  produced a settled non-decision worth keeping.
- `tech-debt.md`: nothing expected; add only what implementation actually
  surfaced.
- `git mv` this story to `kanban/completed/`, set the status header, and run
  `graphify update .` (repo convention).

---

## Execution plan

1. WI-0 → WI-1 (one agent; baseline first).
2. WI-2 (sentinel; proves the F5J pattern).
3. WI-3 + WI-4 + WI-5 + WI-6 (four agents in parallel; only disjoint files).
4. WI-7 (close-out).

**Finish line:** `dotnet build Soarscore.sln`; `dotnet test Soarscore.sln`;
the acceptance suite under both `SOARSCORE_TEST_STORE` values. Known flake:
solution-wide Marten migration race (`tech-debt.md` last item) — re-run the
failing project alone before diagnosing.

**Story invariant for sign-off:** all ten active fixtures replay through
public commands only and compare exact at all three grains, modulo ledgered
divergences that each cite an accepted token (D1–D6, trap 3, N1); the five NZ
fixtures exercise F5J authoring, four re-flight cells, F3K per-group
normalisation across a 12/13-task catalogue, a mid-comp re-draw, the
motor-restart pairing, the retired-pilot ranking, and the 99/162 G4
cast-witness property; no `src/` file changed or mentions GliderScore; no
`/docs` edit; both stores green.

## Out of scope

- The re-flight aggregate-destination engine concept
  (`reflight-aggregate-destination.md`) — comp 135 replays under the jerilderie
  mapping (a) precedent with everything ledgered.
- The ranking RawScore secondary key (`ranking-secondary-rawscore-key.md`) —
  trap-3 firings are ledgered, not fixed.
- The HTTP pre-normalisation view field
  (`pre-normalisation-score-view-field.md`) — grain 1 stays the Q1 in-process
  mechanism. Note for whoever lands that story later: the three F5J fixtures
  and both F3K fixtures will qualify for its D6 "HTTP grain-1" classification
  (all their tasks have empty `scoreNormalised`) — verified here.
- The normalisation floor-at-zero engine change — stubbed in WI-7, delivered
  by its own story.
- Two-timekeeper fixtures, G1/G3 diversity gaps, F5L/F5B families — unchanged
  standing gaps in `index.md`.
