# Story — GliderScore golden-fixture pipeline

**Status:** Backlog · **Raised:** 2026-08-25

## What

Turn real GliderScore competitions into committed, provenance-noted JSON fixtures that
the replay/compare harness consumes. One fixture per competition:

pilots · comp config · class config (family row + landing/bonus tables) · entries ·
realised draw (rounds → groups → fly order) · raw `Scores` rows · GS's persisted
computed values (`RawScore`, `NormalisedScore`) as the expected oracle.

Work items:

1. Fix the extraction workflow. Jet reading on Linux/.NET is not worth runtime
   dependency — extraction runs once per fixture, offline. The ad-hoc pure-Python Jet
   reader used for `gliderscore-db-analysis.md` was not preserved; its outputs
   (`/tmp/opencode/gs_schema.json`, `gs_data.json`) survive today but `/tmp` is
   ephemeral. Recreate the extractor as a small tool kept with the fixtures.
2. Define + document the JSON fixture schema in this file.
3. Commit the first fixture: the analysed ALES sample comp (v6.78 export,
   10 pilots, 3 rounds).
4. Add install example comps (Pete has these) across classes; skip-list any using
   out-of-scope concepts (team scoring, series, merged/prelim — §6 concept gaps).

## Export format — resolved from source 2026-08-25

GliderScore's *Export Competition(s)* (`CompactDB(CompactType=1, CompList)` in
`GlobalFunctions_MOD.vb`, row-copying in `CompactDBProgress.vb`) creates a fresh `.mdb`
with the full table structure and copies:

- **comp-scoped tables filtered to the selected comp(s)** — `Comps`, `CompPilots`,
  `Scores`, `Dur`/`Spd`/`Dis`/`F3K`/`F5B`/`F5K`, per-round schedule tables;
- **all master data unfiltered** — every pilot ever registered (`Pilots`), all landing
  tables (`LndgNames`/`LndgPoints`), all models/devices/countries, plus presentation
  settings.

So an export is *not* the whole master DB — good — but `Pilots` must be filtered down
to `CompPilots` members when building a fixture; it is never the entry list.

## Why it matters

Static fixtures decouple the test framework from Jet parsing entirely and give every
future comparison a stable, reviewable golden record. Corpus diversity (classes,
multi-group rounds, re-flights, drop thresholds) is what makes the gap-hunt valuable.

## Before starting

Depends on `resolve-gliderscore-scoring-arithmetic.md` for which fields the fixture
must carry (decimal/round settings etc.). Decide fixture location under `tests/`.
Fixtures are test data, not `/docs`; each records source file, GS version, and who
exported it.
