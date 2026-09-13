# Story stub - Jerilderie-2010 tape witness (50-f3j parallel run)

**Status:** Backlog
**Raised:** 2026-09-13 — split from `kanban/completed/tape-points-landing-seeds.md`
WI-6 at that story's closure, by owner agreement, so the landed tape capability
did not wait on this witness. This is the WI-6 work verbatim, plus its gate.

## What

The Jerilderie-2010 parallel-run witness under seed `50-f3j`, exercising the
declared-instrument landing model end to end on the corpus's most
tape-shaped fixture:

1. **Gate before full replay.** Surface the fixture's single 100-point
   competition penalty (R11/G3 pilot 2 — confirmed in
   `tests/GliderscoreFixtures/jerilderie-2010/scores-raw.json`, 882 rows, one
   `Penalty` hit; absent from the fixture's `divergences.json`, i.e. untriaged)
   and obtain an evidence-backed, owner-approved disposition. If none exists,
   leave the pair unrunnable and its mapping status honest. Do not hold
   anything hostage on it.
2. Replay under **`50-f3j`**, declaring the NZ F3J-side tape
   (`tape-nz-f3j-side`) for `landingDistance` and submitting the scheme-3
   readings **verbatim** and naming that tape, including the off-the-tape
   zeros. Every row of this fixture is a tape reading; the composition is the
   identity here (`F3J.10.5` ≡ `NZ.2.4.4`), the cleanest possible first
   witness of it. Validate through the production API, not a harness bypass.
   Record the declared instrument in provenance.
3. Re-verify the seed's no-default bindings. The first plan proposed
   `carryPenalties = false` and `flyoffMinRounds = rounds flown` as dormant
   bindings, only the preliminary being drawn. Keep an unknown parameter a
   loud refusal and disclose dormant bindings honestly; add no seed default to
   suit a fixture.
4. Author the ledger at
   `tests/GliderscoreFixtures/jerilderie-2010/parallel-run/50-f3j.json` and a
   scenario under seed `50-f3j`. Triage measured duration, normalisation and
   drop differences with citations. Do not require identical placings, and do
   not infer a ranking difference from an aggregate difference alone.
5. Verify on both stores. Only then mark the mapping row `done` in
   `tests/GliderscoreFixtures/parallel-run-mapping.md` and record coverage
   under `50-f3j`, with no new seed or count.

## Why it matters

The mapping row (`parallel-run-mapping.md` jerilderie-2010) refuses on the
landings-are-tape-readings diagnosis; the declared-instrument model landed by
the parent story makes them expressible, so the remaining gates are the
unrecorded penalty and the run itself. Landing support alone is not a
completed full-fixture witness — this stub is what finishes the pair.

Planning measurements to re-verify before relying on (2026-09-07, from the
parent story's retained "Jerilderie evidence" section): comp 4 `DurGeneral`,
63 pilots, 14 rounds × 5 groups, target 600 s, landing scheme 3 (23 rows,
30–90 in fives then 91–100 in ones — read correctly, the F3J-side scale and
its identity composition; 84 flown rows read 0 = off-the-tape); 843 flown
rows; 145 flights exceed 600 s (GS decays past target, F3J caps the time
contribution per `F3J.10.1 c` — expect raw/normalised differences, no landing
differences); GS drops at rounds 6 and 12 vs `F3J.3.1 a` single drop after
seven; the R13 pilot 29 make-up row resolves through the existing
destination-round mechanics (landed 2026-08-28,
`kanban/completed/reflight-aggregate-destination.md`).

## Before starting

- **The penalty gate is the owner's, not the agent's.** The fixture's offence
  is unrecorded: do NOT label it `towlineNotClearedWithin30s` because the
  point cost matches, do not drop the row, and do not weaken the harness
  refusal (`RecordPenalty`'s unknown-infraction loud refusal — see
  `kanban/tech-debt.md`'s declared-infraction mapping item). The disposition
  must be evidence-backed and owner-approved before any full replay.
- Read the parent story's WI-6 section and owner decisions
  (`kanban/completed/tape-points-landing-seeds.md`) — the refusal contract,
  the three-way distinction (off-tape / 0 m / no measurement), and the
  cross-story rule that the fixture must not decode its landing scheme in
  harness code.
- `gliderscore-replay-and-compare-harness.md` and
  `seed-definition-parallel-run.md` (both completed) define the harness, the
  ledger shape and the comparator grains; `divergences.json` carries the T1
  team-grain deferral for this fixture already.
- Exact-decimal oracle comparison stands; ledger GS binary64 artefacts, never
  emulate them (`kanban/deferred-decisions.md`, replay harness section).
