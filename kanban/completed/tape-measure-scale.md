# Story — Tape-measure scale: a generic distance-marked instrument so every contest declares how landing was measured

**Status:** Completed (2026-09-29; WI-1–WI-3 landed, sub-agent per WI).

## What

A fourth tape in the tape corpus: a generic "tape-measure" instrument
marked in centimetres/metres (not points), so a contest that measures
nose-to-spot with a steel tape or tape-measure declares *that* instead of
leaving landingDistance instrument-free. Its scale must start above 0 so
an entered 0.0 resolves off-scale → 0 landing points through the existing
`OffScaleReading` machinery — no engine change (see
`kanban/deferred-decisions.md`: the exact-zero convention entry — the
engine zero-branch is declined, this tape is the outstanding half).

## Why it matters

Today "no instrument declared — distances only" is the silent default
(`LandingTapeDeclaredScale.feature`: "No instrument declared — distances
only, existing behaviour"). The owner's NdcScore plan makes the choice
mandatory up front (points-tape, one of the named tapes, or tape-measure),
so the record always says how a landing was measured. The SoarScore2 half
is just the missing instrument: the corpus has three tapes
(`TapeCorpus.ExpectedCount = 3`: nz-f3b-side, nz-f3j-side, ales-m-10m) and
no generic distance scale. The metre-marked ALES M tape
(`SeedTapeNzAlesM10m.cs`) is the shape precedent — reading = metres,
identity composition — but it is Class M's prescribed 1 m-increment
instrument with `OffTapeReading` deliberately null, not a generic scale.

## Plan sketch (grows here)

- **WI-1 — the scale — DONE 2026-09-29.** `SeedTapeMeasure`
  (`tools/Soarscore.SeedData/SeedTapeMeasure.cs`, file/name settled):
  centimetre marks 0.01–15.00 m (1500, Reading == UpTo), first mark above 0,
  off-scale reading 0. Sub-question settled: generated programmatically in a
  loop — `TapeMarks`/`TapeIntegrity` only need ascending distinct marks, and
  1500 explicit lines add drift surface, not safety. 15 m covers every corpus
  landing boundary (F3J/F3B sides to 15 m; ALES N/P 25 pt boundary at 15 m).
- **WI-2 — corpus + composition — DONE 2026-09-29.**
  `TapeCorpus.ExpectedCount` 3 → 4 + `tape-measure` entry; integrity green;
  composition matrix in the `SeedTapeNzAlesM10m.cs` comment style (verified
  empirically, not guessed): composes (identity, 1501 awards) with all 11
  distance-keyed tables (50-f3j, 60-f5l, 20-f3b, 30-f5j, 85c-nz-f5j-ndc,
  86-nz-x5j, 80-nz-m-ales200, 81-nz-m-ndc, 83-nz-n-ales123, 85-nz-p-radian,
  87-nz-h-thermal-2m); refuses unitless (unitMismatch); no pairing for
  10-f3k, 70-f3f, 85b-nz-f3k-ndc, 90-aggregate. d==0 pin extended:
  `Tape_measure_scale_starts_above_zero_with_off_scale_zero` (entered 0.0
  resolves off-scale → 0 on F3J and F5J). Domain suite 925/925 green.
- **WI-3 — seeds/fixtures — DONE 2026-09-29.** Emitter exit 0, counts 17/4;
  new `tapes/tape-measure.json` (1500 marks, offTapeReading 0); class JSONs
  byte-silent; emitter idempotent; `json/` still gitignored so nothing to
  commit. Proof/composition filter 49/49 green.

## Before starting

- Confirm with the owner: is "a choice must be declared" enforced anywhere
  server-side (per-competition capture policy exists as a mechanism), or
  purely an NdcScore gate with SoarScore2 only supplying the instrument?
  Default assumption: client-only; no engine/capture change here.
- Confirm the scale's max and mark spacing against what clubs actually lay
  (steel tape / tape-measure practice), and the file/name convention.
- NdcScore's mandatory-choice UI is that repo's work
  (`ss_landing-zero-scores-no-landing-points.md`,
  `ss_f5j-flight-time-cap-at-959.md` unblocked from this side by
  landing-zero-and-flyaway-encoding) — track, don't duplicate.
