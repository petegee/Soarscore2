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
```

- `<export-file>` — the GliderScore export (a Jet database, whatever its file
  extension).
- `--out` — output root directory, default: current directory.
- `--slug` — fixture slug, default: input filename stem.

Output goes to `<OUT>/<slug>/extract/<Table>.json` (directories created as
needed). Since 2026-08-26 this is plain system Python 3.10 with the pinned
`access_parser` below importable from its pip user-site install — no
`PYTHONPATH`, no virtualenv. (The original pin lived under
`/var/data/python/lib/python3.13/site-packages` on Python 3.13; that path no
longer exists.)

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

1. every `scores-raw` PilotNo appears among the entries members; TaskNo /
   RoundNo / GroupNo / SeqNo present (non-null) on every row.
2. every non-zero `Landing` value exists among the referenced scheme's
   (`Dur.durLndg`) LndgPoints distances — an off-table miss silently scores 0 in
   GliderScore, so it fails loudly here. The check runs only when
   `competition.json` actually carries a `Dur` family row; Dur-less comps of the
   F3K/F5K shape have no landing scheme to consult and pass without one.
3. `GroupScoreDecimals ∈ {0,1,2,3}` and `RoundOrTruncate ∈ {0,1}` — out-of-range
   values make GliderScore zero or stale its persisted scores, invalidating any
   fixture.
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

## Differential gate result

2026-08-25: extraction of `/home/pete/Downloads/GliderScoreDownload.txt`
(ALES sample comp) was diffed table-by-table, cell-by-cell against a prior
ad-hoc reader's baseline (stringified column-oriented dumps of the same file).
**Verdict: clean agreement.** All 29 tables matched: identical table sets,
identical ordered columns, identical row counts (105 data rows total), all
1883 populated cells equal under stringification, and zero schema type-name
differences. No disagreements to reconcile.
