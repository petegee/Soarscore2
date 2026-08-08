# Gap analysis — what the codebase does not yet do

**Status:** Re-verified · **Date:** 2026-08-09 · **Commit:** `900df6d`, branch `master`

> **This is a point-in-time audit, not a live document.** Every claim below was
> re-verified by reading the tree at `900df6d`, superseding the original `d273ed1`
> snapshot. Line numbers drift on the next commit that touches a file. **Re-verify
> before acting on any of it** — treat a `file:line` here as a starting point for a
> grep, not as an address.
>
> Test suite: green — 440 tests (Domain 236, Application 167, Architecture 7,
> Acceptance 3, Infrastructure/Storage 27; the storage tests are filtered out of a fast
> local loop with `dotnet test --filter "Category!=Storage"`). That is `900df6d`'s 438
> plus the two `ClassAgnosticismTests` this re-audit added — see §6.
>
> The two **Update** sections below are kept as the record of how the tree got here.
> The gap table and the per-gap sections have been rewritten against `900df6d` and
> no longer need reading through those updates to be trusted.

## Context

Seven plans live in `docs/plans/`: `command-side`, `create-competition`,
`class-definition-adoption`, `register-competitor`, `phase-drawn`, `bind-parameter`,
`capture-a-score`. All seven are implemented in code. Their *What this unlocks*
sections were audited item by item, and this document is what those sections promised
but the tree does not yet contain. An eighth, `scoring-steel-thread-plan.md`, is
proposed and not yet started — it covers gap 5 and §6.

One documentation artefact worth noting because it misleads a reader: only
`command-side-steel-thread-plan.md` carries `**Status:** Complete`. The other six say
`**Status:** Proposed` despite having landed and being test-verified.

Two plan documents are **cited by shipped code but absent from HEAD**:
`docs/plans/scoring-service-plan.md` and `docs/plans/scoring-service-issues.md`, added
in `d1ea17d` and removed in `38cb008`. All eleven files under
`src/Soarscore.Domain/Scoring/` header-cite the former, and the latter holds the eight
resolved design questions the shipped pipeline code implements. Both are recoverable
(`git show d1ea17d:docs/plans/scoring-service-plan.md`).

## Update — 2026-08-08: `bind-parameter-steel-thread-plan.md` implemented

All eight work items landed: `Competition.BindParameter` (decide function),
`ParameterResolver`'s default-value fallback, the `BindParameter` command/handler,
Marten wiring, the `/bind-parameter` endpoint, CsCheck property tests, Postgres
store-backed tests, and a real executed end-to-end run captured at
`docs/verification/bind-parameter-e2e.http`. Full solution build is clean; 339
non-Storage tests pass (Domain 199, Application 136, Architecture 4) plus 22
Storage-tagged tests against a real Postgres — up from the 306/22 baseline this
snapshot recorded.

~~**Not yet committed.**~~ Committed since; the working tree is clean at `900df6d`.

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

## Update — 2026-08-09: `capture-a-score-steel-thread-plan.md` implemented

All thirteen work items landed and are committed on `master`. `Competition.OpenEntry`,
`Entry.OpenFlight` and `Entry.CaptureMeasurement` (decide functions), the
`OpenEntry`/`OpenFlight`/`CaptureMeasurement` commands and handlers, the `entry_index`
read model (`EntryLoader`, `EntryProjection`, `IEntryQuery`, `FindEntries`), Marten
wiring for the three events plus the `EntryIndexProjection`/`MartenEntryQuery`
adapters, the four Api endpoints (`/open-entry`, `/open-flight`,
`/capture-measurement`, `/entries`) with matching DI registrations, CsCheck property
tests (five named invariants, two corpus-generic), store-backed Postgres tests, and a
Reqnroll acceptance-test project driving real HTTP against the real Api over
Testcontainers Postgres. Full solution: 438 tests passing (Architecture 5, Domain 236,
Application 167, Infrastructure/Storage 27, Acceptance 3), 0 failures, clean build.

**Gap 1 (Entry write path) and gap 2 (`entry_index`) are closed.** A score can now be
captured end to end, at both the store layer (`EntryCaptureEventStoreTests.cs`) and
over real HTTP (`Soarscore.Acceptance.Tests`).

**The §7 "no automated end-to-end test" item is closed.** `tests/Soarscore.Acceptance.Tests`
hosts the real `Soarscore.Api` via `WebApplicationFactory<Program>` against a
Testcontainers PostgreSQL and drives it with an `HttpClient` — the first test in the
repo to do either. Three Reqnroll scenarios cover: capturing a flight time for a drawn
competitor, a working time the rulebook leaves open-ended (NZ Class M ALES 200,
exercising nullable `TimeWindow.End`), and a launch before the working time being
recorded rather than refused (the finding-3 regression, `F3K.7`).

**One thing this work surfaced that was not anticipated when the plan was written:**
the real corpus `SeedF3K` definition cannot reach a drawn phase at all — its phases are
`ChooseFromCatalogue`, which `Competition.DrawPhase` rejects, the same
already-deferred "catalogue-choice rounds" gap the bind-parameter thread's update above
hit for F5K. The acceptance test's finding-3 scenario therefore publishes a
hand-authored, single-task F3K-shaped class definition
(`tests/Soarscore.Acceptance.Tests/Support/AcceptanceF3KShape.cs`) reusing F3K's real
task-D numbers, rather than the real corpus F3K — recorded in `tech-debt.md` to
retarget once catalogue-choice draws land.

**Two real bugs were found and fixed by this thread's testing, not by inspection:**

1. **`MartenEntryQuery`'s server-side filters were broken for every strong-typed-id
   comparison.** Marten's LINQ provider duck-types any `...Id`-named type as a bare
   `uuid` scalar, but this repo's ids serialize as `{"value": "<guid>"}` with no custom
   converter — so `Where` clauses on `CompetitionRef`/`GroupRef`/`CompetitorRef` either
   threw `InvalidCastException` or failed server-side with `invalid input syntax for
   type uuid`. This would have broken `OpenEntryHandler`'s `openEntry.alreadyOpen`
   check the first time it ran against real Postgres, silently — nothing catches it
   short of a real store. Found by `EntryCaptureEventStoreTests.cs` (WI-12), fixed by
   filtering client-side after loading, a deliberate trade at this project's scale (≤20
   pilots, ≤8 rounds/day).
2. **`IDispatcher` was registered `AddSingleton`**, capturing the root
   `IServiceProvider` and resolving `Scoped` handlers from it — invisible under `dotnet
   run` but a hard failure under `WebApplicationFactory`'s Development-environment
   scope validation. Found and fixed while building the WI-13 acceptance-test host.

**Consequence for the gap table below:** rows 1 and 2 are resolved. Row 7's
end-to-end-test item is resolved; the remaining §7 items are unaffected. Rows 3, 4, 5,
6 are unaffected by this thread — gap 5 (the orphaned scoring engine) is explicitly
unlocked next, since Entry now produces real measurements for it to consume.

## Gap inventory

| # | Gap | Nature | Consequence | Status at `900df6d` |
|---|---|---|---|---|
| 1 | Entry aggregate has no write path | Largest gap; named by all five plan audits | **No way to capture a score.** | **Closed** — `capture-a-score` |
| 2 | `entry_index` read model does not exist | Permitted by LADR-0001 §3, never built | No leaderboard query surface | **Closed** — `capture-a-score` |
| 3 | Nine of seventeen domain event types are unreachable | Folds exist; no decide functions | Runtime trap, see below | Open (was 7 of 11; recounted) |
| 4 | `BindParameter` / `ParameterBound` has no command | Live functional hole | F5K, F5L, NZ Class M cannot be drawn | **Closed** — `bind-parameter` |
| 5 | The scoring engine is orphaned | 2,153 lines, nine files with no caller, and a deleted plan doc | **Nothing turns captured scores into results.** | Open — planned |
| 6 | The core architectural law has no guarding test | Convention only | Regression is invisible | **Closed** — `ClassAgnosticismTests` |
| 7 | Assorted smaller items | See §7 | — | Six of seven open |

Gap 5 is now the only thing between this system and its purpose. Gaps 1, 2 and 4
closed in sequence, and each closure moved the blocker one step further down the
capture-to-result chain; scoring is the last link.

---

## 1 — Entry aggregate write path · **closed**

Closed by `capture-a-score-steel-thread-plan.md`, committed. `Entry` now carries
`OpenFlight` and `CaptureMeasurement` decide functions and `Competition` carries
`OpenEntry`; the three commands, their handlers, Marten registrations and Api endpoints
all exist. A score can be captured end to end over real HTTP.

Three of Entry's six events remain unreachable — see §3.

## 2 — `entry_index` read model · **closed**

Closed by the same thread. `EntrySummary`, `EntryProjection`, `IEntryQuery`,
`MartenEntryQuery` and the `FindEntries` query all exist, and `MartenConfig` registers
the projection alongside Person, ClassDefinition and Competition.

Worth carrying forward: the Marten adapter filters **client-side after loading**, not
in SQL. Marten's LINQ provider duck-types any `...Id`-named type as a bare `uuid`
scalar, but this repo's ids serialise as `{"value": "<guid>"}`, so server-side `Where`
clauses on `CompetitionRef`/`GroupRef`/`CompetitorRef` either threw or failed with
`invalid input syntax for type uuid`. A deliberate trade at this project's scale
(≤ 20 pilots, ≤ 8 rounds/day), and one nothing short of a real store would have caught.

## 3 — Nine unreachable event types

Recounted at `900df6d`. The original entry said "seven of eleven `CompetitionEvent`
types" and counted only the Competition aggregate; Entry has the same condition on
three of its six events.

**`CompetitionEvent` — six of eleven unreachable.** Folds exist and are tested; the
decide functions on `Competition` are now `Decide`, `RegisterCompetitor`,
`WithdrawCompetitor`, `DrawPhase`, `BindParameter` and `OpenEntry` (the last producing
an `EntryEvent`, not a `CompetitionEvent`). Unreachable: `ReflightGroupAppended`,
`TaskRoundCompleted`, `TaskRoundAnnulled`, `RulesAmended`, `Finalised`,
`PenaltyRecorded`. `ParameterBound` left this list with the bind-parameter thread.

**`EntryEvent` — three of six unreachable.** `EntryOpened`, `FlightOpened` and
`MeasurementCaptured` all have decide functions as of `capture-a-score`. Unreachable:
`MeasurementAmended`, `EntryAnnulled`, `PenaltyRecorded`.

**Runtime trap — read this before writing any command that appends one.**
`src/Soarscore.Infrastructure/MartenConfig.cs` registers exactly the reachable set:
five competition events (`:52-56`) and three entry events (`:67-69`). The comments at
`:40-51` and `:58-66` document the rest as deliberately unregistered. **Appending any
of the nine would fail at runtime.** Any thread touching these must add its own
`MapEventType` line, per LADR-0001 §4.8.

Note the shape of the remaining nine: they are not a backlog so much as three coherent
threads waiting to be taken — task-round lifecycle (`TaskRoundCompleted`,
`TaskRoundAnnulled`, `Finalised`), reflights (`ReflightGroupAppended`), and the second
Entry thread (`MeasurementAmended`, `EntryAnnulled`, both `PenaltyRecorded`s).

## 4 — `BindParameter` / `ParameterBound` · **closed**

Closed by `bind-parameter-steel-thread-plan.md`, committed. `BindParameter` is wired
end to end — decide function, command, handler, Marten registration, `/bind-parameter`
endpoint, property tests, store-backed tests.

**F5L and NZ Class M ALES 200 are drawable as a result.** F5K is not, and not for this
reason: its definition uses `ChooseFromCatalogue` composition, which
`Competition.cs:639-646` rejects with `drawPhase.unsupportedRoundComposition` *before*
parameter resolution is ever reached. See "Catalogue-choice rounds" under the deferred
list. Eight of the eleven corpus classes can be drawn today; F3K and F5K are blocked by
catalogue choice, F3B by multi-task rounds — all three at that same single check.

## 5 — The orphaned scoring engine

Eleven files, 2,153 lines, in `src/Soarscore.Domain/Scoring/`:

| File | Lines | Production callers outside `Scoring/` |
|---|---|---|
| `FlightSelector.cs` | 357 | none |
| `ParameterResolver.cs` | 295 | **three** — `Competition.DrawPhase`, `Competition.OpenEntry`, `TaskResolver` |
| `PhaseAggregator.cs` | 250 | none |
| `ScoringService.cs` | 249 | none |
| `PenaltyEngine.cs` | 231 | none |
| `FlightInterpreter.cs` | 225 | none |
| `ScoringResultTypes.cs` | 179 | none |
| `NormalisationEngine.cs` | 144 | none |
| `PredicateEvaluator.cs` | 101 | none |
| `RankingEngine.cs` | 82 | none |
| `RoundingSupport.cs` | 40 | **one** — `Entry.CaptureMeasurement` |

`ParameterResolver` and `RoundingSupport` were pulled into service by the
bind-parameter and capture-a-score threads respectively; nine files still have no
production caller at all.

Three problems, in order of how much they cost:

**Its plan document was deleted, and so was its issues document.** All eleven files
header-cite `docs/plans/scoring-service-plan.md`. That file was added in `d1ea17d` and
removed in `38cb008` ("renamed folder to reflect true agg root"); so was
`docs/plans/scoring-service-issues.md`, which recorded eight design questions and their
resolutions — the semantics the shipped pipeline implements (issue #4 fixes where
amendment resolution lives, #5 group annulment, #8 the `ByTask` drop algorithm). Both
are recoverable:

```
git show d1ea17d:docs/plans/scoring-service-plan.md
git show d1ea17d:docs/plans/scoring-service-issues.md
```

**It has no caller.** Zero references to `ScoringService` from
`src/Soarscore.Application` or `src/Soarscore.Api`. It is also the only component
with no test file of any kind. `ParameterResolver` gained `ParameterResolverTests` with
the bind-parameter thread; `PredicateEvaluator` has no file of its own but is covered
inside `FlightInterpreterTests.cs:321-382`.

**The nuance that makes most of it cheaper than it looks.** The `object` / `object?`
parameters annotated *TBD* — `ScoringService.cs:26,36,91,136,205,231`,
`FlightInterpreter.cs:30`, `FlightSelector.cs:34` — are **unused in the method
bodies**. The engine already operates on pre-digested inputs (`resolvedMetrics`,
`interpretedFlights`). Six of the seven public methods are complete; rescuing them
needs an adapter that turns an Entry stream into those pre-digested inputs, not a
redesign.

**The exception, which the original snapshot missed.** `ScoreCompetition`
(`ScoringService.cs:133-194`) is a **shell**, not a mis-typed method. It never
populates `allTaskRoundScores` (`:146`), never calls `PhaseAggregator`, and sums an
always-empty list at `:166`, so every competitor scores zero and is then ranked. Its
own comment says so (`:148-149`). That method has to be written.

**Two more things an adapter must reconcile**, neither visible from the file list:
nothing in the tree resolves a `Measurement`'s `Amendments` to an effective value —
the stub meant to do it returns `Empty` (`:204-220`) — and `RecordedPenalty(
InfractionType, OccurrenceCount)` does not match the domain's `Penalty(InfractionType,
Scope)`, which carries no count.

> **Planned.** `scoring-steel-thread-plan.md` (2026-08-09) covers this gap, in two
> slices: score one group, then the ranked leaderboard.

## 6 — The core architectural law is unguarded · **closed**

CLAUDE.md states that "the core system must not know about any specific competition
class" and that this is "not a style preference". The law was held by convention only:
`tests/Soarscore.Architecture.Tests/` asserted layer dependencies, route shape and DI
resolvability, and nothing asserted the absence of class-name branching.

Closed by `tests/Soarscore.Architecture.Tests/ClassAgnosticismTests.cs` (2026-08-09).
It scans every `.cs` file under `src/`, strips comments, and fails on any occurrence of
`F3B|F3F|F3J|F3K|F5J|F5K|F5L|NZMAA|ALES|Radian`. A source scan rather than an
ArchUnitNET rule because neither ArchUnitNET nor reflection can see a string literal or
a switch arm naming a class. Comment stripping is what makes it viable: all twenty
class-name occurrences in `src/` today are rule references or explanatory comments,
which are the good case. A second test asserts the scan reaches real files, so it
cannot pass vacuously.

**The law held in fact when the guard was added** — zero non-comment hits across `src/`,
and no NZ-class leakage either.

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
- ~~**No automated end-to-end test.**~~ **Closed** by `capture-a-score`.
  `tests/Soarscore.Acceptance.Tests` hosts the real Api via
  `WebApplicationFactory<Program>` against a Testcontainers PostgreSQL and drives it
  over `HttpClient` — three Reqnroll scenarios in `Features/CapturingAScore.feature`.
- ~~**No test asserts that every mapped route has a DI registration.**~~ **Closed** by
  `tests/Soarscore.Architecture.Tests/HandlerRegistrationTests.cs`, which resolves
  every mapped command/query handler from the real built `IServiceProvider`. (Raised in
  the 2026-08-08 update below, after a route shipped without its handler registration
  and compiled cleanly.) Its sanity-floor comment at `:71` is stale — it says "ten
  commands and four queries" against thirteen and seven.
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

## Sequencing

1. ~~**`BindParameter` slice**~~ — **done 2026-08-08.** Fixed F5L and NZ Class M ALES
   200 outright; F5K still needs the catalogue-choice thread before its draw succeeds.
2. ~~**Entry write path plus `entry_index`**~~ (gaps 1 and 2) — **done 2026-08-09.**
3. **De-orphan the scoring engine** (gap 5) — **chosen 2026-08-09, planned in
   `scoring-steel-thread-plan.md`, not started.** The critical path: the only remaining
   work that advances the system's actual purpose. Two slices — score one group, then
   the ranked leaderboard.

What comes after is a genuine choice rather than a queue, and the three candidates are
roughly equal in size: **catalogue-choice draws** (unblocks F3K and F5K, retires the
acceptance test's hand-authored F3K stand-in), the **second Entry thread**
(`MeasurementAmended`, `EntryAnnulled`, `PenaltyRecorded` — the inputs the scoring
pipeline can read but nothing can produce), and **task-round lifecycle**
(`TaskRoundCompleted`/`Annulled`, which would let the leaderboard distinguish "not
flown" from "flown, no result" rather than inferring it from Entry presence).

One thing this ordering has consistently rejected: "close the remaining unreachable
events" (gap 3) as a goal in itself is motion without direction — close each one when a
command needs it.

## Standing practice for whoever picks this up

Property-based testing with CsCheck is routine on this repo, not optional garnish:
consider, implement and verify property tests alongside unit tests for any new feature.
Every thread that has landed shipped with them.
