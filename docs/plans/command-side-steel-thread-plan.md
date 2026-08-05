# Plan — Command-side steel thread: Person end-to-end

**Status:** Complete — WI-1 through WI-11 all landed and verified · **Date:** 2026-08-05

Work items are numbered `WI-n` and the numbers are stable — cite them from code
comments the way `People/Person.cs` and `Entries/Entry.cs` already cite WI-0 and
WI-4 of the fold-refactor and scoring-service plans.

## Context

Soarscore has a complete **read/derive** half and no **write** half.

Built: four aggregates as immutable state + `Apply` folds (`Person`, `Competition`,
`Entry`, `PublishedClassDefinition`), the whole scoring engine with property tests,
event-JSON conventions (`SoarscoreEventJson`), content hashing, and an eleven-class
seed corpus.

Missing: everything that turns an intent into an appended event. There is no `Api`
project. `Soarscore.Application` holds two files and no ports, dispatcher or handlers.
`Soarscore.Infrastructure` holds one stream-id helper and has never opened a Marten
session. The aggregates can *fold* `CompetitorRegistered` but nothing decides whether
one may be emitted — which is precisely the anemic domain
`docs/high-level-architecture.md` forbids.

This plan builds the thinnest vertical slice that proves the whole write path:

```
POST /register-person → IDispatcher → ICommandHandler → Person.Register (decide)
  → IEventStore.AppendAsync → Marten/PostgreSQL → Inline `people` projection
  → GET /people?email=…
```

**Why Person and not Competition.** Person is 99 lines and four events, so almost none
of the work is domain work — it is all plumbing, which is the point of a steel thread.
More importantly it is the only slice that exercises the **unique email index**, and
that invariant is the sole reason LADR-0001 §2 mandates Inline projections over the
async daemon. If the plumbing cannot enforce it in the append transaction, the store
decision is wrong, and that should surface now rather than after six handlers are built
on top of it. Person also has no dependency on `Validate()` — the sixteen adoption
checks that gate `CreateCompetition` and are not yet written.

**Outcome:** a running API, a real PostgreSQL-backed event store, a rebuildable read
model, layer rules enforced at build, and a handler/dispatcher skeleton that every
later command hangs off unchanged.

### Out of scope (deliberately)

- `Validate()` / the sixteen adoption checks — the next thread, and large enough to own one.
- Competition, Entry and ClassDefinition commands.
- Scoring endpoints. `ScoringService` already exists and is not touched here.
- The `competitions`, `class_library` and `entry_index` read models (LADR-0001 §3).
- OpenTelemetry, the nuget-license CI step (noted in WI-11, not built).

### Governing documents

`docs/high-level-architecture.md` (hexagonal, intent-based API, CQRS, event-sourced,
functional-like, Law of Demeter), `docs/ladr/ladr-0001-event-store.md` (§2 Inline, §3
read-model inventory, §4 the ten binding constraints),
`docs/ladr/ladr-0003-library-choices.md` (every library choice below is already decided
there — do not re-litigate).

**CLAUDE.md's core architectural law is not engaged by this thread** — Person carries no
class-specific anything — but the `IEventStore` port and dispatcher built here must stay
free of any competition-class knowledge.

---

## Phase A — Foundations

WI-1 and WI-2 are independent of everything else and of each other.

### WI-1 — Adopt the LADR-0003 test stack

The ADR names a stack that was never installed. Do it now, before the suite grows.

`Directory.Packages.props`:

| Package | Now | Target |
|---|---|---|
| `xunit` | 2.9.3 | **remove** |
| `xunit.v3` | — | **3.2.2** |
| `xunit.runner.visualstudio` | 3.0.1 | 3.1.5 |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | 18.8.1 |
| `coverlet.collector` | 6.0.2 | **remove** — LADR-0003 chooses `dotnet test --coverage` and explicitly rejects the Coverlet chain |
| `AwesomeAssertions` | — | 9.5.0 |
| `TngTech.ArchUnitNET.xUnitV3` | — | 0.13.3 |
| `Testcontainers.PostgreSql` | — | 4.13.0 |
| `Verify.XunitV3` | — | 31.28.0 |
| `CsCheck` | 4.7.0 | 4.8.0 |
| `Marten` | 9.22.2 | **leave at 9.22.2** — LADR-0001 §1 verified the MIT licence at exactly this version; bumping is a separate decision with a licence re-check |

Note `TngTech.ArchUnitNET.xUnit` (the v2 package) depends on `xunit.assert` 2.4.1 and must
**not** be used — the `xUnitV3` variant exists at the same version and is the correct one.

Both existing test csproj files: add `<OutputType>Exe</OutputType>` (xUnit v3 test projects
are executables), swap the package references. `using Xunit;` is unchanged in v3.

**WI-1b — assertion conversion (separable commit).** Convert `Assert.*` → `.Should()` across
the ~19 existing test files in `tests/Soarscore.Domain.Tests/` and
`tests/Soarscore.Application.Tests/`. Purely mechanical and the bulk of WI-1's diff; keep it
as its own commit so the package migration stays reviewable.

**Verify:** `dotnet test Soarscore.sln` green with the same test count as before.

**Uncertainty to settle during execution:** LADR-0003's `dotnet test --coverage` needs
Microsoft.Testing.Platform runner mode (`<UseMicrosoftTestingPlatformRunner>true</…>` plus
`Microsoft.Testing.Extensions.CodeCoverage`), which changes how the CI test step invokes the
runner. If it fights the VSTest path, ship WI-1 in VSTest mode and record coverage as a
follow-up rather than blocking the thread.

### WI-2 — Architecture tests

New project `tests/Soarscore.Architecture.Tests`, referencing all four production projects.
Two kinds of rule, both cheap and both guarding a stated law:

1. **Layer rules (ArchUnitNET).** Domain depends on nothing outside the BCL — no
   `Soarscore.Application`, `Soarscore.Infrastructure`, `Soarscore.Api`, no `Marten`,
   no `Npgsql`. Application depends on Domain only — in particular no `Marten` and no
   `IDocumentSession` (LADR-0001 §4.2). Infrastructure does not depend on Api.
2. **Route-shape reflection test.** Build the `WebApplication` in-memory, enumerate
   `EndpointDataSource`, assert every endpoint's HTTP methods ⊆ {GET, POST}
   (`high-level-architecture.md`, "intent-based"; LADR-0003 turns the rule into a failing
   build). This is not an HTTP test — nothing is served — so it stays inside "driven without
   HTTP testing tools". It depends on WI-8 and should be written last.

**Verify:** temporarily add `using Marten;` to a Domain file and confirm the build fails;
temporarily `MapPut(...)` and confirm the route test fails. Revert both.

---

## Phase B — The Application kernel

WI-3 gates everything after it.

### WI-3 — `Result<T>`, dispatcher, ports

**`Result<T>` goes in `Soarscore.Domain`, not Application.** WI-4's decide functions return
it, and Domain cannot reference Application. LADR-0003 names the type without assigning a
layer; Domain is the only placement that works. ~80 LOC, hand-rolled, no library.

Shape: success carries `T`; failure carries a stable machine-readable `code`, a human
message, and an optional `IReadOnlyList<Defect>` — the same `Defect` list LADR-0003's
`Validate()` will return, so the class-definition thread reuses this rather than inventing
a parallel error channel. Total and non-throwing.

**Dispatcher** (`Soarscore.Application`, ~60 LOC over `IServiceProvider`, per LADR-0003 —
MediatR is licence-blocked): `ICommand<TResult>`, `ICommandHandler<TCommand, TResult>`,
`IQuery<TResult>`, `IQueryHandler<TQuery, TResult>`, `IDispatcher`. No behaviour pipeline,
no decorators — inspectability is the stated reason it is hand-rolled.

**`IEventStore` port** (`Soarscore.Application`), exactly the three methods LADR-0001 §4.1
permits and no more. No `IQueryable`, no Marten types:

- `AppendAsync(Guid streamId, ExpectedVersion expected, IReadOnlyList<IDomainEvent> events)` → `Result<long>`
- `ReadStreamAsync(Guid streamId, long fromVersion)` → `Result<IReadOnlyList<IDomainEvent>>`
- `ReadAllAsync(long fromPosition, int batchSize)`

`ExpectedVersion` is a `readonly record struct` with `Any` / `NoStream` / `Exact(long)`.
Concurrency is the `(stream_id, version)` uniqueness constraint and never read-check-write
(§4.4).

**`IDomainEvent` marker** — a small additive change to `Domain/Shared.cs`, implemented by the
four existing abstract event bases (`PersonEvent`, `CompetitionEvent`, `EntryEvent`,
`ClassDefinitionEvent`). Gives the port a typed signature instead of `object`. Touches no
JSON: the `[JsonPolymorphic]` attributes and `$kind` discriminators are unchanged, and the
existing event-JSON tests must still pass byte-identically.

**`IClock`** (`Soarscore.Application`), with `SystemClock` in Infrastructure. Every event
carries an `At`, so handlers need an injectable clock for deterministic tests. LADR-0003:
hand-written fake, three lines, not NSubstitute.

**Verify:** unit tests for `Result<T>` and the dispatcher resolving a handler. No store yet.

### WI-4 — Person decide functions (Domain stops being anemic)

Add to `src/Soarscore.Domain/People/Person.cs`, alongside the existing `Create`/`Apply`:

- `static Result<PersonRegistered> Register(PersonId, string name, ContactDetails, ClubAffiliation?, DateTimeOffset at)`
- `Result<PersonRenamed> Rename(string name, DateTimeOffset at)`
- `Result<ContactDetailsChanged> ChangeContactDetails(ContactDetails, DateTimeOffset at)`
- `Result<ClubAffiliationChanged> ChangeClubAffiliation(ClubAffiliation?, DateTimeOffset at)`

**Decide functions return events; they never mutate and they never append.** The handler
appends. This is the pattern every later aggregate copies, so get its shape right here.

Invariants Person genuinely owns are modest: non-blank name, non-blank and structurally
plausible email. **Email uniqueness is emphatically not one of them** — `Person.cs` already
documents that it is enforced at the index level, because no single instance can check
itself against the population.

**Verify:** decide-function tests in `Soarscore.Domain.Tests` (blank name → failure with a
stable code; valid input → the expected event). Existing fold tests untouched and still green.

### WI-5 — `people` read model + query port

- `PersonSummary` document in Application: `Id`, `Name`, `Email`, `Phone`, `HomeCity`,
  `ClubName`. One of the four read models LADR-0001 §3 permits — do not add a fifth.
- `PeopleProjection.Apply(PersonSummary?, PersonEvent) → PersonSummary?` — a **plain static
  function in Application** (LADR-0001 §4.3). Marten's `IProjection` shim wrapping it lives
  in Infrastructure and is portable ballast; this function is not.
- `IPeopleQuery` in Application, implemented in Infrastructure (§4.2):
  `FindByEmailAsync`, `SearchByNameAsync`.

**`IPeopleQuery` must not have a get-by-id method.** `high-level-architecture.md` is explicit:
*"If querying by ID, then you must use load the stream."* `GetPerson` in WI-6 goes through
`IEventStore` and folds. The read model exists solely for the cross-stream lookups the stream
cannot answer. This is the single most likely thing for an implementing agent to get wrong.

**Verify:** fold tests for `PeopleProjection` — pure function, no store needed.

### WI-6 — Commands, queries and handlers

Commands: `RegisterPerson`, `RenamePerson`, `ChangePersonContactDetails`,
`ChangePersonClubAffiliation`. Queries: `FindPeople` (→ `IPeopleQuery`), `GetPerson`
(→ `IEventStore` + fold).

Handler shape for a mutation — the template for every later handler:

```
read stream → fold to current state → call decide → append with ExpectedVersion.Exact(version)
```

`RegisterPerson` mints the `PersonId` (`Guid.CreateVersion7()`, §4.9) and appends with
`ExpectedVersion.NoStream`.

**Duplicate email is not pre-checked.** The handler appends; the Inline projection's unique
index rejects the transaction; the Marten adapter (WI-7) translates the PostgreSQL unique
violation into a `Result` failure with a stable code. Pre-checking would be read-check-write,
which §4.4 forbids and which is racy under MVCC anyway. Making the constraint the sole arbiter
is the whole point.

**Verify:** Application tests driving `IDispatcher` with real handlers, a fake in-memory
`IEventStore` and a fake clock (LADR-0003's stated test entry point). No HTTP, no containers.

---

## Phase C — Adapters

### WI-7 — Marten event store (Infrastructure)

`MartenEventStore : IEventStore`. The only project allowed to reference Marten.

- **`StoreOptions.Events.MapEventType`** for all four Person events, using the `$kind` strings
  already declared on `PersonEvents.cs` as the logical names. LADR-0001 §4.8: never persist CLR
  type names. Retrofitting this means rewriting an immutable log, so it goes in from event #1.
- **Serializer**: Marten's `SystemTextJsonSerializer` configured from `SoarscoreEventJson.Options`
  — decimals as strings (§4.6) and `$kind` discriminators come along for free.
- **`PeopleProjection` shim**: `Inline`, wrapping the Application fold from WI-5.
  **Never register or start the async daemon** (§2) — Inline is required, not preferred.
- **Unique index on `PersonSummary.Email`**, in the same transaction as the append. This is the
  invariant the whole aggregate boundary rests on.
- **Error translation**: catch `PostgresException` SqlState `23505` and map to a `Result` failure.
  Map Marten's concurrency exception to a distinct failure code. Nothing Marten-shaped escapes
  this project.
- `AddSoarscoreInfrastructure(IConfiguration)` DI extension; connection string from environment
  (LADR-0003: `IConfiguration` + env vars).
- No snapshots, no document store for aggregates, no multi-tenancy (§2).

**Watch:** Marten serialises each event by its concrete type, so System.Text.Json will *not*
emit the `$kind` discriminator on the way in (STJ only writes it when serialising as the
declared base type). Reading back as the concrete type is fine, but a round-trip test asserting
the on-disk shape should confirm which of `mt_events.type` and `$kind` is actually carrying the
discrimination before anyone relies on the latter.

### WI-8 — `src/Soarscore.Api`

ASP.NET Core Minimal APIs on `net10.0`. Add to `Soarscore.sln` under the `src` folder.

- **`MapCommand<TCommand, TResult>(path)` and `MapQuery<TQuery, TResult>(path)` are the only
  routing surface exposed.** No raw `MapPost`/`MapGet` outside those two helpers, so registering
  a PUT is not something a later contributor can do by accident. WI-2's reflection test is the
  backstop.
- Routes — verbs, never nouns: `POST /register-person`, `POST /rename-person`,
  `POST /change-person-contact-details`, `POST /change-person-club-affiliation`,
  `GET /people?email=…&name=…`, `GET /person?id=…`.
- Query parameters bind via `[AsParameters]` record structs, never a request body on GET.
- `Result` failures → RFC 9457 `ProblemDetails` via `IProblemDetailsService`. One mapping in one
  place: failure code → status code.
- `Microsoft.AspNetCore.OpenApi` (pin to the 10.0.x matching the SDK); emit the spec as a build
  artefact, no hosted UI (NFR-3, LADR-0003).
- No auth — `CLAUDE.md`'s trust model. The immutable log is the audit story.

### WI-9 — Store-backed tests

New `tests/Soarscore.Infrastructure.Tests`, Testcontainers PostgreSQL, container disposed per
test class. Four tests carry real weight:

1. Append → read round-trip preserves event order and payload.
2. Stale `ExpectedVersion` is rejected — optimistic concurrency via the uniqueness constraint.
3. **Duplicate email is rejected in the append transaction, and the first registration survives**
   — the LADR-0001 §2 proof. If this fails, Inline vs async was decided on a false premise.
4. Read model is dropped and fully replayed from the log (§4.10) and lands identical.

CI already runs on `ubuntu-latest`, which has Docker. Trait these tests so they can be filtered
out of a fast local loop.

---

## WI-10 — End-to-end verification

```bash
dotnet build Soarscore.sln -c Release          # WI-2 layer rules run at build
dotnet test Soarscore.sln -c Release           # all suites incl. Testcontainers
docker run -e POSTGRES_PASSWORD=... -p 5432:5432 postgres:16
dotnet run --project src/Soarscore.Api
```

Then, against the running API:

- `POST /register-person` → 200, returns the id.
- `GET /people?email=…` → the registered person (read-your-own-writes, which is the other
  reason §2 requires Inline).
- `POST /register-person` with the same email → ProblemDetails, stable error code, and the
  original row intact.
- `GET /person?id=…` → same person, **served by folding the stream, not from the read model**.
- `POST /rename-person` → `GET` reflects it in both paths.
- Confirm the OpenAPI document lists only GET and POST.

---

## WI-11 — Housekeeping

- **`CLAUDE.md` repository map says "No application code yet"** — stale by ~2,500 lines even
  before this thread. Rewrite that section (the file invites this: *"If something here goes
  stale, fix it"*).
- Recorded here, deliberately **not** built: LADR-0003's nuget-license CI step and the
  `PublicAPI.Shipped.txt` baseline. Both are decided but belong with the class-definition
  thread, where the authoring vocabulary they guard actually lives.
- Do not touch anything else under `/docs` — house-keeping rules 3 and 4.

---

## Dependency order

```
WI-1 ─┐ (independent, parallelisable)
WI-2 ─┘  … route test in WI-2 waits on WI-8
WI-3 ─── gates everything below
  ├─ WI-4 (Domain decide)
  ├─ WI-5 (read model + query port)
  └─ WI-6 (handlers) ── needs WI-4, WI-5
WI-7 ── needs WI-3, WI-5
WI-8 ── needs WI-3, WI-6
WI-9 ── needs WI-7
WI-10, WI-11 last
```

## What this unlocks

`Validate()` and the sixteen adoption checks, then `PublishClassDefinition`, then
`CreateCompetition` — at which point the `class_library` and `competitions` read models join
the two already-built halves and the system can hold a real event. Every one of those hangs off
the WI-3 kernel without changing it.
