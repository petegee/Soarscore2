# Story - Unit-aware landing capture (distance or points)

**Status:** In progress (WI-0..WI-3; WI-4 parked at its mapping gate)
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

## WI-0 contract — structural capture agreement (resolved 2026-09-08)

All seven code anchors re-verified as stated; the relevant details are cited
below where the contract leans on them. Owner decisions 1-7 are settled and are
not re-argued here. This section records the structural contract WI-1-WI-3
implement; no `/docs` change is required — `Unit` already exists on
`MetricDefinition` (ScoringVocabulary.cs:45), so this adds an attribute to
existing concepts, not a new concept.

### Corpus survey (evidence for the shape)

Sixteen `LookupTerm` sites across the twelve seed files:

- `landingDistance` — 12 sites over 11 classes (F3B; F3J preliminary and
  fly-off; F5J preliminary and fly-off; F5jNdc; F5L; X5j; NzMAles200;
  NzNAles123; NzPRadian; NzMNdc). Declared unit `m` everywhere. Every site is
  the `Then` of a `ConditionalTerm` (`T.When(...)`), in `Score` except
  SeedNzMAles200, whose landing lookup sits in `ScoreNormalised` (NZ.3.12.1 e
  — landing added after normalising). No `RateTerm`, `PiecewiseTerm` or
  `Comparison` operand anywhere in the corpus references `landingDistance`.
  Per *adopted task* there is exactly one lookup over it: F3J's and F5J's two
  tasks share their `LandingRows` declarations, but an Entry flies one task,
  so capture-time resolution is single.
- `flight.sequence` — 4 sites (F5K Tasks B and E, F5kNdc ×2, negative
  launch-cost rows). It is the intrinsic FlightInterpreter synthesises
  (FlightInterpreter.cs:37), never a declared metric: capture already refuses
  it (`captureMeasurement.metricNotDeclared`), so the points contract never
  engages for it. The walk must not mistake these sites for capturable
  lookup metrics.
- No metric is consumed by two lookups, or by a lookup plus any distance
  consumer. The remaining unit-declared metrics are all lookup-free:
  `flightTime` (rate, piecewise, comparison operands, `RankByMetric`),
  `startHeight` (piecewise), `launchAltitude` (piecewise), `legs` (rate, unit
  `legs`), `courseTime`/`glideTime`/`motorRestartRunTime` (rates),
  `targetTime` (rate + comparison right side), `overflySeconds` (comparison
  operands only — `P.Le`/`P.Gt`/`P.Eq`). Flag metrics (unit null:
  `touchedByCompetitor`, `restedWithin75m`, `startHeightRecorded`,
  `landedInDefinedArea`, `landedWithin75m`, `landedInLandingArea`,
  `amrtPresetsCorrect`, `timingDeviationInFavour`, `lostPart`,
  `touchedBeforeMeasuring`, `damagedAndNotSafelyFlyable`, ...) are comparison
  operands only (`P.Is`).
- Every landing table in the corpus closes `.Rest(0)` — `0` is an award in
  every shipped table (F3J.10.5's "over 15 → 0" row; verified against the
  verbatim source via the fai-rules script). No shipped table repeats a
  `Points` value across rows.

**Finding: no current seed hits the refusal case.** No shipped metric is a
multi-lookup or dual-consumer consumer. The ambiguous-lookup and
distance-consumer refusals below are structural guards for shapes no shipped
class exhibits; every declared metric is either exactly-one-lookup
(`landingDistance`) or lookup-free.

### C1 — the input unit is explicit on the value, and atomic with it

`MeasuredValue` gains one optional property:

```csharp
public sealed record MeasuredValue
{
    public required MeasuredKind Kind { get; init; }
    public decimal? Number { get; init; }
    public bool? Flag { get; init; }
    /// <summary>The supplied input unit on a captured observation ("m", "pts");
    /// null on definition-side literals and flag captures.</summary>
    public string? Unit { get; init; }
    public static MeasuredValue Of(decimal n) => ...;            // unchanged
    public static MeasuredValue Of(decimal n, string unit);      // new overload
    public static MeasuredValue Of(bool f) => ...;               // unchanged — no unit
}
```

Rationale: owner decision 3 ("store the value and unit together") is then
structural, not a merge performed by the decide — the caller's value object is
the stored value object. The command records keep their exact shapes
(`CaptureMeasurement(EntryRef, FlightSequence, Metric, Value)`; `AmendMeasurement(..., NewValue, Reason, By)`); the unit travels inside
`Value`/`NewValue`, so there is one source of truth for it and no
reconciliation step. A top-level command `Unit` field was considered and
rejected: it duplicates the fact the stored record must carry anyway.

- Wire shape: `"value": { "kind": "number", "number": 85, "unit": "pts" }` —
  additive `unit` key inside the existing value object. `MeasuredValue`
  serialises as a plain record exactly as `WhenNotRecorded` already does (the
  ScoringVocabulary comment's "nothing polymorphic, no `$kind` of its own"
  law). Absent/`null` unit serialises as today.
- Validation rules (both capture and amendment, in the decide functions —
  handlers already hold the resolved task):
  - Number metric with a declared unit: `Unit` is **required**; it must equal
    the declared unit or `pts`. Missing → refused (never defaulted to the
    declared unit); any other string → refused.
  - Number metric with `Unit == null` (unitless; latent in the corpus — no
    seed declares one, the model allows it): `Unit` must be absent. This
    preserves the existing unitless capture contract unchanged.
  - Flag metric: `Unit` must be absent. `MeasuredValue.Of(bool)` keeps no
    unit, so existing flag call sites compile and behave unchanged.
- Definition-side literals — `MetricDefinition.WhenNotRecorded`,
  `Comparison.RightValue` — stay `Unit == null`. They are the class's own
  numbers, not supplied observations; no capture-path rule reaches them and
  adoption is untouched.

### C2 — which metrics may accept `pts` (the structural walk)

`pts` is accepted for a metric if and only if, resolved against the **adopted
task's** shape:

1. the metric is the `MetricRef` of **exactly one** `LookupTerm` reachable from
   `Score` or `ScoreNormalised`, including `ConditionalTerm.Then`/`Else`
   nesting (both lists — NzMAles200 proves the `ScoreNormalised` arm is live);
2. the metric is **not consumed as actual distance**: no `RateTerm.MetricRef`
   or `PiecewiseTerm.MetricRef` equals it, and it appears as no `Comparison`
   `LeftMetricRef`/`RightMetricRef` in the same reachable surface — score-term
   `When` predicates, `ValidWhen` and `FlightValidWhen` all included.

The walk is the structural mirror of
`FlightMetricResolution.ReferencedMetrics`/`CollectTermRefs` (same four
inputs: score, scoreNormalised, validWhen, flightValidWhen — the same overload
idiom over `ResolvedTask`/`TaskDefinition`), collecting lookup sites and
distance-consumer flags for one metric instead of bare references.
`BestNFlights.RankByMetric` stays outside the walk, as the existing
referenced-set finding already decides (F16) — it is a ranking key, not a
distance predicate, and no corpus case exercises it on a lookup metric.

Violations refuse the `pts` form only; the declared-unit distance form remains
valid in every case (owner decision 2 — "keep existing definitions and their
distance path valid"):

- metric has no applicable lookup, or also has a distance consumer → refuse
  `pts` with `pointsNotApplicable`;
- metric has two or more applicable lookups → refuse `pts` with
  `ambiguousLookup`. Never select a first match or a union of award sets.

Duplicate `Points` values *within* one lookup's rows are not an ambiguity:
membership is a set test and the contribution equals the supplied value
either way. Only multiple `LookupTerm` sites refuse.

The handler supplies the new context from what it already loads:
`ResolvedTask` (ScoringResultTypes.cs:290) carries `Metrics`, `Score`,
`ScoreNormalised`, `ValidWhen`, `FlightValidWhen`, so both handlers pass one
extra argument — no second load, no re-derivation:

```csharp
// Domain (namespace Soarscore.Domain.Scoring) — the structural surface the
// capture contract reads; both ResolvedTask and TaskDefinition project onto it.
public sealed record TaskScoringShape(
    ImmutableArray<ScoreTerm> Score,
    ImmutableArray<ScoreTerm> ScoreNormalised,
    Predicate? ValidWhen,
    Predicate? FlightValidWhen);

public Result<MeasurementCaptured> Entry.CaptureMeasurement(
    int flightSequence, string metric, MeasuredValue value,
    DateTimeOffset capturedAt,
    ImmutableArray<MetricDefinition> metrics,
    TaskScoringShape scoringShape);                       // new parameter

public Result<MeasurementAmended> Entry.AmendMeasurement(
    int flightSequence, string metric, MeasuredValue newValue,
    string reason, string by, DateTimeOffset at,
    ImmutableArray<MetricDefinition> metrics,
    TaskScoringShape scoringShape);                       // new parameter
```

`Entry` still learns nothing class-specific: `TaskScoringShape` is task data,
the same discipline as the existing `metrics` and `penaltyDefinitions`
parameters. Existing decide callers keep passing what they have; test callers
with unitless synthetic metrics pass their task's term lists unchanged.

### C3 — exact-award validation for `pts`; distance untouched

- A `pts` capture is validated against the supplied value **with no rounding
  applied** — the metric's `Precision` does not reach a pts input (F3J's
  `Truncate 0.1 m` is a distance rule, not an award rule). The value must
  equal some `LookupRow.Points` of the single applicable lookup exactly
  (decimal equality, no epsilon, no clamp, no snap). Any other decimal →
  refuse with `pointsNotAnAward`. `0` is accepted when the table carries it —
  every shipped table does (`.Rest(0)`); F3J accepts `85 pts`, refuses
  `86 pts` and `85.1 pts`.
- A declared-unit capture keeps today's path bit-for-bit: `Precision`
  rounds the stored value, the lookup's `metricValue <= UpTo` boundary walk
  (FlightInterpreter.cs:129-136) is unchanged, and no membership check
  applies — any metres are capturable, the table decides the award.
- Validate-then-store order in both decides: existing gates first
  (annulled, flight, declared, kind, duplicate/`notCaptured`), then unit gates
  (C1), then pts gates (C2, C3) — with `pointsNotApplicable` /
  `ambiguousLookup` before `pointsNotAnAward` — then the store step, which
  rounds a distance value per declared precision and stores a pts value
  verbatim. No refusal appends an event.

### C4 — error-code set

Mirrored prefixes `captureMeasurement.` / `amendMeasurement.`; existing codes
unchanged (`entry.annulled`, `flightNotFound`, `metricNotDeclared`,
`kindMismatch`, `alreadyCaptured` / `notCaptured`, `reasonRequired`,
`byRequired`). New codes:

| Code | Condition |
|---|---|
| `unitRequired` | Number metric declares a unit and the capture carries none. Never defaulted. |
| `unitUnsupported` | Unit supplied but neither the metric's declared unit nor `pts` (e.g. `cm` on an `m` metric). |
| `unitNotAllowed` | Unit supplied on a Flag-kind or unitless (`Unit == null`) metric. |
| `pointsNotApplicable` | `pts` supplied but the metric has no applicable lookup, or is also a distance consumer (rate, piecewise, comparison operand). Distance capture stays valid. |
| `ambiguousLookup` | `pts` supplied but two or more applicable `LookupTerm`s reference the metric. |
| `pointsNotAnAward` | `pts` supplied, lookup unambiguous, but the value does not exactly equal any `LookupRow.Points` (including gaps like 85.1 and out-of-range values). |

Changing unit through amendment is legal (owner decision 5) and runs the
identical gate set: `m`→`pts`, `pts`→`m`, `pts`→`pts` and `m`→`m` are all
amendments carrying reason, author and time; the gate validates the *new*
value+unit against the same declared unit / applicable-lookup rules.

### C5 — storage, digest, projections, reporting

- Events: `MeasurementCaptured` / `MeasurementAmended` payloads carry the
  unit inside their existing `MeasuredValue` — no event-shape change beyond
  the additive optional key, so both stores (Marten/PostgreSQL, Fisher/SQLite)
  round-trip it by their existing JSON serialisation. Green-field: no
  migration, and the project holds no shipped data.
- `MeasurementDigest` needs no code change: it already resolves and returns
  whole `MeasuredValue`s, so the effective value+unit travels together,
  latest-by-`At` with log-order tiebreak unchanged (MeasurementDigest.cs:33-48).
- `ResolvedMeasurements`/`FlightResult.Measurements` therefore surface the
  supplied unit verbatim — reporting shows what was observed, never a
  reverse-computed distance (owner decision 3). `TermContribution.MetricConsumed`
  for a pts lookup contribution is the award value the term consumed.
- `DeclaredMetricView` (TaskRoundRecording.cs:112) gains the declared
  `string? Unit` so recording consumers know what may be supplied; null on
  flag and unitless metrics. `MissingMetrics`/`AwaitingCapture` logic itself
  is unchanged — it reasons on presence (C6/INV-5).
- Scope guard: `BindParameter` and `Parameter.Unit` are out of scope — the
  parameter-binding contract is separate and untouched. The unit contract
  governs `Measurement` capture and amendment only.

### C6 — engine and completeness invariants (owner decisions 4, 5, 6 as testable statements)

- **INV-1 (D4, eligibility unchanged).** For a flight with fixed non-landing
  observations and eligibility inputs, `FlightResultState` and every
  non-landing term contribution are identical whether the landing metric
  carries metres or the equivalent award in `pts`. The landing term's
  enclosing conditional reads only its own metrics (`F3J.10.8`
  `touchedByCompetitor`, `F3J.10.9` `overflySeconds`, `F3J.10.4`
  flight-validity gate) — a pts input cannot bypass, satisfy or weaken any of
  them, and selection, normalisation, penalties and drop rules are untouched.
- **INV-2 (D4, no distance leakage).** A pts-valued resolved value never
  enters a `RateTerm`, `PiecewiseTerm` or `Comparison`, and never enters a
  lookup's `UpTo` boundary walk. `FlightInterpreter.EvaluateLookup` branches
  on the value's unit: `pts` → the contribution is the award the capture
  validated (no row walk); declared unit → the existing walk. The pts branch
  occupies the same stage the lookup occupies (`Score` or `ScoreNormalised`).
- **INV-3 (D5, one active measurement).** A second capture of the same metric
  is refused (`alreadyCaptured`) regardless of the units involved —
  `85 pts` after `3.5 m` is refused as today. The only route to a different
  value or unit is an amendment; after any number of amendments exactly one
  effective value+unit resolves per metric.
- **INV-4 (D5, unit change is an amendment).** Amending between metres and
  points (either direction) appends `MeasurementAmended` retaining
  reason/author/time; the original capture stays in the log; nothing
  double-contributes.
- **INV-5 (D6, completeness follows the alternative).** A flight whose landing
  metric carries a valid pts capture is not pending on that metric:
  `MissingMetrics` and `AwaitingCapture` do not name it, the resolved flight
  is not `Pending` for landing, and `GET /task-round-recording` agrees with
  score resolution. `0 pts` (award 0 — over-15 row), `0 m` (the ≤ 0.2 m
  spot-distance award, 100 in F3J/F5L) and no measurement (pending/assumed)
  are three distinct observable outcomes.
- **INV-6 (NFR-4, no imposed order).** The two forms arrive in any order
  across a competition — points here, metres there, either amended later —
  with no capture-order gate and no competition-wide capture-mode switch.

### C7 — properties WI-3 must hold (named invariants)

- **Representation equivalence:** for a valid distance `d` with declared
  precision `p(d)`, the lookup award `A(p(d))` captured as `pts` produces the
  same landing contribution and score as `d` captured in metres — for every
  generated supported shape and every canonical seed.
- **Refusal totality:** every value that is not an award of the applicable
  table is refused; every unit other than {declared, `pts`} is refused; a
  flag/unitless metric refuses any unit; a non-lookup or distance-consumed
  metric refuses `pts` while accepting metres.

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
