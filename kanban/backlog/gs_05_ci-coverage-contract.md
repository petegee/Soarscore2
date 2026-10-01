# Story — Enforce the GliderScore corpus contract in CI

**Status:** Backlog · **Raised:** 2026-10-01
**Sequence:** GS 05/13 · Milestone 2: systematic coverage and maintenance
**Dependencies:** `gs_04_executable-corpus-manifest.md`.

## What

Make both supported acceptance backends and the curated corpus coverage contract
required CI checks. Publish compact comparison evidence as a build artifact.

## Why it matters

`.github/workflows/build-and-test.yml` currently runs the default acceptance
backend. A locally green SQLite/PostgreSQL pair is useful evidence only if future
changes continue to exercise both. Coverage drift also needs a failing check.

## Before starting

- Inspect the workflow, store-selection fixture, strict-ledger mode, existing
  divergence report hooks and GS 04 regeneration/validation entry points.
- Requirements: repository testing policy requires both supported backends;
  NFR-1/2 seed integrity and `docs/users.md` trustworthy results are supported.
  No scoring rule, domain model or capture policy changes are needed.
- Preserve offline acquisition: CI validates committed curated JSON through a
  build/test-compatible validator. It must not invoke Python extraction/curation,
  require Jet/SQL Server source archives, or contact live GliderScore services.

## Implementation outline

1. Add explicit SQLite and PostgreSQL strict-mode GliderScore checks, avoiding
   unnecessary duplication of unrelated solution tests. Retain existing checks.
2. Validate the registry, fixture contracts and scenario coverage before replay.
3. Run the generated-summary drift check and identify how result-derived reports
   are produced after comparison, including failed comparisons.
4. Publish per-backend test results and a compact report with snapshot identity,
   compared fields/cells, unrun scopes, accepted differences and failures.
5. Make aggregate success depend on both backends and all corpus-contract checks.

## Acceptance criteria

- [ ] Both backend checks are visible and required by the workflow's success path.
- [ ] `SOARSCORE_GS_LEDGER_MODE=strict` is explicit for the checks.
- [ ] Missing scenario coverage, invalid expectations or generated-summary drift
  fails CI with actionable diagnostics.
- [ ] Reports upload even on test failure and distinguish assertions that ran
  from skipped/unsupported comparisons; incomplete runs do not report full coverage.
- [ ] The workflow needs only committed fixtures and its normal disposable stores.

## Verification and completion

Exercise the workflow commands locally on both stores, validate workflow syntax,
and verify a controlled contract failure returns nonzero and still produces its
diagnostic artifact. Use existing comparator negative checks rather than adding
tests that mirror YAML. Reconcile CI instructions and move to `completed/`.
