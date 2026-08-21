# Stop storing `Entry.WorkingTime`

**Status:** Completed — implemented 2026-08-21 · **Raised:** 2026-08-18

Promoted from `kanban/deferred-decisions.md` on 2026-08-18, the day it was recorded
there — the user's doubt is firm enough to be work waiting for a turn rather than a
settled non-decision, which is what that file is for.

## What

`Entry.WorkingTime` is a `TimeWindow` — a `Start` and a nullable `End` — carried on
the `EntryOpened` event and held on the aggregate. Stop storing it.

**Keep the resolution.** The window is *computed* at `OpenEntry` from the task's
declared working time, and that computation is load-bearing for reasons the storage
is not. Separating the two is the actual work; this is not a delete.

## Why it matters

Raised by the user 2026-08-18, immediately after
`kanban/completed/remove-flight-launchat.md` shipped, on the grounds that **a scorer
never captures it.** That is right, and the code is more pointed than "unread":

```csharp
workingTime = new TimeWindow { Start = at, End = at.AddSeconds((double)resolvedWorkingTimeSeconds) };
```

`src/Soarscore.Domain/Competitions/Competition.cs:950`, where `at` is
`clock.UtcNow` at the moment the `OpenEntry` command is *processed*
(`OpenEntryHandler`, `OpenEntry.cs:65`). Nobody observes the window and nobody
types it. It is manufactured from the wall clock of whoever opened the entry.

**Which makes it wrong, not merely unused.**
[NFR-4](../../docs/non-functional-requirements.md#nfr-4--no-imposed-ordering-on-score-capture)
exists precisely because entries legitimately arrive from paper transcribed in bulk
at the end of the day, or from twenty phones at random. Every one of those workflows
stamps a working time with no relation to when the group actually flew: twenty
entries typed at 9pm carry a 9pm working time, and one typed the following week
carries the following week's. A field that is systematically false in the project's
own headline workflow is a liability rather than dead weight — sooner or later
something will display it and someone will believe it.

Nothing in scoring reads it. `ScoringResultTypes.cs:171` carries a `decimal?`
working time resolved from the class definition, which is a *duration*, and that is
what the pipeline uses. The stored window is not an input to any result.

This is a stronger case than the one `remove-flight-launchat.md` answered.
`LaunchAt` was at least a real fact a human had observed; this one is invented.

## What a removal must not break

Both survive the window, but not by accident — they are about **resolving** the
declared working time, not about **storing** the result:

- **`openEntry.workingTimeUndeclared`** (`Competition.cs:932`) — a `Fixed`-timing
  task that declares no `WorkingTime` is a class-definition defect, and `OpenEntry`
  is where it surfaces.
- **`openEntry.parameterUnbound`** (`Competition.cs:948`) — resolving the declared
  working time through `ParameterResolver` is what forces a CD-parameter working
  time to be **bound before entries can open**. This is the F5K case, where the
  working time is a CD parameter rather than a class constant. Delete the
  resolution along with the window and this gate disappears silently, with no test
  failing to say so.

## Decisions — user-confirmed 2026-08-21

1. **Resolution stays inline, value discarded.** `ParameterResolver.Resolve` stays
   exactly where it is in `Competition.OpenEntry`, its result assigned to discard,
   with a comment naming it validation-only (it exists for the two defect codes
   above). No extracted validation method. The two defect codes, their messages and
   their tests stay byte-identical.
2. **`TimeWindow` is deleted entirely.** No `src` code claims it besides `Entry`
   itself (verified 2026-08-21: the only `src` hits are `Entry.cs`,
   `EntryEvents.cs`, `Competition.cs`, plus definition-side `TaskTiming` fields
   which are `NumberOrParam`, not `TimeWindow`). Test hits are sample-construction
   only. Its doc comment's reasoning moves — see WI-1.
3. **The acceptance scenario is re-expressed via the `/competition` query.**
   Assert, over HTTP, that the task the entry opened against has
   `Timing.Kind == UntilAllFlightsComplete` and `Timing.WorkingTime is null` in the
   *adopted* definition — class-model fidelity, tested where the rule lives.
4. **Docs diff approved by the user 2026-08-21** (house-keeping rule 4). The exact
   diff is in WI-6 and is approved as written — apply it with the rest of the work.

## Plan

Work items are ordered; WI-1+2 are one compile unit and land together. WI-3/4/5
need WI-1+2 built first (every test file constructs `EntryOpened` or `Entry`).
Nothing here needs a change to `SoarscoreEventTypes.cs` or `MartenConfig` — no new
event types, `EntryOpened` is already registered, and green-field means no stored
events carry the old shape (CLAUDE.md, "Project status").

### WI-1 — Domain: `EntryOpened` and the `Entry` aggregate

**`src/Soarscore.Domain/Entries/EntryEvents.cs`**

- `EntryOpened` loses its `TimeWindow WorkingTime` parameter. New signature:
  `EntryOpened(EntryId Id, CompetitionId CompetitionRef, int PhaseOrdinal,
  int RoundOrdinal, int TaskRoundOrdinal, GroupId GroupRef,
  CompetitorId CompetitorRef, ReflightRole Role, DateTimeOffset At)`.
  Every existing parameter keeps its position relative to the others; only
  `WorkingTime` (currently second) goes.
- Header comment: remove `TimeWindow` from the "payload reuses Domain's own
  value-object records (TimeWindow, Measurement, ...)" list, and reword the
  `EntryOpened` line — it currently says the event "opens the working-time window
  for one competitor"; say instead that it opens one competitor's record for one
  task-round under one `ReflightRole`.

**`src/Soarscore.Domain/Entries/Entry.cs`**

- Delete the `TimeWindow` record (lines 46-58).
- Delete the `WorkingTime` property (line 154) and the `WorkingTime =
  @event.WorkingTime` assignment in `Create` (line 191).
- The aggregate's header comment (lines 4-8) says Entry is "one competitor's
  working-time window and everything captured in it" — reword to "one competitor's
  record for one task-round and everything captured in it".
- `TimeWindow`'s doc comment (lines 32-45) is one of the better explanations in
  the tree and must not simply vanish. Split its two concerns:
  - *Absence under `UntilAllFlightsComplete`* — already lives on
    `TaskTiming.WorkingTime`'s doc comment
    (`src/Soarscore.Domain/PublishedClassDefinition/ScoringVocabulary.cs:241-245`).
    Nothing to do; verify and leave alone.
  - *Capture discipline* — "flight times are NOT checked against the working time
    at capture; F3K.7's early launch scores zero, it is not refused; the class
    model owns launch-timing rules as data via `TaskDefinition.FlightValidWhen`" —
    condense into the `Entry` class header, adding one sentence: the system no
    longer stores a window at all (this story), so there is nothing for capture to
    check against — the strongest form of the same argument. Cite
    `kanban/backlog/../completed/remove-stored-working-time.md` (this file, by its
    final path) and `remove-flight-launchat.md` as precedent.

### WI-2 — Domain: `Competition.OpenEntry`

`src/Soarscore.Domain/Competitions/Competition.cs`, the `OpenEntry` decide function
(lines ~925-970).

- Delete `TimeWindow workingTime;` (line 925), the two constructions (lines 950
  and 957) and the whole `else` branch — under `UntilAllFlightsComplete` there is
  now nothing to compute, and the comment at 954-956 points at reasoning that
  lives on `TaskTiming.WorkingTime` already.
- Keep, unchanged in wording and order: the `WorkingTimeKind.Fixed` guard and its
  `openEntry.workingTimeUndeclared` failure (lines 926-933); the
  `FlattenParameterBindings` call (935-937); the try/catch around
  `ParameterResolver.Resolve` and its `openEntry.parameterUnbound` failure
  (939-948).
- Change only the success line of the try block: the resolved value is now
  discarded —

  ```csharp
  // Validation only, deliberately: the resolved seconds are discarded. The
  // call exists for its two failure modes — openEntry.workingTimeUndeclared
  // above, and openEntry.parameterUnbound here, which is what forces a
  // CD-parameter working time (F5K) to be bound before entries can open.
  // No window is stored: remove-stored-working-time.md.
  _ = ParameterResolver.Resolve(
      declaredWorkingTime, bindings, AdoptedRules.Definition.Parameters);
  ```

- `EntryOpened` construction at the end drops the `workingTime` argument; `at`
  remains a parameter (still used for the event's `At`).
- Remove any `using` that becomes unused (check what brought `TimeWindow` into
  scope — it is in `Soarscore.Domain.Entries`, which `Competition.cs` may use for
  other types; remove only if nothing else needs it).
- The three success-path tests in `OpenEntryDecideTests` that assert
  `result.Value.WorkingTime` (see WI-3) will stop compiling until WI-3 lands —
  WI-1, WI-2 and WI-3's mechanical edits may be built together to keep the tree
  green per commit.

### WI-3 — Tests: mechanical removal (Domain, Application, Infrastructure)

No assertion in these files means anything by referencing the window except where
WI-4/WI-5 say otherwise. Verified 2026-08-21:

- **`tests/Soarscore.Domain.Tests/OpenEntryDecideTests.cs`** — the two failure
  tests (`OpenEntry_against_a_task_whose_Fixed_timing_declares_no_WorkingTime_...`
  at ~214 and `OpenEntry_against_an_unbound_undefaulted_parameterised_WorkingTime_...`
  at ~227) are this story's invariant: **leave them byte-identical**. The
  success-path assertions on `result.Value.WorkingTime` (~249-250, 270-271) are
  deleted. The two tests at ~275-300 (`OpenEntry_resolves_a_parameterised_
  WorkingTime_from_a_binding` — asserts 420 s — and `..._from_its_declared_default`
  — asserts 600 s) are **not** deleted but moved: see WI-4.
- **`tests/Soarscore.Domain.Tests/`** — `EntryTests` (drop the `WorkingTime =`
  initialisers at 27/112/154 and the `window` local at 193), `EntryFoldTests`,
  `EntryModelBasedFoldTests`, `OpenFlightDecideTests`, `CaptureMeasurementDecideTests`,
  `AmendMeasurementDecideTests`, `EntryCapturePropertyTests` (all: delete the
  `SampleWorkingTime` static field and its uses), `ScoringServicePropertyTests`
  (drop the `new TimeWindow { ... }` at line 74 in sample-Entry construction).
- **`tests/Soarscore.Application.Tests/`** — `EntryEventJsonTests` (delete
  `SampleWorkingTime`; **check the expected JSON** — if the round-trip asserts a
  `workingTime` field on the serialised `EntryOpened`, update the expected payload;
  the event's `$kind` discriminator is unchanged), the four handler test files
  (`OpenEntryHandlerTests` 202, `OpenFlightHandlerTests` 105,
  `CaptureMeasurementHandlerTests` 113, `AmendMeasurementHandlerTests` 131 — drop
  the `TimeWindow` argument from `EntryOpened` constructions), `EntryProjectionTests`,
  `EntryLoaderTests` (delete `SampleWorkingTime`).
- **`tests/Soarscore.Infrastructure.Tests/EntryCaptureEventStoreTests.cs`** —
  round-trip test (~115-135): drop the `TimeWindow` argument and the assertions at
  167-168 (`entry.WorkingTime.Start/End`). Payoff scenarios 3/4 (~308, 373-379,
  392-399): delete the `expectNullWorkingTimeEnd` parameter and both its assertion
  branches — scenario 4 (NZ Class M) keeps its point without it: `groupSize` must
  be bound before entries can open, and the open succeeds under
  `UntilAllFlightsComplete` timing. Update the header comments at 26-28 and 58 that
  explain scenarios via `TimeWindow.End`. Runs against both backends unchanged via
  the fixture pattern.
- **Property-based testing note** (CLAUDE.md): no new property test is warranted.
  A removal's invariant is "everything not touching `WorkingTime` behaves
  identically", which the existing Domain/Application/property suites hold simply
  by staying green; the two defect codes are example-based by nature and already
  have stable-code tests.

### WI-4 — Tests: re-express binding/default resolution at the scoring seam

Today `OpenEntryDecideTests:275-300` is the **only** proof in the suite that a
parameterised working time resolves from a round binding (420 s) and from a
declared default (600 s). With the window gone, that fact is no longer observable
at `OpenEntry`, but it is still true and still load-bearing — scoring resolves the
same parameter through the same `ParameterResolver`
(`src/Soarscore.Domain/Scoring/ParameterResolver.cs:233` `ResolveTiming`, reached
from the public resolve at line 118, surfaced as `ResolvedTiming.WorkingTime` in
`RoundData` and consumed as the `decimal?` at `ScoringResultTypes.cs:171`).

Re-express, never delete — same discipline WI-5 applies to the acceptance
scenario:

- Add Domain tests that exercise the **scoring** entry point (follow how
  `ScoringServicePropertyTests` builds scored inputs; it shows literal
  `TaskTiming` at line 43 — the new tests instead use a `TaskTiming` whose
  `WorkingTime` is a `ParameterRef`, plus `ParameterBindings`, with the binding
  pattern available in `BindParameterPropertyTests`):
  one test where the working time resolves from a **binding** (assert the
  resolved seconds equal the bound value, e.g. 420), one where it resolves from
  the **declared default** (e.g. 600). Assert on the resolved timing in the
  scored result, not on any stored state.
- Place them in a sensibly named new file (e.g.
  `tests/Soarscore.Domain.Tests/ResolvedWorkingTimeTests.cs`) or extend
  `ParameterResolverTests` if the public surface makes that natural — the private
  `ResolveTiming` is not directly testable; go through whatever public method
  `ScoringService` uses.
- Delete the two moved tests from `OpenEntryDecideTests` and leave a one-line
  comment in that file's header naming where they went, following
  `remove-flight-launchat.md`'s precedent for relocated regression tests.
- The `UntilAllFlightsComplete` success assertion at ~270-271 is superseded by
  WI-5's re-expressed acceptance scenario plus `TaskTiming`'s own encoding —
  delete it with the same header note.

### WI-5 — Tests: re-express the acceptance scenario

**`tests/Soarscore.Acceptance.Tests/Features/CapturingAScore.feature`** — scenario
"A working time that the rulebook leaves open-ended" (line 15). The Given/When
lines stay. Replace `Then the entry's working time has no end` with a step worded
to match its new meaning, e.g. `Then the task leaves the working time open-ended`.

**`tests/Soarscore.Acceptance.Tests/Steps/CapturingAScoreSteps.cs`** — replace the
step at line 208 (`ThenTheEntrysWorkingTimeHasNoEnd`):

- GET the existing `/competition` query (`GetCompetition` → `CompetitionView`,
  which carries the whole `Competition` aggregate including
  `AdoptedRules.Definition`). This is the suite's established pattern — every
  step is HTTP (`ApiClient.PostCommandAsync` for commands, `ApiClient.GetAsync`
  for queries), and `/competition` is already fetched exactly this way at
  `CapturingAScoreSteps.cs:319`. Follow that existing usage.
- From the returned `Competition`: find the task-round the entry was opened
  against (`Phases[phase].Rounds[round].TaskRounds[taskRound]` — the same
  ordinals the When step used, which are 0/1/1 in this scenario), take its
  `TaskRef`, and find the task in
  `AdoptedRules.Definition.Phases.SelectMany(p => p.Tasks).First(t => t.Code == taskRef)`
  — the same lookup idiom `Competition.OpenEntry` itself uses.
- Assert `task.Timing.Kind` is `WorkingTimeKind.UntilAllFlightsComplete` **and**
  `task.Timing.WorkingTime` is `null`. Both halves matter: the kind says the
  rulebook leaves it open; the null says the model encodes that as genuine
  absence, not a default — which is the class-model fidelity the scenario has
  always guarded.
- Keep/refresh the step's comment: it guards that absence is the truthful
  encoding for `UntilAllFlightsComplete`, now asserted against the adopted
  definition (where the rule lives) rather than a stored window (which was
  clock-manufactured and is gone — this story).
- `EntryReader` and `AcceptanceFixture` survive untouched — other steps
  (`false start`, `correction`, `entry holds one flight`) still use them; only the
  window assertion goes. The `TimeWindow` mentions in their header comments
  (`AcceptanceFixture.cs:18`, `EntryReader.cs:4`) should be reworded to the
  remaining examples (a flight, a measurement).
- `CapturingAScore.feature.cs` is generated from the `.feature` — regenerate or
  hand-edit consistently with how previous story threads updated it (check git
  history; Reqffiroll regenerates on build).

### WI-6 — Docs — approved 2026-08-21, apply with the rest of the work

The diff, verbatim:

**`docs/soaring-domain-class-diagram.md`**
- Remove `+TimeWindow workingTime` from `class Entry` (~line 107).
- Reword the principle bullet at ~1096-1103 from `TimeWindow.end` to
  `TaskTiming.workingTime` — the principle ("a working time's end/length is
  optional, and its absence is a statement too... recording a guessed value would
  be a fabricated fact") **stays**; its subject becomes the definition-side field,
  consistent with the TaskTiming block at lines 721-732 which already documents
  "workingTime is populated if and only if kind is Fixed".
- No `TimeWindow` value-object class exists in either document's diagrams — only
  the Entry field references it — so there is nothing else to delete.

**`docs/aggregate-roots.md`**
- Delete the `workingTime.end`-nullable paragraph (~lines 382-386; its content
  lives in the class diagram's TaskTiming note).
- Remove `+TimeWindow workingTime` from the mermaid `class Entry` (~line 392).

**Glossary** — no change (checked 2026-08-21: it mentions working time only in
prose about phases, parameters and flights; no `TimeWindow` entry).

Also re-check both documents' §Entry prose for "working-time window" phrasing at
implementation time; reword minimally where it survives.

### WI-7 — Verification

1. `dotnet build` the solution.
2. Run each test project. Note the known solution-wide parallel-run Marten
   migration flake (`kanban/tech-debt.md`, last item) — if a solution-wide
   `dotnet test` goes red in `MartenPersonSummaryProjection`/schema migration on
   the first acceptance scenario, re-run that project alone before diagnosing.
3. `tests/Soarscore.Infrastructure.Tests` runs against both backends by fixture
   (SQLite fast loop untagged; PostgreSQL tagged `Storage` via Testcontainers).
4. Acceptance twice: `SOARSCORE_TEST_STORE=postgres dotnet test` and
   `SOARSCORE_TEST_STORE=sqlite dotnet test` (project-scoped, not solution-wide).
5. **Acceptance invariant for the story:** the two defect-code tests
   (`openEntry.workingTimeUndeclared`, `openEntry.parameterUnbound` — Domain and
   the Infrastructure payoff scenario 4) are green and unmodified except for
   mechanical `EntryOpened` arity; the re-expressed acceptance scenario and the
   two new scoring-seam resolution tests are green.

### Sub-agent split

- Agent A: WI-1 + WI-2 + WI-3's mechanical `OpenEntryDecideTests` edits (one
  compile unit — `src` plus the tests that construct `EntryOpened`).
- Agent B (after A): WI-3's remaining mechanical test edits + WI-4.
- Agent C (after A): WI-5.
- Docs (WI-6) — approved 2026-08-21; any agent may apply it.
- WI-7 last, by whoever finishes.

## Before starting

- ~~Settle the resolution question~~ — settled 2026-08-21, decision 1 above.
- ~~Check whether `TimeWindow` has any claimant besides `Entry`~~ — settled
  2026-08-21, decision 2: none in `src`; deleted entirely.
- Cross-checked against `kanban/backlog/entry-completeness-indicator.md`: it does
  not use the window. If a CD-facing view ever wants *"round 3 ran 10:04–10:14"*,
  that reopens as a new requirement with a real capture path — not by reinstating
  a clock-stamped field.
