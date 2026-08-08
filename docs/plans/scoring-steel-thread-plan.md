# Plan — Scoring: de-orphaning the scoring engine

**Status:** Proposed · **Date:** 2026-08-09 · **Base commit:** `900df6d` · **Gap:** `docs/plans/gap.md` §5

Closes gap 5. Turns 2,153 lines of pipeline that nothing calls into a scored
group and a ranked leaderboard, reachable over HTTP.

## Context

`src/Soarscore.Domain/Scoring/` holds eleven files implementing the whole
scoring pipeline — interpret, select, clamp, round, normalise, aggregate, drop,
penalise, rank. Nine of the eleven have **zero production callers**. The two
exceptions arrived by side door: `ParameterResolver` (used by `DrawPhase`,
`OpenEntry` and `TaskResolver`) and `RoundingSupport` (used by
`Entry.CaptureMeasurement`).

The blocker is gone. `capture-a-score-steel-thread-plan.md` landed the Entry
write path, so real `Measurement`s now exist in the log for the pipeline to
consume. This thread builds the thing that reads them.

### Scope: two slices, both in this thread

**Slice 1 — score one group.** Given a drawn task-round and the Entries flown
in it, produce a `GroupResult`: each competitor's raw score, normalised score,
and the group winner. This is what gets read out at the field when a group
lands.

**Slice 2 — the leaderboard.** Walk the whole Competition, score every group,
aggregate each competitor's phase score with drops applied, and rank. This is
`ScoreCompetition`, and unlike slice 1 it is **new code, not repair** — see
finding 1.

Slice 1 is independently shippable and is the honest steel thread. Slice 2 is
the payoff and rests on one assumption stated in finding 5.

### What the audit found (verified against `900df6d`, not assumed)

#### Finding 1 — `ScoreCompetition` is a shell, not a mis-typed method

`docs/plans/gap.md` §5 says the engine "is **not** mis-typed against a stale
aggregate design; it has vestigial parameters and no caller", and that rescuing
it "needs an adapter … not a redesign". That is true of `InterpretFlight`,
`SelectFlights`, `NormaliseGroup`, `Aggregate`, `Rank` and `ScoreGroup` — all
six are complete, and their `object` parameters are genuinely unused.

It is **not** true of `ScoreCompetition` (`ScoringService.cs:133-194`). That
method never populates `allTaskRoundScores` (`:146`), never calls
`PhaseAggregator` at all, and sums an always-empty list at `:166` — so every
competitor scores `0` and is then ranked. The comment at `:148-149` says so
outright ("since we don't have the aggregate types, this is structural"). Slice
2 writes this method; it does not repair it.

#### Finding 2 — amendment resolution exists nowhere in the tree

The engine consumes `IReadOnlyDictionary<string, MeasuredValue> resolvedMetrics`
per flight. `Entry` stores `ImmutableArray<Measurement>`, each carrying
`ImmutableArray<Amendment>` (`Entry.cs:101-111`). Nothing anywhere computes the
effective value. `Measurement`'s own doc comment (`Entry.cs:96-99`) assigns the
job to `ScoringService`; `ScoringService.InterpretAllFlights` (`:204-220`) is
the stub that was meant to do it and returns `ImmutableArray.Empty`.

This is the only genuinely new domain logic in slice 1, and the reason WI-1 sits
first. Issue #4 of the recovered issues doc already resolved *where* it lives
(orchestrator pre-processes, not inside `FlightInterpreter`) — that resolution
stands.

#### Finding 3 — the engine speaks `string`, the domain speaks typed ids

Every engine type keys on `string competitorRef` / `string groupRef` /
`string TaskCode` (`ScoringResultTypes.cs:86-145`). `Entry` and `Competition`
use `CompetitorId`, `GroupId` (`readonly record struct`s over `Guid`).

**Decision: the adapter stringifies with `.ToString()` (the Guid's "D" form) on
the way in, and the Application layer maps back to typed ids on the way out.**
The engine is not retyped. Two reasons: retyping `ScoringResultTypes` would
make `Soarscore.Domain.Scoring` depend on `Soarscore.Domain.Competitions` in
its *data* types rather than only in its orchestration, and every existing
engine test (seven files, all green) is written against the string form. The
mapping is total and lossless in both directions, and it is exercised by WI-5's
round-trip property.

#### Finding 4 — `RecordedPenalty` and `Penalty` do not have the same shape

The engine consumes `RecordedPenalty(string InfractionType, int OccurrenceCount)`
(`ScoringResultTypes.cs:127`). The domain records `Penalty(string
InfractionType, PenaltyScope Scope)` (`Shared.cs:34-39`) with no count.

**Decision: one recorded `Penalty` is one occurrence.** The adapter groups
`Entry.Penalties` by `InfractionType` and counts, which is exactly what an
accruing penalty definition needs. `PenaltyScope` routes the stage:
`Flight`/`Entry` → `ApplyRawPenalties` (before normalisation),
`TaskRound`/`Competition` → `ApplyAggregatePenalties` (after drops). This
matches the recovered plan's responsibility 5 and issue-resolution "the stage is
derived from the effect, not configured".

**But no penalty is reachable today.** `PenaltyRecorded` has a fold on both
`Entry` (`Entry.cs:248`) and `Competition` (`Competition.cs:411`), and a decide
function on neither — so `Entry.Penalties` is always empty in any stream this
system can produce. Same for `EntryAnnulled`, so `Entry.Annulment` is always
null. **Consequence: penalty routing and the annulment skip are unit-testable
against hand-built aggregates but cannot be covered end to end by this thread**,
and the acceptance test will not exercise them. Deliberate — pulling
`Entry.RecordPenalty` and `Entry.Annul` in would be the second Entry thread, not
this one. Recorded under "Newly deferred".

#### Finding 5 — nothing ever marks a task-round `Complete`, so the leaderboard must derive its own field

`Competition.TaskRoundState` has four members (`Competition.cs:105`) and
`TaskRoundCompleted` has no decide function, so every task-round in every
competition this system can produce sits at `Drawn` forever.
`Scoring.TaskRoundState` (`PhaseAggregator.cs:34`) has two — `Complete`,
`Annulled` — and `PhaseAggregator` reads it to decide what a drop policy may
drop. This is the duplicate-enum item already recorded at `tech-debt.md:6`; this
thread is the first code that must reconcile it, exactly as that item predicted.

Mapping `Drawn` → `Complete` would be a lie with teeth: an undrawn-and-unflown
round would enter the aggregate as a zero, and a drop-worst policy would then
spend its drop on a round nobody has flown, corrupting a live leaderboard.

**Decision: the adapter derives the field from the Entries, not from the state
flag.** A task-round enters the leaderboard when **at least one Entry exists for
it**; a task-round whose Competition state is `Annulled` maps to
`Scoring.TaskRoundState.Annulled`; everything else with entries maps to
`Complete`. A task-round with no entries is omitted from `RoundData` entirely.

The result is a **provisional leaderboard over rounds flown so far**, which is
what a leaderboard means mid-competition, and it makes drop-worst behave the way
a CD expects during an event. The two enums are left in place; the mapping is a
private function in the adapter, and `tech-debt.md:6` is updated to record that
the conversion now exists and where.

#### Finding 6 — `ScoringService` should be static, not an instantiable class

It is `public class ScoringService` (`:20`) holding no state, while all seven
other pipeline components are `public static class`. It has no interface, no DI
registration, and could not be injected today.

**Decision: make it `public static class`.** This departs from the recovered
plan's WI-9 signature block, which specified instance methods. Nothing depends
on the instance form — there is not one caller — and static matches both the
rest of the engine and the way `Application` already calls domain functions.
No DI registration is needed, so `HandlerRegistrationTests` stays satisfied
without a new line.

### Out of scope (deliberately)

- **Reflight scoring.** `ReflightRole.Entitled`/`Filler` selection rules are
  described in the recovered plan's responsibility 4, but `ReflightGroupAppended`
  has no decide function, so no reflight Entry can exist. WI-3 implements the
  `Original` path and returns an explicit `score.reflightNotSupported` failure if
  it ever sees more than one non-annulled Entry for a (task-round, competitor)
  pair, rather than shipping untestable selection logic.
- **`MeasurementAmended`, `EntryAnnulled`, `PenaltyRecorded` commands.**
  Finding 4. The pipeline handles all three as *inputs*; producing them is the
  second Entry thread.
- **Storing results.** LADR-0001 §3 and the recovered plan are both explicit
  that scoring is computed on demand and nothing is projected. No new read
  model, no new event.
- **`FinalRankingKind.LastPhaseReplaces` / `SplitByPromotion` across phases.**
  Every drawable corpus class draws exactly one phase today
  (`Competition.cs:612-621` only ever draws phase 0), so multi-phase ranking has
  no reachable input. `RankingEngine.Rank` already accepts both; WI-3 passes
  them through and WI-9 asserts single-phase behaviour only.
- **Catalogue-choice and multi-task rounds.** F3K, F5K and F3B still cannot be
  drawn (`Competition.cs:639-646`), so they cannot be scored. Unchanged by this
  thread.

### Governing documents

- `docs/plans/scoring-service-plan.md` and `docs/plans/scoring-service-issues.md`
  — **deleted from HEAD in `38cb008`, recoverable from `d1ea17d`.** All eleven
  Scoring files header-cite the former; its eight resolved design issues are the
  semantics the shipped pipeline code implements. WI-12 restores both, subject
  to approval.
- `docs/ladr/ladr-0001-event-store.md` §3 — results are derived, never stored.
- `docs/aggregate-roots.md` §Scoring — the five-method service interface.
- CLAUDE.md's core architectural law — WI-11 turns it into a failing build.

---

## Phase A — Domain

### WI-1 — Amendment resolution

**New:** `src/Soarscore.Domain/Scoring/MeasurementDigest.cs`.

```csharp
public static class MeasurementDigest
{
    /// The effective value of a Measurement is its most recent Amendment's
    /// NewValue by At, or Value when it has no amendments.
    public static ResolvedMeasurements Resolve(Flight flight);
}
```

Ties on `At` resolve to the **last appended** amendment — the event log's order
is the tiebreak, because two amendments bearing the same instant are still
ordered facts. `ResolvedMeasurements` already exists (`ScoringResultTypes.cs:23`)
and is the type every stage reads.

`FlightInterpreter` synthesises the `flight.sequence` intrinsic itself
(`Entry.cs:118-122`), so `Resolve` returns captured metrics only and does not
inject it.

### WI-2 — Retype `ScoringService`, drop the vestigial parameters

`src/Soarscore.Domain/Scoring/ScoringService.cs`.

- `public class` → `public static class` (finding 6).
- `InterpretFlight(object flight, …)` → `InterpretFlight(ResolvedTask task, int
  flightSequence, IReadOnlyDictionary<string, MeasuredValue> resolvedMetrics)` —
  the parameter is dropped, not retyped, because `FlightInterpreter.Interpret`
  ignores it.
- `SelectFlights(object entry, …)` → `SelectFlights(Entry? entry, …)`. Retyped
  rather than dropped: `FlightSelector`'s contract is "pass null if annulled"
  (`FlightSelector.cs:25`) and WI-3 needs somewhere to express that.
- `FlightInterpreter.Interpret`'s `object? flight` (`:30`) and
  `FlightSelector.SelectAndScore`'s `object? entry` (`:34`) — `Interpret`'s is
  dropped; `SelectAndScore`'s is retyped to `Entry?` and its annulment check
  made real (`entry?.Annulment is not null` → `NoResult`), which is the one
  place the parameter was always meant to be read.
- `ScoreGroup`'s `ImmutableDictionary<string, object> entries` →
  `ImmutableDictionary<string, Entry>`; `InterpretAllFlights` and
  `GetEntryPenalties` lose their `object` parameters and gain real bodies
  (WI-1 and finding 4 respectively).

`Soarscore.Domain.Entries` already references `Soarscore.Domain.Scoring`
(`Entry.cs:21`, for `RoundingSupport`), so this creates a mutual namespace
reference inside the one assembly. Legal, intended by the recovered plan
(whose WI-9 signature block names `Entry` and `Flight` directly), and invisible
to `LayerRuleTests`, which constrains assemblies. Noted rather than worked
around.

**Every existing engine test must still pass unchanged** except for the
argument lists of the calls being edited. Any behavioural change here is a
defect, not a refactor.

### WI-3 — `ScoreCompetition`, written for real

`src/Soarscore.Domain/Scoring/ScoringService.cs`. New signature:

```csharp
public static Result<CompetitionResult> ScoreCompetition(
    Competition competition,
    IReadOnlyDictionary<EntryId, Entry> entries);
```

`Result<>` rather than a bare return: reflight-not-supported (out of scope,
above) and a task-round referencing an undeclared task code are both real
failure modes the caller must see, and `Result<T>` is the repo's decide-function
convention.

Reads structure from `competition.Phases` → `Round` → `TaskRound` → `Group`
(`Competition.cs:228-294`) and rules from `competition.AdoptedRules.Definition`.
Parameter bindings flatten last-write-wins exactly as `TaskResolver.cs:64-66`,
`DrawPhase` and `OpenEntry` already do — a fourth copy of that four-line
flatten is worth avoiding, so lift it to an internal helper in this WI and
repoint `TaskResolver` at it.

The walk:

```
for each phase, round, task-round:
    resolve TaskDefinition by TaskRound.TaskRef → ResolvedTask
    for each group:
        entries for this group  →  ScoreGroup(...)  →  GroupResult
        GroupResult.Results     →  TaskRoundScore per competitor
build RoundData/TaskRoundData per finding 5 (entries present ⇒ Complete)
for each competitor:
    PhaseAggregator.Aggregate(...)     → PhaseScores (drops applied)
    PenaltyEngine.ApplyAggregatePenalties(...) → PenaltyApplication
    → FinalCompetitorScore
RankingEngine.Rank(scores, classDef.FinalRanking, phase.Promotion)
```

A competitor drawn into a group with no Entry contributes no `TaskRoundScore` —
they are absent from that round rather than zero in it. Only a *flown* task-round
that produced `NoResult` scores zero.

### WI-4 — Penalty routing

Fills `GetEntryPenalties` and `GetAggregatePenalties` per finding 4: group by
`InfractionType`, count occurrences, split on `PenaltyScope`. Both currently
return `Empty` unconditionally (`ScoringService.cs:236,247`).

Entry-scoped penalties come from `Entry.Penalties`; TaskRound- and
Competition-scoped ones from `Competition.Penalties`, which the `PenaltyRecorded`
fold populates (`Competition.cs:411-412`). Both collections read the same way —
neither is ever non-empty today, since neither event has a decide function
(finding 4), so both paths are unit-tested against hand-built aggregates.

### WI-5 — Property tests (CsCheck)

`tests/Soarscore.Domain.Tests/`. Named invariants, per CLAUDE.md's standing
practice:

1. **Amendment resolution is last-write-wins.** For any `Measurement` and any
   list of `Amendment`s, `Resolve` returns the `NewValue` of the amendment with
   the greatest `At` (last appended on ties), and the original `Value` when the
   list is empty.
2. **Scoring is order-invariant over distinct metrics.** Folding the same set of
   `MeasurementCaptured` events in any permutation (distinct metrics, so no
   `alreadyCaptured` collision) yields an identical `GroupResult`. This is the
   property that says scoring reads the fold, not the event order.
3. **Scoring is a pure function.** Scoring the same `Competition` + entries twice
   yields equal results. Cheap, and it is what makes "results are derived, never
   stored" safe (LADR-0001 §3).
4. **Dropping never raises and never over-lowers.** For any set of task-round
   scores and any `DropPolicy`, the phase aggregate is ≤ the sum of all scores
   and ≥ the sum of the best `n − drops` of them.
5. **Placings are a consistent total order.** For any set of
   `FinalCompetitorScore`, placings are drawn from `1..n`, a higher score never
   receives a worse placing, and equal scores receive equal placings.
6. **Id round-trip is lossless** (finding 3). For any `CompetitorId`/`GroupId`,
   stringify-then-parse is the identity — the property that lets the engine keep
   speaking `string`.
7. **Corpus-generic: every drawable class scores.** For each of the eight
   currently drawable seed classes (`tools/Soarscore.SeedData/`), a competition
   drawn and fully captured scores without throwing, and every competitor with
   at least one Entry receives a placing. This is the test that would catch a
   class-specific assumption leaking into the pipeline.

Invariants 4 and 5 may be partly covered by the existing
`PhaseAggregatorPropertyTests` and `RankingEngineTests` — check before writing,
and extend rather than duplicate.

---

## Phase B — Application

### WI-6 — `EntryCollector`

**New:** `src/Soarscore.Application/Entries/EntryCollector.cs`, `internal
static`, mirroring `EntryLoader.cs`.

Fans out `IEntryQuery.FindAsync` (to learn which Entry streams exist for a
competition — the exact job `entry_index` was built for) then folds each stream
via `EntryLoader.LoadAsync`, returning `IReadOnlyDictionary<EntryId, Entry>`.

Reading every stream is correct here, not lazy: LADR-0001 §3 forbids projecting
scores, and at this project's stated scale — ≤ 20 pilots × ≤ 8 rounds — the
worst case is ~160 short streams.

### WI-7 — The two queries

`src/Soarscore.Application/Scoring/`, a new folder in this project.

- `ScoreTaskRound(CompetitionId CompetitionRef, int PhaseOrdinal, int
  RoundOrdinal, int TaskRoundOrdinal, GroupId? GroupRef) : IQuery<IReadOnlyList<
  GroupScoreView>>` — slice 1. `GroupRef` optional: unset scores every group in
  the task-round.
- `ScoreCompetition(CompetitionId CompetitionRef) : IQuery<CompetitionScoreView>`
  — slice 2.

Both handlers: `CompetitionLoader.LoadAsync` → `EntryCollector.CollectAsync` →
the domain function → map `string` refs back to `CompetitorId`/`GroupId`
(finding 3). `GroupScoreView` and `CompetitionScoreView` are Application-owned
view records carrying typed ids, so no engine type crosses the Api boundary
with a bare `string` where the rest of the API uses ids.

---

## Phase C — Api, guards and verification

### WI-8 — Api endpoints

`src/Soarscore.Api/Queries/Queries.cs` and matching `AddScoped` lines in
`src/Soarscore.Api/Composition.cs`:

```
app.MapQuery<ScoreTaskRound, IReadOnlyList<GroupScoreView>>("/task-round-result");
app.MapQuery<ScoreCompetition, CompetitionScoreView>("/competition-result");
```

Nouns, matching the seven existing query routes (`/entries`, `/competition`) —
`MapQueries`' own "verbs, never nouns" comment describes the command side, which
is where every route in the tree actually obeys it. `HandlerRegistrationTests`
covers the DI half automatically once the routes exist; its stale count comment
at `:71` gets corrected while we are there.

### WI-9 — Store-backed tests

`tests/Soarscore.Infrastructure.Tests/`, `Trait("Category", "Storage")`. Real
PostgreSQL via Testcontainers: create → register → draw → open entries → capture
measurements → score. Asserts a real normalised group and a real ranking off a
real event log, which is the only place the `EntryCollector` fan-out and the
Marten client-side id filtering (`MartenEntryQuery`, the bug the last thread
found) are exercised together.

### WI-10 — Reqnroll acceptance test

`tests/Soarscore.Acceptance.Tests/Features/ScoringACompetition.feature`, driving
real HTTP against the real Api. Three scenarios:

1. **A group's scores are read out after it lands** (F5J) — normalised scores,
   one winner at the normalisation target.
2. **The leaderboard drops a competitor's worst round** (a class with a
   `DropPolicy` — check the corpus for which; F3J and F5J both declare one) over
   enough rounds for the drop to bite.
3. **A competitor who did not fly a round is absent from it, not zeroed in it**
   (finding 5's consequence, and the one most likely to regress silently).

### WI-11 — The architectural-law guard test

**New:** `tests/Soarscore.Architecture.Tests/ClassAgnosticismTests.cs`. Closes
gap 6.

ArchUnitNET cannot see string literals, and reflection cannot see them either,
so this test **scans the source text** of every `.cs` file under `src/`, strips
comments and doc comments, and fails on any occurrence of `F3B|F3F|F3J|F3K|F5J|
F5K|F5L|NZMAA|ALES|Radian`. The source root is located by walking up from
`AppContext.BaseDirectory` to the directory holding `Soarscore.sln`.

Comment stripping is what makes this test viable rather than noisy: the audit
found 20 hits across `src/`, **every one of them a rule reference or an
explanatory comment**, and those are legitimate — a comment citing `F3K.7` is
documentation, not a branch. The law holds in fact today; this test is what
keeps it holding.

No allowlist. If a hit is ever legitimate outside a comment, that is a design
conversation, not a suppression.

### WI-12 — Documentation

Three items, the first needing approval under CLAUDE.md house-keeping rule 4:

1. **Restore `docs/plans/scoring-service-plan.md` and
   `docs/plans/scoring-service-issues.md` from `d1ea17d`** — *ask the user
   first*. Eleven shipped source files cite the plan in their headers, and the
   issues doc holds the eight resolved design questions whose resolutions the
   pipeline code implements (issue #4 governs WI-1, issue #5 governs group
   annulment, issue #8 governs `ByTask` drops). Restoring them fixes eleven
   dangling citations and preserves reasoning that exists nowhere else. Add a
   header note to the restored plan recording that its WI-9 is superseded here.
2. **Refresh `docs/plans/gap.md`** — it is stale on gaps 3 (nine unreachable
   events, not seven — three of them Entry's), 4 (closed for F5L and NZ Class M),
   5 (eleven files/2,153 lines; `ParameterResolver` no longer orphaned and now
   tested; `ScoreCompetition` a shell per finding 1) and 6 (five arch tests in
   three files; the DI-registration item closed by `HandlerRegistrationTests`).
3. **Update `tech-debt.md:6`** — the duplicate-`TaskRoundState` item is
   discharged in the sense that predicted: record the conversion's location and
   finding 5's rule.

---

## Dependency order

```
WI-1 (amendment resolution)  ─┐
WI-2 (retype ScoringService) ─┼─→ WI-3 (ScoreCompetition) ─→ WI-5 (properties)
WI-4 (penalty routing) ───────┘                    │
                                                   ↓
                            WI-6 (EntryCollector) → WI-7 (queries) → WI-8 (Api)
                                                                       │
                                              WI-9 (store) ← ──────────┤
                                              WI-10 (acceptance) ← ────┘

WI-11 (guard test) — independent, any time
WI-12 (docs) — last
```

WI-1, WI-2, WI-4 and WI-11 can run in parallel. WI-3 is the critical path.

## Acceptance

- Slice 1: `POST /task-round-result` returns real normalised scores for a group
  flown end to end over HTTP against real PostgreSQL.
- Slice 2: `POST /competition-result` returns a ranked leaderboard with drops
  applied, over rounds actually flown.
- Every existing test still passes. The 438-test baseline grows; it does not
  change colour.
- No `object`-typed TBD parameter remains under `src/Soarscore.Domain/Scoring/`.
- `ScoringService` has at least one production caller and its first test file.
- `ClassAgnosticismTests` passes on the tree as it stands, and fails if a class
  name is introduced outside a comment.

## What this unlocks

- **The system does its job.** Capture → score → rank is the whole product;
  after this thread the only reachable-but-unscored inputs are penalties and
  annulments.
- **Task-round state transitions become worth building.** Finding 5 derives the
  field from Entry presence precisely because nothing sets `Complete`. A
  `CompleteTaskRound` command would let the leaderboard distinguish "not flown"
  from "flown, no result" without inference.
- **The second Entry thread gets a payoff.** `MeasurementAmended`,
  `EntryAnnulled` and `PenaltyRecorded` all have consumers after this thread —
  WI-1 reads amendments, WI-2 reads annulment, WI-4 reads penalties. Today they
  would be three commands feeding nothing.

## Newly deferred by this thread

- **Reflight scoring** (`Entitled` replacement vs `Filler` better-of). Needs
  `ReflightGroupAppended` to have a decide function first; until then WI-3
  fails loudly rather than guessing.
- **Penalties and annulments end to end.** Finding 4 — unit-tested here,
  acceptance-tested when the commands exist.
- **Multi-phase ranking** (`LastPhaseReplaces`, `SplitByPromotion`). No
  reachable input while only phase 0 is ever drawn.

## Standing practice

Property-based testing with CsCheck is routine on this repo, not optional
garnish — WI-5 names seven invariants and they are part of the deliverable, not
a follow-up.
