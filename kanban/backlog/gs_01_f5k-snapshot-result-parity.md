# Story — Make the F5K snapshot comparison complete

**Status:** Backlog · **Raised:** 2026-10-01
**Sequence:** GS 01/13 · Milestone 1: trustworthy comparisons
**Dependencies:** None. Establishes the result/snapshot pattern for GS 02.

## What

Replay `f5k-ni-round-2` at the snapshot represented by GliderScore's persisted
round-6 progressive standings, and compare totals, placings and the complete
ranked population. Keep the ten-round source record intact.

## Why it matters

The existing fixture documents a passing comparison with different totals:
GS discards a real score from the six scored rounds, while the parity replay
completes ten rounds and discards an unflown placeholder zero. Placings agree,
and the current ranking grain compares only places. Internal conservation
cannot establish agreement with GS's choice of discarded score.

## Before starting

- Read `tests/GliderscoreFixtures/f5k-ni-round-2/{competition,expected-result,
  provenance}.json`, its `index.md` entry, and the persisted progressive fields
  in its extraction. Confirm the scoring window and never-flown pilot's history.
- Inspect `Support/Gliderscore/{ReplayDriver,Comparator,FixtureModels}.cs` in
  `tests/Soarscore.Acceptance.Tests`, plus the public partial-result workflow.
- Decide and document whether the comparison reads a progressive result or a
  legitimately finalised snapshot. Do not invent completion of unflown rounds
  to satisfy the API. If the public workflow cannot express the snapshot,
  surface a product-capability gap before changing semantics.
- Requirements: `docs/users.md` Contest Director trustworthy results;
  NFR-4's absent-versus-zero and partial standings; NFR-1/2's class-data law.
  Historical GS drop configuration is the parity authority; the FAI F5K
  `5.5.10.16` policy belongs to the separate GS 07 seed run. No new domain concept
  or rulebook amendment is authorised by this harness story.

## Implementation outline

1. Add the smallest explicit snapshot/result-oracle contract needed for this
   fixture: source window, lifecycle interpretation, total and ranked population.
   Record provenance per expectation and retain original source values.
2. Represent the six-round scoring boundary through public commands/queries.
   Distinguish later prescribed slots from completed/scored results. Account for
   pilot 88 from the evidence rather than dropping registration to fit the oracle.
3. Extend comparison to final/progressive totals and bidirectional population
   equality, retaining exact cell and place comparison. Declare how archived
   R7–R10 cells are covered or excluded from this particular run.
4. Reconcile the first full comparison. Fix adapter defects at their source;
   triage genuine remaining differences with exact affected values and evidence.
5. Update fixture disclosures and the scenario together. Verify both stores.

## Acceptance criteria

- [ ] The five persisted round-6 totals are asserted exactly: P80 5000.000,
  P75 4928.100, P82 4796.100, P79 4359.900, P102 4319.200; places 1–5 respectively.
- [ ] An extra or missing ranked competitor fails, including an extra competitor
  at a place not present in the oracle. Zero-only population treatment is explicit.
- [ ] A placeholder-zero discard cannot pass merely because placings agree.
- [ ] Source rows and persisted expected scores remain intact; every excluded
  cell/window is disclosed, with no claim of comparison for an unrun cell.
- [ ] Existing GliderScore scenarios pass in strict mode on SQLite and PostgreSQL.

## Verification

Add meaningful negative comparator cases for a wrong total with unchanged places,
and extra/missing ranked identities. Pin the real F5K snapshot through acceptance
tests. Invariant: equality requires the same ranked identity set and equal values
for every declared external result field; internal conservation is an additional,
independent assertion. Use property-based cases if they improve coverage of
arbitrary identity sets rather than duplicating fixed examples.

## Completion

Reconcile the fixture index and deferred/debt inventories; move this story to
`completed/`. GS 02 generalises the landed contract, rather than creating another.
