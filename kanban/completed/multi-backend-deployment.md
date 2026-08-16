# Story — Ship on three stores: Fisher/SQLite, Marten/PostgreSQL, Polecat/SQL Server

**Status:** Completed 2026-08-16 (Fisher/SQLite; Polecat deferred) · **Raised:** 2026-08-16 · **Planned:** 2026-08-16

## Scope of this pass — Fisher/SQLite only

**Polecat/SQL Server is deferred by decision**, not dropped: the shape this story
describes is one shared adapter body plus one thin composition root per backend, and
proving it with a *second* store is what tests the shape. A third adds cost without
adding evidence until someone actually wants SQL Server. Entered in
`kanban/deferred-decisions.md`. Everything below therefore says "both backends", meaning
Marten/PostgreSQL and Fisher/SQLite.

## What

Offer Soarscore as a single codebase deployable on any of the three Critter Stack event
stores, chosen at composition time:

| Store | Backing | Version at raising | Fit |
|---|---|---|---|
| **Fisher** | SQLite, in-process file | 0.7.1, MIT | A club secretary's laptop. No server, backup is `cp`. |
| **Marten** | PostgreSQL | 9.24.0, MIT | Today's only adapter (LADR-0001). |
| **Polecat** | SQL Server 2025 | 5.15.1, MIT | A club already running SQL Server. |

All three implement the same `JasperFx.Events` / `JasperFx.Events.Documents` contracts and
enrol in the same 32-suite, 272-test `JasperFx.Events.ComplianceTests`. The shape is one
shared adapter body plus one thin composition root per backend — roughly 400 LOC shared,
~60 LOC per store — **not** one build switched by configuration: `AddMarten` /
`AddPolecat` / `AddFisher` and their `StoreOptions` are deliberately not shared.

## Why it matters

LADR-0001 accepted "install Docker, run a Postgres container" as the ask of a
self-hoster, while noting that a SQLite option was worth keeping *possible* and not worth
*building* — costed at ~1,400 LOC of hand-rolled store. Fisher removes that cost
entirely: it is a NuGet package by the same author, under the same support contracts,
holding the same compliance suite. The premise the ADR declined on has changed.

There is a second, nearer prize that does not need a Fisher deployment at all:
**`tests/Soarscore.Infrastructure.Tests` currently needs Testcontainers and a real
PostgreSQL** (`Trait("Category","Storage")`, filtered out of the fast loop). A
Fisher-backed peer of that suite runs `dotnet test` with no Docker and no container wait.
That may be worth doing on its own, ahead of any deployment story.

## Before starting

- **Blocked in practice on `kanban/backlog/jasperfx-shared-store-contracts.md`.** Nothing
  here is sane until the adapters are off Marten's own types and Marten is on 9.24.0.
- **Fisher is 0.7.x.** Consider gating a *deployment* claim on 1.0 while allowing the
  test-store use immediately. The deliberate, permanent Fisher gaps — no message bus, no
  partitioning, no `DaemonMode.HotCold`, no Newtonsoft — are all already out of scope per
  LADR-0001 §2, so none of them bind us.
- **One writer per SQLite file is the hard ceiling, and it is the one LADR-0001 §6
  already names.** Fisher's exclusive-append methods *fail* where Marten's *wait*; the
  version guard still runs inside the write transaction, so the safety property is
  unchanged, but code relying on waiting needs a retry. At ≤20 pilots and single-digit
  writes/minute this is not a constraint we are near.
- **`SearchByNameAsync` is the one query whose semantics genuinely differ across
  backends.** `p.Name.Contains(name)` compiles on all three and means something slightly
  different on each — Fisher uses ordinal, case-sensitive `instr`/`substr` deliberately,
  so that `Contains` cannot contradict `==` in the same `Where`. A pilot-name search is
  exactly where a user expects case-insensitivity. Pin the intended behaviour with a
  test that runs against every backend, and decide what it should be before picking a
  store to match.
- **The test matrix triples.** BDD/Gherkin acceptance tests (CLAUDE.md "Testing
  approach") should run against every supported backend, or the support claim is
  unbacked. Budget for that rather than discovering it.
- **Guid storage differs** — Fisher stores Guids as lowercase canonical text where
  PostgreSQL has `uuid` and SQL Server `uniqueidentifier`. Invisible through the API,
  visible in any hand-written SQL and in a cross-store migration.
- **The migration between stores is a replay**, as LADR-0001 §5 already sets out, and
  verifiable by re-deriving every score on the target and diffing against the source.
  With three stores that mechanism stops being hypothetical.

## Plan

Fisher 0.7.1's surface confirmed by reflection over the shipped assembly, not from
documentation. It is deliberately Marten-shaped: `Fisher.DocumentStore.For(Action<StoreOptions>)`,
`Fisher.IDocumentStore : IDocumentSessionFactory`, `Fisher.IQuerySession : IDocumentReadOperations`,
`Fisher.IDocumentSession : IDocumentSessionOperations`, `opts.Schema.For<T>().UniqueIndex(…)`,
`opts.Projections.Add(IProjection, ProjectionLifecycle, name)`, and
`Fisher.Projections.IProjection : IJasperFxProjection<Fisher.IDocumentSession>`. It pulls
JasperFx.Events **2.48.0** — the same version Marten 9.24.0 does, so no package bump is
needed. Five places it is *not* Marten-shaped, each of which the plan names a work item for:

1. **No `EventStoreOptions.AppendMode`.** There is no Rich/Quick distinction to set, so
   whether the version-checked `Append(id, expectedVersion, events)` is honoured has to
   be proven, not configured (WI-6). This is the single riskiest unknown in the story.
2. **No `MapEventType<T>(alias)`.** Fisher has `Events.AddEventType<T>()` and a settable
   `EventTypeName` on the `FisherEventType` that `EventGraph.EventMappingFor(type)` hands
   back. Same registry, different call.
3. **No `QueryAllRawEvents()`.** `ReadAllAsync` is already a per-store method by decision
   (`jasperfx-shared-store-contracts.md` WI-4) for exactly this reason.
4. **Serialization is `ConfigureSerialization(EnumStorage, Casing, …, Action<JsonSerializerOptions>)`,**
   not `UseSystemTextJsonForSerialization(options, …)` — a mutate-in-place callback rather
   than an options instance handed over.
5. **SQLite's unique-violation signal is `SqliteException`,** not `PostgresException`
   SqlState 23505 — the one thing `TranslateAppendException` exists to abstract.

- **WI-1 — Fisher package and the second composition root.** `Fisher` 0.7.1 into
  `Directory.Packages.props` and `Soarscore.Infrastructure.csproj`. `FisherConfig.cs`
  mirrors `MartenConfig.cs` line for line: connection, event-type aliases, serialization,
  the `PersonSummary.Email` unique index, the four Inline projection registrations with
  their names pinned. One project, two stores — see "Deliberately not done" below.
- **WI-2 — `FisherEventStore : JasperFxEventStore`.** The four members the base leaves
  abstract, and nothing else: the two `.Events` accessors, `TranslateAppendException`
  (Fisher's `ExistingStreamIdCollisionException` + the SQLite unique-violation walk), and
  `ReadAllAsync`. If the body needs more than that, the base's seams are wrong and this
  work item says so rather than widening them quietly.
- **WI-3 — The four projection shims.** `Fisher*Projection : <fold><Fisher.IDocumentSession>,
  Fisher.Projections.IProjection`, plus the strong-typed-id `LoadCurrentAsync` override on
  three of the four if Fisher has the same limit Marten does (`jasperfx-shared-store-contracts.md`
  WI-6 — the shared contract's Guid-only identity overloads). Whether it does is an
  empirical question, answered by WI-6, not assumed here.
- **WI-4 — Backend selection at composition time.** `AddSoarscoreInfrastructure` keeps its
  signature and dispatches on a `Soarscore:Store` configuration value (`postgres` |
  `sqlite`) to one of two per-store roots. The two roots stay separate code; only the
  choice between them is configuration.
- **WI-5 — Pin `SearchByNameAsync`.** The one query whose semantics genuinely differ
  ("Before starting"). Decide the intended behaviour first, then a test that asserts it
  runs against both backends. Behaviour on each store is measured, not assumed.
- **WI-6 — `tests/Soarscore.Infrastructure.Tests` runs every existing test against both
  backends.** Not a new suite: each of the eight test classes becomes an abstract
  `…Tests<TFixture> : IClassFixture<TFixture> where TFixture : IStoreFixture` with two
  sealed subclasses. The Postgres subclass keeps `Trait("Category","Storage")`; the Fisher
  subclass carries no such trait, because it needs no Docker — which is the "second,
  nearer prize" this story's Why names. Each store's fixture is one temporary SQLite file
  or one container, disposed with the fixture.
  **This is where the five unknowns above get answered**, because the existing suite
  already covers all of them: version-checked append and stale-version rejection
  (`MartenEventStoreTests`), the unique-index violation inside the append transaction, the
  strong-typed-id projection loads (every `…EventStoreTests` that reads a summary), and
  event-alias round-tripping (every read of a payload). A backend that passes the whole
  suite unchanged has earned the claim; one that needs the tests softened has not.
- **WI-7 — The acceptance suite runs against both backends.** `AcceptanceFixture` is a
  `[BeforeTestRun]` singleton, so this is a per-run choice, not a per-class one: it reads
  `SOARSCORE_TEST_STORE` (default `postgres`, so nothing about today's run changes) and
  builds either a container or a temp SQLite file. CI runs the suite twice.
- **WI-8 — Architecture rules, LADR-0001 amendment, board reconciliation.** The layer
  rules must exclude `Fisher` and `Microsoft.Data.Sqlite` from Domain/Application
  alongside `Marten`/`Npgsql`/`JasperFx` — the reason is unchanged and the rule should
  enforce it rather than leave it to judgement. LADR-0001 §5's store-swap cost estimate is
  now measurable rather than estimated; amend it with what this story actually cost.

### Deliberately not done

- **No per-backend assembly split.** `Soarscore.Infrastructure` references both Marten and
  Fisher, so a SQLite deployment carries Npgsql it never loads. At this project's scale
  that is a few hundred KB, and splitting costs a project per store plus edits to the arch
  rules, the Api, and three test projects. Revisit if Polecat lands. CLAUDE.md's
  repository map says Infrastructure is "the only project allowed to reference Marten";
  that sentence needs widening to "the only project allowed to reference a store", which
  WI-8 does.
- **No compliance-suite enrolment.** `JasperFx.Events.ComplianceTests` proves *Fisher*
  correct; that is JasperFx's job and they already do it. Our suite's job is to prove
  *Soarscore* correct on Fisher, which is a different question and the one WI-6 answers.

### Property-based testing

No new invariant. This story changes which store computes the answers, not what the
answers are — and the honest instrument for that is the existing suite run twice, not a
new property. The one thing worth stating as an invariant is **the whole of WI-6**: for
every test in `Soarscore.Infrastructure.Tests`, the observable result through the
`IEventStore` / `I*Query` ports is identical on both backends. A property test over
generated event sequences would be a weaker version of that, because the existing tests
already encode which sequences matter. If a backend difference is found that the suite
cannot express, *that* is when a property earns its place — and WI-5's
`SearchByNameAsync` is the one candidate visible in advance.

## Outcome — as built, 2026-08-16

All eight work items done for Fisher/SQLite; Polecat deferred by decision (see "Scope of
this pass", and `deferred-decisions.md`). Build clean (0 warnings). Full suite green:

| Suite | Count | Notes |
|---|---|---|
| Domain | 271 | unchanged |
| Application | 175 | unchanged |
| Architecture | 7 | layer rules now name Fisher and Microsoft.Data.Sqlite |
| Infrastructure | **72** | was 34 — every test run against both backends (36 × 2) |
| Acceptance | 8 + 8 | the same 8 scenarios, run once per store |

**The SQLite half of the Infrastructure suite runs in ~1 second with no Docker at all**,
against 9 seconds for the pair. That was named in the story's Why as the "second, nearer
prize" and it landed exactly as described.

### The story's premise did not survive intact — one finding, and it is a serious one

**`expectedVersion` means different things on Marten and on Fisher, and the shared
contract does not say which.** On Marten it is the version the stream will hold AFTER the
append; on Fisher it is the version it holds BEFORE. Established by spiking Fisher
directly: against a one-event stream, `Append(id, 1, …)` succeeds and `Append(id, 2, …)`
throws `EventStreamUnexpectedMaxEventIdException("expected 2 but was 1")`; Marten behaves
the other way round.

This directly contradicts `kanban/completed/jasperfx-shared-store-contracts.md`, which
recorded the Marten reading as "the semantics of the shared
`JasperFx.Events.IEventOperations` contract, so it holds for every store implementing
it". That completed plan is history and is not edited (house-keeping rule 5); this one is
newer and says so, and `JasperFxEventStore.cs`'s header now carries the correction where
anyone touching the code will read it.

It matters more than a normal API difference because **getting it wrong does not throw**.
A backend that answered this the other way round would silently either never fail the
concurrency check or always fail it. So `AppendExpectedVersion(currentVersion, eventCount)`
is `protected abstract` with no default — a third backend is made to state its answer,
and `EventStoreTests.Stale_expected_version_is_rejected_and_the_earlier_append_survives`
fails loudly on either mistake. The safety property itself is unchanged on both: the guard
runs inside the write transaction, and both stores signal a violation the same way.

The four other Fisher/Marten divergences the plan predicted all held, and all were absorbed
where predicted:

- **No `AppendMode`** — Fisher has no Rich/Quick distinction and needs no setting.
- **No `MapEventType<T>(alias)`** — `AddEventType` plus a settable `EventTypeName`.
- **No `QueryAllRawEvents()`** — `ReadAllAsync` was already per-store by decision. The
  Fisher implementation sorts and pages in memory; entered in `tech-debt.md`.
- **`SqliteException` extended codes 2067/1555** in place of PostgreSQL SqlState 23505.
- **Serialization is a mutate-in-place callback**, not an options instance handed over.

### Two things found by building it that the plan did not predict

1. **Fisher does not build its schema lazily.** `AutoCreateSchemaObjects` defaults to
   `CreateOrUpdate` on both stores, but on Fisher that is the policy applied *when* a
   migration runs, not a trigger for running one — a fresh database fails the first append
   with "no such table: fi_streams" from inside `SaveChangesAsync`. `FisherConfig` applies
   the schema while building the store. Entered in `tech-debt.md`: the blocking call is
   defensible in a composition root but should move if an async initialisation seam
   appears.

2. **The real adapters and the Application layer's own test doubles disagreed about what
   a name search means.** WI-5 went looking for a *cross-backend* difference in
   `SearchByNameAsync` and found a *cross-layer* one instead:
   `tests/Soarscore.Application.Tests/Shared/People/TestDoubles.cs` had been
   `OrdinalIgnoreCase` all along, while `DocumentPeopleQuery` was `Contains(name)` —
   case-sensitive on Postgres and on SQLite alike. So every handler test was passing
   against behaviour the real store did not have. Both name searches
   (`IPeopleQuery.SearchByNameAsync`, `IClassLibraryQuery.SearchAsync`) are now explicitly
   `OrdinalIgnoreCase` — user-confirmed 2026-08-16 — and `NameSearchTests` pins it on
   every backend. The two stores agreeing today was luck, not a promise either of them
   makes.

### Also done, not in the original plan

- **`SoarscoreEventTypes.cs`** — the event-type alias table extracted to one list read by
  both composition roots. WI-1 first copied the fourteen lines into `FisherConfig` and
  logged tech debt asking for a test that the two agreed; one list read twice is strictly
  better than two lists checked against each other, since there is nothing left to
  disagree. What stays per-store is the registration *call*, which is genuinely different.
- **`IStoreFixture`** — the seam that let every existing store-backed test run on both
  backends without any test changing. Its two non-port members (drop a read model, rebuild
  a projection by name) are the honest admission that drop-and-replay is an operator's
  action with no application port, and is why it is an interface rather than a base class.
- **Four test methods renamed** off `..._against_real_postgres` / `..._through_postgres`,
  which stopped being true when they started running on two stores.

### What a third backend now costs

Measured rather than estimated, which is the thing LADR-0001 §5 could not do. Fisher took:
one composition root (~110 lines, over half of it comment), one `JasperFxEventStore`
subclass answering four abstract members (~140 lines, likewise), four projection shims of
3–8 lines each, one `switch` arm in `AddSoarscoreInfrastructure`, one package reference,
and one test fixture. No adapter body, no query adapter, no projection fold and no test
assertion changed. The one thing a third backend's author must not inherit on trust is
`AppendExpectedVersion`.

### One thing deliberately left short of the story's title

The story is called "Ship on three stores". It ships **two**, and it stops short of
*announcing* even the second. Fisher is 0.7.1 — pre-1.0, where a minor bump may still be a
breaking one — so the package version is pinned rather than floated, and the deployment
*claim* is gated on 1.0 exactly as the story's own "Before starting" proposed. What that
gate does not hold back is any of the code or any of the testing: `Soarscore:Store=sqlite`
composes the whole system on a SQLite file today and passes everything. When Fisher reaches
1.0 the change is a version bump and a sentence, not work. Entered in
`deferred-decisions.md` alongside the Polecat deferral.
