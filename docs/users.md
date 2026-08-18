# Soarscore — Users

## Purpose

Identifies **who interacts with the system** and, for each, their **role**, their
**key needs**, and the **tasks** they perform. 

---

## User at a glance

| User | Software role | Interaction |
|---|---|---|
| Organiser | Administrator / setup | Direct, hands-on |
| Contest Director | Officiating authority | Direct, decisions | 
| Scorer | Per-competitor field recorder | Direct, one per pilot  |
| Pilot / Competitor | Subject & results consumer | Indirect  |

## Multiple "Hats" Rule
**One person may wear several hats — but not the Scorer.** At a small contest the
Organiser, Contest Director can be the same individual; most
obviously the **Organiser** (who sets the contest up) and the **Contest
Director** (who has authority over how it is run) are one person at many events
but distinct roles. The **Scorer** is different: because a whole group flies at
once, scoring is inherently **one Scorer per competitor**, several working in
parallel. We must not *force* separation of the administrative roles — a
single operator should be able to set up and run a contest — while supporting
many concurrent Scorers during flying.

---

## Direct users

People who operate the software themselves.

### 1. Organiser

The person who prepares the event and owns the competition data. Responsible for
getting a competition **set up correctly** before flying starts. Administrative
rather than officiating: the Organiser builds the contest, but decisions about how
it is *run* belong to the Contest Director.

**Key needs**
- Get a competition configured **correctly and quickly**, ideally from a reusable
  template, without needing to understand the scoring maths.
- Reusable **master data** (pilots, landing tables, templates) so setup is not
  repeated from scratch each event.
- Confidence that the generated **draw is fair** and defensible.


### 2. Contest Director

The official with **authority over the running of the competition** and over the
**key decisions** during it. Where the Organiser sets the contest up, the Contest
Director decides how it proceeds and makes the rulings that change results —
penalties, re-flights, retirements, accepting the draw, and locking the final
result. Highest privilege; often the same person as the Organiser in practice.

**Key needs**
- Authority to make **mid-contest interventions** — penalties, re-flights,
  retirements — and have results recompute correctly and consistently.
- Confidence the **draw is fair** and defensible if challenged, with the ability
  to reject it and re-draw.
- **Trustworthy, final** results they can lock, publish and stand behind.

### 3. Scorer

**One Scorer per competitor**, not a single central operator. During a group's
working time each flying pilot has a Scorer standing beside them who records that
pilot's task metrics — times, landings, laps, heights, motor runs,
penalties.

**Key needs**
- **Eyes on the flight, not the screen** — capture must work while watching the
  model and the pilot. 
- **Scoped to one competitor and task** — the Scorer only ever records for the
  pilot beside them, in the current group; no hunting for the right entry.
- **Record raw metrics only — no interpretation at the point of capture.** The
  Scorer enters what they observed (stopwatch time, tape reading, AMRT number,
  lap count, one record per flight — launch counts are inferred from the
  flight records, never entered); the system applies the scoring
  rules — caps, bonus tables, which flight counts, over-time detection —
  consistently from the raw data
- **Free to record when they can, not when the system says.** A Scorer may be
  flying the next round themselves, or timing for another pilot, the moment a
  flight ends; what they observed is often written down first and entered later,
  in bulk or out of order. Nothing about the running of the contest may depend
  on their being up to date — see
  [NFR-4](non-functional-requirements.md#nfr-4--no-imposed-ordering-on-score-capture).



## Indirect users

### 5. Pilot / Competitor

The reason the contest exists. In the MVP the pilot does **not enter their own
data** — their flight result is recorded by the Scorer beside them, and
the pilot *reads* the draw and results. Pilots do **not** self-score (a conflict of
interest), so they remain an indirect user by design.

That is a statement about the roles at a contest, not a capability Soarscore
polices. Under [NFR-3](non-functional-requirements.md#nfr-3--core-system-only)
the system has no user interface of its own, so whether a given consuming system
lets a competitor key in what they observed is **that system's policy question —
one Soarscore neither enables nor forbids**. What Soarscore guarantees instead is
that whoever does the recording is not made to do it in any particular order or
at any particular moment ([NFR-4](non-functional-requirements.md#nfr-4--no-imposed-ordering-on-score-capture)).
