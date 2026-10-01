# Story — Replay the F5K NI fixture under the canonical seed class

**Status:** Backlog · **Raised:** 2026-10-01
**Sequence:** GS 07/13 · Milestone 3: targeted seed witnesses
**Dependencies:** GS 01 snapshot semantics, GS 02 complete-result comparison and
GS 03 exact difference contracts. Use GS 04 registry/coverage tooling when landed.

## What

Add the first real-data parallel run of `f5k-ni-round-2` under the shipped
`tools/Soarscore.SeedData/json/40-f5k.json`, comparing the source's scored snapshot
at cell and complete-result levels.

## Why it matters

The fixture currently proves a GS-adapted class definition. The canonical seed
has different discard/precision policy, and those differences have not yet been
measured end to end against this real event.

## Before starting

- Read the fixture's definition/version notes, source config/provenance, GS 01
  outcome and `parallel-run-mapping.md`. Re-verify current seed data; earlier
  task A/D pairing defects recorded in tech debt have already been discharged.
- Consult `docs/rules/f5k.md` and original clauses via `fai-rules`, particularly
  `5.5.10.4`, `5.5.10.15–17`, before interpreting launch scoring, precision, drops
  and ties. Historical GS configuration remains the external comparator.
- Requirements: NFR-1/2 seed definitions govern class behaviour; NFR-4 governs the
  partial snapshot; `docs/users.md` requires trusted results and raw-metric capture.
- Confirm capture mapping for each seed-declared metric and task, including task
  B's launch count, A/D flight selection and constant NLH. Declare assumptions.

## Implementation outline

1. Confirm semantic task/metric/instrument compatibility and parameter bindings.
   Use raw recorded observations, not GS-computed point fields as inputs.
2. Replay the canonical seed through the shared public-command driver using the
   GS 01 source snapshot. Do not adapt the seed to recover historical totals.
3. Compare raw/normalised cells, totals, available discards, places and full field.
4. Triage the measured difference set with exact GS 03 expectations and verified
   rule/configuration citations. Correct actual seed/engine defects at source.
5. Add the acceptance scenario, ledger and corpus/seed coverage entry together.

## Acceptance criteria

- [ ] The published/adopted definition is the actual shipped `40-f5k` seed.
- [ ] Snapshot and metric/binding provenance are checked mechanically.
- [ ] All available result fields run under the complete comparison contract.
- [ ] Every observed difference is exact and explained; expected candidates that
  do not occur remain provenance notes rather than dead ledger entries.
- [ ] The historical F5K parity scenario remains independently covered.
- [ ] Both-store strict GliderScore runs and fixture validation pass.

## Verification and completion

Reuse comparison mutation/self-checks from GS 01–03. The new acceptance scenario
is the real-data portability witness; add a domain regression only if triage
finds a genuine scoring defect. Update coverage/debt and move to `completed/`.
