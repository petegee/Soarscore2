# Story — Literal record replay scenarios (a whole fixture readable as Gherkin)

**Status:** Completed 2026-09-07 · **Raised:** 2026-09-04 (design settled interactively same
day; the six decisions below are owner-confirmed, not proposals) ·
**Planned:** 2026-09-07 (plan below is implementation-ready: fixture data
verified, feature file authored, step contracts specified)

## What

A second kind of GliderScore replay scenario, beside the JSON-driven harness
ones, in which the competition itself is visible in the feature file: one
"`<pilot>` enters his scores" block per pilot carrying the whole draw (real
flights as `Time`/`Landing` rows, unflown slots as explicit `no flight`
markers), hand-authored, closing with completion/finalise steps and a literal
placings table — `| Place | Name | Score | Dropped |` with GS's `=n` tie
strings. First target: `ales-sample-comp` (10 ZZ test pilots × 3 rounds × 1
group, 30 rows, no PII).

Settled design decisions (2026-09-04, Pete):

1. **Purpose** — human-readable record first; CI greenness secondary. The
   feature file is the readable record of an entire competition.
2. **Structure** — per-pilot entry blocks only, nothing interleaved between
   them; sequencing lives in closing steps. Blocks ordered by final placing
   (winner first — it reads as the comp's story, and entering out of draw
   order *demonstrates* NFR-4's no-imposed-ordering on score capture).
3. **Authoring** — hand-written, guarded by an in-scenario self-check step
   that diffs the entered tables against `scores-raw.json` cell-for-cell
   (packed-mmss decode, landing, penalty) and **names any typo'd cell
   directly** — authoring errors must not surface disguised as comparator
   failures.
4. **Column schema** — the union column set is designed once (Task, Laps,
   Time, Landing, Height, Penalty, re-flight marker); each fixture's table
   shows only the columns it uses; one family-agnostic step definition maps
   column names → capture fields.
5. **Unflown slots** — explicit `no flight` markers; the whole draw is visible
   in the feature text (ales's 24 `Updated='False'` rows appear as markers).
6. **The Then** — Place, Name, Score, Dropped, derived from
   `expected-result.json` joined to pilot names; the literal table sits beside
   the existing three-grain machinery, which stays the referee (belt and
   braces — precedent: the f3j-international team ladder already pinned
   verbatim in feature text).

Lives in a **new feature file** beside `ReplayingAGliderscoreFixture.feature`
(record character, not harness character); the JSON-driven scenarios stay
untouched. Reuses the `ReplayDriver`'s capture command surface — no new
command surface.

## Why it matters

The JSON harness proves parity but is opaque — the comp's data is never seen,
only referenced. A literal record makes a corpus fixture readable by someone
who has never seen the codebase, demonstrates behaviours the opaque When step
cannot show (out-of-order capture per NFR-4), and gives future work a legible
vehicle: `kanban/backlog/seed-definition-parallel-run.md`'s difference
reports are far more meaningful in this style.

## Before starting

- **Widening constraints to record in the plan** (settled as "record, don't
  solve"): (a) round-completion point — recommend explicit per-round
  "completed and scored" steps before finalise rather than one silent close;
  (b) `no flight` vs *genuine* zero — ales's zeros are all `Updated='False'`
  so the marker is unambiguous, but jerilderie-2010 (genuine zeros) and
  f5j-nz-south-island (flagged zero-time rows) will need a third marker or
  distinct wording — widen only when a first literal scenario per shape lands;
  (c) draw tables — omitted for ales (single group, same order every round),
  required only when a fixture's realised draw actually varies.
- **Step-definition plumbing:** name → PilotNo resolution from `entries.json`
  (verbatim ZZ names, unique); packed-mmss → `mm:ss` decode mirrors
  `ReplayDriver.CaptureDurationInputs`; `no flight` rows are NOT captured
  (`CaptureSlotsSkippingZeros` semantics).
- **Dropped column data source:** the engine's own dropped-cell contributions
  (the conservation machinery already knows them); for ales it is `—`
  everywhere (no drops configured).
- **Corpus discipline:** ales-sample-comp's committed fixture files, ledger
  and existing scenarios are untouched by this story.
- **No new domain concepts** — "record scenario" is harness vocabulary, not a
  glossary term; nothing in `/docs` changes.

---

# Plan (2026-09-07)

Everything below was verified against the tree at planning time. Line refs
drift — re-check before relying on one.

## 1. Ground truth established by planning

The ales fixture (`tests/GliderscoreFixtures/ales-sample-comp/`):
`competition.json`, `class-definition.json`, `entries.json`, `scores-raw.json`,
`expected-scores.json`, `expected-result.json`, `provenance.json`. All read
through `FixtureLoader.Load("ales-sample-comp")`
(`Support/Gliderscore/FixtureLoader.cs:40`).

- **Pilots** (entries.json `pilots` rows, PilotNo → verbatim name — all
  unique, ZZ test names, no PII):

  | PilotNo | Name               | Final place | Final score |
  |--------:|--------------------|-------------|------------:|
  |      70 | Ken ZZFox          | 1           |        1030 |
  |      48 | Chris ZZBarrenger  | 2           |         835 |
  |      13 | Theo ZZArvanitakis | 3           |         440 |
  |      12 | Jim ZZHoudalakis   | =4          |           0 |
  |      17 | Carl ZZStrautins   | =4          |           0 |
  |      21 | Mike ZZO'Reilly    | =4          |           0 |
  |      28 | David ZZPratley    | =4          |           0 |
  |      42 | Greg ZZPotter      | =4          |           0 |
  |      56 | Jamie ZZNancarrow  | =4          |           0 |
  |      65 | Jeff ZZIrvin       | =4          |           0 |

- **Flown slots** — exactly three, all in round 1 (`scores-raw.json`;
  `Updated='True'` on exactly these). The 27 remaining rows are all-zero
  `Updated='False'` placeholders → the 24 literal `no flight` markers plus the
  round-2/3 rows of the three flown pilots. Packed mmss decode is
  `ReplayDriver.DecodePackedMinutesSeconds`
  (`Support/Gliderscore/ReplayDriver.cs:1338`): minutes = Truncate(v/100),
  seconds = v − 100·minutes.

  | Round | Pilot | Packed Time1Mins | → mm:ss | → capture seconds | Landing (m) | Oracle RawScore | Oracle NormalisedScore |
  |------:|------:|-----------------:|--------:|------------------:|------------:|----------------:|-----------------------:|
  |     1 |    13 |            200.0 |    2:00 |               120 |           3 |             160 |                    440 |
  |     1 |    48 |            400.0 |    4:00 |               240 |           4 |             275 |                    835 |
  |     1 |    70 |            500.0 |    5:00 |               300 |           5 |             330 |                   1030 |

- **Rounds 2 and 3: nobody flew.** All ten cells per round are 0 in
  `expected-scores.json`. Final Score = Σ normalised cells (no drops —
  `Drop1AtRound..Drop5AtRound` all 99; no Penalty rows — every
  `Scores.Penalty` is 0).
- **Draw**: single group (G1) of all 10, flying order = `SeqNo` 1..10 =
  PilotNo order 12, 13, 17, 21, 28, 42, 48, 56, 65, 70 — same every round.
  Constraint (c)'s precondition holds; draw tables stay omitted.
- **Class definition** (`class-definition.json`): "Gliderscore DurALES
  (ales-sample-comp)", one phase, one task, metrics `flightTime` +
  `landingDistance`. `triage.UseTeams = false` → no team mapping
  (`MapGliderscoreTeamsAsync` no-ops).
- **Engine ordering law, verified** — `openEntry.taskRoundClosed`
  (`src/Soarscore.Domain/Competitions/Competition.cs:1386`) is the ONLY entry
  gate: an entry may be opened in any round, in any pilot order, until that
  round's task-round completes. Interleaving pilot blocks across all three
  rounds before any completion is legal and IS the NFR-4 demonstration.
- **Command surface** (all exercised by `ReplayDriver`, whose exact payloads
  the new steps mirror — no new surface, per the settled design):
  `/publish-class-definition` (`ReplayDriver.cs:368`), `/create-competition`
  (`:396`), `/register-person` + `/register-competitor` (`:414-421`),
  `/prescribe-draw` + `/accept-draw` (`:519-520`),
  `/open-entry` (`:664-668`), `/open-flight` (`:630`), `/capture-measurement`
  (`:634-638`), `/complete-task-round` (`:702-704`),
  `/finalise-competition` (`:750`). Read-back of drawn structure
  (`CompetitionView`): `:568-588`. Query URLs:
  `/competition-result?competitionRef=…` (`Comparator.cs:303`),
  `/task-round-result?competitionRef=…&phaseOrdinal=…&roundOrdinal=…&taskRoundOrdinal=1`
  (`Comparator.cs:619-624`).
- **Referee reuse** — `Comparator.CompareAsync(fixture, outcome, eventStore,
  client)` (`Support/Gliderscore/Comparator.cs:268`) is public and needs only
  a `ReplayOutcome` (`Support/Gliderscore/ReplayDriver.cs:142`). The literal
  steps accumulate exactly its fields as they drive the commands.

## 2. Files

| File | Action |
|---|---|
| `tests/Soarscore.Acceptance.Tests/Features/RecordingAGliderscoreFixture.feature` | **Create** — the literal record (§3, verbatim) |
| `tests/Soarscore.Acceptance.Tests/Features/RecordingAGliderscoreFixture.feature.cs` | **Create** — generated at build; not tracked (`*.feature.cs` is gitignored — repo convention) |
| `tests/Soarscore.Acceptance.Tests/Steps/RecordingAGliderscoreFixtureSteps.cs` | **Create** — one `[Binding]` class (§4) |
| `tests/Soarscore.Acceptance.Tests/Support/Gliderscore/ReplayDriver.cs` | **One-line widening** — `DecodePackedMinutesSeconds` `private` → `internal static` (single source of truth for the decode; same assembly). Cite this story at the site. |

Nothing else changes. `ReplayingAGliderscoreFixture.feature` and its steps are
untouched; **everything under `tests/GliderscoreFixtures/` is read-only**.

## 3. The feature file, verbatim

Blocks are ordered by final placing (winner first — decision 2); within the
`=4` tie the order is PilotNo ascending. Block order across pilots is
deliberately NOT flying/draw order, which is the NFR-4 point made visible.

```gherkin
@gliderscore
Feature: Recording a GliderScore fixture as a literal record
  A second kind of GliderScore replay scenario, beside
  ReplayingAGliderscoreFixture's JSON-driven ones (kanban backlog story
  literal-record-replay-scenarios): the competition itself is visible in the
  feature file. One "<pilot> enters his scores" block per pilot carries the
  whole draw — real flights as Time/Landing rows, unflown slots as explicit
  "no flight" markers — hand-authored, and the scenario closes with
  completion/finalise steps and a literal placings table in GliderScore's own
  notation ("=n" tie strings). Blocks are ordered by final placing, winner
  first; entering out of draw order, across all rounds before any round
  completes, demonstrates NFR-4's no-imposed-ordering on score capture.
  The JSON harness scenario for this same fixture stays the referee: its
  three-grain exact comparison runs at the end of this scenario too.

  Scenario: ALES sample comp — ten pilots, three rounds, one flown
    Given the GliderScore fixture "ales-sample-comp" is loaded for literal recording
    And its class definition is published and a competition created
    And its 10 pilots are registered under their fixture names
    And the draw is prescribed as 3 rounds of one group in flying order and accepted
    When Ken ZZFox enters his scores
      | Round | Time      | Landing |
      | 1     | 5:00      | 5       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Chris ZZBarrenger enters his scores
      | Round | Time      | Landing |
      | 1     | 4:00      | 4       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Theo ZZArvanitakis enters his scores
      | Round | Time      | Landing |
      | 1     | 2:00      | 3       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Jim ZZHoudalakis enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Carl ZZStrautins enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Mike ZZO'Reilly enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And David ZZPratley enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Greg ZZPotter enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Jamie ZZNancarrow enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Jeff ZZIrvin enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    Then the entered tables match the fixture's scores-raw exactly, cell for cell
    And round 1 is completed and scored
    And round 2 is completed and scored
    And round 3 is completed and scored
    And the competition is finalised
    And the final placings are
      | Place | Name               | Score | Dropped |
      | 1     | Ken ZZFox          | 1030  | —       |
      | 2     | Chris ZZBarrenger  | 835   | —       |
      | 3     | Theo ZZArvanitakis | 440   | —       |
      | =4    | Jim ZZHoudalakis   | 0     | —       |
      | =4    | Carl ZZStrautins   | 0     | —       |
      | =4    | Mike ZZO'Reilly    | 0     | —       |
      | =4    | David ZZPratley    | 0     | —       |
      | =4    | Greg ZZPotter      | 0     | —       |
      | =4    | Jamie ZZNancarrow  | 0     | —       |
      | =4    | Jeff ZZIrvin       | 0     | —       |
    And the three-grain oracle comparison over the same competition still runs exact
```

## 4. Binding class contract — `Steps/RecordingAGliderscoreFixtureSteps.cs`

One `[Binding]` sealed class, one instance per scenario (Reqnroll default —
same discipline as `ReplaySteps.cs`'s header). All HTTP via
`AcceptanceFixture.Client` + `ApiClient.PostCommandAsync` /
`ApiClient.GetAsync` (`Support/ApiClient.cs`) — the Api's own JSON options,
never bare STJ. `By` fields on commands carry `"Gliderscore literal record"`.
Competition name and emails carry a run slug so the shared store never
collides (mirror `ReplayDriver.cs:387-418`).

### State fields

```csharp
private GliderscoreFixture _fixture = null!;
private CompetitionId _competitionId;
private int _phaseOrdinal;
private Dictionary<(int RoundNo, int GroupNo), GroupId> _groupIdByRoundAndGroup = [];
private Dictionary<(int RoundNo, int GroupNo, long PilotNo), EntryId> _entryIdBySlot = [];
private Dictionary<long, CompetitorId> _competitorByPilotNo = [];
private Dictionary<long, string> _pilotNameByNo = [];          // from entries.json
private List<EnteredRow> _enteredRows = [];                    // tables as entered
private int _commandsIssued;                                   // honesty only, unasserted
```

`EnteredRow` (private record): `long PilotNo, string PilotName, int RoundNo,
string? Time` (null when `no flight`), `string? Landing` and
`string? Laps/Height/Penalty` cells as authored (null when the column is
absent from the fixture's tables).

### Step definitions

**Givens — setup**

1. `[Given(@"^the GliderScore fixture ""(.+)"" is loaded for literal recording$")]`
   — `FixtureLoader.Load(slug)`; assert the slug is in
   `FixtureLoader.ActiveSlugs()`; build `_pilotNameByNo` and assert the
   joined `"{FirstName} {LastName}"` values are unique (name-keyed steps
   require it).
2. `[Given(@"^its class definition is published and a competition created$")]`
   — POST `/publish-class-definition` with `fixture.Definition` → content
   hash; POST `/create-competition` with
   `"{Identity.CompName} (literal {slug})"`, `"Gliderscore literal record"`,
   compDate parsed as in `ReplayDriver.cs:391-394`. Assert hash non-empty.
3. `[Given(@"^its (\d+) pilots are registered under their fixture names$")]`
   — assert `fixture.Entries.CompPilots.Rows.Length == N` (pins the fixture
   against silent growth — a different pilot count must force a feature-file
   revisit, not a silent pass). For each row: `/register-person` (name from
   `_pilotNameByNo`, slug-unique email as the driver does) then
   `/register-competitor`; fill `_competitorByPilotNo`.
4. `[Given(@"^the draw is prescribed as (\d+) rounds? of one group in flying order and accepted$")]`
   — derive from `fixture.ScoresRaw.Rows` and ASSERT the shape the feature
   text claims, failing loudly if it doesn't hold (constraint (c)'s guard):
   no re-flight rows (`ReFlightNo == 0` and `OriginalRoundNo == RoundNo`
   everywhere); exactly one `GroupNo` per round; the `SeqNo`-ordered pilot
   list identical across all rounds; round count equals N. Build
   `PrescribedRound(TaskRef: null, Groups: [one PrescribedGroup, competitors
   in SeqNo order mapped through _competitorByPilotNo])` per round ascending
   (duration family: `TaskByRound` is empty → `TaskRef` null, exactly as the
   driver prescribes — `ReplayDriver.cs:472-490`). POST `/prescribe-draw`,
   `/accept-draw`, then read back the `CompetitionView` and fill
   `_phaseOrdinal`, `_groupIdByRoundAndGroup` and the task-code-by-round map
   exactly as `ReplayDriver.cs:568-588` does. Assert the read-back round
   count equals N.

**When — the entry blocks**

5. `[When(@"^(.+) enters his scores$")]` with a `Table` argument.
   - Resolve the captured name → PilotNo via `_pilotNameByNo`; on a miss,
     fail naming the unknown name and the available names (a typo must
     surface HERE, not in a comparator).
   - Validate the table headers against the union column set
     `{Round, Task, Laps, Time, Landing, Height, Penalty}` — unknown column
     = authoring error, fail naming it. (`Task` is accepted-and-ignored for
     now: single-task fixtures carry no Task column; a per-round task
     schedule fixture widens this step.)
   - For each row: parse `Round` (int). `Time` cell:
     - `"no flight"` → record the marker; NO flight opened.
     - `m:ss` (regex `^(\d+):(\d{1,2})$`) → decode to seconds
       (minutes·60 + seconds).
     - anything else → fail naming the cell.
   - Then issue, per row, in this order: `/open-entry` (`OpenEntry(
     _competitionId, _phaseOrdinal, roundOrdinal, 1, groupId, competitorId)`
     — round ordinal = the fixture's RoundNo, contiguous from 1 for this
     fixture, verified by the draw Given; groupId from
     `_groupIdByRoundAndGroup[(roundNo, 1)]`); if flown: `/open-flight` then
     `/capture-measurement` for `flightTime` (decoded seconds) and
     `landingDistance` (Landing cell, invariant decimal metres), flight 1 —
     the same two captures `CaptureDurationInputs` emits for the ales
     definition (`ReplayDriver.cs:1050-1058`). Record the EntryId in
     `_entryIdBySlot` and the authored row in `_enteredRows`.

**Thens — closing sequence**

6. `[Then(@"^the entered tables match the fixture's scores-raw exactly, cell for cell$")]`
   — the decision-3 self-check. Collect ALL mismatches (never first-only),
   and fail with each cell named:
   - Row-set equality keyed `(RoundNo, PilotNo)` between `_enteredRows` and
     `fixture.ScoresRaw.Rows` — a missing or extra round for a pilot is
     named with the pilot's name and round.
   - Flown ⇔ marker: fixture row unflown ⇔ `Time1Mins <= 0` (the
     `CaptureDurationInputs` rule, `ReplayDriver.cs:1034-1037`); a
     discrepancy is named both ways.
   - Flown rows: authored seconds == `DecodePackedMinutesSeconds(row.Time1Mins)`
     (now internal — see §2), and authored Landing == `row.Landing` exactly
     (invariant decimal).
   - Unflown rows: Landing cell must be `—` (fixture value is 0).
   - Omitted-column honesty: for every union column absent from the
     fixture's tables, assert the corresponding fixture values are neutral —
     `Laps`, `Time1Secs`, `Time2Mins/Secs`, `FlightScoreDeduction` all 0,
     `Penalty` all 0. A non-zero value in a column the tables omit is a
     named mismatch ("fixture carries Penalty 100 … but the tables have no
     Penalty column").
   - Message format per cell: `scores-raw mismatch — round {r}, {name}:
     entered {column} '{value}' but fixture says '{value}'`.
7. `[Then(@"^round (\d+) is completed and scored$")]` — POST
   `/complete-task-round` `new CompleteTaskRound(_competitionId,
   _phaseOrdinal, roundOrdinal: N, 1)`. ("Scored" is narrative — scoring is a
   read model; nothing to POST.) Assert N ≤ the prescribed round count.
8. `[Then(@"^the competition is finalised$")]` — POST
   `/finalise-competition`. Build the `ReplayOutcome` from the accumulated
   state (`CommandsIssued: _commandsIssued`) and keep it for the referee.
9. `[Then(@"^the final placings are$")]` with a `Table` argument — the
   decision-6 literal table:
   - Fetch `CompetitionScoreView` from
     `/competition-result?competitionRef={id}`.
   - Resolve each row: Name → PilotNo (`_pilotNameByNo`; unknown name fails
     naming the close matches) → CompetitorId (`_competitorByPilotNo`).
   - Assert the table covers exactly the registered competitors (10 rows,
     no duplicate names — full universe, so tie-group checks below are
     complete).
   - Per row: engine `Placing` == the Place cell parsed as int (trim a
     leading `=`); engine `Score` == the Score cell as invariant decimal,
     EXACT.
   - Tie integrity: for each place value n, the set of names the table shows
     at n must equal the set of competitors the engine places at n.
   - Dropped: every cell must be `—`; and the nothing-dropped witness: fetch
     `/task-round-result` per round (URL shape `Comparator.cs:619-624`), sum
     each competitor's `RawScore` (the post-normalisation score —
     `ScoreTaskRound.cs:26-27`) across rounds, and assert it equals the
     engine's final Score for that competitor (no drops ⇒ aggregate == Σ
     cells; ales also has no aggregate penalties — assert every fixture
     `Penalty` value is 0 here). Cite in a doc comment: a drop-bearing
     fixture widens this check to the engine's own dropped-cell
     contributions (the conservation machinery already knows them —
     `Comparator.CheckConservation`).
10. `[Then(@"^the three-grain oracle comparison over the same competition still runs exact$")]`
    — the referee (decision 6, belt and braces):
    `Comparator.CompareAsync(_fixture, _outcome, AcceptanceFixture.EventStore,
    AcceptanceFixture.Client)`; assert `report.AllGrainsExact` (message
    carries `report.DiffTable()`), `report.Conserves` (message carries
    `report.ConservationTable()`), and `_fixture.Divergences` empty (ales's
    ledger must stay so).

### Gherkin keyword trap (for the implementer)

`And`/`But` inherit the keyword of the PRECEDING step for binding purposes.
In §3 every step after "Then the entered tables match …" is an `And` on a
`Then`, so steps 7–10 MUST be `[Then]` bindings (even though they are
actions), and step 6 must be `[Then]`. The entry blocks follow a `When`, so
step 5 is `[When]`. Getting one of these wrong surfaces as a "no matching
step definition" bind error, not a compile error. Before finalising any
regex, grep `Steps/*.cs` for collisions — Reqnroll binding ambiguity is a
runtime failure, and `^the competition is finalised$`,
`^round (\d+) is completed and scored$` etc. are close to existing wording
(e.g. `ClosingACompetitionSteps.cs:206`) but must remain distinct.

## 5. Work items

Cite this story as `kanban/backlog/literal-record-replay-scenarios.md WI-n`
from code (the path-prefix will read `in-progress`/`completed` as the file
moves — cite the filename).

- **WI-1 — Driver decode widening + Given steps.** `ReplayDriver.cs` decode
  → `internal static`; create feature file (§3 verbatim) and the binding
  class with steps 1–4. Verify: `dotnet build tests/Soarscore.Acceptance.Tests`
  green; the generated `.feature.cs` compiles.
- **WI-2 — Entry blocks + self-check.** Steps 5–6. Verify: run the suite —
  the new scenario should now fail at "round 1 is completed and scored" (no
  binding yet) but never at the self-check; temporarily comment the
  completions if needed to watch the self-check pass, then restore. A
  self-check failure at this point means the feature tables or the step
  logic are wrong — fix them before proceeding, never weaken the check.
- **WI-3 — Closing steps.** Steps 7–8. Verify: scenario now reaches the
  placings step and fails only there (no binding yet).
- **WI-4 — Literal placings.** Step 9. Verify: scenario green through the
  placings, failing only at the referee step.
- **WI-5 — Referee.** Step 10. Verify: the whole scenario green.
- **WI-6 — Full verification, both stores.** Fast loop:
  `SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests`;
  full: `dotnet test tests/Soarscore.Acceptance.Tests` (Postgres via
  Testcontainers; needs Docker). Both must pass — a backend Soarscore claims
  to support is one that passes this whole suite unchanged.
- **WI-7 — Housekeeping.** `graphify update .`; reconcile
  `kanban/tech-debt.md` and `kanban/deferred-decisions.md` (expected: no
  new entries — the no-flight-vs-genuine-zero widening already lives in
  "Before starting"); move the story to `completed/` with `git mv`, set the
  status header.

## 6. Testing approach notes

- **No property-based testing for this story.** There is no new algorithm or
  invariant class here — the scenario is an exact pin of one fixture through
  three verbatim representations (feature tables ≡ `scores-raw.json` ≡
  oracle ≡ engine), and exact-fixture comparison is strictly stronger than a
  generated property over this data. The decode/format pair
  (`DecodePackedMinutesSeconds` ↔ `m:ss`) is already exercised exactly by
  the parity corpus; a CsCheck round-trip over it would test the test.
- **What each failure must name** (the story's attribution discipline): a
  typo'd feature table → the self-check (step 6) names the cell; a typo'd
  pilot name → step 5 or 9 names it; an engine regression → the literal
  placings step names the competitor AND the referee's `DiffTable()` names
  the grain and cell.

## 7. Scope guards and widening gates

- First target is ales only. The other nine corpus fixtures get literal
  scenarios only when someone wants them readable — each new shape (F3K
  task columns, F5K flight strings, drops, genuine zeros) widens the union
  column schema and the one family-agnostic step definition (decisions 4–5),
  never forks a second step surface.
- Widening gates already recorded in "Before starting" — (b) a third marker
  for genuine zeros (jerilderie-2010, f5j-nz-south-island) and (c) draw
  tables when a realised draw varies — are NOT solved here; the draw Given's
  shape assertions (step 4) are the tripwire that forces the widening.
- The `Task` column is accepted-and-ignored by step 5; per-round task
  schedules (F3K/F5K) widen it when a catalogue fixture gets a literal
  scenario.
- House rules: nothing in `/docs` changes; no glossary term; if
  implementation uncovers a feature beyond this story it becomes a new
  `kanban/backlog/` stub, never silent scope growth.