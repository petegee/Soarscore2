# The Competition Class notation — draft spec

A hand-writing notation for a `CompetitionClass`. Written as language-neutral
pseudo-code — the host language is not yet chosen, and §9 states exactly which
host-language features it assumes.

The six FAI classes are written in it in `seed-data/`. They are the notation's
test and the model's: because the notation is isomorphic to
`soaring-domain-class-diagram.md`, anything the six cannot express is a gap in
the model rather than in the notation.

---

## 1. Three rules the notation obeys

1. **One keyword per model element, and no keyword that is not one.** Every
   construct below maps onto a named field or association in
   `soaring-domain-class-diagram.md` §2–§3. There is no `landingBonus`, no
   `launchHeight`, no `poker`.
2. **Isomorphic, not a superset.** Anything writable here is storable in the
   model, and every model instance is writable here. Where the notation offers
   *sugar*, the sugar's expansion is a legal model instance — always shown.
3. **Every rule-derived constant carries its source ref** in a trailing `#`
   comment. A constant with no ref is a defect.

Writing the six classes found six places where rule 2 could not be obeyed —
the model was short of something an FAI rule requires. All six were resolved by
extending the model, and the extensions are in the class diagram:
`param(…)`, `validWhen`, `all(…)`, `cap … perTask`, `rawScore round`,
`piecewise … from` and the `flight.sequence` intrinsic.

---

## 2. Shape

Indentation-scoped blocks; `keyword operand operand …` lines. Ordering is
significant wherever the model says "ordered" (phases, bands, lookup rows,
target values) and insignificant everywhere else.

```
class F5J
  name     "RC Electric Powered Thermal Duration Gliders"
  fai      "F5J"
  version  "FAI F5 Electric 2026 ed.2"
```

Comments are `#` to end of line. `#` immediately after a value is by convention
its source ref.

---

## 3. Class level

```
class <ID>
  name          "<string>"                    # CompetitionClass.name
  fai           "<string>"                    # .faiDesignation
  version       "<string>"                    # .version

  param   <name> <Number|Flag> [<unit>] <default <value> | no default>
                                        [allowed [<v>, …]] boundAt <BindingPoint>
  finalRanking  <SinglePhase|LastPhaseReplaces|SplitByPromotion>

  reflight
    entitled     <Replacement|BetterOf|UndefinedRequiresRuling>
    others       <Replacement|BetterOf|UndefinedRequiresRuling>
    minNewGroup  <int>

  penalty <"infractionType"> <effect> [<points>] at <RawScore|FinalAggregate>

  phase … (one or more, in order)
```

`<BindingPoint>` ∈ `CompetitionSetup | BeforeFlying | PerRound`.
`<effect>` ∈ `deduct <pts> | zeroFlight | zeroRound | zeroTask | disqualify`.

Finding F11 removed five variants no class requires: `LastPhaseOnly`,
`NormalisedRoundScore`, `ZeroScoreTerm` (with `zeroesTermRef`, which could never
be populated — `ScoreTerm` has no id), `wholeFieldAsOneGroup` and `DuringRound`.
Each is readmitted the day a class's rules require it, with the citation.

**`param(<name>)`** — a *parameter reference* (`ParameterRef`), adopted per
finding F1. It is **not** allowed anywhere a numeric literal is: it is legal in
exactly eleven slots, and rejected at adoption anywhere else.

| Slot | Notation |
|---|---|
| `TaskTiming.workingTime` | `timing Fixed param(workingTime.A)` |
| `TaskTiming.maxLaunches` | `timing … maxLaunches param(launches.C)` |
| `ScoreTerm.cap` | `rate flightTime 1 pt/s cap param(maxFlight.B)` |
| `ScoreTerm.origin` | `piecewise launchAltitude from param(nlh)` |
| `GroupConstraint.minPerGroup` | `group minPerGroup param(groupSize)` |
| `ValidityRule.minRounds` | `validity minRounds param(minRounds)` |
| `PromotionRule.topN` | `promotion TopN param(flyoffSize)` |
| `PromotionRule.minGroupSize` / `.maxGroupSize` | `group param(groupSize)..param(groupSize)` |
| `PromotionRule.carryPenalties` | `carryPenalties param(carryPenalties)` |
| `ReflightRule.minNewGroupSize` | `minNewGroup param(minNewGroup)` |

Adoption-time validation: every ref resolves to a declared `Parameter`, and
every referenced parameter is bound before the pipeline stage that reads it.
Widening the list later is additive; narrowing it once seed data depends on a
slot is not.

**`allowed [<v>, …]`** (F8) sets `Parameter.allowedValues` — the permitted
bindings, where the rules state them. **`no default`** (F12) leaves
`Parameter.defaultValue` unset: it marks a value the rules leave *entirely*
open, so the CD must choose at setup and the choice is recorded in the event
log. Grepping the six definitions for `no default` finds every place a rulebook
is silent.

---

## 4. Phase level

```
  phase <Preliminary|Flyoff> [mandatory|optional]
    rounds     <FixedSequence|ChooseFromCatalogue> tasksPerRound <n> [distinctTaskPerRound]
    validity   minRounds <n> [minTasks <n>]
    drop       <ByRound|ByTask> <count> whenCompleted >= <n>      # or: drop none
    promotion  <TopN <n> | TopPercent <pct>> group <min>..<max>
                 carryPenalties <true|false|param(<name>)>
    task … (one or more — the catalogue available in this phase)
```

`drop none` is sugar for `ByRound 0 whenCompleted >= 0` — `DropPolicy` is
mandatory (1..1) on `PhaseDefinition`, so a phase with no drop still needs one.

`promotion` appears only on a phase after the first (`PromotionRule` is 0..1).

A flyoff that changes working times, caps or the task list **restates its
tasks**; it does not inherit them. See `like` (§7) for the notation shortcut.

---

## 5. Task level

```
    task <code> "<name>"
      metric     <name> <Number|Flag> [<unit>] [<Truncate|HalfUp|Ceiling> <precision>] [declared]
      flights    <selection>
      timing     <Fixed <duration> | UntilAllFlightsComplete> [prep <duration>] [maxLaunches <n|unlimited>]
      group      minPerGroup <n> [minValidResults <n>]
      normalise  <HigherIsBetter|LowerIsBetter> winner <n> [round <mode> <precision>]
      rawScore   round <mode> <precision>                           # optional
      validWhen  <predicate>                                        # optional
      score
        <term>
        <term>          # terms are summed
```

`declared` sets `MetricDefinition.declaredBeforeLaunch` — a value the pilot
nominates before releasing (a Poker target).

`round` on `normalise` is **optional** (F12): `Normalisation *-- 0..1 Rounding`,
unset meaning no rounding. F3B, F5J and F5L state no normalised precision and
leave it off. `Rounding` is a value object, so a `Parameter` cannot stand in for
it — this is the one place F12's ruling does not reach.

**`rawScore round`** (F4b) sets `Task.rawScore : Rounding` — the pipeline's
`round` stage *before* normalising, distinct from the `round` on `normalise`
after it. Omit it and the raw score is not rounded, which is what five of the
six classes want. Only F5K sets it (`5.5.10.15`).

**`validWhen`** (F2) sets `Task.validWhen : Predicate`. Absent means always
valid. It is what makes `ResultState.NoResult` reachable from class data —
`ScoringService` produces the state but nothing decided it. Without it F3B Task
C cannot be written: a non-completion scores "zero", and zero seconds is the
fastest time in an inverted group.

### Flight selection

| Notation | `SelectionKind` | Meaning |
|---|---|---|
| `flights last` | `Last` | only the last flight |
| `flights lastN <n>` | `LastN` | the final *n* flights |
| `flights bestN <n>` | `BestN` | the *n* highest-scoring flights |
| `flights all` | `All` | every flight |
| `flights exactlyN <n> targets inOrder [<v>…]` | `ExactlyNInOrder` | flights 1..n take targets 1..n |
| `flights bestN <n> targets anyOrder [<v>…]` | `BestN` + `TargetAssignment.AnyOrder` | longest flight takes the largest target, and so down |

**Target semantics.** Where `targets` is present, each selected flight's
contribution is clamped to its assigned target. This is what
`FlightSelection.targetValues` is for; it is why F3K Task H needs no cap on its
score term. Target values are written in the units of the metric being scored
(seconds) — see finding F14, the model does not say whether they are metric
units or points.

### Score terms

```
rate       <metric> <r> pt/<unit> [cap <v> [<unit>] [perTask]]
lookup     <metric>
             <= <v>  -> <pts>
             any     -> <pts>
piecewise  <metric> [from <origin>]
             <a>..<b>    @ <r> pt/<unit>
             <b>..any    @ <r> pt/<unit>
constant   <v>
when <predicate>
  then <term>
  else <term>
```

- **`rate` `cap`** caps the *metric value consumed*, not the points produced:
  `score = rate × min(metric, cap)`. The two coincide at 1 pt/s, which is why
  the rules write "1 point per second up to a maximum of 600 points (ie 10
  minutes maximum)" (`5.5.11.12 c`) as if they were the same thing. They are not
  at F5L's 2 pt/s, so the notation fixes the meaning and writes the cap with its
  unit.
- **`cap … perTask`** (F4a) sets `ScoreTerm.capScope` to `PerTask`: the cap
  applies to that term's contributions **summed across the selected flights**,
  before the other terms are added. The default, `PerFlight`, is today's
  meaning. Only F5K Tasks A and D need it — `5.5.10.2` caps *total flight time
  used for scoring* at 9:59, which is not a cap on the raw score: a maxed round
  plus a launch-height bonus is 599 + 10 = 609.
- **`piecewise` bands are cumulative** — each band's rate applies to the portion
  of the measurement lying inside it. `0..600 @ 1 pt/s` then `600..any @ -1 pt/s`
  scores 599 at 601 s.
- **`piecewise … from <origin>`** (F5) sets `ScoreTerm.origin`, default 0: bands
  are evaluated over `metric − origin`. Required by F5K, whose launch points are
  per metre *relative to the announced Nominal Launch Height* — `5.5.10.4`,
  "always calculated with reference to the announced NLH". A negative rate over
  a negative portion is what makes a low launch a bonus.
- **`lookup` rows are ascending**; the first row whose `upTo` is ≥ the measured
  value wins. `any -> <pts>` is the unbounded final row — `LookupRow.upTo` is
  nullable (F9), legal only as the last row.
- **`constant`** is a signed literal; negative constants are how a flat derived
  deduction is written (F3J's −30 overfly).

### Metric references

`<metric>` is the name of a `MetricDefinition` declared on the task, **or** one
of a closed list of *intrinsic* flight facts (F6). The list has exactly one
entry:

| Intrinsic | Meaning |
|---|---|
| `flight.sequence` | which launch this flight was, 1-based |

It grows only when a rule requires it and cites it — the same discipline the
score-term vocabulary is held to (NFR-2). Required by F5K's launch penalties
(`5.5.10.2`): Task B selects only the last flight, so the cost of the earlier
launches can be read nowhere else.

### Predicates

```
<metric> <op> <metric|literal>          op ∈  <  <=  >  >=  ==
all(<p>, <p>, …)
```

`all(…)` is `Predicate.allOf` (F3) — a conjunction, usable both in `when` and in
`validWhen`. Exactly one of {leaf comparison, `allOf`} is populated.

**There is no `any`.** All twelve multi-condition sites in the six classes are
conjunctions, so disjunction has no rule behind it and was not adopted. A class
that needs it must arrive with the citation.

This is not an expression language: no arithmetic, no functions, no user-defined
predicates. It stays statically validatable at adoption.

---

## 6. What the notation deliberately cannot say

- **No arithmetic between metrics.** There is no `flightTime - workingTime`.
  Anything of that shape is either a captured metric or a `piecewise … from`.
  This is NFR-2's line: an open expression language would defeat static
  validation at adoption.
- **No discipline vocabulary.** Grep the six definitions for `landing`,
  `height`, `motor`, `lap` and every hit is a *metric name* or a *comment* —
  never a keyword. That is the CLAUDE.md test, mechanised.
- **No tie-breaking.** Deliberately unmodelled; the hole is left open. F3B
  (`F3B.2.8`, an extra full round) and F3K/F5K (`F3K.10`, best dropped score then
  a one-task tie-break flyoff) both need it and neither is writable. See
  finding F15.

---

## 7. Sugar

Three, all expanding to complete model instances. Sugar is a property of the
notation, not of the stored class.

| Sugar | Expands to |
|---|---|
| `metricSet <name>` at class scope; `use <name>` in a task | a copy of each `MetricDefinition` into that task |
| `task <code> "<name>" like <other>` + overrides | a complete `Task` copy with the overrides applied |
| `drop none` | `DropPolicy{ByRound, 0, 0}` |

`like` earns its place on F3K: fourteen tasks that share metrics, timing shape,
group constraint and normalisation, and differ only in flight selection and cap.
Without it the F3K catalogue is ~350 lines of near-identical blocks. It is worth
recording that this is a *notation* fix for a *model* fact — `PhaseDefinition
*-- 1..* Task` means a flyoff genuinely re-states its catalogue.

---

## 8. Worked example — the two hardest terms in the corpus

**F3B Task A flight points.** One `piecewise`, not two terms, because the bands
are cumulative and share a metric:

```
      score
        when landedInDefinedArea == true                    # F3B.2.3 b
          then piecewise flightTime
                 0..600    @  1 pt/s                        # F3B.2.3 b
                 600..any  @ -1 pt/s                        # F3B.2.3 c
          else constant 0
```

At 601 s: `600×1 + 1×(−1)` = **599**.

**F3K Task E, Poker.** Achieving a target credits the *target*, not the flight
time; missing it credits nothing (`F3K.11.5`, worked example in the rule):

```
      metric  flightTime  Number s Truncate 0.1             # F3K.7
      metric  targetTime  Number s Truncate 1 declared      # F3K.11.5
      flights bestN 3                                       # up to three targets
      score
        when flightTime >= targetTime
          then rate targetTime 1 pt/s
          else constant 0
```

Rule's example — announced 45/50/47, flew 46/48,52/49 — gives 45 + 0 + 50 + 47,
best three = **142 s**. `bestN 3` is what enforces "up to three (3) target
times"; `declared` is what makes `targetTime` a pilot nomination rather than a
measurement.

Note what is *not* here: no `poker` keyword, no `target` term type. Poker is a
`Conditional` over a `Rate` reading a `declaredBeforeLaunch` metric — vocabulary
that already existed for other reasons.

---

## 9. Host-language assumptions

The notation assumes five features. All five are common; the fifth is where
language families diverge.

1. **Nested block literals with a leading tag** — `task "A" { … }`. C# object and
   collection initialisers, Kotlin/Groovy builder lambdas, TypeScript object
   literals, F# records, Python nested dicts or dataclasses. Universal.
2. **Named arguments or key–value pairs** — the `keyword operand` lines. In a
   language without named arguments (Java before builders, Go) each becomes a
   builder method, which is longer but not different.
3. **Ordered collections with literal syntax** — bands, lookup rows, target
   values, phases. Universal.
4. **Enum values as bare identifiers** — `Truncate`, `HigherIsBetter`,
   `LastPhaseReplaces`. Universal; string-typed in JSON/YAML hosts, which loses
   the compile-time check.
5. **Symbolic references to declared names** — `flightTime` inside a `score`
   block refers to a `MetricDefinition` declared above it; `param(nlh)` refers to
   a `Parameter`. **This is the one that varies.** Three options:
   - *strings resolved at load* — works everywhere, no compile-time check;
   - *generated constants* — a code-generation step over the metric list;
   - *scoped builder receivers* (Kotlin `@DslMarker`, C# with a typed builder
     context) — the metric names become properties of the task-builder scope and
     the compiler checks them. Only in languages with lambda receivers or
     extension scoping.

   Whichever is chosen, **references must be validated at adoption** (a
   Competition cannot be created holding an invalid rulebook —
   `aggregate-roots.md` §3), so the check exists either way; the question is only
   whether the compiler does it too.

**Nothing above needs operator overloading, macros, or a parser.** The
`0..600 @ 1 pt/s` and `<= 0.2 -> 100` forms are the only places that read like
custom operators; in a host without them they are ordinary calls —
`band(0, 600, 1)`, `row(0.2, 100)` — with no loss of meaning, only of density.

**No recommendation on the host language is made here.** No decision exists in
`docs/`, and the notation does not need one.
