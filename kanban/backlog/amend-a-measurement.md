# Amend a captured measurement

**Status:** Backlog · **Raised:** 2026-08-18

## What

A captured measurement can be corrected. Today it cannot: `Entry.CaptureMeasurement`
rejects a second value for the same metric on the same flight
(`captureMeasurement.alreadyCaptured`, `src/Soarscore.Domain/Entries/Entry.cs:331`),
and the `MeasurementAmended` event that comment points at does not exist. The only
remedy for a mistyped flight time is annulling the whole Entry, which destroys the
other metrics captured alongside it and misrepresents what happened — an annulment
is a ruling, not a typo.

The append-only rule is right and stays: a correction appends a new event and the
original stays readable. What is missing is the correcting event.

## Why it matters

Raised by the user 2026-08-18 while auditing whether the system imposes ordering
on score capture (see `kanban/in-progress/task-round-lifecycle.md`, "The governing
principle"). Soarscore deliberately does not dictate *when* scores are entered:
they may arrive from a connected field-board, from paper transcribed in bulk at
the end of the day, or from twenty phones at random. Every one of those workflows
except the automated one is a human typing numbers, often hours after the flight
and often in a hurry.

Someone entering twenty rounds of cards in one sitting **will** fat-finger one.
Retrospective entry is exactly the workflow the project wants to support and
exactly the workflow that makes an uncorrectable typo intolerable. This is the
single biggest obstacle to that model — larger than anything in the task-round
lifecycle thread that surfaced it.

## Separation of duty — the open design question

Flagged by the user as the reason this is its own story rather than a one-line
event: **the corrector may need to be someone other than the capturer.** A pilot
amending their own score after the fact is a materially different act from a
Contest Director doing it, and the difference is worth recording even where it is
not enforced.

This has to be settled before the shape is fixed, because it decides whether
`MeasurementAmended` carries a `By`, whether the decide function tests it, and
whether anything above the domain has to resolve a role:

- Soarscore's trust model is explicitly **no auth, no score sign-off** — a
  club-level tool for a small trusted group, with an immutable event log
  providing auditability instead (CLAUDE.md, "Key constraints"). A hard
  role check would be the first authorisation gate in the system and should not
  be introduced casually.
- The cheapest position consistent with that model is: **record who amended and
  why, enforce nothing.** `MeasurementAmended(flightSequence, metric, value,
  reason, by, at)`, with the audit trail answering "who changed this" after the
  fact rather than the write path refusing it. `ParameterBinding.By` and
  `TaskRoundAnnulled.Reason` are both precedents for recording an actor or a
  justification without gating on it.
- The alternative — a CD-only amendment — needs the system to know who the CD
  *is*, which no aggregate models today. That is a larger change than the
  amendment itself and should be argued on its own terms.

**Recommendation to put to the user when this is taken up:** record `By` and
`Reason`, enforce no role, and revisit if a real event produces a dispute. Do not
decide this inside an implementation commit.

## Before starting

- Read `capture-a-score-steel-thread-plan.md`'s scope section, which named
  `MeasurementAmended` as deliberately deferred rather than missed, and check what
  shape it assumed.
- Settle the separation-of-duty question above with the user first.
- Check what re-scoring does with an amended measurement: scoring derives from raw
  data every time, so an amendment should need no re-scoring machinery at all —
  confirm that, because "results are derived, so a correction costs a re-query"
  is the claim `docs/aggregate-roots.md` §3 already makes for `RulesAmendment`.
- Decide whether `FlightOpened` needs the same treatment. `Entry.cs:255-261` notes
  that a mistyped `launchAt` cannot be corrected either, for the same reason.
  Likely the same story; confirm rather than assume.
