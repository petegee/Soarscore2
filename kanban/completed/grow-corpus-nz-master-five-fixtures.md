# Story — Grow golden corpus from NZContests.mdb (five fixtures)

**Status:** Completed · **Raised:** 2026-08-27 · **Completed:** 2026-08-27 · Evidence base:
`/tmp/opencode/nz-extract/` (extraction + inventory + 13 per-comp candidate
reports; see `candidate-analysis.md` for the synthesis). The analysis was done
against a full master-DB backup supplied by the NZ competition manager
(`gliderscore/NZContests.mdb`, 168 competitions). **The evidence directory is
in /tmp — this story is only implementable while it still exists; copy any
still-needed material into the repo at curation time** (per-fixture: source
`.mdb` under `tests/GliderscoreFixtures/sources/`, JSON under the fixture's own
`extract/`).

## What

Curate five new fixtures for the GliderScore replay/compare harness, chosen by
gap analysis over the 168-comp NZ master DB. Schema, validation rules and index
contract per `kanban/completed/gliderscore-golden-fixture-pipeline.md` (WI-2);
batch selection rationale in the candidate-analysis synthesis. All five pass
triage cleanly: teams are app-default-only (`Team='0'` everywhere, no team
columns in Scores), series links dead or absent, no prelim/merge links.

| # | CompNo | Name | Slug | Fills |
|---|---|---|---|---|
| 1 | 45 | 2019 F5J Christchurch | `f5j-christchurch-2019` | **G2** first active F5J family fixture + **G4 float32 persist-cast witness** (99/162 NormalisedScore values carry binary32 residue) |
| 2 | 135 | F5J Hawkes Bay and Team Trials | `f5j-hawkes-bay-trials` | **G2** + four re-flights, de-singularising the corpus's single-witness re-flight gap (jerilderie-2010) |
| 3 | 17 | Southern Fling | `f3k-southern-fling` | multi-group F3K normalisation (per-group ×1000 verified) + task letters E/I/J/K first corpus sightings + mid-comp retirement |
| 4 | 121 | NZ South Island F5J | `f5j-nz-south-island` | **G2**: extreme height penalties (19 rows >200 m incl. 1000 m → raw −2026 → norm 0 clamp), motor-restart flag paired with `F5JMotorRestartOption=1` |
| 5 | 54 | 2020 June F3K | `f3k-june-2020` | mid-comp group re-draw witness (cancelled zeros re-flown as new group 4, ReFlightNo untouched); ragged 4-group round |

## Pre-requisites (sub-agent dispatchable — gate WI-1–4)

Sequencing note carried forward from the candidate-selection pass: the story
works best if the five per-comp GliderScore exports are produced *before* an
agent picks the story up. Each item below is independently dispatchable (one
export, one comp); none touches the repo — they stage material only.

**Shared spec (applies to every PRE-n):** on the working GliderScore install
that holds the master, run *Export Competition(s)* for exactly one comp — the
same route the existing fixtures were sourced from. Immediately verify the
result against the pinned extractor (`access_parser 0.0.6` via
`tests/GliderscoreFixtures/extract/extract.py`): it must (a) parse without the
known null-bitmap IndexError and (b) retain the Comps memo/text columns (no
silent drop). Stage the verified `.mdb` as `<slug>.mdb`, record sha256 +
GliderScore build/version, report the two verification outcomes.

- Done when (each): verified `.mdb` staged, sha256/build noted, extractor
  checks clean.
- On failure: if the export carries the newer wider-schema that trips the bug,
  or the owner cannot produce it, mark the item blocked and switch to the
  fallback — the tolerant-extractor extension of `extract.py` (WI-1 sourcing
  option 2) — which must itself be complete before WI-1 begins.
- With all five landed: WI-1 collapses to plain per-export extraction — its
  split/cut-to-single-comp step and the degraded-column expectations fall away.

### PRE-1 — Per-comp export: comp 45, 2019 F5J Christchurch (`f5j-christchurch-2019`)

Shared-spec export + verification. **Batch sentinel:** complete this one's full
verification *before* the remaining four are requested — it proves whether the
install's GS build emits exports the pinned parser tolerates.

### PRE-2 — Per-comp export: comp 135, F5J Hawkes Bay and Team Trials (`f5j-hawkes-bay-trials`)

Shared-spec export + verification.

### PRE-3 — Per-comp export: comp 17, Southern Fling (`f3k-southern-fling`)

Shared-spec export + verification.

### PRE-4 — Per-comp export: comp 121, NZ South Island F5J (`f5j-nz-south-island`)

Shared-spec export + verification.

### PRE-5 — Per-comp export: comp 54, 2020 June F3K (`f3k-june-2020`)

Shared-spec export + verification.

> **As staged 2026-08-27 (PRE-1–5 dispatched to five agents, sentinel first):**
> the GliderScore GUI export route is unexecutable on this host (no install, no
> wine) and the pristine pinned access_parser 0.0.6 crashes on `Comps`
> (IndexError at `_parse_fixed_length_data` ~line 320 — col id 40 `IsPublic`
> raises; 41/42 degrade) while silently dropping its 12 variable-length Text
> columns. Every other table parses clean. **Verdict per item: fallback
> invoked** (sourcing option 2). All five comps staged + verified under
> `/tmp/opencode/nz-pre/<slug>/` (`VERIFICATION.md`, `stage-manifest.json`,
> `tables/*.json` in extract.py shape), cross-checked cell-identical against
> the analysis-grade full-master JSONs where pristine parse applies. Findings
> to carry into WI-1/WI-2: F5J landing points are ADDITIVE (+ scheme-11);
> comp 121 has NO pilot-86 re-flight — real witnesses are the R7G1 seq-gap
> {1,2,4,5} and the −2026 → norm-0 clamp row; comp 54's re-draw round is
> round 7 with ReFlightNo=0 and mixed cancellation flags; comp 17's R9
> phantom Landing=145 hits 12/14 rows (6 of them also FlightScoreDeduction=200)
> and its task catalogue contains A(2) not A(1); G4 residue appears only under
> a binary32 cast-simulation (stored values are clean exact-1dp doubles) so
> assert the cast-witness property, never literal repr bits.

## Work items

### WI-1 — Extract all five via the repo pipeline

For each comp: run `tests/GliderscoreFixtures/extract/extract.py` against a
committed source export, then split/cut the multi-comp master extraction to the
single comp.

> **Extraction-blocker note (resolved during planning):** the pinned extractor
> (access_parser 0.0.6) crashes on NZContests.mdb — an upstream off-by-one in
> `AccessTable._parse_fixed_length_data` (`>` should be `>=`) raises IndexError,
> and the library also silently drops Comps memo/text columns on this DB (the
> analysis recovered them via a custom Jet4 trailer crack;
> `/tmp/opencode/nz-extract/comps-var-columns.json`).
>
> **Sourcing options, in preference order:**
> 1. **Fresh per-comp exports by the DB owner.** The master lives in a working
>    GliderScore install; *Export Competition(s)* produces a clean per-comp
>    `.mdb` (comp-scoped tables only — exactly what existing fixtures were
>    sourced from). Ask for five exports; commit those as `sources/`; extract
>    with the pinned tool unchanged. RISK: exports from the same newer GS build
>    may carry the same wider schema that trips the null-bitmap bug — verify
>    the first one before requesting the rest.
> 2. **Tolerant extraction from the master directly:** extend `extract.py`
>    with an opt-in flag carrying the one-line bounds fix + degraded-column
>    warnings, then slice tables per comp at curation. Committing NZContests.mdb
>    itself as the source is NOT acceptable (all 168 comps + full Pilots
>    master ⇒ PII-broad, and couples five fixtures to one file); provenance
>    must then declare the deviation explicitly (`sourceKind:
>    "master-db-slice"`, sha256 of master + of sliced JSONs).

- Done when: five fixture directories exist with `extract/` tables cut to their
  comp (`Scores`, `CompPilots`, `Comps`, referenced family/schedule/lookup rows),
  plus `DBParams`; each records degraded-column notes where the tolerant reader
  fired (expected: `Comps.IsPublic/UseRegistration/UseRegistrationIdx`;
  recovered text columns from `comps-var-columns.json` cross-checked per-row).

### WI-2 — Curate per schema v1

Per fixture: `provenance.json` / `competition.json` / `entries.json` /
`scores-raw.json` / `expected-scores.json` / `expected-result.json` per WI-2 of
gliderscore-golden-fixture-pipeline.md. Class-specific guidance discovered by
analysis:

- **F5J scoring policy (comps 45/135/121)** — record verbatim in provenance
  notes: RawScore = min(packed-mmss time, 600 s) − height penalty
  (0.5/m ≤200 m, then 100+3.0/m above; launch height stored in the Scores
  column named `FlightScoreDeduction`) + scheme-11 "F5J Enter Landing" lookup.
  Verified exact on 164/164 (#45), 129/129 (#121), 174/174 sibling #37 (not in
  batch but useful reference), 82/83 (#40, not in batch).
- **Normalisation:** duration classes best-in-group→1000, round 1 dp; verified
  with zero residual on all five comps' scored rows.
- **f5j-christchurch-2019**: mid-comp snapshot (rounds 1–11 of unknown total,
  ragged partial groups) — curate exactly what exists; no backfilling. This
  fixture carries the G4 residue: assert on persisted values, never recompute.
- **#45/#98 connection**: comp 98 (not curated) is the secondary G4 witness
  (51 values); cite its report path in c45's provenance notes in case of dispute.
- **f3k-southern-fling**: phantom `Landing=145.0` on every Round-9 row with no
  landing scheme attached — redaction-needed noise; document in provenance,
  keep rows verbatim otherwise. Pilot 89 `Retired=True` after R8 — new
  placeholder-class semantics to carry.
- **f5j-nz-south-island**: pilot 86's re-flight vacates his R7 seat entirely
  (group shape 4/6/4, seq gaps {1,2,4,5}) — original draw unrecoverable; note
  it, don't invent it.
- **f3k-june-2020**: 5 open decode deviants in round 4 (task H slot-sums short
  by 2–30 s) — persist raw untouched; flag deviants in provenance rather than
  resolving them now.
- Validation rules 1–5 + expected-scores↔scores-raw integrity per pipeline
  story; every non-zero Landing vs its LndgPoints scheme (zero off-scheme
  values across all five).

### WI-3 — Ranking oracles

Per ranking-oracle decision (hybrid): prefer transcribing real GS reports where
obtainable — none of these have transcripts yet. Default all five to
`reconstructed-ladder`, computed independently (Python script beside the
fixture, committed as its derivation-of-record), with these anchor checks:
- #45: normalised ladder R1–R11 partial; ties resolved by… (decide at curation)
- #135: all four re-flight cells aggregate to orig-round cells; overall ladder
  after drops; drop config unset DB-wide ⇒ NO drops fire (record explicitly).
- #17/#54: F3K aggregation rules incl. retired-pilot handling (#17) and
  cancelled-zero re-draw group (#54).
- #121: negative-floor clamp rows (raw −2026 → 0.0) in final ladder.
Mismatch policy per pipeline story: needs source-level triage before indicting
the engine.

### WI-4 — Index, validate, harness-green

Add five lines to `tests/GliderscoreFixtures/index.md` (dash-bullet contract),
all `active`. Update the "Diversity wanted" section: move to witnessed — G2
(F5J), re-flight ×4, F3K multi-group/per-group-normalisation, mid-comp
re-draw, motor-restart-effect pairing, float32 residue (G4). Still-open shrinks
to: G1 (Speed/Distance in ACTIVE fixture), G3 (factory-default drop
thresholds — confirmed unwitnessable from this master DB: drop config columns
unset on all 168 Comps rows), G6 (>1 timekeeper — absent DB-wide), F5L/F5B
families, merged/prelim mechanics, D6 divergence.
Done when `validate.py --index` passes all five and the acceptance harness runs
the corpus green (this story does not need new comparison logic — the existing
harness consumes them unchanged, modulo any F5J/F3K-specific oracle gaps the
harness surfaces, which are out of scope here and go to tech-debt/backlog).

## Before starting

- Take into `in-progress/` per board rule 3 — but only after PRE-1–5 have
  landed (or their fallback, the tolerant-extractor WI, has). Then WI-1 gates
  WI-2–4.
- Re-verify claims against `/tmp/opencode/nz-extract/` while it lasts; per-comp
  detail lives in `candidates/<slug>.md` (13 reports + 3 verification reports).
- Corpus additions follow pipeline-story validation rules 1–5; PII: contacts
  empty DB-wide, names-only exposure as with existing fixtures; slice, don't
  ship, the master.
- Housekeeping: nothing in /docs; new knowables discovered during work go to
  tech-debt.md or backlog stubs, not scope growth here.
- Related open stories worth skimming before scoring-oracle decisions:
  `pre-normalisation-score-view-field.md`,
  `ranking-secondary-rawscore-key.md`,
  `reflight-aggregate-destination.md`.

## As built 2026-08-27

Dispatched to sub-agents: PRE sentinel first, then PRE-2–5 in parallel; the
fallback gate as one agent; WI-1, WI-2, WI-3 each as one agent per competition
(five parallel dispatches per work item); WI-4 as one agent.

- **PRE-1–5 (export route):** unexecutable on the host — no GliderScore install,
  no wine — and independently blocked: pristine pinned access_parser 0.0.6
  raises IndexError on `Comps` (null-bitmap off-by-one, col id 40 `IsPublic`;
  ids 41/42 degrade) and silently drops its 12 variable-length Text columns;
  every other user table parses clean. Verdict per the story's own failure
  branch: fallback invoked, not pursued further.
- **Fallback gate:** repo `extract.py` gained opt-in `--tolerant` (surgical
  bounds workaround + loud degradation warnings) and `--recovered-texts`
  (ingests `nz-master/comps-var-columns.json`, byte-validated upstream, hard
  refuse on mismatch/trust failure), writing full 40-column `Comps` +
  `comps-field-provenance.json`. Differential gate clean against all five PRE
  staging sets; default mode re-proved byte-identical on the old sample export;
  validate self-test extended 17/17.
- **WI-1:** five `extract/` cuts committed, zero hard mismatches vs PRE staging;
  only documented null-convention cells differ (pristine-wins).
- **WI-2:** five fixtures curated to schema v1; validator extended with
  declared-oracle-deferral (`expectedResultDeferred`) for the staged WI-3 flow
  and faithful-null rule-3 notes (Decs/RoundOrTruncate are Jet-null DB-wide on
  this master); corpus regression green throughout. competition.json carries a
  `configProvenance` convention on two fixtures (45/135) — recorded as tech debt
  to unify if ever consumed.
- **WI-3:** all five oracles are `reconstructed-ladder` via per-fixture
  `ladder.py` derivations-of-record; recomputed cells matched curated expected
  values exactly everywhere — no divergences.json needed anywhere. Notable
  in-ladder decisions: comp 17's retired pilot ranked on flown rounds; comp 54's
  re-draw resolved by keep-highest-per-original-round dedup alone (cancellation
  flags are flag-mixed, so blind dedup is the faithful semantics).
- **WI-4:** index.md grew five active bullets; Diversity witnessed-list gained
  F5J ×3, re-flights ×4 cells, first F3K multi-group per-group-normalisation,
  mid-comp group re-draw, motor-restart-effect pairing, G4 cast-residue
  (phrased as comparator property over clean stores); Still-open now names D6/G3
  as confirmed unwitnessable from this source. `validate.py --index`: 10/10 PASS;
  acceptance harness green 52/52 on sqlite (and postgres incidentally). Surfaced
  carve-out: harness replay scenarios for the five NZ fixtures are a new backlog
  stub (`nz-fixture-replay-scenarios.md`), not scope growth here.
- **Plan corrections landed during verification** (prose vs data):
  - Comp 121 has NO pilot-86 re-flight — real witnesses are R7/G1 SeqNos
    {1,2,4,5} (shapes 4/5/4) and the −2026→norm-0 clamp row; original draw left
    unreconstructed as instructed.
  - Comp 54's re-draw round is round 7 (round 4 carries the H decode deviants);
    zero breakdown corrected to {P81×6, P84×6, P101×1, P102×1, P128×2}.
  - Comp 17's task catalogue holds A(2), not A(1); phantom-Landing census is
    12/14 R9 rows (six of them also FlightScoreDeduction=200).
  - F5J landing points confirmed ADDITIVE (+ scheme-11) by exact recompute on
    all three F5J comps (164/164-equivalent counts; 147/147; 133/133).
  - G4 discipline phrased precisely: stored values are clean exact-1dp doubles;
    residue exists only under emulated binary32 persist casts (99/162 on #45)
    — assert cast behaviour, never raw repr bits.
- **No sources/*.mdb committed** — per sourcing option 2, provenance declares
  `sourceKind: "master-db-slice"` with master sha256, recovery-evidence sha256
  and degraded-column notes instead.
