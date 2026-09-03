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

**Scope of that guarantee.** A class's arithmetic is expressed as a *closed
vocabulary* of score terms (rate, table lookup, banded rate, constant,
conditional) and flight selectors. Any class whose scoring falls within that
vocabulary — which is every class we ship, FAI and NZ national alike — is pure
data, and adding it touches no code at all. A genuinely novel scoring *shape*,
one no existing class needs, requires a new term type. That is an **additive
core extension: a new variant, never an edit to an existing one**, and existing
definitions, stored data and results remain untouched by it.

The three NZ ALES classes are the sharpest evidence so far that the line is in
the right place. They come from a rulebook by a different body and they needed
**no new score-term type at all** — but they did need four structural changes
(F24–F27), every one of them additive and none of them altering a score any FAI
class produces. Novel *shapes* have not appeared; novel *pipeline stages* have.

F28 is the one exception to "none of them altering a score", and it is an
exception worth keeping in view: re-reading `F3F.1.10` showed penalty exclusion
had been modelled as an equivalence class where the rule states a pairwise
relation, so F3F under-deducted. The change was still additive — a `0..1` became
a `0..*` and no other definition moved — but the error it corrected had been
live in a class that adopted, validated and scored cleanly. Additive
extensibility protects the classes already written; it does not by itself tell
you a class is *right*.

This is deliberate. The alternative — an open expression language, so no core
change is ever needed — would satisfy the letter of this requirement while
destroying two things this system needs more: complete *static validation* of a
class definition before a competition adopts it, and deterministic re-scoring.
An error in a rulebook must be caught at adoption, not discovered mid-contest.

To keep the vocabulary from sprawling into a general rules engine (NFR-3), a
term type is admitted **only when an existing class's rules require it**, cited
to the rule that demands it.

## NFR-3 — Core System Only

This system should be a core headless system only, housing the kernal only. 
It should not build a UI, or assume specific UXs. It is to be consumed by 
HTTP/REST API only. It should make no assumptions about how scores are entered, 
what devices people may use, or how this gets integrated with other systems 
to provide turn-key solutions. It must be as light-weight and as simple as 
possible. No fancy, or imagined features should be added.

## NFR-4 — No imposed ordering on score capture

Where NFR-3 says the system makes no assumption about *how* scores are entered,
this says it makes no assumption about *when*. **Soarscore must not impose an
order on score capture, and must never gate the running of a contest on scores
being up to date.** Nothing waits for round N's scores before round N+1 can
proceed; no coordinate is unreachable because an earlier one is unrecorded.

The field is chaotic, and that is normal rather than exceptional. A pilot may
fly back-to-back across rounds — round 1 in the last group, round 2 in the
first — or be pulled straight from their own flight to time for someone else.
Scores get written down and entered when whoever holds them has a free moment,
which may be immediately, an hour later, or after flying has finished for the
day.

How scores reach the system is therefore the business of whoever uses it, not
of Soarscore. A connected field-board and timer rig feeding entries as they
happen, pen and paper transcribed in bulk that evening, or competitors' own
phones trickling entries in one at a time and in no particular order must all
work equally well, and must be able to mix within one competition.

Two consequences bind the design:

- **Validity is structural, not temporal.** Whether a score may be recorded is
  answered from the shape of the competition — does this coordinate exist, was
  this competitor drawn into it, have they withdrawn — never from what has or
  has not happened elsewhere. A draw creates every round, task-round and group
  it covers at once, so there is no "open the next round" step to be blocked on.
- **Results derive from what is present, not what is expected.** An unrecorded
  task-round is absent from the result, not scored as a zero and not an error.
  Partial data yields a partial standing, so a leaderboard is readable at any
  moment of the day without anyone having to be up to date.

**The one exception, and why it is not a contradiction.** A Contest Director may
mark a task-round complete, which does close capture for it. That is the CD
asserting the task-round's *scores are in and settled* — a statement about the
data, not about the field. It is therefore always an explicit act, never a side
effect of anything the system does on its own, and it is reversible: a score
arriving late reopens the task-round rather than being refused. See
[aggregate-roots.md §3](aggregate-roots.md#3-competition--the-event-structure-field-and-schedule).

---

## Scope amendment (2026-09-02, owner-approved) — teams in MVP software scope

**Team separation** and **team classification** are in **MVP software scope**,
as specified by the Option 2 plan in `kanban/in-progress/teams-mvp.md`. Team
separation is competition-scoped **scoring teams** — with per-member
contribution eligibility — and independent **protection groups**
(many-to-many membership) that drive same-group separation in generated
draws. Team classification is one policy declared as competition-level
configuration: sum each team's best three eligible individual scores,
tie-break by the placing sum of the contributors, then the best individual
placing.

**NFR-1 and NFR-2's class-variation law is untouched by this amendment.** The
feature adds no class-specific behaviour: the classification configuration is
competition-level data, and protection is a generic draw input — nothing
branches on a competition class, and no class's model, definition or scoring
changes.

