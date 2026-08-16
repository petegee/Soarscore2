# Story — Task-round lifecycle: `TaskRoundCompleted` / `TaskRoundAnnulled`

**Status:** Backlog — no plan yet · **Raised:** 2026-08-16

## What

Mapped, folded, unreachable `CompetitionEvent` types. Nothing transitions a task-round
off `Drawn`, so a task-round's state is inferred rather than recorded. `Finalised`
belongs to the same lifecycle and is unreachable for the same reason — a competition
cannot be closed.

## Why it matters

The leaderboard cannot distinguish **not flown** from **flown, no result** — it infers
"provisional, over rounds flown so far" from Entry presence alone. The adapter that
makes that inference is documented in `kanban/tech-debt.md`'s `TaskRoundState` item:
`Drawn`/`InProgress`/`Complete` all collapse to `Scoring.TaskRoundState.Complete`
precisely because nothing can emit `TaskRoundCompleted`. Annulment and re-flights
(`ReflightGroupAppended`) hang off the same lifecycle.

## Before starting

Read `kanban/tech-debt.md`'s `TaskRoundState` mapping note first — it records the exact
semantics the current inference has, which this story replaces rather than reopens.

**Runtime trap:** `src/Soarscore.Infrastructure/MartenConfig.cs` registers only the
event types that are currently reachable, and documents the rest as deliberately
unregistered. Appending `TaskRoundCompleted`, `TaskRoundAnnulled` or `Finalised`
without adding its own `MapEventType` line fails at runtime, per LADR-0001 §4.8.
