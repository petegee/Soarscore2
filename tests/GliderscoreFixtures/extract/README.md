# GliderScore fixture extraction

Static JSON fixtures for the GliderScore golden-fixture pipeline are produced by
hand from real GliderScore exports using the tool in this directory. This is
one-time, developer-run tooling — see the story's plan (WI-1) on the board.

## Purpose

GliderScore stores competitions in Jet (.mdb/.accdb) databases. `extract.py`
wraps the pure-Python [`access_parser`](https://github.com/ClarotyICS/access_parser)
library and dumps **every table the library reports** (user tables plus the
`DBParams` and `MSysObjects` tables it also exposes) to one deterministic JSON
file per table. Those files are committed as fixtures that downstream
conversion steps consume; they are inputs of record, not build products.

## Usage

Extraction is run once per fixture, **by hand, offline**:

```sh
python3 extract.py <export-file> [--out DIR] [--slug NAME]
python3 extract.py <export-file> --tolerant [--recovered-texts PATH] \
    [--out DIR] [--slug NAME]
```

- `<export-file>` — the GliderScore export (a Jet database, whatever its file
  extension).
- `--out` — output root directory, default: current directory.
- `--slug` — fixture slug, default: input filename stem.
- `--tolerant` — opt-in toleration of the access_parser 0.0.6 null-bitmap
  off-by-one (see *NZ master caveat* below); without it the tool's behaviour
  is unchanged from the plain pinned-library run, byte for byte.
- `--recovered-texts PATH` — requires `--tolerant`; ingests a
  `comps-var-columns.json`-style recovery evidence file into the `Comps`
  table and emits `comps-field-provenance.json` beside it (see below).

Output goes to `<OUT>/<slug>/extract/<Table>.json` (directories created as
needed). Since 2026-08-26 this is plain system Python 3.10 with the pinned
`access_parser` below importable from its pip user-site install — no
`PYTHONPATH`, no virtualenv. (The original pin lived under
`/var/data/python/lib/python3.13/site-packages` on Python 3.13; that path no
longer exists.)

## NZ master caveat and opt-in tolerant mode

The corpus grows from a five-fixture batch sliced out of the competition
manager's full NZ master DB (`gliderscore/NZContests.mdb`, 168 competitions;
the file is gitignored and must never be committed — slice, don't ship). That
master trips two defects in the pinned access_parser 0.0.6, and both PRE
staging agents (2026-08-27) verified every other table parses clean:

1. **Crash fingerprint.** Table `Comps`, every row: inside
   `access_parser.py::_parse_fixed_length_data` the bounds check at line 315
   (`if column.column_id > len(null_table)`) lets `column_id ==
   len(null_table)` through to `null_table[column.column_id]` at line 320,
   which raises `IndexError`. The master's wider Comps schema puts column ids
   up to 42 against a 40-slot null bitmap, so `IsPublic` (id 40) raises on
   every row while `UseRegistration` (41) / `UseRegistrationIdx` (42) take the
   adjacent `>` warning branch and degrade instead.
2. **Silent drop of the 12 variable-length Text columns** of `Comps`
   (independent of the crash — this happens even with the bounds bug worked
   around): `CompName, CompVenue, CompSeriesNo, BadgeSpecs, MergedComps,
   CompID, AudioProfileDT, AudioProfileAP, AudioProfileBT, GSCompClass,
   WasLastUploadPublic, F3QDrop6to10`. They are not restorable from within
   access_parser.

`--tolerant` addresses only defect 1, surgically: it wraps
`AccessTable._parse_fixed_length_data` such that an out-of-range read resolves
exactly the way upstream resolves its own out-of-range branch — read what is
readable (fixed-offset bytes; booleans encode their value in the bitmap and
degrade to `None`). Every in-range cell still goes through the pristine
library function untouched, so values keep the exact Python types the library
returns. Each affected `table.column` is loudly warned once, and a summary
line (table, degraded column count, row count) prints after the run.

Defect 2 is addressed by ingesting pre-recovered cells rather than by porting
any cracking code: `--recovered-texts` points at
[`nz-master/comps-var-columns.json`](nz-master/comps-var-columns.json), a
byte-validated custom Jet4 record-trailer recovery of all 12 dropped columns
for all 168 Comps rows (sha256
`d2a9c74e207acb99ae9e3ba55c9ed857c814d547ef5488af2f7bb65543912d64`; produced
by analysis machinery outside the repo, ingested verbatim). On use:

- Per-row verification anchors each recovered record on that row's own
  `CompNo`. Unverifiable identity or structurally broken matched records fail
  the run hard; records flagged untrusted upstream (`varTextTrusted` false, or
  listed contaminated in the evidence's anomalies) are refused their values —
  those rows stay null with loud warnings (in the current evidence file:
  comps 2 and 4; all five fixture comps recover trusted).
- The 12 columns are merged into `<out>/<slug>/extract/Comps.json` so its
  schema + rows reflect the full 40-column surface (28 fixed-length + these
  12 Text), ordered by table-definition order as reported by the Jet tabledef.
- A sibling `comps-field-provenance.json` records per recovered column its
  `sourceKind`, method (`"ingested, byte-validated upstream"`), aggregate
  `varTextTrusted`, Jet type and merged/null row counts, plus the evidence
  file path and sha256.

Both flags default OFF. A plain run is byte-identical to before (re-verified
2026-08-27 against the committed
`../sources/gliderscore-example-comps-extract` reference).

Example invocations for the NZ master (absolute, and the repo-relative form
run from this directory):

```sh
PYTHONPATH=/tmp/opencode/nz-extract/lib python3 extract.py \
    ../../../gliderscore/NZContests.mdb --tolerant \
    --recovered-texts nz-master/comps-var-columns.json --out <dir>
python3 extract.py /home/pete/Source/SoarScore2/gliderscore/NZContests.mdb \
    --tolerant --recovered-texts nz-master/comps-var-columns.json --out <dir>
```

(The relative path runs from this `extract/` directory — three levels up to
the repo root, where the gitignored master DB lives. Both forms were
validated; with the user-site pinned install the `PYTHONPATH=...` prefix is
unnecessary.)

Differential gate, 2026-08-27: extraction of `NZContests.mdb` under
`--tolerant --recovered-texts` was compared against all five PRE-staged
per-comp slices (`/tmp/opencode/nz-pre/<slug>/tables/*.json`) table-by-table,
cell-by-cell — CompPilots, Scores, Dur, F3K, F3KTaskByRound, LndgNames,
LndgPoints, DBParams matched exactly everywhere (zero mismatches); Comps
matched on every fixed column plus all 12 recovered texts, modulo the known
machinery-JSON null convention (`""`) vs pristine parser convention (`null`)
on Jet-null cells, which pristine output wins here (e.g. `CompDate`, drop
thresholds, `PrelimCompNo`).

## Pinned access_parser version

**access_parser 0.0.6**, determined via `python3 -m pip show access_parser`
and confirmed with `importlib.metadata.version("access_parser")`. Originally
pinned from `/var/data/python/lib/python3.13/site-packages` (Python 3.13);
since 2026-08-26 that path is gone and the SAME pinned version 0.0.6 is
installed under pip user-site on system Python 3.10
(`~/.local/lib/python3.10/site-packages`) — see Usage. All four module
sha256s below were **re-verified unchanged on 2026-08-26 under Python 3.10**:

| File                  | sha256                                                            |
| --------------------- | ----------------------------------------------------------------- |
| `access_parser.py`    | `63b01f673155b6612e9883b097f8221fded5cadf1e54b043e645df084c0f94c4` |
| `__init__.py`         | `4016e1fc6eb16df38740a1ab5f14ba624d28c42d7608f3303d12e58feac4262f` |
| `parsing_primitives.py` | `2a8dc9518aab39977c65e9415999efa6a45a9098bf147df15869c9503f95313f` |
| `utils.py`            | `33d259fd0d50dbe7c9ed3185e6dc2d72e4995f8f4039fe7736063895e476f12f` |

## Offline-only rule

Extraction runs once per fixture, by hand, offline. Nothing in `src/`, `tests/`
builds or CI ever reads Jet files or invokes Python; this directory is developer
tooling only, never a runtime dependency. The JSON fixtures it emits are what
the product consumes.

## Output shape contract

One JSON document per table:

```json
{
  "schema": {"<column>": "<Jet type name>", ...},
  "rows": [{"<column>": <value>, ...}, ...]
}
```

Determinism rules:

- Table set is written in sorted order; one file per table.
- Within a table, column order is exactly the order the library reports
  (`parse_table` key order); rows follow the library's natural row order.
- Pretty-printed with 2-space indent, non-ASCII kept literal
  (`ensure_ascii=False`), always a trailing newline. Re-running the tool on the
  same export yields byte-identical files.
- Values are passed through exactly as the library returns them — no
  re-typing, no trimming. Floats round-trip exactly (JSON serialisation uses
  Python `repr` precision); non-finite floats cannot occur in valid fixtures
  (`allow_nan=False`).

### Value encoding

Native JSON types pass through directly: `null`, booleans, integers, floats,
strings. The library returns Jet dates already stringified
(`"2020-01-01 00:00:00"`, fractional seconds when present) and nulls for empty
cells. Non-native types get an explicit marker wrapper so no value is ever
silently coerced:

| Python type        | JSON encoding             | Example                          |
| ------------------ | ------------------------- | -------------------------------- |
| `bytes`            | `{"$bytes": "<hex>"}`     | `b'\x1b\xfd'` → `{"$bytes": "1bfd"}` |
| `datetime`         | `{"$datetime": "<iso>"}`  | defensive; library pre-stringifies dates |
| `date` / `time`    | `{"$date": ...}` / `{"$time": ...}` | defensive              |
| `Decimal`          | `{"$decimal": "<str>"}`   | defensive                        |
| NaN / ±Infinity    | `{"$float": "<repr>"}`    | defensive                        |

Anything else fails the run loudly rather than being coerced. In practice the
only non-native type observed in real exports is raw `bytes` (e.g.
`MSysObjects.Owner`, typed `Binary`).

### Schema type names

Type names come from the library's column-definition type codes, rendered with
Access/Jet nomenclature: `Boolean`, `Byte`, `Integer`, `Long`, `Currency`,
`Single`, `Double`, `DateTime`, `Binary`, `Text`, `OLE`, `Memo`, `GUID`,
`Numeric` (17-byte decimal), `Complex`. Unknown codes abort the run.

## Validation

`validate.py` checks a *curated* fixture directory (the JSON files beside
`extract/`) against the story's schema-v1 rules. It is stdlib-only, run by hand,
offline — same developer-tool status as `extract.py`; nothing in the build or CI
invokes it.

```sh
python3 validate.py <fixture-dir> [--index PATH]
python3 validate.py --self-test
```

- `<fixture-dir>` — e.g. `../ales-sample-comp` from this directory.
- `--index` — path to `tests/GliderscoreFixtures/index.md`. Only needed to prove
  rule 5 for a fixture that trips a concept-gap triage flag.
- `--self-test` — builds throwaway minimal fixtures in a temp directory and
  asserts rule 5 in both directions (flagged without/with unsound/sound
  `triageJustification`, plus the `ales-sample-comp` regression); exits
  non-zero if any case fails.

Enforced rules (story WI-2):

A curated directory normally carries six JSONs including `expected-result.json`
(the final-ranking oracle). A fixture staged at the scores-only stage of the
pipeline (WI-2 before the WI-3 ranking-oracle work) may declare the deferral
explicitly — `"expectedResultDeferred": true` in `provenance.json` — which makes
the missing oracle a loud stderr reminder instead of a failure; declaring the
flag while the file exists fails as a contradiction, and without the declaration
a missing oracle remains a hard failure.

1. every `scores-raw` PilotNo appears among the entries members; TaskNo /
   RoundNo / GroupNo / SeqNo present (non-null) on every row.
2. every non-zero `Landing` value exists among the referenced scheme's
   (`Dur.durLndg`) LndgPoints distances — an off-table miss silently scores 0 in
   GliderScore, so it fails loudly here. The check runs only when
   `competition.json` actually carries a `Dur` family row; Dur-less comps of the
   F3K/F5K shape have no landing scheme to consult and pass without one.
3. `GroupScoreDecimals ∈ {0,1,2,3}` and `RoundOrTruncate ∈ {0,1}` — out-of-range
   values make GliderScore zero or stale its persisted scores, invalidating any
   fixture. Persisted null (unset) knobs are recorded faithfully as `null` and
   warned but not failed: they carry no out-of-range semantics (scores persist
   fine — a DB-wide pattern), and coercing them to defaults would falsify
   curation.
4. exactly one competition per fixture: a single distinct CompNo across
   competition identity, entries and `scores-raw`, matching `competition.json`.
5. concept-gap triage flags (`UseTeams=true`, set series link `CompSeriesNo`,
   preliminary link `PrelimCompNo`, non-empty `MergedComps`) require the fixture
   to be skip-listed in `index.md`: with `--index` given the run fails unless the
   index marks the slug skipped; without `--index` it prints a warning naming the
   requirement. Refinement (2026-08-26): a team or series flag alone no longer
   forces the skip if `competition.json` records a sound `triageJustification`
   inside its `triage` object —
   `{"series": {"deadLinkCount": 0, "evidence": "<non-empty string>"},
   "teams": {"evidence": "<non-empty string>"}}`. The validator mechanically
   checks that series `deadLinkCount` is an integer equal to 0 and that no
   `scores-raw.json` column name contains "team" (case-insensitive); each
   flagged concept needs its own sound justification, and preliminary /
   merged-comp gaps are never excusable.
6. integrity (beyond the five): `expected-scores.json` keys correspond 1:1 with
   `scores-raw` rows on the composite key `{TaskNo}/{RoundNo}/{GroupNo}/
   {ReFlightNo}/{PilotNo}`, every key's pilot is among the entries members, and
   the declared key format is the canonical one.

Exit code 0 on pass, 1 on any failure; warnings go to stderr and do not fail the
run.

### Index contract (rule 5)

`index.md` lists one fixture per line as `- <slug> — <status> — …`. A slug counts
as skip-listed when a line starts with that slug token and contains the word
"skipped" (case-insensitive), e.g.:

```
- some-comp — skipped — team scoring (concept gap)
```

## Replay-and-compare harness

The replay-and-compare harness lives in `tests/Soarscore.Acceptance.Tests`
(feature `ReplayingAGliderscoreFixture`, support code in
`Support/Gliderscore/`). It replays each active fixture through Soarscore's
public command surface only — publish the authored class definition, create,
register, prescribe the realised draw, accept, open entries/flights, capture,
complete rounds, finalise — then compares GliderScore's persisted scores at
three grains (raw flight score · per-round normalised score · final ranking)
with EXACT decimal equality, no tolerance. See the story on the board
(`gliderscore-replay-and-compare-harness.md`) for the full design.

### How the corpus is consumed

- `index.md` is the manifest: every `- <slug> — …` bullet whose line does not
  contain "skipped" is active; the feature holds one scenario per active
  fixture plus the harness's own self-checks (replay determinism, score
  conservation, ledger strictness).
- Fixtures stay exactly where they are — nothing copies or moves them. The
  loader resolves the corpus by walking up from the test assembly's location
  until `tests/GliderscoreFixtures` appears, so build-output depth is never
  hardcoded.
- Per fixture the harness loads the curated JSON (`competition.json`,
  `entries.json`, `scores-raw.json`, `expected-scores.json`,
  `expected-result.json` — `provenance.json` documents curation but is not
  machine-read), plus the hand-authored `<slug>/class-definition.json`, which
  is deserialised with the Api's own ingestion options and posted verbatim to
  `/publish-class-definition`.
- An optional `<slug>/divergences.json` lists accepted differences after
  human triage; absent means an empty ledger. One object per entry:

  | Field     | Meaning                                                    |
  | --------- | ---------------------------------------------------------- |
  | `grain`   | `raw`, `normalised` or `ranking`                           |
  | `round`   | round number; null for the ranking grain                   |
  | `group`   | group number; null for the ranking grain                   |
  | `pilotNo` | pilot number, or `"*"` for all pilots                      |
  | `reason`  | must cite an arithmetic-story divergence ID (`D1`–`D6`) or story trap 3 |

  The comparator subtracts ledgered entries and fails on any remainder. The
  citation rule is asserted by the feature steps, not merely convention.

### Running

The feature carries the `@gliderscore` tag, exposed as xUnit trait
`Category=gliderscore`, so just that feature runs as:

```sh
dotnet test tests/Soarscore.Acceptance.Tests --filter "Category=gliderscore"
```

One store per run, selected by the `SOARSCORE_TEST_STORE` environment
variable — `postgres` (the default) or `sqlite`; postgres spins up its store
via Testcontainers and needs Docker running:

```sh
SOARSCORE_TEST_STORE=sqlite   dotnet test tests/Soarscore.Acceptance.Tests --filter "Category=gliderscore"
SOARSCORE_TEST_STORE=postgres dotnet test tests/Soarscore.Acceptance.Tests --filter "Category=gliderscore"
```

A backend Soarscore claims to support is one that is green under both values.
Drop the filter to run the rest of the acceptance suite alongside.

### Adding a fixture

1. **Curate** through this directory's pipeline as documented above:
   `extract.py`, hand-curation of the six JSON files, `validate.py` passes.
2. **Author `<slug>/class-definition.json` by hand**, following story decision
   D3. The mapping rules from `competition.json`:
   - Normalisation arrangement follows `GroupScoreOption`: option 2 (time
     basis) puts the time term in `Score` and the landing lookup in
     `ScoreNormalised`; option 1 (points basis) puts landing inside `Score`
     and normalisation scales the total. Verify the actual value per fixture
     — do not assume it from the class name.
   - Duration is a `PiecewiseTerm` symmetric decay curve (band `[0,target]`
     rate PPS, band `[target,∞]` rate −PPS), never a plain capped rate term.
   - Landing is an exact-match `LookupTerm` with ascending `upTo` rows from
     the scheme's `LndgPoints`, including the leading-zero row (`upTo` 0 → 0)
     where needed. Validator rule 2 guarantees every flown landing sits
     on-table, so never soften the lookup.
   - Rounding grid: `GroupScoreDecimals`/`RoundOrTruncate` →
     `normalise.round` `{mode: HalfUp|Truncate, precision: 10^-Decs}`.
   - Drops collapse into ONE policy (`dropCount` = thresholds crossed,
     `applyWhenRoundsCompletedAtLeast` = lowest crossed threshold) — the
     engine applies the first matching policy only.
   - Penalty columns map by scope: `Scores.FlightScoreDeduction` →
     entry-scoped penalty definition, replayed via `/record-entry-penalty`;
     `Scores.Penalty` → competition-scoped definition, replayed via
     `/record-competition-penalty` with the competitor as subject.
   The existing authored definitions are the templates — `ales-sample-comp`
   is the simplest; `jerilderie-2010` shows drop collapse and a competition
   penalty.
3. **Add a scenario** to `ReplayingAGliderscoreFixture.feature`, asserting
   the three grains plus conservation, and the ledger shape you expect
   ("carries no ledgered divergences" or "records exactly N accepted
   divergences").
4. **Replay.** Any mismatch prints one diff table (pilot × round × grain,
   ours / expected / delta).
5. **Triage** every difference: *importer/authoring bug* · *our engine
   defect* · *intentional divergence*. Fix the first two at their source;
   only the third goes further.
6. **Ledger** intentional divergences in `<slug>/divergences.json`, each
   reason citing the arithmetic-story divergence ID behind the difference.
   The ledger starts empty; an entry lands only after human triage.

## Differential gate result

2026-08-25: extraction of `/home/pete/Downloads/GliderScoreDownload.txt`
(ALES sample comp) was diffed table-by-table, cell-by-cell against a prior
ad-hoc reader's baseline (stringified column-oriented dumps of the same file).
**Verdict: clean agreement.** All 29 tables matched: identical table sets,
identical ordered columns, identical row counts (105 data rows total), all
1883 populated cells equal under stringification, and zero schema type-name
differences. No disagreements to reconcile.

## Server-DB extraction (`extract-mssql.py`)

2026-09-04: a second acquisition path beside the Jet one. `extract-mssql.py`
slices a **single competition** out of the gliderscore.com **server database**
(`gliderscore/DB_12582_gliderscore_backup.bak`, SQL Server — gitignored, never
committed; restored into the throwaway Docker container). Where `extract.py`
dumps every table of a whole Jet file, this tool dumps exactly the comp-scoped
tables that carry at least one row for the given CompID. Stdlib-only Python
3.10, no third-party dependencies, run by hand, offline.

### Usage

```sh
python3 extract-mssql.py <CompID> [--container NAME] [--db NAME] \
    [--sqlcmd PATH] [--out DIR] [--slug NAME] [--no-redact] \
    [--password-env NAME]
```

- `<CompID>` — case-sensitive hex string (e.g. `237BA891f9949`). The WHERE
  comparison is forced case-sensitive with
  `COLLATE Latin1_General_CS_AS`; a wrong-case ID finds no rows and the run
  aborts with a case hint.
- `--container` — Docker container name, default `mssql-gliderscore`.
- `--db` — database name, default `gliderscore`.
- `--sqlcmd` — sqlcmd path inside the container, default
  `/opt/mssql-tools18/bin/sqlcmd`.
- `--out` / `--slug` — as `extract.py`; slug default `mssql-comp-<CompID>`.
- `--no-redact` — disable redaction (below). Never commit such output; the
  tool prints a loud warning when set.
- `--password-env NAME` — env var holding the SA password, default
  `EXTRACT_MSSQL_SA_PASSWORD`. When unset the tool falls back to the
  throwaway container's default password (`Gliderscore!Restore1` — the
  container is disposable, so this is not treated as a secret); a real
  password must come from the environment and is never printed or written.

Queries run read-only via `docker exec` + sqlcmd:
`-S localhost -U sa -P <password> -C -No -d <db> -Q "<query>" -s '|' -f 65001 -b -y 0`.
`-y 0` is required (the default 256-char display truncation silently
corrupts long rows) and is mutually exclusive with both `-W` and `-h`, so
those classic flags are absent — the hex transport below needs neither.
`-b` makes any SQL error exit non-zero and abort the run loudly.

### Table discovery

Tables are discovered dynamically from `INFORMATION_SCHEMA`: every base table
with a `CompID` column is counted under the case-sensitive filter, and
exactly those with ≥ 1 row are dumped — one `<Table>.json` per table under
`<out>/<slug>/extract/`, written in sorted table order.

### Output shape contract — reuse

Identical to `extract.py`'s (above): `{"schema": {...}, "rows": [...]}`,
2-space indent, `ensure_ascii=False`, `allow_nan=False`, trailing newline,
column order = the table's ordinal position, native JSON values pass through
natively, unknown value types fail the run loudly. Re-running on the same DB
state yields byte-identical files (proven by double-run byte-compare in
`/tmp`).

SQL Server type-name mapping (INFORMATION_SCHEMA `DATA_TYPE` → simple name;
types not listed abort the run):

| DATA_TYPE                                        | name       |
| ------------------------------------------------ | ---------- |
| `char`, `nchar`, `varchar`, `nvarchar`, `text`, `ntext` | `Text` |
| `tinyint`, `smallint`                            | `Integer`  |
| `int`, `bigint`                                  | `Long`     |
| `numeric`, `decimal`                             | `Decimal`  |
| `bit`                                            | `Boolean`  |
| `datetime2`, `datetime`, `smalldatetime`         | `DateTime` |
| `date`                                           | `Date`     |
| `float`, `real`                                  | `Double`   |

### Value transport and NULL/empty

Every cell is converted to nvarchar in SQL (datetime2 via CONVERT style 126 —
ISO 8601, full fractional precision), hex-packed as UTF-16LE, and the fields
joined with `|` into one row string; Python decodes the hex and re-parses
each cell per the declared column type. Hex transport makes the pipeline
immune to column separators, code pages and embedded newlines/quotes in data.

NULL vs empty string is distinguished exactly: text columns carry an
in-query ISNULL sentinel (`<<__NULL_TOKEN__>>`; a stored text value equal to
that literal would be misread as NULL — no such value exists in the corpus
tables), while non-text conversions never render to the empty string, so an
empty field means NULL. Fixed-length `char`/`nchar` values are RTRIMmed (the
padding is a column-width artifact, not data); `varchar`/`nvarchar` pass
through untouched.

Decimals: `numeric`/`decimal` cells are parsed with Python `Decimal` from
the converted text — which preserves the column's stored scale exactly (e.g.
`37.500` on a `numeric(18,3)` stays `37.500`) — and serialised via the
`{"$decimal": "<str>"}` wrapper, matching `extract.py`'s value-encoding
table. Byte-exact round-trip.

### Deterministic row order

Each table query ORDER BYs the table's natural key (ScoringData/ScoringBackup:
RoundNo, GroupNo, SeqNo, ReFlightNo, PilotNo; F5KData/F3KData: RoundNo;
F5KBonusData: Metres; LandingData: Distance, Points; TargetTimeByRound:
RoundNo; DigitalTimerData: RoundNo, GroupNo, ReFlightNo) followed by every
remaining column as a total-order tiebreaker — byte-stable across runs.

### PII redaction (on by default)

`PilotName` and `HelperName` carry real names in the server DB and `FAI_ID`
carries licence numbers. Redaction happens before serialisation, so only
redacted values are ever written:

- Each distinct pilot — keyed on `(CompID, PilotNo)` (name fallback where a
  table has `PilotName` but no `PilotNo`) — gets a deterministic name from a
  fixed 20-name Simpsons pilot pool: keys are ordered by sha256 digest and
  names assigned from the pool in order, so the assignment is collision-free
  (≤ 20 pilots, the project's scale bound), stable across runs, and
  consistent for the same pilot across all tables. Empty/null names pass
  through unchanged.
- Each distinct non-empty `HelperName` gets a deterministic name from a
  disjoint non-pilot Simpsons pool (same hashing), so a helper never shares a
  pilot's name.
- `FAI_ID` is blanked to `""` (null or not).
- **No mapping is persisted anywhere.** The assignment lives only in memory;
  the run prints counts, never names.

`--no-redact` restores the raw values (for verifying the tool against the
database); its output must never be committed.

### Limitations

- The SA password travels as a process argument of `docker exec` (visible in
  the local process table) — accepted for developer-run tooling; prefer
  `--password-env` on shared machines.
- Tables are matched by name in `INFORMATION_SCHEMA` without schema scoping;
  the restore is single-schema, so this is fine today.
- A text value exactly equal to the NULL sentinel would round-trip as NULL
  (see above; none exist in the corpus tables).
- Types outside the mapping abort the run loudly (extend `SQL_TYPE_NAMES`
  then).
- The tool expects the single-resultset, headerless output shape of the
  mssql-tools18 sqlcmd build in the container; a header block, if a build
  ever prints one, is detected and skipped, and anything else malformed
  fails loudly.

Offline-only rule: exactly as for `extract.py` — run once per fixture by
hand, offline; nothing in `src/`, `tests/` builds or CI ever invokes this
script; only its committed JSON output is consumed downstream.
