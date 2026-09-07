# Story — Literal record replay scenarios (a whole fixture readable as Gherkin)

**Status:** Backlog · **Raised:** 2026-09-04 (design settled interactively same
day; the six decisions below are owner-confirmed, not proposals)

## What

A second kind of GliderScore replay scenario, beside the JSON-driven harness
ones, in which the competition itself is visible in the feature file: one
"`<pilot>` enters his scores" block per pilot carrying the whole draw (real
flights as `Time`/`Landing` rows, unflown slots as explicit `no flight`
markers), hand-authored, closing with completion/finalise steps and a literal
placings table — `| Place | Name | Score | Dropped |` with GS's `=n` tie
strings. First target: `ales-sample-comp` (10 ZZ test pilots × 3 rounds × 1
group, 30 rows, no PII).

Settled design decisions (2026-09-04, Pete):

1. **Purpose** — human-readable record first; CI greenness secondary. The
   feature file is the readable record of an entire competition.
2. **Structure** — per-pilot entry blocks only, nothing interleaved between
   them; sequencing lives in closing steps. Blocks ordered by final placing
   (winner first — it reads as the comp's story, and entering out of draw
   order *demonstrates* NFR-4's no-imposed-ordering on score capture).
3. **Authoring** — hand-written, guarded by an in-scenario self-check step
   that diffs the entered tables against `scores-raw.json` cell-for-cell
   (packed-mmss decode, landing, penalty) and **names any typo'd cell
   directly** — authoring errors must not surface disguised as comparator
   failures.
4. **Column schema** — the union column set is designed once (Task, Laps,
   Time, Landing, Height, Penalty, re-flight marker); each fixture's table
   shows only the columns it uses; one family-agnostic step definition maps
   column names → capture fields.
5. **Unflown slots** — explicit `no flight` markers; the whole draw is visible
   in the feature text (ales's 24 `Updated='False'` rows appear as markers).
6. **The Then** — Place, Name, Score, Dropped, derived from
   `expected-result.json` joined to pilot names; the literal table sits beside
   the existing three-grain machinery, which stays the referee (belt and
   braces — precedent: the f3j-international team ladder already pinned
   verbatim in feature text).

Lives in a **new feature file** beside `ReplayingAGliderscoreFixture.feature`
(record character, not harness character); the JSON-driven scenarios stay
untouched. Reuses the `ReplayDriver`'s capture command surface — no new
command surface.

## Why it matters

The JSON harness proves parity but is opaque — the comp's data is never seen,
only referenced. A literal record makes a corpus fixture readable by someone
who has never seen the codebase, demonstrates behaviours the opaque When step
cannot show (out-of-order capture per NFR-4), and gives future work a legible
vehicle: `kanban/backlog/seed-definition-parallel-run.md`'s difference
reports are far more meaningful in this style.

## Before starting

- **Widening constraints to record in the plan** (settled as "record, don't
  solve"): (a) round-completion point — recommend explicit per-round
  "completed and scored" steps before finalise rather than one silent close;
  (b) `no flight` vs *genuine* zero — ales's zeros are all `Updated='False'`
  so the marker is unambiguous, but jerilderie-2010 (genuine zeros) and
  f5j-nz-south-island (flagged zero-time rows) will need a third marker or
  distinct wording — widen only when a first literal scenario per shape lands;
  (c) draw tables — omitted for ales (single group, same order every round),
  required only when a fixture's realised draw actually varies.
- **Step-definition plumbing:** name → PilotNo resolution from `entries.json`
  (verbatim ZZ names, unique); packed-mmss → `mm:ss` decode mirrors
  `ReplayDriver.CaptureDurationInputs`; `no flight` rows are NOT captured
  (`CaptureSlotsSkippingZeros` semantics).
- **Dropped column data source:** the engine's own dropped-cell contributions
  (the conservation machinery already knows them); for ales it is `—`
  everywhere (no drops configured).
- **Corpus discipline:** ales-sample-comp's committed fixture files, ledger
  and existing scenarios are untouched by this story.
- **No new domain concepts** — "record scenario" is harness vocabulary, not a
  glossary term; nothing in `/docs` changes.