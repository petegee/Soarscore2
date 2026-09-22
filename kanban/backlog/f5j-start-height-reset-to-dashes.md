# Story — F5J 5.5.11.1 h) iii: start height reset to "---" zeroes the flight (droppable, local rule)

**Status:** Backlog (raised 2026-09-22, corpus sweep of `kanban/in-progress/add-recorded-predicate.md` WI-4 item 4)

## What

FAI F5J 5.5.11.1 h) iii (`docs/rules/source-docs/f5-electric-2026.md:709-713`): the
AMRT resets the start height display to "---" if the motor is restarted during
the flight, and in that case "the result of the flight is 0 and this 0 result
can be dropped from total score. This rule can be used as a local rule at FAI
World Cup and Open International events, but not at Category One events."

The subject is again the RECORDEDNESS of the observation — the display carries
no height — but three semantics exceed the `IsRecorded` predicate as landed:

1. **A droppable zero.** The zero can be dropped from the total; the
   `flightValidWhen` gate zeroes an *unselected* flight, not a droppable cell.
   Needs a way to distinguish gate-zeroed (consumes the slot) from
   rule-zeroed-and-droppable.
2. **A behavioural trigger.** Motor restart, not the organiser's omission of a
   capture. Encodable as a flag metric, but it is the display state "---" that
   the rule reads.
3. **Local-rule scope.** Legal only at FAI World Cup / Open International
   events, never Category One — a class-permitted *variant*, not the FAI class.

## Why it matters

The corpus sweep found this as the one additional F5J clause of the
recordedness shape beyond 5.5.11.7 e. It is not encoded anywhere today and
should not be silently dropped — but adopting it is a modelling decision (the
droppable-zero semantics) the owner must make, not an additive encoding.

## Before starting

- Confirm the owner wants 5.5.11.1 h) iii modelled at all (it is optional even
  where it applies — "can be used as a local rule").
- Design the droppable-zero shape without breaking the flight-zeroing-vs-task-
  gate combination rule (`kanban/completed/flight-zeroing-vs-task-gate.md`).
- Note: `docs/rules/f5j.md` (condensed) does not mention h) iii — the condensed
  doc needs the clause added (house rule 1: it tracks the sport, so the fix is
  to the condensed summary FROM the source doc).
