# Story — The second Entry thread (annul and penalise)

**Status:** Backlog — no plan yet · **Raised:** 2026-08-16 · **Re-scoped:** 2026-08-18

## What

Two mutations the scoring pipeline can already *read* but nothing can yet *produce*:
`EntryAnnulled` and `PenaltyRecorded` — the last existing on both `EntryEvent` and
`CompetitionEvent`. Folds exist; decide functions, commands, handlers and endpoints
do not.

## Why it matters

`PenaltyEngine` runs over a penalty list that is always empty, and a mis-keyed flight
time is uncorrectable if an amendment is not the right fit. (That last one is already
in hand: corrections landed separately as `kanban/completed/amend-a-measurement.md`,
of which `MeasurementAmended` was the whole subject — so this thread no longer
names it.)

## Before starting

Close only the events this thread needs. "Close the remaining unreachable events" as a
goal in itself is motion without direction — the standing rule this repo has held to is
that each one closes when a command needs it.

**Runtime trap:** the event-type registry
(`src/Soarscore.Infrastructure/SoarscoreEventTypes.cs`) registers `EntryAnnulled` and
`PenaltyRecorded` only once a command appends them. Appending either without adding
its alias line fails at runtime, per LADR-0001 §4.8. The block comment above the
Entry slice already names both as the events that remain.