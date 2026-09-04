# Story — Operational tie-break resolution: record the outcome, re-rank

**Status:** Backlog · **Raised:** 2026-09-04 (from the `nz-ndc-seed-classes` ruling
review — Pete story-stubbed the gap: "nothing records or executes a tie-break
fly-off result")

## What

Give contest flow a way to act on a `PendingTieBreak`. Today the ranking engine
halts at an operational rung with the tie intact, shares the places and annotates
the result (`CompetitionResult.PendingTieBreaks`, D5 of
`kanban/completed/tie-break-policy-in-class-definition.md`) — and nothing further
exists. The CD can fly the tie-break in the real world, but the outcome has no
address to land in and the ranking cannot consume it. Three parts, order TBD at
flesh-out:

1. **Read surface.** `PendingTieBreaks` is domain-only today — its doc comment
   (`src/Soarscore.Domain/Scoring/ScoringResultTypes.cs:238`) says HTTP/read-model
   exposure was deliberately out of scope. The CD cannot see a pending tie without
   reading raw events. A query surfacing each pending tie group (competitors +
   directive) is the prerequisite for everything else.
2. **Record the outcome.** Whatever the directive names — a CD-defined one-task
   fly-off for the tied competitors (`TieBreakFlyoff`; F3K.10.2 / 5.5.10.17), an
   additional full round over all the class's tasks (`AdditionalFullRound`;
   F3B.2.8), more rounds of the class's task (`ClassificationRounds`; F3F.1.13) —
   captured as immutable events in the audit log, like every other mutation.
3. **Re-rank.** The recorded outcome resolves the tie group's ordering; the
   published result reflects it.

## Why it matters

Four corpus classes state operational rungs and can produce unresolved ties at
club events: `20-f3b` (`AdditionalFullRound`), `10-f3k` and `40-f5k`
(`TieBreakFlyoff` after `BestDroppedScore`), `70-f3f` (`ClassificationRounds`
first). For them, a tie at the end of a contest is a permanent shared place plus
an obligation the software records nowhere — the CD's tie-break result has no
address to land in, and the final published result can never reflect who won the
fly-off. This is the unrouted half of the tie-break story's design: the
operational directives were made stateable precisely so contest flow could act
on them as data.

No NZ class is in scope: Pete ruled 2026-09-04 that NZ has NO tie-breaking at
all — ties are never broken, announced equal ("1st equal") at every placing —
and all eight NZ seeds encode that as the `EqualPlaces` directive (see
`kanban/deferred-decisions.md`, Competition class model). Do not re-import
tie-break machinery there.

## Before starting

- Read `kanban/completed/tie-break-policy-in-class-definition.md` first — D5
  (surfacing as data, never a write gate), D10 (the F3K/F5K fly-off clauses are
  preliminary-scoped: "separate" presupposes the regular fly-off phase has not
  absorbed the tied pilots) and D9 (multi-phase contest-flow orchestration, the
  fly-off-phase `QualifyingPosition` wiring — a DIFFERENT thread; do not merge).
  Also the deferred "Flyoff-phase draws" bullet in `kanban/deferred-decisions.md`
  (Draw): fly-off field selection is a different algorithm from the standing one.
- Rules first, via the fai-rules skill: for each directive, does the tie-break
  outcome touch scores or only ordering? The working assumption is ranking
  machinery only — but F3B.2.8's additional full round and F3F.1.13's
  classification rounds must be verified per clause, and the F3K/F5K fly-off
  clauses re-checked in the 2026 editions. A rule you cannot find is a question,
  not an inference.
- The crux design question: the tie-break task is CD-defined ON THE DAY and is
  in no catalogue, while `PublishedClassDefinition` is immutable and adopted up
  front. How the outcome lands in the event model (new events on the competition
  stream? a separate aggregate? a synthetic round?) is the story's central
  decision — hold it against the core architectural law (no class-specific
  branching; the directives are class data and the mechanism must read them
  generically).
- Scope boundary to settle at flesh-out: `UndefinedRequiresRuling` ties (F5L
  only now — the NZ classes state `EqualPlaces` per Pete's 2026-09-04 ruling
  and are out) halt identically but need a CD *ruling*, not flown rounds. Same
  surface, different authority — include here or split into a sibling story;
  do not silently absorb either way.
- NFR-4 applies: a pending tie must never gate score capture or anything else;
  the outcome lands whenever the CD records it.
- No-new-concepts gate: if the design wants a new glossary concept (a
  "tie-break round"?) or a new directive kind, stop and surface — glossary
  changes need approval.
