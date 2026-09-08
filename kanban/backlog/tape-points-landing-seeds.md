# Story - Unit-aware landing capture (distance or points)

**Status:** Backlog
**Raised:** 2026-09-07 - club landing tapes record points rather than metres;
the jerilderie-2010 parallel-run refusal exposed the input mismatch.
**Replanned:** 2026-09-08 - owner-approved replacement of the tape-seed plan.
The filename stays stable for existing citations. The former proposal for
`51-f3j-tape-points` / `SeedF3JTapePoints` is superseded, not implementation work.

## What

Accept a landing measurement on the existing class-declared metric in either
its declared distance unit or explicit points (`pts`). The caller supplies the
unit; the system validates it. Distance follows the existing lookup path;
points must exactly match an available award in that same landing table and
then supply the lookup contribution. Existing landing and flight eligibility
conditions apply identically to both forms.

One Competition Class definition, one landing table, two input forms. Keep
`50-f3j` and its `landingDistance` metric unchanged; no new metric, seed,
identity lookup, scoring vocabulary variant or class-definition schema change.
The capture/amendment contracts and recorded measurement do need to retain the
supplied unit, and the generic scoring and completeness paths must understand
it. This is product work, not just fixture plumbing or a UI conversion.

WI-0 through WI-3 deliver and verify that shared capability. WI-4 retains the
original Jerilderie witness work, subject to its separate penalty-mapping gate.
`kanban/backlog/f5j-christchurch-parallel-run-witness.md` consumes WI-0 through
WI-3 and owns F5J source-mark decoding and its own pair; it must not duplicate
the generic implementation or introduce a tape seed.

## Why it matters

A scorer may read metres from a tape or read the points already printed on it.
Neither changes the competition's rules. Requiring different class definitions
for those observations duplicates rules and multiplies catalogue choices with
each input convention. The existing landing table is sufficient to determine
both distance scoring and the exact set of acceptable supplied points.

## Owner decisions (2026-09-08)

1. **Exact points validation.** Allow direct landing points only when the
   decimal value matches a `Points` value in the applicable lookup rows. Reject
   non-matches; never round, interpolate, clamp or snap an invalid award into
   an allowed one. F3J accepts `85 pts`, rejects `86 pts` and `85.1 pts`.
   Do not duplicate the allowed set in a validator or seed. Zero is accepted
   when present in the table, as it is in the shipped F3J and F5J tables.
2. **Explicit input unit.** For the same landing metric, accept its declared
   distance unit (currently `m`) or `pts`. Reject a missing or unsupported
   unit, including a distance unit that does not match the declaration. Do not
   infer units from the number, metric name, class designation or fixture slug.
   This story does not introduce general unit conversion or a unit registry.
3. **Preserve what was supplied.** Store the value and unit together, including
   amendments. Do not reverse points into a made-up distance. Points identify
   an award, not an exact distance: F3J's 90-point band is over 2 m through 3 m.
   No reverse-distance API or distance-band display is required here.
4. **Eligibility is unchanged.** Supplied points replace only the lookup's
   numeric contribution, inside its existing conditional and pipeline stage.
   They cannot bypass landing eligibility, flight validity, selection,
   normalisation, penalties or other class arithmetic. F3J.10.8 and F3J.10.9
   still remove the landing bonus for touch and overfly respectively.
5. **One active measurement.** Distance and points are alternative forms of
   the same metric, not two contributions. A second capture is refused as
   today; changing the value or unit is an explicit amendment retaining reason,
   author and time. Both representations may be used across one competition;
   no competition-wide capture-mode switch or imposed capture order.
6. **Completeness follows the alternative.** Valid supplied points satisfy
   the landing input requirement; the flight must not remain pending for
   distance. Missing capture retains existing absence semantics. `0 pts`,
   `0 m` and no measurement are distinct: no bonus, the spot-distance award,
   and missing evidence respectively.
7. **No different-table rescoring use case.** Do not design for rescoring an
   observation against a different landing table. Ordinary recalculation after
   a measurement or eligibility correction, against the competition's rules,
   still works and remains deterministic.

## Implementation boundaries

- **Generic, definition-driven core.** Resolve the applicable lookup from the
  adopted task and metric, including nested conditionals and scoring stages.
  No `if F3J`, no hard-coded `landingDistance` branch in the core, and no GS
  scheme numbers there. Fixture adapters may know source field names/schemes.
- **Do not mistake points for distance downstream.** A points-valued input
  must never reach a distance predicate, rate or other distance consumer as
  though it were metres. WI-0 must specify the supported structural shape and
  refusal behaviour when one metric has multiple different lookups or other
  consumers requiring actual distance. Do not select the first matching table,
  accept a union of unrelated award sets or invent a distance. Keep existing
  definitions and their distance path valid; ask before any broader model need.
- **Precision is input-specific.** Distance keeps its declared precision and
  existing lookup boundary behaviour. Supplied points are validated exactly
  before any rounding; they must not inherit distance precision. Existing
  later score rounding stays in its declared stage.
- **No inferred compatibility defaults.** Update repository capture callers
  to supply the required units; do not silently treat omitted landing units as
  metres. There is no shipped-data migration requirement. Unitless metrics and
  flags are not landings: WI-0 must explicitly preserve their valid capture
  contract rather than inventing units for them.
- **Source marks are not necessarily points.** GS scheme 3 in Jerilderie is
  already entered points; scheme 11 in the F5J fixture maps marks 55-100 to
  awards 5-50. The F5J adapter must decode those marks before supplying `pts`.
  Retain source traceability in the fixture/ledger; the core record holds what
  the importer actually submitted. Membership validation is not a claim that
  a physical tape's bands match the rulebook.

## Requirements cross-check

- `docs/users.md`, Scorer: record what was observed, including the tape
  reading; the system applies scoring and eligibility. Supplied tape points
  are an observation, not a final-score override. This preserves the glossary's
  distinction between Measurement and computed Score.
- NFR-1: the existing class definition remains the only source of metric
  shape, lookup awards and eligibility. NFR-2: future classes use the same
  generic mechanism without core class-specific branches.
- NFR-3: headless capture API, no UI or device assumptions. NFR-4: either
  representation can arrive out of order; completeness must recognise it.
- F3J.10.5 defines the awards; F3J.10.6 defines nose-to-spot measurement;
  F3J.10.8/.9 govern landing eligibility. Accepting a reported award does not
  amend those rules or certify the tape's physical calibration.
- `kanban/deferred-decisions.md` contains no settled landing-input restriction.
  Its fly-off draw deferral stands. No new domain concept is proposed and no
  `/docs`, glossary or class-diagram edit is authorised by this story. If WI-0
  reveals a conflict or needs a new concept, surface it before proceeding.

## Code anchors (re-verify before implementation)

- `src/Soarscore.Domain/PublishedClassDefinition/ScoringVocabulary.cs`:
  `MetricDefinition.Unit`, `LookupTerm.MetricRef` and `LookupRow.Points` already
  describe the declared input unit and award table. `MeasuredValue` currently
  distinguishes only number and flag.
- `src/Soarscore.Domain/Entries/Entry.cs`: `Measurement`, `Amendment`,
  `CaptureMeasurement` and `AmendMeasurement` currently have no input-unit
  discriminator; validation sees metric declarations, not the scoring terms.
- `src/Soarscore.Application/Commands/Entries/CaptureMeasurement.cs` and
  `AmendMeasurement.cs`: load the adopted task and pass its metrics to the
  decide functions; extend the contract to supply enough definition context.
- `src/Soarscore.Domain/Scoring/MeasurementDigest.cs`,
  `FlightMetricResolution.cs` and `FlightInterpreter.cs`: preserve the active
  value/unit, resolve completeness and evaluate the lookup without treating
  points as metres. Resolution currently pends before evaluating conditions.
- `src/Soarscore.Application/Queries/Scoring/TaskRoundRecording.cs`: independently
  reports missing/awaited inputs and must agree with score resolution.
- `tools/Soarscore.SeedData/SeedF3J.cs`: shared `LandingRows`, both phases'
  lookup wrappers and flight-validity gates are the unchanged rulebook witness.

## Plan

### WI-0 - Resolve the capture contract

1. Re-verify the anchors and applicable lookup shapes across the seed corpus.
   Specify how a metric identifies an unambiguous supported lookup and how
   points are refused where actual distance is also required. Record the
   resulting structural contract in this story, not a new class-specific rule.
2. Specify the unit field on capture/amendment and the stored active value/unit
   representation, including API validation, unitless/flag handling, amendments,
   projection/serialization and reporting. Keep the class definition unchanged.
3. Confirm the requirements cross-check and plan the tests below. Move this
   story into `in-progress/` before code. Do not reconfirm the owner's settled
   points-vs-distance decisions or gate the capability on tape geometry.

**Done-when:** supported structure and rejection contract are explicit; no
silent ambiguous-lookup handling or unresolved class-model changes.

### WI-1 - Capture, amendment and audit

1. Extend the API, application and domain capture/amendment path with the
   explicit supplied unit. Validate declared metric, value kind, supported
   unit and exact lookup-award membership on both capture and correction.
2. Persist and project value/unit together, including correction history and
   effective latest amendment. Preserve existing concurrency and append-only
   behaviour. Invalid capture/amendment must append no event.
3. Update repository callers/tests for the unit contract without numeric
   behaviour changes to their existing input paths. Test missing/wrong unit,
   undeclared metric, wrong kind, invalid points, duplicate capture and unit
   changes through amendment. No seed refactor or new catalogue entry.

**Done-when:** the real capture path retains and validates both forms; event
round trips and amendments retain their units on both storage backends.

### WI-2 - Scoring and completeness

1. Carry the active input representation through metric resolution and lookup
   evaluation. Declared distance follows existing precision/lookup semantics;
   validated points supply only the applicable lookup contribution.
2. Keep every enclosing condition, flight-validity gate and scoring stage in
   force. Do not feed supplied points into distance-dependent consumers.
3. Align pending-flight resolution and recording-completeness reporting:
   either valid form fulfils landing capture, missing remains missing, and
   an amendment changing unit recalculates without double counting.

**Done-when:** both forms score equivalently under the same eligibility inputs;
completeness and scoring agree, with no changed distance-path results.

### WI-3 - Property and acceptance proof (shared prerequisite)

1. **Representation equivalence (CsCheck):** for a valid distance `d`, let
   `p` be the applicable lookup award after existing capture precision. Holding
   all other observations and eligibility inputs fixed, capturing `d` in its
   declared unit or `p` in `pts` produces identical landing contributions and
   resulting scores. Exercise supported generated lookup shapes as well as
   canonical seeds, not a duplicate implementation of the same table.
2. **Eligibility invariance (CsCheck):** changing the input representation
   cannot enable a landing bonus or valid flight that the same conditions
   disable. Sweep the supported conditional and scoring-stage cases.
3. **Membership and rejection:** test every F3J award (including 0), invalid
   gaps such as 86 and 85.1, out-of-range values and wrong/missing units.
   Include exact lookup thresholds and adjacent representable distances,
   open-ended/duplicate-award rows, and unsupported ambiguous consumers.
4. **BDD workflow:** on unchanged canonical F3J, capture distance and points
   across flights, inspect scores/completeness, reject an invalid capture,
   correct both value and unit, and correct an eligibility flag. Verify audit
   history, `0 pts` versus `0 m` versus absent, and no double contribution.
5. Run Domain, Application, Architecture and Infrastructure tests and the full
   acceptance suite against both SQLite and PostgreSQL. Verify canonical seed
   definitions and catalogue count have no delta from this story. Run
   `graphify update .` after code changes.

**Done-when:** the generic capability is verified through capture, storage,
correction, scoring and reporting. F5J may consume this prerequisite regardless
of the separate Jerilderie mapping gate; coordinate the board before pulling
that story into implementation.

### WI-4 - Jerilderie witness (separate mapping gate)

The former plan also added an unrelated `competitionPenalty` of 100 to its
derived seed. That is not a landing-input change and must not be carried into
canonical F3J. The fixture's offence is unrecorded: do not label it
`towlineNotClearedWithin30s` just because the point cost happens to match, drop
the row/penalty, or weaken the harness refusal.

1. **Gate before full replay:** surface that unidentified penalty and obtain
   an evidence-backed, owner-approved disposition. If none exists, leave the
   pair unrunnable and its mapping status honest. Do not hold the generic
   capability hostage: park the remaining story or, with owner agreement,
   split the witness into a backlog stub before closing this story.
2. When the gate is resolved, replay under **`50-f3j`**, capturing scheme-3
   landing readings (including zero) as `landingDistance` with unit `pts`.
   Preserve fixture evidence and record the input interpretation in provenance.
   Validate through the production API, not a harness-only bypass.
3. Re-verify the seed's no-default bindings. The old plan proposed
   `carryPenalties = false` and `flyoffMinRounds = rounds flown`, disclosed as
   dormant bindings because only the preliminary is drawn. Keep unknown
   parameters a loud refusal and disclose flag bindings honestly; no seed
   defaults added to suit the fixture.
4. Author the witness ledger at
   `tests/GliderscoreFixtures/jerilderie-2010/parallel-run/50-f3j.json` and a
   scenario using seed `50-f3j`. Triage measured duration, normalisation and
   drop differences with citations; do not require identical placings or
   invent ranking differences from an aggregate difference alone.
5. Verify the pair on both stores. Only then mark its mapping row `done` and
   record coverage under `50-f3j`, with no new seed/count. Reconcile board debt
   and deferrals, move the story to `completed/` and update its status. Update
   the F5J backlog story's prerequisite citation if it is still open; never
   edit a completed story to match later changes.

**Done-when:** the full witness is honestly mapped and verified, or its
remaining work is explicitly parked/split by owner agreement. Landing support
alone is not a completed full-fixture witness.

## Jerilderie evidence retained from the former plan

These are the 2026-09-07 planning measurements, not a new run; re-verify before
WI-4. Sources are `tests/GliderscoreFixtures/jerilderie-2010/`, its source
scheme and the replay/parallel-run harness.

- Comp 4, `DurGeneral`, 63 pilots, 14 rounds x 5 groups, group sizes 11-14,
  target 600 s, landing scheme 3; 843 flown rows and 39 all-zero rows.
- Scheme 3 "F3J/F3L/F5L Enter Points" has 23 identity rows: 30-90 in steps
  of 5 and 91-100 in steps of 1. Recorded landings are those values plus 0;
  84 flown rows have zero landing points. These match F3J.10.5's award set.
- 145 flights exceed 600 s, maximum 656 s. GS duration decays after target;
  F3J's seed caps the time contribution (F3J.10.1 c). The former plan expected
  raw and normalised differences from this, not landing lookup differences.
- GS drops at rounds 6 and 12 versus F3J's one drop after seven qualification
  rounds (F3J.3.1 a). Record only the score/ranking differences actually seen.
- One 100-point penalty, R11/G3 pilot 2, is the unresolved mapping gate above.
  One make-up row, R13 pilot 29 counting for R12, uses the existing destination
  mechanics; with a single candidate, Replacement and BetterOf coincide.
- GS normalised cells were all integral; rounding differences were expected
  unwitnessed. The old run plan relied on declared assumptions for
  `overflySeconds = 0`, `touchedByCompetitor = false`, `restedWithin75m = true`.
  Re-verify and disclose these, rather than claiming all eligibility was
  actually observed or synthesising exception measurements.
- Only drawn phases are finalised; the existing `ClosingACompetitionSteps`
  fixture uses canonical `30-f5j` and exercises a never-drawn fly-off. No
  fly-off draw/promotion work is introduced here. Canonical F3J's minimum group
  of 6 fits the fixture groups.

## Cross-story contract

- This story owns the unit-aware product capability, its property invariants
  and BDD proof. F5J waits for WI-0 through WI-3; there is no "whichever seed
  variant lands first" pattern or shared-seed extraction left to perform.
- `kanban/backlog/f5j-christchurch-parallel-run-witness.md` owns source
  scheme-11 decoding, canonical `30-f5j`'s separate 75 m fix, scored-window
  prescription, provenance/ledger widening and its measured witness. No
  `31-f5j-tape-points` seed. Physical tape geometry is not a prerequisite.
- Track each story's actual lane in open-story citations. Later F5J fixtures
  retain their own pairing/triage decisions; this agreement does not silently
  mark them supported or create further seed variants.
