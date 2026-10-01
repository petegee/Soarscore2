# Story — Source an FAI-conformant F3K fixture (seed-definition parallel-run witness)

**Status:** Backlog · **Refined:** 2026-10-01
**Sequence:** GS 08/13 · Milestone 3: targeted seed witnesses
**Dependencies:** Semantic investigation/acquisition can start independently.
Replay uses GS 02 complete-result and GS 03 exact-difference contracts, plus
GS 04 registry/coverage tooling when available.
**Renamed from:** `fai-conformant-f3k-fixture-hunt.md` (originally raised 2026-08-29).

## Current delivery contract

The original discovery and acquisition proposal below are retained as context.
Their ten-fixture count, absence of any seed runs and unbuilt NDC seed are
historical observations, not current prerequisites. Re-check current seed and
fixture data before acting on any catalogue/working-window claim.

1. Establish whether GS can encode the chosen current FAI or NZ NDC task
   semantics, using its source/manual/configuration evidence. Renaming historical
   letters is insufficient: verify flight selection, targets, precision, working
   windows, discard policy and recorded metric observability.
2. Choose and justify one target seed. Consult `docs/rules/f3k.md` (F3K.10–11)
   or the applicable NZ NDC source via `fai-rules`, keeping the rulebooks separate.
3. Acquire one real competition with enough independent configuration, raw input
   and result evidence to exercise that seed. Follow existing acquisition and
   redaction conventions. If GS cannot supply it, propose the alternative source
   before changing this story's real-data deliverable; synthetic evidence must
   remain explicitly synthetic and is not completion of the real-event witness.
4. Curate provenance and expected results, declare snapshot/field availability,
   then run the actual shipped seed through public commands and queries.
5. Compare all available grains with exact expectations. Classify representation,
   evidence and rule/configuration differences before declaring a seed defect;
   the older blanket 'any ledger entry is a bug' expectation is superseded.

## Acceptance criteria

- [ ] Task compatibility is evidenced beyond code-name resemblance.
- [ ] A real competition, selected seed and source snapshot are unambiguously linked.
- [ ] Raw/normalised cells and all available total/discard/place/population fields
  are compared; unavailable evidence is visible.
- [ ] The seed remains rulebook-authored; no fixture-specific arithmetic is added
  to production code and no source inputs are tuned to the expected result.
- [ ] Differences, if any, satisfy GS 03's exact contract and evidence requirements.
- [ ] Fixture validation and strict GliderScore scenarios pass on both stores;
  corpus/seed coverage records the new witness.

## Requirements and verification

Supports NFR-1/2's shipped class-data promise, NFR-4's snapshot/absence semantics
and `docs/users.md`'s raw-metric capture and trustworthy results. No domain concept
or rulebook change is authorised. Use a real-data acceptance witness and existing
comparator negative checks; add focused arithmetic properties only where discovery
exposes an untested rule boundary, stating the invariant in the refined plan.

## Original discovery and acquisition proposal (2026-08-29)

**Original discovery:** 2026-08-29 (seed-vs-corpus conformance
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
