# Plan — The CD's choices: `BindParameter`

**Status:** Complete — implemented and test-verified · **Date:** 2026-08-08

Work items are numbered `WI-n`, scoped to *this* plan document (see
`command-side-steel-thread-plan.md`'s numbering note — WI numbers reset per plan).

## Context

`phase-drawn-steel-thread-plan.md` delivered a draw that reads group sizing from the
adopted rulebook. It works for every class whose `GroupConstraint.minPerGroup` is a
literal. For the three classes whose rulebooks **leave group size to the contest
director**, the draw stops dead:

```
Competition.cs:613 → Result<PhaseDrawn>.Failure("drawPhase.parameterUnbound", …)
```

Nothing in the system can clear that failure, because no command produces
`ParameterBound`. **F5K, F5L and NZ Class M ALES 200 cannot be drawn at all today** —
three of the eleven definitions in the seed corpus (seven FAI classes, four NZ), and
the corpus exists precisely to prove the model can express them.

They are exactly the three whose `minPerGroup` resolves to a parameter carrying **no
default**. Every other definition uses a literal, including `nz-m-ndc`, and F3K's
`minPerGroup` is the literal 5 — F3K's block is a different one (see finding 1).

This is a repair to shipped behaviour, not a new capability. It is deliberately taken
before the Entry write path because it is small, it reuses a shape already executed
four times, and it closes a hole in a thread that is otherwise finished.

**Most of the work is already done.** Reused as-is, not rebuilt:

- The `ParameterBound` event (`CompetitionEvents.cs:105`) and its fold
  (`Competition.cs:368` — `ParameterBindings.Add(@event.Binding)`).
- The `ParameterBinding` record (`Competition.cs:116-125`) — `ParameterName`,
  `BoundValue`, `By`, `At`.
- `Parameter` on the adopted definition (`ClassDefinition.cs:17-41`) — `Name`, `Kind`,
  `Unit`, `DefaultValue`, `AllowedValues`, `BoundAt`.
- `ParameterResolver.Resolve` (`ParameterResolver.cs:45`) and the draw's
  binding-flattening code (`Competition.cs:602-605`), which already takes
  **last-write-wins** per parameter name.
- `CompetitionLoader`, the read-fold-decide-append handler template, `MapCommand`, and
  the whole Marten/Api adapter stack.
- `CompetitionProjection`'s pass-through default arm (`CompetitionProjection.cs:34`) —
  **the `competitions` read model needs no change this thread.**

**What is genuinely missing:** one decide function, one resolver change (finding 2), one
command with handler, one `MapEventType` registration, one endpoint, and the tests.

### What the rules do and do not say (checked, not assumed)

The whole thread rests on the rulebooks being *silent*, so the silence was verified
rather than inferred:

| Class | Min per group | Source |
|---|---|---|
| **F5K** | **not stated** | `5.5.10`; `references/rule-map.md:35` |
| **F5L** | **not stated**; fly-off group size **equals** preliminary group size | `5.5.12.4`; `rule-map.md:36` |
| **NZ Class M** | **not stated anywhere**, nor a round count outside the NDC format | `NZ.3.12`; `class-m-ales200.md:17-20` |

Two consequences worth stating:

1. Per the `fai-rules` discipline — *"a rule you cannot find is a question, not an
   inference… treat it as a Contest Director decision"* — a CD decision is exactly what
   `ParameterBound` exists to record. `class-m-ales200.md:17-20` already says so in as
   many words: *"Both are `no default` parameters in the class definition, bound at
   competition setup and recorded in the event log."* This thread makes that sentence
   true.
2. **F5L's one binding must drive both phases.** The rule fixes the fly-off group size
   *equal to* the preliminary group size, and the class definition honours it by
   referencing a single `groupSize` parameter from both slots. Binding it once binds
   both. This is a property to assert, not code to write — see WI-3.

The three blocked parameters are all `Number`, `boundAt: CompetitionSetup`, with **no
`defaultValue` and no `allowedValues`** (F5K `minPerGroup`; F5L and NZ-M `groupSize`).
So the minimal command — no defaulting, no allowed-value narrowing — is already enough
to unblock all three.

### Two model findings that shape the scope

Both were found while designing this thread. Both have now been decided (2026-08-08);
the reasoning is recorded because the alternatives were close.

#### Finding 1 — `ParameterBindingPoint.PerRound` is unrepresentable · **deferred**

`ParameterBinding` carries no round or phase ordinal (`Competition.cs:116-125`), so a
per-round binding cannot be expressed at all. Six parameters in the corpus are
`PerRound`, all F3K's: `workingTime.A/B/E/L` and `maxFlight.B/L`.

**Decision: defer to the catalogue-choice thread, and do not add scope now.** Adding it
here would unblock nothing. F3K is independently blocked by
`drawPhase.unsupportedRoundComposition` (`Competition.cs:573-580`), and the two
constraints are really one problem: the parameters are named per *task*
(`workingTime.A`), so binding "the working time for round 3" is meaningless until round 3
has a task chosen from the catalogue. Per-round binding is downstream of catalogue
choice, not parallel to it.

**Design note for that thread (agreed 2026-08-08): for the catalogue-choice classes,
the tasks should be set at draw time.** `ChooseFromCatalogue` is exactly two definitions
— F3K (13- and 14-task catalogues) and F5K (5-task catalogues, both phases) — and in
both, `tasksPerRound` is 1. So the draw is the natural point at which each round's task
is fixed, which means `PhaseDrawn` grows a per-round task selection rather than a
separate later event. Once the task is known at draw time, the per-round parameter
scope has an obvious shape and a real consumer.

Do not confuse this with **F3B**, which is `FixedSequence` with `tasksPerRound: 3` — a
*multi-task* round, not a catalogue choice. It is a third, separate deferral.

#### Finding 2 — `Parameter.DefaultValue` is inert · **fixed here, see WI-2**

`ParameterResolver.Resolve` consults only the bindings dictionary and throws when a
`Ref` is unbound; it never falls back to the declared default. Thirteen parameters in
the corpus carry a default (F5K `nlh` = 60 m, NZ-M `targetTime` = 600 s, F3K's six
per-round values, and others).

**Decision: the resolver falls back to `Parameter.DefaultValue`.** The rejected
alternative was seeding a `ParameterBound` per default at `CreateCompetition`. Two
things settled it:

1. **The audit objection does not hold.** `AdoptedRules.Definition` is an immutable copy
   of the whole class definition, written into the log by `CompetitionCreated`. The
   defaults are therefore *already in the event log*, and the effective value is fully
   reconstructible from it without seeding anything.
2. **Seeding breaks `RulesAmended`.** That event exists to append "a corrected
   definition, applied retroactively across the whole competition"
   (`CompetitionEvents.cs:102`). Under a fallback, an amended default applies
   retroactively as intended. Under seeded bindings, the original seeded value silently
   wins and the amendment is a no-op — a subtle and nasty failure.

This does **not** change the acceptance criteria of this thread: the three blocked
parameters have no defaults, so they still require an explicit binding. It is in scope
because it collides with this thread at the same call site (`Competition.cs:609`).

### Out of scope (deliberately)

- **Per-round bindings** — finding 1 above.
- **Catalogue-choice rounds and multi-task rounds** — finding 1 above.
- **`RulesAmended`, `Finalised`, `PenaltyRecorded`, `ReflightGroupAppended`,
  `TaskRoundCompleted`, `TaskRoundAnnulled`** — the other six unreachable events. Same
  shape, but each needs its own rule check; batching them would hide that.
- **Unbinding.** No `ParameterUnbound` event exists and none is proposed. Correcting a
  binding is a re-bind, which the last-write-wins fold already handles.
- **A `parameters` read model or any read-model column.** The bindings are visible via
  `GET /competition`, which already returns the folded aggregate.

### Governing documents

- `docs/aggregate-roots.md` §3 — bindings are events, not configuration, so re-scoring
  reproduces decisions as actually taken. This is quoted in `ParameterBinding`'s own doc
  comment (`Competition.cs:111-115`) and is the reason this is a command at all.
- `docs/ladr/ladr-0001-event-store.md` §4.4 — `ExpectedVersion.Exact(version)` is what
  makes the check race-free.
- CLAUDE.md's core architectural law — nothing here may branch on class name. The decide
  function reads `Parameters` from the adopted definition generically; it never learns
  that `minPerGroup` is special.

---

## Phase A — Domain

### WI-1 — `Competition.BindParameter` decide function

Instance decide function on `Competition`, in the `Defect`-chain style of
`RegisterCompetitor` (`Competition.cs:498`) rather than `DrawPhase`'s early-return style
— no later check needs a value computed by an earlier one.

```csharp
public Result<ParameterBound> BindParameter(
    string parameterName, MeasuredValue value, string by, DateTimeOffset at)
```

Checks, in order:

| Code | Condition |
|---|---|
| `competition.parameter.notDeclared` | `parameterName` is not in `AdoptedRules.Definition.Parameters` |
| `competition.parameter.kindMismatch` | `value.Kind != parameter.Kind` |
| `competition.parameter.valueNotAllowed` | `parameter.AllowedValues` is non-empty and does not contain `value` |
| `competition.parameter.frozen` | `parameter.BoundAt == CompetitionSetup` **and** `!Phases.IsEmpty` |

Four notes on what is deliberately *absent*:

- **No unit check.** `MeasuredValue` carries no unit (`ScoringVocabulary.cs:24-34`) —
  only `Kind` and a nullable `Number`/`Flag`. Unit agreement between a parameter and the
  slot consuming it is checked at *adoption* (check 7,
  `ClassDefinitionValidation.cs`), where both units are actually present. There is
  nothing to compare here.
- **No range check.** No `Parameter` field expresses a range, and inventing one would be
  a class-model change. A `minPerGroup` larger than the field is already caught at draw
  time by `drawPhase.fieldTooSmall` (`Competition.cs:618-622`), which is the right place: it
  depends on the field, which can change after binding.
- **Re-binding before the draw is allowed**, and the last binding wins — the fold appends
  and `Competition.cs:602-605` takes `g.Last()`. A CD correcting a setup typo is the
  expected case; both values stay in the log.
- **The freeze is scoped to `CompetitionSetup` only.** `BeforeFlying` parameters (F5K
  `nlh`, NZ-M `targetTime`) are legitimately bound *after* the draw, so freezing every
  parameter at the draw would be wrong. This is the same asymmetry
  `WithdrawCompetitor` documents at `Competition.cs:521-527`: registration closes at the
  draw, withdrawal never does.

`ValidateFieldNotFrozen` (`Competition.cs:678-680`) carries a comment saying "revisit
this check once `Draw.Status` has a defined value set". This thread adds a **second**
consumer of the same `!Phases.IsEmpty` approximation. Do **not** generalise the two into
one helper — they answer different questions (*is the field closed* vs *is this
parameter settled*) and will diverge when draw acceptance lands. Add a cross-reference
comment on each instead.

**Tests** (`tests/Soarscore.Domain.Tests/BindParameterDecideTests.cs`): one per failure
code, plus success, plus re-bind-before-draw, plus `BeforeFlying`-after-draw succeeds
while `CompetitionSetup`-after-draw fails.

### WI-2 — `ParameterResolver` falls back to the declared default

Finding 2. `ParameterResolver` currently takes only a bindings dictionary; it gains the
declared parameters as a second source, giving a three-step resolution order:

1. a binding, last-write-wins — the CD's explicit choice;
2. `Parameter.DefaultValue` — the rulebook's stated value;
3. otherwise throw `UnresolvedParameterException`, as today.

The public surface is `Resolve`, `ResolveOr`, `ResolveFlag`, `ResolveFlagOr` and
`ResolveTask` (`ParameterResolver.cs:45-103`). All five need the new source, since a
`Ref` can reach any of them.

**Call sites are few.** Production: `Competition.cs:609` (the draw) and
`ScoringService.cs:96` (`ResolveTask`). Tests: nine in `FlightSelectorTests.cs` and
`FlightInterpreterTests.cs`, all passing empty or small dictionaries. Pass
`AdoptedRules.Definition.Parameters` at the draw site; `ScoringService` already receives
the definition.

Two cautions:

- **`ResolveFlag` needs the same treatment**, not just the numeric path. F5K, F5L and
  F3K each declare a `carryPenalties` flag parameter, and `PromotionRule.carryPenalties`
  is the one `FlagOrParam` slot of the thirteen.
- **Kind mismatch must stay an error.** `ResolveBinding` already throws when a bound
  value is the wrong `MeasuredKind` (`ParameterResolver.cs:131-133`). A default of the
  wrong kind is an adoption-time defect, not something to paper over at resolve time —
  let it throw.

**Tests** — `ParameterResolver` currently has **no test file at all** (one of three
untested scoring components). This work item is the moment to add
`tests/Soarscore.Domain.Tests/ParameterResolverTests.cs`: binding wins over default,
default applies when unbound, unbound with no default still throws, wrong-kind binding
still throws, and the same four for the flag path.

### WI-3 — Property tests: binding unblocks the draw

`tests/Soarscore.Domain.Tests/BindParameterPropertyTests.cs`, CsCheck.

1. **The unblocking property.** For any class definition with a parameterised
   `minPerGroup` and any field large enough: a competition that fails `DrawPhase` with
   `drawPhase.parameterUnbound` succeeds after `BindParameter`, and the resulting groups
   satisfy the same size invariants WI-2 of the phase-drawn plan asserts for literal
   classes. *This is the property that proves the thread did its job.*
2. **Last-write-wins.** For any sequence of bindings of one parameter, the draw resolves
   the value from the final binding, and every earlier binding is still present in
   `ParameterBindings`.
3. **F5L's shared binding.** Binding `groupSize` once resolves **both** the preliminary
   and fly-off `minPerGroup` slots to the same value — the rule at `5.5.12.4`, asserted
   against the real seed definition rather than a synthetic one.
4. **Binding beats default, default beats nothing** (needs WI-2). For every parameter in
   the corpus that declares a default: resolving with no binding yields the default;
   resolving with a binding yields the bound value, whatever the default says. The two
   sources must compose in exactly one direction.
5. **Generic over the corpus.** Run 1 across every seed definition with a parameterised
   `minPerGroup` — F5K, F5L, NZ-M ALES 200, and *only* those three, asserted by scanning
   `tools/Soarscore.SeedData/json/` rather than hard-coding the list. If a future class
   definition adds a fourth, this test should pick it up without being edited. The
   architectural law says the draw must not care which class it is, and a test
   parameterised over the corpus is how that gets asserted.

## Phase B — Application

### WI-4 — `BindParameter` command and handler

`src/Soarscore.Application/Competitions/BindParameter.cs`, following
`RegisterCompetitor.cs` exactly: load, fold, decide, append with
`ExpectedVersion.Exact(version)`.

```csharp
public sealed record BindParameter(
    CompetitionId CompetitionRef,
    string ParameterName,
    MeasuredValue Value,
    string By) : ICommand<CompetitionId>;
```

`At` comes from `IClock`, never the caller — as everywhere else.

**`By` is new, and worth a note.** This is the first command to carry it.
`ParameterBinding.By` is `required`, and the trust model has no auth (CLAUDE.md: club
tool, no sign-off, "an immutable event log of all mutations provides auditability
instead"). So `By` is a **self-declared** CD name, validated only as non-empty
(`competition.parameter.byRequired`). It is an audit breadcrumb, not an
authorisation claim, and the handler must not treat it as one. The same field appears on
`Finalisation` (`Competition.cs:139`), so the convention set here will be inherited.

**Tests** (`tests/Soarscore.Application.Tests/Competitions/BindParameterHandlerTests.cs`):
success, `competition.notFound`, each decide failure surfaced faithfully, stale-version
retry, and idempotent replay.

### WI-5 — Marten wiring and event JSON

**This is the step most easily missed, and it fails at runtime rather than at build.**
`MartenConfig.cs:49-52` registers **four** of the eleven `CompetitionEvent` subtypes;
`:41-46` documents the other seven as unregistered. Appending an unregistered event
fails at runtime, so:

```csharp
opts.Events.MapEventType<ParameterBound>("parameterBound");
```

and remove `ParameterBound` from the not-registered comment at `:41-46`, leaving six.

Add a `ParameterBound` case to
`tests/Soarscore.Application.Tests/CompetitionEventJsonTests.cs`, round-tripping
**both** `MeasuredKind` variants — `Number` and `Flag` — since `MeasuredValue`'s two
nullable payload fields serialise asymmetrically and only the `Number` path is exercised
by the draw.

## Phase C — Api and verification

### WI-6 — Api endpoint

`src/Soarscore.Api/Commands/Commands.cs`, one line, verb not noun:

```csharp
app.MapCommand<BindParameter, CompetitionId>("/bind-parameter");
```

`RouteShapeTests` covers it automatically.

### WI-7 — Store-backed tests

`tests/Soarscore.Infrastructure.Tests/BindParameterEventStoreTests.cs`, tagged
`Trait("Category", "Storage")`:

1. A binding survives an append/read round trip through PostgreSQL.
2. Two bindings of one parameter both persist, in order.
3. **The payoff test:** adopt the real F5K definition, register a field, `BindParameter`
   `minPerGroup`, `DrawPhase` — succeeds, with groups of the bound size. Against a real
   store, end to end. This is the test that would have failed before the thread and
   passes after it.
4. Drop the read model and replay from the log; bindings and the resulting draw are
   identical.

### WI-8 — End-to-end verification

Manual pass against a running API + Postgres: publish F5K, create a competition,
register competitors, attempt the draw (expect `drawPhase.parameterUnbound`), bind
`minPerGroup`, draw again (expect success).

Both prior plans' e2e items (phase-drawn WI-8, create-competition WI-7) were manual and
left **no record of having been run**. Either capture this one as a checked-in `.http`
file or script, or state plainly in the PR that it was executed manually — do not leave
a third undocumented manual step.

---

## Dependency order

```
WI-1 ─┬─────────────> WI-4 ──> WI-5 ──> WI-6 ──> WI-7 ──> WI-8
      │               ^
WI-2 ─┴──> WI-3 ──────┘
```

WI-1 and WI-2 are independent of each other and are the only design work; WI-2 also
touches `Competition.cs:609`, so land one before starting the other to avoid a conflict
in that method. WI-3's first property needs both.

## Acceptance

- F5K, F5L and NZ Class M ALES 200 can be drawn. Before this thread, none of them could.
- A parameter with a declared default resolves without an explicit binding; one without
  a default still fails loudly.
- No new domain concept, no glossary change, no class-model change. No event payload
  changes — `ParameterBinding` is untouched.
- No class name appears anywhere in the new code.
- `ParameterBound` is registered in Marten; six subtypes remain unregistered.
- Property tests run over the seed corpus, not a hand-picked class.
- `ParameterResolver` has a test file, reducing the untested scoring components from
  three to two.

## What this unlocks

Bound parameters are the first CD decisions the system records, and the draw is only
their first consumer. The same bindings feed `ParameterResolver.ResolveTask`
(`ParameterResolver.cs:103`), which the scoring engine calls throughout — so every
parameterised working time, target time and launch-height limit in the corpus resolves
through this command once scoring is wired up.

It also settles the shape for the six remaining unreachable `CompetitionEvent` types:
decide function, command, handler, `MapEventType`, endpoint, store-backed test. Each is
now a known quantity rather than a design question.

WI-2 has a second effect worth naming: with defaults resolving, the six `PerRound`
parameters F3K declares all resolve to their stated values (600 s, 240 s, 599 s). That
does not unblock F3K — it is still refused at the draw — but it means the missing
per-round scope costs the CD an *override*, not the ability to score at all. Finding 1
drops from blocking to missing.

**Still gated, and not by this thread:**

- The Entry write path (no decide functions exist) and `entry_index` — the critical path.
- **Catalogue-choice rounds, with each round's task set at draw time**, carrying
  per-round parameter scope with them (finding 1). This is the thread that unblocks F3K
  and completes F5K.
- Multi-task rounds (F3B) — separate from the above, and separately deferred.
