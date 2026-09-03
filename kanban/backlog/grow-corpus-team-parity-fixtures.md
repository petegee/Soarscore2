# Story — Team-parity fixtures: validate team results against GliderScore

**Status:** Backlog · **Raised:** 2026-09-03 (teams-mvp WI-9 landed 2026-09-02;
this story was opened the moment its own corpus facts made the gap explicit)

## What

Extend the golden-corpus parity claim from individual results to **team
results**, in three moves:

1. **First true GS team-standings parity witness.** The WI-9 team grain
   currently "pins the contract rather than GS's own team ladder" — no
   fixture carries GS team-standings output (`kanban/completed/teams-mvp.md`
   WI-9 corpus facts). f3j-international is already team-mapped (8 populated
   teams, `NbrForTeamScore=3` — the one fixture where the team grain runs);
   give it an independently recomputed GS team-ladder oracle, verbatim per
   `Rpt_Results_TeamResults_MOD.vb:303-468` (retain up to `NbrForTeamScore`
   eligible members by score, sum, rank — the same independent-recompute
   practice as the existing reconstructed-ladder oracles).
2. **Grow the corpus with team-bearing comps** (Nbr=3 preferred so the team
   grain can run), targeting the unexercised arms: a comp with real team
   standings in its report, and an `OmitFromTeamScore=true` witness
   (protection-only member — zero corpus or source-extraction sightings
   today).
3. **Amend rule 5's team framing.** `extract/validate.py` `gap_flags` still
   labels `UseTeams=true` a "(team scoring concept gap)" excusable only via a
   no-effect `triageJustification`, and `tests/GliderscoreFixtures/index.md`
   §Standing skip reasons still lists "team scoring" as a §6 concept gap.
   Both are stale since teams-mvp: a team-bearing fixture should activate
   *with* declared team-grain expectations (compare where `NbrForTeamScore==3`,
   T1-ledger where not) instead of merely being excused as inert. Series,
   prelim and merged-prelim flags are untouched.

**Out of scope:** widening the classification method beyond
`bestThreeScoreSum` — `NbrForTeamScore ≠ 3` stays ledgered (token `T1`,
`kanban/deferred-decisions.md`); jerilderie-2010 (14 real teams, Nbr=4) and
f3k-sample-comp (Nbr=2) unlock only via a future classification-policy story.
Draw-generation parity with GS's protected draws remains deliberately not a
goal — protection maps through the adapter for the replay gate only
(`teams-mvp.md` decision 8).

## Why it matters

Teams and protection landed 2026-09-02, but the parity claim's confidence
statement (GOLDEN-COMPARISON-STATE.md) is still individual-results-only. The
one mechanism that made team-bearing comps *interesting* to reject-in-spirit —
teams that actually decide something — is exactly what the corpus cannot yet
witness. Sourcing reality to plan against:

- **Webmine downloads carry no team data** — `Team`/`OmitFromTeamScore` are
  uploaded to the server DB but absent from the download CSV
  (`kanban/completed/gliderscore-webmine-tool.md:101-103`, "avoids
  team-bearing comps as planned"). Zip contents "may be richer — verify
  during triage" is the only online hope; otherwise real team comps come the
  jerilderie route: organiser `.mdb` exports.
- **The NZ master is a dead end** — `Team='0'` on all 168 comps
  (`grow-corpus-nz-master-five-fixtures.md:19`).
- FAI-style international comps standardise on three counting pilots, so
  Nbr=3 candidates are the natural hunt target.

## Before starting

- [ ] Cross-check against users/NFRs and the rules corpus — team
      classification fidelity already settled in teams-mvp (both C.15.6.2
      methods documented; MVP implements score-sum).
- [ ] Decide the oracle strategy per fixture: GS report transcript where a
      real comp provides one; independently recomputed team ladder (private
      source available) otherwise. State the verification for each.
- [ ] Rule-5 amendment touches the `extract/` tooling contract — update
      `extract/README.md` in the same change; the amendment must not weaken
      the series/prelim/merged guards.
- [ ] If any live webmine call is needed (catalogue hunting for team-bearing
      candidates, even though downloads lack team fields), check the
      permission-email gate first (`gliderscore-webmine-tool.md` Before
      starting).
