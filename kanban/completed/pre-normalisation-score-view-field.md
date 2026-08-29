# Story — Expose the pre-normalisation score over HTTP

**Status:** Completed · **Raised:** 2026-08-26 (decision recorded in
`kanban/completed/gliderscore-replay-and-compare-harness.md`, Q1) ·
**Fleshed out:** 2026-08-27

## What

An additive `PreNormalisationScore` on the `/task-round-result` response —
per competitor row — carrying the engine's unnormalised score for that row,
so any consumer can read grain-1 numbers over HTTP without reproducing the
scoring pipeline in-process (the Q1 workaround the GliderScore harness still
uses today).

## Why it matters

Q1 (asked and answered 2026-08-26) chose the in-process mechanism with zero
production change, and explicitly declined the view field *for that story*:
"if HTTP exposure is later wanted it becomes a new backlog stub, not a silent
addition here." This is that stub. Until now no production surface exposes
the pre-normalisation value: `NormalisationEngine.Normalise`
(`src/Soarscore.Domain/Scoring/NormalisationEngine.cs:151`) **overwrites**
`TaskResult.RawScore` with the normalised score inside `ScoreGroup`, so the
value survives nowhere.

## Before starting

- Re-read Q1's rationale; nothing has changed since.
- Additive-only: existing views keep their shapes (NFR-2).
- **The engine's arithmetic is not touched.** Nothing in this story may
  change any computed number — normalisation, penalties, drops, ranking all
  stay byte-identical. The field is observation, not computation.
- Do not edit anything under `/docs` (house rules 3–4). No glossary concept
  is added: "pre-normalisation score" is established vocabulary (Q1, the
  arithmetic story, the harness comparator comments).
- Cross-reference check done at flesh-out time (rule 2): no conflict with
  `docs/users.md`, NFR-1…NFR-4, or the rule corpus. `deferred-decisions.md`
  carries no entry forbidding this. Read them if in doubt before deviating.

---

# Plan

## Design decisions — settled here, do not relitigate

### D1 — Exact semantics of the exposed value

> **`PreNormalisationScore` of a result row = exactly the `TaskResult.RawScore`
> that `NormalisationEngine.Normalise` received for that row.**

Consequences, each binding:

- Populated for **every** row of **every** returned group: Valid rows,
  NoResult rows (normally 0), annulled groups, and the zero-winner guard
  branch (`winnerRaw == 0m` → results overwritten to 0 — their pre-value is
  still carried faithfully).
- Pass-through tasks (`task.Normalise is null`) populate it too, equal to
  the final `RawScore` (they are the same number there).
- It is the *engine's* score after penalties, caps and rounding — i.e. what
  `PenaltyEngine.ApplyRawPenalties` produced. Since `ApplyRawPenalties`
  honours zeroing effects only (replay-harness finding, WI-3-as-built), raw
  stage deductions are either expressed inside class `score` terms (already
  included) or not applied at all (parked in
  `kanban/backlog/entry-scoped-deduct-points-penalties-inert.md`). This is
  correct engine-truth semantics; it is NOT GliderScore composition. Do not
  chase GS equivalence in `src/`.
- Under a reflight shape, results are keyed BY ENTRY, so two live entries of
  one competitor produce two rows with independent pre-values. Parity is
  guaranteed because the map is keyed identically to `Results`.

### D2 — Placement: parallel map on `GroupResult`, not a second field on `TaskResult`

Add to `GroupResult` (`src/Soarscore.Domain/Scoring/ScoringResultTypes.cs`,
after `IsAnnulled`):

```csharp
/// <summary>
/// Per-result-key pre-normalisation score: the <see cref="TaskResult.RawScore"/>
/// this row held when it entered Normalise, preserved through the overwrite.
/// Keys are exactly <see cref="Results"/>' keys. Single writer:
/// <see cref="NormalisationEngine.Normalise"/>.
/// </summary>
ImmutableDictionary<string, decimal> PreNormalisationScores;
```

Rejected alternatives (know why they were rejected):

- *Field on `TaskResult`*: `TaskResult` is constructed by `FlightSelector`,
  re-shaped by `PenaltyEngine.ApplyRawPenalties`, then rewritten again by
  `Normalise` — every stage would have to maintain the copy, multiplying
  touch points and inviting drift. One population point (`Normalise`) is
  auditable.
- *A dedicated query/endpoint* (e.g. `/pre-normalisation-score`): duplicates
  the group-universe logic of `/task-round-result` for one number. The
  verbs-not-nouns routing surface already has the right query.
- *Making `FlightInterpreter.EvaluateTerm` public* for consumers: widens the
  production surface Q1 explicitly declined widening.

This keeps to the core-system law: nothing class-specific anywhere — the
field is a generic observation of pipeline flow.

Note the asymmetry between the two layers' names and shapes — deliberate:

| Layer | Type | Member |
|---|---|---|
| Domain | `GroupResult` | `ImmutableDictionary<string,decimal> PreNormalisationScores` (plural map; keys are internal result keys — CompetitorRef strings ordinarily, composite entry keys under reflight) |
| Application view | `CompetitorTaskResultView` | `decimal PreNormalisationScore` (singular; resolved to one row alongside `CompetitorRef`/`Role`) |

### D3 — Population rules inside `NormalisationEngine.Normalise` (both branches)

Rewrite target `src/Soarscore.Domain/Scoring/NormalisationEngine.cs`:

- Pass-through branch (current line ~49–61): build
  `taskResults.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.RawScore)`
  and return it in the new position. WinnerRef stays null there.
- Normalised branch: capture the map **from the incoming `taskResults`
  dictionary** (never from post-modification copies), before or during the
  loop, so every `resultBuilder[competitorRef] = ... with { RawScore = ... }`
  — including both zero-guard `continue` branches and the LowerIsBetter
  zero-backstop — coexists with the unchanged original value in the map.
- Add NO default parameter value for the new `GroupResult` member. Both
  construction sites are in this very method (grep-checked: `new GroupResult(`
  occurs exactly twice, `NormalisationEngine.cs:55` and `:154`). If a future
  site constructs `GroupResult` elsewhere, the compiler forcing an explicit
  map is the point.

### D4 — Fail-loud view mapping

In `src/Soarscore.Application/Queries/Scoring/ScoreTaskRound.cs`
(`MapGroupResult`, lines ~148–169):

- Append `decimal PreNormalisationScore` as the LAST parameter of
  `CompetitorTaskResultView` — no default value.
- Map it strictly: `result.PreNormalisationScores[kv.Key]` (indexer). A
  missing key means the Results/map parity broke in the domain — an
  exception naming nothing is better than a silent 0. Do not use
  `GetValueOrDefault(0m)`; 0 is a real score value and could mask a bug.

### D5 — API surface changes none

- No new route, no DI registration change: the query/handler wiring is
  `src/Soarscore.Api/Composition.cs:114` and
  `src/Soarscore.Api/Queries/Queries.cs:31`; widening the response type is
  picked up automatically.
- Response JSON: ASP.NET Core web defaults are camelCase (confirmed in
  `Composition.cs:42–54` — options are only added to, not replaced), so the
  new member serialises as `preNormalisationScore`, appended after
  `rawScore` per row. Decimals round-trip exactly over System.Text.Json,
  which is what the comparator relies on (`==` compare, no tolerance).
- `tests/Soarscore.Architecture.Tests` (RouteShapeTests, LayerRuleTests,
  HandlerRegistrationTests, ClassAgnosticismTests) need no edits and MUST
  stay green unmodified.

### D6 — Harness grain-1 flips to HTTP where the authored class permits it

Grain 1 currently recomputes in-process per slot
(`tests/Soarscore.Acceptance.Tests/Support/Gliderscore/Comparator.cs:303-353`)
and then adjusts toward GS's composition (`GsEquivalentRaw`, :371-380)
because option-2 fixtures author landing points inside `scoreNormalised`
(replay story D3) — our raw is time-only while GS's persisted `RawScore`
includes landing. That adjustment needs per-flight metrics, which HTTP does
not carry. Therefore:

1. **Classification rule.** A fixture may switch grain 1 to pure HTTP iff
   every task of every phase in its committed
   `tests/GliderscoreFixtures/<slug>/class-definition.json` has an EMPTY
   `scoreNormalised` array — then our pre-normalisation score IS the
   GS-composed raw directly. Inspect all five active fixtures; do not
   assume from names. (`ales-sample-comp` will not qualify — option-2.
   Verify `f3j-international-flyoff`, `f3j-international`, `f3k-sample-comp`,
   `jerilderie-2010` against their files.)
2. Derive the classification at runtime from the loaded definition (empty
   `ScoreNormalised` arrays across all phases' tasks), falling back loudly —
   a `NotSupportedException` naming the fixture — if a non-conforming
   fixture were marked HTTP-path by mistake. Never hard-code per-slug
   booleans.
3. For conforming fixtures: replace the recompute with one
   `GET /task-round-result` call per round — same plumbing as grain 2
   (`CompareNormalisedGrainAsync`, `Comparator.cs:420-452`: `GetAsync`
   helper, `groupByGroupId`, `pilotByCompetitor`, filter
   `Role == ReflightRole.Original`) — reading the NEW
   `preNormalisationScore` member, comparing exactly against the oracle
   `RawScore`, keeping `RecordCell` bookkeeping identical so
   `EnsureOracleCoverage` stays honest.
4. **Transitional parity gate.** While both mechanisms exist, for
   conforming fixtures assert `legacyComputation == fetchedValue` per cell
   by throwing a harness bug exception (not a ledgerable mismatch) on any
   difference. Run the suite green. Then DELETE the dead machinery in the
   same work item: whatever becomes unreachable (expect `GsEquivalentRaw`,
   `EvaluatePostNormalisationTerm`, `EvaluateLookup`, possibly
   `EntryPenalties` and the direct-provider scoring imports) goes away,
   compiler-driven. Retained non-conforming fixtures keep their legacy path
   verbatim behind the classification split.
5. Consequence to expect and accept: **zero behavioural change** to the
   three-grain comparison or the nine-entry jerilderie ledger — the fetched
   row universe equals `outcome.EntryIdBySlot` by construction (D4 opened an
   entry per slot; the WI-6 synthetic R12/G1 slot included; phantom-group
   rows never got entries). If you observe ANY new mismatch after the flip,
   stop: that is a harness bug in your change, not a scoring difference.

## Property-based testing — articulated invariant (CLAUDE.md requirement)

**Named invariant P1 — pre-normalisation preservation:** for any group of
arbitrary task results under any task definition, `Normalise` preserves
every row's received raw score: `∀k: output.PreNormalisationScores[k] ==
input[k].RawScore`, with the map's key set equal to the results' key set —
while `Results[k].RawScore` legitimately changes. This is a genuine
invariant of the normalisation transformation over a real input class
(state × raw × direction × rounding interact through four code branches:
pass-through, valid-scale, zero-winner guard, LowerIsBetter backstop), so it
gets a CsCheck property, not just examples.

One property is enough — see below for what we deliberately chose NOT to
property-test. Example-based tests cover the branch endpoints the generator
space reaches thinly (all-NoResult groups, NoResult rows carrying non-zero
raws, which real selectors cannot produce but the record type permits and
the semantics must survive).

Deliberately skipped (do not add later without new grounds): a property
re-deriving normalised scores from raws would duplicate the engine's own
formula inside the test (no independent oracle); the GliderScore golden
fixtures already assert numeric truth at scale end-to-end. Precedent:
WI-5 of the harness story (CsCheck reserved for genuine invariants).

## Known traps (pre-answered)

1. **NoResult raw ≠ 0 is constructible at the domain level** (records permit
   it even though `FlightSelector` never emits it) — P1 must hold for those
   too: pre-value preserved, final overwritten to 0. Test it explicitly.
2. **Zero-winner guard** (`winnerRaw == 0m`): results overwritten to 0, but
   pre-values are whatever came in. Not special-cased in the map builder.
3. **Annulled groups**: `IsAnnulled` true, WinnerRef maybe null — map still
   populated; nothing about annulment suppresses the field.
4. **Key-set equality** is part of the contract — a `Keys.SetEquals`
   assertion belongs in both the unit tests and property P1.
5. **Do not "improve" `CompetitorTaskResultView.RawScore` semantics** — it
   remains the post-normalisation value (that is what grain 2 compares). The
   two fields coexisting on one row is the whole point.
6. **Dictionary iteration order** never matters; compare via key lookups
   only.
7. **Engine-only concern:** do not touch `PhaseAggregator`,
   `RankingEngine`, `ReflightSelector`, or the seed corpus
   (`tools/Soarscore.SeedData`) — the last is ingestible data, not a
   consumer.
8. Route-shape reflection test reads endpoint signatures, not response
   payloads — expected untouched, but run it anyway (D5 checkpoint).

## Work items

Strictly sequential. Each WI lands compiling with its checkpoint suites
green — safe to park at every boundary. Code cites work items as
`kanban/in-progress/pre-normalisation-score-view-field.md#WI-n`.

### WI-0 — Board

`git mv kanban/backlog/pre-normalisation-score-view-field.md kanban/in-progress/`;
set `**Status:** In progress · …` in the same commit.

### WI-1 — Domain: `GroupResult.PreNormalisationScores`

Per D2 + D3, plus tests:

- `tests/Soarscore.Domain.Tests/NormalisationEngineTests.cs` — example-based,
  AwesomeAssertions style, one test each:
  1. pass-through task (null `Normalise`): map equals inputs for Valid +
     NoResult mix;
  2. HigherIsBetter with rounding: winner normalises to `WinnerScore` while
     its `PreNormalisationScores[winner]` equals its input raw;
  3. NoResult rows inside a scored group: final 0, pre-value preserved (use
     an input raw of e.g. `7m` on a NoResult row);
  4. all-NoResult group (zero-winner guard): finals all 0, map equals inputs
     including the non-zero input case;
  5. LowerIsBetter with a zero-valid entrant reaching the backstop branch:
     backstop'd row's pre-value preserved;
  6. key-set equality on every constructed group above.
- `tests/Soarscore.Domain.Tests/NormalisationEnginePropertyTests.cs` —
  property P1 (D section above): reuse the file's existing generators
  (`Direction`, `WinnerScore`, `RoundingGen`, `Entry.Array`,
  `ValidResult`, `NoResultResult`, `MakeTask`, `MakeUnnormalisedTask`);
  comment cites this file's P1.

**Checkpoint:** `dotnet build Soarscore.sln`;
`dotnet test tests/Soarscore.Domain.Tests --filter FullyQualifiedName~Normalisation`.

### WI-2 — Application view + architecture gates

- Widen `CompetitorTaskResultView` and `MapGroupResult` per D4.
- New `tests/Soarscore.Application.Tests/Queries/Scoring/ScoreTaskRoundHandlerTests.cs`,
  mirroring `Queries/Entries/FindEntriesHandlerTests.cs` conventions
  (fake store/query, AwesomeAssertions), asserting one seeded-scored group
  exposes rows whose `PreNormalisationScore` matches the values chosen in
  WI-1's helpers. If seeding a full competition proves heavier than the
  neighbouring handler tests allow, extend rather than fake the pipeline:
  never stub `ScoringService`.

**Checkpoint:** `dotnet test tests/Soarscore.Application.Tests`;
`dotnet test tests/Soarscore.Architecture.Tests` (unmodified).

### WI-3 — Harness grain-1 flip (acceptance suite)

Per D6: classify all five fixtures from their committed definitions, flip
conforming ones behind the transitional parity gate, delete dead code, expect
zero ledger or diff-table changes.

**Checkpoint:** `SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests`
— all features green including all five `@gliderscore` scenarios and the
WI-5 self-checks. Postgres leg wherever Docker exists:
`SOARSCORE_TEST_STORE=postgres dotnet test tests/Soarscore.Acceptance.Tests`.

### WI-4 — Board close-out

`git mv` to `completed/`, status header same commit. Reconcile inventories:

- `tech-debt.md`: nothing expected; add only if implementation surfaced
  something real.
- `deferred-decisions.md`: nothing expected.
- **Conditional stub (house rule 6):** if any active fixture retained the
  legacy in-process grain-1 path (expected: yes, at least
  `ales-sample-comp`), create `kanban/backlog/http-grain-one-metric-bridge.md`
  — a short stub proposing the comparator evaluate `scoreNormalised` terms
  against decoded slot metrics so option-2 fixtures also lose their in-process
  dependency. Write What / Why / Before-starting only.
- Run `graphify update .` (repo convention after code changes).

## Execution plan

WI-0 → WI-1 → WI-2 → WI-3 → WI-4, one session each is comfortable; WI-3 is
the only nontrivial one.

**Finish line:** `dotnet build Soarscore.sln`, `dotnet test Soarscore.sln`,
then the acceptance suite under both `SOARSCORE_TEST_STORE` values. Known
flake: solution-wide Marten migration race (`tech-debt.md` last item) — re-run
the failing project alone before diagnosing.

**Story invariant for sign-off:** every computed number in every suite is
bit-identical to pre-story behaviour (goldens prove it); the field appears on
every row of `/task-round-result` for every state enumerated in D1; no `src/`
file mentions GliderScore; no `/docs` edit; architecture gates untouched and
green.

## Out of scope

- Changing any engine arithmetic or making `EvaluateTerm` public (D2).
- GS-composed exposure in `src/` (D1) — the comparator keeps its own
  mirrors where classification demands it (D6).
- Consumers beyond the acceptance suite: nothing else calls this today; the
  field ships because HTTP exposure was wanted, not because a caller waits.

---

# As-built (2026-08-29)

All five WIs landed. Commits: WI-0 `3f29b0a`, WI-1 `af2aa7b`, WI-2 `d54a3f4`,
WI-3 `3a7f03a` + `136467a`. Zero behavioural change anywhere: every checkpoint
suite green with identical pass/fail profiles; the WI-3 parity gate compared
every cell of all nine flipped fixtures and never fired.

Deviations from the plan, as built:

- **D2 placement (WI-1):** `IsAnnulled` carries a default value, and C#
  forbids a required parameter after an optional one (CS1737), so
  `PreNormalisationScores` sits BEFORE `IsAnnulled` in the positional record
  declaration — the binding law (no default value) is kept, and both
  construction sites are named-argument, so the compiler still forces the map
  at every future site.
- **Active fixtures (WI-3):** the story's "five" was stale — ten fixtures are
  active in `index.md` and the feature file. Runtime classification
  (`ScoreNormalisedFree` over `fixture.Definition`, never per-slug booleans)
  flipped NINE to the HTTP path; `ales-sample-comp` alone (its task D carries
  a `scoreNormalised` term) retains the legacy in-process grain-1 path
  verbatim, so `GsEquivalentRaw`/`EvaluatePostNormalisationTerm`/
  `EvaluateLookup`/`EntryPenalties` stayed reachable — nothing else went dead.
- **D6.3's `Role == ReflightRole.Original` filter was stale vs. the code:**
  grain 2 compares ALL rows (trap 10). The HTTP grain 1 therefore compares all
  rows too — no filter — and `EnsureOracleCoverage` stayed honest; the
  binding statement was D6.5's zero-change law, which the parity gate proved.
- **P2 property (WI-1):** added on explicit user request as new grounds —
  pass-through transparency (`Normalise is null` ⇒ map values equal final
  `RawScore`s, D1's identity consequence). NOT the forbidden
  re-derivation-of-normalised-scores property, which stays skipped.
- **Postgres acceptance leg skipped:** Docker unavailable in the
  implementing environment; sqlite leg green (64/64) at both WI-3 checkpoints.
