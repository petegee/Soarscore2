# Amend a captured measurement

**Status:** In progress · **Raised:** 2026-08-18 · **Planned:** 2026-08-18

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

---

# Plan

## Decisions settled before planning (user, 2026-08-18)

1. **Record `By` and `Reason`; enforce no role.** The recommendation this stub
   already carried, confirmed. `MeasurementAmended` records who amended and why;
   the write path refuses nobody. Consistent with the trust model — no auth, no
   score sign-off, an immutable log for auditability instead — and it introduces
   no authorisation gate. Both strings are validated **non-blank in the decide
   function**, following `Competition.AnnulTaskRound`'s `ReasonGiven`
   (`Competition.cs:1015-1030`) and `Competition.Finalise`'s
   `finalise.byRequired` (`Competition.cs:1096-1100`) rather than
   `BindParameter`'s handler-side `By` — an amendment's justification is a
   substantive record of a correction, exactly the distinction
   `AnnulTaskRound`'s doc comment draws.
2. **Launch-time corrections are NOT in this thread — decided 2026-08-18, after a
   rule check reversed the first answer.** The initial decision was that
   `FlightOpened`'s uncorrectable `launchAt` would ride along. Working WI-6
   overturned it: no rule in either rulebook requires a launch instant, so there
   is nothing to correct that matters. See finding 4, which now carries the
   evidence. The launch half (the former WI-6…WI-9) is deleted from this plan and
   the reasoning is recorded in `kanban/deferred-decisions.md`. The user's further
   call — that `Flight.LaunchAt` should be removed from the model altogether — is
   its own story, `kanban/backlog/remove-flight-launchat.md`, not a scope
   increase here (house-keeping rule 6).

## Findings from reading the tree

1. **The read side is already finished.** `MeasurementDigest`
   (`src/Soarscore.Domain/Scoring/MeasurementDigest.cs`) resolves the effective
   value as "the most recent `Amendment.NewValue` by `At`, ties to the
   last-appended", documents itself as *the only place amendment resolution
   happens*, and is what every pipeline stage reads. `Entry.Apply(MeasurementAmended)`
   (`Entry.cs:229-242`) folds correctly. `MeasurementAmended(FlightSequence,
   Metric, Amendment)` exists (`EntryEvents.cs:70`), and `Amendment`
   (`Entry.cs:81-90`) already carries `NewValue`/`Reason`/`By`/`At`. **Nothing
   downstream of the decide function needs writing for the measurement half** —
   which also confirms this stub's "check what re-scoring does" question: scoring
   derives from raw data on every query, so an amendment costs a re-query and no
   re-scoring machinery at all.
2. **`EntryProjection` needs no change.** Its `_ => current` arm is deliberate
   (LADR-0001 §3: scores are never projected) and an amendment changes no
   coordinate on `EntrySummary`. Verified, so that nobody re-derives it.
3. **Nothing in either approval-gated doc models events.** `grep -c` for
   `PhaseDrawn|ParameterBound|MeasurementCaptured|EntryOpened` returns 0 in both
   `docs/soaring-domain-class-diagram.md` and `docs/aggregate-roots.md`: they
   model aggregates and value objects, never the event log. So adding an event
   never needs approval; adding a *value object* to an aggregate does. Recorded
   because the first draft of this plan assumed the opposite and raised an
   approval gate that did not exist.
4. **No rulebook requires a launch instant, and `LaunchAt` is barred from ever
   becoming a scoring input.** The original finding here was the weaker "nothing
   reads `LaunchAt` today". Checked properly against the corpus via the
   `fai-rules` skill, the position is structural, not incidental:
   - The rule map's *"What the timer records"* table lists three things across all
     seven FAI classes — flight-time precision, landing distance, launch height.
     Durations, distances and heights. No wall-clock instant anywhere.
   - Every timing-validity rule is expressed as a captured **flag**, not as a
     comparison against a recorded launch time: `launchedInWorkingTime` (`F3K.7`,
     `SeedF3K.cs:22`), `landedWithinWindow` (`F3K.9.3`), `launchedOnSignal`
     (`F3K.11.3`, launch within 3 s of the acoustic signal), `launchedWithin30s`
     (F3F). F3B Task C records *elapsed* leg time to 1/100 s; F5J's AMRT is a
     30 s motor-run duration with height sampled 10 s after motor stop; reflight
     adjudication turns on a hindrance being "noticed/witnessed by an official",
     a judgment rather than a timestamp comparison.
   - It could not be otherwise: deriving flight validity in the core system by
     comparing `Flight.LaunchAt` against `Entry.WorkingTime` is precisely the
     class-specific leak `TimeWindow`'s doc comment (`Entry.cs:32-45`) and
     CLAUDE.md's core architectural law forbid. The class model owns this as
     data, via `TaskDefinition.FlightValidWhen`.

   So the scoring-relevant fact about a launch — "was this a false start" — is a
   `Measurement`, already corrected by WI-1. A mistyped `LaunchAt` has no scoring
   consequence, now or ever.
5. **The rounding rule is a real leak risk.** `Entry.CaptureMeasurement`
   rounds the observation to `MetricDefinition.Precision` before storing it
   (`Entry.cs:340-342`), on the stated grounds that the stored value *is* the
   raw observation. An amendment that skipped that step would let a correction
   carry precision a capture cannot, and the two paths would disagree about the
   same number. `AmendMeasurement` applies the identical rounding, and
   invariant P3 below is what holds them together.

## Work items

**WI-1 — `Entry.AmendMeasurement` decide function.** In
`src/Soarscore.Domain/Entries/Entry.cs`, alongside `CaptureMeasurement`.
Signature mirrors it — `(int flightSequence, string metric, MeasuredValue
newValue, string reason, string by, DateTimeOffset at,
ImmutableArray<MetricDefinition> metrics)` — with `metrics` resolved by the
handler for the same reason capture's is: `Entry` never learns which class it is
flying under. Returns `Result<MeasurementAmended>`. Defect codes, in order
checked:

- `entry.annulled` — reuse the existing code; an annulled Entry records nothing
  further, exactly as capture and `OpenFlight` already rule.
- `amendMeasurement.flightNotFound`
- `amendMeasurement.notCaptured` — amending a metric with no `Measurement` on
  that flight. This is the mirror image of `captureMeasurement.alreadyCaptured`
  and the two together make the pair total: a first value is a capture, a
  subsequent one is an amendment, and neither can impersonate the other.
- `amendMeasurement.metricNotDeclared` / `amendMeasurement.kindMismatch` — same
  two checks capture makes. `notCaptured` fires first, so `metricNotDeclared` is
  reachable only for a metric captured under a definition since amended; keep it
  anyway rather than assume `RulesAmended` never lands.
- `amendMeasurement.reasonRequired`, `amendMeasurement.byRequired` — decision 1.

Rounds `newValue` by `metricDefinition.Precision` exactly as capture does
(finding 5). Update the `captureMeasurement.alreadyCaptured` comment at
`Entry.cs:324-327`, which currently says `MeasurementAmended` "does not exist
yet".

**WI-2 — Domain tests for WI-1.** New
`tests/Soarscore.Domain.Tests/AmendMeasurementDecideTests.cs`, mirroring
`CaptureMeasurementDecideTests.cs`: one example per defect code, plus the happy
path asserting the emitted `Amendment` carries the rounded value, the reason,
the by and the clock's instant. Property tests P1–P4 below live here too.

**WI-3 — `AmendMeasurement` command and handler.**
`src/Soarscore.Application/Commands/Entries/AmendMeasurement.cs`, copying
`CaptureMeasurement.cs`'s shape verbatim — `EntryLoader`, `CompetitionLoader`,
`TaskResolver`, decide, `AppendAsync` with `ExpectedVersion.Exact(version)`.
`record AmendMeasurement(EntryId EntryRef, int FlightSequence, string Metric,
MeasuredValue NewValue, string Reason, string By) : ICommand<EntryId>`. `At`
comes from `IClock`, never the caller — the same rule capture holds, and the
reason `MeasurementDigest`'s latest-by-`At` ordering can be trusted.

**WI-4 — Route.** `app.MapCommand<AmendMeasurement, EntryId>("/amend-measurement")`
in `src/Soarscore.Api/Commands/Commands.cs`, in the Entries block.

**WI-5 — Register the event type.** One line in
`src/Soarscore.Infrastructure/SoarscoreEventTypes.cs`:
`(typeof(MeasurementAmended), "measurementAmended")`. **This is the runtime trap
both backlog stubs warn about** (LADR-0001 §4.8) — an unregistered type fails at
append, not at compile. Update the block comment at lines 64-67, which lists
`MeasurementAmended` among the three deliberately-absent Entry events, to name
the two that remain.

*WI-1…WI-5 are the measurement half and need no approval. They are independently
shippable; if WI-6 stalls, they land alone and the launch half becomes a stub.*

**WI-6 — Application-layer handler tests.** Mirror
`tests/Soarscore.Application.Tests/Commands/Entries/CaptureMeasurementHandlerTests.cs`
for both handlers: fakes only, no store. Cover the `IClock` instant reaching the
`Amendment.At`, and the optimistic-concurrency append.

**WI-7 — Acceptance scenarios.** Add to
`tests/Soarscore.Acceptance.Tests/Features/CapturingAScore.feature`, which
already owns the capture workflow and has the F5J fixtures these need:

- *A mistyped flight time is corrected without annulling the entry* — capture
  `flightTime` of 4120, amend to 412 with a reason, then assert the scored
  result uses 412 **and** that the entry still holds the other metrics captured
  alongside it. That second assertion is the story's whole point: the remedy
  today destroys them.
- *The original value is still readable after a correction* — the append-only
  promise, asserted end-to-end rather than only in the fold.
- *A launch time is corrected* (WI-6 permitting).

Both stores per CLAUDE.md: run with `SOARSCORE_TEST_STORE=postgres` and
`=sqlite`.

**WI-8 — Board and floor reconciliation.**

- `tests/Soarscore.Architecture.Tests/HandlerRegistrationTests.cs` — the
  sanity floor is 17 with the comment "Seventeen commands + nine queries"
  (`:69-71`). Two new commands make it nineteen; raise the floor and fix the
  comment, per the precedent that a stale floor stops catching the reflection
  technique silently breaking.
- `kanban/backlog/second-entry-thread.md` — it claims `MeasurementAmended`
  alongside `EntryAnnulled` and `PenaltyRecorded`. Edit it to drop the
  amendment (its own "Before starting" says to close only the events a command
  needs) and rename it to the annul-and-penalise pair it actually is.
- `kanban/backlog/out-of-order-flight-entry.md` — its "Before starting" names
  this story as a likely joint landing. It is **not** merged in: WI-7 corrects a
  launch time in place, whereas that story changes what `sequence` means and
  needs an `fai-rules` check first. Add a line there recording that this thread
  landed without it and what it left available.
- `kanban/tech-debt.md` — nothing to tick.
- `kanban/deferred-decisions.md` — the launch-time entry (decision 2) is written
  when this story completes, if it is not already there.

## Property-based invariants (CsCheck)

Named here, during planning, per CLAUDE.md's testing approach. All four are
about the capture/amend pair agreeing, which is exactly where a second write
path can silently diverge from the first.

- **P1 — An amendment is indistinguishable from having captured the right value.**
  For any flight, metric and non-empty sequence of amendments, the
  `MeasurementDigest.Resolve` of the amended measurement equals the `Resolve` of
  a measurement captured once with the latest amendment's value. *The invariant
  the stub itself named.* It is what makes "correct by appending" honest: a
  reader must never be able to tell a corrected number from a right-first-time
  one.
- **P2 — Amending never destroys history.** For any sequence of *n* amendments,
  the folded `Measurement.Value` still equals the originally captured value and
  `Amendments.Length == n`, with every appended `Amendment` present in order.
  The append-only promise, stated as a property rather than trusted to the fold.
- **P3 — Capture and amendment round identically.** For any `decimal` and any
  `Precision` in the corpus, the `NewValue` on the emitted `MeasurementAmended`
  equals the `Value` on the `MeasurementCaptured` that the same input would
  produce. Guards finding 5 — the one place the two paths can drift.
- **P4 — Resolution follows `At`, not append order.** For any set of amendments
  with distinct `At`, the resolved value is the one with the greatest `At`
  regardless of the order they were appended in; and for amendments sharing an
  `At`, the last-appended wins. This is `MeasurementDigest`'s documented rule,
  which nothing has yet tested against an adversarial ordering — and out-of-order
  arrival is precisely what NFR-4's world produces.

## Out of scope

- **Correcting a launch time**, and `FlightAmended` with it — decision 2 and
  finding 4. Recorded in `kanban/deferred-decisions.md`.
- **Removing `Flight.LaunchAt`** — the user's call once finding 4 landed, and a
  larger change than this thread: it touches an event contract, the `/open-flight`
  request body, both approval-gated docs and fifteen test files. Its own story,
  `kanban/backlog/remove-flight-launchat.md`.
- `EntryAnnulled` and `PenaltyRecorded` — the remainder of
  `second-entry-thread.md`, untouched here (WI-8 re-scopes that stub).
- Amending anything on the `Competition` aggregate. `RulesAmended` is a
  different act with retroactive scoring consequences and has no story yet.
- Out-of-order flight sequences — `out-of-order-flight-entry.md`, deliberately
  separate (WI-8).
- Any role or authorisation check, per decision 1.
