# Plan — The field: `RegisterCompetitor` and `WithdrawCompetitor`

**Status:** Complete — implemented and test-verified · **Date:** 2026-08-06

Work items are numbered `WI-n`, scoped to *this* plan document (see
`command-side-steel-thread-plan.md`'s numbering note — WI numbers reset per plan).

## Context

`create-competition-steel-thread-plan.md` ended with a Competition that exists, holds a
real rulebook copy, and has an empty field. This plan fills the field: the two mutations
`aggregate-roots.md` §3 names first in its mutation list — *register or withdraw a
competitor*.

**This is the last cheap thread before the expensive one.** It is deliberately taken
before `PhaseDrawn` because the draw allocates *from* the field: `aggregate-roots.md`'s
`Draw "1" --> "2..*" Competitor : allocates`. There is nothing to draw until competitors
exist, and no way to test a draw's fairness invariant without a field to permute.

**Most of the work is already done.** Reused as-is, not rebuilt:

- `CompetitorRegistered` and `CompetitorWithdrawn`
  (`src/Soarscore.Domain/Competitions/CompetitionEvents.cs`), and both folds
  (`Competition.cs` `Apply(CompetitorRegistered)` / `Apply(CompetitorWithdrawn)`).
- The `Competitor` record itself — `Id`, `PersonRef`, `CompetitorNumber`, `RegisteredAt`,
  `WithdrawnAt` — matching `aggregate-roots.md` §3's class diagram one-for-one.
- `CompetitionLoader` and `PersonLoader`, the read-fold-decide-append handler template
  (`RenamePerson.cs`), `MapCommand`, and the whole Marten/Api adapter stack.
- `CompetitionProjection`'s pass-through default arm — deliberately written to tolerate
  exactly this situation (`CompetitionProjection.cs:19-27`), so **the `competitions` read
  model needs no change at all this thread.** That was the point of writing it that way.

**What is genuinely missing:** two decide functions, two commands with handlers, two
`MapEventType` registrations, two endpoints, and the tests.

### Two invariants this thread is really about

Both are worth stating up front because they are the reason the thread has property tests
rather than only examples.

1. **One registration per `PersonId` per competition.** Aggregate-local: the field lives
   *inside* the Competition aggregate precisely so it can be checked as one consistent set
   (`aggregate-roots.md`:197). Folding the stream and rejecting a duplicate in the decide
   function is the correct enforcement point, and `ExpectedVersion.Exact(version)` — not
   the check — is what makes it race-free, per LADR-0001 §4.4.

2. **A competition cannot hold more competitors than there are registered people.** This
   one needs its scope stated carefully or the property is simply false: **per
   competition**, not system-wide. System-wide, total competitor records legitimately
   exceed total people, because one person enters many competitions over time. Scoped per
   competition it is not a third check to implement — it is a *derived consequence* of
   invariant 1 plus WI-3's `PersonRef`-must-exist check, and that is exactly why it is
   worth asserting as a property: it tests that the two local checks compose into the
   global guarantee, which no example test can show.

### Out of scope (deliberately)

- `PhaseDrawn` and the draw algorithm. The next thread, and the one where CLAUDE.md's core
  architectural law does real work (group sizes, rounds and phases must come from
  `ClassDefinition`'s `PhaseDefinition` / `RoundComposition` / `GroupConstraint`, never
  from a branch on class).
- A competitor-count column on `CompetitionSummary`. Same reasoning that kept `State` out
  of the last thread: no view needs it yet, and `GetCompetition` already returns the whole
  folded `Competition` including `Competitors`.
- A "who is in this competition, by name" query — that is a join from `Competitor.PersonRef`
  to the `people` read model. The API returns ids; a client resolves names via `/person`.
  Add the joined view when a screen actually needs it, not before.
- Editing a registration (changing a competitor number, un-withdrawing). Neither appears in
  `aggregate-roots.md` §3's mutation list; withdrawal is one-way by design.
- Anything Entry-shaped, and `entry_index` — still gated behind the draw, not behind this.

### Governing documents

`docs/aggregate-roots.md` §3 (the `Competitor` shape, the field-freeze note at :330, and
the "field is who is in the competition" callout at :327),
`docs/ladr/ladr-0001-event-store.md` §4.4 (concurrency) and §4.8 (`MapEventType`),
`docs/soaring-domain-class-diagram.md` (`Competition "1" *-- "0..*" Competitor : field`,
`Competitor ..> Person : registration of`).

**No new domain concepts.** `Competitor`, `CompetitorId` and `CompetitorNumber` are all
already in the glossary and the class diagram; this thread adds no vocabulary and needs no
approval under CLAUDE.md's glossary rule.

### What the rules do and do not say (checked, not assumed)

Searched `docs/rules/` via the `fai-rules` skill for field composition — competitor
numbering, minimum/maximum entrants, double entry:

- **The FAI rules are silent on all three.** Per the skill's discipline, silence is a
  Contest Director decision, not a gap to fill by inference from another class. Concretely:
  **do not add a minimum- or maximum-field-size check**, and sequential competitor
  numbering below is a *product* decision, not a rule-derived constant — so it carries no
  `# ref` comment, because there is no ref to carry.
- **Team separation** (`C.16.2.6`) is the one field-adjacent rule, and
  `docs/rules/00-general-rules.md`:15-21 puts it **out of MVP software scope** along with
  frequency management. This closes the only real open question this thread had:
  `Competitor` needs no team/nation attribute, and the draw thread will not need one either.

---

## Phase A — Domain

### WI-1 — `RegisterCompetitor` and `WithdrawCompetitor` decide functions

Both are **instance** methods on `Competition` (like `Person.Rename`, unlike the static
`Competition.Decide`) — each needs the current field to decide.

```csharp
public Result<CompetitorRegistered> RegisterCompetitor(CompetitorId id, PersonId personRef, DateTimeOffset at)
public Result<CompetitorWithdrawn> WithdrawCompetitor(CompetitorId competitorRef, DateTimeOffset at)
```

**`CompetitorId` is minted by the handler and passed in**, not minted here — the same
choice `Person.Register` and `Competition.Decide` already make. Keeping decide functions
deterministic is what lets WI-2's property tests compare an expected event against an
actual one; a decide function that mints its own id cannot be property-tested for equality.

**`CompetitorNumber` is allocated here**, because only the aggregate knows the field:
`Competitors.Select(c => c.CompetitorNumber).DefaultIfEmpty(0).Max() + 1`. Use max+1, not
`Count + 1`: they agree today only because withdrawal never removes a record, and max+1
stays correct if that ever stops being true. **Numbers are never reused** — a withdrawn
competitor's number stays retired, because it has already been written on score sheets.

`RegisterCompetitor` checks, in order:

| Check | Code | Notes |
|---|---|---|
| `personRef` not already in the field | `competition.competitor.alreadyRegistered` | Invariant 1. Compares against *all* competitors, including withdrawn ones — a withdrawal is not a re-entry ticket |
| Field not frozen by an accepted draw | `competition.field.frozen` | `aggregate-roots.md`:330 |

The field-freeze check is **unreachable this thread** — `Phases` is always empty because no
command produces `PhaseDrawn` yet. Write it anyway, exactly as `CreateCompetition`'s
retirement check was written against a state nothing could yet produce. Implement it as
`!Phases.IsEmpty` and leave a comment saying that "accepted" currently means "any phase
drawn", to be revisited when `Draw.Status` gains a defined value set (`Competition.cs`:230-234
records that the status vocabulary is deliberately unspecified).

`WithdrawCompetitor` checks:

| Check | Code |
|---|---|
| `competitorRef` is in the field | `competition.competitor.notFound` |
| Not already withdrawn | `competition.competitor.alreadyWithdrawn` |

**Withdrawal is deliberately not subject to the field freeze** — the asymmetry is the rule:
*"After that a withdrawal is recorded but leaves the draw intact"* (`aggregate-roots.md`:330-333).
Registration closes at the draw; withdrawal never closes. Comment this, because it looks
like an oversight and is not.

**Verify:** `CompetitionDecideTests` — happy path for both; each failure code; numbers
allocate 1, 2, 3 across successive registrations; withdrawing then re-registering the same
person is rejected; withdrawal succeeds against a competition with a drawn phase while
registration against the same competition is rejected.

### WI-2 — Property test: one registration per `PersonId` (invariant 1)

New `CompetitionFieldPropertyTests` in `tests/Soarscore.Domain.Tests`, CsCheck, in the
model-based style `CompetitionModelBasedFoldTests.cs` already establishes.

Generate a pool of 1..8 `PersonId`s and a sequence of 0..30 registration attempts drawn
from it **with deliberate repetition** (a small pool over a long sequence is what makes
duplicates frequent rather than rare — do not generate fresh ids per attempt, or the
property never exercises the check). Fold each accepted event onto the aggregate; discard
each rejection. Assert after every step:

- `Competitors.Select(c => c.PersonRef).Distinct().Count() == Competitors.Length`
- `Competitors.Length <= pool.Length` — the per-competition form of invariant 2, at the
  domain level where the "registered people" set is the generator's pool
- competitor numbers are exactly `1..Competitors.Length`, distinct, in registration order
- every rejection carried code `competition.competitor.alreadyRegistered`, and its
  `personRef` was genuinely already present

Interleave withdrawals into the same sequence in a second property: withdrawal must never
shrink `Competitors`, never change any `CompetitorNumber`, and never make a
previously-registered `PersonRef` registrable again.

**Verify:** the tests are the deliverable.

---

## Phase B — Application

### WI-3 — `RegisterCompetitor` / `WithdrawCompetitor` commands and handlers

```csharp
public sealed record RegisterCompetitor(CompetitionId CompetitionId, PersonId PersonId) : ICommand<CompetitorId>;
public sealed record WithdrawCompetitor(CompetitionId CompetitionId, CompetitorId CompetitorId) : ICommand<CompetitorId>;
```

Both return `CompetitorId` — for registration it is the newly minted id the caller needs;
for withdrawal it echoes the input, following `RenamePerson`'s echo-the-id convention.

`RegisterCompetitorHandler` is the `RenamePerson` template plus one cross-aggregate read:

1. `CompetitionLoader.LoadAsync` → `(competition, version)`.
2. **`PersonLoader.LoadAsync` to confirm the person exists** → on failure,
   `registerCompetitor.personNotFound`. This is a cross-aggregate *read*, the same shape
   and the same justification as `CreateCompetition`'s class-definition lookup
   (`CreateCompetition.cs`:8-14) — LADR-0001 §4.4 forbids read-check-write as the
   *concurrency arbiter*, which this is not. `PersonLoader` is `internal` to
   `Soarscore.Application`, so this needs **no new port**; do not add one.
   *Note the residual race this accepts:* a Person cannot be deleted
   (`Person.cs`:65 — "never conceptually deleted"), so the only way this check can go stale
   is a person who does not exist yet, which the check already rejects. The read is sound
   precisely because Person has no delete.
3. Mint `CompetitorId.New()`, call `competition.RegisterCompetitor(...)`.
4. Append with `ExpectedVersion.Exact(version)` — the arbiter for two organisers
   registering the same person concurrently. Under contention one append fails with
   `eventStore.concurrencyConflict` and the caller retries into the now-visible duplicate
   check. **Do not** add a retry loop here; no other handler has one.

`WithdrawCompetitorHandler` is the plain template — load, decide, append. No person lookup:
withdrawal addresses a `CompetitorId` that, by construction, is already in the field.

**Verify:** `RegisterCompetitorHandlerTests` / `WithdrawCompetitorHandlerTests` against
`FakeEventStore` — success appends exactly one event at the right version; unknown
competition → `competition.notFound`; unknown person → `registerCompetitor.personNotFound`;
duplicate → the domain code surfaces unchanged through the handler; a stale version →
`eventStore.concurrencyConflict`.

### WI-4 — Property test: no more competitors than registered people (invariant 2)

The handler-level companion to WI-2, and the one that actually tests the composition. In
`tests/Soarscore.Application.Tests/Competitions`:

Seed a `FakeEventStore` with `N` (1..8) real `PersonRegistered` streams and one
`CompetitionCreated`. Fire a generated sequence of `RegisterCompetitor` commands whose
`PersonId`s are drawn from a mix of **the N real ids and freshly minted bogus ones**, then
fold the competition stream and assert:

- `Competitors.Length <= N` — never exceeds the registered population, no matter the
  command sequence
- every `PersonRef` is one of the N real ids (no bogus id ever lands)
- every rejection is either `registerCompetitor.personNotFound` (bogus) or
  `competition.competitor.alreadyRegistered` (duplicate) — nothing fails for a third reason

State in the test's header comment why the bound is per-competition and not system-wide, so
the next reader does not "fix" it into a global assertion that is false.

**Verify:** the test is the deliverable.

### WI-5 — Marten wiring and event JSON

- `MartenConfig.cs`: add `MapEventType<CompetitorRegistered>("competitorRegistered")` and
  `MapEventType<CompetitorWithdrawn>("competitorWithdrawn")`, and strike both from the
  comment at `MartenConfig.cs`:38-47 that lists the not-yet-registered subtypes. That
  comment tells this thread to do exactly this; leaving it stale is the failure mode.
- `CompetitionEventJsonTests`: round-trip both events. `CompetitorRegistered` carries a
  whole `Competitor`, whose `PersonRef` is a `PersonId` record struct — it serialises as a
  nested `{"value":"…"}`, consistent with how `CompetitionCreated.Id` already round-trips.
  Assert the shape rather than changing it; a converter to flatten these is a separate,
  log-wide decision, not something to slip in here.

**Verify:** the JSON tests, plus WI-6's store-backed round-trip.

---

## Phase C — Api and verification

### WI-6 — Api endpoints

Through the existing `MapCommand` helper only:

- `POST /register-competitor`
- `POST /withdraw-competitor`

No new queries — `GET /competition?id=…` already returns the folded `Competition` with its
`Competitors` array.

### WI-7 — Store-backed tests

`tests/Soarscore.Infrastructure.Tests`, Testcontainers, `Trait("Category", "Storage")`:

1. Register three competitors against a real competition; read the stream back; the field
   folds to three, numbered 1-3, in order.
2. The same person twice is rejected against real PostgreSQL — proving the check survives
   the real append path, not just the fake.
3. Withdraw one; the record persists with `WithdrawnAt` set and the field still holds three.
4. `competitions` is dropped and fully replayed with the two new event types in the log and
   lands identical (LADR-0001 §4.10) — the direct test of `CompetitionProjection`'s
   pass-through arm now that it has real events to pass through.

### WI-8 — End-to-end verification

Against a running API and PostgreSQL, in order: publish a class definition → create a
competition → `POST /register-person` twice → `POST /register-competitor` for each → `GET
/competition?id=…` shows both with numbers 1 and 2 → registering the first person again
returns `ProblemDetails` with `competition.competitor.alreadyRegistered` → registering a
bogus `PersonId` returns `registerCompetitor.personNotFound` → `POST /withdraw-competitor`
→ `GET /competition` shows `withdrawnAt` set and the field still two long.

---

## Dependency order

```
WI-1 ── first (both decide functions)
WI-2 ── needs WI-1
WI-3 ── needs WI-1
WI-4 ─┐ needs WI-3
WI-5 ─┘ (independent of each other, parallelisable)
WI-6 ── needs WI-3, WI-5
WI-7 ── needs WI-5
WI-8 last
```

## What this unlocks

`PhaseDrawn` — the fair round-by-round draw CLAUDE.md names as core to what this system is
— finally has an input. That thread is where the core architectural law is tested hardest:
the draw must read its phase count, round composition and group sizing from
`AdoptedRules.Definition` (`PhaseDefinition`, `RoundComposition`, `GroupConstraint`) and
branch on no class name anywhere. A promising signal, worth recording here so the next
thread starts from it: those three types already exist and already carry `MinGroupSize`,
`MaxGroupSize`, `MinNewGroupSize`, `TasksPerRound`, `MaxRounds` and
`RequireDistinctTaskPerRound`, so the draw looks expressible without touching the class
model at all. Entry and `entry_index` remain gated behind the draw, not behind this.
