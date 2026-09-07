# Story — Corpus-wide fixture→seed parallel-run mapping table

**Status:** Completed (2026-09-07 — all four WIs executed; table at
tests/GliderscoreFixtures/parallel-run-mapping.md; rows are proposals until
their pair stories are owner-confirmed) · **Raised:** 2026-09-06 (WI-5.3 of
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

## Settled decisions (2026-09-07, proposed — owner may veto any before WI-1)

These resolve the ambiguities the stub left open; each is argued, not asserted.

**D1 — Artifact + location.** The table is a corpus curation file:
`tests/GliderscoreFixtures/parallel-run-mapping.md`, beside the `index.md`
whose spirit it borrows. Not `/docs` (house rules 3–4), not `kanban/` (it
curates the fixture corpus; it is not board state).

**D2 — Closed character taxonomy.** Exactly three characters, one axis (what
the pair is *for*), defined once:

- **near-twin** — the seed's scoring-relevant definition and the fixture's
  effective definition coincide (landing lookup/composition, duration
  piecewise, normalisation grid, drop config vs rule applicability); expected
  difference set empty or unwitnessed candidates only; expected final placings
  identical to the GS oracle.
- **witness** — at least one rulebook-vs-local-practice difference provable on
  the fixture's config or data; the pair exists to surface it.
- **not-expressible** — a runnability gate (below) fails; the harness would
  refuse loudly; terminal.

"Near-exact" is retired as a character — it was an *outcome* word (the landed
ales result: exact final placings, three triaged kind-1 differences). Outcomes
live in the row's `why`, never in the character column. This fixes the stub's
own line-12-vs-line-13 drift.

**D3 — Status vocabulary (orthogonal to character).** `done` (parallel-run
ledger landed), `stubbed` (a sibling stub owns the pair), `planned` (this
table only), `refused` (terminal not-expressible). `refused` ⟺ character
`not-expressible`.

**D4 — Evidence bar: static triage, no harness runs.** The table is the plan
of record; runs belong to the pair stories. Every row cites ≥1 item from the
closed citation set (below). A `witness`/`near-twin` character must be
justified by verified-config or rule citation — never class-name resemblance;
a `refused` row must name its failed gate with a citation. A recorded refusal
(hunt story, index.md) stands as evidence; re-running the harness is not
required to write the row.

**D5 — Cardinality: one primary pair per fixture.** Mirrors index.md's
one-line-per-comp and keeps the table a lookup. Defensible alternates are
noted inside the row's `why` (`also runnable under X — not pursued because
…`), never as rows. The seed-side view is a separate **Seed coverage**
section (WI-4): one line per seed file, which fixture(s) reference it or
`uncovered` + one-line why. `90-aggregate.json` is a real class (NZ
free-flight Aggregate) and gets a coverage line — `uncovered by design` (the
corpus is RC gliding; the class cannot witness here).

**D6 — Documentary, no validator.** Nothing consumes the file mechanically
today (unlike `index.md`'s `validate.py --index` contract), so no `--mapping`
mode is added. Kept honest by an update contract in the file header: the
story that runs a pair flips its row to `done` and folds the measured outcome
into `why`; corpus growth adds its row in the same change; seed growth
extends the coverage section in the same change. Revisit if tooling ever
consumes it.

**D7 — Boundaries.** Skip-listed `f3b-international` gets a row (it is in
the corpus index): character `not-expressible` (gate G2 + the standing skip
reason), seed column records `20-f3b.json — moot`. The two sibling stubs
(`f5j-christchurch-parallel-run-witness.md`,
`f3j-international-parallel-run-retriage.md`) own their pairs' reasoning;
their rows carry a one-line why + `stub` citation, never a duplicate argument.

**D8 — House rule 2 cross-reference (done).** `docs/users.md` "parallel" is
role separation (`docs/users.md:26`) — unrelated; NFR-1/NFR-2 are *supported*
(the table makes the class-model promise measurable: which seed classes can
express which real comps); no new domain concepts (corpus/harness vocabulary,
same precedent as the parent story's); nothing in `/docs` changes.

## Plan

One agent per WI, strictly sequential (WI-1 → WI-2 → WI-3 → WI-4): all
agents edit the one file, and WI-1 owns the contract the others obey.
WI-2 and WI-3 write disjoint rows, but run sequentially to avoid merge
conflicts.

### Shared material (every WI applies it verbatim so rows are uniform)

**Row schema** (one per fixture, a markdown table row):

    | fixture | seed | character | status | why |

`seed` is the seed JSON file name. `why` is ≤2 lines of prose + citations
from the closed set:

- `config <path>` — a verified field-level fact (file + field named)
- `rule <ref>` — FAI `<doc> x.y.z` or `NZ.x.y.z` per the fai-rules skill
- `refusal <code>` — a recorded harness refusal + the doc recording it
- `ledger <path>` — landed parallel-run ledger (for `done` rows)
- `stub <path>` — sibling stub owning the pair
- `index <slug>` — the fixture's index.md line (corpus-skip-listed comps)

**Runnability gates**, checked in order; first failure ⇒ character
`not-expressible`, status `refused`:

- **G1 task catalogue** — every task code in the fixture's per-round schedule
  is in the seed's catalogue (F3K/F5K family); failure refuses with
  `prescribeDraw.taskNotInCatalogue`
  (`kanban/backlog/fai-conformant-f3k-fixture-hunt.md`).
- **G2 round composition** — single task per round; multi-task refuses with
  `unsupportedRoundComposition` (index.md standing skip reasons).
- **G3 metric observability** — every seed-declared metric is either captured
  by the fixture's capture map or `whenNotRecorded`-declared by the seed
  (`metric-absence-semantics`); otherwise the engine throws
  (`PredicateEvaluator.cs:38`, `FlightInterpreter.cs:217`).
- **G4 penalty infractions** — every fixture penalty row's infraction type is
  declared by the seed (parent story WI-1.4's loud refusal).
- **G5 landing evidence** — the fixture's recorded landing evidence satisfies
  the seed's landing metric (entered-distance vs entered-points vs none).
- **G6 standing concept gaps** — series without `triageJustification`,
  merged/prelim (index.md standing skip reasons).

**Triage procedure** per fixture (WIs 2–3):

1. Read: the fixture's `competition.json` (+ `configProvenance`/
   `knobProvenance` where stored knobs are Jet nulls), its index.md line, the
   candidate seed JSON, the rule doc via the fai-rules skill, and existing
   evidence (hunt story, sibling stubs, landed ledger).
2. Choose the primary seed: same class family
   (`docs/competition-class-notation.md`); for NZ-master fixtures argue
   NZ-class vs FAI-family-seed on the fixture's own facts (the
   owner-confirmed christchurch precedent) — name the loser and why.
3. Run gates G1–G6 against the chosen seed; record the first failure or
   all-pass.
4. If runnable: set character per D2, naming the expected difference
   candidates in `why` (or "none expected; candidates unwitnessed").
5. Write the row.

### WI-1 — Artifact + contract + the three known rows

**Agent deliverable:** create `tests/GliderscoreFixtures/parallel-run-mapping.md`.

1. Header carries everything standing: purpose (one paragraph); the update
   contract (D6); the row schema; both closed vocabularies (D2/D3); the
   citation set; the gate list (the story carries the one-time triage
   procedure and per-fixture findings; the file carries the standing rules a
   future corpus-growth agent needs).
2. Main table seeded with exactly the three known rows:
   - `ales-sample-comp` ↔ `80-nz-m-ales200.json` — near-twin, `done`, why
     cites `ledger tests/GliderscoreFixtures/ales-sample-comp/parallel-run/80-nz-m-ales200.json`
     + the outcome in words (near-exact: exact final placings; difference set
     = the three triaged kind-1 raw-grain landing-composition entries).
   - `f5j-christchurch-2019` ↔ `30-f5j.json` — witness, `stubbed`, cites
     `stub kanban/backlog/f5j-christchurch-parallel-run-witness.md` + the
     guaranteed drop divergence in one line.
   - `f3j-international` ↔ `50-f3j.json` — witness, `stubbed`, cites
     `stub kanban/backlog/f3j-international-parallel-run-retriage.md` + the
     rounding-grid candidate in one line.
3. Seed coverage section: header only (filled by WI-4).

**Done-when:** file exists with header + 3 rows + empty coverage section;
every fact in the rows traceable to a citation from the closed set.

### WI-2 — Runnable-pair triage (five fixtures)

**Agent deliverable:** apply the triage procedure to `f3j-international-flyoff`,
`jerilderie-2010`, `f5j-hawkes-bay-trials`, `f5j-nz-south-island`,
`f5k-ni-round-2`; write their rows.

Per-fixture starting facts (from the corpus record — verify, don't assume):

- `f3j-international-flyoff` ↔ `50-f3j.json` — entered-distance landing
  matches the family; no drops configured (Drop*=99 over 4 scored rounds);
  integral scores — expect near-twin with the rounding grid as the same
  unwitnessed candidate the f3j-international row names.
- `jerilderie-2010` ↔ `50-f3j.json` — **contested**: the fixture's landing is
  entered-*points*; G5 decides (verify `50-f3j.json`'s landing metric against
  the fixture's recorded landing evidence). If G5 fails, the row is `refused`
  with `config` citations on both files — a finding, not a failure. If G5
  passes, the drop rule's applicability to Drop1@6+Drop2@12 over 14 scored
  rounds needs a rule citation either way.
- `f5j-hawkes-bay-trials` ↔ `30-f5j.json` — christchurch precedent applies
  (F5J family, launch height captured); drops provably un-fireable on the
  fixture (thresholds unset); whether the rulebook drop-from-5
  (`docs/rules/f5j.md:90`) applies at R1–10 scored rounds decides witness vs
  near-twin — cite the rule's applicability clause.
- `f5j-nz-south-island` — **NZ fixture, per-pair seed decision**: candidates
  `85c-nz-f5j-ndc.json` (NZ class) vs `30-f5j.json` (FAI family). Also
  verify G3 against the extreme-height clamp (flooring at normalisation) and
  the motor-restart flag row (both facts in index.md; the seed's
  `whenNotRecorded` set decides).
- `f5k-ni-round-2` ↔ `40-f5k.json` — index.md records LaunchBands ≡ SeedF5K
  already; the parity story's drop-pool divergence (GS's scored-round-gated
  pool vs ours, place-identical) is the named difference candidate; character
  per whether any rulebook-vs-local difference is provable on config.

**Done-when:** five rows, each with ≥1 citation, character justified per D2,
and the NZ per-pair decision recorded for `f5j-nz-south-island` with the
loser named.

### WI-3 — Not-expressible adjudication (four fixtures)

**Agent deliverable:** verify and write `refused` rows for
`f3k-sample-comp`, `f3k-southern-fling`, `f3k-june-2020`,
`f3b-international`.

1. For each F3K fixture: enumerate the fixture's task codes from its
   per-round schedule (`competition.json`), check each against the candidate
   seed's catalogue (G1). The hunt story's recorded refusal
   (`prescribeDraw.taskNotInCatalogue` on GS task code `A(2)`) is the
   mechanism-failure precedent — cite it per row and name the specific codes
   that fail (they differ per fixture: `A(2)`, `X`, `C(3)`, `B(1)`, `B(2)`,
   `K`, `I`, `E`, `J` per the index.md lines). For NZ-sourced F3K fixtures,
   also check `85b-nz-f3k-ndc.json`'s catalogue before refusing outright —
   refuse on "no seed in the catalogue expresses the comp's task encoding",
   naming both candidates.
2. `f3b-international`: G2 (multi-task-per-round: 3 tasks/round × 9 rounds)
   + `index f3b-international` — no fixture directory exists; seed column
   `20-f3b.json — moot`.
3. Verify catalogue facts against the seed JSON itself (field-named `config`
   citation), not only against prose.

**Done-when:** four `refused` rows, each naming its gate, the failing facts,
and the recording doc.

### WI-4 — Seed coverage section + reconciliation

**Agent deliverable:** complete the coverage section and close the story.

1. One line per seed file in `tools/Soarscore.SeedData/json/` (16 files):
   seed — witnessing fixture(s) (per the table rows) or `uncovered` +
   one-line why. Expected uncovered (verify, don't assume): `60-f5l`,
   `70-f3f` (families absent corpus-wide per index.md "Still open"); all NZ
   classes except `80` (`81`, `83`, `85`, `85b`, `85c`, `85d`, `86` — argue
   each against the corpus); `90-aggregate` (`uncovered by design` — NZ
   free-flight, D5). A seed referenced only by `refused` rows is
   `uncovered (refused rows only)`.
2. Read the finished table end to end against D1–D7 — taxonomy consistent,
   every citation from the closed set, no `why` exceeding two lines.
3. Present the table to the owner: rows are proposals until their pair story
   is owner-confirmed (D2/parent-story precedent); note any row the owner
   overturns.
4. Board reconciliation: `tech-debt.md` / `deferred-decisions.md` if anything
   surfaced (e.g. the jerilderie G5 finding if it refuses); move this story
   to `completed/` with the status header set.

**Done-when:** coverage section complete for all 16 seed files; story
completed.

### Execution notes

- No code, no tests, no corpus file changes: the only new file is
  `tests/GliderscoreFixtures/parallel-run-mapping.md` (D1/D6). WI-2/WI-3 are
  research + writing: no harness runs (D4).
- Property-based testing: none — the product is a curated table; the
  invariant ("every fixture has exactly one primary row; every seed file
  appears in coverage") is structural, checked by WI-4's read-through, and
  becomes mechanical only if D6 is ever revisited.
