# Story — UI prerequisite: CORS origins for SoarscoreUI

**Status:** Backlog · **Raised:** 2026-10-02 · **Source:** `SoarscoreUI/kanban/blocked/ss_engine-prerequisites.md` item 3 + `SoarscoreUI/SOARSCORE2-HANDOVER.md` §3

## What

Allow SoarscoreUI's origins to call the API from the browser: add `https://soarscore-ui.fly.dev` in production and `http://localhost:5174` in the development example settings.

Verified facts (re-verify before acting):

- `CorsOrigins()` reads `Soarscore:Cors:Origins[]` first, falls back to `SOARSCORE_CORS_ORIGINS` comma-separated only when the array is empty; empty = no policy (`src/Soarscore.Api/Composition.cs:229-234,249-252,411-433`). Policy is `WithOrigins(...).AllowAnyHeader().AllowAnyMethod()`, deliberately no `AllowCredentials` (`:223-233`).
- Current: `fly.toml:14-15` = `SOARSCORE_CORS_ORIGINS='https://ndcscore.fly.dev'`; `appsettings.json:9-11` = `[]`; `appsettings.Development.json(.example):4-6` = `["http://localhost:5173"]`.
- Observed: configured origin → `204` with `Access-Control-Allow-Origin` echoed; unlisted → `204` without headers (browser blocks).
- `kanban/deferred-decisions.md:510` left direct-SPA CORS to the front-end story; SoarscoreUI LADR-0001 has now decided direct SPA, no BFF. SoarscoreUI pins Vite at 5174 to avoid colliding with 5173.

## Why it matters

Blocks all deployed use (browser calls from `https://soarscore-ui.fly.dev`). Local dev against `mock` auth is not blocked. Config-only change.

## Before starting

- Confirm the Fly app name in `SoarscoreUI/fly.toml` (`soarscore-ui` → origin above); if the owner renames it, use that value instead.
- Note the precedence trap: document that a local `appsettings.Development.json` array beats the env alias — devs must add index `1` or set `Soarscore__Cors__Origins__1`.
- House rule 2: no rule-corpus engagement; this is the deferred front-end CORS decision landing.

## Acceptance

- [ ] Preflight and actual requests from both origins carry `Access-Control-Allow-Origin`.
