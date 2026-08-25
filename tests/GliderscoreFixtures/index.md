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
  no re-flights. Currently exercises none of the diversity targets below —
  every hunt item remains open.

## Standing skip reasons

A competition matching either of these is indexed as `- <slug> — skipped —
<reason>` and never activated silently:

- §6 concept gaps — team scoring, series, merged/prelim.
- Multi-task-per-round comps (F3B-style), until multi-task rounds exist — they
  hit the deferred `unsupportedRoundComposition` draw rejection today.

## Diversity wanted

Hunt comps that provide (none witnessed yet by ales-sample-comp):

- ≥ 1 multi-group round;
- ≥ 1 re-flight (`OriginalRoundNo ≠ RoundNo`);
- ≥ 1 drop threshold crossed (an F3K comp would witness divergence D6 — GS
  drops at 12 scored rounds vs official 6);
- ≥ 1 `Decs ≥ 1` comp (witnesses the float32 artifacts integral scores hide);
- Speed/Distance families when available.
