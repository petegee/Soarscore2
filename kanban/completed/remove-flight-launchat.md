# Remove `Flight.LaunchAt`

**Status:** Completed 2026-08-18 · **Raised:** 2026-08-18

**Scope, confirmed by the user: `Flight.LaunchAt` only.** `Entry.WorkingTime` was
left exactly as it was — see "What was left alone" below.

## What

Delete the launch timestamp from the `Entry` aggregate: `Flight.LaunchAt`, the
`LaunchAt` field on the `FlightOpened` event, the `launchAt` parameter on
`Entry.OpenFlight`, and the `LaunchAt` property on the `OpenFlight` command and
its request body. Remove `+timestamp launchAt` from `class Flight` in
`docs/soaring-domain-class-diagram.md` and `docs/aggregate-roots.md`.

## Why it matters

Raised by the user 2026-08-18 while working WI-6 of
`kanban/in-progress/amend-a-measurement.md`. That thread set out to make a
mistyped launch time correctable and instead established that **no rule in
either rulebook requires a launch instant at all**:

- The `fai-rules` rule map's *"What the timer records"* table lists three things
  across all seven FAI classes — flight-time precision, landing distance, launch
  height. Durations, distances and heights. No wall-clock instant anywhere.
- Every timing-validity rule is expressed as a captured **flag**:
  `launchedInWorkingTime` (`F3K.7`), `landedWithinWindow` (`F3K.9.3`),
  `launchedOnSignal` (`F3K.11.3` — within 3 s of the acoustic signal),
  `launchedWithin30s` (F3F). F3B Task C records *elapsed* leg time to 1/100 s;
  F5J's AMRT is a 30 s motor-run duration with height sampled 10 s after motor
  stop; reflight adjudication turns on a hindrance being "noticed/witnessed by
  an official".
- The false-start case the field looks like it exists for is already modelled
  correctly, as data: `M.Flag("launchedInWorkingTime")`
  (`tools/Soarscore.SeedData/SeedF3K.cs:22`), consumed by
  `TaskDefinition.FlightValidWhen`.

And it cannot acquire a consumer later. Deriving flight validity in the core
system by comparing `LaunchAt` against `Entry.WorkingTime` is precisely the
class-specific leak that `TimeWindow`'s doc comment
(`src/Soarscore.Domain/Entries/Entry.cs:32-45`) and CLAUDE.md's core
architectural law forbid.

So the field is a required, caller-supplied fact that no rule asks for, nothing
reads, and nothing may read. It is also not free: it is the reason
`capture-a-score-steel-thread-plan.md` had to write down that a mistyped launch
time is uncorrectable, and the reason `amend-a-measurement.md` nearly built an
event, a fold, a command, a route and a model change to fix that.

## Blast radius

Small in `src`, wide in tests — verified 2026-08-18, re-check before acting.

- **`src` — 3 files, 9 references.** `Domain/Entries/Entry.cs` (the `Flight`
  property, the `OpenFlight` parameter and its two doc comments),
  `Domain/Entries/EntryEvents.cs` (`FlightOpened.LaunchAt`),
  `Application/Commands/Entries/OpenFlight.cs` (the command property and the
  comment at `:10` explaining why it is caller-supplied).
- **Tests — 15 files**, across Domain, Application, Infrastructure and
  Acceptance.
- **Docs** — `class Flight` in both approval-gated documents. The glossary's
  `## Flight` entry does not mention a timestamp and needs no change.
- **API contract** — the `/open-flight` request body loses a field. No
  deployment consequence: green-field, no users, no data to migrate
  (CLAUDE.md, "Project status").

## What was done

The tree as built, 2026-08-18. All green: Domain 320, Application 181,
Architecture 7, Infrastructure 80 (both backends), Acceptance 15 × 2 stores.

**`src` — three files.**

- `Domain/Entries/EntryEvents.cs` — `FlightOpened` is now `(int Sequence,
  DateTimeOffset At)`.
- `Domain/Entries/Entry.cs` — `Flight.LaunchAt` deleted; `Apply(FlightOpened)` no
  longer sets it; `OpenFlight(int sequence, int? maxLaunches, DateTimeOffset at)`
  lost its `launchAt` parameter. The comment block that used to explain why
  `launchAt` was deliberately unchecked now explains why there is no timestamp at
  all, and cites the metrics that carry launch timing instead.
- `Application/Commands/Entries/OpenFlight.cs` — the command is now
  `OpenFlight(EntryId EntryRef)`. Worth noting for its own sake: **opening a
  flight now carries no caller-supplied fact whatsoever.** The only timestamp on
  the event is `IClock`'s, which is what LADR-0001 §7 wants everywhere it can
  have it.

**Docs.** `+timestamp launchAt` removed from `class Flight` in
`docs/soaring-domain-class-diagram.md` and `docs/aggregate-roots.md`. Nothing else
in either document referenced it, and the glossary never did.

**Tests — 18 files.** Mostly mechanical: constructor arities and dropped
assertions. Three were not.

- **`OpenFlightDecideTests`** lost
  `OpenFlight_with_a_launch_outside_the_working_time_succeeds_finding_3_regression`.
  It asserted that an out-of-window `launchAt` passed through untouched; with no
  timestamp to pass through, it asserted nothing. Deleting a regression test is
  not a thing to do quietly, so the class doc comment now records where it went
  and why.
- **`CaptureMeasurementDecideTests`** is where it went, as
  `Capturing_a_false_start_observation_is_accepted_not_refused_F3K_7`. This is
  the stronger test: it captures `launchedInWorkingTime = false` **and** a real
  flight time on the same flight, so it proves both that the capture path accepts
  a false start and that recording the infraction does not suppress what was
  flown. The old test could only ever prove that a number survived a fold.
- **The acceptance scenario** was re-expressed, not deleted, exactly as this story
  required. *"A launch before the working time is recorded, not refused"* is now
  *"A false start is recorded, not refused"*, driving `launchedInWorkingTime` over
  real HTTP against the real corpus F3K. The step comment names what it guards:
  the rule staying in the class model rather than migrating into the core system.

## What was left alone

**`Entry.WorkingTime`.** Scope was `LaunchAt` only, by the user's call. The
observation that prompted the question stands and is unchanged by this story:
`Competition.OpenEntry` constructs the `TimeWindow` (`Competition.cs:950`), `Entry`
stores it, and scoring reads a `decimal?` working time resolved from the class
definition (`Scoring/ScoringResultTypes.cs:171`) rather than the Entry's window.
It is **not** the same call as `LaunchAt`, though — the *"A working time that the
rulebook leaves open-ended"* acceptance scenario uses it to prove the model
represents `WorkingTimeKind.UntilAllFlightsComplete` truthfully, which is
class-model fidelity rather than scoring, and that is a real job. Anyone taking it
up should argue it on those terms, not by analogy to this story.

The user recorded a standing doubt about it on the same day, and it is sharper than
the one this story answered: the window is not captured by anyone, it is
manufactured from `clock.UtcNow` at the moment `OpenEntry` is processed
(`Competition.cs:950`), which under NFR-4's bulk retrospective entry is
systematically wrong rather than merely unused. See
`kanban/deferred-decisions.md`, "Score capture and corrections", which also names
the two defect codes any removal must preserve.

## Before starting


- **Re-express the acceptance scenario, do not delete it.**
  `tests/Soarscore.Acceptance.Tests/Features/CapturingAScore.feature`'s *"A launch
  before the working time is recorded, not refused"* asserts *"the flight is
  recorded with its launch time unchanged"* — an assertion that disappears with
  the field. The **principle** it protects does not: NFR-4 and `F3K.7` both say an
  early launch is scored, never refused. Re-express it through
  `launchedInWorkingTime` — capture the flag as `false`, show the write path
  accepts it, and show scoring zeroes that flight per `F3K.7`. That is a *better*
  test of the rule than a timestamp that no rule reads, so this story should
  improve the suite rather than shrink it.
- **Settle `Entry.WorkingTime` at the same time — it is in the same position.**
  `Competition.OpenEntry` constructs the `TimeWindow` (`Competition.cs:950`) and
  `Entry` stores it, but scoring reads a `decimal?` working time resolved from the
  class definition (`Scoring/ScoringResultTypes.cs:171`), never the Entry's
  window. Grep found no reader in `src`. It is **not** obviously the same call —
  the *"A working time that the rulebook leaves open-ended"* scenario uses it to
  prove the model represents `WorkingTimeKind.UntilAllFlightsComplete` truthfully,
  which is class-model fidelity rather than scoring — so decide it deliberately
  with the user rather than sweeping it along with `LaunchAt`.
- **Check `Entry.WorkingTime`'s remaining purpose before removing `LaunchAt`**, in
  that order: if the window goes too, `TimeWindow` and `OpenEntry`'s resolution of
  it are in scope, and this becomes a materially larger story.
- Confirm nothing outside the repo consumes `/open-flight`'s body shape.
- This is a model change to approval-gated documents. The user proposed it
  (2026-08-18) — confirm the diff before applying, per CLAUDE.md house-keeping
  rule 4.
