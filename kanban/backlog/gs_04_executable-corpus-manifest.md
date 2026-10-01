# Story — Establish an executable GliderScore corpus manifest

**Status:** Backlog · **Raised:** 2026-10-01
**Sequence:** GS 04/13 · Milestone 2: systematic coverage and maintenance
**Dependencies:** `gs_02_complete-result-oracles.md`, `gs_03_exact-divergence-contracts.md`.

## What

Create a small machine-readable corpus registry and validated fixture replay data.
Generate the corpus/seed coverage summaries from their authoritative inputs and
verify that every active fixture has an executable comparison.

## Why it matters

`index.md`, `parallel-run-mapping.md`, scenarios and driver dictionaries currently
share responsibility without a consistency check. The F3J mapping retained an old
placing-split count after its ledger changed. Adding a manifest entry alone also
does not currently ensure an acceptance scenario exists.

## Before starting

- Inspect `FixtureLoader.ActiveSlugs`, `ReplaySteps` and `ReplayDriver`'s bindings,
  synthetic slots, scored-window and instrument tables, plus `extract/validate.py`.
- Inventory existing provenance shapes before choosing the smallest shared schema.
  Avoid copying expected values or ledger facts into another authoritative file.
- Requirements: NFR-1/2 keep class arithmetic in definitions; NFR-3 favours a
  focused developer tool; NFR-4 requires explicit absence/snapshot semantics.
  `docs/users.md` needs trustworthy results. This introduces test metadata only.
- Preserve the offline acquisition contract: source databases, Python extraction
  and live GS services remain outside builds, acceptance tests and CI.

## Implementation outline

1. Specify a versioned registry for fixture identity/status, source references,
   oracle provenance, historical/seed modes, applicable comparison grains and
   links to snapshot, reconstruction and divergence evidence.
2. Move fixture-specific replay declarations from C# dictionaries into validated
   fixture data. Keep generic mechanisms in code and class formulas in definitions.
3. Update the loader and offline validator to consume the authoritative registry.
   Either derive data-driven scenarios or validate scenario coverage explicitly;
   retain intentional literal-record scenarios as additional workflows.
4. Generate the corpus index and fixture-to-seed coverage view. Reference ledgers
   and measured report data rather than duplicating result counts by hand.
5. Migrate atomically, recording old-to-new fields, and prove existing behaviour
   under all currently active modes before changing any fixture's coverage.

## Acceptance criteria

- [ ] An active fixture without its required executable scenario fails validation.
- [ ] Duplicate IDs, missing/orphaned references and incompatible snapshot or
  reconstruction declarations fail with the fixture and field identified.
- [ ] Generated summaries distinguish competition count, archive rows, compared
  cells, exclusions, seed witnesses, and external versus reconstructed oracles.
- [ ] Existing numerical expectations have one authoritative location each.
- [ ] GS-specific per-fixture declarations are data, with no new class-specific
  branches in production code.
- [ ] All existing strict comparisons retain their results on both stores.
- [ ] There is a deterministic regeneration/check command for summaries.

## Verification

Validate deliberately incomplete/contradictory registry examples and run the
existing corpus as migration regression coverage. Invariant: active declared
coverage equals executable coverage, excluding explicitly labelled supplemental
scenarios from the unique-competition count. Regeneration must be deterministic.

## Completion

Reconcile the effective-knob and fixture-binding debt this migration actually
discharges, document the curator entry points beside the corpus, and move the
story to `completed/`. GS 05 wires the contract into CI.
