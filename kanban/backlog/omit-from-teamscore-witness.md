# Story — OmitFromTeamScore=true witness fixture

**Status:** Backlog · **Raised:** 2026-09-03

## What

Source a real event that used GliderScore's `OmitFromTeamScore=true` — a team
member drawn alongside countrymen who never contributes — and curate it per
`kanban/in-progress/grow-corpus-team-parity-fixtures.md` WI-2C/WI-2D so the
ladder grain proves the non-contributor is excluded on BOTH sides. GS filters
omit rows out of the team table entirely
(`Rpt_Results_TeamResults_MOD.vb:341-346`, per the WI-1A transcription — the
omitted pilot appears nowhere in the Team Results report); our classification
excludes `Contributes=false` members (teams-mvp decision 8's protection-only
mapping); and the fixture's `expected-teams.json` `countedPilots` must
exclude the omit member, so the ladder grain shows both engines agreeing on
each team's score without them.

## Why it matters

Decision 8's protection-only mapping has zero corpus or source-extraction
sightings: the WI-2D hunt re-verified the zero on 2026-09-03 (every committed
`CompPilots.json` and `entries.json` under `tests/GliderscoreFixtures/`
greps `OmitFromTeamScore=true` count 0) and Pete's ask returned "no leads
yet". Without a witness the protection mapping stays implemented-but-
unexercised, and `GOLDEN-COMPARISON-STATE.md` must keep saying so.

## Before starting

- The hunt trail lives in
  `kanban/in-progress/grow-corpus-team-parity-fixtures.md` WI-2A/WI-2D
  records — read it first (permission-gate state, Pete's asks, and the
  constraint that the `OmitFromTeamScore` flag is only visible at
  export/triage, never in the catalogue or the download CSV).
- Curation follows `kanban/completed/grow-gliderscore-fixture-corpus.md`
  WI-3/4/8 plus the team-oracle requirement: `expected-teams.json` per the
  story's WI-1C spec (REQUIRED for a team-bearing overlap fixture — the
  WI-1D guard throws otherwise).
- PII sweep mandatory (all Pilots contact columns empty; names are GS's
  ZZ-prefixed test data), recorded in the fixture's `provenance.json` notes.
