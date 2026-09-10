# GS ledger modes — strict/ledgered, per-entry disposition, corpus divergence report

**Status:** Completed (2026-09-10)

## What

The GS parity and parallel-run harnesses pass silently while ledgered
differences stand; subtraction is one-directional, so a green run says nothing
about where or how SoarScore diverges from GliderScore. This story parameterises
both harnesses into two modes over one env knob, `SOARSCORE_GS_LEDGER_MODE`:

- **`strict` (default)** — a fixture/pair carrying any ledger entry marked
  `pending` FAILS after the full comparison, with a one-glance explanation: a
  rendered table of every divergence (grain, round/group/pilot, citation
  token/kind, one-line reason). Red IS the visibility; CI cannot forget it.
- **`ledgered`** — exactly today's behaviour (subtract-and-pass / set-equality
  verdict), for local exploration.

Entries gain an optional `disposition` field — `pending` (default when absent)
or `permanent` — so by-design divergences (T1, R1, D5 structural, the
parallel-run kind-1 rulebook-vs-local-practice splits) are reported without
rotting CI red. Unknown disposition values fail loudly at load.

Hygiene in BOTH modes: a `pending` entry that no longer witnesses any computed
mismatch fails the scenario (a discharged divergence must be removed, not
silently tolerated). The parallel-run ledger already asserts witnessing in both
directions; the parity harness gets the same tightness arm.

A run-level corpus report (`TestResults/gs-divergences.md`, written after each
@gliderscore run) lists every ledgered difference across all fixtures and
parallel-run pairs — where and how, green or red.

## Why it matters

The ledger is the only record of divergence and it is invisible in passing
output; GOLDEN-COMPARISON-STATE.md is a hand-maintained snapshot that goes
stale. The owner wants: (1) CI hygiene — an actionable divergence is a red
test with its explanation, never a quiet pass; (2) a standing, generated
answer to "where and how does SoarScore diverge from GS".

Design decisions taken with the owner (2026-09-10): strict by default;
full-compare-then-fail (the red run still reports whether everything else is
exact); per-entry disposition so decided-law divergences don't fail CI; corpus
report yes.

## Before starting

- Cross-referenced: `kanban/deferred-decisions.md` "GliderScore replay
  harness" — the R1 and T1 rulings decide current entries are permanent
  (ledgered, never tolerated or emulated); disposition only changes CI
  failure semantics, not the ledgering discipline. No users.md/NFR conflicts
  (test-harness only).
- Current live triage (all `permanent`): parity — T1×2 (jerilderie-2010,
  f3k-sample-comp; decided 2026-09-02), R1×3 (f3k-june-2020; decided
  2026-08-28), D5×24 (f3j-international 2, f3k-june-2020 8, f3k-southern-fling
  14; structural). Parallel-run — all three pairs' entries are triage kind 1,
  each cited rulebook-vs-local-practice; permanent.
- No `pending` entries exist today, so strict mode is green at landing — by
  design: the loop it enforces is "new divergence → red with explanation →
  fix, or triage to `permanent` with citation".

## Plan

- **WI-1 — Mode + disposition plumbing.** `Support/Gliderscore/GsLedgerMode.cs`:
  enum + `FromEnvironment()` (`strict` default, unknown value throws —
  AcceptanceFixture's store-reader pattern). `DivergenceEntry` and
  `ParallelRunDifferenceEntry` gain `string? Disposition = null` (null-tolerant
  widening precedent: ParallelRunProvenance) with a `Permanent` accessor that
  throws on any token other than `pending`/`permanent`.
- **WI-2 — Parity tightness + strict material.** `Comparator.BuildReport`
  computes `UnwitnessedLedgerEntries` (pending entries covering no
  pre-subtraction mismatch; `permanent` entries are exempt — T1 is
  documentary by design, D5/R1 witness but are not required to).
  `ComparisonReport` gains it as a defaulted record parameter (all existing
  call sites compile).
- **WI-3 — Gates.** A shared gate (new `Support/Gliderscore/LedgerGate.cs`)
  rendered once, called from the When-tail of `ReplaySteps` (after the compare:
  strict → fail listing pending entries + remainder status; any mode → fail
  unwitnessed pending entries) and of `ParallelRunSteps` (strict → fail
  listing pending triaged entries; the verdict/set-equality steps are
  unchanged and still run first in green cases).
- **WI-4 — Corpus triage.** Add `disposition: "permanent"` to all 29 parity
  entries (5 `divergences.json`) and all 3 parallel-run ledgers' entries,
  citing the deferred-decisions rulings; no entry's grain/cell/pilot/reason
  changes. `parallel-run-mapping.md` ledger column notes the field.
- **WI-5 — Corpus report.** `[AfterTestRun]` hook writes
  `TestResults/gs-divergences.md` (repo root discovered by walking up to
  `Soarscore.sln`; best-effort write — report generation must never fail the
  run): per fixture and per pair, every ledger entry with disposition,
  grain/scope, reason/citation, plus totals and the active mode.
- **WI-6 — Self-checks + narratives.** HarnessSelfCheckSteps' synthetic
  ledger-strictness scenario also proves the witnessing arm (exact-cover entry
  → unwitnessed empty; different-pilot entry → entry reported unwitnessed).
  Both feature narratives gain the mode sentence.

Testing: example-based per WI-6's widened self-checks; the witnessing arm is a
set-equality over small fixed sets — no genuine invariant for CsCheck here.
Verify: `SOARSCORE_TEST_STORE=sqlite dotnet test` @gliderscore, both modes
(`strict` default green on today's corpus; `ledgered` byte-equivalent
behaviour); postgres leg per backend-claim discipline before completion.

## Decisions

- Disposition is a CI-semantics annotation, not a re-triage: grain, cell,
  pilot, reason and citation of every existing entry are untouched (WI-4).
- `permanent` entries are reported, never failed: they are decided laws
  (deferred-decisions.md R1/T1) or structural facts (GS rows a replay cannot
  produce), not actionable debt.
- The corpus report is generated output under `TestResults/` — never a docs
  file, never a status document (board rule 7).

## As built

- All six WIs landed as planned; 65 existing entries (29 parity, 36
  parallel-run) annotated `disposition: "permanent"`, byte-identical
  otherwise (verified against HEAD semantically, disposition excluded).
- Gates proven red by temporary mutation: a `pending` parity entry fails
  strict with the divergence table + "every other cell compared exact"
  remainder status; the same entry in `ledgered` mode trips the WI-2
  stale-ledger arm instead; a `pending` pair entry trips the parallel-run
  gate with its citation rendered.
- Deviations from plan: (1) the corpus report must skip index.md's
  non-fixture bullets ("§6 concept gaps" and friends) — `ActiveSlugs`
  tokenises every `- ` bullet, so the report walks only slugs with a fixture
  directory; (2) `parallel-run-mapping.md` was left untouched — it carries an
  uncommitted edit from f3j-international-parallel-run-retriage and its ledger
  column cites paths, not schema; (3) `TestResults/` added to `.gitignore`
  for the generated report.
- Verification: 18/18 @gliderscore on sqlite in strict (default) and in
  `ledgered` mode, 18/18 on postgres (Testcontainers), 92/92 full acceptance
  suite on sqlite, 7/7 architecture tests, clean solution build.
