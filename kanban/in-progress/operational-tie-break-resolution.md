# Story — Operational tie-break resolution: record the outcome, re-rank

**Status:** In Progress · **Raised:** 2026-09-04 (from the `nz-ndc-seed-classes` ruling
review — Pete story-stubbed the gap: "nothing records or executes a tie-break
fly-off result") · **Fleshed out:** 2026-09-07 (the stub's "rules first" item
discharged at source via the fai-rules script — see *Rules verification*; the
crux design question settled as D1–D3; the scope boundary settled as D7.
D1–D3, D6, D7 and the docs list get Pete's sign-off at WI-0)

## What

Give contest flow a way to act on a `PendingTieBreak`. Today the ranking engine
halts at an operational rung with the tie intact, shares the places and
annotates the result (`CompetitionResult.PendingTieBreaks`, D5 of
`kanban/completed/tie-break-policy-in-class-definition.md`) — and nothing
further exists. The CD can fly the tie-break in the real world, but the outcome
has no address to land in and the ranking cannot consume it. Three parts, each
with its design settled below:

1. **Read surface (D8).** A dedicated query, `GetPendingTieBreaks` →
   `GET /competition-pending-tie-breaks`, surfacing each pending tie group —
   the competitors, their shared place, and the directive that halts the
   ladder. `PendingTieBreaks` is domain-only today (its doc comment,
   `src/Soarscore.Domain/Scoring/ScoringResultTypes.cs:265-281`, says
   HTTP/read-model exposure was deliberately out of scope); this is the surface
   that doc comment anticipated.
2. **Record the outcome (D1–D3).** One new event on the Competition stream,
   `TieBreakOutcomeRecorded`, carrying the resolved ordering of the tied group
   (per-competitor placing within the group, ties explicit) plus the directive
   it resolves. One command, `RecordTieBreakOutcome` →
   `POST /record-tie-break-outcome`, load-decide-append. Whatever the
   directive names — a CD-defined one-task fly-off (`TieBreakFlyoff`;
   F3K.10.2 / 5.5.10.17), an additional full round (`AdditionalFullRound`;
   F3B.2.8), more rounds of the class's task (`ClassificationRounds`;
   F3F.1.13), or a CD ruling where the rulebook is silent
   (`UndefinedRequiresRuling`; F5L) — the record lands as an immutable event in
   the audit log, like every other CD declaration.
3. **Re-rank (D4–D5).** The scoring pipeline consumes the recorded outcomes:
   the engine matches each halted tie group against them, orders the group by
   the recorded placing (dormant comparator rungs separating any residual
   recorded tie — F3F.1.13's fallback), and the group no longer surfaces as
   pending. The published result reflects it; `FinaliseCompetition` declares
   the resolved places exactly as derived.

**NZ classes are out of scope.** Pete ruled 2026-09-04 that NZ has NO
tie-breaking at all — ties are never broken, announced equal ("1st equal") at
every placing — and all eight NZ seeds encode that as the `EqualPlaces`
directive (`kanban/deferred-decisions.md`, Competition class model). An
`EqualPlaces` ladder never halts pending (the shared place IS the outcome), so
no NZ class can surface a tie to act on. Do not re-import tie-break machinery
there.

## Why it matters

Four corpus classes state operational rungs and can produce unresolved ties at
club events: `20-f3b` (`AdditionalFullRound`), `10-f3k` and `40-f5k`
(`TieBreakFlyoff` after `BestDroppedScore`), `70-f3f` (`ClassificationRounds`
first). F5L (`60-f5l`) halts identically but needs a CD *ruling*, not flown
rounds. For all five, a tie at the end of a contest is a permanent shared place
plus an obligation the software records nowhere — the CD's tie-break result has
no address to land in, and the final published result can never reflect who won
the fly-off. This is the unrouted half of the tie-break story's design: the
operational directives were made stateable precisely so contest flow could act
on them as data.

## Rules verification (the stub's "rules first" item — discharged 2026-09-07)

The stub asked: for each directive, does the tie-break outcome touch scores or
only ordering? Verified at source via `.claude/skills/fai-rules/scripts/fai-rule.sh`:

| Directive | Clause (verbatim) | Reading |
|---|---|---|
| `AdditionalFullRound` | F3B.2.8: "To decide the winner when there is a tie, the two (or all who have the equal score) competitors will fly an additional round (three tasks)." | **Ordering only.** Only the tied competitors fly; the clause says the round decides the winner, never that its scores enter anyone's aggregate. |
| `TieBreakFlyoff` | F3K.10.2 / 5.5.10.17 (2026 F5 volume, re-checked): "…a separate fly-off for the relevant competitors will be flown **to achieve a ranking**. In this case the contest director will define one task…" | **Ordering only**, stated outright. |
| `ClassificationRounds` | F3F.1.13: "…'classification rounds' are flown **until the ties are broken**. If this is not possible, the result of the discarded round will determine each competitor's position…" | **Ordering only**, with the discarded-round figure as the residual-tie fallback — exactly the dormant `bestDroppedScore` rung the ladder already states after it. |
| `UndefinedRequiresRuling` | F5L 5.5.12.12 states classification and stops. | A CD ruling — the same authority class as `ReflightRulingRecorded`. |

**The working assumption holds for all four: the outcome is ranking machinery
only.** No clause states that tie-break flights' scores enter the published
aggregate, and F3B.2.8's "the two (or all who have the equal score)"
wording forbids reading the additional round as an ordinary round (the
non-tied field never flies it, so it cannot aggregate symmetrically).

Two readings flagged for WI-0 (neither changes the design):

- **F3F.1.13's "If this is not possible"** — ambiguous between "if more rounds
  cannot be flown" and "if the rounds flown did not break the tie". The design
  below makes the two readings coincide in effect: the CD decides when to stop
  flying and what the last recorded ordering is; the dormant comparator rung
  applies only to members the recorded outcome leaves tied.
- **F3B.2.8's aggregation silence** — the clause does not say whether the
  additional round's score is *added* to the tied competitors' totals. It must
  not be here: the tied competitors' aggregates were equal, so adding their own
  additional-round scores would order them identically but could also lift them
  past non-tied competitors — deciding more than "the winner" the clause names.
  Scores stay untouched; the ordering is the whole effect.

A rule you cannot find is a question, not an inference — nothing beyond these
clauses was assumed.

## Decisions

**D1 — the outcome is a recorded ranking, not re-captured scores.** The event
carries the group's resolved ordering: one placing per competitor, ties
explicit (equal placings), 1..* in the engine's own skip-ahead numbering. The
tie-break task is CD-defined ON THE DAY and is in no catalogue
(`PublishedClassDefinition` is immutable and adopted up front), so there are no
adopted rules from which the system could derive the fly-off's result — and
recording raw measurements would demand the whole task/entry/draw machinery
for an ad hoc construct (refused, D3). The rulebooks' outcome language is "to
achieve a ranking" / "to decide the winner" / "until the ties are broken" — the
ranking IS the outcome. Evidence (the task flown, the scoresheet) lives with
the CD; the record's `Reason` field carries it (ReflightRuling.Reason
precedent — a substantive record, not an audit breadcrumb). This does not
breach the declared-vs-derived philosophy (`DeclaredResult`'s doc comment): the
competition's own scores remain fully re-derivable; what is recorded here is
the one figure no rule derives — the fly-off's ranking.

**D2 — the resolution runs through the ranking engine, not around it.** The
engine orders the group from the recorded placings; the CD never publishes
places the engine did not assign. Where the recorded outcome leaves members
tied (B and C both 2nd in the fly-off), the dormant comparator rungs — the
comparators stated AFTER the halt rung — decide; where they tie too (or none
exist), the members share the skipped place. That is F3F.1.13's fallback
machinery operating exactly as designed: the completed tie-break story's D3
kept rungs after an operational one dormant "for exactly that future", and this
is the future.

**D3 — the address is a new event on the Competition stream.** The two
alternatives were refused:

- *A synthetic round* — a tie-break round added to the phase schedule, entries
  opened against it, measurements captured normally. Refused: it needs a new
  glossary concept ("tie-break round" — approval-gated and avoidable), a new
  kind of round that orders but never aggregates (a branch in the core scoring
  walk), unstated interactions with drops, validity and group-draw machinery,
  and aggregate pollution unless explicitly excluded. The heaviest option, and
  no rule requires it.
- *A separate aggregate* — a `TieBreak` stream per resolved group. Refused:
  every other CD declaration on the result (rulings, penalties, finalisation)
  lives on the Competition stream; this is the same authority class
  (`ReflightRulingRecorded` is the direct precedent — "the CD settling what the
  rulebook leaves open"). A separate stream would force a cross-aggregate join
  into the scoring pipeline for a small, low-volume fact.

Chosen: `TieBreakOutcomeRecorded` on the Competition stream, folded into
`Competition` state, fed to the ranking through `TieBreakContext`. The
`Competition` aggregate gains no behaviour beyond fold + decide, exactly like
`PenaltyRecorded`/`ReflightRulingRecorded`.

**D4 — matching and supersession.** An outcome applies to a halted tie group
iff (a) it was recorded for the ranking phase, (b) its competitor-ref set
equals the group's (order-independent, exact — all members, each once), and
(c) its directive is the same kind as the group's halt directive. The
LAST matching outcome in the log wins (ParameterBindings / ReflightRuling
precedent: the log keeps every decision, last-logged wins at lookup). A
non-matching outcome is inert — it stays in the log and changes nothing
(ReflightRuling's planner's call 3: a ruling whose pair never materialises
simply never matches). Inertness covers every natural drift case honestly: a
score amendment reshapes the group, a later DQ removes a member, a
`RulesAmendment` changes the ladder — the recorded outcome stops matching and
the tie surfaces as pending again.

**D5 — no gating anywhere (NFR-4).** Recording an outcome is never required;
a pending tie just sits there, shared, forever. The pending tie gates nothing —
not score capture, not finalisation (which declares the shared places as
derived), not anything else. Conversely the outcome may be recorded at any
time — before finalisation, after it, mid-contest. `FinaliseCompetition`
gains no pending-tie check: it declares what the engine derived, resolved or
shared.

**D6 — validation split: shape and rulebook context in the decide; no
pending-ness check anywhere.**

- *Decide (replay-safe)*: the phase names a drawn phase; the named directive
  is an operational kind or `UndefinedRequiresRuling` (a comparator never
  halts; `EqualPlaces` settles itself — recording against either is a
  self-contradiction, the check-18/21 shape); the directive is stated on that
  phase's `TieBreaks` in the adopted definition (the `ValidateClassRuleSilent`
  analogue — accepting an outcome where the adopted rules state no
  operational/undefined rung would let a CD believe they settled something
  that had no effect); every ref is a registered competitor (typo protection
  only — withdrawal NOT checked, the ruling precedent's planner's call 2); the
  placings are well-formed (refs distinct, ≥2, covering exactly the named
  refs, dense skip-ahead numbering from 1); `Reason` not blank; `By` optional.
- *Handler*: load-decide-append, nothing more. **It does not check that the
  group is currently pending.** The outcome lands whenever the CD records it —
  that is NFR-4's own sentence, and the ReflightRuling precedent states it
  twice over (a ruling may precede or follow the state it rules on; an inert
  ruling simply never matches). Flagged at WI-0 because the stub's
  "Before starting" left this open.

**D7 — scope: `UndefinedRequiresRuling` is included here, not split.** Same
event, same command, same engine path; the directive kind on the record
distinguishes authority (flown outcome vs CD ruling). Splitting would
duplicate the whole pipeline for zero design difference. Surfaced explicitly
for sign-off — the stub forbade silent absorption either way.

**D8 — read surface: one dedicated query; the leaderboard view is untouched.**
`GET /competition-pending-tie-breaks` (the `/competition-*` read prefix —
`/competition-result`, `/competition-teams`). `CompetitionScoreView`
(`GET /competition-result`) gains nothing: the completed story's D5 promise
("no route, DTO or projection change" for the result view) is thereby kept
intact, and the pending question gets its own small read model. No glossary
change, no new directive kinds, no new adoption checks — the vocabulary is
unchanged, and Pete's WI-0 call on the completed story was explicitly "no
glossary sentence" for tie-breaks; this story introduces no new concept (the
outcome is an event over the existing `PendingTieBreak` machinery), recorded
here so it is a decision, not an omission.

## Design

### Value objects (D1) — `src/Soarscore.Domain/Shared.cs`, beside `ReflightRuling`

```csharp
/// The CD's recorded resolution of one tie group whose ladder halted on an
/// operational or UndefinedRequiresRuling rung (D1/D3 — story header).
/// Kin to ReflightRuling: recorded, never derived; consumed by the ranking
/// engine where it matches a halted group, inert where it does not (D4).
public sealed record TieBreakOutcome
{
    /// <summary>0-based positional index into the class's phase list — the PhaseDrawn / TaskRoundCoordinate convention. Always 0 today.</summary>
    public required int PhaseOrdinal { get; init; }
    public required TieBreakDirective Directive { get; init; }
    /// <summary>The group's resolved ordering: every member exactly once, dense skip-ahead from 1, equal places for recorded ties (D1).</summary>
    public required ImmutableArray<TieBreakOutcomePlacing> Placings { get; init; }
    /// <summary>What was flown (task, scoresheet) or the ruling's basis — substantive, ReflightRuling.Reason precedent.</summary>
    public required string Reason { get; init; }
    /// <summary>Who recorded it, when the client supplies it — optional. Penalty.By precedent.</summary>
    public string? By { get; init; }
    public required DateTimeOffset At { get; init; }
}

public sealed record TieBreakOutcomePlacing(CompetitorId CompetitorRef, int PlaceInGroup);
```

### Event — `src/Soarscore.Domain/Competitions/CompetitionEvents.cs`

```csharp
[JsonDerivedType(typeof(TieBreakOutcomeRecorded), "tieBreakOutcomeRecorded")]
public sealed record TieBreakOutcomeRecorded(TieBreakOutcome Outcome) : CompetitionEvent;
```

Wrapper style per `ReflightRulingRecorded`/`PenaltyRecorded` (the header's
"payloads reuse Domain's own value-object records" convention). Bump the
header's event count comment. **No serialization work is needed for the
directive:** `CompetitionCreated` already embeds `AdoptedRules` → a full
`ClassDefinition` whose phases carry `TieBreaks`, so the `TieBreakDirective`
hierarchy already round-trips inside Competition-event payloads on both stores,
under the shared `SoarscoreEventJson.Options` (`AllowOutOfOrderMetadataProperties`
already set — the jsonb key-order trap is already answered for this hierarchy).

### Fold + decide — `src/Soarscore.Domain/Competitions/Competition.cs`

- Fold state: `public ImmutableArray<TieBreakOutcome> TieBreakOutcomes { get; init; } = [];`
  — plain append, never overwritten (the log keeps every decision; last-wins
  happens at lookup, D4).
- `Apply(TieBreakOutcomeRecorded)` appends.
- Decide:

```csharp
public Result<TieBreakOutcomeRecorded> RecordTieBreakOutcome(TieBreakOutcome outcome)
```

Validations, each its own defect code (the `recordReflightRuling.*` naming
pattern, prefix `recordTieBreakOutcome.`):

| Check | Defect code | Notes |
|---|---|---|
| `PhaseOrdinal` names a drawn phase | `phaseNotFound` | Drawn state, not merely authored — a pending tie cannot exist before the phase exists. |
| Directive is operational or `UndefinedRequiresRuling` | `directiveNotAResolution` | A comparator never halts; `EqualPlaces` settles itself. Never a decision to record against either. |
| Directive stated on `AdoptedRules.Definition.Phases[outcome.PhaseOrdinal].TieBreaks` | `directiveNotStated` | `ValidateClassRuleSilent`'s analogue. |
| Every `CompetitorRef` registered | `competitorNotFound` | Typo protection only; withdrawal NOT checked (ruling precedent). |
| Placings well-formed | `placingsMalformed` | Refs distinct; ≥2; the placing list covers exactly the refs (all, each once); dense skip-ahead from 1 — place 1 exists, and the place after a k-way tie is k+1. |
| `Reason` not blank | `reasonRequired` | `ValidateByNotBlank` precedent. |
| No uniqueness check | — | Re-recording supersedes (D4); the log keeps both. Ruling decision-2 precedent. |
| No pending-ness check | — | D6; NFR-4. |

### Command — `src/Soarscore.Application/Commands/Competitions/RecordTieBreakOutcome.cs` (new)

```csharp
public sealed record RecordTieBreakOutcome(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    TieBreakDirective Directive,
    ImmutableArray<TieBreakOutcomePlacing> Placings,
    string Reason,
    string? By = null) : ICommand<CompetitionId>;
```

Handler: `CompetitionLoader.LoadAsync` → build the `TieBreakOutcome` (`At` from
`IClock`) → `competition.RecordTieBreakOutcome(...)` → append. The
`FinaliseCompetition` handler's shape minus the scoring — the plainest
load-decide-append in the codebase (`RecordReflightRuling`'s handler is the
template).

### Query — `src/Soarscore.Application/Queries/Scoring/GetPendingTieBreaks.cs` (new)

```csharp
public readonly record struct GetPendingTieBreaks(CompetitionId CompetitionRef) : IQuery<PendingTieBreaksView>;

public sealed record PendingTieBreakView(
    int PhaseOrdinal,
    ImmutableArray<CompetitorId> CompetitorRefs,
    /// <summary>The place the group currently shares (Placings of any member — all equal).</summary>
    int SharedPlace,
    /// <summary>The halt directive, serialized with its $kind discriminator (e.g. "tiebreakFlyoff").</summary>
    TieBreakDirective Directive);

public sealed record PendingTieBreaksView(ImmutableArray<PendingTieBreakView> Ties);
```

Handler: `CompetitionLoader` → `EntryCollector` → `ScoringService.ScoreCompetition`
(the identical pipeline `ScoreCompetitionHandler` runs) → map
`result.PendingTieBreaks`: refs string→`CompetitorId.Parse` (the finding-3
idiom), `SharedPlace` from `result.Placings` (the first member's — all share),
`PhaseOrdinal` 0 (the only reachable ranking, D9 stance — see Traps).
Returning the Domain `TieBreakDirective` in a view is precedented
(`CompetitionView` returns the whole aggregate); it serialises with `$kind`
automatically — no kind-string mapping to keep in sync. No person-name
resolution: views carry refs (house pattern; no name-resolution query exists).

### Engine — `src/Soarscore.Domain/Scoring/RankingEngine.cs`

`TieBreakContext` gains a third member (optional — existing two-argument
construction sites, production and tests, compile unchanged):

```csharp
public sealed record ResolvedTieBreakOutcome(
    TieBreakDirective Directive,
    /// <summary>CompetitorRef (string, engine world) → recorded place in group.</summary>
    ImmutableDictionary<string, int> PlacesByCompetitor);

public sealed record TieBreakContext(
    ImmutableArray<TieBreakDirective> Directives,
    ImmutableDictionary<string, int> QualifyingPositions,
    ImmutableArray<ResolvedTieBreakOutcome> Outcomes = default)
```

`TieBreakContext.Display` unchanged (empty outcomes — the regression clause
holds by construction). In the place-assignment loop, for a tie group whose
halt is non-null and not `EqualPlaces` (the existing condition at
`RankingEngine.cs:183`): find the LAST outcome in `Outcomes` whose directive
kind matches `halt` and whose key set equals the group's ref set. If found,
order the group's slice by (recorded place ASC, then each DORMANT comparator
rung — the `BestDroppedScore`/`QualifyingPosition` directives stated after the
halt, evaluated with the same comparison logic as the pre-halt rungs); assign
places within the slice with the usual skip-ahead; add NO `PendingTieBreak`
entry. If not found, today's behaviour verbatim. The group's place span,
the skip-ahead arithmetic, and every competitor outside the group are
untouched.

### Threading — `src/Soarscore.Domain/Scoring/ScoringService.cs`

At the `Rank` call (the `new TieBreakContext(classDef.Phases[0].TieBreaks, …)`
site, ~:601): project `competition.TieBreakOutcomes` — filter to outcomes
whose `PhaseOrdinal` names the ranking phase (positional index 0 today, the D9
single-phase stance), map each to a `ResolvedTieBreakOutcome` (CompetitorId →
its string form, the finding-3 idiom's inverse). No `ScoreCompetition`
signature change — the outcomes are folded state on the competition, like
`Penalties`.

### Registration surfaces (all three, or it fails)

1. `src/Soarscore.Infrastructure/SoarscoreEventTypes.cs` — one line:
   `(typeof(TieBreakOutcomeRecorded), "tieBreakOutcomeRecorded")` — alias ==
   the `[JsonDerivedType]` discriminator, as for every line in that list. A
   missing line fails at runtime on BOTH backends (LADR-0001 §4.8).
2. `src/Soarscore.Api/Composition.cs` — two `AddScoped` registrations.
3. `src/Soarscore.Api/Commands/Commands.cs` / `Queries/Queries.cs` —
   `POST /record-tie-break-outcome`, `GET /competition-pending-tie-breaks`.
   GET/POST only — the route-shape test is satisfied.

## Traps (pre-answered — do not reopen inside this story)

1. **Zero fixture impact, by construction.** No frozen Gliderscore fixture
   class states a tie-break policy (the completed story's trap 1), so no
   fixture can produce a pending tie, let alone consume an outcome. The
   engine's empty-outcomes path is structurally today's path. Any fixture diff
   in the acceptance suite is a stop-triage-surface, never a "fix".
2. **The tie-break group is not drawn — do not touch the draw.** The
   deferred-decisions "Flyoff-phase draws" bullet (Draw) is about a class's
   regular fly-off phase and stays deferred. A tie-break fly-off's field IS
   the tied competitor set; no draw algorithm runs. Recording an outcome does
   not open this door.
3. **Multi-group fly-offs are recorded as separate outcomes.** If the CD flies
   one physical fly-off containing two distinct tie groups' pilots, that is
   two `TieBreakOutcome` records (one per group, each `Reason` citing the same
   fly-off) — an outcome spanning the union matches neither group (D4's exact
   set match) and would be inert. The pending query is per-group precisely so
   the CD records what the engine can match.
4. **A DQ'd or reshaped group makes the recorded outcome inert.** If a member
   of a recorded group is later disqualified, the recomputed pending group is
   smaller and the outcome never matches (exact set match); the CD re-records
   against the new group. Honest per D4 — do not "fix" by fuzzy-matching
   subsets.
5. **Dormant rungs evaluate ONLY within an outcome's residual ties.** Without a
   recorded outcome, the engine never evaluates past the halt (unchanged). With
   one, the post-halt comparators apply only to members sharing a recorded
   place — never as fresh sort keys over the whole field.
6. **Do not extend `CompetitionScoreView`.** D8 keeps the leaderboard view
   frozen; the pending surface is the dedicated query. Extending the result
   view would ripple through the ScoringACompetition acceptance scenarios for
   no read-model gain.
7. **PhaseOrdinal conventions.** The outcome/event/command use the 0-based
   positional index (the `PhaseDrawn`/`TaskRoundCoordinate` convention) — NOT
   `PhaseDefinition.Ordinal` (the definition's own vocabulary that check-17
   uses). Always 0 today; the D9 stance holds: no multi-phase handling is
   invented.
8. **`UndefinedRequiresRuling` outcomes are rulings, not flights — but the
   record is identical.** Do not add an authority field or a second event
   kind; the directive on the record already says which authority acted (D7).

## Invariant O — the property, named here per CLAUDE.md (goes verbatim into the
property test's doc comment)

*For any field of active competitors, any stated ladder producing a pending
group G sharing place P, and any outcome O recorded over exactly G's members:
(1) the multiset of places over all active competitors is unchanged by
applying O; (2) every member of G occupies a place within [P, P+|G|−1] after
applying O; (3) for any a ∉ G and b ∈ G: place(a) < P before ⇒ place(a) <
place(b) after, and place(a) > P+|G|−1 before ⇒ place(a) > place(b) after —
an outcome never moves anyone across the group's span boundary; (4) G surfaces
no `PendingTieBreak` after applying O; (5) with no recorded outcomes the
ranking result — placings and `PendingTieBreaks` — is identical to the
outcome-less result (regression clause); (6) an outcome whose ref set or
directive does not match any halted group changes nothing.*

Generator guidance: fields with one or more pending groups (operational and
`UndefinedRequiresRuling` halts), outcomes that are strict orderings and
outcomes with recorded ties, and outcome sets covering the matching, the
superseding (two outcomes, last wins) and the non-matching cases. This
property test lives beside `RankingEnginePropertyTests` and asserts all six
clauses.

## Work items

Each WI lands compiling with its checkpoint green; WI-1 → WI-2 → WI-3 are
strictly sequential (each consumes the previous); WI-4 needs WI-3; WI-5 closes
out. Context budgets are deliberate — a sub-agent given one WI needs only the
files listed as *read*.

### WI-0 — Board and gates

`git mv` the story to `in-progress/`, update the status header in the same
commit. Get Pete's sign-off on:

- **D1 + D2** — the outcome is a recorded ranking (with explicit recorded
  ties), resolved through the ranking engine; figures/raw-measurement capture
  refused. The pitch: "the rulebooks' outcome IS a ranking; the tie-break task
  is in no catalogue, so there is nothing to derive it from — the CD's
  ordering is the one figure no rule produces, and the engine still assigns
  every published place."
- **D3** — the address is a Competition-stream event; synthetic round and
  separate aggregate refused (synthetic refused partly BECAUSE it would need a
  new glossary concept, which is approval-gated and avoidable).
- **D6** — no pending-ness validation: the outcome lands whenever the CD
  records it (NFR-4's own sentence; the ReflightRuling precedent).
- **D7** — `UndefinedRequiresRuling` included here, one mechanism for flown
  outcomes and rulings.
- **The two flagged rule readings** (F3F's "if this is not possible"; F3B's
  aggregation silence) — noted, no design consequence.
- **The docs edits** (house rule 4 — ask, then apply; proposed wording at
  WI-0, not improvised later):
  - `docs/aggregate-roots.md` §3 — the "lightly mutated afterwards" mutation
    list gains "record a tie-break outcome" (one clause).
  - `docs/users.md` — the CD-authority enumerations (the "locking the final
    result" sentence and the "mid-contest interventions" list) gain tie-break
    resolution (one word/clause each).
  - Glossary: **no change** (carrying Pete's explicit "no glossary sentence"
    call on the completed story; this design introduces no new concept).
- **No-new-adoption-checks confirmation** — the directive vocabulary and the
  checks 17–21 inventory are untouched.

### WI-1 — Domain: value objects, event, fold, decide (+ tests)

*Read:* `src/Soarscore.Domain/Shared.cs` (`ReflightRuling` — the authority
precedent), `src/Soarscore.Domain/Competitions/CompetitionEvents.cs` (header
conventions), `src/Soarscore.Domain/Competitions/Competition.cs` (fold state,
the `Apply` switch ~:721, `RecordReflightRuling` ~:2172 and its validators —
the decide template), `tests/Soarscore.Domain.Tests/RecordReflightRulingDecideTests.cs`,
`CompetitionFoldTests.cs`. *Touch:* `Shared.cs`, `CompetitionEvents.cs`,
`Competition.cs`, and those two test files (plus new ones beside them if the
shape wants it).

- Implement the *Value objects*, *Event*, *Fold + decide* sections above —
  every defect code from the table, one decide test per code (violating input
  → exactly that defect), plus fold tests (append never overwrites; two
  outcomes for one group both survive) and a well-formed/supersede happy path.
- No engine or Application work in this WI — nothing reads the fold yet.

**Checkpoint:** `dotnet build Soarscore.sln`; then
`dotnet test tests/Soarscore.Domain.Tests` green.

### WI-2 — Domain: engine + scoring service (+ property tests)

*Read:* `src/Soarscore.Domain/Scoring/RankingEngine.cs`,
`src/Soarscore.Domain/Scoring/ScoringService.cs` (the `Rank` call ~:594-605),
`src/Soarscore.Domain/Scoring/ScoringResultTypes.cs` (the `PendingTieBreaks`
doc comment), `RankingEngineTests.cs`, `RankingEnginePropertyTests.cs`.
*Touch:* `RankingEngine.cs` (`TieBreakContext` + `ResolvedTieBreakOutcome` +
the group-resolution path), `ScoringService.cs` (the projection at the `Rank`
call), `ScoringResultTypes.cs` (retire the "HTTP/read-model exposure is out of
scope" sentence from `PendingTieBreaks`' doc comment — it now has a surface
and a resolution path; keep the never-a-write-gate sentence), both ranking
test files.

- Unit tests: match + reorder + no pending; recorded ties resolved by the
  dormant comparator (the F3F shape — `bestDroppedScore` after
  `classificationRounds`); recorded ties shared when no dormant rungs exist
  (the F3K shape — `tiebreakFlyoff` last); supersession (two matching
  outcomes, last wins); set-mismatch inert; directive-mismatch inert (a
  `RulesAmended`-style context where the halt rung changed); the group's span
  and outsiders untouched.
- Property tests: **Invariant O**, all six clauses, per the generator guidance.

**Checkpoint:** `dotnet build Soarscore.sln`; `dotnet test
tests/Soarscore.Domain.Tests tests/Soarscore.Application.Tests
tests/Soarscore.Architecture.Tests` green.

### WI-3 — Application + Api + Infrastructure: command, query, routes, registration

*Read:* `src/Soarscore.Application/Commands/Competitions/RecordReflightRuling.cs`
(the handler template), `FinaliseCompetition.cs` (loader/collector shape),
`src/Soarscore.Application/Queries/Scoring/ScoreCompetition.cs` (the pipeline
to mirror), `src/Soarscore.Infrastructure/SoarscoreEventTypes.cs`,
`src/Soarscore.Api/Composition.cs`, `Commands.cs`, `Queries.cs`. *Touch:* the
two new Application files, the three registration surfaces, and a store-backed
round-trip test beside `tests/Soarscore.Infrastructure.Tests/EventStoreTests.cs`
(append a `TieBreakOutcomeRecorded`, read it back — sqlite untagged, postgres
`Category=Storage` wherever Docker exists).

**Checkpoint:** full solution build; `dotnet test tests/Soarscore.Infrastructure.Tests`
(sqlite legs) green — proves the `SoarscoreEventTypes` line on a real store.

### WI-4 — BDD acceptance: ResolvingATieBreak.feature

*Read:* `tests/Soarscore.Acceptance.Tests/Features/RecordingAReflightRuling.feature`
+ its steps — the authority precedent's feature, and the shape to mirror;
`Support/Gliderscore/SeedDefinitionLoader.cs` (adopting a corpus seed class
through the same ingestion options a human POST uses). *Touch:* one new
feature + one new steps file.

Definition provisioning: adopt the F5L seed (`60-f5l` — `undefinedRequiresRuling`
on both phases: ANY Score tie pends, no fly-off needed) and the F3K seed
(`10-f3k` — `tiebreakFlyoff` after `bestDroppedScore`; a single-drop class
where two competitors tie on Score AND dropped cell reaches the fly-off rung)
via `SeedDefinitionLoader`, following the parallel-run feature's precedent.
(The seed JSON is a generated artifact — `dotnet run --project
tools/Soarscore.SeedData` first; the HarnessSelfCheck steps are the model for
surfacing that dependency.) Engineer the tie through the seeded-pace helpers.

Scenarios:

1. **The pending tie surfaces** — two competitors tie on Score; the query
   returns the pair, their shared place, and the directive.
2. **Recording the outcome re-ranks** — the CD records the fly-off/ruling
   ordering; the result shows distinct places in the recorded order; the group
   no longer appears as pending; everyone outside the group keeps their place.
3. **A changed mind follows the most recently recorded outcome** — re-record;
   the ordering follows the new record (supersession, the RR feature's
   scenario-3 shape).
4. **A malformed record is refused** — unknown competitor / gapped placings /
   a directive the class does not state → the defect code, result unchanged.
5. **Nothing is gated while a tie pends (NFR-4)** — with the tie pending,
   entries and scores keep flowing normally; finalisation is not blocked and
   declares the shared places as derived.

**Checkpoint:** `SOARSCORE_TEST_STORE=sqlite dotnet test
tests/Soarscore.Acceptance.Tests` green; `postgres` wherever Docker exists.

### WI-5 — Close-out

- Full-suite regression: `dotnet test Soarscore.sln` green; the acceptance
  suite under both `SOARSCORE_TEST_STORE` values. **Expected: zero new fixture
  diffs** (trap 1); any diff → stop, triage, surface to Pete with the numbers.
- `kanban/deferred-decisions.md`: nothing absorbed, nothing added (house
  rules 5–6) — verify the "Flyoff-phase draws" bullet is untouched (trap 2).
- `kanban/tech-debt.md`: reconcile; nothing expected.
- `git mv` to `completed/`, status header same commit.

**Finish line:** `dotnet test Soarscore.sln` green, acceptance green on both
stores with zero new fixture diffs. Known flake: solution-wide Marten
migration race (`tech-debt.md` last item) — re-run the project alone before
diagnosing.

## Out of scope

- **Scoring the tie-break flights** — capturing raw measurements for the
  fly-off / additional round / classification rounds, normalising them, or
  recording the CD's day-of task as structured data (the `Reason` field
  carries it in prose; a structured task snapshot is a new story if ever
  wanted). D1 records the outcome, not the flights.
- **The regular fly-off phase** — field selection (the deferred "Flyoff-phase
  draws" bullet), `QualifyingPosition` wiring, promotion, phase-scope
  finalisation: all D9/multi-phase contest-flow work, untouched here (trap 7).
- **Team tie-breaks** (`C.15.6.2`, the completed story's D6) — unaffected.
- **NZ classes** — `EqualPlaces` everywhere; no pending ties, no outcomes.
- **Glossary, notation, and rules docs** — untouched (`docs/rules/` is
  read-only; the notation describes definitions, which do not change).
- **F3F's five-best scoping** — still unmodelled (the completed story's D7).

## Story invariant for sign-off

The pending tie is surfaced by a dedicated query and resolved by one recorded
ordering: `RecordTieBreakOutcome` lands a `TieBreakOutcomeRecorded` on the
Competition stream whenever the CD records it — validated for shape and
rulebook context, never for pending-ness (NFR-4); the ranking engine matches
it by phase, exact competitor set and directive kind, orders the group within
its own place span through the recorded placings with dormant comparator rungs
separating residual recorded ties, and leaves unmatched outcomes inert; with
no outcomes recorded, every result is byte-identical to today; NZ's
`EqualPlaces` ladders never surface and never resolve; no fixture moves; no
glossary concept, directive kind, or adoption check is added; the leaderboard
view is unchanged; both stores pass; no `src/` file names a class.
