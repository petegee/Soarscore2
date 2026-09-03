# Story — Team-parity fixtures: validate team results against GliderScore

**Status:** In progress · **Raised:** 2026-09-03 (teams-mvp WI-9 landed 2026-09-02;
this story was opened the moment its own corpus facts made the gap explicit)

## What

Extend the golden-corpus parity claim from individual results to **team
results**, in three moves:

1. **First true GS team-standings parity witness.** The WI-9 team grain
   currently "pins the contract rather than GS's own team ladder" — no
   fixture carries GS team-standings output (`kanban/completed/teams-mvp.md`
   WI-9 corpus facts). f3j-international is already team-mapped (8 populated
   teams, `NbrForTeamScore=3` — the one fixture where the team grain runs);
   give it an independently recomputed GS team-ladder oracle, verbatim per
   `Rpt_Results_TeamResults_MOD.vb` `GetTeamScores` + `Reports_MOD.vb`
   `Resolve_Team_Rank_If_Same_Scores` (retain up to `NbrForTeamScore` eligible
   members by score, sum, rank — the same independent-recompute practice as
   the existing reconstructed-ladder oracles), and compare our derived team
   standings against it.
2. **Grow the corpus with team-bearing comps** (Nbr=3 preferred so the team
   grain can run), targeting the unexercised arms: a comp with real team
   standings in its report, and an `OmitFromTeamScore=true` witness
   (protection-only member — zero corpus or source-extraction sightings
   today).
3. **Amend rule 5's team framing.** `tests/GliderscoreFixtures/extract/validate.py`
   `gap_flags` still labels `UseTeams=true` a "(team scoring concept gap)"
   excusable only via a no-effect `triageJustification`, and
   `tests/GliderscoreFixtures/index.md` §Standing skip reasons still lists
   "team scoring" as a §6 concept gap. Both are stale since teams-mvp: a
   team-bearing fixture should activate *with* declared team-grain
   expectations (compare where `NbrForTeamScore==3`, T1-ledgered where not)
   instead of merely being excused as inert. Series, prelim and merged-prelim
   flags are untouched.

**Out of scope:** widening the classification method beyond
`bestThreeScoreSum` — `NbrForTeamScore ≠ 3` stays ledgered (token `T1`,
`kanban/deferred-decisions.md`); jerilderie-2010 (14 real teams, Nbr=4) and
f3k-sample-comp (Nbr=2) unlock only via a future classification-policy story.
Draw-generation parity with GS's protected draws remains deliberately not a
goal — protection maps through the adapter for the replay gate only
(`teams-mvp.md` decision 8). **Move 3 is not fleshed here** — it is a small,
mechanically-scoped `validate.py` + index.md change and stays tight-tail.

## Why it matters

Teams and protection landed 2026-09-02, but the parity claim's confidence
statement (GOLDEN-COMPARISON-STATE.md) is still individual-results-only. The
one mechanism that made team-bearing comps *interesting* to reject-in-spirit —
teams that actually decide something — is exactly what the corpus cannot yet
witness. Sourcing reality to plan against:

- **Webmine downloads carry no team data** — `Team`/`OmitFromTeamScore` are
  uploaded to the server DB but absent from the download CSV
  (`kanban/completed/gliderscore-webmine-tool.md:101-103`, "avoids
  team-bearing comps as planned"). Zip contents "may be richer — verify
  during triage" is the only online hope; otherwise real team comps come the
  jerilderie route: organiser `.mdb` exports.
- **The NZ master is a dead end** — `Team='0'` on all 168 comps
  (`grow-corpus-nz-master-five-fixtures.md:19`).
- FAI-style international comps standardise on three counting pilots, so
  Nbr=3 candidates are the natural hunt target.

## Before starting

- [x] Cross-check against users/NFRs and the rules corpus — team
      classification fidelity already settled in teams-mvp (both C.15.6.2
      methods documented; MVP implements score-sum).
- [x] Decide the oracle strategy per fixture: GS report transcript where a
      real comp provides one; independently recomputed team ladder (private
      source available) otherwise. State the verification for each. —
      **Settled in the Refined plan (WI-1B/WI-1C): transcript preferred but
      non-blocking; the recomputed ladder is the required artifact and is
      verified against the transcript when one exists.**
- [ ] Rule-5 amendment touches the `extract/` tooling contract — update
      `tests/GliderscoreFixtures/extract/README.md` in the same change; the
      amendment must not weaken the series/prelim/merged guards. (Move 3.)
- [x] If any live webmine call is needed (catalogue hunting for team-bearing
      candidates, even though downloads lack team fields), check the
      permission-email gate first (`gliderscore-webmine-tool.md` Before
      starting). **Move 2 WI-2A handles this; the gate is still unticked
      (2026-09-03).** — *WI-2A (2026-09-03): gate verified unticked →
      permission email drafted for Pete to send (verbatim in the Hunt log);
      all live calls deferred until Gerry blesses it. Offline-only analysis
      recorded in the Hunt log; nothing actionable offline.*

## Refined plan

Implementation order: **all of Move 1 first** (it builds the oracle machinery
Move 2's fixtures reuse), then Move 2 as acquisition allows. Move 3 is
independent and small — it can land any time, does not depend on this plan.

Sequencing within Move 1: WI-1A (read/spec) → WI-1B (transcript, parallel) →
WI-1C (recomputed oracle, needs 1A) → WI-1D (harness, needs 1C) → WI-1E
(scenario, needs 1D) → WI-1F (state docs, needs 1D). Move 2: WI-2A (hunt,
permission-gated) → WI-2B (triage arrivals) → WI-2C / WI-2D (curation, each
needs WI-1D) → WI-2E (reconcile). Take the story into `in-progress/` before
code (board rule 3).

### Handoff notes (read this before any sub-agent task)

These rules apply to every WI below. Each WI names its own Read/Do/Verify/
Done-when; the shared rules live here so they need not be restated:

- **Repository grammar.** `tests/GliderscoreFixtures/` holds one directory per
  comp; `index.md` is the manifest (a slug line containing "skipped" is
  skip-listed; everything else is active and the harness replays it).
  `tests/GliderscoreFixtures/extract/` and `webmine/` are developer tooling —
  never a build/runtime dependency. The replay/compare harness lives in
  `tests/Soarscore.Acceptance.Tests/Support/Gliderscore/`, driven by the
  `@gliderscore` feature.
- **Never touch** beyond the WI's named files. In particular: do not edit
  `validate.py` or index.md's §Standing skip reasons (Move 3); do not edit
  anything under `docs/`; do not re-run `extract.py` against a source that is
  already extracted (the committed extractions are inputs of record); never
  soften a guard to make a test pass.
- **On every new or changed fixture** run PII sweep first (all Pilots contact
  columns empty; names are GS's ZZ-prefixed test data) and record the sweep
  in the fixture's `provenance.json` notes.
- **Reporting contract.** End each task with a short report: what was
  produced (paths), what the verify commands printed (counts), any
  assumption made, and anything needing human input. Do not mark a WI done
  when its verify step failed.
- **Verification commands (shared):**
  - Corpus validation: run from `tests/GliderscoreFixtures/` → `python3 extract/validate.py <slug> --index index.md`, and `python3 extract/validate.py --self-test` after any validator change (Move 3 only).
  - Harness: `SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests --filter "Category=gliderscore"` (fast loop) and the same with `SOARSCORE_TEST_STORE=postgres` (needs Docker) before claiming a backend; drop the filter to run the whole acceptance suite.
  - Build hygiene: `dotnet build Soarscore.sln` (repo root) must stay 0 warnings.
  - Webmine offline tests: `python3 -m pytest tests/GliderscoreFixtures/webmine/tests -q`.

---

### Move 1 — f3j-international GS team-ladder oracle + comparison

The gap, precisely: the team grain at
`tests/Soarscore.Acceptance.Tests/Support/Gliderscore/Comparator.cs:836-1063`
compares our `/competition-team-result` standings against the **MVP contract
recomputed over the oracle-verified individual result** — and its own comment
(`:847-852`) records that the corpus "carries no GliderScore team-standings
output". Move 1 supplies that output as an independent oracle and asserts our
derived standings against it.

GS's algorithm in one screen (this IS the oracle spec — transcribe it
faithfully, citations to the private VB source):

- Input rows: `dtCompResults_Create(varCompNo, …)` with scope All, restricted
  "Original", drops active — the SAME source and the same per-pilot `Score`
  column the individual Overall Results ladder uses (`Rpt_Results_TeamResults_MOD.vb:168`
  vs `Rpt_Results_Overall_MOD.vb:221`; `dtCompResults_Create` is defined at
  `Rpt_Results_Overall_MOD.vb:2317` — confirmed; per-pilot `Score` there = Σ
  `NormalisedScore` over the rounds less |`Penalty`|, floored at 0
  (`:2690-2712`), drops applied (`:2735-2739`), then rounded and ranked with
  the `=n` convention by `dtCompResults_FillPilotRankAndPcnt`,
  `Rpt_Results_Calculations_MOD.vb:730-804`). So the per-pilot inputs are exactly the
  final aggregate scores and placings the corpus already proved against GS at
  the individual grains — team parity reuses those, it does not recompute them.
- `GetTeamScores` (`Rpt_Results_TeamResults_MOD.vb:303-470`): captures
  `IndividualRank` = pilot `Rank` with any `=` stripped (`:319-327`); reads
  `NbrForTeamScore` from `Comps` (`:330-333`); team list = `SELECT DISTINCT
  Team FROM CompPilots … ORDER BY Team`, all pilots, no `Team > 0` filter
  (`:336-340`); removes rows with `CompPilots.OmitFromTeamScore=true` from the
  table entirely (`:351-365` — an omitted pilot appears nowhere in the report,
  and a team stripped bare still ranks, on TeamScore 0); sorts the view
  `Team, Score DESC` (`:347-349`) and trims from the tail while
  `count > NbrForTeamScore` (`:368-382`) — the removed row is the view's LAST
  row (`:375-377`), and ADO.NET keeps fully-equal sort keys in table row order
  (duplicates land in satellite trees ordered by rowID — referencesource
  `System.Data/System/Data/Selection.cs:356-391`), so a tie at the cut falls
  on the equal-`Score` member latest in the dtCompResults row order — the
  order `dtCompResults_Create` filled the table (JET join, no ORDER BY,
  `Rpt_Results_Overall_MOD.vb:2368-2373`), not the individual-rank order;
  `TeamScore = Σ` retained members' `Score` (`:383-387`), written onto the
  team row (`:389`) and back onto each retained member row (`:391-394`).
- Rank pass (`:399-453`), all classes: sort the team list `TeamScore DESC`
  (`:410-411`); rank = the team's 1-based position in that list — leader 1, a
  distinct score takes its own list position (numbers skipped after ties), a
  tie shows `=n` on every tied row with n the first-of-group position
  (`:413-441`); `Percent = 100 × TeamScore / TopScore` is computed here
  (`:421`, `:426`, `:437`), and Rank/TeamScore/Percent/HiddenRanking are
  written back onto every member row (`:443-453`).
- Class overlay, **F3J/F5J only** — this fixture IS F3J (`:464-468`):
  `Resolve_Team_Rank_If_Same_Scores` (`Reports_MOD.vb:157-244`) computes per
  team `SumOfIndividualRankings` (Σ retained members' `IndividualRank`, i.e.
  individual place with any `=` stripped — `:182`, `:192`),
  `HighestTeamPilotPlacing` (min — `:183`, `:193-197`), `TeamRawScore` (Σ
  retained members' `RawScore` — `:188`, `:198`; `RawScore` is the PRE-drop
  total — the drop passes update only `Score`,
  `Rpt_Results_Calculations_MOD.vb:471/538` and `:555/636`); sorts
  `TeamScore DESC, SumOfIndividualRankings ASC, HighestTeamPilotPlacing ASC,
  TeamRawScore DESC` (`Reports_MOD.vb:203`); assigns ranks in that order,
  sharing `=n` when the first THREE keys tie (`:212-232`); writes
  `FinalRanking` → Rank and `FinalOrder` → HiddenRanking into every member row
  (`:234-242`) — FinalOrder is display order, FinalRanking the displayed rank.

**WI-1A parse contract — Team Results SaveAsFile CSV.** Written by the
`SaveAsFile` case (`Rpt_Results_TeamResults_MOD.vb:248-298`): three
`WriteLine`s — report heading, column headings, data (`:289-293`), UTF-8 with
BOM (`System.Text.Encoding.UTF8`, `:289` — confirmed against the transcript
already dropped in the fixture dir, `F3J International - Team Results.csv`).

- Heading lines (`SaveReportGetRptHeading`, `:1616-1634`):
  `<CompName> - Team Results  [<venue> <date>]`, then
  `<Drop worst Task score>` / `<Drop worst Round score>` (translated;
  DropScoreOption is read inside `dtCompResults_Create`,
  `Rpt_Results_Overall_MOD.vb:2570`), then `www.GliderScore.com`. Each carries
  its own CRLF and `WriteLine` adds another, so there is exactly ONE blank
  line between `www.GliderScore.com` and the column-heading line; the data
  blob itself ends in CRLF, so the file also ends with a blank line.
- Column headings (`SaveReportGetColHeadings`, `:1636-1740`), in order:
  `Rank, Team|Ctry, TeamScore[, Pcnt,] Name[, Ctry,][ <Regn>,][ Club,][ Pilot
  #,][ Class,] Score[, Raw Score,] Rnd1[, Rnd2 … RndN][, Plty]`. `Team` vs
  `Ctry` follows the international-prompt answer (`:1641-1645`; WI-1B answers
  No ⇒ `Team`, team numbers on data rows `:1758-1759`). **Pcnt is NOT decided
  by the comp's decimals setting** — it appears iff the user setting
  `My.Settings.Report_Show_Percent` is on (`:1649`, value = the team's percent
  of the top team printed fixed 2-dp `:1769`; shipping default True —
  `app.config:207-209`, toggled by the Team Results report-selection dialog
  `ReportsSelections_TeamResults.vb:555`), so Pete's GS options decide; the
  parser must accept its presence or absence. Other optional columns: per-pilot
  `Ctry` iff `Report_Show_Country` and not international (`:1653`, default
  True `app.config:358-360`); `FAI No|RegnNo|FAI_ID` iff `varUseRegistration`
  (`:1655-1661`); `Club` iff `Report_Show_Club` (`:1663`, default True
  `app.config:370-372`); `Pilot #` iff `varUseStartNo` and
  `Report_Show_PilotNbrs` (`:1664`); `Class` iff `varUseClasses` and
  `Report_Show_PilotClass` (`:1665`); `Raw Score` iff `Report_Show_RawScore`
  (`:1669`, default True `app.config:210-212`); `Plty` iff the comp has
  penalties (`:1734`). Round cells run 1..ToRound, one per task — F3J
  single-task ⇒ `Rnd1…Rnd16` (`:1675-1691`).
- One CSV row = one RETAINED (counting) pilot: trimmed and omit-filtered
  members were removed from the table (`:363`, `:377`) and never appear; every
  row repeats its team's Rank and TeamScore (`:449-453`). Fields
  (`:1752-1869`): `Rank` quoted (`:1756`); `Team` unquoted (`:1759`);
  `TeamScore` unquoted, raw Double ToString in current culture (`:1766`);
  Pcnt forced 2-dp (`:1769`); Name quoted `LASTNAME, Firstname`, any
  `(class)` suffix preserved (`:1772-1780`); `Score`/`Raw Score` raw Double
  ToString, no forced decimals (`:1814-1816`); round cells (`:1827-1845`):
  blank/DBNull ⇒ `0`, dropped zero ⇒ `*0`, F5J motor-restart zero ⇒
  `Motor 0`, dropped non-zero ⇒ `*<abs value>` **with no trailing separator —
  the next field glues on** (`:1841-1842`), else the number; Penalty appended
  unquoted when present (`:1867`). `GetNormScoreDecimals` (`:162`) affects
  print layout only, never the CSV.
- Member-row ordering: `dvCompResults.Sort = "Rank, Score DESC"` (`:1749`) —
  `Rank` is a STRING, so rows order lexicographically (`=…` sorts after bare
  digits; `10` before `2`); within one rank, members by `Score DESC`. A
  transcript parser must not assume numeric rank order.
- Separator: `ciLs` = culture ListSeparator (`GlobalVariables_MOD.vb:28`) —
  `,` on EN locales; every cell is followed by it, so lines end with a
  trailing comma unless a Plty value closes the line (`:1734`, `:1867`).
- What a parser must tolerate: the UTF-8 BOM; the blank line after
  `www.GliderScore.com` and the trailing blank line at EOF; the optional
  columns above; quoted `=n` rank strings on every tied row; fields that are
  a bare space (Club with no value prints ` , `); `*<n>`/`*0`/`Motor 0`
  round cells; glued `*<n><next-field>` when a dropped round had a non-zero
  score — the transcript shows both `*986.41000` (glued) and `*0` — match
  round cells against the fixture's known per-round values rather than
  comma-splitting blindly.
- Checkpoint-relevant facts, restated from source: (a) the trim removes the
  view's last row (`:375-377`) and the `Team, Score DESC` view IS stable on
  the dtCompResults row order (framework evidence above), so a cut-boundary
  tie drops the equal-`Score` member latest in fill order and retains the
  earliest — row-order-dependent; the transcript from WI-1B wins on any
  disagreement. (b) There is no `Team > 0` sentinel anywhere — team list
  `:336-340`, score SELECT `Rpt_Results_Overall_MOD.vb:2368-2373` — so a
  Team=0 pilot would surface as team `0` in the CSV; f3j-international has
  none, do not fabricate one.

**Checkpoints to resolve during WI-1C** (none are expected to bite this
fixture; each must be stated, not skipped):

1. **Boundary member-tie.** A team's 3rd/4th members with equal `Score`:
   GS's `Team, Score DESC` view is stable on source row order, so the retained
   set is source-order-dependent. Record the row-order assumption used;
   reconcile against the transcript from WI-1B. If the two disagree, the
   transcript wins.
2. **Team-0 sentinel.** GS queries `SELECT DISTINCT Team …` with no `Team > 0`
   filter; f3j-international has every pilot in Teams 1–8 (sizes
   4,3,4,4,4,3,4,4 — `competition.json:183`), so no sentinel exists. Do not
   fabricate one.
3. **'='-sharing convention.** GS displays shared team places as `=n` on every
   tied row. Our `Placing` uses the shared-place convention over the same
   three rungs (`Total DESC → PlacingSum ASC → BestIndividualPlacing ASC`),
   so place-identity and place-group membership compare directly.
4. **The fourth key is ORDER-only.** GS sorts ties' display order by
   `TeamRawScore DESC`; our declared order uses team name. Comparators must
   compare **places and place-group membership**, never the display order
   inside a shared place — that is a designed method difference, not a
   defect. A tie that actually fires surfaces as a place mismatch → stop and
   escalate (see WI-1D guard).
5. **Decimals.** `GetTeamScores` sums `Score` as doubles with no extra
   rounding; every `Score` is already the oracle-verified final value, so the
   team totals compare exact-decimal on both sides.

**WI-1A — Source transcription (read-only; no production code).**
Read the four anchors above cover-to-cover and write the algorithm into this
story's "GS's algorithm in one screen" block (it is transcribed there already
for the sub-agent — extend/correct it from the source if any reading changes
details). Confirm the CSV-writer contract at
`Rpt_Results_TeamResults_MOD.vb:1616-1878` (SaveAsFile: heading, column
headings `Rank, Team, TeamScore[, Pcnt,] Name, Score…`, per-round cells;
`dvCompResults.Sort = "Rank, Score DESC"` at `:1749`) so a transcript parse
knows what to expect.
**Done when:** the spec block is confirmed/corrected against source; the
parse contract for a Team Results CSV is recorded here (columns, one row per
PILOT with the team's Rank/TeamScore repeated on each member row).
**Do not write any code in this WI.**

**WI-1B — GS Team Results transcript (human-assisted; parallel with WI-1C).**
Ask Pete to run GliderScore's report on f3j-international and save it as CSV:
Report → Team Results → Save As File. Exact asks:
- Verify this is an International competition? **answer No** (Team numbers in
  the CSV, not country codes — the prompt is `Rpt_Results_TeamResults_MOD.vb:114-126`).
- FromRound 1, ToRound 16 (all scored rounds).
Deliverable: `tests/GliderscoreFixtures/f3j-international/team-results-transcript.csv`,
with its sha256 recorded. If Pete cannot produce it, proceed with WI-1C alone
and record "no transcript" in the oracle notes (this matches how the corpus
handles transcript-less fixtures — jerilderie-2010's reconstructed ladder).
This WI must never block WI-1C/1D.

> **WI-1B outcome (2026-09-03): DONE.** Pete produced the transcript same-day;
> committed as `tests/GliderscoreFixtures/f3j-international/team-results-transcript.csv`,
> sha256 `98df694c22c906cf7808487cfafb6378fe725a0c6113515e20169ebb60f980d9`
> (recorded in the fixture's `provenance.json` notes with the PII sweep). Both
> prompt asks verified from the artifact itself: Rank/Team columns carry team
> numbers 1–8 (International = No), and the sheet spans `Rnd1`–`Rnd16` (all
> scored rounds). Shape matches the WI-1A parse contract: 3 heading lines +
> blank + column headings + 24 data rows (8 teams × 3 retained) + trailing
> blank; `Pcnt`, `Ctry`, `Club` present (`Report_Show_Percent` default True);
> glued drop-sentinel fields confirmed live (`*986.41000`).

**WI-1C — Recompute the ladder and author `expected-teams.json`.**
Compute the 8-team ladder by transcribing the spec above over the fixture's
PROVEN individual data (read the final `Score`/placing per pilot from the
already-committed oracle files — `expected-result.json` ranks,
`expected-scores.json` cells — never from the engine, and never re-derive an
individual score). Produce `tests/GliderscoreFixtures/f3j-international/expected-teams.json`:

```json
{
  "source": "reconstructed-gs-team-ladder",
  "verifiedAgainst": null,
  "keyFormat": "teamNumber",
  "notes": [
    "Transcribed verbatim from GetTeamScores (Rpt_Results_TeamResults_MOD.vb:303-468) "
    "and Resolve_Team_Rank_If_Same_Scores (Reports_MOD.vb:157-244); inputs are the "
    "oracle-verified individual final scores/placings.",
    "<each open checkpoint from the story, with the value chosen and why>",
    "<verification: hand-run ladder sample covering top/middle/bottom teams; "
    "transcript agreement when WI-1B produced one>"
  ],
  "standings": [
    { "team": 1,  "rank": "1",  "teamScore": 1234.5, "countedPilots": [62, 49, 56] }
  ]
}
```

`rank` is GS's display string (`"1"`, `"=2"`, …); `countedPilots` are the
retained member PilotNos in trim order; `teamScore` exact-decimal.
**Verify:** (a) independent hand-ladder spot-check on ≥3 teams (the tallest,
middle and lowest scoring) reproduces your numbers; (b) when WI-1B produced a
transcript, all 8 (team, rank, teamScore) agree — any disagreement is a
transcription bug in WI-1C, not a transcript bug; fix the recompute.
**Done when:** the file is committed, notes state the checkpoints and the
verification evidence, and the individual oracle files are untouched.

**WI-1D — Harness: load and compare the ladder oracle.**
- `FixtureModels.cs`: add `ExpectedTeamsFile`/`TeamStandingOracle`
  (`int Team`, `string Rank`, `decimal TeamScore`, `IReadOnlyList<long>
  CountedPilots`) so the fixture loads it.
- `FixtureLoader.cs`: load `expected-teams.json` **optionally** (absent ⇒
  null oracle); surface it on `GliderscoreFixture` without making the corpus
  loader demand it for team-less fixtures.
- `Comparator.cs`: new stage `CompareTeamLadderGrainAsync`, run only when the
  existing overlap condition holds (`:869-871` — `UseTeams`, `NbrForTeamScore
  == 3`, populated teams) **and** the oracle is present. Asserts, per oracle
  team (keyed by team number):
  - our standings count == oracle count;
  - our derived `Total` == oracle `teamScore` (exact-decimal);
  - our shared `Placing` agrees with oracle `rank` (numeric part; `=n` groups
    must match membership exactly — every team at place n on our side is in
    the oracle's n/`=n` group);
  - our contributors' pilot numbers == oracle `countedPilots` as a set.
  Mismatches ride the existing `TeamMismatch` diff-table path
  (`Comparator.cs:198-209`). Update the two doc comments that say "no
  GliderScore team-standings oracle in the corpus" (`:143-152`, `:838-856`).
- **Guard:** when overlap holds and `expected-teams.json` is missing, THROW
  (a team-bearing overlap fixture without a ladder oracle is a curation bug —
  mirror `EnsureOracleCoverage` at `Comparator.cs:1151-1164`). When a place-
  or total-mismatch fires on a REAL tie, do NOT ledger it under `T1` (T1
  means `NbrForTeamScore ≠ 3`) and do not paint over it: stop, verify the
  transcription, and if that is clean escalate to the owner per house rule 2
  — a genuine fourth-rung/name tie-order divergence is a new semantic
  decision, not a ledger entry.
**Verify:** `@gliderscore` replay on sqlite — f3j-international's ladder
grain reports 8 standings compared, zero mismatches; the rest of the suite
unchanged. **Done when:** the guard throws when the oracle is deleted (prove
it with a scratch copy, then restore), the 8 standings compare clean, and the
build is 0 warnings.

**WI-1E — Scenario asserts the ladder.**
In `Features/ReplayingAGliderscoreFixture.feature`, pin the ladder in the
f3j-international scenario ("… and the GS team ladder is reproduced exactly",
naming the 8 standings: team number, rank, score, counted pilots), updating
the scenario's expected counts. The harness's own self-checks (replay
determinism, conservation, ledger strictness) stay untouched.
**Verify/Done when:** the scenario is green on sqlite AND postgres.

**WI-1F — State documentation.**
- `GOLDEN-COMPARISON-STATE.md`: extend Confidence (current text `:34-49`) —
  team results now carry a GS-derived oracle witness on f3j-international
  (ladder grain, exact-decimal); restate that the protection-only arm
  (`OmitFromTeamScore=true`) is still unwitnessed (ties the WI-2D hunt).
  Keep the point-in-time snapshot discipline (date the block).
- f3j-international's index.md bullet: add a clause naming the team ladder
  oracle and its verification.
**Done when:** both docs reflect exactly what the harness now asserts, and
nothing promises more than the corpus holds.

---

### Move 2 — Grow the corpus with team-bearing comps

Goal: at least one NEW active team-bearing fixture beyond f3j-international,
ideally capturing the two unexercised arms — a comp whose report carries real
team standings (a second Nbr=3 overlap witness), and an
`OmitFromTeamScore=true` member (the protection-only arm of decision 8,
unexercised by any replay today — `teams-mvp.md:471-473`).

Sourcing reality (bind every WI below): webmine downloads carry **no team
columns** (`gliderscore-webmine-tool.md:101-103`), so online data can only
*shortlist* candidates whose `.mdb` must then come the organiser/export route;
the NZ master has `Team='0'` on all 168 comps. The one already-sourced
team-bearing candidate besides f3j-international is f3b-international —
extracted with `UseTeams=true` but skip-listed for multi-task rounds, a
hard `unsupportedRoundComposition` draw rejection, not usable here.

**WI-2A — The hunt (permission-gated; produces a shortlist, not a fixture).**
1. Gate check: the webmine courtesy email is still unticked
   (`gliderscore-webmine-tool.md` Before starting, item 2; `webmine/README.md`
   §Permission state). If it is unticked, draft the email to
   gerry.carter(at)gliderscore.com for Pete to send, and do NOT make live
   calls. All offline analysis below is gate-free.
2. Offline first: inspect `tests/GliderscoreFixtures/webmine/` for any
   already-downloaded catalogue/triage artifacts (none are committed as of
   2026-09-03 — if none, record that).
3. Live path (only after the gate clears): `mine_catalogue.py --range all`
   per `webmine/README.md` usage; filter the catalogue for FAI-style
   international/team events ("International", "Team", F3J/F5J naming); pick
   2–4 candidates and run `fetch_comp.py` on each with the zip-entry guard
   to (a) confirm the triage shape and (b) test the "zip contents may be
   richer than the observed CSV" flag for team fields
   (`gliderscore-webmine-tool.md:111-114`). If any zip carries team columns,
   that changes move-2 economics — record it loudly in the story.
   `NbrForTeamScore` is NOT visible in the catalogue or CSV; it is confirmed
   only at export/triage (record that: the shortlist is by class/name, the
   Nbr=3 filter applies at WI-2B).
4. Deliverable: a shortlist of CompID+name pairs + the records of the grep,
   recorded in this story under a **Hunt log** note, with the zip-entry
   finding. Hand the shortlist to Pete for `.mdb` export requests.
**Done when:** the gate state is recorded, the catalogue analysis (or its
offline-only substitute) is on record, and a shortlist exists or an explicit
"nothing actionable offline" conclusion is written.

**WI-2B — Triage of each arriving export / zip.**
For every `.mdb` (or richer zip) that arrives:
1. PII sweep (mandatory, `grow-gliderscore-fixture-corpus.md` Before
   starting), then `python3 extract.py <file> --out <scratch>` per
   `extract/README.md`.
2. Profile the comp: class, shape, `UseTeams` / `UseTeamProtection` /
   `NbrForTeamScore`, `CompPilots.Team` distribution, `OmitFromTeamScore`
   counts (`sources/…-extract/CompPilots.json` sweeps for the arriving
   export). Classify into: **Nbr=3 overlap candidate** (target #1), Nbr≠3
   (T1-ledgered — not this story's prize), `UseTeams=false` (inert), or
   skip-listed for an unrelated reason (record, don't force).
3. Corruption/PII/cleanliness findings go into the story; nothing is curated
   in this WI.
**Done when:** each arrival is classified and recorded, with the exact
numbers that placed it in its bucket.

**WI-2C — Curate the second Nbr=3 team-bearing fixture.**
For the accepted overlap candidate from WI-2B, run the complete curation
pipeline exactly as `grow-gliderscore-fixture-corpus.md` WI-3/4/8 did, plus
this story's new team-oracle requirement:
1. Split at curation (one CompNo per fixture — rule 4); author
   `competition.json` (with honest `triageJustification`, following
   f3j-international's shape), `entries.json`, `scores-raw.json`,
   `expected-scores.json`, `expected-result.json`, `provenance.json` (PII
   note mandatory), and hand-author `class-definition.json` per
   `extract/README.md` "Adding a fixture".
2. Author `expected-teams.json` following **WI-1C's** spec and verification
   discipline (recompute over the fixture's own proven individual oracle +
   transcript when Pete can run GS on it — the WI-1B ask applies to any new
   Nbr=3 comp). This file is REQUIRED for a team-bearing overlap fixture
   (WI-1D guard throws otherwise).
3. `python3 extract/validate.py <slug> --index index.md` PASS; add the
   index.md active bullet naming the team witness.
4. Add the feature scenario (three grains + conservation + team ladder);
   replay on sqlite then postgres; triage every difference (importer/authoring
   bug · engine defect · intentional divergence); fix the first two at source;
   **only an intentional divergence is ledgered**, and only under a token
   whose meaning fits (T1 is Nbr≠3 — a different tie-order behaviour is NOT
   T1; escalate rather than stretch).
**Done when:** validate PASS; the ladder grain reproduces the oracle cleanly
on both stores; the ledger, if any, cites real divergence IDs.
**Do not:** activate a comp whose individual or team oracle you could not
verify; skip the PII sweep; reuse another fixture's `class-definition.json`
without re-deriving it from this comp's `competition.json`.

**WI-2D — The `OmitFromTeamScore=true` witness.**
This arm (decision 8's protection-only mapping — a member drawn alongside
countrymen who never contributes) has zero sightings in the corpus or its
source extraction (`teams-mvp.md:471-473`; re-verify with a sweep of every
committed `CompPilots.json` for `OmitFromTeamScore: true`). The witness must
come from a real event that used the flag — ask Pete (his events, his club,
international contacts) while WI-2A's shortlist request is in flight. When one
lands, it is curated exactly like WI-2C (its value is the ladder grain proving
the non-contributor is excluded on BOTH sides — GS filters the omit rows at
`Rpt_Results_TeamResults_MOD.vb:341-346`, our classification excludes
`Contributes=false` members), and the `OmitFromTeamScore` value in the fixture
makes the `expected-teams.json` `countedPilots` exclude that member.
If, at the end of this story's hunt, no such comp exists: record the fact and
the requests made, and open a backlog stub — the arm stays implemented-but-
unexercised, and unsilencing it later is a data problem, not a code one.
**Done when:** either the witness is curated and green on both stores, or the
failed-hunt trail is recorded in the story and a stub sits in `kanban/backlog/`.

**WI-2E — Corpus-state reconciliation.**
Re-run the corpus-wide team-field sweep (every `extract/CompetitionFile`
triage + every `CompPilots.json`): state the current truth on
`UseTeams`/`UseTeamProtection`/`NbrForTeamScore`/`Team` occupancy and
`OmitFromTeamScore`. Update `index.md` *Diversity wanted* (witnessed / still
open) to name whatever Move 2 landed and the Omit arm's standing. Do NOT
touch validate.py or the §Standing skip reasons — that is Move 3.
**Done when:** the index reflects the corpus's actual team coverage, and the
facts agree with the fixtures on disk.

**Terminal state for Move 2 if no source lands:** the story closes Move 2 as
"hunt recorded, acquisition pending" — WI-2A's shortlist + WI-2B's record +
WI-2D's stub are the deliverables, and the team grain remains proven on the
one Nbr=3 witness (f3j-international) that Move 1 delivered. Do not invent a
witness, synthesize an entry, or widen the transient rule to manufacture
one.

### Hunt log (WI-2A — 2026-09-03)

**Gate state: UNTICKED — no live calls made.** Evidence:

- `tests/GliderscoreFixtures/webmine/README.md` §Permission state:
  "**No live use yet.** First live call waits on the courtesy email to
  gerry.carter(at)gliderscore.com…".
- `kanban/completed/gliderscore-webmine-tool.md` Before starting, item 2:
  `- [ ] Permission email drafted/sent (gate for first live call, not for
  code)`.
- This story's fourth Before-starting checkbox said the same until this WI
  ticked it (see annotation above).

Per the gate, zero network operations were performed; everything below is
offline.

**Offline inspection of `tests/GliderscoreFixtures/webmine/` — no downloaded
artifacts exist (verified, not assumed).** Full tree: five tool modules
(`gsclient.py`, `mine_catalogue.py`, `fetch_comp.py`, `csvparse.py`,
`triage.py`), `README.md`, and `tests/` (conftest + five test files;
`__pycache__` dirs transient). Zero catalogue/triage outputs anywhere: no
`comps.json`/`comps.csv`, no `<CompID>_records.json`, no
`<CompID>_triage.json`, no `.zip`, no `.jsonl` audit logs — exactly as the
story expected for 2026-09-03. The tree's only data file is
`tests/GliderscoreFixtures/webmine/tests/fixtures/2381887cb81b_DownloadData.csv`
(41 lines) — the preserved wire-format download CSV for F3K NI Round 2
(README §Wire-format facts). It is one comp's score rows in the 22-column
download format; grep finds no `Team`/`OmitFromTeamScore`/catalogue tokens in
it — a real artifact confirming the "downloads carry no team columns" claim
first-hand. It is not a catalogue; nothing can be shortlisted from it.
`tests/GliderscoreFixtures/webmine/tests/` embeds no real catalogue excerpts
either: `test_mine_catalogue.py`'s HTML fixtures are explicitly SYNTHETIC,
hand-built from the documented OnLineScores.aspx page shape (its header note
says so) — no genuine catalogue text exists offline.

**Offline candidate brainstorm (no network) — what the corpus supports:**

- Webmine downloads carry no team columns — source-confirmed
  (`gliderscore-webmine-tool.md:101-103`) and now visible on the one real
  preserved download CSV above.
- NZ master: `Team='0'` on all 168 comps
  (`grow-corpus-nz-master-five-fixtures.md:19`) — dead end.
- f3b-international: `UseTeams=true` but skip-listed for multi-task rounds
  (a hard `unsupportedRoundComposition` draw rejection) — unusable here
  regardless of team interest.
- FAI-style international comps standardise on three counting pilots, so
  Nbr=3 is the right hunt target — **but `NbrForTeamScore` is NOT visible in
  the catalogue or the CSV**; it is confirmed only at export/triage. The
  shortlist can therefore only be by class/name (F3J/F5J, "International"/
  "Team" naming); the Nbr=3 filter applies at WI-2B.
- Whether the zip is richer than the observed CSV for team fields
  (`gliderscore-webmine-tool.md:111-114`) is only testable by fetching a real
  download — gated.

**Conclusion: nothing actionable offline — the shortlist requires the live
path after the gate clears** (`mine_catalogue.py --range all`, then 2–4
`fetch_comp.py` runs per WI-2A step 3, zip-entry guard on). No CompID+name
pairs can be honestly produced without the live catalogue; none are
fabricated here. The true prize (Nbr=3 confirmation and real team columns)
additionally needs organiser `.mdb` exports — the live path's role is to
produce the exact CompID+name pairs for those requests.

**Permission email — draft for Pete to send to gerry.carter(at)gliderscore.com**
(nothing sent by this WI; placeholders in brackets for Pete to fill):

```
Subject: Permission request — reading public competition data from
gliderscore.com (open-source scoring project)

Dear Gerry,

I'm Pete [surname], a RC glider pilot in New Zealand. I'm developing
"Soarscore", a personal open-source scoring system for RC glider
competitions (F3B/F3J/F3K/F5J/F5K) — no commercial use of any kind.

To check my scoring engine produces exactly the results GliderScore
produces, I'm building a small test corpus from real competitions. The
score downloads your public site already serves to anyone viewing online
results are the perfect source: public by design, and used by me strictly
as read-only test data.

May I have your blessing to fetch those public downloads with a small
script — the same read-only sequence the GliderScore client itself
performs (check scores exist, create the zip, download it, delete the
server copy) — at a modest rate: a couple of seconds between requests,
one competition at a time, a handful of comps in a sitting plus the
occasional catalogue listing? Only comps visible on the public results
pages are touched; nothing private.

GliderScore stays the source of truth for every comparison, and you're
most welcome to a look at anything I build. Happy to adjust or limit the
approach in any way you prefer.

Best regards,
Pete [surname]
[email / phone]
```

**Note carried to WI-2B:** `NbrForTeamScore` confirmation happens only at
triage of an arriving export/zip — the Nbr=3 filter and the
zip-may-be-richer-than-CSV team-field test both land there, not here.

### WI-2B triage state (2026-09-03)

- **Arrivals: none** as of 2026-09-03. No `.mdb` exports, no richer zips.
  The webmine permission gate is unticked (see Hunt log above), so no zip
  could have arrived via fetch either.
- **Triage performed: zero times.** The four classification buckets each
  hold zero entries:
  - Nbr=3 overlap candidate — 0
  - Nbr≠3 T1-ledgered — 0
  - `UseTeams=false` inert — 0
  - skip-listed unrelated — 0
- **Procedure stands ready** for the first arrival: PII sweep first, then
  extract/profile per `extract/README.md`, classify into the buckets above
  with the exact numbers recorded.

### WI-2C state (2026-09-03) — curation pending acquisition

No accepted overlap candidate exists: WI-2A produced "nothing actionable
offline" and WI-2B recorded zero arrivals, so the curation pipeline ran zero
times. Every artifact it would produce is unstarted — `competition.json`,
`entries.json`, `scores-raw.json`, `expected-scores.json`,
`expected-result.json`, `provenance.json`, `class-definition.json`,
`expected-teams.json`, the index.md bullet, and the feature scenario.

When a candidate arrives, the pipeline runs exactly per the WI-2C checklist:
split at curation; PII note mandatory in `provenance.json`;
`class-definition.json` hand-authored, re-derived from this comp's own
`competition.json`; `expected-teams.json` per WI-1C's spec and verification
discipline (recompute over the fixture's proven individual oracle, transcript
when Pete can run GS on it); `validate.py` PASS; feature scenario covering
three grains + conservation + team ladder; replay on sqlite then postgres;
triage every difference at source; ledger only for intentional divergences,
under a token whose meaning fits. The f3j-international chain (Move 1's
WI-1A/1B/1C/1D/1E) is the template a second Nbr=3 fixture follows.

### WI-2D state (2026-09-03) — failed hunt, stub opened

**Zero sightings re-verified by sweep (not assumed) — `OmitFromTeamScore=true`
count is 0 in every committed fixture file.** Method: grep of every committed
`CompPilots.json` and `entries.json` under `tests/GliderscoreFixtures/`, plus
the triage outputs and prose. Per file (`data rows: true / false`):

| File | Rows | `true` | `false` |
|---|---|---|---|
| `ales-sample-comp/extract/CompPilots.json` | 11 | 0 | 10 |
| `f3k-june-2020/extract/CompPilots.json` | 16 | 0 | 15 |
| `f3k-southern-fling/extract/CompPilots.json` | 16 | 0 | 15 |
| `f5j-christchurch-2019/extract/CompPilots.json` | 19 | 0 | 18 |
| `f5j-hawkes-bay-trials/extract/CompPilots.json` | 19 | 0 | 18 |
| `f5j-nz-south-island/extract/CompPilots.json` | 14 | 0 | 13 |
| `sources/gliderscore-example-comps-extract/CompPilots.json` | 134 | 0 | 133 |
| **CompPilots totals** | **229** | **0** | **132** |
| `entries.json` (10 fixtures: f3j-international 31, f3j-international-flyoff 8, jerilderie-2010 64, f5j-christchurch-2019 19, f5j-hawkes-bay-trials 19, f3k-june-2020 16, f3k-southern-fling 16, f5j-nz-south-island 14, ales-sample-comp 11, f3k-sample-comp 11) | 209 | 0 | 199 |

Every file also carries exactly one non-boolean line, the extract's
column-type record `"OmitFromTeamScore": "Boolean"` — a schema marker, not a
data value. Triangulating checks: f3j-international, f3j-international-flyoff,
f3k-sample-comp and jerilderie-2010 have no committed `extract/CompPilots.json`
(their source extraction is not committed); their `entries.json` rows above
carry the imported values. The committed triage outputs (`extract/Comps.json`
× 6 + `sources/.../Comps.json`) contain no `OmitFromTeamScore` field at all —
only `UseTeams`/`UseTeamProtection`; no `*_triage.json` is committed anywhere
(the webmine tree has zero triage artifacts — WI-2A Hunt log, re-verified),
and the one preserved download CSV
(`webmine/tests/fixtures/2381887cb81b_DownloadData.csv`) carries no team/omit
tokens. Three prose mentions exist (`f5j-nz-south-island/competition.json:156`
triageJustification — "OmitFromTeamScore=false for all 13 pilots";
`f3k-sample-comp/divergences.json:7` and `jerilderie-2010/divergences.json:7`
T1 reasons — the `Contributes=!OmitFromTeamScore` mapping); none is a `true`
sighting.

**Pete's answer (2026-09-03): "no leads yet"** — for any real event that used
the flag (his events, his club, international contacts).

**Requests made:** the ask was re-asked 2026-09-03 and stays in flight;
WI-2A's permission email to Gerry Carter is drafted in the Hunt log above,
awaiting Pete's send — the live catalogue hunt may surface team-bearing comps
once the gate clears, though the `OmitFromTeamScore` flag itself is only
visible at export/triage, never in the catalogue or the download CSV.

**Conclusion (per this WI's own Done-when): the failed-hunt trail is this
record, and the backlog stub `kanban/backlog/omit-from-teamscore-witness.md`
is opened.** The arm stays implemented-but-unexercised — a data problem, not
a code one; unsilencing it later means sourcing a real witness, not touching
the engine or the mapping.

### WI-2E corpus-state reconciliation (2026-09-03)

Corpus-wide team-field sweep re-run from the files on disk (every committed
`CompPilots.json` — the six fixture `extract/` dirs plus
`sources/gliderscore-example-comps-extract/` — every committed `Comps.json`,
every per-fixture `competition.json` triage block, and every `entries.json`
`compPilots` array for the four fixtures whose source extraction is not
committed). Team-field truth per fixture:

| Fixture | UseTeams | UseTeamProtection | NbrForTeamScore | Teams populated? | Omit=true | Status |
|---|---|---|---|---|---|---|
| ales-sample-comp | false | false | 3 | yes — 10/10 in teams 1,2,6,8 (inert: knob off, no teams justification required) | 0 | active |
| f3j-international | true | true | 3 | yes — 30/30 across 8 teams (sizes 4,3,4,4,4,3,4,4) | 0 | active — THE one overlap witness; carries `expected-teams.json` (transcript-verified 8/8) |
| f3j-international-flyoff | false | false | 2 | no — Team='0' ×7 | 0 | active |
| f3k-sample-comp | true | true | 2 | yes — 10/10 in 4 teams (1,2,6,8) | 0 | active; team grain T1-ledgered (Nbr=2) |
| jerilderie-2010 | true | true | 4 | yes — 63/63 across 14 teams (1–14) | 0 | active; team grain T1-ledgered (Nbr=4) |
| f3k-june-2020 | true | true | 3 | no — Team='0' ×15 | 0 | active; Nbr=3 but zero populated teams — overlap condition fails on teams |
| f3k-southern-fling | true | true | 2 | no — Team='0' ×15 | 0 | active |
| f5j-christchurch-2019 | true | true | 2 | no — Team='0' ×18 | 0 | active |
| f5j-hawkes-bay-trials | true | true | 3 | no — Team='0' ×18 ('Team Trials' is name-only) | 0 | active |
| f5j-nz-south-island | true | true | 3 | no — Team='0' ×13 | 0 | active |
| f3b-international | true | true | 3 | yes — 23/23 in 6 teams (1,2,3,6,7,8) | 0 | skipped (multi-task rounds); no fixture dir — sources extract only |

`OmitFromTeamScore=true` total: **0** everywhere — 222 rows in the seven
committed `CompPilots.json` (ales 10; f3k-june-2020 15; f3k-southern-fling 15;
f5j-christchurch-2019 18; f5j-hawkes-bay-trials 18; f5j-nz-south-island 13;
source extract 133) plus 199 `entries.json` compPilots rows across the ten
fixtures; every sighting is `false`.

Anchor reconciliation — all verified, two sharpenings, one correction:

- f3j-international UseTeams=true Nbr=3, 8 teams — **confirmed** (Comps.json
  CompNo=1, competition.json triage, entries.json 30/30 in teams 1–8; the
  Move-1 oracle files agree).
- jerilderie-2010 14 real teams Nbr=4 — **confirmed** (63/63 in teams 1–14).
  Correction to the anchor as phrased: jerilderie is an **active** fixture —
  what is not exercised is its team grain, T1-ledgered in its
  `divergences.json`; skip-listing applies to f3b-international, not here.
- f3k-sample-comp Nbr=2, T1-ledgered — **confirmed** (4 populated teams,
  `divergences.json` T1 record).
- f3b-international UseTeams=true and skip-listed — **confirmed**, sharpened:
  its Nbr is also **3** with 6 populated teams, so it satisfies the ladder
  grain's full overlap condition and is blocked *only* by the
  multi-task-round skip (`unsupportedRoundComposition`). It is the latent
  second witness if multi-task rounds ever land.
- NZ master Team='0' on all 168 comps — **confirmed** on the five committed
  NZ fixtures (79 pilots, every Team='0'; matches each fixture's
  triageJustification).
- example-comps export = CompNo 1–5 — **confirmed** (F3J International,
  F3J International Flyoff, F3B International, Jerilderie 2010, F3K Sample
  Comp).

Surprises: (1) ales-sample-comp is the corpus's only UseTeams=false comp with
populated `CompPilots.Team` assignments (10/10 in teams 1,2,6,8) — inert by
the knob, data honest, consistent with its absent teams justification;
(2) f3k-june-2020 carries the ladder grain's class-level knobs (UseTeams=true,
Nbr=3) but zero populated teams, so it is *not* a second witness;
(3) WI-2D's recorded row counts were grep line-counts (data rows + the one
`"OmitFromTeamScore": "Boolean"` schema marker per file) — the JSON data-row
counts are one fewer per file (e.g. ales 10 not 11; entries total 199 data
rows, not 209). Substance unchanged: zero `true` anywhere.

Reconciliation statement: `tests/GliderscoreFixtures/index.md`'s *Diversity
wanted* section was updated to match this state — the Witnessed list now
names f3j-international's GS team-ladder oracle witness, and Still open now
carries the three team arms (second Nbr=3 overlap witness pending
acquisition; `OmitFromTeamScore=true` implemented-but-unexercised with its
stub; Nbr≠3 T1-ledgered pending a classification-policy story). Facts above
agree with the fixtures on disk; `validate.py` PASS re-confirmed on the
touched manifest (f3j-international, jerilderie-2010). validate.py and the
§Standing skip reasons were not touched (Move 3's).

Move 2 closes as hunt recorded, acquisition pending — WI-2A shortlist (none
offline), WI-2B zero arrivals, WI-2D stub opened; the team grain is proven on
the one Nbr=3 witness (f3j-international) that Move 1 delivered.

WI-1F already extended `GOLDEN-COMPARISON-STATE.md` with the witness claim
(team results now carry a GS-derived oracle witness on f3j-international,
ladder grain, exact-decimal; protection-only arm restated as unwitnessed).