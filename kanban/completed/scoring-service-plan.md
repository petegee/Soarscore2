# Scoring Service Build Plan

> **Status: restored history. WI-1 through WI-8 are implemented; WI-9 is superseded.**
>
> This document was added in `d1ea17d` and removed in `38cb008` ("renamed folder to
> reflect true agg root"), then restored verbatim at `900df6d` because all eleven files
> under `src/Soarscore.Domain/Scoring/` header-cite it and nothing else records the
> reasoning behind their design.
>
> **WI-1 through WI-8 shipped and are the code in the tree** — result types, parameter
> resolver, flight interpreter, flight selector, normalisation, penalty engine, phase
> aggregator, ranking engine. Read them as built, not as planned.
>
> **WI-9 (the orchestrator) did not ship.** `ScoringService.cs` exists but has no
> caller, `ScoreCompetition` is a shell, and amendment resolution was never written.
> **`kanban/completed/scoring-steel-thread-plan.md` (2026-08-09) supersedes WI-9** and takes
> it to a working end. Where the two disagree, the newer plan wins — it departs from
> this one in three recorded places: `ScoringService` becomes a `static` class rather
> than an instantiable one, the engine keeps its `string` refs rather than being
> retyped to the domain's id structs, and `ScoreCompetition` returns `Result<T>`.
>
> The eight design issues this plan defers to `scoring-service-issues.md` are all
> resolved, and those resolutions still bind — issue #4 in particular governs where
> amendment resolution lives.

## Overview

Build the `ScoringService` domain service per the architecture documents:

- `docs/high-level-architecture.md` — core principles (headless, hexagonal, DDD, event-sourced, CQRS, functional-like, immutable)
- `docs/aggregate-roots.md` — aggregate boundaries (Competition, Entry, Person, CompetitionClass) and ScoringService interface
- `docs/soaring-domain-class-diagram.md` — the full domain model including the scoring pipeline
- `docs/competition-class-notation.md` — the class definition notation, worked examples, and findings

### What already exists

`src/Soarscore.Domain/` contains the complete `CompetitionClasses` namespace:

| File | Contents |
|---|---|
| `ClassDefinition.cs` | `ClassDefinition`, `PhaseDefinition`, `TaskDefinition`, `Parameter`, `ReflightRule`, `PenaltyDefinition`, `PenaltyEffectSpec`, `DropPolicy`, `ValidityRule`, `PromotionRule`, `RoundComposition` |
| `ScoringVocabulary.cs` | `MeasuredValue`, `MetricDefinition`, `FlightSelection` (5 subtypes), `ScoreTerm` (5 subtypes), `Predicate` (2 subtypes), `Band`, `LookupRow`, `TaskTiming`, `GroupConstraint`, `Normalisation`, `Rounding` |
| `Enumerations.cs` | All enums (`MeasuredKind`, `PhaseType`, `NormalisationDirection`, `Comparator`, `PenaltyEffect`, `ReflightSelection`, etc.) |
| `ParameterReference.cs` | `NumberOrParam`, `FlagOrParam` (discriminated unions for the 13 slots that accept ParameterRefs) |

`tools/Soarscore.SeedData/` contains 11 class definitions in C# (F3B, F3F, F3J, F3K, F5J, F5K, F5L, NZ-M, NZ-M-NDC, NZ-N, NZ-P) that serve as test fixtures.

`Soarscore.Domain.csproj` has **zero PackageReference** — the domain layer has no dependencies.

### What we are building

The `ScoringService` is a **domain service**, not an aggregate. It reads adopted rules + structure from the `Competition` aggregate and raw data from the `Entry` aggregate. It produces derived results — nothing is stored, everything is computed on demand.

### The scoring pipeline (fixed, core-owned)

```
capture → interpret flight → select flights → assemble raw → clamp →
  round(raw) → normalise → add normalised terms → round(normalised) →
  aggregate phase → drop → apply penalties → rank
```

Two stages are skipped (no-ops) when a class is silent: `normalise` when the task has no `Normalisation`, and `add normalised terms` when the task writes no `ScoreNormalised` list.

### ScoringService interface (from aggregate-roots.md §Scoring)

```
interpretFlight(Flight) → FlightResult
selectFlights(Entry) → TaskResult
normaliseGroup(Group) → GroupResult
aggregate(Competitor, Phase) → PhaseResult
rank(Competition) → ScoreResult
```

---

## Issue Tracking

**Before starting any work item, check `kanban/completed/scoring-service-issues.md`.**

There are 8 open design questions. Each WI that depends on an unresolved issue is marked with **[SEE ISSUE #N]**. The agent must confirm the resolution (check the issue checkbox) before implementing anything that depends on the unresolved semantics.

Issue resolutions are owned by the project lead. Agents **stop and ask** if they hit a dependency on an unresolved issue.

---

## Work Items

Nine work items in three waves. Each is a self-contained deliverable with defined inputs, outputs, and acceptance criteria.

### Dependency Graph

```
Wave 1 (parallel):
  WI-1 (Result Types)     ←── no dependencies
  WI-2 (Parameter Resolver) ←── WI-1

Wave 2 (parallel within tiers):
  WI-3 (Flight Interpreter) ←── WI-1, WI-2
  WI-6 (Penalty Engine)     ←── WI-1

  WI-4 (Flight Selector)    ←── WI-1, WI-2, WI-3 [ISSUE #1, #2, #3, #6]
  WI-7 (Phase Aggregator)   ←── WI-1, WI-2 [ISSUE #8]

  WI-5 (Normalisation)      ←── WI-1, WI-2, WI-4 [ISSUE #5]
  WI-8 (Ranking)            ←── WI-1

Wave 3:
  WI-9 (Orchestrator)       ←── WI-1..WI-8 [ISSUE #4]
```

### Parallelism Summary

| Can run together | Work Items |
|---|---|
| Immediately | WI-1, then WI-2 |
| After WI-1+WI-2 | WI-3 + WI-6 (together) |
| After WI-3 | WI-4 |
| After WI-2 (doesn't need WI-3) | WI-7 |
| After WI-4 | WI-5 |
| Independent of pipeline | WI-8 |
| After all above | WI-9 |

---

## Wave 1 — Foundation

### WI-1: Scoring Result Types

**Agent instructions**: Define the value objects produced by each pipeline stage. Pure data, no behaviour. All types are `sealed record`s.

**Location**: `src/Soarscore.Domain/Scoring/ScoringResultTypes.cs`

**New namespace**: `Soarscore.Domain.Scoring`

**Types to define**:

```csharp
/// Holds the effective measurements for one flight, with amendments resolved.
/// This is what all pipeline stages read — they never see raw Amendment lists.
public sealed record ResolvedMeasurements(
    IReadOnlyDictionary<string, MeasuredValue> Metrics
);

/// One flight, evaluated through flightValidWhen and raw score terms.
public sealed record InterpretedFlight(
    FlightResult Result,
    decimal Score,
    /// Per-term contributions: term list index → (raw metric consumed, points contributed).
    /// FlightSelector needs this for CapScope.PerTask rate terms.
    IReadOnlyDictionary<int, TermContribution> TermContributions
);

public sealed record TermContribution(
    decimal MetricConsumed,   // the metric value this term read (0 for Constant/Conditional)
    decimal Points            // the points this term contributed
);

public enum FlightResultState { Valid, NoResult }

public sealed record FlightResult(
    FlightResultState State,
    ResolvedMeasurements Measurements
);

/// Which flights were selected and what target each was assigned.
public sealed record SelectedFlights(
    ImmutableArray<InterpretedFlight> Flights,
    /// Maps flight sequence → assigned target value (null if no target for that flight)
    IReadOnlyDictionary<int, decimal?> TargetAssignments
);

public enum TaskResultState { Valid, NoResult }

/// One Entry's result for one task.
public sealed record TaskResult(
    TaskResultState State,
    SelectedFlights? Selection,  // null when NoResult
    decimal RawScore             // summed + clamped + rounded
);

/// One group's worth of task results.
public sealed record GroupResult(
    /// CompetitorRef → TaskResult. Includes NoResult entries.
    ImmutableDictionary<string, TaskResult> Results,
    /// The competitor with the best normalised score, or null if no valid results.
    string? WinnerRef,
    /// Count of Valid (non-NoResult) results in this group.
    int ValidCount,
    /// True when ValidCount < MinValidResults — the group is annulled (Issue #5).
    bool IsAnnulled = false
);

/// One competitor's task-round score for one task in one round.
public sealed record TaskRoundScore(
    string TaskCode,
    int RoundOrdinal,
    int TaskOrdinal,
    decimal Score
);

/// One competitor's scores across a phase, after drops.
public sealed record PhaseScores(
    string CompetitorRef,
    decimal Aggregate,
    ImmutableArray<TaskRoundScore> AllScores,   // before drops
    ImmutableArray<TaskRoundScore> DroppedScores // which scores were dropped
);

/// Penalties applied to one competitor at one scope.
public sealed record PenaltyApplication(
    decimal Deduction,
    bool Disqualified
);

/// A competitor's final score before ranking.
public sealed record FinalCompetitorScore(
    string CompetitorRef,
    decimal Score,
    bool Disqualified
);

/// The final competition result.
public sealed record CompetitionResult(
    ImmutableDictionary<string, FinalCompetitorScore> Scores,
    ImmutableDictionary<string, int> Placings
);

/// A recorded penalty against an Entry or Competition.
public sealed record RecordedPenalty(
    string InfractionType,
    int OccurrenceCount
);

/// A TaskDefinition with all NumberOrParam/FlagOrParam resolved to concrete values.
/// Pipeline stages consume this — they never see unresolved parameter refs.
public sealed record ResolvedTask(
    string Code,
    string Name,
    ImmutableArray<MetricDefinition> Metrics,
    FlightSelection Flights,
    ResolvedTiming Timing,
    ResolvedGroupConstraint? Group,
    Normalisation? Normalise,
    Predicate? ValidWhen,
    Predicate? FlightValidWhen,
    Rounding? RawScore,
    ReflightRule? Reflight,
    ImmutableArray<ScoreTerm> Score,
    ImmutableArray<ScoreTerm> ScoreNormalised
);

public sealed record ResolvedTiming(
    WorkingTimeKind Kind,
    decimal? WorkingTime,
    decimal? PreparationTime,
    int? MaxLaunches
);

public sealed record ResolvedGroupConstraint(
    decimal MinPerGroup,
    int? MinValidResults
);
```

**Dependencies**: None — depends only on existing domain types in `Soarscore.Domain.CompetitionClasses`.

**Acceptance criteria**:
- [ ] All types are `sealed record`s
- [ ] No methods beyond equality/ToString
- [ ] `ResolvedTask` covers every nullable slot on `TaskDefinition` that contains `NumberOrParam`/`FlagOrParam`
- [ ] `TermContribution` correctly separates metric-consumed from points-produced (needed by WI-4 for CapScope.PerTask)
- [ ] Compiles with zero warnings

---

### WI-2: Parameter Resolver

**Agent instructions**: Resolve `NumberOrParam`/`FlagOrParam` to concrete `decimal`/`bool` values, and produce `ResolvedTask` snapshots.

**Location**: `src/Soarscore.Domain/Scoring/ParameterResolver.cs`

**Interface**:

```csharp
public static class ParameterResolver
{
    /// Resolve a NumberOrParam to a concrete decimal.
    /// Literal passes through; Ref looks up the binding.
    /// Throws UnresolvedParameterException if a Ref has no binding.
    public static decimal Resolve(
        NumberOrParam value,
        IReadOnlyDictionary<string, MeasuredValue> bindings
    );

    /// Resolve with a fallback when the slot itself is null.
    public static decimal ResolveOr(
        NumberOrParam? value,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        decimal @default
    );

    /// Resolve a FlagOrParam to a concrete bool.
    public static bool Resolve(
        FlagOrParam value,
        IReadOnlyDictionary<string, MeasuredValue> bindings
    );

    /// Resolve with a fallback when the slot itself is null.
    public static bool ResolveOr(
        FlagOrParam? value,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        bool @default
    );

    /// Produce a ResolvedTask from a TaskDefinition by resolving every
    /// NumberOrParam/FlagOrParam slot against the provided bindings.
    /// This is the main entry point — pipeline stages consume ResolvedTask.
    public static ResolvedTask ResolveTask(
        TaskDefinition task,
        IReadOnlyDictionary<string, MeasuredValue> bindings
    );
}

/// Thrown when a ParameterRef has no corresponding binding.
public sealed class UnresolvedParameterException : Exception
{
    public string ParameterName { get; }
    // ...
}
```

**Algorithm**:

1. `Resolve(NumberOrParam, bindings)`:
   - If `Literal` → return `.Value`
   - If `Ref` → look up `.ParameterName` in `bindings`. If found and `Kind == Number` → return `.Number.Value`. If not found → throw `UnresolvedParameterException`.
2. `Resolve(FlagOrParam, bindings)`:
   - If `Literal` → return `.Value`
   - If `Ref` → look up `.ParameterName` in `bindings`. If found and `Kind == Flag` → return `.Flag.Value`. If not found → throw.
3. `ResolveTask(task, bindings)`:
   - Walk every `NumberOrParam`/`FlagOrParam` slot on the `TaskDefinition` and its children (bands, lookup rows, etc.).
   - Produce a `ResolvedTask` with all values concrete.
   - Note: `ScoreTerm` trees contain `NumberOrParam` inside `RateTerm.Cap`, `PiecewiseTerm.Origin`, `Band.From`, `Band.To`. These must all be resolved recursively.

**Dependencies**: WI-1 (for `ResolvedTask`, `UnresolvedParameterException`), existing `ParameterReference.cs`.

**Acceptance criteria**:
- [ ] Literal values pass through unchanged
- [ ] ParameterRef resolves from bindings dictionary
- [ ] Missing binding throws `UnresolvedParameterException` with the parameter name
- [ ] `ResolveTask` produces a fully concrete `ResolvedTask` — verify by round-tripping through an F3K task with all its parameters bound
- [ ] Band bounds that are `NumberOrParam.Ref` resolve correctly (NZ Class M's parameterised target time)

---

## Wave 2 — Pipeline Stages

### WI-3: Flight Interpreter (`interpret flight`)

**Agent instructions**: Evaluate one `Flight`'s measurements through `flightValidWhen` and through the raw score terms. This is a pure function — same inputs, same outputs.

**Location**: `src/Soarscore.Domain/Scoring/FlightInterpreter.cs`

**This WI also owns `PredicateEvaluator`** — a shared static class used by WI-3 and WI-4:

**Location**: `src/Soarscore.Domain/Scoring/PredicateEvaluator.cs`

```csharp
public static class PredicateEvaluator
{
    /// Evaluate a Predicate against a set of resolved measurements.
    /// The measurements come from one flight (for flightValidWhen and score-term
    /// conditionals) or from selected flights collectively (for validWhen — see WI-4).
    public static bool Evaluate(
        Predicate predicate,
        IReadOnlyDictionary<string, MeasuredValue> measurements
    );
}
```

**FlightInterpreter interface**:

```csharp
public static class FlightInterpreter
{
    /// Evaluate one flight: resolve measurements, apply flightValidWhen,
    /// evaluate each raw score term, return score + per-term breakdown.
    public static InterpretedFlight Interpret(
        Flight flight,
        ResolvedTask task,
        int flightSequence,  // the flight.sequence intrinsic (1-based)
        IReadOnlyDictionary<string, MeasuredValue> resolvedMetrics
            // pre-resolved effective measurements for this flight (amendments applied)
    );
}
```

**Algorithm**:

1. **Build metric dictionary**: Start with `resolvedMetrics`. Add the intrinsic: `"flight.sequence" → MeasuredValue.Of(flightSequence)`.

2. **Evaluate `flightValidWhen`**: If `task.FlightValidWhen` is not null, evaluate it against the metric dictionary. If it fails → return `InterpretedFlight` with `State = Valid`, `Score = 0`, and all `TermContributions` zeroed. The flight is still counted — it is zeroed, not voided.

3. **Evaluate raw score terms**: For each `ScoreTerm` in `task.Score` (indexed), call `EvaluateTerm(term, metrics)` to get a `TermContribution`. Sum the `.Points` values for the total `Score`.

4. **Term evaluation** (`EvaluateTerm`):

   - **`ConstantTerm`**: `MetricConsumed = 0, Points = term.Value`
   - **`RateTerm`**: Resolve the metric from `metrics[term.MetricRef]`. For `CapScope.PerFlight` with a cap: `metricValue = min(rawMetric, cap)`. For `CapScope.PerTask` or no cap: `metricValue = rawMetric` (uncapped — WI-4 handles the per-task cap). `Points = metricValue × term.Rate`. `MetricConsumed = rawMetric` (always the uncapped raw value, so WI-4 can sum them).
   - **`LookupTerm`**: Resolve metric. Walk `term.Rows` in order. First row where `metricValue ≤ row.UpTo` (or `row.UpTo` is null) wins. `Points = row.Points`. `MetricConsumed = metricValue`.
   - **`PiecewiseTerm`**: Resolve metric. Compute `adjusted = metricValue - (term.Origin ?? 0)`. For each band in order, compute the portion of `adjusted` falling in `[band.From, band.To]` (where null means unbounded below/above), multiply by `band.RatePerUnit`. Sum these for `Points`. `MetricConsumed = metricValue`.
     - Bands are cumulative: at 601s with bands `[0..600 @ 1, 600..any @ -1]`, the score is `600×1 + 1×(-1) = 599`.
   - **`ConditionalTerm`**: Evaluate `term.When` predicate. If true → recursively evaluate `term.Then`. If false → recursively evaluate `term.Else`, or return `(0, 0)` if `Else` is null.

**Predicate evaluation** (`PredicateEvaluator.Evaluate`):

- **`Comparison`**: Get left = `measurements[pred.LeftMetricRef]`. Get right = `pred.RightMetricRef != null ? measurements[pred.RightMetricRef] : pred.RightValue`. Compare per `pred.Op`. Flag values compared with `==` only (other operators on flags are undefined — the adoption check prevents this, but flag it if encountered at runtime).
- **`AllOf`**: `pred.Children.All(c => Evaluate(c, measurements))`.

**Dependencies**: WI-1 (result types, `InterpretedFlight`, `TermContribution`), WI-2 (`ResolvedTask`).

**Acceptance criteria**:
- [ ] F3B Task A: flightTime=601, landedInDefinedArea=true → score 599 (600×1 + 1×(−1))
- [ ] F3B Task A: flightTime=601, landedInDefinedArea=false → score 0 (when predicate fails, no else → 0)
- [ ] F3K Task A: flightTime=200, flightValidWhen (landedWithinWindow ∧ launchedInWorkingTime) passes → score 200
- [ ] F3K Task A: flightTime=200, landedWithinWindow=false → score 0, State=Valid (zeroed, still counted)
- [ ] F3K Task E (Poker): flightTime=46, targetTime=45 → conditional true → rate targetTime → 45
- [ ] F3K Task E: flightTime=44, targetTime=45 → conditional false → score 0 (no else)
- [ ] F5K Task A: launchAltitude at NLH+15 with bands `any..0 @ -0.5, 0..10 @ -1, 10..any @ -3` and origin=60m → adjusted=15 → portions: 0..10 @ -1 (10 pts * -1 = -10), 10..15 @ -3 (5 pts * -3 = -15) → total -25
- [ ] `flight.sequence` intrinsic is available as a metric named `"flight.sequence"` (F5K Task B uses it)
- [ ] PiecewiseTerm with null `From`/`To` (unbounded) works correctly
- [ ] LookupTerm ascending rows work: value 3 gives row ≤3, not row ≤5

---

### WI-4: Flight Selector (`select flights` + `assemble raw` + `clamp` + `round raw`)

**Agent instructions**: Apply flight selection to an Entry, check validWhen, assemble the raw score, apply CapScope.PerTask caps, and round.

**[ISSUE #1, #2, #3, #6] — DO NOT START until these are resolved.** The agent must read `kanban/completed/scoring-service-issues.md`, confirm that issues #1, #2, #3, and #6 have checkmarks, and understand the resolved semantics before implementing anything that depends on them (CapScope.PerTask interaction, validWhen evaluation, AnyOrder target pairing, validWhen ordering).

**Location**: `src/Soarscore.Domain/Scoring/FlightSelector.cs`

**Interface**:

```csharp
public static class FlightSelector
{
    /// Select flights from an Entry, assemble raw score, apply caps, round.
    /// Returns NoResult if validWhen fails or the Entry is annulled.
    public static TaskResult SelectAndScore(
        Entry entry,
        ResolvedTask task,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings
    );
}
```

**Algorithm**:

1. **Annulled?** If `entry.Annulment != null` → return `TaskResult(NoResult, null, 0)`.

2. **Interpret every flight**: For each `Flight` in `entry.Flights` (in sequence order):
   - Resolve amendments: the effective measurement value is the most recent `Amendment.NewValue`, or the original `Measurement.Value` if no amendments. Produce a `ResolvedMeasurements`.
   - Call `FlightInterpreter.Interpret(flight, task, flight.Sequence, resolvedMetrics)`.
   - Collect all `InterpretedFlight`s.

3. **Evaluate `validWhen`**: [ISSUE #2, #6 — semantics and ordering TBD].
   - Tentative: After flight selection, evaluate against the selected flights' measurements. If any selected flight fails the predicate → `NoResult`.

4. **Select flights**: Apply `task.Flights`:
   - **`LastFlight`**: Keep the flight with the highest `flight.Sequence` number.
   - **`LastNFlights(n)`**: Keep the n flights with the highest sequence numbers, in sequence order.
   - **`AllFlights`**: Keep all flights, in sequence order.
   - **`BestNFlights(n, rankByMetric, targets, targetValues)`**:
     a. Rank candidates. If `rankByMetric` is set, sort flights descending by that metric's raw value. If null, sort by `InterpretedFlight.Score` descending.
     b. Keep the top n.
     c. If `targets != None`, assign targets — [ISSUE #3].
     d. For each selected flight with a target, clamp that flight's metric to the target value. Adjust the flight's score proportionally (only the metric-based terms are affected — this requires recomputing the rate term for that flight with the clamped metric).
   - **`ExactlyNInOrder(n, targets, targetValues)`**:
     a. Keep the first n flights in sequence order.
     b. Assign `targetValues[i]` to the i-th selected flight.
     c. Clamp each flight's metric to its target.

5. **Assemble raw score**: Sum the (possibly target-clamped) scores of selected flights.

6. **Apply `CapScope.PerTask` caps**: [ISSUE #1 — interaction with WI-3 TBD]
   - For each `RateTerm` in `task.Score` where `CapScope == PerTask` and `Cap` is set:
     - Sum the `TermContribution.MetricConsumed` for that term index across all selected flights.
     - If the sum exceeds the cap, compute the reduction: `reduction = (sum - cap) × term.Rate`.
     - Subtract the reduction from the total raw score.
   - The flight interpreter computes uncapped contributions for PerTask-scoped terms (it doesn't know the sum). The selector sums the raw metrics and corrects.

7. **Apply raw rounding**: If `task.RawScore` is set, apply `Rounding` to the raw score.

8. Return `TaskResult(Valid, SelectedFlights, roundedRawScore)`.

**Target assignment algorithm** (for `BestNFlights` with `Targets.AnyOrder`):
- [ISSUE #3]
- Tentative: The selected flights are already ranked descending (step 4b). `TargetValues` is ascending `[60, 120, 180, 240]`. Pair rank position `i` (0 = best) with `TargetValues[n-1-i]`. So best flight → 240, second-best → 180, etc. This produces F3K Task H's 569 per the rule's worked example.

**Dependencies**: WI-1, WI-2, WI-3 (FlightInterpreter, PredicateEvaluator).

**Acceptance criteria**:
- [ ] `LastFlight` selection: Entry with 3 flights → only the last one counts
- [ ] `LastNFlights(2)`: keeps last 2 flights
- [ ] `AllFlights`: all flights counted
- [ ] `BestNFlights(3)` without rankByMetric: ranks by score, keeps best 3
- [ ] F3K Task E (Poker): `BestNFlights(3)`, no rankBy → flights ranked by score (target credits), best 3 selected. 45 + 0 + 50 + 47 across 4 flights → best 3 = 142 per rule's example
- [ ] F3K Task H: `BestNFlights(4) rankBy flightTime`, targets AnyOrder [60,120,180,240]. 4 flights, longest gets 240, etc. → 569 per rule's example
- [ ] `ExactlyNInOrder(5)`: first 5 flights in order, assigned targets [60,90,120,150,180] in order
- [ ] `validWhen` failing → `TaskResult.NoResult` [exact semantics per issues #2, #6]
- [ ] `CapScope.PerTask`: F5K Task A with 4 flights and flat 150s each → total metric 600, capped at 599 → reduction of 1
- [ ] Annulled Entry → `TaskResult.NoResult`
- [ ] `RawScore` rounding applied (F5K: Truncate to 1 decimal)

---

### WI-5: Normalisation Engine (`normalise` + `add normalised terms` + `round normalised`)

**Agent instructions**: Normalise task results within a group, apply post-normalisation score terms, and round. If the task has no `Normalisation`, the raw score IS the result (pass-through).

**[ISSUE #5] — group annulment threshold.** The agent must confirm resolution of issue #5 (who checks `minValidResults` and what happens when it's not met) before finalising the `GroupResult` return semantics.

**Location**: `src/Soarscore.Domain/Scoring/NormalisationEngine.cs`

**Interface**:

```csharp
public static class NormalisationEngine
{
    /// Normalise a group's task results. If the task has no Normalisation,
    /// raw scores pass through unchanged. NoResult entries are excluded
    /// from winner finding.
    public static GroupResult Normalise(
        string groupRef,                        // which group
        ImmutableDictionary<string, TaskResult> taskResults,  // CompetitorRef → TaskResult
        ResolvedTask task,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings
    );
}
```

**Algorithm**:

1. **Count valid results**: `ValidCount` = number of entries where `TaskResult.State == Valid`.

2. **No normalisation?** If `task.Normalise` is null:
   - Each competitor's score is their `TaskResult.RawScore` (0 if NoResult).
   - `WinnerRef = null` (no normalised comparison).
   - Return `GroupResult`.

3. **Has normalisation**: Filter to valid (non-NoResult) entries.

4. **Find winner**:
   - `HigherIsBetter` → competitor with highest `RawScore`
   - `LowerIsBetter` → competitor with lowest `RawScore`
   - If no valid results → `WinnerRef = null`, all scores 0.

5. **Compute normalised scores**: For each valid competitor:
   - `HigherIsBetter`: `score = (WinnerScore × CompetitorRawScore) / WinnerRawScore`
   - `LowerIsBetter`: `score = (WinnerScore × WinnerRawScore) / CompetitorRawScore`
   - Precision: use `decimal` throughout for accurate division.

6. **Round normalised**: If `task.Normalise.Round` is set, apply it to each normalised score.

7. **Add normalised terms**: For each competitor with a valid result:
   - Use the selected flights' measurements from `TaskResult.Selection`.
   - For each `ScoreTerm` in `task.ScoreNormalised`, evaluate using the same term evaluation logic as `FlightInterpreter` (coordinate with WI-3 agent to expose `EvaluateTerm` as `internal static`).
   - Add the result to the normalised score.
   - These terms are NOT scaled by normalisation — they are added after.

8. **Round if needed**: Apply `task.Normalise.Round` after step 7 (the pipeline shows `round` after `add normalised terms`).

**Important**: `NoResult` entries are excluded from winner finding but still appear in the output with score 0. They do not drag down the winner's score.

**Dependencies**: WI-1, WI-2, WI-4 (`TaskResult`). Note: WI-5 needs term evaluation logic. Coordinate with WI-3 agent to expose `EvaluateTerm` as `internal static` and add `InternalsVisibleTo` for the test project.

**Acceptance criteria**:
- [ ] `HigherIsBetter winner 1000`: A=600, B=500 → A=1000, B=833 (1000×500/600)
- [ ] `LowerIsBetter winner 1000`: A=15s, B=20s → A=1000, B=750 (1000×15/20)
- [ ] NZ Class M: normalise flight time, THEN add landing bonus from `ScoreNormalised`. A=600s flight + 9m landing (10pt bonus), B=500s flight + 1m landing (50pt bonus) → A=1000+10=1010, B=833+50=883
- [ ] No normalisation (NZ N/P): raw scores pass through, `WinnerRef = null`
- [ ] `NoResult` competitor excluded from winner finding, doesn't affect divisor
- [ ] All competitors `NoResult` → `WinnerRef = null`, all scores 0
- [ ] `Rounding` on normalised score applied (F3K: HalfUp to 0.1)
- [ ] [ISSUE #5] Group annulment handled per resolved semantics

---

### WI-6: Penalty Engine

**Agent instructions**: Apply penalties at both the raw-score level (`ZeroFlight`, `ZeroRound`, `ZeroTask`) and the final-aggregate level (`DeductPoints`, `Disqualify`), with exclusion group semantics and accrual.

**Location**: `src/Soarscore.Domain/Scoring/PenaltyEngine.cs`

**Interface**:

```csharp
public static class PenaltyEngine
{
    /// Apply raw-score penalties (ZeroFlight, ZeroRound, ZeroTask).
    /// Called BEFORE normalisation. Modifies the TaskResult.
    public static TaskResult ApplyRawPenalties(
        TaskResult result,
        ImmutableArray<RecordedPenalty> penalties,    // from Entry / Competition
        ImmutableArray<PenaltyDefinition> definitions  // from AdoptedRules
    );

    /// Apply aggregate penalties (DeductPoints, Disqualify).
    /// Called AFTER drops, BEFORE ranking.
    public static PenaltyApplication ApplyAggregatePenalties(
        decimal score,
        ImmutableArray<RecordedPenalty> penalties,
        ImmutableArray<PenaltyDefinition> definitions
    );

    /// Compute which penalties survive exclusion-group suppression.
    /// Single-pass over recorded infractions. Returns the set of
    /// PenaltyDefinitions that should be applied.
    internal static ImmutableArray<PenaltyDefinition> ResolveExclusion(
        ImmutableArray<RecordedPenalty> penalties,
        ImmutableArray<PenaltyDefinition> definitions
    );
}
```

**Algorithm for raw penalties**:

1. Match each `RecordedPenalty` to its `PenaltyDefinition` by `InfractionType`.
2. For each matched definition, check its `Effects`:
   - `ZeroFlight`: The competitor's flight score becomes 0. For the scope `Flight` → zero that specific flight. `Entry` scope → zero all flights in the Entry. The penalty's scope (from the `Penalty` entity on the Entry/Competition aggregate) determines which flights are affected.
   - `ZeroRound`: The competitor's round score becomes 0 (all task-rounds in that round).
   - `ZeroTask`: The competitor's task result becomes 0 for that task.
3. Exclusion groups do NOT apply at the raw level — they only apply to `DeductPoints` effects (enforced by adoption check 16).

**Algorithm for aggregate penalties**:

1. Match each `RecordedPenalty` to its `PenaltyDefinition`.
2. Filter to effects that are `DeductPoints` or `Disqualify`.
3. Compute **accrued contribution** for each matched definition:
   - `OncePerAttempt`: contribution = `points` (regardless of occurrence count)
   - `PerOccurrence`: contribution = `points × occurrenceCount`
4. **Exclusion group suppression** (single-pass, NOT iterative):
   - For each exclusion group name, find the definition in that group with the largest accrued contribution.
   - Every other definition in that group is suppressed.
   - A definition in multiple groups is suppressed if ANY of its groups holds a larger contribution from another definition.
   - Surviving definitions are applied exactly once, regardless of how many groups they belong to.
   - **Critical**: Suppression is ONE PASS over recorded infractions. A suppressed penalty must not un-suppress a third. This guarantees evaluation order independence.
5. Sum deductions from surviving `DeductPoints` effects → `PenaltyApplication.Deduction`.
6. If any surviving effect is `Disqualify` → `PenaltyApplication.Disqualified = true`.

**Dependencies**: WI-1 (result types).

**Acceptance criteria**:
- [ ] `ZeroFlight` penalty zeroes a flight's contribution
- [ ] `ZeroRound` zeroes the whole round
- [ ] F3B.2.2 p (`nonConformingWinch`): one infraction with two effects — `ZeroFlight` AND `DeductPoints 1000`. Both apply at their respective stages.
- [ ] F3K.4.3 exclusion group: `safetyAreaObjectContact` (100) and `safetyAreaPersonContact` (300) in same group → only 300 applied
- [ ] F3F.1.10 pairwise exclusion (F28):
  - `safetyPlaneCrossing` in ["safetyMax"], `safetyAreaObjectContact` in ["contact"], `safetyAreaPersonContact` in ["contact", "safetyMax"]
  - crossing (100) + object contact (100) → both survive (different groups): 200 total
  - crossing (100) + person contact (1000) → crossing suppressed by safetyMax group, object suppressed by contact group: 1000 total
- [ ] `PerOccurrence` (F23): two crossings @ 100 each → contribution 200. Person contact's 1000 still supersedes.
- [ ] Single-pass suppression: penalty A in groups [X], penalty B in groups [X, Y], penalty C in groups [Y]. A=500, B=300, C=400. B suppressed by A in X. C survives (400 > 0 from suppressed B in Y). A survives. Total = 900.
- [ ] `Disqualify` effect → `PenaltyApplication.Disqualified = true`

---

### WI-7: Phase Aggregator + Drop Engine

**Agent instructions**: Aggregate task-round results into round scores, then phase scores, applying drop policies in order. Handle both `ByRound` and `ByTask` drop dimensions.

**[ISSUE #8] — ByTask drop algorithm.** The agent must confirm the `ByTask` drop algorithm (especially how per-task results are grouped across rounds) before implementing.

**Location**: `src/Soarscore.Domain/Scoring/PhaseAggregator.cs`

**Interface**:

```csharp
public static class PhaseAggregator
{
    /// Aggregate all task-round results for one competitor across a phase,
    /// applying drops.
    public static PhaseScores Aggregate(
        string competitorRef,
        PhaseDefinition phase,
        ImmutableArray<RoundData> rounds,  // ordered by round ordinal
        IReadOnlyDictionary<string, TaskRoundScore> allScores
            // all task-round scores for this competitor in this phase
    );
}
```

Where:
```csharp
public sealed record RoundData(
    int RoundOrdinal,
    ImmutableArray<TaskRoundData> TaskRounds
);

public sealed record TaskRoundData(
    int TaskOrdinal,
    string TaskCode,
    TaskRoundState State   // Complete or Annulled
);
```

**Algorithm**:

1. **Build round scores**: For each round, sum the `TaskRoundScore.Score` for all task-rounds in that round. This gives `(roundOrdinal, roundScore)` pairs. Annulled task-rounds contribute 0.

2. **Apply drops**: Evaluate `phase.Drops` in order:
   - For each `DropPolicy`, check both gates (if populated):
     - `ApplyWhenRoundsCompletedAtLeast`: number of completed (non-annulled) rounds >= this value
     - `ApplyWhenResultsAtLeast`: for `ByRound`, results in that round; for `ByTask`, results for that specific task across rounds. Both gates must hold (conjunctive — F18).
   - First policy whose gates ALL hold: apply it.
   - **`ByRound`**: Sort round scores ascending, drop the lowest `DropCount` round scores.
   - **`ByTask`**: [ISSUE #8] Group all task-round scores by task code. For each task code, sort scores ascending, drop the lowest `DropCount`. The phase aggregate is the sum of all remaining scores across all tasks.
   - No policy matches → no drop.
   - `Drops` is empty → no drop.

3. **Sum remaining scores** → the phase aggregate.

4. Return `PhaseScores` with `AllScores` (before drops) and `DroppedScores` (which were dropped).

**Drop ordering is significant** (F22). The `Drops` list is ordered, and the first matching policy wins. Adoption check 10 ensures gates are strictly descending, so a later policy with a lower gate doesn't accidentally match first.

**Dependencies**: WI-1, WI-2.

**Acceptance criteria**:
- [ ] No drops → all round scores summed
- [ ] `ByRound 1 whenRounds >= 4`: 5 rounds, scores [100,200,300,400,500] → drop 100, aggregate = 1400
- [ ] `ByRound 1 whenRounds >= 4`: 3 rounds → no drop (gate fails), aggregate = sum of 3
- [ ] F3F two-tier (F22): `ByRound 2 whenRounds >= 15`, `ByRound 1 whenRounds >= 4`. 15 rounds → first policy matches, drops 2. 5 rounds → second matches, drops 1. Verify the `>= 4` policy does NOT match when `>= 15` matches.
- [ ] F3B `ByTask 1 whenRounds >= 6 whenResults >= 6`: Task A scores [800,900,700,850,750,950] across 6 rounds → drop 700, sum rest. Verify both gates — if Task C has only 5 results, Task C's drop does NOT apply even though rounds >= 6.
- [ ] `ByTask` with mixed task codes: Task A in rounds 1,3,5, Task B in 2,4,6. Drop 1 per task → drop the lowest A and lowest B independently.
- [ ] Empty `Drops` list → no discard (9 of 16 phases in the corpus)
- [ ] Annulled task-rounds excluded from completed-round count for gate evaluation

---

### WI-8: Ranking Engine

**Agent instructions**: Produce final placings from per-competitor scores, handling `finalRanking` kinds and ties.

**Location**: `src/Soarscore.Domain/Scoring/RankingEngine.cs`

**Interface**:

```csharp
public static class RankingEngine
{
    /// Rank competitors by final scores.
    public static CompetitionResult Rank(
        ImmutableArray<FinalCompetitorScore> scores,
        FinalRankingKind finalRanking,
        PromotionRule? promotion   // for SplitByPromotion
    );
}
```

**Algorithm**:

1. **Remove disqualified**: Disqualified competitors get no placing (excluded from ranking).

2. **Sort**: By `Score` descending (higher is better — scores have already been normalised/inverted at the task level).

3. **Apply `finalRanking`**:
   - **`SinglePhase`** (or null, meaning one phase): Sort all competitors, assign placings.
   - **`LastPhaseReplaces`** (F3K.10): For promoted competitors (those in the fly-off), their final score is their fly-off phase aggregate. Non-promoted competitors keep their preliminary score. Rank all together. [The orchestrator passes pre-computed scores — this method ranks what it receives.]
   - **`SplitByPromotion`** (F3J.11): Promoted competitors ranked against each other on fly-off scores. Non-promoted ranked against each other on preliminary scores. Two separate ranking lists.

4. **Assign placings**: Standard competition placing with ties:
   - Equal scores → same placing.
   - Next placing skips: scores [1000, 900, 900, 800] → placings [1, 2, 2, 4].
   - Tie-breaking is unmodelled (per the docs). Equal scores share a placing, period.

**Dependencies**: WI-1.

**Acceptance criteria**:
- [ ] Simple ranking: [1000, 900, 800] → [1, 2, 3]
- [ ] Ties: [1000, 900, 900, 800] → [1, 2, 2, 4]
- [ ] `LastPhaseReplaces`: receives pre-computed final scores, ranks them
- [ ] `SplitByPromotion`: produces two ranking lists (qualifiers, non-qualifiers)
- [ ] Disqualified competitors excluded from placings
- [ ] Empty input → empty output

---

## Wave 3 — Integration

### WI-9: ScoringService Orchestrator

**Agent instructions**: Wire all pipeline stages together into the `ScoringService` domain service. Handle parameter resolution, reflight rules, penalty routing, and cross-aggregate coordination.

**[ISSUE #4] — Measurement amendment resolution.** The agent must confirm where amendment resolution lives (pre-processing in the orchestrator vs. inside FlightInterpreter) before implementing the measurement-reading pathway.

**Location**: `src/Soarscore.Domain/Scoring/ScoringService.cs`

**Interface** (matches `aggregate-roots.md` §Scoring):

```csharp
public class ScoringService
{
    // Granular methods — callers can compute only what they need.

    public InterpretedFlight InterpretFlight(
        Flight flight, ResolvedTask task, int flightSequence,
        IReadOnlyDictionary<string, MeasuredValue> resolvedMetrics
    );

    public TaskResult SelectFlights(
        Entry entry, ResolvedTask task,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings
    );

    public GroupResult NormaliseGroup(
        string groupRef,
        ImmutableDictionary<string, TaskResult> results,
        ResolvedTask task,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings
    );

    public PhaseScores Aggregate(
        string competitorRef,
        PhaseDefinition phase,
        ImmutableArray<RoundData> rounds,
        IReadOnlyDictionary<string, TaskRoundScore> allScores
    );

    public CompetitionResult Rank(
        ImmutableArray<FinalCompetitorScore> scores,
        FinalRankingKind finalRanking,
        PromotionRule? promotion
    );

    // Convenience: full pipeline for one task-round group.
    public GroupResult ScoreGroup(
        string groupRef,
        TaskDefinition task,
        PhaseDefinition phase,
        ClassDefinition classDef,
        ImmutableDictionary<string, Entry> entries,  // CompetitorRef → Entry
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings,
        ImmutableArray<RecordedPenalty> competitionPenalties
    );

    // Convenience: full competition result.
    public CompetitionResult ScoreCompetition(
        ClassDefinition classDef,
        ImmutableArray<PhaseDefinition> phases,  // from AdoptedRules
        ImmutableDictionary<string, ImmutableArray<Entry>> entriesByCompetitor,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings,
        ImmutableArray<RecordedPenalty> allPenalties
    );
}
```

**Responsibilities**:

1. **Resolve parameters**: Before invoking any pipeline stage, resolve the relevant `TaskDefinition` to a `ResolvedTask` via `ParameterResolver.ResolveTask(task, bindings)`.

2. **Wire the pipeline for `ScoreGroup`**:
   ```
   For each Entry in the group:
     For each Flight in the Entry:
       Resolve measurements (amendments) → ResolvedMeasurements [ISSUE #4]
       FlightInterpreter.Interpret(flight, resolvedTask, seq, resolvedMetrics)
         → InterpretedFlight
     FlightSelector.SelectAndScore(entry, resolvedTask) → TaskResult
     PenaltyEngine.ApplyRawPenalties(result, entryPenalties, definitions)
       → modified TaskResult
   Collect all TaskResults → NormalisationEngine.Normalise(groupRef, results, task)
     → GroupResult
   ```

3. **Wire the pipeline for `ScoreCompetition`**:
   ```
   For each phase, for each round, for each task-round, for each group:
     ScoreGroup(...) → GroupResult
   
   For each competitor:
     Collect TaskRoundScores across all rounds
     PhaseAggregator.Aggregate(competitor, phase, rounds, scores) → PhaseScores
   
   Apply finalRanking logic (LastPhaseReplaces / SplitByPromotion)
   
   Apply aggregate penalties:
     PenaltyEngine.ApplyAggregatePenalties(score, penalties, definitions)
       → PenaltyApplication
   
   RankingEngine.Rank(finalScores, finalRanking, promotion) → CompetitionResult
   ```

4. **Reflight handling**: When two `Entry`s point at the same group for the same competitor (one `Original`, one `Entitled`/`Filler`):
   - `Entitled` + `Replacement`: The entitled Entry counts (original discarded).
   - `Filler` + `BetterOf`: Both Entries scored, the better one counts.
   - `NotPermitted`: No reflight — only the original should exist.
   - `UndefinedRequiresRuling`: Both Entries scored, the result is flagged for CD decision — return a `RequiresRuling` state on the result.

5. **Penalty routing**: Separate penalties by scope:
   - `Flight`/`Entry` scope → applied to the specific Entry (raw penalties in `ScoreGroup`)
   - `TaskRound`/`Competition` scope → applied at the aggregate level (in `ScoreCompetition`)
   - Route `ZeroFlight`/`ZeroRound`/`ZeroTask` effects to `ApplyRawPenalties`
   - Route `DeductPoints`/`Disqualify` effects to `ApplyAggregatePenalties`
   - The stage is derived from the effect, not configured.

6. **Measurement resolution** [ISSUE #4]: Before any pipeline stage reads measurements, resolve amendments: the effective value of a `Measurement` is the most recent `Amendment.NewValue` (by timestamp), or the original `Measurement.Value` if no amendments.

**Dependencies**: WI-1 through WI-8.

**Acceptance criteria**:
- [ ] Full F3K round scored end-to-end from seed data: Task A, 5 competitors, one group → correct normalised scores
- [ ] Full F3F competition: one phase, one task, multiple rounds with drops → correct rankings
- [ ] NZ Class M: normalised flight score + landing bonus added after normalisation → correct scores
- [ ] F5K Task A with `CapScope.PerTask` → cap applied correctly
- [ ] Reflight: entitled competitor takes replacement; filler takes better-of
- [ ] Annulled Entry drops out at `select flights`
- [ ] Parameters resolved from bindings before pipeline stages run
- [ ] Raw penalties zero flights/rounds before normalisation
- [ ] Aggregate penalties deduct/disqualify after drops

---

## File Layout

```
src/Soarscore.Domain/
  Scoring/
    ScoringResultTypes.cs       # WI-1
    ParameterResolver.cs         # WI-2
    PredicateEvaluator.cs        # WI-3 (shared)
    FlightInterpreter.cs         # WI-3
    FlightSelector.cs            # WI-4
    NormalisationEngine.cs       # WI-5
    PenaltyEngine.cs             # WI-6
    PhaseAggregator.cs           # WI-7
    RankingEngine.cs             # WI-8
    ScoringService.cs            # WI-9
```

---

## Design Rules Every Agent Must Uphold

These are non-negotiable. If an agent finds one of these rules conflicts with implementation, **stop and escalate** — do not work around it.

1. **Immutability everywhere.** Every type is a `sealed record`. Every function is pure — same inputs → same outputs. No `DateTime.Now`, no `Random`, no static mutable state.

2. **No I/O in domain.** The scoring service receives data structures as parameters. It does not load aggregates from a store. That is the Application layer's job.

3. **Flight-local boundary.** `interpret flight` sees one `Flight`'s `Measurement`s + `flight.sequence` intrinsic. It never sees sibling flights, task-level state, or arithmetic between flights. This is a core invariant from `high-level-architecture.md`.

4. **`NoResult` ≠ zero.** A competitor with `NoResult` is excluded from normalisation's winner finding. A raw zero in a `LowerIsBetter` task would otherwise be the fastest time — do not conflate them.

5. **Exclusion group suppression is single-pass.** Compute all accrued contributions first, then suppress. Never iterate over survivors — the result must not depend on evaluation order.

6. **Penalty stage is derived from effect, not configured.** `ZeroFlight`/`ZeroRound`/`ZeroTask` apply at the raw-score end. `DeductPoints`/`Disqualify` apply at the aggregate end. There is no `appliedAt` attribute to read — the effect enum IS the routing decision.

7. **Caps clamp the metric, not the points.** `rate 2 pt/s cap 300s` means `score = 2 × min(metricValue, 300)`, not `score = min(2 × metricValue, 300)`. This matters whenever the rate is not 1.

8. **Bands are cumulative.** A `PiecewiseTerm` applies each band's rate to the portion of the measurement falling inside it. `0..600 @ 1 pt/s` then `600..any @ -1 pt/s` scores 599 at 601 s.

9. **Nothing is named for a discipline concept.** No `landingBonus`, no `launchHeight`, no `poker`. Everything is `LookupTerm`, `PiecewiseTerm`, `RateTerm` etc.

10. **Zero dependencies.** The `Soarscore.Domain` project has zero `PackageReference`s. Do not add any. System.Text.Json is in the shared framework and is already used for polymorphic discriminators — that's fine.

---

## Test Strategy

Per the architecture: **black-box sociable tests**. Each WI's tests should:

- Instantiate real types from `CompetitionClasses` (use seed data as test fixtures).
- Call the public method with those inputs.
- Assert the output value.
- Do NOT mock the scoring vocabulary types.
- Do NOT test that `FlightInterpreter` called `PredicateEvaluator` — test that the output score is correct.
- Reference worked examples from the notation doc and seed data comments:
  - F3B Task A at 601s with landing → 599
  - F3K Task E → 142s (`F3K.11.5`)
  - F3K Task H → 569s (`F3K.11.8`)
  - F5K Task A with 4×150s flights → capped at 599
  - NZ Class M normalised + landing → 1010 vs 883

---

## Open Issues

See `kanban/completed/scoring-service-issues.md` for the 8 unresolved design questions. Each WI that depends on an issue has a **[ISSUE #N]** marker. Agents must check that file before starting and confirm resolutions. Issues are resolved by the project lead and checked off.

The issues in priority order:
1. `CapScope.PerTask` interaction between WI-3 and WI-4 (CRITICAL — affects F5K scoring)
2. `validWhen` evaluation semantics (CRITICAL — affects WI-4, could produce wrong `NoResult`)
3. `BestNFlights` AnyOrder target pairing algorithm (CRITICAL — affects F3K Task H scoring)
4. Measurement amendment resolution — where does it live? (IMPORTANT)
5. `minValidResults` and group annulment — whose job? (IMPORTANT)
6. `validWhen` and flight selection ordering (IMPORTANT)
7. `ResolvedTask` type placement (MINOR)
8. `ByTask` drop dimension — exact algorithm (MINOR)
