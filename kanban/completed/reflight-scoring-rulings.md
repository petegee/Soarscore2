# Reflight-scoring rulings

**Status:** Completed 2026-08-24 · **Raised:** 2026-08-24 · **Planned:** 2026-08-24

## What

A competition-time command — **record a CD ruling** of which score counts for a
re-flight — for the three places the rulebook is silent (`ReflightSelection.UndefinedRequiresRuling`:
F3B Task C, F5L, NZ Class M). `kanban/completed/reflight-groups.md` (decision 4)
left scoring failing honestly with `score.reflightRequiresRuling` rather than
assume; the group physically flew, and a ruling is the ordinary, witnessed
resolution. This thread gives the decision somewhere to live:

1. a **`ReflightRuling` value object** (coordinate + competitor + Replacement/BetterOf
   + reason/by/at) and a **`ReflightRulingRecorded`** event folded onto
   `Competition.Rulings` — the `Penalty`/`ParameterBound` shape;
2. a **decide function** `Competition.RecordReflightRuling` validating the ruling
   against the adopted class's resolved `ReflightRule` (data, never a branch on
   class — CLAUDE.md's core law);
3. **scoring consumption** — `ReflightSelector.Select` accepts the ruled selection
   and applies it *only where the class rule is silent*, so the leaderboard
   computes instead of refusing;
4. command/handler/route/registration end-to-end, store-backed and BDD-tested.

## Why it matters

`UndefinedRequiresRuling` keeps the class model honest, but an honest refusal
with no way forward strands a real contest: the group exists, scores are
captured, and `score.reflightRequiresRuling` blocks the leaderboard indefinitely.
A recorded ruling turns "the rules are silent" from a dead end into a decision
the log keeps — auditable like everything else, with who ruled, why, and when.

## Before starting — done

- **Decision cited**: `kanban/completed/reflight-groups.md` decision 4 and
  `:504` (the backlog stub this story fleshed out). Code string corrected here:
  the failure code is **`score.reflightRequiresRuling`** (lowercase f), raised at
  `src/Soarscore.Domain/Scoring/ReflightSelector.cs:90-93`, surfaced through the
  collapse loop at `src/Soarscore.Domain/Scoring/ScoringService.cs:262-269`.
- **Rules cross-check** (citations carried verbatim from the reflight thread's
  2026-08-21 skill pass, which verified each silence): F3B.1.5 e names Tasks A/B
  only, so Task C's re-flight has no stated resolution (`docs/rules/f3b.md`);
  F5L 5.5.12.9 grants the re-flight and stops (`docs/rules/f5l.md:116` notes the
  gap is "a CD ruling, not an F5L rule"); NZ.3.12.5 l likewise grants and never
  says which score counts (`docs/rules/nz/class-m-ales200.md:114`). Nothing in
  either rulebook constrains *how* the CD resolves a silence, so a recorded
  Replacement/BetterOf choice contravenes nothing; where a rulebook *does*
  speak (every other class), this mechanism refuses to accept a competing
  "ruling" at all (decision 3 below).
- **NFR-4 checked**: a ruling is a recorded fact, never a gate. The decide
  function validates **no entry data** — a ruling may precede capture (CD rules
  at the incident) or follow it (scoring is computed afresh per query, so the
  very next leaderboard reflects it). Capture around an unresolved re-flight is
  and stays unrestricted.

## Decisions settled before planning (user, 2026-08-24)

1. **Home: Competition-level, not Entry-side.** A `ReflightRuling` value object +
   `ReflightRulingRecorded` event folded into `Competition.Rulings`. The ruling
   is kin to `ParameterBinding` — it fills what the class left open, chosen at
   competition time — not to `Entry.Annulment`, which voids one attempt. Bonus:
   `CompetitionView` wraps the aggregate, so `GET /competition` surfaces rulings
   with zero projection work, and `ScoreCompetition` reads them straight off the
   state it already receives.
2. **Supersede: last ruling wins.** Re-recording for the same (task-round,
   competitor) is never refused; the most recently *logged* ruling is the
   effective one. `BindParameter` accumulates bindings the same way
   (`Competition.cs:437` folds `Add`, no duplicate refusal). A changed mind or a
   protest outcome is itself an auditable event with its own `Reason`; refusing
   would strand a mistake with no correction mechanism.
3. **`classRuleSpeaks` refusal.** If *both* resolved slots
   (`EntitledScores`, `OthersScore`) are concrete — Replacement/BetterOf/
   NotPermitted, e.g. F3K, F5J — recording a ruling fails
   `recordReflightRuling.classRuleSpeaks`: the rulebook governs, and a silently
   ignored ruling would let a CD believe they settled something that had no
   effect. Classes with at least one silent slot stay acceptable (see planner's
   call 4 for the mixed-case caveat).
4. **Docs amendments approved.** Glossary sentence, class-diagram node,
   aggregate-roots mutation list — WI-8, in this thread's completing commit.

### Planner's calls — flag for veto when this plan is reviewed

1. **Granularity is per competitor + task-round, not per group.** A re-flown
   group needs one ruling per affected competitor — which is what the FAI
   default pattern wants anyway (entitled → Replacement, fillers → BetterOf are
   *different* resolutions), and at ≤ 20 pilots bulk-recording is not a burden.
   A one-shot group ruling is a convenience command for later if a CD asks.
2. **Withdrawn competitors may receive rulings.** Registration *is* checked
   (typo protection — a ruling keyed to nobody would silently never apply);
   withdrawal is not. `AppendReflightGroup` refuses withdrawn members because it
   forms a group; a ruling is not group formation, and a moot ruling is inert,
   not harmful.
3. **No entry-existence requirement.** Deliberate, NFR-4 (Before-starting
   above): dangling-by-coordinate is impossible (the task-round must exist),
   and a ruling whose pair of entries never materialises simply never matches a
   candidate pair.
4. **Mixed-class inert rulings accepted.** Where exactly one slot is silent
   (F3F: `OthersScore` undefined, `EntitledScores` Replacement —
   `SeedF3F.cs:88`), a ruling is accepted but may turn out inert for a
   competitor whose role's slot speaks: `Competition` holds no entry data, so
   the decide cannot know the competitor's role. Documented in the doc comment;
   scoring ignores such a ruling by the RR1 law below.
5. **The selector grows an optional third parameter**
   (`ReflightSelection? ruledSelection = null`) rather than callers pre-resolving
   the effective selection — the role→slot law stays in exactly one place.
6. **Command/handler return convention mirrors `RecordPenalty`'s command**
   (verify its convention in-tree during WI-5 and copy it).

## Findings from reading the tree

Written 2026-08-24 against the tree as it stands after
`kanban/completed/reflight-groups.md`. File references cite `file:line`;
re-verify before acting on one.

1. **The failure to replace is narrow and precise.** `ReflightSelector.Select`
   (`src/Soarscore.Domain/Scoring/ReflightSelector.cs:58-97`) resolves
   `selection = isEntitled ? rule.EntitledScores : rule.OthersScore` at `:81`,
   then switches; `UndefinedRequiresRuling` → `score.reflightRequiresRuling` at
   `:90-93`. Its only production caller is the collapse loop
   (`ScoringService.cs:264`), which propagates the failure verbatim. Plumbing a
   ruling in touches these two functions and nothing else — `ScoreTaskRound`'s
   per-group view deliberately shows both rows and never collapses
   (`CompetitorTaskResultView.Role`, from the reflight thread's WI-7).
2. **`RecordPenalty` is the decide precedent**: a self-contained value object,
   chained `Validate*` helpers returning `Defect?`, one code per command
   (`Competition.cs:1338-1405`). Two of its helpers need generalising:
   `ValidateTaskRoundCoordinate` hardcodes `recordPenalty.*` codes
   (`:1364-1393`) — take a code-prefix parameter; `ValidateByNotBlank` (`:1401`)
   is reusable as-is in spirit (`byBlank`).
3. **The supersede precedent holds**: `ParameterBindings` accumulates
   (`Competition.cs:437`), no already-bound refusal anywhere in `BindParameter`
   (`:619-644`); effectiveness is a lookup-time concern. Rulings do the same
   with an explicit last-wins law (RR3).
4. **Event mechanics**: closed union with `[JsonDerivedType]` names
   (`CompetitionEvents.cs:22-34`, twelve today — the header comment counts them),
   payloads wrap Domain value objects directly (`:10-14`); registration is ONE
   line in `src/Soarscore.Infrastructure/SoarscoreEventTypes.cs` (`All`,
   missing it fails at runtime on both backends per LADR-0001 §4.8); the API
   binds enums as strings already (`JsonStringEnumConverter` on HTTP JSON);
   anything-not-notFound maps to 400 with no routing change
   (`EndpointRouteBuilderExtensions.cs:60-67`).
5. **Read side is free**: `CompetitionView(Competition, …)`
   (`src/Soarscore.Application/Queries/Competitions/GetCompetition.cs:21`) wraps
   the aggregate — `Rulings` rides along. `CompetitionProjection` needs nothing.
6. **Corpus classes carrying `Undefined×2`**: F5L (`SeedF5L.cs:118-119`),
   NZ M ALES 200 (`SeedNzMAles200.cs:114-115`), NZ M NDC
   (`SeedNzMNdc.cs:85-86`), F3B's *Task C override* (`SeedF3B.cs:119-120`;
   F3B's class-level rule is defined). F3F is the one mixed case
   (`SeedF3F.cs:88`). For acceptance, NZ Class M (`80-nz-m-ales200`) is already
   driven end-to-end by `tests/Soarscore.Acceptance.Tests/Steps/CapturingAScoreSteps.cs:411`
   — proven drivable, and it is one of the story's named classes.
7. **Existing test coverage to extend**: `ReflightSelectorTests.cs`
   (`UndefinedRequiresRuling` facts at `:67-75`, `:113-121`),
   `ReflightScoringTests.cs` (pipeline failure fact `:182-194`),
   `ReflightSelectionPropertyTests.cs` (R1/R2/R3 generators — grow these, don't
   fork them), `AppendReflightGroupDecideTests.cs` (corpus-driven aggregate
   construction pattern), `ReflightGroupEventStoreTests.cs` (abstract-over-
   fixture store pattern), `ReflightingAGroupSteps.cs` (acceptance step
   conventions; its existing step definitions are reusable by new features —
   Reqnroll ambiguity is only about *defining* the same regex twice).

---

# Plan

## Property-based invariants (named now, per CLAUDE.md)

The CsCheck work (WI-4) proves these; they are stated here so the tests are
meaningful rather than discovered after the fact:

- **RR1 — a ruling fills silences only.** For any candidate pair, rule and
  ruled selection: when the role-applicable class slot is not
  `UndefinedRequiresRuling`, the selector's outcome is identical to the no-ruling
  call. The rulebook always beats the CD.
- **RR2′ — the ruled selection law.** Where the role-applicable slot IS silent
  and a ruled selection applies, the output is exactly the ruled application:
  `Replacement` → the reflight-role candidate's score; `BetterOf` → the max of
  both candidates' scores. (Extension of the reflight thread's R2.)
- **RR3 — last ruling wins.** Folding any sequence of `ReflightRulingRecorded`
  events yields, per (task-round, competitor) key, the selection of the
  sequence's final element. Log order is truth.

## Work items

Ordering constraints: **WI-1 first** (an unregistered event type fails at
runtime, but the shape should still land before anything appends it); WI-2 and
WI-3 before WI-5; WI-5 before WI-6 and WI-7; WI-3's scoring change before any
acceptance scenario asserting a score (WI-7).

### WI-0 (board) — take the story in flight

`git mv kanban/backlog/reflight-scoring-rulings.md kanban/in-progress/` and
update the status header in the same commit, before the first code commit.

### WI-1 (Domain) — value object, event, state, fold

**`ReflightRuling`** in `src/Soarscore.Domain/Shared.cs`, beside
`Penalty`/`TaskRoundCoordinate` (which it reuses):

```csharp
/// <summary>
/// A CD's recorded answer to the question the class rulebook leaves silent
/// (ReflightSelection.UndefinedRequiresRuling): which of this competitor's two
/// attempts for one task-round counts. Selection is Resolution-shaped only —
/// Replacement or BetterOf; NotPermitted asserts the rulebook forbids and
/// UndefinedRequiresRuling asserts it is silent, and neither is a decision.
/// Recorded, never derived (NFR-4): valid with no entries yet captured.
/// </summary>
public sealed record ReflightRuling
{
    public required TaskRoundCoordinate TaskRound { get; init; }
    public required CompetitorId CompetitorRef { get; init; }
    public required ReflightSelection Selection { get; init; }
    public required string Reason { get; init; }

    /// <summary>Optional, never blank — Penalty.By precedent.</summary>
    public string? By { get; init; }

    public required DateTimeOffset At { get; init; }
}
```

**Event** in `src/Soarscore.Domain/Competitions/CompetitionEvents.cs` — the
thirteenth event, wrapping the value object per the header's own convention
(`:10-14`):

```csharp
[JsonDerivedType(typeof(ReflightRulingRecorded), "reflightRulingRecorded")]  // line ~28

/// <summary>The CD settling which score counts where the class rulebook is
/// silent (reflight-scoring-rulings.md). Superseding rulings accumulate —
/// the log keeps every decision.</summary>
public sealed record ReflightRulingRecorded(ReflightRuling Ruling) : CompetitionEvent;
```

Update the file-header count comment (twelve → thirteen) and the mutation-list
sentence to name ruling-recording.

**State + fold** in `src/Soarscore.Domain/Competitions/Competition.cs`:
`public ImmutableArray<ReflightRuling> Rulings { get; init; } = [];` (+ the
creation-fold empty init alongside `Penalties = []` at `:373`), a typed
`Apply(ReflightRulingRecorded)` overload returning
`this with { Rulings = Rulings.Add(@event.Ruling) }` — **accumulate, never
replace**: supersede is a lookup law (RR3), the log keeps everything — and the
generic replay arm alongside `:495-511`.

### WI-2 (Domain) — the decide function

`Competition.RecordReflightRuling(ReflightRuling ruling)` in
`Competition.cs`, Defect-chain style next to `RecordPenalty` (`:1338-1350`),
returning `Result<ReflightRulingRecorded>`:

| Code | Condition |
|---|---|
| `recordReflightRuling.selectionNotAResolution` | `Selection` is `NotPermitted` or `UndefinedRequiresRuling` — neither is a decision |
| `recordReflightRuling.reasonRequired` | reason null or whitespace (`ReasonGiven` helper, substantive-record precedent) |
| `recordReflightRuling.byBlank` | `By` supplied but whitespace (`ValidateByNotBlank` shape) |
| `recordReflightRuling.taskRoundNotFound` | coordinate does not navigate to a task-round |
| `recordReflightRuling.taskRoundAnnulled` | task-round `State == Annulled` (nothing scores there — `AppendReflightGroup`'s stance) |
| `recordReflightRuling.competitorNotFound` | competitor not registered (typo protection; withdrawn NOT checked — planner's call 2) |
| `recordReflightRuling.classRuleSpeaks` | resolved rule has BOTH slots concrete — the rulebook governs, there is nothing to fill (decision 3) |

Rule resolution mirrors `AppendReflightGroup` (`Competition.cs:1111-1114`):
scan declared tasks for `Code == taskRound.TaskRef`, then
`task.Reflight ?? AdoptedRules.Definition.Reflight`. Extract the coordinate
navigation shared with penalties: generalise `ValidateTaskRoundCoordinate`
(`:1364-1393`) with a code-prefix argument, penalty call site passing
`"recordPenalty"` (existing penalty tests guard the refactor).

Doc comment records the **deliberately absent**, lifecycle-function style: no
entry-existence or pair-shape check (planner's call 3, NFR-4); no uniqueness
check (decision 2); no per-role necessity check in mixed classes (planner's
call 4 — `Competition` cannot know roles).

**Tests** — new `tests/Soarscore.Domain.Tests/RecordReflightRulingDecideTests.cs`,
mirroring `AppendReflightGroupDecideTests.cs`'s corpus-driven construction: one
fact per defect code (use F5L/NZ M for the accepting classes and F3K for
`classRuleSpeaks`), plus happy-path facts: the event carries the ruling
verbatim; folding appends; folding two rulings for one key yields two entries in
log order (RR3's fold half). Optional tidy within budget: extract the task-scan
rule-resolution shared with `AppendReflightGroup` into one private helper.

### WI-3 (Domain) — selector + scoring consumption

**a) `ReflightSelector.Select`** (`ReflightSelector.cs:58-97`) gains the third
parameter and applies it under exactly one condition:

```csharp
public static Result<decimal> Select(
    IReadOnlyList<(ReflightRole Role, decimal Score)> candidates,
    ReflightRule rule,
    ReflightSelection? ruledSelection = null)
```

```csharp
var selection = isEntitled ? rule.EntitledScores : rule.OthersScore;

// RR1: a ruling fills silences only — where the class speaks it governs.
if (selection == ReflightSelection.UndefinedRequiresRuling && ruledSelection is { } r)
{
    selection = r;
}
```

Doc comment updated: `ruledSelection` reaches here only from a validated
`ReflightRuling` (Replacement/BetterOf), but the switch's exhaustiveness keeps
it total regardless.

**b) `ScoringService.ScoreCompetition`** (`ScoringService.cs`), inside the
task-round body after `reflightRule` resolution (`:210`):

```csharp
var rulingsByCompetitor = competition.Rulings
    .Where(r => r.TaskRound.PhaseOrdinal == phase.Ordinal
             && r.TaskRound.RoundOrdinal == round.Ordinal
             && r.TaskRound.TaskRoundOrdinal == taskRound.Ordinal)
    .GroupBy(r => r.CompetitorRef.ToString())
    .ToDictionary(g => g.Key, g => g.Last().Selection);
```

and the collapse call becomes
`ReflightSelector.Select(candidates, reflightRule, rulingsByCompetitor.GetValueOrDefault(competitorRef))`.
`g.Last()` is the RR3 law made code: `Rulings` preserves fold (= log) order.
Everything outside the task-round body untouched; `ScoreTaskRound`'s view
untouched.

**Tests** — extend `ReflightSelectorTests.cs`: ruled Replacement and ruled
BetterOf for both roles (entitled pair, filler pair) over a silent rule;
RR1 facts (defined slot + ruling present → identical outcome); default-parameter
calls behave byte-identically to today. Extend `ReflightScoringTests.cs`: an
`Undefined×2` corpus class with a recorded ruling scores; without one still
fails `score.reflightRequiresRuling`; two rulings on one key → the later
decides.

### WI-4 (Domain tests) — property invariants (CsCheck)

Extend `tests/Soarscore.Domain.Tests/ReflightSelectionPropertyTests.cs`
(grow the existing generators with an optional ruled selection; do not fork):

- **RR1** — generate candidates, rule, ruling; when the applicable slot ≠
  `UndefinedRequiresRuling`, `Select(..., ruled)` equals `Select(...)`.
- **RR2′** — generate candidate score pairs and ruled selections over a silent
  rule; output equals the ruled application exactly (max vs reflight score).
- **RR3** — generate ruling sequences with repeated keys; the folded-state
  lookup per key equals the final sequence element's selection.

### WI-5 (Application) — command, handler, route, DI, registration

New `src/Soarscore.Application/Commands/Competitions/ReflightRulings.cs`,
mirroring `ReflightGroups.cs`:

```csharp
public sealed record RecordReflightRuling(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    CompetitorId CompetitorRef,
    ReflightSelection Selection,
    string Reason,
    string? By) : ICommand</* RecordPenalty's command return convention */>;

public sealed class RecordReflightRulingHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<RecordReflightRuling, /* same */>
```

Handler body, standard chain: `CompetitionLoader.LoadAsync` → construct the
`ReflightRuling` (`At = clock.UtcNow`, coordinate from the three ordinals) →
`competition.RecordReflightRuling(ruling)` → fail-propagate verbatim →
`eventStore.AppendAsync(..., ExpectedVersion.Exact(version), [decision.Value], ct)`
→ success per planner's call 6 (copy `RecordPenalty`'s command/handler return
convention exactly).

- **Route** — `src/Soarscore.Api/Commands/Commands.cs`, after the reflight-group
  line: `app.MapCommand<RecordReflightRuling, …>("/record-reflight-ruling");`
- **DI** — `src/Soarscore.Api/Composition.cs`, beside the other Competition handlers.
- **Registration** — `src/Soarscore.Infrastructure/SoarscoreEventTypes.cs`:
  `(typeof(ReflightRulingRecorded), "reflightRulingRecorded")` after the
  `reflightGroupAppended` line, commented `// reflight-scoring-rulings.md WI-5`.
  Missing this line fails at runtime on BOTH backends (LADR-0001 §4.8).
- **Architecture floor** — `tests/Soarscore.Architecture.Tests/HandlerRegistrationTests.cs`:
  raise the mapped-command count by one, comment updated.
- **Enum binding** — free: `"selection": "BetterOf"` binds via the existing
  HTTP `JsonStringEnumConverter`.

### WI-6 (Infrastructure) — store-backed

New `tests/Soarscore.Infrastructure.Tests/ReflightRulingEventStoreTests.cs`,
mirroring `ReflightGroupEventStoreTests.cs` exactly (one abstract class over
`IStoreFixture`; Postgres subclass tagged `Trait("Category", "Storage")`; SQLite
subclass untagged; read-back through `GetCompetitionHandler` so the fold is
fresh). Corpus `80-nz-m-ales200` (silent × 2). Drive
`RecordReflightRulingHandler` after a drawn phase; assert the read-back
competition carries the ruling verbatim (coordinate, competitor, selection,
reason, by, at); record a second ruling for the same key and assert both ride
in `Rulings` in log order; assert failure codes survive the round trip
(`reasonRequired`, `taskRoundNotFound`). This suite fails at runtime if WI-5's
alias line is missing — that is its purpose.

### WI-7 (Acceptance) — BDD

New `tests/Soarscore.Acceptance.Tests/Features/RecordingAReflightRuling.feature`
(+ steps class). Corpus **NZ Class M ALES 200** (`80-nz-m-ales200`) — already
driven end-to-end by `CapturingAScoreSteps.cs:411`, and one of the story's
named classes. Conventions per `ClosingACompetitionSteps.cs`' header: unique
GUID slug per scenario; step *definitions* self-contained (reuse existing
definitions from `ReflightingAGroupSteps.cs` where they fit — defining a
duplicate regex is the only sin); flight values chosen so raw == flightTime and
normalised scores are exact. Scenarios, in this order:

1. **The honest dead end, then the way out (governing scenario)** — draw, fly,
   append a reflight group, open the Entitled entry (worse re-flight) and
   Filler entries; `GET /scores` fails `score.reflightRequiresRuling`; record a
   Replacement ruling for the entitled pilot; the leaderboard computes, and the
   entitled pilot's score is exactly the worse re-flight's normalised score.
   Scores were captured *before* the ruling — NFR-4 visible, not asserted
   separately.
2. **Filler, ruled BetterOf** — the filler takes the better of their two
   normalised scores (RR2′ user-visible).
3. **Changed mind** — record BetterOf, then Replacement for the same
   competitor; the leaderboard follows the second (RR3 user-visible).
4. **Refusals** — `Selection: NotPermitted` fails
   `recordReflightRuling.selectionNotAResolution`; an unregistered competitor
   fails `recordReflightRuling.competitorNotFound`; a blank reason fails
   `recordReflightRuling.reasonRequired`.

Run the whole suite twice — `SOARSCORE_TEST_STORE=postgres` and
`SOARSCORE_TEST_STORE=sqlite` — a backend claim is unbacked otherwise (CLAUDE.md).

### WI-8 (house-keeping) — board and docs

- `git mv` to `kanban/completed/`, status header updated, in the completing commit.
- **Docs amendments (user-approved 2026-08-24)**, in the same commit:
  - `docs/soaring-domain-glossary.md` line ~59, the Entry paragraph's closing
    sentence gains: "…decided by the class rules or, where the class rules are
    silent, by the Contest Director's recorded ruling."
  - `docs/soaring-domain-class-diagram.md`: `ReflightRuling` value object;
    `Competition "1" *-- "0..*" ReflightRuling : settled by CD ruling`; one
    prose bullet in the notes section near the existing ruling discussion
    (~`:1172-1182`), citing this story.
  - `docs/aggregate-roots.md` §3: the mutation list gains ruling-recording (the
    events file header claims one-for-one mirroring — keep it true).
- Reconcile `kanban/tech-debt.md` and `kanban/deferred-decisions.md`: nothing
  open is discharged by this thread; record nothing unless implementation
  defers something real. New out-of-scope discoveries become `backlog/` stubs,
  never silent scope.

## Out of scope — deliberately

- **A one-shot group ruling** (one command settling every member of a reflight
  group) — per-competitor granularity subsumes it; a convenience command for
  later if a CD asks (planner's call 1).
- **Amend/revoke as distinct operations** — re-recording supersedes (decision
  2); revocation adds a concept for what a superseding ruling already expresses.
- **Preventing inert rulings in mixed classes** (F3F) — impossible at the
  aggregate boundary without entry data; documented, not policed (planner's
  call 4).
- **Any read surface beyond `GET /competition`'s passthrough** — no dedicated
  rulings query until someone asks for one.
- **Fly-off phases** — no second phase can be drawn (deferred-decisions.md);
  rulings are phase-generic already.

## Risks

- **WI-3b touches the collapse loop that feeds every leaderboard** — the same
  regression surface the reflight thread flagged. RR1's property is the guard:
  with no matching ruling the lookup misses and the call is byte-identical to
  today's. `ScoringACompetition`'s acceptance feature and the corpus property
  tests are the tripwire.
- **Last-wins depends on fold order surviving** — `ImmutableArray.Add` in fold
  order is trivially log order, but RR3 states it so a future fold rewrite that
  sorts or dedupes fails a named invariant instead of a leaderboard.
- **`classRuleSpeaks` needs task-rule resolution in a second decide** — the
  task-scan + `??` fallback duplicated from `AppendReflightGroup`; extract the
  shared helper in WI-2 while touching neither behaviour (both decides' existing
  tests guard).
- **The `ValidateTaskRoundCoordinate` generalisation moves penalty codes'
  plumbing** — pure refactor guarded by the existing penalty decide tests;
  land it in the same commit as its second consumer so no intermediate state
  compiles-with-a-lie.
