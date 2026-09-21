# Story — Presence-gated flight validity (`Recorded` predicate)

**Status:** Backlog
**Raised:** 2026-09-21 — owner requirement, voiced through the NdcScore
organiser experience (NDC F5J class): entering the day's scores must be ONE
input per fact. Today the organiser types a start height and then must ALSO
assert "the height was recorded" (key tick column, or a compliance entry in
the round drop-down). That second input is silly, unnecessary and confusing —
and it is forced by the vocabulary, not by the UI.

## What

Amends the absence semantics of `metric-absence-semantics.md` (WI-1/WI-4) with
one new fact: a predicate that reads a metric's **recordedness** — whether the
flight carries a measurement — so a class can say "an unrecorded X fails the
flight" without declaring an assumed value and without a companion flag.

FAI 5.5.11.7 e, carried by NZ.0.3 c into the NDC F5J class
(`SeedF5jNdc`), cancels a flight whose AMRT records no Start Height data.
The rule's subject is the RECORDEDNESS of the observation, not a value:

- A gate-referenced metric without `whenNotRecorded` pends on absence
  (FlightMetricResolution tier 2) before any predicate evaluates — absence is
  a capture gap, never an evaluable fact.
- The only zero-on-absence encoding today is an assumed flag, and an assumed
  metric is by definition a non-key, drop-down-captured exception whose
  "recorded" side needs explicit capture — a second input contradicting the
  captured height. (An attempted encoding — `startHeightNotRecorded` with
  `whenNotRecorded: true` — was drafted and rejected 2026-09-21: it surfaced
  a double-negative drop-down entry and silently zeroed flights whose height
  WAS typed unless a second, contradictory input was ticked.)

The predicate vocabulary (`Comparison`, `AllOf` only) cannot express
recordedness; `Comparison` throws on a missing left metric
(`PredicateEvaluator.cs`) — absence is a throw, not a `false`.

## Proposal

1. **New `Predicate` subtype `Recorded(string MetricRef)`** (`$kind:
   "recorded"`). Evaluates TRUE iff the flight's **digest-resolved
   measurements** contain the metric — resolved BEFORE `whenNotRecorded`
   insertion, so an inserted assumption is never a recording (explicit
   capture wins; assumptions are absence semantics, not observations).
2. **Tier rules** (amends the WI-1 table):
    - Tier 3 unchanged: `Recorded` refs count as referenced — an undeclared
      ref stays an adoption refusal
      (`ClassDefinitionValidation.CheckMetricReferencesResolve`).
    - Tier 2 carve-out: metrics referenced by a `Recorded` predicate do NOT
      pend on absence — absence is the predicate's `false`, not a capture gap.
      Safety rests on the existing gate short-circuit: FlightInterpreter
      evaluates `flightValidWhen` BEFORE the score terms and returns a zeroed
      flight on failure (FlightInterpreter.cs:72-88), so a score term sharing
      the metric (F5J's Piecewise on `startHeight`) never evaluates without
      its input. A passed gate implies every `Recorded` child true implies
      every such metric present. `flightTime` (never `Recorded`-referenced)
      still pends normally.
    - **Scoping (v1):** `Recorded` is legal ONLY inside `flightValidWhen` —
      adoption refuses it in `ValidWhen` and `ConditionalTerm.When`, where
      per-term evaluation could reach an absent metric and throw. Widen with
      the first rule that cites it elsewhere (house pattern: disjunction was
      readmitted the same way).
3. **FlightSelector consistency:** `IsZeroedByFlightGate` re-evaluates the
   gate on interpreted flights (FlightSelector.cs:300-311) — an absent metric
   is absent from the interpreted dictionary, so `Recorded` → false → the
   zeroed-stays-selected path holds: a cancelled flight is FLOWN, consumes
   the slot, scores zero (5.5.11.7 e semantics, unchanged).
4. **Seed (`SeedF5jNdc`):** delete the flag metric entirely;
   `startHeight` stays a demanded Number (no assumption — it can no longer
   pend, its only absence path is the gate):

       FlightValidWhen = Predicate.All(
           Predicate.LessThanOrEqual("overflySeconds", 60),
           Predicate.Recorded("startHeight"),        // 5.5.11.7 e, NZ.0.3 c
           Predicate.Is("landedWithin75m", true))    // NZ.0.3 h

   Cite with the `fai-rules` skill discipline; the arithmetic check
   (4 × 650 = 2600) is untouched. New contentHash → new class version;
   adopted competitions keep their pinned definition. Re-bless any corpus
   fixtures embedding this definition (drift guard: backlog
   `ci-seed-corpus-drift-guard.md`).
5. **NdcScore: zero changes.** Columns, drop-down and capture are
   definition-driven and generic; with no flag metric the drop-down entry
   vanishes, `startHeight` keeps its per-flight column, blank ⇒ zero,
   typed ⇒ valid. One input. Client work is schema regeneration only
   (`Predicate` gains a `$kind`).

## Why it matters

Removes a mandatory second input per flight from every AMRT-style class and
closes a correctness trap: today's second-input shapes either block the round
(pend) or silently zero measured flights (assumed flag) when the organiser
forgets the tick. The class definition — not the UI, not the core — says what
absence MEANS, which is the same lever WI-1 installed for assumed values;
this story extends it from "absence resolves to a value" to "absence resolves
to invalid".

## Tests (WI-5 discipline)

- **Recorded semantics:** present ⇒ true; absent ⇒ false; a `whenNotRecorded`
  assumption is NOT a recording (pre-insertion evaluation) — unit +
  property.
- **Carve-out:** absent `Recorded`-referenced metric + failing gate ⇒ flight
  zeroed (State Valid, score 0), NOT pending, round completes; absent
  non-`Recorded` metric (flightTime) still pends with its awaited diagnostic;
  gate pass ⇒ terms score normally.
- **Zeroed-stays-selected:** gate-zeroed flight is the entry's selected
  flight for the round at zero (FlightSelector).
- **Adoption:** `Recorded` over an undeclared metric refused; `Recorded`
  outside `flightValidWhen` refused (v1 scoping).
- **Transparency analogue:** for any capture subset, an entry's score equals
  the full score with absent-`Recorded` flights contributing ZERO (not
  removed) — the WI-5 partial-capture invariant, amended for the new tier
  row.
- **Architecture guard:** the core never branches on metric names —
  `Recorded` is generic; no per-class knowledge.

## Decisions needed (owner)

- Name: `Recorded` vs `IsRecorded` vs `Present`.
- Confirm the v1 scoping (flightValidWhen-only).
- Confirm the NDC F5J seed adopts on the new version once landed.

## Glossary

New domain concept: **presence-gated validity** (recordedness as a predicate
fact). Needs a glossary entry and class-diagram update — approval per the
glossary's own rule.