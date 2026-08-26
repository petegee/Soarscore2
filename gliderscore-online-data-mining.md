# GliderScore online data mining — findings & proposal

**Date:** 2026-08-26 · **Status:** research notes + proposal, nothing implemented in-tree
**Author context:** prototyped live against gliderscore.com; all artifacts currently in `/tmp/opencode/gs/` (transient).

## 1. Why

`tests/GliderscoreFixtures/` holds golden fixtures produced **by hand from real
GliderScore Jet exports** (`extract/README.md`: one-time, developer-run,
offline). Finding suitable real competitions is manual and limited to comps
someone can export. gliderscore.com hosts uploaded scoring data for thousands of
public competitions; if we can enumerate them and pull their scoring data, we get:

- a worldwide catalogue to **gap-hunt against** (pick comps by class/diversity
  targets from `index.md`),
- real draw + raw-score datasets for candidate selection and corroboration,
- CompID + exact Comp Name pairs needed to request full `.mdb` exports.

## 2. Findings

### 2.1 Competition catalogue (public, easy)

`https://gliderscore.com/OnLineScores.aspx` is ASP.NET WebForms but renders the
competition picker as plain HTML `<select name="…TabPanel1$CompList1">`:

- `<option value="…">` = **CompID** (10–15 hex chars, case-sensitive)
- option text = **FullCompName**, format `"YYYY Mmm DD - Title     (Venue)"`

A plain GET returns the default "Last 30 days" range (53 comps on the day).
Replaying the form postback with
`__EVENTTARGET=<CompRange select>` and value `"All competitions"` (carrying
`__VIEWSTATE`/`__EVENTVALIDATION`) returns every competition — **3172 unique
comps** mined on 2026-08-26 (`/tmp/opencode/gs/comps.json|.csv`, script
`mine_gliderscore.py`). Parsing works; note lexical month sorting is wrong —
parse dates with `%Y %b %d`.

NZ-relevant examples found: `F3K NI Round 2` / `F5K NI Round 2` (Haumoana),
`Millennium Cup Rnd 3` (HSL).

### 2.2 What `eScoringInterface.exe` actually is

It is **not** a database downloader. It is a score-entry client for pilots on a
shared PC: enter Comp Name + CompID, then the pilot enters their **internal
pilot number** (the number embedded at the end of each QR-code string on Score
Records). It then opens that pilot's phone-style scoring screen. It has write
access — never submit anything on a live comp.

### 2.3 Server API (recovered by decompiling GliderScore.exe 6.79 U5)

All endpoints are plain HTTP GET, no authentication observed.

| Endpoint | Purpose |
|---|---|
| `scoringdatamanage.aspx?ACTION=ValidateCompID&ID=` | returns `ValidCompID` / `InvalidCompID` |
| `scoringdatadownload.aspx?ACTION=CheckScoresExist&ID=&FR=1&TR=99` | `ScoringDataFound` / `NoScoringDataFound` |
| `scoringdatadownload.aspx?ACTION=CreateScoringDataAsZipArchive&ID=` | `DownloadFileCreationSuccess`; builds server-side zip |
| `scoredownload/<CompID>_DownloadData.zip` | the zip itself |
| `scoringdatadownload.aspx?ACTION=DeleteDownloadFile&ID=&FR=1&TR=99` | `DownloadFileDeleteSuccess` |
| `eScoring.aspx?ID=<CompID>&P=<pilotNo>` | per-pilot HTML screen: name, round/group list, **task description per round** |
| `escoreinterface.aspx` | browser wrapper that redirects into `eScoring.aspx?ID=&P=` |

This is exactly the app's "Download to PC" flow (decompiled
`ScoringOnLine_MOD.ScoringOnLine_*`). Other actions exist on
`scoringdatamanage.aspx` — `DeleteComp`, `MakeScoresZero`,
`ScoresBackup/ScoresRestore*`, `ScoreEntryOpen/Close`, `InsertDataFromZipFile`
— these mutate server state and MUST NOT be called.
`scoringdataupload.aspx` / FTP `scoreupload/` likewise.

### 2.4 Download zip contents

One pipe-delimited UTF-8 CSV `<CompID>_DownloadData.csv`. Column order verified
against the decompiled importer `ScoringOnline_Create_dtDownloadedData()`:

```
0 CompID        1 CompType   2 RoundNo    3 GroupNo     4 ReflightNo
5 PilotNo       6..12 Data1..Data7 (class-dependent raw inputs)
13 LandingOver75m  14 Penalty  15 PilotName  16 ModelID
17..20 Flight1..Flight4 (F3K/F5K flights)  21 F5JMotorReStarted
```

No header row. Scores are **not** included — the PC re-calculates from the raw
inputs (that is good for us: raw inputs are what a converter needs).

Verified end-to-end on `2381887cb81b` (F3K NI Round 2, Haumoana NZ, 2026-05-30):
41 records, 7 pilots (#75 Botherway … #151 Pawson), complete 6-round × 2-group
draw reconstruction, plus task-per-round from the pilot screen
("L1 5max in 7m", "AllUp 3:00\*3", "Big Ladder", …).

### 2.5 Caveats learned the hard way

- **Pilot numbers are global internal DB ids** (75, 82, …151 here), never
  1..N per comp — enumerating small P values always fails.
- **CompID is case-sensitive** (warning string inside the exe).
- `Data1..7` semantics vary by competition type (the PC importer maps them onto
  `Laps/Time1Mins/Time1Secs/.../Landing` per class). Observed F3K values
  (249.500, 400.000, 500.000) look like points rather than seconds — pin the
  per-class mapping before writing converters.
- The zip carries **scoring data only**: no comp-setup tables (landing table,
  target times, drop rules, teams, start numbers mostly empty). Team info is
  not exposed anywhere in this path.

## 3. Fit with the existing fixture pipeline

The golden-fixture contract (`extract/`, `validate.py`, `index.md`) consumes
**whole Jet exports** — every table — because replay-exactness needs setup
tables (landing tables, `DurTargetTimeByRound`, `F3KTaskByRound`, drops).
Web mining cannot replace that: it yields scores + draw + tasks only.

So the realistic roles for web mining:

1. **Discovery** — pick candidate comps by class/diversity gap
   (e.g. F5K2024 with re-flights, F3B multi-task, ALES with drops) instead of
   relying on whoever can export what.
2. **Triage** — CheckScoresExist + downloaded CSV tells us rounds/groups/
   re-flights/raw-input shapes before asking anyone for an export.
3. **Acquisition** — we then know the exact CompID + FullCompName to request a
   full `.mdb` export for (from the CD or Gerry Carter), feeding
   `extract.py` unchanged.
4. Auxiliary corroboration of draw/scores for comps we already hold.

## 4. Proposal

Add developer-run, hand-invoked tooling beside the existing extractor —
`tests/GliderscoreFixtures/webmine/` (same status as `extract/`: never a build
or runtime dependency):

- `mine_catalogue.py` — catalogue mining (prototype exists: `mine_gliderscore.py`);
  emits `comps.json|csv`.
- `fetch_comp.py <CompID> [--out DIR]` — the four-step download sequence above;
  parses the CSV with the verified column order and emits one JSON per comp:
  `{compId, name, pilots[], rounds[{round, group, assignments[], rawData[]}], tasksByRound?}`
  (tasks optionally scraped via `eScoring.aspx?ID=&P=` for one pilot).
- Guardrails baked in:
  - throttled (~1 req/s), tiny volumes;
  - always finish with `DeleteDownloadFile`;
  - hard-coded refusal list of mutating ACTIONs;
  - public catalogue CompIDs only.

Then: choose gap-target comps from `index.md` diversity wants → fetch → triage
→ request full exports → land through the normal `extract.py`/`validate.py`
path as new golden fixtures (new board story; WI numbering per convention).

### Etiquette / permission

This is an undocumented interface on someone else's free service. Keep volumes
polite, and email gerry.carter(at)gliderscore.com — explain Soarscore, ask for
(bulk) sample databases and/or blessing for occasional downloads. His FAQ shows
he responds to feedback; the download pack already ships sample-comp databases.

## 5. Open questions

1. Per-class meaning of `Data1..7` (and whether Flight1..4 vs Data-columns
   differ for F5K2024).
2. Do uploads persist indefinitely? (2024 comps still served — looks yes.)
3. Team-bearing comps: any read-only path? (Probably not; avoid them anyway.)
4. Does `CreateScoringDataAsZipArchive` work while a comp is mid-event being
   auto-uploaded? Assume only finished comps.
