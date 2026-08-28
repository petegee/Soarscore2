# Story — Normalisation lower clamp (floor NormalisedScore at 0)

**Status:** Completed · **Raised:** 2026-08-28 (found by
`kanban/completed/nz-fixture-replay-scenarios.md` — its D2/N1) ·
**Plan:** 2026-08-28 (decisions settled and WIs written below — take the story
into `in-progress/` before coding)

## What

A lower clamp on the normalised score in `NormalisationEngine`
(`src/Soarscore.Domain/Scoring/NormalisationEngine.cs:117-130` has no lower
bound today): after rounding — and after any `ScoreNormalised` terms — floor
the normalised value at 0, matching GliderScore's per-branch floors
(`Scoring_MOD.vb:310` option-1, `:367` option-2 sum). One engine change, one
witness fixture's ledger emptied, the harness's N1 token retired.

## Why it matters

A raw score can legitimately be negative (F5J: a huge launch-height deduction
can exceed flight time plus landing points — comp 121 witness cell
`1/3/3/0/99`: raw −2026, GS normalises to 0.0, ours to −4031.8). The corpus
carries exactly one such cell (`f5j-nz-south-island`), so the fixture replays
today only because the divergence is ledgered under citation token **N1**.
Landing this story discharges comp 121's four N1 ledger entries (1 normalised
+ 3 ranking knock-ons) and lets that scenario assert an empty ledger — the
last corpus-wide arithmetic divergence the engine owns.

## Context map (keep the implementer's window small)

Everything needed to decide and build is quoted below. Deliberately **do not
read** (they are historical records of record; the relevant text is already
here):

- `kanban/completed/nz-fixture-replay-scenarios.md` (715 lines) — only §D2/N1
  matters and it is quoted in this file's decisions and in the ledger text.
- `kanban/completed/resolve-gliderscore-scoring-arithmetic.md` (862 lines) —
  the GS branch evidence is summarised in D1 below.

Do open (small, necessary):

- `src/Soarscore.Domain/Scoring/NormalisationEngine.cs` (162 lines) — the
  change site.
- `tests/Soarscore.Domain.Tests/NormalisationEngineTests.cs` and
  `NormalisationEnginePropertyTests.cs` — the test patterns to follow.
- `tests/Soarscore.Acceptance.Tests/Features/ReplayingAGliderscoreFixture.feature`
  (:103-111) and `Steps/ReplaySteps.cs` (:112-158) — the ledger pins to update.
- `tests/GliderscoreFixtures/f5j-nz-south-island/divergences.json` — the ledger
  to empty (quoted below).

## Decisions settled during planning (do not relitigate)

### D1 — The clamp is uniform: every arrangement with a `Normalisation`, both directions

GS floors the normalised score in every normalisation branch that produces one:

- option-1 points (Duration): `max(0, RoundHalfUp1dp(1000·Raw/MaxRaw))` —
  `Scoring_MOD.vb:310`;
- option-2 time (ALES): the assembled sum `NormTimeScore + LandingScore` is
  floored after landing is added — `Scoring_MOD.vb:367` (sum itself unre-rounded);
- speed: raw ≤ 0 → NS = 0 outright — `Scoring_MOD.vb:479`;
- families 2/4/5/6 points branch (`Scoring_MOD.vb:423`): no negative witness
  exists in the corpus, so a floor there is unverified in GS source — but
  unobservable on the corpus either way, and consistent with every other branch.

Our model has exactly one normalised path (`NormalisationEngine.Normalise`:
round → `ScoreNormalised` terms → re-round); GS's option 1/2 distinction is an
implementation taxonomy of GS's Duration family, **not** a concept of the
Competition Class model. Replicating the per-branch floors would mean teaching
the core which arrangement a task is — a class-specific leak against CLAUDE.md's
core architectural law (and it would need a new field on the class model:
glossary-gated). Therefore: **one unconditional clamp at the end of the
normalised path**, all directions, every task whose `Normalise` is non-null.
For LowerIsBetter (F3B speed/F3F) it is a no-op today (raws are positive
seconds; GS's own speed branch writes 0 for raw ≤ 0) — keep it unconditional;
no direction branch.

### D2 — Rulebook position (cited): the clamp implements "negative → zero" at the normalised grain

- **F5J 5.5.11.12 f** (verbatim): *"Where the score is negative (below zero), a
  zero score will be recorded. Note that any penalty points applied in the
  round will remain effective. (5.5.11.4)."* Under a literal reading the
  **raw** total (flight points + landing − height deduction) would be floored.
  GS instead persists the negative raw (comp 121's −2026 sits on its score
  sheet) and implements the rule's *outcome* one step later, at the normalised
  grain. The fixture oracle pins that raw — the raw-grain comparison of
  `f5j-nz-south-island` matches on −2026 today — so this story floors **only
  the normalised value** and leaves raw composition untouched. The ranking
  number then carries exactly what the rulebook records ("a zero score will be
  recorded") while the score-sheet raw keeps the audited GS value.
- The fai-rules skill's cross-class FAI invariant states the same outcome: a
  raw score that would go negative is recorded as zero. For every FAI class
  except F5J the raw cannot go negative without a penalty (which `PenaltyEngine`
  already floors at the raw stage per C.19/General §6), so the clamp is a no-op
  for them — evidence that a uniform engine-level clamp is safe and generic.
- **NZMAA**: `NZ.3.12.3` (Class M scoring) is silent on negative scores; the NZ
  fixtures' raws are non-negative (time-points and landing bonuses), so the
  clamp is a no-op there and consistent with GS's option-2 floor (`:367`) that
  the NZ ALES fixtures already replay under.
- Citations to carry into the code comment: `F5J 5.5.11.12 f`;
  `Scoring_MOD.vb:310` and `:367`; `nz-fixture-replay-scenarios.md D2/N1`; this
  story's path.

### D3 — Placement and exact form

Clamp **after** the post-terms re-round (step 8 in `NormalisationEngine.cs`),
immediately before writing `resultBuilder`, i.e. the final assembled value is
`max(0, round(round(1000·raw/winnerRaw) + Σ ScoreNormalised terms))`. Two
reasons, both GS-anchored:

- GS option-1 floors after rounding (`:310`) — clamping before rounding is
  equivalent on the grid but clamping after mirrors the cited branch exactly.
- GS option-2 floors the **sum after landing** (`:367`) — clamping before the
  `ScoreNormalised` terms would mis-score the rescue case (norm −100 + landing
  +130 must score 30, not `max(0,−100)+130` = 40). No fixture witnesses this
  yet; the example test in WI-2 pins it.

Exact form: `if (normalised <= 0m) normalised = 0m;` — **`<=`, not `<`, and a
literal `0m`, not `Math.Max`**: rounding a value in (−0.05·precision, 0) can
produce decimal negative zero (`-0.0m`), which serialises as `-0.0` and reads
as an artefact; `<= 0m` guarantees a literal positive zero. Do **not** apply it
to the two early-continue branches (NoResult / no-valid-winner) — they already
write `0m`.

### D4 — Deliberately out of scope (do not "fix" here)

- **Pass-through (`Normalise` null) stays an identity on raws, including
  negative ones.** GS's option-0 floors the rounded raw (`Scoring_MOD.vb:277`),
  but no fixture exercises a no-normalisation negative raw, and flooring the
  pass-through would change raw-grain semantics the oracle pins. If a future
  fixture ever witnesses it, that is a new triaged divergence, not this story.
- **Aggregate floor ≥ 0.** GS floors the final score after penalties
  (`Rpt_Results_Overall_MOD.vb:2690-2712`); our `ScoringService.ScoreCompetition`
  (`Score: totalScore - penaltyResult.Deduction`) does not. No fixture evidence;
  aggregate penalty behaviour is the adjacent backlog story
  `aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md`. With
  the NS clamp landed, comp 121's ranking matches GS exactly (see ground
  truth), so nothing aggregate-level is needed for the corpus.
- `PenaltyEngine.ApplyRawPenalties`' existing raw floor (`PenaltyEngine.cs:118`,
  the C.19 analogue) is untouched and unrelated.
- No class-definition/seed changes, no `/docs` changes, no glossary addition —
  the clamp is normalisation arithmetic, not a new concept.

## Ground truth — witness cell and expected post-change outcomes

Comp 121 (`f5j-nz-south-island`), round 3, group 3, pilot 99, reflight 0:

- Packed time 714 → 434 s; height 1000 m → 100 + 3·800 = 2500 against it;
  landing 90 → scheme-11 40. Raw = 434 − 2500 + 40 = **−2026** (both sides).
- Group basis (winner raw) = 502.5. Normalised = 1000·(−2026)/502.5 =
  −4031.840… → HalfUp 0.1 → −4031.8 → GS floors to **0.0**; ours emits −4031.8
  today. The fixture's `expected-scores.json` already carries GS's 0.0 — the
  oracle does not change.
- After the clamp, all four ledger entries vanish and the replay is exact at
  all three grains: cell normalised 0.0; pilot 99 final 3290.0 + 4031.8 =
  **7321.8**, place **10** (oracle '10'); pilot 131 back to place **11**
  (final 6149.6); pilot 137 to place **12** (final 5158.0); pilot 86 stays 13.
  No drops fire anywhere in the comp, so the conservation self-check holds
  unchanged.

## Plan

### WI-1 — Engine clamp (`src/Soarscore.Domain/Scoring/NormalisationEngine.cs`)

At the end of the normalised-cell computation (after step 8's re-round, before
`resultBuilder[competitorRef] = taskResult with { RawScore = normalised }` at
:151), add per D3:

```csharp
// Lower clamp — F5J 5.5.11.12 f ("where the score is negative, a zero score
// will be recorded"), implemented at the normalised grain per GS's floors
// (Scoring_MOD.vb:310 option-1, :367 option-2 sum); nz-fixture-replay-scenarios.md
// D2/N1, kanban/backlog/normalisation-lower-clamp.md D1-D3. `<=` so a rounded
// decimal -0.0 cannot escape.
if (normalised <= 0m)
    normalised = 0m;
```

Update the file-header comment (lines 1-6) to mention the clamp in the stage
list. Nothing else in the file changes: the pass-through branch (:49-61), the
`winnerRaw == 0m` zeroing (:91-96) and the LowerIsBetter zero-raw backstop
(:100-115) are untouched.

### WI-2 — Example tests (`tests/Soarscore.Domain.Tests/NormalisationEngineTests.cs`)

Follow the file's existing `MakeNormalisedTask`/`ValidResult` helper style.
Four tests, exact numbers:

1. **Witness cell** — HigherIsBetter, winner 1000, HalfUp 0.1, raws
   `{502.5, −2026}` → winner 1000; loser 0 (the −4031.8 pre-clamp value must
   never be observable). Asserts `RawScore.Should().Be(0m)`.
2. **Terms rescue before clamp** — HigherIsBetter, winner 1000, raws
   `{100, −10}` (loser normalises −100), one `ScoreNormalised` term contributing
   +130 → loser scores **30**, not 40 (clamp must not pre-empt terms) and not 0
   (terms must not be floored away). Uses the property-test file's shape for a
   single flat `ScoreTerm` contribution if one exists there, else build the
   minimal term — mirror however `ScoreNormalised` terms are constructed in
   existing tests; keep it to one term + one flight.
3. **Negative-zero boundary** — HigherIsBetter, winner 1000, HalfUp 0.1, raws
   `{100, −0.004}` → loser 1000·(−0.004)/100 = −0.04 → rounds to −0.0 → clamps
   to exactly `0m`. Asserts `== 0m`.
4. **Pass-through asymmetry** — no-normalisation task, raws `{600, −2026}` →
   passes through unchanged (`−2026` stays), pinning D4's exclusion.

### WI-3 — Property tests (`tests/Soarscore.Domain.Tests/NormalisationEnginePropertyTests.cs`)

Add a negative-capable generator **beside** the existing ones (do not widen the
shared `RawScore` generator — the existing five properties stay meaningful
untouched):

```csharp
private static readonly Gen<decimal> SignedRawScore =
    Gen.Int[-100_000, 100_000].Select(i => i / 100m);
```

Two new facts, with the invariants named in their doc comments:

- **`No_normalised_cell_is_negative`** (invariant: *with a `Normalisation`
  present, every emitted normalised value is ≥ 0, whatever the raws*) —
  generate direction + winnerScore + a group where a positive basis raw
  (from the existing positive `RawScore` gen) coexists with signed raws; every
  valid entry's result ≥ 0, and the winner's result == winnerScore whenever
  the winner raw > 0.
- **`Clamping_preserves_weak_order`** (invariant: *the clamp collapses but
  never inverts* — for raws a ≥ b the normalised pair satisfies n(a) ≥ n(b)
  for HigherIsBetter, n(a) ≤ n(b) for LowerIsBetter, signed raws included;
  strict order may collapse to equality at the floor but never flips).

### WI-4 — Corpus + harness (`tests/Soarscore.Acceptance.Tests`, `tests/GliderscoreFixtures`)

1. `tests/GliderscoreFixtures/f5j-nz-south-island/divergences.json` → `[]`
   (single line, matching `f5j-hawkes-bay-trials/divergences.json`). Do not
   edit the other fixtures' ledgers.
2. `ReplayingAGliderscoreFixture.feature:103-111` — retitle the scenario to
   the exact-match form ("…reproduces GliderScore exactly at all three
   grains", like f5j-hawkes-bay-trials) and replace its two ledger Then-steps
   (`cites an arithmetic-story divergence ID`, `records exactly 4`) with
   `And the fixture carries no ledgered divergences`.
3. `Steps/ReplaySteps.cs` — delete the N1 paragraph of the comment block
   (:129-137) and drop `\bN1\b` from the citation regex (:152), leaving a
   one-line note that N1 was discharged by this story (the token's referent is
   fixed; keeping it live would license future untriaged divergences). The
   trap-3 and R1 commentary stays. No other fixture's ledger cites N1, so the
   widened-to-narrowed regex is safe — the "records exactly N" pins on the
   other four ledgers are untouched.

### WI-5 — Verification (definition of done)

```bash
dotnet test tests/Soarscore.Domain.Tests
dotnet test tests/Soarscore.Application.Tests
dotnet test tests/Soarscore.Architecture.Tests
dotnet test tests/Soarscore.Infrastructure.Tests   # SQLite backend runs in the fast loop
SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests
dotnet test tests/Soarscore.Acceptance.Tests        # postgres (Testcontainers) run
```

All green with no fixture oracle or expected-result edits anywhere. The
f5j-nz-south-island scenario passes with an empty ledger; every other fixture
(ales-sample-comp especially — the option-2 `ScoreNormalised` shape) replays
byte-identically to today. No event or read-model migration is needed: the
normalised value is derived at read time by `ScoringService`, nothing persists
it.

### WI-6 — Board and housekeeping

Move this file to `kanban/completed/` (`git mv`), set the `**Status:**` header,
reconcile `kanban/tech-debt.md` and `kanban/deferred-decisions.md` (nothing is
expected to be owed — record anything the implementation surfaces instead of
silently absorbing it), and run `graphify update .` so the graph tracks the
engine change. Completed-story plan notes: this plan's WIs describe the tree
as built; cite `file:line` freshly at completion time.

## As built (2026-08-28) — WI-1..WI-4 landed, WI-5 fast loop green

All code changes are on disk and every non-Docker suite is green:

- WI-1 — clamp at `src/Soarscore.Domain/Scoring/NormalisationEngine.cs:152-159`
  (step 9, after the step-8 re-round, before the `resultBuilder` write), file
  header updated. `<= 0m` → literal `0m` per D3.
- WI-2 — four example tests at
  `tests/Soarscore.Domain.Tests/NormalisationEngineTests.cs:187` (witness cell),
  `:215` (terms rescue, `ConstantTerm { Value = 130m }`, loser −100 + 130 = 30),
  `:240` (negative-zero boundary; also pins `ToString` = `"0"` so a surviving
  `-0.0m` would fail), `:270` (pass-through asymmetry); helper
  `ValidResultWithFlight` beside `ValidResult`.
- WI-3 — `SignedRawScore` gen at
  `tests/Soarscore.Domain.Tests/NormalisationEnginePropertyTests.cs:22`;
  `No_normalised_cell_is_negative` at `:204` (positive basis + signed raws,
  every result ≥ 0, winner == WinnerScore when winnerRaw > 0);
  `Clamping_preserves_weak_order` at `:249` (guarded on winnerRaw > 0 — the
  domain where the pre-clamp transform is order-preserving; a negative winner
  inverts the ratio formula itself, out of scope per D1).
- WI-4 — `f5j-nz-south-island/divergences.json` is `[]`; feature scenario
  retitled to the exact-match form at
  `tests/Soarscore.Acceptance.Tests/Features/ReplayingAGliderscoreFixture.feature:103`
  with `And the fixture carries no ledgered divergences`; N1 paragraph replaced
  by a discharge note at `tests/Soarscore.Acceptance.Tests/Steps/ReplaySteps.cs:129`
  and `\bN1\b` dropped from the regex. No other fixture cites N1.
- WI-5 green so far: Domain 512/512, Application 216/216, Architecture 7/7,
  Infrastructure fast loop (`--filter 'Category!=Storage'`) 64/64, Acceptance
  sqlite (`SOARSCORE_TEST_STORE=sqlite`) 64/64 — f5j-nz-south-island replays
  exact with the empty ledger; ales-sample-comp byte-identical.

**WI-5 verification (all green, no fixture oracle or expected-result edits
anywhere):** Domain 512/512, Application 216/216, Architecture 7/7,
Infrastructure full suite 128/128 (postgres Testcontainers + Fisher/SQLite in
one run), Acceptance sqlite (`SOARSCORE_TEST_STORE=sqlite`) 64/64 and
Acceptance postgres (default) 64/64 — f5j-nz-south-island replays exact with
the empty ledger; ales-sample-comp and every other fixture byte-identical to
before. No event or read-model migration was needed: the normalised value is
derived at read time by `ScoringService`, nothing persists it.

WI-6 done: story moved to `kanban/completed/`; the pass-through-as-identity
(GS option-0) deferral recorded in `kanban/deferred-decisions.md` under
"GliderScore replay harness"; nothing owed to `tech-debt.md` (D4's aggregate
floor is already the adjacent backlog story; nothing else surfaced).
