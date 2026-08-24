# Flights within an Entry can be recorded out of order

**Status:** Backlog · Planned · **Raised:** 2026-08-18 · **Planned:** 2026-08-24

## What

A pilot entering an F3K task retrospectively reaches for their best flights
first — "my 62 and my 58, then the rest" — and cannot record them: flight
sequence must arrive exactly in order. The constraint is *within* one Entry
(one competitor, one task-round), so it orders nothing across rounds, groups or
competitors; but within a multi-launch task it is a real ordering rule the
system imposes on its users, contradicting the governing principle that
Soarscore does not dictate how or when scores are collected and entered.

**Premise correction (2026-08-24 planning):** the constraint lives twice, and
the stub described only the second one. `OpenFlightHandler` derives the
sequence itself as `Flights.Length + 1` and accepts no sequence from the
caller at all (`src/Soarscore.Application/Commands/Entries/OpenFlight.cs:50`,
header comment lines 8–13) — so a phone app cannot even *request* flight 3
first. `Entry.OpenFlight`'s contiguity check
(`openFlight.sequenceOutOfOrder`, `Entry.cs:251–256`) then guards the fold,
per that file's own comment. Both halves must change for retrospective entry
to work.

## Why it matters

Harmless for single-flight tasks, which is most of the corpus. It bites in
multi-launch tasks (F3K five-launch tasks, F5K four-launch Task A) whenever
the entering party is a pilot on a phone rather than a scorer working down a
card in fly order. The rejection gives no obvious remedy, and the workaround —
knowing the full set and typing it in launch order — is exactly what the
retrospective workflow cannot rely on.

---

# Plan

## Decisions settled during planning (2026-08-24)

These discharge the three open questions the stub's "Before starting" carried.

1. **`sequence` means "which launch this was": a stable 1-based chronological
   label, chosen by whoever records the flight.** It is *scoring-relevant
   data* — the `flight.sequence` intrinsic feeds lookup terms in F5K Tasks B
   and E (`SeedF5K.cs:161,282`, the `5.5.10.2` launch penalties) — but it is a
   label, not a claim about when it was typed. The `fai-rules` cross-check
   found no rule in either rulebook requiring flights to be *entered* in
   chronological order: F5K Tasks A and D are flown "in any order"
   (`5.5.10.2`), and the order-sensitive rules (F3K Tasks A/B score the
   "last"/"next to last" flight, `F3K.11.1`/`F3K.11.2`) are about launch
   chronology on the field — which the recorder knows retrospectively from
   their scorecard ("that 58 was my third launch"). So the label stays, its
   meaning is unchanged, and only the entry-time contiguity requirement dies.
   This is the same shape as `Measurement`: a human-supplied fact, trusted and
   recorded, auditable in the log (trust model: no auth, no sign-off).
2. **Gaps are allowed; duplicates and non-positive sequences are not.**
   Sparse `Flights` (2 and 4 present, 1 and 3 absent) is representable and
   scores correctly once finding 2's ordering assumption is repaired: flight
   interpretation is per-flight (`FlightInterpreter` sees one flight plus its
   own sequence intrinsic, never siblings — `FlightInterpreter.cs:5–6`),
   and `maxLaunches` counting stays length-based, which is order-independent.
   A gap means "not entered yet", which the provisional leaderboard already
   treats as "score what exists" (NFR-4's world;
   `ScoringService.cs:303–314`). The contiguity defect code
   `openFlight.sequenceOutOfOrder` is deleted, replaced by
   `openFlight.sequenceNotPositive` and `openFlight.duplicateSequence`.
3. **The fold maintains `Flights` sorted ascending by `Sequence` — insertion
   in place, not append.** Sortedness becomes an aggregate invariant instead
   of a convention every consumer must remember. This is forced by finding 2:
   `FlightSelector.SelectLast` takes `flights[^1]` with the comment *"We
   assume flights are ordered by sequence"* (`FlightSelector.cs:111–117`), and
   `SelectLastN`/`SelectExactlyN` are positional too. Under capture-order
   freedom those selections silently pick the wrong flight — e.g. an F3K
   Task-B entry recorded 3rd-best-first would score launches 2-and-3 as
   "next-to-last and last" after 1 arrives late. Fixing it in the fold fixes
   every consumer at once (`ScoringService.InterpretAllFlights`, all four
   positional selection kinds, the read-side metric-gaps view), and replay of
   pre-existing logs is byte-identical: contiguous events always insert at the
   end, which is what append did.
4. **The command gains an optional caller-supplied sequence; omission derives
   `max + 1`, never `length + 1`.** `int? Sequence = null` on the
   `OpenFlight` record; when null the handler derives
   `Flights.Count == 0 ? 1 : Flights.Max(f => f.Sequence) + 1`. Max-plus-one
   is the safe derivation once gaps exist: for flights {1, 3}, `Length + 1`
   would mint the collision 3. Existing callers that omit the field keep
   working unchanged (every current call site posts `new OpenFlight(entryId)`),
   and the decide function remains the real guard — the derivation is a
   convenience for the scorer-working-down-a-card workflow, exactly as the
   current header comment frames it.

## Findings from reading the tree

1. **The double gate.** As set out in the premise correction above:
   `OpenFlightHandler` derives and refuses no input; `Entry.OpenFlight`
   enforces `sequence != Flights.Length + 1` → fail. The event contract
   already carries whatever sequence the decide function emits
   (`FlightOpened(int Sequence, DateTimeOffset At)`, `EntryEvents.cs:54–56`)
   and is already registered (`SoarscoreEventTypes.cs`) — **no event or store
   change anywhere in this story.**
2. **Positional selection is load-bearing corpus-wide and assumes
   sortedness.** `LastFlight` selection is used by F3K Task A, F5K Task B,
   F3B, F3J, F3F, F5J, F5L and all four NZ classes; F3K also carries
   `LastNFlights(2)` (Task B, next-to-last+last), `LastNFlights(3)`, and two
   `ExactlyNInOrder` tasks with per-position targets
   (`SeedF3K.cs:55,168,179,214`). All of these read array positions as launch
   positions. Today the assumption holds only because the write path forces
   contiguity — decision 3 makes it hold structurally instead.
3. **Everything else is already capture-order agnostic.** Amendment resolution
   (`MeasurementDigest.Resolve`) works within one flight; capture/amend look
   up flights *by sequence value* (`Flights.FirstOrDefault(f => f.Sequence ==
   …)`, `Entry.cs:284,365`), never by position; normalisation, aggregation,
   drops and ranking never see flight order. The read models are unaffected:
   `EntrySummary` is coordinate-only by deliberate design
   (`EntrySummary.cs:8–12`), and the metric-gaps view iterates `entry.Flights`
   for display only (`TaskRoundRecording.cs:231–238`) — the sorted fold keeps
   that display sane.
4. **No approval-gated document changes.** The glossary and class diagram
   describe `Flights` as an *ordered list* — decision 3 keeps that true (now
   ordered by sequence, visibly, rather than by arrival). No aggregate or
   value-object shape changes, so the precedent recorded in
   `amend-a-measurement.md` finding 3 applies: events need no approval, and
   none is sought here.
5. **Four test files encode contiguity and must change, not merely extend:**
   - `tests/Soarscore.Domain.Tests/OpenFlightDecideTests.cs:51–58` asserts the
     `sequenceOutOfOrder` code that decision 2 deletes.
   - `tests/Soarscore.Domain.Tests/EntryCapturePropertyTests.cs` invariant 2
     (lines 127–163) generates drifting sequences expecting rejection and
     asserts `[1..n]` — rewritten; invariants 1 and 5 (lines 79–97, 393–420)
     compare flights *positionally*, which breaks once insertion reorders —
     re-keyed by sequence; their generators only ever open `Length + 1`.
   - `tests/Soarscore.Domain.Tests/EntryModelBasedFoldTests.cs:67–75` — the
     reference model appends (`model.Flights.Add(...)`); it must mirror the
     fold's insertion-by-sequence, and its operation should generate
     out-of-order sequences now that they are legal.
   - `tests/Soarscore.Domain.Tests/OpenFlightDecideTests.cs:104–118`
     (`sequence_advances_1_2_3_across_successive_folds`) survives as-is —
     in-order capture is still legal and still yields 1, 2, 3.
6. **Nothing else in production reads `openFlight.sequenceOutOfOrder`**, and no
   acceptance scenario asserts it (grep over `tests/` confirms both counts).
   The Architecture tests' sanity floor counts commands and queries — neither
   changes (no new command; `OpenFlight` grows an optional member).

## Work items

**WI-1 — Rewrite `Entry.OpenFlight`'s validation** (`src/Soarscore.Domain/
Entries/Entry.cs:243–265`). Signature unchanged. Defect codes, in the order
checked:

1. `entry.annulled` — unchanged, first as today.
2. `openFlight.sequenceNotPositive` — `sequence < 1`.
3. `openFlight.duplicateSequence` — `Flights.Any(f => f.Sequence ==
   sequence)`. Message names the duplicated launch.
4. `openFlight.maxLaunchesExceeded` — unchanged, count-based
   (`Flights.Length >= max`), deliberately checked *after* duplicate so a
   re-opened launch reports the more useful error.

Delete the contiguity branch entirely. Update the decide function's doc
comment: sequence is the stable launch label (decision 1), gaps legal
(decision 2).

**WI-2 — Sorted fold.** `Entry.Apply(FlightOpened)` (`Entry.cs:183–192`):
insert the new Flight at the index of the first existing flight whose
Sequence is greater (`ImmutableArray.Insert`), replacing `Flights.Add(...)`.
One line plus a comment stating the invariant: `Flights` is always ascending
by Sequence, which is what makes positional flight selection
(`FlightSelector.SelectLast` etc.) mean launch position under any capture
order.

**WI-3 — Command and handler.** `src/Soarscore.Application/Commands/Entries/
OpenFlight.cs`: record becomes
`record OpenFlight(EntryId EntryRef, int? Sequence = null) : ICommand<EntryId>`.
Handler: when `command.Sequence is { } s` use it; else derive
`entry.Flights.Count == 0 ? 1 : entry.Flights.Max(f => f.Sequence) + 1`
(decision 4). Pass the resolved value into `entry.OpenFlight(...)` unchanged.
Rewrite the header comment — "No Sequence parameter" is no longer true; state
the optional-parameter contract and the max-plus-one derivation.

**WI-4 — Update the contiguity-encoding domain tests** (finding 5):

- `OpenFlightDecideTests`: replace the `sequence_that_is_not_next_fails` test
  with three — opening at 2 on an empty entry *succeeds* (the story's whole
  point); a duplicate sequence fails `openFlight.duplicateSequence`;
  `OpenFlight(0, …)` fails `openFlight.sequenceNotPositive`. Keep the annulled,
  maxLaunches, happy-path and unbounded-run tests as they stand.
- `EntryCapturePropertyTests` invariant 1: keep the append-only property, key
  flight comparisons by `Sequence` (match flights across before/after by
  sequence value) instead of by index, and let the OpenFlight step attempt
  arbitrary sequences (any unused positive value), not `Length + 1`.
- Invariant 2 becomes *"Sequences are unique, positive, and ascending"*:
  generate attempts from a small positive range including repeats; after the
  walk, assert strict ascent, positivity, and that every accepted open is
  still accepted on replay (fold idempotence is already covered by invariant
  5's machinery — do not duplicate it here).
- Invariant 3 (launch limit per corpus task) survives unchanged — it walks
  1..n in order; add one assertion inside it that opening the same limit in a
  scrambled order hits `maxLaunchesExceeded` at exactly the same count.
- Invariant 5: the `DecideOpenFlight` generator picks an unused positive
  sequence (from a bounded range, rejecting collisions the way the decide
  function does); the model inserts by sequence so `DecideStructurallyEqual`'s
  positional walk stays valid.
- `EntryModelBasedFoldTests`: mirror the same two changes — the operation
  generates unused positive sequences; the model maintains a
  sequence-sorted list.

**WI-5 — Property-based invariants** (named here per CLAUDE.md's testing
approach; live in `EntryCapturePropertyTests` unless noted).

- **P1 — Capture-order independence (the story's invariant).** For any finite
  set of flights with distinct sequences, each with any valid measurement
  payload, folding the accepted Open/Capture events in *any* arrival order
  produces an Entry structurally equal to folding them in sequence order —
  same sequence-per-flight, same measurements per flight. This is what makes
  "correct by entering late" honest: a reader can never tell a
  retrospectively completed card from a live-typed one.
- **P2 — Sortedness is an aggregate invariant.** After any interleaving of
  accepted and rejected opens, `entry.Flights` is strictly ascending by
  Sequence with no duplicates. (Guards WI-2 directly.)
- **P3 — The launch limit is a count, not an ordinal.** For any permutation of
  `1..max`, accepting all of them succeeds and the next open — whatever its
  value — fails `openFlight.maxLaunchesExceeded`; generalises the existing
  corpus-wide invariant 3 from the in-order case to all orders.
- **P4 — Selection is capture-order independent.** In
  `FlightSelectorTests`, for each positional kind in the vocabulary that the
  corpus uses (`LastFlight`, `LastNFlights`, `ExactlyNInOrder`), scoring an
  entry whose FlightOpened/CaptureMeasurement events arrived in a shuffled
  order yields the same selected flights and raw score as arrival in sequence
  order. This pins finding 2 — the regression decision 3 exists to kill — at
  the selector level, complementing P1's fold level.

**WI-6 — Acceptance scenarios** (`tests/Soarscore.Acceptance.Tests/Features/
CapturingAScore.feature`, which owns the capture workflow and draws the real
corpus F3K since catalogue-choice-draws-plan WI-7):

- *Flights recorded out of order score identically* — two entries, identical
  captured values; one enters flights 1-then-2, the other 2-then-1; both score
  the same.
- *Only the last launch is scored, however the card was typed* — on a
  LastFlight-shaped task, enter the better flight first as launch 2 and the
  worse flight afterwards as launch 1; the scored result uses launch 2's
  flight. This is the end-to-end form of finding 2: selection follows the
  launch label, not typing recency.
- *A duplicated launch is refused* — posting `/open-flight` twice with the
  same explicit `sequence` returns the stable defect code.

Run against both stores per CLAUDE.md (`SOARSCORE_TEST_STORE=postgres` and
`=sqlite`).

**WI-7 — Board reconciliation.**

- Move this file to `kanban/completed/`, set the status header, citing the
  actual file:line and test counts as built.
- Write the deferred decision: **a mislabeled launch cannot be corrected** —
  `sequence` is immutable once opened and there is no renumbering event;
  today's remedy is annulling the Entry and starting a fresh one (a CD act,
  proportionate to how rarely a scorer mislabels a launch at club scale).
  Goes in `kanban/deferred-decisions.md` under "Score capture and
  corrections", carrying this reasoning; reopen only if CDs hit it in
  practice.
- `kanban/backlog/smaller-items.md` and `kanban/tech-debt.md`: nothing to
  tick — verified, so nobody re-reads them looking for it.

## Out of scope

- **Correcting a mislabeled sequence** — deferred, see WI-7.
- Any change to `FlightOpened`, the store, projections, or the route surface —
  finding 1; the route shape test and the handler-registration floor are
  untouched.
- Target-pairing semantics inside `ApplyTargets` (`FlightSelector.cs:159–213`)
  — its pairing is over ranked flights, orthogonal to capture order; observed
  while planning and left alone.
- Out-of-order *measurement* capture, cross-entry or cross-round ordering —
  none is gated today; the audit that raised this story
  (`task-round-lifecycle.md`, governing principle) found the Entry boundary
  the only remaining imposed ordering, and this story is that finding's
  discharge.
