# Story — UI prerequisite: typed OpenAPI responses and strict numbers

**Status:** Backlog · **Raised:** 2026-10-02 · **Source:** `SoarscoreUI/kanban/blocked/ss_engine-prerequisites.md` item 4 + `SoarscoreUI/SOARSCORE2-HANDOVER.md` §4 · **Engine observed at:** `843ef25`

## What

Declare every endpoint's `200` response body and `ProblemDetails` errors in `/openapi/v1.json`, and type numbers as numbers.

Verified facts (re-verify before acting):

- `MapCommand`/`MapQuery` return `IResult` with no `Produces` metadata (`src/Soarscore.Api/Routing/EndpointRouteBuilderExtensions.cs:16-39`); repo-wide grep for `Produces|WithOpenApi` under `src/Soarscore.Api` returns zero matches. At `843ef25`: 59 operations, 0 with a `200` schema.
- Handover-tested fix: `.Produces<TResult>().ProducesProblem(StatusCodes.Status400BadRequest)` on both helpers → 59/59 typed `200`s, schemas 127 → 202.
- `ConfigureHttpJsonOptions` in `src/Soarscore.Api/Composition.cs:71-78` sets only `AllowOutOfOrderMetadataProperties`; no `NumberHandling` anywhere. Web JSON defaults' `AllowReadingFromString` produced 140 `number | string` unions in `openapi-typescript` output; `JsonNumberHandling.Strict` beside existing settings took it to 0 (scratch-copy verified).
- Caveat: `Produces<TResult>` does not describe the `{value, warnings}` envelope on draw commands — needs a stated convention or `oneOf`.

## Why it matters

SoarscoreUI generates its API client from this document and fails CI on drift. Without response schemas every response type is hand-written and drifts silently. This is the UI's "do first" item and unblocks `generated-api-client.md`. It is also the lowest-risk item — do it before the roster/auth work.

## Before starting

- Re-run the counts (operations, typed 200s, `number | string` unions with `openapi-typescript@7.13`).
- Decide the `{value, warnings}` envelope documentation; optionally document 401/403/404/409 and publish the document as a build-time CI artifact.
- House rule 2 cross-check: NFR surface unaffected; scoring rules do not govern this. No `/docs` change without owner approval.
- Run full suites on both stores (`postgres` + `sqlite`) — strict numbers reject numbers-sent-as-strings.

## Acceptance

- [ ] `openapi-typescript` against `/openapi/v1.json` gives a typed `200` body for every endpoint.
- [ ] Generated types contain no `number | string` unions.
