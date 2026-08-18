# Story — Smaller items

**Status:** Backlog — a holding list, not a thread · **Raised:** 2026-08-16

Items too small to be a story of their own. Take one opportunistically when a thread is
already touching that file; none of them justifies a trip on its own. Drained from
`gap.md` §7 (deleted 2026-08-16) and re-verified against the tree on that date — check
before acting, this list is not self-updating.

- [ ] **`RetireClassDefinition` has no command.** The event is mapped in `MartenConfig`,
  the fold exists on `PublishedClassDefinition`, and `ClassDefinitionProjection` handles
  it — but nothing produces it. So `CreateCompetition`'s
  `createCompetition.classDefinitionRetired` branch is reachable only by tests
  hand-appending the event (`CompetitionEventStoreTests.cs`).
- [x] **No `State` column on the `competitions` read model.**
  `create-competition-steel-thread-plan.md` predicted it would arrive with `PhaseDrawn`.
  `PhaseDrawn` landed; the column did not — `CompetitionSummary.cs` still carried the
  "deliberately excludes a State column" note and `CompetitionProjection` still returned
  `_ => current`. Taken opportunistically by `kanban/completed/task-round-lifecycle.md`
  WI-8, once `Finalised` gave the column a third value to hold: `CompetitionCreated` →
  `"created"`, `PhaseDrawn` → `"drawn"`, competition-scope `Finalised` → `"finalised"`.
  Phase-scope `Finalised` deliberately does not move it, and the `_ => current` default
  arm stays for the reason its doc comment gives.
- [ ] **`EvaluatorVersion` is a hard-coded literal** — `"1"` at
  `CreateCompetition.cs:36`, flagged as a deferred decision in that file's own header.
- [ ] **No competitor-count column and no by-name joined view** on `CompetitionSummary`,
  declared out of scope by `register-competitor-steel-thread-plan.md`.
- [ ] **`command-side-steel-thread-plan.md` WI-11 housekeeping was never done** — no
  `nuget-license` step in `.github/workflows/build-and-test.yml`, and no
  `PublicAPI.Shipped.txt` baseline anywhere in the tree.
- [ ] **The `fai-rules` skill cannot check a live definition.**
  `.claude/skills/fai-rules/references/compliance-check.md` never mentions the API; it
  routes only to `docs/rules/`. Compliance is checkable against authored text, never
  against what was actually POSTed.
- [x] **Stale sanity-floor comment** in
  `tests/Soarscore.Architecture.Tests/HandlerRegistrationTests.cs` — said "ten commands
  and four queries" against thirteen and seven. Corrected by
  `kanban/completed/task-round-lifecycle.md` WI-6, which was already editing the routing
  surface: seventeen commands and nine queries, and the floor raised from 10 to 17 so it
  keeps catching a reflection technique that has silently stopped matching.

## Unclaimed

`RulesAmended` is mapped, folded and unreachable, and is the one such event no backlog
story covers — the other unreachable events belong to `task-round-lifecycle`,
`reflight-groups` or `second-entry-thread`. Amending a rulebook mid-competition is a
real CD action with retroactive scoring consequences (see
`kanban/deferred-decisions.md`'s `Parameter.DefaultValue` note, which turns on exactly
that intent), so it needs a story of its own before it needs code — not an item on this
list.
