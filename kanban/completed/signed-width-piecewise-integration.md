# Story — Signed-width piecewise bands (the FAI F5K below-NLH bonus)

**Status:** Completed (2026-09-04) · **Raised:** 2026-09-04 (drilled out of the tech-debt entry
filed by `kanban/completed/nz-ndc-seed-classes.md`; user approved pulling it as a
second in-progress story alongside `grow-corpus-team-parity-fixtures.md` — zero file
overlap) · **Rulings:** none needed

## Completion note (2026-09-04)

Built per the plan (WI-1..WI-6). The failing-test-first prescription held:
WI-1's two below-origin cases went red under the unsigned engine; WI-2's engine
change turned them green and fired the `NzNdcSeedArithmeticTests` tripwire on
exactly the bonus rows (41–57); WI-3's seed flip restored the NZ table with
every expected value unchanged — the rulebook numbers never moved, only the
encoding. One discovery rewrote the story's framing: the notation doc had
documented signed semantics all along ("a negative rate over a negative portion
is what makes a low launch a bonus"), so WI-2 made the engine match its own
spec rather than change it; WI-4 added the explicit engine-rule sentence there
(user-approved). Key fact surfaced by the drill and worth keeping: the notation
doc + SeedF5K's comment were RIGHT and the ENGINE was wrong — the seed author
trusted the spec, the arithmetic didn't follow.

Suites green: Domain 690 (+2, the new below-origin cases), Application 276,
Architecture 7, Infrastructure 144 (both stores — Docker was up), Acceptance 72
postgres + 72 sqlite. Seed JSON re-emitted, round-trip byte-identical;
`40-f5k.json` unchanged as predicted (SeedF5K data untouched — its encoding was
correct under the engine it always described). `graphify update .` run.

The tech-debt entry is ticked with the discharge note.

## What

Make `FlightInterpreter.EvaluatePiecewise` integrate bands **with sign**: the walk
from the origin to `metric − origin` is a genuine signed integral
∫rate·d(adjusted), so a band below the origin contributes
`−width × rate` and a band above it `+width × rate`.

Effects:

1. **The engine now implements the notation doc's documented semantics.**
   `docs/competition-class-notation.md` has said all along: "A negative rate over
   a negative portion is what makes a low launch a bonus." The engine's
   unsigned-width walk never did that — the sentence was aspirational. It becomes
   true.
2. **`SeedF5K` becomes correct as written** — `Below(0, -0.5m)` at 10 m below the
   NLH scores +5 (5.5.10.4's bonus), not −5. Data unchanged; comment updated.
3. **`SeedF5kNdc`'s below bands flip to negative rates**
   (`Below(-6, -2).UpTo(-2, -1)…`) — same rulebook numbers, opposite encoding,
   per NZ.3.16.29 g. Data + comments change; JSON re-emitted.
4. **`NzNdcSeedArithmeticTests` must pass unchanged** — its expected values are
   NZ.3.16.29's own worked table, not encoding details. It failing between WI-2
   and WI-3 is the designed tripwire doing its job.

## Why it matters

The FAI F5K below-bonus scored as a deduction — the rule *rewards* a weak launch,
the engine *punished* it (verified empirically against the real seed: launch
NLH−10 → −5, dead launch → −30). No test covered a below-origin launch and no
GliderScore F5K fixture carries flight heights, so nothing could catch it. The
unsigned convention also made the two F5K seeds encode the same concept ("below
the NLH is a bonus") with opposite rate signs — `SeedF5K` negative,
`SeedF5kNdc` positive — each right under a different mental model. Signed-width
gives one convention for both rulebooks: **a bonus is a negative rate; the
direction of travel from the origin flips its sign below the origin.** F5J-style
"low launch = less deducted, never rewarded" needs no below-origin band at all
(`From(0)` penalties), and is untouched.

## Blast-radius audit (all 12 `T.Piecewise` call sites)

- Bands below the origin with non-zero rate — the only behavioural change:
  `SeedF5K.LaunchBands` (correct after the fix, no data change) and
  `SeedF5kNdc.LaunchAdjustment` (flipped in WI-3). No others.
- Origin-0 positive-domain overtime bands (ALES ×3, F3B, F5L, F5K
  penalty-only): walk direction is positive for real data; products identical.
  Note the deliberate semantic: a garbage-negative metric now produces a
  negative contribution rather than a positive one — more defensible.
- Origin-0 height bands (F5J ×2, F5J NDC): same, `From(0)` penalties only.
- Zero bands either side: unaffected.

## Plan

**WI-1 — failing test first** (the tech-debt entry's own prescription). Add
below-origin cases to `FlightInterpreterTests`' FAI F5K Task A theory: NLH−10 →
launch term +5 (total 185), dead launch 0 m → +30 (total 210). Run: the two new
cases fail under the unsigned engine, the covered cases (NLH+15, at-NLH) pass.

**WI-2 — engine.** `EvaluatePiecewise`: accumulate `width × rate` per band
overlap, multiply the total by the direction of travel (`adjusted < 0 ? −1 : 1`).
Comment states the signed-integral semantics and cross-references the story.
Expected state after: new F5K cases green; `NzNdcSeedArithmeticTests` RED —
the tripwire firing as designed.

**WI-3 — NZ seed.** `SeedF5kNdc.LaunchAdjustment` bands become
`Below(-6, -2).UpTo(-2, -1).UpTo(2, 0).UpTo(6, -1).Rest(-2)`; rewrite the SIGN
CONVENTION comment block and its worked-example arithmetic; rewrite
`NzNdcSeedArithmeticTests`' header comment (expected values untouched).
Expected state after: NZ table green again, unchanged values.

**WI-4 — comments + docs + JSON.** `SeedF5K.LaunchBands` comment (lines 48-50):
the reasoning becomes "a negative rate; the signed walk below the origin flips
it to the 5.5.10.4 bonus". Notation doc (user-approved): one explicit sentence
making the signed-integral rule unambiguous at the `from <origin>` bullet.
Re-emit JSON (`85d-nz-f5k-ndc.json` changes; `40-f5k.json` must be byte-identical
— SeedF5K data is unchanged).

**WI-5 — suites.** Domain (full), Application, Architecture; Infrastructure and
Acceptance on sqlite (fast loop). Postgres when Docker is next up — the change
touches no store code, but the discipline is the whole suite per backend.

**WI-6 — board.** Tick the tech-debt entry with the discharge note; complete this
story; `graphify update .`.
