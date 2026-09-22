# Story — Presence-gated flight validity (`IsRecorded` predicate)

**Status:** Completed 2026-09-22 · **Raised:** 2026-09-21 — owner
requirement, voiced through the NdcScore organiser experience (NDC F5J class):
entering the day's scores must be ONE input per fact. Today the organiser types
a start height and then must ALSO assert "the height was recorded" (key tick
column, or a compliance entry in the round drop-down). That second input is
silly, unnecessary and confusing — and it is forced by the vocabulary, not by
the UI.

## What

Amends the absence semantics of `metric-absence-semantics.md` (WI-1/WI-4) with
one new fact: a predicate that reads a metric's **recordedness** — whether the
flight carries a measurement — so a class can say "an unrecorded X fails the
flight" without declaring an assumed value and without a companion flag.

FAI 5.5.11.7 e — "the AMRT does not record any Start Height data", one of the
*the Flight is cancelled and recorded as a zero score if* clauses
(`docs/rules/source-docs/f5-electric-2026.md:847`) — carried by NZ.0.3 c into
the NDC F5J class and present directly in the FAI F5J seed, cancels a flight
whose AMRT records no Start Height data. The rule's subject is the
RECORDEDNESS of the observation, not a value:

- A gate-referenced metric without `whenNotRecorded` pends on absence
  (FlightMetricResolution tier 2) before any predicate evaluates — absence is
  a capture gap, never an evaluable fact.
- The only zero-on-absence encoding today is an assumed flag, and an assumed
  metric is by definition a non-key, drop-down-captured exception whose
  "recorded" side needs explicit capture — a second input contradicting the
  captured height. (An attempted encoding — `startHeightNotRecorded` with
  `whenNotRecorded: true` — was drafted and rejected 2026-09-21: it surfaced
  a double-negative drop-down entry and silently zeroed flights whose height
  WAS typed unless a second, contradictory input was ticked.)

The predicate vocabulary (`Comparison`, `AllOf` only — `ScoringVocabulary.cs:147-171`)
cannot express recordedness; `Comparison` throws on a missing left metric
(`PredicateEvaluator.cs:38-40`) — absence is a throw, not a `false`.

## Settled decisions (2026-09-22, Pete)

1. **Name: `IsRecorded`** — `public sealed record IsRecorded : Predicate`,
   wire `$kind: "isRecorded"`, notation `recorded(<metric>)`. (The
   story's original candidates were `Recorded` / `IsRecorded` / `Present`.)
2. **Scoping confirmed: `flightValidWhen`-only (v1).** Adoption refuses
   `IsRecorded` in `ValidWhen` and `ConditionalTerm.When`, where per-term
   evaluation could reach an absent metric and throw. Widen with the first
   rule that cites it elsewhere (house pattern: disjunction was readmitted
   the same way).
3. **NDC F5J seed adopts on the new version once landed.** Confirmed.
4. **Both F5J seeds change** (owner decision, extending the story's original
   single-seed scope): `SeedF5jNdc` AND `SeedF5J`. The FAI seed carries the
   identical demanded-flag shape (`SeedF5J.cs:34` metric, `:104` gate) under
   the same rule 5.5.11.7 e directly — same second-input problem, and the
   seed corpus is the model's test. The `f5j-nz-south-island` witness pairing
   (`parallel-run-mapping.md:103`) is planned, not started; it re-runs against
   the new 30-f5j definition, and parity holds: fully captured flights score
   byte-identically, and a no-height flight is 5.5.11.7 e-zeroed on both sides.
5. **Assumption contradiction refused at adoption.** A metric referenced by an
   `IsRecorded` predicate MUST NOT declare `whenNotRecorded` (new check 24).
   Reasoning: "absence resolves to value X" and "absence fails the flight"
   cannot both hold for one metric — a self-contradictory declaration, the
   same style as check 22's kind match. The refusal is what makes the
   evaluation mechanism a plain dictionary lookup (`PredicateEvaluator`
   checks the resolved metrics for the metric's presence) with NO recordedness
   plumbing anywhere: since the assumption loop can never insert a value for
   an `IsRecorded`-referenced metric, the post-insertion dictionary contains
   such a metric iff the digest-resolved measurements do — so the lookup IS
   "evaluated before insertion", trivially and everywhere, including
   `FlightSelector.IsZeroedByFlightGate`'s re-evaluation
   (`FlightSelector.cs:300-311`), which today cannot distinguish an inserted
   value from a real one and would otherwise read an assumed metric as
   recorded, un-zero a gate-zeroed flight under clamping
   (`ClampAndRecompute`'s guard, `FlightSelector.cs:329`) and re-admit it to
   task-gate combination decisions.
6. **Both-missing ordering: pend first.** A flight missing an
   `IsRecorded`-referenced metric AND an ordinary referenced metric (e.g.
   blank start height AND blank flight time) pends on the ordinary metric
   (first in the task's declared-metric order, the existing awaited rule) and
   zeroes when that capture arrives — the gate is already determinately false,
   and the two orderings share the same end state. Chosen because it is the
   natural implementation of the carve-out, keeps the mid-lag data-quality
   view honest ("awaiting flightTime"), and is the NFR-4-tolerant reading.
7. **Read model: exclude gate facts from AwaitingCapture.** `RecordingCore`'s
   AwaitingCapture list (`TaskRoundRecording.cs:309-318`) reports
   referenced-and-unassumed absent metrics as "scoring awaits" — but an
   `IsRecorded`-referenced absent metric is NOT awaited: absence zeroes the
   flight. It is excluded from AwaitingCapture; MissingMetrics keeps listing
   it (the column stays demanded — blank ⇒ zero, typed ⇒ valid, one input).
   NdcScore client work remains schema regeneration only.
8. **Docs approved (house rules 3/4):** glossary entry "presence-gated
   validity / recordedness", class-diagram `Predicate <|-- IsRecorded` node,
   and a `recorded(<metric>)` clause + F31 row in
   `docs/competition-class-notation.md`.

Cross-references (house rule 2): amends the metric-absence tier table with one
row (absence of an `IsRecorded`-referenced metric is the predicate's false, not
tier 2 and never tier 3); consistent with NFR-4 (informative absence declared
by the class, not a system error); additive-only per NFR-2 (new closed-subtype
member, no existing shape changes meaning); the architectural law holds —
`IsRecorded` is generic, the core never branches on a metric name or class.

## Why it matters

Removes a mandatory second input per flight from every AMRT-style class and
closes a correctness trap: today's second-input shapes either block the round
(pend) or silently zero measured flights (assumed flag) when the organiser
forgets the tick. The class definition — not the UI, not the core — says what
absence MEANS, which is the same lever WI-1 installed for assumed values; this
story extends it from "absence resolves to a value" to "absence resolves to
invalid".

## Plan

### WI-1 — Vocabulary + engine (`src/Soarscore.Domain`)

1. `ScoringVocabulary.cs`: `IsRecorded : Predicate` with
   `required string MetricRef`; register
   `[JsonDerivedType(typeof(IsRecorded), "isRecorded")]` on the
   `[JsonPolymorphic]` hierarchy (lines 147-153). The closed hierarchy stays
   closed (`private protected` ctor). Wire shape — canonical, camelCase,
   null-omitting (`ClassDefinitionIngestion.Options`):
   `{ "$kind": "isRecorded", "metricRef": "startHeight" }`.
   Update the header comment ("Two subtypes" → three; keep the no-`anyOf`
   note and the closed-vocabulary discipline sentence).
2. `PredicateEvaluator.cs`: add the arm
   `IsRecorded r => measurements.ContainsKey(r.MetricRef)` to the switch
   (line 24-29). Presence by metric name — either input form fulfils it, the
   same convention the absence story's decision 9 fixed for measurements
   (a valid reading or a valid distance). Document in the arm's comment why
   the plain lookup is the pre-insertion evaluation (settled decision 5:
   check 24 guarantees the assumption loop never touches these metrics).
3. `FlightMetricResolution.cs`:
   - `CollectPredicateRefs` (lines 196-210): add the `IsRecorded` arm
     (`refs.Add(r.MetricRef)`) — the referenced set must contain these metrics
     for the read model and the tier logic.
   - New public walk mirroring `ReferencedMetrics` (lines 48-69):
     `IsRecordedReferencedMetrics(ResolvedTask)` / `(TaskDefinition)` — the
     set of metric names referenced by any `IsRecorded` predicate. Reused by
     the awaited loop (here), `RecordingCore` (WI-3) and adoption check 24
     (WI-2).
   - `ResolveAndInterpret` (lines 106-170): the tier-2 awaited loop
     (lines 139-149) skips metrics in the `IsRecorded`-referenced set —
     absence on them is the gate's `false`, never a capture gap. Everything
     else stands: tier 1 unchanged (check 24 makes its interaction with these
     metrics impossible — note that in a comment); tier 3 unchanged
     (`IsRecorded`-referenced metrics are declared, refused otherwise by
     check 1); a flight missing ONLY such metrics does not pend — it
     interprets, the gate evaluates `IsRecorded` → false → zeroed
     (`State: Valid`, score 0, no contributions — 5.5.11.7 e: cancelled and
     recorded as zero, still FLOWN); a flight missing one of these AND an
     ordinary referenced metric pends on the ordinary one (settled decision
     6, pend first) and zeroes when it arrives.
   - Update the file header: tier 2 gains the carve-out row.
4. `FlightSelector.cs`: NO change — and the plan says so deliberately.
   `IsZeroedByFlightGate` (lines 300-311) re-evaluates the gate on
   `flight.Metrics` (post-insertion); per decision 5, an `IsRecorded`-
   referenced metric is in that dictionary iff the digest had it, so the
   re-evaluation reproduces the interpreter's gate decision exactly:
   absent ⇒ `IsRecorded` false ⇒ the zeroed-stays-selected path holds
   (a cancelled flight is FLOWN, consumes the slot, scores zero), and
   `ClampAndRecompute` (line 329) keeps it un-un-zeroable. A sub-agent must
   not "fix" anything here; a test proves it (WI-5).

### WI-2 — Adoption validation (`ClassDefinitionValidation`)

File header: "the twenty-two adoption checks" → twenty-four.

1. **Check 1 extension** (`CheckMetricReferencesResolve`, lines 81-120):
   `AllPredicates`/`WalkPredicate` already yield every predicate node with
   paths; the filter at line 102 (`is not Comparison … continue`) gains the
   `IsRecorded` arm — an undeclared `MetricRef` is a defect at
   `{predPath}.metricRef`.
2. **New check 23 — `IsRecorded` scoping.** Over the same `AllPredicates`
   walk: any `IsRecorded` whose path does not begin with
   `{taskPath}.flightValidWhen` is refused — code
   `class-definition.check-23.is-recorded-outside-flight-gate`. Nesting
   inside the gate's `AllOf` tree is legal (paths still carry the
   `flightValidWhen` prefix); `validWhen` and `ConditionalTerm.When` sites
   (`{termPath}.when`) are the refusals the check exists for.
3. **New check 24 — assumption contradiction.** Per task: any declared metric
   that (a) declares `whenNotRecorded` and (b) is in
   `FlightMetricResolution.IsRecordedReferencedMetrics(task)` is refused —
   code `class-definition.check-24.assumption-on-is-recorded-metric`, path
   `{taskPath}.metrics[m].whenNotRecorded`. Message states the contradiction
   (the metric's absence cannot both resolve to a value and fail the flight)
   and cites 5.5.11.7 e's shape.

### WI-3 — Read model (`TaskRoundRecording.cs`)

1. The handler (line 220) additionally computes
   `FlightMetricResolution.IsRecordedReferencedMetrics(taskDefinition)` and
   passes it into `RecordingCore.ComputeGroupViews` (line 222-224).
2. `RecordingCore`'s AwaitingCapture projection (lines 315-318) excludes
   `IsRecorded`-referenced metrics: those absences are gate facts — the
   flight zeroes, it does not pend, so "scoring awaits" would mislabel them.
   MissingMetrics (lines 309-314) unchanged — the recorded fact stands and
   the column stays demanded. Update the projection's doc comment
   (lines 301-308).
3. View tests: the recording view for an F5J-NDC-shaped task shows
   `startHeight` under MissingMetrics and NOT under AwaitingCapture when
   blank, and the pending diagnostics of ordinary awaited metrics are
   untouched (WI-5).

### WI-4 — Seeds (`tools/Soarscore.SeedData`)

1. `Authoring.cs` `Predicate` factories (line 167+):
   `public static IsRecorded IsRecorded(string metric) => new() { MetricRef = metric };`
2. `SeedF5jNdc.cs`: delete `Metric.Flag("startHeightRecorded")` (line 44) and
   its comment; the gate's `Predicate.Is("startHeightRecorded", true)`
   (line 113) becomes `Predicate.IsRecorded("startHeight")` cited
   `5.5.11.7 e (carried by NZ.0.3 c)`. Update the metricSet header comment
   (lines 30-38): `startHeight` stays a demanded Number with NO assumption —
   it can no longer pend; its only absence path is the gate, which zeroes per
   5.5.11.7 e. Blank ⇒ zero, typed ⇒ valid: ONE input. The arithmetic check
   (4 × 650 = 2600) is untouched.
3. `SeedF5J.cs`: the identical edit — metric line 34, gate line 104, header
   comment lines 26-36 — same rule directly (FAI F5J). Add one sentence to
   the header: the `f5j-nz-south-island` witness pairing re-runs against this
   definition; parity holds (decision 4).
4. **Corpus sweep with the `fai-rules` skill** (the metric-absence WI-4
   discipline — resolve each case with the skill, not by symmetry): confirm
   which other corpus classes carry a recordedness-cancel rule (F5K/F5L
   height-data clauses, 5.5.10.x). Expected: only the F5J classes. Any
   additional finding becomes a new `kanban/backlog/` stub (house rule 6) —
   never a silent scope increase. Record the sweep's citations in this
   story's Verification section when done.
5. Regenerate the corpus (`dotnet run --project tools/Soarscore.SeedData`):
   `30-f5j.json` and `85c-nz-f5j-ndc.json` change. No committed fixtures
   embed either definition (the corpus is gitignored today;
   `ci-seed-corpus-drift-guard.md` will make drift a red build when it
   lands). Version strings UNCHANGED — the rulebook edition did not change,
   the encoding did; new `contentHash` per definition → new catalogue stream
   (`ClassCorpusSeeder` is idempotent by hash, `ClassCorpusSeeder.cs:17-21`),
   adopted competitions keep their pinned definition, and there is no real
   data to migrate (green-field).

### WI-5 — Tests: the named invariants

Property invariants articulated up front (testing approach; the
`MetricAbsenceSemanticsPropertyTests.cs` precedent — each with mutation-proven
teeth):

1. **Partial-capture transparency (amended).** For any capture subset of an
   entry's flights, the entry's score over the subset equals the full score
   where flights missing ONLY `IsRecorded`-referenced metrics contribute ZERO
   and STAY SELECTED (zeroed, not removed), and flights missing ordinary
   referenced metrics are removed (pending). The WI-5 partial-capture
   invariant, amended for the new tier row.
2. **Digest-equivalence.** `IsRecorded(M)` ⇔ the amendment-resolved digest
   contains M — over generated measurement/amendment sets; presence by metric
   name, either input form.
3. **Pend-then-zero convergence.** A flight pending on an ordinary referenced
   metric while an `IsRecorded`-referenced metric is absent reaches exactly
   the zeroed state (State Valid, score 0, still selected) once the ordinary
   capture arrives — no capture order produces a different final state (the
   both-missing owner call, decision 6).

Example-based, mapped to files:

- `FlightInterpreterTests` (precedent lines 376-430): `IsRecorded` present ⇒
  true; absent ⇒ false; nested in `AllOf`; unknown-subtype arm untouched.
- Carve-out through `FlightMetricResolution.InterpretAllFlights` (the
  pending-vs-zeroed distinction only exists at the orchestrator):
  absent `startHeight`, all else present ⇒ zeroed (Valid, 0, no
  contributions, NO Awaited) and NOT pending; absent `flightTime` still pends
  with its awaited diagnostic; both absent ⇒ Pending(awaited = first
  declared-order absent ordinary metric) then zeroed on its arrival.
- Gate pass ⇒ terms score normally: `NzNdcSeedArithmeticTests` continue at
  550 / 500 / 0 with the updated `F5jNdcMetrics` helper (the
  `heightRecorded` parameter and dictionary entry go).
- Zeroed-stays-selected: the gate-zeroed flight is the entry's selected
  flight for the round at zero (`FlightSelectorTests` /
  `FlightZeroingTaskGateTests` precedent) — decision 5's re-evaluation
  consistency, proved not asserted.
- Adoption (`ClassDefinitionValidationTests`): check 1 — `IsRecorded` over an
  undeclared metric refused; check 23 — refused in `validWhen` and in
  `ConditionalTerm.When`, accepted at `flightValidWhen` (top level and
  nested in `AllOf`); check 24 — `whenNotRecorded` on an `IsRecorded`-
  referenced metric refused, on an unrelated metric fine.
- Store round-trips: the new subtype through the existing event-JSON
  round-trip tests (postgres + sqlite).
- Seed view: `F5JSeed75mGateTests` drops its `startHeightRecorded` capture
  (lines 97-98, 115) — the metric no longer exists; its 75 m assertions stand
  unchanged.
- Architecture guard: `ClassAgnosticismTests` stays green — `IsRecorded` is
  generic; the core never branches on metric names.

### WI-6 — Docs (approvals on record, decision 8)

1. `docs/soaring-domain-glossary.md`: entry **"presence-gated validity /
   recordedness"** — a predicate fact about whether a flight carries a
   measurement for a metric; the class can say "an unrecorded X fails the
   flight" without an assumed value and without a companion flag; absence of
   an `IsRecorded`-referenced metric is the predicate's `false`, never a
   capture gap (tier 2) and never an error (tier 3); the reference demands
   the metric stay unassumed (check 24).
2. `docs/soaring-domain-class-diagram.md`: `Predicate <|-- IsRecorded` node +
   `metricRef` attribute, note tying it to 5.5.11.7 e / NZ.0.3 c and the
   `flightValidWhen`-only scope.
3. `docs/competition-class-notation.md`: the predicate grammar (§5, line
   840-851) gains `recorded(<metric>)` — notation `recorded(m)` ⇔
   `Predicate.IsRecorded(m)` ⇔ `$kind: "isRecorded"`; "Exactly one of {leaf
   comparison, `allOf`}" wording updated for three leaf forms; a note that
   `recorded` is legal only inside `flightValidWhen` (§6's "cannot say"
   section gains the scoping sentence); the rule-figure table gains
   **F31 `recorded(<metric>)` — `Predicate.IsRecorded`** citing
   `5.5.11.7 e`, `NZ.0.3 c`.

### WI-7 — NdcScore (client repo, `~/Source/NdcScore`)

Regenerate the TS client from `/swagger` (`Predicate` schema gains the
`isRecorded` discriminator). Zero behaviour changes: columns, drop-down and
capture are definition-driven and generic; with no flag metric the drop-down
entry vanishes, `startHeight` keeps its per-flight column, blank ⇒ zero,
typed ⇒ valid. One input.

## Tests (WI-5 discipline — summary)

- **`IsRecorded` semantics:** present ⇒ true; absent ⇒ false; an inserted
  assumption is never a recording — vacuously true by check 24 and stated by
  the digest-equivalence property — unit + property.
- **Carve-out:** absent `IsRecorded`-referenced metric + failing gate ⇒
  flight zeroed (State Valid, score 0), NOT pending, round completes; absent
  ordinary metric (`flightTime`) still pends with its awaited diagnostic;
  both absent ⇒ pend first, zero on arrival (decision 6); gate pass ⇒ terms
  score normally.
- **Zeroed-stays-selected:** gate-zeroed flight is the entry's selected
  flight for the round at zero (FlightSelector re-evaluation consistency,
  decision 5).
- **Adoption:** `IsRecorded` over an undeclared metric refused (check 1);
  outside `flightValidWhen` refused (check 23); assumption on an
  `IsRecorded`-referenced metric refused (check 24).
- **Transparency analogue:** for any capture subset, an entry's score equals
  the full score with absent-`IsRecorded` flights contributing ZERO (not
  removed) — the WI-5 partial-capture invariant, amended for the new tier
  row.
- **Architecture guard:** the core never branches on metric names —
  `IsRecorded` is generic; no per-class knowledge.

## Verification

- Domain + Application suites green; Architecture tests green (no new
  dependencies; the feature is Domain-internal + read-model + adoption
  checks); Infrastructure fast loop green.
- Both stores: event round-trip of the new subtype + full acceptance suite
  (`SOARSCORE_TEST_STORE=postgres` and `=sqlite`).
- The three named invariants (WI-5) documented with their names.
- Corpus regenerated; `30-f5j.json` and `85c-nz-f5j-ndc.json` are the only
  emitted changes.
- The corpus sweep's rule citations (WI-4 item 4) recorded here:
  *(filled at implementation — fai-rules skill discipline)*.
  Sweep run 2026-09-22: the recordedness-cancel shape is FAI F5J-only —
  5.5.11.7 e (`docs/rules/source-docs/f5-electric-2026.md:847`), carried into
  NDC F5J by NZ.0.3 c (`docs/rules/nz/source-docs/nzmaa-s5-soaring-2024.md:126-127`);
  encoded in both seeds. One additional F5J clause of the same shape found —
  5.5.11.1 h) iii (`f5-electric-2026.md:709-713`): start height reset to "---"
  ⇒ flight 0, DROPPABLE, local-rule scope (FAI WC/Open events only) — backlog
  stub raised (`kanban/backlog/f5j-start-height-reset-to-dashes.md`, house
  rule 6). No other corpus class cancels/zeroes on absent recorded data:
  F5K's no-record case is a RE-FLIGHT, 5.5.10.13 ii (`f5-electric-2026.md:1390-1391`)
  — backlog stub raised (`kanban/backlog/f5k-altitude-not-recorded-reflight.md`);
  its "---" reset states no consequence (`f5-electric-2026.md:1252`) and its
  motor-restart zero is behavioural (5.5.10.12 c, `:1363`). F5L has only the
  value-based pre-sets rule (5.5.12.7, `:1589`). Flight-time/"not judged"
  absences are human-process → re-flight: 5.5.11.5 d (`f5-electric-2026.md:799-800`),
  F3J.3.1 e (`f3-soaring-2025.md:1498-1500`), F3B.1.5 b v
  (`f3-soaring-2025.md:106-107`), F3F.1.5 c (`f3-soaring-2025.md:640`); F3K is
  silent (F3K.9.6, `:2405-2406`; unsigned-card round zero is F3K.1.2 sign-off,
  `:2088-2096`). NZ M/N/P: no recordedness rule — ALS is a limiter, not scoring
  data (NZ.2.8, `nzmaa-s5-soaring-2024.md:611-652`). NDC F5K routes to NZ
  Class Q §3.16 (NZ.0.4, `:137-139`): organisers-fault re-flight only (3.16.23,
  `:2211-2212`), the FAI 5.5.10.13 ii ground is not carried.
- Existing replays untouched: the parity fixtures capture everything their
  definitions reference — with the flag metric gone, fully captured flights
  score byte-identically and the `@gliderscore` suite staying green proves
  the additive claim. One deliberate fixture-adjacent edit: the parallel-run
  ledger's `startHeightRecorded` derivedMetrics disclosure
  (`tests/GliderscoreFixtures/f5j-christchurch-2019/parallel-run/30-f5j.json`)
  tracked the seed's new encoding — with the flag gone the old disclosure is
  exactly the seed-vs-ledger drift `ParallelRunComparator.CheckProvenance`
  exists to break on. Run DATA (oracle cells, triage, notes) untouched.

## As-built results (2026-09-22)

- Tallies at completion: Domain 874 · Application 441 · Infrastructure 182
  (full suite incl. the postgres/Testcontainers `Storage` cases) ·
  Architecture 18 · Acceptance 114/116 per store — the 2 failures are the
  pre-existing `CorsPreflightSmokeTests` pair, reproduced at HEAD and
  unrelated to this story; recorded in `kanban/tech-debt.md`.
- The three named invariants live in
  `tests/Soarscore.Domain.Tests/IsRecordedPredicatePropertyTests.cs`
  (P-PartialCapture, P-DigestEquivalence, P-PendThenZero), each mutation-proven:
  tier-2 carve-out skip removed ⇒ pend properties red; `IsRecorded` arm
  forced `true` ⇒ digest-equivalence red; reverted and green after each.
- Corpus regenerated exactly (`30-f5j.json`, `85c-nz-f5j-ndc.json`) and
  idempotent on re-run; note the pre-edit on-disk `85c` still carried the
  rejected 2026-09-21 draft encoding — the regen reconciled it with source.
- WI-5's story list under-reported the flag's blast radius; the repair pass
  also covered `TapeLandingScaleProofTests` (Domain), the
  `TaskRoundLifecycle`/`Scoring`/`TaskRoundRecording` Infrastructure fixtures,
  and the Acceptance Steps + `ReplayDriver` capture generation
  (code, not fixture data).
- WI-7 landed in the NdcScore repo: `openapi/v1.json` refreshed from the
  running service's `/openapi/v1.json`, `npm run gen:client`, `85c` fixture
  byte-identical to the regenerated corpus, `tsc -b`/eslint/vitest (144)
  green. Zero behavioural client code changed; the definition-driven
  drop-down loses the flag entry by itself.

## Glossary

New domain concept: **presence-gated validity** (recordedness as a predicate
fact). Glossary entry, class-diagram node and notation clause approved by the
owner 2026-09-22 (decision 8) — landed by WI-6 per the glossary's own rule.

## House-keeping

- Keep this filename stable; implementation cites work items as
  `kanban/backlog/add-recorded-predicate.md` WI-n.
- Move to `in-progress/` (with `git mv`) before writing code; to
  `completed/` with the plan updated *as built* on completion.
- Residual debt (if any) goes to `kanban/tech-debt.md`; anything the corpus
  sweep surfaces beyond the F5J classes becomes a `kanban/backlog/` stub
  (house rule 6).
