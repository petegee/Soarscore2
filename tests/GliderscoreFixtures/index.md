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
  ladder (30/30 ranks, 30/30 dropped-round picks); GroupScoreDecimals=1 yet no
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

## Standing skip reasons

A competition matching either of these is indexed as `- <slug> — skipped —
<reason>` and never activated silently:

- §6 concept gaps — team scoring, series, merged/prelim. Amendment
  (2026-08-26): a team or series gap forces skip-listing UNLESS the fixture's
  `competition.json` records a sound `triageJustification` — series:
  `CompSeries` dead-link count 0; teams: no team columns in `Scores`, with any
  populated `CompPilots.Team` assignments noted honestly. Preliminary /
  merged-prelim gaps remain unconditional skips. A harness mismatch backstops
  any wrongly justified activation.
- Multi-task-per-round comps (F3B-style), until multi-task rounds exist — they
  hit the deferred `unsupportedRoundComposition` draw rejection today.

## Diversity wanted

Status after the 2026-08-26 five-fixture growth: most original targets are now
witnessed corpus-wide (per-fixture detail lives in the Competitions lines
above); pick the next gap-hunt target from **Still open**.

Witnessed:

- Multi-group normalisation rounds — f3j-international (16 × 4 groups),
  jerilderie-2010 (14 × 5);
- Re-flight (`OriginalRoundNo ≠ RoundNo`) — jerilderie-2010 only (R13/G1,
  OriginalRoundNo=12);
- Drop threshold crossed — f3j-international (Drop1@8 over 16 rounds),
  f3k-sample-comp (Drop1@5 via option-0 distinct-scored-round counting, with
  Drop2@9 never biting), jerilderie-2010 (Drop1@6 AND Drop2@12 — two drops);
- F3K family with per-round task catalogue — f3k-sample-comp (G, A(1), F, D,
  C(3), X×4 via F3KTaskByRound);
- Decimal vs integral scoring — GroupScoreDecimals=1 (f3j-international)
  against 0 in all four others;
- Placeholder zero rounds — ales-sample-comp (R2–R3 wholly unflown,
  Updated='False'), f3k-sample-comp (NoTaskSet rounds 6–9), f3j-international
  (phantom R1 group 5); jerilderie-2010's zeros are genuine, not placeholders;
- Penalty deduction — jerilderie-2010 (pilot 2, −100), f3k-sample-comp (four
  Penalty=100 rows subtracted post-sum);
- Perfect maximum final score — jerilderie-2010 leader 12000 = 12 kept × 1000;
- Fly-off-shaped standalone comp — f3j-international-flyoff (small
  single-group shape; not fly-off mechanics).

Still open:

- Float32 persist-cast artifacts under `Decs ≥ 1` — null result so far:
  f3j-international runs Decs=1 yet no binary32 widening appears anywhere in
  the export; needs a future export from a GS version/build that stores
  Singles;
- Speed/Distance family rows in an ACTIVE fixture (they exist only for
  skip-listed f3b-international);
- Divergence D6 proper (factory-default drop threshold, GS 12 vs official 6) —
  every crossed threshold so far is custom (8; 5/9; 6/12);
- F5J/F5L/F5B families — absent from the corpus entirely;
- Merged/prelim comp (fly-off selected within one comp) — PrelimCompNo=-1 and
  MergedComps empty throughout;
- More than one timekeeper — every Dur row carries durNumberOfTimekeepers=1.
