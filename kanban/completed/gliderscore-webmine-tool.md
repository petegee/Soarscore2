# Story — GliderScore webmine tool (read-only online comp acquisition)

**Status:** Completed · **Raised:** 2026-08-26 · **Implementation started:** 2026-08-27 · **Completed:** 2026-08-27

Research basis: `gliderscore-online-data-mining.md` (repo root), **validated against the
GliderScore VB.NET source** (`gliderscore/`, private — not public; see Confidentiality).

## What

Developer-run, hand-invoked Python tooling beside the existing extractor —
`tests/GliderscoreFixtures/webmine/` (same status as `extract/`: never a build or
runtime dependency):

- `gsclient.py` — the **safety kernel**: an HTTP client hard-limited to a read-only
  ACTION allowlist, throttled, with a local append-only request audit log.
- `mine_catalogue.py` — enumerates the public competition picker
  (`OnLineScores.aspx`) into `comps.json|csv`.
- `fetch_comp.py <CompID>` — the app's own download sequence
  (CheckScoresExist → CreateScoringDataAsZipArchive → GET zip → DeleteDownloadFile),
  parses the pipe-delimited CSV, optionally scrapes one pilot's `eScoring.aspx`
  screen for per-round tasks, emits one triage JSON per comp
  (`{compId, name, compType, pilots[], rounds[{round, group, reflight, assignments[]}], tasksByRound?}`).
- README recording etiquette, volumes and the permission state.

Feeds the corpus pipeline: gap-target discovery (`index.md` "Diversity wanted"),
pre-export triage, and CompID+name pairs for requesting full `.mdb` exports that
land through `extract.py`/`validate.py` unchanged. Later, an agent skill wraps the
same tool so agents can run it under the same guardrails.

**Safety contract (non-negotiable):**

1. **Read-only, enforced by allowlist.** The client can only issue these requests,
   verified read-only in source:

   | Request | Source |
   |---|---|
   | GET `OnLineScores.aspx` (+ form postback for range change) | public page |
   | `scoringdatamanage.aspx?ACTION=ValidateCompID` | `ScoringOnLine_MOD.vb` |
   | `scoringdatadownload.aspx?ACTION=CheckScoresExist` | `ScoringOnLine_MOD.vb:223` |
   | `scoringdatadownload.aspx?ACTION=CreateScoringDataAsZipArchive` | `ScoringOnLine_MOD.vb:256` |
   | GET `scoredownload/<CompID>_DownloadData.zip` | `ScoringOnLine_MOD.vb:280` |
   | `scoringdatadownload.aspx?ACTION=DeleteDownloadFile` | `ScoringOnLine_MOD.vb:299` |
   | GET `eScoring.aspx?ID=&P=` (one pilot per comp, for tasks) | observed |

   Everything else on those pages — and *every* action discovered on
   `scoringdataupload.aspx` / FTP `scoreupload/` — is refused by construction.
   Known mutating ACTIONs (refuse-list documentation, not the mechanism):
   `DeleteComp`, `MakeScoresZero`, `RemoveData`, `InsertDataFromZipFile`,
   `ScoreEntryOpen/Close`, `ScoresBackup*`, `ScoresRestore*`,
   `DeleteAllTransferFiles`, all `Upload*`.
2. **Courteous volumes, defendable.** ≥1 s between any two requests (default 2 s),
   one comp fetched per invocation by default, explicit `--i-understand-the-cost`
   style opt-in for any bulk mode, and a machine-readable audit log (timestamp,
   URL, bytes) so total traffic can be evidenced.
3. **Leave no trace server-side.** Always finish with `DeleteDownloadFile`
   (best-effort, failures logged not raised — matches the app's own lenient handling).
4. **Public comps only.** CompIDs come from the public catalogue; PRIVATE comps are
   not visible there by design — do not probe.
5. **Permission gate.** First live use waits on the courtesy email to
   gerry.carter(at)gliderscore.com explaining Soarscore and asking blessing for
   occasional downloads (see mining doc §Etiquette). Offline/unit work needs no gate.

## Why it matters

The replay-and-compare harness (`kanban/in-progress/gliderscore-replay-and-compare-harness.md`)
is only as good as the fixture corpus, and the corpus today grows only via manual
Jet exports. gliderscore.com hosts thousands of completed public comps — a worldwide
catalogue to gap-hunt against, real draw+raw-score datasets for triage, and the exact
CompID/name pairs needed to request full exports. This turns corpus growth from
"whoever can export what" into targeted acquisition.

## Validation of the mining approach (source cross-reference, 2026-08-26)

Assumptions in `gliderscore-online-data-mining.md` checked against the GliderScore
source tree:

- **Download flow & endpoints confirmed.** Exactly the four-step client sequence
  exists (`ScoringOnLine_MOD.vb:219–337`); no auth on these calls beyond optional
  network credentials the app itself supplies.
- **CSV layout confirmed.** Pipe-delimited, headerless, columns 0–21 exactly as
  documented — importer reads `lineArray(0..21)` into the same names
  (`ScoringOnLine_MOD.vb:104–130`; table schema `:583–616`). Nuance: the importer's
  DataTable has a 23rd column `LandingScore` never populated from the file.
- **Open question #1 (per-class meaning of Data1..7) substantially resolved:**
  - Upload aliases them uniformly as `Laps, Time1Mins, Time1Secs, Time2Mins,
    Time2Secs, FlightScoreDeduction, Landing` (`ScoringOnLine_MOD.vb:959–973`),
    and the download import maps them back the same way (`:194–209`) — duration
    classes (F3J/F5J/ALES/Thml) store packed mm/ss splits plus landing.
  - For **F3K**, the server-side scorer treats Data1..Data7 as up-to-seven
    **flight times in seconds** consumed per task letter
    (`App_Code/ShowScores.vb:1144+ CalcRawScoreF3K`). The mining doc's observed
    "249.500, 400.000, 500.000 look like points" were seconds (4:09.5 etc.) —
    mystery closed.
  - For **F5K/F5K2024**, flight times ride in `Flight1..4`; scoring also consumes
    launch heights + bonus tables (`App_Code/F5KCode.vb:210+ CalcRawScoreF5K`) —
    **heights are NOT in the download CSV**, so F5K comps yield draw + times only,
    not full raw inputs. New limitation, recorded here.
- **Decimal/null conventions pinned.** Wire format canonicalises decimal comma to
  "." (`App_Code/VBCode.vb:1632`); absent values are written as `"0"`
  (`getData`, `ScoringOnLine_MOD.vb:2240`).
- **Team data confirmed absent from downloads.** Team/OmitFromTeamScore are uploaded
  to the server DB (`ScoringOnLine_MOD.vb:1233–1234`) but the download CSV does not
  carry them — avoids team-bearing comps as planned.
- **Missing source, suspicion confirmed.** The tree has no code-behinds for
  `OnLineScores.aspx`, `scoringdatamanage/download/upload.aspx` (server side of the
  endpoints) and none for `eScoringInterface.exe`. Present: full PC client
  (`GliderScore_Master/`), display/eScoring helpers (`App_Code/`), `eScoring.aspx`,
  `Score_F3J.aspx`, and a SQL Server `.bak` of the site DB. Consequence: server-side
  behaviour (exact zip contents per comp type) stays empirically verified, not
  source-verified — fetcher should list zip entries and fail loudly on surprises.
- **Setup tables exist server-side** (`TargetTimeByRound`, `F3KTaskByRound`,
  `F5KTaskandRefHeightByRound`, `LandingData` upload writers) but only task text is
  known retrievable read-only (via the pilot screen). Zip contents may therefore be
  richer than the one observed CSV for some comp types — verify during triage.

## Before starting

- [x] Cross-checked against users/NFRs — no conflict: developer-run offline-style
      tooling, no runtime surface, no new domain concepts (converter targets exist
      fixture concepts only).
- [ ] Permission email drafted/sent (gate for first live call, not for code).
- [x] Prototype artifacts in `/tmp/opencode/gs/` are transient — either land the
      prototype miner script into the new location early or accept re-mining.
      (2026-08-27: the dir is gone; accepted re-mining — the miner was written
      from the mining doc's documented page structure, tests run on synthetic
      pages.)
- [x] Confirm CompID case-sensitivity handling (warning string reported inside the
      exe; not found in this source slice — treat IDs as opaque case-sensitive).
      (Enforced as-is in the kernel: `^[0-9a-fA-F]{10,15}$`, never normalised;
      covered by tests.)

## Plan

- **WI-1 — Safety kernel (`gsclient.py`) + tests.** Allowlist-enforcing client
  (URL builders are the only way to make a request; nothing else reachable),
  min-interval throttle, append-only JSONL audit log, `DeleteDownloadFile`
  best-effort finaliser. Property tests (CsCheck-equivalent via pytest + hypothesis
  is fine for Python tooling):
  - *Allowlist closure:* for every ACTION string enumerated from the source tree
    inventory, the client either emits it on the read-only list or refuses — no
    third outcome.
  - *Throttle floor:* over any generated request log, pairwise gaps ≥ configured
    minimum.
- **WI-2 — Catalogue miner.** Port the prototype; parse dates with `%Y %b %d`
  (lexical month sort bug already noted); emit `comps.json|csv` with
  `{compId, name, date, title, venue}`.
- **WI-3 — Comp fetcher.** Four-step sequence; zip-entry listing asserted against
  expectation (single `<CompID>_DownloadData.csv`) before parse; strict column
  parser (22 cols, pipe, "." decimals, "0" nulls) with per-record round/group/
  reflight/pilot keys.
- **WI-4 — Converter to triage JSON.** Generic mapping per CompType (duration split
  fields vs F3K slot-times vs F5K flights); draw reconstruction completeness check;
  optional single-pilot `eScoring.aspx` task scrape. Invariants worth property
  testing: each pilot appears exactly once per (round, group) modulo re-flights;
  record count equals parsed line count; no silent column reordering.
- **WI-5 — README + etiquette notes; agent skill wrapper** (separate small story if
  it grows): exposes `mine-catalogue` / `fetch-comp` commands with the safety
  contract restated, so an agent can operate it without reading the source first.

## Open questions carried forward

1. Exact F3K task-letter set accepted by `CalcRawScoreF3K` (source has A/B/C/D/E/F/G/H/X
   variants — enumerate when writing the converter).
2. Does `CreateScoringDataAsZipArchive` include setup tables for some comp types?
   (verify by listing entries across classes during triage).
3. Only finished comps assumed safe to fetch (mid-event auto-upload interference).
4. Persistence of uploads looks indefinite (2024 comps still served) — recheck when
   bulk selection matters.

## Confidentiality

`gliderscore/` holds Gerry Carter's private source, gitignored, **not to be made
public**. Tooling written here must not embed decompiled/private snippets in any
public artifact; cite behaviour, don't copy code.

## As built (2026-08-27)

All five WIs delivered; the tool stayed Python per this plan (the C#-reuse
question was weighed first: ~0% literal overlap with any existing C# test code —
the seams are JSON artifacts, and a sidecar tool structurally cannot leak into
`src/`). Built with one sub-agent per WI: WI-1 (`gsclient.py` kernel + property
tests for allowlist closure / throttle floor / audit completeness) → WI-2 +
WI-3 in parallel → WI-4 → orchestrator cross-WI offline smoke of
catalogue(last30+all)+fetch(--tasks) through one scripted fake transport, then
README.

- Files: `tests/GliderscoreFixtures/webmine/{gsclient,csvparse,mine_catalogue,
  fetch_comp,triage}.py`, `README.md`, `tests/test_*.py`.
- Tests: 104 passing offline (gsclient 21 · mine_catalogue 8 · csvparse 16 ·
  fetch_comp 24 · triage 35), pytest + hypothesis (`max_examples=50`);
  no live network anywhere.
- Deviations worth knowing: comp postback goes back verbatim including the
  CompList select value; assignments within an (round,group,reflight) bucket are
  canonically sorted rather than file-ordered (wire order preserved in
  `<CompID>_records.json`); `drawGaps` always carries per-pilot base-slot
  summaries, shortfall lines name "missing rounds"; violations withhold only the
  triage artifact — csv + records json stay as evidence, delete finaliser always
  fires.
- Still live-gated: permission email unsent; eScoring scraper proven against
  synthetic pages only; per-comp-type zip richness unverified until first
  permitted runs (`--keep-zip` retains evidence).
- WI-5's agent-skill wrapper split out as `kanban/backlog/webmine-agent-skill-wrapper.md`.
