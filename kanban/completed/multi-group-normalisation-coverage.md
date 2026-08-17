# Story — Coverage: normalisation is per group, not per round

**Status:** Completed 2026-08-17 · **Raised:** 2026-08-17

## What

Every scoring test ran a field that drew to exactly one group, so nothing
distinguished "normalised within the group" from "normalised across the round" —
the two readings coincide when there is only one group. Closed at two levels:

- **WI-1 — acceptance.** `Scenario: Each group in a round is normalised against
  its own winner` in
  `tests/Soarscore.Acceptance.Tests/Features/ScoringACompetition.feature`: a
  12-competitor F5J field, two groups of 6, one group flying markedly longer
  times (300..400) than the other (480..600). Asserts each competitor's score
  against their *own* group's winner, exactly two competitors on 1000 (one per
  group), and — the explicit counterfactual — that the slower group's scores are
  **not** what normalising against the round's best time would give.
- **WI-2 — property.** `tests/Soarscore.Domain.Tests/NormalisationGroupIsolationPropertyTests.cs`,
  two CsCheck properties over `ScoringService.ScoreCompetition`:
  *a competitor's normalised score depends only on their own group's raw scores*
  (perturb one group's times, every other group's scores are bit-identical), and
  *the count of competitors on the normalisation target equals the group count*.
- **WI-3 — the single-group assumption made explicit.** The pre-existing
  scenario now says `round 1 is drawn as a single group holding all 6
  competitors` rather than leaving it implicit in the step helpers'
  `Groups.Single(...)`, and its Then steps are worded per group.

## Why it mattered

The rule is per group: highest raw total in the group scores the normalisation
target, others `own raw × target / group winner's raw` (`docs/rules/f5j.md` §3,
`5.5.11.12`). The pipeline already honoured it — `ScoringService.ScoreGroup`
normalises one group at a time — but a regression that normalised across the
round would have passed the entire suite untouched, scoring a pilot whose group
flew in worse air against another group's winner.

## As built — notes

- **The draw splits as assumed.** `PhaseDraw.BuildGroups` takes
  `groupCount = max(1, field.Length / minPerGroup)` and `GroupSizes` splits
  evenly, so 12 against F5J's `MinPerGroup = 6` gives exactly 2 × 6. The
  fairness floor (`5.5.11.14.1`) never engages at this size. Both the acceptance
  Given and the property fixture assert the split rather than assuming it — a
  draw that stopped splitting would otherwise degenerate the tests into
  single-group tests that prove nothing, silently and still green.
- **The property is stated at `ScoreCompetition`, not `NormalisationEngine`.**
  The engine receives one group's results and cannot see another group even in
  principle, so a property there is true by construction. `ScoreCompetition`'s
  `foreach (var group in taskRound.Groups)` is where the partition is actually
  made and therefore the only level at which the claim can fail.
- **Both levels were mutation-checked** before being called done. Removing the
  `e.GroupRef == group.Id` filter (round-wide normalisation) fails both
  properties at 100 samples; a subtler mutant that kept per-group membership and
  valid counts but took the round's winner as divisor still fails the acceptance
  scenario on the score itself (900 expected, 600 found), so the assertions bite
  on the number, not just on group bookkeeping.
- Verified under both stores: `SOARSCORE_TEST_STORE=sqlite` and `postgres`,
  9/9 acceptance each.

## Deferred

- The counterfactual assertion identifies the slower group by comparing against
  the round's best time. With three or more groups it would only ever check one
  of them. Fine at two groups; revisit if a scenario ever draws three.
