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
