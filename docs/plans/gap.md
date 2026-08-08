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

> **Update — 2026-08-08, later the same day.** `bind-parameter-steel-thread-plan.md`
> (WI-1 through WI-8) has been implemented and verified — see the new "Update" section
> below, right after Context, for what changed, what is still uncommitted, and two
> issues the work surfaced that this snapshot did not anticipate. The gap table and
> the per-gap sections below it are left as originally written (they are the
> `d273ed1` snapshot); do not treat gap 4's row or §4's prose as current without
> reading the update.

## Context

Five plans live in `docs/plans/`: `command-side`, `create-competition`,
`class-definition-adoption`, `register-competitor`, `phase-drawn`. All five are
implemented in code. Their *What this unlocks* sections were audited item by item, and
this document is what those sections promised but the tree does not yet contain.

One documentation artefact worth noting because it misleads a reader: only
`command-side-steel-thread-plan.md` carries `**Status:** Complete`. The other four still
say `**Status:** Proposed` despite having landed. **`bind-parameter-steel-thread-plan.md`
now joins that list** — it carries `**Status:** Proposed` in its own header despite the
work below being implemented and test-verified.

## Update — 2026-08-08: `bind-parameter-steel-thread-plan.md` implemented

All eight work items landed: `Competition.BindParameter` (decide function),
`ParameterResolver`'s default-value fallback, the `BindParameter` command/handler,
Marten wiring, the `/bind-parameter` endpoint, CsCheck property tests, Postgres
store-backed tests, and a real executed end-to-end run captured at
`docs/verification/bind-parameter-e2e.http`. Full solution build is clean; 339
non-Storage tests pass (Domain 199, Application 136, Architecture 4) plus 22
Storage-tagged tests against a real Postgres — up from the 306/22 baseline this
snapshot recorded.

**Not yet committed.** Every change above is uncommitted in the working tree (`git
status` shows it all as modified/untracked against `master`). Gap 4 and gap 3 below
should not be treated as closed in the repository's actual history until a commit
lands.

**Two things this work surfaced that were not anticipated when gap 4 was written:**

1. **F5K is still not drawable in practice, for a different reason than the one this
   thread fixed.** `BindParameter` makes F5K's `minPerGroup` parameter bindable, but
   F5K's real seed definition (`tools/Soarscore.SeedData/SeedF5K.cs`) uses
   `ChooseFromCatalogue` composition on both phases, which `Competition.DrawPhase`
   rejects with `drawPhase.unsupportedRoundComposition` *before* it ever reaches the
   parameter-resolution check — confirmed both by a live HTTP run (F5K leg of
   `docs/verification/bind-parameter-e2e.http`) and by `BindParameterEventStoreTests.cs`
   needing to fall back to NZ Class M ALES 200 for its Postgres payoff test. This is
   the pre-existing, already-deferred "Catalogue-choice rounds" gap (see "Deliberately
   deferred" below) — not a defect in the bind-parameter thread — but it means gap 4's
   original consequence line ("F5K, F5L and NZ Class M cannot be drawn at all today")
   is only fully resolved for **F5L and NZ Class M**. F5K needs the catalogue-choice
   thread on top of this one before it can actually be drawn end to end, despite its
   parameter now being bindable.
2. **A DI-registration gap with no automated guard.** While building WI-6 (the
   `/bind-parameter` endpoint), a sub-agent added the route
   (`app.MapCommand<BindParameter, CompetitionId>(...)` in
   `src/Soarscore.Api/Commands/Commands.cs`) but did not add the matching
   `builder.Services.AddScoped<ICommandHandler<BindParameter, CompetitionId>,
   BindParameterHandler>()` line in `src/Soarscore.Api/Composition.cs`. This compiles
   cleanly and `RouteShapeTests` (gap 6's architecture-test suite) passes unchanged,
   because that test only reflects over route shape (path/verb), not DI resolvability —
   the gap would only have surfaced as a 500 at first real request. Caught by manual
   review, not by any test, and fixed before this thread's WI-8 e2e run. **No test in
   the repo currently asserts that every `MapCommand`/`MapQuery` registration has a
   corresponding DI registration** — worth adding as a cheap addition to
   `tests/Soarscore.Architecture.Tests` (a reflection test resolving each mapped
   command/query type's handler interface from the built `WebApplication`'s
   `IServiceProvider` would catch this class of mistake at build time, the same
   protection `RouteShapeTests` already gives route shape). Not tracked elsewhere in
   this document; added here as a new, small item for whoever next touches
   `Soarscore.Architecture.Tests`.

**Consequence for the gap table below:** row 4 (`BindParameter`) is resolved subject to
the F5K caveat above; row 3 (unreachable `CompetitionEvent` types) drops from seven to
six — `ParameterBound` is now reachable, leaving `ReflightGroupAppended`,
`TaskRoundCompleted`, `TaskRoundAnnulled`, `RulesAmended`, `Finalised`,
`PenaltyRecorded`. Rows 1, 2, 5 and 6 are unaffected by this thread.

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
despite all three sitting in the seed corpus under `tools/Soarscore.SeedData/`. They are
exactly the three definitions whose `minPerGroup` resolves to a parameter with no
declared default; the other eight use a literal.

> **Planned.** `bind-parameter-steel-thread-plan.md` (2026-08-08) covers this gap. It also
> resolves two model findings turned up while designing it — see the deferred list below.

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
- **Multi-task rounds (F3B).** `FixedSequence` with `tasksPerRound: 3` — structurally
  rejected at `Competition.cs:573-580` with `drawPhase.unsupportedRoundComposition`.
- **Catalogue-choice rounds (F3K and F5K only).** Rejected at the same site, but a
  *different* problem from F3B's. **Decided 2026-08-08: when this thread is taken, each
  round's task is set at draw time** — so `PhaseDrawn` grows a per-round task selection
  rather than a separate later event. **Confirmed live 2026-08-08 (bind-parameter
  thread, see the Update section above): this is now the *only* thing standing between
  F5K and a working draw** — `BindParameter` resolves its `minPerGroup`, but
  `Competition.DrawPhase` still rejects F5K's real definition with
  `drawPhase.unsupportedRoundComposition` first. F3K is blocked by the same check.
- **Per-round parameter bindings.** `ParameterBinding` carries no round or phase ordinal
  (`Competition.cs:116-125`), so `ParameterBindingPoint.PerRound` is *unrepresentable*.
  Six parameters are affected, all F3K's. **Decided 2026-08-08: deferred into the
  catalogue-choice thread above**, because binding "the working time for round 3" is
  meaningless until round 3 has a task, and adding scope alone unblocks nothing — F3K is
  independently blocked by the round composition. Reasoning in
  `bind-parameter-steel-thread-plan.md`, finding 1.
- **The `.class` notation parser** and **class-definition drift detection** — both
  settled out of scope.

**One item has moved off this list.** `Parameter.DefaultValue` was inert —
`ParameterResolver` consulted only bindings and threw on an unbound `Ref`. **Decided
2026-08-08: the resolver falls back to the declared default**, rather than seeding
`ParameterBound` events at `CreateCompetition`. The audit objection to a fallback does
not hold (`AdoptedRules.Definition` is an immutable copy already in the log, so defaults
are auditable and the effective value is reconstructible), and seeding would silently
defeat `RulesAmended`'s retroactive intent. Scheduled as WI-2 of
`bind-parameter-steel-thread-plan.md`.

## Recommended sequencing — a proposal, not a decision

1. **`BindParameter` slice first** (~1 day). It is small, it is the same shape already
   executed four times, and it fixes three seed-corpus classes that are broken *now*
   (gap 4). Cheapest real value in the repo. **Done, 2026-08-08 — see the Update
   section above.** Fixes F5L and NZ Class M ALES 200 outright; F5K still needs the
   catalogue-choice thread (item 3's sibling, listed under "Deliberately deferred") on
   top before its draw actually succeeds.
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
