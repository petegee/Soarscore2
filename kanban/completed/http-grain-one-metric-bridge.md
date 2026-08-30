# Story — Evaluate `scoreNormalised` terms against decoded slot metrics so option-2 fixtures also lose their in-process grain-1 dependency

**Status:** Completed 2026-08-30 · **Raised:** 2026-08-29 from
`kanban/completed/pre-normalisation-score-view-field.md` WI-4 (house rule 6) ·
**Fleshed out:** 2026-08-30

## What

Extend the Gliderscore comparator's HTTP grain-1 path so fixtures whose tasks
carry `scoreNormalised` terms (option-2: landing points authored there) can
also take it: fetch `preNormalisationScore` from `GET /task-round-result`,
evaluate the task's `scoreNormalised` terms against each slot's decoded flight
metrics (read from the replayed entry streams), and add the contributions onto
the fetched value. This replaces the last in-process grain-1 recompute —
`CompareRawGrainAsync`'s full pipeline copy (`GsEquivalentRaw` +
`EvaluatePostNormalisationTerm` + `EvaluateLookup` + `EntryPenalties` +
`ApplyRawPenalties`) — with a fetch plus a mirror-evaluated composition, and
retires the grain-1 classification split so **one mechanism serves all
fixtures**. Comparator-side only: no `src/` change of any kind.

## Why it matters

`pre-normalisation-score-view-field.md` flipped grain 1 to
`GET /task-round-result`'s `preNormalisationScore` for the nine
`scoreNormalised`-free fixtures. `ales-sample-comp` — the one active fixture
whose task D authors landing points inside `scoreNormalised` — still runs the
full in-process pipeline copy, because HTTP does not carry per-flight metrics.
That keeps a whole second comparison mechanism, and its term-kind mirror,
alive for one fixture. Every extra mechanism is a second thing that can drift
from the engine it mirrors; retiring it leaves the harness with exactly one
grain-1 path, proven by the same golden fixtures.

## Before starting

- Re-read `kanban/completed/pre-normalisation-score-view-field.md` (its D6 and
  as-built are this story's direct parent) and the grain-1 header paragraph of
  `tests/Soarscore.Acceptance.Tests/Support/Gliderscore/Comparator.cs:6-40`.
- **The engine arithmetic stays untouched; this is comparator-side only.** No
  `src/` edit is in scope — not the view, not the engine, not the API. If you
  find yourself editing `src/`, stop and re-read D7.
- The stub's open question — does the term-kind mirror stay sufficient for
  `ales-sample-comp`? — is now **answered: yes, verified against the fixture's
  committed `class-definition.json`**: task D's `scoreNormalised` holds exactly
  one `LookupTerm` over `landingDistance` (11 rows, last unbounded). The
  mirror's Constant/Lookup-only support with loud refusal remains the widening
  gate for future term kinds.
- Verified fixture facts the plan leans on (re-verify if the corpus grows):
  - `ales-sample-comp` is duration-family (`GSCompClass: DurALES`), so the
    replay opens **at most one flight per entry** — placeholders stay
    flight-less (`ReplayDriver.CaptureDurationInputs`, D4); every capture is
    `Flight: 1`.
  - Task D: `flights: last`, `maxLaunches: 1`, **no target values**, no
    `validWhen`/`flightValidWhen`, `penalties: []` at class level, no
    `Scores.Penalty` rows, no reflight rows; 3 rounds, 1 group, 30 slots of
    which 27 are flight-less placeholders (R2–R3 wholly unflown).
  - `divergences.json` does not exist for this fixture — its ledger is empty,
    so "zero behavioural change" is checkable as "no ledger file appears".
- Cross-reference check done at flesh-out time (rule 2): no conflict with
  `docs/users.md`, NFR-1…NFR-4, the rule corpus, `tech-debt.md`, or
  `deferred-decisions.md` (its grain-1-adjacent entries — binary64 oracle
  artefacts, pass-through negatives — are orthogonal and untouched). Do not
  edit anything under `/docs` (house rules 3–4); no glossary concept arises —
  "decoded slot metrics", "mirror", "pre-normalisation score" are all
  established harness vocabulary.

---

# Plan

## Design decisions — settled here, do not relitigate

### D1 — The composition formula and its row condition

For every fetched row, the comparator's grain-1 "ours" value is:

```
composed = row.PreNormalisationScore
         + (row.State == TaskResultState.Valid
               ? Σ over the slot entry's flights
                     Σ over the round task's ScoreNormalised terms
                         MirrorEvaluateTerm(term, decodedMetrics(flight))
               : 0m)
```

Bindings of the decision, each binding:

- The state condition mirrors `GsEquivalentRaw` exactly: the engine evaluates
  `ScoreNormalised` only for Valid rows with a non-null Selection
  (`NormalisationEngine.cs:155-167`); NoResult rows contribute nothing. The
  HTTP row's `State` (`CompetitorTaskResultView.State`,
  `ScoreTaskRound.cs:32-37`) is the engine's post-raw-penalty state, which is
  exactly what gated step 7 — so the condition transfers faithfully. Zeroed-by-
  penalty rows (Zero* effects) arrive as NoResult and correctly contribute 0.
- The fetched `preNormalisationScore` is the engine's raw score *after*
  `ApplyRawPenalties` (it is captured entering `Normalise` — prior story D1),
  which honours zeroing effects only. Raw-stage deductions remain parked
  (`kanban/backlog/entry-scoped-deduct-points-penalties-inert.md`); if one ever
  lands, that is a new triage, not this story's business.
- Zero-winner-guard rows (Valid rows of a group whose winner raw is 0) still
  compose: the overwrite happens after capture, and `GsEquivalentRaw` — which
  the bridge replaces — predates the guard too. Both sides speak GS's
  composition; do not special-case them.
- Exact decimal comparison everywhere, unchanged: `==`, no tolerance
  (Comparator.cs:54-57).

### D2 — Which flights: all of the entry's, guarded to be equivalent to the selection

The engine sums its `ScoreNormalised` contributions over the **selected**
flights (`NormalisationEngine.cs:158`), and HTTP does not carry the selection.
The bridge evaluates over **all flights of the slot's entry** instead, which is
equivalent to the selection under guards that refuse loudly rather than guess
(same posture as the prior story's D6.2 — runtime-derived from definition and
data, never per-slug booleans):

1. **Flight-count guard (per slot, when the row is Valid).** The slot's entry
   must hold at most one flight, unless the task's `FlightSelection` is
   `AllFlights` (where selection == all by construction, and `AllFlights`
   carries no targets). With ≤1 flight, every selection kind the model has
   (`FlightSelector.SelectFlights`) yields exactly that flight for a Valid row
   — so all-flights ≡ selected-flights, and the flight-count assumption the
   bridge leans on is checked, not assumed. Refuse with `NotSupportedException`
   naming the fixture, the slot, the count, and the selection kind.
2. **Contradiction guard.** `State == Valid` with a flight-less entry is
   impossible under `FlightSelector` (no flights ⇒ NoResult) — throw
   `InvalidOperationException` rather than silently contribute 0.
3. **Target-clamp guard (per task with non-empty `ScoreNormalised`).** The
   engine evaluates `ScoreNormalised` over the selection's metrics **after**
   `ApplyTargets`/`ClampAndRecompute` rewrote them (`FlightSelector.cs:159-259`),
   while the bridge decodes unclamped metrics from the entry stream. Refuse
   loudly (`NotSupportedException`) when a task with non-empty
   `ScoreNormalised` declares a target-bearing selection (`BestNFlights` or
   `ExactlyNInOrder` with non-empty `TargetValues`). Never reached by the
   corpus — task D declares none — but it keeps the bridge
   exact-by-construction for everything it accepts.

Rejected alternative: call `ScoringService.SelectFlights` in-process to obtain
the true selection. Rejected because it keeps the pipeline dependency
(`ParameterResolver` → `MeasurementDigest` → `FlightInterpreter` →
`SelectFlights`) this story exists to retire — the raw NUMBER would come from
HTTP but its computation would still run locally, and the "one mechanism" goal
would be quietly lost. The guards make the cheap path sound; a fixture that
trip one is a new triage, not a silent mis-score.

### D3 — Term source: the resolved task, per round

Resolve the round's task exactly as the legacy loop did —
`classDef.Phases[outcome.PhaseOrdinal].Tasks.Single(t => t.Code ==
outcome.TaskCodeByRoundNo[roundOfView])`, bindings flattened with
`ScoringService.FlattenParameterBindings(competition.ParameterBindings,
outcome.PhaseOrdinal, outcome.RoundOrdinalByRoundNo[roundNo])`, then
`ParameterResolver.ResolveTask` — and read `ScoreNormalised` off the
`ResolvedTask`. The per-round resolution is load-bearing (the prior story's
WI-4 lesson: f3k-sample-comp prescribes a different task each round), and it
keeps the bridge engine-faithful. Verified non-obvious fact making this cheap:
the mirror-supported term kinds are parameter-free **by type** —
`ConstantTerm.Value` is `decimal`, `LookupRow` is decimals
(`ScoringVocabulary.cs:166,218-222`), and
`ParameterResolver.ScoreTermReferences` returns false for both — so resolution
is identity for everything the mirror accepts today. Widening the mirror to a
parameter-carrying kind (`RateTerm.Cap`, `PiecewiseTerm.Origin`) then requires
exactly this resolved-task plumbing, which is why it is built now.

### D4 — Metrics construction: decode, plus the intrinsic — do not call Interpret

Build each flight's metrics exactly as `FlightInterpreter.Interpret` does
(`FlightInterpreter.cs:34-38`): `MeasurementDigest.Resolve(flight).Metrics`
plus the `flight.sequence` intrinsic (`"flight.sequence"` — the literal is
fine; cite `FlightInterpreter.Intrinsic`). This makes the mirror's term input
byte-equivalent to what the engine's step 7 reads, modulo the clamping D2.3
refuses. Do **not** call `FlightInterpreter.Interpret`: evaluating raw score
terms is not the bridge's business, and skipping it costs nothing — the
engine's step-7 metrics are those same resolved metrics with the intrinsic,
regardless of `flightValidWhen` outcomes (an invalidated flight keeps its
metrics; see trap 3). The `MeasurementDigest.Resolve` call is the same decode
the legacy path fed `Interpret`, so no decoding behaviour is introduced.

### D5 — The classification split dissolves; the mirror survives, re-anchored

After the flip **every** fixture routes to the HTTP bridge — for the nine
`scoreNormalised`-free ones the contribution is an empty sum, so the bridge
degenerates to exactly today's HTTP path. Consequences:

- `ScoreNormalisedFree` (`Comparator.cs:334-336`) and the belt-and-braces
  classification guard inside `CompareRawGrainViaHttpAsync` (`:360-365`) die.
- The bridge extends `CompareRawGrainViaHttpAsync` in place; when the legacy
  path is deleted it takes (or keeps — either name is acceptable, pick one)
  the plain grain-1 role. Its loop, `RecordCell` bookkeeping, `AddIfDifferent`
  calls, and the fetched-row universe (all rows, no Role filter — prior
  story's as-built, trap 10) stay **byte-identical**; the only new step per
  row is D1's contribution, sitting between `RecordCell` and `AddIfDifferent`.
- `EvaluatePostNormalisationTerm` + `EvaluateLookup` survive with their names
  (they evaluate the `ScoreNormalised` stage — still accurate), updated doc
  comments to say they now evaluate against decoded slot metrics onto the
  fetched `preNormalisationScore`, and the same loud refusal for unsupported
  kinds, with the message's anchor text updated.
- The comparator's grain-1 header paragraph (`Comparator.cs:6-40`) is
  rewritten for the single-mechanism reality; the Q1 in-process mirror's
  history lives in the completed stories, not in this file.

### D6 — Transitional parity gate, then delete (the prior story's proven pattern)

While both mechanisms exist (`CompareRawGrainAsync` and the extended bridge):

1. For **every** fixture (not just `ales-sample-comp` — vacuous for the nine,
   decisive for ales, and the symmetry is cheap), compute each grain-1 cell
   BOTH ways: the legacy in-process `GsEquivalentRaw` chain, and the D1 bridge
   value. Compare exact-decimal; on any difference throw a **harness-bug**
   exception — `InvalidOperationException` naming the cell (task/round/group/
   pilot) and both values — explicitly NOT a ledgerable `GrainMismatch`.
   Mechanics are the implementer's (extracting the legacy per-slot computation
   into a helper the bridge loop calls is the obvious shape); the contract
   above is not negotiable.
2. Run the suite green under the gate.
3. Then delete the dead machinery, compiler-driven. Expected deletions:
   `CompareRawGrainAsync`, `GsEquivalentRaw`, `EntryPenalties` (its only user
   was the legacy grain 1) and the `PenaltyEngine.ApplyRawPenalties` call;
   expected to STAY: `ScoringService` (FlattenParameterBindings + Aggregate),
   `ParameterResolver`, `MeasurementDigest`, `PenaltyEngine`
   (ApplyAggregatePenalties, conservation), and the store-loading helpers —
   `LoadCompetitionAsync`/`LoadEntriesAsync` are consumed by conservation and
   grain 2 regardless. Let the compiler name anything else.
4. **Zero behavioural change is the pass condition:** every fixture's
   mismatches, cell counts, and ledger expectations identical before and
   after; no `divergences.json` appears for `ales-sample-comp`; the feature
   file's pinned counts (e.g. "the fixture carries no ledgered divergences")
   untouched. Any new mismatch means YOUR change broke the harness — stop and
   fix, never ledger.

### D7 — Nothing outside `tests/` + `kanban/`

No `src/` edit (the view already carries `State` and `PreNormalisationScore`;
enum round-tripping is already exercised by the same deserialisation grain 2
uses). `tests/Soarscore.Architecture.Tests` must stay green unmodified, and
`git diff --stat` at every checkpoint may name only files under `tests/` and
`kanban/` (plus `graphify-out/` at close-out). No `/docs` edit.

## Property-based testing — articulated invariant (CLAUDE.md requirement)

**Named invariant B1 — bridge/legacy equivalence under the shape guards:** for
any slot the comparator accepts (D2's guards), the D1 composition equals the
legacy `GsEquivalentRaw` value exactly.

It does **not** get a CsCheck property, deliberately: the only honest oracle
for it is the engine's own selection+evaluation — a generator producing
class definitions rich enough to exercise it would re-implement the pipeline
the property would check, with no independent oracle. The golden corpus at
three grains is the proof of record, and D6's parity gate is the
example-based check of that invariant across every replayed cell. Precedent:
the prior story's deliberately-skipped re-derivation property (same reason:
no independent oracle). If the mirror is ever widened past
Constant/Lookup, revisit — that widening changes the term algebra and
re-opens the question.

## Known traps (pre-answered)

1. **NoResult rows contribute 0 even when the entry holds flights** — a
   NoResult-with-flights shape exists (validWhen failures; Zero*-zeroed rows).
   Mirror `GsEquivalentRaw` exactly: the state gate, nothing else.
2. **`flightValidWhen`-zeroed flights still contribute** — the engine's step 7
   evaluates `ScoreNormalised` over a selected flight's metrics without
   re-checking per-flight validity (`NormalisationEngine.cs:158-166` reads
   `flight.Metrics`, which `Interpret` preserves on the zeroed path,
   `FlightInterpreter.cs:47-54`). Do not "improve" the bridge by gating on
   anything but the row state. (Unreachable for ales — task D declares no
   `flightValidWhen` — but the bridge is generic.)
3. **ales' unflown rounds R2–R3** are flight-less entries ⇒ NoResult rows ⇒
   contribution 0 ⇒ placeholder oracle zeros. They must still be COMPARED
   (`RecordCell` runs for every row) — the fetched-row-universe-equals-
   `EntryIdBySlot` equality is load-bearing; the prior story proved it and
   nothing here may disturb its inputs.
4. **The mirror's missing-metric throw stays** ("Metric … was never captured",
   `Comparator.cs:494-498`). For ales it is unreachable — `landingDistance` is
   captured for every flown duration slot (`ReplayDriver.cs:634-636`) and
   flight-less slots are gated off by D1. Keep the throw; it is the widening
   gate, not dead code.
5. **`LookupTerm`'s fallback row** (all `UpTo` null-exhausted ⇒ 0, the
   engine's `:138-139` shape) is already mirrored — keep it verbatim; the
   class lookup scoring "zero as zero" (empty landing distance) relies on the
   last unbounded row, not the fallback, but both sides must agree by
   construction, not by data luck.
6. **Don't touch grain 2, grain 3, conservation, or the ledger-strictness
   self-check** — their code and their read paths are exactly what the
   zero-change law is measured against. No opportunistic refactoring of
   unrelated comparator code.
7. **Decimals round-trip exactly** over `System.Text.Json` with the API's
   options — already relied on (`Comparator.cs:54-57`); the bridge adds no new
   serialisation surface. `State` deserialises through the same
   `GroupScoreView` type grain 2 already binds.
8. **Task D's metric truncation** (`precision` on the declared metrics) is
   already applied by the time metrics are decoded — both the engine's
   pipeline and the bridge read through `MeasurementDigest.Resolve` of the
   same captured measurements, so the bridge introduces no decode behaviour.
   Do not add precision handling to the mirror.

## Work items

Strictly sequential. Each WI lands compiling with its checkpoint suites green
— safe to park at every boundary. Code cites work items as
`kanban/in-progress/http-grain-one-metric-bridge.md#WI-n`.

### WI-0 — Board

`git mv kanban/backlog/http-grain-one-metric-bridge.md kanban/in-progress/`;
set `**Status:** In progress · …` in the same commit.

### WI-1 — The bridge, behind the parity gate

Per D1–D5: extend `CompareRawGrainViaHttpAsync` with the D1 composition over
D2-guarded all-flights metrics, terms from the D3 resolved task, metrics built
per D4; keep the loop and bookkeeping identical. Add the D6 parity gate
(legacy + bridge both computed per cell for every fixture, harness-bug
exception on any difference) — `CompareRawGrainAsync`, `GsEquivalentRaw`,
`EntryPenalties` stay untouched and reachable underneath it.

**Checkpoint:** `dotnet build Soarscore.sln`; then
`SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests` —
all ten `@gliderscore` fixture scenarios and the WI-5 self-checks green, with
the gate live and silent (zero harness-bug throws). Postgres leg wherever
Docker exists: `SOARSCORE_TEST_STORE=postgres dotnet test
tests/Soarscore.Acceptance.Tests`.

### WI-2 — Delete the legacy path, close the split

Per D6.3 + D5: remove `CompareRawGrainAsync`, `GsEquivalentRaw`,
`EntryPenalties`, the gate scaffolding, `ScoreNormalisedFree`, the
classification branch and its belt-and-braces guard, and every import the
compiler now calls dead — plus the grain-1 header rewrite (`Comparator.cs`
top block). `dotnet build` must show zero lingering references; grep for
`GsEquivalentRaw|EvaluatePostNormalisationTerm|ScoreNormalisedFree` and expect
only the re-anchored mirror pair.

**Checkpoint:** `dotnet build Soarscore.sln`;
`SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests`
(identical pass/fail profile to WI-1, gate now gone);
`dotnet test tests/Soarscore.Architecture.Tests` (unmodified); `git diff
--stat` names only `tests/` files. Postgres leg where Docker exists.

### WI-3 — Board close-out

`git mv` to `completed/`, status header same commit. Reconcile inventories:

- `tech-debt.md`: nothing expected; add only if implementation surfaced
  something real.
- `deferred-decisions.md`: nothing expected.
- **Conditional stub (house rule 6):** none expected — the classification
  split this story closes was the last known grain-1 divergence mechanism. If
  the mirror's loud refusal fired on any corpus fixture, that fixture's needs
  become a new backlog stub instead (widen the mirror or expose the selection
  over HTTP — design then, with the refusing fixture as evidence).
- Run `graphify update .` (repo convention after code changes).

## Execution plan

WI-0 → WI-1 → WI-2 → WI-3; WI-1 and WI-2 may share one session (the gate is
short-lived by design), but each must land as its own commit so the parity
proof is visible in history.

**Finish line:** `dotnet build Soarscore.sln`, `dotnet test Soarscore.sln`,
then the acceptance suite under both `SOARSCORE_TEST_STORE` values (postgres
leg wherever Docker exists). Known flake: solution-wide Marten migration race
(`tech-debt.md`) — re-run the failing project alone before diagnosing.

**Story invariant for sign-off:** every computed number in every suite is
bit-identical to pre-story behaviour (the ten golden fixtures prove it; the
parity gate proved the bridge equals the legacy mirror cell-for-cell before
the legacy code was deleted); grain 1 has exactly one mechanism for all
fixtures; the term-kind mirror is the only remaining harness-side scoring
arithmetic; `git diff --stat` against the pre-story tree names only `tests/`
and `kanban/` (plus `graphify-out/`); no `/docs` edit; architecture gates
untouched and green.

## Out of scope

- Any `src/` change, including exposing per-flight metrics or the selection
  over HTTP (D7 — if the guards refuse a real fixture, that refusal is the
  evidence a new story designs from, not licence to widen the view here).
- Widening the term-kind mirror beyond Constant/Lookup (D5 — refused kinds
  stay refused until a fixture needs one).
- GS-composition semantics in `src/` (prior story D1 stands: engine-truth
  pre-normalisation exposure, NOT GliderScore composition).
- The engine's own option-2 behaviour (post-normalisation term evaluation,
  double rounding, lower clamp) — exercised end-to-end by grain 2, untouched.
- The NZ fixtures' ALES classes (`SeedData`/`docs/rules/nz/`) — ingestible
  data and read-only rule text, not consumers of the comparator.
