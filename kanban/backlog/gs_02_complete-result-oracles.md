# Story — Extend complete result parity across the GliderScore corpus

**Status:** Backlog · **Raised:** 2026-10-01
**Sequence:** GS 02/13 · Milestone 1: trustworthy comparisons
**Dependencies:** `gs_01_f5k-snapshot-result-parity.md`.

## What

Extend the result-oracle contract established by GS 01 across every active
fixture, asserting all result fields supported by existing evidence and declaring
the rest unavailable. Give each comparison an explicit snapshot scope.

## Why it matters

Rank-only agreement leaves incorrect totals, penalties and discarded-round
choices undetected when they preserve order. Seven current ranking oracles are
reconstructions, three are GS report transcripts and one is server-persisted;
their evidential strength must stay visible as comparison becomes richer.

## Before starting

- Review all `expected-result.json`, transcript and provenance files under
  `tests/GliderscoreFixtures`, and the GS 01 contract.
- Read `Comparator.CheckConservation`, `CompareRankingGrain` and the literal-record
  result assertions. Reuse their public read paths without using SoarScore's own
  output or drop selection to construct an external expected value.
- Requirements: `docs/users.md` trustworthy published results; NFR-1/2 and NFR-4.
  Consult `fai-rules` for any rule-derived interpretation, keeping historical GS
  configuration separate from current seed policy. Existing R1/T1 decisions in
  `kanban/deferred-decisions.md` stand.

## Implementation outline

1. Inventory existing evidence per fixture and field. Design explicit available,
   unavailable and inapplicable states with reasons; omission must not mean zero.
2. Support total, pre-drop total, aggregate penalty deduction, discarded scoring
   units and values, places/tie membership and the complete ranked population.
   Preserve source distinctions such as a round versus a task discard.
3. Populate expectations from reports, persisted standings or a separately
   identified reconstruction. Preserve source hashes/locations and derivations.
4. Declare the included scoring window and row population for every fixture;
   distinguish drawn, unflown, completed and included-in-standings evidence.
5. Run all fixtures, triage newly exposed differences, and update scenarios and
   coverage disclosures. Keep conservation as an internal consistency check.

## Acceptance criteria

- [ ] Every active fixture declares snapshot scope and availability of each
  result field; all available fields are asserted automatically.
- [ ] A wrong total preserving places fails. A wrong discarded identity preserving
  the total fails wherever that identity is externally evidenced.
- [ ] Complete ranked populations and tie memberships agree bidirectionally.
- [ ] Unknown discard identity is not presented as a proven empty discard set;
  equally-low candidates are handled according to the source's actual evidence.
- [ ] Reconstructed expectations never acquire a report/persisted provenance label.
- [ ] Both-store strict GliderScore runs and relevant fixture validation pass.

## Verification

Exercise wrong-total, wrong-discard, wrong-penalty, extra/missing competitor and
unavailable-field cases. Invariant: changing an asserted field or identity must
break equality even when every other field and final order is unchanged. Keep
negative harness checks separate from the real-data acceptance witnesses.

## Boundaries and completion

Acquire new reports under `gs_06_independent-result-report-acquisition.md`; this
story uses what is already available. A discovered product defect is fixed or
explicitly tracked and triaged, never hidden by weakening an oracle. Reconcile
coverage/debt, then move the story to `completed/`.
