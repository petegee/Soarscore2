# Soarscore — Non-Functional Requirements

Cross-cutting qualities the design must have, as distinct from what any one
area does.

---

## NFR-1 — One centralised, flexible competition class model

The specifics and variations of each competition type — especially the
scores/metrics recorded — must be encoded **in one central place**. Some
classes are multi-task; tasks variously require laps, time, time + landing
points, time + launch height + landing points, launch counts, target-time
calls, and so on ([rules/](rules/)). That single encoding must drive:

- **shape and structure of competition** rules about run and sequence a competition
- **what values are recorded** (fields, precision, multi-flight shape),
- **how they are validated** (caps, windows, per-class limits), and
- **how they are scored** (the system-side arithmetic).

There must be exactly one place that knows a task's shape; nothing else may
hard-code per-class behaviour. 

This solution will provide instances of these competition class models as seed
data for well-known FAI competitions so the system has sensible starting point
and defaults. Users of the system *can* author their own models for custom 
competitions.


## NFR-2 — Additive-only extensibility for new competition types

Adding a **new competition type must not require changing existing code** —
extension is additive only: a new task/class definition (NFR-1's encoding),
not edits to what already runs. Existing classes' behaviour, stored data and
results must be unaffected by the addition.

## NFR-3 — Core System Only

This system should be a core headless system only, housing the kernal only. 
It should not build a UI, or assume specific UXs. It is to be consumed by 
HTTP/REST API only. It should make no assumptions about how scores are entered, 
what devices people may use, or how this gets integrated with other systems 
to provide turn-key solutions. It must be as light-weight and as simple as 
possible. No fancy, or imagined features should be added.

