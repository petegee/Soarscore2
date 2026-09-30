# Story — Draw view and meeting-frequency matrix (draw fairness read)

**Status:** Backlog · **Raised:** 2026-09-30

## What

Provision a new API query/read that shows the draw and a matrix of which
pilots meet which pilots and how often, so the organiser can judge how fair
the draw is.

Fixed shape (no open design left — implement as specified):

- New sibling query `GetDrawMatrix` → `GET /draw-matrix`, folded from the
  competition stream (`CompetitionId` in, never a new read model). NOT an
  extension of `GET /competition`: the matrix plus the draw view is a large
  payload with its own round-window scoping, and WI-6a's "gain a field"
  precedent does not scale to a second consumer (organiser UI, clubhouse
  display, accept/redraw decision) that needs the draw without the full
  aggregate.
- Response covers both halves: "who is in which group" (draw view) AND
  "who meets whom how often" (matrix + stats). One round trip for the
  fairness judgement.

## Why it matters

The draw's fairness invariant is "any two pilots meet as few times as
possible" (`00-general-rules.md#1`, `phase-drawn-steel-thread-plan.md`).
Today the organiser has the raw material but no fairness read: `GET
/competition` returns the folded `Competition` (`Phases` → `Rounds` →
`TaskRound` → `Group.CompetitorRefs`) plus WI-6a's flat
`pairwiseCoOccurrence` list (`{competitorA, competitorB, count}`), and `GET
/draw-diagnostics` names only protected-pair violations. Judging a draw
means mentally pivoting a flat pair list into a who-meets-whom matrix and
resolving bare `CompetitorId`s back to pilots — exactly the work the read
side should do once, deterministically, for every consumer.

## GliderScore precedent (checked, not assumed)

Three distinct views over the same data in `gliderscore/GliderScore_Master/`:

1. Check-draw screen (`CheckDraw_MOD.vb:3`, `CheckDrawDisplay.vb`): sortable
   Pilot1/Pilot2/Meetings grid (`dtPilotMeetings_Create:633`,
   `DtPilotMeetings_Fill:646`, three sort orders `SetSortOrder:774`),
   meetings→count histogram (`PictureBox1_Paint:800`), Mean Absolute
   Deviation fairness scalar (`CalcMeanAbsoluteDeviation`,
   `GlobalFunctions_MOD.vb:4833`), From/To round selector
   (`ToRnd_ValueChanged:975`), re-flights excluded (`ReFlightNo=0`,
   `dtScores_Create:585`), meaningless zeros suppressed (same-team under
   team protection, same/adjacent-frequency clashes,
   `DtPilotMeetings_Fill:695-726`).
2. Draw Matrix report (`Rpt_DrawMatrix_MOD.vb:41`): printable/CSV
   pilot-vs-pilot matrix (`CreateTable_dtPilotVsPilot`,
   `GlobalFunctions_MOD.vb:4711` — zero-filled table, per-group increments,
   `SumOfMtgs` row totals), Name + `#` ref columns, blank-when-zero cells,
   grey diagonal, light-grey `F`/`T` clash overlays (`DrawLines:308`), round
   range heading.
3. The draw itself — Draw Table (`Rpt_DrawTable_MOD.vb:59`, landscape
   pilot-rows × round-columns of group/seq) and Draw small-format details
   (`Rpt_DrawDetailsNew_MOD.vb:47`, portrait round-major group lists with
   Team/Lane/Pilot#/Name/Freq/Country/Class/OrgRnd columns).

Deliberate deviations from GliderScore (recorded so a future reader does
not "fix" them back):

- Names are NOT resolved server-side. GliderScore embeds `PltName` from a
  single MDB join; Soarscore keeps the aggregate boundary — `Competitor`
  carries `PersonRef` only, and no competition query to date resolves people
  (`GetTeamRosters` returns bare `CompetitorId`s). The client joins pilot
  labels via the existing people reads (`GET /people`, `GET /person`).
- Re-flight groups ARE included. GliderScore filters `ReFlightNo=0`; the
  Soarscore fold has no such marker — `Group.CompetitorRefs` is all the
  query sees, and provenance-blind reading is the `teams-mvp.md` WI-6
  precedent (generated and prescribed draws read identically).
- No team/frequency clash suppression or overlays. That is draw-protection
  diagnostics' job (`GET /draw-diagnostics`); this query reports raw meeting
  counts, never a pass/fail verdict.
- MAD is computed over the UNDIRECTED pair universe (`N choose 2`,
  matching `PairwiseCoOccurrence` semantics). GliderScore's `dtPilotMeetings`
  holds directed rows (each unordered pair twice); the scalar differs by
  construction, so pin the undirected definition here and in tests.

## Contract

### Query

```csharp
// src/Soarscore.Application/Queries/Competitions/GetDrawMatrix.cs
public readonly record struct GetDrawMatrix(
    CompetitionId CompetitionRef,
    int? PhaseOrdinal = null,   // null = all live phases, flattened (GetCompetition precedent)
    int? FromRound = null,      // null = no lower bound (GliderScore From/To selector)
    int? ToRound = null)        // null = no upper bound
    : IQuery<DrawMatrixView>;
```

Binding is `[AsParameters]` query-string, like every other `MapQuery` GET —
`CompetitionId` already implements `IParsable<>` for this.

### Response

```csharp
public sealed record DrawMatrixPilotView(
    CompetitorId CompetitorRef,
    int CompetitorNumber,        // the human-meaningful axis (GliderScore `#` refs), NOT Guid order
    bool Withdrawn);             // WithdrawnAt is not null — drawn pilots stay listed (draw intact on withdrawal)

public sealed record DrawMatrixGroupView(
    int PhaseOrdinal,
    int RoundOrdinal,            // per-phase ordinals, as folded — never globalised
    int TaskRoundOrdinal,
    int GroupOrdinal,
    GroupId GroupRef,
    ImmutableArray<CompetitorId> CompetitorRefs);  // drawn order preserved, never re-sorted

public sealed record DrawMatrixEntryView(
    CompetitorId CompetitorA,    // canonical: smaller CompetitorNumber first
    CompetitorId CompetitorB,
    int Count);                  // >= 1 only — zeros implied by absence (sparse, WI-6a precedent)

public sealed record DrawMatrixDistributionEntry(
    int Meetings,                // gap-filled from 0..Max (GliderScore PictureBox1_Paint precedent)
    int Count);                  // unordered pairs with exactly Meetings meetings, zeros included

public sealed record DrawMatrixView(
    CompetitionId CompetitionRef,
    int? PhaseOrdinal,           // echo of the request scope
    int? FromRound,
    int? ToRound,
    ImmutableArray<DrawMatrixPilotView> Pilots,        // CompetitorNumber asc
    ImmutableArray<DrawMatrixGroupView> Groups,        // phase/round/task-round/group ordinal order
    ImmutableArray<DrawMatrixEntryView> Entries,       // (row CompetitorNumber, col CompetitorNumber) asc
    ImmutableArray<DrawMatrixDistributionEntry> Distribution,  // Meetings asc
    int MinMeetings,             // over ALL unordered pairs incl. never-met (0 when N < 2 or no groups in scope)
    int MaxMeetings,             // over ALL unordered pairs incl. never-met (0 when N < 2 or no groups in scope)
    double MeanMeetings,         // over ALL unordered pairs incl. never-met (0 when N < 2)
    double MeanAbsoluteDeviation);// GliderScore CalcMeanAbsoluteDeviation port, undirected universe (0 when N < 2)
```

### Semantics (normative)

1. Load via `CompetitionLoader.LoadAsync` — unknown id →
   `competition.notFound`, exactly the `GetDrawProtectionDiagnostics`
   pattern (`GetDrawProtectionDiagnostics.cs:44-55`).
2. Scope: live phases only (`Competition.Phases` holds only live phases —
   a rejected draw's phase is removed, D2). `PhaseOrdinal` set →
   that phase; absent ordinal → `competition.notFound`-style defect is
   WRONG, return empty sections (see 7) — no, precisely: unknown
   `PhaseOrdinal` → defect `drawMatrix.phaseNotFound`. Null phase →
   all live phases flattened (`GetCompetition.cs:37-41` precedent).
3. Round window: `FromRound`/`ToRound` apply to `Round.Ordinal` WITHIN each
   in-scope phase (rounds are per-phase numbered; no global round axis
   exists). `FromRound > ToRound` → defect `drawMatrix.roundRangeInvalid`
   (only validation failure besides not-found). Null bound = open.
4. Counting kernel: reuse `PairwiseCoOccurrence.Compute(rounds)` over the
   in-scope rounds — do NOT reimplement the double loop. The handler maps
   `Compute`'s dictionary to `DrawMatrixEntryView`s ordered by
   `(CompetitorNumber, CompetitorNumber)`, canonicalised smaller-number
   first (deviation from `ComputeEntries`' Guid ordering is intentional:
   Guid order is meaningless to the organiser; number order matches the
   `Pilots` axis and GliderScore `#` refs).
5. Pilot universe = the competition's full field (`Competitors`), ordered
   by `CompetitorNumber` asc — known pre-draw, returned even when no groups
   are in scope. Withdrawn pilots included with `Withdrawn: true`.
6. Stats universe = all unordered pilot pairs (`N choose 2`) within the
   scoped rounds, zeros included: `Min/Max/Mean/MAD` see never-met pairs
   as 0. `Distribution` is gap-filled `0..Max` (GliderScore
   `PictureBox1_Paint:840-850` precedent). `N < 2` → all stats 0, empty
   `Distribution`/`Entries`.
7. Undrawn competition or empty scope (no groups in scope) is SUCCESS with
   empty `Groups`/`Entries`, `Distribution` covering the all-zero universe
   (`Meetings: 0, Count: N choose 2` when `N >= 2`), stats 0 — never
   not-found. (Mirrors `GetDrawProtectionDiagnostics`: undrawn → empty.)
8. Pure derivation, never stored: no event, no projection change, no
   `ICompetitionsQuery` method (the `ICompetitionsQuery.cs:6-9` ban on
   get-by-id stands — fold the stream).
9. Class-agnostic: no branch on class, task, or team (NFR-1/NFR-2). Counts
   pairs across every task-round in scope, exactly like `Compute`.
10. Auth: `AuthenticatedPolicy` (D4 query default — per-query opt-out is
    `public-read-surface.md`'s decision, not this story's).

## Work items

**WI-1 — `GetDrawMatrix` query + handler.**
- New `src/Soarscore.Application/Queries/Competitions/GetDrawMatrix.cs`:
  records above, handler as specified (loader → scope → `Compute` →
  project). MAD formula: port `CalcMeanAbsoluteDeviation`
  (`GlobalFunctions_MOD.vb:4882-4906`) over the undirected pair universe —
  `Avg = Σ(meetings×count)/pairs`, `MAD = Σ(|avg−meetings|×count)/pairs`
  over the gap-filled distribution. Raw `double`, no formatting (GliderScore
  `F5` is display-side).
- Header comment cites this story path + the four deviation decisions, so a
  later reader does not "align" them away.

**WI-2 — Tests (all in `tests/Soarscore.Application.Tests/Queries/Competitions/`).**
- `GetDrawMatrixHandlerTests.cs` (FakeEventStore seeding pattern from
  `GetDrawProtectionDiagnosticsHandlerTests.cs:28-60` — `SeedCompetition`,
  fold-then-`Append` via decide functions, `ClassDefinitionFixtures.Minimal()`):
  - hand-built 3-round × 2-group-of-3 fixture (reuse
    `PairwiseCoOccurrenceTests.cs:36-39` partitions and hand-computed counts
    `:44-64`) → entries match, ordered by competitor number; distribution
    gap-filled incl. zeros (`BD`/`BE` absent → `Meetings: 0` bucket counts
    them); `Min/Max/Mean/MAD` hand-computed against the same fixture.
  - round window: `FromRound/ToRound` subset returns that slice's counts
    only; `FromRound > ToRound` → `drawMatrix.roundRangeInvalid`.
  - unknown competition → `competition.notFound`; unknown `PhaseOrdinal` →
    `drawMatrix.phaseNotFound`; undrawn competition → success with pilots
    listed, empty groups/entries, all-zero distribution.
  - prescribed draw reads identically to generated (diagnostics precedent —
    one test each, same row shape).
  - withdrawn pilot still listed with `Withdrawn: true`; group
    `CompetitorRefs` preserve drawn order.
- No new property test: the counting kernel is already covered
  (`PairwiseCoOccurrenceTests`, `PhaseDrawPropertyTests`); this WI pins the
  projection/ordering/stats, which are example-shaped.

**WI-3 — Wiring (three files, one line each — miss one and WI-10 fails closed).**
- `src/Soarscore.Api/Queries/Queries.cs`: `app.MapQuery<GetDrawMatrix,
  DrawMatrixView>("/draw-matrix");` (kebab-case, GET-only via `MapQuery`).
- `src/Soarscore.Api/Composition.cs`: `AddScoped<IQueryHandler<GetDrawMatrix,
  DrawMatrixView>, GetDrawMatrixHandler>()` (no assembly scanning — LADR-0003).
- `src/Soarscore.Application/Auth/CommandPolicyTable.cs`:
  `[typeof(GetDrawMatrix)] = new AuthenticatedPolicy(),` in the queries
  block. Totality (`PolicyTableTotalityTests`) and route-shape
  (`HandlerRegistrationTests`) cover the wiring automatically — no new arch
  test.

**WI-4 — End-to-end verification.**
- Against a running API + PostgreSQL: publish seed class → create
  competition → register ≥6 competitors → draw 2+ rounds → `GET
  /draw-matrix?competitionRef=…` shows groups per round, sparse entries,
  gap-filled distribution, stats → `&fromRound=…&toRound=…` slices →
  unknown id returns `competition.notFound` ProblemDetails. Record the run
  in the story on completion.

## Dependency order

```
WI-1 ── first
WI-2 ── needs WI-1
WI-3 ── needs WI-1 (independent of WI-2, parallelisable)
WI-4 last (needs WI-3)
```

## Constraints (binding)

- Fold-the-stream for by-id reads (`ICompetitionsQuery.cs` deliberately has
  no get-by-id; `high-level-architecture.md`); `MapQuery` GET-only routing
  (`EndpointRouteBuilderExtensions.cs`, route-shape test); class-agnostic
  derivation only (NFR-1/NFR-2); `Authenticated` default policy.
- No new domain concepts: `Draw`, `Phase`, `Round`, `TaskRound`, `Group`,
  `Competitor` already cover this (glossary approval required for anything
  new — CLAUDE.md). No rule-corpus engagement: the rules state the goal
  ("as few times as possible"), never a method or threshold — this story
  reports counts, never a verdict.
- No `/docs` changes expected.
