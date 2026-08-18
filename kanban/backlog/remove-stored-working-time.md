# Stop storing `Entry.WorkingTime`

**Status:** Backlog · **Raised:** 2026-08-18

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

Decide deliberately whether the resolution stays where it is (computing a value
that is then discarded, purely for its two defect codes — honest but odd-looking,
and needs a comment saying so) or moves to an explicit validation step. The first is
smaller; the second is clearer. Do not leave it looking accidental either way.

## What reads it today

**One acceptance scenario**: *"A working time that the rulebook leaves open-ended"*
(`tests/Soarscore.Acceptance.Tests/Features/CapturingAScore.feature:15`), asserting
`End is null` for NZ Class M. What it actually proves is class-model fidelity — that
`WorkingTimeKind.UntilAllFlightsComplete` is represented as a genuine absence rather
than defaulted to something — and that is a real job worth keeping. But it is a job
about the **class definition**, so re-express it against the definition rather than
against a stored window. Same discipline `remove-flight-launchat.md` applied to the
false-start scenario: re-express, never delete, and prefer the version that tests
the rule where the rule lives.

## Blast radius

Verified 2026-08-18; re-check before acting.

- **`src`** — `Domain/Entries/Entry.cs` (the property and `Create`'s assignment),
  `Domain/Entries/EntryEvents.cs` (`EntryOpened.WorkingTime`),
  `Domain/Competitions/Competition.cs` (the construction at `:950` and the `else`
  branch at `:956`), plus `TimeWindow` itself if nothing else claims it. Note
  `TimeWindow`'s doc comment is one of the better explanations in the tree of why
  absence is the truthful encoding for `UntilAllFlightsComplete` — if the type goes,
  that reasoning needs a home, because it is about the class model and stays true.
- **Tests** — 16 files reference `TimeWindow`, most only to build a sample `Entry`.
- **Docs** — `Entry`'s `+TimeWindow workingTime` in
  `docs/soaring-domain-class-diagram.md` and `docs/aggregate-roots.md`, and the
  `TimeWindow` value object if it goes entirely. Approval required (CLAUDE.md
  house-keeping rule 4); the user proposed this, but confirm the diff.
- **No deployment consequence** — green-field, no users, no data to migrate.

## Before starting

- Settle the resolution question above first; it decides the shape of everything
  else.
- Check whether `TimeWindow` has any claimant besides `Entry` before deleting the
  type as opposed to the field.
- Cross-check against `kanban/backlog/entry-completeness-indicator.md`, which reasons
  about what "expected data" means per task-round. It does not use the window today,
  but it is the most likely future claimant — if a CD-facing view ever wants to say
  *"round 3 ran 10:04–10:14"*, that is the requirement that would reopen this, and it
  should be argued as a new requirement with a real capture path rather than by
  reinstating a clock-stamped field.
