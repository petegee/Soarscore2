# Entry-completeness indicator

**Status:** Complete — built and verified 2026-08-24 · **Raised:** 2026-08-18 · **Planned:** 2026-08-24

## What

A read-side query answering, per task-round: how much of the expected data is
actually recorded. Roughly — of the competitors drawn into this task-round and
not withdrawn, how many have an Entry; of those, how many have at least one
flight; and does any flight lack a metric the task declares.

Surfaced to the Contest Director as a prompt — *"Round 3 — 20/20 entries, no
gaps"* or *"18/20 entries, 2 missing"* — so they can see at a glance whether a
task-round is ready to be marked complete, without walking the field asking.

**A query, never a state.** It computes nothing that gates anything, emits no
event, and does not transition a `TaskRound`. The CD presses the button; the
system only tells them what it can see.

## Why it matters

`kanban/in-progress/task-round-lifecycle.md` makes `TaskRoundCompleted` an
explicit act of the CD, deliberately — completion is the CD asserting a
task-round's *scores are in and settled*, which no amount of data inspection can
establish (see below). That is the right call, but it leaves the CD doing
bookkeeping by hand at the one moment it matters: deciding whether the contest
can be finalised.

This removes that burden without moving the authority. The CD still decides; they
just decide informed.

## Why it cannot simply be derived — the reason this is an indicator, not a state

Asked directly (user, 2026-08-18): if every metric for a task-round's groups is
entered, is the task-round complete? The honest answer is that **presence of data
can prove a task-round is not ready; absence can never prove that it is.** Three
legitimate domain outcomes are indistinguishable from "nobody has typed it yet":

- **The number of flights is data, not a constant.** `FlightSelection` has five
  kinds and only `ExactlyNInOrder` pins a count; for `last`, `all` and `bestN` —
  most of the corpus — a task's launch allowance is a ceiling, not a requirement.
  A pilot who took three of five allowed launches looks exactly like one who took
  five and entered three.
- **Metrics carry no required/optional flag.** `MetricDefinition` declares a name,
  kind, unit, precision and whether it is nominated before launch — nothing that
  says a value must be present. A landing metric absent because the pilot missed
  the box has the same shape as one absent because the Scorer has not got there.
- **`NoResult` is a first-class outcome that looks like missing data.** The
  glossary is emphatic that a flight never validly completed has *no result*,
  which is not zero; flight selection returns `NoResult` when an Entry has no
  flights at all. The domain deliberately treats absence as meaningful, which is
  exactly what stops absence from also meaning "not recorded yet". Overloading
  the one signal with both meanings would destroy the distinction that decides
  whether a contest is valid: "eighteen flew, two did not" versus "twenty flew,
  two are not entered".

A fourth, once re-flights land: a protest can append a group to a task-round
after it looked finished, so even a genuinely complete task-round can grow.

## Design constraints

- **Never phrase the output as "complete".** *"No gaps detected"*, *"18/20
  recorded"* — a factual count, never a verdict. If the output reads as
  authoritative, CDs will treat it as authoritative within a week and the project
  will have derived completeness by convention instead of by decision. This is
  the single most important line in this story.
- **A query, so it cannot gate.** It lives with the other read-side queries and
  answers on demand; nothing in a write path consults it. That is what keeps it
  compatible with [NFR-4](../../docs/non-functional-requirements.md) — an
  indicator that started gating anything would be the exact behaviour NFR-4
  forbids.
- **No new read model.** `IEntryQuery.FindAsync` already slices `entry_index` by
  task-round coordinate, and LADR-0001 §3 is explicit that scores are never
  projected. Counting *entries* is cheap from the index; establishing whether a
  flight lacks a declared metric needs the Entry streams themselves, so decide
  deliberately how far the indicator goes — an entry count alone may be most of
  the value for a fraction of the cost.
- **Class-agnostic, per CLAUDE.md's core architectural law.** The declared metric
  list comes from the adopted class definition's task; nothing here may branch on
  discipline.

## Before starting

- Decide the depth: entry-count-only (cheap, `entry_index` alone) versus
  metric-level gap detection (needs folding Entry streams for the task-round).
  Establish whether the CD actually wants the second before paying for it.
- Settle what "expected" means for a competitor with no Entry at all. A pilot who
  never launched may have no Entry, in which case the count can never reach 20/20
  and the indicator misleads in the opposite direction. This is the same ambiguity
  the section above describes, one level up, and it may want an explicit
  "did not fly" record — which would be a new concept and therefore needs approval
  (CLAUDE.md), not an inference.
- Check against `kanban/in-progress/task-round-lifecycle.md` once it completes:
  that thread owns `TaskRoundCompleted` and the reopen path, and this story must
  not quietly become the thing that decides completion.

## Not blocked by, and does not block

Independent of the task-round lifecycle thread. It reads state that thread
introduces, so it is more useful afterwards, but nothing in it needs to wait.

## Before starting — done

- **Depth** (user, 2026-08-24): **counts + metric gaps.** The indicator folds
  Entry streams and reports flights missing a metric the task declares, not
  just entry counts. At this project's stated scale the extra cost is trivial —
  `ScoreTaskRoundHandler` already folds exactly these streams on every
  leaderboard read — and partial transcription is precisely what a CD wants to
  catch before closing a round.
- **"Expected" semantics** (user, 2026-08-24): **drawn minus withdrawn**, using
  only existing vocabulary. `Group.CompetitorRefs` is the drawn allocation,
  `Competitor.WithdrawnAt is null` keeps it current, and withdrawal is already
  the established way to record "won't fly" (`OpenEntry` refuses withdrawn
  competitors). No new concept, no glossary change. A competitor who never
  launched and was never withdrawn *should* show as not recorded — that is the
  truth of the record, and the CD resolves it by withdrawal or by capture.
- **Lifecycle cross-check**: `task-round-lifecycle.md` is complete (2026-08-18).
  Its governing principle — completion is the CD's assertion, presence-of-data
  proves *not ready* but never *ready*, NFR-4 forbids anything that gates on
  recorded data — is restated in this story's design constraints and holds for
  everything below. This thread adds no write-path code at all.

## Work items

### WI-1 — The query: views, pure compute core, handler (Application)

`src/Soarscore.Application/Queries/Scoring/TaskRoundRecording.cs`, alongside
`ScoreTaskRound.cs`, whose shape it mirrors:
`CompetitionLoader.LoadAsync` → walk phases/rounds/task-rounds by ordinal →
optional group filter → task definition by `TaskRef` code → slice
`IEntryQuery.FindAsync` at the full coordinate → fold each matched stream via
`EntryLoader`.

Query: `GetTaskRoundRecording(CompetitionRef, PhaseOrdinal, RoundOrdinal,
TaskRoundOrdinal, GroupRef?) : IQuery<TaskRoundRecordingView>`.

Views, named so no field can read as a verdict — counts are facts about what
is *recorded*, never about whether the round is finished:

- `TaskRoundRecordingView(CompetitionRef, PhaseOrdinal, RoundOrdinal,
  TaskRoundOrdinal, TaskRef, Groups)`
- `GroupRecordingView(GroupRef, Ordinal, ExpectedCompetitorRefs,
  NotRecordedCompetitorRefs, RecordedWithoutFlightCompetitorRefs,
  MetricGaps)` — per group; counts derive client-side from list lengths
  ("20/20 entries, no gaps").
- `EntryGapsView(EntryRef, CompetitorRef, Role, Flights)` /
  `FlightGapsView(Sequence, MissingMetrics)` — only flights actually missing
  ≥ 1 declared metric appear; `MissingMetrics` follows declared order.

Bucketing rules, all decided here rather than left to inference:

| Bucket | Rule |
|---|---|
| `Expected` | drawn into the group ∧ `WithdrawnAt is null`. Withdrawal after opening an Entry removes them from every bucket — their half-entered data is noise once they are out of the contest. |
| `NotRecorded` | expected with no live Entry |
| `RecordedWithoutFlight` | expected whose live Entries exist but hold zero Flights between them |
| `MetricGaps` | per live Entry per Flight, declared metrics absent from its Measurements |

A live Entry is one with `Annulment is null`: an annulled Entry has no result,
so it neither records its competitor nor reports gaps — same treatment the
scoring queries give it. Capture refuses undeclared metric names, so there is
no "unexpected metric" case to report. A competitor holding two live Entries in
one group (the reflight shape) counts once as recorded/flown; both Entries'
gaps are reported.

The bucketing lives in an `internal static` pure function over
`(Competition, coordinate, IReadOnlyDictionary<EntryId, Entry>)` — no store, no
clock — which is what WI-3's property tests drive directly
(`InternalsVisibleTo` already covers Application.Tests).

Defect codes follow `scoreTaskRound.*`'s precedent (400s, matching its
`taskRoundNotFound` style): `taskRoundRecording.taskRoundNotFound`,
`taskRoundRecording.groupNotFound`, `taskRoundRecording.taskNotDeclared`;
`competition.notFound` passes through from the loader.

### WI-2 — Route and composition (Api)

```
app.MapQuery<GetTaskRoundRecording, TaskRoundRecordingView>("/task-round-recording");
```

plus its one `AddScoped<IQueryHandler<,>>` line in `Composition.cs`.
Opportunistic fix while touching registration: `HandlerRegistrationTests.cs`'s
sanity comment says "nine queries"; this makes ten.

### WI-3 — Property tests (CsCheck), invariants named up front

`tests/Soarscore.Application.Tests/Queries/Scoring/TaskRoundRecordingPropertyTests.cs`,
generating arbitrary shapes (field size, who withdraws, who enters, flights per
entry, which metrics captured) against the pure core:

- **Invariant P1 — buckets partition expected.** For any shape,
  `Expected = NotRecorded ⊎ RecordedWithoutFlight ⊎ RecordedAndFlown`, disjointly,
  and nobody outside Expected appears in any bucket. This is the property a
  hand-written example suite cannot cover: the reflight double-entry,
  withdrawn-after-entry, and annulled-only cases interact combinatorially.
- **Invariant P2 — gap soundness.** Every reported `MissingMetrics` is a
  subsequence of the task's declared metric names, reported iff that flight
  lacks them, and a flight with every declared metric captured never appears.

Non-vacuity: weaken each oracle (let a bucket overlap / drop the subset check)
and watch the test fail, recorded in the story on completion.

### WI-4 — Store-backed tests (both backends)

`tests/Soarscore.Infrastructure.Tests/TaskRoundRecordingEventStoreTests.cs`,
abstract-generic over `IStoreFixture`, F5J corpus class throughout (literal
MinPerGroup 6 gives deterministic single-group draws; its six declared metrics
give real gap lists). Scenarios:

1. Full house — everyone entered and flown, no gaps anywhere.
2. Two competitors absent → `NotRecorded` names exactly those two.
3. An Entry opened but never flown → `RecordedWithoutFlight`.
4. A flight capturing only `flightTime` → gap naming the other five declared
   metrics in declared order.
5. Withdrawn after opening an Entry → excluded from every bucket.
6. Sole Entry annulled → competitor reads as not recorded.
7. 12 pilots (two groups), `GroupRef` filter → exactly that group's view.

### WI-5 — Acceptance feature

`SeeingWhatIsRecorded.feature` + self-contained steps (the assembly-wide regex
rule keeps each Binding class's phrasing its own), driving real HTTP:

```gherkin
Scenario: A fully recorded task-round shows every competitor recorded, with no gaps
Scenario: Unrecorded competitors are named, and an unflown entry is shown as such
Scenario: A partially captured flight is named with its missing metrics
```

The first scenario runs immediately before a close in spirit — it is the CD's
glance at the indicator, and the phrasing of the Then asserts facts
("recorded", "missing"), never "complete".

### WI-6 — Verification loop

Full fast loop (`dotnet test` excluding `Category=Storage`), then the Storage
suite if Docker permits, then acceptance twice (`SOARSCORE_TEST_STORE=sqlite`
and `postgres`) if Docker permits; lint/typecheck via `dotnet build` warnings.

### WI-7 — Board reconciliation

On completion: `git mv` to `kanban/completed/`, set the status header, and
reconcile `tech-debt.md` / `deferred-decisions.md` — expected additions: none
(this thread deliberately creates no deferred debt); the "did not fly" concept
stays unraised unless a CD asks, per the decision above.

## As built

Landed as planned on 2026-08-24, with three notes worth keeping:

- **The bucketing is `RecordingCore`, and the property tests drive it directly.**
  `ComputeGroupViews` takes already-loaded aggregate state (Competition for the
  withdrawal check, coordinate, groups, folded Entries, declared metric names)
  and returns the views — no store, no clock — which is why
  `TaskRoundRecordingPropertyTests` needs no fakes at all. The handler owns
  only loading: `CompetitionLoader` walk, task definition by `TaskRef`,
  `FindAsync` sliced at the full coordinate (fewer stream folds than
  `EntryCollector`'s whole-competition fan-out), `EntryLoader` per stream.
- **Non-vacuity was mutation-checked, as WI-3 promised.** Weakening P1's oracle
  so a competitor could sit in two buckets made the test fail; weakening P2's to
  report every declared metric as missing regardless of capture also failed.
  One test-side lesson: the withdrawn-with-entries vacuity counter must range
  over the whole field, not just the expected set — withdrawn competitors are
  by definition outside Expected, so an expected-only enumeration can never see
  the shape it is guarding for.
- **Verified end to end:** fast loop green across all five test projects;
  Storage-tagged suite green against real PostgreSQL via Testcontainers;
  acceptance suite green twice (`SOARSCORE_TEST_STORE=postgres` and `sqlite`),
  including all three SeeingWhatIsRecorded scenarios. Test counts: one CsCheck
  fact over both invariants with six shape-class guards; seven store-backed
  facts per backend; three BDD scenarios per store.

Reconciliation: no tech debt deferred. One entry added to
`kanban/deferred-decisions.md`: the "did not fly" record stays unraised
(drawn-minus-withdrawn is the settled answer), and that file's stale pointer to
this story's backlog path was corrected in the same edit.
