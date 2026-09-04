# Story — F5K fixture from the GliderScore server DB export

**Status:** complete (2026-09-04)

## What

Add the corpus's first F5K-family fixture, `f5k-ni-round-2`, sliced from the
gliderscore.com **server database export** (`gliderscore/DB_12582_gliderscore_backup.bak`,
SQL Server — gitignored, never committed), which is a new acquisition path:
the corpus so far grows from Jet exports only.

Source facts (queried 2026-09-04 from the restored container `mssql-gliderscore`):

- Comp `237BA891f9949` — "F5K NI Round 2", Haumoana New Zealand, 2026-05-29,
  `CompType=F5K2024`, latest NZ F5K comp by CompDate and upload (`DateCreated`
  3804 vs 3802 for same-day "F5K NI 2", comp `233DDc0ae8889`).
- 6 pilots × 10 rounds, 55 score rows (ragged — retirements/absences expected),
  2 groups seen, no re-flight rows observed.
- Knobs: `GroupScoreOption=1`, `GroupScoreDecimals=1`, `RoundOrTruncate=0`
  (half-up 1 dp), `Drop1At=5` (crosses at 10 rounds), `Drop2At=99`,
  `DropScoreOption=0`, `UseTeams=False`, `TimingDecimalPlaces=0`.
- F5K config: `F5KData` task-per-round (A–E catalogue) with per-round
  `F5KRefHeight` (NLH), max flights/flight-time/total-time;
  `F5KBonusData` ladder (−1→0.5, 0→0.0, 1→1.0, 11→3.0) matches
  `tools/Soarscore.SeedData/SeedF5K.cs` `LaunchBands` exactly (0.5/m bonus below
  NLH; 1.0/m to 10 m over; 3.0/m beyond).
- Per-flight detail lives in `ScoringData.Flight1..4` structured strings
  (`FltNbr/FltTim/MinSec/TimPts/HgtVal/HgtPts/FltPlty/LdgOut/LdgOutPlty/
  NbrFlts/NbrFltsPlty/ZeroRnd/Updated`); `RawScore`/`NormalisedScore`/
  `Progressive*` persisted server-side — a full three-grain oracle is possible.
- **PII**: server `ScoringData` carries real pilot names. All committed corpus
  artifacts are name-free today (Jet `Scores` has no name column); any
  name-bearing column in the new extract path must be redacted to Simpsons
  character names, deterministically by PilotNo, with no mapping committed.

## Why it matters

- First F5K-family fixture (index "Diversity wanted" has F5L/F5B absent, F5K
  unexercised) and first `F5K2024`-rules witness: launch-altitude band-integral
  scoring relative to a per-round NLH (bonus below, penalty above), task
  catalogue A–E, flight-count and landing-out penalties.
- Establishes the server-DB extraction path beside the Jet `extract.py` path.

## Before starting

- `tests/GliderscoreFixtures/extract/README.md` — pipeline contract, output
  shape contract, offline-only rule, validator rules.
- `tests/GliderscoreFixtures/index.md` — corpus manifest + skip rules.
- `tools/Soarscore.SeedData/SeedF5K.cs` — the F5K class model (launch bands,
  tasks, guards) to author `class-definition.json` from.
- Invariant to hold (named per testing approach): **every persisted
  `RawScore` cell re-computes exactly** as the sum of per-flight points, each
  flight point being time points + the signed band-integral of the launch
  ladder (0.5/m below NLH; 1.0/m to 10 m over; 3.0/m beyond) + flight-count
  and landing-out penalties, minus flight penalties — verified cell-exact
  before the oracle is trusted; the replay harness then asserts all three
  grains with exact decimal equality.

## Plan

- **WI-1 — Server-DB extract tooling.** `tests/GliderscoreFixtures/extract/
  extract-mssql.py`: single-comp slice from the running SQL Server container
  (via `docker exec` + `sqlcmd`), emitting the same `extract/<Table>.json`
  shape contract as `extract.py` (schema map, sorted tables, deterministic row
  order, 2-space pretty JSON, `ensure_ascii=False`, trailing newline).
  Comp-scoped tables only (rows WHERE `CompID=<id>`). Deterministic Simpsons
  redaction of `PilotName`/`HelperName` (and `FAI_ID`) on by default; no
  redaction mapping persisted. README section; offline-only rule unchanged;
  determinism proven by double-run byte-compare in /tmp.
- **WI-2 — Extract and curate `f5k-ni-round-2`.** Run WI-1 tooling for
  `237BA891f9949`; curate the six JSONs (`competition.json`, `entries.json`,
  `scores-raw.json`, `expected-scores.json`, `expected-result.json`,
  `provenance.json` — server knobs are stored ints here, record real values,
  noting provenance kind) and author `class-definition.json` from SeedF5K
  adapted to the comp's realised tasks/knobs (per-round NLH, task schedule,
  drops: Drop1@5 crossed, Drop2 unset). Verify the raw arithmetic cell-exact;
  run `validate.py` clean.
- **WI-3 — Replay scenario + triage.** Add the fixture scenario to
  `ReplayingAGliderscoreFixture.feature`, run the `@gliderscore` feature under
  both stores (`SOARSCORE_TEST_STORE=sqlite` and `postgres`), triage any
  mismatch (importer/authoring bug · engine defect · intentional divergence;
  ledger divergences with arithmetic-story citations), fix defects at source.
- **Closure** — index.md entry for `f5k-ni-round-2`, reconcile
  `tech-debt.md`/`deferred-decisions.md`, `graphify update .`, move story to
  `completed/`.
## As built (2026-09-04)

- **WI-1** — `tests/GliderscoreFixtures/extract/extract-mssql.py` + README
  section. Determinism proven (double-run byte-compare on the F5K Sample Comp);
  comp discovery is case-sensitive-hex aware; decimals carry the DB's stored
  scale via `extract.py`'s `{"$decimal"}` convention; Simpsons redaction of
  `PilotName`/`HelperName` and blanking of `FAI_ID` on by default.
- **WI-2** — fixture `tests/GliderscoreFixtures/f5k-ni-round-2/` (curated six
  JSONs + `class-definition.json` + `extract/`). Arithmetic verified cell-exact
  before curation: 80 flights' time/clamp + height-ladder + penalty-identity
  terms, 30/30 RawScore sums, 30/30 normalisation, 30/30 progressive+drop —
  355 cells, 0 deviations. Three coexisting Flight-string serialisations in the
  data (upper, lower, legacy long-key) — all decoded. Validator PASS with no
  triage flags; class definition deserialises through the real ingestion
  options with 0 defects. Raggedness characterised: pilot 88 never flew
  (stub rows R1–R5, no rows after), R7–R10 unscored placeholder rounds; oracle
  = GS's own R6 progressive standings, independently recomputed.
- **WI-3** — harness widened (F5K schedule table + task lookup, per-task F5K
  capture map decoding Flight1..4 into `flightTime`/`launchAltitude`/flag
  captures, task-B stored-last-flight shape with NOF padding, prescription-only
  synthetic slots for never-flew pilot 88). One authoring bug found and fixed
  in the fixture's class definition (`rankByMetric: flightTime` on tasks A/D —
  GS pairs any-order targets longest-first; 3 real rows witness the
  difference); seed gap → tech-debt. **Replays all 10 rounds** (WI-2's
  christchurch-precedent correction: that fixture's snapshot lives in its
  oracle, not in round-skipping) — the drop-a-placeholder-zero difference vs
  GS's scored-round-gated pool is placing-identical and touched by no grain.
  Ledger EMPTY — clean parity on all three grains.
- **Tests**: `Category=gliderscore` 13/13 passed under BOTH
  `SOARSCORE_TEST_STORE=sqlite` and `postgres`; validate.py PASS; no `src/`
  changes needed; no divergences ledgered.
- **Closure** — index.md bullet added (WI-3); tech-debt gained four items
  (FAI seed A/D `RankByMetric`; FlightSelector-vs-FlightInterpreter zeroing
  conflict; per-round NLH bindings; server-path version pin). Nothing to
  reconcile in `deferred-decisions.md`. No glossary/class-diagram change — no
  new domain concepts.
