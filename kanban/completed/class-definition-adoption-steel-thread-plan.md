# Plan — Class-definition adoption steel thread: `Validate()` and `PublishClassDefinition`

**Status:** Complete — implemented and test-verified · **Date:** 2026-08-05

Work items are numbered `WI-n`, scoped to *this* plan document — the convention
`command-side-steel-thread-plan.md` itself establishes (its own WI-0 citation points at
the fold-refactor plan's WI-0, a different document; numbering resets per plan, not
globally).

## Context

This is the first of the two threads named in `command-side-steel-thread-plan.md`'s
"What this unlocks": *`Validate()` and the sixteen adoption checks, then
`PublishClassDefinition`.* `CreateCompetition` is a deliberately separate plan
(`create-competition-steel-thread-plan.md`) — see "Out of scope" below.

Builds entirely on the WI-3 kernel (`Result<T>`, `IDispatcher`, `IEventStore`, `IClock`)
and the WI-7/WI-8 Marten/Api adapters. Nothing in that plumbing changes.

**More has already been built toward this thread than "missing: everything" suggests.**
Already in place and reused as-is, not rebuilt:

- The full `PublishedClassDefinition`/`ClassDefinition` domain model
  (`src/Soarscore.Domain/PublishedClassDefinition/`), both events
  (`ClassDefinitionPublished`, `ClassDefinitionRetired`) and the aggregate fold.
- `ClassDefinitionHashing.ComputeContentHash` (`src/Soarscore.Application/CompetitionClasses/`)
  — ADR-0002 §5's canonical-JSON SHA-256 hash, already byte-compatible with
  `tools/Soarscore.SeedData` by construction (shared `Hashable`-equivalent options).
- `SoarscoreEventJson.Options` (`src/Soarscore.Application/EventJson.cs`) already registers
  the `NumberOrParam`/`FlagOrParam` converters specifically *because*
  `ClassDefinitionPublished` embeds a full `ClassDefinition` — the event-log serialisation
  path is ready for this event today.
- `tools/Soarscore.SeedData` — eleven FAI + four NZ definitions as canonical, round-tripped,
  depth-checked JSON. This is the acceptance fixture LADR-0002 §1 names: *"seed classes
  must enter through the same door as user classes."*

**What is genuinely missing** (confirmed by grep — no `Validate` method exists anywhere over
`ClassDefinition` today): `Validate()` itself, the ingestion input limits LADR-0002 §4 asks
for beside it, the `class_library` read model and query port, the `PublishClassDefinition`
command/handler, the Marten registration for the two `ClassDefinitionEvent`s, and the Api
endpoints.

### Out of scope (deliberately)

- `CreateCompetition` and everything Competition-shaped — own plan.
- `RetireClassDefinition`. `ClassDefinitionRetired` already exists as an event and needs no
  new Domain work, but the command/handler/endpoint is a small, separable follow-on once
  `PublishClassDefinition` proves the pattern — not needed to prove the pipeline. (One
  consequence carried into the other plan: nothing in the system can produce a retired
  definition yet, so `CreateCompetition`'s retirement check is currently unreachable but
  still worth writing — see that plan.)
- Drift-detection tooling ("this class changed since your last adoption"). LADR-0002 §5 is
  explicit: *"We expose the hashes and the comparison. We do not build the warning."*
- A notation (`.class`) parser — LADR-0002 §3, settled.
- Editing a published definition in place. LADR-0002 §5: identity is the content hash, so
  different content is a different stream; there is no edit, only publish (and,
  out-of-scope-here, retire).

### Governing documents

`docs/ladr/ladr-0002-class-definition-representation.md` (the whole document — this plan is
its implementation), `docs/ladr/ladr-0001-event-store.md` §3 (`class_library` read model)
and §4 (the ten binding constraints, already respected by the reused WI-7 adapter),
`docs/ladr/ladr-0003-library-choices.md` (the `Validate()` row),
`docs/high-level-architecture.md`'s "Validated at adoption" section — the canonical,
numbered inventory of all sixteen checks. **This plan cites checks by number and does not
restate them**; if a check's wording is needed, read it there.

---

## Phase A — `Validate()` and ingestion limits

Independent of storage; both operate on an in-memory `ClassDefinition`.

### WI-1 — Ingestion input limits

LADR-0002 §4: *"payload size, nesting depth, band/row/term/parameter/task counts... the
absence of them is the obvious denial-of-service surface."* Nesting depth already has a
value to copy: `SoarscoreJson.IngestionMaxDepth = 24` (`tools/Soarscore.SeedData/Json.cs`,
a spike finding — the corpus's deepest path is 11) needs an Application-owned copy, since
`SoarscoreJson` lives in the seed tool and Application must not depend on it.

- New ingestion-JSON options in `src/Soarscore.Application/CompetitionClasses/`, mirroring
  `SoarscoreJson.Ingestion`'s settings (camelCase, `WhenWritingNull`, `MaxDepth = 24`,
  `AllowOutOfOrderMetadataProperties = true`, the two `NumberOrParam`/`FlagOrParam`
  converters). This is what a `POST /publish-class-definition` body binds through.
- A payload size cap (bytes), enforced at the Api/model-binding layer.
- Count ceilings — bands, rows, terms, parameters, tasks per definition — set generously
  relative to the corpus's actuals (the seed tool's `Program.cs` already prints
  tasks/terms per class; use those as the "just inside" baseline) and enforced as part of
  ingestion, before `Validate()` runs, since they bound how much work `Validate()` itself
  has to do on adversarial input.

**Verify:** a definition just inside each limit passes; just outside is rejected with a
stable code. Use the corpus's actual maxima as fixtures rather than invented numbers.

### WI-2 — `Validate()`: the sixteen adoption checks

**Placement: `src/Soarscore.Application/CompetitionClasses/ClassDefinitionValidation.cs`,
not Domain.** This follows the precedent `ClassDefinitionHashing.cs` already sets in the
same folder: LADR-0002 §4 frames validation and hashing as two steps of one ingestion
pipeline (`deserialise → Validate → canonicalise+hash → append`), not as an aggregate
invariant the way `Person.Register`'s checks are. `Result`/`Defect` (Domain, WI-3) are
reused without a new dependency — Application already references Domain.

- `static IReadOnlyList<Defect> Validate(ClassDefinition definition)` — total and
  non-throwing on well-typed input (LADR-0002 §4: "returns every defect, not the first").
  One private method per numbered check (1–16). Cite the check number in a comment on each
  method, the same way the Domain model already does —
  `ClassDefinition.cs:242`'s `FinalRanking` property already says *"Two adoption checks,
  one each way (11, 12)"*.
- `Defect.Code` convention: `class-definition.check-<n>.<slug>`. LADR-0002 §4 asks the
  rendered defect to carry "check identity"; a code a user can grep straight back to the
  numbered list earns more than `Person`'s domain-flavoured `person.name.blank` style would
  here.
- `Defect.Path`: a JSONPath into the offending construct (e.g.
  `$.phases[1].tasks[0].score[2]`, extending `Person`'s `$.name` convention). LADR-0002 §4:
  this is *"the only feedback a user authoring a class ever gets"* — the path must locate
  the construct in a POSTed document, not just name the check.
- The two checks that **left** the inventory (the `Predicate` "exactly one of" combination;
  the all-normalised-terms task) are unrepresentable by construction, per the note directly
  below the sixteen in `high-level-architecture.md`. Do not write checks for them.

**Verify** — per the user's stated preference that property-based testing is routine
alongside unit tests:

- Sixteen unit tests minimum, one negative fixture per check: take a corpus definition,
  mutate exactly the one construct the check guards, assert `Validate()` returns exactly
  that check's code and nothing else.
- All fifteen seed definitions (eleven FAI + four NZ) validate clean — zero defects. This is
  the corpus's job as "the model's test" (`CLAUDE.md`), now exercised through the real gate.
- Consider a CsCheck property test: generate a valid definition (a corpus definition plus one
  random mutation drawn from a table mapping "this mutation" → "should trip check N"), assert
  the *only* defect raised is check N. Sixteen independent unit tests cannot catch a check
  firing on the wrong input or silently swallowing an adjacent one; this can.

---

## Phase B — `class_library` read model and the write path

### WI-3 — `class_library` read model + query port

- `ClassDefinitionSummary` in Application: `ContentHash`, `Name`, `FaiDesignation`,
  `Version`, `PublishedAt`, `RetiredAt`. One of the four read models LADR-0001 §3 permits.
- `ClassDefinitionProjection.Apply(ClassDefinitionSummary?, ClassDefinitionEvent) →
  ClassDefinitionSummary?` — plain static function in Application (LADR-0001 §4.3), mirroring
  `PeopleProjection`. Folds `ClassDefinitionRetired` even though `RetireClassDefinition` (the
  command) is out of scope this thread — the projection is total over the event union
  regardless of which commands exist yet, exactly as `PublishedClassDefinition.Apply` in
  Domain already is.
- `IClassLibraryQuery` in Application, implemented in Infrastructure: `FindByHashAsync`,
  `SearchAsync(string? name, bool activeOnly)`. **No shortcut that returns the full
  `ClassDefinition` from this interface** — same rule WI-5 of the command-side plan stated
  for people: a lookup that needs the full definition folds the stream (`GetClassDefinition`,
  WI-4 below); this read model exists solely for the cross-stream search a stream can't
  answer.

**Verify:** fold tests for `ClassDefinitionProjection` — pure function, no store needed.

### WI-4 — `PublishClassDefinition` command, `GetClassDefinition`/`FindClassDefinitions` queries

**First, a layering conflict to resolve — read before writing the handler.**
`ClassDefinitionStreamId.From(contentHash)` (`src/Soarscore.Infrastructure/CompetitionClasses/`)
derives the deterministic Guid Marten uses as the stream key, and its own header comment
calls this *"an Infrastructure-only concern... nothing outside Infrastructure should need to
know this derivation exists."* That cannot hold once this WI is written: the
`PublishClassDefinition` handler must supply a `Guid streamId` to
`IEventStore.AppendAsync(Guid, ExpectedVersion, ...)`, and `GetClassDefinition` below must
supply the same Guid to `ReadStreamAsync` — both are Application-layer code, which must not
depend on Infrastructure. The function itself has zero Marten/Npgsql dependency (pure
`Guid`-from-bytes arithmetic), so the fix is relocation, not redesign: **move
`ClassDefinitionStreamId` into `src/Soarscore.Application/CompetitionClasses/`**, alongside
`ClassDefinitionHashing`. Infrastructure keeps calling it from its new home; nothing about
its behaviour changes. (This is the kind of thing an implementing agent is likely to get
wrong by leaving the derivation where the comment says it belongs and then improvising a
second one in Application — don't; there is exactly one.)

`PublishClassDefinition(ClassDefinition Definition) : ICommand<string>` — returns the
content hash, not a minted id (there is none to mint; `PublishedClassDefinition.cs`'s own
comment: *"there is no ClassDefinitionId to mint"*).

Handler shape — **and this deviates from the WI-6 handler template**
(`read stream → fold → decide → append Exact(version)`) because there is no prior stream to
read and no decide function to call: creation only, and creation is idempotent by design.

```
1. Validate(definition) -> defects. Non-empty -> Result.Failure, one Defect per finding.
2. hash = ClassDefinitionHashing.ComputeContentHash(definition)
3. streamId = ClassDefinitionStreamId.From(hash)
4. append ClassDefinitionPublished(hash, definition, clock.UtcNow) with ExpectedVersion.NoStream
5. an append failure with code "eventStore.streamAlreadyExists" -> Result.Success(hash) anyway:
   identical content already published is a safe no-op — ClassDefinitionEvents.cs's own
   comment says so ("republishing identical content targets the same stream and is a safe
   no-op at the domain level"), and MartenEventStore already returns exactly this code for a
   NoStream append against an existing stream (MartenEventStore.cs), so no new adapter
   behaviour is needed — only the handler choosing not to treat this one code as failure.
   Any OTHER append failure still propagates as Result.Failure.
```

- `GetClassDefinition(string ContentHash) : IQuery<ClassDefinition>` — folds the stream via
  `IEventStore`, same pattern as `GetPerson`.
- `FindClassDefinitions(string? Name, bool ActiveOnly) : IQuery<IReadOnlyList<ClassDefinitionSummary>>`
  → `IClassLibraryQuery`.

**Verify:** Application tests via `IDispatcher`, a fake `IEventStore`, a fake clock. Cover:
a valid definition publishes once; publishing the same definition twice returns the same
hash both times and only one event is recorded by the fake store (assert against its
recorded events, not just the `Result`); an invalid definition's `Result` carries every
defect `Validate()` found, not just the first.

### WI-5 — Marten wiring (Infrastructure)

Extends `ServiceCollectionExtensions.cs` exactly the way it already does for Person — no new
pattern:

- `opts.Events.MapEventType<ClassDefinitionPublished>("classDefinitionPublished")`, same for
  `ClassDefinitionRetired`, using the `$kind` strings already declared on
  `ClassDefinitionEvents.cs`.
- `opts.Projections.Add(new ClassDefinitionSummaryProjection(), ProjectionLifecycle.Inline)`
  — Inline, never the async daemon (LADR-0001 §2), even though `class_library` enforces no
  uniqueness invariant the way `PersonSummary.Email` does: read-your-own-writes on
  `POST /publish-class-definition` immediately followed by `GET /class-definitions` still
  needs it.
- **No unique index is required** on `class_library`. The content hash's uniqueness is
  already the Marten stream key (`ExistingStreamIdCollisionException`, handled in WI-4), not
  a document-level constraint.

**Verify:** none new here — folded into WI-7's store-backed tests.

---

## Phase C — Api and end-to-end verification

### WI-6 — Api endpoints

Through the existing `MapCommand`/`MapQuery` helpers only
(`src/Soarscore.Api/Routing/EndpointRouteBuilderExtensions.cs`) — no new routing surface.

- `POST /publish-class-definition` — body is a `ClassDefinition`, bound through the WI-1
  ingestion options. Returns the content hash on success; a `Validate()` failure returns
  every `Defect` in the `ProblemDetails` body — verify this renders as a list, not just the
  first (LADR-0002 §4).
- `GET /class-definitions?name=…&activeOnly=…` → `FindClassDefinitions`.
- `GET /class-definition?hash=…` → `GetClassDefinition`, folding the stream — same
  "query by id folds the stream" rule as `GET /person?id=…`.

### WI-7 — Store-backed tests

New tests in the existing `tests/Soarscore.Infrastructure.Tests` project (Testcontainers,
`Trait("Category", "Storage")`):

1. Publish → read round-trip preserves the full `ClassDefinition`, including every
   polymorphic `ScoreTerm`/`Predicate`/`FlightSelection` subtype and every
   `NumberOrParam`/`FlagOrParam` slot — the richest payload the event log holds, and the one
   `SoarscoreEventJson.Options`'s converters exist for.
2. Publishing identical content twice appends exactly one event and both calls return the
   same hash — the LADR-0002 §1 idempotency proof.
3. `class_library` is dropped and fully replayed from the log and lands identical
   (LADR-0001 §4.10), same discipline as the command-side plan's people test.

### WI-8 — Seed corpus through the real ingestion path

The proof LADR-0002 §1 demands: *"seed classes must enter through the same door as user
classes."* Drive all fifteen seed JSON files (`tools/Soarscore.SeedData/json/`) through the
actual `PublishClassDefinitionHandler` (an Application-level test — a fake store is enough,
since this exercises `Validate()` and the handler, not Marten) and assert zero defects for
every one. This is also where Phase A's "all fifteen validate clean" claim gets its real,
end-to-end assertion rather than a hand-picked subset.

### WI-9 — End-to-end verification

Same shape as the command-side plan's WI-10, against a running API and PostgreSQL:

- `POST /publish-class-definition` with a corpus definition → 200, the content hash.
- The same request again → 200, the *same* hash, and `class_library` still has one row.
- A hand-corrupted definition (e.g. break check 8's adjacent-band rule) →
  `ProblemDetails` naming check 8's code and path.
- `GET /class-definitions` lists it; `GET /class-definition?hash=…` returns it, folded from
  the stream.

---

## Dependency order

```
WI-1 ─┐ (independent, parallelisable)
WI-2 ─┘
WI-3 ── independent of WI-1/2
WI-4 ── needs WI-2, WI-3 (and the ClassDefinitionStreamId relocation it carries)
WI-5 ── needs WI-4
WI-6 ── needs WI-4, WI-5, WI-1 (ingestion options)
WI-7 ── needs WI-5
WI-8 ── needs WI-4
WI-9 last
```

## What this unlocks

`CreateCompetition` (`create-competition-steel-thread-plan.md`) can adopt a real, validated
definition by content hash — `AdoptedRules` stops being a value object with nowhere to
source its copy from, and that plan's handler folds the `PublishedClassDefinition` stream
this thread makes appendable. It also gives the `fai-rules` skill's compliance-checking a
live, POSTed definition to check against instead of only the seed corpus.
