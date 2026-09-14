# NDC Scoresheet v3 cross-reference — Soarscore vs the scoring spreadsheet

Source workbook: `Soaring_NDC_Scoresheet_v3.xlsm` (sheets: ALES 123, ALES 123
Alternate, ALES 200, ALES 200 Alternate, ALES Radian, X5J, F5J, F3K).
Compared against the seed class definitions in `tools/Soarscore.SeedData/` and
the rule texts in `docs/rules/nz/source-docs/nzmaa-s5-soaring-2024.md` (and FAI
volume F5 2026 ed.2 for F5J). Method: static extraction of the sheet formulas,
tables and headers from the workbook XML; no test harness was built.

## Headline finding

**The spreadsheet never normalises — anywhere in the workbook.** For Classes N,
P, X5J, F3K NDC and F5J NDC the rules say raw sums, so the sheet's raw pipeline
is *correct* for those. But the sheet titled "ALES 200" (Class M, group-scored
form, NZ.3.12.3 c) computes raw `600-ABS(600-t)+landing` summed over 4 rounds
with no ×1000 group normalisation and no group column at all. That is the
**NDC pipeline** (NZ.3.12.7), not the ALES 200 contest form. Soarscore models
both: `SeedNzMAles200` (normalises, then adds landing) and `SeedNzMNdc` (raw
sum) — the spreadsheet corresponds to the NDC definition only. A full
group-normalised ALES 200 contest maxes at ~4×(1000+50)=4200; the sheet's
ceiling is 2600.

## Per-class findings

### ALES 123 (Class N) — sheets 1 and 2 vs `SeedNzNAles123`

- Flight arithmetic matches: `360-ABS(360-t)` ≡ `UpTo(360,1).Rest(-1)`
  (NZ.3.13.1 c); 3 rounds; raw sum; no drop; negatives unclamped in both.
- **WRONG LANDING TABLE.** The sheet uses Class M's graded metre table
  (50/45/40/35/30/25/20/15/10/5/0). The rule, NZ.3.13.1 e, is a two-step
  circle bonus, which is what Soarscore encodes
  (`Rows.UpTo(7,50).Then(15,25).Rest(0)`, `SeedNzNAles123.cs:68`):

  | Landing | Sheet | Rule / Soarscore |
  |---|---|---|
  | 4 m | 35 | 50 |
  | 9 m | 10 | 25 |
  | 12 m | 0 | 25 |
  | 16 m | 0 | 0 ✓ |

- Missing rule enforcement (no mechanism in the sheet; scorer must
  hand-adjust, and a zeroed time entry still awards landing points):
  - motor restart → watch stops + landing lost (NZ.3.13.1 g);
  - still airborne at round end → time stops + landing lost (NZ.3.13.1 j);
  - landed >75 m → whole flight cancelled (NZ.2.4.6, via `FlightValidWhen`).
- No re-flights permitted (NZ.3.13.1 h) — sheet N/A; Soarscore `NotPermitted`
  (`SeedNzNAles123.cs:89`). Agree.

### ALES Radian (Class P) — sheet 5 vs `SeedNzPRadian`

- Flight arithmetic matches: 420 target, −1/s overtime, 3 rounds
  (NZ.3.15.1 c).
- **Landing table: correct on the tape scale, but keyed on tape readings.**
  Rule (NZ.3.15.1 e) and Soarscore use the same 50/25/0 shape as Class N
  (`SeedNzPRadian.cs:66-68`). The sheet's table is an *exact* 50/25/0 encoding
  of NZ.3.15.1 e when its keys are read as **NZ.2.4.4 FAI-tape readings**
  (key 70 ↔ the 6–7 m band, 65 ↔ 7–8 m, … 30 ↔ 14–15 m, 0 ↔ over 15 m — the
  sheet's own `<8m` label on key 65 confirms this). It is not broken; the
  residual risk is a **unit-convention** one: anyone entering the distance in
  metres gets `#N/A` (exact-match lookup, no keys 8–15). Verified against the
  source text at `nzmaa-s5-soaring-2024.md:497-513`.
- Same missing enforcement trio as Class N: restart (NZ.3.15.1 g),
  airborne-at-round-end (NZ.3.15.1 j), >75 m cancellation (NZ.2.4.6).

### X5J (Class O) — sheet 6 vs `SeedX5j`

- Mostly agreement: 4 flights raw-summed, 650/flight, 2600 max; landing table
  = the Class M metre table ✓ (NZ.2.4.5; `SeedX5j.cs:81-84`).
- **Sheet deducts −1/s past 600** (`600-ABS(600-t)`). The rule has no overtime
  deduction — NZ.3.14.2 (second d) *stops the watch* at working-time end,
  which is why `SeedX5j.cs:63-70` encodes a bare `Rate(glideTime, 1)` with no
  rest band. (Soarscore's uncapped Rate would score a >600 glide linearly —
  also divergent, but procedurally unreachable since the watch stops; the
  sheet would score 1200−t.)
- **Sheet misses the motor-restart deduction** (−1/s subsequent run times,
  NZ.3.14.2 e) and the restart / still-airborne landing forfeits — Soarscore
  has `Rate(motorRestartRunTime,-1)` + `When` guard (`SeedX5j.cs:72-84`). A
  restarted flight gains points in the sheet.
- Same zeroed-flight-still-earns-landing problem; >75 m cancellation
  unenforced.

### F5J (NDC) — sheet 7 vs `SeedF5jNdc`

- **Start-height deduction: identical.** Sheet
  `IF(h>200,100+3*(h-200),0.5*h)` ≡ `Bands.From(0).UpTo(200,-0.5m).Rest(-3)`
  (`SeedF5jNdc.cs:68-71`); continuous at 200 m in both.
- **Overtime diverges — the sheet is wrong.** FAI 5.5.11.12 c awards points
  only *within* working time (max 600); Soarscore = `Rate(flightTime,1,cap:600)`
  with the landing forfeited on overfly (`When(overflySeconds==0)`,
  `SeedF5jNdc.cs:117,124`) and zero if over >60 s (5.5.11.12 g). The sheet's
  `600-ABS(600-t)` invents a −1/s overtime deduction (copied from the ALES
  classes where NZ rules *do* state one): a 610 s flight scores 590+landing in
  the sheet vs 600, no landing, in Soarscore/rules. NZ.0.3 g only carries the
  FAI overtime rules — there is no −1/s clause for F5J.
- Sheet has no >60 s-overfly zero, no 75 m zero (NZ.0.3 h), no "no start-height
  data" cancellation (5.5.11.7 e), no FAI penalty schedule (100/300/1000
  deductions, corridor infractions).
- Cosmetic: sheet tab F5J has A1 = "ALES 200" (copy-paste remnant; X5J sheet
  too).

### F3K (NDC) — sheet 8 vs `SeedNzF3kNdc`

Best agreement of the set. Task arithmetic matches NZ.0.2.1 exactly:

| Task | Sheet | Soarscore / NZ.0.2.1 |
|---|---|---|
| B | 2 flights × MIN(240) | 2 last flights, 240 s cap, 10 min window ✓ |
| D | 2 × MIN(300) | ✓ (`SeedNzF3kNdc.cs:74`) |
| G | 5 × MIN(120) | ✓ (`SeedNzF3kNdc.cs:89`) |
| H | MIN(60)+MIN(120)+MIN(180)+MIN(240) | targets 60/120/180/240 ✓ (`TargetValues`, `SeedNzF3kNdc.cs:107`) |
| Total | B+D+G+H raw | raw sum ✓, max 2280 ✓ both |

- Process difference 1: the sheet's H fixes a target per column while
  F3K.11.8 assigns the four *longest* flights to targets by rank — same
  numbers only if the scorer hand-ranks correctly (Soarscore's
  `BestNFlights{RankByMetric}` automates it).
- Process difference 2: B is hardwired to the 10-min/240 s variant where
  Soarscore carries the NZ.0.2.1 b parameter (large field → 420/180).
- Missing enforcement: F3K.9.3 late-landing flight zero, F3K.7 early-launch
  zero, the F3K.4.3 penalty schedule and unsigned-card zero — the sheet cannot
  zero a flight at all.

## Cross-cutting findings

1. **No normalisation anywhere in the workbook.** Correct for N, P, X5J, F3K
   NDC, F5J NDC (their rules say raw sum). Wrong for a group-scored Class M
   ALES 200 (headline finding).
2. **Exact-match VLOOKUP fragility** — not limited to the Alternate sheets:
   sheets 2, 4, 5 (Radian), 6 (X5J) and 7 (F5J) all use exact-match
   `VLOOKUP(...,FALSE)`, so any off-table entry → `#N/A` poisons the pilot's
   whole total. Soarscore validates readings against the class's declared
   instrument instead (`FlightInterpreter.cs:238-241`).
3. The `U1`/`Q1`/`Y1` "mode 2" tape switch VLOOKUPs the distance in the
   **score** column (`$W$4:$W$27` / `$S$4:$S$27` / `$AA$4:$AA$27`).
   Mechanically that is a **pass-through/validator for directly-typed landing
   scores**, not a distance→score lookup — an entry equal to a valid score
   (e.g. 5 or 10) returns itself. Note the shipped defaults differ: sheets 4
   (ALES 200 Alt) and 7 (F5J) open in mode 2 (score-entry); sheets 2, 5, 6
   open in mode 1 (tape readings). Worth confirming the intent with the NDC
   owners.
4. **Zeroed/cancelled flights still earn landing points in the sheet** in
   every class with a landing term (F3K has no landing term, so the claim is
   vacuous there); in F5J a zeroed entry can go negative (0 flight + landing −
   height deduction). Soarscore zeroes the whole flight/round by rule.
5. **Possible Soarscore gap (both sides wrong):** FAI 5.5.11.12 f — "where the
   score is negative, a zero will be recorded" — is implemented by neither the
   sheet nor `SeedF5jNdc` (a 200 m launch with a short flight yields −100
   through the height piecewise; `FlightInterpreter.Interpret` applies no
   floor to the summed raw score, and the clamp in
   `NormalisationEngine.cs:191-194` only runs on the normalised path, which
   F5J NDC never enters). Candidate backlog story.
6. **ALES 200 sheet hardwires the 600 s target.** NZ.3.12.1 f makes the
   target a CD-announced parameter (the F27 gap recorded at
   `SeedNzMAles200.cs:110`) — a second, independent way the sheet cannot
   score a real Class M contest whose CD set a target other than 10 minutes.
7. **Class M NDC's landing table is genuinely ambiguous in the source.**
   NZ.3.12.7 b's internal cross-reference is garbled ("Contest rules as per
   3.13.1 except scoring as per 3.13.7.c" — no 3.13.7 exists), so whether
   Class M NDC inherits the graded metre table or Class N's 50/25/0 circles
   is a reading, not a citation. Both the sheet and `SeedNzMNdc` resolve it
   to the graded table; flag for confirmation with the NZMAA.

## Verification

Findings were independently verified by a sub-agent that re-extracted all 740
formulas from the workbook XML (30 distinct formula shapes), re-read every
cited seed file and rule text, and hand-checked the numeric examples. Outcomes:
headline CONFIRMED; ALES 123 CONFIRMED; Radian PARTIAL — the original "broken
landing table" claim was **refuted** (keys are NZ.2.4.4 tape readings, not cm;
the table is a correct 50/25/0 encoding — this doc has been corrected); X5J,
F5J, F3K, cross-cutting 1/2/5 CONFIRMED, 3 CONFIRMED with the pass-through
interpretation above, 4 CONFIRMED with the F3K nit. All quoted line cites in
the seed files were checked exact. Additions from verification are folded in
above (cross-cutting 2 scope, mode-2 meaning and defaults, findings 6–7).

## Sheet-by-sheet formula reference (extracted)

- ALES 200 (sheets 3/4): `600-ABS(600-(D*60+E))+landing`; sheet 4 builds the
  landing lookup in: `MIN(50,IF($U$1=2,VLOOKUP(F4,$W$4:$W$27,1,FALSE),VLOOKUP(F4,$V$4:$W$27,2,FALSE)))`;
  total `G4+K4+O4+S4`.
- ALES 123 (sheets 1/2): `360-ABS(360-(D*60+E))+landing`; total `G4+K4+O4`;
  sheet 2 lookup keyed on `$R$4:$S$27`.
- ALES Radian (sheet 5): `420-ABS(420-(D*60+E))+MIN(50,IF($Q$1=2,VLOOKUP(F4,$S$4:$S$27,1,FALSE),VLOOKUP(F4,$R$4:$S$27,2,FALSE)))`;
  total `G4+K4+O4`.
- X5J (sheet 6): `600-ABS(600-(D*60+E))+MIN(50,IF($U$1=2,VLOOKUP(F4,$W$4:$W$27,1,FALSE),VLOOKUP(F4,$V$4:$W$27,2,FALSE)))`;
  total `G4+K4+O4+S4`.
- F5J (sheet 7): `600-ABS(600-(D*60+E))+MIN(50,IF($Y$1=2,VLOOKUP(G4,$AA$4:$AA$27,1,FALSE),VLOOKUP(G4,$Z$4:$AA$27,2,FALSE)))-IF(F4>200,100+3*(F4-200),0.5*F4)`;
  total `H4+M4+R4+W4`.
- F3K (sheet 8): B `MIN(240,D*60+E)+MIN(240,F*60+G)`;
  D `MIN(300,J*60+K)+MIN(300,L*60+M)`;
  G `MIN(120,P*60+Q)+...` (five terms);
  H `MIN(60,AB*60+AC)+MIN(120,AD*60+AE)+MIN(180,AF*60+AG)+MIN(240,AH*60+AI)`;
  total `H4+N4+Z4+AJ4`.

Landing tables (U/V/W or Q/R/S columns): ALES 200, X5J, ALES 123, F5J use the
graded metre table 50/45/40/35/30/25/20/15/10/5/0 keyed on NZ.2.4.4 FAI-tape
readings (100…50 for the 1–10 m bands, plus 45…0 → 0); ALES Radian uses
50/25/0 on the same tape scale (keys 100…70 → 50, 65…30 → 25, 0 → 0), which
is a correct encoding of NZ.3.15.1 e on that instrument.
