# Flights within an Entry can be recorded out of order

**Status:** Backlog · **Raised:** 2026-08-18

## What

`Entry.OpenFlight` demands the flight sequence be exactly `Flights.Length + 1`
(`openFlight.sequenceOutOfOrder`, `src/Soarscore.Domain/Entries/Entry.cs:273`), so
flight 3 cannot be recorded before flight 1. The constraint is *within* one Entry
— one competitor, one task-round — so it orders nothing across rounds, groups or
competitors. But within a multi-launch task it is a real ordering rule the system
imposes on its users.

## Why it matters

Raised by the user 2026-08-18 during the ordering audit in
`kanban/in-progress/task-round-lifecycle.md` ("The governing principle"): Soarscore
should not dictate how or when scores are collected and entered. Whoever builds on
this system may capture from a field-board, from paper, or from pilots' phones,
during the contest or entirely afterwards.

Harmless for single-flight tasks, which is most of the corpus. It bites in F3K,
where a task allows five or more launches and a pilot entering retrospectively
naturally reaches for their best flights first — "my 62 and my 58, then the rest".
A phone app collecting them in that order gets a rejection with no obvious remedy,
and the sequence number is a positional label, not a claim about chronology.

Not urgent: nothing is blocked, and the workaround (enter in order) is available
whenever the entering party knows the full set. It matters when the entering party
is a pilot on a phone rather than a scorer working down a card.

## Before starting

- Decide what `sequence` actually means. If it is a stable label for "which launch
  this was", accepting them in any order and rejecting only duplicates and gaps at
  *read* time is the smaller change. If it is meant to assert chronology, then
  `launchAt` already carries that and the sequence check is redundant.
- Check what depends on flight order downstream. `maxLaunches` counts flights, and
  the drop/selection rules in the scoring engine may assume density; confirm
  whether a sparse `Flights` array (2 and 4 present, 1 and 3 absent) is
  representable and what the scoring engine does with one, before allowing gaps.
- Cross-check the F3K rules via the `fai-rules` skill for whether launch *order*
  within a task carries any scoring meaning of its own — if it does, this is a
  domain constraint rather than a data-entry one and the story changes shape.
- Related but separate: `kanban/backlog/amend-a-measurement.md`. Both are
  "retrospective entry is a first-class workflow" problems in the `Entry`
  aggregate, and they may want to land together.
