# Story — Resolve GliderScore scoring arithmetic from source

**Status:** Completed · **Raised:** 2026-08-25 · **Planned:** 2026-08-25

**Version pinned (2026-08-25):** source tree `app.config` `DBVersion = 6.78`
(`/home/pete/source/gliderscore/GliderScore_Master/app.config`), matching the
fixture producer v6.78 export. All findings below attribute to this tree.

## What

Read the GliderScore VB calculation code and pin down, exactly, how a competition's
scores are produced. We hold their full source at
`/home/pete/source/gliderscore/GliderScore_Master/`, so this is research, not
reverse-engineering. Deliverable: the formulas written up in this story file,
validated by recomputing every persisted score of the analysed ALES sample comp.

Primary modules — corrected during planning (2026-08-25). The stub's list named only
the report modules; the arithmetic core is elsewhere:

- **`Scoring_MOD.vb`** — the engine that computes and *persists* `RawScore` /
  `NormalisedScore`. Entry: `Update_AllScores` (:5) → per row `Update_RawScore`
  (:137), per group `Update_GroupScores` (:247). Helpers: `GetTimeScore` (:635),
  `GetLandingBonus` (:726), `GetLapsScore` (:805), `GetHeightPenalty` (:848),
  F5J height params (:863/:870/:877), `GetDurTargetTime` (:884),
  `CalcRawScoreF3K` (:1467).
- **`GlobalFunctions_MOD.vb`** — `RoundNumber` (:3116), `TruncateNumber` (:3155).
- `Rpt_Results_Calculations_MOD.vb` — final totals, drop-worst, rank/percent for
  Overall / Team / Comp Series / By Task reports (`dtCompResults_ApplyDropScores`
  :292, `GetDropScoreOption` :424, DropTasks :437 / DropRounds :546 variants,
  `dtCompResults_FillPilotRankAndPcnt` :730). These consume the persisted columns.
- `Rpt_Results_Overall_MOD.vb` — overall presentation incl. fly-off rank resolution
  (`Resolve_FlyOff_Rank_If_Same_Scores` :235) and sort orders (:277/:293/:304).
- `Rpt_RoundResults_MOD.vb` and the other report modules only *display* the
  persisted columns; no WI needs them unless a display-only rule surfaces.

Questions to answer:

1. **`NormalisedScore`** — unresolved in `gliderscore-db-analysis.md` §4 ("winner =
   1030 for RawScore 330 + 25 landing does not reproduce from any obvious formula").
   Do not trust import arithmetic until resolved. *(Planning hypothesis to test, not
   assume: group-score-on-time adds landing points after normalising time alone —
   winner = 1000·t/max ≤ 1000 **+ landing**, so 1030 ≈ 1000 + 30 landing pts.)*
2. **RawScore composition** — flight score + landing points − height penalty −
   flight deductions; confirm against §4's worked example (200 s flight, 40-pt
   deduction from `durRefHeight=200`, `durPenaltyUpToRefHeight=0.5/m`). Also check
   the zero-floor `Update_RawScore` applies to Duration (:182) against what FAI
   expects pre-normalisation.
3. **Rounding/truncation** — where `GroupScoreDecimals` and `RoundOrTruncate` bite;
   what precision each intermediate carries. Known so far: half-up via
   `Int(x + 0.5·10⁻ᵈ)`; truncation nudges by +1e-6 first; Decs 0..3 only; and
   `NormalisedScore` is **persisted as 32-bit Single**
   (`Scoring_MOD.vb:2771/:2947`) — the compare-harness oracle is float32, and final
   `Score`/`RawScore` get re-rounded at report time (`FillPilotRankAndPcnt` :738).
4. **Drop-worst activation** — `Drop1AtRound…Drop10AtRound`, `DropScoreOption`,
   `F3QDrop6to10` staged schedule; tie-breaking among equal candidates.
5. **Ranking/tie-break order** for the overall result — known so far: sort
   `Score DESC, RawScore DESC` (:748), duplicate ranks shown "=n", F3K breaks ties
   by best dropped score (:806+); what `HiddenRanking` vs displayed Rank means for
   ordering.

## Why it matters

The golden-path comparison harness treats GliderScore's persisted numbers as the
oracle. Without the exact arithmetic we cannot triage a mismatch into *our defect* vs
*misunderstood oracle*. Also feeds property-test invariants later (normalisation
scaling, drop monotonicity). Downstream stories:
`gliderscore-golden-fixture-pipeline.md` needs to know which config fields the
fixture must carry; `gliderscore-replay-and-compare-harness.md` needs exact formulas
and the float32-oracle caveat.

## Plan

Research executed by agents/sub-agents in three waves. Every WI writes its findings
as a section at the end of this file (house rule 4: never `/docs`), citing
`file:line` for every claim, and states explicitly anything it could not resolve.

Ground rules for all WIs:

- The GliderScore tree is read-only reference material; no Soarscore code changes.
- Take the story into `in-progress/` before starting (board rule 3); one WI at a
  time per agent; update this file's findings sections as you complete each WI.
- Where GS arithmetic contradicts the FAI/NZ rulebooks, do not judge it here —
  record it under *Divergences* with the rule citation (`fai-rules` skill). Those
  become intentional-divergence candidates for the compare harness.
- Pin the version: find the source tree's DB/app version (`My.Settings.DBVersion`,
  `MainMenu_FRM.vb:1697` compares against the DB's `DBParams`) and record it next
  to the fixture producer version 6.78, so findings can be attributed.

### WI-1 — RawScore composition, per task family

Scope: `Scoring_MOD.Update_RawScore` (:137–244) and helpers
(`GetTimeScore` :635, `GetLandingBonus` :726, `GetLapsScore` :805,
`GetHeightPenalty` :848, `CalcRawScoreF3K` :1467, `GetTimeInSeconds` :626).

Deliverable — findings section *Raw score*: one formula block per family
(Duration/Distance/Speed/F5B/F3K/F5K, the `TskNbr` Select Case), covering:

- exactly which raw inputs feed it (`Time1Mins` holds seconds! §4 of the analysis)
- the `varFltDednIdx` semantics (0 none / 1 late-landing / 2 motor-run-as-time /
  3 height-penalty / 4 F3Q) — set from comp setup UI (`Comps1Saved.vb:2699`),
  meaning table at `Comps1_MOD.vb:909-941`; how each enters the sum
- zero-floors and side effects (`Updated` flag, `FltPenalty` column)
- landing-distance → points resolution against `LndgPoints` (bucket matching,
  interpolation or nearest?)
- time→points curve for duration (`GetTimeScore`): cap at target? seconds
  precision used? decimals parameter (`varDurTimeDecimals` etc.)
- F3K `CalcRawScoreF3K`: task-string parsing → per-flight sums, penalties
- F5K four-flights-in-four-columns packing (:229-236) and bonus-table handling

Done when: the §4 worked example (pilot 13: 200 s, RawScore 160) reproduces from
the written formula, and every family branch has been read, not inferred.

### WI-2 — NormalisedScore: the group-score matrix

Scope: `Scoring_MOD.Update_GroupScores` (:247–486) called from
`Update_AllScores` (:117). Depends on WI-1 (helper semantics) and WI-3 (rounding).

Deliverable — findings section *Normalisation*: a decision matrix of
`GroupScoringOption` (0 raw / 1 points / 2 time-based) × family × `varFltDednIdx`,
covering:

- option 0: Normalised = rounded Raw (:269-281) — note this permits >1000
- option 1: `1000 × Raw / MaxRawInGroup` (:303), Max computed by explicit loop
  not the DataView sort (:283-289 — why? F5K sort bug comment), zero-max guard
- option 2 + dednIdx=0: normalise time alone then **add landing after** (:335-376)
  — the >1000 mechanism; test the §4 winner=1030 case against the sample data
- option 2 + dednIdx=3: normalise (time − height penalty), landing added (:381-414)
- Speed inverse scaling `1000 × MinPositiveRaw / Raw` (:442-482) and its
  zero/non-positive guards
- post-normalisation F3Q deduction subtraction when dednIdx=4 (:318-327)
- who calls this when settings change mid-comp (`Update_AllScores` re-run paths)

Done when: the §4 "winner = 1030" anomaly is reproduced from the matrix using the
sample comp's actual config values, or definitively explained otherwise.

### WI-3 — Precision, rounding, and storage map

Scope: `RoundNumber`/`TruncateNumber` (`GlobalFunctions_MOD.vb:3116/:3155`); every
call site of both plus `GroupScoreDecimals` / `RoundOrTruncate` plumbing
(`GlobalVariables_MOD.vb` defaults; where loaded from `Comps`); column types in
`dsScores` and the persist path (`Scoring_MOD.vb:2712/:2771/:2947`).

Deliverable — findings section *Precision & storage*: a stage-by-stage table with
one row per stage, columns `stage · precision carried · rounding applied · stored as`,
covering at least:

- time entry seconds → TimeScore (per-family decimals vars)
- RawScore (Double in-memory; Jet column type)
- NormalisedScore (rounded/truncated at group calc; **persisted Single/float32**)
- final Score/RawScore re-rounded at report time (`FillPilotRankAndPcnt` :738-740)

Plus: exact semantics of RoundNumber (half-up via `Int(x+0.5·10⁻ᵈ)`; note it is
*not* banker's rounding despite VB's reputation) and TruncateNumber (+1e-6 fudge —
when does the fudge itself flip a digit?), Decs limited to 0..3 (what if config
holds another value? falls through unrounded — check), Double-vs-Single drift
implications for an exact-match comparator (recommend tolerance or decimal-literal
comparison strategy for the harness).

Done when: the table names every place precision can be lost between raw input and
displayed final rank.

### WI-4 — Drop-worst: activation schedule and algorithm

Scope: `Rpt_Results_Calculations_MOD.vb` — `Create_dtRoundScores` (:128),
`Create_dtTaskScores` (:149), `dtCompResults_ApplyDropScores` (:292),
`GetDropScoreOption` (:424), `dtCompResults_UpdateScores_DropTasks` (:437),
`dtCompResults_UpdateScores_DropRounds` (:546), negative-marker helpers
(:694/:705); the `Comps` columns `Drop1AtRound…`, `DropScoreOption`,
`F3QDrop6to10`; how `f3kRecord` gates the F3K variant.

Deliverable — findings section *Drop-worst*:

- the staged activation table: rounds flown → how many drops, per DropScoreOption
  (and what F3QDrop6to10 changes at 6..10 rounds)
- drop selection basis: lowest *round* normalised total vs lowest *task* score —
  when each applies (TaskList-driven vs round-driven paths)
- tie-breaking among equal drop candidates (first-in-sort? pilot order?) — cite
  the sort before selection
- how a dropped score is marked (negation trick) and excluded from `Score`
- interaction with re-flights / `OriginalRoundNo` in the round-score rollup

Done when: a table `rounds flown → drops taken → which score drops` can be written
without re-opening the source, and the tie-break behaviour is stated (even if the
answer is "arbitrary — first row wins").

### WI-5 — Final ranking, tie-breaks, percent, fly-offs

Scope: `dtCompResults_FillPilotRankAndPcnt`
(`Rpt_Results_Calculations_MOD.vb:730-1084`), the F3K equal-rank resolution block
(:806+, uses `Drop1F3K…Drop5F3K` columns), `Rpt_Results_Overall_MOD.vb`
(sort orders :277/:293/:304, `Resolve_FlyOff_Rank_If_Same_Scores` :235,
fly-off round handling).

Deliverable — findings section *Ranking & tie-breaks*:

- primary sort keys and direction; what `HiddenRanking` is for vs the displayed
  `"=n"` rank string
- the full tie-break ladder per class family: Score → RawScore → (F3K) best
  dropped score chain → ? ; where the ladder ends in "still tied"
- Percent column formula (:797-803) and whether anything consumes it
- fly-off/preliminary-final rank override mechanics (note only — merged/prelim
  comps are a §6 concept gap, out of scope)
- team/series filtering effects on ranking scope (note only, same reason)

Done when: the ladder is expressible as an ordered list of comparisons our engine
could mirror, with class conditions attached.

### WI-6 — Reconciliation gate and consolidation

Depends on WI-1..WI-5 all done. Two parts:

1. **Reconcile the sample comp end-to-end.** Throwaway script (in `/tmp/opencode`,
   not committed — house rule; the golden-fixture pipeline will productise the
   extractor): re-read `/home/pete/Downloads/GliderScoreDownload.txt` (extracts
   still exist at `/tmp/opencode/gs_data.json` / `gs_schema.json` today but are
   ephemeral), recompute all 30 `Scores` rows' `RawScore` and `NormalisedScore`
   from raw inputs + comp config, following the WI-1/WI-2/WI-3 formulas literally,
   and diff against persisted values. Every mismatch must be either fixed in the
   findings or explained (e.g. settings changed after scoring). This is the gate
   that discharges db-analysis §4's "do not trust import arithmetic until resolved".
2. **Consolidate**: reconcile the five findings sections into a single coherent
   formula narrative at the top of *Findings*; fill *Divergences* (each with an
   FAI/NZ rule citation via the `fai-rules` skill); write *Handoff notes* for the
   two downstream stories (fields the fixture must carry: GroupScoreOption,
   GroupScoreDecimals, RoundOrTruncate, varFltDednIdx source column, decimals vars,
   drop columns; float32-oracle caveat for the comparator).

Done when: all 30 rows reconcile (or every exception has a written explanation),
and the two downstream backlog stories have their open questions answerable from
this file alone.

### Execution waves for sub-agents

- **Wave 1** (parallel, independent reading): WI-1, WI-3, WI-4, WI-5
- **Wave 2**: WI-2 (needs WI-1 helper semantics + WI-3 rounding map)
- **Wave 3**: WI-6 (gate + consolidation; single agent)

Each wave-1 agent must leave its findings section in the state it would want to
inherit: claims cited, unresolved items flagged inline rather than omitted.

## Before starting

- Pure reading + one throwaway reconciliation script; no Soarscore code changes.
- Findings live here, not `/docs` (house rule 4). Do not edit
  `gliderscore-db-analysis.md` — it is a snapshot; if this story invalidates it,
  say so in Handoff notes.
- The GliderScore source tree may be newer than the v6.78 export; pin versions
  first and attribute findings.
- Property-test invariants named in the stub (normalisation scaling, drop
  monotonicity) are *later* work — capture them as candidate invariant statements
  in the findings, but they land via the scoring stories, not this one.

## Findings

*(WIs write below as they complete; WI-6 consolidates.)*

### Formula narrative (consolidated)

**Attribution:** all arithmetic below is GliderScore **DBVersion 6.78** (`/home/pete/source/gliderscore/GliderScore_Master/app.config:591-592`). Validated end-to-end: all 30 persisted `Scores` rows of the ALES sample comp recompute exactly from raw inputs + config and the ranking reproduces — see *Reconciliation result*. This walks execution order only; depth lives in the five wave sections below.

**1 · Input encoding — packed mmss.s.** Time columns hold minutes×100+seconds (`500.0` = 300 s): `GetTimeInSeconds(v) = Fix(v/100)·60 + (v − 100·Fix(v/100))` (`Scoring_MOD.vb:626-631`; seconds < 60 enforced at `Scoring.vb:1494-1502`). Two timekeepers average decoded times, falling back to the single non-zero time (`CalcTimeScore`, `GlobalFunctions_MOD.vb:3677-3706`). No rounding anywhere in raw-score math — family timing-decimals vars are display-only (rounding block commented out, `Scoring_MOD.vb:663-673`).

**2 · Per-family RawScore** — `Update_RawScore`, Select Case on TaskNo (`Scoring_MOD.vb:137-244`, branch :162):
- **Duration** (:164-184): `TS = CalcTimeScore(...)`; if `TS > TargetTime` (per-round via `GetDurTargetTime`, :884-908): cap at target when `varFltDednIdx=3` ∧ class ∉ {DurALES, F3G}, else `TS -= 2·(TS − Target)` (:651-657); `TS *= DurPointsPerSecond` (:658); `+ LandingScore` (exact-match `LndgPoints` PK lookup + nudge-round, :169/:737-764); `− FltPenalty` per varFltDednIdx 0 none / 1 late-landing pts / 2 motor-run mss→s / 3 height penalty / 4 not-handled-here (:174-179); idx 3 penalty = linear two-rate `H≤refH ? H·upTo : refH·upTo + (H−refH)·over` (`CalcHeightPenalty`, `GlobalFunctions_MOD.vb:4910-4927`); floor `RawScore ≥ 0` (:182).
- **Distance**: `RawScore = Laps` (:186-191). **Speed**: averaged seconds untouched, lower-better (:193-198, :678-689).
- **F5B** (:200-208): decayed time + landing + `Laps·PointsPerLap` − motor-run/watt-min penalties − `CInt(Time1Secs)` (:694-706, :1962-1989).
- **F3K** (:210-216 → `CalcRawScoreF3K` :1467-1887): task string → seven slots summed positionally; landing-distance and deduction slots count as flight times; 2024 variant cuts working window by 1 s per non-zero slot via `getNbrOfFlights` (:611-621); any violation ⇒ 0.
- **F5K** (:227-238): plain sum of four per-flight point columns (:236).

**3 · Persist cast #1.** `RawScore` written through an OleDbParameter typed `Single` (`Scoring_MOD.vb:2767-2774`; insert :2943-2950) — first Double→binary32 rounding, on disk before anything reads it back.

**4 · Group-normalisation matrix** — `Update_GroupScores` per distinct (TaskNo,RoundNo,GroupNo,ReFlightNo) (:247-486), group max by explicit scan (:283-289), over `GroupScoreOption` 0 None / 1 Points / 2 Time:
- Opt 0: `NS = RoundNumber-or-TruncateNumber(Raw, GroupScoreDecimals)`, floor ≥ 0, early return — nothing caps 1000 (:269-281).
- Duration opt 2 idx 0 (**the >1000 mechanism**, :335-376): pass 1 recomputes TimeScores to find MaxTS; pass 2 `NormTime = 1000·TS/MaxTS` rounded/truncated, then **`NS = NormTime + freshly looked-up landing`** (:366), sum *not* re-rounded (:367) ⇒ winner ≤ 1000 + max landing by design.
- Duration opt 2 idx 3 (:381-414): basis `TS − HeightPenalty`, landing added after from stored column (:407); no zero-max fallback, no floor.
- Duration opt 1 (+ idx 4 only): `1000·Raw/MaxRaw` (:303), then `NS -= FlightScoreDeduction` floored ≥ 0 (:319-327). Duration opt 2 × idx ∈ {1,2,4}: no branch fires — NS stays stale.
- Distance/F5B/F3K/F5K, any non-zero option: `1000·Raw/MaxRaw` (:416-439). **Speed**: inverse `NS = 1000·MinPositive/Raw` (:442-482) — fastest ⇒ exactly 1000 pre-rounding.
- Rounding: `RoundNumber` half-up via `Int(x + half·10⁻ᵈ)` / `TruncateNumber` with +1e-6 nudge (`GlobalFunctions_MOD.vb:3116-3134` / :3155-3176); fp-boundary quirks in Precision & storage §1–§3.

**5 · Persist cast #2.** `NormalisedScore` likewise Single-typed on write (`Scoring_MOD.vb:2771`/`:2947`) — second float32 rounding.

**6 · Aggregation + drop-worst.** Report build sums best-per-original-round/task NormalisedScore (re-flights de-duplicated keeping highest), minus |penalties|, floored ≥ 0 (`Rpt_Results_Overall_MOD.vb:2690-2712`). Drop N activates iff rounds flown ≥ `Comps.DropNAtRound` (99 = never); option 0 drops lowest task cells / option 1 lowest round totals (`Rpt_Results_Calculations_MOD.vb:412-415`, :437/:546); candidates sorted `…ASC, RoundNo DESC` so the latest equally-bad score drops first (:469/:560); dropped cells negated in place, sentinel ±0.00001 (:694-727); `Score -= positive dropped value` (:496 etc.). Factory defaults incl. DurALES = no drop (`Comps1.vb:6382-6384`) — full table in Drop-worst §3.

**7 · Report-time re-round → ranking.** `dtCompResults_FillPilotRankAndPcnt` re-rounds `Score` and `RawScore` to GroupScoreDecimals **before** any comparison (`Rpt_Results_Calculations_MOD.vb:736-740`); sort `"Score DESC, RawScore DESC"` (:748); ties display `"=n"` while HiddenRanking stays a distinct total order (:778-794); F3K-only rescue chain over cumulative dropped scores (:806-1029); otherwise tied pilots share rank — ladder ends. `Percent = 100·Score/HighScore`, unrounded, never a ranking input (:797-803). Full ladder in Ranking & tie-breaks.

### Raw score

All references are to files in `/home/pete/source/gliderscore/GliderScore_Master/`. Line numbers verified by reading. Project compiles with `Option Strict Off` (`GliderScore_Master.vbproj:23`), so implicit narrowing conversions occur throughout; where VB semantics matter they are called out below.

#### Score pipeline and persistence

- `Update_AllScores(CompNo)` (`Scoring_MOD.vb:5`) loads all `Scores` rows joined to `Pilots`/`CompPilots` (:37–48), adds in-memory columns `TimeScore`, `LandingScore`, `FltPenalty` (:51–53), then calls `Update_RawScore` **only for rows whose persisted `Updated` column already equals `"True"`** (:66–72). It then recomputes group (normalised) scores for every distinct `(TaskNo,RoundNo,GroupNo,ReFlightNo)` touched (:87–119) and persists everything via `daScores.Update(dtScores)` with the UPDATE command built by `Scores_SetUpdateCommands` (`Scoring_MOD.vb:2698`, writes `RawScore`, `NormalisedScore`, `Updated`, etc., keyed on CompNo/TaskNo/RoundNo/GroupNo/ReFlightNo/PilotNo).
- During interactive data entry the row is scored directly: `DgvScores_CellValidated` → `CellChangedValidatedAction` (`Scoring.vb:2587`) → `Update_RawScore(drv, varCompNo)` → `Update_GroupScores(...)` → `Scores_Save()`. So raw scores are recomputed on every validated cell edit regardless of the `Updated` gate; `Update_AllScores` is the bulk re-score path (e.g. after config change).

#### What the time columns actually hold (corrects the earlier analysis)

The time columns hold a **packed mmss.s number — minutes×100 + seconds — not minutes and not seconds**: `GetTimeInSeconds(v) = Fix(v/100)*60 + (v − 100*Fix(v/100))` (`Scoring_MOD.vb:626–631`). Comment elsewhere confirms: "motor run is in mss format eg 101 = one minute and one second" (`Scoring_MOD.vb:1963`). Entry UI enforces this: "to enter 1 minute and 15 seconds, type 115", seconds part must be < 60 (`Scoring.vb:1494–1502`, `ValidateTimeData` at `GlobalFunctions_MOD.vb:3370+`; also `ImportScores_CheckTimeData` messages at `Comps1_MOD.vb:880–886`).

| Column | Duration/Speed/F5B meaning | F3K meaning | F5K meaning |
|---|---|---|---|
| `Time1Mins` | flight time #1, packed mmss.s | flight score #2, packed mmss.s | **points of flight 2** |
| `Time1Secs` | unused (0) | flight score #3 | **points of flight 3** |
| `Time2Mins` | flight time #2 (timekeeper 2), packed mmss.s | flight score #4 | **points of flight 4** |
| `Time2Secs` | F5B only: motor-run / no-laps-penalty, packed mmss.s (integer) | flight score #5 | unused (0) |
| `Laps` | unused | flight score #1 | **points of flight 1** |
| `FlightScoreDeduction` | deduction payload (units depend on `varFltDednIdx`: points / mss motor-run / metres start-height / watt-min) | flight score #7 | unused |
| `Landing` | landing distance bucket (metres, 1 dp) | flight score #6 | unused |

So the earlier doc's claim "`Time1Mins` holds **seconds**" is wrong; e.g. pilot 70's `500.0` is 5 min = 300 s, proven by its `RawScore` 330 = 300 + 30 landing (see worked example).

#### Per-family raw-score formulas (`Update_RawScore`, `Scoring_MOD.vb:137–244`; branch on `drv("TaskNo")` at :162)

Null traps zero-fill all inputs first (:142–155). `FltPenalty` is an in-memory/persisted helper column initialised to 0 if DBNull (:151).

**Case 1 — Duration (:164–184)**

```
TargetTime   = CDbl(GetDurTargetTime(RoundNo, CompNo))            ' :166; String return, CDbl at call site
TimeScore    = CalcTimeScore(Time1Mins, Time2Mins, varDurTimekeepers)   ' GetTimeScore Case 1, :648
If TimeScore > TargetTime Then                                    ' :651
    If varFltDednIdx = 3 And class <> "DurALES" And class <> "F3G" Then TimeScore = TargetTime  ' cap (F5J-style)
    Else TimeScore -= 2*(TimeScore - TargetTime)                  ' symmetric over-target decay, may go negative
TimeScore *= varDurPointsPerSecond                                ' :658
LandingScore  = exact-match LndgPoints lookup(Landing)            ' :169 → GetLandingBonus Task 1
FltPenalty    = f(varFltDednIdx, FlightScoreDeduction)            ' :174–179, see table below
RawScore = TimeScore + LandingScore − FltPenalty                  ' :181
If RawScore < 0 Then RawScore = 0                                 ' :182 floor (Duration only)
If TimeScore > 0 Then Updated = "True"                            ' :183
```
`CalcTimeScore(T1, T2, n)` (`GlobalFunctions_MOD.vb:3677–3706`): n=1 → `GetTimeInSeconds(T1)`; n=2 → average of the two decoded times, falling back to whichever single nonzero time exists if one is 0.

`GetDurTargetTime` (`Scoring_MOD.vb:884–908`): per-round override from `DurTargetTimeByRound.TargetTime` else default `Dur.durTargetTime`.

**Case 2 — Distance (:186–191)**: `RawScore = Laps` verbatim (`GetLapsScore` Task 2 just returns `CDbl(drv("Laps"))`, `Scoring_MOD.vb:816–819`). No rounding, no floor. `Updated="True"` if > 0.

**Case 3 — Speed (:193–198)**: `RawScore = CalcTimeScore(Time1Mins, Time2Mins, varSpdTimekeepers)` (`Scoring_MOD.vb:678`) — i.e. the averaged flight time in seconds (lower = better; inversion happens at normalisation). No multiplier, no rounding (all rounding code commented out, :680–689), no over-target logic, no floor.

**Case 4 — F5B (:200–208)**

```
TimeScore = CalcTimeScore(Time1Mins, Time2Mins, varF5BTimekeepers)          ' :694
If TimeScore > varF5BTargetTime Then TimeScore -= 2*(over)                  ' :696–698, always decay (no cap)
TimeScore -= GetF5BMotorRunPenalty(Time2Secs) - GetF5BWattMinPenalty(FlightScoreDeduction)  ' :701–704
LandingScore = landing-table exact match (Task 4 path)                      ' :203
LapsScore    = Laps * varF5BPointsPerLap (rounded, :821–836)                ' :204
RawScore = TimeScore + LandingScore + LapsScore − CInt(Time1Secs)           ' :205–206  (no floor)
```
- `GetF5BMotorRunPenalty` (`Scoring_MOD.vb:1962–1968`): decodes `Time2Secs` as mmss → whole seconds (`Math.Truncate`) × `varF5BPointsPerSecondOfMotorRun`. Quirk recorded neutrally: the same column doubles as the fixed "No Laps Flown Penalty" entry (validated to be only 0 or 30 at `Scoring.vb:1688–1694`); entering 30 decodes to exactly 30 s, giving −30 when the per-second rate is 1.
- `GetF5BWattMinPenalty` (`Scoring_MOD.vb:1970–1989`): `excess = FlightScoreDeduction − varF5BWattMinBaseQuantity`; blocks = `Ceiling` / `Floor` / exact division of `excess ÷ varF5BWattMinQuantity` per `varF5BWattMinRoundingIdx` (0/1/2); penalty = `CInt(blocks × varF5BWattMinPoints)`. Note `WattMinBlocks` is declared `Single` (:1977) — minor precision difference.
- The `- CInt(drv("Time1Secs"))` term at :205 subtracts raw points entered in `Time1Secs` (same 0-or-30 mechanism), unvalidated at this layer.

**Case 5 — F3K (:210–216)**: `RawScore = CalcRawScoreF3K(drv)` (:214), see below. `Updated="True"` if result > 0.

**Case 6 — F5K (:227–238)**: `RawScore = Laps + Time1Mins + Time1Secs + Time2Mins` (:236) — a plain sum of four already-computed per-flight **point** values stored in those columns by the F5K entry screens (see F5K section below). No rounding, no floor here.

#### `varFltDednIdx` semantics (verified from code, not the comment alone)

Loaded from `Dur.DurFlightPenalty` at startup (`GlobalFunctions_MOD.vb:1316`); set from setup-UI combo `durTypeOfFltDedn.SelectedIndex` whose items are, in order, None / Late Landing / Motor Run / Height Penalty / Penalty F3Q (`Comps1_MOD.vb:114–118`, mirrored with comment at `Comps1Saved.vb:98–106` and `Comps1.vb:132`; UI handler `Comps1Saved.vb:2691–2727` sets `dr("durFlightPenalty")` and the global at :2699/:2705). Import validation independently labels each index's payload ("Late Landing" integer points, "Watt-min" integer, "Motor Run" mss number, "Start height" integer metres) at `Comps1_MOD.vb:909–951`.

| Idx | Meaning | Enters Duration RawScore how (`Scoring_MOD.vb:174–179`) |
|---|---|---|
| 0 | None | `FltPenalty = 0` |
| 1 | Late Landing | `FltPenalty = CDbl(FlightScoreDeduction)` — raw **points** subtracted from sum |
| 2 | Motor Run | `FltPenalty = GetTimeInSeconds(FlightScoreDeduction)` — mss payload decoded to **seconds**, subtracted 1-for-1 against point-sum (seconds ≡ points via `DurPointsPerSecond`) |
| 3 | Height Penalty | `FltPenalty = GetHeightPenalty(FlightScoreDeduction, CompNo)` — see below; subtracted |
| 4 | Penalty F3Q | **no `Case 4` and no `Case Else`** — `FltPenalty` keeps its previous value (0 unless a prior calc under another idx left residue). The deduction never enters RawScore; it is applied at normalisation: `NormalisedScore -= FlightScoreDeduction` floored at 0 when `varFltDednIdx=4` (`Scoring_MOD.vb:319–327`), and additionally displayed added (+) into a grid-only `NormalisedPoints` column for F3Q (`Scoring_MOD.vb:3759–3764`). |

Height penalty chain: `GetHeightPenalty(H, CompNo)` (`Scoring_MOD.vb:848–861`) reads `durRefHeight`, `durPenaltyUpToRefHeight`, `durPenaltyOverRefHeight` straight from the `Dur` table (:863–882) and calls `CalcHeightPenalty` (`GlobalFunctions_MOD.vb:4910–4927`):

```
H ≤ refH :  penalty = H·upTo
H > refH :  penalty = refH·upTo + (H − refH)·over
```

Interaction with the target-time rule: idx=3 caps TimeScore at TargetTime **except** for classes `DurALES` and `F3G` (`Scoring_MOD.vb:652`), which take the symmetric-decay branch even with a height penalty configured.

#### Zero-floors and side effects

- **Floor:** only Duration floors `RawScore` at 0 (`Scoring_MOD.vb:182`). Tasks 2–6 store whatever the arithmetic yields (negative values are practically unreachable there except theoretically in F5B/F5K penalty stacks).
- **`Updated`:** set to `"True"` inside each case when the primary component is positive (Duration: `TimeScore>0` :183; Distance `LapsScore>0` :190; Speed :197; F5B `TimeScore>0` :207; F3K `RawScore>0` :215; F5K `RawScore>0` :237). It is **never set back to `"False"` by scoring code**; the entry screen toggles it manually via double-click on the Updated cell (`HandleClickInUpdatedColumn`, `Scoring.vb:7128–7163`) or zeroes it with the row via `ZeroScoresEnteredOnRow` (`Scoring.vb:6936–6952`). Because `Update_AllScores` re-scores only rows flagged `"True"` (:67), a stale `"True"` on an empty row yields RawScore 0 via the traps.
- **`FltPenalty`:** written only in Duration case (:175–178); left untouched for other tasks.
- **`LandingScore` / `TimeScore`:** in-memory columns filled during Duration/F5B (:169, :203); `Try/Catch` around the Duration landing call sets `LandingScore=0` on any exception (:168–172).
- **`Penalty`:** the standalone penalty column is never touched by `Update_RawScore` (it feeds team/round penalties elsewhere; F5K-2024 accumulates safety penalties into it at `Scoring_MOD.vb:2227–2230`).
- **Rounding of persisted `RawScore`:** none as a whole; only sub-scores are rounded (landing, F5B laps — see next sections). All timing-decimals variables are **display-only** (see below).

#### Landing distance → points (`GetLandingBonus`, `Scoring_MOD.vb:726–803`)

Table source: rows of `LndgPoints` for `LndgNo = durLndgScheme`, loaded once into `varDTLndgs` with an appended `{Distance=0, Points=0}` row and `Distance` made the primary key (`GlobalFunctions_MOD.vb:1319–1330`).

- **Resolution is exact-match on the stored distance value — no buckets, no interpolation, no nearest.**
  - Task 1: `dtLandings.Rows.Find(LndgDistance)` (PK lookup, :745); miss → `LndgScore = 0` (:747–748); `Landing=0` short-circuits to 0 (:737–739).
  - Task 4: `varDTLndgs.Select("Distance=" & …)` (:778) and then **unguarded** `drLndg(0)("Points")` (:779) — a non-matching distance would throw rather than score 0; entry-time validation (`Validate_Landing_Data`, `Scoring.vb:1997–2032`) is what keeps values on the table.
- Distances outside the table therefore score **0** (Task 1) or throw (Task 4, if ever reached with a bad value).
- After lookup, points get a decimal nudge then round-half-up via `Int()`: `+0.05/0.005/0.0005/0.00005` per `varGroupScoreDecimals` (0–3) (:756–764, :787–795), then `RoundNumber` (`GlobalFunctions_MOD.vb:3116–3134`, `Int(Nbr + halfUnit)` scaled — deliberately *not* VB banker's rounding). With integer point tables (the norm) this is the identity.

#### Duration time→points curve (`GetTimeScore` Case 1, `Scoring_MOD.vb:645–673`)

- Input precision is **full Double seconds after mmss decode**; there is **no whole-second rounding anywhere in the raw-score math**. The `DecimalsForTiming` parameter and every `RoundNumber(TimeScore, …)` call are commented out (:663–673, :680–689). Consequently `varDurTimeDecimals` / `varSpdTimeDecimals` / `varF5BTimeDecimals` / `varF3KTimeDecimals` / `varF5KTimeDecimals` affect **only data-entry display format** (`GetDefaultCellTimeFormat`, `Scoring.vb:4332–4349`) and message strings (`FormatSecondsToMinSec`, `GlobalFunctions_MOD.vb:3762+`) — never the stored score.
- Cap vs decay: over-target time is either capped (idx=3, non-DurALES/non-F3G) or decays 2 pts/s past target (:651–657); the commented-out `If TimeScore < 0 Then TimeScore = 0` (:661) shows negatives were considered — today only the final Duration floor at :182 catches them.
- Points scaling: ×`varDurPointsPerSecond` (:658) (sample comp: 1.0).
- Speed returns the raw averaged seconds with no transformation (:678–689).

#### F3K (`CalcRawScoreF3K`, `Scoring_MOD.vb:1467–1887`)

- Task string comes from `F3KTaskByRound.Task` for the round (`GetF3KTaskForRound`, `GlobalFunctions_MOD.vb:686–699`).
- Seven input slots are decoded mmss→seconds into `ScrArr(0..6)` = `Laps, Time1Mins, Time1Secs, Time2Mins, Time2Secs, Landing, FlightScoreDeduction` (:1477–1484). **Landing distance and the deduction slot are treated as flight times** — they participate in working-window sums and, in tasks that count all entries, are literally added into points (recorded neutrally; divergent from FAI intent).
- Per-task Select Case (:1503–1854) computes `Points`:
  - A/A(2)/B/C/J/K/L/L(1)/M/N style tasks: positional slots with per-flight maxima; exceeding any max sets `FlightWorkingTimeExceeded` → `Points = 0`.
  - D, H: ladders assigning descending targets (D: 30 s then +15 s iterating slots 0–6; H: sort ascending, clamp to 240/180/120/60 walking down).
  - E/E(1)/E(2), O/U10, U15: plain sums (O/U10 sum **all seven** slots; window 600 s or 900 s).
  - F, G, I: first-n slots with per-flight caps.
- 2024 rule: nominal working window reduced by **1 second per flight** using `getNbrOfFlights` (`Scoring_MOD.vb:611–621`), which counts non-zero entries across all seven slots — including the landing and deduction slots (:1587, :1610, :1630, etc.). Applies to D(1), E(1), E(2), F, G, H, I, J, K, L, L(1), M, N, O/U10, U15 but not old E or D.
- Window/per-flight violations raise `MsgBox` and force `Points = 0` (:1858–1873). The same checks exist earlier in entry validation (`Scoring.vb:1735–1983`) which cancels the edit outright; `CalcRawScoreF3K`'s own check covers bulk rescoring paths.
- Result is a pure sum of non-negative decoded times → never negative; zero on any violation.

#### F5K four-flights-in-four-columns packing and height bonus

- Mapping (comment `Scoring_MOD.vb:229–234`, enforced identically in three places): flight 1→`Laps`, flight 2→`Time1Mins`, flight 3→`Time1Secs`, flight 4→`Time2Mins`; `RawScore` = their sum (`Update_RawScore` Case 6 :236; bulk `ReCalcRawScoreF5K` :2050–2057; string writer `Update_F5KFlightDataStrings` :2170–2174). Full per-flight detail (times, heights, bonuses, penalties) persists in the semicolon-separated descriptor strings `Flight1..Flight4` (`key=value&…`, :2149–2167), parsed back via `getParamValue` (`GlobalFunctions_MOD.vb:2042`).
- Per-flight points come from `CalcRawScoreF5K` (`Scoring_MOD.vb:2237–2427`) per round-task code (A/B/C/D/E):
  `FltPts = TimPts + HgtPts + LandOutPlty + FltPenalties [+ NbrFltsPlty]` where
  - `TimPts` = mmss-decoded seconds capped at the task's target; tasks A and D sort flights descending and assign targets 240/180/120(/90…) to the longest flights first (:2259–2272, :2308, :2391);
  - `LandOutPlty` = −10 if landed out and flight flew, else 0 (:2146–2147, :2301–2303);
  - `HgtPts` = `GetF5KBonusOrPenalty` (:3299–3367): with the `F5KBonusData` rows for the selected bonus scheme filtered and sorted `Metres DESC` (:2246–2248), `diff = launchHeight − refHeight`; above reference the function walks metre-by-metre accumulating each bracket's `BonusPointsPerMetre` while `diff >= Metres`, rounds to 1 dp and **negates** (penalty); below reference it walks upward and returns a positive bonus; diff 0 or launch 0 → 0. Bonus suppressed entirely if flight shorter than `varF5KMinTimeForHeightBonus` (:2297–2299).
  - Zero-flight suppressions: land-out/height zeroed when `TimPts < 0.0001` (:2303).
  - The F5K-2024 variant (`CalcRawScoreF5K2024`, :2429+, strings with 3-letter keys) adds `OutOfFld`/`MotorRestart` flight-zeroing, `HitPerson` round-zeroing, late-landing penalties, and accumulates safety penalties into `Penalty` (:2227–2230).
- Sample DB has no bonus schemes defined (`F5KBonusNames`/`F5KBonusData` empty).

#### Validation gate — worked example, reproduced from actual config

Persisted row (CompNo 17, PilotNo 13, Round 1, TaskNo 1): `Time1Mins=200.0`, `Time1Secs=0.0`, `Time2Mins=0.0`, `Time2Secs=0.0`, `Laps=0.0`, `FlightScoreDeduction=0.0`, `Landing=3.0`; persisted `RawScore=160.0`, `Updated="True"` (`gs_data.json` → `Scores`).

Config consumed (`gs_data.json` → `Comps`, `Dur`, `LndgPoints`, `DurTargetTimeByRound`): `GSCompClass=DurALES`; `durFlightPenalty=0` (⇒ `varFltDednIdx=0`); `durNumberOfTimekeepers=1`; `DurPointsPerSecond=1.0`; `durTargetTime=600` and `DurTargetTimeByRound` empty ⇒ TargetTime=600; `durLndg=6` ⇒ scheme 6 table {1→50, 2→45, 3→40, …}; `GroupScoreDecimals=0`.

Arithmetic through the code path:

1. `Update_AllScores` sees `Updated="True"` → `Update_RawScore` (`Scoring_MOD.vb:67–68`).
2. `TargetTime = CDbl(GetDurTargetTime(1,17)) = 600.0` (:166; :884–908).
3. `CalcTimeScore(200.0, 0.0, 1)` = `GetTimeInSeconds(200.0)` = `Fix(200/100)=2` min, `200−200=0` s → **120.0 s** (:648; `GlobalFunctions_MOD.vb:3686`).
4. Over-target test: 120 ≤ 600 → skip (:651). `TimeScore = 120.0 × 1.0 = 120.0` (:658).
5. `LandingScore = GetLandingBonus(3.0, 1)`: PK-exact match `Distance=3.0` → `Points=40`; nudge `40+0.05` then `Int(40.55)=40` (:169; :745–764).
6. `varFltDednIdx=0` → `FltPenalty = 0` (:175).
7. `RawScore = 120.0 + 40 − 0 = 160.0` (:181). Floor not triggered (:182). `TimeScore>0` → `Updated="True"` (:183). ✔ matches persisted 160.0/"True".

Cross-checks (all reproduce): pilot 48 `400.0`→240 s +35 = **275** ✔; pilot 70 `500.0`→300 s +30 = **330** ✔. Group-score-on-time normalisation (`GroupScoreOption=2`, idx=0 branch, `Scoring_MOD.vb:335–376`; max TimeScore=300): pilot 13 `1000·120/300 + 40 = 440` ✔, pilot 48 `800+35=835` ✔, pilot 70 `1000+30=1030` ✔ — which also resolves §4's "Unresolved" normalisation item of the earlier analysis document.

**Correction to the earlier analysis (§4):** the "200 s flown, 40-point height-penalty deduction" reading is contradicted by the data. `FlightScoreDeduction=0.0` and `durFlightPenalty=0`, so `GetHeightPenalty` is never invoked for this row; the 160 = 120 time-points + 40 landing-points. The flight was 2 min 0 s (packed `200.0`), not 200 s. Had a real height penalty applied, the arithmetic would be `CalcHeightPenalty` as above with rates 0.5 up to / 3.0 over 200 m.

#### Unresolved

- *(none blocking)* Two behavioural oddities worth flagging for later agents:
  - `GetLandingBonus` Task 4 indexes `drLndg(0)` without a bounds check (`Scoring_MOD.vb:779`) — out-of-table F5B distances would throw rather than score 0 (entry validation masks this).
  - `CellChangedValidatedAction` passes the **default** `varDurTargetTime` to `Update_GroupScores` (`Scoring.vb:2604–2610`) instead of the per-round value; a comp using `DurTargetTimeByRound` overrides could normalise interactive edits against the wrong target. Not exercised by the sample data.

### Normalisation

All references are to files in `/home/pete/source/gliderscore/GliderScore_Master/`. Line numbers verified by reading. VB semantics flagged inline where they change arithmetic (`Int` floors toward −∞, `Fix` truncates toward 0, `Option Strict Off` makes every `DataRow` access a late-bound `Object→Double` conversion).

#### Names and configuration plumbing

- **The real names:** DB column **`Comps.GroupScoreOption`** (migration `UpdateDatabase_MOD.vb:55-56`; read at `GlobalFunctions_MOD.vb:1479`); module global **`varGroupScoringOption`** with the authoritative decode comment `' 0 = None; 1 = Score; 2 = Time'` (`GlobalVariables_MOD.vb:81`). The function parameter is spelled `GroupScoringOption` (`Scoring_MOD.vb:253`) — same value, different name.
- **All values decoded:** UI combo `cbGroupScore` items in order are None / Points / Time (`Comps1_MOD.vb:122-124`); the stored value is the SelectedIndex (`Comps1Saved.vb:1839`, live twin `Comps1.vb:2953`). Migration backfill sets 1 ("based on Score (not Time only)") or 0 ("no group scoring") by class (`UpdateDatabase_MOD.vb:70,:72`); new-comp default 1 (`CompNew.vb:691`); copy inherits (`CompNew.vb:647`).
- **UI-only constraints:** for F5B/F3K comps the "Time" item is removed and a saved 2 is coerced to 1 (`Comps1.vb:1964-1972`) — but the engine itself treats tasks 2/4/5/6 identically for *any* non-zero option (below), so this is presentational. For DurALES the option is **forced** to 2 with an author message stating intent: *"For ALES class the only group scoring option is Time. Flight times are normalised and landing points are added after that… Height penalty may be selected. If selected, time minus height penalty is normalised and landing points are added after that."* (`Comps1.vb:5250-5255`), and the generic description *"TIME - means that the best time gets 1000 points And landing points are then added"* (`Comps1.vb:5266-5267`). This is direct authorial confirmation of the >1000 mechanism.
- Changing the option mid-comp prompts confirmation, then calls `UpdateScores()` → full rescore (`Comps1.vb:5270-5278`); likewise `RoundOrTruncate_SelectedIndexChanged` (`Comps1.vb:5281-5286`) and `CbGroupScoreDecimals_SelectedIndexChanged` (`Comps1.vb:5291-5295`).

#### Shape of `Update_GroupScores` (`Scoring_MOD.vb:247-486`)

Rows are filtered to one **group key = TaskNo ∧ RoundNo ∧ GroupNo ∧ ReFlightNo** (`dvScr.RowFilter`, `Scoring_MOD.vb:262`; the filter literal spells `ReflightNo` but DataColumn name matching is case-insensitive) and sorted `RawScore DESC` (:263). The caller enumerates **distinct** `(TaskNo,RoundNo,GroupNo,ReFlightNo)` over the whole scored table (`Update_AllScores`, `Scoring_MOD.vb:75-93`), so **every group is normalised against its own maximum only** — cross-group comparability comes purely from each group's top score mapping to 1000. A re-flight lives in its own `(RoundNo,GroupNo,ReFlightNo)` group and is normalised independently of the original round.

**Option 0 — None (raw passthrough)** (:269-281): `NormalisedScore = RoundNumber(RawScore, GroupScoreDecimals)` or `TruncateNumber(…)` per `RoundOrTruncate` 0/1 (:273,:275), floored `<0 → 0` (:277), then early `Return True` (:280) — **before** the max-scan. Nothing caps at 1000: any Raw > 1000 persists verbatim (rounded). Rounding is exactly the half-up-via-`Int` / nudge-and-floor pair documented under Precision & storage.

**Max-by-explicit-loop** (:283-289): for every non-zero option, `MaxRawScore` is found by an explicit scan, not from the sorted view. Verbatim reason (:283):

> `' had to introduce this because the dataview sort was not working for F5K. Couldn't discover why.`

i.e. the author distrusted `DataView.Sort`'s ordering for F5K rows (root cause not documented anywhere in the tree) and scans defensively instead of taking `dvScr(0)`. The sort itself stays in force (:263) and only determines iteration order, which is harmless because each row's computation is independent. The comparisons are late-bound `Object > Double` (:286) — fine under `Option Strict Off`.

**Zero-max guard:** every dividing branch guards `max > 0` and writes 0 otherwise — points `:302/:312-315`, time `:351/:369-374`, cases 2/4/5/6 `:422/:435-438`, speed exits wholesale `:445-451`. Exception: the height-penalty branch's guard has its else **commented out** (`:409-411`) — a non-positive max leaves all rows stale (flagged below).

#### Decision matrix — `GroupScoreOption` × task family × `varFltDednIdx`

| Family | Opt 0 (None) | Opt 1 (Points) | Opt 2 (Time) |
|---|---|---|---|
| **1 Duration** | rounded/truncated `RawScore` (:273-277) — any `varFltDednIdx` | `1000·Raw/MaxRaw` (:303), round/trunc (:304-309), floor ≥0 (:310); **if idx=4**, afterwards `NS -= FlightScoreDeduction` per row where payload > 0, floored ≥0 (:319-327) | **idx=0**: normalise `GetTimeScore` alone, add landing after (:335-376) — see below. **idx=3**: normalise `GetTimeScore − GetHeightPenalty`, add landing after (:381-414). **idx ∈ {1,2,4}: no branch fires — `NormalisedScore` is never written; stale value survives** |
| **2 Distance, 4 F5B, 5 F3K, 6 F5K** | rounded/truncated `RawScore` | `1000·Raw/MaxRaw` (:423) — **regardless of `varFltDednIdx`; identical code path for option 2**, since the branch sits outside any option test (:416-439). F3Q deduction is *not* applied here even at idx=4 (moot: F3Q comps are Duration-family) | same as Points |
| **3 Speed** | rounded/truncated `RawScore` | inverse scaling `1000·MinPositive/Raw` (:468) — same for any non-zero option (:442-482) | same |

Details per branch:

1. **Opt 2, idx=0 — the >1000 mechanism** (:335-376). Two passes over the group:
   - Pass 1 (:337-345): `MaxTimeScore` = max of `GetTimeScore(drv, TskNbr=1, TargetTime, DecimalsForTiming:=0, CompNo)` — recomputed from the mmss-packed columns, *not* read from the in-memory column. The basis is therefore the **full Duration point curve**: mmss decode (`Fix(v/100)·60 + …`, `Scoring_MOD.vb:626-631`), over-target symmetric decay (the cap arm at `:652-653` requires idx=3, unreachable here), ×`DurPointsPerSecond` (`:648-658`) — **not raw seconds**. With 1 pt/s they coincide.
   - Pass 2 (:350-375): `NormTimeScore = 1000·TimeScore/MaxTimeScore` (:353), rounded/truncated per `RoundOrTruncate` (:354-359); written to the in-memory `TimeScore` column (:360) — after this point `TimeScore` holds the *normalised* time component, used for screen display (`GlobalFunctions_MOD.vb:3104-3110` recomputes it as `NS − LandingScore` elsewhere). `LandingScore` is **freshly re-derived** via exact-match `GetLandingBonus(Landing, TaskNo=1)` (:363-364; lookup `:737-763` incl. the +half-unit nudge before `RoundNumber`), then **`NormalisedScore = NormTimeScore + LandingScore` (:366)**. **The sum is not re-rounded** — only floored `<0 → 0` (:367). Winner ≤ 1000 + max landing ⇒ scores above 1000 are by design.
2. **Opt 2, idx=3 — height penalty** (:381-414). Basis per row is `GetTimeScore(drv,1,TargetTime,0,CompNo) − GetHeightPenalty(drv("FlightScoreDeduction"), CompNo)` (:387,:397) — yes, the height penalty is deducted from the time-points **before** normalisation, and the group max is the max of that netted quantity (:383-391). After rounding (:399-404) the netted norm goes into `TimeScore` (:405), then **`NS = drv("TimeScore") + drv("LandingScore")` (:407)** using the **stored** landing column filled by `Update_RawScore` (`:169`) — contrast idx=0's fresh lookup — guarded for DBNull (:406). **No floor-at-zero check in this branch**, and none on the sum. Class interaction lives inside `GetTimeScore`: the cap-at-target arm applies only when idx=3 **and** class ∉ {DurALES, F3G}; DurALES/F3G take symmetric decay even with a height penalty configured (`Scoring_MOD.vb:651-657`).
3. **Speed inverse scaling** (:442-482). If `MaxRawScore = 0` → all `NS = 0`, `Exit Function` (:445-451). Otherwise `MinRawScore` starts at `MaxRawScore` (:454) and the scan keeps only strictly-positive raws (:458). Per row: raw > 0 → `NS = 1000·MinRawScore/Raw` (:468) — fastest pilot lands exactly 1000 pre-rounding, everyone slower below — rounded/truncated (:470-475); raw ≤ 0 → `NS = 0` (:479). No negative possible; range (0,1000].
4. **F3Q post-deduction (idx=4)**: lives **only** inside Duration × option 1 (`Scoring_MOD.vb:318-327`), after the points loop: rows with `FlightScoreDeduction > 0` get `NS -= FlightScoreDeduction` (late-bound subtraction), floored at 0 (:323). It is *not* applied in option 0, nor in either option-2 branch, nor in cases 2/4/5/6. (The Raw-score section's separate finding stands: the deduction never enters Duration `RawScore` because the `Select Case varFltDednIdx` at `:174-179` has no Case 4.)
5. **Rounding gaps common to all branches:** every `Select Case RoundOrTruncate` here has no `Case Else` — an out-of-range value leaves `NormalisedScore` **untouched** (stale), not merely unrounded. Same for `NormTimeScore`/`NormScore` locals (:354-359, :399-404). UI constrains `RoundOrTruncate ∈ {0,1}` (`Comps1_MOD.vb:138-141`); DB does not.

#### Re-run paths (who recomputes normalised scores)

`Update_GroupScores` call sites (live code; `Scoring - Copy.vb` and `Comps1Saved.vb` are **not in the .vbproj compile list** — dead backups, the Copy still shows the old 9-arg signature):

- `Scoring_MOD.vb:117` — from `Update_AllScores`.
- Interactive scoring screen (`Scoring.vb`): `CellChangedValidatedAction` :2606 (every validated cell edit; passes module default `varDurTargetTime`, not per-round — see Unresolved), `Leaving_dgvScores` :2637, `Groups_GotFocus` :3079, `F3KbtnClick_FinaliseClickedTask` :4941, `UpdateRoundScores` :5990 (itself called after a target-time edit at :5842 and F5K flight-points bulk entry at :5952), `OpenF5KDataEntryForm` :6634, `OpenF5K2024DataEntryForm` :6763 (+ inner :6674/:6803), `HandleClickInF3KTaskD` :7245.
- `GlobalFunctions_MOD.vb:1606` — inside `dtCompScores_UpdateGroupScores` (:1490), a Restricted-Scope recalc that reads `GroupScoreOption/GroupScoreDecimals/RoundOrTruncate` straight from `Comps` with DBNull fallbacks 1/0/0 (:1564-1580) and a single comp-level TargetTime (:1583-1593, **default only, ignores `DurTargetTimeByRound`**); its only caller is commented out (`Rpt_Results_CompSeries_MOD.vb:1110`) — effectively dead.
- `Rpt_Results_Overall_MOD.vb:2479` — Restricted-Scope Overall report re-runs the whole per-group loop inline (:2437-2480), correctly using per-round `GetDurTargetTime` (:2452).
- `Comps1.vb:6800` — F5K-only settings change (`cbf5kMinTimeForHeightBonus_SelectedIndexChanged`, :6742) after `ReCalcRawScoreF5K()`.

`Update_AllScores` callers: comp-setup saves that alter scoring inputs — `Comps1.vb::UpdateScores` :4261, invoked from nine handlers (`DurTargetTime_Validate` :2258, `DurRefHeight_Update` :2288, `DurPenaltyUpToRefHeight_Update` :2316, `DurPenaltyOverRefHeight_Update` :2344, `F5bTargetTime_Validate` :2375, `F5Bpointsperlap_Validate` :2412, `F5BWattMinPoints_Validate` :2439, `F5BWattMinBaseQuantity_Validate` :2468, `F5BWattMinQuantity_Validate` :2497) plus the three combo handlers above (:5277, :5284, :5294); score import end (`Comps1_MOD.vb::ImportScores` :551); online-score download merge (`ScoringOnlineDownload.vb:210`) and periodic auto-download (`ScoringOnLine_MOD.vb:622` inside `ScoringOnline_Update_AllScores` :618, driven by `ScoringOnlineAutoDownloadUpload.vb:223`); Restricted-Scope Detailed report with an input table (`Rpt_Results_Detailed_MOD.vb:2485`).

#### Validation gate — sample comp reproduced from the matrix

Config (`/tmp/opencode/gs_data.json`): `CompNo=17`, `GSCompClass='DurALES'`, `GroupScoreOption='2'` (Time), `GroupScoreDecimals='0'`, `RoundOrTruncate='0'` (rounded); `durFlightPenalty='0'` ⇒ `varFltDednIdx=0` (`GlobalFunctions_MOD.vb:1315`); `durTargetTime='600'`, `DurTargetTimeByRound` empty ⇒ TargetTime=600 (`Scoring_MOD.vb:884-908`); `DurPointsPerSecond='1.0'`, 1 timekeeper; landing scheme LndgNo 6 = {1→50, 2→45, 3→40, 4→35, 5→30, …}. Branch selected: **Case 1 × option 2 × idx=0** (`Scoring_MOD.vb:335`).

Step by step for the winner (pilot 70, round 1, group 1/0):
1. Pass 1: `GetTimeInSeconds(500.0) = Fix(500/100)·60 + 0 = 300 s`; 300 ≤ 600 so no decay; ×1.0 ⇒ TS=300. All ten rows scanned (including seven all-zero rows, TS=0) ⇒ `MaxTimeScore = 300` (:339-345).
2. Pass 2 (:350-374): `NormTimeScore = 1000·300/300 = 1000.0`; `RoundNumber(1000, 0) = Int(1000 + 0.5) = 1000` (:356; `GlobalFunctions_MOD.vb:3116+`).
3. `LandingScore = GetLandingBonus(5, 1)` → exact PK match Distance 5 → 30 pts; nudge +0.05, `Int(30.05)=30` (:363; `:745-764`).
4. `NormalisedScore = 1000 + 30 = 1030` (:366), sum not re-rounded (:367 no-op). ✔ persisted 1030.0 — **the db-analysis anomaly is resolved: landing points are added after time normalisation, so the group winner exceeds 1000 by design** (author's own description at `Comps1.vb:5250-5252`).

Full round-1 recomputation (script mirrored the code literally; all ten rows in one group):

| Pilot | Time1Mins | TS (s) | NormTime = 1000·TS/300 | Landing | NS = Norm+Lndg | Persisted | Match |
|---|---|---|---|---|---|---|---|
| 70 | 500.0 | 300 | 1000 | 30 | **1030** | 1030.0 | ✔ |
| 48 | 400.0 | 240 | 800 | 35 | **835** | 835.0 | ✔ |
| 13 | 200.0 | 120 | 400 | 40 | **440** | 440.0 | ✔ |
| 12,17,21,28,42,56,65 | 0.0 | 0 | 0 | 0 | **0** | 0.0 | ✔ |

Zero rows pass the `MaxTimeScore > 0` guard and normalise to 0 + landing 0 (:369-374 not triggered). **10/10 match — hypothesis confirmed, no correction needed.**

Candidate property-test invariants surfaced (for later stories, per the plan): scaling — for options 1/2 the per-group maximum maps to exactly 1000 (+landing in option 2); speed inversion — NS ∈ (0, 1000] monotone decreasing in raw time; option 0 preserves raw exactly (post-rounding).

#### Unresolved

- **Root cause of the F5K sort failure** is unknown — the only record is the author's own comment "Couldn't discover why" (`Scoring_MOD.vb:283`). Contained by the explicit max-scan; a harness need only replicate the scan.
- **Duration × option 2 × idx ∈ {1,2,4} writes nothing** — `NormalisedScore` keeps its stale persisted value silently (`Scoring_MOD.vb:291-484` contains no firing branch). Intent unclear (late-landing/motor-run comps arguably should normalise on time-minus-penalty like idx=3); recorded neutrally; not exercised by sample data.
- **Option-2 idx=3 branch has neither a zero-max fallback nor a floor-at-zero** — the else is commented out (:409-411) and no `<0 → 0` check exists (:395-413), unlike every other branch; a pilot whose `TS − HP < 0` while some peer's is positive yields a negative `NormalisedScore` plus landing.
- **`RoundOrTruncate` outside {0,1} leaves `NormalisedScore` stale in every branch** (no `Case Else` at :271-276, :304-309, :354-359, :399-404, :425-430, :470-475) — UI-preventable only.
- Minor, interactive paths only: `CellChangedValidatedAction` (:2600-2606) and the form-level `TargetTime` field (`Scoring.vb:50`, set to `varDurTargetTime` default at :1196) can normalise an edited cell against the **default** target rather than the per-round override; bulk rescoring (`Update_AllScores`) is unaffected (per-round at `Scoring_MOD.vb:98`).

### Precision & storage

**Scope analysed:** `/home/pete/source/gliderscore/GliderScore_Master/`, plus the exported ALES comp at `/tmp/opencode/gs_data.json` / `gs_schema.json`. VB semantics flagged where they bite: VB `Int()` **floors toward −∞** (≡ `Math.Floor`), `Fix()` truncates toward zero; `Single` literals/params are IEEE-754 binary32; project builds with `<OptionStrict>Off</OptionStrict>` (`GliderScore_Master.vbproj:23`), so all `DataRow` accesses are late-bound `Object→Double` conversions with no compile-time checking.

#### 1. `RoundNumber(Nbr As Double, Decs As Integer) As Double` — `GlobalFunctions_MOD.vb:3116-3134`

```
Case 0: Numb = Int(Nbr + 0.5)
Case 1: Numb = Int((Nbr + 0.05)  * 10)  / 10
Case 2: Numb = Int((Nbr + 0.005) * 100) / 100
Case 3: Numb = Int((Nbr + 0.0005)* 1000)/ 1000
```

- **Confirmed half-up, not banker's.** `RoundNumber(2.5,0)=3`, `(3.5,0)=4` (verified numerically; VB `Math.Round` banker's would give 2 and 4). The `Int(+0.5)` idiom is unaffected by VB's rounding reputation.
- **Not decimal-exact half-up either.** The half is added *before* scaling in binary floating point, so values whose true decimal expansion sits exactly on `.xx5` can round **down**: `RoundNumber(0.35, 1) = 0.3` (because 0.35 is stored as 0.34999999999999997…; (x+0.05)\*10 = 3.9999999999999996; `Int` → 3). Same for 2.05, 2.15, 2.55, 4.85, … (8854 boundary divergences found in a systematic scan of d∈{1,2,3}). An exact-match harness must reproduce this exact operation order in binary64 — a Decimal half-up implementation will mismatch.
- **Negatives:** ties round *up toward +∞*, not away from zero: `(-2.5,0) → -2`; `(-2.6,0) → -3`; `(-2.4,0) → -2`. Reachable only through pathological data (RawScore/NormalisedScore are clamped ≥ 0 at `Scoring_MOD.vb:182,277,310` etc.; report-time `RawScore` can only be negative via the −0.00001 drop-marker trick, `Rpt_Results_Calculations_MOD.vb:699,718`).
- **No Case Else:** see §3 — out-of-range `Decs` returns **0.0**, not the input.

#### 2. `TruncateNumber(Nbr As Double, Decs As Integer) As Double` — `GlobalFunctions_MOD.vb:3155-3176`

Adds a fixed **+0.000001** to *every* input (comment: "in case of computer calc error… 999.999999999 would truncate to 999.9, where 1000.0 was intended" — `GlobalFunctions_MOD.vb:3159-3161`), then floors:

```
Nbr += 0.000001
Case 0: Int(Nbr)          Case 1: Int(Nbr*10)/10   Case 2: Int(Nbr*100)/100   Case 3: Int(Nbr*1000)/1000
```

Verified examples:
- `TruncateNumber(999.999999999, 1) = 1000.0` — the fudge works as designed.
- **The fudge flips a digit whenever the true value lies within 1 µ of any truncation boundary:** `TruncateNumber(0.9999994, 0) = 1` (exact truncation gives 0); `TruncateNumber(12.3399995, 2) = 12.34` (exact gives 12.33); contrast `12.3399949 → 12.33`.
- **Negatives:** `Int()` floors, so "truncation" of a negative rounds *down*: `TruncateNumber(-2.3, 0) = -3` (not −2). Only reachable via imported/typed oddities; scores are clamped ≥ 0 downstream.
- Because `Int()` is used (not `Fix`), the function is monotone non-decreasing despite the nudge — no value moves down.

#### 3. `Decs` range

**Claim confirmed: only 0–3 are handled.** Both functions are bare `Select Case Decs … Case 0/1/2/3` with **no `Case Else`** (`GlobalFunctions_MOD.vb:3121-3130`, `3163-3172`). If config held e.g. `Decs = 4`, local `Numb` keeps its default `Double` value **0.0 and is returned** — every score silently becomes **zero**; it does *not* fall through unrounded. UI makes this hard: `cbGroupScoreDecimals` items are exactly `{"0","1","2","3"}` (`Comps1.Designer.vb:2257`), but the DB column is unconstrained (`Byte`) and `CInt(drComps("GroupScoreDecimals"))` is trusted at load (`Comps1Saved.vb:379-380`). Per-task-family timing decimals: Dur/Spd/F5B combos allow `{"0","1","2","3"}` (`Comps1.Designer.vb:714,541,567`); F3K/F5K only `{"0","1"}` (`Comps1.Designer.vb:1049,1063`).

#### 4. Stage-by-stage storage map (acceptance gate)

| # | Stage | Precision carried | Rounding applied | Stored as |
|---|-------|-------------------|------------------|-----------|
| 1 | Time entry `mss.hh` string → number (e.g. `956.69` = 9:56.69) | binary64 Double | none (entry validation counts decimals only, `GlobalFunctions_MOD.vb:943-957`) | `Time1Mins/Time1Secs/Time2Mins/Time2Secs` DataColumn **Double** (`dsScores.Designer.vb:669-670`) |
| 2 | CSV/import ingest | Double | **`TruncateNumber(…, TimeDecimals)` incl. +1e-6 nudge** on every time field (`Comps1_MOD.vb:532-544`) — a loss point | same Double columns |
| 3 | mmss → seconds, `GetTimeInSeconds` (`Scoring_MOD.vb:626-631`, uses `Fix`) | Double | none | in-memory only |
| 4 | Two-timekeeper average `CalcTimeScore` `(T1+T2)/2` (`GlobalFunctions_MOD.vb:3677-3705`) | Double | none; ÷2 introduces off-grid binary fractions | in-memory |
| 5 | Duration over-target halving & `×varDurPointsPerSecond` (`Scoring_MOD.vb:651-658`) | Double | **none** — the historical nudge+round block is commented out (`Scoring_MOD.vb:663-673`); `GetTimeScore` returns full precision | in-memory |
| 6 | Landing bonus | Double | biased half-up: `LndgScore += 0.05/0.005/0.0005/0.00005` by `varGroupScoreDecimals`, **then** `RoundNumber` (`Scoring_MOD.vb:756-763`; F5B :787-794; laps :828-835) — net bias +half-grid/2 upward, e.g. 25.45 @Decs0 → 26 | `LandingScore` Double col added at runtime (`Scoring_MOD.vb:52`) |
| 7 | **RawScore assembly** (Duration: time+landing−penalty, clamp ≥0) `Scoring_MOD.vb:164-183`; F5K sum `:236`; F3K `CalcRawScoreF3K` `:214` | Double | none | `RawScore` DataColumn **Double** (`dsScores.Designer.vb:653`) |
| 8 | **Persist Scores row** | — | **Double→binary32 conversion here** — update params typed `GetType(Single)` for `RawScore` and `NormalisedScore` (`Scoring_MOD.vb:2767-2774`); insert ditto (`Scoring_MOD.vb:2943-2950`); time cols stay Double params (`:2743-2757, 2923-2937`); driver `daScores.Update` `Scoring_MOD.vb:123` | Jet `Scores.RawScore`, `NormalisedScore` hold float32-rounded values (e.g. 999.999 → 999.9990234375 when widened) |
| 9 | Read-back for scoring/reports | Double **holding widened float32** | none | untyped/typed DataTable columns Double |
| 10 | Group normalisation `Update_GroupScores` (`Scoring_MOD.vb:247-486`): points basis `1000·Raw/MaxRaw` (:303, :423), time basis `1000·TS/MaxTS` (:353), speed inverse `1000·MinRaw/Raw` (:468) | Double | **`RoundNumber`/`TruncateNumber` to `GroupScoreDecimals` here** (`:304-308, 354-358, 399-403, 425-429, 470-474`); time-basis `NormalisedScore = rounded NormTimeScore + LandingScore` — **sum itself not re-rounded** (:360-366) | `NormalisedScore` DataColumn Double in memory (`dsScores.Designer.vb:645`) |
| 11 | Persist `NormalisedScore` again | — | **second Double→float32 loss point**, same param typing as #8 | Jet float32 |
| 12 | Overall-results aggregation: per-round NS summed, drop-scores subtracted (`Rpt_Results_Overall_MOD.vb:2704-2705`; `Rpt_Results_Calculations_MOD.vb:471-538`, `FinalScore -= drop`) | Double arithmetic over float32-widened inputs | none during summation | `Score`/`RawScore` Double cols (`Rpt_Results_Overall_MOD.vb:2586-2587`) |
| 13 | **Final re-round at report time** `dtCompResults_FillPilotRankAndPcnt` (`Rpt_Results_Calculations_MOD.vb:736-740`): `Score` and `RawScore` re-`RoundNumber`ed to `varGroupScoreDecimals`; HighScore :754; each comparison score :770 | Double | half-up to comp grid — this is what ranks compare on (:770-795, sort :748) | Double in report table |
| 14 | Percent & display | Double | `Percent = 100·Score/HighScore` **unrounded** here (:797-803); individual reports round PCent to 2 dp (`Rpt_RoundResults_MOD.vb:2724`; `Rpt_PrelimAndFlyOff_MOD.vb:756,765`; `Rpt_Results_CompSeries_MOD.vb:1341`); cells formatted `ToString("0.00")` etc. (`Rpt_RoundResults_MOD.vb:956-959`) | printed string |

**Every precision-loss point between raw input and displayed rank:** entry validation (none), import truncation (#2), binary averaging/division (#4-#5, #10), landing-bonus biased rounding (#6), **Double→Single at persist, twice** (#8 RawScore+NormalisedScore, #11 NormalisedScore), group round/truncate including the unrounded NS=Landing sum (#10), drop-sum arithmetic on float32-widened values (#12), report-time re-round (#13), display formatting (#14). Related display-only paths: score-check recalcs with family timing decimals (`Scoring_ScoreCheck.vb:685, 715-725`); progressive-online rounding to 1 dp (`ScoringOnLine_MOD.vb:1119-1121`).

**Sample-data evidence (`gs_data.json`, comp 17 "ALES sample comp", `GSCompClass=DurALES`):** `Comps.GroupScoreDecimals = '0'`, `RoundOrTruncate = '0'` (rounded), `GroupScoreOption = '2'` (group score on time); `Dur.durDecimalsForTiming='0'`, target 600 s, 1 timekeeper, 1 pt/s. All 30 `Scores.NormalisedScore` values are clean integers (`0.0, 440.0, 835.0, 1030.0`) — **no float32 artifact is observable in this particular export**, because Decs=0 forces integral scores, which binary32 represents exactly. Float32 evidence is from the parameter typing (#8/#11), not this dataset.

#### 5. Config plumbing

- **Defaults:** `varGroupScoreDecimals = 0`, `varRoundOrTruncate = 0` (`GlobalVariables_MOD.vb:82,86`); new comp defaults `GroupScoreDecimals = 1`, `RoundOrTruncate = 0` (`CompNew.vb:692-693`); copy-comp inherits (`CompNew.vb:650`); DB-migration backfill `RoundOrTruncate = 0` "means rounded" (`UpdateDatabase_MOD.vb:35-48`).
- **Load:** per-comp globals loaded without DBNull guard at `GlobalFunctions_MOD.vb:1479-1481`; guarded reload path (`DBNull → 0`) at `GlobalFunctions_MOD.vb:1564-1580`; `GetNormScoreDecimals()` reads the column straight from `Comps` for two reports (`GlobalFunctions_MOD.vb:3215-3226`, callers `Rpt_Results_ByTask_MOD.vb:173`, `Rpt_Results_TeamResults_MOD.vb:162`).
- **UI/save:** combo items {0,1,2,3} (`Comps1.Designer.vb:2257`); saved `drComps("GroupScoreDecimals") = CInt(cbGroupScoreDecimals.SelectedItem)` (`Comps1Saved.vb:1842`), `RoundOrTruncate` from combo SelectedIndex 0=Rounded/1=Truncated (`Comps1_MOD.vb:138-141`, `Comps1Saved.vb:1841,3116`).
- **Consume:** passed into every `Update_GroupScores` call (11 sites in `Scoring.vb`, e.g. `Scoring.vb:2606`; `Scoring_MOD.vb:117`; `Rpt_Results_Overall_MOD.vb:2479`; `Comps1.vb:6800`) and into report re-rounding (§4 #13).
- **Valid sets:** `GroupScoreDecimals ∈ {0,1,2,3}` (enforced only by combobox); `RoundOrTruncate ∈ {0,1}`; anything else → scores become 0 via §3.

#### 6. Comparator recommendation

**Do not use a tolerance. Replicate the chain and compare fixed-rounding decimal literals after emulating the float32 cast.** Justification from the code path:

- The Double→Single conversion happens at exactly one identifiable point per value: the `OleDbParameter` typed `GetType(Single)` on write (`Scoring_MOD.vb:2767-2774, 2943-2950`). It is deterministic: persisted value ≡ `widen(binary32(RoundOrTrunc_double))`.
- Error magnitude is fully bounded: binary32 has a 24-bit mantissa ⇒ relative error ≤ 2⁻²⁴ ≈ 5.96×10⁻⁸; at the top of the score range (~1030) that is ≤ 6.1×10⁻⁵ absolute, and adjacent representable values near 1030 are spaced ~1.22×10⁻⁴ apart. This is smaller than the smallest scoring grid (0.001 at Decs=3) but **larger than 0** — two genuinely different Doubles can collapse to one Single (verified: distinct doubles 1030.000061 vs 1030.0 → identical binary32).
- A tolerance big enough to absorb float32 noise (>6×10⁻⁵) also masks real calculation bugs of 10⁻⁴ magnitude — precisely the class of bug such a harness exists to catch, and it would also mask the boundary divergences of `RoundNumber` (§1: `0.35 → 0.3`). Exact emulation (binary64 arithmetic in GS's operation order → apply `Int`-floor rule → cast through binary32 → widen → compare, treating both sides' rank-relevant value as the report-time re-round of §4 #13) has zero ambiguity left.

#### Unresolved

- **Jet declared type of `Scores.RawScore`/`NormalisedScore`:** the blank DB is cloned at runtime from a template `GliderScoreData.mdb` that is not in the tree (`GlobalCreateBlankDB_MOD.vb:25-153` maps `System.Double→DOUBLE` but reads the template's actual schema), so the column DDL cannot be confirmed from source. Observably moot — the `Single`-typed parameter fixes the written value either way — but flagged. Note `docs/database_to_upload_mapping.md:37,107` labels RawScore "Double": that documents the online-upload JSON (serialised post-widening), not Jet storage.
- **Sample JSON shows no float32 artifacts** (integral scores, Decs=0); the float32 claim rests on the parameter-typing code path, not on observed data. A comp exported with `GroupScoreDecimals ≥ 1` would be needed to witness e.g. `999.9990234375`.
- `dsScores.xsd:14` declares `NormalisedScore` as `xs:double` — consistent with in-memory Double; no contradiction, just confirming the asymmetry (Double in memory, Single on the wire) lives entirely in the adapter command layer.

### Drop-worst

All citations are `File.vb:LINE` in `/home/pete/source/gliderscore/GliderScore_Master/`. Primary module: `Rpt_Results_Calculations_MOD.vb`. VB notes: the language is case-insensitive (`getRoundsFlownForTask` ≡ `GetRoundsFlownForTask`, two overloads distinguished by arity), module-level variables are shared mutable state across calls, and implicit String→Integer coercion occurs at assignment sites flagged below.

#### 1. Configuration source (Comps table)

| Column | Type | Read by | Written by |
|---|---|---|---|
| `DropScoreOption` | Long | `GetDropScoreOption` `Rpt_Results_Calculations_MOD.vb:424-434` | setup form save `Comps1.vb:2960`; column added with default 0 `UpdateDatabase_MOD.vb:1790,1824` |
| `Drop1AtRound`…`Drop5AtRound` | Byte | `GetDropXAtRound(RndNo,CompNo)` builds `"Select Drop{N}AtRound FROM Comps"` `GlobalFunctions_MOD.vb:4323-4334` | save `Comps1.vb:2936-…`; UI defaults per class below |
| `F3QDrop6to10` | Text(14), CSV `"d6,d7,d8,d9,d10"` | `GetDrop6to10` `GlobalFunctions_MOD.vb:4336-4342` (**ignores its `CompNo` parameter — queries `varCompNo` instead**, `GlobalFunctions_MOD.vb:4339`) | save `Comps1.vb:6576`; column added with default `'99,99,99,99,99'` `UpdateDatabase_MOD.vb:2870-2878` |

Semantics: **`DropNAtRound` = the round-count threshold at which drop N activates** — drop N is taken iff *rounds flown* ≥ `DropNAtRound`; **99 = never** (module defaults, `Rpt_Results_Calculations_MOD.vb:24-33`). UI validation enforces numeric entry and strict monotonicity: Drop1 ∈ [2,…], Drop(N+1) > DropN unless either side is 99 (`Comps1.vb:2113-2149`).

#### 2. `DropScoreOption` decode and gating

- **0 = "Drop worst Task scores"** (drop individual lowest task/flight scores); **1 = "Drop worst Round scores"** (drop lowest round totals). Item order fixes the index: item0=Task, item1=Round (`Comps1_MOD.vb:224-225,242-243`); stored `SelectedIndex` (`Comps1.vb:2960`); header strings confirm `Rpt_Results_PositionReport_MOD.vb:451-452`.
- **UI gating**: option 1 is offered **only** when `varGSCompClass` is `"F3Q"` or `"DurSpd"`; all other classes are forced to `SelectedIndex = 0` and item 1 removed (`Comps1.vb:5836-5848`). Nothing in the calculation engine re-checks this — a hand-edited DB could set 1 anywhere; engine honours whatever is stored (`Rpt_Results_Calculations_MOD.vb:331-339`).

#### 3. Staged activation — how many drops at R rounds flown

**Engine dispatch** `Rpt_Results_Calculations_MOD.vb:412-415`: option 0 → `dtCompResults_UpdateScores_DropTasks` (:437), option 1 → `dtCompResults_UpdateScores_DropRounds` (:546).

**Option 0 (by Task)** — activation denominator is *per-task competition-level rounds flown*: `TNRndsFlown` = COUNT(DISTINCT RoundNo) with `RawScore>0` for that task within [FromRound,ToRound] (`:357` → `GlobalFunctions_MOD.vb:2313-2333`). Each task's own count gates its own drops:

| Condition (per task T) | Drops taken from task T's scores |
|---|---|
| `TNRndsFlown < Drop1AtRound` | 0 (`:493` else-branch writes 0 into Drop field) |
| `≥ Drop1AtRound` | 1 (`:493-497`) |
| `≥ Drop2AtRound` | 2 (`:502-505`) |
| task 5 (F3K) only, `≥ Drop3AtRound` / `≥4` / `≥5` | 3 / 4 / 5 (`:510-534`) |

Tasks 1–4,6 have exactly **two** drop slots (`Drop1Dur…Drop2F5K` field mapping `:484-491`); **task 5 alone has five** (`Drop1F3K…Drop5F3K`) — this, plus the rank tie-break in the Ranking section, is the F3K-specific shape.

**Option 1 (by Round)** — activation denominator is the **nominal window size** `RoundsFlown = ToRound − FromRound + 1` (`:564`), *not* actual scores present:

| Condition | Drops taken (lowest round totals) |
|---|---|
| `< Drop1AtRound` | 0 |
| `≥ Drop1AtRound` | 1 (`:565-571`) |
| `≥ Drop2AtRound` … `≥ Drop10AtRound` | 2 … 10, sequentially (`:573-634`) |

Dropped round numbers accumulate into `DropRnds` as space-separated text (`:570,577,…`).

**`F3QDrop6to10`**: drops 6–10 exist **only for rounds-mode**, and their thresholds are loaded **only when `varGSCompClass="F3Q"`** — CSV split into module vars Drop6AtRound…Drop10AtRound (`Rpt_Results_Calculations_MOD.vb:318-326`; same gate at `Rpt_Results_Overall_MOD.vb:2725-2733`). VB flag: elements are Strings implicitly coerced to Integer (:321-325). For non-F3Q comps these module vars keep value 99 — **or a stale value from a previously processed F3Q comp in the same session** (module-level state, `:29-33`; hazard flagged, not observed).

**Factory defaults by comp class** (set when the class is chosen; user-editable afterwards) — `Comps1.vb`:

| Class | D1 | D2 | D3–D5 | D6–D10 | Citation |
|---|---|---|---|---|---|
| F3B | 6 | 99 | – | – | :6113-6114 |
| F3G | 6 | 99 | – | – | :6144-6145 |
| Spd (F3F) | 4 | 14 | – | – | :6171-6172 |
| F3J | 8 | 99 | – | – | :6197-6198 |
| F3K | 12 | 99 | 99 | – | :6229-6230 (+hidden 3–5 forced "99", :5812-5814) |
| F3Q | 3 | 5 | 9,99,99 | 99×5 | :6256-6265 |
| F5B | 4 | 99 | – | – | :6294-6295 |
| F5K | 5 | 99 | – | – | :6323-6324 |
| F5J | 5 | 99 | – | – | :6380,6384 |
| F5L | 6 | 99 | – | – | :6381-6384 |
| DurGeneral / **DurALES** / F3L / Elec | **99 (no drop)** | 99 | – | – | :6382-6384 |
| Dis, DurDis, DurSpd, DisSpd | 99 | 99 | – | – | :6430-6431, :6460-6461, :6484-6485 |

New-comp default is also 99 everywhere (`CompNew.vb:676-677,709`).

Worked examples (option 0, single-task class, defaults): F3B → 1 drop once 6 scored rounds exist; F3J → at 8; F3F → 1 drop at 4 rounds, 2nd at 14; F3K → 1 drop at 12 scored rounds; DurALES → never.

#### 4. Selection basis (task-driven vs round-driven)

Decision rule (`Rpt_Results_Calculations_MOD.vb:331-339,371-415`):

- `DropScoreOption = 0` → per pilot, build `dtTaskScores` (RoundNo, TaskNo, TaskScore) by walking every task × round `FromRound..ToRound`, skipping rounds beyond the task's last updated round (`TaskLastRound` = MAX(RoundNo) where `Updated='True'`, `:386` → `GlobalFunctions_MOD.vb:2337-2352`); absent cells become **0 candidates** (DBNull→0, `:390`). Operates over **individual normalised task scores** (cell = NormalisedScore placed at column `10*OriginalRoundNo+TaskNo`, `Rpt_Results_Overall_MOD.vb:2698-2706`).
- `DropScoreOption = 1` → per pilot, accumulate `dtRoundScores` = sum of that pilot's normalised cells per round (`:403-408`); operates over **round normalised totals**.
- Callers: Overall Results `Rpt_Results_Overall_MOD.vb:2738`, Detailed Results `Rpt_Results_Detailed_MOD.vb:2699`; Team Results reimplements the same conventions inline (`Rpt_Results_TeamResults_MOD.vb:278-282,572`); Comp Series call is commented out (`Rpt_Results_CompSeries_MOD.vb:1112`).

#### 5. Tie-breaking among equal drop candidates — deterministic

- Tasks mode: `dvDropTasks.Sort = "TaskNo, TaskScore ASC, RoundNo DESC"` (`Rpt_Results_Calculations_MOD.vb:469`), filtered per task (:481). Equal-score candidates are ordered by **higher round number first**, so **the equal score from the LATER round is dropped first**. Keys (TaskNo, TaskScore, RoundNo) are unique per pilot ⇒ fully deterministic.
- Rounds mode: `dvRoundScores.Sort = "RoundScore ASC, RoundNo DESC"` (:560); same rule — **among equally-bad rounds, the latest round number is dropped first**. One row per round ⇒ deterministic.
- Picks are positional: `dvDropTasks(0)/(1)/…` under the RowFilter, `dvRoundScores.Item(0..9)` — i.e., first-in-sort-order wins. No reliance on DataTable insertion order for the drop itself.

#### 6. Marking and exclusion

- **Marking**: the per-round/task result cell is **negated** in place. Tasks mode `MakeTaskDropScoreNegative` (:694-703): if the cell is 0 it is first set to `+0.00001` then negated → `-0.00001`; otherwise `-value`. Rounds mode `MakeRoundDropScoreNegative` (:705-727) negates **every task cell of the dropped round**, writing `-0.00001` directly for zeros (DBNull→0 first). Sentinel `±0.00001` distinguishes a *dropped zero* from a genuine zero; `+0.00001` additionally means "F5J motor-restart zero, not droppable".
- **Exclusion from Score**: the reported `Score` starts as Σ(best NormalisedScore per round/task) − |penalties|, floored at 0 (`Rpt_Results_Overall_MOD.vb:2690-2712`); each drop then does `FinalScore -= <positive dropped value>` (`Rpt_Results_Calculations_MOD.vb:496,504,513,522,530,568,575,…`) and writes back (:538,636). The negative cell values are **display markers only** — the sum is never recomputed from cells, so negation causes no precision loss (Double sign flip is exact). Report rendering: `-0.00001` → `*0`, other negatives → `*<abs>`, `+0.00001` → `Motor 0` (`Rpt_Results_Overall_MOD.vb:2016-2025`). Drop fields (`Drop1Dur`…) store the **positive** dropped value. Minor: FP residue may differ from an order-naïve re-sum; a comparison harness should mirror the subtract-from-total operation order or use ~1e-9 tolerance.
- **F5J interplay** (alters the candidate pool): with `varF5JMotorRestartOption=1`, restart zeros are temporarily 99999 to survive de-duplication, restored to `0.00001` (`Rpt_Results_Overall_MOD.vb:2525-2564`), skipped when building candidates (`Rpt_Results_Calculations_MOD.vb:393-394`) and removed from the pool (:459-466). Option 2 = zero droppable like any other.

#### 7. Re-flights

A re-flight creates a **new Scores row** (new `RoundNo`/`GroupNo`/`ReFlightNo`) carrying the **original `OriginalRoundNo`** copied from the pilot's existing row (`ChooseReFlightPilots.vb:918-971`, specifically `:926` and `:942`). The results rollup selects on `OriginalRoundNo` (SQL computes key `RndTsk = originalroundno*10+taskno`, `Rpt_Results_Overall_MOD.vb:2368-2373`; cells filled via `OriginalRoundNo=RndNbr`, `:2698-2706`) and **de-duplicates multiple flights of the same original round by keeping the highest NormalisedScore** (:2533-2556). That surviving best normalised score is what feeds both the round sums and the drop candidate pools — i.e., drop selection never sees superseded re-flight scores.

#### 8. F3K-gated variant (`f3kRecord`)

After drops and ranking, `If f3kRecord Then` (`Rpt_Results_Calculations_MOD.vb:809`) runs a rank tie-break for F3K only: among pilots sharing a rank, repeatedly test `Score + k highest droppers DESC` for k = 1…5 (:898-1007; dropper list sorted ascending then read backwards so the biggest dropper leads, `:866-878`) — "highest dropper gets highest ranking" (`:806-807`); still-tied pilots share an `"=n"` rank (:1018-1029). This consumes the stored positive `Drop1F3K…Drop5F3K` values. (Commented-out remnant of former F3K gating of the drop-threshold UI: `Comps1Saved.vb:887-891`.)

#### 9. Analysed sample comp (read-only JSON)

`gs_schema.json` confirms column types: `Drop1AtRound`–`Drop5AtRound` = Byte, `DropScoreOption` = Long, `F3QDrop6to10` = Text, `GSCompClass` = Text. The single `Comps` row: `CompNo=17`, **`GSCompClass='DurALES'`**, `DropScoreOption=0`, `Drop1AtRound=Drop2AtRound=Drop3AtRound=Drop4AtRound=Drop5AtRound='99'`, `F3QDrop6to10='99,99,99,99,99'`. Scores: 30 rows over OriginalRounds 1–3. **No drops activated**: all thresholds are 99 ⇒ `DropScores` flag stays False (`Rpt_Results_Overall_MOD.vb:2720-2724`) ⇒ `dtCompResults_ApplyDropScores` is never called (`:2735`). This matches the DurALES factory default of no discard (`Comps1.vb:6382`).

#### Unresolved

- **De-dup tie order for equal re-flight scores**: the comment claims "keep the first one flown" (`Rpt_Results_Overall_MOD.vb:2519-2520`), but the SELECT has no ORDER BY (`:2368-2373`) and the keep-first pass relies on DataView sort `PilotNo, RndTsk, NormalisedScore DESC` leaving equal keys in underlying row order (:2534-2550) — provider-dependent (Jet/PK order in practice), not guaranteed by code.
- **Stale `Drop6AtRound…Drop10AtRound` module state** across competitions in one session (only F3Q reloads them, `Rpt_Results_Calculations_MOD.vb:318-326`); a following non-F3Q comp's rounds-mode run would reuse the previous comp's values. Latent; unobserved in sample data.
- **`GetDrop6to10` ignores its `CompNo` argument** and reads `varCompNo` (`GlobalFunctions_MOD.vb:4339`) — divergent behaviour possible when reporting a non-current comp (e.g. Comp Series); not exercised here.
- Dead variable `MaxRoundsFlown` (`Rpt_Results_Calculations_MOD.vb:329,338`) — computed, never read.
- Tasks-mode asymmetry (recorded, deliberate-looking): activation counts only rounds with `RawScore>0` (`GlobalFunctions_MOD.vb:2320`), while candidate pools include unscored rounds as zero candidates (`Rpt_Results_Calculations_MOD.vb:388-390`) — a pilot who missed a scored round can have that miss dropped as a 0 even though it never counted toward activation.

### Ranking & tie-breaks

Source: `Rpt_Results_Calculations_MOD.vb::dtCompResults_FillPilotRankAndPcnt` (Rpt_Results_Calculations_MOD.vb:730-1048) over the report table built by `dtCompResults_Create` (Rpt_Results_Overall_MOD.vb:2317-2754).

#### Sort-key spec (primary ladder)

| Key | Direction | Meaning |
|---|---|---|
| `Score` | DESC | **Post-drop** total: Σ best-per-round/task `NormalisedScore` − \|penalties\| − Σ dropped scores. Initially identical to `RawScore` (Rpt_Results_Overall_MOD.vb:2709-2710), then reduced by `dtCompResults_ApplyDropScores`, which writes **only** `Score` (Rpt_Results_Calculations_MOD.vb:538, 636). Floored at 0 pre-drop (Rpt_Results_Both — Rpt_Results_Overall_MOD.vb:2712). |
| `RawScore` | DESC | **Pre-drop** total: Σ `NormalisedScore` − \|penalties\|. Never touched by drop logic. ⚠ naming trap: this is the pre-drop *normalised* total, unrelated to the per-flight `Scores.RawScore` DB column. |

Both keys are rounded to the comp's `varGroupScoreDecimals` **before** any ranking comparison (Rpt_Results_Calculations_MOD.vb:736-740); `RoundNumber` is round-half-up via `Int(Nbr + 0.5·10^-d)` (GlobalFunctions_MOD.vb:3116-3134) — i.e. arithmetic, not banker's. Primary sort verified: `dv.Sort = "Score DESC, RawScore DESC"` (Rpt_Results_Calculations_MOD.vb:748).

Display sorts (after ranking): `"HiddenRanking, RawScore DESC, Score DESC"` for F3K (Rpt_Results_Overall_MOD.vb:277) and F5K (:293); `"HiddenRanking ASC, RawScore DESC, Score DESC"` otherwise (:304). The returned table is ordered by `HiddenRanking` alone (Rpt_Results_Overall_MOD.vb:2750).

#### `HiddenRanking` vs displayed `Rank`

`Rank` is a **String** (Rpt_Results_Overall_MOD.vb:2589): plain `"n"` or duplicate-marking `"=n"` (standard competition numbering: first row of an equal group keeps the group's position; every successor — and retroactively the leader — becomes `"=n"`, Rpt_Results_Calculations_MOD.vb:778-794). `HiddenRanking` (Int32, :743) is the machine ordering column: each pilot always holds a **distinct sequential position** (tied pilots get their loop index +1, :789), so downstream `Sort = "HiddenRanking"` yields a deterministic total order while the printed column shows ties honestly. Exception: after F3K resolution the resolved pilots' `HiddenRanking` is overwritten with the resolved rank number itself (:892, :915, :937…), and fully-tied survivors share one `HiddenRanking` value (:1023).

#### THE LADDER — ordered comparisons

Applies identically in the Overall, Detailed/ByTask, Position, Scoring-screen and Team-report pipelines (all funnel through `dtCompResults_FillPilotRankAndPcnt`; calls at Rpt_Results_Overall_MOD.vb:2743, Rpt_Results_Detailed_MOD.vb:2704).

1. **`Score` DESC** (rounded to `GroupScoreDecimals`) — all classes. Equal ⇒ next.
2. **`RawScore` DESC** (rounded likewise) — all classes. Equal ⇒ next.
3. **F3K only** (`If f3kRecord`, Rpt_Results_Calculations_MOD.vb:809), among rows sharing a `"=n"` rank: the **dropped-score rescue chain** — highest dropper wins. Per pilot the five drop values (`Drop1F3K…Drop5F3K`, populated only when `DropScoreOption = 0`; forced for every class except F3Q/"DurSpd" per Comps1.vb:5836-5848) are sorted descending (:874-878) and compared cumulatively, stopping at the first unique value:
   - 3a. `Score + Drop₁(highest)` DESC — :901, :907, uniqueness test :910
   - 3b. `+ Drop₂` DESC (filtered to those still tied after 3a) — :928, :931, test :932
   - 3c. `+ Drop₃` DESC — :950, :953, test :954
   - 3d. `+ Drop₄` DESC — :972, :975, test :977
   - 3e. `+ Drop₅` DESC — :995, :998, test :999
   Each rung peels off exactly one winner (gets plain `txtRank`, `txtRank += 1`, :914-918 etc.); a lone survivor takes the current `txtRank` directly (:888-896).
4. **Ladder provably ends**: if `Score + all five drops` are still equal, all remaining pilots keep `"=" & txtRank` and `txtRank` advances by the group size (:1014-1029). Classes other than F3K/F5K have **no rung 3** — they end at rung 2 and simply share rank. F5K has drop columns but is explicitly *excluded* from the rescue chain (condition is `f3kRecord`, not `f3kRecord OrElse f5kRecord`, :809).

#### Percent column

`Percent = 100 × Score / HighScore`, where `HighScore` is the top rounded `Score`; 0 if the high score is 0 or `Score` is DBNull (Rpt_Results_Calculations_MOD.vb:797-803). Stored **unrounded** (Double division); rounding happens only at consumption: display/CSV format `"0.00"` (Rpt_Results_Overall_MOD.vb:1078, :2001), progressive scoring screen rounds to 1 dp via `RoundNumber(dr("Percent"), 1)` (ScoringOnLine_MOD.vb:1121). **Never consumed as a ranking or tie-break input anywhere** — purely presentational. (Comp-Series and Team reports compute separate percent columns from their own totals.)

#### Fly-off / preliminary-final override (note)

F3J/F5J only. Gate: fly-off report flagged on the selection form (Rpt_Results_Overall_MOD.vb:193-196); if any `Rank` contains `"="` with `Score > 0` (:223-233), `Resolve_FlyOff_Rank_If_Same_Scores` runs (Reports_MOD.vb:52-155): rebuilds the preliminary comp's results independently (:57-59), attaches `PrelimCompScore`/`PrelimCompRawScore` (:61-76), then for each tied group orders by `PrelimCompScore DESC` and re-ranks sequentially (:107-114) — all-equal prelim scores leave the tie standing (:104-105); partially-different prelim scores re-rank with fresh `"=n"` marks among still-equal pairs (:116-143). ⚠ The header comment promises a prelim **raw-score** final rung (:56) but it is commented out (:89) — `PrelimCompRawScore` is computed yet never used. Fly-offs do **not** merge into the main ladder: the fly-off report is a separate ladder over the fly-off round window only. The combined Prelim+FlyOff report ranks fly-off pilots by `FOScore DESC`, then appends non-fly-off pilots ranked by prelim `Score` starting at `NbrInFlyOff + 1` (Rpt_PrelimAndFlyOff_MOD.vb:769-839). Merged/preliminary-comp semantics remain a known concept gap (not chased here).

#### Team / Comp-Series / By-Task (note)

These change **scope, not the pilot ladder**: Team report sums each team's top-`NbrForTeamScore` member `Score`s into `TeamScore` and ranks teams `TeamScore DESC`, re-ranking F3J/F5J team ties by sum-of-individual-ranks ASC → best individual placing ASC → team raw score DESC (sort key Reports_MOD.vb:203; displayed-rank equality deliberately stops before raw score, :219-230; member selection Rpt_Results_TeamResults_MOD.vb:348-395). Comp-Series normalises each comp to 1000×score/max (rounded), counts the best *N* comps preferring the earlier comp on inclusion ties, then ranks `Total DESC` with **no further tie-break**, and builds per-comp results with drop-scoring disabled (Rpt_Results_CompSeries_MOD.vb:1096, 1147-1148, 1203, 1300-1333). By-Task reuses the same create/rank pipeline restricted to one task (Rpt_Results_Overall_MOD.vb:106-198 → :2704 equivalent), so the ladder is unchanged.

#### Sanity check vs sample comp (ALES, `/tmp/opencode/gs_data.json`)

Comp 17 "ALES sample comp": 3 rounds, no drops (`Drop1AtRound = 99`), `GroupScoreDecimals = 0`, teams off, class DurALES ⇒ ladder ends at rung 2. Reconstructing per-pilot totals from `Scores.NormalisedScore` gives 1030 > 835 > 440 > seven pilots at 0 — matching the documented algorithm exactly: ranks 1/2/3, then all seven zero-scorers display `"=4"` with distinct `HiddenRanking` 4–10, and Percent = 100 / 81.07 / 42.72 / 0. Note the Access DB persists **no** rank/percent — these are computed at report time only, so verification was against reconstructed totals, which agree.

#### Unresolved

- **DataView sort stability**: .NET does not guarantee `DataView.Sort` stability, so the order *within* a fully-tied group (which determines which pilot gets which `HiddenRanking` slot, :789) is implementation-defined. Bounded impact: such pilots share an identical displayed `"=n"`.
- **Locale hazard in the F3K chain**: rung filters build `"ScorePlusNDrop='" & HighScore & "'"` strings (:925, :947, :969, :992, :1014); under a decimal-comma locale the filter can match nothing. The author's commented-out `.Replace(",", ".")` (:924 etc.) shows this was encountered. Any reimplementation must compare numerically, not via formatted strings.
- **Silent exception swallow**: the ranking loop wraps score conversion in `Try/Catch` with a no-op handler (`a2b = a2b`, :768-776), leaving `ThisScore` stale on error — a latent mis-grouping risk, not observable in normal data.

### Reconciliation result

**Method & provenance.** The authoritative export `/home/pete/Downloads/GliderScoreDownload.txt` is a binary Microsoft Access database; `mdb-tools` is not installed on this machine, so reconciliation consumed the pre-generated column-oriented JSON extract of that same DB (`/tmp/opencode/gs_schema.json` + `/tmp/opencode/gs_data.json`; all values strings; `Scores` = 30 rows). Comp config was re-verified from the extract before use: CompNo 17 "ALES sample comp", `GSCompClass='DurALES'`, `GroupScoreOption='2'` (Time), `GroupScoreDecimals='0'`, `RoundOrTruncate='0'`, `durFlightPenalty='0'` (⇒ varFltDednIdx=0), `durNumberOfTimekeepers='1'`, `DurPointsPerSecond='1.0'`, `durTargetTime='600'` with empty `DurTargetTimeByRound`, landing scheme LndgNo 6 = {1→50 … 10→5}. The script implements the documented formulas literally — packed-mmss decode via Fix-truncation (`Scoring_MOD.vb:626-631`), Duration TimeScore with symmetric over-target decay ×pts/s (`Scoring_MOD.vb:645-673`), exact-match landing lookup + 0.05 nudge + `Int(x+0.5)` half-up round (`Scoring_MOD.vb:726-764`, `GlobalFunctions_MOD.vb:3116-3134`), RawScore floor at 0 (`Scoring_MOD.vb:164-183`), normalisation option-2/idx-0 two-pass per `(TaskNo,RoundNo,GroupNo,ReFlightNo)` group with recomputed MaxTimeScore, fresh landing lookup, sum not re-rounded, floored <0→0, zero-max guard writing 0 (`Scoring_MOD.vb:335-376`) — then casts every computed Double through binary32 before comparing (GS persists `RawScore`/`NormalisedScore` via `Single`-typed parameters, `Scoring_MOD.vb:2767-2774`/:2943-2950). numpy unavailable ⇒ float32 emulation via `struct.pack('f', …)`.

**Script:** `/tmp/opencode/wi6_reconcile.py` (throwaway; not committed).

**Outcome:** **30 of 30 rows reconcile exactly** (RawScore ✔ 30/30, NormalisedScore ✔ 30/30; mismatches: **0**). Non-matching rows: none. The three flown flights reproduce end-to-end: pilot 13 (packed 200.0 = 2 min → TS 120 + landing 40 = Raw 160; NS 400+40=440), pilot 48 (400.0 → 240+35 = 275; NS 800+35=835), pilot 70 (500.0 → 300+30 = 330; NS 1000+30=1030). Rounds 2–3 are all-zero with `Updated='False'` and persisted zeros — consistent under both the Updated-gated raw path (`Scoring_MOD.vb:66-72`) and the zero-max normalisation guard; no ambiguity arises from this dataset. Note the binary32 fidelity step is a no-op witness here (Decs=0 forces integral scores, exactly representable), as already flagged under *Precision & storage* §4.

**Ranking ladder:** reconstructed totals (no drops, `Drop1AtRound=99`; report-time re-round at `Rpt_Results_Calculations_MOD.vb:736-748`, sort `Score DESC, RawScore DESC` :748) give ranks **1 / 2 / 3 distinct (pilots 70=1030, 48=835, 13=440) then seven pilots tied displaying "=4"**, Percent = 100·Score/HighScore unrounded (:797-803) = **100 / 81.07 / 42.72 / 0** (2 dp) — verified ✔.

This gate discharges gliderscore-db-analysis.md §4's caveat *"do not trust import arithmetic until resolved"*: every persisted score of the analysed comp is now reproducible from the documented formulas, and the §4 "winner=1030 anomaly" and "200 s / height-penalty" readings stand corrected by the *Raw score* section's worked example (landing added after time normalisation; packed mmss, no height penalty configured).

**FORMULA CORRECTIONS NEEDED:** none — the documented formulas reproduce all 30 persisted values exactly as written; no correction required.

Dataset limits for downstream consumers (recorded elsewhere in findings too): the float32 persist-cast is asserted from the parameter-typing code path rather than witnessed in this export, and the `Duration × option 2 × idx∈{1,2,4}` stale-write gap plus out-of-range `RoundOrTruncate` stale paths are unexercised by this data.

### Divergences from FAI/NZ rules

Sample class DurALES = ALES duration + landing points; nearest rulebook shapes are NZMAA Class M and FAI F3J/F5J. Refs verified in condensed docs; `F3B.2.3`, `5.5.11.12`, `NZ.3.12.3`, `F3K.10` checked verbatim via the fai-rules skill script. GS behaviour cited to source.

**D1 — Winner >1000: landing added after time normalisation.**
GS: opt-2/idx-0 normalises TimeScore alone, adds landing after, sum unre-rounded (`Scoring_MOD.vb:353-366`, floor only :367); authorial intent stated at `Comps1.vb:5250-5255`. The idx-3 variant nets height penalty into the basis but still adds landing after (:381-414, sum :407).
Rules: FAI normalises the **total** raw — winner's flight+landing(−height deduction) maps to 1000 (`F3J.10.10–10.11`; `F3B.2.6` Partial A basis includes landing; `5.5.11.12 l-m`). No FAI class permits >1000.
NZ: Class M mandates GS's exact order — "normalised flight score and the landing score" (`NZ.3.12.3 c,d`; restated `NZ.3.12.1 e`).
Impact/oracle: DurALES-shaped fixtures are rule-correct per NZ; replaying FAI F3J/F5J-style comps through GS embeds the divergence. Intentional-divergence candidate: **YES vs FAI classes**; **NO for NZ Class M** (complies).

**D3 — Over-target arithmetic.**
GS symmetric decay `TS -= 2·(TS−Target)` (`Scoring_MOD.vb:651-657`) is arithmetically identical at `DurPointsPerSecond=1` to the official cap-then-deduct model (`T+d` scores `T−d`): matches `F3B.2.3 c` ("one point deducted for each full second flown in excess of 600 s") and `NZ.3.12.3 b` (+1/s to target, −1/s beyond; NZ's own 700-for-600 example reproduces). Genuine divergences beside it:
(a) decay applied **before** ×DurPointsPerSecond (:658) ⇒ equivalence holds only at PPS=1;
(b) idx-3 arm caps AT target with **no deduction** for height-penalty classes ∉ {DurALES, F3G} (:652-653) — matches no rulebook;
(c) FAI F3J/F5J have no decay at all: F5J accrues capped 600/900 with >1-min-overfly zeroing (`5.5.11.12 c,g`), F3J overfly is −30 up to 1 min / zero beyond (`F3J.10.3–10.4`) — GS's universal decay mis-scores such comps.
Impact/oracle: none for the DurALES sample; intentional-divergence candidate **YES** for (b), and for (a)/(c) on FAI-class fixtures.

**D5 — F3K working window and task sums.**
GS: `getNbrOfFlights` counts all seven slots including landing and deduction (`Scoring_MOD.vb:611-621`), reducing the window 1 s per flight (:1587, :1610, :1630 …); sum-tasks (O/U10/U15) add all seven slots as points.
Rules: `F3K.11` fixes working times per task (7/10/15 min) — no per-flight reduction exists in the corpus — and each task counts specified flights ("last flight", "best 3 of ≤6"…); a landing distance is not a flight. Minor precision rider: official F3K timing truncated to 0.1 s (`f3k.md §2`); GS carries hundredths into sums.
Impact/oracle: any F3K fixture diverges on both window length and task composition. Intentional-divergence candidate: **YES**.

**D6 — Drop schedules vs official thresholds.**
Compliant (therefore not entries below): F3B D1=6 ≡ ">5 complete rounds, lowest partial of each task" (`F3B.2.8`; GS drops per-task cells, `Rpt_Results_Calculations_MOD.vb:484-491`); F3J D1=8 ≡ ">7 qualification rounds" (`F3J.3.1 a`; single-task ⇒ cell ≡ round); F5J D1=5 ≡ ">4 rounds" (`5.5.11.13`); F5L D1=6 ≡ ">5"; DurALES default no-drop ≡ NZ M/N/P (no discard stated). Genuine divergences:
- **F3K**: factory first drop at 12 scored rounds (`Comps1.vb:6229-6230`) vs official "if six (6) or more rounds are flown then the lowest score is dropped" (`F3K.10.1`). Candidate **YES**.
- **F5K**: factory first drop at 5 rounds (`Comps1.vb:6323-6324`) vs official "if 7 or more rounds… the lowest is dropped" (`f5k.md §5`, from `5.5.10`). Candidate **YES**.
Engine-shape notes: staged multi-drop schedule (D2…D5, `F3QDrop6to10`) exceeds every rulebook — each named class discards at most one score; GS's F3K tie-break chain replaces `F3K.10.2`'s "best dropped score → fly-off" with five deterministic cumulative rungs and no fly-off (Ranking & tie-breaks, rung 3).

**Checked, compliant — omitted as divergences:** zero-floor candidate: GS floors the Duration raw sum at 0 pre-normalisation (`Scoring_MOD.vb:182`), exactly F5J's "where the score is negative… a zero score will be recorded" (`5.5.11.12 f`); NZ silent ⇒ CD territory. Height-penalty candidate: `CalcHeightPenalty`'s two-rate linear model (`GlobalFunctions_MOD.vb:4910-4927`; rates from Dur table, `Scoring_MOD.vb:863-882`) **is** the current rule — "half (0,5) a point up to 200m and three (3) points above it" (`5.5.11.12 e`); the bracketed fixed-band ladder premise reflects pre-2022 F5J text. Speed inversion complies with `F3B.2.6` Partial C.

**Unresolved inline:** GS averages two timekeepers (`GlobalFunctions_MOD.vb:3694-3700`); neither FAI nor NZ condensed docs state any two-timekeeper reconciliation rule (only re-flight when no official time results, `F3J.3 e` via `f3j.md §6`) — nothing to judge against.

### Handoff notes

For `gliderscore-golden-fixture-pipeline.md` and `gliderscore-replay-and-compare-harness.md`. Version attribution DBVersion 6.78 throughout.

**1. Config fields a fixture MUST carry**

| Field | Source | Notes |
|---|---|---|
| `Comps.GSCompClass` | Comps row | selects branches (e.g. DurALES exempt from idx-3 cap, `Scoring_MOD.vb:652`) |
| `Comps.GroupScoreOption` | read `GlobalFunctions_MOD.vb:1479` | 0 raw / 1 points / 2 time |
| `Comps.GroupScoreDecimals`, `Comps.RoundOrTruncate` | save `Comps1Saved.vb:1841-1842` | rounding grid everywhere |
| `Dur.DurFlightPenalty` (= varFltDednIdx) | load `GlobalFunctions_MOD.vb:1316` | deduction mode 0–4 |
| family timing decimals (varDurTimeDecimals…varF5KTimeDecimals; e.g. `Dur.durDecimalsForTiming`) | family rows | **display-only, never affect stored scores** (`Scoring_MOD.vb:663-673`; display `Scoring.vb:4332-4349`) |
| `Dur.DurPointsPerSecond` | family row | curve scale (:658) |
| `Dur.durNumberOfTimekeepers` | family row | 1 decode vs 2-TK average w/ fallback |
| `Dur.durTargetTime` + `DurTargetTimeByRound.TargetTime` | `GetDurTargetTime` `Scoring_MOD.vb:884-908` | per-round overrides win |
| `Dur.durLndg` + `LndgPoints(LndgNo,Distance,Points)` | load `GlobalFunctions_MOD.vb:1319-1330` | exact-match table |
| `durRefHeight`, `durPenaltyUpToRefHeight`, `durPenaltyOverRefHeight` | `Scoring_MOD.vb:863-882` | needed when idx=3 |
| `Comps.Drop1AtRound…Drop5AtRound` (Byte), `Comps.DropScoreOption` (Long), `Comps.F3QDrop6to10` (CSV Text) | gs_schema.json types | drop activation/mode |
| class params as applicable: F5B target/laps-rate/motor-run/watt-min quartet; `F3KTaskByRound.Task` per round; F5K `F5KBonusNames`/`F5KBonusData` + min-time-for-height-bonus | Raw-score findings | family formulas |

Also carry the raw `Scores` half verbatim including `Updated` and `OriginalRoundNo` — `Updated='True'` gates bulk rescoring (`Scoring_MOD.vb:66-72`); OriginalRoundNo drives the report rollup. Flag: some family-row column spellings were traced via variable names — confirm against `gs_schema.json` at extraction.

**2. Comparator strategy — NO tolerance.** Replicate operation order in binary64: mmss decode (`Fix`) → optional 2-TK average → over-target branch → ×PPS → landing lookup (+half-grid nudge then `Int`) → assembly/floor → `RoundNumber`/`TruncateNumber` semantics: Int-floor half-up **including its fp boundary quirks** (`RoundNumber(0.35,1)=0.3`; ~8854 boundaries diverge across d∈{1,2,3}) and TruncateNumber's +1e-6 nudge flipping values within 1 µ of a truncation boundary. Emulate the binary32 persist casts at the two known points (update `Scoring_MOD.vb:2767-2774`, insert :2943-2950), widen back. Compare post-report-re-round decimal literals (`Rpt_Results_Calculations_MOD.vb:736-740`) plus rank order; mirror the subtract-drop order when recomputing `Score` (Drop-worst §6). Guard: `Decs ∉ {0..3}` returns literal 0.0 (Precision & storage §3) — replicate or assert config range.

**3. Float32-oracle caveat & packed-mmss encoding.** The float32 claim rests on parameter typing, not observed data — the sample export's integral scores represent exactly in binary32; only a Decs≥1 export would witness artifacts (Precision & storage, Unresolved). Fixtures must carry packed mmss.s values (`500.0` = 300 s; `Scoring_MOD.vb:626-631`), never seconds; import-path times were additionally truncated via `TruncateNumber(…, TimeDecimals)` incl. nudge (`Comps1_MOD.vb:532-544`). Landing distances must be on-table whole metres — an exact-match miss silently scores 0 (`Scoring_MOD.vb:747-748`; officially metre-grain tables, `NZ.2.4.5`).

**4. Other must-knows.** UI-only (do not model): timing decimals; F5B/F3K GroupScoreOption 2→1 coercion (`Comps1.vb:1964-1972`); negation markers (sum never recomputed from cells, Drop-worst §6); Percent never feeds ranking. Stale-value hazards to reproduce or avoid: rescore skips rows without `Updated='True'`; Duration×opt2×idx∈{1,2,4} leaves NS stale; out-of-range `RoundOrTruncate`/`Decs` leave NS stale or zero it; interactive edits can normalise against the default rather than per-round target (`Scoring.vb:2604-2610`); `GetDrop6to10` ignores CompNo with cross-comp module-state bleed (Drop-worst, Unresolved); re-flight dedup keep-first is provider-dependent; DataView tie order implementation-defined; F3K chain builds locale-sensitive string filters — compare numerically. Supersedes the two corrected claims in `gliderscore-db-analysis.md` §4 — `Time1Mins` units (packed mmss, not seconds) and the pilot-13 reading (160 = 120 time pts + 40 landing; no height penalty applied; flight was 2 min 0 s). **This story supersedes them there; the analysis doc itself stays untouched** (snapshot, per story Before-starting).

**5. Candidate property-test invariants** (candidates only; land via later scoring stories): normalisation scaling — options 1/2 map each group max to exactly 1000 (+ max landing in opt-2-time); speed inversion monotonicity — NS ∈ (0,1000] strictly decreasing in raw time; option-0 preservation — NS equals rounded/truncated Raw exactly; plus drop monotonicity (raising a threshold never increases a pilot's Score) named in the stub.
