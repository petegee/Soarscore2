# Story — The second Entry thread: amend, annul, penalise

**Status:** Backlog — no plan yet · **Raised:** 2026-08-16

## What

Three mutations the scoring pipeline can already *read* but nothing can yet *produce*:
`MeasurementAmended`, `EntryAnnulled`, `PenaltyRecorded` — the last existing on both
`EntryEvent` and `CompetitionEvent`. Folds exist; decide functions, commands, handlers
and endpoints do not.

## Why it matters

`PenaltyEngine` runs over a penalty list that is always empty, and a mis-keyed flight
time is uncorrectable — the only remedy today is to not make the mistake. At the field,
with a score already read out, that is the first thing a CD will ask for.

## Before starting

Close only the events this thread needs. "Close the remaining unreachable events" as a
goal in itself is motion without direction — the standing rule this repo has held to is
that each one closes when a command needs it.

Name the property-test invariants during planning (CLAUDE.md, Testing approach) —
amendment is a natural candidate: the fold of an amended measurement must equal the
fold of the corrected capture.

**Runtime trap:** `src/Soarscore.Infrastructure/MartenConfig.cs` registers only the
event types that are currently reachable, and documents the rest as deliberately
unregistered. Appending any of these three without adding its own `MapEventType` line
fails at runtime, per LADR-0001 §4.8.
