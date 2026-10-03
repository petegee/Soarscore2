# Story — Enforce the GliderScore corpus contract in CI

**Status:** Completed (2026-10-03) · **Raised:** 2026-10-01
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

- [x] Both backend checks are visible and required by the workflow's success path.
- [x] `SOARSCORE_GS_LEDGER_MODE=strict` is explicit for the checks.
- [x] Missing scenario coverage, invalid expectations or generated-summary drift
  fails CI with actionable diagnostics.
- [x] Reports upload even on test failure and distinguish assertions that ran
  from skipped/unsupported comparisons; incomplete runs do not report full coverage.
- [x] The workflow needs only committed fixtures and its normal disposable stores.

## Verification and completion

Exercise the workflow commands locally on both stores, validate workflow syntax,
and verify a controlled contract failure returns nonzero and still produces its
diagnostic artifact. Use existing comparator negative checks rather than adding
tests that mirror YAML. Reconcile CI instructions and move to `completed/`.

## Completion (2026-10-03)

Workflow (`.github/workflows/build-and-test.yml`, existing `build-and-test`
job untouched): new `gs-corpus-contract` job (offline — `validate.py
--index`, `corpus.py regen --check`, then the store-free contract unit tests:
`CorpusCoverageTests` + `ExactDivergenceContractTests` +
`CompleteResultOracleTests`), then `gs-sqlite` / `gs-postgres` jobs needing
it, each running only `Category=gliderscore` with explicit
`SOARSCORE_TEST_STORE` + `SOARSCORE_GS_LEDGER_MODE=strict`, TRX per backend,
and `upload-artifact` under `if: always()`. `deploy` needs
`[build-and-test, gs-sqlite, gs-postgres]` (contract rides transitively), so
aggregate success requires both backends and every corpus check. Serialising
replay behind the contract is deliberate: a known-bad corpus never pays for
replay (outline step 2 "before replay", literally).

Report (`CorpusDivergenceReport.cs`, test support only — no `src/`
scoring/domain/capture changes): header pins run identity (store + ledger
mode) and corpus snapshot (commit or working tree + registry schema v1 with
named active/skipped sets; compared-cell totals referenced from the generated
`index.md` block, never copied); tail adds "no ledgered divergences" and
"never compared" sections so compared fixtures are distinguished from skipped
ones and unsupported grains stay visible as documentary entries. The ledger-
visibility caveat states a red run claims no coverage from the file — the
TRX carries ran/passed/failed/skipped.

Docs reconciled: `extract/README.md` (CI now invokes the two gates over
committed JSON only; GS 05 past tense), `README.md` (CI paragraph),
`CLAUDE.md` (one sentence).

Proof, all local, workflow commands verbatim: contract job 61/61 unit tests;
`validate.py --index` corpus PASS (11 active, 1 skipped), `--self-test`
29/29; `corpus.py regen --check` fresh, `--self-test` 16/16; sqlite strict
18/18 (50 s), postgres strict 18/18 via Testcontainers (1 m 44 s), zero
skips each — the GS 04 gate values hold on both stores. Controlled failures:
scratch-copy index drift → exit 1 naming fixture + field (`status`);
generated-block perturbation → exit 1 with unified diff naming the file;
`SOARSCORE_TEST_STORE=bogus` GS run → exit 1 (`Unknown
SOARSCORE_TEST_STORE`) with `gs-divergences.md` + TRX still written (the
`if: always()` path). Workflow YAML parsed and structure-asserted with
PyYAML. No new tests: the comparator negative checks above are the failure
evidence.

Leftovers: none functional. `TestResults/` (gitignored) holds the local
`gs-sqlite`/`gs-postgres` TRX evidence; `/tmp/opencode/gs05-proof/` holds the
scratch failure logs. Nothing committed (per instruction).
