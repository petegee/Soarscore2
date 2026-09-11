# GET /competition-event-log — read the event log for a competition

**Status:** Complete — built and verified 2026-09-11 · **Raised:** 2026-09-11 · **Planned:** 2026-09-11

## What

A GET endpoint that, given a competition id (the minted Guid), returns the
competition's event log as JSON: the event name for every event, a
human-readable summary string where one adds value ("John entered round 2,
group A"), and the serialised payload optionally.

Owner decisions (2026-09-11):

- **Id is the Guid** — no hash/short-code identifier for competitions; that
  would be a new domain concept with no current need.
- **Scope is competition stream + every entry stream** — the audit trail of
  the contest. Entry events (flights, measurements, entry penalties,
  annulments) live on entry streams, not the competition stream, so the log
  must merge them. Person and class-definition streams stay out.
- **Shape: name + summary by default, payload opt-in** — `?includePayload=true`
  adds the `SoarscoreEventJson` serialisation of each event.

## Why it matters

The trust model has no auth and no score sign-off; **the immutable event log
is the auditability** (CLAUDE.md key constraints, LADR-0002 §6). Today that
log is only readable by querying the database directly. This exposes it as
the audit view it was always meant to be, and gives a CD the raw trace for
questions `GET /competition` cannot answer — e.g. *why* a draw was rejected
(`DrawRejected` reasons are stream-only; see `kanban/deferred-decisions.md`
"Draw" section — the polished draw-history read surface stays deferred; this
endpoint surfaces the raw trace, it does not discharge that deferral).

## Cross-references

- **Trust model:** immutable event log = auditability — this is the read
  surface for that promise. Aligned.
- **NFR-3** (core headless, no imagined features): a modest read surface,
  justified by the trust model above — not a UI, no ordering or gating
  (NFR-4 untouched; reading is always allowed).
- **LADR-0001 §4:** uses only port methods (`ReadStreamAsync` per stream);
  `ReadAllAsync` (whole-log scan) is deliberately *not* used — the log is
  assembled per stream.
- **Layer law:** summarisation is event-kind branching, never class
  branching — class-specific values (task codes, metrics) come from event
  payload data generically.

## Before starting

- Competition id = Guid; route `GET /competition-event-log?id=…` (the
  `[AsParameters]` binding key is the parameter name, `id` — the same
  convention `/competition` already uses).
- `ReadStreamAsync` returns bare events — no version metadata. Stream order
  is implied by list position (versions are 1..n per stream); the response
  exposes an explicit per-stream `version` derived from that order.
- Most events carry `At` but entry-scoped ones (`MeasurementCaptured`,
  `EntryAnnulled`) do not — a global cross-stream timeline is not
  reconstructible. Ordering is therefore **per stream** (competition stream
  first, then entries by task-round coordinate/competitor number), which the
  response states explicitly by grouping events under their stream.
- Names: entry streams carry `CompetitorId` only. The competition fold gives
  Competitor↔`PersonRef`; `IPeopleQuery` has no by-id lookup, so it gains
  `FindByIdsAsync` (additive port extension — the four-ports shape is kept,
  per the settled deferral).
- Every registered event kind (the `SoarscoreEventTypes` alias list) needs a
  summary case; unknown kinds fall back to name-only so a future event type
  degrades gracefully (additive-only, NFR-2 style).

## Plan

### WI-1 — `EventLogSummariser` (Application, pure)

Pure function per event kind → short summary string; unknown kind → no
summary. One case per event contract in
`src/Soarscore.Domain/{Competitions,Entries,People}` — the alias list in
`SoarscoreEventTypes.cs` is the checklist (34 types; `RulesAmended` has no
command yet, but a case may exist for it or fall back).

**Named invariant for the property test:** *log round-trip completeness and
order* — for any generated competition log (competition stream + entry
streams, arbitrary interleaving of capture order), the view contains every
appended event exactly once, ordered by stream version within its stream,
with the competition stream first. No-loss, no-reorder, no-duplicate.

### WI-2 — `GetCompetitionEventLog` query + handler (Application)

`Application/Queries/Competitions/GetCompetitionEventLog.cs`:
`GetCompetitionEventLog(CompetitionId Id, bool IncludePayload) : IQuery<CompetitionEventLogView>`.

Handler flow:
1. `CompetitionLoader.LoadAsync` — existence check (`competition.notFound`
   failure convention) + competitor map (CompetitorId → PersonRef,
   CompetitorNumber).
2. `IEntryQuery.FindAsync(competitionRef)` — entry streams + coordinates.
3. `ReadStreamAsync` per stream — competition first, entries ordered by
   phase/round/task-round/competitor number.
4. `IPeopleQuery.FindByIdsAsync` (new port method) — person names.
5. Build `CompetitionEventLogView`: competition name; a group per stream
   (kind: `competition` | `entry`; label: competitor number + name, and
   task-round coordinate for entries); per event: `version`, `name` (the
   `$kind`/alias-equivalent), optional `summary`, optional `payload` when
   `includePayload` (serialised with `SoarscoreEventJson.Options`).

### WI-3 — API route + DI

`GET /competition-event-log` via `MapQuery` in `Api/Queries/Queries.cs`;
handler registered in the DI composition (HandlerRegistrationTests holds).
Route shape: GET-only — the reflection test already enforces this.

### WI-4 — tests, both backends

- Application unit tests against fakes: merge order, completeness,
  name resolution, `includePayload` serialisation shape, unknown-kind
  fallback, `competition.notFound`.
- CsCheck property (WI-1 invariant) in `Soarscore.Application.Tests`.
- Store-backed (Infrastructure, `IStoreFixture`, runs on Marten + Fisher):
  append a realistic sequence through real commands (people, competition,
  entry, measurement, withdrawal), read via the handler, assert the merged
  log.
- Run: full unit suites, architecture tests, store-backed suite on both
  backends, acceptance suite on both stores.

## Built as (2026-09-11)

- `EventName.Of` (`src/Soarscore.Application/Queries/Competitions/EventLogSummariser.cs`)
  resolves the `$kind` discriminator by reflection over the union bases'
  `[JsonDerivedType]` attributes — the contract is the single declaration, so
  a new event type is named the moment it is authored.
- `EventLogSummariser` covers all competition- and entry-scoped event kinds;
  people/class-definition kinds need no case (they cannot appear in scope).
  Unknown kinds → `null` summary, name-only. The `EventLogNames` fallback for
  a competitor absent from the fold is `competitor {guid}`; a person absent
  from the read model degrades to `competitor #{number}`.
- `GetCompetitionEventLogHandler` (`src/Soarscore.Application/Queries/Competitions/GetCompetitionEventLog.cs`)
  folds the competition stream for existence + the competitor map, resolves
  entry streams via `IEntryQuery.FindAsync`, reads each stream, and orders
  entries by phase/round/task-round then competitor number then entry id.
  Payloads serialise through the event's **union base** (`GetType().BaseType`)
  so `$kind` is emitted — the concrete declared type writes no discriminator.
- `IPeopleQuery.FindByIdsAsync` added (additive port extension — the by-id
  fold rule is untouched: it is a cross-stream label lookup, documented in
  `IPeopleQuery.cs`). `DocumentPeopleQuery` filters in memory, the
  DocumentEntryQuery precedent for strong-typed-id predicates.
- Route `GET /competition-event-log` (`Api/Queries/Queries.cs`); the
  `[AsParameters]` binding keys are `id` and `includePayload`.
- Tests: `GetCompetitionEventLogHandlerTests` (8, Application fakes, including
  the WI-1 property) and `CompetitionEventLogEventStoreTests` (3 × both
  backends). Verified end-to-end against the running API (compact + payload).

## Status

Complete.