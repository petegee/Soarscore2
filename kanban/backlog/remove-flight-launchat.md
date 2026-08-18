# Remove `Flight.LaunchAt`

**Status:** Backlog · **Raised:** 2026-08-18

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
