# Tech debt

Residual technical debt identified or intentionally deferred while implementing a feature.
See CLAUDE.md house-keeping rule 5.

- [x] Duplicate `TaskRoundState` enums. Two public enums share the name with different
  members: `Soarscore.Domain.Competitions` (`Competition.cs:105` — `Drawn`, `InProgress`,
  `Complete`, `Annulled`) and `Soarscore.Domain.Scoring` (`PhaseAggregator.cs:34` —
  `Complete`, `Annulled`). They compile because the namespaces differ. Identified while
  planning `kanban/completed/capture-a-score-steel-thread-plan.md`; discharged by
  `kanban/completed/scoring-steel-thread-plan.md` WI-3, exactly as predicted — the mapping is
  lossy in one direction and the adapter filters, rather than collapsing the enums. The
  conversion lives in `ScoringService.ScoreCompetition`
  (`src/Soarscore.Domain/Scoring/ScoringService.cs`), one `Competitions.TaskRoundState`
  → `Scoring.TaskRoundState` mapping per task-round, per finding 5's rule: a task-round
  enters the walk only when at least one Entry exists for it (an unflown task-round is
  omitted from `RoundData` entirely, never mapped to `Complete`); among task-rounds with
  entries, the Competition side's `Annulled` maps to `Scoring.TaskRoundState.Annulled`
  and everything else (`Drawn`, `InProgress`, `Complete`) maps to
  `Scoring.TaskRoundState.Complete` — `Drawn`/`InProgress` are reachable inputs only
  because nothing yet transitions a task-round to `Complete` on the Competition side
  (`TaskRoundCompleted` has no decide function); the entries-present test is what
  the leaderboard means "provisional, over rounds flown so far" instead of that.
  **Superseded 2026-08-18 by `kanban/completed/task-round-lifecycle.md` WI-4**, which
  gave `TaskRoundCompleted` a decide function and so made `Complete` a state the
  mapping can actually receive. The catch-all `else` is gone: the switch is now total
  over all four write-side states. `Drawn`/`InProgress` still map to
  `Scoring.TaskRoundState.Complete`, but as the provisional leaderboard's deliberate
  choice — score what has been captured so far — rather than as an artefact of an
  unreachable event. The entries-present filter is unchanged and still carries the
  "provisional, over rounds flown so far" meaning.
- [ ] `Competition.OpenEntry`'s inline task walk vs. `TaskResolver`. WI-8
  (kanban/completed/capture-a-score-steel-thread-plan.md) extracted the
  phase→round→task-round→task traversal into
  `Soarscore.Application.Entries.TaskResolver`, shared by `OpenFlightHandler`
  and `CaptureMeasurementHandler`. `Competition.OpenEntry` (WI-2, already
  landed) still walks the same path inline to derive the working time — it
  was not repointed at `TaskResolver`, deliberately, to avoid touching
  `Competition.cs` while other work may have been in-flight against it.
  Repointing it would remove the last duplicate copy of this traversal, per
  the plan's own note that "there is no third copy... worth writing".
- [x] WI-13's scenario 3 (`tests/Soarscore.Acceptance.Tests/Features/CapturingAScore.feature`,
  "A launch before the working time is recorded, not refused") publishes a
  hand-authored, single-task F3K-shaped `ClassDefinition`
  (`Support/AcceptanceF3KShape.cs`) instead of the real corpus F3K
  (`Corpus.All`, `10-f3k`, `SeedF3K.Definition`). The real F3K's own phases
  are `CompositionKind.ChooseFromCatalogue` with more than one task, which
  `Competition.DrawPhase` explicitly rejects
  (`drawPhase.unsupportedRoundComposition`) — catalogue-choice rounds are
  `capture-a-score-steel-thread-plan.md`'s own documented, out-of-scope gap
  ("Still gated, and not by this thread: Catalogue-choice rounds... still the
  only thing between F3K/F5K and a draw"), so the real corpus definition
  cannot reach a drawn phase at all today. The stand-in reuses F3K's real
  task-D numbers (10-minute fixed working time, 2 launches, flightTime
  truncated to 0.1 s per F3K.7) restructured as the sole task on a
  `FixedSequence` phase. Once catalogue-choice draws land, retarget this
  scenario at the real corpus F3K and delete `AcceptanceF3KShape.cs`.
  Discharged by `kanban/completed/catalogue-choice-draws-plan.md` WI-7: the
  scenario now draws the real corpus F3K's preliminary phase naming task D
  (and three other distinct tasks) for its four rounds via a Gherkin table,
  and `AcceptanceF3KShape.cs` is deleted.
- [x] Round-scoped `ParameterBinding`'s freeze rule is an approximation, not the
  rule the plan actually wanted. `kanban/completed/per-round-parameter-bindings-plan.md`
  decided (2026-08-16, user-confirmed) to freeze a round-scoped bind once the
  target round's `TaskRound.State` leaves `Drawn` — the only signal available
  inside `Competition`, which holds no live flight data
  (`src/Soarscore.Domain/Competitions/Competition.cs`'s own design note). The
  intent was "not after that round's first flight"; nothing today transitions a
  `TaskRound` to `InProgress` on the first `Entry` opened against it (the same
  gap the `TaskRoundState` tech-debt item above already names), so a
  round-scoped rebind is still silently accepted for a round mid-flight, right
  up until `TaskRoundCompleted`/`TaskRoundAnnulled` lands. Closing this
  properly needs either a domain event marking a task-round `InProgress` on
  first `Entry`, or a deliberate decision that the approximation is good
  enough for a club-scale event.
  **Discharged 2026-08-18 by `kanban/completed/task-round-lifecycle.md` WI-9**, and by
  neither of those two routes. A `TaskRoundInProgress` event was considered and
  rejected (that plan's decision 1: it would make `OpenEntryHandler` append to two
  streams on the highest-volume write path, for a signal already derivable, that
  nothing would read). Instead `Competition.BindParameter` takes a trailing
  `bool roundHasEntries`, which `BindParameterHandler` resolves from `IEntryQuery` —
  and only for a genuinely round-scoped bind, so the unscoped path takes no extra
  query. The freeze is now `State != Drawn || roundHasEntries`, with a second defect
  code `competition.parameter.roundInProgress` for the new half. The aggregate
  boundary holds: `Competition` receives an already-resolved fact, exactly as it
  receives an already-resolved `AdoptedRules`, and still holds no live flight data.
- [ ] `FisherEventStore.ReadAllAsync` reads the whole log to return one page.
  `kanban/completed/multi-backend-deployment.md` WI-2. Fisher has no
  `QueryAllRawEvents()` — the LINQ-over-the-event-log surface Marten's
  implementation orders and pages with — so the SQLite adapter filters in the
  database (`QueryEventsAsync(e => e.Sequence >= fromPosition)`) and then sorts
  and takes `batchSize` in memory. Acceptable today because the method has zero
  production callers and exists for LADR-0001 §4.10's replay path, which walks
  the whole log anyway, against a local file at club scale. If a caller ever
  pages a large log incrementally, this needs Fisher's `IAdvancedSql` and a
  hand-written `ORDER BY`/`LIMIT`. The Marten implementation is unaffected.
- [ ] `FisherConfig.ConfigureDocumentStore` blocks on
  `ApplyAllConfiguredChangesToDatabaseAsync()`. `kanban/completed/multi-backend-deployment.md`
  WI-1. Fisher does not build its schema lazily on first use the way Marten
  does, so a fresh database fails the first append with "no such table:
  fi_streams" — found by the WI-6 suite. Applying the schema synchronously
  while the store is built puts the failure at startup instead of in front of a
  user, but `.GetAwaiter().GetResult()` in a composition root is not something
  to leave unexamined. If `AddSoarscoreInfrastructure` ever grows an async
  counterpart (an `IHostedService` initialiser, say), move it there.
- [ ] The two-group normalisation scenario's counterfactual check assumes two
  groups. `kanban/completed/multi-group-normalisation-coverage.md`. The step
  `nobody is normalised against the best flight time in the other group`
  (ScoringACompetitionSteps.cs) finds "the slower group" by comparing its best
  time against the round's best, which identifies exactly one group. With three
  or more groups it would check only that one and silently skip the rest.
  Correct for the two-group scenario as written; generalise if a scenario ever
  draws three groups.
- [ ] Solution-level parallel test runs can flake in Marten's schema migration.
  Seen once while implementing `kanban/completed/task-round-lifecycle.md` WI-10 and
  not reproduced since: a solution-wide `dotnet test` failed all fifteen acceptance
  scenarios with a `NullReferenceException` from
  `Marten.ValueTypeMemberSource.TryResolve` → `ComputedIndex.buildColumns`, raised
  out of `MartenPersonSummaryProjection.LoadCurrentAsync` during schema migration on
  the very first `/publish-class-definition`. Unrelated to task-round work — it
  fires before any lifecycle event exists, in the People projection's computed
  index — and every targeted re-run of the same suite passed, on both stores. The
  likely cause is that a solution-level run starts five test projects in parallel,
  each racing its own Testcontainer and its own first-touch schema migration. Left
  unfixed because nothing was diagnosed, only observed; recorded so the next person
  to see red on CI checks here before chasing their own change.
- [ ] Effective-knob records are non-uniform across the five NZ fixtures.
  `f5j-christchurch-2019` and `f5j-hawkes-bay-trials` carry structured
  `configProvenance` blocks ({stored, effective, basis} per knob); the other
  three (`f3k-southern-fling`, `f5j-nz-south-island`, `f3k-june-2020`) record
  their behaviourally-derived effective-grid reasoning in provenance notes only
  (GroupScoreOption / GroupScoreDecimals / RoundOrTruncate are Jet-null DB-wide
  on the NZ master, so stored config cannot prove scoring-time behaviour).
  Unify to the structured shape if any consumer ever needs machine-readable
  effective knobs corpus-wide.
- [x] `FlightInterpreter.EvaluatePiecewise` scores below-origin bonus bands as
  deductions. The walk integrates each band's rate over the UNSIGNED width of its
  overlap with `[0, metric − origin]`, so a bonus on the below-origin side needs a
  POSITIVE rate under the current engine. `SeedF5K.cs`'s `LaunchBands` therefore
  mis-scores the FAI F5K launch bonus (`5.5.10.4`: "+0.5 points per meter bonus"
  below the NLH) as a −0.5/m deduction: `Below(0, -0.5m)` at 10 m below the NLH
  yields −5, and no test covers a below-origin launch (`FlightInterpreterTests`
  exercises +15 m and exactly-at-NLH only). Found 2026-09-04 during the NZ NDC
  seed work (`kanban/completed/nz-ndc-seed-classes.md`), which encodes
  `SeedF5kNdc`'s symmetric NZ.3.16.29 bands with positive below-origin rates —
  correct under the evaluator as it is — and locks them in
  `NzNdcSeedArithmeticTests`. Two fixes are possible: signed-width integration in
  the engine (then `SeedF5K` is correct as written and `SeedF5kNdc`'s below bands
  must flip sign) or flipping `SeedF5K`'s rate sign. Either way, write the failing
  test first; the NZ arithmetic tests are the tripwire that catches whichever
  convention moves.
  **Discharged 2026-09-04 by
  `kanban/completed/signed-width-piecewise-integration.md`**, by the signed-width
  route. The drill found the notation doc had documented signed semantics all
  along ("a negative rate over a negative portion is what makes a low launch a
  bonus", `docs/competition-class-notation.md`) — the engine never implemented
  it. `EvaluatePiecewise` now multiplies the accumulated rate × width by the
  walk direction, `SeedF5K` is correct as written (no data change), and
  `SeedF5kNdc`'s below bands flipped to negative rates with the NZ.3.16.29
  table passing unchanged — the tripwire fired on exactly the bonus rows
  between the engine fix and the seed flip, as designed. The failing-test-first
  prescription was followed: the two new `FlightInterpreterTests` below-origin
  cases went red before the engine change.
