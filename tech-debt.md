# Tech debt

Residual technical debt identified or intentionally deferred while implementing a feature.
See CLAUDE.md house-keeping rule 5.

- [ ] Duplicate `TaskRoundState` enums. Two public enums share the name with different
  members: `Soarscore.Domain.Competitions` (`Competition.cs:68` — `Drawn`, `InProgress`,
  `Complete`, `Annulled`) and `Soarscore.Domain.Scoring` (`PhaseAggregator.cs:34` —
  `Complete`, `Annulled`). They compile because the namespaces differ, and nothing
  converts between them today only because the scoring engine has no caller. The
  Entry-to-scoring adapter (gap 5) is the first code that must map one to the other, and
  the mapping is lossy in one direction — `Drawn` and `InProgress` have no scoring
  counterpart. Decide then whether the scoring enum collapses into the aggregate's or
  the adapter filters those two states out; recorded now so it is not discovered late.
  Identified while planning `docs/plans/capture-a-score-steel-thread-plan.md`.
- [ ] `Competition.OpenEntry`'s inline task walk vs. `TaskResolver`. WI-8
  (docs/plans/capture-a-score-steel-thread-plan.md) extracted the
  phase→round→task-round→task traversal into
  `Soarscore.Application.Entries.TaskResolver`, shared by `OpenFlightHandler`
  and `CaptureMeasurementHandler`. `Competition.OpenEntry` (WI-2, already
  landed) still walks the same path inline to derive the working time — it
  was not repointed at `TaskResolver`, deliberately, to avoid touching
  `Competition.cs` while other work may have been in-flight against it.
  Repointing it would remove the last duplicate copy of this traversal, per
  the plan's own note that "there is no third copy... worth writing".
- [ ] WI-13's scenario 3 (`tests/Soarscore.Acceptance.Tests/Features/CapturingAScore.feature`,
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
