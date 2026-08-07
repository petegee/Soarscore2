# Plan — The draw: `DrawPhase`

**Status:** Proposed · **Date:** 2026-08-06

Work items are numbered `WI-n`, scoped to *this* plan document (see
`command-side-steel-thread-plan.md`'s numbering note — WI numbers reset per plan).

## Context

`register-competitor-steel-thread-plan.md` ended with a field: competitors exist, and
`aggregate-roots.md`'s field-freeze note has a check written against it
(`Competition.cs`'s `ValidateFieldNotFrozen`) that nothing can yet trigger, because no
command produces `PhaseDrawn`. This plan is that command. It is the thread that plan named
explicitly: *"That thread is where the core architectural law does real work (group sizes,
rounds and phases must come from `ClassDefinition`'s `PhaseDefinition` / `RoundComposition` /
`GroupConstraint`, never from a branch on class)."*

**Fold already exists.** `Competition.Apply(PhaseDrawn @event)` (`Competition.cs`:326-337)
already builds a `Phase` from the event and appends it to `Phases`. What is missing is
everything upstream of that: the pairing algorithm, the decide function, the command, the
handler, the wiring, and the tests.

### A model gap this plan closes before WI-1, not silently

Checking the model against what a draw actually needs to do surfaced a real gap, not an
implementation detail: **`Group` (`Competition.cs`:189-194) carries no competitor list.**
`aggregate-roots.md`:324-328 states this deliberately — *"Group membership is not stored
here — it is the set of Entries whose `groupRef` points at a given Group"* — and
`Competition.cs`:184-187 repeats it with `// Do not add one back`. That reasoning holds for
*who flew* (reflights and fillers make "who ultimately counts in Group C" an Entry-derived
fact), but it cannot also mean *who was drawn*: the class diagram's own `Draw "1" --> "2..*"
Competitor : allocates` relationship has to live somewhere, `PhaseDrawn`'s payload reuses
these exact `Round`/`TaskRound`/`Group` types (so even the *event*, not just the fold, has
nowhere to carry an allocation today), and `GET /competition` would otherwise show group
*counts* with no way to say who is in group 3 — Entry, which could answer that later, is
gated behind this very thread.

**Resolution (confirmed with the user before writing WI-1):** `Group` gains
`CompetitorRefs: ImmutableArray<CompetitorId>` — who this draw put in the group. The
`aggregate-roots.md` note is narrowed to state what it always meant operationally: this list
is the *drawn* allocation, fixed at draw time; who a scoring pass actually counts for the
group (after reflights, fillers, annulments) remains an Entry-derived query and is still not
duplicated here. `Group`'s use in `ReflightGroupAppended` (`Competition.cs`:339-344,
`CompetitionEvents.cs`:77-82) picks up the same field for free — a reflight group's initial
membership needs exactly the same fact.

### WI-0 — Apply the model fix

- `Group` (`Competition.cs`:189-194): add `public required ImmutableArray<CompetitorId>
  CompetitorRefs { get; init; }`. Update its doc comment: drop `// Do not add one back` and
  state the drawn-vs-flown distinction above.
- `docs/aggregate-roots.md`:324-328 and `docs/soaring-domain-class-diagram.md` (`Group`
  class, `Draw ..> Group : produces initial`): update the callout to match — **this is a
  docs change and needs the user's sign-off before it lands**, per CLAUDE.md's house-keeping
  rules 3–4. Draft wording is above; do not apply it without asking again at implementation
  time, a fresh confirmation separate from this planning conversation's answer.
- No event shape changes needed beyond this — `PhaseDrawn`'s `ImmutableArray<Round>` already
  nests `Group`, so the new field flows through once `Group` carries it.

**Verify:** existing `CompetitionFoldTests` construction of a bare `Group` needs updating to
supply `CompetitorRefs`; no behavioural test yet, since nothing produces a populated one until
WI-1.

### Two invariants this thread is really about

1. **Fairness — "any two pilots meet as few times as possible."** This is the rule text
   itself (`00-general-rules.md`#1, "Group composition changes every round, arranged so that
   any two pilots meet as few times as possible"), not a software invention. It is the reason
   this thread carries a property test comparing the algorithm's output against a brute-force
   reference on small fields, the same reference-oracle shape `ParameterResolver` and
   `NormalisationEngine` already get tested against elsewhere in `Soarscore.Domain.Tests`.
2. **The core architectural law, under real pressure.** Unlike `RegisterCompetitor` (whose
   checks were aggregate-local and class-agnostic), a draw's shape — group size, round count
   ceiling, whether a round holds one task or several — comes entirely from
   `RoundComposition`, `ValidityRule` and `GroupConstraint` on the adopted `ClassDefinition`.
   **The test this thread must pass:** nothing in `Competition.DrawPhase` may say `F3J` or
   `F5K` anywhere. Where the current model cannot yet express a class's shape generically
   (multi-task rounds, catalogue task choice — see Out of scope), the code must say so with a
   named defect, not silently draw something wrong.

### Out of scope (deliberately)

- **Multi-task rounds** (`RoundComposition.Kind == FixedSequence` with `TasksPerRound > 1`,
  e.g. F3B's fixed A/B/C sequence). `GroupConstraint` lives on `TaskDefinition`, not
  `PhaseDefinition`, so a multi-task round can legitimately need a *different* group split
  per task within the same round — a real algorithmic step this thread does not build.
  Rejected with `drawPhase.unsupportedRoundComposition`, not silently drawn as if
  single-task.
- **Catalogue-choice rounds** (`RoundComposition.Kind == ChooseFromCatalogue`, F3K's A–N and
  F5K's A–E). Which task is flown in a given round is a per-round Contest Director choice the
  rules leave open — a decision this thread's `DrawPhase` command has no input for. Same
  rejection code.
- **Parameterised `MinPerGroup`** (F5K, F5L, NZ Class M — confirmed in `tools/Soarscore.SeedData/`:
  `NumberOrParam.Param("minPerGroup" | "groupSize")`). Resolving it needs a bound
  `ParameterBinding`, and nothing appends `ParameterBound` yet — the eleven-event fold
  already handles it, but no command produces it, the same "fold built, decide/command
  missing" gap this plan is itself closing for `PhaseDrawn`. Rejected with
  `drawPhase.parameterUnbound`, generically (the decide function does not know or care which
  class this is) — it just cannot resolve a `NumberOrParam.Ref` with no matching binding.
  Concretely, this is what makes F3J / F3K / F5J / F3F (all literal `MinPerGroup`) the classes
  this thread can run end-to-end today, and F5K / F5L / NZ Class M not yet — a fact about the
  corpus, not a branch in the code.
- **Flyoff-phase draws** (`PhaseOrdinal > 0`). Needs the promoted-competitor subset from a
  `Finalised` phase-scope event, which does not exist yet. `DrawPhase`'s field input this
  thread is unconditionally "every non-withdrawn `Competitor`" — there is no parameter for a
  narrower population. Do not read this thread's `Phases.Length`-based ordinal addressing as
  proof a flyoff draw is "nearly done" — the field-selection logic still needs to change.
- **Redrawing.** The glossary: *"A draw is produced... and can be accepted or rejected and
  redrawn."* This thread models only the first, unconditional draw — `Phases.IsEmpty` is the
  only precondition, and there is no `RejectDraw`/`RedrawPhase`. Own future thread. **Acceptance
  criteria for that thread, fixed here so the requirement isn't lost in the meantime:**
  `aggregate-roots.md`:330-333 and the glossary (`soaring-domain-glossary.md`:51) already state
  the field freezes on *acceptance*, not on draw — `draw → reject → draw → accept → freeze`.
  `Competition.cs`:539-541 already flags today's `ValidateFieldNotFrozen` (`!Phases.IsEmpty`) as
  a stand-in for this: *"'Accepted' currently means 'any phase drawn' because `Draw.Status`
  carries no defined value set — revisit this check once it does."* The next thread is where
  that revisit happens: `Draw.Status` gets a real vocabulary (`drawn` / `accepted` / `rejected`),
  `AcceptDraw` and `RejectDraw` commands are added, and `ValidateFieldNotFrozen` moves from
  `Phases.IsEmpty` to "the current phase's `Draw.Status == accepted`." *This* thread's WI-7
  `field.frozen` test is unaffected — it already tests the documented approximation, not final
  semantics, and stays correct under it.
- **Mid-round regroup floors** (F3J: move a pilot up if a group falls to ≤3, `F3J.13.1 c`;
  F5J: refill if ≤5 or ≤4 in small contests, `5.5.11.14.1`). These react to a live shortfall —
  a withdrawal or a reflight mid-contest — and land on `ReflightGroupAppended`, not on the
  upfront `PhaseDrawn`. They carry no `# ref` comment on anything this thread writes; a future
  implementer should not try to fold them into WI-1's group-size algorithm.
- **Team separation and frequency management.** Still out of MVP scope per
  `00-general-rules.md`'s scope note, carried over from the previous plan. This is the one
  place in the whole draw framework where that scope note actually removes a rule-mandated
  behaviour (`C.16.2.6`) rather than a merely-adjacent one — worth restating here because the
  draw is where it would otherwise bite.
- **Proof the pairing algorithm is globally optimal at full contest scale** (≤20 pilots,
  CLAUDE.md's ceiling). WI-2 checks it against a brute-force reference only where brute force
  is tractable. The rule text itself only asks for "as few as possible," not a proof.
- Anything Entry-shaped. Entry becomes addressable once `Group.CompetitorRefs` exists and a
  `GroupId` can be handed to something, but building Entry is not this thread.

### Governing documents

`docs/aggregate-roots.md` §3 (`Draw`, `Phase`, `Round`, `TaskRound`, `Group`, the field-freeze
note, and the model-gap note this plan adds), `docs/soaring-domain-class-diagram.md` §1 (same
types) and §2 (`RoundComposition`, `ValidityRule`, `GroupConstraint`, `PhaseDefinition`),
`docs/soaring-domain-glossary.md`'s `Draw`, `Round`, `Task-Round` and `Group` entries,
`docs/ladr/ladr-0001-event-store.md` §4.4 (concurrency) and §4.8 (`MapEventType`), and the
`fai-rules` skill's `references/rule-map.md` "The draw / group sizes" table.

**No new domain concepts.** `Draw`, `Phase`, `Round`, `TaskRound` and `Group` are all already
in the glossary, the class diagram, and the (already-folding) code; `Group.CompetitorRefs` is
a field on an existing concept, not a new one, the same way `Competitor` gained no new
vocabulary in the previous thread.

### What the rules do and do not say (checked, not assumed)

Searched `docs/rules/` via the `fai-rules` skill for the draw itself:

- **Rule-mandated, with refs:** an initial random draw before the contest (`C.16.2.6`); group
  composition changing every round to minimise repeat matchups
  (`00-general-rules.md`#1); per-class minimum group sizes — F3J 6, prefer 8–10
  (`F3J.6.1`), F3K 5 (`F3K.9.1`), F5J 6 (`5.5.11.8`), F3B per-task 5/3/8-or-all
  (`F3B.1.8 b`), F3F 10 (`F3F.1.7`); a "fewest groups, most competitors each" preference
  stated in the same clauses (F3K: "as few groups as possible"; F5J: "fewest groups per round
  with the most competitors").
- **Rule-silent, left to the Contest Director / software:** the anti-repeat algorithm itself —
  the rules state the *goal* ("as few times as possible"), never a method; any **maximum**
  group size — only minimums and a preference are stated, never a ceiling; how many rounds to
  schedule when `RoundComposition.MaxRounds` is unset (F5K states none at all). **Do not add a
  maximum-group-size check** on the strength of the "fewest groups" preference — that is a
  tie-break between otherwise-valid splits, not a stated bound, exactly the distinction the
  previous plan drew for minimum/maximum *field* size.
- **F5K, F5L and NZ Class M state no group minimum at all** (F12 in the seed data's own
  comments) — the class declares a no-default `Parameter` instead, which is exactly the
  `drawPhase.parameterUnbound` case above, not a gap in this plan's rule research.

---

## Phase A — Domain

### WI-1 — Pairing algorithm and `Competition.DrawPhase` decide function

**`PhaseDraw.BuildGroups`** — a new pure static function, `src/Soarscore.Domain/Competitions/PhaseDraw.cs`:

```csharp
public static ImmutableArray<ImmutableArray<CompetitorId>> BuildGroups(
    ImmutableArray<CompetitorId> field, int minPerGroup, int roundCount)
```

Returns, for each of `roundCount` rounds, a partition of `field` into groups:

- **Group count and sizes** — `groupCount = max(1, field.Length / minPerGroup)` (integer
  division: *as few groups as possible*, `F3K.9.1` / `5.5.11.8`). Sizes are `field.Length /
  groupCount` or one more, remainder spread one-per-group — no rule states groups must be
  exactly equal, only that the split should favour fewer, fuller groups. **No maximum size is
  applied** — see the rules note above.
- **Anti-repeat construction, per round** — greedy least-paired-first: track a running
  `pairCount[CompetitorId, CompetitorId]` map, initially zero. For each group in the round,
  seed it with the not-yet-placed competitor who has been placed in the fewest groups *this
  round so far* (there is only ever one placement per competitor per round, so this just picks
  in field order for the first group and matters for subsequent ones only as a tie-break
  hook), then repeatedly add the not-yet-placed competitor with the lowest sum of `pairCount`
  against everyone already in the group, ties broken by field order (determinism —
  no unseeded randomness, so a replay and a CsCheck shrink both reproduce). After the round,
  increment `pairCount` for every newly-co-grouped pair.
- **Deterministic**, given a stable `field` ordering. `GroupId` is minted inside this function
  (`GroupId.New()`), unlike `CompetitorId`/`CompetitionId` elsewhere in this codebase — nothing
  outside a single `DrawPhase` call needs to predict it yet (Entry, which would reference it,
  is not built), so WI-2's property tests compare the *grouped `CompetitorId` sets*, not
  `GroupId` values, for equality against the reference.

**`Competition.DrawPhase(int rounds, DateTimeOffset at) : Result<PhaseDrawn>`** — instance
method, same shape as `RegisterCompetitor`/`WithdrawCompetitor`. Checks, in order:

| Check | Code | Notes |
|---|---|---|
| `Phases.IsEmpty` | `drawPhase.alreadyDrawn` | Only the first, unconditional draw — see Out of scope |
| `rounds >= 1` | `drawPhase.roundsInvalid` | |
| `rounds <= RoundComposition.MaxRounds` when set | `drawPhase.roundsInvalid` | `MaxRounds` is nullable — unset means no ceiling (F21) |
| Exactly one `TaskDefinition` in the phase's catalogue, `Kind == FixedSequence`, `TasksPerRound == 1` | `drawPhase.unsupportedRoundComposition` | The structural shape check, not a class-name check — see the architectural-law note above |
| Eligible field (`Competitors.Where(c => c.WithdrawnAt is null)`) non-empty | `drawPhase.fieldEmpty` | |
| If the task has a `GroupConstraint`: resolved `MinPerGroup <=` eligible field size | `drawPhase.fieldTooSmall` | Absent `GroupConstraint` means no group-scoring at all (NZ N/P) — one whole-field group, this check does not apply |

`MinPerGroup` resolution: build a bindings dictionary from `Competition.ParameterBindings`
(`GroupBy(b => b.ParameterName).ToDictionary(g => g.Key, g => g.Last().BoundValue)` — latest
binding per name wins), then call `ParameterResolver.Resolve`. This is the first caller of
`ParameterResolver` outside `ScoringService` — confirms it was written as a general Domain
utility, not scoring-only, and needs no new port. An `UnresolvedParameterException` is caught
here and translated to `Result<PhaseDrawn>.Failure("drawPhase.parameterUnbound", …)` —
decide functions never throw for a client-input-shaped failure, `Person.Register`'s standing
convention.

Building the event: `PhaseOrdinal = Phases.Length` (always `0` this thread — the `alreadyDrawn`
check above guarantees it, but the expression itself does not hardcode `0`, so it need not
change if a later thread adds a second call site); `Type = phaseDefinition.Type`; `Draw = new
Draw { CreatedAt = at, Status = "drawn" }` — a stable literal, following the precedent
`create-competition-steel-thread-plan.md` set for `EvaluatorVersion`: `Draw.Status` still
carries no defined vocabulary (`Competition.cs`:230-234), and inventing one is not this
thread's job; `Rounds` built from `PhaseDraw.BuildGroups`'s output, one `Round { Ordinal;
TaskRounds = [ TaskRound { Ordinal = 1; State = Drawn; TaskRef = task.Code; Groups } ] }` per
round, `Groups` from each round's group partition via `Group { Id = GroupId.New(); Ordinal;
CompetitorRefs }`.

**Verify:** `PhaseDrawnDecideTests` in `Soarscore.Domain.Tests` — happy path against 12
competitors / `minPerGroup 6` / 3 rounds → 3 rounds × 2 groups of 6, every competitor placed
exactly once per round; already-drawn → `alreadyDrawn`; zero/negative `rounds` →
`roundsInvalid`; `rounds` over `MaxRounds` → `roundsInvalid`; a multi-task-per-round fixture
(F3B-shaped) → `unsupportedRoundComposition`; empty field → `fieldEmpty`; field smaller than
`MinPerGroup` → `fieldTooSmall`; an unbound `Param("minPerGroup")` fixture (F5K-shaped) →
`parameterUnbound`; withdrawn competitors excluded from every group; a task with no
`GroupConstraint` (NZ N/P-shaped) → one group holding the whole field, every round.

### WI-2 — Property test: pairing fairness (invariant 1)

New `PhaseDrawPropertyTests` in `tests/Soarscore.Domain.Tests`, CsCheck, in the model-based
style `CompetitionFieldPropertyTests.cs` (register-competitor thread) established.

Generate field size 4..16, `minPerGroup` 2..(field/2), `roundCount` 1..5. Two properties:

1. **Structural — every generated input.** Every competitor appears in exactly one group in
   every round; every group's size is `minPerGroup` or `minPerGroup + 1`, never smaller or
   larger; `Round`/`TaskRound`/`Group` ordinals are sequential from 1.
2. **Fairness — small inputs only (field 4..9, `roundCount` 1..4, kept small enough to
   brute-force).** `BuildGroups`'s maximum pairwise co-occurrence count across the whole draw
   equals the true minimum found by an exhaustive search over all valid partitions-per-round
   for the same inputs — a reference-oracle comparison, the same shape `ParameterResolver` and
   `NormalisationEngine` are already tested against elsewhere in this project. State in a
   header comment why this only runs at small `N` (the partition search space is combinatorial)
   and that the greedy is *not* claimed optimal at full contest scale (≤20 pilots) — only
   checked for correctness where "optimal" is itself checkable, mirroring the rule text's own
   "as few times as possible," not "provably minimal."

**Verify:** the tests are the deliverable.

---

## Phase B — Application

### WI-3 — `DrawPhase` command and handler

```csharp
public sealed record DrawPhase(CompetitionId CompetitionId, int Rounds) : ICommand<CompetitionId>;
```

Returns `CompetitionId`, echoing the input — a `Phase` has no synthetic id of its own, only
its ordinal (always `0` this thread), so there is nothing new to mint or report back, unlike
`RegisterCompetitor`'s `CompetitorId`.

`DrawPhaseHandler` is the plain `RenamePerson`/`WithdrawCompetitorHandler` template — load,
decide, append, no cross-aggregate read: the class definition is already sitting in
`AdoptedRules`, copied in at `CreateCompetition`.

1. `CompetitionLoader.LoadAsync` → `(competition, version)`.
2. `competition.DrawPhase(command.Rounds, clock.UtcNow)`.
3. Append with `ExpectedVersion.Exact(version)` — the arbiter if two organisers draw
   concurrently; the loser's retry re-reads `Phases` non-empty and fails cleanly with
   `drawPhase.alreadyDrawn`, never a corrupted schedule. **Do not** add a retry loop — no
   other handler in this codebase has one.

**Verify:** `DrawPhaseHandlerTests` against `FakeEventStore` — success appends exactly one
event at the right version; unknown competition → `competition.notFound`; every domain
failure code surfaces unchanged through the handler; a stale version →
`eventStore.concurrencyConflict`.

### WI-4 — Property test: composition at the handler level (invariant 1, end to end)

Handler-level companion to WI-2, `tests/Soarscore.Application.Tests/Competitions`, in
`RegisterCompetitorPropertyTests.cs`'s shape. Seed a `FakeEventStore` with a real
`CompetitionCreated` adopting a literal-`MinPerGroup` seed class (F3J or F3K — the "checked,
not assumed" note above found these need no `ParameterBound` first) and a generated number
(1..20, CLAUDE.md's field ceiling) of `RegisterCompetitor` appends, then a single `DrawPhase`
with a generated round count. Assert:

- a field at or above the class's `MinPerGroup` always succeeds, and every competitor is
  grouped in every round;
- a field below it always fails with `fieldTooSmall`, never any other code;
- folding the appended `PhaseDrawn` a second time (idempotent replay) reproduces
  `Competition.Phases[0]` exactly — LADR-0001 §4.10's usual check, done here because it is
  cheap at this level rather than deferred entirely to WI-7.

State in the test's header comment which classes this exercises and why (literal
`MinPerGroup`), so the next reader does not "fix" it into asserting F5K/F5L/NZ-M succeed,
which they cannot until `BindParameter` exists.

**Verify:** the test is the deliverable.

### WI-5 — Marten wiring and event JSON

- `MartenConfig.cs`: add `opts.Events.MapEventType<PhaseDrawn>("phaseDrawn")`, and strike it
  from the "not yet registered" list at `MartenConfig.cs`:41-44 (eight subtypes remain named
  there after this thread, not nine).
- `CompetitionEventJsonTests`: round-trip `PhaseDrawn` — the first event in the log carrying a
  genuinely nested structure (`Round` → `TaskRound` → `Group`, three levels, `Group` now
  carrying `CompetitorRefs`), so this is also the first real exercise of the JSON shape
  `CompetitionEvents.cs` declared for `Draw`/`Round`/`TaskRound`/`Group` but never tested.
  Assert the shape rather than inventing a flatter one.

**Verify:** the JSON tests, plus WI-7's store-backed round-trip.

---

## Phase C — Api and verification

### WI-6 — Api endpoint

`POST /draw-phase`, through the existing `MapCommand` helper only. No new query — `GET
/competition?id=…` already returns the folded `Competition`, `Phases` included.

### WI-6a — Pairwise co-occurrence view

So the organiser can see *why* a draw's pairings look the way they do — and judge whether to
accept it, once WI-6's "Redrawing" acceptance criteria above are built — expose the pairwise
meeting counts `PhaseDraw.BuildGroups` already tracks internally while constructing groups.
This is a pure derivation over data the fold already has (`Rounds` → `TaskRound` → `Group` →
`CompetitorRefs`), not new domain state — no event shape change, no change to `BuildGroups`'s
write path.

`PairwiseCoOccurrence.Compute(ImmutableArray<Round> rounds) : ImmutableDictionary<(CompetitorId, CompetitorId), int>`
— for every group in every round, increments a count for each unordered competitor pair
co-located in it. Pure, class-agnostic (counts pairs regardless of what class the competition
runs), cheap at CLAUDE.md's ≤20 pilots/≤8 rounds ceiling. Lives in `Soarscore.Application`
alongside `PeopleProjection` — a read-model derivation over folded state, not a decide
function, so it does not belong beside `PhaseDraw.BuildGroups` in Domain despite the shared
shape.

**Api:** no new query, extending WI-6's stance — the `GET /competition?id=…` response gains a
`pairwiseCoOccurrence` list (`{competitorA, competitorB, count}`) alongside `phases`, computed
on read from the folded `Competition`, not stored or denormalised.

**Verify:** a unit test against a hand-built `Rounds` fixture with known counts (e.g. 3 rounds
× 2 groups of 3 → predictable per-pair totals); cross-checked against WI-2's brute-force
fairness oracle at the same small-`N` cases already generated there, so this does not need its
own reference-oracle infrastructure.

### WI-7 — Store-backed tests

`tests/Soarscore.Infrastructure.Tests`, Testcontainers, `Trait("Category", "Storage")`:

1. Register 12 competitors against an F3J-adopting competition, draw 3 rounds; read the stream
   back; the field folds to 3 rounds × 2 groups of 6, every competitor placed exactly once per
   round, no duplicates.
2. Drawing a second time against the same stream is rejected with `drawPhase.alreadyDrawn`
   against real PostgreSQL.
3. **Registering a competitor after the draw is now rejected with `competition.field.frozen`**
   — the first real exercise of `RegisterCompetitor`'s `ValidateFieldNotFrozen` check, written
   "unreachable this thread" in the previous plan and reachable for the first time here.
   Withdrawing one, by contrast, still succeeds — the asymmetry that plan documented.
4. `competitions` is dropped and fully replayed with `PhaseDrawn` in the log and lands
   identical (LADR-0001 §4.10) — the direct test of `CompetitionProjection`'s pass-through arm
   for `PhaseDrawn`, now with a real event to pass through.

### WI-8 — End-to-end verification

Against a running API and PostgreSQL, in order: publish an F3J class definition → create a
competition → `POST /register-competitor` six or more times → `POST /draw-phase` with e.g. 2
rounds → `GET /competition?id=…` shows `phases[0].rounds` with the expected group split and
membership, and `pairwiseCoOccurrence` with the matching per-pair counts → drawing again
returns `ProblemDetails` `drawPhase.alreadyDrawn` → registering a
further competitor now returns `competition.field.frozen`, closing the loop the previous
thread's plan left open → against a *fresh* competition with no registrations, `POST
/draw-phase` returns `drawPhase.fieldEmpty`.

---

## Dependency order

```
WI-0 ── first (model fix; needs the docs sign-off noted above before landing)
WI-1 ── needs WI-0
WI-2 ── needs WI-1
WI-3 ── needs WI-1
WI-4 ─┐ needs WI-3
WI-5 ─┘ (independent of each other, parallelisable)
WI-6 ── needs WI-3, WI-5
WI-6a ─ needs WI-1 (independent of WI-6 itself, but shares its Api surface)
WI-7 ── needs WI-5
WI-8 last
```

## What this unlocks

Rounds and groups exist for the first time, with real membership. `ReflightGroupAppended`,
`TaskRoundCompleted` and `TaskRoundAnnulled` — whose folds already exist in `Competition.cs`,
the same "fold built, decide/command missing" gap this plan closed for `PhaseDrawn` — become
reachable, since they all navigate `Phase`/`Round`/`TaskRound` by ordinal, which now exist.
More importantly, **a `GroupId` with real `CompetitorRefs` is the first input Entry — the
live-capture aggregate `aggregate-roots.md` §4 describes — has ever had to reference.**
Building Entry is not this thread, but nothing was blocking it before this thread ran; now
only Entry's own design is.

Deliberately still not unlocked: multi-task rounds (F3B), catalogue-choice rounds (F3K's
per-round task pick, F5K), flyoff-phase draws (needs `Finalised`/promoted-competitor data),
redrawing, and any class whose `MinPerGroup` is parameterised (F5K, F5L, NZ Class M) until
`BindParameter` exists to resolve it.
