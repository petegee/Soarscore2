# Story — Gliderscore golden-fixture pipeline

**Status:** Backlog · **Raised:** 2026-08-25 · **Planned:** 2026-08-25

## What

Turn real GliderScore competitions into committed, provenance-noted JSON fixtures that
the replay/compare harness (`gliderscore-replay-and-compare-harness.md`) consumes. One
fixture per competition:

pilots · comp config · class config (family row + landing/bonus tables) · entries ·
realised draw · raw `Scores` rows · GS's persisted computed values (`RawScore`,
`NormalisedScore`) as the expected oracle · a final-result oracle (*Ranking oracle*
below).

The field contract is settled: `kanban/completed/resolve-gliderscore-scoring-arithmetic.md`
(completed 2026-08-25) *Handoff notes* §1–3 name every config field a fixture must carry,
the packed-mmss encoding (`500.0` = 300 s), on-table landing distances, and the
no-tolerance comparator strategy with its two emulated binary32 persist casts. That
story's findings are the single source this pipeline cites for *what the numbers mean*;
this story decides *how they are packaged*.

## Work items

### WI-1 — Extraction tool

A small Python extractor kept with the fixtures — extraction runs once per fixture,
offline; nothing in `src/`, `tests/` build or CI ever reads Jet or invokes Python
(that decoupling is the point of static fixtures).

- **Library:** wrap `access_parser` (pure-Python Jet reader). Discovered installed
  2026-08-25 at `/var/data/python/lib/python3.13/site-packages/access_parser/` and
  verified against `/home/pete/Downloads/GliderScoreDownload.txt` — all 28 user tables +
  `DBParams`, `Scores` = 30 rows × 29 cols with correct types. Pin the version in the
  tool README. This retires the story's earlier assumption that the reader must be
  recreated from scratch: the prior ad-hoc reader was not preserved, but wrapping a
  maintained library beats re-owning a Jet parser.
- **Shape:** `extract.py <export-file>` writes one JSON per table next to the chosen
  fixture directory (`<slug>/extract/<Table>.json`): `{"schema": {col: type}, "rows":
  [{col: value}, …]}` — row-oriented, values exactly as the library returns them.
  `DBParams` included; it carries the producer version the provenance record cites.
- **Differential gate:** the surviving ad-hoc outputs (`/tmp/opencode/gs_schema.json`,
  `gs_data.json` — confirmed present 2026-08-25, ephemeral) are the known-good
  baseline. First action of this WI: normalise both representations (stringify) and
  diff table-by-table, cell-by-cell, on the same source export. Any disagreement is
  resolved before anything downstream is built. Do **not** commit the baseline copies —
  the `.txt` regenerates them deterministically.
- **Done when:** differential clean on the sample export; README records usage, pinned
  library version, and the offline-only rule.

### WI-2 — Fixture schema v1 (documented here)

Layout: `tests/GliderscoreFixtures/` — a neutral home, since the harness story
deliberately defers choosing between a tagged feature in `Soarscore.Acceptance.Tests`
and a sibling project. Fixtures move by `git mv` when that decision lands.

Source exports are committed beside the fixtures under
`tests/GliderscoreFixtures/sources/<slug>.mdb` (the `.txt` extension on
`GliderScoreDownload.txt` is camouflage, the bytes are Jet) — decided 2026-08-25:
provenance that points at `~/Downloads` is unverifiable by anyone else, the
differential gate and any re-extraction need the exact bytes, and 450 KB once is
cheaper than an unprovable chain. `provenance.json` then cites a repo-relative path,
the *original* filename it came from, and the sha256. PII check before committing each
source: the sample carries only GliderScore's shipped test pilots (ZZ-prefixed names,
empty contacts), but real comps must have contact columns inspected — blank or redact
at curation rather than committing members' personal data.

Per fixture:

| File | Content |
|---|---|
| `provenance.json` | source filename + sha256, producer version (from `DBParams`), who exported, export date, extractor/library versions, free-text notes |
| `competition.json` | comp identity; curated whitelist of scoring knobs per Handoff §1: `GSCompClass`, `GroupScoreOption`, `GroupScoreDecimals`, `RoundOrTruncate`, `DropScoreOption`, `Drop1AtRound…Drop5AtRound`, `F3QDrop6to10`; family row(s) (`Dur` fields incl. target-time, points-per-second, timekeepers, landing-scheme ref, ref-height/rates; `Spd`/`Dis`/`F3K`/`F5B`/`F5K` rows when populated); per-round schedule tables (`DurTargetTimeByRound`, `F3KTaskByRound`, `F5KTaskandRefHeightByRound`); referenced lookups only (`LndgPoints` rows for the comp's scheme; `F5KBonus*` when used); triage fields (`UseTeams`, team counts, series/prelim links) recorded so skip-decisions are auditable |
| `entries.json` | `CompPilots` verbatim + `Pilots` rows filtered to those members — exports carry *all* master pilots unfiltered (see Export format), filtering is curation |
| `scores-raw.json` | full `Scores` row **minus exactly `RawScore` and `NormalisedScore`** — no cherry-picking; packed mmss values untouched; `Updated` and `OriginalRoundNo` carried (they gate GS rescoring and drive the report rollup). The realised draw derives from these rows (`RoundNo → GroupNo → SeqNo` order); there is no separate draw section — one derivation, no drift |
| `expected-scores.json` | persisted `RawScore` / `NormalisedScore` keyed `(TaskNo, RoundNo, GroupNo, ReFlightNo, PilotNo)` |
| `expected-result.json` | final-ranking oracle — see *Ranking oracle* |

Deliberate exclusions: per-family timing decimals (UI display-only, never affect stored
scores — Handoff §4 says do not model); presentation/device/audio tables;
`ScoresChecked` sign-off flag stays in `scores-raw.json` as inert evidence but maps to
nothing (no sign-off in our trust model).

Validation rules (checked at curation; a `validate.py` beside `extract.py` may enforce):

1. every `scores-raw` PilotNo ∈ entries; TaskNo/RoundNo/GroupNo/SeqNo present;
2. every non-zero `Landing` value exists in the referenced `LndgPoints` distances —
   an off-table miss silently scores 0 in GS, so this must be a loud failure here;
3. `GroupScoreDecimals ∈ {0..3}` and `RoundOrTruncate ∈ {0,1}` — out-of-range zeroes
   or stales GS scores (Handoff §2 guard); such an export is invalid as a fixture;
4. exactly one competition per fixture — multi-comp exports are split at curation;
5. any triage field marking a §6 concept gap (teams, series, merged/prelim) ⇒ the
   fixture must be skip-listed in `index.md`, never silently active.

### WI-3 — Commit fixture #1: ALES sample comp

Slug `ales-sample-comp`. Curated via WI-1 output from
`/home/pete/Downloads/GliderScoreDownload.txt`, committed as
`tests/GliderscoreFixtures/sources/ales-sample-comp.mdb` with sha256 recorded.

Provenance notes to carry: producer DBVersion 6.78; 10 pilots, 3 rounds × 1 group;
rounds 2–3 all-zero with `Updated='False'`; Decs=0 forces integral scores so the
float32 persist cast is unwitnessed by this fixture (arithmetic story, Precision &
storage §4 Unresolved); `durFlightPenalty=0` so the height-penalty config fields are
inert despite being set; drops never activate (`Drop*=99`). Spot-check the curated
oracle against the reconciliation table (1030 / 835 / 440 / seven zeros) before
committing.

**Done when:** validation rules pass and committed — this discharges the harness
story's "first fixture committed" precondition.

### WI-4 — Corpus growth + skip-list

Add Pete's install example comps across classes. Each addition is now small:
extract → curate → validate → index.

- `tests/GliderscoreFixtures/index.md`: one line per competition — status (active /
  skipped), class, what it exercises, skip reason. Skipped comps stay listed forever;
  the corpus manifest is how gap-hunting targets get chosen.
- Standing skip reasons: §6 concept gaps (team scoring, series, merged/prelim);
  multi-task-per-round comps (F3B-style) until multi-task rounds exist — they hit the
  deferred `unsupportedRoundComposition` draw rejection today.
- Diversity wanted (hunt comps that provide): ≥ 1 multi-group round; ≥ 1 re-flight
  (`OriginalRoundNo ≠ RoundNo`); ≥ 1 drop threshold crossed (an F3K comp would witness
  divergence D6 — GS drops at 12 scored rounds vs official 6); ≥ 1 `Decs ≥ 1` comp
  (witnesses the float32 artifacts the sample cannot show); Speed/Distance families
  when available.

## Ranking oracle — decided 2026-08-25 (hybrid)

GS persists no rank or percent — both are computed at report time
(arithmetic story, Ranking & tie-breaks). `expected-result.json` therefore records its
own source:

```json
{ "source": "gs-report-transcript" | "reconstructed-ladder", "ranks": [ … ] }
```

- **`gs-report-transcript`** — preferred wherever possible: run GS Overall Results for
  the comp and transcribe rank order (and displayed `"=n"` ties) by hand. Strongest
  evidence; costs manual effort per fixture.
- **`reconstructed-ladder`** — computed from the documented ladder (arithmetic story,
  THE LADDER section) when no transcript exists. Fully automated, but a mismatch here
  needs source-level triage before it indicts our engine — the fixture must say so.

Percent is display-only and never a ranking input (arithmetic story) — transcribe ranks,
not percents.

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

## Sequencing

Independent of `prescribed-draw-import.md`. `gliderscore-replay-and-compare-harness.md`
needs this story's WI-2 + WI-3 (plus prescribed draw) to go green; WI-4 grows the corpus
opportunistically thereafter. The scoring-arithmetic dependency named when this stub was
raised is discharged — see the completed story's Handoff notes.

## Before starting

- Take into `in-progress/` per board rule 3; WI-1 and WI-2 can run in parallel, WI-3
  needs both.
- Fixtures are test data, not `/docs` (house rules 3–4); each records source file,
  sha256, GS version, and exporter in `provenance.json`.
- No runtime dependencies added anywhere: the extractor is developer tooling under
  `tests/GliderscoreFixtures/extract/`, run by hand, once per fixture.
- If the differential gate (WI-1) surfaces disagreements with the surviving extracts,
  stop and reconcile against the VB source before curating anything — the arithmetic
  story's reconciliation ran over those extracts, so any extraction difference taints
  the validated formulas' input data.
