# Story — Turn-around cap and window-sum plausibility warnings (field readout)

**Status:** Backlog · **Raised:** 2026-09-30 from NdcScore
`kanban/backlog/turn-around-window-score-validation.md` (raised 2026-09-23,
organiser feedback after an F3K evening). Counterpart story: the NdcScore item
defers both rules engine-side (client law 2 — no client arithmetic) and forbids
raising SoarScore work from that repo; this is the SoarScore-side story it calls
for. No SoarScore2 backlog item covers it (verified 2026-09-30: no turn-around /
window-sum story in `kanban/backlog/`, only `CapScope.PerTask` mechanics in
completed scoring stories).

## What

Two generic, warn-through plausibility checks on the field readout
(`GET /task-round-result`), computed per entry per group at score time, surfaced
verbatim for NdcScore to render. Scores are never altered, nothing is refused.

Given a resolved task with `resolvedTask.Timing.Kind == Fixed` and
`resolvedTask.Timing.WorkingTime == W` (seconds, resolved literal — param-bound
or literal alike), and an entry whose `TaskResult.State == Valid` with a
non-null `Selection` of `n = Selection.Flights.Length` selected flights, let

- `flightTimeSum` = Σ over every selected flight, over every term index `i`
  where `ScoreTermRefs.GetTermMetricRef(resolvedTask.Score[i]) == "flightTime"`,
  of `flight.TermContributions[i].MetricConsumed`
  (the uncapped raw value — `FlightInterpreter.cs:154-156` stores raw in
  `MetricConsumed` and clamps only `Points`, so per-flight `cap: 300`-style
  clamps never hide the observation).

Then, at most one warning per entry per group, evaluated in this order — and only
when `n > 1` (a single scored flight needs no turn-around; a boundary max such
as F5J's 600 s in a 600 s window or F3K Task N's 599 is legal flying):

1. **`score.windowSumExceeded`** iff `flightTimeSum > W`, or `flightTimeSum ==
   W` with `n > 1`. Message names the group, competitor/entry, the summed
   seconds (recorded precision), and `W`. This is the organiser's verbatim rule
   2 ("flag as invalid any round scores that sum up to the window time or
   greater") — the `== W` arm fires only for multi-flight selections, where
   zero turn-around time is physically impossible.
2. Else **`score.turnaroundCapExceeded`** iff `flightTimeSum > W - n`. Message
   names the group, competitor/entry, the summed seconds, the cap `W - n`, `W`,
   and `n`. This is the organiser's verbatim rule 1 ("maximum on turn-around
   tasks that is the window time in seconds minus number of flights; e.g. task
   D (2 five minute flights) maximum raw score 598" → `W = 600`, `n = 2`,
   cap `598`).

Worked example (the organiser's): F3K Task D, `WorkingTime = 600`
(`SeedF3K.cs:102`), two selected flights scoring 300 + 300 → `flightTimeSum =
600`, `n = 2` → `score.windowSumExceeded` (rule 2 fires; rule 1 would also hold
at `600 > 598`). Flights of 300 + 298 = 598 → neither fires (exactly at cap is
clean — the cap is strict-greater). Flights of 299.5 + 299 = 598.5 → rule 1
fires (`598.5 > 598`), rule 2 does not. Single-flight selections never fire:
F3K Task N 599 in 600 → clean by the `n > 1` gate, as is F5J's legal 600.

F5K is covered by the same generic formula — no F5K-specific code. F5K Tasks A
(`WorkingTime = 600`, 4 flights to 60/120/180/240 targets, `SeedF5K.cs:92-110`)
and D (`WorkingTime = 600`, 3 flights to 180/180/240, `SeedF5K.cs:220-241`)
are turn-around tasks with the identical physics: targets sum to exactly 600,
so a perfect card leaves zero turn-around time. Worked F5K-D example: 180 +
180 + 240 = 600 → the rulebook's own `cap 599 perTask` (5.5.10.2 "maximum total
flight time used for scoring: 9.59 min", seeded `SeedF5K.cs:137` via Task A
inheritance) clamps the points to 599 AND the window warning fires — clamp and
warning coexist (one shapes points, the other flags plausibility). The
organiser's stricter turn-around caps (A: 600 − 4 = 596, D: 600 − 3 = 597)
apply as warnings only; the seeded 599 is untouched.

Applicability exclusions (all structural, never per-class): `TaskResultState`
not `Valid` → skip; `Selection` null (NoResult) → skip; `n <= 1` → skip (no
turn-around possible with one scored flight); pending flights never
enter `Selection` (metric-absence semantics) so they contribute nothing;
`Timing.Kind == UntilAllFlightsComplete` (F3K Task C — the working time is not
a class datum, `ScoringVocabulary.cs:264-269`) → skip; `WorkingTime` null →
skip; tasks whose resolved `Score` list contains no `flightTime`-consumed term
→ skip (sum is 0, neither threshold trips — this structurally excludes both
Poker tasks, F3K-E and F5K-E, whose scored sum is nominated `targetTime`, not
flown time; actual flown time exceeding the window on a Poker task is
follow-up analysis, not v1); **per-launch windows** → skip via the generic
ceiling test below. `MaxLaunches`, `PreparationTime`, landing
windows (`F3K.9.3` 30 s; F5K 15 s post-window), and per-flight caps play no
part in the formula itself.

**Per-launch-window ceiling test** (the F5K Task C case, stated generically):
F5K Task C flies three separate 4:01 windows (`WorkingTime = 241` per launch,
`MaxLaunches = 3`, `SeedF5K.cs:189-202` — "working time 4:01 PER launch (F13)"),
so one shared-window sum over three 240 s-capped flights (≈720 s vs `W = 241`)
would flag every normal card. The model has no per-launch marker, and this
story must not invent one for a heuristic — instead, skip the check when the
task's own scoring allows more than the window could ever contain: let
`nMax` = the selection's structural flight count (`BestNFlights.Count` /
`LastNFlights.Count` / `ExactlyNInOrder.Count`; `AllFlights` → `MaxLaunches`;
`LastFlight` → 1, already excluded by the `n > 1` gate), and `ceiling` = Σ of
the per-flight scorable maxima (`TargetValues` sum where targets are assigned,
else per-flight `RateTerm` cap × `nMax`; unbounded where neither exists). Skip
iff `ceiling` is finite and `W < ceiling`. Spot checks: F5K-C 3 × 240 = 720 >
241 → skip; F3K-D 2 × 300 = 600 ≤ 600 → apply; F5K-A/D targets sum 600 ≤ 600 →
apply; F3K-F 3 × 180 = 540, G 5 × 120 = 600, H targets 600, K targets 600, M
targets 900 ≤ 900 → apply. Unbounded-ceiling tasks (uncapped, untargeted)
default to apply — the thresholds themselves are the guard there.

Known blind spot (recorded, not v1): last-flight/last-N selections discard
flown time that still consumed window (F5K Task B: three launches in a 420 s
window, only the last scored — three long flights are physically impossible yet
a scored 300 never trips either threshold). Summing *all recorded* flights
instead would false-positive on legal BestN flying (short test flights
legitimately discarded), so the scored-selection sum stands and the B-shape
stays a follow-up.

Hardness is settled here: **warn-through only, v1**. `CompleteTaskRound`
(`Competition.cs:1898-1922`) stays a state gate — no score inspection, no new
refusal. Rationale: the organiser's "flag as invalid" is honoured as a
recorded, rendered flag the CD can see at readout; refusal would block the
evening's capture flow (NFR-4 tension) and a silent clamp would rewrite
standings behind the audit trail. Scores, `PreNormalisationScores`,
normalisation inputs, drops, and aggregates are byte-identical with and without
this story.

Scope is settled here: **every Fixed-working-time task with a `flightTime`
term and more than one scored flight**, not turn-around tasks only — and that
includes F5K's turn-around tasks (A and D) by construction, with F5K-C
structurally excluded by the ceiling test and Poker structurally out of scope.
Rationale: the formula needs only `(W, n, flightTimeSum)`, all generic engine
data; a turn-around-only allowlist would be per-class branching against the
core law, and the organiser already wondered whether rule 2 generalises ("This
may apply to more than F3K"). UntilAllFlightsComplete is excluded by
construction, not by class name.

## Why it matters

Physically impossible scores (two 300 s flights inside one 600 s window — the
pilot would need zero turn-around time) are captured and normalised silently
today, skewing the whole group (winner-takes-1000 ratio) before anyone notices
at finalisation. The field readout is the last point where the CD can correct a
transcription error cheaply; after normalisation the error is baked into every
row of the group. NdcScore cannot help: law 2 forbids it from computing or
second-guessing a score, so without an engine warning there is nothing truthful
for it to render.

## Verified facts (traced 2026-09-30)

**The rulebook is silent on both rules — they are organiser heuristics, not
FAI text.** Full `F3K.11` text pulled via
`.claude/skills/fai-rules/scripts/fai-rule.sh show F3K.11` (tasks A–N incl.
`F3K.11.4` Task D: "two (2) flights … added together … maximum accounted single
flight time 300 seconds … Working time 10 minutes"): no clause states a
window-minus-flights cap, a turn-around deduction, or a window-sum validity
rule. `docs/rules/f3k.md:64-82` catalogue table confirms (per-task maxima and
working times only; no cross-term). Rule-map (`references/rule-map.md`) flight-
points row: F3K "scored seconds per task rule", no window arithmetic. Per the
fai-rules skill ("a rule you cannot find is a question, not an inference"),
neither number may be seeded as rulebook data with a citation — there is no
citation to write. Seeding `cap: 598 CapScope.PerTask` on Task D would also be
a fabrication twice over: the rulebook never states it, and the 1 s-per-flight
turn-around allowance is the organiser's estimate, not physics. The same
silence holds in F5K: full `5.5.10.2` text pulled via `fai-rule.sh show
5.5.10.2` states per-task maxima, working times, and — for A and D only — the
rulebook's own total cap ("Maximum total flight time used for scoring: 9.59
min", seeded as `cap: 599 perTask`, `SeedF5K.cs:137` with D inheriting it via
`like`, `SeedF5K.cs:212-218`). That 599 is points-shaping data with a citation;
the organiser's 596/597 turn-around caps are a stricter plausibility overlay
with none — the two coexist without touching each other. `docs/rules/f5k.md:
52-60` catalogue confirms the shape (A: 4 flights/600 s; D: 3 flights/600 s; C:
3 × 4:01 per-launch windows — the per-launch structure behind the ceiling
test; B: single scored flight; E: nominated targets).

**The turn-around cap is not expressible in the existing scoring vocabulary.**
`RateTerm.Cap` clamps the metric consumed with `CapScope` (`ScoringVocabulary.cs:
210-221`); `FlightInterpreter.EvaluateRate` (`FlightInterpreter.cs:140-158`)
clamps per flight, `FlightSelector.ComputePerTaskReduction`
(`FlightSelector.cs:385-426`) corrects per-task sums — but one term carries one
cap in one scope, Task D's term already carries `cap: 300` PerFlight (inherited
from TaskA via `like`, `SeedF3K.cs:52,97-103`), and `ComputePerTaskReduction`
handles only `NumberOrParam.Literal` caps. A formula cap (`W - n`) has nowhere
to live on the term, and a literal `598` PerTask would need a second term or a
new datum — both rejected below. Hence validation (warn), not scoring (clamp).

**The engine, grain by grain — where the check lives and what carries it:**

- Per flight: `FlightInterpreter.Interpret` sums term contributions verbatim;
  `TermContribution.MetricConsumed` is always uncapped raw
  (`ScoringResultTypes.cs:40-47`). The check reads this field — per-flight caps
  never mask the observation.
- Per task: `FlightSelector.SelectAndScore` selects then applies PerTask-cap
  reduction and optional rounding. The check runs after selection (needs
  `Selection.Flights.Length` for `n` and the contributions for the sum).
- Per group: `ScoringService.ScoreGroup` steps 2a–2d
  (`ScoringService.cs:122-192`: interpret → select → raw penalties → `FloorAtZero`
  gated clamp) then `NormalisationEngine.Normalise` (`:194-196`). The new step
  **2e (warn, after 2d, before Normalise)** computes the per-entry warnings from
  `(resolvedTask, taskResult)` pairs already in hand; it writes no score field.
  `NormalisationEngine` untouched.
- Resolved inputs already exist: `ParameterResolver.ResolveTask`
  (`ParameterResolver.cs:108`) resolves `Timing` to `ResolvedTiming(Kind,
  WorkingTime, …)` (`ScoringResultTypes.cs:320-325`), literal or param-bound —
  the check reads `resolvedTask.Timing`, never the unresolved definition.
- No warning slot exists on the scoring path today (verified): `TaskResult`
  carries `State/Selection/RawScore/Disqualified/AwaitingCapture`
  (`ScoringResultTypes.cs:116-144`); `GroupResult` carries
  `Results/WinnerRef/ValidCount/PreNormalisationScores/IsAnnulled` (`:149-169`);
  `CompetitorTaskResultView` / `GroupScoreView` (`ScoreTaskRound.cs:74-92`)
  carry no warnings; `ScoreCompetition`'s `CompetitionScoreView` (`ScoreCompetition.cs:
  25`) is aggregate-only with no per-cell slot. The only warning precedent is
  the draw path: `DrawWarning(Code, Message)` (`CompetitionEvents.cs:100`),
  `PhaseDrawn.Warnings`, `Result<T>.Advisories`,
  `EndpointRouteBuilderExtensions.cs:48-58` 200-with-`warnings` envelope.
- `CompleteTaskRound` is a pure lifecycle gate (already-Complete / Annulled →
  defect, else success; `Competition.cs:1898-1922`) — it never looks at entries
  or scores, and no ordering across rounds is ever imposed (NFR-4). This story
  does not touch it.

**The definition side:** F3K seeds (`SeedF3K.cs:31-224`) carry Fixed working
times (600 literals for D/F/G/H/I/J/K/N; params for A/B/E/L) and Task C
`UntilAllFlightsComplete` (`:74-79`); per-flight caps 300/240/180/120/200/599
as the rulebook states each. `GET /task-round-result` (`ScoreTaskRound.cs:101-
237`) resolves the task once per task-round and scores each group via
`ScoreGroup` — the single insertion point covers every group the readout shows.

## Cross-reference (house rule 2)

- **NFR-1 / core law:** the checks read generic engine data (`WorkingTime`,
  selection length, `flightTime` contributions via `ScoreTermRefs.
  GetTermMetricRef` unwrapping — the same helper `ScoreTaskRound.cs:314` uses).
  No `if task.Code == "D"`, no F3K branch: adding a class never touches this
  code. Metric-name (`flightTime`) is corpus-wide vocabulary, not a class name.
- **NFR-2:** additive-only. New warning record + two view arrays (omitted/empty
  when clean); no existing payload, seed, or score changes. Term-type admission
  rule untouched — no new `ScoreTerm` variant (nothing in the rulebook demands
  one).
- **NFR-4:** no capture gating, no completion gating. Warnings derive on read
  (the `PairwiseCoOccurrence` precedent: derive/surface on read, no sibling
  endpoint); late/out-of-order capture re-scores and re-derives with no refusal
  anywhere.
- **Precedents followed:** `FloorAtZero` (`minimum-score-floor.md`) for
  rulebook-stated data vs engine policy — this story is the mirror case
  (rulebook-silent, so *no* datum, validation only); SHOULD-warn
  (`should-level-minima-warn-dont-refuse.md`) for warn-through + auditability,
  except warnings here are derived on read rather than event-carried (scores
  are derived data; draws are logged decisions — different grains, different
  carriage, same hardness philosophy).
- **`docs/rules/` untouched** (agents ask before changing `docs/`; rule docs
  are never edited to fit software). No glossary concept added. `FloorTotalAtZero`
  (`ClassDefinition.cs:331-361`) and the normalised-grain clamp are orthogonal
  and untouched.

## Plan

### WI-1 — Domain: warning type + pure computation + `ScoreGroup` step 2e

Scope: `src/Soarscore.Domain/Scoring/ScoringResultTypes.cs` only, plus the
`ScoreGroup` insertion in `src/Soarscore.Domain/Scoring/ScoringService.cs:
170-197`.

1. New `public sealed record ScoreWarning(string Code, string Message);`
   beside `DrawWarning`'s pattern (codes: `score.windowSumExceeded`,
   `score.windowSumExceeded` checked first; else `score.turnaroundCapExceeded`).
   Message format (stable, NdcScore renders verbatim): for window —
   `$"Entry {competitor} in group {group}: flight-time sum {sum}s reaches
   the {W}s working time (task {code})"`; for turn-around —
   `$"Entry {competitor} in group {group}: flight-time sum {sum}s exceeds
   the turn-around cap {cap}s (working time {W}s minus {n} flights, task
   {code})"`. Seconds rendered at recorded precision (F3K 0.1 s,
   `SeedF3K.cs:23`; F5K whole seconds, `SeedF5K.cs:27`).
2. Pure static function (testable without groups), e.g.
   `WindowPlausibility.Check(ResolvedTask, TaskResult) → ScoreWarning?`:
   implements the Applicability exclusions, the `n > 1` gate, the ceiling test,
   and the two-threshold order from **What** exactly — `Valid` + non-null
   non-empty `Selection` with `n > 1` + Fixed + `WorkingTime.HasValue` + ≥1
   `flightTime`-consumed term + ceiling either unbounded or `W >= ceiling`,
   else null. Sums `MetricConsumed` only at indices whose unwrapped metric ref
   is `"flightTime"` (missing index → contributes 0, never throws).
3. `GroupResult` gains `ImmutableDictionary<string, ImmutableArray<ScoreWarning>>
   Warnings` (keys exactly `Results` keys; empty array = clean; never null —
   default `ImmutableDictionary.Empty` so old construction sites compile with
   an explicit empty). `ScoreGroup` step 2e: after the 2d floor block, per
   `(competitorRef, taskResult)`, `Warnings[competitorRef] = Check(…) is { } w
   ? [w] : []`. No score field touched in this step — the diff on
   `RawScore`/`PreNormalisationScores` for any input is empty by construction.
4. `ResolvedTask`/`ResolvedTiming` untouched (already carry everything).

Done-when: every existing `ScoreGroup` caller compiles with an explicit empty
map default; scores for a 300+300 Task D input are unchanged (600 pre- and
post-normalisation inputs identical) while `Warnings` carries exactly one
`score.windowSumExceeded`; 300+298 carries none; 299.5+299 carries exactly one
`score.turnaroundCapExceeded`; single-flight input (Task N 599, F5J 600) carries
none via the `n > 1` gate; F5K-C-shaped input (3 × 240 vs `W` 241) carries none
via the ceiling test; F5K-D-shaped input (180+180+240 = 600, points clamped to
599 by the seeded PerTask cap) carries exactly one `score.windowSumExceeded`
with the 599 points unchanged; Poker-shaped input (no `flightTime`-consumed
term) carries none; NoResult/Pending input carries none.
Testing: Domain unit table (Task D 600/598/598.5 boundaries; Task N single-
flight 599; F5J-shaped single 600; F5K-C ceiling skip; F5K-D clamp+warning
coexistence; param-bound WorkingTime e.g. Task A 420/600; per-flight-
capped input proving raw-sum visibility — 350 s recorded flight capped to 300
in points still sums 350 toward the window; multi-term task proving only
`flightTime` indices sum; Poker-shape proving the metric-name exclusion).
ArchUnitNET clean (no new assembly refs).

### WI-2 — Read model: warnings on `GET /task-round-result`

Scope: `src/Soarscore.Application/Queries/Scoring/ScoreTaskRound.cs`
(+ Api serialisation if a shape test demands it — no endpoint change, no new
query, verbs-only routing untouched).

1. `CompetitorTaskResultView` gains `ImmutableArray<ScoreWarningView>
   Warnings` (new `ScoreWarningView(string Code, string Message)` — verbatim
   projection, no formatting, last positional arg so existing construction
   sites fail loudly if missed; empty, never null, for clean rows; NoResult
   rows carry empty). `MapGroupResult` projects
   `result.Warnings[kv.Key]` per row (missing key → empty, never throws).
2. `ScoreCompetition` walk and `CompetitionScoreView` untouched (aggregate-only
   shape has no per-cell slot — explicit scope cut, recorded here so it is not
   mistaken for an omission). `CompleteTaskRound` untouched.

Done-when: the WI-1 Task D inputs read back through the handler with warnings
on exactly the expected rows and `RawScore`/`PreNormalisationScore`/winner
identical to pre-story; clean groups serialise `warnings: []`.
Testing: handler tests (one group, three entries: clean / cap-only / window —
assert row warnings + unchanged scores); JSON round-trip (old payloads without
the array still read — default empty).

### WI-3 — Seeds: explicitly none

Scope: prove the negative. No `tools/Soarscore.SeedData/*.cs` change, no
regeneration, no `json/` diff, no NdcScore fixture mirror. Rationale recorded
in **Verified facts**: the rulebook states neither threshold, so there is no
citation to encode and a literal `598` would be fabrication; the formula
derives `W` from the already-seeded working times at score time.

Done-when: `git status --porcelain -- tools/Soarscore.SeedData/json` empty at
completion (drift-guard discipline per
`kanban/backlog/ci-seed-corpus-drift-guard.md`).
Testing: none (the empty diff is the assertion; WI-4's corpus silence covers
fixtures).

### WI-4 — Properties, acceptance, corpus silence

1. **CsCheck property** (extends the scoring property suite): named invariant —
   *for any Fixed-working-time task with a `flightTime`-consumed term, ceiling
   either unbounded or `W >= ceiling`, and any selection of `n > 1` valid
   flights: warnings ⟺ (sum > W or (sum == W) → window) else (sum > W-n →
   turn-around); scores equal the no-story scores exactly; UntilAllFlights-
   Complete / per-launch-ceiling / single-flight / NoResult / pending / Poker-
   shaped inputs never warn.* Generated over corpus task shapes (incl. F5K-A/D
   target shapes and the F5K-C ceiling shape) × small flight-counts/sums
   straddling both thresholds.
2. **Acceptance BDD** (task-round-result feature area): F3K Task D 600 s group —
   enter 300+300 for one pilot, legal sums for the rest → `GET
   /task-round-result` 200, flagged row carries `score.windowSumExceeded` with
   unchanged normalised scores for the group; second scenario for the
   cap-only band (e.g. 299.5+299 → `score.turnaroundCapExceeded`). Both stores
   not required here (sqlite); WI-5 covers postgres.
3. **Corpus silence:** full acceptance suite (Gliderscore replay + parallel-run
   oracles) green — scores byte-identical on every fixture. Warnings MAY appear
   on historical fixture rows (old sheets contain exactly the transcription
   errors this story surfaces); that is the feature working, not drift — the
   assertion is scores-unchanged, not warnings-absent. Ledger nothing.

Done-when: property + new BDD scenarios green; full suite otherwise unchanged.
Testing: as above; noSeed/property work outside WI-1's table duplicates
nothing.

### WI-5 — Full verification + board reconciliation

Local, in order: Domain + Application + Architecture green; Infrastructure
non-Storage + Acceptance on **both stores** (`SOARSCORE_TEST_STORE=sqlite`
then postgres via Testcontainers); build 0 warnings; `graphify update .`.
Reconcile `kanban/deferred-decisions.md` only if a bullet claims scores are
never annotated on read (amend, don't delete history); `kanban/tech-debt.md`
gains an entry only if a follow-up is deliberately cut (e.g. per-cell warnings
on competition-result, `CompleteTaskRound` advisory envelope) — otherwise no
new debt. Leave the NdcScore item parked (its render-verbatim work is that
repo's business on landing this one); move this file to `completed/` with the
plan updated *as built*.

## Before starting

- Read the NdcScore stub (`~/Source/NdcScore/kanban/backlog/
  turn-around-window-score-validation.md`) for the verbatim organiser quotes;
  then the rulebook proof above — do not re-litigate whether F3K states a cap
  (it does not; the script output is the evidence).
- Read `ScoreGroup` 2a–2e, `FlightInterpreter.EvaluateRate`,
  `ComputePerTaskReduction`, and `ScoreTaskRound.MapGroupResult` at the cited
  lines — cite before relying (line numbers verified 2026-09-30, re-verify at
  implementation).
- Confirm `ScoreTermRefs.GetTermMetricRef` unwraps `ConditionalTerm` (Poker E
  nests its `flightTime`/`targetTime` rate terms inside `When`); if it does not,
  unwrap in the check — generically, never per-task.

## Alternative homes considered and set aside

- **Literal `598` PerTask cap on Task D (or any seed datum):** fabrication —
  no rulebook citation exists; bakes one organiser's 1 s/flight estimate into
  every future contest; needs a second cap slot or a new datum for a formula
  the vocabulary has no shape for. Rejected.
- **New task-level `MaxRaw` / formula datum:** same citation problem plus a
  vocabulary extension for a heuristic (NFR-2 admits term types only when a
  class's rules require them — no rule requires this). Rejected; the working
  times that feed the formula are already data.
- **Capture-time refuse / `CompleteTaskRound` refusal:** NFR-4 tension (gates
  the evening on scoring state) and against the organiser's own softer reading
  ("flag"); a mistyped flight would block the whole group instead of flagging
  one row. Rejected for v1; a CD-explicit hard gate (if ever wanted) is a
  separate story with its own hardness datum, not a silent addition here.
- **Silent clamp of the raw score:** rewrites standings without a trace,
  violating the auditability trust model; the CD could never distinguish a
  flown 598 from a clamped 700. Rejected — scores are never touched.
- **Event-carried warnings (draw-path precedent):** scores are derived on read,
  not logged decisions — persisting per-entry warnings would version the event
  log for data a pure function already yields. Rejected; derive on read.

## NdcScore's part once it lands

Nothing structural. NdcScore renders each row's `warnings` verbatim (code +
message, results-block flag where the sheet has one) — no arithmetic, no
thresholds, no window math in client code (law 2). Today's silent impossible
scores then show a flag beside the offending row at readout, before
normalisation bakes them in. OpenAPI `v1.json` refresh picks up the new arrays;
no fixture change is forced (empty arrays serialise as `[]`).
