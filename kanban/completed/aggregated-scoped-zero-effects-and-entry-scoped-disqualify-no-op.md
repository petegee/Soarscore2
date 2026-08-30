# Story — Aggregate-scoped Zero* records zero nothing; entry-scoped Disqualify does nothing

**Status:** Completed 2026-08-30 — (a) and (b) landed (WI-1..WI-5); one WI-2
wiring defect found by WI-3's P-FlagOrAccumulation and fixed in-story. (c)
remains glossary-gated below. · **Raised:** 2026-08-27 — mirrors of the no-ops left open
by `kanban/completed/entry-scoped-deduct-points-penalties-inert.md` (WI-0),
which wired the first direction (entry-scoped `DeductPoints` → raw stage) but
deliberately narrowed rather than solved its siblings. ·
**Scoped:** 2026-08-30 — plan below is implementation-ready; decisions D-A1..D-B4
settled in-story; (c) remains glossary-gated.

## What

Two gaps in `PenaltyEngine` routing, plus one hardening idea:

1. **(a) TaskRound/Competition-scoped record of a Zero*-carrying definition
   zeroes nothing.** `ScoringService.GetAggregatePenalties`
   (`src/Soarscore.Domain/Scoring/ScoringService.cs:583-590`) feeds
   aggregate-scoped records only into `PenaltyEngine.ApplyAggregatePenalties`
   (`src/Soarscore.Domain/Scoring/PenaltyEngine.cs:131-167`), which honours
   `DeductPoints`/`Disqualify` and ignores `ZeroFlight`/`ZeroRound`/`ZeroTask`.
   Live example: F3B's own `nonConformingWinch` is ZeroFlight + DeductPoints
   1000 (`tools/Soarscore.SeedData/SeedF3B.cs:164-172`); recorded at TaskRound
   or Competition scope, its zeroing half silently does nothing.
2. **(b) `Disqualify` on an entry-scoped record sets no flag.** Entry-penalty
   records reach only `ApplyRawPenalties` (`PenaltyEngine.cs:58-119`), whose
   Residual R1 acknowledges-not-actions a Disqualify effect (`PenaltyEngine.cs:46-49,103-104`).
   `FinalCompetitorScore.Disqualified` is set only from
   `ApplyAggregatePenalties` (`ScoringService.cs:466-472`), so an entry-scoped
   record of a Disqualify-carrying definition (e.g. F3F's
   `deliberatelyBlockingAnotherModel...`-shaped definition,
   `tools/Soarscore.SeedData/SeedF3F.cs:130`) leaves the competitor ranked
   normally.
3. **(c) Hardening idea:** let a `PenaltyDefinition` optionally declare its
   permitted recording scopes, so mis-scopeable definitions are caught at
   adoption instead of at scoring time. **New field on the class model —
   glossary-gated:** argued in WI-6, user approval required before any `/docs`
   edit (housekeeping rule 4). Everything else in this story is implementable
   without it.

## Why it matters

Both (a) and (b) are CD-visible no-ops on an immutable audit trail — the same
trust-model objection that motivated the parent story. The decision there
(scoping argument, "option 2 rejected") covers why *routing*, not adoption-time
refusal, is the preferred family of fix; re-read it before contradicting.

## Before starting — resolved at scoping (2026-08-30)

- **Parent D1 re-read.** These gaps are its deliberate residuals. D1 says:
  *Flight/Entry scope ⇒ the task-round stage owns all effects;
  TaskRound/Competition scope ⇒ the aggregate stage, unchanged in every way.*
  The fixes below amend D1 the same way the parent's approved class-diagram
  amendment already licenses: the stage is a property of the *effect within
  the stages where the recorded scope makes it visible* — and a Zero* effect
  is not visible at the aggregate stage (there is no flight or round result
  there to zero), so it routes to the one stage that can act on it.
- **Are the gaps actually reachable today?** Yes, through the write side
  unchanged. Neither `Competition.RecordPenalty`
  (`src/Soarscore.Domain/Competitions/Competition.cs:1684-1696`) nor
  `Entry.RecordPenalty` (`src/Soarscore.Domain/Entries/Entry.cs:514-552`)
  constrains *which* infraction types may be recorded at *which* scope — only
  that the scope matches the aggregate (TaskRound/Competition vs Flight/Entry)
  and the type is declared. So a Zero*-carrying definition is recordable at
  Competition scope today, and a Disqualify-carrying one at Entry scope today.
  No seed or fixture currently *does* either, but the stub does not stay parked
  in `deferred-decisions.md` territory: the no-op is one recorded infraction
  away, and the fix is read-path-only.
- **`(c)` is sequenced last and gated.** See WI-6.

## Cross-references checked (housekeeping rule 2)

- NFR-1/NFR-2 (class model owns variance; additive-only extension): both fixes
  are generic engine behaviour keyed off `PenaltyEffect` values already in the
  class model — no class branch, compliant.
- NFR-4 (no imposed ordering): penalties stay recordable at any time; all
  changes are at scoring (read) time except one new *record-time validation*
  (D-A3) which constrains payload completeness, not when the record may be
  made.
- Rule docs: the zeroing rules name their round — F3K.1.2 "the score **for the
  round** will be 0", F3K.4.1 "a zero score for the whole round", F3B.2.2 p
  "the flight preceding the test is zeroed" (plus the F3B.2.2 p deduction from
  the final score, which is the *other* half of the same definition — see
  D-A4). No rule zeroing clause is scope-free. (fai-rules skill, class docs
  first.)
- `docs/soaring-domain-glossary.md` / `docs/soaring-domain-class-diagram.md`:
  no new concepts. The class-diagram PenaltyEffectSpec note was already amended
  by the parent story (approved 2026-08-27) — this story needs **no** `/docs`
  edit except under gated WI-6.
- **`deferred-decisions.md` conflict found — to reconcile in WI-0:** the entry
  *"The task-round coordinate on an aggregate penalty is recorded but read by
  nothing"* (2026-08-21) becomes false once D-A2 routes Zero* effects by that
  coordinate. WI-0 updates that bullet to record that the coordinate now has a
  scoring reader, and that the *score-sheet report* half (its actual deferral)
  still stands.
- `kanban/tech-debt.md`: nothing owed by this story; reconcile at WI-5.

## Design decisions (settled here, cited from code)

### D-A1 — Aggregate-scoped Zero* acts at the task-round stage, through the existing raw-stage engine path

A TaskRound/Competition-scoped record whose matching definition carries a
Zero* effect has that effect applied at the task-round stage **via
`PenaltyEngine.ApplyRawPenalties` itself** — merged into `ScoreGroup` step 2c
(`ScoringService.cs:109-112`) alongside the entry's own penalties — rather
than by inventing a third apply function. Consequences, deliberate:

- Identical semantics to the pinned raw-stage behaviour: Zero* → `NoResult`,
  `Selection = null`, `RawScore = 0` (`PenaltyEngineTests.cs:19-39`), so the
  zeroed competitor is excluded from normalisation's winner-finding — the same
  guarantee an entry-scoped Zero* already gets.
- ZeroFlight / ZeroRound / ZeroTask are **not differentiated** at this stage —
  they already are not at the raw stage (one shared early-out, existing pinned
  contract). Zeroing the task-round result is the model's answer to all three
  for any task with the granularity the pipeline carries. If a rulebook or
  fixture ever demands flight-granular zeroing inside a multi-flight
  TaskResult, that is a new story — surface it, don't grow it here.
- Reflight interaction, stated not solved: routing is by the *hosting*
  task-round named in `Penalty.TaskRound`. A zeroed make-up entry collapses
  per the class's `ReflightRule` like any other zeroed candidate (a
  BetterOf pair keeps the surviving candidate; a Replacement pair scores 0).
  No special case is added.

### D-A2 — Anchoring: the Zero* record must name the task-round it zeroes

`Penalty.TaskRound` (`src/Soarscore.Domain/Shared.cs:66`) is the coordinate
the record already carries, and `Competition.RecordPenalty` already validates
it for existence (`Competition.cs:1713-1742`). Routing:

- In the `ScoreCompetition` walk (`ScoringService.cs:162-378`), for each
  task-round, collect `competition.Penalties` where:
  - scope is TaskRound or Competition, **and**
  - the definition matching the infraction type carries ≥ 1 Zero* effect, **and**
  - `p.TaskRound` equals this task-round's coordinate
    (PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal), **and**
  - the subject filter matches — grouped per `p.CompetitorRef`.
  Pass this per-competitor map into `ScoreGroup` (new optional parameter,
  default empty) so step 2c can merge each competitor's aggregate-scoped
  Zero* records into the `RecordedPenalty` list it already builds from
  `GetEntryPenalties` (count each record as one occurrence, same as entry
  penalties — finding 4 of the parent thread).
- `GetAggregatePenalties` at the final stage is **unchanged** in what it
  feeds, and `ApplyAggregatePenalties` is **unchanged**: it ignores Zero*
  effects, so there is no double-count by construction. `DeductPoints` and
  `Disqualify` halves of the same records keep acting at the aggregate stage.
- The `ScoreTaskRound` query (`src/Soarscore.Application/Queries/Scoring/ScoreTaskRound.cs:146-147`)
  has `competition` in scope and passes the same per-competitor map, so the
  provisional leaderboard shows the zeroing too.

### D-A3 — A Zero* record with no `TaskRound` coordinate cannot be anchored: refused at record time, refused loudly at score time

Every zeroing clause in the corpus names its round (see cross-references), so
a coordinate-less aggregate-scope record of a Zero*-carrying definition is
incomplete data, not a differently-scoped one. Green-field (no events to
preserve, CLAUDE.md project status), so:

- **Write side:** `Competition.RecordPenalty` gains a validation after
  `ValidateInfractionType`: if the matched definition carries any Zero* effect
  and `penalty.TaskRound is null` → defect
  `recordPenalty.zeroEffectRequiresTaskRound`. This *completes* the record, it
  does not refuse the scope — consistent with the parent story's option-1
  ruling (that rejection was about scope+effect *combinations* inventing
  scope policy; this is about an effect that cannot act without its
  coordinate).
- **Read side safety net** (events already in a log): if the walk meets such a
  record anyway, `ScoreCompetition` returns
  `Result<CompetitionResult>.Failure("score.zeroEffectUnanchored", …)` — loud,
  in the D7 house style ("refuse loudly rather than let it vanish"), never
  silence. Skipping silently would re-create the exact bug this story closes.

### D-A4 — Mixed-effect definitions act in both stages; that is the rule, not a double-count

F3B `nonConformingWinch` is ZeroFlight + DeductPoints 1000 — F3B.2.2 p zeroes
the flight **and** deducts 1000 from the final score (`SeedF3B.cs:162-163`).
Under D-A1/D-A2 the record's Zero* half zeroes the named task-round result and
its DeductPoints half still deducts flat at the aggregate stage. Both halves
of one recorded infraction apply — the definition says both, the recorded
scope makes both visible, no effect acts twice. Assert this exact shape in a
unit test (WI-3) so a future refactor cannot silently drop either half.

### D-B1 — `ApplyRawPenalties` surfaces a Disqualify flag; the raw stage's return type grows

New record in `ScoringResultTypes.cs`:
`public sealed record RawPenaltyApplication(TaskResult Result, bool Disqualified);`
and `ApplyRawPenalties` returns it. `ScoreGroup` step 2c unpacks it. The
engine's Disqualify accrual already exists (`AccruedInfo.HasDisqualify`);
R1's acknowledge-not-action becomes: set the flag, keep the rest of the
behaviour byte-identical.

### D-B2 — The flag is flag-only: no score change, OR-accumulated through the walk

Symmetry with the aggregate stage, where Disqualify only sets
`FinalCompetitorScore.Disqualified` and changes no arithmetic (RankingEngine
excludes flagged competitors from placings):

- `TaskResult` gains `bool Disqualified = false` (default keeps every
  construction site compiling). A pure-Disqualify entry-scoped record leaves
  state Valid and RawScore untouched; a DeductPoints co-effect still deducts;
  a Zero* co-effect still early-outs — and D-B3 keeps the flag through that
  early-out.
- `NormalisationEngine.Normalise` preserves the flag on every TaskResult it
  rebuilds (`with`-expressions carry it; verify, don't assume — WI-2 step).
- `ScoreCompetition` carries it out of the group loop: the candidate tuple
  gains a bool, `ReflightSelector.Select` is untouched (the flag travels
  outside the selector), collapse ORs across a competitor's candidates, and a
  `rawDisqualified` set accumulates per competitor across the whole walk.
- Final assembly (`ScoringService.cs:466-472`):
  `Disqualified: penaltyResult.Disqualified || rawDisqualified.Contains(competitorRef)`.
- Zeroing a disqualified competitor is *not* added: aggregate-stage
  Disqualify does not zero, so entry-scoped does not either. If a rulebook
  ever demands "disqualified ⇒ zeroed AND flagged", that is a class-model
  question, not engine policy.

### D-B3 — Flag survives the Zero*-dominance early-out

The early-out (`PenaltyEngine.cs:83-101`) returns before the deduction loop.
Reorder so the Disqualify accrual is known first: compute
`anyDisqualify = contributions.Values.Any(i => i.HasDisqualify)` before the
Zero* scan, and have the early-out return
`new RawPenaltyApplication(zeroedResult, anyDisqualify)`. A
ZeroFlight + Disqualify definition then yields NoResult **and** the flag —
both declared effects acted.

### D-B4 — Exclusion groups cannot suppress the flag, and never could

Adoption check 16 admits only all-DeductPoints definitions into exclusion
groups, so a Disqualify-carrying definition is never suppressed out of
flagging (same argument as D3 of the parent story for Zero*). Pin with one
unit test; no engine change needed.

## Work items

Each WI lands compiling with its checkpoint green. WIs are sequential; WI-1
and WI-2 touch disjoint surfaces but share `ScoringService`/`ScoringResultTypes`,
so do them in order. Code cites work items as
`kanban/backlog/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-n`.

### WI-0 — Board, deferral ledger, gates

1. `git mv` this story to `in-progress/`, status header updated in the same
   commit.
2. Update the `deferred-decisions.md` bullet "The task-round coordinate on an
   aggregate penalty is recorded but read by nothing": the coordinate now has
   a scoring reader (this story, D-A2); the deferred *score-sheet report*
   half stands unchanged. Do this in the same commit as the first code that
   reads the coordinate (WI-1), not before.
3. No `/docs` edits — the class-diagram note was amended and approved in the
   parent story; nothing here touches `docs/`. (WI-6's glossary work happens
   only if granted.)

Checkpoint: story in `in-progress/`; nothing else.

### WI-1 — (a) Route aggregate-scoped Zero* into the group walk

`src/Soarscore.Domain/Scoring/`:

1. `ScoringResultTypes.cs`: nothing new needed for (a) — routing reuses
   `RecordedPenalty`.
2. `ScoringService.cs`:
   - `ScoreGroup` gains an optional parameter
     `ImmutableDictionary<string, ImmutableArray<RecordedPenalty>> taskRoundPenalties = null`
     (null/empty ⇒ exactly today's behaviour). In step 2c, merge
     `taskRoundPenalties.GetValueOrDefault(competitorRef)` into the list fed
     to `ApplyRawPenalties` (empty-safe both ways).
   - New private helper `GetTaskRoundZeroPenalties(ImmutableArray<Penalty> competitionPenalties, ClassDefinition classDef, TaskRoundCoordinate coordinate)`
     returning the per-competitor map per D-A2 (match Zero*-carrying
     definitions via the classDef's `Penalties`; subject key = `p.CompetitorRef.ToString()`;
     one record = one occurrence). Raise
     `score.zeroEffectUnanchored` if a Zero*-scoped record has a null
     coordinate (D-A3 read side).
   - The `ScoreCompetition` walk builds the coordinate
     `(phase.Ordinal, round.Ordinal, taskRound.Ordinal)` and passes the map
     into `ScoreGroup`.
   - `GetAggregatePenalties` (final stage) unchanged.
3. `src/Soarscore.Domain/Competitions/Competition.cs`: record-time validation
   `recordPenalty.zeroEffectRequiresTaskRound` per D-A3 (write side), after
   `ValidateInfractionType` so the definition lookup is shared — extract the
   lookup into a small private helper rather than duplicating the
   `Any(d => d.InfractionType == …)` scan.
4. `src/Soarscore.Application/Queries/Scoring/ScoreTaskRound.cs`: pass the
   same map (the handler has `competition`; build the coordinate from the
   query's `PhaseOrdinal`/`RoundOrdinal`/`TaskRoundOrdinal`).

Checkpoint: `dotnet build Soarscore.sln`; `dotnet test
tests/Soarscore.Domain.Tests tests/Soarscore.Application.Tests
tests/Soarscore.Architecture.Tests` — all green **unmodified** (behaviour only
changes when aggregate-scoped Zero* records exist, and none exist in any
fixture — the parent story's D5 guarantee, restated).

### WI-2 — (b) Thread the raw-stage Disqualify flag to the final result

1. `ScoringResultTypes.cs`: add `RawPenaltyApplication` (D-B1); add
   `bool Disqualified = false` to `TaskResult` (D-B2).
2. `PenaltyEngine.cs`: change `ApplyRawPenalties` return type; D-B3
   reordering; rewrite Residual R1's comment to point at the story (the
   residual is closed, the comment must not outlive it); update the file
   header block and XML docs (D1 wording: every declared effect now acts —
   Zero* → NoResult, DeductPoints → subtract, Disqualify → flag carried out).
3. `ScoringService.cs`: `ScoreGroup` unpacks the return; candidate tuple and
   collapse gain the flag (OR across a competitor's candidates); accumulate
   `rawDisqualified` per competitor across the walk; final assembly ORs it
   into `FinalCompetitorScore.Disqualified`.
4. `NormalisationEngine.cs`: verify the flag survives `Normalise`'s TaskResult
   rebuilds; fix if not (expected: `with`-expressions carry it, zero changes).
5. `ScoreTaskRound.cs` view mapping: decide and do nothing —
   `CompetitorTaskResultView` gains no field this story (the disqualified
   competitor's *row* is unchanged; the flag surfaces in final results only).
   Note the decision in the WI commit message.

Checkpoint: as WI-1, all green unmodified (fixtures record no entry-scoped
Disqualify).

### WI-3 — Unit and property tests

Unit (`tests/Soarscore.Domain.Tests/PenaltyEngineTests.cs` + a sociable
`ScoringServiceTests.cs` if the walk assertions need more than the engine):
black-box style, generators/patterns as existing:

- entry-scoped pure-Disqualify → `Disqualified == true`, state Valid,
  RawScore unchanged (D-B2).
- entry-scoped DeductPoints + Disqualify → raw reduced **and** flag (D1 of
  parent + D-B1).
- entry-scoped Zero* + Disqualify → NoResult **and** flag (D-B3).
- aggregate-routed Zero* (sociable, through `ScoreGroup` with the new
  parameter): subject's TaskResult → NoResult in the named task-round; other
  competitors untouched (D-A1).
- F3B `nonConformingWinch` shape end-to-end: NoResult at the task-round stage
  **and** 1000 deducted at the aggregate stage (D-A4).
- coordinate-less aggregate Zero* → `score.zeroEffectUnanchored` failure (D-A3).
- Disqualify-carrying definition in an exclusion group is impossible per check
  16 — the flag test uses a non-grouped definition; a comment cites check 16
  (D-B4).
- regression guard: every existing engine test green unmodified.

Property (`CsCheck`, extend `ScoringServicePropertyTests.cs`), invariants
named at planning:

- **P-FlagOrAccumulation:** the final `Disqualified` equals the OR over every
  recorded entry-/aggregate-scoped Disqualify effect for that competitor —
  invariant under permutation of the recorded penalties (order-independence,
  mirroring P-RawOrderIndependence of the parent story).
- **P-ZeroRoutingEquivalence:** a Zero* infraction recorded at TaskRound scope
  naming coordinate (p, r, t) produces, for the subject, the same observable
  task-round outcome (`NoResult`, winner-finding exclusion) as the identical
  infraction recorded entry-scoped on an entry in that task-round — the two
  scopes are routes to one engine path (D-A1).

Checkpoint: `dotnet test tests/Soarscore.Domain.Tests`.

### WI-4 — Acceptance, both stores

`tests/Soarscore.Acceptance.Tests/Features/ScoringACompetition.feature` +
`Steps/ScoringACompetitionSteps.cs` (beside the existing penalty scenarios):

```gherkin
Scenario: A task-round-scoped zero penalty zeroes the named round's flight score
  Given a published class declaring a Zero-carrying penalty like F3B's nonConformingWinch
  And a competition adopting it, drawn, with competitor 1 having flown round 1
  When the CD records that infraction against competitor 1 scoped to round 1's task-round
  Then competitor 1's round 1 result is NoResult and they are not the group winner
  And competitor 1's final total is still reduced by the definition's point deduction

Scenario: An entry-scoped disqualification removes the competitor from the placings
  Given a published class declaring a Disqualify-carrying penalty
  And a competition adopting it, drawn, with competitor 1 having flown
  When the CD records that infraction against competitor 1's entry
  Then competitor 1's score is unchanged but they hold no placing
```

Run both legs: `SOARSCORE_TEST_STORE=sqlite dotnet test
tests/Soarscore.Acceptance.Tests` and again unset (postgres default) wherever
Docker exists — per CLAUDE.md, a backend is supported only with this suite
green unchanged.

### WI-5 — Closeout

Move to `completed/` (`git mv` + status header), reconcile `tech-debt.md`
(expect: none), confirm the `deferred-decisions.md` coordinate bullet reads
correctly post-landing, delete Residual-R1 references wherever the grep finds
them stale, and `graphify update .`.

### WI-6 — (c) Permitted-scope hardening — GATED on user approval, do not start without it

**Resolution 2026-08-30:** the gate argument was presented and APPROVED — as
its own story, not an addendum here: `kanban/backlog/permitted-scopes-on-penalty-definitions.md`
(the /docs approval is recorded there). Nothing below was implemented in this
story.

**Gate (housekeeping rule 4):** adding anything to `/docs` — here the glossary
and the class diagram's `PenaltyDefinition` — requires explicit user approval
first. Present the argument below; if declined, delete this WI from the story
at closeout and record the decline in `deferred-decisions.md`.

Argument to present: a `PenaltyDefinition` today may be recorded at any scope
the write side allows, and the parent story proved scope+effect *combinations*
cannot be judged at adoption because the definition carries no scope
knowledge. An optional `PenaltyScope[]? PermittedScopes` field (null ⇒ any
scope, so every existing class definition is untouched) lets a class author
say "this infraction is a flight-level fact" and get `recordPenalty.scopeNotAllowed`
at recording time instead of discovering at score time that the record landed
where its effects cannot act. It is the adoption-time mirror of D-A3's
record-time completeness check, and it is data-driven (NFR-1) — the core
system reads the list generically. Cost: one optional field, one decide-function
check per aggregate, one glossary/class-diagram note.

If approved: implement after WI-5 in the same story (one field, two decide
functions, adoption check, unit tests) — or, if the approver prefers, as its
own story; ask which.

## Out of scope

- Flight-granular zeroing inside a multi-flight `TaskResult` (D-A1: surface as
  a new story if a rulebook demands it).
- Zeroing or otherwise arithmetically penalising a disqualified competitor
  (D-B2).
- The score-sheet report that would *display* `Penalty.TaskRound`
  (`deferred-decisions.md`, 2026-08-21 — unchanged).
- Any change to event shapes, store aliases, or the API surface beyond the one
  new record-time defect code; `RecordEntryPenaltyHandler` needs zero edits —
  read-path engine fixes plus one `Competition.RecordPenalty` validation.
- Re-authoring any seed class or fixture to record Zero*/Disqualify at the
  previously-inert scopes (the acceptance scenarios create their own records).
