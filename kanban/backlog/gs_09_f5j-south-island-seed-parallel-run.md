# Story — Witness exceptional F5J scoring with the South Island seed run

**Status:** Backlog · **Raised:** 2026-10-01
**Sequence:** GS 09/13 · Milestone 3: targeted seed witnesses
**Dependencies:** GS 02 complete-result comparison and GS 03 exact difference
contracts. Reuse GS 01 snapshot conventions and GS 04 metadata when available.

## What

Replay `f5j-nz-south-island` under the shipped `30-f5j` seed, pinning the
exceptional height/zeroing rows and the resulting standings at the source snapshot.

## Why it matters

The historical-definition fixture includes a persisted raw score of −2026,
normalisation to zero, a recorded motor-restart/zeroed row and partial-round
history. Christchurch's existing canonical-seed run does not witness the same
exceptional data. Seed portability needs evidence at those boundaries.

## Before starting

- Read the fixture's config, raw rows, expected cells, ladder and provenance;
  identify actual scored-window and exception evidence before constructing replay.
- Read current `SeedF5J`/`30-f5j`, the Christchurch parallel-run provenance and
  `kanban/deferred-decisions.md` floor decisions. Do not rely on old floor defaults.
- Consult `docs/rules/f5j.md` and source clauses via `fai-rules`: `5.5.11.7`,
  `5.5.11.12` and `5.5.11.13`. Establish what a GS motor-restart flag actually
  proves before mapping it to an offence or a seed input.
- Confirm landing evidence/instrument compatibility and preserve recorded readings.
  The seed pairing is canonical F5J, not an inferred NDC classification by venue.
- Requirements: NFR-1/2 class-data law, NFR-4 missing/partial metrics and
  `docs/users.md` raw-metric capture and trustworthy standings.

## Implementation outline

1. Declare source snapshot, observed metrics, evidence limitations, instrument and
   parameter mapping. Separate an observed zero outcome from proof of its cause.
2. Replay with the shipped seed through the shared driver and public API.
3. Assert the extreme-height row at raw and normalised grains, and compare
   zero/no-flight/exception rows according to their actual evidence.
4. Compare totals, available discards, places and complete population. Measure
   drop/grid/floor differences rather than assuming the Christchurch result set.
5. Triage exact differences under GS 03, add the scenario and update coverage.

## Acceptance criteria

- [ ] The canonical seed and declared snapshot are verified in the replay.
- [ ] The negative-raw source row has an explicit seed-side raw/normalised outcome
  with rulebook versus GS configuration reasoning.
- [ ] Motor-restart or other exception mapping is evidence-backed; a flight-less
  replay producing zero is not described as validating a causal penalty branch.
- [ ] All available final-result fields and population are compared.
- [ ] Unobserved flags/defaults remain disclosed assumptions or coverage gaps.
- [ ] Historical parity and all seed comparisons pass strict checks on both stores.

## Verification and completion

Use the exceptional real rows as acceptance pins. Add focused domain tests only
for defects or genuinely new boundary behaviour exposed by triage; a known zero
must not substitute for exercising its rule predicate. Update coverage and any
related debt, then move to `completed/`.
