# Story — Reflights: `ReflightGroupAppended`

**Status:** In progress · **Raised:** 2026-08-16 · **Planned:** 2026-08-21 · **In flight:** 2026-08-24

## What

`ReflightGroupAppended` is mapped and folded but unreachable — no decide function, no
command. A group that must be re-flown cannot be recorded, and any competition in which
one competitor somehow holds two live Entries for one task-round cannot be scored at all
(`score.reflightNotSupported`, `src/Soarscore.Domain/Scoring/ScoringService.cs:168-179`).

This thread makes reflight groups a first-class workflow, end to end:

1. a **decide function** on `Competition` that appends a reflight group to an existing
   task-round, validating it against the adopted class's `ReflightRule` (resolved as data,
   never a branch on class — CLAUDE.md's core architectural law);
2. an **`AppendReflightGroup` command** with handler, route and event registration
   (closing the last-but-one unreachable `CompetitionEvent`);
3. **role-aware entry opening** — `OpenEntry` grows a `ReflightRole` so an entitled
   competitor's re-flight and a filler's supplementary flight are recorded as what they
   are (`Entry.cs:111-121` already defines the three roles; `Competition.cs:962` currently
   hardcodes `Original`);
4. **reflight scoring** — replacing the `score.reflightNotSupported` guard with
   Entitled/Filler selection per the class's `ReflightRule`
   (`ReflightSelection { Replacement, BetterOf, NotPermitted, UndefinedRequiresRuling }`,
   `src/Soarscore.Domain/PublishedClassDefinition/Enumerations.cs:31-37`).

The event payload grows a required `string Reason` while nothing appends it (shape changes
are free only until the first append — the annul thread's decision-1 precedent).

## Why it matters

Reflights are ordinary at a real contest — a mid-air, a timing failure, a launch equipment
fault. Today the only way to record one is to not need one. The F3F.1.5 one-pilot shape
(annul the provisional attempt, re-open) landed with the annul thread; the *group*
re-flight — F3K.9.6 b, F5J 5.5.11.6 c ii, F3J.4 priority 2, F3B.1.5 e — is the common case
and is what this story serves.

## Before starting — done

- **Rules cross-check, via the `fai-rules` skill (2026-08-21).** The common FAI pattern,
  verified verbatim for F3K.9.6, F3J.4, F5J 5.5.11.6 and F3B.1.5 e: the entitled
  competitor's re-flight is the official score *even if worse*; every other competitor in
  the reflight group (the fillers) takes the *better* of their two results; the new group
  has a class-specific minimum size (F3K 4, F3J 4, F5J 6; F3B names a parameter);
  priorities are (a) a following/incomplete group, (b) a new group of re-flyers completed
  by random draw, (c) the original group at the end of the round — only (b) forms a new
  group, and this event exists for exactly that case. Where the rulebook is silent the
  corpus records `UndefinedRequiresRuling`, not an assumption (F5L 5.5.12.9; F3B Task C —
  F3B.1.5 e names Tasks A/B only; NZ.3.12.5 l grants Class M a re-flight and never says
  which score counts). NZ Classes N and P permit none at all (NZ.3.13.1 h, NZ.3.15.1 h).
  The seed corpus already encodes all of this (`ReflightRule` per class, table below) —
  **no class-definition model changes are needed.**
- **Runtime trap, corrected.** The stub said `MartenConfig.cs`; the registry has since
  moved. The single alias table is `src/Soarscore.Infrastructure/SoarscoreEventTypes.cs`
  (`All`, lines 35-81); both `MartenConfig.cs:32-35` and `FisherConfig.cs:47-51` loop it.
  This thread adds ONE line and narrows the lines 55-57 comment to `RulesAmended` alone.
  Appending the event without that line fails at runtime on both backends per
  LADR-0001 §4.8 (`docs/ladr/ladr-0001-event-store.md:104-108`).

## Decisions settled before planning (user, 2026-08-21)

1. **Full thread, write-side and scoring together.** Without the scoring half the event
   lands write-only and a contest with a reflight stays unscoreable.
2. **"Better of the two results" compares normalised task-round scores** (the "official
   score" in group classes is the normalised one), not raw scores.
3. **The entitled competitor's ORIGINAL entry stays in its group's normalisation** — the
   flight physically happened; it can still set the 1000 basis for others — but contributes
   nothing to the entitled competitor's own aggregate; the Entitled entry replaces it there.
4. **`UndefinedRequiresRuling` selections (F3B Task C, F5L, NZ Class M): append allowed,
   scoring fails honestly** with an explicit code (`score.reflightRequiresRuling`). The
   group physically flew; the rulebook is silent; a CD-ruling recording mechanism does not
   exist and becomes a backlog stub (WI-11), not a silent assumption.
5. **Event shape: add a required `string Reason`** — audit parity with `TaskRoundAnnulled`,
   recording the entitlement basis (collision, hindrance, timing failure; F5J 5.5.11.6 b
   requires the hindering condition be noted).

### Planner's calls — flag for veto when this plan is reviewed

- **No marker distinguishes a reflight group from a drawn group.** None is needed: scoring
  discriminates by `Entry.Role`, not by group identity, and F3K.9.6 priorities (a)/(c) put
  entitled entries into *drawn* groups by design. `Group` stays as it is
  (`Competition.cs:243-251`).
- **Append is allowed on a `Complete` task-round** (protest-driven reflights are the
  ordinary late case — `kanban/backlog/entry-completeness-indicator.md`'s fourth reason);
  only `Annulled` refuses.
- **The command takes an explicit member list.** The rules say fillers are "selected by
  random draw", but automating that draw is a convenience feature, not a rule; the CD
  supplies the members that actually flew.
- **Entitlement is not validated.** Whether a competitor is entitled is a witnessed ruling
  (organiser's fault, collision); the system records it (as `Role` at entry-open, and as
  `Reason` on the append), it does not adjudicate it.
- **`CompetitorTaskResultView` gains a `ReflightRole Role` field** so the per-group score
  view can tell an entitled re-flight from a filler's second attempt apart.

## Findings from reading the tree

Written 2026-08-21 against the tree as it stood after
`annul-and-penalise-the-second-entry-thread.md` completed. File references cite
`file:line`; re-verify before acting on one — later commits move them.

1. **The event exists complete**: `ReflightGroupAppended(PhaseOrdinal, RoundOrdinal,
   TaskRoundOrdinal, Group, At)` at `src/Soarscore.Domain/Competitions/CompetitionEvents.cs:77-83`,
   JSON discriminator `"reflightGroupAppended"` at line 27. The fold
   (`Competition.cs:405-410`) appends the `Group` verbatim to the addressed task-round via
   `ReplaceTaskRound`; the generic replay arm is `Competition.cs:502`. Confirmed
   unreachable: no decide, command, handler, route, DI registration or alias.
2. **The class model already carries the whole rule.** `ReflightRule { EntitledScores,
   OthersScore, MinNewGroupSize? }` (`src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs:48-62`),
   class-level default at `ClassDefinition.cs:253`, per-task override at `:182` (F19),
   validated at adoption (`ClassDefinitionValidation.cs:62,138,173`). `MinNewGroupSize`
   null means *inapplicable* — no new group is ever formed (F26; F3F.1.5, NZ N/P) — and a
   `ParameterRef` means the CD binds it (F3B, F5L, NZ M). Every seed class declares it:
   F3K `SeedF3K.cs:246-250` (Replacement/BetterOf/4, F3K.9.6); F5J `SeedF5J.cs:146-150`
   (…/6, 5.5.11.6); F5K `SeedF5K.cs:320-324` (…/4, 5.5.10.13); F3J `SeedF3J.cs:126-130`
   (…/4, F3J.4); F3B `SeedF3B.cs:147-152` (…/param, F3B.1.5 e) with Task C's override
   Undefined×2 at `:114-122`; F3F `SeedF3F.cs:85-88` (Replacement/Undefined/null, F3F.1.5);
   F5L `SeedF5L.cs:116-120` (Undefined×2/param, 5.5.12.9); NZ M `SeedNzMAles200.cs:112-116`,
   `SeedNzMNdc.cs:83-87` (Undefined×2/param, NZ.3.12.5 l); NZ N/P `SeedNzNAles123.cs:82-85`,
   `SeedNzPRadian.cs:79-82` (NotPermitted×2).
3. **Roles exist on the Entry side and nowhere else.** `ReflightRole { Original, Entitled,
   Filler }` (`src/Soarscore.Domain/Entries/Entry.cs:121`), carried by `EntryOpened`
   (`EntryEvents.cs:50`), `Entry.Role` (`Entry.cs:153`) and the entry index
   (`EntrySummary.cs:36`). `Competition.OpenEntry` hardcodes `Original`
   (`Competition.cs:962`); the handler guard only inspects Original-role entries
   (`src/Soarscore.Application/Commands/Entries/OpenEntry.cs:62-81`).
4. **`Group.CompetitorRefs` is the drawn allocation, not "who flew"**
   (`Competition.cs:236-242`) — who a scoring pass counts is Entry-derived via `GroupRef`.
   Appending a group therefore needs no Entry data inside `Competition`, and
   `OpenEntry`'s `openEntry.competitorNotDrawn` check (`Competition.cs:902-906`) already
   accepts any member of the appended group.
5. **`Competition` mints `GroupId`s, `PhaseDraw` does not** (`Competition.cs:808-830`, and
   `PhaseDraw.cs`'s header) — the decide function mints `GroupId.New()` and
   `Ordinal = Groups.Length + 1` itself.
6. **Parameter resolution precedent.** `DrawPhase` resolves `MinPerGroup` through
   `ScoringService.FlattenParameterBindings` + `ParameterResolver.Resolve` with an
   `UnresolvedParameterException` catch (`Competition.cs:763-804`); `OpenEntry` does the
   same for working time at `:936-951`, round-scoped. `AppendReflightGroup` resolves
   `MinNewGroupSize` identically, with `(phaseOrdinal, roundOrdinal)` context.
7. **Scoring's group loop keys entries by competitor string**
   (`ScoringService.cs:201-203`, and `ScoreTaskRound.cs:120`), via
   `ToImmutableDictionary(e => e.CompetitorRef.ToString(), …)`. Two live entries for one
   competitor in one group — F3K.9.6 priority (c), an Entitled entry in the *original*
   group — would throw on duplicate keys once the `reflightNotSupported` guard
   (`ScoringService.cs:168-179`) is removed. The keying must become entry-based.
8. **The normalised score is `GroupResult.Results[..].RawScore` after normalisation**
   (`NormalisationEngine.cs:151` writes the normalised value into `RawScore`), and that is
   what `TaskRoundScore` carries (`ScoringService.cs:221-222`). So "better of the two
   normalised scores" (decision 2) is a plain comparison of two candidates' `RawScore`
   values from their respective `GroupResult`s.
9. **The aggregate keys scores by `"{RoundOrdinal}|{TaskOrdinal}|{index}"**
   (`ScoringService.cs:258-260`) — a competitor with two `TaskRoundScore`s for one
   task-round would corrupt drop-worst. Selection must collapse to exactly one score per
   competitor per task-round.
10. **The command chain to mirror is `AnnulTaskRound`**: record
    `src/Soarscore.Application/Commands/Competitions/TaskRoundLifecycle.cs:29-34`, handler
    `:54-62`, shared load→decide→append at `ExpectedVersion.Exact` `:79-107`, route
    `/annul-task-round` `src/Soarscore.Api/Commands/Commands.cs:31`, DI
    `src/Soarscore.Api/Composition.cs:94`, alias line `SoarscoreEventTypes.cs:64`.
11. **The API already binds enums as strings**: `Composition.cs:46-54` adds
    `ClassDefinitionIngestion.Options`' converters — including `JsonStringEnumConverter`
    (`ClassDefinitionIngestion.cs:134`) — to HTTP JSON, so a command body may carry
    `"role": "Entitled"` with no API-side change.
12. **Failure codes map themselves**: anything not `.notFound`/`eventStore.*` is a 400
    (`EndpointRouteBuilderExtensions.cs:60-67`) — every new code below needs no routing
    change.

---

# Plan

## Work items

Ordering constraints: **WI-1 first** (the event's shape is free only until WI-4 appends
it). WI-2 before WI-4; WI-3 before WI-5; WI-6 before WI-7; WI-4 before WI-9 and WI-10; the
scoring WIs (6-8) before any acceptance scenario that asserts a score (WI-10).

### WI-0 (board) — take the story in flight

`git mv kanban/backlog/reflight-groups.md kanban/in-progress/` and update the status
header in the same commit, before the first code commit.

### WI-1 (Domain) — grow the event payload

`src/Soarscore.Domain/Competitions/CompetitionEvents.cs:77-83`:

```csharp
/// <summary>A reflight group appended to an existing task-round. Reason records the
/// entitlement basis (collision, hindrance, timing failure — F5J 5.5.11.6 b); audit-only,
/// exactly like TaskRoundAnnulled's.</summary>
public sealed record ReflightGroupAppended(
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    Group Group,
    string Reason,
    DateTimeOffset At) : CompetitionEvent;
```

The fold ignores `Reason` (unchanged, `Competition.cs:405-410`). Update the four
construction sites positionally: `tests/Soarscore.Domain.Tests/CompetitionFoldTests.cs:155-165`
and `:298-320`; `CompetitionReplaceTaskRoundPropertyTests.cs` (`EventKind.AppendGroup`
generator); `tests/Soarscore.Application.Tests/Queries/Competitions/CompetitionProjectionPropertyTests.cs:112`.
No other references exist (verified 2026-08-21).

### WI-2 (Domain) — the decide function

`Competition.AppendReflightGroup` in `src/Soarscore.Domain/Competitions/Competition.cs`,
next to the task-round lifecycle decide functions (`:1011-1026` and helpers
`:1057-1072`), defect-chain style:

```csharp
public Result<ReflightGroupAppended> AppendReflightGroup(
    int phaseOrdinal,
    int roundOrdinal,
    int taskRoundOrdinal,
    ImmutableArray<CompetitorId> members,
    string reason,
    DateTimeOffset at)
```

Checks in order (each returns and no later check needs an earlier value except the
task-round, so: navigate first, then one chain):

| Code | Condition |
|---|---|
| `appendReflightGroup.taskRoundNotFound` | no phase/round/task-round with these ordinals (`FindTaskRound`, `Competition.cs:1057-1060`) |
| `appendReflightGroup.taskRoundAnnulled` | `taskRound.State == TaskRoundState.Annulled` (Complete/Drawn/InProgress all allowed — planner's call above) |
| `appendReflightGroup.reasonRequired` | reason null or whitespace (`ReasonGiven` helper) |
| `appendReflightGroup.newGroupNeverFormed` | resolved rule's `MinNewGroupSize` is null — the class never forms new groups (F26: F3F.1.5, NZ N/P) |
| `appendReflightGroup.notPermitted` | `EntitledScores` or `OthersScore` is `NotPermitted` (NZ N/P ordinarily already refuse at the check above, since they declare null min — belt and braces for a hypothetical class declaring both) |
| `appendReflightGroup.parameterUnbound` | `MinNewGroupSize` is a `ParameterRef` with no effective binding |
| `appendReflightGroup.membersEmpty` | members list empty |
| `appendReflightGroup.memberNotRegistered` | a member is not a registered `Competitor` |
| `appendReflightGroup.memberWithdrawn` | a member's `WithdrawnAt` is not null |
| `appendReflightGroup.memberDuplicated` | the same member appears twice |
| `appendReflightGroup.groupTooSmall` | `members.Length` < resolved `MinNewGroupSize` |

Rule resolution: find the task by scanning every phase's declared tasks for
`Code == taskRound.TaskRef` (the `OpenEntry` pattern, `Competition.cs:921-923`), then
`task.Reflight ?? AdoptedRules.Definition.Reflight` (F19 override). Resolve
`MinNewGroupSize` with `ParameterResolver.Resolve(rule.MinNewGroupSize,
ScoringService.FlattenParameterBindings(ParameterBindings, phaseOrdinal, roundOrdinal),
AdoptedRules.Definition.Parameters)` inside a `try`/`catch
(UnresolvedParameterException)` — the `OpenEntry` precedent at `Competition.cs:945-951`.

Mint and emit:

```csharp
var group = new Group(GroupId.New(), taskRound.Groups.Length + 1, members);
return Result<ReflightGroupAppended>.Success(
    new ReflightGroupAppended(phaseOrdinal, roundOrdinal, taskRoundOrdinal, group, reason, at));
```

Deliberately absent (record in the doc comment, as the lifecycle functions do): any check
that members already flew this task-round, that an entitled member exists, or that a
member already holds a reflight entry — `Competition` holds no Entry data (its own design
note, `Competition.cs:855-858`); entitlement and membership are CD rulings recorded as
`Reason` and as entry `Role`s, and the duplicate-reflight guard is WI-5's, on the Entry
side, where the data lives.

**Tests** — new `tests/Soarscore.Domain.Tests/AppendReflightGroupDecideTests.cs`, mirroring
`TaskRoundLifecycleDecideTests.cs`'s aggregate construction (fold
`CompetitionCreated → CompetitorRegistered×N → PhaseDrawn` in-memory; the corpus is
available to Domain tests — see `ScoringCorpusPropertyTests.cs`). One fact per defect code
plus the happy path asserting: the event's `Group.Id` differs from every existing group id,
`Group.Ordinal == Groups.Length + 1`, members verbatim, reason and `At` verbatim, and the
fold appends exactly one group. Use the corpus F3K (`10-f3k`, min 4) for literals and a
hand-built definition with a parameterised `MinNewGroupSize` for
`parameterUnbound`/binding cases (mirror `PhaseDrawnDecideTests`' parameterised
definitions).

### WI-3 (Domain) — role-aware `OpenEntry`

`Competition.OpenEntry` (`Competition.cs:859-964`) gains a `ReflightRole role` parameter,
between `competitorRef` and `at`; the event construction at `:954-963` emits it instead of
the hardcoded `ReflightRole.Original` at `:962`. No new decide checks — the role is data;
`Competition` cannot validate entitlement (finding 3). Extend the "Deliberately absent"
doc comment (`:855-858`) to say so.

**Tests** — extend `tests/Soarscore.Domain.Tests/OpenEntryDecideTests.cs`: the role
round-trips into the event for each of the three values; every existing fact is unchanged
(default role does not alter any existing behaviour — the handler supplies `Original`).

### WI-4 (Application) — command, handler, route, DI, registration

New file `src/Soarscore.Application/Commands/Competitions/ReflightGroups.cs`, mirroring
`TaskRoundLifecycle.cs`:

```csharp
public sealed record AppendReflightGroup(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    IReadOnlyList<CompetitorId> Members,
    string Reason) : ICommand<GroupId>;

public sealed class AppendReflightGroupHandler(IEventStore eventStore, IClock clock)
    : ICommandHandler<AppendReflightGroup, GroupId>
```

Handler body: `CompetitionLoader.LoadAsync` → `competition.AppendReflightGroup(...)` with
`Members` converted to `ImmutableArray` → on success
`eventStore.AppendAsync(competitionRef.Value, ExpectedVersion.Exact(version),
[decision.Value], ct)` → return `decision.Value.Group.Id`. Failure forwarding verbatim,
exactly as `TaskRoundLifecycle.AppendAsync` (`TaskRoundLifecycle.cs:79-107`) does.

- **Route** — `src/Soarscore.Api/Commands/Commands.cs`, after the `/record-competition-penalty`
  line (33): `app.MapCommand<AppendReflightGroup, GroupId>("/append-reflight-group");`
  (verbs, never nouns).
- **DI** — `src/Soarscore.Api/Composition.cs`, next to the other Competition handlers
  (~line 94): `AddScoped<ICommandHandler<AppendReflightGroup, GroupId>,
  AppendReflightGroupHandler>()`.
- **Registration** — `src/Soarscore.Infrastructure/SoarscoreEventTypes.cs`: add
  `(typeof(ReflightGroupAppended), "reflightGroupAppended")` after the
  `competitionPenaltyRecorded` line (67) with a `// reflight-groups.md WI-4` comment;
  narrow the lines 55-57 comment to name `RulesAmended` alone as unregistered.
- **Architecture floor** — `tests/Soarscore.Architecture.Tests/HandlerRegistrationTests.cs:71`:
  raise `HaveCountGreaterThanOrEqualTo(22)` to `23` and update the comment at lines 69-70
  (one more command is mapped).

### WI-5 (Application) — `OpenEntry` command role + handler guard

- **Command** — `src/Soarscore.Application/Commands/Entries/OpenEntry.cs:36-38` gains
  `ReflightRole Role = ReflightRole.Original` (optional, default `Original`: every existing
  caller, test and acceptance scenario is unchanged; the API binds `"role": "Entitled"`
  as a string — finding 11). The handler passes `command.Role` into the decide call at
  `:84-91`.
- **Guard** — replace the loop at `OpenEntry.cs:62-81`. Load the stream for *every* entry
  the index returns for this competitor+task-round (not only Original-role ones; the index
  carries `Role`, `EntrySummary.cs:36`, but live/annulled needs the stream load — the
  existing stance, header lines 17-25). Then:
  - `command.Role == Original`: any live entry of **any** role → `openEntry.alreadyOpen`
    (message unchanged).
  - `command.Role == Entitled or Filler`: any live **reflight-role** entry → new code
    `openEntry.reflightAlreadyOpen`, message naming the rule: a competitor who was not
    allocated the new attempt is not entitled to another working time (F3K.9.6,
    5.5.11.6 iv closing sentence). A live `Original` does **not** block — that is the
    reflight shape.
  - Annulled entries of any role never block (unchanged).

**Tests** — `tests/Soarscore.Application.Tests/Commands/Entries/`: live Original + Entitled
open succeeds; live Entitled + second Entitled open fails `openEntry.reflightAlreadyOpen`;
annulled Entitled does not block a new Entitled open; live Filler blocks a new Original
open; command without `Role` behaves exactly as before (default).

### WI-6 (Domain) — reflight scoring

Two pieces in `src/Soarscore.Domain/Scoring/`:

**a) `ReflightSelector.cs`** — a pure static helper, no Entry dependency:

```csharp
public static Result<decimal> Select(
    IReadOnlyList<(ReflightRole Role, decimal Score)> candidates,
    ReflightRule rule)
```

- One candidate → its score.
- Two candidates, one `Original` + one reflight-role (call it R): the applicable
  `ReflightSelection` is `R.Role == Entitled ? rule.EntitledScores : rule.OthersScore`;
  `Replacement` → R's score; `BetterOf` → `max(original.Score, R.Score)`;
  `NotPermitted` → `score.reflightNotPermitted`; `UndefinedRequiresRuling` →
  `score.reflightRequiresRuling` (message names the class/task and states the rulebook is
  silent — decision 4).
- Any other shape (two same-role candidates, three or more) → `score.reflightShapeUnsupported`
  (message names the competitor coordinate and the roles seen).

**b) `ScoringService.ScoreCompetition`** — restructure the task-round body
(`ScoringService.cs:153-248`), keeping everything outside it untouched:

1. `taskRoundEntries` (live entries for the coordinate) — unchanged (`:155-159`; annulled
   exclusion stays at `:201`).
2. Replace the `score.reflightNotSupported` guard (`:168-179`) with the shape guard:
   group by `CompetitorRef`; each competitor's live entries must be one entry of any role,
   or exactly one `Original` plus exactly one reflight-role entry — otherwise
   `score.reflightShapeUnsupported` (via `ReflightSelector`, which owns the shape law).
3. Per group: collect the group's live entries as a *list*; skip if empty (unchanged,
   `:207-208`); key them **by entry** for `ScoreGroup` — key format
   `$"{entry.CompetitorRef}|{entry.Id}"`, defined once (a `static` on `ReflightSelector`,
   e.g. `EntryKey(Entry)`) so WI-7 uses the identical format. `ScoreGroup`'s signature is
   unchanged — its key parameter is already caller-chosen; document that in its doc
   comment. Two live entries of one competitor in one group now both normalise in it
   (decision 3 — the original still competes for the 1000 basis).
4. Collect candidates per competitor: for each entry in the group,
   `(entry.Role, groupResult.Results[ReflightSelector.EntryKey(entry)].RawScore)` — the
   normalised score, per finding 8.
5. Select per competitor via `ReflightSelector.Select(candidates, rule)` where
   `rule = taskDefinition.Reflight ?? classDef.Reflight`; the selected decimal builds the
   ONE `TaskRoundScore` (`:221-222` construction, fed by the selection) — invariant R1.
6. State mapping (`:238-245`) and everything downstream — unchanged. The aggregate
   keying (`:258-260`) is safe again because selection collapsed the candidates (finding 9).

**Tests** — extend `tests/Soarscore.Domain.Tests/ScoringServiceAnnulmentTests.cs`'s
fixture style and add facts: entitled's worse reflight replaces a better original
(Replacement, F3K corpus shape); filler takes the better of two normalised scores
(BetterOf); entitled re-flight in the *same* group as the original (priority c) does not
throw and replaces; a `NotPermitted` class fails `score.reflightNotPermitted`; an
`UndefinedRequiresRuling` class fails `score.reflightRequiresRuling`; two same-role live
entries fail `score.reflightShapeUnsupported`; a lone Entitled entry (original annulled)
scores as an ordinary entry. Pure-selector facts for `ReflightSelector` directly (one per
`ReflightSelection` × role).

### WI-7 (Application) — `ScoreTaskRound` entry keying

`src/Soarscore.Application/Queries/Scoring/ScoreTaskRound.cs:112-131`: the group loop keys
by entry (same `ReflightSelector.EntryKey`, finding 7's duplicate-key fix), keeping a side
map entry-key → `Entry` as it builds the dictionary. `MapGroupResult` (`:136-149`) decodes
via the side map: `Results` rows become one `CompetitorTaskResultView` per entry — a
competitor with two entries in one group appears twice, honestly (planner's call: add the
`ReflightRole Role` field to `CompetitorTaskResultView` so the rows are distinguishable) —
and `WinnerRef` decodes through the same map before `CompetitorId.Parse`. Per-group
selection is deliberately NOT applied here: the view is per group, and the collapse to one
score per task-round is the aggregate's job (`ScoreCompetition`).

**Tests** — `tests/Soarscore.Application.Tests/Queries/`: a task-round with an appended
reflight group returns views for every group (reflight group included); a group holding
Original + Entitled entries of one competitor returns two rows with their roles and the
correct winner.

### WI-8 (Domain tests) — property-based invariants (CsCheck)

New `tests/Soarscore.Domain.Tests/ReflightSelectionPropertyTests.cs`. The invariants,
named here per CLAUDE.md so the tests are meaningful rather than discovered after the
fact (before WI-6 each is false by construction — the tests land with the fix):

- **R1 — one score per task-round.** However many live Entries a competitor holds in a
  task-round, `ScoreCompetition` produces at most one `TaskRoundScore` for that
  competitor for that task-round. Generate fields, multi-entry shapes
  ({Original, Entitled}, {Original, Filler}) and selections (Replacement/BetterOf).
- **R2 — the selection law.** Under `BetterOf` the selected score is exactly the max of
  the competitor's candidates' normalised scores; under `Replacement` it is exactly the
  reflight-role candidate's. Generate candidate score pairs and check the selector output
  against the definition directly.
- **R3 — reflight groups are additive.** Appending a reflight group — with any reflight
  entries — changes no task-round score of any competitor who holds no reflight-role
  entry in that task-round. Generate, score, append (fold the event plus entries), score
  again, compare the untouched competitors' scores.

### WI-9 (Infrastructure tests) — store-backed

New `tests/Soarscore.Infrastructure.Tests/ReflightGroupEventStoreTests.cs`, mirroring
`TaskRoundLifecycleEventStoreTests.cs` exactly (one abstract class over `IStoreFixture`,
Postgres subclass tagged `Trait("Category", "Storage")`, SQLite subclass untagged; real
handlers, read-back through `GetCompetitionHandler` so the fold is fresh — that file's
header, lines 5-17, states why). F5J corpus (`30-f5j`): literal `MinPerGroup` 6 and
reflight min 6 (`SeedF5J.cs:146-150`). Drive `AppendReflightGroupHandler` with a
6-member group after a drawn phase; assert the read-back competition shows the group with
`Ordinal` 2 and the members verbatim; assert the failure codes survive the round trip
(`groupTooSmall` with 5 members, `reasonRequired`, `newGroupNeverFormed` needs a null-min
class so cover it in WI-2 only). This is the suite that fails at runtime if WI-4's
registration line is missing — on both backends (LADR-0001 §4.8).

### WI-10 (Acceptance) — BDD

New `tests/Soarscore.Acceptance.Tests/Features/ReflightingAGroup.feature` +
`Steps/ReflightingAGroupSteps.cs`. Follow `ClosingACompetitionSteps.cs`'s conventions:
self-contained step regexes (Reqnroll binds assembly-wide — a regex shared verbatim with
another Binding class is an ambiguous match), a unique GUID slug per scenario, resolve the
draw's shape with `ResolveGroupAsync` rather than assuming it, and flight-time values
chosen so raw == flightTime and normalised round scores are exact
(`ClosingACompetitionSteps.cs`'s header, lines 1-26). F3K corpus (`10-f3k`), drawn as a
catalogue-choice phase naming its tasks via a Gherkin table — the
`DrawingACatalogueChoicePhase` feature's own pattern (its steps class shows the table
binding). Scenarios, in this order:

1. **Governing principle** — a new reflight group (F3K.9.6 b): an entitled competitor
   whose re-flight is *worse* is scored on the re-flight; a filler is scored on the better
   of their two normalised scores; a competitor in no reflight group keeps their score
   (R3, user-visible). Members: the entitled competitor plus fillers to reach F3K's
   minimum 4; open the Entitled/Filler entries with `"role"` in the `/open-entry` body.
2. **Priority (c)** — the entitled competitor re-flies with their original group: an
   `Entitled` entry opened against the *original* group's id replaces the original score
   (assert the aggregate score equals the worse re-flight's normalised score).
3. **Minimum size** — a 3-member group is refused with `appendReflightGroup.groupTooSmall`.
4. **A completed task-round can grow** — complete the task-round
   (`/complete-task-round`), then append the reflight group successfully and see it via
   `GET /competition` (the protest shape — `entry-completeness-indicator.md`'s fourth
   reason, now real).

Score assertions mirror `ScoringACompetitionSteps`' mechanics with fresh step texts.
Run the whole suite twice — `SOARSCORE_TEST_STORE=postgres` and
`SOARSCORE_TEST_STORE=sqlite` — a backend claim is unbacked otherwise (CLAUDE.md).

### WI-11 (house-keeping) — board and inventories

- `git mv` to `kanban/completed/`, status header updated, in the completing commit.
- New stub `kanban/backlog/reflight-scoring-rulings.md`: a CD ruling mechanism for
  `UndefinedRequiresRuling` selections (F3B Task C, F5L, NZ Class M) — recording a
  competition-time ruling of Replacement/BetterOf per reflight. What/Why/Before starting,
  citing decision 4 and `score.reflightRequiresRuling`.
- Note in `kanban/backlog/smaller-items.md` §Unclaimed that `RulesAmended` is now the
  *only* unreachable event (this thread took `ReflightGroupAppended`).
- Reconcile `kanban/tech-debt.md` and `kanban/deferred-decisions.md`: nothing open is
  discharged by this thread; add nothing unless implementation defers something real.
  Any new out-of-scope discovery becomes a `backlog/` stub, never silent scope.

## Out of scope — deliberately

- **F3F.1.5 one-pilot re-flights into the running order** — already served by
  annul-and-reopen (`kanban/completed/annul-and-penalise-the-second-entry-thread.md`);
  F3F declares `MinNewGroupSize` null and the append refuses (`newGroupNeverFormed`).
- **A CD ruling mechanism for `UndefinedRequiresRuling`** — decision 4; stub in WI-11.
- **Automated filler draw** ("selected by random draw") — the command takes explicit
  members (planner's call above).
- **Fly-off constraints** (F3K.9.6: fly-off reflights may use only priority c) — no
  second phase can be drawn yet; deferred with the phase-scope deferrals in
  `kanban/deferred-decisions.md`.
- **Repointing `Competition.OpenEntry`'s inline traversal at `TaskResolver`** (open
  tech-debt item, `kanban/tech-debt.md:32-41`) — `TaskResolver` lives in Application and
  Domain cannot reference it; discharging the item means *moving* `TaskResolver` to
  Domain, a dependency-direction change, not a repoint. Not this thread's risk budget
  while WI-3/WI-6 are already editing these exact functions.
- **Preparation time / working-time windows for reflight groups** — the standing
  remove-stored-working-time stance; entries validate the same binding `OpenEntry` always
  has.
- **`RulesAmended`** — after this thread the sole remaining unreachable event; it needs
  its own story (`smaller-items.md` §Unclaimed already says so).

## Risks

- **WI-6 is the regression surface**: the group-scoring loop feeds every leaderboard.
  `ScoringACompetition`'s acceptance feature and `ScoringCorpusPropertyTests` guard it;
  R3 exists to prove the no-reflight path is bit-identical.
- **Entry keying (WI-6b/WI-7) changes what `GroupResult.Results` keys mean** at the two
  call sites — the side maps must be built where the dictionaries are, or `WinnerRef`
  parsing breaks silently. `ScoreTaskRound`'s winner assertions in
  `ScoringEventStoreTests.cs` are the tripwire.
- **WI-5 touches the highest-volume write path** (`OpenEntryHandler`): the widened guard
  loads one stream per existing entry instead of per Original entry. Club scale (≤ 20
  pilots) makes this irrelevant; recorded so the cost is deliberate.
- **The event-shape change (WI-1) must land first** — after WI-4 appends the event for
  real, `Reason` can never be added without a new event version (LADR-0001 §4.8's
  rationale: the log is immutable).
