# Story — Per-term score breakdown on the wire (per-flight, generic)

**Status:** Completed (landed 2026-09-29, unblocking NdcScore
`kanban/blocked/ss_per-term-landing-points.md` — owner direction).
Owner decisions 2026-09-29 (settled before writing): granularity is
**per-flight**; coverage is **all score terms, generic** (no landing
special-case — NFR-1); shape is **extend the existing result views**,
additively (no new endpoint).

## What

Expose the engine's already-computed per-term points over the API so
NdcScore renders its landing-points column **purely from the wire** —
never by mapping readings to points in client code (NdcScore law 2).

Concretely: `GET /task-round-result` (`GroupScoreView` /
`CompetitorTaskResultView` in
`src/Soarscore.Application/Queries/Scoring/ScoreTaskRound.cs:38-52`)
today carries only task-level totals per competitor (`RawScore`,
`PreNormalisationScore`). The engine already resolves what the column
needs — `FlightInterpreter.Interpret` evaluates every raw score term
into `TermContribution(MetricConsumed, Points)` indexed by term-list
position (`src/Soarscore.Domain/Scoring/ScoringResultTypes.cs:44-47`,
`FlightInterpreter.cs:91-99`), `FlightSelector.SelectAndScore`
carries those contributions forward on `SelectedFlights`
(`FlightSelector.cs:88-135`, including the target-clamp re-score at
`:317-373` which recomputes them), and `TaskResult.Selection` holds
the selected `InterpretedFlight`s. `ScoreTaskRoundHandler.MapGroupResult`
(`ScoreTaskRound.cs:182-215`) then **drops them**: it maps only
`State`, `RawScore`, `PreNormalisationScores[kv.Key]` and
`AwaitingCapture`. This story surfaces them, verbatim, through the
same functions scoring resolves through — no second implementation.

## Why it matters

Single source of truth (the NdcScore blocked story's argument,
adopted here): the class's landing table turns a mark's band into
points; the engine composes the terms (including the tape-composed
lookup path, `FlightInterpreter.cs:203-244`). A client-side
reading→points lookup is a second implementation of that table: every
conditional branch, rounding mode, cap, floor and normalisation step a
drift surface, with silent disagreement against the engine as the
failure mode. The breakdown keeps one source of truth: the engine
awards, the client renders (`String(value)`, no arithmetic).

This blocks **only** the NdcScore landing-points column. The results
table as it stands (echoed marks + verbatim raw score + totals) ships
without it. Do not hold any capture, declaration or scoring work for
this breakdown.

## Verified facts (SoarScore2, traced 2026-09-29)

- **The data already exists, per flight.** `InterpretedFlight`
  (`ScoringResultTypes.cs:88-100`) carries `Score` plus
  `TermContributions: IReadOnlyDictionary<int, TermContribution>`
  (term-list index → consumed/points). `FlightSelector` preserves it
  through selection, target clamping (`ClampAndRecompute` re-evaluates
  every term, `:359-366`), PerTask-cap correction (`ApplyPerTaskCaps`,
  `:377-391` — reads `MetricConsumed` per term index across selected
  flights) and rounding; `TaskResult.Selection: SelectedFlights?`
  (`:116-144`) holds the flights that produced `RawScore`.
- **The wire drops it in exactly one place.**
  `ScoreTaskRoundHandler.MapGroupResult` (`ScoreTaskRound.cs:197-205`)
  builds `CompetitorTaskResultView` from `kv.Value.State`,
  `kv.Value.RawScore`, `result.PreNormalisationScores[kv.Key]`,
  `AwaitingCapture` — `kv.Value.Selection` is never read.
- **Term identity is generic today.** `ScoreTerm` subtypes
  (`src/Soarscore.Domain/PublishedClassDefinition/ScoringVocabulary.cs:205-255`):
  `RateTerm.MetricRef`, `LookupTerm.MetricRef`, `PiecewiseTerm.MetricRef`,
  `ConstantTerm` (none), `ConditionalTerm{Then, Else?}` (wraps). The
  engine's own `GetTermMetricRef`
  (`FlightSelector.cs:277-285`) unwraps conditionals to the branch
  metric — the same unwrap the breakdown needs (85c's landing lookup
  sits inside a conditional; F3J touch/overfly gates do too).
- **Composition already flowed through.** The tape-composed lookup
  (`FlightInterpreter.cs:203-244`) returns a `TermContribution`
  consuming the READING with the class table's award — so a mark-mode
  landing's breakdown value is the composed award, not a distance
  fallback, with zero extra work.
- **Grains the breakdown must respect** (all existing semantics, none
  invented here): `RawScore` = sum of selected flight scores →
  PerTask-cap correction → `RawScore` rounding → raw penalties
  (`ScoringService.ScoreGroup` steps 2b–2d, `ScoringService.cs:130-191`,
  incl. `FloorAtZero` at `:184-189`) → normalisation
  (`NormalisationEngine.Normalise`). `ScoreNormalised` terms (if any)
  evaluate inside normalisation, not in the flight interpretation.
- **Absence, never zero.** `TaskResultState.NoResult` carries
  `Selection: null`; `FlightResultState.Pending` flights are excluded
  from selection but their diagnostics ride `AwaitingCapture`
  (`FlightSelector.cs:57-79`). A missed landing is absence — the
  breakdown must carry "no value", never a client-mappable zero.

## Cross-reference (house rule 2)

- **NFR-1 (core knows no class).** Coverage is all terms, keyed by the
  metric each term consumes — never a landing special-case, never a
  class-name check, never a positional index the client bakes in.
  Landing is just the term whose `metricRef` is the class's landing
  metric; the client filters by string equality against the class
  definition's own metric name.
- **NFR-2 (additive-only).** New fields are additive on the existing
  views; absent for `NoResult` rows; canonical seed JSON untouched;
  old clients ignore the additions byte-for-byte.
- **NFR-4.** Read-only projection work — no capture gating, no new
  commands, no write-path change.
- **Glossary.** No new domain concept: "term contribution" is the
  existing `TermContribution` value object, now projected. No glossary
  edit.
- **`docs/rules/` untouched.** No rulebook reading required — this is a
  projection of values the engine already awards.
- **Sibling:** `ss_tape-catalogue-on-the-wire.md` (the catalogue story)
  is independent — picker source vs points column; neither holds the
  other.

## Plan

- **WI-1 — the view shape (Application).** Extend
  `CompetitorTaskResultView` (`ScoreTaskRound.cs:38-44`) with an
  additive per-flight breakdown, e.g.
  `Flights: ImmutableArray<FlightTermView>` where each flight carries
  its sequence plus `Terms: ImmutableArray<TermView>` and each term
  carries `TermIndex`, `MetricRef` (`string?` — null for
  `ConstantTerm`), `MetricConsumed`, `Points`. Key by `metricRef`,
  not position: term order is an engine detail (NdcScore law 3). Empty
  array (never null) for `NoResult` rows (`Selection: null`); pending
  flights omitted from the breakdown with `AwaitingCapture` unchanged
  as the "awaiting capture" signal. State the granularity in the
  record doc comments: per-flight `Points` sum to the flight's score
  **before** PerTask-cap correction and rounding (name both), so the
  column never pretends to re-derive `RawScore` by addition.
- **WI-2 — the projection (Application, one function).** In
  `MapGroupResult` (`ScoreTaskRound.cs:182-215`), project
  `TaskResult.Selection` through the resolved task's term list to
  attach each contribution's `MetricRef` (unwrap conditionals per the
  existing `GetTermMetricRef` precedent — factor it shared rather than
  duplicating the unwrap). Values cross verbatim (`decimal` as-is; no
  rounding, no formatting, no normalisation math — normalisation
  already consumed `RawScore` downstream). `ScoreNormalised` terms are
  out of scope: document that the breakdown covers raw `Score` terms
  only.
- **WI-3 — edge semantics (pin in tests, not prose).** NoResult →
  empty breakdown; Pending flights → omitted, diagnostics unchanged;
  flight-gate-zeroed flights → their zeroed contributions (the
  interpreter's own zeroing, `FlightInterpreter.cs:73-88`); PerTask-cap
  and rounding deltas stay server-side (WI-1's doc states the sum
  relationship); `FloorAtZero`-floored rows keep their unfloored term
  values with `RawScore == 0` (the floor is a task-level recording
  rule, not a term rewrite — same stance as `PreNormalisationScores`
  preserving what normalisation consumed); aggregate/recorded
  penalties do not rewrite term values (they act at their own stages,
  `ScoringService.cs:158-168`); reflight duplicate rows each carry
  their own entry's breakdown (rows are per-Entry, `ScoreTaskRound.cs:21-25`).
- **WI-4 — contract + tests.** OpenAPI regenerates from the views (no
  hand-written schema). Unit tests through `ScoreGroup` +
  `MapGroupResult`: (a) single-flight 85c shape — landing term's
  `Points` equals the engine's own lookup award incl. a tape-composed
  reading; (b) multi-flight `all` shape (F3K/F5K) — one breakdown per
  flight, aggregation stays server-side; (c) conditional landing gate
  (F3J-style touch/overfly) — gated-out branch contributes its
  else-branch value; (d) NoResult/Pending rows — empty/omitted, never
  zero. **Property test (CsCheck)** — named invariant: *for any task
  and any metric inputs, the sum of a selected flight's term `Points`
  equals that flight's pre-cap, pre-rounding score* (the interpreter's
  own addition loop, `FlightInterpreter.cs:94-99`); plus the
  no-silent-mismatch guard: every surfaced `Points` value is the exact
  `TermContribution.Points` the engine resolved (reference-equality of
  the decimal, not a re-computation).
- **WI-5 — fixtures / drift guard.** No seed change; no golden-fixture
  movement expected (read-side projection only). Run the acceptance
  suite + Gliderscore replay oracles: silence is the assertion. NdcScore
  fixture mirrors untouched (no definition data changes).

## Design questions — settled (owner, 2026-09-29)

- **Extend vs new read:** extend `GroupScore`/`CompetitorTaskResultView`
  additively. No `GET /task-round-term-results`.
- **Granularity:** per-flight (from `Selection.InterpretedFlights`),
  so each mark cell gets its own points cell. No per-(competitor,
  task-round) subtotal fallback needed — the server already holds the
  finer grain.
- **Coverage:** every score term, generically. Landing-only is refused
  on NFR-1 grounds: the core must not know which metric is "landing".

## Before starting

- Confirm the exact view-field names against the route-shape reflection
  test (`tests/Soarscore.Architecture.Tests` — endpoints must stay
  GET/POST only; this adds fields, not routes).
- Confirm the `MetricRef`-null convention for `ConstantTerm` and the
  conditional-unwrap sharing with the owner (factor vs duplicate
  `GetTermMetricRef`).
- Check `GET /competition-result` consumers: this story touches only
  `GET /task-round-result`; competition totals still come from
  `ScoreCompetition` unchanged. Note it explicitly if any consumer
  expects the breakdown there too — that is a new story, not scope
  creep here.

## Alternative homes considered and set aside

- **New `GET /task-round-term-results` endpoint** — more surface for
  the same data the handler already walks; the extended view keeps one
  read per group readout (the field readout the query's own docstring
  describes).
- **Landing-only term exposure** — a class-specific branch in the core,
  violating NFR-1; the generic breakdown costs nothing extra (the
  contributions already exist for every term) and keeps the client free
  of per-class logic.
- **Per-(competitor, task-round) subtotal only** — coarser than the
  engine's own grain and insufficient for the per-flight column the
  results table renders; the finer grain is already in memory.

## Acceptance

- [x] NdcScore can render, beside each captured landing mark, the
  landing points the engine awarded for that flight, from wire reads
  alone — with zero award arithmetic in client code (mirrors the
  blocked story's acceptance §1). Verified: `ScoreTaskRoundHandlerTests`
  pins landing `Points` verbatim per flight, keyed by `metricRef`,
  incl. the tape-composed reading.
- [x] Every rendered value equals what the engine's own scoring
  resolved (conditionals, caps, rounding, composition, floor,
  normalisation included at their own grains) — no silent mismatch
  between the points column and the raw-score/total columns (mirrors
  §2; the breakdown states the pre-cap/pre-rounding sum relationship
  so the two columns never disagree). Verified: CsCheck invariant
  `TermBreakdownPropertyTests` + conditional-gate + floor/penalty
  non-rewrite pins; full suite green (Domain 929, Application 446,
  Architecture 14, Infrastructure 91; Gliderscore replay 18/18 sqlite;
  2 pre-existing CORS env failures also fail on the pristine tree).
- [x] `NoResult` rows carry no breakdown values (absence, never zero);
  pending flights stay "awaiting capture", not zero. Verified:
  `Per_term_breakdown_carries_absence_never_zero` + `"flights":[]`
  serialisation pin.
- [x] Full acceptance suite + Gliderscore replay/parallel-run oracles
  green; no seed, fixture or canonical-JSON movement. Verified:
  replay oracles 18/18; `git status` shows no seed/fixture movement.

## NdcScore's part once it lands

The results table adds one column per landing column rendering the
term value verbatim next to the echoed mark text (off-tape `0` shows
the engine's zero, never a client-mapped one). No reading→points
mapping enters client code; the choice story's laws 2/3 hold
unchanged. Regenerate the TS client from `/openapi/v1.json`.
