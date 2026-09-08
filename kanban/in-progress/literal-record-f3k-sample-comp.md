# Story — Literal record scenario: f3k-sample-comp (task columns, penalties, real drops)

**Status:** In progress · **Raised:** 2026-09-07 (identified during the closing review of
`literal-record-replay-scenarios.md`: a corpus survey of all ten active fixtures ranked
this #1 on new-behaviour-per-widening-cost) ·
**Planned:** 2026-09-07 (plan below is implementation-ready: fixture data verified
cell-for-cell, feature file authored verbatim, step contracts specified; the three
open design decisions were settled with the owner same day — see "Settled design
decisions")

## What

A second scenario beside `RecordingAGliderscoreFixture.feature`'s ales one, for
`f3k-sample-comp` — the only other fixture whose draw passes the ales Given's
shape assertions unchanged (single group, no re-flight rows, **identical SeqNo
flying order across all 9 rounds**, verified against `scores-raw.json`), yet
widens the recorded form at five points at once:

- **`Task` column goes live** — per-round task codes (G, A(1), F, D, C(3),
  X×4) → `PrescribedRound.TaskRef` via the fixture's `F3KTaskByRound`
  schedule (today the column is accepted-and-ignored and every prescribed
  round carries `TaskRef: null`).
- **`Penalty` column goes live** — 4 rows × 100 aggregate penalties →
  `/record-competition-penalty` (the command surface already exists; the step
  gains a penalty arm, no new commands).
- **Positional slot columns** — F3K rows are packed-mmss slots decoded per
  task code (`Laps`/`Time1Mins`/`Time1Secs`/…); task D (R4) uses all seven
  slot positions, so carrying a literal `Landing` column would be a lie
  (provenance: "the zero Landing values (145.0, round 4, pilots 13/28/56) are
  NOT landing-scheme lookups"). Task-aware `Slot 1..7` columns replace
  Laps/Time/Landing semantics for this fixture — the one family-agnostic step
  definition's column map grows, it does not fork.
- **The `Dropped` column becomes real content** — Drop1@5 fires, dropping
  R9's placeholder-zero for all ten pilots (`expected-result.json`: "exactly
  one '*' per pilot, on Rnd9") — the corpus's most readable drop story,
  against ales's all-`—`.
- **Referee relaxation** — step 10's `_fixture.Divergences` empty becomes
  "exactly the ledgered set" (one `T1` team entry in the fixture's
  `divergences.json`).

10 compact 9-row blocks, ZZ names (no PII), no per-fixture step
special-handling — every widening is data-driven off the fixture files.

### Settled design decisions (2026-09-07, Pete)

1. **Slot cells are authored decoded m:ss** (`2:43`), not packed GS literals
   (`243`) — the ales record's human convention; the step decodes with the
   same mmss rule and the self-check compares against the packed fixture
   cell. Verified lossless for all 90 rows (every packed value's seconds
   component < 60, so decode ∘ encode is the identity here).
2. **The undecidable zero cell reads `zero (unrecorded)`.** R4 / Greg
   ZZPotter's all-zero row is the one zero cell `scores-raw.json` cannot
   classify (flown-zero vs not-flown; `Updated='True'` carries no
   information). The other 40 zero rows are provenance-attested `NoTaskSet`
   placeholders and keep the ales `no flight` marker. Both markers behave
   identically (entry opened flight-less — D4); the wording difference is the
   record's honesty, and the self-check pins each marker to its arm (see
   step 6).
3. **Placings table gains a numeric `Penalty` column and `Dropped` reads
   GS's round naming** — `| Place | Name | Score | Dropped | Penalty |`,
   Dropped cell `Rnd9` (empty → `—`), Penalty the transcript's integer total
   (0 printed as `0`).

## Why it matters

The ales literal record demonstrates the record form but exercises almost none of the
union column schema: every pilot block is the same three rows, the Dropped column is
uniformly `—`, and no round carries a task or a penalty. f3k-sample-comp makes the
record form earn its keep — it is the cheapest fixture that shows task schedules,
aggregate penalties, positional F3K slots, and a live drop in one readable file, and it
stresses the parent story's design decisions (4–5: one union schema, one step
definition) the way ales could not.

---

# Plan (2026-09-07)

Everything below was verified against the tree at planning time. Line refs
drift — re-check before relying on one.

## 1. Ground truth established by planning

Fixture `tests/GliderscoreFixtures/f3k-sample-comp/`: `competition.json`,
`class-definition.json`, `entries.json`, `scores-raw.json`,
`expected-scores.json`, `expected-result.json`,
`overall-results-transcript.csv`, `divergences.json`, `provenance.json`. All
read through `FixtureLoader.Load("f3k-sample-comp")` — the slug is active in
the manifest (the JSON harness replays it today).

- **Pilots** (entries.json, verbatim ZZ names, all unique, no PII), in final
  placing order (transcript order — this is also the feature file's block
  order):

  | Place | PilotNo | Name               | Score | Penalty | Dropped |
  |------:|--------:|--------------------|------:|--------:|---------|
  |     1 |      28 | David ZZPratley    |  4609 |       0 | Rnd9    |
  |     2 |      12 | Jim ZZHoudalakis   |  3749 |       0 | Rnd9    |
  |     3 |      48 | Chris ZZBarrenger  |  3672 |       0 | Rnd9    |
  |     4 |      13 | Theo ZZArvanitakis |  3588 |       0 | Rnd9    |
  |     5 |      65 | Jeff ZZIrvin       |  3477 |     100 | Rnd9    |
  |     6 |      21 | Mike ZZO'Reilly    |  3421 |       0 | Rnd9    |
  |     7 |      70 | Ken ZZFox          |  3414 |       0 | Rnd9    |
  |     8 |      56 | Jamie ZZNancarrow  |  3402 |     200 | Rnd9    |
  |     9 |      17 | Carl ZZStrautins   |  3255 |       0 | Rnd9    |
  |    10 |      42 | Greg ZZPotter      |  2957 |     100 | Rnd9    |

  No ties — ranks display 1..10 without `=` (`expected-result.json` notes).
  Scores come from `overall-results-transcript.csv` verbatim (Score == Raw
  Score, GroupScoreDecimals=0); Penalty is the transcript's per-pilot total.

- **Task schedule** (`competition.json` `scheduleTables.F3KTaskByRound`, all
  9 rows comp 5): R1=G ('Best5 2:00max'), R2=A(1) ('L1 5max in 10m'),
  R3=F ('Best3 3:00max'), R4=D ('Ladder (Not FAI)'), R5=C(3) ('AllUp
  3:00*5'), R6–R9=X ('NoTaskSet'). Six codes; the class definition declares
  exactly these six as catalogue tasks (`ChooseFromCatalogue`, one task per
  round). `Scores.TaskNo` is 5 on every row — task identity lives entirely
  in the schedule.

- **Slot semantics** (`ReplayDriver.F3KSlotMap`,
  `Support/Gliderscore/ReplayDriver.cs:1178`): GS packs up to seven inputs
  per row — ScrArr(0..6) = Laps, Time1Mins, Time1Secs, Time2Mins, Time2Secs,
  Landing, FlightScoreDeduction — and reads a task-specific prefix as flight
  times, each packed-mmss decoded (`DecodePackedMinutesSeconds`,
  `ReplayDriver.cs:1339`, already `internal`): minutes = Truncate(v/100),
  seconds = v − 100·minutes. Per task in THIS fixture:
  G → five slots, each engine-clamped at 120 s; A(1) → Laps slot alone,
  capped at 300 s; F → three slots, capped at 180 s; D → ALL SEVEN slots
  positionally clamped at the ladder targets 30/45/60/75/90/105/120 (the
  landing-distance and deduction slots count as flight times here —
  provenance); C(3) → five slots, capped at 180 s; X → no slots, nothing
  ever captured.

- **Planning re-verified the arithmetic end to end**: decoding every row's
  task slots → skipping zeros (preserving slot order) → engine cap/ladder →
  sum reproduces **all 90** `expected-scores.json` RawScore cells exactly
  (50 data rows + 40 placeholder zeros). Also verified, so the feature
  tables are safe to author as m:ss: every packed value in the fixture has a
  seconds component < 60; every task-D row is prefix-shaped (no interior
  zero after a non-zero slot — the driver's positional-mispair guard,
  `ReplayDriver.cs:1294-1311`, would refuse one); rounds 6–9 are all-zero
  for all ten pilots; R4 pilot 42 is the only zero row outside R6–R9.

- **Penalties**: exactly four rows carry `Penalty=100` — pilot 42 in R1,
  pilot 56 in R2 and R3, pilot 65 in R3. Per-pilot totals 100 / 200 / 100
  (ZZPotter / ZZNancarrow / ZZIrvin — transcript reconciled). Normalised
  cells are pre-penalty; GS subtracts the total after summing kept cells
  (provenance; our pipeline applies aggregate penalties in the same place —
  `Comparator.cs` conservation note and `ScoringService.GetAggregatePenalties`).

- **The drop**: `Drop1AtRound=5` with option-0 activation (counts DISTINCT
  rounds with RawScore>0) — rounds 1–5 are scored, so Drop1 activates exactly
  at threshold; Drop2@9 never activates. Candidates sort value ASC then
  RoundNo DESC (latest equally-bad first), so R9's placeholder zero drops for
  every pilot ahead of every real score (`expected-result.json`: "exactly
  one '*' per pilot, on Rnd9"). Because every dropped candidate is a ZERO
  cell, the drop identity (Σ cells − dropped − penalties == Score) holds for
  ANY wrong-round drop too — the dropped ROUND must be checked against the
  engine's own dropped cells, not just the dropped sum (this is why step 9
  needs the engine's dropped round ordinals, not only the conservation
  identity).

- **Divergences ledger** (`divergences.json`): exactly one entry, grain
  `team` — NbrForTeamScore=2 vs the MVP's fixed three-score method
  (`teams-mvp.md` decision 8); the team grain does not run and GS's expected
  team ladder is neither carried nor comparable. Individual grains
  unaffected. `Comparator` subtracts the ledger
  (`SubtractLedger`, `Comparator.cs:1362`) before reporting.

- **Draw shape**: single group (G1) of all 10, flying order = `SeqNo`
  1..10 = PilotNo order 12, 13, 17, 21, 28, 42, 48, 56, 65, 70 — same every
  round; no re-flight rows; RoundNo contiguous 1..9. The existing Given's
  assertions hold unchanged; the Given only needs the task-schedule arm.

- **Command surface** — no new commands. The widenings reuse what
  `ReplayDriver` already drives: per-round `TaskRef` on
  `PrescribedRound` (`ReplayDriver.cs:490` — `TaskRef:
  taskByRoundNo.GetValueOrDefault(round)`), one `/open-entry` per row always
  (flight-less rows included — D4), one `/open-flight` + one
  `/capture-measurement` (flightTime) PER NON-ZERO SLOT in slot order with
  flight sequence = capture index (`FlyAndCaptureAsync`,
  `ReplayDriver.cs:662-696`), `/complete-task-round` per round,
  `/record-competition-penalty` per penalty row —
  `new RecordCompetitionPenalty(competitionId, CompetitionPenaltyInfractionType,
  PenaltyScope.Competition, competitorByPilotNo[row.PilotNo], TaskRound: null,
  By: …)` (`ReplayDriver.cs:741-750`; the command record is
  `src/Soarscore.Application/Commands/Competitions/RecordCompetitionPenalty.cs:14`),
  then `/finalise-competition`. The driver's infraction constant
  `CompetitionPenaltyInfractionType = "competitionPenalty"`
  (`ReplayDriver.cs:946`, matches the definition's declared infractionType)
  is `private` — widen to `internal` like the decode precedent. Entries with
  FEWER flights than task D's exactlyN=7 are proven acceptable (the driver
  replays exactly this; the engine pairs in order against the leading
  targets).

- **Referee reuse** — `Comparator.CompareAsync(fixture, outcome, eventStore,
  client)` is unchanged and already handles this fixture: ledger subtraction,
  conservation across drops AND aggregate penalties (`CheckConservation`,
  `Comparator.cs:771` — "Σ our grain-2 normalised cells − Σ contributions of
  the engine's DROPPED cells − aggregate-penalty deductions == the
  competitor's /competition-result Score"). Its dropped set comes from
  `ScoringService.Aggregate`'s `PhaseScores.DroppedScores` — each
  `TaskRoundScore` carries `RoundOrdinal`
  (`src/Soarscore.Domain/Scoring/ScoringResultTypes.cs:163-176`), which is
  the engine's own dropped-round witness step 9 needs.

## 2. Files

| File | Action |
|---|---|
| `tests/Soarscore.Acceptance.Tests/Features/RecordingAGliderscoreFixture.feature` | **Append** the second scenario (§3, verbatim). The ales scenario is untouched. |
| `tests/Soarscore.Acceptance.Tests/Steps/RecordingAGliderscoreFixtureSteps.cs` | **Widen** — the six step definitions grow data-driven arms (§4). No new step regexes, no second binding class. |
| `tests/Soarscore.Acceptance.Tests/Support/Gliderscore/ReplayDriver.cs` | **Three one-line widenings** — `TaskByRound`, `F3KSlotMap`, `CompetitionPenaltyInfractionType` `private` → `internal` (same single-source-of-truth precedent as `DecodePackedMinutesSeconds`). Cite this story at each site. |
| `tests/Soarscore.Acceptance.Tests/Support/Gliderscore/Comparator.cs` | **One new internal helper** + refactor — `ConservationByCompetitor(...)` returning the per-competitor conservation row (§4 step 9); `CheckConservation` refactored to consume it, behaviour unchanged (the referee's own `Conserves` assertion proves the refactor). |

Nothing else changes. `ReplayingAGliderscoreFixture.feature` and its steps are
untouched; **everything under `tests/GliderscoreFixtures/` is read-only**.

## 3. The feature file, verbatim

Blocks are ordered by final placing (winner first — parent decision 2); the
ale-block order is deliberately NOT flying/draw order (NFR-4). Every m:ss
cell is the decoded packed-mmss fixture value; `—` marks a slot the round's
task does not read or that decodes to zero (skipped — not captured);
`no flight` marks a provenance-attested NoTaskSet placeholder row; `zero
(unrecorded)` marks the one zero row the data cannot classify (decision 2).

```gherkin
  Scenario: F3K sample comp — ten pilots, nine rounds, six tasks, live drop and penalties
    Given the GliderScore fixture "f3k-sample-comp" is loaded for literal recording
    And its class definition is published and a competition created
    And its 10 pilots are registered under their fixture names
    And the draw is prescribed as 9 rounds of one group in flying order and accepted
    When David ZZPratley enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 2:00   | 1:45   | 1:58   | 1:43   | 1:22   | —      | —      | —       |
      | 2     | A(1)  | 4:53   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 1:45   | 2:56   | 2:54   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | 1:30   | 1:45   | —      | —       |
      | 5     | C(3)  | 0:03   | 0:03   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Jim ZZHoudalakis enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 0:45   | 0:56   | 1:18   | 1:56   | 0:43   | —      | —      | —       |
      | 2     | A(1)  | 5:00   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 1:25   | 2:45   | 3:12   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | 1:30   | —      | —      | —       |
      | 5     | C(3)  | 0:03   | 0:01   | 0:01   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Chris ZZBarrenger enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 1:32   | 1:44   | 1:46   | 1:46   | 1:59   | —      | —      | —       |
      | 2     | A(1)  | 4:32   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 2:31   | 2:59   | 2:21   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | —      | —      | —      | —      | —       |
      | 5     | C(3)  | 0:02   | 0:01   | 0:01   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Theo ZZArvanitakis enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 0:29   | 0:31   | 0:48   | 0:50   | 1:02   | —      | —      | —       |
      | 2     | A(1)  | 2:11   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 2:55   | 2:12   | 2:12   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | 1:30   | 1:45   | 2:00   | —       |
      | 5     | C(3)  | 0:03   | 0:01   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Jeff ZZIrvin enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 1:23   | 1:43   | 1:43   | 2:00   | 0:22   | —      | —      | —       |
      | 2     | A(1)  | 3:12   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 2:20   | 3:00   | 2:12   | —      | —      | —      | —      | 100     |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | —      | —      | —      | —       |
      | 5     | C(3)  | 0:03   | 0:01   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Mike ZZO'Reilly enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 2:01   | 1:55   | 1:34   | 1:20   | 1:10   | —      | —      | —       |
      | 2     | A(1)  | 1:56   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 1:22   | 2:11   | 2:10   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | 1:30   | —      | —      | —       |
      | 5     | C(3)  | 0:03   | 0:02   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Ken ZZFox enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 1:20   | 0:02   | 1:54   | 0:58   | 1:19   | —      | —      | —       |
      | 2     | A(1)  | 4:23   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 3:00   | 1:30   | 2:20   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | —      | —      | —      | —      | —       |
      | 5     | C(3)  | 0:03   | 0:01   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Jamie ZZNancarrow enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 1:32   | 1:35   | 1:43   | 1:48   | 2:00   | —      | —      | —       |
      | 2     | A(1)  | 0:12   | —      | —      | —      | —      | —      | —      | 100     |
      | 3     | F     | 2:43   | 1:54   | 2:33   | —      | —      | —      | —      | 100     |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | 1:30   | 1:45   | —      | —       |
      | 5     | C(3)  | 0:03   | 0:03   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Carl ZZStrautins enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 0:45   | 0:55   | 1:05   | 1:15   | 1:25   | —      | —      | —       |
      | 2     | A(1)  | 2:18   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 3:03   | 2:34   | 2:16   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | —      | —      | —      | —       |
      | 5     | C(3)  | 0:02   | 0:03   | 0:02   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Greg ZZPotter enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 2:00   | 2:00   | 2:00   | 2:00   | 1:50   | —      | —      | 100     |
      | 2     | A(1)  | 4:18   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 1:56   | 0:50   | 1:12   | —      | —      | —      | —      | —       |
      | 4     | D     | zero (unrecorded) | — | —   | —      | —      | —      | —      | —       |
      | 5     | C(3)  | 0:02   | 0:02   | 0:01   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    Then the entered tables match the fixture's scores-raw exactly, cell for cell
    And round 1 is completed and scored
    And round 2 is completed and scored
    And round 3 is completed and scored
    And round 4 is completed and scored
    And round 5 is completed and scored
    And round 6 is completed and scored
    And round 7 is completed and scored
    And round 8 is completed and scored
    And round 9 is completed and scored
    And the competition is finalised
    And the final placings are
      | Place | Name               | Score | Dropped | Penalty |
      | 1     | David ZZPratley    | 4609  | Rnd9    | 0       |
      | 2     | Jim ZZHoudalakis   | 3749  | Rnd9    | 0       |
      | 3     | Chris ZZBarrenger  | 3672  | Rnd9    | 0       |
      | 4     | Theo ZZArvanitakis | 3588  | Rnd9    | 0       |
      | 5     | Jeff ZZIrvin       | 3477  | Rnd9    | 100     |
      | 6     | Mike ZZO'Reilly    | 3421  | Rnd9    | 0       |
      | 7     | Ken ZZFox          | 3414  | Rnd9    | 0       |
      | 8     | Jamie ZZNancarrow  | 3402  | Rnd9    | 200     |
      | 9     | Carl ZZStrautins   | 3255  | Rnd9    | 0       |
      | 10    | Greg ZZPotter      | 2957  | Rnd9    | 100     |
    And the three-grain oracle comparison over the same competition still runs exact
```

Authoring notes for whoever touches these tables later: the m:ss cells are
the decoded packed-mmss fixture values (packing rule: minutes = v/100
truncated, seconds = v − 100·minutes; e.g. 243 → 2:43, 100 → 1:00, 3 →
0:03). The `Task` cells are the fixture's `F3KTaskByRound` codes verbatim.
`Slot k` cells beyond a task's slot count are `—` (task A(1) reads only the
Laps slot = Slot 1; task D reads all seven, where Slot 6 is GS's Landing
column and Slot 7 its FlightScoreDeduction column, both flight-time ladder
inputs here). The 40 `no flight` rows are the R6–R9 NoTaskSet placeholders;
the single `zero (unrecorded)` row is R4 / Greg ZZPotter.

## 4. Binding class contract — deltas to `Steps/RecordingAGliderscoreFixtureSteps.cs`

No new bindings, no signature changes — six step definitions grow data-driven
arms. All HTTP, slugs, `By` provenance and run-slug discipline are unchanged
from the ales implementation.

### State changes

`EnteredRow` becomes:

```csharp
private sealed record EnteredRow(
    long PilotNo, string PilotName, int RoundNo,
    string? Time, string? Landing, string? Laps, string? Height, string? Penalty,
    string? Task, IReadOnlyList<string?> SlotCells);   // NEW — slot family; SlotCells[0..6], null-never, "—"/m:ss/markers
```

New fields:

```csharp
private bool _slotFamilyTables;                       // tables carry Slot 1..7 (and no Time column)
private const string MarkerNoFlight = "no flight";    // provenance-attested placeholder rows
private const string MarkerZeroUnrecorded = "zero (unrecorded)"; // the undecidable zero row (decision 2)
```

(Keep `Time`/`Landing`/`Laps`/`Height` as-is for the duration family — ales
is untouched. `Penalty` already exists.)

### Given 4 — draw prescription grows the task arm

After the existing shape assertions (all hold for this fixture), derive the
per-round task schedule the way the driver does —
`ReplayDriver.TaskByRound(fixture)` (widened `internal`): non-empty here,
one row per prescribed round. Assertions:

- every prescribed round number has a schedule row (and vice versa);
- every named task code is declared by the published definition's phase
  tasks (`fixture.Definition` — fail loudly otherwise: prescribing a task
  the catalogue does not carry is an authoring error);
- build `PrescribedRound(TaskRef: schedule[roundNo], Groups: [...])` —
  `null` stays the duration-family behaviour (`TaskByRound` returns empty
  for ales);
- after the read-back, assert `_taskCodeByRoundNo` equals the schedule
  exactly (the drawn task-rounds carry the prescribed TaskRefs).

### When 5 — entry blocks grow the slot arm

Branch on the table's shape (authoring error, named, if a table carries BOTH
`Time` and `Slot 1` or NEITHER beyond `Round`):

- **Duration arm** — existing code, byte-identical.
- **Slot arm** (new):
  1. Validate headers against the widened union column set
     `{Round, Task, Time, Landing, Laps, Height, Penalty, Slot 1..Slot 7}` —
     unknown column fails naming it (existing discipline). Require `Task`
     present (the task column is live for slot tables).
  2. Per row: parse `Round`; resolve the round's task code
     `_taskCodeByRoundNo[roundNo]`; resolve its slot list
     `ReplayDriver.F3KSlotMap[taskCode]` (widened `internal`; a code the map
     does not carry is the driver's widen-first gate — mirror its
     `NotSupportedException` message shape). Record the authored `Task` cell
     on the `EnteredRow` — **cells never drive behaviour**; the fixture
     schedule does. (A wrong Task cell surfaces in the self-check.)
  3. Parse the seven slot cells:
     - `—` → contributes nothing;
     - m:ss (`MmssPattern`) → decode to seconds, append to the capture list
       in slot order;
     - `no flight` / `zero (unrecorded)` → the row is UNFLOWN; only legal in
       the Slot 1 cell (fail naming the cell otherwise); all other slot
       cells must be `—` (fail naming).
     - anything else → fail naming the cell.
  4. Issue, per row: `/open-entry` (identical payload to the duration arm —
     every row gets an entry, flight-less rows included, D4); if the row is
     flown, for each capture at index i (0-based): `/open-flight` then
     `/capture-measurement(entryId, flight: i+1, "flightTime",
     MeasuredValue.Of(seconds))` — flight sequence = slot-capture order,
     exactly the driver's `Flight: captures.Count + 1`.
  5. **Penalty arm**: the `Penalty` cell is `—` or a positive invariant
     integer (fail naming otherwise). An integer → POST
     `/record-competition-penalty` with
     `new RecordCompetitionPenalty(_competitionId,
     ReplayDriver.CompetitionPenaltyInfractionType, PenaltyScope.Competition,
     _competitorByPilotNo[pilotNo], TaskRound: null, By: CdName)` — one POST
     per occurrence row (pilot 56's two rows → two occurrences → one 200
     deduction via PerOccurrence accrual). Recorded inline with its row —
     the natural reading of the record; the read model is order-independent
     (penalties apply at scoring time), so this matches the driver's
     post-round recording in effect, and the referee cannot tell the
     difference.
  6. Record the `EnteredRow` with `Task` and `SlotCells`.

### Then 6 — self-check grows the slot arm

Branch on `_slotFamilyTables` (set in step 5; the duration arm is untouched).
Slot arm, still collecting ALL mismatches with each cell named:

- Row-set equality keyed `(RoundNo, PilotNo)` — unchanged.
- **Task cell**: authored `Task` == the fixture's schedule code for that
  round, else named mismatch.
- **Flown ⇔ marker, slot edition**: a fixture row is unflown ⇔ ALL its
  task's slots decode to 0 (X rows always are; this is the F3K analogue of
  the `Time1Mins <= 0` rule). A discrepancy is named both ways.
- **Marker discipline** (decision 2, machine-checked): an authored
  `no flight` marker is only valid on a row whose task code is `X` (the
  provenance-attested NoTaskSet arm); `zero (unrecorded)` is only valid on
  an unflown row whose task code is NOT `X` (the undecidable arm — for this
  fixture exactly one, R4 / pilot 42). A marker on the wrong arm is a named
  mismatch.
- **Unflown rows without a marker**: every authored slot cell must be `—`.
- **Flown rows**: no marker cells; for each slot i < slot list length:
  authored cell m:ss decodes to
  `ReplayDriver.DecodePackedMinutesSeconds(fixture value of slot i's
  column)` — mismatch names both values; authored `—` where the fixture
  decodes non-zero and authored m:ss where it decodes 0 are both named. For
  i ≥ slot list length (columns the task does not read): authored must be
  `—`.
- **Penalty**: authored (`—` ⇔ 0, integer otherwise) == `fixtureRow.Penalty`,
  else named.
- **Omitted-column honesty, slot edition**: the seven raw columns
  (Laps…FlightScoreDeduction) are all potentially slots; for every raw
  column that NO task in this fixture's schedule maps, assert all fixture
  values are 0 (named otherwise). For this fixture the set is empty (task D
  maps all seven) — the check is generic, not fixture-named.
- **Duration-arm honesty stays**: `Time1Secs`/`Time2Mins`/`Time2Secs`
  neutrality etc. is asserted only in the duration arm — the slot arm must
  NOT run those checks (they would fire on this fixture's real values).

### Then 9 — placings table grows Penalty + a real Dropped witness

First the new support helper, then the step:

- **`Comparator.ConservationByCompetitor(outcome, eventStore, client)`**
  (new, `internal static`): for each competitor, the same state collapse
  `CheckConservation` already performs — load the competition from the event
  store, arrange the task-round results into `TaskRoundScores`, fold
  `ScoringService.Aggregate`, read `PhaseScores` — returning per competitor:
  `CellSum` (Σ post-normalisation per-round cells),
  `DroppedSum`, `DroppedRoundOrdinals` (ascending, from
  `DroppedScores`' `TaskRoundScore.RoundOrdinal`), `PenaltyDeduction`
  (aggregate-penalty total), `FinalScore` (/competition-result Score).
  Refactor `CheckConservation` to consume this helper — behaviour
  unchanged, proven by the referee's `Conserves` assertion staying green.
  Map engine round ordinals → fixture RoundNos via the outcome's
  `RoundOrdinalByRoundNo` inverse (contiguous 1..9 here, but key by fixture
  coordinates as everywhere else in this class).
- Step assertions, per table row (generalised; ales's tables carry no
  Penalty column and drop nothing, so the ales scenario passes unchanged):
  - `Place`/`Score`/universe/tie-integrity — unchanged (no `=` ties here).
  - **Dropped cell** (NEW semantics): `—` ⇔ the engine's
    `DroppedRoundOrdinals` is empty; otherwise the cell must equal the
    engine's dropped fixture rounds rendered `"Rnd"` + comma-joined numbers
    (`Rnd9` here). This is the load-bearing check: every dropped candidate
    in this fixture is a zero cell, so the conservation identity holds even
    if the engine dropped the WRONG zero round — only the engine's own
    dropped round ordinals catch that.
  - **Penalty cell** (when the column is present): parsed invariant integer
    == the engine's `PenaltyDeduction` for that competitor AND == Σ fixture
    `Penalty` values for that pilot (the transcript's total cross-checked
    against the fixture data — a mistyped 200 vs 2×100 surfaces here).
  - **Witness, generalised**: `CellSum − DroppedSum − PenaltyDeduction ==
    FinalScore` for every competitor (ales: dropped 0, penalties 0 → the
    existing Σ cells == Score). Delete the ales-only guards
    (`ScoresRaw.Rows.Where(r => r.Penalty != 0).Should().BeEmpty(...)` and
    `row["Dropped"].Should().Be("—", ...)` — both become data-driven).
    Cite `Comparator.CheckConservation` in a doc comment: the referee
    asserts the same identity independently.

### Then 10 — referee relaxation

Replace `_fixture.Divergences.Should().BeEmpty(...)` with the ledgered-set
assertion: `_fixture.Divergences` must be exactly the committed ledger —
for this fixture exactly one entry, grain `"team"` (assert grain name and
count; the entry's reason cites `teams-mvp.md` decision 8 /
NbrForTeamScore=2). `report.AllGrainsExact` and `report.Conserves` assertions
are unchanged — `CompareAsync` subtracts the ledger
(`Comparator.cs:1362`) before reporting, so the excused team grain never
masks an individual-grain mismatch. For ales the ledger is empty and the
new assertion reduces to the old one — keep one implementation, data-driven
off `_fixture.Divergences` (assert each loaded entry's grain is non-empty;
do NOT hard-code "team" for ales's sake).

### Gherkin keyword trap (unchanged, still applies)

Every step after "Then the entered tables match …" is an `And` on a `Then`
— steps 7–10 stay `[Then]`-bound; the entry blocks stay `[When]`-bound.
No new step text is introduced by this story, so no new collision surface;
still, grep `Steps/*.cs` before touching any regex.

## 5. Work items

Cite this story as `kanban/backlog/literal-record-f3k-sample-comp.md WI-n`
from code (the path-prefix will read `in-progress`/`completed` as the file
moves — cite the filename).

- **WI-1 — Support widenings + draw/task Given.** `ReplayDriver`:
  `TaskByRound`, `F3KSlotMap`, `CompetitionPenaltyInfractionType` →
  `internal` (three one-liners, cite the story). Steps: Given 4's task arm
  (§4). Verify: `dotnet build tests/Soarscore.Acceptance.Tests` green; run
  the ales scenario — still green (no behaviour change on the duration
  path).
- **WI-2 — Feature scenario + slot/penalty entry arm.** Append §3 verbatim;
  implement step 5's slot arm and penalty arm. Verify: run the f3k scenario
  — it must execute all 10 blocks and 90 `/open-entry` POSTs plus the
  multi-flight captures and 4 penalty POSTs without command rejection, and
  fail only at the (not-yet-widened) self-check — never inside a block.
  A failure inside step 5 means the slot/penalty parsing or the task
  prescription is wrong: fix it before proceeding; never weaken a check.
- **WI-3 — Self-check slot arm.** Step 6's slot arm per §4. Verify: f3k
  scenario fails now only after the self-check (at the placings step, whose
  Dropped/Penalty arms don't exist yet). Temporarily comment the completion
  steps if needed to watch the self-check pass in isolation, then restore.
  Deliberately corrupt one authored cell (e.g. swap two slot values) and
  confirm the self-check names it — then restore. Never weaken the check.
- **WI-4 — Conservation helper + placings arms.** `Comparator.ConservationByCompetitor`
  + `CheckConservation` refactor; step 9's Dropped/Penalty/witness
  generalisation per §4. Verify: ales scenario green (generalised checks
  reduce to the old ones); f3k scenario fails only at the referee step.
- **WI-5 — Referee relaxation.** Step 10's ledgered-set assertion per §4.
  Verify: the whole f3k scenario green; ales scenario green.
- **WI-6 — Full verification, both stores.** Fast loop:
  `SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests`;
  full: `dotnet test tests/Soarscore.Acceptance.Tests` (Postgres via
  Testcontainers; needs Docker). Both must pass — a backend Soarscore claims
  to support is one that passes this whole suite unchanged.
- **WI-7 — Housekeeping.** `graphify update .`; reconcile
  `kanban/tech-debt.md` and `kanban/deferred-decisions.md` (expected: no new
  entries — the widening gates below stay gates); move the story to
  `completed/` with `git mv`, set the status header.

## 6. Testing approach notes

- **No property-based testing for this story** — same reasoning as the
  parent: the scenario is an exact pin of one fixture through four verbatim
  representations (feature tables ≡ `scores-raw.json` ≡ oracle ≡ engine),
  and exact-fixture comparison is strictly stronger than a generated
  property over this data. The slot decode/format pair is already exercised
  exactly by the parity corpus (90/90 proven at WI-4 of
  `gliderscore-replay-and-compare-harness.md`, re-verified at planning).
- **What each failure must name** (attribution discipline): a typo'd slot or
  penalty cell → the self-check (step 6) names the cell; a typo'd pilot or
  task name → step 5 or 9 names it; a wrong-round drop → step 9's Dropped
  arm names the engine's dropped rounds vs the literal cell (conservation
  alone CANNOT catch this — every dropped candidate is a zero); an engine
  regression → the literal placings step names the competitor AND the
  referee's `DiffTable()` names the grain and cell.

## 7. Scope guards and widening gates

- One scenario is added; the ales scenario and the JSON harness are
  untouched. The slot arm is task-map-generic (driven by
  `F3KTaskByRound` + `F3KSlotMap`), NOT fixture-named — a future F3K/NZ
  fixture reuses it as data. The 16-code NZ catalogue is already in
  `F3KSlotMap` (`nz-fixture-replay-scenarios.md` D5); only task K/H's
  non-plain slot disciplines (all-slots-in-order, sorted-descending) are NOT
  expressible in this step's plain skip-zeros walk — a K/H fixture is a loud
  widening gate at step 5, not a silent mis-score.
- Still gates, recorded not solved (parent "Before starting", unchanged):
  (a) round-completion point stays explicit per-round steps;
  (b) genuine-zero wording — the `zero (unrecorded)` marker (decision 2) is
  the third marker's first witness; a fixture with MULTIPLE undecidable
  zeros reuses the wording, no new decision;
  (c) draw tables — still omitted; this fixture's Given-shape assertions
  (unchanged) remain the tripwire.
- F5K flight strings, re-flight markers, and Height columns remain future
  fixtures' widenings (`f3j-international-flyoff` #2, `f5j-hawkes-bay-trials`
  #3 — the latter PII-blocked). The five NZ-master fixtures stay
  PII-blocked for literal records until a redaction decision exists.
- House rules: nothing in `/docs` changes; no glossary term ("slot" is
  harness/record vocabulary for GS's packed columns, already described in
  `provenance.json`); if implementation uncovers a feature beyond this
  story it becomes a new `kanban/backlog/` stub, never silent scope growth.
