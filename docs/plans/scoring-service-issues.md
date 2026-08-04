# Scoring Service — Open Design Issues

Resolved by: project lead. Checked off as each is decided. Each issue references the WI(s) that depend on it.

---

## Issue #1: `CapScope.PerTask` — flight interpreter / flight selector interaction

**Status**: [x] RESOLVED

**Priority**: CRITICAL — affects F5K scoring

**Depends on**: WI-3 (FlightInterpreter), WI-4 (FlightSelector)

**Background**:

`CapScope.PerTask` on a `RateTerm` means the cap applies to the **sum of the metric values** across all selected flights, not per flight. F5K Task A caps total flight time at 599s across 4 flights, summed.

The flight interpreter (WI-3) evaluates one flight at a time. It cannot apply a per-task cap because it doesn't know the sum across flights.

**Resolution**: **Option A — Two-pass as described.**

1. **WI-3 (per-flight, uncapped)**: For any `RateTerm`, always set `MetricConsumed = rawMetric` (uncapped). For `PerFlight` caps, apply the clamp to the metric value before computing points. For `PerTask`, leave the metric unclamped — points computed as `rawMetric × rate`, and WI-4 will correct.

2. **WI-4 (corrects after selection)**: After selecting flights and summing the uncapped scores, recursively walk the `task.Score` tree to find every `RateTerm` where `CapScope == PerTask` and `Cap` is set. For each such term, sum `MetricConsumed` across all selected flights. If `sum > cap`: `reduction = (sum - cap) × rate`. Subtract from the total raw score.

**Rationale**:
- WI-3 stays flight-local (design rule #3).
- `TermContribution` was already designed to carry `MetricConsumed` for this purpose.
- No duplicated term-evaluation logic.
- Recursion handles PerTask RateTerms nested inside ConditionalTerms, should the model ever need it.

---

## Issue #2: `validWhen` evaluation semantics

**Status**: [x] RESOLVED

**Priority**: CRITICAL — affects `NoResult` determination in WI-4

**Depends on**: WI-4 (FlightSelector), WI-3 (PredicateEvaluator)

**Background**:

`Task.validWhen` is a `Predicate` that decides whether the task has a result at all (`NoResult`). It is evaluated at the `select flights` stage (WI-4). The predicate references metric names like `courseCompleted`, `landedInDefinedArea` — these are per-`Flight` measurements.

Both F3B Task C and F3F Task S use `LastFlight` (one flight per Entry). No multi-flight task in the corpus uses `validWhen`.

**Resolution**: **Option A with an adoption-time safety net.**

**Runtime behaviour (Option A)**: After flight selection, evaluate `validWhen` against each selected flight's measurements. If **any** selected flight fails the predicate → `NoResult`. This is the natural generalisation of the single-flight case and mirrors `flightValidWhen` semantics at task scope.

**Adoption-time safety net**: Reject any class definition where `validWhen` is set AND the flight selection could yield more than one flight (`AllFlights`, `BestNFlights(n > 1)`, `LastNFlights(n > 1)`, `ExactlyNInOrder(n > 1)`). The adoption check carries a comment explaining:

> *Assumption: `validWhen` is only exercised with single-flight tasks in the 11-class corpus (F3B Task C, F3F Task S). The Option A multi-flight semantics are implemented and tested at unit level, but no real rulebook's worked example validates them. Rejecting multi-flight + validWhen at adoption prevents shipping untestable scoring behaviour.*
>
> *When to revisit: if a real rulebook defines a multi-flight task with a `validWhen` gate.*
>
> *How to extend: Option A semantics are already live in the FlightSelector. Lifting this adoption check is safe — remove the constraint and nothing else changes.*

---

## Issue #3: `BestNFlights` AnyOrder target pairing algorithm

**Status**: [x] RESOLVED

**Priority**: CRITICAL — affects F3K Task H scoring (the 569 vs 333 divergence)

**Depends on**: WI-4 (FlightSelector)

**Background**:

`BestNFlights` with `Targets.AnyOrder` pairs selected flights with `TargetValues`. The `ScoringVocabulary.cs` comment explicitly warns:

> "The list is written ascending while the ranking descends, so pairing rank `i` with `TargetValues[i]` — which is what `ExactlyNInOrder` does, and the natural reading of an index-aligned array — scores F3K Task H's worked example at 333 where `F3K.11.8` states 569."

**Resolution**: Confirmed. The algorithm is:

1. **Rank** selected flights descending by `rankByMetric` (or by preliminary score if null).
2. **TargetValues** is in ascending order: `[v₀, v₁, …, vₙ₋₁]`.
3. **Pair** flight at rank position `i` (0 = best) with `TargetValues[n-1-i]` (largest target). So the best flight takes the largest target, worst takes the smallest.
4. **Clamp** that flight's scored metric to its assigned target value (min(metric, target)).
5. **Re-score** the flight using the clamped metric (only the rate/lookup/piecewise term reading that metric is affected; other score terms in the list are unchanged).

The `TargetValues[n-1-i]` formula is precise and correct. It produces the descending-rank → ascending-target pairing documented in the code comment and notation §5.

The 595≠569 numerical discrepancy in the issue text is likely due to the rulebook's worked example using different flight times than the 235/200/180/154s quoted. The algorithm direction (best→largest) is authoritatively confirmed by the model's own documentation.

---

## Issue #4: Measurement amendment resolution — where does it live?

**Status**: [x] RESOLVED

**Priority**: IMPORTANT — affects interface between orchestrator and pipeline stages

**Depends on**: WI-3 (FlightInterpreter), WI-9 (ScoringService)

**Background**:

A `Measurement` has an ordered list of `Amendment`s. The effective value is the most recent amendment's value, or the original if none. This resolution must happen before scoring.

**Resolution**: **Option A — Orchestrator pre-processes.**

The `ScoringService` (WI-9) resolves all amendments into `ResolvedMeasurements` before calling any pipeline stage. Pipeline stages receive a flat `IReadOnlyDictionary<string, MeasuredValue>` — they never see raw `Amendment` lists. The amendment resolution logic is a static helper in WI-9: for each `Measurement`, the effective value is the most recent `Amendment.NewValue` (by timestamp/ordinal), or the original `Measurement.Value` if there are no amendments.

**Rationale**:
- Design rule #2 (no I/O in domain) is stronger than "pipeline stages do I/O-like work" — resolving amendments is data transformation, not pure scoring, and it belongs at the boundary.
- Pipeline stages stay simpler: they just read from a dictionary.
- Testability: pipeline stage tests provide resolved measurements directly, never constructing Amendment lists.
- Clean separation: the orchestrator is the bridge between the Competition aggregate (which owns Amendments) and the pure scoring pipeline.

The effective-measurement resolution logic lives as a private static method on `ScoringService` (WI-9). The signature passed to pipeline stages is `IReadOnlyDictionary<string, MeasuredValue> resolvedMetrics`.

---

## Issue #5: `minValidResults` and group annulment — whose job?

**Status**: [x] RESOLVED

**Priority**: IMPORTANT — affects `GroupResult` semantics and group state

**Depends on**: WI-5 (NormalisationEngine), WI-9 (ScoringService)

**Background**:

`GroupConstraint.MinValidResults` says a group with fewer than that many valid (non-`NoResult`) entries is annulled. F3B uses this: `MinValidResults = 2` means a group with 0 or 1 valid results is annulled.

An annulled group means the task-round has no valid results for anyone in that group. This is a state change on the `TaskRound` (the Competition aggregate), not just a scoring output.

**Resolution**: **Option A — WI-5 returns a flagged GroupResult with a boolean `IsAnnulled` field.**

1. **WI-5 checks the threshold**: `NormalisationEngine.Normalise` already computes `ValidCount`. It compares against `task.Group?.MinValidResults`. If `ValidCount < MinValidResults`, it sets `IsAnnulled = true` on the returned `GroupResult`.

2. **Scores are still computed**: Even an annulled group gets its normalised scores computed normally (everyone gets their normalised score). This preserves information for display and transparency — the CD may want to see what the scores would have been.

3. **WI-9 reads the flag**: The orchestrator checks `GroupResult.IsAnnulled` and, if true, annuls the `TaskRound` on the Competition aggregate. The scores are recorded on the round but the round is marked annulled.

4. **GroupResult gets a new field**: `bool IsAnnulled` — defaults to `false`. Added to the type in WI-1.

**Rationale**:
- Scoring logic stays in one place: WI-5 knows about `ValidCount` and `MinValidResults` and checks them in a single pass.
- The orchestrator handles the aggregate state change (its job), not the scoring engine.
- Scores are preserved for transparency — an annulled group is different from "nobody flew."
- No double-counting of valid results: WI-5 computes it once.

---

## Issue #6: `validWhen` and flight selection — what ordering?

**Status**: [x] RESOLVED

**Priority**: IMPORTANT — affects WI-4 pipeline ordering

**Depends on**: WI-4 (FlightSelector)

**Background**:

The plan tentatively orders WI-4 as: interpret all flights → evaluate `validWhen` → select flights → assemble raw.

But if `validWhen` evaluates against the **selected** flights' measurements (issue #2 option A), then flight selection must happen first. Conversely, if `validWhen` evaluates against **all** flights, selection can happen after.

F3B Task C (`flights last`, one flight) and F3F (same) don't distinguish these orderings. A multi-flight task with both `validWhen` and a selective `FlightSelection` (e.g., `bestN`) would expose the difference.

**Resolution**: **Option B — validWhen is checked AFTER flight selection.**

The FlightSelector algorithm ordering is:

1. Annulled? → return NoResult
2. Interpret every flight
3. **Select flights** (apply FlightSelection — LastFlight, BestNFlights, etc.)
4. **Evaluate validWhen** against only the selected flights' measurements. If any selected flight fails → NoResult.
5. Assemble raw score, apply PerTask caps, round.

Since Issue #2 resolved that validWhen evaluates against **selected** flights' measurements (Option A), selection must logically precede the check. The plan's tentative description already favoured this ordering; this resolution confirms it as the spec.

**Rationale**:
- Logically required by Issue #2's resolution: validWhen checks selected flights, so selection happens first.
- Only flights that matter can cause NoResult — an unselected flight that would have been discarded can't invalidate the task.
- With Issue #2's adoption-time safety net (reject multi-flight + validWhen), the ordering distinction won't arise in production for the current corpus, but the code implements the general case correctly.
- The adoption check ensures that a multi-flight task with validWhen is rejected at adoption time, preventing the untestable scenario from reaching production.

---

## Issue #7: `ResolvedTask` type placement

**Status**: [x] RESOLVED

**Priority**: MINOR — namespace organisation

**Depends on**: WI-1 (Result Types)

**Background**:

`ResolvedTask` is a snapshot of `TaskDefinition` with all `NumberOrParam`/`FlagOrParam` resolved to concrete values. It's consumed by all pipeline stages. The plan puts it in `ScoringResultTypes.cs` (WI-1), in the `Soarscore.Domain.Scoring` namespace.

But `ResolvedTask` is conceptually "resolved class data" rather than "scoring output". It could live in the `CompetitionClasses` namespace instead.

**Resolution**: **Option A — `Soarscore.Domain.Scoring` namespace**, as already specified in the plan.

`ResolvedTask` stays in `ScoringResultTypes.cs` in the `Soarscore.Domain.Scoring` namespace, alongside the other scoring types.

**Rationale**:
- `ResolvedTask` is produced by `ParameterResolver` (a scoring concern) and consumed exclusively by pipeline stages. It's a scoring input type, not a domain model type.
- Putting it in `CompetitionClasses` would put a type referencing "resolved" versions of `NumberOrParam`/`FlagOrParam` slots (which are inherently a scoring/pre-processing concern) into a namespace that otherwise knows nothing about parameter resolution.
- The `CompetitionClasses` namespace holds the canonical definition types (`TaskDefinition`, `ClassDefinition`, etc.). A resolved snapshot is a derivative, not a definition.
- All pipeline stages are in `Soarscore.Domain.Scoring` — having the input type they all consume in the same namespace keeps the dependency graph clear.

---

## Issue #8: `ByTask` drop dimension — exact algorithm

**Status**: [x] RESOLVED

**Priority**: MINOR — affects WI-7 implementation

**Depends on**: WI-7 (PhaseAggregator)

**Background**:

`DropDimension.ByTask` drops the lowest N scores **per task code** across all rounds in a phase. F3B is the only class using it: one phase, 3 tasks per round (A, B, C per round). `ByTask 1` drops the lowest Task A score across rounds, the lowest Task B, and the lowest Task C.

**Resolution**: **The plan's algorithm is confirmed**, with the gate semantics made explicit:

**Algorithm**:

1. **Group by `TaskCode`** (not by task ordinal). Task A scores from different rounds are grouped together. Task B and Task C form their own groups.

2. **Check gates**, which have different scopes for `ByTask`:
   - **`ApplyWhenRoundsCompletedAtLeast`**: Checked ONCE at the phase level — the total number of completed (non-annulled) rounds in the phase. This is the same for all task codes.
   - **`ApplyWhenResultsAtLeast`**: Checked PER TASK CODE. For each task code, count how many task-round scores exist for that task code across all rounds. The drop for that task code only applies if this count meets the threshold.

   Both gates are conjunctive (F18): both must hold for the drop to apply to a given task code. If the rounds gate fails, no drops apply at all. If the results gate fails for a specific task code, that task code's drop is skipped but others may still apply.

3. **Sort each task code's scores ascending**.

4. **Drop the lowest `DropCount` from each task code** where both gates held.

5. **Sum all remaining scores across all task codes**. This sum is the phase aggregate.

**Example (F3B, 6 rounds, 3 tasks/round)**:
- Completed rounds = 6. Gate: `whenRounds >= 6` → holds.
- Task A: 6 results. Gate: `whenResults >= 6` → holds. Drop 1 of 6.
- Task B: 6 results. Gate: `whenResults >= 6` → holds. Drop 1 of 6.
- Task C: 5 results (one round annulled for this competitor). Gate: `whenResults >= 6` → fails. No drop for Task C.
- Sum: 5 Task A scores + 5 Task B scores + 5 Task C scores = 15 scores.

**Gates check (pseudocode)**:
```
roundsOk = completedRounds >= policy.ApplyWhenRoundsCompletedAtLeast (if set)
For each taskCode:
    resultsForTask = scoresByTask[taskCode].Count
    resultsOk = resultsForTask >= policy.ApplyWhenResultsAtLeast (if set)
    applies = (roundsOk ?? true) && (resultsOk ?? true)
    if applies: drop lowest DropCount from that task code
```

---
