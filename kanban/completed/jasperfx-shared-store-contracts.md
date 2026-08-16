# Story — Move the store adapters onto the JasperFx shared contracts

**Status:** Completed 2026-08-16 · **Raised:** 2026-08-16 · **Planned:** 2026-08-16

## What

`Soarscore.Infrastructure` is written against Marten's own types (`IDocumentStore`,
`IDocumentSession`, `IDocumentOperations`, `Marten.Events.Projections.IProjection`).
Since **JasperFx 2.47.0** most of that surface exists as store-agnostic contracts in
`JasperFx.Events` and `JasperFx.Events.Documents`, which Marten, Polecat and Fisher each
implement *as their own types* — no adapter, no wrapper.

Rewrite the adapter bodies against those contracts, leaving only the composition root
store-specific. Two prerequisites and four pieces of work:

- **Bump Marten 9.22.2 → 9.24.0.** We are on JasperFx.Events **2.37.0**; the document
  contracts landed in **2.47.0**. Marten 9.24.0 pulls **2.48.0** — the same version
  Fisher 0.7.1 is built against. Nothing below compiles until this happens.
- **The four query adapters** (`Marten*Query.cs`, 172 LOC) become store-agnostic
  outright. `IDocumentSessionFactory.QuerySession()` → `IDocumentReadOperations.Query<T>()`
  → `ToListAsync` / `FirstOrDefaultAsync` from `JasperFx.Events.Documents.DocumentQueryableExtensions`.
  Every member they use is on the shared contract; only the `using Marten;` goes.
- **The four projection shims** (172 LOC) become `IJasperFxProjection<TOps> where TOps :
  IDocumentWriteOperations` — they use only `LoadAsync` and `Store`, both shared. Each
  store's *registration* API still wants its own marker interface
  (`Marten.IProjection : IJasperFxProjection<Marten.IDocumentOperations>`), so a ~5-line
  per-store shim class stays.
- **`MartenEventStore.cs`** (161 LOC) is ~90% portable. `StartStream`, the
  version-checked `Append(id, expectedVersion, events)`, `FetchStreamAsync(fromVersion:)`,
  `FetchStreamStateAsync`, `EventAppendMode.Rich` and
  `EventStreamUnexpectedMaxEventIdException` are all on `JasperFx.Events`. What stays
  per-store is the two exception translations — `ExistingStreamIdCollisionException` and
  the `PostgresException` SqlState 23505 walk (SQLite's equivalents are 2067/1555).

## Why it matters

It is the enabling step for `kanban/backlog/multi-backend-deployment.md`, but it does not
depend on it and should not wait for it: the result is strictly better code on Marten
alone — less of our adapter is coupled to one vendor's type names, for no loss.

It also corrects LADR-0001. §5 costs a store swap at "1,000–1,200 LOC to rebuild" on the
premise that the session and store types are Marten's. That premise is now half false,
and the ADR's §4 constraints are what make the rewrite cheap — §4.5 (never query into
event JSON) and §4.6 (decimals as strings) have paid off exactly as intended. See the
amendment section on `docs/ladr/ladr-0001-event-store.md`.

## Before starting

- **`MartenConfig.cs` and the DI wiring (158 LOC) stay per-store, entirely.**
  `AddMarten`/`AddPolecat`/`AddFisher`, `StoreOptions`, `MapEventType`, `UniqueIndex` and
  projection registration are *not* shared and JasperFx is not trying to share them. Do
  not chase this; a per-backend composition root is the intended shape.
- **`IEventStore` (the three-method port) stays.** Its justification was never
  portability — it returns `Result<T>` instead of throwing, rejects `Guid.Empty`, and
  translates store exceptions into domain failure codes (LADR-0001 §4.1). Deleting it
  pushes `catch (EventStreamUnexpectedMaxEventIdException)` into Application handlers.
- **`ReadAllAsync` is the one method with no shared equivalent, and has zero production
  callers** — it exists on the interface, in `MartenEventStore`, and in seven test fakes.
  `IReadOnlyEventStore.QueryEventsAsync(EventQuery)` is the portable alternative but is
  flat filters plus page-number paging, with no sequence cursor and so no replay
  ordering guarantee. Decide deliberately: keep it as a per-store method, or question
  whether LADR-0001 §4.10's replay path should have an unused port at all.
- **Verify `AppendMode = Rich` behaves identically on the target store before relying on
  it.** The compliance suite covers `FetchForWritingCompliance`; our path is the raw
  `Append(id, version, events)` overload, and `MartenEventStore.cs`'s header records that
  Marten 9's default Quick mode fails it in a non-obvious way.
- **`IPeopleQuery` / `IClassLibraryQuery` / `ICompetitionsQuery` / `IEntryQuery` should
  survive this story unchanged.** With a store-agnostic `IDocumentSessionFactory` there
  is now a case for collapsing them into the handlers, but LADR-0001 §4.2's reasoning
  ("this follows from hexagonal dependencies pointing inward, independently of
  portability") still holds and is the deciding one. Flagged so it is refused
  deliberately rather than rediscovered.

## Plan

Contract surface confirmed by reflection over the actual `JasperFx.Events` 2.48.0 and
`Marten` 9.24.0 assemblies, not from documentation:

- `Marten.IDocumentStore : IDocumentSessionFactory<IDocumentSession, IQuerySession>`
  (and so `IDocumentSessionFactory`), `Marten.IQuerySession : IDocumentReadOperations`,
  `Marten.IDocumentOperations : IDocumentWriteOperations`,
  `Marten.IDocumentSession : IDocumentSessionOperations`,
  `Marten.Events.Projections.IProjection : IJasperFxProjection<Marten.IDocumentOperations>`.
- `IDocumentReadOperations` gives `Query<T>()` and `LoadAsync<T>(Guid|string)`;
  `IDocumentWriteOperations` adds `Store<T>`; `IDocumentSessionOperations` adds
  `SaveChangesAsync`. `DocumentQueryableExtensions` gives `ToListAsync` /
  `FirstOrDefaultAsync` / `CountAsync` / `AnyAsync`.
- `JasperFx.Events.IEventOperations` carries `StartStream(Guid, IEnumerable<object>)` and
  the version-checked `Append(Guid, long, IEnumerable<object>)`;
  `IQueryEventStore` carries `FetchStreamAsync(id, version, timestamp, fromVersion, token)`
  and `FetchStreamStateAsync`.

**The one gap the story did not anticipate:** the `.Events` accessor is *not* on any
shared session contract. `Marten.IQuerySession.Events` returns `Marten.Events.IQueryEventStore`
and `Marten.IDocumentOperations.Events` returns `Marten.Events.IEventStoreOperations` —
both of which *derive from* the JasperFx contracts, but reaching them from an
`IDocumentSessionOperations` requires a per-store step. WI-4 handles this with three
abstract members on a shared base rather than by wrapping anything.

- **WI-1 — Bump Marten 9.22.2 → 9.24.0.** `Directory.Packages.props`. Pulls
  JasperFx.Events 2.48.0. Prerequisite for everything below.
- **WI-2 — The four query adapters become store-agnostic.** Constructor takes
  `IDocumentSessionFactory`; `using Marten;` becomes `using JasperFx.Events.Documents;`.
  Renamed off the `Marten*` prefix (`DocumentPeopleQuery`, `DocumentClassLibraryQuery`,
  `DocumentCompetitionsQuery`, `DocumentEntryQuery`) — a class with no Marten type left in
  it should not be named for Marten.
- **WI-3 — The four projections become `IJasperFxProjection<TOps> where TOps :
  IDocumentWriteOperations`.** The fold body is shared; a per-store shim
  (`Marten*Projection : ...<IDocumentOperations>, Marten.IProjection`) carries the
  registration marker.
- **WI-4 — `MartenEventStore` splits.** `JasperFxEventStore` holds the portable body
  (`Guid.Empty` guards, `StartStream` / version-checked `Append` / `FetchStreamAsync` /
  `FetchStreamStateAsync`, the Tombstone filter). `MartenEventStore` overrides only:
  the two `.Events` accessors, the append-exception translation
  (`ExistingStreamIdCollisionException` + the `PostgresException` 23505 walk), and
  `ReadAllAsync`.
- **WI-5 — Composition root.** `AddSoarscoreInfrastructure` registers
  `IDocumentSessionFactory` alongside `IDocumentStore`; `MartenConfig` registers the
  Marten projection shims. Both stay per-store by design ("Before starting").
- **WI-6 — Verify against a real PostgreSQL.** Two things must not be taken on trust:
  that `EventAppendMode.Rich` still backs the version-checked `Append` after the bump,
  and that the shared `LoadAsync<T>(Guid)` resolves documents whose id is a
  strong-typed value (`PersonId`, `EntryId`, …) — Marten's own `LoadAsync<T>(TId)` is
  what the projections call today precisely because the wrong id type throws
  `DocumentIdTypeMismatchException`. If the Guid overload does not work, WI-3's
  projections keep a per-store load step and this plan says so.
- **WI-7 — Amend LADR-0001 §5** and reconcile `tech-debt.md` /
  `deferred-decisions.md`.

### Decision — `ReadAllAsync` stays, as a per-store method

Recorded rather than left implicit ("Before starting"). `IReadOnlyEventStore.QueryEventsAsync`
is not a substitute: flat filters and page-number paging give no sequence cursor, so no
replay ordering guarantee, which is the entire point of LADR-0001 §4.10's replay path.
Deleting the port instead is a bigger question than this story — it changes what
`IEventStore` promises — and would have to be argued on its own terms, not smuggled in
as a side effect of a portability refactor. So: keep the method, keep it abstract on
`JasperFxEventStore`, implement it per-store. Entered in `deferred-decisions.md`.

### Property-based testing

No new invariant here. This story changes *which types* express the adapter, not what it
computes — the existing store-backed suite in `tests/Soarscore.Infrastructure.Tests` is
the right instrument, and it must pass unchanged. A behaviour change that made a property
test worth writing would be a bug in this story, not a feature of it.

## Outcome — as built, 2026-08-16

All seven work items done. Build clean (0 warnings), full suite green:
Domain 271, Application 175, Architecture 7, Infrastructure 34 (all `Category=Storage`,
against a real PostgreSQL via Testcontainers), Acceptance 8. Total 495.

### Two gaps the story's premise did not survive

Both were found by building it, and both are now per-store seams of the same shape —
a `protected abstract`/`virtual` member on a shared base, overridden by a small
store-specific subclass. Neither is a wrapper.

1. **The `.Events` accessor is not on any shared session contract.** `Marten.IQuerySession.Events`
   and `Marten.IDocumentOperations.Events` return Marten types that *derive from*
   `JasperFx.Events.IQueryEventStore` / `IEventStoreOperations`, but reaching them from a
   shared `IDocumentSessionOperations` needs a per-store step. `JasperFxEventStore` takes
   it as two abstract members.

2. **The shared `LoadAsync<T>(Guid)` cannot load a document whose id is a strong-typed
   value.** `IDocumentReadOperations` exposes Guid and String identity overloads only —
   deliberately — and Marten binds those to its own `LoadAsync<T>(Guid)`, which statically
   binds `TId` to `Guid` and throws `DocumentIdTypeMismatchException`. Inferred from the
   shipped metadata first, then **confirmed empirically** when the acceptance suite failed
   on `PersonSummary`/`PersonId`. Three of the four projections therefore keep a per-store
   load override; `ClassDefinitionSummaryProjection`, whose id is a bare `Guid`, does not.

So "the four projection bodies become store-agnostic outright — every member they use is
on the shared contracts" is **false as written**. The fold logic is shared; the load step
is not. This matters beyond this story: it is a limit of the shared contract, not a Marten
quirk, so any backend storing these documents under a strong-typed id needs the same
override — and LADR-0001 §8's cost estimate should say so.

### Also done, not in the original plan

- **`LayerRuleTests` now excludes `JasperFx` alongside `Marten`** for both Domain and
  Application. The contracts being store-agnostic makes them tempting to reach for in
  `Application`; §4.2 refuses that on dependency-direction grounds, and the rule now
  enforces the refusal instead of leaving it to judgement.
- **Projection registrations pin their name explicitly**
  (`opts.Projections.Add(new MartenPersonSummaryProjection(), Inline, "PersonSummaryProjection")`).
  Marten derives a projection's registered name from the instance's type, so introducing
  the shims would have silently renamed all four. That name is the async-daemon
  progression key and the handle `RebuildProjectionAsync` takes.
- **The four query adapters are renamed** `Marten*Query` → `Document*Query`, since no
  Marten type remains in them.

### Two collisions worth knowing about

Both are in the header comments of the files affected, but they generalise:

- `JasperFx.Events.IEventStore` collides with `Soarscore.Application.IEventStore`.
- `Marten.QueryableExtensions.ToListAsync<T>` and
  `JasperFx.Events.Documents.DocumentQueryableExtensions.ToListAsync<T>` are ambiguous
  when both namespaces are imported. `MartenEventStore.cs` does not import the JasperFx
  one and writes its two override parameter types out in full instead.
