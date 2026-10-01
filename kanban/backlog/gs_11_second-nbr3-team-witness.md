# Story — Curate the second Nbr=3 team-standings witness

**Status:** Backlog · **Raised:** 2026-09-07

**Sequence:** GS 11/13 · Milestone 3: targeted team witnesses
**Refined:** 2026-10-01 · **Renamed from:** `second-nbr3-team-witness.md`.
**Dependencies:** Acquisition can start independently. Incorporation uses GS 02
complete results and GS 03 exact difference contracts; use GS 04 coverage tooling
when available. GS 10 is required only if adding a seed parallel-run team grain.

## Delivery and acceptance (2026-10-01 refinement)

1. Re-check the existing hunt and acquisition state before requesting a source.
   Obtain an actual competition export/configuration plus an independent team
   result report; a catalogue listing alone does not establish overlap.
2. Curate the competition and individual result snapshot, then transcribe the
   GS team ladder with identity, total, place and counted contributors.
3. Replay through the public API, assert the complete team population and all
   available fields, and record provenance/coverage in the corpus.

- [ ] A second distinct active competition satisfies the full overlap condition.
- [ ] Team expectations are externally evidenced and independently checked, not
  computed from SoarScore's own individual totals as the sole oracle.
- [ ] Totals, places/ties and contributor sets match exactly, or any supported
  difference is individually triaged under the applicable strict contract.
- [ ] Fixture validation and strict replay pass on SQLite and PostgreSQL.
- [ ] Lack of a suitable source remains explicit acquisition work; an inaccessible
  F3B fixture is not counted as an active second witness.

**Requirements:** `docs/users.md` trustworthy results; NFR teams-MVP amendment,
NFR-1/2 class-data law and the T1 deferral in `kanban/deferred-decisions.md`.
The team's GS configuration is the parity authority. Verify any rulebook claims
via `fai-rules`; no alternative classification policy is introduced here.

**Verification:** Add the real-data acceptance scenario and reuse the exact
team-field/population negative checks. Invariant: each counted member is eligible
and the externally declared contributor set/total/place is reproduced. Preserve
the original hunting context below and reconcile coverage on completion.

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
