# Tech debt

Residual technical debt identified or intentionally deferred while implementing a feature.
See CLAUDE.md house-keeping rule 5.

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
