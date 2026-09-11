# Soarscore

A scoring and running system for radio-control glider competitions — FAI classes
F3B, F3J, F3K, F5J, F5K, F5L, F3F and the NZMAA national classes (ALES 200,
ALES 123, Radian and others).

Soarscore manages master data, competition setup, fair round-by-round draws,
raw score capture, score calculation, and results reporting. It is the headless
core kernel of a wider competition system: it offers no UI of its own and is
consumed strictly through an intent-based REST API, leaving displays, score
entry devices and integrations to whoever builds on it.

> **Status:** under active green-field development. There are no current users
> and no existing data to preserve or migrate.

## Why

Several times a year, national RC soaring competitions are run under rulebooks
published by the FAI and national bodies. Every class scores differently —
tasks, metrics, precision, penalties, landing tables, launch heights, drop
rules, normalisation — yet the machinery of running a contest (people,
entries, draws, groups, rounds, results) is the same. Soarscore separates the
two: the machinery is generic code; every class's variation lives in a
data-driven **Competition Class** definition the system reads and interprets.

## Design in brief

- **Headless core** — HTTP/REST is the only adapter; POST commands and GET
  queries, verbs not nouns, no PUT/PATCH/DELETE.
- **Hexagonal architecture** — `Domain` → `Application` → `Infrastructure` /
  `Api`, dependencies pointing inward; the Domain depends on nothing outside
  the BCL (enforced by architecture tests).
- **Event-sourced, CQRS** — an append-only immutable event log is the state of
  record; read models are projections. The event store is pluggable:
  Marten/PostgreSQL or Fisher/SQLite, chosen at composition time.
- **One class model** — all variance between competition classes (tasks,
  metrics, scoring terms, penalties, landing tables, drop rules, normalisation,
  fly-off phases) is encapsulated in a class definition. The core never
  branches on a specific class; adding a class is adding data, not code.
- **Validated at adoption** — a class definition is statically checked once,
  on adoption; a Competition cannot exist holding an invalid one.
- **No imposed ordering on score capture** — the contest is never gated on
  scores being up to date; results derive from what is present.
- **Immutable event log as audit** — no auth, no sign-off; club-scale trust
  model (≤ ~20 pilots, 1–2 day events).

## Repository layout

| Path | What it is |
|---|---|
| `src/Soarscore.Domain` | Aggregates as immutable state + `Apply` folds, `Result`-returning decide functions, the scoring engine. BCL-only. |
| `src/Soarscore.Application` | Hexagonal core: `IDispatcher`, command/query handlers, ports (`IEventStore`, `IPeopleQuery`, `IClock`), read-model projections. Domain-only. |
| `src/Soarscore.Infrastructure` | Event-store adapters (Marten/PostgreSQL, Fisher/SQLite), selected at composition time by `Soarscore:Store`. |
| `src/Soarscore.Api` | ASP.NET Core Minimal API front door. `MapCommand`/`MapQuery` is the only routing surface. |
| `tests/` | Unit/property tests (`Domain`, `Application`), architecture rules (`Architecture`), store-backed tests run against every backend (`Infrastructure`), and end-to-end Gherkin-style acceptance tests (`Acceptance`). |
| `tools/Soarscore.SeedData` | Seven FAI classes and the NZ national classes authored in C#, with canonical JSON emitted to `tools/Soarscore.SeedData/json/`. The seed corpus is the model's test: anything it cannot express is a gap. |
| `docs/` | Domain glossary, class diagram, architecture, NFRs, LADRs, and the rule knowledge base (`docs/rules/`, read-only). |
| `kanban/` | The work board — `backlog/`, `in-progress/`, `completed/`, `blocked/`, plus `tech-debt.md` and `deferred-decisions.md`. |

## Building and testing

Requires the .NET 10 SDK.

```sh
dotnet build Soarscore.sln
dotnet test Soarscore.sln --filter "Category!=Storage"   # fast loop, no Docker
dotnet test Soarscore.sln                                # full suite incl. PostgreSQL (Testcontainers)
```

Store-backed tests are written once against `IStoreFixture` and run against
every supported backend: a real PostgreSQL via Testcontainers (tagged
`Category=Storage`) and a Fisher/SQLite temp file (untagged, in the fast loop).
Acceptance tests run against one store per run, selected by
`SOARSCORE_TEST_STORE` (`postgres`, the default, or `sqlite`) — proving a
backend means running the suite once per store.

## Running

```sh
dotnet run --project src/Soarscore.Api
```

The store is selected at runtime by `Soarscore:Store` (`postgres` | `sqlite`).
Defaults to SQLite (`soarscore.db`); for PostgreSQL set the connection string,
e.g.:

```sh
SOARSCORE_STORE=postgres \
ConnectionStrings__Soarscore="Host=...;Database=...;Username=...;Password=..." \
dotnet run --project src/Soarscore.Api
```

At startup the API seeds the frozen class corpus (the FAI and NZ class
definitions emitted by `tools/Soarscore.SeedData`) through the ordinary
publish command — idempotent by content hash, safe on every boot; disable with
`Soarscore:SeedCorpus=false`.

### Docker

The image defaults to SQLite and persists its database in the `/data` volume:

```sh
docker build -t soarscore .
docker run -p 8080:8080 -v soarscore-data:/data soarscore

# PostgreSQL instead:
docker run -e SOARSCORE_STORE=postgres \
  -e SOARSCORE_CONNECTION_STRING="Host=...;Database=...;Username=...;Password=..." \
  ...
```

A maintained instance is deployed to Fly.io (see `fly.toml`).

## Documentation

- [`docs/soaring-domain-glossary.md`](docs/soaring-domain-glossary.md) — start
  here for domain concepts
- [`docs/soaring-domain-class-diagram.md`](docs/soaring-domain-class-diagram.md)
  and [`docs/competition-class-notation.md`](docs/competition-class-notation.md)
  — the Competition Class model and how to write one by hand
- [`docs/high-level-architecture.md`](docs/high-level-architecture.md) —
  architecture principles
- [`docs/aggregate-roots.md`](docs/aggregate-roots.md) — the aggregates
- [`docs/non-functional-requirements.md`](docs/non-functional-requirements.md)
  — NFR-1 … NFR-4
- [`docs/ladr/`](docs/ladr/) — architecture decision records
- [`docs/rules/`](docs/rules/) — the rule knowledge base (FAI and NZMAA), with
  verbatim official text in `docs/rules/source-docs/`
- [`docs/users.md`](docs/users.md) — who uses the system and why

## Licence

Open source is a design goal and all dependencies are open source; a licence
has not yet been chosen.
