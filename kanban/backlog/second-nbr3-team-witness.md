# Story — Curate the second Nbr=3 team-standings witness

**Status:** Backlog · **Raised:** 2026-09-07

## What

When a second team-bearing export arrives (organiser `.mdb` export, or a
richer webmine zip), curate it as an active fixture meeting the ladder
grain's full overlap condition: `UseTeams=true`, `NbrForTeamScore=3`,
populated `CompPilots.Team`. Curation is the
`grow-corpus-team-parity-fixtures.md` WI-2C pipeline exactly, including its
`expected-teams.json` requirement (per that story's WI-1C spec — now also
enforced at curation time by `extract/validate.py` rule 5's team arm, Move 3).

## Why it matters

The team-parity claim rests on ONE Nbr=3 witness (f3j-international,
transcript-verified 8/8). f3b-international already satisfies all three
overlap conditions but sits behind its multi-task-round skip, making it the
latent second witness if multi-task rounds ever land.

## Before starting

- The webmine permission gate is still unticked (2026-09-03) — see the hunt
  log in `kanban/completed/grow-corpus-team-parity-fixtures.md` (WI-2A) for
  the drafted permission email and the constraint that `NbrForTeamScore` is
  visible only at export/triage, never in the catalogue or the download CSV.
- Curation follows `kanban/completed/grow-gliderscore-fixture-corpus.md`
  WI-3/4/8 plus the WI-2C checklist in
  `kanban/completed/grow-corpus-team-parity-fixtures.md` (PII sweep
  mandatory; `class-definition.json` re-derived from this comp's own
  `competition.json`; the GS Team Results transcript ask applies to any new
  Nbr=3 comp).
- `extract/validate.py` (rule 5 team arm) must PASS on the curated
  directory before the feature scenario is written.
