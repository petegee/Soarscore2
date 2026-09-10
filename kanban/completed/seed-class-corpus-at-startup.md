# Story — Seed the class corpus into a deployment at startup

**Status:** Completed 2026-09-11 · **Raised:** 2026-09-11 · **Planned:** 2026-09-11

## What

A freshly created store has no published class definitions, so a new deployment
cannot run a competition until the class catalogue exists. Ship the frozen seed
corpus (`tools/Soarscore.SeedData/json/*.json` — the sixteen FAI and NZ class
definitions, tapes excluded) inside the application image and publish it on
startup, dispatching each file through the existing `PublishClassDefinition`
command — LADR-0002 §1's "same door as user classes".

Gated by configuration (`Soarscore:SeedCorpus`, default on) so a deployment that
manages its own catalogue can turn it off.

## Why it matters

- The target deployment is a club-level tool run on a secretary's laptop or a
  single `docker run` — there is no CI/CD pipeline guaranteed to exist that
  could seed instead. The system must bootstrap itself (LADR-0001 §6).
- `PublishClassDefinitionHandler` is idempotent by design: the stream id is the
  content hash and `eventStore.streamAlreadyExists` returns success. Re-seeding
  on every boot is a safe no-op after the first run, so startup is the simplest
  correct place — no one-off migration step to forget.
- Without it, the container built by the repo's Dockerfile starts useful-but-
  empty: queries work, but no class can be adopted into a competition.

## Design notes (settled while raising)

- **Tapes are not deployment data.** No ingestion command exists for
  `TapeDefinition`; the tape catalogue backs property tests only. Only
  `json/*.json` (flat, non-recursive) is shipped and seeded.
- **Fail fast on corpus defects.** The corpus is frozen and test-verified
  byte-exact by the seed tool; a definition failing `Validate` at startup is a
  programming error and should stop the app, not degrade it. This does not
  touch NFR-4, which is about score capture ordering, not catalog bootstrap.
- **Idempotent replays are expected, not exceptional.** The seeder logs what it
  published and treats `streamAlreadyExists` as the no-op the handler already
  returns — no special-casing above the command.
- **Authoring-drift guard (separate small job):** CI should run
  `dotnet run --project tools/Soarscore.SeedData` and `git diff --exit-code` the
  emitted JSON, so a C# seed edit cannot ship a stale corpus. Tracked as its own
  backlog item — see `kanban/backlog/ci-seed-corpus-drift-guard.md`.

## Plan (as built)

- **WI-1 — the seeder mechanics, in Application** (`src/Soarscore.Application/Seeding/ClassCorpusSeeder.cs`).
  Pure: walks a directory top-level (`*.json`, name-ordered — so `json/tapes/`
  is never seen), deserialises through `ClassDefinitionIngestion.Options` (the
  exact options a POSTed body binds through), and dispatches
  `PublishClassDefinition` via `IDispatcher` — the same `IDispatcher` the HTTP
  surface uses. Returns a `ClassCorpusSeedReport` (file name + content hash
  per definition); throws naming the file on malformed JSON or a failed
  publish. No logging dependency added to Application.
- **WI-2 — the host policy, in Api** (`src/Soarscore.Api/Seeding/ClassCorpusSeederHost.cs`).
  `IHostedService`: gate `Soarscore:SeedCorpus` (default true),
  directory `Soarscore:SeedCorpusDirectory` (default `<AppContext.BaseDirectory>/seed`).
  Missing directory → log a warning and skip (a bare `dotnet run` from the repo
  must still start); dispatch failure → propagate, so the host fails fast.
  Resolves the dispatcher from a created scope (handlers are Scoped —
  Composition.cs). Registered in Composition.cs: hosted services start before
  the generic web host, so the catalogue exists before Kestrel accepts a
  request.
- **WI-3 — the image ships the corpus** (`Dockerfile` + `.dockerignore`).
  `COPY tools/Soarscore.SeedData/json/ /app/seed/`; `.dockerignore` re-includes
  only the class JSON (tapes re-excluded).
- **WI-4 — unit tests** (`tests/Soarscore.Application.Tests/Seeding/ClassCorpusSeederTests.cs`,
  7 tests): corpus publishes through the real dispatcher/handler onto
  content-hash streams; report hashes match `ClassDefinitionHashing`; re-seed
  is a no-op (stream count unchanged); failed publish and malformed JSON both
  throw naming the file; missing directory throws; `tapes/` not enumerated.
- **WI-5 — acceptance proof** (`Features/SeedingTheClassCatalogue.feature` +
  `Steps/SeedingTheClassCatalogueSteps.cs`): `AcceptanceFixture` points the
  host at the repo corpus (`Soarscore__SeedCorpusDirectory`, same env-var
  mechanism as the connection string), so the whole suite now runs against a
  realistically seeded deployment; the scenario queries `GET /class-definitions`
  and asserts all sixteen corpus hashes are in the catalogue. Green on both
  backends (93/93 postgres, 93/93 sqlite).

## Verification

- Unit: 317/317 Application, 820/820 Domain, 7/7 Architecture, 80/80
  Infrastructure (fast loop, Storage excluded).
- Acceptance: 93/93 postgres and 93/93 sqlite.
- Container: image rebuilt; first boot logs
  `Class corpus seeded: 16 definition(s) from /app/seed` and
  `GET /class-definitions` returns 16; a second boot against a persisted
  volume still returns 16 (idempotent).
