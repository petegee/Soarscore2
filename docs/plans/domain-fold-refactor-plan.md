# Move aggregate events + fold logic into the Domain layer

## Why

`docs/high-level-architecture.md` was just amended (2026-08-05) to say the Domain
layer contains "aggregate roots and their entities/value objects, domain events,
business operations on aggregates, aggregate root event fold logic," and that
Domain "should not be an anemic domain." Application's job is now "domain
services, use cases, read-models, cross-aggregate orchestration, commands and
query handlers."

This corrects a call made earlier in the same build: event contracts and fold
("projection") logic for the four aggregate roots were built in
`Soarscore.Application`, reasoning from the pre-amendment HLA text and from
LADR-0001 §4.3 ("fold logic is plain functions in Application"). That reasoning
turned out weaker than it looked — LADR-0001's concern was keeping *Marten
types* out of Application, which holds regardless of whether fold logic sits in
Domain or Application (Domain already has zero `PackageReference`, and events
are plain records + in-box `System.Text.Json`, no package needed either way).
The anemic-domain concern is the deciding one, and this plan implements the fix:
relocate events and fold logic for all four aggregates, with `Apply` **on the
aggregate root itself**, not in a separate static projection class.

**Scope is deliberately narrow.** This plan moves *fold* logic (how an aggregate
reconstructs itself from its history) into Domain. It does **not** add
*command*-side business methods (e.g. a validating `Person.Rename(name)` that
enforces invariants and returns an event to append) — HLA's "business operations
on aggregates" phrase covers that too, but no command handlers exist yet for
those methods to serve, and inventing validation rules that were never asked for
risks getting them wrong. That is separate, larger, future work. Flag it if you
get partway through and it looks free — do not do it as a side effect here.

## What exists today (built in the session that produced this plan)

Four aggregates, each with event contracts + fold logic in `Soarscore.Application`,
a thin Marten wiring shim in `Soarscore.Infrastructure`, and fold/JSON tests in
`Soarscore.Application.Tests`:

| Aggregate | Application events | Application fold | Infrastructure shim | Tests |
|---|---|---|---|---|
| Person | `src/Soarscore.Application/People/PersonEvents.cs` | `.../People/PersonProjection.cs` | `src/Soarscore.Infrastructure/People/PersonProjectionShim.cs` | `tests/Soarscore.Application.Tests/People/PersonProjectionTests.cs` |
| Competition | `src/Soarscore.Application/Competitions/CompetitionEvents.cs` | `.../Competitions/CompetitionProjection.cs` | `src/Soarscore.Infrastructure/Competitions/CompetitionProjectionShim.cs` | `tests/Soarscore.Application.Tests/Competitions/CompetitionProjectionTests.cs` |
| Entry | `src/Soarscore.Application/Entries/EntryEvents.cs` | `.../Entries/EntryProjection.cs` | `src/Soarscore.Infrastructure/Entries/EntryProjectionShim.cs` | `tests/Soarscore.Application.Tests/Entries/EntryProjectionTests.cs` |
| CompetitionClass | `src/Soarscore.Application/CompetitionClasses/ClassDefinitionEvents.cs` | `.../CompetitionClasses/ClassDefinitionProjection.cs` | `src/Soarscore.Infrastructure/CompetitionClasses/ClassDefinitionProjectionShim.cs` | `tests/Soarscore.Application.Tests/CompetitionClasses/ClassDefinitionProjectionTests.cs` |

Every event hierarchy already follows one convention (mirrors
`src/Soarscore.Domain/CompetitionClasses/ScoringVocabulary.cs`'s `ScoreTerm`
pattern): an abstract base with a `private protected` ctor, sibling (not
nested) sealed record subtypes, `[JsonPolymorphic(TypeDiscriminatorPropertyName
= "$kind")]` + `[JsonDerivedType]` with camelCase `$kind` strings. Every fold
class is a static `XProjection` with a switch-based `Apply(TDoc? current,
TEventBase @event)` dispatcher plus one overload per concrete event type. Read
one pair of files (e.g. `PersonEvents.cs` + `PersonProjection.cs`) before
starting — they are the actual pattern being relocated, more precise than any
description of it here.

Two files are explicitly **out of scope** and stay where they are, unchanged
except for `using` statements:

- `src/Soarscore.Application/CompetitionClasses/ClassDefinitionHashing.cs` —
  content-hash computation for the ingestion pipeline (LADR-0002 §4:
  deserialise → validate → canonicalise+hash → append). This is use-case
  ("how do we ingest a POSTed definition") logic, not "how does the aggregate
  fold its own history" logic — it belongs to a future `PublishClassDefinition`
  command handler, which is what makes it an Application concern under the
  amended HLA, not a leftover from the old placement.
- `src/Soarscore.Infrastructure/CompetitionClasses/ClassDefinitionStreamId.cs` —
  the derived-Guid Marten stream key, a pure Infrastructure storage concern
  (see that file's own header comment for why). Update its `using` from
  `Soarscore.Application.CompetitionClasses` to `Soarscore.Domain.CompetitionClasses`
  once `ClassDefinitionPublished` moves; nothing else about it changes.

`src/Soarscore.Application/EventJson.cs` (`SoarscoreEventJson.Options` — the
`$kind`/decimal-as-string/`NumberOrParam` JSON conventions) also **stays in
Application**. It configures *serialisation for storage*, which is a fair
Application/Infrastructure-adjacent concern even though the types it serialises
now live in Domain — exactly parallel to how `NumberOrParamConverter` lives in
Domain today but SeedData's ingestion-specific `JsonSerializerOptions` variants
stay in the SeedData tool. Domain must never reference `System.Text.Json`
options bundles or configure serialisation policy for itself; it only carries
the `[JsonPolymorphic]`/`[JsonDerivedType]` attributes that are already, by
precedent (`ScoringVocabulary.cs`), accepted as "the model's own wire shape,"
not an adapter concern.

## Target shape — the pattern every aggregate follows

Using Person as the worked example. This exact shape is what WI-1 through WI-4
below implement for each aggregate; read it once here rather than have it
re-derived four times.

```csharp
// src/Soarscore.Domain/People/PersonEvents.cs — namespace Soarscore.Domain.People
// (moved verbatim from Application, only the namespace and `using`s change)

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(PersonRegistered), "personRegistered")]
[JsonDerivedType(typeof(ContactDetailsChanged), "contactDetailsChanged")]
[JsonDerivedType(typeof(ClubAffiliationChanged), "clubAffiliationChanged")]
[JsonDerivedType(typeof(PersonRenamed), "personRenamed")]
public abstract record PersonEvent
{
    private protected PersonEvent() { }
}

public sealed record PersonRegistered(PersonId Id, string Name, ContactDetails Contact, ClubAffiliation? Club, DateTimeOffset At) : PersonEvent;
public sealed record ContactDetailsChanged(ContactDetails Contact, DateTimeOffset At) : PersonEvent;
public sealed record ClubAffiliationChanged(ClubAffiliation? Club, DateTimeOffset At) : PersonEvent;
public sealed record PersonRenamed(string Name, DateTimeOffset At) : PersonEvent;
```

```csharp
// src/Soarscore.Domain/People/Person.cs — Apply added to the existing record.
// PersonProjection.cs is deleted; its logic becomes these methods.

public sealed record Person
{
    public required PersonId Id { get; init; }
    public required string Name { get; init; }
    public required ContactDetails Contact { get; init; }
    public ClubAffiliation? Club { get; init; }

    /// <summary>The creation event. Every stream begins with exactly one of these.</summary>
    public static Person Create(PersonRegistered @event) => new()
    {
        Id = @event.Id,
        Name = @event.Name,
        Contact = @event.Contact,
        Club = @event.Club,
    };

    // One overload per non-creation event — both the domain's own fold-by-type
    // API *and*, unchanged from today's Infrastructure shim, exactly what
    // Marten's conventional-method discovery on SingleStreamProjection<TDoc,TId>
    // matches on. See WI-0 before assuming these overloads stay unused by Marten.
    public Person Apply(ContactDetailsChanged @event) => this with { Contact = @event.Contact };
    public Person Apply(ClubAffiliationChanged @event) => this with { Club = @event.Club };
    public Person Apply(PersonRenamed @event) => this with { Name = @event.Name };

    /// <summary>
    /// Generic replay entry point — same signature `PersonProjection.Apply` had,
    /// so `tests`/`stream.Events.Aggregate(...)`-style callers barely change.
    /// Not what Marten calls (Marten calls the typed overloads above via its own
    /// conventional-method discovery, or via a shim — see WI-0); this is for code
    /// that only has the closed union type, not the concrete event type.
    /// </summary>
    public static Person? Apply(Person? current, PersonEvent @event) =>
        @event switch
        {
            PersonRegistered registered => Create(registered),
            ContactDetailsChanged or ClubAffiliationChanged or PersonRenamed =>
                current is null
                    ? throw new ArgumentException($"{@event.GetType().Name} folded with no current projection — a change event can never be first in the stream.")
                    : current.Apply((dynamic)@event), // see note below — prefer explicit switch arms per concrete type over `dynamic`
            _ => throw new ArgumentException($"Unknown PersonEvent subtype: {@event.GetType().Name}"),
        };
}
```

**Do not actually use `dynamic`** in the static dispatcher above — it was left
in only to show the shape being replaced; write it as an explicit switch with
one arm per concrete type calling the matching instance overload, exactly as
`PersonProjection.Apply`'s dispatcher does today:

```csharp
public static Person? Apply(Person? current, PersonEvent @event) =>
    @event switch
    {
        PersonRegistered registered => Create(registered),
        ContactDetailsChanged e => Require(current, e).Apply(e),
        ClubAffiliationChanged e => Require(current, e).Apply(e),
        PersonRenamed e => Require(current, e).Apply(e),
        _ => throw new ArgumentException($"Unknown PersonEvent subtype: {@event.GetType().Name}"),
    };

private static Person Require(Person? current, PersonEvent @event) =>
    current ?? throw new ArgumentException($"{@event.GetType().Name} folded with no current projection — a change event can never be first in the stream.");
```

Three method groups, all on the aggregate root, all satisfying "Apply lives on
the aggregate root": `Create` (static, the one creation event), `Apply`
(instance, one overload per non-creation event — this is what a caller holding
a concrete event type uses, and what Marten's convention wants), `Apply`
(static, nullable-current + closed union — this is what generic replay code and
most tests use, byte-for-byte the same call shape `XProjection.Apply` had).

## WI-0 — Resolve the Infrastructure shim question (orchestrator only, do this first)

**Do not fork this one out.** It decides a shared fact all four aggregate WIs
below need, and four independent investigations risk four different answers.

**Question:** can Marten's `SingleStreamProjection<TDoc, TId>` (or an
equivalent Marten registration path) discover conventional `Create`/`Apply`
methods declared directly on the aggregate type itself (`Person`), without a
separate wrapper class — i.e. can `PersonProjectionShim.cs` be deleted entirely
and `Person` registered directly? Or does Marten always require a dedicated
subclass, in which case the shim survives but shrinks to pure one-line
pass-throughs calling `Person.Apply(...)`/`Person.Create(...)` instead of
`PersonProjection.Apply(...)`?

**How to find out:** the previous session resolved the *shape* of
`SingleStreamProjection<TDoc,TId>`'s conventional methods by reflecting on the
installed Marten package rather than trusting recalled API knowledge — do the
same here. `dotnet build` alone won't answer this; you need either (a) Marten's
own documentation/source for a self-aggregating-type registration API (search
`Marten.Events.Projections` / `StoreOptions.Projections` for something like
`LiveStreamAggregation<T>()` that takes a bare type, vs. requiring a
`SingleStreamProjection<TDoc,TId>` subclass), or (b) a scratch project (see
`/tmp/.../scratchpad` conventions) that actually registers `Person` both ways
and confirms which compiles/works. Time-box this to well under an hour — it's a
"which of two known-working shapes do we use," not an open research question.

**Deliverable:** a two-or-three-sentence finding recorded at the top of WI-1
(edit this file), plus the decision. Both outcomes are fine; do not let this
block starting WI-1..WI-4's event/fold relocation, which is correct either way
— only the exact contents of each `Infrastructure/{X}/{X}ProjectionShim.cs`
file depend on the answer.

## WI-0 finding

Marten 9.22.2 supports registering a self-aggregating projection directly
against a bare type via `StoreOptions.Projections.Snapshot<T>(SnapshotLifecycle,
...)` (`Marten.Events.Projections.ProjectionOptions.Snapshot<T>`, confirmed from
the installed package's XML docs: "Perform automated snapshot on each event for
selected entity type" — takes only `T`, no wrapper/subclass argument). This uses
the same conventional `Create`/`Apply` method discovery that
`SingleStreamProjection<TDoc,TId>` subclasses rely on internally, so a bare
`Person`/`Competition`/`Entry`/`PublishedClassDefinition` with `Create`/`Apply`
methods on itself is a complete, valid Marten registration on its own.
**Decision: delete the four `*ProjectionShim.cs` files entirely** (option (a) in
WI-0's question) rather than shrink them to pass-throughs — nothing in the repo
wires up `StoreOptions` yet (no `AddMarten`/`DocumentStore` call exists today),
so there is no call site to migrate, only files to remove. Future Infrastructure
wiring registers each aggregate with `opts.Projections.Snapshot<Person>(SnapshotLifecycle.Inline)`
(and equivalent for the other three) directly against the Domain type.

## WI-1 — Person

**Files:**
- Move `src/Soarscore.Application/People/PersonEvents.cs` → `src/Soarscore.Domain/People/PersonEvents.cs`. Change namespace `Soarscore.Application.People` → `Soarscore.Domain.People`. Drop the `using Soarscore.Domain;` / `using Soarscore.Domain.People;` lines that are now same-namespace or redundant.
- Fold `src/Soarscore.Application/People/PersonProjection.cs`'s logic into `src/Soarscore.Domain/People/Person.cs` per the target shape above, then delete `PersonProjection.cs`. Delete the now-empty `src/Soarscore.Application/People/` folder.
- Update `src/Soarscore.Infrastructure/People/PersonProjectionShim.cs` per WI-0's finding: either delete it (if Marten self-aggregates a bare type) or rewrite its four methods as one-line calls to `Person.Create(...)` / `Person.Apply(...)` instead of `PersonProjection.Apply(...)`.
- Move `tests/Soarscore.Application.Tests/People/PersonProjectionTests.cs` → `tests/Soarscore.Domain.Tests/PersonFoldTests.cs` (flat — `Soarscore.Domain.Tests` has no per-aggregate subfolders today, see `PersonTests.cs`/`CompetitionTests.cs`/`EntryTests.cs` sitting directly in the project root; match that). Rewrite calls from `PersonProjection.Apply(...)` to `Person.Apply(...)` / `Person.Create(...)`. **Drop the two JSON-round-trip facts** (`Events_round_trip_through_SoarscoreEventJson_byte_for_byte`, `Registered_event_serialises_with_the_kind_discriminator`) from this file — they test `SoarscoreEventJson.Options`, an Application-owned artifact, and belong in a new, much smaller `tests/Soarscore.Application.Tests/PersonEventJsonTests.cs` that references `Soarscore.Domain.People.PersonEvent` and asserts round-trip + `$kind` only (no fold assertions — those are already covered in Domain.Tests). Delete the now-empty `tests/Soarscore.Application.Tests/People/` folder.

**Acceptance:** `dotnet build` clean (0 warnings — `TreatWarningsAsErrors=true`); `dotnet test tests/Soarscore.Domain.Tests/Soarscore.Domain.Tests.csproj --filter "FullyQualifiedName~PersonFold"` and the existing `PersonTests` both green; the new `PersonEventJsonTests` in Application.Tests green.

## WI-2 — Competition

Same shape as WI-1, more moving parts:

- Events: `src/Soarscore.Application/Competitions/CompetitionEvents.cs` → `src/Soarscore.Domain/Competitions/CompetitionEvents.cs`, namespace → `Soarscore.Domain.Competitions`.
- Fold: `CompetitionProjection.cs`'s logic (11 event overloads) folds into `src/Soarscore.Domain/Competitions/Competition.cs`. The existing private helper for "navigate to Phase→Round→TaskRound by ordinal and replace it" (shared by the reflight-group/task-round-completed/task-round-annulled events) becomes a `private` instance method on `Competition` — keep it as one shared helper, do not inline it three times.
- Shim: `src/Soarscore.Infrastructure/Competitions/CompetitionProjectionShim.cs` per WI-0's finding.
- Tests: `tests/Soarscore.Application.Tests/Competitions/CompetitionProjectionTests.cs` → `tests/Soarscore.Domain.Tests/CompetitionFoldTests.cs` (flat, alongside the existing `CompetitionTests.cs`), rewritten to call `Competition.Apply`/`Competition.Create`. Split out a small `tests/Soarscore.Application.Tests/CompetitionEventJsonTests.cs` for the JSON/decimal-as-string round-trip facts only (this aggregate's events embed `DeclaredResult.Aggregate`, a `decimal` — keep the test that checks it serialises as a JSON string, per LADR-0001 §4.6).
- Delete the emptied `Application/Competitions/` folder.

**Acceptance:** same as WI-1, filtered to Competition.

## WI-3 — Entry

Same shape again:

- Events: `src/Soarscore.Application/Entries/EntryEvents.cs` → `src/Soarscore.Domain/Entries/EntryEvents.cs`, namespace → `Soarscore.Domain.Entries`.
- Fold: `EntryProjection.cs`'s logic (6 event overloads, plus the private `ReplaceFlight`/`ReplaceMeasurement` helpers) folds into `src/Soarscore.Domain/Entries/Entry.cs` as instance methods — keep both helpers as shared private methods, `MeasurementAmended`'s fold needs both nested.
- Shim: `src/Soarscore.Infrastructure/Entries/EntryProjectionShim.cs` per WI-0.
- Tests: `tests/Soarscore.Application.Tests/Entries/EntryProjectionTests.cs` → `tests/Soarscore.Domain.Tests/EntryFoldTests.cs` (flat, alongside `EntryTests.cs`), rewritten. Split out `tests/Soarscore.Application.Tests/EntryEventJsonTests.cs` for the JSON round-trip + decimal-as-string checks (this aggregate's `Measurement.Value.Number` is the other concrete decimal-as-string case worth keeping, alongside Competition's `DeclaredResult.Aggregate`).
- Delete the emptied `Application/Entries/` folder.

**Acceptance:** same as WI-1, filtered to Entry.

## WI-4 — CompetitionClass (do this one last, and carefully — it's not mechanical like the other three)

This aggregate differs from the other three in a way that matters here: unlike
`Person`/`Competition`/`Entry`, its aggregate-root type (`PublishedClassDefinition`)
was *invented in Application* in the same session that built everything else —
it never lived in Domain to begin with. `ClassDefinition` (the rulebook value
object/library type) already lives in Domain and is unaffected by this WI;
`PublishedClassDefinition` (id, content hash, the `ClassDefinition` it wraps,
published/retired timestamps) is the thing that needs to move, alongside its
events and fold.

- Events: `src/Soarscore.Application/CompetitionClasses/ClassDefinitionEvents.cs` → `src/Soarscore.Domain/CompetitionClasses/ClassDefinitionEvents.cs`, namespace → `Soarscore.Domain.CompetitionClasses`.
- Aggregate root + fold: move the `PublishedClassDefinition` record and fold logic out of `ClassDefinitionProjection.cs` into a new `src/Soarscore.Domain/CompetitionClasses/PublishedClassDefinition.cs`, with `Create`/`Apply` on the type per the target shape. Delete `ClassDefinitionProjection.cs`.
- **Decide what to do with the `Guid Id` property.** It exists solely so Marten's document-identity convention has something to bind to (see the property's own doc comment in the current `ClassDefinitionProjection.cs` for the full reasoning) — `ContentHash` is the actual business identity. Putting `PublishedClassDefinition` in Domain makes this property a small, deliberate Infrastructure-convention leak into a pure layer. Two ways to resolve it, pick one and note which in the commit/PR:
  1. Accept it, documented as-is (simplest — one ugly, well-commented property beats a wrapper type).
  2. Push it back out: keep `Guid Id` off `PublishedClassDefinition` entirely, and have `ClassDefinitionProjectionShim` (or its replacement per WI-0) carry a private Infrastructure-only wrapper that adds the `Id` around the pure Domain type. More layers, fully pure Domain.

  If genuinely unsure, stop and ask rather than guessing — this is exactly the kind of judgment call CLAUDE.md asks to surface rather than silently resolve.

  **Decision taken: option 1** (accept `Guid Id`, documented as-is on `PublishedClassDefinition`). WI-0's finding already eliminated the per-aggregate Infrastructure wrapper-type shape for all four aggregates; introducing one back here — solely to carry this one field — would cut against the symmetry the rest of this refactor establishes, for a property nothing in Domain or Application ever reads.
- Shim/stream-key: `src/Soarscore.Infrastructure/CompetitionClasses/ClassDefinitionProjectionShim.cs` per WI-0's finding, referencing `PublishedClassDefinition`'s new location. `ClassDefinitionStreamId.cs` needs only its `using` updated (see "out of scope" note above) — do not otherwise change it.
- `ClassDefinitionHashing.cs` stays in Application, untouched except (if needed) its `using Soarscore.Domain.CompetitionClasses;` for `ClassDefinition` (already correct — it never referenced `PublishedClassDefinition` or the events, only `ClassDefinition` itself, so check whether any edit is even needed before making one).
- Tests: `tests/Soarscore.Application.Tests/CompetitionClasses/ClassDefinitionProjectionTests.cs` splits three ways:
  - Fold-only facts (`Published_creates_...`, `Retired_sets_RetiredAt_...`, `Retired_against_no_current_...`, `A_full_event_stream_folds_...`) → new `tests/Soarscore.Domain.Tests/ClassDefinitionFoldTests.cs` (flat), rewritten against `PublishedClassDefinition.Apply`/`.Create`.
  - Hashing-only facts (`ComputeContentHash_is_deterministic_...`) stay in Application.Tests — `ClassDefinitionHashing` didn't move.
  - JSON facts (`Events_round_trip_...`, `Decimals_serialise_...`, `Published_event_serialises_with_the_kind_discriminator`) → `tests/Soarscore.Application.Tests/ClassDefinitionEventJsonTests.cs`, referencing `Soarscore.Domain.CompetitionClasses.ClassDefinitionEvent`.
- Delete the emptied `Application/CompetitionClasses/` subfolder's moved files — the folder itself survives (it still holds `ClassDefinitionHashing.cs`).

**Acceptance:** same build/test bar as WI-1..3, filtered to CompetitionClass, plus explicit confirmation of which `Id`-property option was taken and why.

## WI-5 — Integration (orchestrator, after WI-1..4 land)

1. Full solution build: `dotnet build` from repo root, zero warnings.
2. Full test run: `dotnet test` from repo root — every project.
3. **Re-verify `Soarscore.Domain.csproj` still has zero `PackageReference`** (LADR-0003, "the Domain has ZERO PackageReference by design" — this is the one invariant most at risk of quietly breaking during this move; check the `.csproj` by eye, don't just trust the build succeeding, since a build can succeed with an added package that happens not to be used yet).
4. Confirm `Soarscore.Application/{People,Competitions,Entries}/` folders are gone, and `Soarscore.Application/CompetitionClasses/` contains only `ClassDefinitionHashing.cs`.
5. If WI-0 concluded shims can be deleted, confirm `Soarscore.Infrastructure/{People,Competitions,Entries}/` are gone or near-empty (whatever else, if anything, still legitimately belongs there).
6. Diff the total test count against the pre-refactor baseline (127 — 77 Domain + 50 Application as of the plan being written) as a sanity check that tests were relocated, not silently dropped: expect roughly the same total, redistributed between the two test projects rather than concentrated in Application.Tests.

## Process note, from how the original build went

The four aggregates split cleanly across parallel sub-agents last time (`isolation:
"worktree"`), each given the full pattern to mirror plus its own precise event
list — that worked well and is the right approach for WI-1 through WI-3 here too
(fork three, keep WI-4 for a closer look given the extra judgment call it
carries). One sub-agent stalled after finishing its work but before reporting
back; its worktree still held the finished, correct files, recoverable by
reading directly from `.claude/worktrees/agent-<id>/...`. Don't assume a
"failed" status means no usable work was produced — check the worktree before
redoing anything.
