# Story — Seed-definition parallel run (corpus fixtures under the seed classes)

**Status:** Completed 2026-09-06 — all WIs landed and verified: the
`@gliderscore` parallel-run scenario 14/14 and the full acceptance suite 74/74
on both stores (sqlite + postgres); the product is the ales pair's witnessed
difference set — exactly the three triaged raw-grain landing-composition
entries — with final placings identical to the GS oracle. Reality differed
from the plan in five recorded ways; see **As built** at the end. **Raised:**
2026-09-04 (design discussed interactively same day; pair findings below are
measured, not estimated) · **Plan written:**
2026-09-06 (decisions 1–4 below owner-confirmed that day) · **Unblocked
2026-09-06** — `kanban/completed/metric-absence-semantics.md` landed (its WI-7):
the seed classes declare their own `whenNotRecorded` assumptions and the engine
resolves them, so WI-1's harness emits **no** assumed values — the capture map
stays exactly the parity replay's; the superseded block above and WI-1 item 3
already describe the shrunken shape.

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

Scope (decision 3): this story lands **one pair** —
`ales-sample-comp` ↔ `80-nz-m-ales200.json` — proving the machinery end to
end. The divergence-witness pairs are stubbed for follow-up stories (WI-5).

### The load-bearing mechanism: seed metrics the foil never recorded

A naive seed swap **cannot run**. Both first-pair seed classes declare metrics
the GS data never records, and the engine throws on any predicate or score term
referencing an uncaptured metric (`PredicateEvaluator.cs:38` — "references
metric … not in the measurements dictionary"; `FlightInterpreter.cs:217` for
score terms). The seed NZ ALES 200 class declares three flags GS's ales data
has no column for — `landedWithin75m` (the flight-validity gate),
`damagedAndNotSafelyFlyable` and `touchedByCompetitor` (the landing-bonus
conditional) — so flight 1 would throw before any score exists.

The policy that resolves it (decision 1) rests on one fact about contest
scoring: **the club records exceptions, not compliance**. A timer writes down
the out-of-bounds landing, the touched model, the overfly — a clean flight is
recorded as just time and distance. GS's data model follows the same shape: its
deduction/penalty/zeroed-row columns carry the exceptions when they happen. So
an unrecorded exception is not a missing observation; it is a *negatively
recorded* one.

Mechanically the assumed values are captured as **ordinary measurements** by
the parallel-run capture map — zero engine change, zero seed change. The
engine keeps throwing on genuinely-uncaptured metrics, which stays correct for
real capture workflows; the assumption lives in the per-pair mapping (test
support), where every other GS→Soarscore mapping lives.

> **Superseded 2026-09-06:** the assumption mechanism moved into the model —
> `kanban/in-progress/metric-absence-semantics.md` gives `MetricDefinition` a
> declared `whenNotRecorded` value resolved by the engine, and the seed
> classes declare their own assumptions. WI-1's capture-map item below
> shrinks accordingly: the harness emits **no** assumed values; P1 becomes
> seed-declared data (P3 and the anti-goal are unchanged — the assumptions
> are justified against the rulebook's observation protocol, never against
> GS). The load-bearing description above is kept as the record of why the
> model feature exists.

Measured first pair — `ales-sample-comp` ↔ `80-nz-m-ales200.json`:

- Near-twin definitions: landing lookup identical (50…5/0 per metre),
  duration piecewise identical, winner 1000.
- Parameters: seed `targetTime` (BeforeFlying) default 600 = the comp's actual
  `durTargetTime` 600; `groupSize`/`minRounds`/`minNewGroup`
  (CompetitionSetup) have **no defaults** — they must be explicitly bound
  before `/prescribe-draw` (the frozen rule,
  `bind-parameter-steel-thread-plan.md`:
  `competition.parameter.frozen` once `!Phases.IsEmpty`).
- Genuine rule differences found at curation: landing-distance precision
  Truncate (GS) vs **Ceiling** (NZ rulebook — real, invisible on whole-metre
  data); `landedWithin75m` flight validity (assumed-true per the policy — no
  contrary record); ZeroRound penalties (declared, never recorded in this
  fixture — unwitnessed); `equalPlaces` tie-break; normalise rounding
  HalfUp-1dp (GS's proven grid) vs the seed's exact unrounded values —
  **potentially witnessed** on fractional normalised cells, since ales's
  1000·t/600-style cells are fractional off round multiples.
- **All but the rounding difference are expected unwitnessed on this
  fixture's data — the pair may legitimately come out exact**, which is itself
  a reportable result ("the club could have run NZ ALES 200 in parallel and
  got identical placings"). Exactness discipline (below) applies.

## Settled decisions (2026-09-06, Pete)

1. **Metric-observability policy** for a seed-declared metric the foil never
   observed:
   - **P1 — assumed-clean unless recorded to the contrary.** Flags: no
     contrary record ⇒ the rulebook-benign flag value (within 75 m ⇒ true;
     damaged/touched ⇒ false). Numbers encoding an exception (overfly): no
     contrary record ⇒ 0. Rationale: absence of the exception in a
     exception-recording data model is the negative recording.
   - **P2 — derive, don't assume, where the foil records a finer fact** the
     seed metric follows (e.g. F5J `startHeight` from GS's
     `FlightScoreDeduction` height payload; within-75m where a distance is
     recorded). Not needed for the ales pair.
   - **P3 — mapping, never tuning.** The assumptions live in the harness's
     per-pair mapping table (test support). The engine learns nothing; the
     seed classes stay rulebook-faithful and are never adjusted to GS.
   - **P4 — disclosure + bounded claim.** Each assumed/derived metric is
     disclosed in the pair's ledger provenance block (NZ fixtures'
     deviation-block style), and the parallel-run claim is stated *given the
     recorded data*.
   - **Overfly specifically** (worked through with the owner): does not arise
     for this story — the NZ ALES 200 seed declares no overfly metric. In the
     FAI seeds it is P1 (assumed 0) plus one open *semantic* question: whether
     GS's F3J late-landing −30 payload is the same offence the rulebook's
     overfly gate penalises — a `fai-rules` question, deferred to the witness
     story that runs an FAI seed (WI-5 stub).
2. **Pair corrections (measured, owner-confirmed).** The story's original
   f3j-international claim — "guaranteed drop-policy divergence: GS's local
   Drop1@8 vs the official rule" — is **withdrawn**: GS's `Drop1AtRound=8`
   equals the seed's `applyWhenRoundsCompletedAtLeast: 8` and the FAI rule
   (drop beyond 7 qualification rounds, `docs/rules/f3j.md:99`). The pair's
   real differences are elsewhere (normalise rounding HalfUp-1dp vs the
   rulebook's Truncate-0.1, metric observability, the seed's never-flown
   fly-off phase under `SplitByPromotion`). The **guaranteed-divergence
   witness** role moves to `f5j-christchurch-2019` ↔ `30-f5j.json`: all three
   F5J fixtures have **no drop thresholds configured** (verified: null
   `Drop1AtRound`/`Drop2AtRound`) against the rulebook's drop-from-5
   (`docs/rules/f5j.md:90`) over 11 scored rounds — a provable
   final-aggregate split, and the driver already captures F5J launch height.
3. **Scope: ales only.** The F5J witness pair, the f3j re-triage, and the
   corpus-wide mapping table are follow-up stubs (WI-5), not this story.
4. **Vehicle:** the JSON harness gains a parallel-run mode; a new feature
   file beside `ReplayingAGliderscoreFixture.feature` (harness character,
   matching the record/harness split the literal-record story settled). When
   `kanban/backlog/literal-record-replay-scenarios.md` lands, a literal
   display of the ales parallel run is preferred — the mode stays
   independently runnable either way.

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
fixture-authored definitions hardcode — including the freeze-before-draw rule
for CompetitionSetup parameters, which the harness's PerRound precedent never
hits.

## Plan

### WI-1 — Parallel-run mode in the harness (`tests/Soarscore.Acceptance.Tests/Support/Gliderscore`)

1. **Seed-definition loader** — locate `tools/Soarscore.SeedData/json/*.json`
   by the same assembly-path arithmetic `FixtureLoader` uses for
   `tests/GliderscoreFixtures/`; deserialise into the same
   `PublishClassDefinition` shape the driver already posts. The seed JSON is
   the single source of truth — no copies.
2. **Driver mode** — `ReplayAsync` gains a parallel-run entry that:
   - publishes the **seed** definition instead of `fixture.Definition`
     (same `/publish-class-definition`; adoption validation must pass as-is —
     a seed class that fails adoption is an authoring-gap defect, not a
     harness workaround);
   - binds the seed's parameters from the comp's actual config **before
     `/prescribe-draw`** (CompetitionSetup params freeze once phases exist;
     no-default params *must* be bound — ales: `groupSize`, `minRounds`,
     `minNewGroup`), and BeforeFlying params before any flight
     (`targetTime` = 600, competition-wide bind — round-scoped binds are
     refused for non-PerRound parameters);
   - entries, realised draw, flights: **unchanged** from the parity replay.
3. **Parallel-run capture map** — the `declared` gate in
   `CaptureDurationInputs` reads the *published* definition's metrics. The
   per-pair assumption-emitting layer originally planned here is
   **superseded** (see the block above): the seed class declares its
   `whenNotRecorded` assumptions (`metric-absence-semantics.md` WI-4) and the
   engine resolves them — the harness only captures what the fixture data
   actually records, exactly as the parity replay does.
4. **Penalty path** — `RecordPenalty` validates infraction types against the
   adopted rules (`RecordCompetitionPenalty.cs:44` → the decide function), so
   GS penalty rows under a seed class need a declared-infraction mapping.
   Moot for ales (no `Scores.Penalty` rows) — implement the loud refusal
   (unknown infraction under a seed class = mapping gap, fail the scenario)
   and leave the mapping to the pair that needs it.

### WI-2 — Parallel-run ledger + comparator

1. **Ledger artifact** — one file per pair beside the fixture:
   `tests/GliderscoreFixtures/<slug>/parallel-run/<seed-slug>.json`.
   Schema distinct from `divergences.json` (never merged — story law):
   provenance block (pair, seed class + version, parameter bindings, the
   P1/P2 metric-mapping table with disclosures), then the triaged difference
   set — each entry: triage kind, grain, the difference in words, rulebook
   or local-practice citation.
2. **Grains** — the ledger speaks at two grains: **final placings** (the
   product — where the two would have split on the day) and **score cells**
   (diagnostic detail). The comparator's existing three-grain machinery
   computes the difference set between GS's oracle and the seed-run; the
   scenario asserts the computed set **equals** the triaged set.
3. **Escalation** — a computed difference outside the triaged set, or a
   triaged difference that fails to appear, fails the scenario. Classification
   into triage kind 3 (engine divergence) is never done by editing the
   ledger to fit — it escalates to a defect (story What).

### WI-3 — The ales pair end to end (`ParallelRunningAGliderscoreFixture.feature`)

1. New feature file beside `ReplayingAGliderscoreFixture.feature`,
   `@gliderscore` tag (existing filter discipline), one scenario:
   *"The ales-sample-comp parallel run under NZ ALES 200 reports exactly the
   triaged differences"* — Given the fixture, When run under the seed class
   with the recorded bindings and mappings, Then the parallel-run ledger is
   exactly the triaged set.
2. **Exactness discipline:** if the pair comes out exact (empty difference
   set vs empty triaged set), the scenario name and ledger state say so
   loudly — "may come out exact" must not quietly become a tuned expectation.
3. Run under **both stores** (`SOARSCORE_TEST_STORE=postgres|sqlite`) —
   a backend Soarscore claims to support passes this suite unchanged.
4. No corpus files change: fixture directory, `divergences.json`,
   `validate.py --index` (10/10) untouched — the parallel-run artifacts are
   additions beside them.

### WI-4 — Seed-class plumbing checks (`tools/Soarscore.SeedData`)

The seed JSONs were authored for the scoring corpus, not for a replayed
competition: confirm `80-nz-m-ales200.json` round-trips through
publish → create → bind → draw → score **as a live competition** (the corpus
tests exercise scoring arithmetic, not the draw/binding lifecycle). Any fix
lands in the seed authoring (`SeedNzAles200.cs` + re-emit), justified against
`docs/rules/nz/` — never against GS behaviour (decision 1 P3).

### WI-5 — Board reconciliation

1. **New backlog stub** — `f5j-christchurch-parallel-run-witness.md`: the
   guaranteed-divergence witness (decision 2), carrying the measured drop
   facts and the F5J metric-mapping questions (`startHeightRecorded`,
   `overflySeconds` P1/P2, the fly-off/`SplitByPromotion` spike — same open
   question the f3j pair raises).
2. **New backlog stub** — f3j-international parallel-run re-triage: the
   corrected difference list (rounding mode is the witnessed candidate),
   the overfly-semantics `fai-rules` question, the promotion spike.
3. **New backlog stub** — corpus-wide fixture→seed mapping table
   (index.md spirit: skip reasons per pair; NZ fixtures → NZ classes decided
   per pair, never by class-name resemblance).
4. Reconcile `tech-debt.md` / `deferred-decisions.md` as the story closes.

## Property-based testing assessment

No new CsCheck property is introduced. The story's assertions are set-equality
between a computed difference set and a curated triaged set, per hand-curated
pair — the invariant ("the report is exactly the triaged differences") is
structural and example-asserted per pair; generating random class/fixture
mutations would test the comparator, not the product. The engine's genuine
invariants stay covered by the existing scoring corpus property tests.

## Before starting (standing constraints)

- **Sibling story:** `kanban/backlog/fai-conformant-f3k-fixture-hunt.md` —
  its seed-swap attempt records the one known mechanism failure
  (`prescribeDraw.taskNotInCatalogue` on GS task code `A(2)`, a mismatch
  failing loudly, which is correct behaviour). The ales pair's single `D`
  task passes that gate.
- **Ledger semantics:** the parallel-run ledger is a *different* contract
  from the fixture divergence ledger — distinct comparator grain, distinct
  schema, never merged. Disclose deviations in the NZ fixtures'
  deviation-block style.
- **Anti-goal, stated hard:** seed classes are never tuned to GS. An
  authoring gap is fixed against the rule docs; a GS-vs-rulebook
  disagreement is reported, not reconciled.
- **House rule 2 cross-reference:** checked `docs/users.md` and
  `docs/non-functional-requirements.md` — no conflict found (users.md's
  "parallel" is role separation, unrelated); no new domain concepts —
  "parallel-run ledger", "triage kind", "assumed metric" are harness
  vocabulary (the "record scenario" precedent); nothing in `/docs` changes.
- **Naming:** the published class and created competition carry the seed
  class's own name/version (e.g. "ALES 200 (Altitude Limited Electric
  Soaring)", "NZMAA Section 5 Soaring, March 2024") so parallel-run artifacts
  are self-describing against the fixture's GS-mirrored twin.

## As built (2026-09-06)

All four WIs landed; every suite green. The plan text above is kept as
written — this section is the tree as built.

- **WI-1** — `tests/Soarscore.Acceptance.Tests/Support/Gliderscore/SeedDefinitionLoader.cs`
  (new); `ReplayDriver.ReplayAsync(fixture, ParallelRunMode?)` publishes the
  seed definition and binds `targetTime=600` (the comp's actual
  `durTargetTime`, unscoped BeforeFlying), `groupSize=10` (largest actual
  group), `minRounds=3` (rounds flown), `minNewGroup=1` (no re-flights in the
  fixture; `NZ.3.12.5 l` states no minimum — the least-constraining
  rulebook-consistent choice) before `/prescribe-draw`; the capture gate
  reads the published seed definition; penalties refuse loudly (unknown
  infraction under a seed class = mapping gap, per WI-1.4); the competition
  is named `{seed.Name} — {seed.Version}`; `ReplayOutcome` gained
  `DefinitionContentHash` + `ParallelRunBindings`. **Deviation from plan:**
  the fixture's round-scoped `RoundParameterBindings` are refused loudly in
  parallel-run mode — an anti-goal guard the plan did not name.
- **WI-2** — ledger
  `tests/GliderscoreFixtures/ales-sample-comp/parallel-run/80-nz-m-ales200.json`
  (provenance block with the bindings + metric-mapping disclosures
  `NZ.2.4.6` / `NZ.3.12.2 d` / `NZ.3.12.2 e` and four unwitnessed-candidate
  notes; triaged set = three kind-1 raw-grain entries, pilots 13/48/70,
  R1/G1, citing `NZ.3.12.1 e` + `NZ.3.12.3 d`);
  `Support/Gliderscore/ParallelRunLedger.cs` + `ParallelRunComparator.cs`
  (new), `Comparator.cs` widened private→internal only. **Two as-built
  decisions beyond the plan:** the parallel raw grain compares the seed-run's
  `PreNormalisationScore` as produced against GS's stored `RawScore` with
  **no D1 composition** — composing would re-enact GS's composition on the
  seed side and erase exactly the triaged difference (the anti-goal); and
  provenance is **verified, not just disclosed** (bindings ≡ actual config,
  seed identity ≡ adopted definition, disclosures ≡ declared
  `whenNotRecorded`). Conservation/team grains deliberately not run in the
  parallel comparator (ales is `UseTeams=false` anyway).
- **WI-3** — `Features/ParallelRunningAGliderscoreFixture.feature` (one
  scenario, `@gliderscore`) + `Steps/ParallelRunSteps.cs`: asserts verdict
  `MatchesTriagedSet` (an Exact verdict on this pair is a loud failure — a
  ledger/state contradiction), the untriaged/missing/provenance-break sets
  empty, the ranking grain exactly matching the GS oracle, and every triaged
  entry kind ∈ {1,2} with a citation. `@gliderscore` 14/14 and the full
  acceptance suite 74/74 on **both** stores. **Drift:** WI-3.4's
  "`validate.py --index` (10/10)" is now 11/11 — the corpus grew a fixture
  (`f5k-ni-round-2`) after this story was written; 11/11 passes, no corpus
  file changed.
- **WI-4** — no authoring gap: the seed class confirmed rulebook-faithful
  with citations; CS↔JSON in sync; no changes.

**The product:** the ales pair's witnessed difference set is exactly the
three raw-grain landing-composition entries — GS folds landing into the
pre-normalisation raw (160/275/330) where the rulebook composition gives
120/240/300; the normalised cells are exact (440/835/1030) and the final
placings are identical to the GS oracle (1/2/3/=4). **Where reality differed
from the plan:** the predicted normalise-rounding difference did *not*
materialise on this data (disclosed as an unwitnessed candidate in the ledger
provenance) — the witnessed difference is the raw composition instead.
WI-5's output: `kanban/backlog/f5j-christchurch-parallel-run-witness.md`,
`kanban/backlog/f3j-international-parallel-run-retriage.md`,
`kanban/backlog/corpus-parallel-run-mapping-table.md`, plus the
tech-debt/deferred-decisions reconciliation.
