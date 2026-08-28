# Story — Ranking's secondary key: RawScore tie-break

**Status:** Completed · **Raised:** 2026-08-26 (found by
`kanban/completed/gliderscore-replay-and-compare-harness.md` — story trap 3,
fired for real on `jerilderie-2010`) · **Fleshed out:** 2026-08-28 · **Amended:** 2026-08-29 (FAI tie-break
citations, NZ/F5L-silence correction, qualifying-position rung added to WI-3)

## What

`RankingEngine.Rank` sorts by Score only
(`src/Soarscore.Domain/Scoring/RankingEngine.cs:47`). The prior art's ladder is
**Score DESC, RawScore DESC** (`Rpt_Results_Calculations_MOD.vb:748`; see
`kanban/completed/resolve-gliderscore-scoring-arithmetic.md`, section *Ranking &
tie-breaks* — the authoritative spec, no need to reopen the VB source). Its
`RawScore` key is the **pre-drop normalised total**: Σ normalised cells less
aggregate penalties, *before* drops remove cells (arithmetic story :737-738).
Where two pilots tie on Score with different pre-drop totals, the prior art
displays distinct ranks and we display a shared place.

Witness (figures corrected during planning, 2026-08-28): jerilderie-2010 pilots
P4/P21 tie on post-drop Score **11784**; their pre-drop totals are **13305 vs
13280**, which is what orders GS's displayed "8"/"9"; we place both 8th.
⚠ The committed ledger entry cites "RawScore sums 9283 vs 9257" — those numbers
are the *per-flight* `Scores.RawScore` column sums, exactly the naming trap the
arithmetic story warns about at :738. Both quantities order this pair the same
way and both reproduce the fixture's full 63-pilot ladder (checked during
planning), so the witness does not discriminate the definitions — D1 below does.
The ledger text is historical; this story discharges the entry, not its wording.

## Why it matters

Every fixture with a Score tie and distinct pre-drop totals will flag comparator
grain 3. The harness works as intended — but the divergence is an engine gap,
not a rulebook conflict: the FAI classes break Score ties before sharing ranks,
each by its own mechanism (`F3B.2.8` additional full round; `F3K.10` and
`5.5.10.16–10.18` best dropped score then tie-break fly-off; `F3J.11` and
`5.5.11.13` qualifying position), while F5L and every NZ class state no
tie-break at all — silence is a CD decision. The engine ladder below is
class-agnostic either way: it coincides with FAI where FAI speaks (for
single-drop classes, PreDropScore *is* the "best dropped score" rung) and is
the prior art's established practice where the rulebook is silent.

## Decisions (settled during planning 2026-08-28; D1 gets Pete's sign-off at WI-0)

**D1 — the secondary key is the pre-drop total** *(recommended; sign-off
gate)*. For each competitor:

```
PreDropScore = Σ over phases ( phase Aggregate + Σ DroppedScores.Score )
             − (the same aggregate-penalty deduction Score loses)
```

Rationale, so nobody relitigates from scratch:

- *Authority.* The ladder key in the prior art is the pre-drop normalised total
  (arithmetic story :738), not the per-flight raw sum. The fixtures cannot
  discriminate (both reproduce 63/63 on jerilderie), so the written spec decides.
- *Rule-soundness.* The per-flight raw sum is on the *unnormalised* scale: a
  hard group yields high raws but low normalised cells, and in time-basis
  normalisation (GS option 2; NZ Class M) landing points enter *after*
  normalisation (arithmetic story D1; `docs/rules/nz/nz-ales-general-rules.md`
  bonus table) — a raw sum tie-break would not see landing points at all. The
  pre-drop total lives on the comparable scale and sees every component.

**D2 — rungs 1–2 only.** The further dropped-score rescue chain is gated on
F3K in the prior art (arithmetic story :754, `f3kRecord` at :809) — a
class-specific rung, which the core system may not know (CLAUDE.md core law).
No fixture exercises it: the NZ F3K ladder oracles are explicitly built from
rungs 1–2 (`tests/GliderscoreFixtures/f3k-june-2020/ladder.py:16-18`). Lands as
a `deferred-decisions.md` entry (WI-3), never as code here.

**D3 — no floor.** The prior art floors the total at 0 pre-drop (arithmetic
story :737); that bites only when penalties exceed the total and no fixture
witnesses it. Our `Score` is not floored at the aggregate stage today
(`ScoringService.cs:465-468`) and this story does not change `Score` semantics —
the secondary key follows our Score's existing conventions, floors and all.

**D4 — no rounding pass.** The prior art re-rounds both keys before comparing
(:740) because its Double sums drift; our normalised cells are exact decimals
already rounded to the class grid by `NormalisationEngine`, so sums of them are
on-grid and exact. No re-round.

**D5 — naming: `PreDropScore`.** Do **not** call it `RawScore`: that name is
taken by `TaskResult.RawScore` (per-flight, pre-normalisation — the very trap
the witness numbers fell into). The doc comment states the ladder role and cites
this story path; it must **not** name GliderScore (no `src/` file mentions the
prior art — harness story sign-off invariant). Glossary and class diagram stay
untouched by default: this is a derived quantity of existing concepts
(aggregate, drops, penalties), not a new domain concept. If Pete wants a
glossary entry, that is a separate approval (house rule 4), not part of this
story.

## Engine design (the entire `src/` change)

1. **`src/Soarscore.Domain/Scoring/ScoringResultTypes.cs`** — `PhaseScores`
   (:110-116) gains a computed property
   `PreDropAggregate => Aggregate + DroppedScores.Sum(s => s.Score)`
   (post-drop aggregate plus the dropped cells = the phase's pre-drop total;
   penalties are per-competitor and applied later, once).
2. **Same file** — `FinalCompetitorScore` (:135-139) gains
   `decimal PreDropScore` immediately after `Score`, with the D5 doc comment
   and a cite of this story.
3. **`src/Soarscore.Domain/Scoring/ScoringService.cs`** — in the phase loop,
   beside `totalsByCompetitor` (:447-448), mirror-accumulate
   `preDropTotals[competitorRef] += phaseScores.PreDropAggregate` (a second
   dictionary, same loop — it inherits whatever phase-combination semantics the
   existing accumulator has; multi-phase policies are unreachable today, so do
   not invent special handling). In the final loop (:452-469) pass
   `PreDropScore: preDropTotal - penaltyResult.Deduction` — the same deduction
   `Score` loses (the prior art subtracts penalties from both keys,
   arithmetic story :737-738).
4. **`src/Soarscore.Domain/Scoring/RankingEngine.cs`** — sort
   `OrderByDescending(s => s.Score).ThenByDescending(s => s.PreDropScore)`
   (:46-48); tie groups form only when **both** keys are equal (:57-62 compares
   the pair); the place-assignment/skip loop is otherwise unchanged.

**Invariant R — the property, named here per CLAUDE.md** (goes verbatim into the
property test's doc comment): *placings realise the total preorder induced by
the lexicographic ladder (Score DESC, PreDropScore DESC): for any two active
competitors a, b — ladder(a) >lex ladder(b) ⇒ place(a) < place(b); equal keys ⇒
equal place; placings are drawn from 1..n with standard skip-ahead numbering.*
Useful engine relationship: `PreDropScore − Score == Σ dropped cells ≥ 0` (same
deduction on both) — the property generator should respect it, making the
generated input class the real one.

## Known traps (pre-answered — do not reopen inside this story)

1. **No floor, no rounding** (D3/D4). If a reviewer asks for GS's 0-floor
   parity: out of scope, note it and move on.
2. **A NEW ranking diff in the harness means stop.** Both candidate definitions
   reproduce jerilderie 63/63, so WI-2 expects *zero* new diffs once the ledger
   entry is removed. If one appears: triage, do **not** silently switch to the
   per-flight raw sum, surface to Pete with the numbers.
3. **Zero-ties must stay shared.** ales-sample-comp's seven pilots at Score 0
   (pre-drop 0) still display "=4" — rung 2 separates nothing when both keys
   are equal. The jerilderie replay already pins the shared-place shape via
   other tied groups; ales pins the zero case.
4. **Order within a full tie is implementation-defined** (stable `OrderBy`
   today; the prior art's DataView sort is not stable either). Displayed shared
   places are unaffected. Don't chase determinism beyond the ladder.
5. **Construction sites.** `ScoringService` is the only production constructor
   of `FinalCompetitorScore`; `RankingEngineTests` and
   `RankingEnginePropertyTests` construct it positionally and will not compile
   until updated — that is WI-1 work, expected.
6. **Comparator code does not change.** Grain 3 already matches our placing `n`
   against oracle `"n"` and `"=n"` alike (harness D6); only jerilderie's
   `divergences.json` loses its entry.

## Work items

Each WI lands compiling with its checkpoint green; WI-1 → WI-2 are strictly
sequential; WI-3 closes out. Context budgets are deliberate — a sub-agent
given one WI needs only the files listed as *read*.

### WI-0 — Board and gate

`git mv` the story to `in-progress/`, update the status header in the same
commit. Get Pete's sign-off on **D1** (the analysis above is the pitch; the
default if he says "proceed as planned" is exactly D1–D5 as written).

### WI-1 — Engine + unit/property tests (Domain only)

*Read:* `RankingEngine.cs` (82 lines), `ScoringResultTypes.cs:100-146`,
`ScoringService.cs:439-478`, `RankingEngineTests.cs`,
`RankingEnginePropertyTests.cs`. *Do not open:* the fixture corpus, the
completed harness story, or any VB source — this WI is pure Domain work.
*Touch:* the four `src` edits above plus the two test files.

- `RankingEngineTests` additions: Score tie broken by PreDropScore (higher
  pre-drop wins, e.g. (1000,1000) vs (1000,1100) → places 2,1); full tie on
  both keys shares the place and skips; differing Scores ignore PreDropScore
  entirely; disqualified exclusion unchanged.
- `RankingEnginePropertyTests`: extend the generator to
  `(score, dropped ≥ 0, disqualified)` with `PreDropScore = score + dropped`
  (trap-5/Invariant-R input class); generalise the pairwise assertions to the
  lex-ladder form of Invariant R; update the doc comment to cite Invariant R
  and this story (it currently cites scoring-steel-thread WI-5 invariant 5 —
  keep that lineage mention).

**Checkpoint:** `dotnet build Soarscore.sln`; then
`dotnet test tests/Soarscore.Domain.Tests tests/Soarscore.Application.Tests
tests/Soarscore.Architecture.Tests` green.

### WI-2 — Harness discharge (the ledger entry)

*Read:* `tests/GliderscoreFixtures/jerilderie-2010/divergences.json` (1 entry).
*Touch:* that file only — delete the pilot-21 entry so the file is `[]`
(empty-array ledgers are precedented: both f5j fixtures ship one). Do not edit
the comparator, the driver, or any other fixture.

Run: `SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests`
(fast loop), then the same with `postgres` wherever Docker exists.

**Expected:** P4 places 8 / P21 places 9, matching oracle "8"/"9"; jerilderie's
ledger empty; **every other fixture's grains and ledgers byte-identical**; the
`=4` zero group on ales-sample-comp still shared (trap 3). Any new diff → trap
2: stop, triage, surface.

**Checkpoint:** acceptance suite green under both stores with jerilderie's
ledger empty.

### WI-3 — Board reconciliation and close-out

- `kanban/deferred-decisions.md`: new entry under a Scoring-appropriate heading
  — *ranking ladder stops at rung 2; the dropped-score rescue chain (rung 3) is
  class-gated prior art and stays out until a fixture oracle or class
  definition demands it, as does the F3J/F5J fly-off rung (qualifying position
  breaks fly-off ties — `F3J.11`, `5.5.11.13` — a class-specific key the fixed
  ladder cannot reproduce)* (D2 reasoning, pointer to this story and to the
  arithmetic story's ladder section; both rungs belong to the tie-break policy
  layer, `kanban/backlog/tie-break-policy-in-class-definition.md`).
- `kanban/tech-debt.md`: reconcile; nothing is expected (house rules 5–6).
- `git mv` to `completed/`, status header same commit.

**Finish line:** `dotnet test Soarscore.sln` green, plus
`tests/Soarscore.Acceptance.Tests` under both `SOARSCORE_TEST_STORE` values.
Known flake: solution-wide Marten migration race (`tech-debt.md` last item) —
re-run the project alone before diagnosing.

## Out of scope (restated for sign-off)

- Rung-3 dropped-score rescue chain, and the F3J/F5J qualifying-position
  fly-off tie-break (D2 → deferred-decisions entry; the policy layer is
  `kanban/backlog/tie-break-policy-in-class-definition.md`).
- Aggregate-stage zero-floor parity with the prior art (D3).
- HTTP exposure of `PreDropScore` — no view change
  (`pre-normalisation-score-view-field.md` is a different, already-declined
  field).
- A `HiddenRanking`-style distinct total order — display-only prior-art
  machinery; our shared-place placings remain the single truth.
- Glossary/notation edits (D5 — approval-gated, default none).

## Story invariant for sign-off

The ladder (Score DESC, PreDropScore DESC) is implemented once, in
`RankingEngine.Rank`; `PreDropScore` is computed from the existing aggregates
and loses exactly the penalty deduction `Score` loses; jerilderie-2010 replays
exact at all three grains with an empty ledger; every other fixture's grains
and ledgers are unchanged; both stores pass; no `src/` file names the prior
art; no `/docs` change.

## As built (2026-08-29)

- **WI-0.** D1 sign-off taken from the user's instruction to implement the
  sharpened story as written — the stated default ("proceed as planned" =
  D1–D5 exactly).
- **WI-1.** As planned, no deviations. Checkpoint green: 515 Domain /
  216 Application / 7 Architecture tests.
- **WI-2.** `divergences.json` emptied as planned, but the suite did not go
  green on that alone: the jerilderie BDD scenario
  (`ReplayingAGliderscoreFixture.feature`) pinned the ledger at its old
  triaged size — title "modulo its ledgered tie-order divergence", a
  divergence-ID step, and "the fixture ledger records exactly 1 accepted
  divergences", whose guard exists precisely to force a re-triage when the
  ledger changes. This story is that re-triage, so the scenario was
  reconciled to the empty-ledger idiom `f3k-sample-comp` already uses
  ("the fixture carries no ledgered divergences"). Strictly beyond WI-2's
  "touch that file only", so recorded here; the comparator, driver and all
  other fixtures were untouched. The replay itself produced **zero** new
  diffs — trap 2 never fired; P4/P21 place 8/9 against oracle "8"/"9" and
  the ales zero-tie group still shares.
- **WI-2 postgres leg did not run:** no Docker in the implementing
  environment; the sqlite leg is green (64/64). Per the story, postgres runs
  "wherever Docker exists" — run it before claiming both-store proof.
- **WI-3.** Deferred-decisions entry added under a new "Scoring and ranking"
  heading; `tech-debt.md` unchanged (nothing deferred, as predicted).
