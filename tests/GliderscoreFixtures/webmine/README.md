# webmine/ — GliderScore online competition acquisition (read-only)

Developer-run, hand-invoked Python tooling that acquires public competition data
from gliderscore.com for the fixture corpus. Same status as `../extract/`:
**never a build or runtime dependency of Soarscore proper**; nothing in `src/`
knows this directory exists.

Story: `kanban/completed/gliderscore-webmine-tool.md` (safety contract +
wire-format validation). Research basis: `gliderscore-online-data-mining.md`
(repo root). Endpoints/behaviour are cited from those documents — this tree
contains no GliderScore source text.

## Permission state

**No live use yet.** First live call waits on the courtesy email to
gerry.carter(at)gliderscore.com explaining Soarscore and asking blessing for
occasional downloads (story §Safety contract 5, mining doc §Etiquette).
Offline work, tests and triage of already-downloaded artifacts need no gate.

## Etiquette and volumes

- ≥1 s between any two requests, enforced by the kernel (`min_interval_seconds`,
  default 2 s; constructor refuses below the 1 s floor). One comp per invocation;
  bulk acquisition means repeated manual invocations — deliberate friction.
- `--tasks` costs one extra request per run and defaults OFF.
- Always finish with `DeleteDownloadFile` (best-effort finaliser runs even on
  failures) — leave no trace server-side.
- Public comps only: CompIDs come from the public catalogue; PRIVATE comps are
  not visible there by design and must not be probed.
- Every request (and every refusal!) is appended to a JSONL audit log when
  `--audit PATH` is given — keep one per working session so total traffic can
  be evidenced:
  `{"ts": "<ISO8601 UTC>", "op": "download_zip", "method": "GET",
    "url": "https://…", "status": 200, "bytes": 1234, "refused": false}`

## Layout

| File | Role |
|---|---|
| `gsclient.py` | **Safety kernel.** Allowlisted URL builders are the only way to issue a request (read-only ACTIONs only, closure-tested); throttle floor; append-only audit log; best-effort delete finaliser primitive; wire verdict constants. |
| `mine_catalogue.py` | Catalogue miner → `comps.json` + `comps.csv`. `last30` = one GET; `all` = GET + WebForms postback ("All competitions"). Dedupes by CompID (first wins), chronological sort (`%Y %b %d` parsing — never sort month names lexically). |
| `fetch_comp.py` | One comp per invocation: CheckScoresExist → CreateScoringDataAsZipArchive → GET `scoredownload/<CompID>_DownloadData.zip` → zip-entry guard → parse → `<CompID>_records.json` + `<CompID>_triage.json`; delete finaliser always fires. |
| `csvparse.py` | Strict parser for the headerless pipe-delimited download CSV (exactly 22 columns, `.` decimals server-canonicalised). Flag columns carry exact-case `'True'`/`'False'`; unused Flight1..4 slots arrive as `''`. Parser raises loudly on any deviation (count mismatch, interior blanks, non-numeric typed columns outside the proven accommodations). |
| `triage.py` | Per-CompType generic decode (duration mm/ss splits · F3K seven slot-times in seconds · F5K Flight1..4), draw-completeness check (duplicate base slots abort; absences reported as gaps), tolerant one-pilot eScoring task scraper. |
| `tests/` | pytest + hypothesis. Offline only — fake transports/clocks throughout. |

## Usage

```bash
# catalogue (default: last 30 days)
python3 tests/GliderscoreFixtures/webmine/mine_catalogue.py \
    --range all --out /tmp/gs-catalogue --audit /tmp/gs-audit.jsonl

# fetch + triage one comp (CompIDs are CASE-SENSITIVE hex)
python3 tests/GliderscoreFixtures/webmine/fetch_comp.py 2381887cb81b \
    --out /tmp/gs-comps --audit /tmp/gs-audit.jsonl --name "F3K NI Round 2"
# add --tasks for one pilot's per-round tasks (one extra request)

# offline test suite (no network anywhere)
python3 -m pytest tests/GliderscoreFixtures/webmine/tests -q
```

## Wire-format facts worth remembering (cited, not re-derived)

- **Booleans and absents — corrected against live data, confirmed 2026-08-27
  against CompID 2381887cb81b** (F3K NI Round 2, Haumoana NZ; 41 rows, fixture
  preserved at `tests/fixtures/2381887cb81b_DownloadData.csv`). This supersedes
  the earlier "absent values arrive as `\"0\"`" assumption from the mining doc:
  - Boolean columns `LandingOver75m` (idx 13) and `F5JMotorReStarted` (idx 21)
    arrive as exact-case `'True'`/`'False'` (.NET `Boolean.ToString()`), never
    `'0'`/`'1'`. The parser accepts both spellings; other casings still fail
    loudly.
  - Absent values are **not** uniformly `"0"`: unused `Flight1..4` slots (idx
    17–20) arrive as empty strings (`''` → parsed as null), as does an unset
    `ModelID` (text column; passes through verbatim). Numeric `Data1..7` /
    `Penalty` keep the zero-padded decimal placeholder form (`"0.000"`).
- Duration classes pack minutes/seconds into Data2..Data5
  (`Time1Mins/Time1Secs/Time2Mins/Time2Secs`); Data6 = FlightScoreDeduction,
  Data7 = Landing, Data1 = Laps (unused). F3K's Data1..7 are up-to-seven flight
  times in seconds consumed per task letter. F5K/F5K2024 carry times in
  Flight1..4; **launch heights are NOT in the download CSV** — F5K comps yield
  draw + times only (limitation is stamped into each triage document).
- Pilot numbers are global DB ids (e.g. 75–151), never 1..N per comp.
- Server zip richness beyond the single expected CSV member is unverified per
  comp type: the fetcher prints every entry name and fails loudly on surprises
  (use `--keep-zip` to retain evidence).
- eScoring task scraping was verified against synthetic pages built from the
  documented page shape only; first permitted live run must confirm real-page
  behaviour before bulk use.

## Pipeline position

Gap-target discovery (`index.md` diversity wants) → catalogue mine → fetch +
triage → request full `.mdb` exports for chosen comps (exact CompID + name now
known) → those land through `../extract/extract.py` / `validate.py` unchanged
and feed the replay-and-compare harness as golden fixtures. Triage JSONs are
working artifacts for humans; they do not enter the corpus directly.
