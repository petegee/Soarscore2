# Story — Ship on three stores: Fisher/SQLite, Marten/PostgreSQL, Polecat/SQL Server

**Status:** Backlog — no plan yet · **Raised:** 2026-08-16

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
