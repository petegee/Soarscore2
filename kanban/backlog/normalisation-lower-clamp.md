# Story — Normalisation lower clamp (floor NormalisedScore at 0)

**Status:** Backlog · **Raised:** 2026-08-28 (found by
`kanban/completed/nz-fixture-replay-scenarios.md` — its D2/N1)

## What

A lower clamp on the normalised score in `NormalisationEngine` (`NormalisationEngine.cs:121`
has no lower bound today): after rounding, floor the normalised value at 0, matching
GliderScore's option-1 branch (`Scoring_MOD.vb:310` — `max(0, RoundHalfUp1dp(1000 · raw /
groupMax))`).

## Why it matters

A raw score can legitimately be negative (F5J: a huge launch-height deduction can exceed
flight time plus landing points — comp 121 witness cell `1/3/3/0/99`: raw −2026, GS
normalises to 0.0, ours to −4031.8). The corpus carries exactly one such cell
(`f5j-nz-south-island`), so the fixture replays today only because the divergence is
ledgered under citation token **N1**. Landing this story would discharge comp 121's four
N1 ledger entries (1 normalised + 3 ranking knock-ons) and let that scenario assert an
empty ledger.

Deciding the clamp is a rule-adjacent choice, not a mechanical port: GS floors per its
normalisation option-1 branch, and our engine serves several normalisation arrangements
(option-2 ales-style puts the landing lookup in `scoreNormalised` instead) — the story
must say which arrangements floor and cite the `fai-rules` skill for the rulebook
position.

## Before starting

- Read the parent story's §D2 (N1 wording) and its "F5J arithmetic" ground truth for the
  worked example.
- Read `kanban/deferred-decisions.md`'s replay-harness section — exact-decimal comparison
  and the divergence-token discipline are settled and bind how the change is proven.
- Decide whether the floor belongs in `NormalisationEngine` for all arrangements or only
  the option-1 shape; property-based testing (CsCheck) is a natural fit for the
  invariant "no normalised cell is negative, and clamping preserves order" — name the
  invariant in the plan.
- After landing: empty `f5j-nz-south-island`'s ledger, drop its ledger-count pin to 0,
  and re-run the acceptance suite on both stores.
