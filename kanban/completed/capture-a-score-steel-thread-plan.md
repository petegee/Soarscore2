# Plan — Capturing a score: the Entry write path and `entry_index`

**Status:** Complete — implemented and test-verified · **Date:** 2026-08-08

Work items are numbered `WI-n`, scoped to *this* plan document (see
`command-side-steel-thread-plan.md`'s numbering note — WI numbers reset per plan).

## Context

`gap.md` names this gap first and states it plainly: **there is currently no way to
capture a score in this system.** `src/Soarscore.Domain/Entries/Entry.cs` is folds only
— `Create` plus six `Apply` overloads and a static dispatcher, and **zero `Result<>`
decide functions.** Above the Domain there is nothing at all: no
`src/Soarscore.Application/Entries/`, no command, no handler, no endpoint, no
`MapEventType` for any Entry event. The draw is the last thing wired end to end.

Its blocking input has arrived. `Group.CompetitorRefs` (`Competition.cs:198`) carries a
real allocation, populated by the draw, and `bind-parameter-steel-thread-plan.md` made
the parameterised classes drawable. The reason Entry was gated is gone.

`entry_index` (gap 2) is the twin of this gap and is closed with it, not after it.
LADR-0001 §3 lists it as one of exactly four permitted read models and nothing exists:
zero hits for `entry_index`, `EntrySummary` or `IEntryQuery` anywhere under `src/` or
`tests/`.

**Most of the work is already done.** Reused as-is, not rebuilt:

- All six Entry events (`EntryEvents.cs`) and the whole fold (`Entry.cs:158-250`), which
  is well tested — `EntryFoldTests.cs`, `EntryModelBasedFoldTests.cs`, `EntryTests.cs`,
  and `EntryEventJsonTests.cs` in Application.Tests.
- The value objects the events already carry: `TimeWindow`, `Measurement`, `Amendment`,
  `Annulment`, `Penalty`, `MeasuredValue`.
- `ParameterResolver.Resolve` with its default fallback (WI-2 of the bind-parameter
  thread) — this thread's second consumer, after the draw.
- The read→fold→decide→append handler template, `CompetitionLoader`, `MapCommand`,
  `MapQuery`, the projection-shim pattern (`CompetitionSummaryProjection.cs`), and the
  whole Marten/Api adapter stack.

**What is genuinely missing:** three decide functions, four commands with handlers, one
new aggregate loader, one read model with its query port and projection, three
`MapEventType` registrations, four endpoints, and the tests.

### Scope: the narrow capture slice

**Decided 2026-08-08.** Three of the six Entry events land here — `EntryOpened`,
`FlightOpened`, `MeasurementCaptured`. The other three (`MeasurementAmended`,
`EntryAnnulled`, `PenaltyRecorded`) are a **second thread**, for the same reason every
thread on this repo has been one slice: they are corrections and rulings, a different
workflow from capture, and each carries its own rule check that batching would hide.

"Capture a score" end to end is the acceptance test for this thread, and it does not
need a correction path to be true.

### What the rules do and do not say (checked, not assumed)

Three rule facts shape the decide functions, and each of them **removes** a check a
naive implementation would have added.

| Fact | Source | Consequence for this thread |
|---|---|---|
| A working time admits **many attempts**, each its own launch — F3B and F5L unlimited, F3K 1–6 per task, F5J/NZ one | `F3B.1.5 a` (verbatim: "the competitor is entitled an unlimited number of attempts"); `rule-map.md` "Contest shape"; `TaskTiming.MaxLaunches` in the seed corpus | `Flight` is a bounded attempt with its own `LaunchAt` — the model already says this. The launch *count* limit is a class datum (`MaxLaunches`), so it is checked from data, never branched on. |
| A launch **before** the working time is **scored 0, not rejected** | `F3K.7`, verbatim: *"if the airplane is launched before the beginning of the working time then that flight receives a zero score"*; condensed at `f3k.md:29` | **`OpenFlight` must not gate on the working-time window.** See finding 3. |
| Flight-time precision is per class and **per metric** — 0.1 s truncated (F3K), 0.1 s (F3J), whole s (F5J/F5K/F5L, F3B-duration), 1/100 s (F3B-speed) | `rule-map.md` "What the timer records"; `F3K.7`, `F3J.10.2` | Capture precision is `MetricDefinition.Precision`, already in the class model. See finding 4. |

Two silences worth recording, per the `fai-rules` discipline that *a rule you cannot
find is a question, not an inference*:

1. **No rulebook states when a working time "starts" as a wall-clock fact.** It is a
   contest-running decision. So `TimeWindow.Start` is the clock at the moment the
   scorer opens the Entry — recorded, not derived.
2. **No rulebook forbids a competitor having two records for one task-round.** The
   opposite: a re-flight *is* a second Entry (`aggregate-roots.md` §4). So duplicate
   entries cannot be rejected on rule grounds — only the narrower "two `Original`-role
   entries" case can, and that is a bookkeeping check, not a rule. See WI-8.

### Four findings that shape the scope

All four were found while designing this thread. All four have been decided
(2026-08-08); the reasoning is recorded because two of them change the model.

#### Finding 1 — `entry_index` cannot be built from the Entry events as they stand · **fixed here**

LADR-0001 §3 defines the read model as *"Entry → competition, task-round, group,
competitor"*. `EntryOpened` (`EntryEvents.cs:42`) carries only `GroupRef` and
`CompetitorRef` — **no competition, no phase/round/task-round coordinate.** An Inline
projection sees one stream and cannot join, so the index as specified is unbuildable.

It bites the *write* path harder than the read path. Every `CaptureMeasurement` must
validate the metric against the task's `MetricDefinition` list, and every `OpenFlight`
against the task's `MaxLaunches`. Reaching the task from a bare `GroupId` means loading
the Competition and scanning every phase → round → task-round → group for a matching
id — on the highest-volume write in the system.

**Decision: the full coordinate goes on both the event and the aggregate.**

```
EntryOpened  + CompetitionRef, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal
Entry        + CompetitionRef, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal
```

This is a **class-model change** and was approved on that basis (WI-1). Ordinal
addressing is the established navigation idiom on Competition already —
`ReplaceTaskRound(phaseOrdinal, roundOrdinal, taskRoundOrdinal, …)` at
`Competition.cs:377-390` walks exactly this path. Referencing another aggregate's
internals by id is the precedent `GroupRef` and `CompetitorRef` already set
(`Entry.cs:10-12`); `CompetitionRef` is the weaker case of the same thing, a root id.

The rejected alternative was indexing by `GroupId` alone and having callers walk the
Competition. It needs no model change, but it pushes a full-structure scan onto every
measurement capture, and it makes `entry_index` a group→entry lookup rather than the
index LADR-0001 §3 describes.

#### Finding 2 — `TimeWindow.End` cannot be stated under `UntilAllFlightsComplete` · **fixed here**

`TimeWindow` requires both `Start` and `End` (`Entry.cs:34-39`), but
`WorkingTimeKind.UntilAllFlightsComplete` means, in the class model's own words, *"the
working time is not a class datum at all — the round ends when the last flight does"*
(`ScoringVocabulary.cs:240-244`). Four seed definitions use it: **F3F, F3K task C, NZ
Class M ALES 200, and NZ Class M NDC**.

That list matters. **NZ Class M ALES 200 is one of exactly the three classes the
bind-parameter thread just unblocked for the draw.** The first class that can now be
drawn end to end is one whose entries cannot state an `End`.

**Decision: `TimeWindow.End` becomes `DateTimeOffset?`, and null means exactly what the
class model already means by an absent value.** This is a **class-model change** and was
approved on that basis (WI-1).

The reasoning is the model's own, applied consistently: absent `Normalisation` means the
class does not normalise (`ScoringVocabulary.cs:268-273`), absent `GroupConstraint`
means it does not group-score (`:254-259`), and in both cases the doc comment insists
that absence is *the only truthful encoding*. A fabricated `End` under
`UntilAllFlightsComplete` is the same category of lie those two comments warn against.

The rejected alternative — requiring the caller to supply an `End` — records a guess as
a fact, and nobody knows the end of an open-ended working time at the moment it opens.

#### Finding 3 — `OpenFlight` must not gate on the working-time window · **scope removal**

`Entry.cs:30-32` currently asserts, in a doc comment: *"Flight times captured within the
owning Entry cannot exceed this window; that invariant is enforced at capture, not
here."*

**That comment is wrong, and this thread corrects it.** `F3K.7` is explicit that an
early launch is *scored*, not refused: *"This means that if the airplane is launched
before the beginning of the working time then that flight receives a zero score."* The
same clause makes the working time a scoring input in the other direction too — the
flight time runs *"until a landing … or the working time expires"*. F3B grants unlimited
attempts within the window and says nothing about refusing one. Encoding "reject a
launch outside the window" in
`Entry.OpenFlight` would put a scoring rule into the core system — precisely the
breach CLAUDE.md's core architectural law forbids, and one the class model already
handles: `TaskDefinition.FlightValidWhen` *"zeroes ONE FLIGHT while leaving it
selected (F17)"* (`ClassDefinition.cs:175-176`).

So `OpenFlight` records `LaunchAt` as observed and validates **nothing** about it. The
consequence is honest and worth stating: a mistyped launch time cannot be corrected in
this slice, because `FlightOpened` has no amendment event. That goes on the deferred
list, not into scope.

#### Finding 4 — capture-time rounding · **decided: apply it**

`MetricDefinition.Precision` is documented as *"Capture precision, 0..1: a Flag metric
has nothing to round"* (`ScoringVocabulary.cs:50-51`), and `RoundingMode` carries
`Truncate` — which exists because `F3K.7` truncates rather than rounds.

**Decision: `CaptureMeasurement` applies `Precision` and stores the rounded value.** The
signed score card is the authoritative record of what was observed (`C.16.1`,
`F3K.1.2`), and the card carries the rounded value — so the rounded value *is* the raw
observation, not a derivation from it. Storing 412.37 s under a class that records
tenths would invent a precision no timekeeper produced.

**One refactor comes with it.** `ApplyRounding` is currently a *private* method
duplicated in two places — `FlightSelector.cs:358` and `NormalisationEngine.cs:146`.
Capture would be a third copy. Extract it once to an internal helper in
`Soarscore.Domain/Scoring/` and repoint both existing call sites. No behaviour change;
the existing tests for both components are the regression net.

### Out of scope (deliberately)

- **`MeasurementAmended`, `EntryAnnulled`, `PenaltyRecorded`** — the corrections-and-
  rulings thread. See "Scope" above.
- **Reflight roles.** Every Entry opened by this thread is `ReflightRole.Original`; the
  command does not take a `Role`. `Entitled` and `Filler` arrive with
  `ReflightGroupAppended`, which is the event that creates a reflight group in the first
  place, and which is still unregistered in Marten.
- **Correcting a launch time.** Finding 3.
- **De-orphaning the scoring engine** (gap 5). This thread produces the raw data that
  makes it testable, which is the whole reason `gap.md` sequences it second, but the
  adapter and a `ScoreTaskRound` query are gap 5's thread. **Nothing here may call
  `ScoringService`** — scores are never projected (LADR-0001 §3).
- **`TaskRoundCompleted` / `TaskRoundInProgress`.** A task-round's `State` stays `Drawn`
  through this thread. Capture does not advance it; the state machine is its own thread.
- **A competitor-facing or scorer-facing UI.** API only, as with every thread so far.

### Governing documents

- `docs/aggregate-roots.md` §4 — Entry is a separate root *because* live capture is the
  high-concurrency write path. This thread is the first that actually exercises that
  argument, and WI-12 is where it gets tested against a real store.
- `docs/ladr/ladr-0001-event-store.md` §3 — the four-read-model inventory, and "scores
  are never projected". §4.4 — `ExpectedVersion` as the sole concurrency arbiter. §4.6 —
  decimals inside event JSON, which `MeasuredValue.Number` is named in as one of exactly
  two exposures. §4.8 — every event type needs its own `MapEventType`.
- `docs/ladr/ladr-0003-library-choices.md` — gains a Reqnroll entry (WI-13).
- CLAUDE.md's core architectural law — findings 3 and 4 are both applications of it.
  Nothing here may branch on class name; every constraint is read from the adopted
  definition as data.

---

## Phase A — Domain

### WI-1 — The two approved model changes

Both were approved 2026-08-08 under CLAUDE.md house-keeping rule 4. **Do these first and
land them together** — everything downstream compiles against them.

1. **`TimeWindow.End` becomes nullable** (finding 2):

   ```csharp
   public sealed record TimeWindow
   {
       public required DateTimeOffset Start { get; init; }

       /// <summary>
       /// Null under WorkingTimeKind.UntilAllFlightsComplete: the working time
       /// is not a class datum at all, the round ends when the last flight does
       /// (ScoringVocabulary.cs, TaskTiming.WorkingTime). Absence is the only
       /// truthful encoding — the same rule absent Normalisation and absent
       /// GroupConstraint follow.
       /// </summary>
       public DateTimeOffset? End { get; init; }
   }
   ```

2. **`Entry` and `EntryOpened` gain the coordinate** (finding 1): `CompetitionRef`,
   `PhaseOrdinal`, `RoundOrdinal`, `TaskRoundOrdinal`, alongside the existing `GroupRef`
   and `CompetitorRef`.

**The doc edits are part of this work item, not a follow-up.** Both changes must land in
`docs/soaring-domain-class-diagram.md` (the `Entry` and `TimeWindow` classes, §1) and
`docs/aggregate-roots.md` §4 (the Entry diagram and its prose) in the same change, or
the diagrams stop being the authority they claim to be. **No glossary change** — neither
introduces a concept; one relaxes a cardinality, the other adds ids already implied by
"an Entry belongs to a competition".

Existing fold tests (`EntryFoldTests.cs`, `EntryModelBasedFoldTests.cs`, `EntryTests.cs`)
and `EntryEventJsonTests.cs` need updating for the new fields. Add a JSON round-trip case
with `End = null` specifically — a nullable `DateTimeOffset` and a nullable
`MeasuredValue.Number` serialise by different paths, and only the latter has ever been
exercised.

### WI-2 — `Competition.OpenEntry` decide function

**The open-entry invariants live on `Competition`, not `Entry`.** Every one of them —
does this coordinate exist, was this competitor drawn into this group, is the field
still valid, what is the working time — is a question only the Competition can answer.
`Entry` at the moment of opening has no state to check against.

So the decide function sits on `Competition` and returns an event belonging to another
aggregate's stream:

```csharp
public Result<EntryOpened> OpenEntry(
    EntryId id, int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal,
    GroupId groupRef, CompetitorId competitorRef, DateTimeOffset at)
```

**This shape is new to the repo and is deliberate.** Every prior decide function returns
an event for its own stream. Flag it in review rather than copying it blindly elsewhere:
it is justified here because the Competition is the sole authority on whether an Entry
may exist and what working time it gets, and the alternative — a helper that returns a
tuple for `Entry` to assemble into an event — adds a type without moving a single check.

Checks, in `DrawPhase`'s early-return style rather than `RegisterCompetitor`'s
`Defect`-chain style, because later checks need values (the task-round, the group, the
resolved working time) computed by earlier ones and the happy path needs them again:

| Code | Condition |
|---|---|
| `openEntry.phaseNotDrawn` | no `Phase` with `Ordinal == phaseOrdinal` |
| `openEntry.roundNotFound` | no `Round` with `Ordinal == roundOrdinal` in that phase |
| `openEntry.taskRoundNotFound` | no `TaskRound` with `Ordinal == taskRoundOrdinal` in that round |
| `openEntry.groupNotFound` | no `Group` with `Id == groupRef` in that task-round |
| `openEntry.taskRoundClosed` | `TaskRound.State` is `Complete` or `Annulled` |
| `openEntry.competitorNotDrawn` | `competitorRef` not in `group.CompetitorRefs` |
| `openEntry.competitorWithdrawn` | that `Competitor.WithdrawnAt` is not null |
| `openEntry.workingTimeUndeclared` | `Timing.Kind` is `Fixed` but `Timing.WorkingTime` is null — a definition defect |
| `openEntry.parameterUnbound` | `ParameterResolver` throws `UnresolvedParameterException` resolving the working time |

Then derive the `TimeWindow` from the task reached via `TaskRound.TaskRef`
(`Competition.cs:201-205` — `TaskRef` is the task's `Code`, the only stable handle):

- **`Fixed`** → `Start = at`, `End = at + Resolve(Timing.WorkingTime)` seconds. The
  bindings dictionary is flattened last-write-wins exactly as `Competition.cs:631-633`
  already does for the draw; `AdoptedRules.Definition.Parameters` is passed as the
  default source, per the bind-parameter thread's WI-2.
- **`UntilAllFlightsComplete`** → `Start = at`, `End = null` (finding 2).

Two checks deliberately **absent**:

- **No "already open" check here.** It is a read-model question — see WI-8.
- **No `PreparationTime` handling.** `TaskTiming.PreparationTime` is a real class datum
  and half the corpus sets it, but nothing in the domain model represents a preparation
  window as state. Recording one would be a new concept. Out of scope, on the deferred
  list.

**Tests** (`tests/Soarscore.Domain.Tests/OpenEntryDecideTests.cs`): one per failure code,
plus success under `Fixed` (asserting the derived `End`), plus success under
`UntilAllFlightsComplete` (asserting `End is null`), plus a parameterised working time
that resolves from a binding and one that resolves from a declared default.

### WI-3 — `Entry.OpenFlight` decide function

```csharp
public Result<FlightOpened> OpenFlight(int sequence, DateTimeOffset launchAt, int? maxLaunches)
```

`maxLaunches` is passed in **already resolved** rather than read from a
`TaskDefinition`. This keeps `Entry` free of any dependency on the class definition's
task shape and makes the decide function trivially testable; the handler does the
resolution (WI-8). Null means the task limits launches not at all — half the corpus
(`ScoringVocabulary.cs:250`).

| Code | Condition |
|---|---|
| `entry.annulled` | `Annulment is not null` — reserved now, unreachable until the corrections thread |
| `openFlight.sequenceOutOfOrder` | `sequence != Flights.Length + 1` |
| `openFlight.maxLaunchesExceeded` | `maxLaunches is { } m && Flights.Length >= m` |

**Nothing is checked about `launchAt`** — finding 3. Correct `Entry.cs:30-32`'s doc
comment in this work item, citing `F3K.7`, so the next reader does not re-add the check.

**Tests** (`tests/Soarscore.Domain.Tests/OpenFlightDecideTests.cs`): one per failure
code, plus success, plus the sequence advancing 1→2→3 across successive folds, plus
**a launch outside the working time succeeding** — that one is the regression test for
finding 3 and should say so in its name.

### WI-4 — `Entry.CaptureMeasurement` decide function

```csharp
public Result<MeasurementCaptured> CaptureMeasurement(
    int flightSequence, string metric, MeasuredValue value,
    DateTimeOffset capturedAt, ImmutableArray<MetricDefinition> metrics)
```

Same principle as WI-3: the task's declared metrics arrive as an argument, so `Entry`
never learns which class it is flying under.

| Code | Condition |
|---|---|
| `entry.annulled` | `Annulment is not null` |
| `captureMeasurement.flightNotFound` | no `Flight` with `Sequence == flightSequence` |
| `captureMeasurement.metricNotDeclared` | `metric` not in `metrics` by `Name` |
| `captureMeasurement.kindMismatch` | `value.Kind != metricDefinition.Kind` |
| `captureMeasurement.alreadyCaptured` | that flight already holds a `Measurement` for that metric |

`alreadyCaptured` is what makes the aggregate's append-only promise enforceable rather
than aspirational: a second value for the same metric is a *correction*, which is
`MeasurementAmended`'s job, and that command does not exist yet. The failure message
should say so.

On success the value is rounded per `MetricDefinition.Precision` (finding 4), using the
`ApplyRounding` helper extracted in that finding's refactor. A `Flag`-kind metric has
nothing to round and `Precision` is null there by construction.

**Tests** (`tests/Soarscore.Domain.Tests/CaptureMeasurementDecideTests.cs`): one per
failure code, plus success for both `MeasuredKind` variants, plus **rounding applied at
each `RoundingMode`** — `Truncate` in particular, with an F3K-shaped 0.1 s metric, since
truncation is the mode that differs from `decimal.Round`'s default and the one the rules
actually name.

### WI-5 — Property tests

`tests/Soarscore.Domain.Tests/EntryCapturePropertyTests.cs`, CsCheck. Five invariants,
each named because a property test without a named invariant is just a slow unit test.

1. **Capture is append-only.** *For any accepted sequence of `OpenFlight` and
   `CaptureMeasurement` decisions, folding the resulting events never removes or alters a
   previously recorded `Measurement`: each `Flight.Measurements` array grows monotonically
   and every earlier element is unchanged.* This is **the** invariant of the aggregate —
   `Entry.cs:76-81` and `aggregate-roots.md` §4 both state it in prose, and nothing
   currently tests it against the *write* path.
2. **Flight sequences are contiguous and 1-based.** *After any accepted sequence of
   `OpenFlight` decisions, `Flights.Select(f => f.Sequence)` equals `[1..n]`.* The fold
   navigates by sequence (`Entry.cs:216-220`), so a gap silently misroutes every later
   measurement.
3. **The launch limit is exactly the class's, for every class in the corpus.** *For each
   seed definition and each of its tasks, `OpenFlight` accepts exactly
   `MaxLaunches` flights and refuses the next; where `MaxLaunches` is unset it accepts
   an unbounded run.* Parameterised by scanning `tools/Soarscore.SeedData/json/`, never
   a hard-coded class list — this is how the core architectural law gets asserted, the
   same technique WI-3 of the bind-parameter thread used.
4. **Only declared metrics are ever stored.** *For any capture accepted against any task
   in the corpus, the stored `Measurement.Metric` is one of that task's declared metric
   names and its `MeasuredValue.Kind` matches that metric's declared `Kind`.* Also
   generic over the corpus.
5. **Decide and fold agree.** *For any accepted command sequence, folding the appended
   events reproduces the same `Entry` the decide functions were reasoning about.*
   `EntryModelBasedFoldTests.cs` already model-checks the fold against hand-built events;
   this extends the same model to events the decide path produced.

## Phase B — Application

### WI-6 — `EntryLoader`

`src/Soarscore.Application/Entries/EntryLoader.cs`, mirroring `CompetitionLoader.cs`
exactly: read the stream, fold it, return the aggregate and the version the next append
must be `Exact()` against. `Entry`'s fold requires a non-null current after
`EntryOpened` (`Entry.cs:248-250` throws otherwise), so it follows `CompetitionLoader`'s
shape rather than `ClassDefinitionLoader`'s. Failure code `entry.notFound`, which the
`*.notFound` convention in `EndpointRouteBuilderExtensions.cs:62` maps to 404 with no
change to that file.

### WI-7 — The `entry_index` read model

Three pieces, following `PeopleProjection.cs` / `CompetitionSummaryProjection.cs`:

```csharp
public sealed record EntrySummary(
    EntryId Id,
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    GroupId GroupRef,
    CompetitorId CompetitorRef,
    ReflightRole Role);
```

**The coordinate and nothing else.** LADR-0001 §3 describes this as an index of *which
streams exist where*, and §3 also says scores are never projected. No flight count, no
capture timestamp, no annulled flag — every one of those makes the projection fire on
the highest-volume event in the system to store something a stream load already answers.
The consequence is that `EntryProjection.Apply` handles **`EntryOpened` only** and
passes every other `EntryEvent` through unchanged, which is exactly
`CompetitionProjection.cs:34`'s shape.

- `src/Soarscore.Application/Entries/EntryProjection.cs` — the plain fold function
  (LADR-0001 §4.3: the fold is portable, the shim is not).
- `src/Soarscore.Application/Entries/IEntryQuery.cs` — one method:
  `FindAsync(CompetitionId, int? phase, int? round, int? taskRound, GroupId?,
  CompetitorId?, CancellationToken)`. No `IQueryable` (LADR-0001 §4.2).
- `src/Soarscore.Application/Entries/FindEntries.cs` — the query and handler.

No unique index. Two Entries legitimately share a (task-round, competitor) pair once
reflights exist, so the uniqueness `PersonSummary.Email` gets would be wrong here.

### WI-8 — The three commands and their handlers

`src/Soarscore.Application/Entries/`, following `RegisterCompetitor.cs` — load, fold,
decide, append with `ExpectedVersion`.

```csharp
public sealed record OpenEntry(
    CompetitionId CompetitionRef, int PhaseOrdinal, int RoundOrdinal,
    int TaskRoundOrdinal, GroupId GroupRef, CompetitorId CompetitorRef) : ICommand<EntryId>;

public sealed record OpenFlight(EntryId EntryRef, DateTimeOffset LaunchAt) : ICommand<EntryId>;

public sealed record CaptureMeasurement(
    EntryId EntryRef, int FlightSequence, string Metric, MeasuredValue Value) : ICommand<EntryId>;
```

`At` and `CapturedAt` come from `IClock`, never the caller — as everywhere else.
`LaunchAt` is the one exception and is caller-supplied on purpose: it is a domain fact a
timekeeper observed, not the moment the POST arrived, and LADR-0001 §7 is explicit that
domain-meaningful timestamps live in the payload. `OpenFlight` does not take a
`Sequence` either — the handler derives it as `Flights.Length + 1`, so the decide
function's contiguity check (WI-3, invariant 2) guards against a fold bug rather than
against the caller.

**`OpenEntry`** appends to a **new** stream keyed on the minted `EntryId` with
`ExpectedVersion.NoStream` — the first command in the repo to open a second aggregate's
stream from a handler that also read a different aggregate. Two cross-aggregate reads
precede the decide:

1. `CompetitionLoader.LoadAsync` — required, it owns every check.
2. `IEntryQuery.FindAsync` for the same (task-round, competitor) — the **`openEntry.alreadyOpen`**
   check. Reject a second Entry when one already exists for that competitor in that
   task-round with `Role == Original`. **This is a read-model check and therefore
   advisory**, in exactly the class of race `RegisterCompetitor.cs:32-36` already
   documents and accepts: the index is Inline, so it is read-your-own-writes consistent,
   and the residual race is two simultaneous opens for one pilot — which a single
   scorer at a single task-round does not produce. It is bookkeeping, not a rule
   (see "What the rules do and do not say", silence 2); the failure message must not
   imply the rules forbid it.

**`OpenFlight` and `CaptureMeasurement`** each load the Entry *and* the Competition —
the Entry for its state, the Competition for the task's `MaxLaunches` / `Metrics`. The
Entry's coordinate (WI-1) makes the second load a direct ordinal walk rather than a
scan. Factor that walk into one internal helper, `TaskResolver`, shared by both handlers
and reused by `Competition.OpenEntry`; there is no third copy of a phase→round→
task-round→task traversal worth writing.

**Two loads on the hot capture path is accepted, not overlooked.** At ≤20 pilots and ≤8
rounds/day a Competition stream is low hundreds of events and an Entry stream 20–60
(LADR-0001 §2), folding is sub-millisecond, and the alternative — caching the adopted
rules outside the log — trades a correctness property for a benchmark nobody has run.
Revisit only if measured.

**Tests** (`tests/Soarscore.Application.Tests/Entries/`): per handler — success,
`competition.notFound` / `entry.notFound`, each decide failure surfaced faithfully,
stale-version retry, and idempotent replay. Reuse `Competitions/TestDoubles.cs`; it will
need an `IEntryQuery` fake.

## Phase C — Infrastructure, Api and verification

### WI-9 — Marten wiring

**The step most easily missed, and it fails at runtime rather than at build.** Three
registrations in `MartenConfig.cs`, alongside a new comment block in the style of
`:38-49`, recording that three of six `EntryEvent` subtypes remain unregistered because
nothing appends them yet:

```csharp
opts.Events.MapEventType<EntryOpened>("entryOpened");
opts.Events.MapEventType<FlightOpened>("flightOpened");
opts.Events.MapEventType<MeasurementCaptured>("measurementCaptured");
```

Plus `src/Soarscore.Infrastructure/Entries/EntryIndexProjection.cs` — the `IProjection`
shim over `EntryProjection.Apply`, mirroring `CompetitionSummaryProjection.cs` including
its strong-typed-id `LoadAsync` note — registered `Inline`, and
`src/Soarscore.Infrastructure/Entries/MartenEntryQuery.cs` implementing `IEntryQuery`.

### WI-10 — Api endpoints

`src/Soarscore.Api/Commands/Commands.cs` and `Queries/Queries.cs` — verbs, never nouns:

```csharp
app.MapCommand<OpenEntry, EntryId>("/open-entry");
app.MapCommand<OpenFlight, EntryId>("/open-flight");
app.MapCommand<CaptureMeasurement, EntryId>("/capture-measurement");
app.MapQuery<FindEntries, IReadOnlyList<EntrySummary>>("/entries");
```

**And the four matching `AddScoped` lines in `Composition.cs`.** `gap.md`'s Update §2
records that the bind-parameter thread shipped a route with no DI registration, caught
by manual review and by nothing else. This thread adds four at once — the largest batch
yet — which is why WI-11 exists.

### WI-11 — The DI-resolvability architecture test

`tests/Soarscore.Architecture.Tests/HandlerRegistrationTests.cs`. `gap.md` flags this as
a new, untracked item with no automated guard: `RouteShapeTests` reflects over route
*shape* (path and verb) and cannot see that a mapped command has no handler, so the
failure surfaces as a 500 on first real request.

Build the same `WebApplication` `RouteShapeTests` already builds — `Composition.Build`
exists precisely so a test can (`Composition.cs:1-4`) — enumerate every mapped
command/query type, and assert its `ICommandHandler<,>` / `IQueryHandler<,>` closes and
resolves from the built `IServiceProvider`. Cheap, and it retires a whole class of
mistake for every future thread, not just this one.

### WI-12 — Store-backed tests

`tests/Soarscore.Infrastructure.Tests/EntryCaptureEventStoreTests.cs`, tagged
`Trait("Category", "Storage")`:

1. An Entry stream round-trips through PostgreSQL — open, three flights, measurements on
   each — and folds back identical.
2. `entry_index` is populated Inline and is queryable by every filter combination
   `IEntryQuery` exposes.
3. **The payoff test:** adopt a real seed definition, register a field, draw, open an
   Entry per drawn competitor in group 1, capture a flight time for each. Against a real
   store, end to end. This is the test that could not have been written before this
   thread.
4. **Run 3 twice — once under a `Fixed` class and once under NZ Class M ALES 200** —
   because NZ-M is `UntilAllFlightsComplete` *and* parameter-bound, so it is the only
   case that exercises finding 2's null `End` and the bind-parameter thread's resolver
   together.
5. Drop the read model and replay from the log; `entry_index` rebuilds identically
   (LADR-0001 §4.10).

### WI-13 — Reqnroll acceptance test

**Approved 2026-08-08.** New project `tests/Soarscore.Acceptance.Tests`, packages
`Reqnroll.xunit.v3` plus the existing `xunit.v3` / `xunit.runner.visualstudio` (the
repo's 3.2.2 / 3.1.5 satisfy Reqnroll's ≥3.0.2 floor), added to
`Directory.Packages.props` under the existing central-package-management setup.
**LADR-0003 gains an entry recording the choice** — a new test framework is a library
decision and that ADR is where they live.

`Features/CapturingAScore.feature`:

```gherkin
Feature: Capturing a score
  A scorer at the flight line records what a competitor flew, against the
  rulebook the competition adopted.

  Scenario: A scorer captures a flight time for a drawn competitor
    Given a published F5J class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase of 4 rounds
    When the scorer opens an entry for competitor 3 in round 1, group 1
    And the scorer opens a flight launched at 10:03:12
    And the scorer captures flightTime of 412 seconds
    Then the entry holds one flight with a flightTime of 412
    And the entry appears in the index for round 1, group 1

  Scenario: A working time that the rulebook leaves open-ended
    Given a published NZ Class M ALES 200 class definition
    And a competition adopting it with 6 registered competitors
    And groupSize bound to 6 by the contest director
    And a drawn preliminary phase of 4 rounds
    When the scorer opens an entry for competitor 1 in round 1, group 1
    Then the entry's working time has no end

  Scenario: A launch before the working time is recorded, not refused
    Given a published F3K class definition
    ...
    Then the flight is recorded with its launch time unchanged
```

Driven through **real HTTP against the real API over Testcontainers PostgreSQL** — the
same `PostgresFixture` the Infrastructure tests use, plus
`Microsoft.AspNetCore.Mvc.Testing`. That scope is deliberate: `gap.md` §7 records *"No
automated end-to-end test. Nothing spins up the API against PostgreSQL"*, and both
prior threads' e2e work items were manual procedures, one of which left no evidence it
ran at all. **This work item closes that item outright**, which is why no `.http` file
is proposed to sit beside `docs/verification/bind-parameter-e2e.http` — an executable
test supersedes a manual script.

Scenario 3 is the finding-3 regression test at the acceptance level, and it is worth
having at both levels: it is the check a future contributor is most likely to "fix" by
adding.

---

## Dependency order

```
WI-1 ──┬──> WI-2 ──┬──────────────> WI-8 ──> WI-9 ──> WI-10 ──> WI-12 ──> WI-13
       │           │                 ^                  │
       ├──> WI-3 ──┼──> WI-5         │                  └──> WI-11
       │           │                 │
       ├──> WI-4 ──┘                 │
       │                             │
       └──> WI-6 ──> WI-7 ───────────┘
```

WI-1 gates everything — both model changes plus their doc edits. WI-2, WI-3, WI-4 and
WI-6/WI-7 are then independent of each other. WI-5 needs the three decide functions.
WI-8 needs the decide functions *and* the query port (for `openEntry.alreadyOpen`).
WI-11 is independent of WI-12/WI-13 and can land as soon as WI-10 does.

The `ApplyRounding` extraction (finding 4) is a prerequisite of WI-4 and touches
`FlightSelector.cs` and `NormalisationEngine.cs`; land it as the first commit of WI-4 so
it is reviewable on its own.

## Acceptance

- **A score can be captured.** Before this thread, it could not — by any path, at all.
- An Entry can be opened under both `WorkingTimeKind` variants, including NZ Class M
  ALES 200, whose `End` is null.
- A launch outside the working time is recorded unchanged, at both the domain and the
  acceptance level.
- A measurement is stored at the precision its metric declares, truncating where the
  rulebook truncates.
- `entry_index` exists, is queryable by the full coordinate, and rebuilds from the log.
- No class name appears anywhere in the new code; the two corpus-wide property tests
  (WI-5, invariants 3 and 4) assert it rather than trusting it.
- Three of six `EntryEvent` subtypes registered in Marten; three still unregistered, and
  the comment says which and why.
- Every mapped route resolves its handler from DI, proven by a test rather than by
  review.
- The repo has its first automated end-to-end test, closing that item in `gap.md` §7.
- No glossary change. Two approved class-model changes, with the diagrams updated in the
  same work item.

## What this unlocks

**The scoring engine becomes testable.** `gap.md` sequences this thread second precisely
because "de-orphan scoring first cannot be validated — without Entry there are no real
measurements to feed it". After this thread there are. Gap 5's adapter has a real Entry
stream to turn into the `resolvedMetrics` / `interpretedFlights` inputs that
`FlightInterpreter` and `FlightSelector` already expect, and the vestigial `object`
parameters at `ScoringService.cs:26,36,91,205,231`, `FlightInterpreter.cs:30` and
`FlightSelector.cs:34` can be typed and the dead ones dropped.

It also settles the shape of the second Entry thread — `MeasurementAmended`,
`EntryAnnulled`, `PenaltyRecorded` — down to the loader, the projection arm and the
handler template. Each is now a known quantity rather than a design question.

**Still gated, and not by this thread:**

- **Catalogue-choice rounds, with each round's task set at draw time**, carrying
  per-round parameter scope with them. Still the only thing between F3K/F5K and a draw.
- **Multi-task rounds (F3B)** — separately deferred.
- **Reflight groups**, and with them the `Entitled` / `Filler` roles that make
  `ReflightRole` more than a constant.
- **Task-round state transitions.** Nothing moves a `TaskRound` off `Drawn`.

## Newly deferred by this thread

For `gap.md`'s "Deliberately deferred" list, so nobody "fixes" them by mistake:

- **Correcting a launch time.** `FlightOpened` has no amendment event and none is
  proposed here (finding 3).
- **Preparation time.** `TaskTiming.PreparationTime` is real class data with no
  representation in the domain model; recording one would be a new concept and needs
  approval before it is designed (WI-2).
- **Two `TaskRoundState` enums exist** — `Competition.cs:68` (`Drawn`, `InProgress`,
  `Complete`, `Annulled`) and `Scoring/PhaseAggregator.cs:34` (`Complete`, `Annulled`).
  Different namespaces, so they compile, but they will collide when gap 5's scoring
  adapter has to map one to the other. Not this thread's to reconcile; recorded so the
  adapter thread does not discover it late. → `kanban/tech-debt.md`.

## Standing practice

Property-based testing with CsCheck is routine on this repo, not optional garnish — WI-5
names five invariants, and two of them are corpus-generic on purpose, because that is
how the core architectural law is asserted rather than assumed.
