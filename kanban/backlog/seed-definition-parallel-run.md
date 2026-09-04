# Story — Seed-definition parallel run (corpus fixtures under the seed classes)

**Status:** Backlog · **Raised:** 2026-09-04 (design discussed interactively
same day; pair findings below are measured, not estimated)

## What

Run existing corpus fixtures under the **real competition classes** from
`tools/Soarscore.SeedData/json/` instead of their fixture-authored
`class-definition.json` — the parallel-run claim: *GliderScore as the
authoritative system, Soarscore as a parallel run "by the book"*, and the test
reports where the two would have split on the day. The claim's shape differs
from parity, so the test shape must too: a **parallel-run ledger** per
(fixture, seed class) pair asserting *"the differences are exactly the triaged
set"* — never "exact match" as the goal (exactness is a possible *outcome*).

Each ledger entry names an expected rulebook-vs-local-practice difference and
cites its triage kind:

1. **Local variation** — the club ran a variant of the rulebook; correct on
   both sides. This is the story's product: what *would have* differed.
2. **Seed-class authoring gap** — our class misauthors the rulebook. Fix
   against `docs/rules/` (+ the `fai-rules` skill), **never against
   GliderScore** — GS is the parallel run's foil, not its oracle.
3. **Engine divergence** — a genuine bug; escalates out of the ledger into a
   defect.

Mechanically near-trivial by the class-model law: the seed class is published
through the same `/publish-class-definition` command the harness already uses
(`ReplayDriver.cs:199` posts `fixture.Definition`; a parallel run posts the
seed JSON instead), seed parameters (`targetTime`, `groupSize`, `minRounds` —
see `80-nz-m-ales200.json`) are bound from the comp's actual config, and
entries, realised draw and flights are unchanged.

Measured first pairs:

- `ales-sample-comp` ↔ `80-nz-m-ales200.json` — near-twin definitions:
  landing lookup identical (50…5/0 per metre), duration piecewise identical,
  winner 1000. Genuine rule differences found: landing-distance precision
  Truncate (GS) vs **Ceiling** (NZ rulebook — real, invisible on whole-metre
  data); `landedWithin75m` flight validity; ZeroRound penalties; `equalPlaces`
  tie-break; normalise rounding HalfUp-1dp vs exact. **All unwitnessed on this
  fixture's data — the pair may legitimately come out exact**, which is itself
  a reportable result ("the club could have run NZ ALES 200 in parallel and
  got identical placings").
- `f3j-international` ↔ `50-f3j.json` — guaranteed drop-policy divergence
  witness: GS's local Drop1@8 vs the official rule — a difference in *final
  placings*, exactly the "real difference that would've happened".

## Why it matters

D2 of `kanban/completed/gliderscore-replay-and-compare-harness.md` (every
fixture gets its own authored definition) proves *engine equivalence* under
GS-mirrored configs; `kanban/backlog/fai-conformant-f3k-fixture-hunt.md` hunts
the *conformant* half (a rulebook-configured comp should replay cell-exact,
empty ledger). This story is the third cell: **club-configured comps run under
the rulebook** — the real-world acceptance question for a club considering a
parallel run. It also exercises the parameter-binding machinery
(`kanban/completed/bind-parameter-steel-thread-plan.md`) that the parity
harness never touches, since seed classes are parameterised and
fixture-authored definitions hardcode.

## Before starting

- **Sibling story:** `kanban/backlog/fai-conformant-f3k-fixture-hunt.md` —
  read first; its seed-swap attempt records the one known mechanism failure
  (`prescribeDraw.taskNotInCatalogue` on GS task code `A(2)`, a mismatch
  failing loudly, which is correct behaviour). Duration fixtures' single `D`
  task should pass that gate.
- **Mapping table:** fixture → seed class pairs are curated with skip reasons,
  in the spirit of `index.md` — not every fixture has an honest match
  (`f3j-international-flyoff` is DurGeneral club-comp shape; NZ fixtures may
  map to the NZ NDC classes rather than the FAI ones — decide per pair, never
  by class-name resemblance).
- **Ledger semantics:** the parallel-run ledger is a *different* contract from
  the fixture divergence ledger (expected rulebook differences, not excused
  arithmetic mismatches) — distinct comparator grain, distinct schema, never
  merged. Disclose deviations in the NZ fixtures' deviation-block style.
- **Anti-goal, stated hard:** seed classes are never tuned to GS. An authoring
  gap is fixed against the rule docs; a GS-vs-rulebook disagreement is
  reported, not reconciled.
- **Vehicle:** runnable through the JSON harness; where a literal record
  scenario exists for the same fixture
  (`kanban/backlog/literal-record-replay-scenarios.md`), prefer it as the
  display vehicle — but keep the mode independently runnable either way.
- **House rule 2 cross-reference:** checked `docs/users.md` and
  `docs/non-functional-requirements.md` — no conflict found (users.md's
  "parallel" is role separation, unrelated); no new domain concepts; nothing
  in `/docs` changes.
- **Exactness discipline:** if a pair comes out exact, say so loudly in the
  scenario name and ledger state — "may come out exact" predictions (ales ↔
  NZ ALES 200) must not quietly become tuned expectations.