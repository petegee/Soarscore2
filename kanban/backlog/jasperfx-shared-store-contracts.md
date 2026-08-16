# Story — Move the store adapters onto the JasperFx shared contracts

**Status:** Backlog — no plan yet · **Raised:** 2026-08-16

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
