# Story — Establish an executable GliderScore corpus manifest

**Status:** Completed (2026-10-02) · **Raised:** 2026-10-01
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

- [x] An active fixture without its required executable scenario fails validation.
  (`validate.py --index` scenario-coverage gate + `--self-test` missing-scenario
  case; `CorpusCoverageTests.Every_active_fixture_has_its_replay_scenario` and
  `Every_registry_parallel_run_mode_has_its_parallel_run_scenario` pass 4/4.)
- [x] Duplicate IDs, missing/orphaned references and incompatible snapshot or
  reconstruction declarations fail with the fixture and field identified.
  (`--self-test` 29/29: incomplete entry, duplicate slug, orphaned seed,
  orphaned replay declaration, contradicting snapshot scope, missing scenario,
  index drift, missing directory — each naming slug and field.)
- [x] Generated summaries distinguish competition count, archive rows, compared
  cells, exclusions, seed witnesses, and external versus reconstructed oracles.
  (`corpus.py regen --check` fresh; `--self-test` 16/16.)
- [x] Existing numerical expectations have one authoritative location each.
  (The generator copies counts and shapes only — expected VALUES stay in
  `expected-*.json`, class arithmetic in `class-definition.json`; verified by
  reading `corpus.py measure_fixture`/`measure_pair_ledger` — no value fields.)
- [x] GS-specific per-fixture declarations are data, with no new class-specific
  branches in production code. (No `src/` changes; registry keyed by slug —
  `grep switch` over the new/changed harness files finds only the doc comment
  "never switched"; `ReplayDriver` delegates to `CorpusReplayRegistry`.)
- [x] All existing strict comparisons retain their results on both stores.
  (Strict `@gliderscore` runs 2026-10-02: sqlite 18/18, postgres 18/18 —
  zero failures, zero skips on each.)
- [x] There is a deterministic regeneration/check command for summaries.
  (`corpus.py regen` / `regen --check`; byte-determinism proven by `--self-test`.)

## Verification

Validate deliberately incomplete/contradictory registry examples and run the
existing corpus as migration regression coverage. Invariant: active declared
coverage equals executable coverage, excluding explicitly labelled supplemental
scenarios from the unique-competition count. Regeneration must be deterministic.

## Completion

Step 5 (2026-10-02) — atomic migration, behaviour proven before any fixture's
coverage changed. No step changed what any fixture compares: Steps 2–4 moved
declarations and summaries; this step records the mapping and proves the
results.

### Old-to-new field record

- `FixtureLoader.ActiveSlugs`: whole-file `index.md` bullets ("skipped"
  anywhere) → the registry's `active` slugs in registry order, cross-checked
  against the `## Competitions` bullets (a slug on either side only, or a
  status mismatch, throws naming the slug). Registry absent → index alone
  (pre-registry checkouts stay green).
- `ReplayDriver`'s six per-fixture dictionaries → `corpus-registry.json`
  `replay` declarations, each carrying a `basisRef` to its derivation;
  `CorpusReplayRegistry` is the single reader and generic mechanisms
  (parallel-run binding derivation, slot maps, capture maps) stayed in code.
- `index.md` oracle table → generated block: `source` → oracle `source` +
  external/reconstructed class; `window` → `scoredWindow` + `lifecycle`
  (+ "of drawn …" / `excludedRounds` in exclusions); `drops`/`penalties` →
  measured `discards[]`/`penalties[]` counts. New measured columns with no
  hand predecessor: archive rows, compared cells, exclusions, seed witnesses.
  Full column mapping lives beside the corpus (`index.md` "Column migration").
- Standings scope: accepted gap, stated openly. The parity standings reach
  ("standings R1–R11/R1–R10" on the three F5J bullets) has no machine-readable
  field — scoredWindow pins the COMPARED window (full drawn scope incl.
  placeholders); only the parallel-run scored-window assertion (christchurch
  R1–R11) is machine-readable and it governs the parallel-run rollup, never
  parity scope. Recorded in `index.md`, not silently dropped.
- Pairs f3j hand row reconciled against the landed ledger: the old counts
  (174 normalised / 264 / 20-pilot split) were pre-gs_03-retriage; the ledger
  measures raw union 32 (11 deduction + 22 decay), normalised union 232
  (200 grid + 32 cascade), 6 pilots / 18 ranking lines — the generated view
  aggregates 10 entries / 283 structured witness cells. Hand row rewritten to
  the ledger values with a pointer to the generated view (rewrite-or-cite, no
  silent duplicate). Other done-rows verified clean: ales 3/3, christchurch
  127 + 21 = 148.

### Proof (existing behaviour, all active modes)

- `validate.py --index ../index.md`: PASS (registry schema v1; 11 active,
  1 skipped; index, snapshot scopes, references and scenarios agree).
  `--self-test`: 29/29.
- `corpus.py regen --check`: fresh (also re-verified after the Step 5 hand-
  prose edits — edits sit outside the `corpus-generated` markers).
  `--self-test`: 16/16.
- `CorpusCoverageTests`: 4/4 (registry ↔ loader ↔ both feature files).
- Full strict `@gliderscore` runs 2026-10-02: sqlite 18/18 green
  (`Passed: 18, Skipped: 0`, 2 m 9 s); postgres 18/18 green
  (`Passed: 18, Skipped: 0`, 1 m 33 s via Testcontainers) — all existing
  strict comparisons retain their results on both stores, zero failures
  and zero skips on each.
- `CorpusReplayRegistry` fallbacks: KEPT, deliberately. The `Fallback*`
  tables are the standing verbatim-migration tripwire —
  `VerifyAgainstFallbacks` compares them element-for-element against the
  registry on every load and throws naming slug and field — and the
  absent-file path keeps pre-registry checkouts green. Removal (load-or-throw)
  would delete the proof mechanism for zero runtime benefit.

### Debt reconciliation

- Fixture-binding debt: the code-location half is DISCHARGED — all six
  pre-registry dictionaries are now registry data with `basisRef`s, verified
  on every load. Untouched and still open: the loader-visibility half
  (`kanban/tech-debt.md`: varying per-round F5K NLH needs `F5KDataByRound`
  decoding — curation/loader work GS 04 never claimed).
- Effective-knob debt: discharged NONE, openly — registry `sourceRef`s cite
  provenance but unify nothing and `corpus.py` reads no knob fields; the
  non-uniform `configProvenance` item in `kanban/tech-debt.md` stays open.
- Curator entry points (beside the corpus): `extract/README.md`
  "Adding a fixture" — registry entry + index bullet + scenario, then
  `validate.py --index`, `corpus.py regen` (with `--check` kept green), and
  the `CorpusCoverageTests` gate.

GS 05 wires the contract into CI: `corpus.py regen --check` and
`validate.py --index` are the CI-ready gates (both exit 1 on drift with a
diff). Unblocked — all seven acceptance criteria hold.
