# Story — webmine agent-skill wrapper

**Status:** Backlog · **Raised:** 2026-08-27

Split out of `kanban/completed/gliderscore-webmine-tool.md` WI-5 ("agent skill
wrapper — separate small story if it grows").

## What

An agent skill (`.claude/skills/…`, sibling of `fai-rules`) that wraps the
delivered webmine tooling so an agent can run `mine-catalogue` / `fetch-comp`
under the same guardrails without reading the sources first:

- restates the safety contract in the skill body: read-only allowlist,
  ≥1 s throttle floor, one comp per invocation, delete-finaliser, audit log
  mandatory for any live call;
- documents the permission gate state and refuses live invocation guidance
  while it is ungranted (offline/triage work needs no gate);
- exposes the two CLIs plus artifact locations (`comps.json|csv`,
  `<CompID>_triage.json`) and how triage output feeds gap-hunting against
  `tests/GliderscoreFixtures/index.md`.

## Why it matters

Lets agents assist corpus acquisition with zero chance of improvising requests
outside the kernel, and keeps the etiquette/volume discipline in one reviewed
artifact instead of per-session judgement.

## Before starting

- [ ] Permission email outcome known — if blessing is granted, encode the
      agreed volume limits verbatim in the skill text.
- [ ] First permitted live run has validated the eScoring scraper on real
      pages (synthetic fixtures only until then) — the skill must not present
      task scraping as proven.
