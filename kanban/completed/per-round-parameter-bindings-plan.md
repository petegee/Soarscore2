# Plan — Per-round parameter bindings

**Status:** Completed · **Date:** 2026-08-16

## What

`ParameterBinding` grows an optional round (and, once flyoff draws land, phase)
scope, so a CD can override a `ParameterBindingPoint.PerRound` parameter for one
specific round rather than only setting it once for the whole competition.

## Why it matters

The six `ParameterBindingPoint.PerRound` parameters in the corpus — all F3K's,
`workingTime.A/B/E/L` and `maxFlight.B/L` — all carry declared defaults, so this
is an **override** capability, not a blocker: F3K is fully drawable, floppable
and scorable without it (see `kanban/completed/catalogue-choice-draws-plan.md`).
It buys the CD the ability to, for example, drop task E's working time to 900 s
for one round.

This was deferred *into* `catalogue-choice-draws-plan.md` by
`bind-parameter-steel-thread-plan.md` finding 1, on the grounds that "binding
the working time for round 3 is meaningless until round 3 has a task." That
precondition is now discharged — rounds have tasks — but
`catalogue-choice-draws-plan.md` (2026-08-16) decided this stays a **separate**
follow-on thread rather than landing alongside the draw change, because (1) it
unblocks nothing today, and (2) it is a different change in a different place:
`ParameterBinding` grows a round scope, and `BindParameter`, `ParameterResolver`,
`TaskResolver` and `Competition.OpenEntry` all grow round context — landing it
alongside a draw change would put two unrelated diffs in one review.

## Shape, as far as the prior thread's design settled it

Recorded in `kanban/completed/catalogue-choice-draws-plan.md`, Appendix A, so it
is not re-derived. Summary:

- `ParameterBinding` grows an optional round scope. Since a `PerRound`
  parameter is named per *task* (`workingTime.A`) and each round has exactly
  one task, a round ordinal is sufficient scope; add a phase ordinal at the
  same time (cheaper than adding one twice once flyoff draws land).
- Resolution order: round-scoped binding → unscoped binding → declared default
  → throw. `ParameterResolver` takes the round as context; `TaskResolver` and
  `Competition.OpenEntry` already know the round ordinal and pass it through.
- `Competition.BindParameter` validates that a round-scoped binding names a
  round that exists and whose task actually consumes that parameter — only
  possible now that rounds have tasks.
- **Open question, not settled by the prior thread:** the freeze rule. A
  `PerRound` binding is legitimately made after the draw (unlike
  `CompetitionSetup`) but presumably not after that round's first flight.
  `ValidateParameterNotFrozen` currently keys on `BoundAt` alone.

## Before starting

- Read `kanban/completed/catalogue-choice-draws-plan.md` Appendix A in full. Done.
- Cross-reference `kanban/deferred-decisions.md`'s "Per-round parameter
  bindings" entry. Done.
- Check whether any other class in the corpus has grown `PerRound` parameters
  since this stub was written — today it is F3K only. Confirmed unchanged:
  `grep` across `tools/Soarscore.SeedData/Seed*.cs` for `ParameterBindingPoint.PerRound`
  still finds only `workingTime.A/B/E/L` and `maxFlight.B/L` in `SeedF3K.cs`.

## The freeze rule — decided

Asked the user directly, since the prior thread left this open and the two live
options have materially different validation logic. **Decided: freeze once the
target round's `TaskRound.State` leaves `Drawn`** (i.e. `Complete` or `Annulled`) —
the only signal available inside `Competition`, which holds no live flight data
(this file's own design note on the aggregate). This approximates "not after that
round's first flight"; the gap — a rebind is still accepted mid-round, after a
flight has opened but before the round is marked `Complete` — is real and tracked
in `kanban/tech-debt.md`, not silently treated as correct. The rejected
alternative (freezing on the round's first `Entry`) would need `Competition` to
query the `Entry` aggregate, contradicting its documented boundary
(`docs/aggregate-roots.md` §3 / this file's own "holds no live flight data" note).

## Work items — as built

`ParameterBinding` (`src/Soarscore.Domain/Competitions/Competition.cs`) grows
`PhaseOrdinal`/`RoundOrdinal` (`int?`, both-or-neither, 0-based/1-based matching
`Phase.Ordinal`/`Round.Ordinal` — **not** `TaskRound.Ordinal`, which indexes the
task within the round). `Competition.BindParameter` gains two optional trailing
parameters and a new validator, `ValidateRoundScope`, appended to the existing
defect chain:

| Code | Condition |
|---|---|
| `competition.parameter.roundScopeIncomplete` | exactly one of phase/round given |
| `competition.parameter.roundScopeNotPermitted` | round-scoped, but the parameter's `BoundAt` isn't `PerRound` |
| `competition.parameter.roundNotFound` | the named phase/round hasn't been drawn |
| `competition.parameter.notConsumedByTask` | the round's task doesn't reference this parameter anywhere |
| `competition.parameter.roundFrozen` | the round's task-round has left `Drawn` |

`ParameterResolver` gains `TaskReferencesParameter(TaskDefinition, string)` — walks
every ParameterRef slot a `TaskDefinition` can carry (`Timing.WorkingTime/
PreparationTime/MaxLaunches`, `Group.MinPerGroup`, a task-level `Reflight`
override, and both `Score`/`ScoreNormalised` term trees) — this is what makes
`notConsumedByTask` checkable.

`ScoringService.FlattenParameterBindings` grows two optional `(phaseOrdinal,
roundOrdinal)` arguments implementing Appendix A's resolution order: a binding
scoped to exactly the queried round wins; failing that, the last unscoped
binding; failing that, the parameter is absent (falls to the declared default,
or throws). Every caller that already knows its round now passes it through:
`Competition.OpenEntry`, `Application.Entries.TaskResolver`,
`Application.Queries.Scoring.ScoreTaskRound`, and `ScoringService.ScoreCompetition`
(moved from one whole-competition flatten to one per round, keyed on
`(phase.Ordinal, round.Ordinal)` — **not** `taskRound.Ordinal`). `Competition.DrawPhase`
calls the same helper with no round context, since the rounds it is about to
create cannot yet have a round-scoped binding naming them. This also deduplicated
two pre-existing inline copies of the flatten (`DrawPhase`, `OpenEntry`) onto the
one already lifted to `ScoringService` — they had drifted from the comment
claiming they were already deduplicated.

`Application.Commands.Competitions.BindParameter` (command + handler) grows the
same two optional properties and threads them straight through.

New test coverage for the round-scope behaviour itself: `BindParameterDecideTests.cs`
(one case per new code above, plus a success case and a resolution-order case
asserting a round-scoped bind wins for its own round and leaves every other
round's resolution untouched), `BindParameterPropertyTests.cs` property 6 (named
invariant: for any sequence of unscoped/round-scoped bindings of one parameter
and any queried round, `FlattenParameterBindings` resolves the last binding
scoped to exactly that round if one exists, else the last unscoped binding, else
the parameter is absent — driven directly against hand-built binding sequences,
independent of any one class fixture), `BindParameterHandlerTests.cs` (the
handler threads the two new properties through unchanged, both failure and
success), `CompetitionEventJsonTests.cs` (a round-scoped `ParameterBound`
round-trips byte-for-byte), and `BindParameterEventStoreTests.cs` (a round-scoped
binding survives a real Postgres append/read round-trip).

**Verified:** full solution build clean; full test suite — `Domain`,
`Application`, `Infrastructure` incl. `Category=Storage`, `Architecture`,
`Acceptance` — **495 passed, 0 failed, 0 skipped** (482 pre-existing + 13 new),
zero regressions.
