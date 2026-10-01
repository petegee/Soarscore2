# Story — Make accepted GliderScore differences precise and executable

**Status:** Backlog · **Raised:** 2026-10-01
**Sequence:** GS 03/13 · Milestone 1: trustworthy comparisons
**Dependencies:** `gs_02_complete-result-oracles.md` for the complete result grains.

## What

Replace broad wildcard/count-only numerical allowances with structured, exact
difference expectations. Retain distinct historical-parity and seed-parallel
claims, sharing comparison primitives where useful.

## Why it matters

Some parallel ledgers pin mismatch counts inside prose while accepting any cell
in a grain. A different set or magnitude of mismatches can satisfy the same count.
Parity's permanent entries also need different treatment when they describe a
real numerical difference versus an intentionally unrun comparison.

## Before starting

- Read `Support/Gliderscore/{FixtureModels,ParallelRunLedger,Comparator,
  ParallelRunComparator,LedgerGate}.cs`, the replay/parallel steps and all ledgers.
- Preserve the exact-decimal, R1 representation and T1 team-policy decisions in
  `kanban/deferred-decisions.md`, and strict-mode behaviour from the completed
  `gs-ledger-modes.md` story. Numerical permanence does not mean unbounded waiver.
- Requirements: trustworthy results (`docs/users.md`), deterministic class-driven
  scoring (NFR-1/2). This changes test evidence contracts, not scoring rules or
  domain concepts; rulebook citations remain subject to `fai-rules` verification.

## Implementation outline

1. Define typed expectations for numerical differences, excluded source cells,
   synthetic replay cells and unsupported comparisons. Include stable identities,
   grain/field, evidence reference, reason and disposition.
2. For finite golden snapshots pin GS and SoarScore values/deltas or sets. A
   relationship-based expectation needs a narrow, justified predicate and an
   explicit identity universe; arbitrary tolerance is not an alternative.
3. Migrate existing ledgers using measured comparisons and their prior evidence.
   Re-triage unexplained changes rather than snapshotting today's output blindly.
4. Require bidirectional matching for observed differences, including permanent
   numerical ones. Validate documentary exclusions against their declared scope
   and applicability rather than requiring a nonexistent numerical mismatch.
5. Remove prose-regex count extraction and render readable explanations from
   structured fields. Preserve historical-versus-seed provenance distinctions.

## Acceptance criteria

- [ ] A mismatch moved to another cell fails even with the same total count.
- [ ] An unexpected value/delta or contributor-set change fails at the same key.
- [ ] A resolved numerical difference fails its stale expectation in every mode.
- [ ] Documentary T1 entries disclose an unrun team comparison without pretending
  to witness numeric equality or a computed mismatch.
- [ ] Pending differences still fail strict mode; unknown disposition tokens fail.
- [ ] No expectation count is parsed from descriptive prose.
- [ ] Existing fixtures and seed pairs pass strict verification on both stores.

## Verification

Use property-based perturbations of identity, grain, field and value around small
valid difference sets. Invariant: only the declared difference set satisfies the
contract; adding, removing, relocating or altering one difference fails. Test
exclusion and unsupported-comparison validation separately from numeric matching.

## Completion

Record any changed dispositions with evidence, reconcile deferred/debt records,
and move this story to `completed/`. GS 04 consumes this contract as coverage data.
