# Story — CORS for the NdcScore companion SPA

**Status:** Complete — built and verified 2026-09-14 · **Raised:** 2026-09-14

## What

Config-driven CORS on the Soarscore API so the NDC companion SPA
(`~/Source/NdcScore`, React/TS) can call it cross-origin. Origins come from
configuration — `Soarscore:Cors:Origins` (array) or the flat env alias
`SOARSCORE_CORS_ORIGINS` (comma-separated), matching the
`SOARSCORE_STORE` / `SOARSCORE_CONNECTION_STRING` convention — and the feature
is **off when nothing is configured**: no origins means no CORS policy, no
middleware, byte-identical responses to today.

## Why it matters

NdcScore is a pure web client of this API (no BFF — settled with the user
2026-09-14): the SPA is served from its own origin (Vite dev server, or a
static host) and the API from another (localhost at home, Fly.io later).
Without CORS the browser blocks every command and query. CORS belongs on the
service, not in a proxy: the API is the single front door
(high-level-architecture.md), and the trust model is the club-level no-auth
one — no credentials are ever sent, so `AllowCredentials` is deliberately not
set.

## Before starting

- Read `docs/high-level-architecture.md` and LADR-0003 "Web / API host" —
  middleware additions must not create endpoints (the WI-2 route-shape
  reflection test enumerates EndpointDataSource; `UseCors` adds none).
- The API already serves OpenAPI at `/openapi/v1.json` and Swagger UI at
  `/swagger` in every environment; NdcScore generates its TS client from the
  same document. CORS must not gate those responses for same-origin callers —
  with the default policy only configured origins get headers, which is the
  existing behaviour for everyone else.

## Plan — as built

- **WI-1** — `Composition.Build` resolves origins via a private
  `CorsOrigins(IConfiguration)` (array key first, `SOARSCORE_CORS_ORIGINS`
  comma-separated second — the same flat-alias convention as
  `SOARSCORE_STORE`); the default policy (`WithOrigins` + any header/method,
  no credentials) and `app.UseCors()` are registered only when non-empty.
  `UseCors` sits after `UseStaticFiles`, before endpoints; it adds no
  endpoint, so the WI-2 route-shape reflection test is unaffected (7/7).
- **WI-2** — `appsettings.json` documents `Soarscore:Cors:Origins` (empty
  array by default — off is the normal case; same-origin callers need
  nothing).
- **WI-3** — `CorsPreflightSmokeTests` (4 facts, standalone SQLite factories,
  no Docker): preflight from a configured origin → 204 + origin echo; simple
  GET carries `Access-Control-Allow-Origin`; nothing configured → no CORS
  headers even with `Origin` present (byte-identical to pre-CORS behaviour);
  the `SOARSCORE_CORS_ORIGINS` alias resolves comma-separated, and an
  unlisted origin gets no header. The smoke-test GETs exercise
  `/class-definitions`, not `/people` — `FindPeople` answers 400
  `findPeople.noCriteria` without criteria, and a criteria-bearing URL would
  have made the CORS assertion hostage to unrelated binding rules.
- **WI-4 (discovered, not planned)** — the repo-root finders behind the seed
  corpus (`Directory.Exists(<root>/.git)`) all failed in a linked worktree,
  where `.git` is a *file* (gitdir pointer). Fixed in all five sites:
  `AcceptanceFixture.cs`, `SeedingTheClassCatalogueSteps.cs`,
  `CatalogueDrawPropertyTests.cs`, `SeedCorpusIngestionTests.cs`,
  `ClassCorpusSeederTests.cs`, plus `tools/Soarscore.SeedData/Program.cs`'s
  `FindRepoRoot`. Without this the user's chosen worktree workflow could not
  run any corpus-backed suite at all. The duplication itself is recorded in
  `kanban/tech-debt.md`.

## Verification

- `CorsPreflightSmokeTests` 4/4.
- Architecture 7/7; Domain 820/820; Application 325/325; Infrastructure
  (SQLite) 166/166; BDD acceptance suite `SOARSCORE_TEST_STORE=sqlite`
  97/97 — all on the worktree host.