# Story — OmitFromTeamScore=true witness fixture

**Status:** Backlog · **Raised:** 2026-09-03

**Sequence:** GS 12/13 · Milestone 3: targeted team witnesses
**Refined:** 2026-10-01 · **Renamed from:** `omit-from-teamscore-witness.md`.
**Dependencies:** Acquisition can start independently, and one suitable source
may also satisfy GS 11. Incorporation uses GS 02/03 and GS 04 when available.

## Delivery and acceptance (2026-10-01 refinement)

1. Source a real best-three team competition with an explicit omitted member
   and independent team-result evidence. Prefer an omitted pilot whose score
   would otherwise enter the counted set, so exclusion has a numerical witness.
2. Preserve the member's individual scores and draw/protection membership;
   map the GS flag to contribution eligibility through the public team commands.
3. Compare the complete external team ladder, including counted sets and totals,
   and the available individual result fields at the same snapshot.

- [ ] The source actually contains `OmitFromTeamScore=true`; it is not a synthetic
  edit presented as a historical observation.
- [ ] The pilot's individual result remains present as evidenced, while the
  pilot is excluded from the contributing set and team sum.
- [ ] Public read state confirms the expected protection/membership mapping;
  prescribed historical groups are not claimed to prove draw-generator fairness.
- [ ] A favourable omission that changes the best-three set is exercised, or the
  report explicitly limits the evidence to membership mapping and leaves that
  numerical-effect coverage gap open.
- [ ] External totals/places/contributors and whole team population are asserted;
  fixture validation and strict replay pass on both stores.

**Requirements:** `docs/users.md` trustworthy results and the NFR teams-MVP
amendment's per-member contribution eligibility; NFR-1/2 and the existing T1
decision stand. GS configuration is the parity authority. No new team policy or
rulebook amendment is introduced; consult `fai-rules` for any rule-derived claim.

**Verification:** Add a real-data acceptance witness and reuse exact team-oracle
checks. Invariant: a non-contributing member is absent from the counted set
without losing their independently evidenced individual result. Apply current
corpus redaction conventions; ZZ-prefixed names are an example, not a requirement
to falsify a new source. Report coverage through GS 04 rather than a separate
point-in-time status document.

## What

Source a real event that used GliderScore's `OmitFromTeamScore=true` — a team
member drawn alongside countrymen who never contributes — and curate it per
`kanban/completed/grow-corpus-team-parity-fixtures.md` WI-2C/WI-2D so the
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
unexercised; the corpus index must disclose the gap until it is witnessed.

## Before starting

- The hunt trail lives in
  `kanban/completed/grow-corpus-team-parity-fixtures.md` WI-2A/WI-2D
  records — read it first (permission-gate state, Pete's asks, and the
  constraint that the `OmitFromTeamScore` flag is only visible at
  export/triage, never in the catalogue or the download CSV).
- Curation follows `kanban/completed/grow-gliderscore-fixture-corpus.md`
  WI-3/4/8 plus the team-oracle requirement: `expected-teams.json` per the
  story's WI-1C spec (REQUIRED for a team-bearing overlap fixture — the
  WI-1D guard throws otherwise).
- PII sweep mandatory (all Pilots contact columns empty; names are GS's
  ZZ-prefixed test data), recorded in the fixture's `provenance.json` notes.
