# Story — Competition-grain score floor (5.5.11.12 n), and the FAI-class floor audit

**Status:** Completed 2026-09-30 (raised 2026-09-27 as a split from `kanban/completed/minimum-score-floor.md` — owner decision 2026-09-27, design question 2: keep that story the round-grain fix; owner settled the four design questions 2026-09-30 — class-level datum, universal seeding, no pre-view, fly-off floors uniformly — and the story moved to completed/).

## Owner rework note (2026-09-30, after the As-built below)

The As-built records the story as planned (phase-level datum, seeded where stated). The owner then ruled: **class-level datum, seeded corpus-wide** ("in general this rule applies to all classes"), no aggregate-grain pre-view, fly-off totals floor uniformly (cells can't print negative; post-deduction totals floor with the rest). Rework deltas:

- Datum moved `PhaseDefinition` → `ClassDefinition` (`ClassDefinition.cs:361`, doc cites 5.5.11.12 n + 00-general §6 as digest synthesis + owner ruling).
- Final assembly simplified to class grain (`ScoringService.cs` final assembly: floored class contributes `max(0, total − deduction)` / `max(0, preDrop − deduction)`; per-phase subtotal dicts removed).
- All 17 seeds carry `FloorTotalAtZero = true` (FAI: §6 + own-clause-checked-silent note; NZ: owner ruling; 85c additionally NZ.2.4(c)). Corpus diff: every class JSON gains top-level `floorTotalAtZero: true`; no phase-level key anywhere; task-level `floorAtZero` retained in the 8 round-grain files.
- WI-5 assertions unchanged (single-phase behavior identical); BDD D-A4 fixture now clears the class datum.
- Authority honesty: only verbatim competition-grain clause is 5.5.11.12 n; universal application is the owner ruling (+ GS prior art), recorded in D4's third amendment. The 50-f3j round-grain CD question stands (different grain).

## As built

- **WI-1 — phase-grain datum.** `PhaseDefinition.FloorTotalAtZero` (`bool?`,
  `src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs:296`, doc
  `:268-295` citing 5.5.11.12 n verbatim): phase-aggregate grain, null = D4
  identity, additive NFR-2, omitted-when-null via the global `Json.cs`
  `WhenWritingNull` (no per-property wiring, no adoption check, no
  `ParameterResolver` change).
- **WI-2 — floor at final assembly.** `ScoringService.cs`: per-phase subtotals
  accumulate alongside the cross-phase sums (`:232-233`, written `:600-603`);
  final assembly (`:623-683`) lets each floored phase contribute
  `max(0, phaseAggregate − deduction)` on both `Score` and `PreDropScore`
  (`<= 0m → 0m` literal; `:660-665`), unfloored phases as today. Single-phase
  reachable case is exactly `max(0, total − penalties)` on both keys.
  `Disqualified` OR survives (`:695`); multi-phase attribution accepted as-is
  in the comment while only phase 0 is drawable.
- **WI-3 — seeds + regen.** `SeedF5J.cs:252-256` (preliminary phase only,
  fly-off absent), `SeedF5jNdc.cs:220-224` (single phase, via NZ.2.4(c)),
  `SeedF5K.cs:119-123` (round-grain `FloorAtZero` on TaskA, inherited by B–E
  via `with`; 40-f5k *phase* datum stays absent — 5.5.10.16 silent). Regen
  green (round-trip, source-gen agreement, 17 classes / 4 tapes); corpus diff
  exactly `30-f5j.json`, `85c-nz-f5j-ndc.json`, `40-f5k.json`, other 14
  byte-identical (seed JSON is gitignored build output). No NdcScore mirror —
  no `~/source/NZ-NDC-Score` checkout on this machine. Sibling
  `landing-zero-and-flyaway-encoding.md` already landed (in `completed/`) so
  no shared drift-guard run was needed.
- **WI-4 — fixture silence, with one correction.** All three parallel-run
  oracles pass unchanged; GS replay silent. One hand-built BDD pin broke
  (`ScoringACompetition.feature:74`, pinned −1000 now floors to 0 through the
  inherited 30-f5j prelim phase): the floor merges the two outcomes D-A4 must
  distinguish (deducted −1000 vs dropped-half 0), so the fixture class clears
  the datum (`ScoringACompetitionSteps.cs:85-97`, commented) and keeps pinning
  the raw −1000. No oracle or expected-result file touched.
- **WI-5 — tests.** `ScoreFloorTests.cs:408-745`: (a) `:548` untouched when
  non-negative; (b) `:571` floored rounds + penalty → 0 on both keys;
  (c) `:594` no datum → −200 passes through (D4); (d) `:616` floor after
  drops with retained dropped-round penalty; (e) `:656` Disqualified survives;
  CsCheck invariant `:693` (`max(0, Σ−pen)` on both keys, ≥ 0) and tail-merge
  pin `:728`.
- **WI-6 — FAI audit.** 40-f5k: seeded (stated 5.5.10.1 clause; digest
  misattributes it to 5.5.10.15 — recorded, `docs/rules/` untouched). 50-f3j:
  open CD question in `deferred-decisions.md` (silence, reachable −5 band, no
  ruling). 20-f3b / 60-f5l: closed, no datum no question (raws provably ≥ 480
  by working-time-capped arithmetic).
- **Suites (2026-09-30, Postgres/Testcontainers):** Domain 936/936 (10 floor +
  7 new), Acceptance 116/116, Application 446/446, Infrastructure 182/182,
  Architecture 14 pass / 4 skipped.

## What

Two related residuals of the score floor, deliberately not landed with the task-round floor:

1. **The competition total.** FAI F5J `5.5.11.12 n` — *"All penalties are cumulative and will be deducted from the competitor's total score at the end of the preliminary rounds … In case the total score after deduction of the penalties is negative, a zero (0) score will be recorded."* The engine subtracts aggregate penalties with no clamp (`src/Soarscore.Domain/Scoring/ScoringService.cs` final assembly, `Score: totalScore - penaltyResult.Deduction`), and the D4-era "Aggregate floor ≥ 0" deferral (`kanban/completed/normalisation-lower-clamp.md` D4) rested on "no fixture evidence" — but the RULEBOOK evidence exists for the F5J family (carried by the F5J NDC definition via NZ.2.4(c), which disregards only 5.5.11.12.m). A 5.5.11.12-f-floored round (recorded 0) plus an aggregate penalty lands below zero today.
2. **The FAI-class floor audit.** `minimum-score-floor.md` seeded the datum only where a class's own clause states it (85c, 85d, 30-f5j). The remaining normalising FAI classes — 20-f3b, 40-f5k, 50-f3j, 60-f5l — carry negative-rate terms and rely on `NormalisationEngine`'s normalised-grain clamp, which covers the common case (a positive winner) but NOT an **all-negative group**: there the winning raw is itself negative and the ratio formula inverts, so a loser can outscore the winner with normalised scores above 1000 (the clamp only fixes `≤ 0`). This is exactly why 30-f5j was seeded in the floor story. Examine each class's own clause — seed the datum where stated; where the rulebook is silent, record it as a Contest-Director question, not an inference (the fai-rules skill's rule).

## Verified facts (traced 2026-09-30; every value below was read, not recalled)

**The rules.**

- FAI F5J `5.5.11.12 n`, verbatim via `fai-rule.sh show 5.5.11.12` (source `docs/rules/source-docs/f5-electric-2026.md:1007-1011`): *"Penalties shall be listed on the score sheet of the round in which the infringement(s) occurred. All penalties are cumulative and will be deducted from the competitor's total score at the end of the preliminary rounds. Penalties earned in the preliminary rounds are not carried forward into the fly-off rounds. In case the total score after deduction of the penalties is negative, a zero (0) score will be recorded. The same total score will be used for individuals and team classifications."* Digest: `docs/rules/f5j.md:95-96` (sec 5); round-grain floor at `docs/rules/f5j.md:73` (sec 3).
- NZ F5J NDC carries it whole: NZ.2.4(c) adopts FAI Volume F5; NZ.2.4(d) disregards **only** 5.5.11.12.m and scores "the sum of the Raw Scores from the four rounds" (`tools/Soarscore.SeedData/SeedF5jNdc.cs:8-14`). So `n` carries into 85c; `f` already did (`SeedF5jNdc.cs:118-124`).
- FAI F5K final: `5.5.10.16` states the sum-of-normalised-scores total and the 7+-round drop **with no floor clause** (verbatim shows only sum/drop/scorecard sentences). Digest `docs/rules/f5k.md:69-70` (sec 3) claims "negative totals recorded as 0" — but attributes it to `5.5.10.15`, whose verbatim text contains **no such sentence** (digest-vs-source discrepancy; the real nearby text is the 5.5.10 task-overview intro at `f5-electric-2026.md:~1107`: *"If the total of all points is negative, the score is zero (0). The score is the accumulation of the flight times, adjusted for penalties and bonuses…"* — task grain, not competition grain; audit lead, not a conclusion).
- FAI F5L final: `5.5.12.12` states sum/drop/fly-off ranking **with no floor clause**. `5.5.12.11` states the raw flight score with none either. Digest `docs/rules/f5l.md:61-93` (sec 3–5).
- F3B: `F3B.2.7` totals partials, `F3B.2.8` classifies by sum with per-task drops — **no floor clause**. F3J: `F3J.10.10/10.11` normalise, `F3J.11.4` final placing — **no floor clause found** (grep for negativ/below-zero across `f3-soaring-2025.md` hits only the F5J-embedded 250 m clause and overfly zeros). F3K: `F3K.10.1` is "sum of the normalised scores … minus penalty points" — **no floor clause**.
- Common parents: `docs/rules/00-general-rules.md` sec 5 (`:106-114`) — aggregate = sum of round scores, drops discard the lowest, **penalties retained even if their round is dropped**; sec 6 (`:132-139`) — penalties cumulative, deducted from the final score, "A score that would go negative is recorded as **zero** (penalties still stand)". `docs/rules/f5-general-rules.md:8-11` — the F5 volume's §5.5.1/5.5.2 generals are scoped primarily to F5B: **no general competition floor to inherit**; each class must state its own (same per-class discipline as the round floor).
- NZ Class Q (85d) is a NZ class, NOT FAI F5K — nothing in NZ.7.8 nominates the FAI book (`SeedF5kNdc.cs:14-26`) — so no FAI clause carries there at all; its NDC total is `NZ.7.8(mm)(iii)` "sum of the rounds RAW Scores only" (`SeedF5kNdc.cs:8-12`).

**The engine, competition grain.**

- `ScoringService.ScoreCompetition` (`src/Soarscore.Domain/Scoring/ScoringService.cs:213-654`): per-competitor `totalsByCompetitor` / `preDropTotals` accumulate phase aggregates (`:580-594`); final assembly (`:599-621`) deducts each competitor's own aggregate penalties once (`GetAggregatePenalties`, `:830-837`, subject-filtered; `PenaltyEngine.ApplyAggregatePenalties`, `PenaltyEngine.cs:154-190`, which itself applies **no floor**) at `:608`, writing `Score: totalScore - penaltyResult.Deduction` (`:612`) and `PreDropScore: preDropTotals[…] - penaltyResult.Deduction` (`:613`). No clamp on either. Only phase 0 is ever drawn (`Competition.DrawPhase`), so each total is exactly one phase's aggregate today (`:220-223`).
- GS prior art (D4-era note): GS floors the final score after penalties (`Rpt_Results_Overall_MOD.vb:2690-2712`).
- The round-grain floor composes underneath: `ScoreGroup` step 2d (`ScoringService.cs:170-189`) floors post-raw-penalty raws where `ResolvedTask.FloorAtZero` is set, so competition cells already arrive floored — this story floors only the post-deduction total.
- Team classification reads the same total: `n`'s last sentence ("same total … for individuals and team classifications"); the teams MVP sums final scores (`deferred-decisions.md` T1 context) — no separate team work.

**Definitions and seeds.**

- Round-grain datum shape to mirror: `TaskDefinition.FloorAtZero`, `bool?`, doc at `ClassDefinition.cs:208-228` (grain = task-round, after raw penalties, additive NFR-2, canonical JSON omits null via `Json.cs` `WhenWritingNull`); threaded as-is through `ParameterResolver.ResolveTask` (`ParameterResolver.cs:127`) onto `ResolvedTask` (`ScoringResultTypes.cs:316-317`).
- Current seeds carrying `FloorAtZero = true`: 30-f5j TaskD (`SeedF5J.cs:112-119`; fly-off task inherits through the `with` at `:143` — correct at round grain since `f` covers all groups), 85c (`SeedF5jNdc.cs:118-124`), 85d TaskA with B/C/E inheriting (`SeedF5kNdc.cs:169-178`), plus the four 2026-09-30 Joe Wurts local-ruling NZ definitions (80/81/83/85). 40-f5k (`SeedF5K.cs:87-140`, TaskA with `Normalise` + `RawScore` truncation, Score terms Rate + LaunchAltitude + PilotArea + Overfly) and 60-f5l (`SeedF5L.cs:45-119`, TaskD 2pt/s to 390, `Rest(-2)`) carry **no** datum. 20-f3b carries `Rest(-1)` (`SeedF3B.cs:52`, F3B.2.3 c); 50-f3j carries `Constant(-30)` (`SeedF3J.cs:109`, F3J.10.3); 10-f3k carries no negative-rate term (Rate caps only — audit confirms, no floor question arises at round grain).
- Corpus: 17 classes (`tools/Soarscore.SeedData/Corpus.cs:26-47`); regen command `dotnet run --project tools/Soarscore.SeedData` (`tools/Soarscore.SeedData/README.md:17`).

**Blast radius:** the final-assembly subtraction is shared by every class, but a phase-gated datum changes numbers only where seeded (30-f5j preliminary, 85c) — and only for competitors whose post-deduction total is negative, a cell no GS fixture exercises (fixture-silence WI proves it).

## Why it matters

The floor story fixed the round grain the NdcScore sheet flagged. The competition grain is the same defect one grain later, with explicit rulebook wording: any F5J-family competitor whose aggregate penalties exceed a (possibly already floored) total is ranked on a negative today where the rulebook records 0 — and the team classification inherits the same wrong total. The all-negative-group anomaly is the complementary hole: for any normalising FAI class whose raw can go fully negative, one bad-weather group inverts the 1000-basis and the winner loses to arithmetic, silently. Both are unwitnessed by any fixture, which is why they survive.

## Cross-reference (house rule 2)

- **Deferred decision D4** (`kanban/deferred-decisions.md:345-362`, pass-through identity + 2026-09-27 floor-datum amendment): this story adds the second rulebook-stated route — a phase-level competition datum — without touching the identity default. On landing, amend that bullet again (competition grain), don't delete.
- **2026-09-30 Joe Wurts precedent** (`deferred-decisions.md:277-287`, ALES floor ruling): the shape for recording any audit outcome that seeds-against-silent-text — local ruling cited verbatim, `docs/rules/` untouched (house-keeping rule 1), scope pinned to exactly the definitions asked about. Silence without such a ruling stays unseeded.
- **NFR-1/NFR-2** — same mechanism as the round floor: definition data (phase-level datum), additive, omitted when absent; no `if class ==` engine branch.
- **NFR-4** — no capture gating; scoring interpretation only.
- **Sibling** `landing-zero-and-flyaway-encoding.md` — shares nothing mechanically, but if it lands first the same drift-guard run should cover both (both edit seed definitions).

## Plan

- **WI-1 — the datum (phase grain).** Add `bool? FloorTotalAtZero` to `PhaseDefinition` (`ClassDefinition.cs:255-297`, natural slot beside `Drops`), with a doc comment citing `5.5.11.12 n` verbatim and stating: grain is the **phase aggregate at the end of the phase's rounds** ("at the end of the preliminary rounds"); null = no floor (D4 identity at aggregate grain); additive (NFR-2), omitted by canonical JSON when null. Phase-level, not class-level: `n` is preliminary-scoped, fly-off penalties are not carried forward, and the fly-off aggregate states no floor — a class-level flag would wrongly floor the fly-off. No `ParameterResolver` change: phases carry no parameterised slots and `ScoreCompetition` already reads `classDef.Phases[phase.Ordinal]` directly (`ScoringService.cs:252`).
- **WI-2 — the floor, once, at final assembly.** In `ScoreCompetition`, accumulate **per-phase** (aggregate and pre-drop) subtotals per competitor alongside today's cross-phase sums (`:580-594`), then at final assembly (`:599-621`): each phase whose definition sets the datum contributes `max(0, phaseAggregate − deduction)` and `max(0, phasePreDrop − deduction)` where `deduction` is today's single `ApplyAggregatePenalties` figure; unfloored phases contribute as today. In every reachable contest (single phase) this is exactly `max(0, total − penalties)` on both `Score` and `PreDropScore`. `<= 0m` → literal `0m` (the clamp's −0.0 lesson, `normalisation-lower-clamp.md` D3). `Score` and `PreDropScore` floor **together**: both are the same "total after deduction" at different drop stages, `n` floors the total, and GS floors the final score (`Rpt_Results_Overall_MOD.vb:2690-2712`); a floored `Score` beside a negative `PreDropScore` would corrupt the display-ladder rung 2 countback. Multi-phase penalty attribution (fly-off-coordinate penalties swept into the one deduction figure) is accepted as-is while only phase 0 is drawable — record it in the phase doc comment, don't solve it.
- **WI-3 — seeds + drift guard.** Set the datum citing each phase's own clause in a C# comment ("cite what you encode"; the wire carries only the bool): **30-f5j preliminary phase** (`5.5.11.12 n`; fly-off phase left absent — `n` is preliminary-scoped, seed only where stated), **85c single phase** (`5.5.11.12 n` via NZ.2.4(c); (d) disregards only m). Explicitly absent (silence, no inference): 40-f5k (`5.5.10.16` silent), 60-f5l (`5.5.12.12` silent), 85d (`NZ.7.8(mm)` checked clause by clause — NZ class, no FAI carry), 20-f3b (`F3B.2.8` silent), 50-f3j (`F3J.11` silent), and every other phase. Regenerate (`dotnet run --project tools/Soarscore.SeedData`, all integrity gates green) and mirror `85c-nz-f5j-ndc.json` into the NdcScore fixture if it moves. Expected corpus diff: exactly the two seeded phases; the other 15 classes byte-identical. Drift-guard discipline per `kanban/backlog/ci-seed-corpus-drift-guard.md`.
- **WI-4 — fixture silence.** Full acceptance suite (Gliderscore replay + parallel-run oracles) green; no oracle or expected-result edit anywhere. No GS fixture drives a post-deduction total below zero, so the suite passing unchanged IS the proof — ledger nothing, silence is the assertion. (Postgres/Testcontainers run where Docker is available; sqlite fast loop otherwise, per the clamp story's WI-5.)
- **WI-5 — tests.** Through `ScoreCompetition` with synthetic single-phase classes in `ScoreFloorTests.cs`' class-agnostic style (follow the lightest existing `ScoreCompetition` driver in the suite for the Competition/Entry scaffolding): (a) total minus penalties non-negative → untouched on both keys; (b) floored-zero rounds + aggregate penalty exceeding the total → `Score == 0m` **and** `PreDropScore == 0m`; (c) no datum → the negative total passes through (aggregate-grain D4, mirroring the comp-121 pin); (d) drop interaction — a class with a drop policy: the dropped round's penalties are retained (00-general sec 5) and the floor applies after drops + deduction; (e) the Disqualified flag survives the floor rewrite. **Property tests (CsCheck)** — the named invariant: *for any competitor with any round scores and any aggregate penalties, under a phase that states the competition floor, the final score is exactly `max(0, sum − penalties)` and satisfies `score ≥ 0`* (both keys); plus the tail-merge pin: two competitors with `sum − penalties` at a < b ≤ 0 both record 0. Domain suite green (floor story: 884 passing baseline).
- **WI-6 — the FAI floor audit (no code until it concludes).** For EACH of 20-f3b, 40-f5k, 50-f3j, 60-f5l, fill one table row with citations (use `fai-rule.sh show <ref>` for verbatim quotes; never edit `docs/rules/`):
  | class | (a) verbatim floor clause or explicit silence | (b) can the raw go negative without penalties? (name the term) | (c) is an all-negative group reachable? (show the arithmetic) | (d) outcome: seed `FloorAtZero` or record a CD question |
  Leads (not conclusions — verify each): **40-f5k** — the 5.5.10 intro *"If the total of all points is negative, the score is zero (0)"* (`f5-electric-2026.md:~1107`, task grain) looks like a stated round floor the digest misattributes to 5.5.10.15 (`docs/rules/f5k.md:69-70` vs the verbatim, which states none); negative terms: NLH-band penalties, −10 pilot-area, −100 overfly, B/E launch penalties (`SeedF5K.cs:126-139`); reachability: yes in principle (short flight, high launch). **50-f3j** — `Constant(-30)` overfly deduction (`SeedF3J.cs:109`, F3J.10.3) can drag a near-zero flight negative (e.g. 0 s + 0 landing − 30); floor-clause silence → presumptive CD question. **20-f3b** — `Rest(-1)` over-600 s deduction (`SeedF3B.cs:52`, F3B.2.3 c) plus landing forfeit; check the working-time-capped arithmetic for whether Task A can actually print negative; `F3B.2.6/2.7/2.8` silence → presumptive CD question; note Task C is LowerIsBetter (ratio already inverted by design). **60-f5l** — `Rest(-2)` over-390 s (`SeedF5L.cs:69-72`, 5.5.12.11.1): worst case 390×2 + 150×(−2) = 480 ≥ 0, and every zero-condition zeroes the whole flight rather than deducting — verify the raw is non-negative by construction, in which case the all-negative group is unreachable and the row closes with no datum and no question. Outcome discipline: a stated clause → seed in this story's drift-guard run; silence → CD question recorded in `deferred-decisions.md` (Joe Wurts shape), never an inference, never an engine default.

## Design questions to settle

- **Datum shape** (WI-1 proposes `bool?` on `PhaseDefinition`): confirm phase-level vs class-level from the wording with the owner — the deciding sentences are `n`'s "at the end of the preliminary rounds" plus "not carried forward into the fly-off". The 2026-09-27 round-floor precedent (`bool?`, revised from record-with-citation the same day) says keep it a bare bool.
- **`PreNormalisationScores`**: settled for the round floor (floored zero — `ScoreFloorTests.cs:134-135`); the competition floor adds no aggregate-grain pre-view — confirm no new view field is wanted (nothing in the wire carries floor metadata today).
- **Fly-off floor silence**: `n` floors the preliminary total; the fly-off aggregate states nothing — confirm the fly-off phase stays unseeded (a CD question only if a fly-off total ever prints negative).
- **Drop-policy interaction**: pinned by WI-5(d) — penalties retained through drops (00-general sec 5), floor strictly after drops + deduction.

## Before starting

- Settle the datum-grain question from the FAI/NZ wording (not symmetry), with the owner.
- Confirm the WI-3 seed list against each phase's own clause — seed only where stated; the F5K/F5L "equivalents" (`5.5.10.16`, `5.5.12.12`, `NZ.7.8(mm)`) have been checked clause by clause above and are silent.
- Run the WI-6 audit before any round-grain seeding it may recommend, so all seed edits land in one drift-guard run.
- Land together with `landing-zero-and-flyaway-encoding.md` if both touch seeds (one review, one drift-guard run).

## Alternative homes considered and set aside

- **Unconditional floor in `ScoreCompetition`'s final assembly** — the one-line fix, but unstated engine policy for every class whose rules are silent, and exactly what D4 refuses at aggregate grain; it would also rewrite total semantics the GS oracles pin.
- **Capture-time reject/clamp** — needs new metric range data plus a capture gate (NFR-4 tension) for a problem purely of *scoring interpretation*.
- **Class-level datum** — would floor the fly-off aggregate that `n` deliberately excludes (penalties not carried forward); phase grain is what the wording states.

## NdcScore's part once it lands

Nothing structural. The client renders the engine's number verbatim (no arithmetic), so once the engine floors, provisional standings and the shared individual/team total correct themselves with no client change. Today's sub-zero total (floored-zero rounds minus aggregate penalties) shows verbatim; after, `0`.
