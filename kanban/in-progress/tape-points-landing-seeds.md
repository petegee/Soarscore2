# Story - The landing tape as a declared reading scale

**Status:** In-progress (WI-0 active)
**Raised:** 2026-09-07 - the jerilderie-2010 parallel-run refusal exposed a
landing input the system cannot express.
**Replanned:** 2026-09-08 (first) - "unit-aware capture (distance or points)".
**Replanned:** 2026-09-08 (second) - the owner supplied the actual New Zealand
practice and the GliderScore landing tables it is scored with. **Both earlier
plans rested on a false premise** and are superseded, not implementation work.
The filename stays stable for its two live citations
(`kanban/backlog/f5j-christchurch-parallel-run-witness.md` and
`tests/GliderscoreFixtures/parallel-run-mapping.md:94`).

**Superseded and not to be revived:** direct-points capture; `pts` as an input
unit; exact `LookupTerm.Rows.Points` membership as the validator; a per-capture
input unit; `51-f3j-tape-points` / `SeedF3JTapePoints`; any derived tape-variant
class. Each of these modelled F3J's coincidence as if it were the general rule.

## What

New Zealand scores every precision landing that needs a fine measurement off
**one standard double-sided landing tape**. One side is graduated in F3J's
scale, the other in F3B's. The scorer reads the appropriate side, writes that
number on the score sheet, and the number is a **reading on a scale** - not a
distance, and not points. For F3J it happens to equal the awarded points,
because F3J's own table is what that side of the tape is printed with. For
every other class the number is a lookup key that the class's own landing table
translates into points.

Introduce the tape as what it is: an **instrument with a reading scale**,
defined independently of any class, declared by the competition that used it and
named by each measurement it produced. A reading denotes a **distance band**, and
the class's existing, unchanged, rulebook-keyed landing table turns that band
into points. The system composes the two; it never stores a fabricated distance
and never accepts a bare award.

**Measuring a landing in metres remains fully supported and unchanged.** A
competition may declare no instrument at all, and a competition that declares
one may still record distances - for the spots it had no tape for. A measurement
naming no instrument is a distance in the metric's declared unit, exactly as
today. Decision 3 covers both cases and decision 5 is why mixing them is fair.

Three things already in the tree say this is the intended model:

- `tests/GliderscoreFixtures/parallel-run-mapping.md:94` already diagnoses the
  Jerilderie refusal exactly right - "the rulebook distance lookup **pre-composed
  on the tape**" - and then no plan acted on it.
- `:30` already lists "landing lookup/**composition**" as a near-twin criterion.
- `src/Soarscore.Domain/PublishedClassDefinition/ScoringVocabulary.cs:183`,
  `LookupRow(decimal? UpTo, decimal Points)`, is an ascending upper-bound step
  function over a distance - which is *already* a mark-band table read one way
  and an award table read the other. The tape needs no new structural shape.

WI-0 through WI-5 deliver and verify the capability.
`kanban/backlog/f5j-christchurch-parallel-run-witness.md` consumes them and
owns only its seed fix and its own witness pair; it must not decode a landing
scale in harness code. WI-6 retains the Jerilderie witness behind its separate,
unrelated penalty-mapping gate.

## Why it matters

Because the rulebook award function and the instrument that measured the
landing are two different facts, and GliderScore's landing catalogue fuses
them. Its thirteen "Landing Names" entries are the cross product of roughly
four instruments and roughly seven classes: `F3J Enter Points`,
`F5J Enter Landing`, `F3B Enter Points`, `F3J Enter Cms`, `F3J Enter Dist`,
`F5J Enter Distance`, `F5B Enter Distance`, `FAI 15 Metre Tape`,
`ALES Landing`, `ALES Radian Landing`. Every one of those is derivable from a
class's rulebook table plus the scale it was read on, and every one of them, as
authored data, can drift from the rule it restates - the `F22`/`F24` failure
shape `tools/Soarscore.SeedData/SeedF3J.cs:36-41` exists to warn about, where
one wrong row still adopts, still runs and still produces a plausible number.

Modelling the instrument instead of the fused table means the class definitions
stay the rulebook witness they are, the fused tables become derivations a test
can check, and the club's practice is *proved* faithful rather than asserted.

## The arithmetic that settles it (verified 2026-09-08; re-verify in WI-0)

The tape's F3J side is `NZ.2.4.4`
(`docs/rules/nz/source-docs/nzmaa-s5-soaring-2024.md:497-513`), which is
row-for-row identical to `F3J.10.5` (`docs/rules/f3j.md:38-55`). Reading it
backwards gives each mark's distance band: `100 <- [0, 0.2]`, `99 <- (0.2, 0.4]`,
... `91 <- (1.8, 2.0]`, `90 <- (2.0, 3]`, `85 <- (3, 4]`, ... `30 <- (14, 15]`,
and a distinguished off-the-tape reading for `(15, inf)`. The inversion is
well-defined because that table's `Points` are strictly decreasing.

Composing those bands with FAI F5J's own table (`5.5.11.12 h`,
`docs/rules/f5j.md:42-57`, seeded at `tools/Soarscore.SeedData/SeedF5J.cs:55-59`)
reproduces the owner's `F5J Enter Landing` screenshot **row for row**:

| reading | band | F5J award | GS `F5J Enter Landing` |
|---|---|---|---|
| 96-100 | `[0, 1]` | 50 | 50 |
| 91-95 | `(1, 2]` | 45 | 45 |
| 90 | `(2, 3]` | 40 | 40 |
| 85 | `(3, 4]` | 35 | 35 |
| 80 / 75 / 70 / 65 / 60 / 55 | `(4,5]` ... `(9,10]` | 30 / 25 / 20 / 15 / 10 / 5 | same |
| 50 and below, and off-tape | `(10, inf)` | 0 | absent, so 0 |

The same composition against `F3B.2.3 d` (`docs/rules/f3b.md:45-56`) reproduces
`F3B Enter Points`, including its `91-95 -> 95` and `96-100 -> 100` bands. And
against `F3J.10.5` it is the identity - which is the whole explanation of why
the F3J reading looks like points.

**The condition for composition to be well-defined** is that the class table's
boundaries are a subset of the tape's boundaries; then every tape band lies
inside one award band. It holds for every landing lookup in the corpus against
the F3J side, whose boundaries `{0.2, 0.4 ... 2.0, 3, 4 ... 15}` refine:

- `{0.2 ... 15}` - F3J, F5L (`SeedF5L.cs:90-96`): identity
- `{1 ... 15}` - F3B (`SeedF3B.cs:64-68`)
- `{1 ... 10}` - F5J, F5J-NDC, X5J, NZ-M-ALES200, NZ-M-NDC
  (`SeedF5jNdc.cs:74-77`, `SeedX5j.cs:81-84`, `SeedNzMAles200.cs:87-90`,
  `SeedNzMNdc.cs:64-67`)
- `{7, 15}` - NZ-N-ALES123, NZ-P-Radian (`SeedNzNAles123.cs:68`,
  `SeedNzPRadian.cs:65`)

It also correctly **fails** where it should. The F3B side, boundaries
`{1 ... 15}`, cannot score F3J or F5L: a reading of 95 there means `(1, 2]`,
which F3J splits five ways. ALES M's own tape - a different instrument,
`NZ.3.12.1 a`, "a 10 meter tape marked in 1 meter increments" - cannot score
ALES N or P, whose 25-point band boundary at 15 m lies beyond the tape's last
mark. A fused-table catalogue would accept either as authored data without a
murmur. **That refusal is a feature and must be loud.**

## What the rulebooks say about the instrument (verified 2026-09-08)

The instrument is not a local convenience the rules ignore. They range from
mandating a points-marked tape to saying nothing at all, which is precisely why
it belongs in the model as a declared thing rather than as an assumption:

- **F5J mandates one, and mandates that it reads in points.** `5.5.11.12 i`
  (`docs/rules/source-docs/f5-electric-2026.md:992-993`): "A dedicated
  non-elastic tape **marked in bonus (landing) points** is the means, by which
  this distance is measured." The FAI rule itself says the observation is a
  tape reading, not a distance - the metre table in `h` is the award function,
  expressed in the distance the tape stands for. This is the strongest possible
  corroboration of the model, and it raises a **seed finding for WI-0**:
  `SeedF5J.cs:33` declares `landingDistance` in `m` at `Truncate 0.1`, citing
  `5.5.11.12 i` - it models `h`'s key, not `i`'s instrument. No number changes,
  but the corpus is meant to surface exactly this.
- **F5L permits an unmarked one.** `5.5.12.x c`
  (`f5-electric-2026.md:1566-1567`): "measured by a tape or string, which may
  be fixed at the landing point" - so a distance measurement is in-rule.
- **F3J says nothing.** `F3J.10.6` (`f3-soaring-2025.md:1909-1914`) defines only
  nose-to-spot; the instrument is unconstrained, so either form is in-rule.
- **ALES M mandates a metre-marked one.** `NZ.3.12.1 a`: "a 10 meter tape marked
  in 1 meter increments" - a *different instrument*, reading in metres.

**Consequence for the mixed case (decision 3).** A tape-measure landing is
in-rule for F3J and F5L, is what ALES M prescribes, and is a **deviation from
`5.5.11.12 i` for F5J**. Score-equivalent by decision 5, but a deviation. The
trust model - a club tool recording what happened, with the event log as the
audit (CLAUDE.md, Key constraints) - says record it honestly and let it be
visible, never refuse to record a landing the club actually measured; refusing
would breach the Scorer's role in `docs/users.md` and NFR-4. **WI-0 must put
this to the owner** as a question about disclosure, not decide it: whether a
non-mandated instrument is merely recorded, surfaced in reporting as a
disclosed deviation, or warned about at declaration time. Do not add a
class-specific instrument rule to the core to enforce it either way.

Metre readings are therefore genuinely real: ALES prescribes them, two FAI
classes permit them, and the field forces them when the tapes run out. The
existing distance path is not legacy and must survive untouched.

## Owner decisions (2026-09-08, superseding the earlier set)

1. **The independently-defined, referenced artefact is the tape, not the fused
   table.** A versioned catalogue of reading scales, each mapping a reading to a
   distance band. Class definitions keep their rulebook-keyed landing tables
   verbatim and gain nothing. No fused reading-to-points table is ever authored
   by hand.
2. **The composition is derived, and refuses rather than guesses.** Where a tape
   band straddles two awards in the class's table, the pairing is refused,
   naming the reading, its band and the straddled awards. Never select the wider
   award, the narrower, the first match, an average, or a representative
   distance inside the band.
3. **A competition declares the instruments in use, and each measurement names
   the one it came from.** The declaration is a set - possibly empty - recorded
   as an event and corrected by appending a corrected declaration, which
   re-scores retroactively: the `RulesAmendment` shape, "because results are
   derived, that costs nothing but a re-query"
   (`src/Soarscore.Domain/Competitions/Competition.cs:131-145`). Two input
   forms therefore coexist and **may be mixed freely within one competition,
   one round and one group**:
   - a **reading** naming a declared tape, validated against that tape's
     reading set and composed with the class's table;
   - a **distance** naming no instrument, in the metric's declared unit, taking
     the existing path unchanged.

   Mixing is required by the field, not a convenience. The rules allocate one
   landing spot per competitor in a group (`F3J.2.3 b`, `F3J.9.1`,
   `5.5.12.x b`), so a group of six needs six instruments at once; a club with
   four tapes measures the rest with a tape measure. **This corrects the
   2026-09-08 (second) recommendation of a competition-wide convention**, which
   assumed one tape per event and was wrong about the field. It also restores
   the scope instinct of the superseded first plan's decision 5, which was right
   that both forms coexist within a competition even though it was wrong about
   what was being captured.

   There is no free-form unit on a capture: a measurement names a **declared
   instrument or none**, never an arbitrary unit string.
4. **A measurement records the reading and the instrument it was read on.** The
   record is self-describing, so an audit does not depend on the competition's
   current declaration and a corrected declaration is visible as a difference.
   Never reverse a reading into a distance: a reading identifies a band, not a
   point. No reverse-distance API and no distance-band display is required here.
5. **Mixing is fair, and WI-5's central invariant is already the proof.**
   Composition fidelity says the composed award for a reading equals the class
   table's award for any distance in that reading's band. So the same landing
   scores identically whether it was read off a declared tape or measured with a
   tape measure, whenever the pairing composes - which decision 2's
   declaration-time check already guarantees. There is nothing further to prove
   and no fairness caveat to write into a class definition.
6. **Points are not an input form.** There is no `pts` unit, no direct-award
   capture and no `Rows.Points` membership validator. A reading is validated as
   a member of the named tape's **reading set**. F3J capture is numerically
   unchanged in practice, because its reading set and its award set are the same
   numbers - a derived coincidence, recorded as such.
7. **Eligibility and the pipeline are untouched.** Composition rewrites only
   what a `LookupTerm` is evaluated against, inside its existing conditional and
   scoring stage. It cannot bypass landing eligibility, flight validity,
   selection, normalisation, penalties or any other class arithmetic. F3J.10.8
   and F3J.10.9 still remove the landing bonus for touch and for overfly.
8. **One active measurement, and a three-way distinction.** A second capture is
   refused as today; changing the value is an explicit amendment retaining
   reason, author and time. The **off-the-tape reading** (the club writes 0),
   **0 m** (a landing on the spot) and **no measurement at all** are three
   distinct facts: no bonus, the top award, and missing evidence.
9. **Completeness follows either form.** A valid reading or a valid distance
   satisfies the landing input requirement; the flight must not stay pending
   waiting for the other form. Missing capture retains existing absence
   semantics.
10. **No rescoring against a different table, and no physical calibration
    claim.** Ordinary recalculation after a measurement, declaration or
    eligibility correction stays deterministic. Nothing here certifies that a
    physical tape is printed accurately; the model states which instrument the
    club declared each reading came from.

## New domain concept - approved in principle, wording for review

A reading scale is a new concept and the glossary and class diagram both
require explicit approval (house rules 3-4, and the glossary's own no-new-
concepts rule). The owner has **approved it in principle and asked to review
the wording**. WI-0 proposes exact text for
`docs/soaring-domain-glossary.md` and `docs/soaring-domain-class-diagram.md`
and **lands no `/docs` edit until that wording is approved**.

Naming and shape questions for WI-0 to put with the wording, not to settle
alone:

- **The name.** "Landing tape" is the club's word. The concept is narrower than
  the object: one physical tape carries two scales, so the model element is a
  side. Recommendation: name each side its own tape (`nz-f3j-side`,
  `nz-f3b-side`) and treat double-sidedness as a fact about equipment, not
  scoring. The alternative - one tape with two sides - buys nothing scoring
  reads.
- **Is the F3B side graduated in F3B points?** The owner states F3B is read on
  the F3B side. `F3B Enter Points`' identity rows are exactly F3B's award set,
  `{30, 35 ... 100}`, which is consistent; but its extra `91-94` and `96-99`
  rows only make sense as tolerance for an F3J-side read. Re-verify against the
  physical tape or the fixtures before seeding the F3B side. Do not guess.
- **Whether a tape reuses `LookupRow`.** A tape is a distance-keyed step
  function to a reading; a landing table is a distance-keyed step function to
  points. Reusing the type is honest and free; the `Points` member name would
  be wrong. WI-0 decides and records why.

## Implementation boundaries

- **Generic, definition-driven core.** The composition is a pure function of a
  tape and a `LookupTerm`'s rows. No `if F3J`, no `landingDistance` branch, no
  GliderScore scheme numbers, no class name anywhere in `src/`.
- **A tape is declared for a metric, not for "the landing".** `LookupTerm` is
  not landing-specific: F5K uses one over `Intrinsic.FlightSequence`
  (`tools/Soarscore.SeedData/SeedF5K.cs:169-170`). A declaration binds a tape to
  a named metric whose declared unit the tape's bands are in; every other lookup
  in the definition is untouched by construction.
- **Where the refusal fires.** Validate the pairing when the declaration is made
  - that is where it is actionable, and the answer is "that side of the tape
  cannot score this class". Compose at resolution so there is one source of
  truth. Do not let a straddled pairing reach scoring in any form.
- **Precision belongs to the input form.** A reading is not a distance and must
  not inherit `landingDistance`'s declared capture precision
  (`Truncate 0.1 m`); it is validated as an exact member of the tape's reading
  set. Distance capture keeps its declared precision and existing lookup
  boundary behaviour byte for byte. Later score rounding stays in its stage.
- **Band semantics must be stated once and tested.** `LookupRow.UpTo` is an
  inclusive upper bound, so row *i*'s band is `(UpTo[i-1], UpTo[i]]`, the first
  row's is `[0, UpTo[0]]`, and an unbounded last row's is `(UpTo[last], inf)`.
  The tape needs a distinguished reading for that terminal band. Inversion
  requires the readings be distinct; a tape with a repeated reading is refused
  at authoring.
- **No inferred compatibility defaults, and no default tape.** A measurement
  naming no instrument is a distance in the metric's declared unit - the base
  case, already implemented, not a fallback. Never invent a tape for a
  competition that declared none, never make a declared tape implicit for
  captures that did not name it, and never guess an instrument from the number,
  the metric name, the class designation or a fixture slug. Unitless metrics
  and flags are not landings - preserve their capture contract rather than
  inventing instruments for them.
- **The corpus keeps its shape.** Tapes are reference data, catalogued and
  counted separately. No class definition changes, no new class seed, and the
  class corpus count must show no delta from this story.

## Requirements cross-check

- `docs/users.md`, Scorer: record what was observed. The reading off the tape
  *is* the observation, and the system does the scoring - which preserves the
  glossary's Measurement / Score distinction better than the superseded plan
  did, because a reading is never mistaken for an award.
- NFR-1: the class definition remains the only source of the award function and
  eligibility; the tape is the only source of the scale. Neither duplicates the
  other. NFR-2: a new class needs no tape work and a new tape needs no class
  work - additive on both axes.
- NFR-3: headless capture, no UI or device assumptions. NFR-4: a reading can
  arrive out of order like any measurement, and completeness recognises it.
- `F3J.10.5` and `NZ.2.4.4` define the F3J scale; `F3J.10.6` defines
  nose-to-spot measurement; `F3J.10.8`/`.9` govern eligibility; `5.5.11.12 h`,
  `F3B.2.3 d`, `NZ.2.4.5` are the other award tables; `NZ.3.12.1 a` is ALES M's
  metre tape. Composing a declared scale with a rulebook table amends none of
  them and certifies no tape's physical calibration.
- `kanban/deferred-decisions.md` holds no settled landing-input restriction; its
  fly-off draw deferral stands, and its score-capture section
  (`:272-293`) is the precedent that a scoring-relevant fact about a landing is
  a `Measurement`, corrected by amendment. If WI-0 finds a conflict, surface it
  with a recommended resolution before proceeding.

## Code anchors (re-verify before implementation)

- `src/Soarscore.Domain/PublishedClassDefinition/ScoringVocabulary.cs`:
  `LookupRow` (`:183`) and `LookupTerm` (`:216-222`) are the award table;
  `MetricDefinition.Unit` (`:45`) declares the input unit; `MeasuredValue`
  (`:24-35`) distinguishes only number and flag.
- `src/Soarscore.Domain/Competitions/Competition.cs`: `AdoptedRules`
  (`:107-129`) is the "the Competition owns its rulebook" snapshot;
  `RulesAmendment` (`:131-145`) is the retroactive-correction shape;
  `ParameterBinding` (`:147-177`) is the existing "a choice the class left open,
  recorded as an event so re-scoring reproduces it" idiom. The tape declaration
  should look like these, not like configuration.
- `src/Soarscore.Domain/Entries/Entry.cs`: `Measurement` (`:83-93`) and
  `Amendment` (`:63-72`) carry no scale; `CaptureMeasurement` (`:317`) and
  `AmendMeasurement` (`:396`) validate against metric declarations.
- `src/Soarscore.Application/Commands/Entries/CaptureMeasurement.cs` and
  `AmendMeasurement.cs`: load the adopted task and pass its metrics to the
  decide functions; they will need the declaration too.
- `src/Soarscore.Domain/Scoring/MeasurementDigest.cs`,
  `FlightMetricResolution.cs`, `FlightInterpreter.cs`: preserve the reading and
  its scale, resolve completeness, and evaluate the composed lookup. Resolution
  currently pends before evaluating conditions.
- `src/Soarscore.Application/Queries/Scoring/TaskRoundRecording.cs`:
  independently reports missing inputs and must agree with score resolution.
- `tools/Soarscore.SeedData/SeedF3J.cs:36-49`: `LandingRows`, the rulebook
  witness and - read backwards - the F3J side's scale.

## Plan

### WI-0 - Resolve the model and get the wording approved

1. Re-verify this story's arithmetic: the `NZ.2.4.4`/`F3J.10.5` identity, the
   three compositions against the owner's screenshots, and the boundary-subset
   check across every landing lookup in the corpus. Record any row that does not
   compose as expected as a finding, not a rounding.
2. Settle the structural contract in this file: the tape's shape and identity,
   band semantics, the reading set including the off-the-tape reading, how a
   competition declares its set of instruments and how that is amended, how a
   measurement names its instrument or none, what the measurement stores, where
   the refusal fires, and how tapes are catalogued and counted.
3. Propose the glossary and class-diagram wording, with the three naming and
   shape questions above, for owner review. **Land no `/docs` edit before
   approval.** Resolve the F3B-side question with evidence.
4. **Put the two instrument questions to the owner** (from the rulebook section
   above): the disclosure treatment of a non-mandated instrument - `5.5.11.12 i`
   mandates a points-marked tape for F5J, so a tape-measure landing there is a
   recorded deviation - and the `SeedF5J.cs:33` finding that `landingDistance`
   models `h`'s key rather than `i`'s instrument. Neither changes a number;
   neither is the agent's to decide.
5. Plan the tests below and move this story to `in-progress/` before code. Do
   not reconfirm the settled decisions, and do not gate the capability on
   physical tape calibration.

**Done-when:** the model, the refusal contract and the approved doc wording are
explicit; no silent straddle handling and no unresolved class-model change.

### WI-1 - The composition function (pure Domain, tests first)

1. Implement reading-to-band inversion and band-to-award composition as a pure
   `Result`-returning Domain function over a tape and a `LookupTerm`'s rows.
   Refuse a straddled band, a repeated reading, a non-monotone tape, a tape
   whose unit does not match the metric's declared unit, and a reading outside
   the tape's set - each with a distinct, named refusal.
2. Test against the corpus first: identity for F3J and F5L; the owner's
   `F5J Enter Landing` and `F3B Enter Points` tables reproduced row for row;
   refusal for the F3B side against F3J/F5L and for ALES M's metre tape against
   ALES N/P.
3. No dependency outside the BCL; the architecture tests already enforce it.

**Done-when:** composition is exact where it is defined and refuses loudly where
it is not, proven against the shipped corpus without a duplicate table in a test.

### WI-2 - The tape catalogue and its seeds

1. Add the tape catalogue as reference data, authored in `tools/Soarscore.SeedData`
   beside the classes, with the same integrity checks and canonical JSON
   emission. Seed the NZ F3J side (`NZ.2.4.4`) and, subject to WI-0's evidence,
   the NZ F3B side; ALES M's metre tape (`NZ.3.12.1 a` with `NZ.2.4.5`); and any
   further scale a fixture actually needs. Cite the clause, never GliderScore.
2. Each seeded tape's header states which corpus classes it composes with and
   which it refuses, as the tape's own rulebook witness.
3. **No class definition changes and no class corpus count change.** The tape
   catalogue is counted separately and its count is pinned.

**Done-when:** the tool is green, the tapes emit canonically, and
`git diff tools/Soarscore.SeedData/json/` shows additions only - no edit to any
class JSON.

### WI-3 - Declaration, capture, amendment, audit

1. Add the per-competition declaration of the **set** of instruments in use,
   each binding a tape to a named metric, as an event, refusing at declaration
   time any pairing WI-1 cannot compose. Add its correction path, retroactive by
   re-derivation, retaining reason, author and time. An empty set is valid and
   is the default.
2. Extend capture and amendment to name **a declared instrument or none**. A
   reading naming an instrument is validated against that tape's reading set; a
   measurement naming none is a distance in the metric's declared unit and takes
   the existing path byte for byte. Naming an instrument the competition has not
   declared is refused. Both forms are accepted for the same metric in the same
   round and group, in any order.
3. Persist and project the value with its instrument, including correction
   history and the effective latest amendment. Preserve existing concurrency and
   append-only behaviour; an invalid capture, amendment or declaration appends
   no event.
4. Update repository capture callers and tests, with no numeric change to any
   existing distance path. Test: a competition with no declaration (distances
   only, unchanged); a reading off the named tape's scale; an undeclared
   instrument; an undeclared metric; the wrong value kind; duplicate capture; an
   amendment changing the reading; an amendment changing the instrument; a
   corrected declaration; and a declaration refused for a straddled pairing.

**Done-when:** the real capture path records and validates both forms, mixed
within one competition; events round trip with their instrument on both storage
backends; a competition that declares nothing behaves exactly as today.

### WI-4 - Scoring, completeness and reporting

1. Carry the value and its instrument through metric resolution and lookup
   evaluation, composing at resolution for a reading. A measurement naming no
   instrument follows existing precision and lookup semantics unchanged.
2. Keep every enclosing condition, flight-validity gate and scoring stage in
   force, and leave every non-landing `LookupTerm` untouched.
3. Align pending-flight resolution with recording-completeness reporting:
   either form fulfils landing capture, missing stays missing, and an amendment
   or a corrected declaration re-scores without double counting.

**Done-when:** the same landing scores identically whether measured in metres or
read off a declared tape whose composition is defined, including when both forms
appear in one group; completeness and scoring agree; no distance-path result
changes.

### WI-5 - Property and acceptance proof (the shared prerequisite)

1. **Composition fidelity (CsCheck).** *For any tape and any landing table whose
   boundaries the tape refines, and any distance `d` in the tape's range,
   composing gives the same award as the class table applied to `d` directly.*
   The central invariant: the tape changes the observation's scale, never the
   rule. Exercise generated tapes and tables as well as the corpus.
2. **Refinement is exactly the condition (CsCheck).** *Composition succeeds if
   and only if every boundary of the class table is a boundary of the tape.*
   Generated near-misses must refuse, not approximate.
3. **Instrument equivalence within a group (CsCheck).** *For a landing whose
   distance `d` a declared tape reads as `r`, capturing `d` with no instrument
   and capturing `r` naming that tape produce the same landing contribution and
   the same score, with all other observations and eligibility inputs fixed.*
   This is decision 5's fairness claim, and it is the invariant that makes a
   mixed-instrument group legitimate rather than merely tolerated. It follows
   from property 1; assert it at the scoring grain anyway, because that is the
   grain the fairness question is asked at.
4. **Eligibility invariance (CsCheck).** Changing the instrument cannot enable
   a landing bonus or a valid flight that the same conditions disable. This is
   true by construction under WI-4; assert it anyway, sweeping the conditional
   and scoring-stage cases.
5. **Membership, refusal and the three-way distinction.** Every F3J reading;
   readings the named tape has no mark for; an instrument the competition never
   declared; the off-the-tape reading versus `0 m` versus no measurement; exact
   band boundaries and adjacent representable distances; unbounded and
   repeated-reading rows; the refused pairings from WI-1.
6. **BDD workflow.** On unchanged canonical F3J and F5J, two scenarios:
   - **No instrument declared** - capture distances only and score, proving the
     base case is the existing behaviour and nothing about it changed.
   - **Not enough tapes** - declare the NZ F3J-side tape, then score one group
     in which some pilots' landings are readings on it and the rest are
     tape-measure distances. Inspect scores and completeness, reject an
     off-scale reading and an undeclared instrument, amend a reading, amend a
     measurement's instrument, correct the declaration and watch the retroactive
     re-score, and correct an eligibility flag. Verify audit history, that the
     mixed group's awards agree with property 3, and that no landing contributes
     twice.
7. Run Domain, Application, Architecture and Infrastructure suites and the full
   acceptance suite against both SQLite and PostgreSQL. Verify the class corpus
   is unchanged and its count has no delta. Run `graphify update .`.
8. **Curate the mapping table's landing vocabulary.** `parallel-run-mapping.md`'s
   G5 gate still reads "entered-distance vs entered-points vs none" and row
   `:94`'s rationale still proposes "a derived-equivalent tape-points F3J seed".
   Both are premise-stale. Rewrite them for the declared-instrument model as a
   curation edit, keeping the row's status honest until WI-6.

**Done-when:** the capability is verified through declaration, capture, storage,
correction, scoring and reporting, for a competition that declares no instrument
and for one that mixes a tape with a tape measure. F5J may consume this
prerequisite regardless of WI-6's separate gate; coordinate the board before
pulling that story in.

### WI-6 - Jerilderie witness (separate, unrelated mapping gate)

The first plan also added a `competitionPenalty` of 100 to a derived seed. That
is not a landing change and must never reach canonical F3J. The fixture's
offence is unrecorded: do not label it `towlineNotClearedWithin30s` because the
point cost matches, do not drop the row, and do not weaken the harness refusal.

1. **Gate before full replay.** Surface that unidentified penalty and obtain an
   evidence-backed, owner-approved disposition. If none exists, leave the pair
   unrunnable and its mapping status honest. Do not hold the capability hostage:
   park the remainder or, with owner agreement, split the witness into a backlog
   stub before closing this story.
2. When the gate resolves, replay under **`50-f3j`**, declaring the NZ F3J-side
   tape and submitting the scheme-3 readings **verbatim** and naming that tape,
   including the off-the-tape zeros. Every row of this fixture is a tape
   reading; the composition is the identity here, which is the cleanest possible
   first witness of it. Validate through the production API, not a harness
   bypass. Record the declared instrument in provenance.
3. Re-verify the seed's no-default bindings. The first plan proposed
   `carryPenalties = false` and `flyoffMinRounds = rounds flown` as dormant
   bindings, only the preliminary being drawn. Keep an unknown parameter a loud
   refusal and disclose dormant bindings honestly; add no seed default to suit a
   fixture.
4. Author the ledger at
   `tests/GliderscoreFixtures/jerilderie-2010/parallel-run/50-f3j.json` and a
   scenario under seed `50-f3j`. Triage measured duration, normalisation and
   drop differences with citations. Do not require identical placings, and do not
   infer a ranking difference from an aggregate difference alone.
5. Verify on both stores. Only then mark the mapping row `done` and record
   coverage under `50-f3j`, with no new seed or count. Reconcile board debt and
   deferrals, move this story to `completed/` and set its status. Update the F5J
   story's prerequisite citation if it is still open; never edit a completed
   story to match later reality.

**Done-when:** the witness is honestly mapped and verified, or its remaining work
is explicitly parked or split by owner agreement. Landing support alone is not a
completed full-fixture witness.

## Jerilderie evidence retained from the first plan

These are 2026-09-07 planning measurements, not a fresh run; re-verify before
WI-6. Sources are `tests/GliderscoreFixtures/jerilderie-2010/`, its landing
scheme and the replay/parallel-run harness.

- Comp 4, `DurGeneral`, 63 pilots, 14 rounds x 5 groups, group sizes 11-14,
  target 600 s, landing scheme 3; 843 flown rows and 39 all-zero rows.
- Scheme 3, "F3J/F3L/F5L Enter Points", has 23 rows: 30-90 in fives and 91-100
  in ones. Recorded landings are those readings plus 0, and 84 flown rows read
  0. **Read correctly, this is the F3J side of the tape and its identity
  composition** - the readings are the F3J scale, and the 0s are off-the-tape,
  not spot landings.
- 145 flights exceed 600 s, maximum 656 s. GliderScore's duration decays past
  target; F3J's seed caps the time contribution (`F3J.10.1 c`). Expect raw and
  normalised differences from this, and no landing differences at all.
- GliderScore drops at rounds 6 and 12 against F3J's single drop after seven
  qualification rounds (`F3J.3.1 a`). Record only the differences actually seen.
- The one 100-point penalty, R11/G3 pilot 2, is the gate above. The make-up row,
  R13 pilot 29 counting for R12, uses existing destination mechanics; with a
  single candidate, Replacement and BetterOf coincide.
- GliderScore's normalised cells were all integral, so rounding differences were
  expected unwitnessed. The first plan relied on declared assumptions for
  `overflySeconds = 0`, `touchedByCompetitor = false` and
  `restedWithin75m = true`. Re-verify and disclose them rather than claiming the
  eligibility was observed or synthesising exception measurements.
- Only drawn phases are finalised; `ClosingACompetitionSteps` uses canonical
  `30-f5j` and exercises a never-drawn fly-off. No fly-off draw or promotion work
  is introduced here. Canonical F3J's minimum group of 6 fits the fixture groups.

## Cross-story contract

- This story owns the tape concept, the catalogue, the declaration, the
  composition, the capture contract and the property and BDD proof.
  `kanban/backlog/f5j-christchurch-parallel-run-witness.md` waits for WI-0
  through WI-5 and owns only its `30-f5j` 75 m seed fix, its scored-window
  prescription, its provenance and ledger widening, and its measured witness.
- **The F5J fixture must not decode its landing scheme in harness code.** GS
  scheme 11 is the FAI F5J table pre-composed on the F3J side of the tape;
  reproducing it in an adapter would put a rulebook table in test code and
  reintroduce exactly the duplication this story removes. The fixture declares
  the tape and submits the mark; scheme 11 becomes a **verification target** for
  WI-1's composition, not adapter logic.
- No `31-f5j-tape-points` seed, no shared-seed lift, no class variant, and no
  growth in the class corpus count from either story.
- Track each story's actual lane in open-story citations. Later F5J fixtures keep
  their own pairing and triage decisions; this agreement does not mark them
  supported or authorise further seeds.
