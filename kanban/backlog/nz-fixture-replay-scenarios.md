# Story — Harness replay scenarios for the five NZ fixtures

**Status:** Backlog · **Raised:** 2026-08-27 · Parent:
`kanban/completed/grow-corpus-nz-master-five-fixtures.md` (WI-4 carve-out)

## What

Extend the GliderScore replay/compare harness so its scenarios actually replay
the five NZ fixtures (`f5j-christchurch-2019`, `f5j-hawkes-bay-trials`,
`f3k-southern-fling`, `f5j-nz-south-island`, `f3k-june-2020`) end-to-end against
their reconstructed-ladder oracles. WI-4 proved only that the harness consumes
them via the manifest path (`FixtureLoader.ActiveSlugs`); each needs a
per-fixture `class-definition.json` + scenario before replay/compare covers it.

## Why it matters

The five fixtures are exactly what the corpus was missing: first active F5J
family witnesses (incl. the float32 persist-cast cast-residue property and the
−2026→norm-0 clamp), four re-flight cells beyond jerilderie's single witness,
first F3K multi-group per-group normalisation, a mid-comp group re-draw, and a
motor-restart-effect pairing. Until they are replayed, none of that exercises
the engine.

## Before starting

- Read the parent story's *As built* section for the plan corrections and
  per-fixture caveats (comp 45 G4 discipline: assert persisted values, never
  recompute repr bits; comp 54's R7 keep-highest dedup; comp 17's retired-pilot
  ranking; comp 121's unfloored raw).
- F5J raw arithmetic: min(packed-mmss, 600 s) − two-rate height penalty +
  ADDITIVE scheme-11 landing points; check `reflight-aggregate-destination.md`
  for comp 135's OriginalRoundNo-keyed aggregation before choosing mechanics.
- Comp 17's phantom-Landing noise and comp 54's five decode deviants may need
  divergence-ledger treatment rather than faithful replay.
- Run the suite once before touching anything (`SOARSCORE_TEST_STORE=sqlite`
  avoids Docker) to record the baseline.
