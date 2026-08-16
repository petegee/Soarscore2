# Story — Reflights: `ReflightGroupAppended`

**Status:** Backlog — no plan yet · **Raised:** 2026-08-16

## What

`ReflightGroupAppended` is mapped and folded but unreachable — no decide function, no
command. A group that must be re-flown cannot be recorded.

## Why it matters

Reflights are ordinary at a real contest — a mid-air, a timing failure, a launch
equipment fault. Today the only way to record one is to not need one. It is the last of
the three coherent event threads left unreachable, alongside
`kanban/backlog/task-round-lifecycle.md` and `kanban/backlog/second-entry-thread.md`.

## Before starting

Check the rules first via the `fai-rules` skill — reflight entitlement and how a
reflight's score replaces or supplements the original is rule-governed and varies by
class, so it must resolve through the class definition rather than a branch on class
(CLAUDE.md's core architectural law).

**Runtime trap:** `src/Soarscore.Infrastructure/MartenConfig.cs` registers only the
event types that are currently reachable, and documents the rest as deliberately
unregistered. Appending `ReflightGroupAppended` without adding its own `MapEventType`
line fails at runtime, per LADR-0001 §4.8.
