# Story — Corpus-wide fixture→seed parallel-run mapping table

**Status:** Backlog · **Raised:** 2026-09-06 (WI-5.3 of
`kanban/completed/seed-definition-parallel-run.md`)

## What

A corpus-wide table pairing every fixture in `tests/GliderscoreFixtures/`
with a seed class from `tools/Soarscore.SeedData/json/` — or recording its
skip reason — in the spirit of `tests/GliderscoreFixtures/index.md`: one
entry per pair with the seed class chosen, the pairing's expected character
(witness / near-twin / not-expressible), and why. The landed and stubbed
pairs are the first entries: ales ↔ `80-nz-m-ales200.json` (done, near-exact),
`f5j-christchurch-2019` ↔ `30-f5j.json` (witness stub), f3j-international ↔
`50-f3j.json` (re-triage stub).

## Why it matters

The parallel-run machinery is proven but its coverage is ad hoc — three pairs
chosen by hand. The table is the plan of record for the rest of the corpus:
it makes the skip reasons explicit (the F3K fixtures' GS task catalogues are
the known not-expressible case — `A(2)` et al. fail `prescribeDraw.taskNotInCatalogue`
loudly, per `fai-conformant-f3k-fixture-hunt.md`), turns "which pair next?"
from a conversation into a lookup, and exposes which seed classes have no
witnessing fixture at all.

## Before starting

- **NZ fixtures → NZ classes is decided per pair, never by class-name
  resemblance.** The NZ fixtures come from a separate rulebook (`docs/rules/nz/`)
  and the FAI cross-class invariants do not hold for them; a NZ fixture may
  legitimately pair with an FAI seed (the F5J witness did, owner-confirmed)
  or with a NZ class — each pair is argued on its own facts.
- Skip reasons cite their evidence the way the stubbed pairs do (verified
  config, rule citation, or harness refusal code) — "skip" is a claim, not a
  shrug.
- The harness refuses what cannot run loudly; the table records refusals as
  findings, never as harness workarounds (the parent story's anti-goal).
- House rule 2 cross-reference: check `docs/users.md` and
  `docs/non-functional-requirements.md` before the table lands; nothing in
  `/docs` changes.
