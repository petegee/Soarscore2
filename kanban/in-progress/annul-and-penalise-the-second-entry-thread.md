# Story — The second Entry thread (annul and penalise)

**Status:** In progress — implementing · **Raised:** 2026-08-16 · **Re-scoped:** 2026-08-18 · **Planned:** 2026-08-21

## What

Two mutations the scoring pipeline can already *read* but nothing can yet *produce*:
`EntryAnnulled` and `PenaltyRecorded` — the last existing on both `EntryEvent` and
`CompetitionEvent`. Folds exist; decide functions, commands, handlers and endpoints
do not.

This thread closes both halves:

- **Annul an Entry** — `POST /annul-entry`. A ruling that one competitor's attempt in
  one task-round does not count (F3F.1.5's provisional re-flight is the canonical
  shape: the competitor re-flies, the jury later annuls one of the two attempts).
- **Record a penalty against an Entry** — `POST /record-entry-penalty`. Flight/Entry
  scoped infractions (F5K `motorRestartInFlight` → ZeroFlight, etc).
- **Record a penalty against a competitor** — `POST /record-competition-penalty`.
  TaskRound/Competition scoped infractions: final-aggregate point deductions and
  disqualifications (F3B.1.7.b/e/f, F5K 5.5.10.12 safety b).

The third command exists only because the `Penalty` payload is first extended with a
subject (see decision 1) — without it, a competition-scoped penalty would deduct from
*every* competitor in the field.

## Why it matters

`PenaltyEngine` runs over a penalty list that is always empty, and a mis-keyed flight
time is uncorrectable if an amendment is not the right fit. (That last one is already
in hand: corrections landed separately as `kanban/completed/amend-a-measurement.md`,
of which `MeasurementAmended` was the whole subject — so this thread no longer
names it.)

Penalties and annulments are ordinary events at a real contest — F3B.1.7 alone names
four distinct per-competitor deductions, and every ruleset has a safety-penalty
clause. Today the only way to record any of them is to not need one.

## Before starting

Close only the events this thread needs. "Close the remaining unreachable events" as a
goal in itself is motion without direction — the standing rule this repo has held to is
that each one closes when a command needs it.

**Runtime trap:** the event-type registry
(`src/Soarscore.Infrastructure/SoarscoreEventTypes.cs`) registers `EntryAnnulled` and
`PenaltyRecorded` only once a command appends them. Appending either without adding
its alias line fails at runtime, per LADR-0001 §4.8. The block comment above the
Entry slice already names both as the events that remain. See WI-9 — the alias
situation is subtler than the stub knew (finding 3).

---

# Plan

Written 2026-08-21 against the tree as it stood after `amend-a-measurement.md` and
`task-round-lifecycle.md` completed. File references cite `file:line`; re-verify
before acting on one, later commits move them.

## Decisions settled before planning (user, 2026-08-21)

1. **The whole thread ships, with the subject fix now.** The Competition-stream
   `PenaltyRecorded` has no `CompetitorRef`, and `ScoringService.ScoreCompetition`
   applies every TaskRound/Competition-scoped penalty to *every* competitor
   (`src/Soarscore.Domain/Scoring/ScoringService.cs:263-275`) — while every rule
   that creates such a penalty deducts from one competitor (F3B.1.7.b: "a deduction
   from the competitor's final score"). Rather than defer, the `Penalty` payload
   gains a nullable `CompetitorRef` in this thread: nothing appends the event yet,
   so changing its shape is free today and expensive the moment anything does.
2. **`Penalty` gains an optional `By`.** Recorded when the client supplies it, never
   required — clients are not forced to collect it. Mirrors `Annulment.By` /
   `Amendment.By` precedent (recorded, enforced on nobody's role) at
   "who recorded this infraction" strength. No `At`: the user scoped the audit
   addition to `By` only, and the append itself is logged.
3. **The decide functions validate `InfractionType` against the adopted class
   definition.** `PenaltyEngine` silently skips a penalty whose `InfractionType`
   matches no `PenaltyDefinition` (`PenaltyEngine.cs:41-42`) — the CD believes they
   penalised someone and nothing happens. The write path rejects it instead
   (`recordPenalty.infractionTypeNotDeclared`), mirroring
   `captureMeasurement.metricNotDeclared`. The engine's read-side tolerance stays:
   it is the safety net for events already in a log.
4. **Aggregate penalties carry an optional task-round coordinate.** F3B.1.7.b wants
   the deduction "listed on the score sheet of the round in which the penalisation
   was applied". Nothing reads the coordinate today (no score-sheet report exists);
   it is recorded when supplied, validated for existence, and read by nothing yet.
5. **The two `PenaltyRecorded` CLR types get distinct store aliases** —
   `entryPenaltyRecorded` and `competitionPenaltyRecorded` (finding 3). The JSON
   `$kind` discriminators stay `"penaltyRecorded"` on both unions: they never meet,
   each union deserialises within itself. The store alias is a *flat* on-disk
   identity (LADR-0001 §5's store-to-store migration replays by it) and two distinct
   CLR types cannot share one. **Planner's call, not a user decision — flag for
   veto when this plan is reviewed.**
6. **Annulment and penalty recording are deliberately NOT gated on task-round or
   competition state.** `OpenEntry` closes on `TaskRoundState.Complete/Annulled`
   (`Competition.cs:896-900`) because opening an Entry *creates* flight data; an
   annulment or a penalty is a *ruling about* recorded data, and the normal case is
   exactly a protest after the round looked finished (NFR-4's world; see
   `deferred-decisions.md`'s "Blocking score capture on `Finalised`" for the same
   stance). A lesser agent's instinct to mirror `openEntry.taskRoundClosed` here
   would break the feature's main workflow — do not.
7. **Re-annulment is allowed; the latest ruling stands.** The fold overwrites
   (`Entry.cs:220-221`), which is the right semantics for a jury revising a ruling.
   No `annulEntry.alreadyAnnulled` defect. Property P2 holds it true.
8. **A withdrawn competitor can still be penalised.** Withdrawal leaves scores intact
   (`aggregate-roots.md:330-333`, `Competition.cs:593-597`) — an aggregate deduction
   against a withdrawn competitor's accumulated score still deducts. Deliberately no
   `ValidateNotAlreadyWithdrawn` call, unlike `WithdrawCompetitor`.

## Findings from reading the tree

1. **The read side is finished for both events.** `EntryAnnulled` →
   `FlightSelector.SelectAndScore` step 0 returns `NoResult` regardless of captured
   data (`FlightSelector.cs:40-42`); the scoring reflight guard permits a second
   Entry per competitor+task-round exactly when all but one are annulled
   (`ScoringService.cs:168-179`). `PenaltyRecorded` on Entry →
   `GetEntryPenalties`/`ApplyRawPenalties` (raw stage, before normalisation);
   on Competition → `GetAggregatePenalties`/`ApplyAggregatePenalties` (after drops,
   before ranking). **Nothing downstream of the decide functions needs writing
   except the subject filter (WI-5).**
2. **The subject gap, precisely.** `GetAggregatePenalties` is computed once from
   `competition.Penalties` (`ScoringService.cs:263`, `:366-371`) and the identical
   `PenaltyApplication` (deduction *and* `Disqualified` flag) is applied inside the
   per-competitor loop (`ScoringService.cs:267-275`). As modeled, the only
   honestly-representable aggregate penalty is one that hits the whole field — no
   rule in either rulebook does that. WI-5 fixes it; P4 is the invariant that holds
   the fix true.
3. **The alias collision.** `Soarscore.Domain.Entries.PenaltyRecorded` and
   `Soarscore.Domain.Competitions.PenaltyRecorded` are distinct CLR types (they
   compile today only because the namespaces differ — the same situation as the
   superseded `TaskRoundState` duplicate-enum tech-debt item). `SoarscoreEventTypes.All`
   is a flat `(type, alias)` list consumed by both `MartenConfig.cs:32-35`
   (`MapEventType`) and `FisherConfig.cs:47-51` (`EventTypeName`); the alias is the
   type's on-disk identity and the deserialization key, so both types sharing
   `"penaltyRecorded"` cannot work. Decision 5.
4. **`openEntry.alreadyOpen` blocks the F3F.1.5 reflight shape — and the fix is not
   a read-model change.** `OpenEntryHandler` refuses a new Entry when the index
   shows an existing `ReflightRole.Original` one
   (`src/Soarscore.Application/Commands/Entries/OpenEntry.cs:42-55`), and
   `EntrySummary` deliberately carries "the coordinate and nothing else — … no
   annulled flag" (`EntrySummary.cs` header), so annulment is invisible to the
   guard. Extending the read model would contradict that header's design;
   instead the handler answers annulment the way `EntrySummary`'s own header says
   such questions are answered — "a stream load already answers" it. WI-6: when the
   index shows Original-role entries, load and fold just those (typically one)
   streams via `EntryLoader` and refuse only if a *live* (non-annulled) Original
   exists. The index stays coordinate-only; `EntryProjectionTests`' assertion that
   `EntryAnnulled` leaves the summary unchanged stays true.
5. **The corpus already declares every penalty kind the scenarios need.** F5K
   (`tools/Soarscore.SeedData/SeedF5K.cs:327-344`) declares all three in one class:
   `motorRestartInFlight` → ZeroFlight (5.5.10.12 flight c), `hitPersonOtherThanTimer`
   → ZeroRound (safety a), `safetyZone` → DeductPoints 300 "deducted from the final
   score" (safety b). No seed-data changes are needed for this thread.
6. **`Penalty` has zero constructor call sites** in `src/` and `tests/` (verified by
   grep — only `PenaltyDefinition` initialisers in SeedData). Extending the payload
   is free of source-compatibility concerns.
7. **`aggregate-roots.md` models `Penalty` in two class diagrams**
   (`docs/aggregate-roots.md:292-294`, `:420-422`) with exactly `infractionType` +
   `scope`. WI-1 amends both diagrams with the three optional fields — user-approved
   2026-08-21 via decisions 1, 2 and 4. The glossary's Penalty entry
   (`docs/soaring-domain-glossary.md:69-71`) needs no change: "the penalty itself
   only records what occurred" — the subject and the recorder are part of what
   occurred. `TaskRoundCoordinate` is a value object (the same ordinal triple
   `EntryOpened` already carries), not a domain concept — no glossary entry.
8. **Nothing in the NFR docs conflicts.** NFR-1 (class model owns penalty *costs*) is
   honoured by decision 3 — the core validates against the model, never encodes
   costs; the trust model's auditability is served by decision 2's `By` and the
   append-only log. Cross-checked per house-keeping rule 2; no inconsistencies found.

## Work items

Each WI is small enough for one agent session and lands compiling with tests green.
Work them in order; WI-1 first, WI-9 before any runtime test that appends either
event (the acceptance suite is the first such). The Entry half (WI-1, WI-2, WI-3,
the entry slices of WI-7–WI-10) is independently shippable if the Competition half
stalls, exactly as `amend-a-measurement.md` partitioned its halves.

**WI-1 — Extend the `Penalty` contract.** In
`src/Soarscore.Domain/Shared.cs` (lines 27-41):

- Add three nullable init properties to `Penalty`: `string? By`,
  `CompetitorId? CompetitorRef`, `TaskRoundCoordinate? TaskRound`. Update the doc
  comment: `By` records who recorded the infraction (optional, decision 2);
  `CompetitorRef` is the subject, meaningful only at TaskRound/Competition scope,
  enforced by the decide functions; `TaskRound` is the reporting coordinate the
  rules ask to list the deduction against (F3B.1.7.b), read by nothing yet.
- Add the value object in the same file:
  `public sealed record TaskRoundCoordinate(int PhaseOrdinal, int RoundOrdinal, int TaskRoundOrdinal);`
  with a doc comment noting it is the same triple `EntryOpened` carries, a value
  object and not a glossary concept.
- Amend `docs/aggregate-roots.md`'s two `Penalty` classes (finding 7) with
  `+string? by`, `+CompetitorId? competitorRef`, `+TaskRoundCoordinate? taskRound`.

No event-record changes: `EntryEvents.PenaltyRecorded(Penalty)` and
`CompetitionEvents.PenaltyRecorded(Penalty)` already carry the shared payload.

**WI-2 — `Entry.AnnulEntry` decide function + domain tests.** In
`src/Soarscore.Domain/Entries/Entry.cs`, alongside `AmendMeasurement`:

```csharp
public Result<EntryAnnulled> AnnulEntry(string reason, string by, DateTimeOffset at)
```

Constructs `new EntryAnnulled(new Annulment { Reason = reason, By = by, At = at })`.
Defect codes, in order checked:

- `annulEntry.reasonRequired` — reason null/blank (mirrors
  `amendMeasurement.reasonRequired`).
- `annulEntry.byRequired` — by null/blank.

No other checks: not already-annulled (decision 7), not gated on task-round state
(decision 6), and nothing to validate against the class definition — an annulment
is a ruling, not an infraction with a modelled cost.

Tests: new `tests/Soarscore.Domain.Tests/AnnulEntryDecideTests.cs`, mirroring
`AmendMeasurementDecideTests.cs`: one `[Fact]` per defect code, a happy path
asserting the emitted `Annulment` carries reason, by and the passed instant, and a
re-annulment fact (second call succeeds; see P2). Update `EntryEvents.cs`'s header
block comment (lines 14-18), which describes both events as the unreachable
remainder — they stop being that this thread.

**WI-3 — `Entry.RecordPenalty` decide function + domain tests.** In
`src/Soarscore.Domain/Entries/Entry.cs`:

```csharp
public Result<PenaltyRecorded> RecordPenalty(
    Penalty penalty, ImmutableArray<PenaltyDefinition> penaltyDefinitions)
```

`penaltyDefinitions` is resolved by the handler from the Competition's
`AdoptedRules.Definition.Penalties` — `Entry` never learns which class it is flying
under, exactly as `CaptureMeasurement`'s `metrics` parameter. Defect codes, in
order checked:

- `entry.annulled` — reuse the existing code; an annulled Entry accepts nothing
  further, exactly as `OpenFlight`/`CaptureMeasurement`/`AmendMeasurement` rule.
- `recordPenalty.wrongScope` — `penalty.Scope` is not `Flight` or `Entry`.
- `recordPenalty.subjectNotAllowed` — `penalty.CompetitorRef` or `penalty.TaskRound`
  is non-null at Entry scope: the Entry *is* the subject and the coordinate.
- `recordPenalty.infractionTypeNotDeclared` — no `PenaltyDefinition` in
  `penaltyDefinitions` has a matching `InfractionType` (decision 3).
- `recordPenalty.byBlank` — `By` supplied but null/blank; absent is fine, blank is
  a typo.

Success: `new PenaltyRecorded(penalty)`.

Tests: new `tests/Soarscore.Domain.Tests/RecordEntryPenaltyDecideTests.cs`, same
shape as WI-2's file: one fact per defect code, happy path asserting the payload
round-trips into the event, and a fold fact asserting `Penalties` grows (P3's
example case).

**WI-4 — `Competition.RecordPenalty` decide function + domain tests.** In
`src/Soarscore.Domain/Competitions/Competition.cs`, defect-chain style like
`WithdrawCompetitor` (`Competition.cs:591-602`):

```csharp
public Result<PenaltyRecorded> RecordPenalty(Penalty penalty)
```

Reads `AdoptedRules.Definition.Penalties` from its own state — the same
self-service read `OpenEntry` makes of `AdoptedRules` (`Competition.cs:921-923`);
unlike Entry, Competition holds the adopted rules. Defect codes, in order checked:

- `recordPenalty.wrongScope` — `penalty.Scope` is not `TaskRound` or `Competition`.
- `recordPenalty.competitorRequired` — `penalty.CompetitorRef` is null (decision 1).
- `competition.competitor.notFound` — reuse `ValidateCompetitorExists`
  (`Competition.cs:1214`) exactly as `WithdrawCompetitor` does. Deliberately no
  withdrawn check (decision 8).
- `recordPenalty.taskRoundNotFound` — `penalty.TaskRound` supplied but no such
  phase/round/task-round in the drawn structure. Walk `Phases → Rounds → TaskRounds`
  exactly as `OpenEntry` does (`Competition.cs:868-887`); one code, message names
  which level was missing. Absent coordinate is fine (decision 4).
- `recordPenalty.infractionTypeNotDeclared` — same check as WI-3, own definitions.
- `recordPenalty.byBlank` — same check as WI-3.

No finalisation gate (decision 6). Success: `new PenaltyRecorded(penalty)`.

Tests: extend `tests/Soarscore.Domain.Tests/CompetitionDecideTests.cs` (or a new
`RecordCompetitionPenaltyDecideTests.cs` if that file is crowded — either is fine,
name the new file if created): one fact per defect code plus the happy path,
seeded by folding `CompetitionCreated` → `CompetitorRegistered` → `PhaseDrawn`, the
pattern `CompetitionDecideTests.cs` already uses.

**WI-5 — Subject-filtered aggregate penalties in `ScoringService` + tests.** In
`src/Soarscore.Domain/Scoring/ScoringService.cs`:

- `GetAggregatePenalties(ImmutableArray<Penalty>)` (lines 366-371) becomes
  `GetAggregatePenalties(ImmutableArray<Penalty>, CompetitorId competitorRef)` —
  keep the scope filter, add `p.CompetitorRef == competitorRef`.
- Move the call from line 263 (outside the loop) to inside the per-competitor loop
  at line 267, passing each `competitorRef`. The deduction and the `Disqualified`
  flag now land on the offender only.

Tests: extend `tests/Soarscore.Domain.Tests/ScoringServicePropertyTests.cs` (and
add example facts alongside): a two-competitor fixture where a 300-point
`DeductPoints` penalty on one leaves the other's total untouched, and a
`Disqualify` penalty flagging only its subject. P4 (below) is the property form.

**WI-6 — Annulment-aware `alreadyOpen` guard + handler tests.** In
`src/Soarscore.Application/Commands/Entries/OpenEntry.cs` (lines 42-55): keep the
index query; when any `ReflightRole.Original` entries come back, load each via
`EntryLoader.LoadAsync` (the same loader `AmendMeasurementHandler` uses) and refuse
with the existing `openEntry.alreadyOpen` code only if one has `Annulment is null`.
Forward `EntryLoader` failures verbatim. Update the handler's doc comment to record
finding 4's reasoning: the index stays coordinate-only, the stream answers
annulment. Tests: extend
`tests/Soarscore.Application.Tests/Commands/Entries/OpenEntryHandlerTests.cs` —
`FakeEntryQuery` seeded with an Original entry whose stream (in `FakeEventStore`)
holds `EntryAnnulled` → open succeeds; without the annulment → `openEntry.alreadyOpen`.

**WI-7 — Commands, handlers, routes, registrations + handler tests.**

- `src/Soarscore.Application/Commands/Entries/AnnulEntry.cs` —
  `record AnnulEntry(EntryId EntryRef, string Reason, string By) : ICommand<EntryId>`.
  Handler: `EntryLoader` → `entry.AnnulEntry(command.Reason, command.By, clock.UtcNow)`
  → append with `ExpectedVersion.Exact(version)`. One load; no Competition needed.
- `src/Soarscore.Application/Commands/Entries/RecordEntryPenalty.cs` —
  `record RecordEntryPenalty(EntryId EntryRef, string InfractionType, PenaltyScope Scope, string? By) : ICommand<EntryId>`.
  Handler: `EntryLoader` + `CompetitionLoader` (the two-load shape from
  `AmendMeasurementHandler`) → construct `Penalty` with `CompetitorRef`/`TaskRound`
  null → `entry.RecordPenalty(penalty, competition.AdoptedRules.Definition.Penalties)`
  → append. No `TaskResolver` — penalties are task-agnostic.
- `src/Soarscore.Application/Commands/Competitions/RecordCompetitionPenalty.cs` —
  `record RecordCompetitionPenalty(CompetitionId CompetitionRef, string InfractionType, PenaltyScope Scope, CompetitorId CompetitorRef, TaskRoundCoordinate? TaskRound, string? By) : ICommand<CompetitionId>`.
  Handler: `CompetitionLoader` → construct `Penalty` → `competition.RecordPenalty(penalty)`
  → append to the competition stream with `ExpectedVersion.Exact(version)`.

All three copy `AmendMeasurement.cs`'s shape verbatim: sealed records + handler in
one file, primary-constructor ports only, failures forwarded verbatim, `//` header
citing this story's WI. Routes in `src/Soarscore.Api/Commands/Commands.cs`:
`/annul-entry`, `/record-entry-penalty` (Entries block) and
`/record-competition-penalty` (Competitions block). Registrations in
`src/Soarscore.Api/Composition.cs` — one `AddScoped` line each, next to their
siblings. No error-mapping changes (`EndpointRouteBuilderExtensions.cs`'s
suffix-based mapping already buckets `.notFound` → 404; everything here is 400).

Handler tests: three new files in
`tests/Soarscore.Application.Tests/Commands/{Entries,Competitions}/`, mirroring
`AmendMeasurementHandlerTests.cs` — fakes only, clock instant reaching the
`Annulment.At`, domain codes surfaced unchanged, optimistic-concurrency conflict.

**WI-8 — Architecture floor.** `tests/Soarscore.Architecture.Tests/HandlerRegistrationTests.cs:71`:
three new commands raise the floor from 19 to 22; raise it and fix the comment,
per the precedent that a stale floor stops catching the reflection technique
silently breaking.

**WI-9 — Register the event types (the runtime trap).** In
`src/Soarscore.Infrastructure/SoarscoreEventTypes.cs`: add
`(typeof(EntryAnnulled), "entryAnnulled")` to the Entry block, and to the
Competition block `(typeof(Domain.Competitions.PenaltyRecorded), "competitionPenaltyRecorded")`
and to the Entry block `(typeof(Domain.Entries.PenaltyRecorded), "entryPenaltyRecorded")`
— **distinct aliases, decision 5 / finding 3; sharing `"penaltyRecorded"` is the
trap hiding inside this story's original stub.** Update both block comments
(lines 51-53 and 64-70) to drop the "deliberately absent" notes for all three.
`MartenConfig` and `FisherConfig` need no edits — they loop the table.

**WI-10 — Acceptance scenarios.** Both stores per CLAUDE.md
(`SOARSCORE_TEST_STORE=postgres` and `=sqlite`).

In `tests/Soarscore.Acceptance.Tests/Features/CapturingAScore.feature` (+ its steps
file), reusing that feature's existing class/competition/draw/capture Givens:

- *An entry is annulled by a recorded ruling* — open an entry, capture a flight
  time, `POST /annul-entry` with a reason; then assert via `EntryReader` that the
  stream holds the `Annulment` *and* the original measurements (append-only), and
  that a further `POST /capture-measurement` is refused with `entry.annulled`.
- *An undeclared infraction type is refused* — `POST /record-entry-penalty` with
  `infractionType: "madeUp"` → 400 `recordPenalty.infractionTypeNotDeclared`.

In `tests/Soarscore.Acceptance.Tests/Features/ScoringACompetition.feature` (+ its
steps file), publishing the corpus F5K (`Corpus.All` / `SeedF5K.Definition`) for
the penalty scenarios (finding 5):

- *An annulled entry scores no result and a replacement stands* (the F3F.1.5 shape,
  exercises WI-6 end-to-end): competitor's entry captured, second open refused with
  `openEntry.alreadyOpen`, first annulled, second open now succeeds and is
  captured, score → the second entry's time is the pilot's result.
- *A flight-scoped penalty zeroes the flight* — record `motorRestartInFlight`
  against the entry → that pilot's task-round result is zero/NoResult, the other
  pilot's is unaffected.
- *An aggregate penalty deducts from its subject only* — record `safetyZone`
  (DeductPoints 300) against one competitor with a task-round coordinate → that
  competitor's final score is 300 lower, every other competitor's is unchanged.
  This is P4 asserted end-to-end and the whole point of decision 1.

**WI-11 — Board and doc reconciliation.**

- `git mv` this story to `kanban/completed/` when done; set the status header.
- `kanban/tech-debt.md` — nothing to tick.
- `kanban/deferred-decisions.md` — add: *the task-round coordinate on aggregate
  penalties is recorded but read by nothing* (decision 4's deferred half — the
  score-sheet report that would read it does not exist).
- `kanban/backlog/smaller-items.md` — no changes; `RulesAmended` and
  `ReflightGroupAppended` remain the unreachable remainder, each already covered
  by its own note.
- If a penalty/annulment read surface (a query showing recorded penalties and
  annulments over HTTP) is wanted, raise a new `kanban/backlog/` stub — house-keeping
  rule 6; do not build it here. Today the audit trail is the stream itself, read
  by tests via `EntryReader`.

## Property-based invariants (CsCheck)

Named here, during planning, per CLAUDE.md's testing approach. All live in
`tests/Soarscore.Domain.Tests` alongside their subjects.

- **P1 — Annulment dominates capture.** For any Entry state — any number of
  flights, any measurements — folding an `EntryAnnulled` makes
  `FlightSelector.SelectAndScore` return `NoResult`. The invariant
  `FlightSelector.cs:40-42` already encodes; nothing has tested it against
  adversarial capture density.
- **P2 — The latest ruling stands.** For any non-empty sequence of annulments, the
  folded `Entry.Annulment` equals the last one's payload. Holds decision 7's
  overwrite semantics true.
- **P3 — Penalties are append-only.** For any sequence of *n* successful
  `RecordPenalty` calls (either aggregate), the folded `Penalties.Length == n`
  and every payload is present in order.
- **P4 — Subject isolation.** For any set of competition-stream penalties with
  arbitrary subjects and any definitions, each competitor's
  `ApplyAggregatePenalties` deduction is identical to the deduction computed from
  that competitor's own penalties alone — partition invariance. **The invariant
  decision 1 exists to make true**; before WI-5 it is false by construction
  (finding 2), so the test lands with the fix, not before it.

## Out of scope

- **Un-annulment.** A reversed ruling is either a re-annulment (decision 7) or a
  fresh Entry; no `EntryReinstated` event, per the standing rule that events close
  when commands need them.
- **`ReflightGroupAppended`** — `kanban/backlog/reflight-groups.md`, untouched. The
  reflight *shape* this thread enables (annul + re-open) is the F3F.1.5
  provisional case, which needs no reflight group; group re-flights are that
  story's own subject.
- **`RulesAmended`** — still the one unreachable event no story covers
  (`kanban/backlog/smaller-items.md`, "Unclaimed").
- **A penalty/annulment read surface** — WI-11's stub rule; the stream is the
  audit trail.
- **The score-sheet report that would read the task-round coordinate** —
  deferred-decisions entry in WI-11.
- **Any role or authorisation check** — `By` is recorded, never enforced, per
  decision 2 and the trust model.
