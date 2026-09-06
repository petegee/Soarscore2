# Story — Metric absence semantics (assumed values + pending results)

**Status:** Completed 2026-09-06 — all WIs landed and verified: Domain 701,
Application 286, Architecture 7, Infrastructure 146 (both backends), Acceptance
73/73 on sqlite and postgres; the three named invariants are in
`MetricAbsenceSemanticsPropertyTests.cs` with mutation-proven teeth. **Raised:** 2026-09-06 (owner requirement stated
interactively; mechanism, docs approvals and sequencing owner-confirmed same
day) · **Extended same day:** the owner's general tolerance rule (decision 5)
widened the story from assumed values to the full absence-semantics mechanism,
folded from a would-be second story (decision 6); NFR-4 amended accordingly
(decision 7, owner-approved).

## What

A metric declared in a competition class may carry a **`whenNotRecorded`
assumed value**: the value it resolves to when a flight carries no measurement
(and no amendment) for it. This encodes how contest scoring actually works —
**officials record exceptions, not compliance**: the timekeeper writes down the
out-of-bounds landing, the touched model, the overfly; a clean flight is
recorded as its measurements only (time, distance). Without declared
semantics, the engine's current behaviour throws on any referenced-but-
uncaptured metric (`PredicateEvaluator.cs:38-40`, `FlightInterpreter.cs:215-219`),
forcing an integrator to fabricate compliance observations — capture
`landedWithin75m = true` on every flight — pure ceremony that contradicts the
domain protocol and the audit model (the event log would hold manufactured
"measurements" indistinguishable from real ones).

### The general rule: tolerance to missing/incomplete data

The owner's governing rule (2026-09-06): **the system should be as tolerant as
possible to missing/incomplete data. Some data is critical and required;
other data is "required" only in a completeness sense — its absence is not a
system error, and the system calculates and scores what it can at that point
in time.** This is NFR-4's principle ("results derive from what is present,
not what is expected") extended to the flight-metric grain, where the current
throw contravenes it: mid-comp out-of-order capture is the *normal* state
NFR-4 exists for, and partial per-flight capture must not break scoring.

The three tiers — and the answer to "how does the system know what is
acceptable to be missing": **the class definition says, per metric**, exactly
as it says everything else about the class. The core never branches on metric
names (the architectural law).

| Tier | Situation | Behaviour |
|---|---|---|
| **1. Assumed** | the metric declares `whenNotRecorded` | absence is *informative* (exception not recorded ⇒ compliant) — resolves to the assumed value; the flight scores now |
| **2. Pending** | declared, not assumed, not yet captured | **not an error** — the flight result is `Pending` (`FlightResultState` gains the value): contributes nothing at this point in time (same arithmetic path as NoResult), everything else scores, rescoring fills it in when the capture arrives; diagnostics name the awaited entry/flight/metric |
| **3. Critical** | definition/config integrity — undeclared metricRef, kind mismatches, unbound no-default parameters | loud error, unchanged — refused at adoption (`ClassDefinitionValidation.CheckMetricReferencesResolve`) or binding time. Missing **measurements** are never tier 3; parameters stay critical (class configuration, not observations) |

Tier 2 is safe against silent definition typos precisely because adoption
already refuses any referenced-but-undeclared metricRef — a runtime-missing
metric is always a genuine capture gap. Tier 2 is a **distinct, approved
domain concept** ("pending flight result" — glossary + class diagram):
arithmetically identical to NoResult, distinguishable for reporting and
data-quality views ("3 flights awaiting capture in R2/G1" versus a genuine
no-result).

The "score what it can" composition needs no new machinery: pending flights
are simply not selectable, so a partially-captured entry scores from its
complete flights, a partially-captured group normalises over what it has, and
the leaderboard is readable at any moment. A late capture arriving after
task-round completion rides NFR-4's reopen path and the rescore fills the
cell.

Rejected alternatives (for the record): kind-based defaulting (all flags
optional ⇒ silent wrong assumptions for must-observe flags, and an implicit
convention instead of class data); missing ⇒ predicate false (a forgotten
capture silently invalidates a flight); capture-side auto-fill of assumed
values (fabricates observations into the immutable event log — poison for the
trust model; the assumption must be a scoring-time interpretation of absence,
declared in the class definition, not a stored pseudo-measurement); keeping
the throw for unassumed metrics (contravenes the owner's tolerance rule and
NFR-4 at the metric grain).

## Settled decisions (2026-09-06, Pete)

1. **Mechanism:** per-metric declared `whenNotRecorded` on `MetricDefinition`,
   kind-checked at adoption validation, resolved at the single measurement
   resolution point; **explicit capture always wins** — a recorded value (or
   amendment) is an observation and displaces the assumption; absence is the
   only trigger.
2. **Docs approvals granted (round 1):** glossary entry ("assumed value /
   when-not-recorded semantics"), `MetricDefinition` extension in
   `docs/soaring-domain-class-diagram.md`, and a `whenNotRecorded` clause in
   `docs/competition-class-notation.md`.
3. **Sequencing:** this story lands **before** the parallel-run story
   (`kanban/blocked/seed-definition-parallel-run.md`), whose harness mapping
   layer then shrinks to consuming seed-declared assumptions.
4. **Justification discipline (anti-goal guard):** every seed-class assumption
   is justified against `docs/rules/` as the rulebook's observation protocol —
   never against what makes GliderScore data score well. The parallel-run
   anti-goal ("seed classes are never tuned to GS") applies verbatim here.
5. **Tolerance generalisation:** the owner's rule — as tolerant as possible to
   missing/incomplete data; critical only where the system cannot function
   coherently; score what can be scored at that point in time — governs the
   unassumed leg too: **pending, not error** (the tier table above).
6. **Story shape:** the pending tier folded into this story (one mechanism,
   one resolution point, one test/doc surface) and the file retitled from
   `assumed-metric-values.md` — nothing cited the old filename.
7. **NFR-4 amended (owner-approved):** the second consequence now states the
   tolerance at every grain and reserves system errors for configuration and
   definition integrity. Applied to `docs/non-functional-requirements.md`
   directly on approval.
8. **"Pending flight result" approved** as a distinct domain concept (round-2
   docs approvals): glossary entry + `FlightResultState.Pending` in the class
   diagram.

## Why it matters

Real users never capture compliance ceremony — the API is tolerant of an
integrator supplying only what was observed, and tolerant of data still to
come. The event log stays honest. Mid-comp partial captures stop being
scoring failures and become pending cells the rescore fills in — NFR-4 made
true at the last grain where it was false. And the rulebook-faithful seed
classes become runnable against real comp data at all: NZ ALES 200's three
flags (`landedWithin75m`, `damagedAndNotSafelyFlyable`,
`touchedByCompetitor`) and the FAI seeds'
`overflySeconds`/`restedWithin75m`/`touchedByCompetitor` are exactly the
never-recorded shapes — without this feature the seed-definition parallel run
cannot execute a single flight, and every future rulebook-faithful class
carries the same wall.

## Plan

### WI-1 — Model + engine (`src/Soarscore.Domain`)

1. `MetricDefinition` (ScoringVocabulary.cs:39) gains
   `MeasuredValue? WhenNotRecorded` — JSON
   `"whenNotRecorded": { "kind": "Flag", "flag": true }`, the same value
   shape the predicates already use. Class definitions are **events**: the
   serialised shape must round-trip through both stores — extend the existing
   event-JSON round-trip tests (postgres + sqlite).
2. `FlightResultState` (ScoringResultTypes.cs:38) gains `Pending` — additive
   (NFR-2); arithmetic path identical to `NoResult` (contributes nothing),
   distinct identity for reporting.
3. Resolution at the single point: `ScoringService.InterpretAllFlights`
   (ScoringService.cs:669) — after `MeasurementDigest.Resolve(flight)`, per
   declared metric absent from the flight's resolved measurements: insert the
   assumed value when declared; otherwise the flight interprets as `Pending`.
   The point feeds both `FlightSelector` (task `validWhen`) and
   `FlightInterpreter` (`flightValidWhen`, score terms) — verify no other
   path resolves flight metrics (ScoreTaskRound query, TaskResolver) and
   route them through the same resolution. Pending flights are not
   selectable; an entry with every flight pending yields no result (the
   flight-less precedent, harness D4).
4. Diagnostics: a pending result names the entry, flight sequence and the
   metric awaited — visible in the result, never thrown.

### WI-2 — Adoption validation (`ClassDefinitionValidation`)

`whenNotRecorded` kind must match the metric's kind (Flag/Flag, Number/Number)
— refusal code per the existing validation-code style. No other restriction:
an assumption on a reporting-only metric is harmless; the class author owns
the semantics. Re-verify `CheckMetricReferencesResolve` covers every predicate
and score-term metricRef walk (the tier-2 safety claim).

### WI-3 — Read-model exposure

Views surfacing a task's metrics and flight/task results carry the new facts:
`whenNotRecorded` (so integrators/UIs mark a metric optional and show what
absence resolves to) and `Pending` states with their awaited-metric
diagnostics (the data-quality view). Locate the exact views in-flight; no new
query unless none exposes them today.

### WI-4 — Seed classes declare their assumptions (`tools/Soarscore.SeedData`)

Per class, each assumption cites its rule (decision 4):

- `80-nz-m-ales200.json`: `landedWithin75m` ⇒ true,
  `damagedAndNotSafelyFlyable` ⇒ false, `touchedByCompetitor` ⇒ false —
  justified against `docs/rules/nz/` (NZMAA S5 observation protocol).
- FAI duration seeds (`50-f3j`, `30-f5j`, `60-f5l`, `70-f3f`):
  `overflySeconds` ⇒ 0, `touchedByCompetitor` ⇒ false, within-75m/rested
  flags per their rules — each checked via the `fai-rules` skill. Where the
  rulebook demands the observation rather than tolerating its absence (e.g.
  F5J `startHeightRecorded` — is an unrecorded height a valid flight?), the
  seed declares **no** assumption — such flights run as Pending until the
  height arrives, which is the tolerance-correct behaviour; resolve each
  case with the skill, not by symmetry.
- F3K/F5K seeds: same sweep, same discipline.

### WI-5 — Tests: the named invariants (property-based)

Per the testing approach, the invariants articulated at planning:

1. **Transparency** — scoring is observationally identical whether an
   assumed-declared metric is explicitly captured at its assumed value or not
   captured at all. CsCheck: for generated classes (assumptions declared) and
   generated measurement sets, `score(with explicit assumed captures) ==
   score(without)`. This is the property that makes absence semantics safe.
2. **Explicit-wins** — a captured value different from the assumed value
   always scores as the captured value; the assumption never displaces an
   observation. CsCheck companion to 1.
3. **Pending-then-filled** — a flight missing an unassumed metric contributes
   nothing and everything else scores; capturing the awaited metric and
   rescoring yields the full result; the pending diagnostics name
   entry/flight/metric throughout. Example-based per pipeline stage plus a
   CsCheck sweep over partial-capture subsets: for any subset of an entry's
   captures, the score over that subset equals the full score with the
   missing flights' contributions removed (the "score what it can" invariant).
4. Unit tests: adoption kind-mismatch refusals; store round-trips (WI-1);
   parameters stay critical (unbound no-default parameter still refused —
   the tolerance boundary).

### WI-6 — Docs (approvals on record, decisions 2 and 8)

Glossary: "assumed value / when-not-recorded semantics" and "pending flight
result". Class diagram: `MetricDefinition.whenNotRecorded` +
`FlightResultState.Pending`. Notation: the `whenNotRecorded` metric attribute
clause. NFR-4: already amended (decision 7). Nothing else in `/docs` changes.

### WI-7 — Unblock the parallel run

Update `kanban/blocked/seed-definition-parallel-run.md`: its WI-1 mapping
layer shrinks to consuming seed-declared assumptions — no harness-emitted
assumed values; the unblock condition is discharged and the story returns to
`in-progress/`. The ales pair then proceeds with zero assumption machinery in
the harness.

## Verification

- Domain + Application suites green; Architecture tests green (no new
  dependencies; the feature is Domain-internal + read-model).
- Both stores: event round-trip of the new field + full acceptance suite
  (`SOARSCORE_TEST_STORE=postgres` and `=sqlite`).
- The three named invariants (WI-5) documented with their names.
- Existing replays untouched: the parity harness never hits the new paths
  (its fixtures capture everything their definitions reference) — the
  `@gliderscore` suite staying green proves the additive claim.
