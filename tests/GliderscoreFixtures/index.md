# GliderScore fixture corpus index

One line per competition — the manifest of what the corpus holds, and how
gap-hunting targets get chosen. Skipped comps stay listed forever. This file is
the contract `extract/validate.py --index` consumes (rule 5): each competition
is one `- <slug> — <status> — …` bullet; a slug counts as skip-listed when its
line starts with the slug token and contains "skipped".

## Competitions

- ales-sample-comp — active — DurALES (ALES duration + landing) — exercises
  packed-mmss time encoding; landing-points-after-time-normalisation giving
  winner 1030 > 1000; integral scores via GroupScoreDecimals=0, so the float32
  persist cast is unwitnessed; no drops active (Drop*=99); single-group rounds;
  no re-flights. Witnesses placeholder zero rounds (R2–R3 wholly unflown,
  Updated='False'); every other hunt item was witnessed by later growth —
  see Diversity wanted below.
- f3j-international — active — F3J (duration + entered-distance landing) —
  exercises 16 four-group rounds × 30 pilots with multi-group normalisation
  throughout; first crossed drop threshold (Drop1@8 over 16 scored rounds: one
  worst-score drop per pilot, landing wherever the worst sits, R1–R16);
  late-landing deductions live (`durFlightPenalty=1`, 11 rows at −30);
  gs-report-transcript oracle verified against an independently recomputed
  ladder (30/30 ranks, 30/30 dropped-round picks); team-standings witness — a
  reconstructed GS team ladder (`expected-teams.json`) transcript-verified 8/8
  against `team-results-transcript.csv`, asserted per-team by the harness's
  ladder grain (count, exact-decimal total, place identity + `=n` group
  membership, contributor set); GroupScoreDecimals=1 yet no
  binary32 widening visible anywhere — the float32 persist-cast witness remains
  open; no re-flights (the export's single re-flight row belongs to another
  comp).
- f3j-international-flyoff — active — DurGeneral (duration + entered-distance
  landing) — exercises a fly-off-shaped short comp: 7 pilots × 4 single-group
  scored rounds with integral scores (GroupScoreDecimals=0, so again unwitnessing
  the float32 persist cast); no drops configured (Drop*=99 over 4 rounds);
  gs-report-transcript oracle covering all four rounds, verified against an
  independently recomputed ladder (7/7 ranks and Score values, 28/28 round cells).
- f3k-sample-comp — active — F3K (task-per-round catalogue) — exercises the
  corpus's first F3K-family witness: nine single-group rounds × 10 pilots with
  the per-round task schedule carried faithfully from F3KTaskByRound (G, A(1),
  F, D, C(3), X×4 — six distinct tasks over nine rounds; task identity lives in
  that table, Scores.TaskNo stays 5); live-drop witness with the precise
  activation story — option-0 drops count DISTINCT rounds with RawScore>0, so
  Drop1@5 crosses at exactly five scored rounds while Drop2@9 never activates,
  rounds 6–9 are NoTaskSet placeholder zeros that don't count toward activation
  yet sit in the candidate pool, and R9 drops for every pilot without ever
  touching a real score; integral scores via GroupScoreDecimals=0 (float32
  persist cast again unwitnessed); teams + '1;2' series activated via sound
  triageJustification (dead CompSeries table; 4 populated teams never reaching
  persisted scores); gs-report-transcript oracle verified against an
  independently recomputed ladder (10/10 ranks, Score values and dropped-round
  picks).
- jerilderie-2010 — active — DurGeneral (duration + entered-points landing) —
  exercises the corpus's largest fixture: 63 pilots × 14 scored rounds × 5
  groups (70 groups of 11–14, 882 score rows) with multi-group normalisation
  throughout; the corpus's ONLY re-flight witness — R13/G1 SeqNo=14 carries
  OriginalRoundNo=12 (pilot 29 missed R12 entirely and re-flew inside R13,
  twice in one round; ReFlightNo=0 means the slot normalises within R13/G1 and
  aggregation keys it to the orig-12 cell); first TWO-drop witness — Drop1@6 +
  Drop2@12 both crossed over 14 scored rounds, so every pilot loses exactly two
  worst cells wherever they sit; per-pilot penalty subtraction witnessed
  (one −100); teams (14 populated) + '1;2' double dead-series activated via
  sound triageJustification; reconstructed-ladder oracle pending an Overall
  Results transcript — foundation is an independent recompute matching all 882
  persisted RawScore and NormalisedScore values exactly.
- f5j-christchurch-2019 — active — F5J (height-penalised duration + scheme-11
  landing) — exercises the corpus's first ACTIVE F5J-family fixture and THE
  float32 persist-cast witness (G4): persisted NormalisedScore values are
  clean exact-1dp doubles while a simulated binary32 persist cast flips
  99/162 scored values (secondary witness comp 98 uncited-in-corpus) — assert
  persisted values and emulate the cast in comparators, never expect dirty
  stores; a mid-comp snapshot: R1–R11 scored of 18 drawn rounds, ragged
  partial groups inside scored rounds (14–16 of 18 updated), R12–18
  wholly-unflown placeholders; F5J arithmetic min(packed-mmss, 600) −
  two-rate height penalty (0.5/m ≤200 m then 100+3.0/m above; launch height
  stored in Scores.FlightScoreDeduction) + ADDITIVE scheme-11 "F5J Enter
  Landing" points; effective 1dp half-up points-normalisation while the
  stored scoring knobs are Jet nulls DB-wide (the first corpus fixtures
  whose Comps knobs are nulls); reconstructed-ladder oracle.
- f5j-hawkes-bay-trials — active — F5J (height-penalised duration + scheme-11
  landing) — exercises FOUR re-flight cells (all pilot 128: R1→R5/G2,
  R2→R5/G3, R4→R6/G1, R3→R6/G3) with delete-on-reflight semantics — pilot 128
  holds no rows in R1–R4 and OriginalRoundNo ≠ RoundNo is the only in-DB
  marker (ReFlightNo=0 throughout: a detection trap) — de-singularising the
  corpus's single-witness re-flight gap (jerilderie-2010); the orig→new
  mapping is many-originals-to-many-new-cells across two destination rounds,
  keyed on OriginalRoundNo alone; 'Team Trials' is name-only (sound
  triageJustification recorded); 16 drawn rounds × 18 pilots, R1–10 scored;
  reconstructed-ladder oracle with drops provably un-fireable (thresholds
  unset or '99-never' in the Comps row; drop config is unset DB-wide).
- f3k-southern-fling — active — F3K (task-per-round catalogue) — exercises the
  corpus's FIRST per-group normalisation inside an F3K comp (15 rounds ×
  G1–G3, best-in-group → 1000 at 1 dp exact on 218/218 rows); task letters
  E/I/J/K are first corpus sightings (catalogue K,I,A(2),E,H,G,J,C(1),D,F,
  B(1),H,G,B(2),C(1)); mid-comp retirement as silent absence — pilot 89
  Retired=true after R8, no placeholder stubs afterwards, ranked on flown
  rounds only; ten tied-winner groups incl. a whole-group 5-way tie yet zero
  final-ladder ties; [REDACTION-NEEDED NOISE] phantom Landing=145.0 on 12/14
  R9 rows kept verbatim, inert by construction (no landing scheme attaches
  anywhere in this F3K comp — never read it as touchdown evidence);
  reconstructed-ladder oracle.
- f5j-nz-south-island — active — F5J (height-penalised duration + scheme-11
  landing) — exercises extreme height penalties: 19 scored rows over 200 m
  incl. a 1000 m launch whose computed raw −2026 persists UNfloored while its
  NormalisedScore clamps to 0.0 — flooring is witnessed at normalisation only;
  one motor-restart flag row (zeroed score fields, Updated=true) paired with
  its effect knob Dur.F5JMotorRestartOption=1; vacant-seat honesty — R7/G1
  SeqNos {1,2,4,5}, occupant unknowable from data, noted and NOT invented;
  zero-time no-flight rows score trivially exact; 16 rounds × 13 pilots
  (129 genuine flights among 133 updated rows); reconstructed-ladder oracle
  asserting the clamp row and un-fireable drops.
- f3k-june-2020 — active — F3K (task-per-round catalogue) — exercises the
  corpus's mid-comp group re-draw witness: R7's cancelled zeros re-flown as a
  NEW group G4 with re-flight bookkeeping untouched (ReFlightNo=0 everywhere
  and OriginalRoundNo==RoundNo — the re-draw lives entirely outside the
  re-flight model; cancellation flags mix True-flagged standing zeros and
  False stale placeholders side by side, so keep-highest-per-original-round
  dedup alone resolves the round); ragged empty seats G1 s1 / G2 s2+s3; five
  persisted R4/H slot-decode deviants kept raw untouched (slot-sum shortfalls
  12.3/2/4/29.6/12 s — a hardened RawScore==decoded-slot-sum validator must
  whitelist those five keys, never repair them); normalisation exact 199/199
  despite null stored knobs; reconstructed-ladder oracle settling the
  cancelled-zero aggregation.
- f5k-ni-round-2 — active — F5K2024 (multiple-task duration scored against a
  per-round nominal launch height) — exercises the corpus's FIRST F5K-family
  witness and the FIRST server-DB acquisition (`extract/extract-mssql.py`,
  WI-1 tooling, PII redaction on — a new path beside the Jet one): 6 pilots ×
  10 rounds × 2 groups (55 rows); per-round task schedule A–D carried
  faithfully from F5KTaskandRefHeightByRound (A,B,C,D,A,B,C,D,B,D) with NLH 60
  every round; launch-altitude band-integral scoring per flight (0.5/m bonus
  below the NLH under the ≥30 s guard, 1.0/m to 10 m over, 3.0/m beyond —
  SeedF5K LaunchBands exactly), decoded from GS's structured Flight1..4
  strings in THREE coexisting serialisations (uppercase, lowercase, one legacy
  long-key row); task B's stored-last-flight shape replayed with NOF
  launch-count padding so the counted flight carries its true sequence for the
  cumulative launch cost (0/−10/−20); every scored group's winner at exactly
  1000.000 under HalfUp-1dp points normalisation; mid-comp snapshot — R1–R6
  scored, R7–R10 unscored placeholder stubs (Updated=False) replayed as
  flight-less zeros, GS's results never recomputed past R6; a never-flew pilot
  (88: zero stubs R1–R5, rows deleted from R6, group unknowable —
  prescription-only slots, no entry, oracle omits him from R6 on); drop
  activation Drop1@5 over the ten prescribed rounds — our drop lands on a
  placeholder zero where GS's scored-round-gated pool drops a real score,
  placing-identical (verified at curation and by the replay), the score-level
  difference formerly never compared by any grain; oracle = GS's own persisted round-6
  progressive standings, independently recomputed cell-exact (30/30 raw,
  normalisation and drop arithmetic); empty divergence ledger. GS 01
  (2026-10-01) closes the score-level gap as a finalised snapshot: the replay
  prescribes the full ten-round draw but enters and completes only R1-R6,
  then finalises (6 flown >= minRounds 1 -- no invented completion), so R7-R10
  stay absent-not-zero (NFR-4, finding 5) and the drop lands on the worst
  real score exactly as GS's pool does; the oracle pins the five round-6
  totals exactly (5000.000 / 4928.100 / 4796.100 / 4359.900 / 4319.200,
  places 1-5), declares pilot 88 zero-only unranked (registered, never
  flew), excludes R7-R10 from comparison (35 cells compared of 55 intact),
  and the ranking grain enforces bidirectional population equality.
- f3b-international — skipped — multi-task-per-round (Duration+Speed+Distance)
  hits unsupportedRoundComposition; NO fixture directory was curated — it
  remains available only in the shared extraction of record
  sources/gliderscore-example-comps-extract/, which also makes it the export's
  only Speed/Distance family-row witness (Spd/Dis populated for this comp
  alone), so the class-family data exists if multi-task rounds ever land;
  shape verified there: 23 pilots × 9 rounds × 3 tasks/round, 579 score rows
  (rounds 1–8 fly all three tasks, round 9 is speed-only), grouped per task —
  duration in 4 groups, distance in 6 small groups, speed as one all-pilot
  group; UseTeams=true + dead series '1' flagged honestly, moot under this
  unconditional concept-gap skip.

## Complete-result oracle coverage (GS 02, 2026-10-01)

Every active fixture's `expected-result.json` now carries the complete-result
oracle contract beside its rank strings: `totals`, `preDropTotals`,
`penalties` (explicit zero where none — omission never means zero),
`discards` (the fixture's own drop dimension as the unit — `round` for
ByRound, `task` for ByTask — with round identities and values), `scoredWindow`
+ `lifecycle` + `excludedRounds` (the snapshot scope and row population), and
`fieldAvailability` (every result field declared available, unavailable or
inapplicable with a reason). The comparator asserts every available field
automatically (exact-decimal, no tolerance); conservation stays the
independent internal check. Provenance labels are unchanged —
reconstructed expectations never acquire a report/persisted label.

### Column migration (GS 04 Step 5)

The hand-maintained oracle table this block replaces (one row per fixture:
`fixture | source | window | drops | penalties`) migrated field-for-field —
no fixture's coverage changed, only where each cell is owned:

- `source` → `oracle`: `expected-result.json: source`, classed external
  (`gs-report-transcript`, `server-persisted-progressive`) vs reconstructed
  (`reconstructed-ladder`) by the generator's source reading.
- `window` → `window`: `expected-result.json: scoredWindow` first–last plus
  `lifecycle`; snapshot scope renders "of drawn R…–R…" when the window is a
  strict subset, and `excludedRounds` moved into `exclusions` with its
  measured uncompared-cell count. Lifecycle tokens absorb the old prose
  qualifiers (`-including-unflown-placeholders`, `-with-retirement-absence`,
  `-with-redraw-dedup`, `-snapshot-of-scored-window`).
- `drops` → `drops (unit)`: `expected-result.json: discards[]` — unit plus
  max dropped-round count over pilots-with-drops / ranked pilots; the old
  hand qualifiers ("proven empty", "`*` marks") are replaced by measured
  counts. `penalties` → `penalties`: `expected-result.json: penalties[]`
  (pilot:deduction, explicit zero where none).
- NEW measured columns with no hand predecessor: `archive rows`
  (`scores-raw.json` row count), `compared cells` (`expected-scores.json`
  keys inside the window), `exclusions` (`divergences.json` entries by grain
  plus parallel-run excluded oracle cells), `seed witnesses` (registry modes
  plus landed parallel-run ledger entry/cell counts).
- Prose-only, accepted openly (no machine-readable home): the parity
  standings-scope nuance on the three F5J bullets ("standings R1–R11/R1–R10")
  lives only in the bullet prose and the registry `oracleRef` parenthetical —
  `expected-result.json` scoredWindow pins the COMPARED window (full drawn
  scope including placeholder zeros), not the standings reach. The only
  machine-readable scored window is the parallel-run assertion
  (christchurch R1–R11), and it governs the parallel-run rollup, never parity
  scope. Unify into a schema field if a consumer ever needs standings reach
  corpus-wide.

<!-- corpus-generated:begin:oracle-coverage -->
| fixture | oracle | window | archive rows | compared cells | exclusions | drops (unit) | penalties | seed witnesses |
|---|---|---|---|---|---|---|---|---|
| ales-sample-comp | reconstructed-ladder (reconstructed) | R1–R3, finalised-full-including-unflown-placeholders | 30 | 30 | none | 0 (round, proven empty) | 0 | 80-nz-m-ales200: ledger 3 entries/3 cells |
| f3j-international | gs-report-transcript (external) | R1–R16, finalised-full | 485 | 485 | ledger 2 (1 normalised, 1 raw); parallel-run excluded 1 cell | 1 (task, 30/30 pilots) | 0 | 50-f3j: ledger 18 entries/291 cells, 1 excluded |
| f3j-international-flyoff | gs-report-transcript (external) | R1–R4, finalised-full | 28 | 28 | none | 0 (round, proven empty) | 0 | parity only |
| f3k-sample-comp | gs-report-transcript (external) | R1–R9, finalised-full-including-unflown-placeholders | 90 | 90 | ledger 1 (1 team) | 1 (task, 10/10 pilots) | pilot 42:−100, pilot 56:−200, pilot 65:−100 | parity only |
| jerilderie-2010 | reconstructed-ladder (reconstructed) | R1–R14, finalised-full | 882 | 882 | ledger 1 (1 team) | 2 (task, 63/63 pilots) | pilot 2:−100 | parity only |
| f5j-christchurch-2019 | reconstructed-ladder (reconstructed) | R1–R18, finalised-full-including-unflown-placeholders | 324 | 324 | none | 0 (round, proven empty) | 0 | 30-f5j: ledger 8 entries/148 cells (scored window R1–R11) |
| f5j-hawkes-bay-trials | reconstructed-ladder (reconstructed) | R1–R16, finalised-full-including-unflown-placeholders | 288 | 288 | none | 0 (round, proven empty) | 0 | parity only |
| f3k-southern-fling | reconstructed-ladder (reconstructed) | R1–R15, finalised-full-with-retirement-absence | 218 | 218 | ledger 14 (7 normalised, 7 raw) | 0 (round, proven empty) | 0 | parity only |
| f5j-nz-south-island | reconstructed-ladder (reconstructed) | R1–R16, finalised-full-including-unflown-placeholders | 208 | 208 | none | 0 (round, proven empty) | 0 | parity only |
| f3k-june-2020 | reconstructed-ladder (reconstructed) | R1–R13, finalised-full-with-redraw-dedup | 199 | 199 | ledger 11 (4 normalised, 7 raw) | 0 (round, proven empty) | 0 | parity only |
| f5k-ni-round-2 | server-persisted-progressive (external) | R1–R6 of drawn R1–R10, finalised-snapshot-of-scored-window | 55 | 35 | excludedRounds=[7, 8, 9, 10] (20 cells uncompared) | 1 (round, 5/5 pilots) | 0 | parity only |
| f3b-international | none (skipped) | — | n/a (no directory) | n/a | n/a | n/a | n/a | skipped |

Corpus: 12 competitions (11 active, 1 skipped); 2807 archive rows; 2787 compared cells; 20 excluded-round cells uncompared; 29 ledgered divergence entries; 3 seed-witness pairs (3 ledgers landed, 442 structured witness cells); oracles 4 external / 7 reconstructed; team ladders 1.
<!-- corpus-generated:end:oracle-coverage -->

Pilot 88 (f5k, registered never-flew) is declared zero-only unranked and
carries no discard assertion — his tied-zero cells evidence no external
identity. Percent stays display-only and deliberately unrecorded everywhere.

## Standing skip reasons

A competition matching either of these is indexed as `- <slug> — skipped —
<reason>` and never activated silently:

- §6 concept gaps — series, merged/prelim. Amendment (2026-08-26): a series
  gap forces skip-listing UNLESS the fixture's `competition.json` records a
  sound `triageJustification` — `CompSeries` dead-link count 0. Preliminary /
  merged-prelim gaps remain unconditional skips. A harness mismatch backstops
  any wrongly justified activation. Amendment (2026-09-07, team-parity story
  Move 3): **team scoring is no longer a concept gap** — teams-mvp landed it,
  so a team-bearing fixture activates with declared team-grain expectations
  instead of a no-effect excuse: `NbrForTeamScore == 3` with populated
  `CompPilots.Team` requires `expected-teams.json` (the ladder grain's
  oracle); any other Nbr requires a documentary `T1` entry (`grain: "team"`)
  in `divergences.json`. `extract/validate.py` enforces both arms (rule 5);
  the harness guard backstops the Nbr=3 arm at replay time.
- Multi-task-per-round comps (F3B-style), until multi-task rounds exist — they
  hit the deferred `unsupportedRoundComposition` draw rejection today.

## Diversity wanted

Status after the 2026-08-26 five-fixture growth, the 2026-08-27 NZ-master
growth (five fixtures) and the 2026-09-03 team-parity story: most original
targets are now witnessed corpus-wide
(per-fixture detail lives in the Competitions lines above); pick the next
gap-hunt target from **Still open**.

Witnessed:

- Multi-group normalisation rounds — f3j-international (16 × 4 groups),
  jerilderie-2010 (14 × 5);
- Re-flight (`OriginalRoundNo ≠ RoundNo`) — jerilderie-2010 (single cell,
  R13/G1, OriginalRoundNo=12) and now FOUR further cells in
  f5j-hawkes-bay-trials (all pilot 128; delete-on-reflight semantics;
  many-originals-to-many-new-cells across two destination rounds, keyed on
  OriginalRoundNo alone);
- F5J family — three fixtures with complementary configurations:
  f5j-christchurch-2019 (mid-comp snapshot with ragged partial groups;
  two-rate height penalty exercised in both regimes), f5j-hawkes-bay-trials
  (delete-on-reflight aggregation), f5j-nz-south-island (extreme-height clamp,
  motor-restart pairing, vacant seat);
- F3K multi-group / per-group normalisation — f3k-southern-fling (FIRST corpus
  witness: 15 rounds × G1–G3; ten tied-winner groups incl. a whole-group 5-way
  tie yet zero final-ladder ties), f3k-june-2020 (also, incl. a four-group
  re-draw round);
- Mid-comp group re-draw — f3k-june-2020 (R7 cancelled zeros re-flown as a NEW
  group G4 outside the re-flight bookkeeping — ReFlightNo=0 and
  OriginalRoundNo==RoundNo everywhere; keep-highest-per-original-round dedup
  alone resolves the mixed cancellation flags);
- Motor-restart-effect pairing — f5j-nz-south-island (flagged row zeroed
  outright beside Dur.F5JMotorRestartOption=1; contrast f5j-christchurch-2019,
  where restart flags persist under a null option and take no scoring effect);
- Float32 persist-cast residue — witnessed as a comparator property over CLEAN
  stored data: f5j-christchurch-2019's persisted NormalisedScore values are
  exact-1dp doubles and only an EMULATED binary32 persist cast flips 99/162
  scored values (secondary comp 98 uncited-in-corpus) — comparator strategy
  must emulate the persist cast rather than expect dirty stores;
- Drop threshold crossed — f3j-international (Drop1@8 over 16 rounds),
  f3k-sample-comp (Drop1@5 via option-0 distinct-scored-round counting, with
  Drop2@9 never biting), jerilderie-2010 (Drop1@6 AND Drop2@12 — two drops);
- F3K family with per-round task catalogue — f3k-sample-comp (G, A(1), F, D,
  C(3), X×4 via F3KTaskByRound), f3k-southern-fling (letters E/I/J/K are first
  corpus sightings; catalogue K,I,A(2),E,H,G,J,C(1),D,F,B(1),H,G,B(2),C(1));
- Decimal vs integral scoring — stored knobs: GroupScoreDecimals=1
  (f3j-international) against 0 in all four others; NOTE the NZ master stores
  GroupScoreDecimals/GroupScoreOption as Jet nulls DB-wide, so the five NZ
  fixtures record their behaviourally-derived effective grids (e.g. 1 dp
  half-up points-normalisation) via configProvenance/knobProvenance in each
  competition.json instead of stored values;
- Placeholder zero rounds — ales-sample-comp (R2–R3 wholly unflown,
  Updated='False'), f3k-sample-comp (NoTaskSet rounds 6–9), f3j-international
  (phantom R1 group 5), f5j-christchurch-2019 (R12–18 wholly unflown);
  jerilderie-2010's zeros are genuine, not placeholders;
- Penalty deduction — jerilderie-2010 (pilot 2, −100), f3k-sample-comp (four
  Penalty=100 rows subtracted post-sum);
- Perfect maximum final score — jerilderie-2010 leader 12000 = 12 kept × 1000;
- Fly-off-shaped standalone comp — f3j-international-flyoff (small
  single-group shape; not fly-off mechanics);
- GS team-standings ladder (the Nbr=3 overlap grain) — f3j-international: a
  reconstructed GS team ladder (`expected-teams.json`, 8 teams) transcript-
  verified 8/8 against `team-results-transcript.csv` and asserted per-team by
  the harness's ladder grain (standings count, exact-decimal total, place
  identity + `=n` group membership, contributor set) — the corpus's first
  true GliderScore team-standings parity witness.

Still open:

- Speed/Distance family rows in an ACTIVE fixture (they exist only for
  skip-listed f3b-international);
- Divergence D6 proper (factory-default drop thresholds, GS default 12 vs
  official 6) — every crossed threshold so far is custom (8; 5/9; 6/12), and
  the NZ master confirms this unwitnessable from that source: Drop-config
  columns are unset on ALL 168 Comps rows of NZContests.mdb;
- More than one timekeeper — absent DB-wide on the NZ master; every Dur row
  carried by the corpus sets durNumberOfTimekeepers=1;
- F5L/F5B families — still absent entirely (the NZ growth added F5J/F3K only);
- Merged/prelim comp (fly-off selected within one comp) — PrelimCompNo=-1 and
  MergedComps empty throughout, NZ fixtures included;
- Second Nbr=3 team-standings witness — a second ACTIVE comp meeting the
  ladder grain's overlap condition (UseTeams=true, NbrForTeamScore=3, AND
  populated teams — met today by f3j-international alone; f3b-international
  meets all three but sits behind its multi-task-round skip); hunt recorded,
  acquisition pending — webmine permission gate unticked, nothing actionable
  offline (NZ master Team='0' on all 168 comps);
- `OmitFromTeamScore=true` witness — a protection-only member excluded from
  its team's score; implemented but unexercised: zero sightings in every
  committed CompPilots.json, entries.json and source extraction; hunt
  recorded, stub kanban/backlog/gs_12_omit-from-teamscore-witness.md;
- Team classification for `NbrForTeamScore ≠ 3` — jerilderie-2010 (Nbr=4, 14
  populated teams) and f3k-sample-comp (Nbr=2, 4 populated teams) carry real
  team assignments but their team grain is T1-ledgered; unlocks only via a
  future classification-policy story.
