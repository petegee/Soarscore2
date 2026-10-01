# Story — Strengthen reconstructed GliderScore result oracles

**Status:** Backlog · **Raised:** 2026-10-01
**Sequence:** GS 06/13 · Milestone 2: independent result evidence
**Dependencies:** Acquisition can start immediately; consume the richer oracle
contract from `gs_02_complete-result-oracles.md` when incorporating reports.

## What

Seek independently observed GS result reports for the seven fixtures whose final
ladders are reconstructed, and promote each oracle only after a verified match
to the corresponding source snapshot.

## Why it matters

Independent reconstruction checks consistency with our interpretation of GS.
An actual GS report provides stronger evidence of its report-time behaviour,
particularly drops, penalties, make-up aggregation and ties.

## Acquisition checklist

- [ ] `jerilderie-2010` — first priority: two drops, penalty and make-up flight.
- [ ] `f5j-hawkes-bay-trials` — destination-round aggregation.
- [ ] `f3k-june-2020` — catalogue scoring and cancelled/re-drawn rows.
- [ ] `f3k-southern-fling` — retirement and historical row population.
- [ ] `f5j-christchurch-2019` — partial scored window with later placeholders.
- [ ] `f5j-nz-south-island` — extreme-height and zeroed-cell aggregation.
- [ ] `ales-sample-comp` — sample totals and shared zero-score placings.

Each item may be delivered independently. Record requested report, acquisition
route, source/version/window evidence and disposition in the fixture provenance
or this story while open; move blocked acquisition work to the appropriate lane.

## Before starting

- Read each fixture's provenance/ladder notes and GS 02's evidence availability
  contract. Confirm this list against current oracle provenance.
- Prefer supplied reports or a faithfully reproduced report from the corresponding
  GS source snapshot/version. A rescore by a different release/configuration is
  not automatically the same oracle; record and investigate the difference.
- Observe existing source/redaction and live-acquisition permission requirements
  in `tests/GliderscoreFixtures/{extract,webmine}/README.md`.
- Requirements: trustworthy results (`docs/users.md`), deterministic scoring
  (NFR-1/2). Historical reports do not overrule official seed rules; use `fai-rules`
  if interpreting a rules difference. No production or rulebook change is implied.

## Per-report delivery

1. Obtain the report with competition identity, included rounds, configuration and
   product-version evidence where available. Preserve the original artifact/hash
   according to the corpus's existing data handling conventions.
2. Transcribe totals, places, penalties and discard information actually displayed.
   Verify pilot/team mapping and do a second independent transcription/check.
3. Compare the report to the existing reconstruction and current replay; triage
   every disagreement before changing expectations or provenance labels.
4. Populate the GS 02 oracle fields and record unavailable fields honestly.
5. Validate the fixture, run affected comparisons on both stores and regenerate
   coverage summaries via GS 04 when available.

## Acceptance criteria

- [ ] Every target has an explicit acquisition outcome and evidence location.
- [ ] Every acquired report is tied to the source snapshot and scored window.
- [ ] Every promoted oracle is independently transcription-checked and reconciled.
- [ ] Unobtainable reports retain reconstructed provenance and a visible evidence
  gap; a Python ladder's output is never relabelled as a GS report.
- [ ] Remaining acquisition dependencies are explicit blocked/follow-up work, so
  completing an engineering tranche never implies all seven reports were obtained.

## Completion

Close only when every target is either delivered or explicitly transferred to
tracked follow-up acquisition work with its reason. This story does not block
GS 01–05 or the seed witnesses.
