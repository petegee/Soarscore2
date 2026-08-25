# Gliderscore Jet DB — Schema Analysis and Indicative Domain Mapping

Analysis of a legacy **Gliderscore** database export (the application Soarscore aims to
supersede). Purpose: cross-validate our domain model against a real instance of the prior
art. This is an analysis snapshot, not living documentation — the tree is the source of truth.

- **Source file:** `/home/pete/Downloads/GliderScoreDownload.txt` (a Microsoft Access / Jet 4.0
  database saved with a `.txt` extension), 450,560 bytes.
- **Producer version:** Gliderscore DB version `6.78` (from `DBParams`).
- **Contents:** one competition — *"ALES sample comp"*, CompNo 17, `GSCompClass = DurALES`,
  dated 2020-01-01, 10 pilots, 3 rounds × 1 group of score rows.
- **Method:** schema and data extracted programmatically (pure-Python Jet reader, since
  `mdbtools` was unavailable on this machine). Raw extracts retained at
  `/tmp/opencode/gs_schema.json` and `/tmp/opencode/gs_data.json` (not committed).

---

## 1. Table inventory

28 user tables plus Access system tables (`MSysObjects`). Grouped by concern; row counts are
from this particular export, not the schema's capacity.

### Master data

| Table | Rows | Purpose |
|---|---|---|
| `Pilots` | 10 | Person registry: name, country, FAI licence number, contact details, address, club, usual class/model |
| `CountryCodes` | 0 | Country code → name lookup |
| `ModelNames` | 0 | Model registry (`ModelNo`, `ModelName`) |
| `DeviceNames` | 0 | Timer/device names |
| `Roles` | 1 | Role codes (`Plt` = Pilot) |
| `CompSeries` | 0 | Multi-event series/league definitions (`ScoresToCount`, `Decimals`, `SeriesType`) |

### Competition setup

| Table | Rows | Purpose |
|---|---|---|
| `Comps` | 1 | One row per competition: name/date/venue plus global scoring knobs (drop rules, team use, decimals, draw mode, registration/publication flags) |
| `Dur` | 1 | Per-comp config for **duration** classes (F3J/F5J/ALES): target time, landing-table id, ref height + per-metre penalties, lanes, timekeepers, motor-restart option |
| `F3K` | 0 | Per-comp config for F3K |
| `F5B` | 0 | Per-comp config for F5B (target time, points/lap, Watt-min specs) |
| `F5K` | 0 | Per-comp config for F5K (bonus table id, ref height, lane option, min-time-for-height-bonus) |
| `Spd` | 0 | Per-comp config for speed tasks |
| `Dis` | 0 | Per-comp config for distance tasks |
| `DurTargetTimeByRound` | 0 | Target time per round (duration classes) |
| `F3KTaskByRound` | 0 | Task assignment per round (F3K) |
| `F5KTaskandRefHeightByRound` | 0 | Task + ref height per round (F5K) |

### Scoring tables (class-definition data)

| Table | Rows | Purpose |
|---|---|---|
| `LndgNames` → `LndgPoints` | 1 → 10 | Named landing bonus tables: distance → points. Export contains one table ("ALES Landing", `LndgNo` 6): 1 m = 50 pts … 10 m = 5 pts |
| `F5KBonusNames` → `F5KBonusData` | 0 → 0 | Named F5K launch-height bonus tables (metres → points/metre) |

### Event-time records

| Table | Rows | Purpose |
|---|---|---|
| `CompPilots` | 10 | Entry: pilot ↔ competition, class, team, radio frequency (`DrawFreq`), role, start number, retired flag, model |
| `Scores` | 30 | One row per pilot per round/group/re-flight (see §4) |

### Presentation / device only (no domain content)

`AudioSettings` (0), `TimerNames` (0), `TimerSettings` (0), `QRCodeSpecs` (0),
`CustomReportsData` (0), `DBParams` (5 — locale/user/version), `MSysObjects`.

---

## 2. Relationships

The database declares **no enforced foreign keys** (typical of Access applications);
relationships below are inferred from column naming and verified against the data present.

```
Comps (CompNo) ──< CompPilots >── Pilots (PilotNo)
   │                  │
   │                  └──> Roles (Role)                      e.g. 'Plt'
   ├──< Scores            (CompNo + PilotNo → CompPilots;
   │                       denormalises DrawFreq and Model into each row)
   ├──(1:1)< Dur / Spd / Dis / F3K / F5B / F5K               per-family config
   ├──< DurTargetTimeByRound / F3KTaskByRound /
   │    F5KTaskandRefHeightByRound                           per-round schedule
   ├──< CustomReportsData                                    report headers/logos
   └──> CompSeries (SeriesNo)

LndgNames (LndgNo) ──< LndgPoints ;  referenced by Dur.durLndg (= 6 here)
F5KBonusNames (BonusNo) ──< F5KBonusData ;  referenced by F5K.f5kBonusTable
ModelNames (ModelName) ⇢ Pilots.UsualModel / CompPilots.Model / Scores.ModelID  (text join)
TimerNames (TimerNo) ──< TimerSettings.TimerNo
```

Integrity is entirely the application's responsibility — orphaned rows are possible and
nothing prevents them.

---

## 3. Indicative mapping to the Soarscore domain

| Gliderscore | Our domain | Notes |
|---|---|---|
| `Pilots` | **Person** aggregate | Contact/address fields exceed what we hold today; club/country map cleanly |
| `Comps` (+ `CompNo` scoping everywhere) | **Competition** aggregate | Name, date, venue; the rest of the row is class/scoring config (below) |
| `GSCompClass` + the `Dur`/`F3K`/`F5B`/`F5K`/`Spd`/`Dis` config rows | **PublishedClassDefinition** | The central finding — see §5 |
| `Drop1AtRound…Drop5AtRound`, `DropScoreOption`, `F3QDrop6to10` | drop-worst rule (in class definition) | Staged activation by round count |
| `GroupScoreOption`, `GroupScoreDecimals`, `RoundOrTruncate` | normalisation / group-score basis options | |
| `LndgNames`+`LndgPoints`, `F5KBonus*` | landing table / height-bonus metric (class definition) | Already first-class concepts in our model |
| `GroupsPerRound`, `PilotsMax/Min`, `DrawLocked`, `AllowBackToBack`, `DrawMode`, `SeqNo`, `DrawFreq` separation | **Draw** + its fairness inputs | Radio-frequency separation is a draw constraint our model does not have today |
| `F3KTaskByRound` etc. | round task schedule (round/class setup) | Matches task-per-round in the class model |
| `CompPilots` | **Entry** aggregate | Adds role, team assignment, start number, retired flag |
| `Scores` raw half (`Time1Mins/Secs`, `Time2Mins/Secs`, `Laps`, `Landing`, `Penalty`, `FlightScoreDeduction`, `ReFlightNo`, `OriginalRoundNo`) | **score capture** | Maps naturally onto our immutable event log; re-flight linkage included |
| `Scores` computed half (`RawScore`, `NormalisedScore`) | **scoring engine output** | They persist derived state alongside raw input; we project computed scores instead |
| `UseTeams`, `Team`, `NbrForTeamScore`, `OmitFromTeamScore` | ⚠️ team scoring — **no glossary concept** | See §6 |
| `CompSeries`, `MergedComps`, `PrelimCompNo` | ⚠️ series / merged / preliminary-final competitions — **no glossary concepts** | See §6 |
| `IsPublic`, `WasLastUploadPublic`, `CompID` upload identity | Gliderscore's online-upload layer | Out of scope for us |
| Audio/timer/QR/report tables | presentation & device concerns | No domain mapping intended |

---

## 4. Observations on `Scores`

30 rows = 10 pilots × 3 rounds, all `TaskNo = 1` (ALES has a single fixed task).

- Key grain: `(CompNo, TaskNo, RoundNo, GroupNo, ReFlightNo, PilotNo)` with `SeqNo` holding
  fly order within the group.
- `Time1Mins` holds **seconds** despite its name (values up to 500 against a 600 s ALES
  window); `Time2Mins/Secs` presumably serves the second timing leg of F5B.
- `Landing` stores the achieved **distance bucket** (e.g. `3.0`); points come from resolving
  that against `LndgPoints.Distance` for the comp's landing table.
- `RawScore` reflects launch-height penalties already applied: pilot 13 flew 200 s,
  `RawScore` 160 ⇒ 40-point deduction, consistent with `durRefHeight = 200 m` and
  `durPenaltyUpToRefHeight = 0.5/m` (an 80 m over-height). So this sample's ALES variant is
  scored F5J-style on launch height — i.e. the NZ-style ALES behaviour, not pure FAI F3J.
- `Updated` and `ScoresChecked` are app-level bookkeeping/sign-off flags; our trust model has
  no sign-off step, so they have no counterpart by design.
- **Unresolved:** `NormalisedScore` values (winner = 1030 for `RawScore` 330 + 25 landing
  points) do not reproduce from any obvious percentage-of-group-winner or additive formula.
  Reverse-engineering the exact normalisation would require Gliderscore source or more
  populated exports. Do not trust import arithmetic until resolved.

---

## 5. The structural lesson

Gliderscore encodes class variance as **per-family config tables with family-prefixed
columns** (`f3kGroupsPerRound`, `durRefHeight`, `f5kBonusTable`, …) plus a class key string
(`GSCompClass`). Every place their code branches on those tables/columns is a place ours
must instead read generically from a **Competition Class** definition. Conversely, the
existence of these six parallel tables is strong evidence that our one-centralised-class-model
constraint (NFR-1/NFR-2) covers the real variation between FAI families — nothing in the
export needs a concept outside tasks, metrics, penalties, landing/height tables, drop-worst,
group-score basis, and draw inputs.

Two genuine additions their model has that ours lacks as *inputs*: radio-frequency
separation in the draw, and multi-leg timing (two timing windows per flight).

---

## 6. Concept gaps surfaced (require glossary approval — not silently added)

1. **Team scoring** (`UseTeams`, `NbrForTeamScore`, `OmitFromTeamScore`, team protection).
2. **Series/league scoring across competitions** (`CompSeries`, `ScoresToCount`, `SeriesType`).
3. **Merged competitions and preliminary→final competition flow** (`MergedComps`,
   `PrelimCompNo`).

Per house rules none of these enter the glossary without explicit approval; if wanted, they
belong as backlog story stubs first.

Also noted: CLAUDE.md records that no legacy data needs preserving or migrating, so this
analysis is design validation only. Should a Gliderscore importer ever be wanted, it is a
natural backlog story — including resolution of the `NormalisedScore` question above first.
