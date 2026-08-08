# Gap analysis — what the codebase does not yet do

**Status:** Snapshot · **Date:** 2026-08-08 · **Commit:** `d273ed1` ("draw"), branch `master`

> **This is a point-in-time audit, not a live document.** Every claim below was
> verified by reading the tree at `d273ed1`. Line numbers drift on the next commit
> that touches a file. **Re-verify before acting on any of it** — treat a `file:line`
> here as a starting point for a grep, not as an address. Nothing in this file is a
> decision; the sequencing section is a recommendation the user has not chosen.
>
> Test suite at that commit: green — 306 tests
> (Domain 177, Application 125, Architecture 4; Infrastructure storage tests
> filtered out with `dotnet test --filter "Category!=Storage"`).

## Context

Five plans live in `docs/plans/`: `command-side`, `create-competition`,
`class-definition-adoption`, `register-competitor`, `phase-drawn`. All five are
implemented in code. Their *What this unlocks* sections were audited item by item, and
this document is what those sections promised but the tree does not yet contain.

One documentation artefact worth noting because it misleads a reader: only
`command-side-steel-thread-plan.md` carries `**Status:** Complete`. The other four still
say `**Status:** Proposed` despite having landed.

## Gap inventory

| # | Gap | Nature | Consequence |
|---|---|---|---|
| 1 | Entry aggregate has no write path | Largest gap; named by all five plan audits | **No way to capture a score.** |
| 2 | `entry_index` read model does not exist | Permitted by LADR-0001 §3, never built | No leaderboard query surface |
| 3 | Seven of eleven `CompetitionEvent` types are unreachable | Folds exist; no decide functions | Runtime trap, see below |
| 4 | `BindParameter` / `ParameterBound` has no command | Live functional hole | F5K, F5L, NZ Class M cannot be drawn |
| 5 | The scoring engine is orphaned | ~2,100 lines with no caller and a deleted plan doc | Untestable end-to-end, unshipped |
| 6 | The core architectural law has no guarding test | Convention only | Regression is invisible |
| 7 | Assorted smaller items | See §7 | — |

---

## 1 — Entry aggregate write path

`src/Soarscore.Domain/Entries/Entry.cs` (251 lines) is **folds only**: `Create`
(`:158`), the `Apply` overloads (`:171` onward) and the static dispatcher (`:236`). It
contains **zero `Result<>` decide functions**.

The events exist and round-trip:
`src/Soarscore.Domain/Entries/EntryEvents.cs:42-75` — `EntryOpened`, `FlightOpened`,
`MeasurementCaptured`, `MeasurementAmended`, `EntryAnnulled`, `PenaltyRecorded`. The
fold is well tested (`tests/Soarscore.Domain.Tests/EntryFoldTests.cs`,
`EntryModelBasedFoldTests.cs`, `EntryTests.cs`, and
`tests/Soarscore.Application.Tests/EntryEventJsonTests.cs`).

What is absent above the Domain: **everything**. There is no
`src/Soarscore.Application/Entries/` directory. Grepping
`src/Soarscore.Application`, `src/Soarscore.Infrastructure` and `src/Soarscore.Api`
for `Soarscore.Domain.Entries` or `EntryId` returns **zero hits** — no commands, no
handlers, no endpoints, no `MapEventType` for any Entry event.

Stated plainly: **there is currently no way to capture a score in this system.** The
draw is the last thing wired end to end.

Its blocking input has now arrived. `Group` carries a real allocation —
`Competition.cs:198` (`CompetitorRefs`), populated by the draw at `Competition.cs:644`.
The reason Entry was gated is gone.

## 2 — `entry_index` read model

LADR-0001 §3 provides for it (`docs/ladr/ladr-0001-event-store.md:64` describes the
document, `:72` describes the leaderboard query that resolves through it). Nothing
exists: zero hits for `entry_index`, `EntrySummary` or `IEntryQuery` anywhere under
`src/` or `tests/`. `MartenConfig.cs:69-81` registers exactly three Inline projections —
Person, ClassDefinition, Competition.

This gap is the twin of gap 1 and should be closed with it, not after it.

## 3 — Seven unreachable `CompetitionEvent` types

Folds exist and are tested (`Competition.cs:344-374`, dispatched at `:433-439`). The
only decide functions on `Competition` are `Decide` (`:457`), `RegisterCompetitor`
(`:498`), `WithdrawCompetitor` (`:521`) and `DrawPhase` (`:540`).

Unreachable: `ReflightGroupAppended`, `TaskRoundCompleted`, `TaskRoundAnnulled`,
`RulesAmended`, `ParameterBound`, `Finalised`, `PenaltyRecorded`.

**Runtime trap — read this before writing any command that appends one.**
`src/Soarscore.Infrastructure/MartenConfig.cs:49-52` registers exactly **four**
competition events (`competitionCreated`, `competitorRegistered`,
`competitorWithdrawn`, `phaseDrawn`). The comment at `MartenConfig.cs:41-46`
documents the other seven as deliberately unregistered. **Appending any of the seven
would fail at runtime.** Any thread touching these must add its own `MapEventType`
line, per LADR-0001 §4.8.

## 4 — `BindParameter` / `ParameterBound`

Not a future feature — a hole in shipped behaviour. No command exists, so no parameter
can ever be bound, and the draw rejects parameterised group sizes outright:
`Competition.cs:613` returns `drawPhase.parameterUnbound`.

**Consequence: F5K, F5L and NZ Class M (ALES 200) cannot be drawn at all today**,
despite all three sitting in the seed corpus under `tools/Soarscore.SeedData/`.

## 5 — The orphaned scoring engine

Ten files, 2,134 lines, in `src/Soarscore.Domain/Scoring/`:

| File | Lines |
|---|---|
| `FlightSelector.cs` | 386 |
| `ParameterResolver.cs` | 257 |
| `PhaseAggregator.cs` | 250 |
| `ScoringService.cs` | 249 |
| `PenaltyEngine.cs` | 231 |
| `FlightInterpreter.cs` | 225 |
| `ScoringResultTypes.cs` | 179 |
| `NormalisationEngine.cs` | 174 |
| `PredicateEvaluator.cs` | 101 |
| `RankingEngine.cs` | 82 |

Three problems, in order of how much they cost:

**Its plan document was deleted.** All ten files header-cite
`docs/plans/scoring-service-plan.md`. That file was added in `d1ea17d` and removed in
`38cb008` ("renamed folder to reflect true agg root"); it is absent from HEAD. It is
recoverable:

```
git show d1ea17d:docs/plans/scoring-service-plan.md
```

**It has no caller.** Zero references to `ScoringService` from
`src/Soarscore.Application` or `src/Soarscore.Api`.

**Three components have no tests.** `ScoringService`, `ParameterResolver` and
`PredicateEvaluator` have no test file. The other seven components do
(`FlightInterpreterTests`, `FlightSelectorTests` + `FlightSelectorPropertyTests`,
`NormalisationEngineTests` + property tests, `PenaltyEngineTests`,
`PhaseAggregatorTests` + property tests, `RankingEngineTests`).

**The nuance that makes this cheaper than it looks.** The `object` / `object?`
parameters annotated *TBD* — `ScoringService.cs:26,36,91,205` (and the untagged
`object entry` at `:231`), `FlightInterpreter.cs:30`, `FlightSelector.cs:34` — are
**completely unused in the method bodies**. The engine already operates on
pre-digested inputs (`resolvedMetrics`, `interpretedFlights`). So it is **not
mis-typed against a stale aggregate design**; it has vestigial parameters and no
caller. Rescuing it needs an adapter that turns an Entry stream into those
pre-digested inputs — not a redesign.

## 6 — The core architectural law is unguarded

CLAUDE.md states that "the core system must not know about any specific competition
class" and that this is "not a style preference". `tests/Soarscore.Architecture.Tests/`
contains four tests total (`LayerRuleTests.cs`, `RouteShapeTests.cs`) and **none of
them asserts the absence of class-name branching**. The law holds by convention only.

The draw path was checked and does honour it: no `F3B`/`F3J`/`F3K`/`F5J`/`F5K`/`F5L`
literals in the draw logic outside a rule-reference comment at `PhaseDraw.cs:18` and
one explanatory comment at `ClassDefinitionIngestion.cs:27`. The point is that nothing
would catch it if that changed.

## 7 — Smaller items

- **`RetireClassDefinition` has no command.** The event is mapped
  (`MartenConfig.cs:36`), the fold exists (`PublishedClassDefinition.cs:50`) and the
  projection handles it (`ClassDefinitionProjection.cs:55`) — but no command produces
  it. So `CreateCompetition`'s `createCompetition.classDefinitionRetired` branch
  (`CreateCompetition.cs:67-72`) is reachable only by tests hand-appending the event
  (`CompetitionEventStoreTests.cs:65`).
- **No `State` column on the `competitions` read model.**
  `create-competition-steel-thread-plan.md:53-57` predicted it would arrive with
  `PhaseDrawn`. `PhaseDrawn` landed; the column did not. `CompetitionSummary.cs:23-30`
  has no `State`, and `CompetitionProjection.cs:34` still returns `_ => current`.
- **`EvaluatorVersion` is a hard-coded literal** — `"1"` at `CreateCompetition.cs:36`,
  flagged as a deferred decision in that file's own header.
- **No competitor-count column and no by-name joined view** on `CompetitionSummary`
  (declared out of scope at `register-competitor-steel-thread-plan.md:63-67`).
- **`command-side-steel-thread-plan.md` WI-11 housekeeping was never done**: no
  `nuget-license` CI step in `.github/workflows/build-and-test.yml`, no
  `PublicAPI.Shipped.txt` baseline anywhere in the tree.
- **No automated end-to-end test.** Nothing spins up the API against PostgreSQL — no
  `WebApplicationFactory` or `HttpClient` usage in `tests/`. `phase-drawn` WI-8 and
  `create-competition` WI-7 were manual procedures with no recorded evidence of
  execution.
- **The `fai-rules` skill cannot check a live definition.**
  `.claude/skills/fai-rules/references/compliance-check.md` has zero mentions of the
  API; it routes only to `docs/rules/`. Compliance is checkable against authored text,
  never against what was actually POSTed.

---

## Deliberately deferred — decisions, not gaps

Recorded here so nobody "fixes" them by mistake:

- **Redraw / draw acceptance.** Acceptance criteria are already drafted at
  `phase-drawn-steel-thread-plan.md:110-121` (`Draw.Status` vocabulary,
  `AcceptDraw`/`RejectDraw`, moving `ValidateFieldNotFrozen` off `Phases.IsEmpty`).
- **Flyoff-phase draws.** The current draw's field is unconditionally "every
  non-withdrawn Competitor"; flyoff field selection is a different algorithm.
- **Multi-task rounds (F3B) and catalogue-choice rounds (F3K/F5K).** Structurally
  rejected at `Competition.cs:573-580` with `drawPhase.unsupportedRoundComposition`.
- **The `.class` notation parser** and **class-definition drift detection** — both
  settled out of scope.

## Recommended sequencing — a proposal, not a decision

1. **`BindParameter` slice first** (~1 day). It is small, it is the same shape already
   executed four times, and it fixes three seed-corpus classes that are broken *now*
   (gap 4). Cheapest real value in the repo.
2. **Entry write path plus `entry_index`** (gaps 1 and 2). This is the critical path:
   the only option that advances the system's actual purpose, and the thing that makes
   the scoring engine testable at all. **Write a plan document before coding** — every
   thread that landed cleanly had one, and Entry's decide surface is the least
   specified thing in the repo.
3. **De-orphan the scoring engine** (gap 5): drop the dead parameters, restore the plan
   doc from `d1ea17d`, backfill the three missing test files, then build the
   Entry-to-scoring adapter and a `ScoreTaskRound` query.

Two things this ordering deliberately rejects. "Close the remaining Competition events"
(gap 3) as a goal in itself is motion without direction — close each one when a command
needs it. And "de-orphan scoring" *first* cannot be validated, because without Entry
there are no real measurements to feed it.

## Standing practice for whoever picks this up

Property-based testing with CsCheck is routine on this repo, not optional garnish:
consider, implement and verify property tests alongside unit tests for any new feature.
Every thread that has landed shipped with them.
