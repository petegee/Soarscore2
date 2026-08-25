# Story — Prescribed-draw import capability

**Status:** In progress · **Raised:** 2026-08-25 · **Fleshed out:** 2026-08-25 · **Implementation started:** 2026-08-26

## What

A way to set a phase's groups explicitly — which pilots in which group, and fly order —
instead of only drawing fresh. Needed so an imported GliderScore competition can
reproduce the *realised* draw recorded in its `Scores` rows (`RoundNo → GroupNo → SeqNo`;
fixture pipeline, `gliderscore-golden-fixture-pipeline.md` *Fixture schema v1* table,
`scores-raw.json` row: *"The realised draw derives from these rows… there is no separate
draw section — one derivation, no drift"*).

The three open questions from the stub are settled below (**D1–D3**); two further
choices made during planning are flagged for veto (**P1**, **G1**).

## Why it matters

Normalisation is winner-per-group (`NormalisationEngine` scales by group winner), so
group membership changes normalised scores. Re-drawing an imported comp produces
different groups and hence different numbers than the oracle — the comparison would be
noise. Identical group composition is a precondition for meaningful score comparison.

This is also the last unmet precondition of
`kanban/backlog/gliderscore-replay-and-compare-harness.md` (its replay step 1.3 names
this story; its *Before starting* lists "prescribed draw available"). Arithmetic is
resolved and fixture #1 is committed, so this thread unblocks the harness outright.

## Before starting

- Read `kanban/deferred-decisions.md`'s Draw section — done during planning
  (2026-08-25). Nothing there covers prescription. Two entries constrain the scope and
  must stay intact: **flyoff-phase draws** (the field is unconditionally every
  non-withdrawn competitor; prescription inherits that, so it targets the preliminary /
  live-phase slot exactly as `DrawPhase` does) and **multi-task rounds** (structurally
  rejected; prescribed imports of F3B-style comps stay skip-listed with them).
- **Two asks to the user before code starts**, same discipline as the redraw story's
  F1/F2 — fresh confirmation at implementation time, separate from this planning pass:
  - **G1 — glossary reading (house rule: no `/docs` change without approval).** The
    glossary defines the Draw as *"pseudo-random, but with a purpose…"*
    (`docs/soaring-domain-glossary.md:51`). This story's position (**D3**) is that
    prescription introduces **no new concept**: Draw, Round, TaskRound and Group are
    reused verbatim; prescribing is another way to *produce* the existing Draw
    structure, and the glossary sentence describes how draws are typically produced,
    not a validity condition. Under that reading **no glossary edit is needed**. If you
    read "pseudo-random" as definitional instead, the fix is a one-clause amendment —
    proposed wording: *"It is usually pseudo-random …; it may also be set explicitly
    (prescribed), for example when reproducing an imported competition's realised
    draw."* **Ask, land only the approved option, never edit unprompted.**
  - **P1 — provenance field on the existing event, flagged for veto.** Decision D1
    reuses `PhaseDrawn`, which means the folded state cannot distinguish a prescribed
    draw from a generated one. Recommendation: append one optional audit-only property
    `string? PrescribedBy = null` to the `PhaseDrawn` record (who prescribed, the
    `BindParameter.By` precedent). Additive: all twenty-plus positional `new PhaseDrawn(`
    test call sites still compile; old stored payloads deserialise with the property
    null. Strike this if you want zero event-contract change — the cost is that the log
    records the schedule but not that it was prescribed, which sits oddly with the
    trust model (*"an immutable event log of all mutations provides auditability"*).
- Prerequisite for the replay-and-compare harness; independent of
  `grow-gliderscore-fixture-corpus.md`. No dependency on the fixture pipeline's
  remaining work.

---

# Plan

## Decisions settled during planning (2026-08-25)

1. **D1 — Separate `PrescribeDraw` command; it emits the same `PhaseDrawn` event.**
   Considered and rejected:
   - *Extending `DrawPhase` with an optional groups argument* — one command wearing two
     hats, a validation flow that branches on payload presence, and every existing
     `/draw-phase` caller carrying a parameter only importers use.
   - *A post-draw "set-groups-on-phase" mutation event* — a second write to the
     schedule breaks the atomic-draw property (`aggregate-roots.md` §3: created up
     front, lightly mutated), adds a drawn-but-unsettled intermediate state between
     draw and accept, and buys nothing: prescription happens *instead of* drawing,
     before acceptance, when no entries can exist (the D4 gate of
     `draw-acceptance-redraw.md` requires an accepted draw first — so a
     prescription-after-draw window has no legitimate client).
   Emitting the identical event is why this stays cheap: **no new event type** — no
   `[JsonDerivedType]` entry, no fold arm, no `SoarscoreEventTypes.cs` registry pair,
   no `aggregate-roots.md` staleness — and every downstream behaviour (accept, reject,
   remove-and-redraw, D2's `Phases.Length` addressing) works unchanged because the
   folded state is bit-for-bit the shape it already handles.
2. **D2 — Validation: a prescribed schedule must satisfy exactly the invariants a
   generated one guarantees.** Nothing looser (it would break downstream consumers'
   assumptions) and nothing stricter (fairness is NOT re-adjudicated — the realised
   draw is historical fact; the pairing-minimisation objective is a property of our
   generator, not of the data model):
   - each round **partitions exactly the eligible field** — every non-withdrawn
     competitor appears in exactly one group; this is precisely invariant P1(d) of
     `draw-acceptance-redraw.md` WI-6;
   - grouped ids must be **eligible competitors** (registered, not withdrawn) — a
     generated draw filters the field, so a prescription naming anyone else is a
     malformed input, not a variation;
   - **≥ 2 members per group** (`Group.CompetitorRefs` doc: "2..*",
     `Competition.cs:249`);
   - **class minimum honoured**: smallest group in a round ≥ the round's resolved
     `MinPerGroup` — the same resolved value `DrawPhase` computes, so
     `parameterUnbound` / `fieldTooSmall` arise identically. This is what makes NZ
     N/P (no `GroupConstraint` ⇒ `minPerGroup := field.Length`,
     `Competition.cs:865-869`) come out right with no special case: exactly one
     whole-field group per round. It is also the tripwire for genuine divergences —
     GS comps whose realised groups are smaller than the declared minimum fail loudly
     here and become intentional-divergence triage in the harness, not silent
     mis-normalisation;
   - **task resolution identical to `DrawPhase`**: FixedSequence phases take no task
     choice; catalogue phases name one task per round from the catalogue,
     distinct when required; `TasksPerRound != 1` stays structurally rejected.
   Deliberately **not** validated: group count against the fewest-groups formula
   (GS decides; reproducing it is the point), cross-round pairing minimisation, and
   anything resembling fairness.
3. **D3 — Vocabulary: reuse, no new concepts.** The command is a verb over the
   existing Draw concept (`PrescribeDraw` beside `DrawPhase`/`AcceptDraw`); the event,
   aggregate shape, glossary nouns and diagram are untouched. See **G1** for the one
   glossary sentence that could be read otherwise — surface, don't edit.
4. **Fly order is preserved as list order, nothing more.** GliderScore's `SeqNo`
   (starting order within the group) maps onto the existing ordering of
   `Group.CompetitorRefs` — the importer supplies members in `SeqNo` order and the
   model stores them as given. No new field, no new concept; today nothing downstream
   reads member order (normalisation is order-blind; launch labels are Entry-side,
   `out-of-order-flight-entry.md`), so this is information preservation at zero cost,
   documented on the command, not modelled.
5. **Radio frequencies are not imported.** `Scores.DrawFreq` drops. Frequency
   management and team separation are standing deferrals
   (`C.16.2.6` scope note; `draw-acceptance-redraw.md` *Out of scope*); Soarscore has
   no frequency concept and this thread does not invent one.
6. **Reflights are out of v1.** GS rows with `ReFlightNo > 0` or
   `OriginalRoundNo ≠ RoundNo` are re-flights into separately drawn groups — the
   analogue here is `AppendReflightGroup`, not the base draw. Derivation for import
   filters those rows out; fixtures containing them stay skip-listed (harness scope
   guard) until prescribing reflight groups is wanted. Recorded as a deferral at
   close-out (WI-8).
7. **Lifecycle position:** `PrescribeDraw` requires an empty `Phases` (own code,
   `prescribeDraw.alreadyDrawn` — same condition as `drawPhase.alreadyDrawn`,
   distinct prefix so logs diagnose which command hit it). Reject→re-prescribe is
   therefore legal exactly as reject→redraw is (D2 removal semantics,
   `CompetitionEvents.cs:141-148`). No `phaseHasEntries`-style fact is needed: no
   live phase ⇒ no accepted draw ⇒ no entries can exist (structural, per the D5
   reasoning in `draw-acceptance-redraw.md`).

## Findings from reading the tree (verified 2026-08-25)

1. **No store change anywhere.** No new event type ⇒ no `[JsonDerivedType]`
   (`CompetitionEvents.cs:23-38`), no `SoarscoreEventTypes.cs` pair, no
   `MartenConfig`/`FisherConfig` edit. Marten's conventional discovery already matches
   the existing `Apply(PhaseDrawn)` overload (`Competition.cs:404-415`); the fold is
   untouched.
2. **`PhaseDrawn` gains only an appended optional parameter** (if P1 survives): every
   existing constructor call is positional ending in `At` — one production site
   (`Competition.cs:923`) and ~twenty test sites (grep `new PhaseDrawn\(`) — all
   compile unchanged with `string? PrescribedBy = null` last.
3. **The shared validation in `DrawPhase` extracts cleanly.**
   `Competition.DrawPhase` (`Competition.cs:743-931`) reads top-to-bottom as:
   guards (no live phase :749-753, rounds bounds :760-769, unsupported composition
   :775-802) → task resolution (:784-839) → eligible field (:841-849) → per-round
   `minPerGroup` resolution (:851-895) → `PhaseDraw.BuildGroups` (:897) → event build
   (:899-930). Everything except the last two steps is common to both commands.
   Extract steps 1–4 into one private helper returning
   `Result<ResolvedSchedule>` where `ResolvedSchedule` bundles
   `(PhaseDefinition, ImmutableArray<string> ResolvedTaskRefs, ImmutableArray<CompetitorId>
   Field, ImmutableArray<int> MinPerGroupByRound)`. The helper takes the defect-code
   prefix as a parameter so `DrawPhase` keeps emitting `drawPhase.*` and the new path
   emits `prescribeDraw.*` for identical conditions — specific, diagnosable codes on
   both surfaces, one body.
4. **GroupId minting stays at event build.** `PhaseDraw.cs`'s header
   (:4-6) says `Competition.DrawPhase` mints `GroupId`s; after this story two methods
   do — update that comment to say "Competition.cs's draw decide functions".
5. **Routing grows by one POST** (`/prescribe-draw`, beside
   `Commands.cs:27-29`); route-shape test unaffected (GET/POST only). DI gains one
   line beside `Composition.cs:90-92`. Handler-registration floor
   (`HandlerRegistrationTests.cs:74`, currently `≥26`) rises to `≥27` and its count
   comment gains one.
6. **Nothing consumes group member order today** (grep across scoring, projection,
   co-occurrence) — storing `SeqNo` order is safe; assert it survives the round trip
   in one test so the promise is load-bearing if a future report wants it.
7. **Test seams that already exist**: corpus classes with literal `MinPerGroup`
   (`tools/Soarscore.SeedData/Corpus.cs`, e.g. F5J `MinPerGroup = 6`) for property
   generation; `SamplePhaseDrawnEvent`
   (`tests/Soarscore.Application.Tests/CompetitionEventJsonTests.cs:109-145`) extends
   for the provenance round-trip; `DrawAcceptanceDecideTests` /
   `DrawAcceptancePropertyTests` / `DrawAcceptanceEventStoreTests` /
   `AcceptingTheDraw.feature` are the lifecycle precedents to mirror file-by-file.
8. **`ScoringService.cs:136`'s "Only phase 0 is ever drawn today" note stays true** —
   prescription addresses the same single-slot lifecycle; no scoring-path edit.

## Work items

Each WI is small enough for one agent session and lands compiling. Tests green at each
stage boundary per the execution plan.

### WI-1 — Domain: shared validation + `PrescribeDraw` decide (`Competition.cs`, `CompetitionEvents.cs`, `PhaseDraw.cs`)

1. Extract the common schedule validation from `DrawPhase` into the private
   `ResolvedSchedule` helper (finding 3), preserving every message verbatim; prefix
   parameterised. `DrawPhase` keeps its signature and public behaviour — its existing
   tests are the regression net and must not move.
2. Append `string? PrescribedBy = null` to `PhaseDrawn` (pending P1 veto) and extend
   its doc comment: *"Set when the schedule was prescribed explicitly (imported
   realised draw) rather than generated by the pairing algorithm; audit-only, never
   branched on."* `DrawPhase`'s construction site passes nothing.
3. New instance decide function `PrescribeDraw(IReadOnlyList<PrescribedRound> …)`
   after `DrawPhase`, early-return style (later checks need earlier values — the
   reason `DrawPhase` itself deviates from defect-chain, `Competition.cs:737-742`):

   | Code | Condition |
   |---|---|
   | `prescribeDraw.alreadyDrawn` | live phase exists |
   | `prescribeDraw.roundsInvalid` | fewer than 1 round, or beyond `MaxRounds` |
   | `prescribeDraw.unsupportedRoundComposition` | `TasksPerRound != 1` (shared) |
   | `prescribeDraw.taskSelectionNotPermitted` / `…Required` / `…CountMismatch` / `…NotDistinct` / `taskNotInCatalogue` | shared task-resolution rules (catalogue rounds must name their task per round) |
   | `prescribeDraw.parameterUnbound` / `prescribeDraw.fieldTooSmall` | shared minPerGroup resolution |
   | `prescribeDraw.competitorNotInField` | a grouped id is not an eligible (non-withdrawn) competitor |
   | `prescribeDraw.competitorRepeated` | an eligible competitor appears twice in one round |
   | `prescribeDraw.competitorMissing` | an eligible competitor appears in no group of some round |
   | `prescribeDraw.groupTooSmall` | a group has fewer than 2 members |
   | `prescribeDraw.groupBelowClassMinimum` | a group smaller than the round's resolved `MinPerGroup` |

   Happy path builds the same `PhaseDrawn` — `PhaseOrdinal = Phases.Length`,
   `Type` from the positional phase definition, `Draw { CreatedAt = at, Status =
   "drawn" }`, `GroupId.New()` minted per group, ordinals assigned by position
   (caller supplies membership only), members stored in supplied order (decision 4),
   `PrescribedBy` carried when P1 stands.
4. Update the `PhaseDraw.cs` header comment (finding 4) and `DrawPhase`'s
   alreadyDrawn-era comments only where they claim uniqueness of the draw path.

### WI-2 — Domain tests (`tests/Soarscore.Domain.Tests`)

**Example-based `PrescribeDrawDecideTests.cs`**, one case per code above plus happy
paths: happy fold equals a hand-built expected `PhaseDrawn` (deterministic decide —
no id minting surprises beyond asserting well-formedness); each validation failure;
reject → re-prescribe legal; prescribed draw accepts, opens entries, and a generated
and prescribed comp score identically when handed the same groups (ties the story to
its reason for existing — reuse a `NormalisationEngine` assertion, no new engine
code).

**Property-based (CsCheck), invariants named up front per CLAUDE.md:**

> **PD-P1 — generation/prescription self-consistency.** For any generated field and
> round count on which `PhaseDraw.BuildGroups` succeeds, feeding its output back
> through `PrescribeDraw` succeeds, and the two events fold to schedules equal up to
> `GroupId` minting. Invariant: *every draw the system can generate is a legal
> prescription.* Guards the validation set drifting stricter than what generation
> guarantees (the exact-once partition, the ≥2 floor and the minPerGroup floor are
> precisely what generation promises — nothing more may be required).
>
> **PD-P2 — partition enforcement.** For any valid prescription, mutating it —
> deleting a competitor from a group, duplicating one across groups of a round,
> swapping in an unregistered or withdrawn id, shrinking a group below 2 or below
> the resolved minimum — is rejected with the corresponding code from WI-1's table.
> Invariant: *nothing enters the log that violates the drawn-allocation invariants.*
> Mutation-check non-vacuity (task-round-lifecycle WI-10 discipline): weakening the
> exact-once check in the decide must make PD-P2 fail.

Generate fields within a literal-`MinPerGroup` corpus class (`Corpus.All[0]`
pattern) so resolved minima are known without binding parameters.

### WI-3 — Command, handler, JSON contract (Application)

`src/Soarscore.Application/Commands/Competitions/PrescribeDraw.cs`, on the
`DrawPhase.cs` template (including its documented `IReadOnlyList`-not-
`ImmutableArray` boundary convention, `DrawPhase.cs:13-22`):

```csharp
public sealed record PrescribedGroup(IReadOnlyList<CompetitorId> Competitors);

/// Members are listed in flying order (SeqNo for imported comps) — preserved as-is.
public sealed record PrescribedRound(string? TaskRef, IReadOnlyList<PrescribedGroup> Groups);

public sealed record PrescribeDraw(CompetitionId CompetitionId, IReadOnlyList<PrescribedRound> Rounds)
    : ICommand<CompetitionId>;
```

Note the deliberate absence of a separate `Rounds: int` — the round list is the
single source of truth (`PhaseDraw.cs:36-38`'s "two values that must agree"
anti-pattern, avoided). `TaskRef` null is legal only for FixedSequence phases —
the decide's shared validation arbitrates, exactly as `/draw-phase` does.

Handler: `IEventStore` + `IClock` only — no cross-aggregate read (AdoptedRules rides
in the stream; the no-entries precondition is structural, decision 7).
read→fold→decide→append at `ExpectedVersion.Exact(version)`, `DrawPhaseHandler`'s
concurrency comment applies verbatim (two concurrent prescribers: loser re-reads
non-empty `Phases`, fails cleanly).

**JSON contract tests** in `CompetitionEventJsonTests.cs`: extend
`SamplePhaseDrawnEvent` usage — (a) `PhaseDrawn` *with* `PrescribedBy` round-trips
byte-for-byte; (b) a legacy payload *without* the property deserialises to null
(backward compatibility is what makes P1 safe against both stores' persisted
history).

### WI-4 — Wiring (Api)

`app.MapCommand<PrescribeDraw, CompetitionId>("/prescribe-draw");` beside
`Commands.cs:27-29`; DI line beside `Composition.cs:90-92`; floor
`HandlerRegistrationTests.cs:74` → `HaveCountGreaterThanOrEqualTo(27)`, count
comment updated (finding 5). Route-shape test unaffected.

### WI-5 — Store-backed proof (Infrastructure.Tests)

New `PrescribeDrawEventStoreTests.cs`, written once against `IStoreFixture` — runs
on Postgres/Testcontainers (`Trait("Category", "Storage")`) and Fisher/SQLite
automatically. Through the dispatcher, not hand-appended events:

1. create → register ×N → **prescribe** → accept → open entry succeeds (entry opens
   against a prescribed GroupId — proves the whole downstream contract);
2. create → register → prescribe → reject (reason) → re-prescribe → accept — the
   D2 removal semantics exercised through a real store;
3. persisted `PrescribedBy` survives the round trip on both backends (finding 2's
   runtime half; WI-3's JSON test is the contract half);
4. prescription missing a competitor refused, nothing appended (stream length
   asserted).

### WI-6 — Acceptance feature (Acceptance.Tests)

New `Features/PrescribingADraw.feature` + steps, mirroring `AcceptingTheDraw`'s
shape; run against both stores (`SOARSCORE_TEST_STORE=postgres|sqlite`):

```gherkin
Scenario: A CD sets the groups explicitly and accepts the prescribed draw
Scenario: A prescription that leaves a registered pilot unplaced is refused
Scenario: A rejected prescription is replaced by a corrected one, then accepted
Scenario: A catalogue-choice phase is prescribed with a named task per round
```

The first asserts `GET /competition` shows the prescribed groups and accepted state;
the second is WI-1's table end-to-end through HTTP; the fourth covers the
`TaskRef`-carrying payload shape the ALES harness will actually send. Existing
features need **no mechanical edits** — their `/draw-phase` Givens are untouched,
which keeps them honest regression guards for the generated path.

### WI-7 — Harness hand-off note (this story only)

When done, `gliderscore-replay-and-compare-harness.md`'s blocker list
("prescribed draw available") is dischargeable — but do **not** edit that story
beyond striking the blocker if its text invites it; its own fleshing-out will map
GS `PilotNo → CompetitorId`, derive `(RoundNo → GroupNo → SeqNo)` from
`scores-raw.json` (filtering `ReFlightNo > 0` / `OriginalRoundNo ≠ RoundNo` rows,
decision 6), and order members by `SeqNo` (decision 4). Those mapping rules belong
to the importer, not to this command — the core system never hears the name
GliderScore.

### WI-8 — Board reconciliation

`git mv` to `completed/`, status header in the same commit. In
`deferred-decisions.md`, Draw section, add:

- **Prescribing reflight groups.** v1 prescribes base rounds only; GS re-flight
  rows (`ReFlightNo > 0`, `OriginalRoundNo ≠ RoundNo`) have no prescription path —
  fixtures exercising them stay skip-listed until wanted (decision 6).
- **Mid-comp-withdrawal reproduction.** An imported comp where someone withdrew
  mid-event flew earlier rounds alongside pilots our field-freeze model would have
  kept in every group; reproducing that faithfully needs either withdrawal-timing
  import or per-round eligibility, neither designed. Skip-listed at curation until
  a fixture demands it.

Tick nothing in `tech-debt.md` / `smaller-items.md` (verified 2026-08-25 — neither
lists draw items this touches).

## Execution plan — how an agent (or agents) runs this

**Sequential; one deep compile unit, so parallelism buys nothing** — same verdict
as `draw-acceptance-redraw.md`'s execution plan, for the same reasons. One
implementer, four staged checkpoints, each ending compiling with its layer's suites
green so a crash or park leaves clean ground:

1. **Stage 1 — Domain core + unit mirror** (WI-1, WI-2). Checkpoint:
   `dotnet build Soarscore.sln`; `dotnet test tests/Soarscore.Domain.Tests
   tests/Soarscore.Application.Tests` green (Application.Tests is untouched-green
   here — the appended optional parameter changes no existing call). Ask the G1
   question and the P1 veto now; land any approved glossary clause in this stage's
   commit.
2. **Stage 2 — Wiring** (WI-3, WI-4). Checkpoint: build green;
   `dotnet test tests/Soarscore.Architecture.Tests` green (floor bump included);
   API boots.
3. **Stage 3 — Store-backed proof** (WI-5). Checkpoint:
   `dotnet test tests/Soarscore.Infrastructure.Tests` — SQLite leg always; Postgres
   leg wherever Docker exists.
4. **Stage 4 — Acceptance + close-out** (WI-6, WI-7, WI-8). Checkpoint: both
   `SOARSCORE_TEST_STORE` runs of `tests/Soarscore.Acceptance.Tests` green; then
   the board moves. Known flake: the solution-wide Marten migration race
   (`tech-debt.md` last item) — re-run the project alone before diagnosing.

**Full-suite finish line:** `dotnet test Soarscore.sln` once for the record, then
the two store-tagged acceptance runs.

**Story invariant for sign-off:** `DrawPhase`'s public behaviour and every existing
test that exercises it are unchanged (the extraction is invisible); a prescribed
comp and a generated comp with identical groups produce identical scores; the log
distinguishes prescribed from generated (P1) or the omission is recorded as
user-accepted (veto); no new event type, no new glossary concept, no `/docs` edit
without an approval on record.

## Out of scope (deferrals restated, untouched)

Flyoff-phase draws (prescription included — it targets the same single slot);
multi-task rounds (F3B); frequency/team-aware drawing and import (`C.16.2.6`);
reflight-group prescription; mid-comp-withdrawal faithful reproduction; a
draw-history read surface (`deferred-decisions.md`, decided 2026-08-24); any UI for
hand-editing groups (a CD *could* misuse this command to hand-set a club night's
groups — noted as a possible future story, deliberately not designed here).
