# Plan — Create-competition steel thread: `CreateCompetition`

**Status:** Complete — implemented and test-verified · **Date:** 2026-08-05

Work items are numbered `WI-n`, scoped to *this* plan document (see
`command-side-steel-thread-plan.md`'s own numbering note — WI numbers reset per plan).

## Context

The second thread named in `command-side-steel-thread-plan.md`'s "What this unlocks":
*"then `CreateCompetition` — at which point the `class_library` and `competitions` read
models join the two already-built halves and the system can hold a real event."*

**This plan depends on `class-definition-adoption-steel-thread-plan.md`.** `CreateCompetition`
adopts a class definition by content hash, which means folding the
`PublishedClassDefinition` stream that plan's `PublishClassDefinition` makes appendable, and
reusing the `ClassDefinitionStreamId` helper that plan relocates into
`Soarscore.Application/CompetitionClasses/`. Do not start this plan's WI-2 before that one's
WI-4 lands.

**Deliberately narrow scope, matching how the command-side plan scoped Person.** `Competition`
(`docs/aggregate-roots.md` §3) is a large aggregate — field, phases, rounds, draw,
task-rounds, groups, rules amendments, parameter bindings, finalisation, penalties. Eleven
events already exist for it in
`src/Soarscore.Domain/Competitions/CompetitionEvents.cs`, and the fold for all eleven is
already written in `Competition.cs`. **This thread builds one decide function and one
command: bringing a `Competition` into existence with an empty field and no phases yet.**
Everything else — registering a competitor, drawing a phase (the fair-draw algorithm
`CLAUDE.md` names as a core capability), annulling a task-round, amending rules, binding a
parameter, finalising — is its own future thread, the same way `RegisterPerson` shipped
before `RenamePerson`'s siblings proved out the pattern, except here even the *first*
mutation-after-creation is future work, not part of this plan.

The `CompetitionCreated` event's own doc comment already names the shape this leaves behind:
*"this event alone folds to a Competition with an empty field and no phases yet — a
transient state a command handler, not this fold, is responsible for not leaving exposed."*
Concretely: nothing in this plan claims a freshly created Competition is ready to fly. It is
one legitimate, honest step in an event-sourced setup sequence, and the next thread picks up
from here.

### Out of scope (deliberately)

- `RegisterCompetitor` / `CompetitorWithdrawn` — the field.
- `PhaseDrawn` — the draw itself. This is where the fairness invariant
  (`CLAUDE.md`: "fair round-by-round draws") actually lives; it deserves its own thread and
  should not be rushed in alongside creation.
- `ReflightGroupAppended`, `TaskRoundCompleted`, `TaskRoundAnnulled`, `RulesAmended`,
  `ParameterBound`, `Finalised`, `PenaltyRecorded` — the rest of the eleven events. Folds for
  all of them already exist in `Competition.cs`; only their decide functions and commands are
  missing, and none are built here.
- `entry_index` (LADR-0001 §3) and anything Entry-shaped — a different aggregate, a later
  thread.
- A `competitions` read model `State` column. LADR-0001 §3 says the read model must support
  listing "by date, class, state", but with only `CreateCompetition` in scope every row's
  state is identically "created" — adding a `State` enum with one live value now is exactly
  the kind of hypothetical-future-requirement design this codebase avoids. The column arrives
  with the thread that first gives it a second value (most likely `PhaseDrawn`).

### Governing documents

`docs/aggregate-roots.md` §3 (Competition's shape and the `AdoptedRules` copy-at-creation
rule), `docs/ladr/ladr-0001-event-store.md` §3 (`competitions` read model) and §4,
`docs/ladr/ladr-0002-class-definition-representation.md` §5 ("the library can be edited or
retired without any live or historical event noticing" — this plan is the first place that
guarantee gets exercised for real), `class-definition-adoption-steel-thread-plan.md` (the
dependency above).

---

## Phase A — `competitions` read model

### WI-1 — `competitions` read model + query port

- `CompetitionSummary` in Application: `Id`, `Name`, `Location`, `StartDate`, `EndDate`,
  `ClassName`, `ClassContentHash` (denormalised from `AdoptedRules` at creation, so a list
  view never needs to fold every stream to show what class each competition runs). One of
  the four read models LADR-0001 §3 permits — do not add a fifth.
- `CompetitionProjection.Apply(CompetitionSummary?, CompetitionEvent) → CompetitionSummary?`
  — plain static function in Application (LADR-0001 §4.3), mirroring `PeopleProjection`. Only
  the `CompetitionCreated` case is reachable this thread; the `switch` still needs a total
  arm for the other ten event types (the same event union `Competition.Apply` folds) —
  **the correct choice is `_ => current` (pass through unrecognised-yet events unchanged),
  not `throw`**, because a real deployment's log will start accumulating those events
  before every command that produces them has landed, and a still-unhandled event type must
  not crash the Inline projection for every competition that later gets one appended to its
  stream.
- `ICompetitionsQuery` in Application, implemented in Infrastructure: `SearchAsync(DateOnly?
  onOrAfter, string? classContentHash)`. No get-by-id method, same rule as `IPeopleQuery` and
  `IClassLibraryQuery` — `GetCompetition` (WI-3) folds the stream.

**Verify:** fold tests for `CompetitionProjection` — feed it a `CompetitionCreated` and,
separately, one of the ten out-of-scope event types against a non-null summary, and assert
the second call is a no-op rather than a throw.

---

## Phase B — `CreateCompetition` write path

### WI-2 — `Competition.Create` decide function (Domain)

Add to `src/Soarscore.Domain/Competitions/Competition.cs`, alongside the existing
`Create`/`Apply` fold — note the fold is already called `Create` (`CompetitionCreated →
Competition`), so the decide function needs a different name to avoid a collision;
`Competition.Decide` or a static factory on a small `CompetitionCreation` holder are both
fine, **`Person.Register`'s naming convention does not transfer directly here** because
`Person` had no pre-existing `Create` fold method name to collide with.

```
static Result<CompetitionCreated> Decide(
    CompetitionId id, string name, string location,
    DateOnly startDate, DateOnly endDate, string evaluatorVersion,
    AdoptedRules adoptedRules, DateTimeOffset at)
```

Invariants this aggregate genuinely owns, checked here (mirroring how `Person.Register`
checks only what `Person` itself can know, per the same reasoning
`command-side-steel-thread-plan.md` WI-4 states):

- Non-blank `name`, non-blank `location`.
- `startDate <= endDate`.

**`AdoptedRules` validity is emphatically not checked here.** By the time this function is
called, the handler (WI-3) has already resolved `adoptedRules` by folding the
`PublishedClassDefinition` stream and confirming it exists and is not retired — a decide
function takes already-resolved value objects as input, the same way `Person.Register` takes
an already-constructed `ContactDetails` rather than reaching out to check anything about it
itself. **Nor does this function re-run `Validate()`.** A definition that validated once at
publish time validates identically forever — `ClassDefinition` is immutable, content-addressed
data (LADR-0002 §5) — so re-validating at every adoption would be pure repeated work with no
new information to find.

**Verify:** decide-function tests in `Soarscore.Domain.Tests` — blank name/location →
failure with a stable code; `startDate > endDate` → failure; valid input → the expected
event, `AdoptedRules` passed through unchanged. Existing fold tests untouched and still
green.

### WI-3 — `CreateCompetition` command, `GetCompetition`/`FindCompetitions` queries

`CreateCompetition(string Name, string Location, DateOnly StartDate, DateOnly EndDate, string
ClassContentHash) : ICommand<CompetitionId>`.

Handler shape — **a new pattern, not the WI-6 template**: this is the first command whose
decide function needs data from a *different* aggregate's stream before it can run.

```
1. classStreamId = ClassDefinitionStreamId.From(command.ClassContentHash)
   (Application-owned per class-definition-adoption-steel-thread-plan.md WI-4's relocation —
   if that plan's WI-4 has not landed, this call does not compile; that is the dependency
   stated in Context.)
2. read that stream via IEventStore.ReadStreamAsync; fold with
   PublishedClassDefinition.Apply. No stream, or the fold returns null -> Result.Failure,
   "createCompetition.classDefinitionNotFound".
3. folded.RetiredAt is not null -> Result.Failure, "createCompetition.classDefinitionRetired"
   (LADR-0002 §5 / ClassDefinitionEvents.cs: "removes it from what a new Competition may
   adopt". Currently unreachable in practice — RetireClassDefinition is out of scope in the
   other plan, so nothing can produce a retired definition yet — but the check costs three
   lines and removes a TODO rather than leaving a silent gap for whenever that command lands.)
4. id = CompetitionId.New()
5. adoptedRules = new AdoptedRules { Definition = folded.Definition, SourceClassId =
   folded.ContentHash, SourceVersion = folded.Definition.Version, AdoptedAt = clock.UtcNow }
6. decision = Competition.Decide(id, name, location, startDate, endDate, evaluatorVersion,
   adoptedRules, clock.UtcNow)
7. decision failure -> propagate. Else append CompetitionCreated with ExpectedVersion.NoStream,
   same shape as RegisterPerson's first append.
```

This is a cross-aggregate *read*, not a cross-aggregate write or a read-check-write against
*this* aggregate's own invariants (LADR-0001 §4.4 forbids the latter, not the former) — folding
another aggregate's stream to copy data into a new one is exactly what `AdoptedRules` being
"a complete copy... taken at creation" (`aggregate-roots.md` §3) requires, and there is no
foreign-key alternative in an event-sourced model.

**`evaluatorVersion` has no existing source in the codebase** — `ScoringService` does not
expose a version constant today (confirmed by grep). Introduce a simple stable constant for
this thread (e.g. a literal `"1"`, or the assembly informational version) and treat choosing
its real long-term source as a separate decision; nothing here depends on the value being
meaningful yet, only present and stable across a replay.

- `GetCompetition(CompetitionId) : IQuery<Competition>` — folds the stream via `IEventStore`,
  same pattern as `GetPerson`/`GetClassDefinition`.
- `FindCompetitions(DateOnly? OnOrAfter, string? ClassContentHash) :
  IQuery<IReadOnlyList<CompetitionSummary>>` → `ICompetitionsQuery`.

**Verify:** Application tests via `IDispatcher`, a fake `IEventStore` pre-loaded with a
published class-definition stream, fake clock. Cover: creating against a known, active
class-definition hash succeeds and the appended event carries a full `AdoptedRules` copy;
an unknown hash fails with `classDefinitionNotFound`; a retired one (construct the fake
stream with both events) fails with `classDefinitionRetired`; blank name/location and
`startDate > endDate` fail via `Competition.Decide`.

### WI-4 — Marten wiring (Infrastructure)

Extends `ServiceCollectionExtensions.cs`:

- `opts.Events.MapEventType<CompetitionCreated>("competitionCreated")` **only** —
  the other ten `CompetitionEvent` subtypes are not registered this thread, because nothing
  appends them yet. **Each future thread that adds a command producing one of the other ten
  must add its own `MapEventType` line before that command can append** — easy to forget
  since the JSON `$kind` discriminators for all eleven are already declared on
  `CompetitionEvents.cs` and compile cleanly whether or not Marten's registry knows them;
  only the registry is per-command, the discriminators are not.
- `opts.Projections.Add(new CompetitionSummaryProjection(), ProjectionLifecycle.Inline)`.
- No unique index — nothing about a competition's summary fields is unique the way
  `PersonSummary.Email` is.

**Verify:** none new — folded into WI-6's store-backed tests.

---

## Phase C — Api and end-to-end verification

### WI-5 — Api endpoints

Through the existing `MapCommand`/`MapQuery` helpers only, no new routing surface:

- `POST /create-competition`.
- `GET /competitions?onOrAfter=…&classContentHash=…` → `FindCompetitions`.
- `GET /competition?id=…` → `GetCompetition`, folding the stream.

### WI-6 — Store-backed tests

New tests in `tests/Soarscore.Infrastructure.Tests` (Testcontainers,
`Trait("Category", "Storage")`):

1. Publish a class definition, then create a competition adopting it by hash; read the
   competition stream back and confirm `AdoptedRules.Definition` matches the published
   definition exactly — the round-trip proof for the cross-aggregate copy.
2. Creating against a retired definition's hash is rejected (publish, retire — via a direct
   `IEventStore.AppendAsync` of `ClassDefinitionRetired` in the test, since
   `RetireClassDefinition` the command does not exist yet — then attempt creation).
3. `competitions` is dropped and fully replayed from the log and lands identical
   (LADR-0001 §4.10).

### WI-7 — End-to-end verification

Against a running API and PostgreSQL, in order:

- `POST /publish-class-definition` (from the other plan) with a corpus definition → the hash.
- `POST /create-competition` naming that hash → 200, the competition id.
- `GET /competitions` lists it with the right class name.
- `GET /competition?id=…` returns it folded from the stream, `AdoptedRules` populated.
- `POST /create-competition` with a bogus hash → `ProblemDetails`,
  `createCompetition.classDefinitionNotFound`.

---

## Dependency order

```
class-definition-adoption-steel-thread-plan.md WI-4 ── external prerequisite
WI-1 ─┐ (independent, parallelisable)
WI-2 ─┘
WI-3 ── needs WI-2, WI-1, and the external prerequisite
WI-4 ── needs WI-3
WI-5 ── needs WI-3, WI-4
WI-6 ── needs WI-4
WI-7 last
```

## What this unlocks

A Competition exists and holds a real, provable rulebook copy — `RegisterCompetitor` and
`PhaseDrawn` (the fair-draw thread `CLAUDE.md` names as core to what this system is) both
hang off a real `CompetitionId` for the first time. `entry_index` and the Entry aggregate
remain gated behind those, not this thread.
