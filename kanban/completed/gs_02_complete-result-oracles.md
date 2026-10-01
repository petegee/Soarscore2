# Story — Extend complete result parity across the GliderScore corpus

**Status:** Completed · **Raised:** 2026-10-01 · **Completed:** 2026-10-01
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

- [x] Every active fixture declares snapshot scope and availability of each
  result field; all available fields are asserted automatically.
- [x] A wrong total preserving places fails. A wrong discarded identity preserving
  the total fails wherever that identity is externally evidenced.
- [x] Complete ranked populations and tie memberships agree bidirectionally.
- [x] Unknown discard identity is not presented as a proven empty discard set;
  equally-low candidates are handled according to the source's actual evidence.
- [x] Reconstructed expectations never acquire a report/persisted provenance label.
- [x] Both-store strict GliderScore runs and relevant fixture validation pass.

## Completion (2026-10-01)

Contract: `ExpectedResultFile` gains `preDropTotals`, `penalties` (explicit
zeros), `discards` (own drop dimension as unit — round/task — with round
identities + values), `scoredWindow`/`lifecycle`/`excludedRounds` (required
for every active fixture) and `fieldAvailability` (available/unavailable/
inapplicable + reason per field). All 11 oracles populated from external
evidence only (transcript `*` marks + Score/Penalty columns cross-checked
against persisted best-per-round sums; ladder.py-arithmetic recompute over
persisted cells; server-persisted progressive + curation recompute) — never
SoarScore output. No new product capability: actuals read the public
`/competition-result` finals plus the conservation collapse (same rows as
the internal check); expected identities/values always come from the
oracle. No rulebook interpretation was needed (historical GS drop config;
no fai-rules amendment); R1/T1 stand.

Grains: pre-drop, penalty and discards ride the ranking remainder under
their own names (gs_01 totals precedent); conservation unchanged as the
internal check. ReplayDriver's snapshot assertion now runs only when rounds
are actually excluded (f5k shape); full-window fixtures enter/compare
placeholder zeros as before. Negative cases live in
`CompleteResultOracleTests.cs`, real-data witnesses in the feature
scenarios' new "complete result oracle matches exactly" step. Full
acceptance green strict on SQLite (154/154) and replay green strict on
PostgreSQL (13/13); `validate.py` passes every fixture; index.md carries
the coverage table. Zero new divergences triaged — no product defect found.

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
