# Story — Entry-scoped point-deduction penalties are inert

**Status:** In progress · **Raised:** 2026-08-26 (found by
`kanban/completed/gliderscore-replay-and-compare-harness.md` WI-3) ·
**Scoped:** 2026-08-27 — resolution is **option 1, wire it** (decision argued
below; option 2 rejected).

## What

`PenaltyEngine.ApplyRawPenalties`
(`src/Soarscore.Domain/Scoring/PenaltyEngine.cs:29-63`) honours only `ZeroFlight`
/ `ZeroRound` / `ZeroTask`; `DeductPoints` is aggregate-only
(`ApplyAggregatePenalties`, lines 72-138). But `RecordEntryPenalty`
(`src/Soarscore.Application/Commands/Entries/RecordEntryPenalty.cs`) validates any
declared infraction type (`Entry.RecordPenalty`,
`src/Soarscore.Domain/Entries/Entry.cs:496-534`, check
`recordPenalty.infractionTypeNotDeclared`), and a class definition may declare an
entry-scoped `deduct <pts>` penalty. Such a record passes validation, enters the
event log — and then no score changes anywhere:

- `ScoringService.GetEntryPenalties` (`src/Soarscore.Domain/Scoring/ScoringService.cs:441-446`)
  feeds Flight/Entry-scoped records **only** into `ApplyRawPenalties`, which skips
  their `DeductPoints` effects.
- `GetAggregatePenalties` (lines 457-464) reads only `Competition.Penalties`
  filtered to TaskRound/Competition scope, so entry-scoped records never reach
  `ApplyAggregatePenalties` either.

A CD-visible no-op on an immutable audit trail. Same root cause, third symptom:
a TaskRound/Competition-scoped record of a definition carrying a Zero* effect
(e.g. F3B's own `nonConformingWinch`: ZeroFlight + DeductPoints 1000,
`tools/Soarscore.SeedData/SeedF3B.cs:163-170`) zeroes nothing — its raw-stage
half never sees `ApplyRawPenalties`. This story wires the first direction; the
mirror gap and two smaller residuals become stubs (WI-5).

## Decision (argued, per "to be argued in-story")

**Option 1 — wire it** (user decision, 2026-08-27). Route Flight/Entry-scoped
`DeductPoints` into the pipeline at GliderScore's placement: inside the raw
score, pre-normalisation.

The argument, with the corpus evidence gathered 2026-08-27:

| Evidence | Says |
|---|---|
| FAI invariant (fai-rules skill, `docs/rules/00-general-rules.md` §6 / C.19) | Recordable point penalties deduct from the **final** score; a score that would go negative records as zero. No FAI class requires a pre-normalisation *recordable* deduction today. |
| Pre-normalisation deductions in FAI | All are **derived score terms**, not recorded infractions — F5J start-height (`5.5.11.12 e`), F5K NLH (`5.5.10.3–10.4`), F3J/F3B overtime, F5L overfly. `SeedF3J.cs:81`: "A DERIVED deduction, not a Penalty: nobody records an infraction." |
| NZ classes (`docs/rules/nz/`) | No aggregate deductions at all; hardware limits are not scoring data. No late-landing-style clause. |
| GliderScore `varFltDednIdx=1` late landing (`resolve-gliderscore-scoring-arithmetic.md` L363) | `FltPenalty` subtracted inside `RawScore`, pre-normalisation — the one proven scheme that needs this route (fixture-proven by `f3j-international`). |
| Model contract already pinned | `scoring-service-plan.md` WI-6 criterion: "one infraction with two effects — both apply at their respective stages", pinned end-to-end only at unit level (`PenaltyEngineTests.cs:196-227`). The real pipeline breaks that contract for entry-scoped records, exactly as this story says. |
| Class data vs penalty: different schemes | A metric + rate term deducts an *arbitrary captured amount* per flight; a declared penalty deducts a *fixed declared amount per recorded occurrence*. Neither subsumes the other. Today only the first is expressible — and only by faking a measurement into the capture workflow (what `tests/GliderscoreFixtures/f3j-international/class-definition.json` had to do: metric `lateLandingDeduction` + rate term at ~lines 69 and 224). |

Why wire beats reject:

1. **Reject cannot be implemented cleanly.** `PenaltyDefinition` carries no
   scope of its own, so "an entry-scoped declaration whose only effects are
   aggregate-stage" cannot be recognised at adoption time — the same definition
   may legitimately be recorded at Competition scope later (F3B
   `nonConformingWinch` is mixed-effect by rule, not by accident). Reject would
   have to fire per *recording*, refusing records whose *combination* the CD
   chose — inventing scope-validation machinery, a new concept, to make
   silence instead of arithmetic.
2. **The audit-trail objection cuts for wiring too.** Refusing at record time
   still leaves the adopted definition misleading the class author (adopted
   classes validate today); wiring makes every recorded infraction determinate.
3. **It delivers what GS's placement demands** without touching capture UX:
   the late-landing flavour of deduction becomes declarable class data, and
   imported rulebook configs stop needing the metric-faking workaround.
4. Residual inertness is strictly narrowed to `Disqualify` on entry-scoped
   declarations and the mirrored Zero*-on-aggregate-record gap — both named as
   stubs rather than silently kept (WI-5).

## Why it matters

A trust-model audit trail that records a penalty which changes nothing is worse
than refusing it. And until resolved, imported rulebooks whose deductions act
pre-normalisation must be authored around (as f3j-international was), not
declared. Wiring closes both.

## Cross-references checked (housekeeping rule 2)

- NFR-1/NFR-2 (class model owns variance; additive-only extension): wiring is
  data-driven generic engine behaviour — compliant, no class branch.
- NFR-4 (no imposed ordering): penalties stay recordable at any time; scoring
  is computed at read/finalise time — unchanged.
- `00-general-rules.md` §6 / C.19 ("deducted from the final score"): a wired
  entry-scoped deduction does NOT replace the aggregate stage for
  TaskRound/Competition-scoped records — those semantics are untouched. What
  changes is that the *effect enum alone* no longer decides the stage; see D1
  and the doc-conflict gate in WI-4.
- `deferred-decisions.md`: nothing contradicts; the related parked view-field
  item is `kanban/backlog/pre-normalisation-score-view-field.md` (displaying
  pre-normalisation scores is how deductions will become visible to users —
  separate story).
- Rule-conflict flagged for user approval before any `/docs` edit: the
  class-diagram note (`docs/soaring-domain-class-diagram.md:430-439`)
  states "the pipeline stage is a property of the EFFECT". D1 amends that law;
  approval is required to change that note (and nothing else under `docs/`).

## Design decisions (settled here, cited from code)

### D1 — Stage follows recorded scope; effect picks the action within the stage

Recorded scope is the only staging knowledge the model already has, and it is
the truth of the pipeline: an Entry exists only up to its group's normalisation
input, so its penalties are visible nowhere later. Therefore:

- **Flight/Entry scope ⇒ the task-round (raw/pre-normalisation) stage owns ALL
  effects** of matching definitions. Within it: Zero* → NoResult (today);
  `DeductPoints` → subtract the accrued contribution from `TaskResult.RawScore`.
- **TaskRound/Competition scope ⇒ the aggregate stage** — unchanged in every way.

Consequences, deliberate:

- An entry-scoped `deduct 30` fires **once**, pre-normalisation. No double-count:
  `GetAggregatePenalties` continues to read `Competition.Penalties` only.
- The same infraction type deducted from Entry scope scales with the group's
  winner basis (it is inside the ratio), while Competition scope deducts flat
  points off the final total. That difference IS the GS distinction between
  flight-scores and post-sum penalties; it is data-chosen by where the CD
  records, not configured anywhere (no appliedAt survives — the recorded
  record's residence decides visibility).

### D2 — Accrual and exclusion-group semantics at the raw stage are identical to the aggregate stage

Extract the accrued-contribution computation (accrual PerOccurrence × count /
OncePerAttempt once; single-pass `ResolveExclusion` suppression) into a shared
private helper used by both apply functions — reuse, not reimplement
(`ResolveExclusion` is already extracted, `PenaltyEngine.cs:146-209`; extract
the contribution loop from `ApplyAggregatePenalties:84-112` next to it).

This finally makes `PenaltyDefinition.ExclusionGroups`'s doc comment true at
every reachable site ("Within one flight attempt at most one penalty from a
group applies" — `ClassDefinition.cs:77-82`). Adoption check 16
(`ClassDefinitionValidation.cs:420-442`) is unchanged and stays correct: only
all-DeductPoints definitions may join groups, so a Zero*-carrying definition can
never be suppressed out of zeroing.

### D3 — Ordering within one entry's penalty set: contribution, suppression, then zeroing dominance

For each entry, in ONE pass over its `RecordedPenalty`s (order-independent):
match definitions; accrue contributions; resolve exclusions among them; then

- if **any** matched definition (suppressed or not — Zero* defs cannot be in
  groups per check 16, so suppression cannot touch them) has a Zero* effect →
  return the existing early-out shape: `NoResult`, `Selection = null`,
  `RawScore = 0`. A deducted-then-zeroed value is arithmetically moot downstream
  (NoResult contributes nothing after normalisation), which keeps the pinned
  unit shape (`PenaltyEngineTests.cs:196-227`) intact.
- otherwise subtract the surviving total from `result.RawScore` (state stays
  Valid, selection untouched).

Placement relative to GS: `ScoreGroup` step 2c
(`src/Soarscore.Domain/Scoring/ScoringService.cs:109-114`) applies penalties to
`FlightSelector.SelectAndScore`'s output — i.e. after per-flight target clamping
and task-level rounding, immediately before `NormalisationEngine.Normalise`. The
deduction therefore feeds **winner finding**: a penalised best raw no longer
anchors the group's 1000 (winner = max over deducted raws,
`NormalisationEngine.cs:70-84,119-122`), which is exactly GS idx=1 behaviour and
is pinned by an acceptance assertion.

### D4 — Floor: a deducted HigherIsBetter raw never goes below zero

`00-general-rules.md` §6: "A score that would go negative is recorded as **zero**
(penalties still stand)". At the raw stage the analogue floors the deducted
HigherIsBetter raw at 0 before normalisation. For LowerIsBetter tasks
(F3B speed / F3F) a declared-points deduction pre-normalisation has no
rule-grounded meaning in either rulebook — the property suite pins the
HigherIsBetter floor, and the LowerIsBetter case is left behaving as an additive
worsening (+points toward worse), documented in the function comment, until a
rulebook or fixture actually exercises it (surface it if one ever does).

### D5 — Existing fixtures and seed classes are unaffected byte-for-byte

Nothing changes until a class declares an entry-scoped `DeductPoints`
definition AND such infractions get recorded. All seven FAI seed classes declare
their `deduct` definitions for Competition-scope recording (aggregate stage —
unchanged); f3j-international stays on its metric+rate-term authoring. Its
equivalence condition is worth recording in the WI notes: penalty-subtract lands
after rounding while the rate term sits inside the sum, indistinguishable here
because −30 is integral against a 0.1 grid.

### D6 — Read-side tolerance unchanged

Unmatched infraction types remain skipped by the engine (events-already-in-log
safety net, `Entry.RecordPenalty` doc comment). `Disqualify` encountered on an
entry-owned record is acknowledged-not-actioned at the raw stage — logged in the
function comment as residual R1, not silently dropped further.

## Work items

Each WI lands compiling with its checkpoint green. WIs are sequential (one
compile unit through WI-3); WI-4/WI-5 close out. Code cites work items as
`kanban/backlog/entry-scoped-deduct-points-penalties-inert.md#wi-n`.

### WI-0 — Board and residual stubs

1. `git mv` this story to `in-progress/`, status header updated in the same commit.
2. Create the stubs this scoping produces (so they survive independent of this
   thread finishing):

   - `kanban/backlog/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md`:
     mirrors of the no-op this story leaves open — (a) a TaskRound/Competition-
     scoped record of a Zero*-carrying definition zeroes nothing
     (F3B `nonConformingWinch` is the live example; needs Zero* routing into the
     group walk using `Penalty.TaskRound` / subject filters);
     (b) `Disqualify` on an entry-scoped record sets no flag (needs carry-out of
     an aggregate-stage action from a raw-owned record);
     (c) hardening idea noted there: let a definition optionally declare its
     permitted recording scopes (new field — glossary-gated, argue there).
   - Nothing added to `tech-debt.md` (no deferred debt made insufficient — D5).

3. Get explicit user approval for the one-line amendment to the class-diagram
   PenaltyEffectSpec note (`docs/soaring-domain-class-diagram.md:430-439`) —
   proposed wording: *"the pipeline stage is a property of the EFFECT within the
   stages where the recorded penalty's SCOPE makes it visible: Flight/Entry
   records act at the task-round stage, TaskRound/Competition records at the
   final aggregate"* — and apply it only if granted.

### WI-1 — Wire DeductPoints into the raw stage

`src/Soarscore.Domain/Scoring/PenaltyEngine.cs`:

- Extract shared helper `Accrue(penalties, definitions)` returning the
  per-definition contribution dictionary exactly as `ApplyAggregatePenalties`
  builds it today (move lines 84-112 verbatim behind one signature used by both
  apply functions). Behaviour-neutral refactor asserted by the whole existing
  suite staying green untouched.
- `ApplyRawPenalties` gains D3/D4: skip NoResult inputs; accrue; suppress via
  existing `ResolveExclusion`; Zero*-dominance early-out preserved; else
  `result with { RawScore = Max(0m, result.RawScore - total) }` (floor per D4
  with its C.19 citation in the comment).
- Update the file-header block comment and the XML docs (header claims effects
  per stage; say D1 now).
- `ClassDefinition.cs:64-69` `PenaltyEffectSpec` design comment rewritten per D1
  (code comment — the gated twin lives in WI-0 step 3).

Checkpoint: `dotnet build Soarscore.sln`;
`dotnet test tests/Soarscore.Domain.Tests tests/Soarscore.Application.Tests
tests/Soarscore.Architecture.Tests`.

### WI-2 — Unit tests pinning the new engine behaviour

`tests/Soarscore.Domain.Tests/PenaltyEngineTests.cs` (extend, black-box style):

- entry-scoped-shaped call: two `PerOccurrence` deductions × 2 occurrences →
  raw reduced by 400.
- OncePerAttempt ignores occurrence count.
- exclusion group: `{objectContact(100), personContact(300)}` recorded together →
  300 subtracted, not 400 (same shape as the aggregate test at lines 119-147).
- mixed Zero+Deduct definition → NoResult early-out wins, RawScore 0 (existing
  test at lines 196-227 must still pass unmodified — that is the D3 contract).
- deduction pushing raw negative floors at 0 (D4).
- pure-deduct definition produces Valid result with reduced raw and intact
  Selection (state/selection contract: normalisation and reflight collapse read
  these fields).
- behavioural-refactor guard: every aggregate-stage test above runs green
  unmodified after the extraction.

### WI-3 — Property tests: named invariants (CsCheck)

Extend `tests/Soarscore.Domain.Tests` (pattern: `ScoringServicePropertyTests.cs`
generators building scored competitions with penalty facts). Invariants stated
at planning, per house style:

- **P-RawSymmetry:** for the same recorded set and definitions,
  `ApplyRawPenalties`'s surviving-deduction total equals
  `ApplyAggregatePenalties.Deduction` modulo the ≥0 floor — because both run the
  one shared accrual + `ResolveExclusion` path (this is the property that keeps
  the two stages provably in lockstep as future rules land).
- **P-RawOrderIndependence:** the resulting `TaskResult` is invariant under any
  permutation of the recorded `Penalty`s on the Entry (single-pass guarantee,
  mirroring the aggregate algorithm's documented order-independence,
  `PenaltyEngine.cs:4-7`).
- Generator facts: occurrence counts 0..3, PerOccurrence/OncePerAttempt mix,
  exclusion groups from a small vocabulary, optional Zero-carrying co-def.

Checkpoint: `dotnet test tests/Soarscore.Domain.Tests`.

### WI-4 — Acceptance: user-visible workflow

`tests/Soarscore.Acceptance.Tests/Features/ScoringACompetition.feature` +
`Steps/ScoringACompetitionSteps.cs` (fixtures/steps beside the two existing
penalty scenarios, lines 47-60):

```gherkin
Scenario: An entry-scoped deduction lowers the flight score before normalisation
  Given a published class declaring a PerOccurrence entry-scoped deduct-points penalty
  And a competition adopting it, drawn, with competitors flying alongside competitor 1
  When competitor 1 commits the infraction twice and everyone else flies clean
  Then competitor 1's pre-normalisation group score is 200 lower than their unpenalised raw
  And competitor 1 is not the group winner-anchor if any clean flight outscores their deducted raw
```

(A simpler second assertion variant: group winner scores 1000 off the clean best
raw while competitor 1's normalised cell reflects the deducted ratio.)

Run both legs: `SOARSCORE_TEST_STORE=sqlite dotnet test
tests/Soarscore.Acceptance.Tests` and again unset (postgres default) wherever
Docker exists — per CLAUDE.md, a backend is supported only with this suite green
unchanged.

### WI-5 — Closeout

Reconcile board state: move to `completed/` (`git mv` + status header), tick/
append `tech-debt.md` (expect: none), confirm the two new backlog stubs exist
with citations back to this story, and `graphify update .`.

## Out of scope

- The mirrored gaps aggregated into the WI-0 stub (aggregate-recorded Zero*,
  entry-scoped Disqualify, permitted-scope hardening).
- Displaying pre-normalisation scores (`pre-normalisation-score-view-field.md`).
- Any change to event shapes, store aliases, API surface, or the write-side
  validation in `Entry.RecordPenalty` / `RecordEntryPenaltyHandler` — the command
  layer needs zero edits; this is a read-path engine fix.
- Re-authoring f3j-international onto the penalty route (its fixture stays as
  the metric+term witness; equivalence recorded in D5).
