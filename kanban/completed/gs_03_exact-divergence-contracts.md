# Story — Make accepted GliderScore differences precise and executable

**Status:** Completed · **Raised:** 2026-10-01 · **Completed:** 2026-10-01
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

- [x] A mismatch moved to another cell fails even with the same total count.
- [x] An unexpected value/delta or contributor-set change fails at the same key.
- [x] A resolved numerical difference fails its stale expectation in every mode.
- [x] Documentary T1 entries disclose an unrun team comparison without pretending
  to witness numeric equality or a computed mismatch.
- [x] Pending differences still fail strict mode; unknown disposition tokens fail.
- [x] No expectation count is parsed from descriptive prose.
- [x] Existing fixtures and seed pairs pass strict verification on both stores.

## Verification

Use property-based perturbations of identity, grain, field and value around small
valid difference sets. Invariant: only the declared difference set satisfies the
contract; adding, removing, relocating or altering one difference fails. Test
exclusion and unsupported-comparison validation separately from numeric matching.

## Completion (2026-10-01)

Contract: `DivergenceEntry` gains `kind` (`numeric` | `excludedOracleCell`
| `syntheticSlot` | `unsupportedComparison`), exact-decimal `ours`/`expected`
pins and an `evidence` reference; unknown kind/disposition tokens and
pin-less numeric entries throw in `BuildReport` in every mode.
`CoversMismatch` is value-exact (identity AND both pins); documentary kinds
never subtract — excluded cells witness the coverage-gap shape, synthetic
slots the orphan-slot shape (`Comparator.LedgerWitnesses`), and
`unsupportedComparison` (the T1 shape) witnesses nothing by design and is
validated by `CheckLedgerScope` against the fixture's declared team method
(`UseTeams` + non-3 `NbrForTeamScore`) and `teamsCompared == 0`. The
witnessing arm now covers every observed-difference entry whatever its
disposition: a resolved permanent numerical difference fails stale in every
mode. `ParallelRunDifferenceEntry` gains `cells` (stable identity with
pinned GS/seed-run values, null sides where the comparator emits null);
`MatchDifferenceSets` is bidirectional over the ledger's whole declared set,
`ValidateCells` fails cell-less entries, out-of-scope cells and unknown
dispositions loudly, and `MissingDifferences` is cell-level so a partially
discharged entry still fails. No count is parsed from prose anywhere
(`ParallelRunSteps` now counts structured declared cells); `LedgerGate` and
the corpus report render kind/pins/evidence/cell tables from structured
fields. The recording referee enforces kind/pins/value-exact excuse beside
its T1 checks.

Migration (measured, prior evidence kept; entry counts and all `permanent`
dispositions unchanged): parity — 2 f3j-international exclusions, 8
f3k-june-2020 exclusions + 3 R1 numerics pinned from the oracle's
`valuesAsPersisted` artefacts (281.7/281.70000000000005,
682.6/682.5999999999999, 385.2/385.20000000000005), 14 southern-fling
synthetic slots, 2 T1 unsupported; parallel — ales 3 single cells (values
match prose, no re-triage), christchurch 127 + 7×3 lines, f3j 11 + 22 (union
32, P44 shared) + 200 + 32 (union 232) + 6×3 lines. One re-triage: the f3j
ledger's "no =n membership lines" claim was wrong — swaps fire membership
lines (measured 18 ranking lines, 3 per pilot); wildcard cover had absorbed
them. Prose pin/count sentences replaced with declared-set statements;
citations, reasons and evidence references preserved. `deferred-decisions.md`
R1/T1 untouched; no disposition changed, so no disposition record to add.

Verification: `ExactDivergenceContractTests` (36 tests — add/remove/
relocate/alter perturbations per criterion, exclusion vs numeric separation,
unsupported applicability, unknown-token failures). Full `@gliderscore`
strict green on SQLite (18/18) and PostgreSQL (18/18); ledgered green on
SQLite (18/18); `validate.py` passes every fixture; unit neighbours
(Snapshot/CompleteResult/Contract: 72) green.

## Boundaries and follow-up

GS 04 consumes this contract as coverage data. `parallel-run-mapping.md`'s
f3j line still carries pre-landing-zero-retriage counts (54/264) — a
documentation staleness predating this story, left for the mapping's owner.
The temporary `SOARSCORE_DUMP_COMPUTED` measurement hook was reverted before
landing (no trace in the diff).
