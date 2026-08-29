# Story — Source an FAI-conformant F3K fixture (seed-definition parallel-run witness)

**Status:** Backlog · **Raised:** 2026-08-29 (seed-vs-corpus conformance
analysis: swapping seed `10-f3k.json` in for a fixture-authored definition
fails at `/prescribe-draw` with `prescribeDraw.taskNotInCatalogue` on
`A(2)`, and the by-code definition diff shows zero arithmetically identical
tasks except K)

## What

Get a real completed F3K competition into the golden corpus whose scoring
configuration actually matches a rulebook the seed encodes — the FAI F3K
catalogue (A–N per `F3K.11`, working-time windows, drop-worst from round 6)
or the NZ NDC format (B/D/G/H raw sum, `NZ.0.2.1`) — so the replay harness
can run it under the **seed definition** (`tools/Soarscore.SeedData/json/10-f3k.json`,
or the NDC class from `kanban/backlog/nz-f3k-ndc-seed-class.md`) and compare
against GS at the usual three grains.

This is the missing witness for the theory the corpus otherwise proves only
half of: *engine equivalence* is cell-exact for all ten fixtures, but always
under per-fixture authored definitions (harness story D2); *seed-definition
portability* — "run Soarscore's seed data in parallel against GS and get
the same results" — has never been exercised against a single real export.

## Why it matters

Every corpus F3K fixture (f3k-sample-comp, f3k-june-2020,
f3k-southern-fling) uses GliderScore's own task catalogue — codes like
`A(2)`, `B(1)`, `C(1)`, `X`, "Ladder (Not FAI)" — with slot-sum arithmetic,
no working-time windows and no drops. None of that is expressible by the
seed F3K definition, and the rulebooks say it shouldn't be: NZMAA S5 §3.8.1
nominates FAI §5.7 for F3K outright. The divergence between seed and
fixtures is *club GS configuration*, not the rulebook — but the corpus
currently proves nothing about what happens when a comp **is** configured
to the rulebook. That cell of the matrix is empty.

## Before starting

- **What counts as conformant:** schedule drawn from the FAI A–N catalogue
  with FAI arithmetic; working-time windows configured; drop-worst per FAI
  (fires from 6 rounds). An NDC fixture instead needs the seed class from
  `nz-f3k-ndc-seed-class.md` first — dependency, not blocker for an FAI one.
- **First question to answer — can GliderScore express FAI F3K at all?** GS's
  F3K task table looks like a historical catalogue with per-comp slot
  semantics (`D` = "Ladder (Not FAI)", seven slots; sub-numbered variants).
  Verify whether GS can configure FAI letters with FAI arithmetic and
  working windows, from the GS UI/manual/source before hunting exports —
  if it cannot, the hunt moves to another scoring system's export, or the
  corpus gains a synthetic-but-rulebook-faithful fixture whose
  `provenance.json` says so loudly (see NZ fixtures' deviation blocks for
  the disclosure style).
- **Expectation to state up front:** on a conformant fixture the seed
  definition should replay cell-exact with an *empty* divergence ledger —
  any ledger entry is a seed-definition bug to fix in `SeedF3K.cs`, not an
  accepted difference.
- **Diff tooling:** the session's by-code definition diff (seed vs
  `class-definition.json`, matched on task codes not array position) is a
  candidate committed helper — optional scope, propose before building.
- **Corpus discipline:** new fixture follows the existing entry protocol —
  `index.md` bullet, `provenance.json`, validation sweep, harness scenario;
  existing green replays stay untouched.
- **Cross-reference (house rule 2):** `docs/users.md` (club-level tool,
  NZ usage) and the harness story's skip-list rules for anything this
  contradicts.
