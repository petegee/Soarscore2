# Story — Lane/spot assignment for drawn groups

**Status:** Backlog · **Raised:** 2026-08-31 (split out of the teams
direction discussion — see `teams-feature-options.md`. Independent of teams:
under Option 2 the teams story stops at roster data and explicitly excludes
physical lane/spot allocation, so if this is wanted it must stand alone.) ·
**Fleshed out:** 2026-08-31 — owner settled all five planning decisions
(§Decisions); the plan below is implementable as written.

## What

Make the physical field position of a competitor within a drawn group
explicit data: per task-round group, map each live competitor to a **spot**
— a single integer label whose physical meaning (lane, launch spot, landing
spot, winch line) belongs to the venue, not to Soarscore — and expose that
mapping through the operational read views used at score-capture time.

Deliberately **team-blind**. GliderScore derives lane allocation from its
single team number (`teams-feature-options.md` §"What GliderScore
implements"); Soarscore must not reproduce that coupling. Team-aware field
coordination (e.g. F3J's landing-spot rule referencing team members) is out
of scope here and would return to the teams thread if ever wanted.

## Why it matters

- The draw today ends at an ordered `Group.CompetitorRefs` array
  (`src/Soarscore.Domain/Competitions/Competition.cs:243-251`); nothing says which
  physical position each slot corresponds to, so every consuming system
  has to guess or hard-code one — exactly the overload the research paper
  warns against (design principle 10: "Sequence position is not
  automatically a physical lane, launch spot, landing spot, or winch
  line. Do not overload the existing ordered competitor array without
  deciding which fact is being represented.").
- Decision question 5 in the paper ("Does the first release need physical
  lanes/spots, or only group membership and sequence? These are different
  facts.") is answered *separately* from the teams option; this story is
  where the physical-fact half gets decided on its own merits.
- NFR-3: Soarscore exposes report-ready data — the mapping is exactly the
  kind of fact a consuming scoring/sheet application needs and cannot
  derive.
- `docs/users.md`'s Scorer ("scoped to one competitor and task… no hunting
  for the right entry") is the direct beneficiary: the consuming capture UI
  can say "spot 3 → competitor X" instead of inferring it from array order.

## Decisions settled during planning (2026-08-31, owner-confirmed)

1. **One generic spot label.** A spot is one `int` per competitor per group
   — distinct positive integers, *not* required contiguous (a broken lane
   being skipped is the ordinary case). Launch spot, landing spot and winch
   line share one physical position at club scale, and no rule in either
   rulebook varies the *data shape* of a designation (see the rules check
   below) — so one label, venue-interpreted, is the whole fact. Separate
   launch/landing/winch assignments, if a venue ever demands them, are an
   additive extension (NFR-2), not speculation to build now.
2. **Always explicit, per group.** A command assigns (or replaces) the
   complete mapping for one group. No per-competition default, no
   auto-generated identity mapping at draw or accept time — the domain never
   infers a spot from sequence position (principle 10); a consuming UI that
   wants identity prefill sends it as data in one call. Until assigned, a
   group reads *unassigned* — a fact, not a gap (the `TaskRoundRecording`
   philosophy: presence proves, absence never disprove).
3. **Live mutable; dies with the draw.** Assignments live ON the `Group`
   record in the fold, so: they may be re-assigned any time the task-round
   is not annulled (broken winch → shift lanes is normal operations; the
   last assignment wins and the audit trail is the stream); rejecting the
   draw removes the phase (D2 of `draw-acceptance-redraw.md`) and every
   assignment on it automatically; a redraw's groups start unassigned.
   Nothing gates on assignment existing — NFR-4 is untouched.
4. **Full coverage required.** One command assigns every live member of the
   group (drawn ∧ not withdrawn) to a distinct positive spot. A competitor
   who withdraws *after* assignment leaves the assignment recorded and
   reading as vacant — Expected = drawn minus withdrawn, the established
   pattern (`entry-completeness-indicator.md`'s deferral note).
5. **Glossary/class-diagram concept approved.** Name: **Spot**. Approved
   wording (lands verbatim in WI-1; any drift returns to the owner):
   > *A spot is a competitor's designated physical field position within a
   > group for a task-round — a lane, launch spot, landing spot or winch
   > line as the venue arranges them. Spots are explicit data, never implied
   > by draw sequence; the field layout itself lives outside Soarscore.*
6. **`aggregate-roots.md` §3 sentence (F1 pattern).** `CompetitionEvents.cs`'s
   header states the event set mirrors §3's mutation list one-for-one
   (`:8-9`), so the sixteenth event makes §3's mutation sentence stale
   (`docs/aggregate-roots.md:199-203`). Draft wording in WI-1; **ask the
   owner fresh at implementation time** — a separate confirmation from this
   planning conversation, exactly as `draw-acceptance-redraw.md` F1 required.

## Rules check (fai-rules, 2026-08-31)

- **No class rule varies the data shape of an assignment** — the rule
  references to spots are *scoring consequences* of a designated spot, which
  the consuming system (knowing the venue) enforces, not shapes Soarscore
  must store differently per class:
  - F5J `5.5.11.7 d/f` — zero if the nose is not within 75 m of the
    competitor's **designated landing spot**; zero if not launched from the
    **correct starting point**.
  - NZ `NZ.2.4.6` (`docs/rules/nz/00-nz-general-rules.md` §3) — the same 75 m
    landing-spot zero, universal across the NZ classes, as a
    `flightValidWhen` gate.
  - Re-flight placement priority 1 in F3J/F3K/F5J/F5K admits a complete
    group flying "on **additional launch spots**" — so *appended reflight
    groups are spot consumers too*, which is why the command below works on
    any group, drawn or appended.
  - F3J's "no member of his team in it" adjacency is team-aware and is *not*
    a driver here (the story's standing exclusion).
- Therefore **nothing lands in the class definition** — no parameter, no
  task field, no named-class branch (NFR-1/NFR-2 respected by absence).
  Spot count and layout are venue data, outside Soarscore.
- **Cite what you encode:** the `GroupSpot` record's doc comment carries
  these refs so a future edition bump is traceable.

## Findings from reading the tree (verified 2026-08-31)

1. **Groups are round-scoped already.** `DrawPhase` mints fresh `GroupId`s
   for every round's groups up front (`Competition.cs:758-802`), and
   `PrescribeDraw` does the same for prescribed schedules (`:818-921`).
   "Per group" is therefore automatically "per task-round" — no extra
   coordinate is needed on the assignment beyond group identity. A reflight
   group appended later (`AppendReflightGroup`, `:1402-1520`) mints its own
   `GroupId` and is assignable like any other.
2. **The fold has a ready-made navigation shape.** One typed `Apply` overload
   per event (`Competition.cs:406-494`), the `ReplaceTaskRound` helper for
   ordinal-safe phase/round/task-round rebuilds (`:503-538`), and the generic
   replay switch that needs one new case (`:546-565`). The new fold arm is a
   `ReplaceTaskRound` call whose mutate maps the named group to
   `group with { Spots = … }` — the `ReflightGroupAppended` pattern, one
   level deeper.
3. **`GET /competition` needs no edit.** It returns the folded aggregate
   directly (`GetCompetition.cs`, `CompetitionView(Competition, …)`), so
   `Group.Spots` is exposed the moment it exists on the record.
4. **The operational view to extend is `GET /task-round-recording`.**
   `GroupRecordingView` (`TaskRoundRecording.cs:50-56`) is the capture-time
   read; `RecordingCore.ComputeGroupViews` (`:183-246`) is the pure fold the
   property tests already drive with no store. It gains the spot list;
   everything else about its shape (Expected = drawn minus withdrawn,
   fact-only fields, "nothing may be phrased as complete") is untouched.
5. **Payload compatibility is additive and safe.** `Group` travels inside
   `PhaseDrawn` and `ReflightGroupAppended` payloads (`CompetitionEvents.cs:75-95`).
   The new `Spots` property must NOT be `required` (initializer `= []`) so
   any pre-existing payload deserializes with empty spots. Green-field — no
   production streams exist (CLAUDE.md project status); test streams are
   created fresh per run. The JSON tests round-trip freshly-built events
   (`CompetitionEventJsonTests.cs:269-279`, `:317-327`), so no pinned-text
   breakage is expected — if one surfaces, updating the expectation is
   correct (additive property), not a workaround.
6. **Routing/wiring cost is one line each.** Handler DI beside
   `Composition.cs:92-100`; route beside `Commands.cs:37`
   (`/append-reflight-group`); the handler-registration floor is
   `HaveCountGreaterThanOrEqualTo(27)` at
   `tests/Soarscore.Architecture.Tests/HandlerRegistrationTests.cs:74` → 28.
   Route-shape test unaffected (POST allowed).
7. **Event-type registry:** one pair appended in the competition block of
   `SoarscoreEventTypes.cs` after `DrawRejected` — a missing line fails at
   runtime on BOTH backends (LADR-0001 §4.8; WI-7's store suite is the net).
8. **Acceptance-test templates exist.** `AcceptingTheDraw.feature` +
   steps are the newest lifecycle-feature pair; step definitions live under
   `tests/Soarscore.Acceptance.Tests/StepDefinitions/`. The shared draw-Given
   steps already end in an accept call (WI-7 of `draw-acceptance-redraw.md`),
   so the new feature's Givens compose with them unchanged.
9. **No cross-aggregate fact is needed at assignment time.** Group live
   membership is inside the Competition aggregate; the handler needs only
   `IEventStore` + `IClock` (the `AcceptDrawHandler` template,
   `AcceptDraw.cs`) — unlike `RejectDraw`, which resolves
   `phaseHasEntries` through `IEntryQuery`.
10. **Cross-reference (house rule 2) — clean.** `docs/users.md` (Scorer's
    scoped-capture need is directly served; no new user role), the NFRs
    (NFR-1/2: no class variation to model — rules check above; NFR-3:
    report-ready data only; NFR-4: nothing gates, capture untouched), the
    rule docs (no contradiction), and `teams-mvp.md` (explicitly excludes
    lanes and cites this story — no dependency in either direction). No
    conflicts found; nothing to reconcile.

---

# Plan

## Work items

Each WI is small enough for one agent session and lands compiling.

### WI-1 — Spot value record, event, fold, and approved docs (Domain)

**`src/Soarscore.Domain/Competitions/Competition.cs`:**

- New value record beside `Group` (doc comment cites this story and the
  rules-check refs; "cite what you encode" applies to the *concept*, the
  number itself is venue data):

  ```csharp
  /// <summary>
  /// One competitor's designated physical field position within a group for a
  /// task-round — a lane, launch spot, landing spot or winch line as the venue
  /// arranges them. The label's meaning is the venue's; Soarscore stores only
  /// the explicit mapping (rules refs: F5J 5.5.11.7 d/f, NZ.2.4.6, re-flight
  /// priority 1 "additional launch spots" — scoring consequences of a
  /// designated spot, enforced by consuming systems). Never implied by
  /// sequence position — teams-feature-options.md design principle 10.
  /// </summary>
  public sealed record GroupSpot(CompetitorId CompetitorRef, int Spot);
  ```

- `Group` gains `public ImmutableArray<GroupSpot> Spots { get; init; } = [];`
  — deliberately **not** `required` (finding 5), with a doc comment stating:
  empty means *unassigned* (a fact), assignments are explicit data, and
  withdrawal after assignment leaves the entry reading as vacant (D4).
- New event in **`CompetitionEvents.cs`**, appended after
  `ReflightRulingRecorded`; discriminator `groupSpotsAdded` to the
  `[JsonDerivedType]` block (`:23-38`); header comment `fifteen → sixteen`
  and the mutation sentence gains "assign a group's field spots" (`:5-9`):

  ```csharp
  /// <summary>
  /// The CD (or a consuming setup UI) assigning one group's field spots for a
  /// task-round. Whole-replacement semantics: the payload is the complete
  /// mapping and replaces whatever was there (decision D3 — re-assignment is
  /// ordinary field operations; the audit trail is the stream). Assigned to
  /// the group by Id; spots are venue-interpreted labels (D1), validated
  /// against the group's live membership by the decide function.
  /// </summary>
  public sealed record GroupSpotsAssigned(
      int PhaseOrdinal,
      int RoundOrdinal,
      int TaskRoundOrdinal,
      GroupId GroupRef,
      ImmutableArray<GroupSpot> Spots,
      DateTimeOffset At) : CompetitionEvent;
  ```

- Fold overload after `ReflightRulingRecorded`'s (`Competition.cs:493-494`),
  reusing the `ReplaceTaskRound` helper with an inner group map:

  ```csharp
  public Competition Apply(GroupSpotsAssigned @event) =>
      ReplaceTaskRound(
          @event.PhaseOrdinal, @event.RoundOrdinal, @event.TaskRoundOrdinal,
          taskRound => taskRound with
          {
              Groups = taskRound.Groups
                  .Select(g => g.Id == @event.GroupRef ? g with { Spots = @event.Spots } : g)
                  .ToImmutableArray(),
          });
  ```

- One case in the generic replay switch (`:546-565`):
  `GroupSpotsAssigned e => Require(current, e).Apply(e),`.

**Docs, landing the owner-approved wording (decisions D5/D6):**

- `docs/soaring-domain-glossary.md` — the Spot entry, verbatim from decision
  5, inserted after the task-round entry (its nearest neighbour conceptually).
- `docs/soaring-domain-class-diagram.md` — `Group` gains the association
  `Group "1" *-- "0..*" GroupSpot : field spots`, a `GroupSpot` class
  (`+CompetitorId competitorRef`, `+int spot`), and a one-line note: "empty
  = unassigned; assignments die with the phase (draw rejection removes them)".
- `docs/aggregate-roots.md` §3 (`:199-203`) — extend the mutation sentence to
  "…append a reflight group, **assign a group's field spots**, complete /
  annul / reopen a task-round, record a reflight ruling" — **only after the
  fresh owner confirmation D6 requires.**

### WI-2 — Decide function (Domain: `Competition.cs`)

Instance decide function `AssignGroupSpots`, placed after
`AppendReflightGroup` (`:1402-1520`), same defect-chain style (each check
independent, early returns). Signature:

```csharp
public Result<GroupSpotsAssigned> AssignGroupSpots(
    int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal,
    GroupId groupRef,
    IReadOnlyList<GroupSpot> spots,
    DateTimeOffset at)
```

Defect table, in check order (codes prefixed `assignSpots.`):

| Code | Condition |
|---|---|
| `assignSpots.taskRoundNotFound` | `FindTaskRound(...)` is null |
| `assignSpots.taskRoundAnnulled` | task-round state is `Annulled` (the `AppendReflightGroup` precedent at `:1421-1427` — resolution, not a block; Complete/Drawn/InProgress all allow) |
| `assignSpots.groupNotFound` | no group with `groupRef` in this task-round |
| `assignSpots.assignmentsEmpty` | `spots` empty |
| `assignSpots.competitorNotInGroup` | an assigned `CompetitorRef` is not a **live** member of the group (not drawn, or withdrawn) — D4's live-membership boundary |
| `assignSpots.competitorRepeated` | the same `CompetitorRef` appears twice |
| `assignSpots.spotDuplicated` | the same spot number appears twice |
| `assignSpots.spotInvalid` | a spot number is < 1 |
| `assignSpots.memberMissing` | a live group member has no assignment (full coverage, D4) |

Semantics spelled out so no implementer guesses:

- **Live membership** = `group.CompetitorRefs` where the competitor exists in
  `Competitors` and `WithdrawnAt is null` — the same definition
  `RecordingCore` uses for Expected (`TaskRoundRecording.cs:202-204`); the
  decide function re-derives it from the fold, never trusts the caller.
- Success emits `GroupSpotsAssigned` with the spots **as given** (no
  reordering, no normalization) — the fold stores exactly what was commanded;
  the read view sorts.
- **No acceptance or task-round-state gate beyond annulment** — D3: spots are
  operational configuration, assignable from draw time onward, re-assignable
  while the round lives. Nothing else consumes or requires them (NFR-4).
- Doc comment cites decisions D1–D4, this story's path, and the rules-check
  refs (F5J `5.5.11.7 d/f`, NZ.2.4.6, re-flight priority 1).

### WI-3 — Command, handler, DI, route (Application + Api)

**`src/Soarscore.Application/Commands/Competitions/AssignGroupSpots.cs`** —
the `AppendReflightGroup` template (`ReflightGroups.cs:20-76`): the command
returns the `GroupId` it named (the caller already knows it, but the shape
keeps the competition-command family uniform; `ICommand<GroupId>`), and the
handler is the plain `CompetitionLoader.LoadAsync → decide → AppendAsync` at
`ExpectedVersion.Exact(version)` walk with **no port beyond
`IEventStore`/`IClock`** (finding 9):

```csharp
public sealed record AssignGroupSpots(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    GroupId GroupRef,
    IReadOnlyList<GroupSpot> Spots) : ICommand<GroupId>;
```

- DI line in `src/Soarscore.Api/Composition.cs` beside `:92-100`.
- Route beside `Commands.cs:37`:
  `app.MapCommand<AssignGroupSpots, GroupId>("/assign-group-spots");`
- Bump `HandlerRegistrationTests.cs:74` to
  `HaveCountGreaterThanOrEqualTo(28)` and correct its count comment if it
  names a now-stale total (finding 6).

### WI-4 — Event type registry (Infrastructure)

`src/Soarscore.Infrastructure/SoarscoreEventTypes.cs` — one pair appended in
the competition block after `DrawRejected`, with a comment citing this story:

```csharp
(typeof(GroupSpotsAssigned), "groupSpotsAssigned"),
```

One list, both backends — no `MartenConfig`/`FisherConfig` edit; a missing
line fails at runtime on both stores (LADR-0001 §4.8), which WI-7 is the net
for. Update the block's running comment (the `RulesAmended`-is-the-only-
registered-nothing-sibling note stays true; the new line's citation joins
the commentary).

### WI-5 — Operational read view (Application)

`src/Soarscore.Application/Queries/Scoring/TaskRoundRecording.cs`:

- `GroupRecordingView` gains
  `ImmutableArray<GroupSpotView> Spots` where
  `GroupSpotView(int Spot, CompetitorId CompetitorRef)` — the assignment
  **as recorded, ordered by spot**, including any entry whose competitor has
  since withdrawn (a vacant spot is the consumer's derivation, joining
  against `ExpectedCompetitorRefs`; the view states facts, never verdicts —
  the file's header law, `:1-16`).
- `RecordingCore.ComputeGroupViews` passes `group.Spots` through
  spot-ordered. A group with no assignment yields an empty array — the
  unassigned fact, not an error and not a default (D2).
- `GET /competition` needs nothing (finding 3). `ScoreTaskRound` /
  `ScoreCompetition` need nothing — spots never touch scoring arithmetic;
  a spot-coupled scorer view would be a consuming-app composition (NFR-3).

### WI-6 — Unit-level tests (Domain.Tests + Application.Tests)

**New `GroupSpotsDecideTests.cs`** (Domain.Tests), one case per defect code
plus the happy paths:

- assign happy path → folds `Group.Spots` to exactly the commanded set;
- each defect code, one case, asserted by stable code;
- re-assignment replaces completely (assign A→1,B→2; then B→2,C→1; fold
  shows exactly the second list — D3's last-write-wins, asserted on the fold);
- withdrawal *after* assignment leaves `Spots` intact (vacancy is read-side);
- withdrawal *before* assignment → `assignSpots.competitorNotInGroup` for
  that competitor;
- **reject-draw lifecycle**: draw → assign → reject → phase gone, `Spots`
  gone with it → redraw succeeds → new groups read unassigned (D3's
  dies-with-the-draw claim asserted concretely);
- appended reflight group accepts an assignment like any drawn group
  (rules-check: re-flight priority 1's "additional launch spots").

**Handler tests** (Application.Tests, `Commands/Competitions/`), on the
`AcceptDrawHandlerTests` template: happy path appends the event and returns
the GroupId; decide failure passes the stable code through; no cross-aggregate
port is involved.

**Serialization:** one round-trip case in
`tests/Soarscore.Application.Tests/CompetitionEventJsonTests.cs`
(`GroupSpotsAssigned`, `$kind` discriminator asserted, `DrawAccepted`-style
`:317-327`) plus one asserting a `PhaseDrawn` payload *without* spots
deserializes with `Spots = []` (finding 5's compatibility claim, pinned).

**Property-based (CsCheck), invariant named up front per CLAUDE.md:**

> **P1 — a spot assignment is an explicit bijection over the live group,
> replaced whole, and dies with the draw.** For any generated competition
> (field, drawn phase, withdrawals) and any generated sequence of
> `AssignGroupSpots` attempts driven through the real decide functions and
> folded via `Apply`: (a) after every successful attempt, `Group.Spots` is
> exactly a bijection between the group's live members (drawn ∧ ¬withdrawn)
> and a set of distinct positive integers; (b) every defective attempt —
> unknown/withdrawn competitor, repeated competitor, repeated spot, spot < 1,
> missing live member, empty list, unknown coordinate, annulled round — fails
> with its stable code and leaves the fold unchanged; (c) a second success
> replaces the first in its entirety; (d) `DrawRejected` removes the phase
> and every assignment on it, and the redraw's groups start unassigned; (e)
> `RecordingCore` projects the assignment verbatim, spot-ordered. Small
> reference model tracks {drawn, withdrawn, assigned?} in lockstep — the
> `CompetitionFieldPropertyTests` shape, in its own file citing P1.
> Mutation-check non-vacuity (the task-round-lifecycle WI-10 discipline):
> weakening (a)'s distinctness or (c)'s replacement must fail.

### WI-7 — Store-backed tests (Infrastructure.Tests)

One new class `GroupSpotsEventStoreTests.cs` written once against
`IStoreFixture` — runs on Postgres/Testcontainers (`Trait("Category",
"Storage")`) and Fisher/SQLite automatically. Full cycles through the
dispatcher:

1. create → register ×N → draw → accept → assign spots → `GET /competition`
   shows `Group.Spots` (the fold round-tripped a real store);
2. assign → re-assign → read shows only the second list;
3. draw → assign → reject → redraw → new groups unassigned, re-assign
   succeeds (the D3 lifecycle against a real store, where the
   `groupSpotsAssigned` registration line is the thing under test — LADR-0001
   §4.8);
4. withdraw a member after assignment → read shows the spot recorded and
   `ExpectedCompetitorRefs` without them.

### WI-8 — Acceptance tests (Acceptance.Tests)

New feature `Features/AssigningSpots.feature` + steps (the
`AcceptingTheDraw` pair as template; `SOARSCORE_TEST_STORE=postgres|sqlite`
both, per CLAUDE.md):

```gherkin
Scenario: A CD assigns field spots to a drawn group and the recording view shows them
Scenario: Re-assigning a group's spots replaces the previous assignment
Scenario: Rejecting a draw discards its spot assignments; the redraw starts unassigned
Scenario: A spot cannot be assigned to a withdrawn competitor
Scenario: Score capture works on a group with no spots assigned
```

The last scenario is the NFR-4 guard: capture must not gate on (or be
changed by) spot assignment — it exercises the existing capture path against
an unassigned group and must pass with no new code. The Givens reuse the
shared draw steps (already ending in accept, finding 8).

### WI-9 — Board reconciliation

Move the story to `completed/` (`git mv`, status header in the same commit).
`tech-debt.md` and `deferred-decisions.md`: verify both before editing
(expected: nothing to tick; no new deferral — the out-of-scope list below is
scope statement, not settled-missing-thing). D6's `aggregate-roots.md`
sentence lands in WI-1's commit with its fresh approval.

---

## Execution plan — how an agent with sub-agents runs this

**Four sequential stages, checkpointed compiling and green.** The work is one
deep vertical slice — the read views (WI-5) consume the fold's new field
(WI-1), the handler tests (WI-6) need the command (WI-3), and the acceptance
suite needs everything — so concurrency buys little and risks hand-off
drift. Split stages across sub-agents by passing the tree forward, in this
order; one agent may run several stages.

**Stage 1 — Domain + approved docs** (WI-1 including the glossary/diagram
wording, WI-2, WI-6's decide tests/property test/JSON cases). Ask the D6
question (`aggregate-roots.md` sentence) and land the approved wording in
this stage. Checkpoint: `dotnet build Soarscore.sln`; `dotnet test
tests/Soarscore.Domain.Tests tests/Soarscore.Application.Tests` green
(Application.Tests passes here because its handler tests are staged into
stage 2's sweep).

**Stage 2 — Wiring** (WI-3, WI-4, WI-5, WI-6's handler tests).
Checkpoint: build green; `dotnet test tests/Soarscore.Architecture.Tests`
green (floor bump included); API boots.

**Stage 3 — Store-backed proof** (WI-7).
Checkpoint: `dotnet test tests/Soarscore.Infrastructure.Tests` — SQLite leg
always; Postgres leg wherever Docker exists. An unregistered event type
fails here loudly.

**Stage 4 — Acceptance + close-out** (WI-8, then WI-9).
Checkpoint: `SOARSCORE_TEST_STORE=postgres dotnet test
tests/Soarscore.Acceptance.Tests` and `SOARSCORE_TEST_STORE=sqlite dotnet
test tests/Soarscore.Acceptance.Tests`, both green; then the board move.
Known flake: the solution-wide Marten migration race (`tech-debt.md`) — if
a red first-scenario names schema migration, re-run the project alone before
diagnosing.

**Full-suite finish line:** `dotnet test Soarscore.sln` once for the record
(accepting the known migration-flake caveat), then the two store-tagged
acceptance runs above.

**Story invariant for sign-off:** every new decide test and the P1 property
test are green, including the mutation checks; the reject→redraw→re-assign
scenario is green on both stores; the no-spots capture scenario is green
unchanged; **no scoring test moved** — spots never touch scoring arithmetic,
normalisation, ranking or capture gating (NFR-4); no class-definition file
changed (the rules check found no per-class variation, so none may appear).

## Out of scope (deferrals restated, untouched)

- **Venue layout and spot inventory** — how many lanes exist and where; the
  consuming system's data (NFR-3).
- **Group-level field area** (which part of the field a group occupies) — a
  different fact from per-competitor spots; not modelled.
- **Team-aware spot rules** — F3J's no-team-member-adjacent-spot and any
  future team-coordination return to the teams thread
  (`teams-mvp.md` explicitly composes with this story, not duplicates it).
- **Separate launch vs landing spot facts** — additive extension if a venue
  ever needs them (D1).
- Flyoff-phase draws; F3B multi-task rounds — unchanged deferrals
  (`deferred-decisions.md` §Draw).
- GliderScore lane-allocation parity — deliberately not reproduced
  (team-derived coupling); fixture replay DTOs are untouched by this story.
