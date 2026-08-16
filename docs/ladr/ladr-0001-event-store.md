# LADR-0001 — Event store: PostgreSQL + Marten

**Status:** Accepted · **Date:** 2026-08-03 · **Amended (proposed):** 2026-08-16 — see §8

## Decision

The event store is **Marten on PostgreSQL**. It is the only adapter we build.

The architecture keeps a **SQLite adapter viable for a third party to write**, but we
do not write one, do not test one, and do not accept design cost to keep the option
open beyond the constraints listed in §4. "Swappable" here means *the domain does not
have to change*, not *the swap is free*.

## 1. Why Marten

- **MIT.** Marten 9.22.x, targets `net10.0` (net8.0 dropped), PostgreSQL 13 minimum,
  15+ recommended. Actively released. Satisfies the open-source constraint.
- **We write no store code.** The hand-rolled SQLite alternative was costed at
  ~1,400 LOC / 7–10 focused days, plus a long tail of SQLite-specific footguns
  (deferred-transaction lock upgrade, WAL checkpoint starvation, decimal type
  affinity, timestamp lexical ordering).
- **`Live` aggregation is our architecture's rule expressed in Marten's vocabulary.**
  `high-level-architecture.md` mandates that query-by-aggregate-ID folds the stream.
  That is `Live`, exactly, with no storage and no daemon.
- **Ordering correctness comes for free.** On PostgreSQL, `bigserial` allocates at
  INSERT, not commit, so a naive `WHERE seq > @checkpoint` projector can permanently
  skip an event that commits late. Marten's high-water-mark agent exists for this.
  A hand-rolled Postgres store would have to solve it; a hand-rolled *SQLite* store
  does not have the problem at all (single writer ⇒ commit order == position order),
  which is precisely why SQLite code cannot be lifted to Postgres unexamined.

The counter-case, recorded because it is real: at ≤40 pilots, ≤8 rounds/day, and the
read-model inventory in §3, we use roughly 15% of Marten. The deciding factor is not
capability — it is that "install Docker, run a Postgres container" is an acceptable
ask of self-hosters, and writing a store is not an acceptable use of the schedule.

## 2. What we use, and what we deliberately do not

| Marten feature | Use? | Why |
|---|---|---|
| `Live` projections | **Yes** | The by-ID path. Fold the stream, always. |
| `Inline` projections | **Yes** | The four cross-stream indexes (§3), in the append transaction. |
| `Async` daemon | **No** | No external subscribers, no message bus, four tiny indexes. Never started. |
| Snapshots / `Inline` single-stream aggregates | **No — permanently** | Entry streams are 20–60 events; Competition streams low hundreds. Folding is sub-millisecond. A snapshot taken before an upcaster is a correctness liability, not an optimisation. |
| Document store (`mt_doc_*` for domain objects) | **No** | Events are the state. Documents only as projection storage. |
| Multi-tenancy, archival, partitioning | **No** | Not at this scale. |

Inline, not async, is **required** rather than merely convenient: `aggregate-roots.md`
enforces Person email uniqueness at the unique-index level. On an async projection
that guarantee is false — two concurrent registrations both succeed at the write side
and the projection fails afterwards with no way to reject either. Inline also gives
read-your-own-writes, which the POST-command-then-GET-query API shape needs.

## 3. Read models — the complete inventory

Projections are an index of *which streams exist where*. They are never a source of
truth and are always fully rebuildable from the log.

| Read model | Why it must exist |
|---|---|
| `people` | Query by email/name; **unique index on email is a real invariant**. |
| `competitions` | List by date, class, state. |
| `class_library` | The library adopted from. |
| `entry_index` | Entry → competition, task-round, group, competitor. ≤640 rows per competition. |

Everything else loads a stream. The draw is **not** a read model — it lives inside the
Competition aggregate.

**Scores are never projected.** `ScoringService` derives results from `AdoptedRules`
plus raw Entry data. Materialising a leaderboard would re-implement the Competition
Class model outside its one place — a breach of the core architectural law and NFR-1.
A leaderboard query resolves `entry_index` to a set of stream ids, loads them, and
scores. Group membership resolves through the same index.

## 4. Constraints that keep a SQLite adapter possible

These are binding on our code regardless, because most of them are also just correct.

1. **The port is narrow and total.** `AppendAsync(streamId, expectedVersion, events)`,
   `ReadStreamAsync(streamId, fromVersion)`, `ReadAllAsync(fromPosition, batchSize)`.
   No `IQueryable`, no Marten types above `Infrastructure`.
2. **Read access is via one query interface per read model**, defined in `Application`,
   implemented in `Infrastructure`. `IDocumentSession` does not appear in `Application`
   — this follows from hexagonal dependencies pointing inward, independently of
   portability.
3. **Projection fold logic is plain functions** in `Application`, wired into Marten
   `IProjection` shims by the adapter. The shim is portable ballast; the fold is not.
4. **Optimistic concurrency is the `(stream_id, version)` uniqueness constraint** —
   never read-check-write. Read-check-write is accidentally safe under SQLite's single
   writer and unsafe under MVCC; making the constraint the sole arbiter is what makes
   the append semantics identical on both.
5. **Never query into event JSON from SQL.** Payloads are opaque to the store. This
   single rule neutralises the whole SQLite-JSON vs `jsonb` divergence.
6. **Decimals are serialised as strings or scaled integers inside event JSON**, never
   as JSON numbers, never in a typed SQL column. Defends against JavaScript consumers
   parsing as `double` (this is a REST API for external integrators), `jsonb` numeric
   normalisation, and SQLite's NUMERIC type affinity silently converting a `decimal`
   to a `double` when the first 15 significant digits round-trip. Scores are derived
   and not stored; the exposure is `MeasuredValue.number` and `DeclaredResult.aggregate`.
7. **Domain-meaningful timestamps live inside the event payload**, not in a store
   column. Launch time and the working-time window are domain facts;
   `mt_events.timestamp` is an infrastructure fact. Also insulates us from whether a
   given Marten version preserves original timestamps on append during a replay.
8. **Logical event-type names and an explicit event schema version, from event #1.**
   Never persist CLR type names — Marten's `mt_dotnet_type` exists for compatibility,
   not as a good idea. Register via `StoreOptions.Events.MapEventType`. The upcaster
   chain itself can wait; the naming registry and version discriminator cannot, because
   retrofitting them means rewriting an immutable log.
9. **IDs are `Guid.CreateVersion7()`.** Time-ordered, good index locality.
10. **Read models are dropped and replayed, never migrated.** Change the shape, replay.
    There is no read-model migration tooling and there will not be.

## 5. What a swap would actually cost

Honest accounting, so nobody reads "swappable" as "free".

**Discarded:** the Marten adapter and its projection shims, the schema, and the
operational story. Marten's `mt_streams` / `mt_events` layout is not our layout, so
none of it transfers. Call it 1,000–1,200 LOC to rebuild against SQLite.

**Kept:** the domain layer, event contracts, all fold and apply logic, upcasters,
projection definitions, read-model shapes, the query interfaces, and the test suite
re-pointed at the new adapter. This is the hexagonal dividend and it is the part that
represents the actual investment.

**The migration itself is a replay** — read events in order, append through the new
adapter, rebuild projections — and it is *verifiable* in a way a CRUD migration never
is: re-derive every score on the target and diff against the source and against the
captured `DeclaredResult` aggregates. That comparison mechanism already exists for
other reasons.

**One trap, written down now:** a SQLite adapter may use a naive `position > checkpoint`
cursor because single-writer SQLite guarantees commit order equals position order.
That guarantee is a property of SQLite, not of event sourcing. Carrying such a cursor
back to PostgreSQL causes silent, permanent event loss.

## 6. When to revisit

- **More than one writer process** — rolling deploys, HA, horizontal scale. This is
  SQLite's hard ceiling (one writer per file; WAL does not work over network
  filesystems) and the reason a SQLite adapter is a self-hoster's single-process
  convenience, not a path we can follow.
- Marten moving a feature we depend on behind a commercial licence. The cores are
  open source and CritterWatch is the paid add-on; re-verify at each major upgrade.
- Full projection rebuild exceeding a few seconds on the shared instance.

## 7. Not decided here

The representation of a Competition Class definition — external notation, C# records,
fluent internal DSL, or JSON — is open and is the subject of the next ADR. Note that
§4.6 (decimals) and §4.8 (event versioning) apply to the adopted-rulebook payload
whatever that decision is, and that a class definition must be **data at rest** in the
adoption event for replay determinism to hold.

## 8. Amendment, 2026-08-16 — the swap cost in §5 is wrong

**Status: Accepted.** Nothing above is rewritten; §1–§7 stand as the reasoning that was
correct when it was written. This section records what has since changed and what it does
and does not overturn.

Revised 2026-08-16 after the adapter refactor was actually built
(`kanban/completed/jasperfx-shared-store-contracts.md`). The costings below are now
measured rather than estimated, and two of them moved: the projections are less portable
than first read, and `MartenEventStore` keeps three things per-store rather than one. The
conclusion is unchanged; the numbers behind it are firmer.

Accepted 2026-08-16 when the second backend actually shipped
(`kanban/completed/multi-backend-deployment.md`). Everything below was written from one
store plus the shipped metadata of another; it is now written from two stores running the
same suites. One claim did not survive that — see "The sixth seam" — and the seam count
went from five to six.

### What changed

Two things, neither of which existed when this ADR was accepted:

1. **JasperFx now defines the store surface as store-agnostic contracts.**
   `JasperFx.Events` (`IEventStoreOperations`, `IEventOperations`, `IQueryEventStore`,
   `IJasperFxProjection<TOperations>`) and, from **JasperFx 2.47.0**,
   `JasperFx.Events.Documents` (`IDocumentReadOperations`, `IDocumentWriteOperations`,
   `IDocumentSessionOperations`, `IDocumentSessionFactory`, plus
   `DocumentQueryableExtensions` supplying the async terminals through
   `IDocumentQueryExecutor`). Marten, Polecat and Fisher each implement these *as their
   own types* — no adapter and no wrapper on any of the three.

2. **Fisher exists.** A SQLite-backed document store and event store, MIT, by the same
   author, under the same JasperFx support contracts, enrolled in and passing the same
   32-suite / 272-test `JasperFx.Events.ComplianceTests` that holds Marten and Polecat
   accountable. 0.7.1 at the date of this amendment.

### What that overturns

**§5's accounting.** "Discarded: … Call it 1,000–1,200 LOC to rebuild against SQLite"
rested on the premise that the session and store types are Marten's. That premise is now
half false. A second backend is a NuGet package plus a composition root, not a store
implementation:

- The four query adapters (172 LOC) become store-agnostic outright — every member they
  use is on the shared contracts. Built and confirmed; they no longer name Marten at all,
  and are renamed `Document*Query` accordingly.
- The four projection bodies (172 LOC) become store-agnostic **except for the document
  load**. `IDocumentReadOperations` exposes Guid and String identity overloads only, by
  deliberate design, and Marten binds those to its own `LoadAsync<T>(Guid)`, which
  statically binds `TId` to `Guid` and throws `DocumentIdTypeMismatchException` for a
  document configured with a strong-typed id. Three of our four read models have one
  (`PersonId`, `EntryId`, `CompetitionId`), so they keep a four-line per-store load
  override; `ClassDefinitionSummary`, keyed by a bare `Guid`, does not. This is a limit
  of the shared contract rather than a Marten quirk — every backend storing these
  documents under a strong-typed id needs the same override.
- `MartenEventStore.cs` (161 LOC) is ~90% portable. What stays per-store is the two
  exception translations, **the two `.Events` accessors** — `Events` is not on any shared
  session contract, so reaching `IEventStoreOperations` / `IQueryEventStore` from an
  `IDocumentSessionOperations` takes a per-store step — and **`ReadAllAsync`**, the one
  port method with no shared equivalent (see §4.10 and the deferred-decisions entry).
- `MartenConfig.cs` and the DI wiring (158 LOC) stay per-store **by design**. `AddMarten`
  / `AddPolecat` / `AddFisher`, `StoreOptions`, `MapEventType`, `UniqueIndex` and
  projection registration are not shared and JasperFx is not trying to share them.

So: one shared adapter body plus a thin composition root per backend, not one build
switched by configuration — with the qualification that "shared body" means shared *logic*
with a handful of narrow per-store seams, not a body a second backend inherits untouched.
The seams are uniform in shape (an abstract or virtual member on a shared base, overridden
by a small subclass), and there are **six** of them: two `.Events` accessors, the append
exception translation, `ReadAllAsync`, the projection load, and `AppendExpectedVersion`.

### The sixth seam — `expectedVersion` is not the same argument on two stores

Found by shipping Fisher, and the one thing in this section that a reader must not skim.
The version-checked `Append(streamId, expectedVersion, events)` overload is on the shared
`JasperFx.Events.IEventOperations` contract, and the contract does not say what
`expectedVersion` means. **Marten reads it as the version the stream will hold AFTER the
append; Fisher reads it as the version it holds BEFORE.** Both established empirically,
each against its own running store; neither is documented by its package.

`kanban/completed/jasperfx-shared-store-contracts.md` recorded the Marten reading as a
property of the shared contract "so it holds for every store implementing it". That is
false, and it is false in the most dangerous available way: **answering it wrongly does not
throw.** A backend that had this inverted would silently either never fail the concurrency
check or always fail it, and every store-level test of ordinary appends would still pass.

So `JasperFxEventStore.AppendExpectedVersion(currentVersion, eventCount)` is `protected
abstract` with no default — a third backend is made to state its answer rather than
inherit one — and the stale-version test in `tests/Soarscore.Infrastructure.Tests`
fails loudly on either mistake, on every backend. The safety property itself is unchanged
on both stores: the guard runs inside the write transaction either way.

The general lesson is worth more than the specific bug. A shared interface that two
implementations satisfy is not evidence that they agree about semantics the interface does
not state, and the only reliable way to find out which is to run the same tests against
both. That is now what `Soarscore.Infrastructure.Tests` is for.

**§1's counter-case, in part.** "Install Docker, run a Postgres container is an
acceptable ask of self-hosters, and writing a store is not an acceptable use of the
schedule" was a choice between those two options. There is now a third, and the schedule
argument does not apply to it.

### What it does not overturn

- **§4's ten constraints, all of them.** They are why the rewrite is cheap rather than a
  rewrite. §4.5 (never query into event JSON from SQL) and §4.6 (decimals as strings or
  scaled integers) in particular have paid off exactly as they were written to.
- **§4.1's port.** `IEventStore` stays. Its justification was never portability — it
  returns `Result<T>` instead of throwing, rejects `Guid.Empty`, and translates store
  exceptions into domain failure codes.
- **§4.2's rule** that `IDocumentSession` does not appear in `Application`. A
  store-agnostic `IDocumentSessionFactory` now *could* appear there, which would collapse
  the four query interfaces and their adapters. It should not: §4.2's own stated reason
  is hexagonal dependencies pointing inward, independently of portability, and that
  reason is untouched.
- **§6's first bullet.** More than one writer process remains SQLite's hard ceiling — one
  writer per file, WAL unusable over network filesystems. Fisher does not move it; it
  documents it (its exclusive-append methods *fail* where Marten's *wait*, with the
  version guard still inside the write transaction). A SQLite deployment stays a
  single-process convenience.
- **§2 and §3.** The feature inventory and the four read models are unchanged, and every
  one of Fisher's deliberate permanent gaps — message bus, partitioning,
  `DaemonMode.HotCold`, Newtonsoft — is already outside §2.

### The decision, restated

**Marten on PostgreSQL remains the reference deployment.** What changes is the second
sentence of §Decision: a SQLite adapter is no longer "viable for a third party to write"
— it is a supportable target for us, gated on Fisher reaching 1.0, and Polecat makes SQL
Server a third on the same terms.

Two prerequisites, both recorded as work rather than decided here:

- ~~We are on Marten 9.22.2 → JasperFx.Events **2.37.0**. The document contracts are
  2.47.0. Marten 9.24.0 pulls 2.48.0, the version Fisher is built against. Nothing
  follows until that bump.~~ **Done, 2026-08-16.** Marten 9.24.0 / JasperFx.Events
  2.48.0, and the adapters are on the shared contracts —
  `kanban/completed/jasperfx-shared-store-contracts.md`.
- ~~A support claim for three backends means the acceptance suite runs against three
  backends. Separately, a Fisher-backed peer of `Soarscore.Infrastructure.Tests` removes
  the Testcontainers dependency from the storage suite — worth having independently of
  any deployment.~~ **Done for two backends, 2026-08-16.** Every store-backed test is
  written once against `IStoreFixture` and runs against both (72 tests, 36 per store, in
  one `dotnet test`); the BDD acceptance suite runs once per store, selected by
  `SOARSCORE_TEST_STORE`. Not a Fisher-backed *peer* suite but the *same* suite run twice
  — a peer would have proved Fisher works, where the point is that Soarscore works on
  Fisher. The SQLite half needs no Docker and runs in about a second.

### What a backend actually costs, measured

Replacing §5's "1,000–1,200 LOC to rebuild" and this section's own estimate. Fisher took:
one composition root (~110 lines, over half comment), one `JasperFxEventStore` subclass
answering four abstract members (~140 lines, likewise), four projection shims of 3–8 lines
each, one `switch` arm in `AddSoarscoreInfrastructure`, one package reference and one test
fixture. **No adapter body, no query adapter, no projection fold and no test assertion
changed.** Two things had to be discovered by building rather than by reading: Fisher does
not create its schema lazily, and `AppendExpectedVersion`.

Polecat/SQL Server is deliberately not built — see `kanban/deferred-decisions.md`. The
shape is proved by a second store; a third adds cost without adding evidence until someone
wants SQL Server.

Tracked at `kanban/completed/jasperfx-shared-store-contracts.md` and
`kanban/completed/multi-backend-deployment.md`.
