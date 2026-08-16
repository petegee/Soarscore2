# Gap analysis — what the codebase does not yet do

**Status:** Re-verified · **Date:** 2026-08-16 · **Commit:** `562d935`, branch `master`

> **This is a point-in-time audit, not a live document.** Every claim below was
> re-verified by reading the tree at `562d935`, superseding the `900df6d` snapshot.
> Line numbers drift on the next commit that touches a file. **Re-verify before acting
> on any of it** — treat a `file:line` here as a starting point for a grep, not as an
> address.
>
> Test suite: green — 455 tests (Domain 246, Application 167, Architecture 7,
> Acceptance 6, Infrastructure/Storage 29; the storage tests are filtered out of a fast
> local loop with `dotnet test --filter "Category!=Storage"`). That is `900df6d`'s 440
> plus the ten Domain property tests, three Acceptance scenarios and two Storage tests
> `scoring-steel-thread-plan.md` added closing gap 5 — see the Update below and §5.
>
> The three **Update** sections below are kept as the record of how the tree got here.
> The gap table and the per-gap sections have been rewritten against `562d935` and no
> longer need reading through those updates to be trusted.

## Context

Eight plans live in `docs/plans/`: `command-side`, `create-competition`,
`class-definition-adoption`, `register-competitor`, `phase-drawn`, `bind-parameter`,
`capture-a-score`, `scoring-steel-thread`. All eight are implemented in code. Their
*What this unlocks* sections were audited item by item, and this document is what those
sections promised but the tree does not yet contain.

One documentation artefact worth noting because it misleads a reader: only
`command-side-steel-thread-plan.md` carries `**Status:** Complete`. The other seven say
`**Status:** Proposed` despite having landed and being test-verified.

Two further plan documents were **cited by shipped code but absent from HEAD** —
`docs/plans/scoring-service-plan.md` and `docs/plans/scoring-service-issues.md`, added
in `d1ea17d` and removed in `38cb008`. **Restored at `900df6d`**, each carrying a status
header recording what shipped and what is superseded: WI-1 through WI-8 are the code in
the tree, WI-9 is superseded by `scoring-steel-thread-plan.md`, and all eight design
issues are resolved and still binding.

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

## Update — 2026-08-16: `scoring-steel-thread-plan.md` implemented

All twelve work items landed on `master` (commit `562d935`; the plan document itself
still says `**Status:** Proposed`, per the pattern already noted in Context above).
`MeasurementDigest.Resolve` (amendment resolution), the retyped `ScoringService` static
class, a real `ScoreCompetition` (no longer a shell — it now walks
`Competition.Phases`, builds `RoundData`/`TaskRoundData`, calls `PhaseAggregator` and
`PenaltyEngine`, and ranks via `RankingEngine`), penalty routing
(`GetEntryPenalties`/`GetAggregatePenalties`), `EntryCollector`
(`src/Soarscore.Application/Queries/Entries/EntryCollector.cs`), the `ScoreTaskRound`
and `ScoreCompetition` queries and handlers, the `/task-round-result` and
`/competition-result` Api endpoints with matching DI registrations, CsCheck property
tests (the seven named invariants WI-5 specified), Postgres store-backed tests
(`ScoringEventStoreTests.cs`), and a three-scenario Reqnroll acceptance test
(`Features/ScoringACompetition.feature`). Full solution: 455 tests passing (Domain 246,
Application 167, Architecture 7, Acceptance 6, Infrastructure/Storage 29), 0 failures,
clean build.

**Gap 5 (the orphaned scoring engine) is closed.** All eleven files under
`src/Soarscore.Domain/Scoring/` now have a production caller, `ScoreCompetition` is a
real implementation rather than a shell, and a captured score is reachable as a ranked
result over real HTTP.

**Gap 6 stays closed under the new code.** `ClassAgnosticismTests` scans everything
under `src/`, so this thread's new files (`MeasurementDigest.cs`, `EntryCollector.cs`,
the two query files) are covered without any change to the guard test itself, and no
class-name branching was introduced.

The tech-debt item this thread was predicted to discharge did discharge: the duplicate
`TaskRoundState` enum (`tech-debt.md`) is now resolved by an explicit, documented
mapping inside `ScoreCompetition`, exactly as `capture-a-score-steel-thread-plan.md`
anticipated — checked off in `tech-debt.md`.

**Consequence for the gap table below:** row 5 is resolved. Rows 3, 4, 6 and 7 are
unaffected by this thread. This closes the last of the three gaps
(`gap.md`'s own "Sequencing" section) that stood between the system and its purpose;
what remains is genuine choice between roughly equal-sized threads — see Sequencing
below.

## Gap inventory

| # | Gap | Nature | Consequence | Status at `562d935` |
|---|---|---|---|---|
| 1 | Entry aggregate has no write path | Largest gap; named by all five plan audits | **No way to capture a score.** | **Closed** — `capture-a-score` |
| 2 | `entry_index` read model does not exist | Permitted by LADR-0001 §3, never built | No leaderboard query surface | **Closed** — `capture-a-score` |
| 3 | Nine of seventeen domain event types are unreachable | Folds exist; no decide functions | Runtime trap, see below | Open (was 7 of 11; recounted) |
| 4 | `BindParameter` / `ParameterBound` has no command | Live functional hole | F5K, F5L, NZ Class M cannot be drawn | **Closed** — `bind-parameter` |
| 5 | The scoring engine is orphaned | 2,153 lines, nine files with no caller, and a deleted plan doc | Nothing turned captured scores into results | **Closed** — `scoring-steel-thread` |
| 6 | The core architectural law has no guarding test | Convention only | Regression is invisible | **Closed** — `ClassAgnosticismTests` |
| 7 | Assorted smaller items | See §7 | — | Six of seven open |

Gaps 1, 2, 4 and 5 closed in sequence, and each closure moved the blocker one step
further down the capture-to-result chain. A score can now be captured, and turned into
a ranked result, end to end over real HTTP. What remains is §7's smaller items plus the
three roughly-equal-sized threads named in Sequencing below — none of them blocking the
system's core purpose the way gaps 1–5 did.

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

## 5 — The orphaned scoring engine · **closed**

Closed by `scoring-steel-thread-plan.md`, committed (`562d935`). Eleven files, 2,153
lines, in `src/Soarscore.Domain/Scoring/` — every one now has a production caller.

| File | Lines | Production callers outside `Scoring/` |
|---|---|---|
| `FlightSelector.cs` | 357 | `ScoringService.ScoreGroup` |
| `ParameterResolver.cs` | 295 | `Competition.DrawPhase`, `Competition.OpenEntry`, `TaskResolver` |
| `PhaseAggregator.cs` | 250 | `ScoringService.ScoreCompetition` |
| `ScoringService.cs` | 249 | `ScoreTaskRoundHandler`, `ScoreCompetitionHandler` |
| `PenaltyEngine.cs` | 231 | `ScoringService.ScoreCompetition` |
| `FlightInterpreter.cs` | 225 | `ScoringService.ScoreGroup` |
| `ScoringResultTypes.cs` | 179 | throughout `ScoringService` and the two query handlers |
| `NormalisationEngine.cs` | 144 | `ScoringService.ScoreGroup` |
| `PredicateEvaluator.cs` | 101 | `FlightInterpreter` |
| `RankingEngine.cs` | 82 | `ScoringService.ScoreCompetition` |
| `RoundingSupport.cs` | 40 | `Entry.CaptureMeasurement` |

What the `scoring-steel-thread` did, against the three problems the previous audit
found:

- **`ScoreCompetition` was a shell; it is now a real implementation.** It walks
  `competition.Phases` → `Round` → `TaskRound` → `Group`, builds `RoundData`/
  `TaskRoundData` (a task-round enters the walk only when at least one Entry exists
  for it — an unflown task-round is omitted, never scored as zero), calls
  `PhaseAggregator.Aggregate` and `PenaltyEngine.ApplyAggregatePenalties`, and ranks via
  `RankingEngine.Rank`.
- **Amendment resolution exists.** `MeasurementDigest.Resolve` (new file) reduces a
  `Measurement`'s amendments to an effective value: most recent by `At`, last-appended
  on ties.
- **Penalty routing is real.** `GetEntryPenalties`/`GetAggregatePenalties` group by
  `InfractionType`, count occurrences, and split on `PenaltyScope`, reading
  `Entry.Penalties` and `Competition.Penalties` respectively — both still empty in
  practice today, since neither `PenaltyRecorded` event has a decide function yet (see
  §3), but the routing itself is no longer a stub.
- **`ScoringService` is `static`, not `public class`,** and its `object`/`object?`
  *TBD* parameters are gone — retyped to `ResolvedTask`, `Entry?` and
  `ImmutableDictionary<string, Entry>` where the engine actually reads them.

**New callers added to close the gap:** `EntryCollector`
(`src/Soarscore.Application/Queries/Entries/EntryCollector.cs`) fans out
`IEntryQuery.FindAsync` then folds each Entry stream, giving `ScoreCompetition` the
`IReadOnlyDictionary<EntryId, Entry>` it needs; the `ScoreTaskRound` and
`ScoreCompetition` Application queries and their handlers; the `/task-round-result` and
`/competition-result` Api endpoints.

**Test coverage, previously nonexistent, now exists at every layer:** seven named
CsCheck property tests (amendment resolution is last-write-wins, scoring is
order-invariant over distinct metrics, scoring is a pure function, dropping never
raises and never over-lowers, placings are a consistent total order, id round-trip is
lossless, every drawable corpus class scores without throwing), Postgres store-backed
tests (`ScoringEventStoreTests.cs`), and a three-scenario Reqnroll acceptance test
(`Features/ScoringACompetition.feature`).

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
3. ~~**De-orphan the scoring engine**~~ (gap 5) — **done 2026-08-16**, in
   `scoring-steel-thread-plan.md`'s two slices (score one group, then the ranked
   leaderboard). The critical path is closed: a captured score is now reachable as a
   ranked result over real HTTP.

What comes next is a genuine choice rather than a queue, and the three candidates are
roughly equal in size: **catalogue-choice draws** (unblocks F3K and F5K, retires the
acceptance test's hand-authored F3K stand-in), the **second Entry thread**
(`MeasurementAmended`, `EntryAnnulled`, `PenaltyRecorded` — the inputs the scoring
pipeline can now read but nothing can yet produce, per §5's penalty-routing note above),
and **task-round lifecycle** (`TaskRoundCompleted`/`Annulled`, which would let the
leaderboard distinguish "not flown" from "flown, no result" rather than inferring it
from Entry presence, per `tech-debt.md`'s `TaskRoundState` mapping note).

One thing this ordering has consistently rejected: "close the remaining unreachable
events" (gap 3) as a goal in itself is motion without direction — close each one when a
command needs it.

## Standing practice for whoever picks this up

Property-based testing with CsCheck is routine on this repo, not optional garnish:
consider, implement and verify property tests alongside unit tests for any new feature.
Every thread that has landed shipped with them.
