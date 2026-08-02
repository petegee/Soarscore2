# The Competition Class notation — draft spec

A hand-writing notation for a `CompetitionClass`. Written as language-neutral
pseudo-code — the host language is not yet chosen, and §9 states exactly which
host-language features it assumes.

Seven FAI classes and three NZ national classes are written in it in
`seed-data/`. They are the notation's test and the model's: because the notation
is isomorphic to `soaring-domain-class-diagram.md`, anything they cannot express
is a gap in the model rather than in the notation.

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

Re-checking the notation against the two hardest rulebooks afterwards found six
more (F16–F21, §10): `rankBy`, `flightValidWhen`, the two-gate `drop`, a
per-task `reflight` override, multi-effect penalties with `exclusionGroup`, and
`maxRounds`.

Writing a *seventh* class the notation was never designed against — F3F, RC
slope soaring — found two more (F22–F23, §11) and confirmed the rest. That is
the extensibility claim of NFR-2 being tested rather than asserted, so §11
records what held as carefully as what did not.

Pointing it at a **different rulebook entirely** — three NZ national classes
from NZMAA Section 5, not FAI classes at all — found four more (F24–F27, §12).
Three of the four are structural and one of them, F24, would have mis-scored a
class that adopted and ran cleanly. The FAI corpus could not have found them:
all seven FAI classes agree on two things the NZ classes do not.

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

  param   <name> [<Number|Flag>] [<unit>] <default <value> | no default>
                                        [allowed [<v>, …]] [boundAt <BindingPoint>]
  [finalRanking <SinglePhase|LastPhaseReplaces|SplitByPromotion>]

  reflight
    entitled     <Replacement|BetterOf|NotPermitted|UndefinedRequiresRuling>
    others       <Replacement|BetterOf|NotPermitted|UndefinedRequiresRuling>
    [minNewGroup <int>]

  penalty <"infractionType"> [exclusionGroup <"name">] [perOccurrence]
    <effect> [<points>]
    <effect> [<points>]                                # one or more

  metricSet <name>                                   # class-scope reusable
    <metric …>                                       #   groups: zero or more
  rows <name>                                        #   of each, each named by
    <lookup row …>                                   #   a `use` at its site.
  bands <name>                                       #   §7.1 sugar.
    <band …>

  phase … (one or more, in order)
```

`<BindingPoint>` ∈ `CompetitionSetup | BeforeFlying | PerRound`. Omitted, it is
`CompetitionSetup`; an omitted kind is `Number`. Both are §7 defaults.
`<effect>` ∈ `deduct <pts> | zeroFlight | zeroRound | zeroTask | disqualify`.

**`NotPermitted`** (F26) is a rulebook that definitely grants no re-flight, and
it is not the same statement as `UndefinedRequiresRuling`. NZ Classes N and P
say "no re-flights are permitted" in as many words (`NZ.3.13.1 h`,
`NZ.3.15.1 h`); F5L's `5.5.12.9` states entitlement and stops, leaving the CD to
decide. Writing the first as the second puts a ruling in front of a CD that the
rules have already made; writing the second as the first invents one.

**`minNewGroup` is optional, and absent is not the same as unstated.** One fact
was previously spelled three ways, all of them `0`. The three cases are
distinct and each is now written differently:

- **The rules state a minimum** — the number, cited. F3K `4` (`F3K.9.6`), F3J
  `4` (`F3J.4`), F5K `4` (`5.5.10.13`), F5J `6` (`5.5.11.6`).
- **The rules are silent** — a `no default` parameter, so the CD chooses at
  setup and the choice reaches the event log. F3B (`F3B` states no minimum),
  F5L (`5.5.12.9`) and NZ Class M in both its forms (`NZ.3.12.5 l`) write
  `minNewGroup param(minNewGroup)`. This is F12 applied here rather than a
  fabricated zero.
- **The field is meaningless** — omitted, with a comment saying why. NZ Classes
  N and P grant no re-flight at all (`NZ.3.13.1 h`, `NZ.3.15.1 h`, F26), and
  `F3F.1.5` re-flies one pilot into the running order rather than into a new
  group. The rulebook has answered; the answer makes the field inapplicable.

`0` is never the right value: as a minimum it means "a group of none is
acceptable", which no rulebook says. **Adoption rejects `minNewGroup` written
where both selections are `NotPermitted`** — the rules have already ruled out
the group the number would size.

**`finalRanking` is optional, and a single-phase class omits it.** A class with
exactly one `phase` can only rank on that phase, so `SinglePhase` restates the
phase list rather than adding to it; six of the eleven definitions wrote
`SinglePhase` and now none does, leaving the five multi-phase classes as the
only ones with a line. The keyword stays because the other two values are real
choices a multi-phase class must make and neither is derivable —
`LastPhaseReplaces`
(`F3K.10`, `5.5.10.16`) and `SplitByPromotion` (`F3J.11`, `5.5.11.13`,
`5.5.12.12`) differ on exactly what a fly-off does to a non-qualifier's score.
Both adoption checks are needed, in both directions: **`finalRanking` written
as `SinglePhase` on a class with more than one phase is rejected**, and **a
class with more than one phase and no `finalRanking` is rejected too** — the
default is only available where it is forced.

**Penalties carry one or more effects** (F20), and **the effect is what decides
where in the pipeline it lands.** `deduct` and `disqualify` act on the final
aggregate; `zeroFlight`, `zeroRound` and `zeroTask` act on the raw score, which
is the only stage where a flight or a round still exists as a thing to zero.
There is no `at` clause, and there never was a class that needed one: across
the eleven definitions all 24 `deduct`s and the single `disqualify` were
written at the final aggregate and all 13 `zeroFlight`/`zeroRound`s at the raw
score, and the opposite pairings are not rules a rulebook could state — zeroing
a flight at the final aggregate names a flight the aggregate no longer
distinguishes.

One infraction can still act twice, and it is *because* the two effects differ
that the two stages do: `F3B.2.2 p` zeroes the flight *and* deducts 1000 from
the final score; `F3K.4.1` deducts *and* zeroes the round. A single-effect
penalty is written on one line and is the common case:

```
  penalty "safetyPlaneCrossing" deduct 300   # F3B.2.5 h
```

**`exclusionGroup`** (F20) is how `F3K.4.3` and `F3J`'s "each flight attempt may
only incur a single penalty, the largest applying" is written. Within one flight
attempt at most one penalty from a named group is applied. A group may contain
only `deduct` effects — "largest" has no meaning between a `zeroFlight` and a
deduction — and that is rejected at adoption rather than resolved by an invented
ordering. That check is also what keeps the derivation above out of the group's
way: every member of a group is a deduction, so every member lands at the same
stage, and which one wins was never a function of the stage.

**`perOccurrence`** (F23) sets `PenaltyDefinition.accrual`. The default,
`OncePerAttempt`, is what `F3K.4.3` and `F3J.2.4 c` say word for word — "each
flight attempt may only incur a single penalty" — and it is what all six
original classes assume, so no existing definition writes it. `F3F.1.10` is the
first rule that counts: a safety-plane crossing is "penalised by 100 points
each", and the deduction is the recorded occurrence count times the points.

The two interact, and the order matters. Each definition's **accrued
contribution** is computed first, and the exclusion group then keeps the largest
contribution rather than the largest `points` value. F3F needs exactly that —
two crossings contribute 200, and a person contact's 1000 still supersedes them
("only 1000 points will be deducted"). Where every member of a group is
`OncePerAttempt` the contribution *is* the points, which is why this refines
F3K and F3J without changing a score either produces.

Finding F11 removed five variants no class requires: `LastPhaseOnly`,
`NormalisedRoundScore`, `ZeroScoreTerm` (with `zeroesTermRef`, which could never
be populated — `ScoreTerm` has no id), `wholeFieldAsOneGroup` and `DuringRound`.
Each is readmitted the day a class's rules require it, with the citation.

The same discipline, applied to the eleven definitions once they were all
written, removed four more — three of them elements the notation had a keyword
or operand for, so rule 1 removed the keyword with them:

- **`PenaltyApplication` and the `at` clause** — derivable from the effect, as
  above. Readmission needs a rule that deducts points from something other than
  the final aggregate, or zeroes a flight after the aggregate exists.
- **`PenaltyCatalogue`** — a value object with no attributes between the class
  and its penalty definitions. `CompetitionClass *-- 0..* PenaltyDefinition`
  says the same thing, and the notation never had a keyword for it. Readmission
  needs something true of a class's penalties collectively.
- **`FinalRankingRule`** — a value object holding one enum, now
  `CompetitionClass.finalRanking`. The keyword is unchanged; only the wrapper
  went. Readmission needs a second attribute on final ranking.
- **`PhaseDefinition.mandatory` and the `[mandatory|optional]` token** — see §4.

`wholeFieldAsOneGroup` was re-examined against `F3B.1.8 b` — "a group may consist
of a minimum of eight competitors **or all competitors**" — and **stays removed**.
With any field of eight or more, "as few groups as possible, minimum 8" already
produces the rule's answer; below eight the escape hatch is needed, but it is
needed *universally* (F3K's minimum of 5 is equally unsatisfiable in a four-pilot
event, and nobody calls that contest impossible). It is a draw invariant, not
class data — see `high-level-architecture.md`.

**`param(<name>)`** — a *parameter reference* (`ParameterRef`), adopted per
finding F1. It is **not** allowed anywhere a numeric literal is: it is legal in
exactly thirteen slots, and rejected at adoption anywhere else.

| Slot | Notation |
|---|---|
| `TaskTiming.workingTime` | `timing Fixed param(workingTime.A)` |
| `TaskTiming.maxLaunches` | `timing … maxLaunches param(launches.C)` |
| `ScoreTerm.cap` | `rate flightTime 1 pt/s cap param(maxFlight.B)` |
| `ScoreTerm.origin` | `piecewise launchAltitude from param(nlh)` |
| `Band.from` / `Band.to` (F27) | `0..param(targetTime) @ 1 pt/s` |
| `GroupConstraint.minPerGroup` | `group minPerGroup param(groupSize)` |
| `ValidityRule.minRounds` | `validity minRounds param(minRounds)` |
| `PromotionRule.topN` | `promotion TopN param(flyoffSize)` |
| `PromotionRule.minGroupSize` / `.maxGroupSize` | `group param(groupSize)..param(groupSize)` |
| `PromotionRule.carryPenalties` | `carryPenalties param(carryPenalties)` |
| `ReflightRule.minNewGroupSize` | `minNewGroup param(minNewGroup)` |

Band bounds (F27) carry one extra adoption check the other slots do not need:
piecewise bands must still meet, so where one band's `to` and the next band's
`from` are both parameters they must be the *same* parameter. A gap or an
overlap between bands is a silent mis-score, not a load error.

Adoption-time validation: every ref resolves to a declared `Parameter`, and
every referenced parameter is bound before the pipeline stage that reads it.
Widening the list later is additive; narrowing it once seed data depends on a
slot is not.

Every adoption check stated anywhere in this document is also listed, one line
each, in `high-level-architecture.md` under "Validated at adoption". That list
is the exhaustive inventory and the reasoning stays here; adding a check here
means adding a line there.

**`allowed [<v>, …]`** (F8) sets `Parameter.allowedValues` — the permitted
bindings, where the rules state them. **`no default`** (F12) leaves
`Parameter.defaultValue` unset: it marks a value the rules leave *entirely*
open, so the CD must choose at setup and the choice is recorded in the event
log. Grepping the definitions for `no default` finds every place a rulebook is
silent.

**A mandatory slot the rules leave open takes a `no default` parameter, and the
case against that was examined once and rejected.** Four phases declare one
solely to fill `ValidityRule.minRounds` — F5K's preliminary, NZ Class M, and the
F5J and F3J fly-offs. (F3B's `minRounds` is *not* one of them: `F3B.1.8 b`
states 1 normally and 5 at World and Continental Championships, so its parameter
carries a default and an `allowed` list.) Making `ValidityRule` optional and
letting those four omit it looks obviously right — unlike `Normalisation`,
`minRounds` has an identity value, so "the rules state no minimum" and "no
minimum applies" appear to be the same statement, which is exactly the reasoning
F25 used to make normalisation optional. It was rejected on two grounds:

- **It costs the inventory.** Four rulebook silences would survive only as
  comments, and the grep above stops finding them. That inventory is the reason
  `no default` exists rather than a sensible default.
- **Phase validity has teeth on a fly-off.** Omission means one round is a valid
  phase, and under `LastPhaseReplaces` a single fly-off round would then replace
  the preliminary scores — which is precisely why F3K and F5K *do* state 3.
  Where a rulebook is silent about that, making the CD choose is the right
  outcome, not a silent "no minimum".

Four parameters is the price and it is worth paying. Recorded here because the
argument for the change is the more obvious one and will otherwise be
re-derived.

---

## 4. Phase level

```
  phase <Preliminary|Flyoff>
    [rounds    <FixedSequence|ChooseFromCatalogue> tasksPerRound <n>
                 [distinctTaskPerRound] [maxRounds <n>]]              # §7 default
    validity   minRounds <n> [minTasks <n>]
    [drop      <ByRound|ByTask> <count> [whenRounds >= <n>] [whenResults >= <n>]
                 … (one or more, in order)]                       # optional
    promotion  <TopN <n> | TopPercent <pct>> group <min>..<max>
                 carryPenalties <true|false|param(<name>)>
    task … (one or more — the catalogue available in this phase)
```

**A phase does not say whether it is mandatory**, and the reason is more
interesting than the redundancy that first suggested removing the token. Across
the eleven definitions the flag was perfectly correlated with `PhaseType` — every
`Preliminary` mandatory, every `Flyoff` optional — but the correlation was
achieved by *mis-recording the one real case*. `5.5.10` makes the F5K fly-off
mandatory for seniors at World and Continental Championships, and `F3K.9.1`'s
likewise at championships; both definitions wrote `optional` and put the truth
in a comment, because mandatoriness there is conditional on the **event level**,
which the model has no notion of. A flag that can only ever be written wrong for
the case it exists to record is not recording anything, so it went, and the two
comments stay where they were. Readmission is not a matter of finding a class
with a mandatory fly-off — F3K and F5K already are that class — but of the model
gaining an event level for the condition to read.

**`drop` takes two gates, both optional and conjunctive** (F18) — the drop
applies only when every gate written holds. `F3B.2.8` states both at once:

```
    drop ByTask 1 whenRounds >= 6 whenResults >= 6
                          # F3B.2.8 "if more than five complete rounds are flown,
                          #   the lowest partial score of each task with more
                          #   than five results is omitted"
```

The two counts diverge whenever a group is annulled under `F3B.1.8 c`: a
competitor can have six completed rounds but five Task-C results. F3K needs only
the first gate (`drop ByRound 1 whenRounds >= 6`, `F3K.10.1`).

**A phase carries an ordered list of drops and the first whose gates all hold is
the one that applies** (F22). Every one of the six original classes writes a
single line, which is why the list was not noticed until F3F. `F3F.1.13` tiers
the discard and needs two, most-specific first:

```
    drop ByRound 2 whenRounds >= 15   # F3F.1.13 "if more than fourteen (14) rounds
                                      #   were flown, the two (2) lowest round scores"
    drop ByRound 1 whenRounds >= 4    # F3F.1.13 "a minimum of four (4) rounds …
                                      #   the lowest round score will be discarded"
```

Order is significant and the more selective gate must come first — written the
other way round a fifteen-round contest matches the `>= 4` line and discards
one. That is a real hazard rather than a theoretical one, because both orderings
produce a plausible number, so **adoption rejects a list whose gates are not
strictly descending**: the writer does not get to rely on remembering.

**`drop` is optional, and a phase that omits it discards nothing.** The
reasoning that used to sit here is now reversed. `DropPolicy` was mandatory
(1..\*) on `PhaseDefinition`, so a phase with no discard still needed one, and
`drop none` was sugar for a single `ByRound 0` with neither gate. That is F25's
shape — a mandatory slot satisfied with an invented value — and it was the
majority case, not the exception: nine of the sixteen phases in `seed-data/`
have no discard to state, including every fly-off in the corpus and all four NZ
definitions. Unlike normalisation, discarding nothing does at least *have* an
identity value, so the fabrication cost no arithmetic; what it cost was the
claim itself, a `DropPolicy` asserting that the phase's rules contain a discard
rule when they contain none. `PhaseDefinition *-- 0..* DropPolicy`, and the
sugar went with the multiplicity. The nine phases keep their citations as
comments — that `F3K.10`'s and `5.5.11.13`'s discards apply to the *preliminary*
aggregate is exactly why their fly-offs have none, and that is worth recording;
it is just not a `DropPolicy`.

**`maxRounds`** (F21) is the ceiling on what may be scheduled, and it is on
`rounds` rather than on `validity` deliberately: a phase over its ceiling is not
"invalid" in the sense `minRounds` means. Only F3K's fly-off states one —
`F3K.10.3`, "at least three (3) rounds with a maximum of six (6)".

`promotion` appears only on a phase after the first (`PromotionRule` is 0..1).

A flyoff that changes working times, caps or the task list **restates its
tasks**; it does not inherit them. See `like` (§7) for the notation shortcut.

---

## 5. Task level

```
    task <code> "<name>"
      use        <name>                                             # a class-scope
                                                                    #   `metricSet`, §7.1
      metric     <name> <Number|Flag> [<unit>] [<Truncate|HalfUp|Ceiling> <precision>] [declared]
      flights    <selection>
      timing     <Fixed <duration> | UntilAllFlightsComplete> [prep <duration>] [maxLaunches <n>]
      group      minPerGroup <n> [minValidResults <n>]             # optional
      normalise  <HigherIsBetter|LowerIsBetter> winner <n> [round <mode> <precision>]
                                                                    # optional
      rawScore   round <mode> <precision>                           # optional
      validWhen  <predicate>                                        # optional
      flightValidWhen <predicate>                                   # optional
      reflight                                                      # optional override
        entitled <…> ; others <…> [; minNewGroup <n>]
      score
        <term>
        <term>          # terms are summed
      score normalised                                              # optional
        <term>          # added AFTER normalising; not scaled by it
```

`declared` sets `MetricDefinition.declaredBeforeLaunch` — a value the pilot
nominates before releasing (a Poker target).

**An omitted `maxLaunches` means the task limits launches not at all**, which is
half the corpus — F3B, F3J, F5J, F5L and eight of F3K's tasks. It is the absence
of a constraint rather than an invented one, and it is a §7 default; so is the
omitted `else` on a conditional term, which contributes 0 to the sum. Both are
stated in full there, with the three `else` clauses in F5K that are load-bearing
and must stay written.

**`group` is optional, and absent is not the same as `param(groupSize)` with no
default.** The two were previously written almost identically and they state
different facts:

- **`group` omitted** — *this class does not group-score at all.* NZ Classes N
  and P and Class M's NDC format total each pilot's own raw points and never
  compare one pilot against another (`NZ.3.13.1 i`, `NZ.3.15.1 i`,
  `NZ.3.12.7 c`), so there is no minimum group size to state and no annulment
  threshold to state, because there is no scoring group. All three used to
  write `minPerGroup 1` and their own comments called it the degenerate value.
  `1` is a fabricated rule of exactly F25's kind, and not an inert one:
  `minPerGroup 1` tells the draw that a group of one is an acceptable split,
  which is a statement about how the field may be divided, where the truth is
  that dividing it does not affect anyone's score.
- **`group minPerGroup param(groupSize)` with a `no default` parameter** — *this
  class does group-score, and the rulebook does not state the size.* F5K
  (`5.5.10`), F5L (`5.5.12.4`) and NZ Class M (`NZ.3.12`, "Man-On-Man (Group
  scored)") are this case: the CD chooses at setup and the choice reaches the
  event log (F12). The group is load-bearing; only its size is open.

The question that decides it, for anyone writing a new class: *does one pilot's
score depend on another pilot's score in the same flying group?* If it does,
write `group` — the number where the rules state one, a `no default` parameter
where they do not. If it does not, omit `group` entirely.

Absent `group` says two things downstream and both of them are absences. The
draw takes no size constraint from the task, so the core invariant "a field
smaller than a task's `minPerGroup` flies as one group"
(`high-level-architecture.md`) has nothing to engage with and grouping becomes a
running-order convenience; and no group is ever annulled for want of valid
results, which is already what an unset `minValidResults` meant inside a written
`group`. Neither absence can move a score, because a task with no
`Normalisation` reads nothing from its group in the first place. That is also
the adoption check the optionality needs: **a task that writes `normalise` and
no `group` is rejected** — normalisation is defined against the best score in
the group, so a class that normalises has to say how groups are formed. The
eleven definitions already agree without exception, every normalising task
writing a `group` and every non-normalising one omitting it.

**`normalise` is itself optional** (F25). Written, the task normalises and the
`score` block is what normalisation consumes. Omitted, the task does not
normalise at all: the raw score *is* the task result, and rounds aggregate raw
points. All seven FAI classes write it, which is why it was mandatory until the
NZ classes; `NZ.3.13.1 i` and `NZ.3.15.1 i` — "each flight counts. The final
score is the total of all points over three flights" — do not. There is no
normalisation that leaves scores unchanged, so there was no honest way to write
these classes with a mandatory `Normalisation`; a `winner 1000` put there to
satisfy a multiplicity is a fabricated rule.

**`score normalised`** (F24) is the second, optional term list —
`ScoreTerm.applyAt = Normalised`. Its terms are evaluated per the same
vocabulary but added *after* the `normalise` stage, so normalisation does not
scale them. `NZ.3.12.1 e` states it outright: "landing points will be added to
the **normalized** flight score". Every FAI class wants the other order and so
writes only the plain `score` block — F5J and F5L normalise their landing bonus
along with the flight time deliberately, and nothing about them changes.

Two adoption checks: a `score normalised` block on a task with no `normalise` is
rejected (there is no stage for it to land at), and so is a task that has *only*
a `score normalised` block (nothing for normalisation to consume). See §12 for
the worked example showing that the two orders reorder a group, which is why
this is a stage rather than a formatting choice.

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

**`flightValidWhen`** (F17) sets `Task.flightValidWhen : Predicate`, and is the
*other* gate: it zeroes one flight's contribution while leaving that flight
selected. Absent means every flight counts. It is read at the pipeline's
`interpret flight` stage, which until now had no class data to read.

```
      flightValidWhen all(landedWithinWindow == true,     # F3K.9.3 30 s window
                          launchedInWorkingTime == true)  # F3K.7
```

The two gates are not interchangeable and the difference is load-bearing. A
zeroed flight *stays* the last flight: `F3K.11.1` scores only the last flight,
and a late-landing last flight must score zero rather than promote its
predecessor. That is also why this is not a flag on `Flight` — voiding it there
would hide it from `flights last` and change Task A's answer. It replaces the
`when launchedOnSignal == true … else constant 0` wrapper F3K Task C used to
need, and it applies once per task rather than once per term, which matters on a
multi-term task like F3B's Task A where a forgotten wrapper silently awards a
landing bonus on a voided flight.

**`reflight` on a task** (F19) overrides `CompetitionClass.reflightRule` for that
task only. Absent — the case in five of the six original classes, in every F3K
task, and in all three NZ classes —
means the class default applies. `F3B.1.5 e` scopes its better-of rule to "task A
… or task B" by name and says nothing about Task C, which is writable only as an
override:

```
    task C "Speed"
      reflight
        entitled UndefinedRequiresRuling ; others UndefinedRequiresRuling
                                          # F3B.1.5 e covers Tasks A and B only
```

### Flight selection

| Notation | `SelectionKind` | Meaning |
|---|---|---|
| `flights last` | `Last` | only the last flight |
| `flights lastN <n>` | `LastN` | the final *n* flights |
| `flights bestN <n>` | `BestN` | the *n* highest-scoring flights |
| `flights all` | `All` | every flight |
| `flights exactlyN <n> targets inOrder [<v>…]` | `ExactlyNInOrder` | flights 1..n take targets 1..n |
| `flights bestN <n> rankBy <metric> targets anyOrder [<v>…]` | `BestN` + `TargetAssignment.AnyOrder` | longest flight takes the largest target, and so down |

**`rankBy`** (F16) sets `FlightSelection.rankByMetric` and is meaningful only to
`bestN`. Omitted, the candidate flights are ranked **by score** — which is what
Poker needs, because `F3K.11.5` credits an achieved *target* rather than the
flight time, so the longest flight is often not the best one. Written, they are
ranked by that metric's **raw value**:

```
      flights bestN 4 rankBy flightTime targets anyOrder [60,120,180,240] s
```

F3K Task H is the only place in the corpus that needs it, and it needs it
because ranking by score there is circular: `F3K.11.8` assigns the targets to the
four longest *flights*, and no flight has a score until a target has been
assigned to it. Ranking by `targets != None` would happen to discriminate
correctly across all six classes, but nothing stored would say so and nothing
could validate it — so the notation says it.

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
lookup     <metric> use <name>                                        # §7.1 sugar
piecewise  <metric> [from <origin>]
             <a>..<b>    @ <r> pt/<unit>
             <b>..any    @ <r> pt/<unit>
piecewise  <metric> [from <origin>] use <name>                        # §7.1 sugar
constant   <v>
when <predicate>
  then <term>
  [else <term>]                                                       # §7 default
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
- **`use <name>`** on a `lookup` or a `piecewise` names a class-scope `rows` or
  `bands` list in place of the inline one. It is sugar (§7.1): the rows are
  copied into the term before adoption, so the stored `ScoreTerm` is exactly
  the one the long form writes, and nothing in the model records that the list
  was shared. A term writes the list or the `use`, never both.

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

**There is no `any`.** All twelve multi-condition sites in the six original
classes are conjunctions, and F3F and the three NZ classes added no disjunction
either, so it still has no rule behind it and was not adopted. A class that
needs it must arrive with the citation.

This is not an expression language: no arithmetic, no functions, no user-defined
predicates. It stays statically validatable at adoption.

---

## 6. What the notation deliberately cannot say

- **No arithmetic between metrics.** There is no `flightTime - workingTime`.
  Anything of that shape is either a captured metric or a `piecewise … from`.
  This is NFR-2's line: an open expression language would defeat static
  validation at adoption.
- **Nothing beyond the flight being scored.** A score term sees one flight's
  measurements and the `flight.sequence` intrinsic — not its sibling flights, not
  task-level values like the working time. `flights` is the first thing that sees
  the whole entry, and the only thing that needs to. This is what makes
  `rankBy` (F16) belong on the selection rather than on a term, and what refuses
  `F3K.9.3`'s Task C cascade (an airborne model in the preparation period zeroes
  the *next* attempt) as something the notation should express: the timekeeper
  records the next attempt's flag, and `flightValidWhen` reads it.
- **One rule is knowingly not implemented.** `F3K.7` limits the sum of scored
  flight times per task to `workingTime − scoredFlightCount` seconds, which binds
  on Tasks D, G and I — a perfect Task G is 595 s, not 600 s. Writing it needs a
  `cap … perTask` whose *value* is arithmetic over a parameter and a selection
  count, which the two rules above refuse. The class-agnostic part of it — flight
  times within an entry cannot exceed the entry's working time — is a core
  invariant instead (`high-level-architecture.md`), and the one-second-per-flight
  of slack is an accepted deviation, not an oversight.
- **No discipline vocabulary.** Grep every definition in `seed-data/` for
  `landing`, `height`, `motor`, `lap` and every hit is a *metric name* or a
  *comment* — never a keyword. That is the CLAUDE.md test, mechanised, and it
  still holds across a second rulebook.
- **No tie-breaking.** Deliberately unmodelled; the hole is left open. F3B
  (`F3B.2.8`, an extra full round), F3K/F5K (`F3K.10`, best dropped score then
  a one-task tie-break flyoff) and F3F (`F3F.1.13`, "classification rounds" flown
  until the tie breaks, then the discarded round decides) all need it and none is
  writable. See finding F15 — three of seven classes now.
- **No exceptions to a score term.** `F3B.2.3 b` and `F3B.2.4 f` zero a flight
  that misses the landing area *"except in the case of midair collision"*. A
  predicate over measurements cannot reach it; it is a contest official's ruling,
  and where it lands is not yet decided.

---

## 7. Sugar and defaults

Both are properties of the notation, not of the stored class, and both expand
before adoption — what `AdoptedRules` snapshots is always the complete model
instance, never the abbreviation.

### 7.1 Sugar

Three, all expanding to complete model instances.

| Sugar | Expands to |
|---|---|
| `metricSet <name>` at class scope; `use <name>` in a task | a copy of each `MetricDefinition` into that task |
| `rows <name>` / `bands <name>` at class scope; `lookup <metric> use <name>` / `piecewise <metric> [from …] use <name>` in a term | a copy of each `LookupRow` / `Band` into that `ScoreTerm` |
| `task <code> "<name>" like <other>` + overrides | a complete `Task` copy with the overrides applied |

The middle row is **one sugar written with two keywords**, and rule 1 is why:
`LookupRow` and `Band` are two model elements, so each takes its own declaration
keyword and each `use` site accepts only its own kind. The scoping, the
expansion and the checks are identical for both.

All three are **scoped to one class definition** and none of them survives
expansion. There is deliberately no library across definitions: a `rows` list
declared in `30-f5j.class` is invisible to `80-nz-m-ales200.class`, which writes
the same eleven rows out again, and `85-nz-p-radian.class` still re-transcribes
Class N in full. Each definition stays an independent test of the model, which
is what `seed-data/` is for, and the duplication between definitions stays on
the page as an honest record of what that discipline costs.

The table's previous third row was `drop none`, and the middle row replaces it
rather than joining it. `drop none` expanded to a one-element list
`[DropPolicy{ByRound, 0, 0}]` and existed only to satisfy a mandatory
multiplicity; with `DropPolicy` optional (§4) an omitted `drop` is the model
instance, so the sugar had nothing left to expand to.

`like` earns its place on F3K: fourteen tasks that share metrics, timing shape,
group constraint and normalisation, and differ only in flight selection and cap.
Without it the F3K catalogue is ~350 lines of near-identical blocks. It is worth
recording that this is a *notation* fix for a *model* fact — `PhaseDefinition
*-- 1..* Task` means a flyoff genuinely re-states its catalogue.

**The granularity of an override is the keyword block.** A keyword block the
derived task restates replaces the parent's block **entirely**; a keyword block
it does not restate is **inherited whole**; nothing is ever merged *within* a
block. There is no third case: the smallest thing `like` can override is one
keyword, and the notation has no way to reach inside a block and change part of
it. Everything in §7.2's precedence rules follows from this sentence.

For a one-line keyword — `flights`, `rawScore`, `group`, `use` — the rule is
barely visible. On a **multi-line block it is the whole of the story**:

- **`score`** — restating it replaces the parent's *entire* term list. F5K Task
  D restates no `score` at all and so inherits A's four terms, `cap 599 perTask`
  included; F5K Task B needs one different cap and must therefore restate the
  launch-altitude `piecewise` and the −10 / −100 terms it did not want to
  change. `score` and `score normalised` are two blocks, replaced independently.
- **`flightValidWhen`** (and `validWhen`) — the predicate is one term,
  `all(…)` and all. A derived task adding a third condition writes all three, as
  F3K Task C does.
- **`timing`** — `prep` and `maxLaunches` belong to the `timing` line, so a
  restated `timing` that omits them means *no prep, unlimited launches*, not the
  parent's. That is §7.2 rule 3 meeting this one.
- **`metric`** — a derived task's own `metric` declarations replace the
  parent's own `metric` declarations. They do not touch what `use` brought in:
  `use` is a separate keyword, inherited or restated on its own. F3K Tasks C and
  E each add one metric to a parent that declares none, so both readings agree
  there; the rule above is what settles the case the corpus does not yet have.

**The cost of "replaces entirely" is real, and most of it has now been paid
off.** Because a `score` block cannot be partly overridden, a derived task that
needs to change one term restates all of them. F5J's and F3J's fly-off tasks
restate their whole `score` block because one cap moved; F5K's Tasks B, C and E
restate theirs because a cap or a guard moved. What that used to mean was a
verbatim second copy of a scoring **table** — 24 rows in F3J, 11 rows plus a
band list in F5J, and a three-band launch-altitude list four times over in F5K.
A hand-maintained duplicate of a scoring table is the F22/F24 failure shape: one
drifted row still adopts, still runs, and still produces a plausible number, so
that was a standing liability and not a tidiness complaint. The tables are now
declared once and referenced — `rows` and `bands`, below — and the restatements
no longer carry them.

**What remains is the restatement itself**, and it is a smaller thing. F5K Tasks
B and C still re-transcribe A's launch conditional — now three lines, two of
them a `use` — along with the `−10` pilot-area and `−100` overfly terms, and
F3J's fly-off still re-transcribes its `−30` overfly term, because a `score`
block is still all-or-nothing. Those are one-line terms carrying one number or
one name each, where a divergence is visible on the page rather than buried in
row nineteen of twenty-four — a different order of risk from the same cause,
and the tables behind the names can no longer drift at all. Closing
it needs a term-level override, which re-opens F11's reasoning that a
`ScoreTerm` has no id, and this section does not pre-empt that question.
Stating the granularity buys one thing either way: it tells a load-bearing
restatement from a no-op one, and a restatement that reproduces what `like`
already supplies changes nothing and has been deleted from the corpus.

**`rows` and `bands` — the unit of reuse, and why it is the row list.**
A `rows` declaration names an ordered `LookupRow` list and a `bands`
declaration an ordered `Band` list, both at class scope beside `metricSet`,
both referenced by `use` where the list would otherwise be written out.

**The expansion, in full.** The `use` is replaced by a copy of the declared
list, in order, and nothing else about the term changes:

```
  bands f5kLaunch                                     # 5.5.10.4
    any..0   @ -0.5 pt/m
    0..10    @ -1.0 pt/m
    10..any  @ -3.0 pt/m
  …
          then piecewise launchAltitude from param(nlh) use f5kLaunch
```

expands to exactly what the long form writes, and to nothing else:

```
          then piecewise launchAltitude from param(nlh)
                 any..0   @ -0.5 pt/m                 # 5.5.10.4
                 0..10    @ -1.0 pt/m
                 10..any  @ -3.0 pt/m
```

The stored `ScoreTerm` has the same kind, metric, origin and cap it had before,
and three `Band`s of the same bounds and rates in the same order. No class,
attribute, multiplicity or enum moved to make this possible, and the fragment's
name is not stored anywhere: `AdoptedRules` snapshots the second form, and a
class that declared one list used four times is indistinguishable, once
adopted, from one that wrote the list out four times. That is the point — the
guarantee is that the four *cannot differ*, not that the model records that they
came from one place.

The unit was picked against the corpus rather than picked and then justified:
every list written more than once in `seed-data/` was extracted and compared.

| Duplicated list | Copies | Where | Declared as |
|---|---|---|---|
| 24-row landing table (`F3J.10.5`) | 2 | F3J preliminary and fly-off | `rows f3jLanding` |
| 11-row landing table (`5.5.11.12 h`) | 2 | F5J preliminary and fly-off | `rows f5jLanding` |
| 2-band start-height list (`5.5.11.12 e`) | 2 | F5J preliminary and fly-off | `bands f5jStartHeight` |
| 3-band launch-altitude list (`5.5.10.4`) | 4 | F5K Tasks A, B, C, E | `bands f5kLaunch` |
| 2-band penalty-only list (`5.5.10.4`) | 3 | F5K Tasks A, B, C | `bands f5kLaunchPenaltyOnly` |
| 3-row `flight.sequence` list (`5.5.10.2`) | 2 | F5K Tasks B and E | **nothing** — see below |

Three things fall out of that table, and only the first was expected.

**The row list, not the score term.** A whole-`ScoreTerm` unit was the obvious
alternative and it very nearly fits: F3J's and F5J's landing conditionals are
identical guard and all, F5J's two start-height terms are identical, and F5K
Tasks A, B and C carry a `when flightTime >= 30 / then … / else …` conditional
that is identical between the three, cap and origin included. It fails on the
fourth site. F5K Task E scores the *same bands* under a *different* guard —
`5.5.10.2` adds a target-achieved condition and there is no `else` — so a
term-level unit reaches three of that list's four uses and leaves E writing the
bands out again, while the row-list unit reaches all four. That is a difference
on fit, and it points the same way as the two other grounds.

The row list is the smaller surface, and it names something the model already
treats as a whole: `ScoreTerm *-- 0..* Band` and `*-- 0..* LookupRow` are
ordered collections, so a name for one is a name for a thing that already exists
as a thing. Naming a `ScoreTerm` is not that. It is the term-level override
question wearing a different hat — F11's "`ScoreTerm` has no id" being re-opened
— which is a decision with its own consequences and is not one to take as a side
effect of removing a duplicated table. What the row list leaves behind is
recorded above: F5K Tasks B and C still restate that three-line conditional and
their `−10` and `−100` terms verbatim, and nothing here forecloses fixing that.

**The criterion is one rulebook clause, not one character match.** F5K Tasks B
and E carry three `flight.sequence` rows that are character-identical, and they
are deliberately left as two lists. `5.5.10.2` states the two launch penalties
separately and they are not the same statement: Task B selects only the last
flight, so its rows are the *cumulative* cost at that flight's sequence number
and total −20 over three launches, while Task E selects every flight, so its
rows are the per-launch *increment* and total −30. The rule states both totals.
Naming them one table would assert an agreement the rulebook does not make, and
would make a future amendment to one of them silently amend the other. Where the
fragments *were* taken, the opposite is true: `5.5.10.4` is one clause and one
table that every F5K task scores against, `F3J.10.5` and `5.5.11.12 h` are each
one clause governing both of their class's phases. One clause, one list.

**Single use is not reuse.** The 11-row landing table also appears once in
NZ-M and once in NZ-M-NDC, the 24-row table once in F5L, and the 3-row
`NZ.3.13.1 e` table once each in NZ-N and NZ-P. None of those is a fragment: a
class-scoped name for a list used once in that class is worse than the list,
because the reader now has to look somewhere else to find three rows. The
duplication those five represent is *between* definitions, which the scoping
rule above declines to solve.

**`like` and `use` compose. `like` copies the parent task as-written, `use`
included, and the two orders of expansion agree anyway.** A derived task may
restate a block containing a `use` or inherit one, and both occur in the corpus:
F5J's and F3J's fly-off tasks restate a `score` block and write their own `use`,
while F5K's Task D and all five of its fly-off tasks restate no `score` at all
and inherit A's, its two `bands` references included.

The normative reading is the as-written one — `like` is a copy of the notation,
and the copy is expanded afterwards along with everything else — because it is
the one that keeps §7.2's precedence rules stating a property of the *notation*
rather than of a half-expanded intermediate. Expanding `use` first and copying
the resulting rows gives the identical result, for one reason: a fragment is
scoped to the class definition and `like` never leaves the class, so the parent
and the derived task resolve the same name against the same declaration. There
is no shadowing — a name is declared once per definition — so no site can
distinguish the two orders, and every `like` in the eleven definitions was
checked against both. Nothing rests on picking one; the pick is so that the
question has an answer.

That equivalence is contingent on the scoping and is worth recording as such: a
cross-definition library would make the order observable, because a name could
then resolve to different rows in the two places, and the as-written reading
would become load-bearing rather than merely tidier.

**Two adoption checks, in both directions.** A `use` must name a **declared**
class-scope group, and one of the kind its site requires: a `metricSet` in a
task, a `rows` list on a `lookup`, a `bands` list on a `piecewise`. And a
declared group that no `use` names is **rejected** rather than ignored.

The second is the one worth arguing, because it catches nothing the first does
not when a name is *mistyped* — a typo produces an unresolved `use` and an
orphan declaration together, and the first check fires. What it catches on its
own is the declaration left behind when the last `use` of it is deleted, and
that leftover is a cited scoring table sitting in a class definition that
nothing scores against. Rule 3 makes every constant carry its source ref; an
orphan fragment is the inverse defect — a ref carrying constants the class no
longer uses — and a reader checking the definition against `5.5.10.4` would find
the table present and correct while the tasks scored against something else.
Rejecting costs a definition nothing it should have wanted.

The check counts zero uses against one or more, and deliberately not one against
many. `like` multiplies a single written `use` across a whole catalogue, which
is exactly what F3K's `metricSet` does — written once in Task A and reaching all
fourteen tasks — so a use count of one is evidence of nothing. Whether a
once-used list should have been written inline instead is the judgement made
above, and it is a judgement, not a well-formedness question.

The existing structural checks — that lookup rows ascend with at most one
unbounded row last, and that adjacent piecewise bands meet — run on the expanded
term, which is the only form that exists by adoption time, so sharing a list
neither weakens them nor needs a variant of them.

### 7.2 Defaults

A default is the notation declining to write a value the model already treats
as absent or as the identity. **No default here changes the model** — no
multiplicity, attribute or enum moved to make one possible; each writes exactly
the instance the long form wrote. Five exist.

| Written nowhere | Means | Sites removed |
|---|---|---|
| `else <term>` on a conditional | the omitted branch contributes **0** to the sum | 26 of 29 conditionals |
| `maxLaunches <n>` on `timing` | **unlimited** launches | 16 of 32 `timing` lines |
| `rounds …` on a phase | `FixedSequence tasksPerRound 1`, no `distinctTaskPerRound`, no `maxRounds` | 8 of 16 phases |
| `boundAt <point>` on a `param` | `CompetitionSetup` | 22 of 33 params |
| the kind on a `param` | `Number` | 29 of 33 params |

The three that were *not* adopted are recorded in §7.3, because the half that
did not change is the more useful half.

**How a reader tells "defaulted" from "the author forgot".** The corpus already
answers this twice: rule *silence* is a `no default` parameter (§3, F12), and an
*inapplicable* field is omitted with a `# no drop:` / `# no group:` /
`# no minNewGroup:` comment saying why (§3, §4, §5). Neither convention is
carried over wholesale here, and the reason is that neither situation arises —
each of the five is a case where absence and the written value are the *same
statement*, not two different ones:

- **`else`** — none needed, and none is possible in principle. An absent `else`
  and `else constant 0` produce the identical number, so a forgotten `else`
  cannot mis-score; the only thing forgetting can produce is the intended
  answer. What a writer can still get wrong is meaning a *non-zero* fallback and
  omitting it — and that is guarded by the corpus habit that a non-zero `else`
  is always written out. F5K Tasks A, B and C are the three that have one: their
  launch-altitude term keeps `else piecewise …`, because under 30 s the height
  *penalties* still apply while the bonus does not (`5.5.10.4`). Those three
  `else` clauses are load-bearing and must never be dropped.
- **`maxLaunches`** — none needed. Absence of a launch limit is not a rule the
  class is failing to state; it is the ordinary case, and 16 of the corpus's 32
  `timing` lines are in it. Where a rulebook says *unlimited attempts* in as
  many words the citation already lives on the `flights` line, as F3B
  (`F3B.1.5`) and F5L (`5.5.12.4`) both write it. A `maxLaunches` that is
  genuinely unknown is a `param(<name>)` with `no default`, exactly as F3K Task
  C writes `param(launches.C)`.
- **`rounds`** — none needed as a marker, but **a citation the line carried
  survives as a comment**, in the `# no drop:` style. F3F is the only such case
  in the corpus: `F3F.1.7`'s "the flights are to be performed round by round" is
  a rule about round composition and is worth keeping even though the
  composition it states is the default one. A phase always writes `validity`, so
  a phase block is never empty and a missing `rounds` cannot be misread as a
  truncated block.
- **`boundAt`** — none needed; the asymmetry is the marker. `BeforeFlying` and
  `PerRound` place an obligation on the CD at a named moment during the event
  and are always written. `CompetitionSetup` places none — it is where the CD is
  already sitting with the class definition open — so it is the point a
  parameter binds at when nothing says otherwise.
- **`param` kind** — none needed; `Flag` is always written, and there are only
  two kinds. The four `Flag` parameters in the corpus are all `carryPenalties`.
  Note that this default is **not** extended to `metric`, whose kind stays
  mandatory: metrics are 39 `Flag` to 28 `Number`, so there is no dominant value
  to default to and the reader of a `score` block needs the kind to hand.

**Defaults and `like` compose; the precedence is stated once, here.** Applying
§7.1's granularity rule — a restated block replaces, an unrestated block is
inherited, nothing merges within a block — a derived task (`task X … like Y`)
resolves in this order, most specific first:

1. **A keyword written in the derived task wins**, over both the parent and the
   default.
2. **A keyword block the derived task does not restate is inherited entire from
   the parent** — including values the parent itself took by default.
3. **A keyword absent from the derived task, from the parent, and from any block
   the derived task *did* restate, takes its default.**

Rule 3 is the one with an edge: because a restated block replaces it whole, a
keyword the parent wrote and the restated block omits takes the *default*, not
the parent's value. No `like` in the eleven definitions relies on that — every
`timing` line in the corpus that is restated writes its own launch limit or
means unlimited — so no default made an existing `like` ambiguous. The other
four defaults cannot interact with `like` at all: `rounds` is on a phase and
phases have no `like`, and `param` is class-scoped.

### 7.3 Defaults considered and rejected

Three values are as dominant in the corpus as the five above and are still
written out every time.

- **`winner 1000` on `normalise`** — all 10 `normalise` clauses in the corpus
  write it. Rejected under notation rule 3: it is a rule-derived constant, and
  every rule-derived constant carries its source ref. `F3B.2.6`, `F3F.1.12`'s
  `Ri = 1000 × Tw / Ti`, `5.5.11.12 m` and `NZ.3.12.3 c` each state the 1000
  themselves, and a default would leave a `#` ref pointing at a number that is
  no longer on the line.
- **`1 pt/s` on `rate`** — the dominant rate. Rejected for the same reason, and
  F5L is why it matters: `5.5.12.11.1` is 2 pt/s, so the rate is a number the
  rules genuinely vary and a reader who has stopped seeing it will not notice
  the class that differs.
- **`HigherIsBetter` on `normalise`** — 8 of 10 directions. **Rejected as
  actively dangerous, not merely unhelpful.** F3B Task C and F3F both invert
  (`F3B.2.6`, `F3F.1.12`), and in an inverted task a wrong direction still
  produces a full, plausible ranking. This is the F22 and F24 failure shape a
  third time: the definition adopts, the contest runs, and the number is merely
  wrong. §8's F3K trap is the same hazard one level down, and the notation
  already spends a paragraph on it. Making the safe-looking value the silent one
  would move that hazard from "check these two against the rule text" to
  "nothing on the page to check".

The distinction between the five and these three: the five are places where the
rules say *nothing*, and writing nothing is therefore the honest transcription.
These three are places where the rules state a number or a direction, and the
notation's job is to carry it.

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
```

At 601 s: `600×1 + 1×(−1)` = **599**. The `when` has no `else`, so a flight
outside the landing area contributes 0 to the sum (§7).

**F3K Task E, Poker.** Achieving a target credits the *target*, not the flight
time; missing it credits nothing (`F3K.11.5`, worked example in the rule):

```
      metric  flightTime  Number s Truncate 0.1             # F3K.7
      metric  targetTime  Number s Truncate 1 declared      # F3K.11.5
      flights bestN 3                                       # up to three targets
      score
        when flightTime >= targetTime
          then rate targetTime 1 pt/s
```

Rule's example — announced 45/50/47, flew 46/48,52/49 — gives 45 + 0 + 50 + 47,
best three = **142 s**. `bestN 3` is what enforces "up to three (3) target
times"; `declared` is what makes `targetTime` a pilot nomination rather than a
measurement.

Note what is *not* here: no `poker` keyword, no `target` term type. Poker is a
`Conditional` over a `Rate` reading a `declaredBeforeLaunch` metric — vocabulary
that already existed for other reasons.

> **Trap — F3K Tasks E and H.** Both are `bestN`, they sit a few lines apart in
> the F3K definition, and they rank their candidate flights on **opposite**
> bases. Task E above must be ranked by *score*, so it takes no `rankBy`: a 52 s
> flight against a 50 s target scores 50, while a 49 s flight against a 47 s
> target scores 47, and the shorter flight is the better one. Task H must be
> ranked by *flight time* and needs `rankBy flightTime`, because `F3K.11.8`
> assigns the targets to the four longest flights and no flight has a score until
> a target is assigned to it (§5, F16).
>
> The failure is silent in both directions — each still produces *a* number — so
> whenever the seed data is re-written, check these two against the worked
> examples in the rule text (`F3K.11.5` → 142 s, `F3K.11.8` → 569 s) rather than
> against each other.

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

---

## 10. Findings F16–F21

F1–F15 came from writing the six classes. These six came from re-checking the
notation against the two hardest rulebooks, F3K and F3B, clause by clause against
the verbatim source text. Each names the rule that forced it.

| # | Extension | Forced by |
|---|---|---|
| F16 | `rankBy <metric>` on `bestN` — `FlightSelection.rankByMetric` | `F3K.11.8` assigns targets to the four longest *flights*; ranking by score is circular, and `F3K.11.5` needs the opposite |
| F17 | `flightValidWhen` — `Task.flightValidWhen : Predicate` | `F3K.9.3` (landing window), `F3K.11.3` (launch signal), `F3K.7` (launch before working time) all zero a flight that is still selected |
| F18 | `drop … whenRounds >= <n> whenResults >= <n>` — two nullable gates on `DropPolicy` | `F3B.2.8` states both; they diverge when a group is annulled under `F3B.1.8 c` |
| F19 | `reflight` on a task — `Task *-- 0..1 ReflightRule` | `F3B.1.5 e` scopes its rule to Tasks A and B and leaves Task C unstated |
| F20 | Multi-effect `penalty` + `exclusionGroup` — `PenaltyDefinition *-- 1..* PenaltyEffectSpec` | `F3B.2.2 p` and `F3K.4.1` each do two things; `F3K.4.3` and `F3J` say penalties do not simply sum |
| F21 | `maxRounds` on `rounds` — `RoundComposition.maxRounds` | `F3K.10.3` caps the fly-off at six rounds |

Three things the same pass decided **not** to change, which is the more useful
half of the record:

- **The evaluation boundary held.** `F3K.7`'s per-task sum limit, `F3K.11.8`'s
  ranking and `F3K.9.3`'s cascade all pushed on "what may a score term see", and
  all three were answered without widening it (§6). No arithmetic was admitted.
- **`wholeFieldAsOneGroup` stays removed** (§3). `F3B.1.8 b` looked like it
  required readmitting F11's variant and does not — it is a draw invariant.
- **`GroupConstraint` and the thirteen `ParameterRef` slots are unchanged.**

---

## 11. Findings F22–F23 — the F3F probe

F1–F21 came from the six classes the notation was built against, which makes
them a poor test of NFR-2: a notation shaped by six rulebooks will fit those six.
So the notation was pointed at a **seventh class it had never seen** — F3F, RC
slope soaring (`F3F.1`, F3 Soaring 2025) — chosen because it is the most
structurally distant class in the volume: no launch, no landing bonus, no
duration, a course flown against the clock on a hillside.

The result is the useful part. F3F is one phase, one task, one flight, one
metric, and it is **shape-identical to F3B Task C** — `LowerIsBetter winner
1000`, which is `F3F.1.12`'s `Ri = 1000 × Tw / Ti` written out. It needed no new
score-term type, no new intrinsic, no arithmetic, no `anyOf`, and no discipline
vocabulary. Two things broke, both structural, both additive.

| # | Extension | Forced by |
|---|---|---|
| F22 | Ordered `drop` list — `PhaseDefinition *-- 0..* DropPolicy` | `F3F.1.13` tiers the discard: one round at ≥4, two above 14. A single `DropPolicy` writes only the first tier |
| F23 | `perOccurrence` — `PenaltyDefinition.accrual`, and "largest in group" comparing accrued contributions | `F3F.1.10` deducts 100 points per safety-plane crossing, while a person contact's 1000 still supersedes the lot |

F22 is the one that matters. Without it F3F is not merely inexpressible but
**silently mis-scored**: the class still adopts, still runs, and discards one
round where the rule says two. F3F contests routinely fly more than fourteen
rounds, so this is an ordinary Saturday, not a corner case.

### What held, which is the more useful half

- **`exclusionGroup` semantics are unchanged, and the first proposed fix was
  wrong.** The obvious reading of `F3F.1.10` — sum repeats of one infraction,
  exclude between infractions — was checked against `F3K.4.3` and `F3J.2.4 c`
  and contradicts both: each says a flight attempt may incur *one* penalty, full
  stop, so two object contacts in one F3K attempt cost 100 and not 200.
  Accrual therefore had to become per-definition class data (F23) rather than a
  redefinition of the group. Recorded because the wrong fix is cheap to
  re-derive and expensive to adopt.
- **`validWhen` versus a zero score term paid off a second time.** `F3F.1.6`
  lists nine conditions under which a flight is "official but gets a zero score",
  in an inverted task where a raw zero is the *fastest* time in the group. All
  nine route to `validWhen`, exactly as `F3B.2.5 g` does. The corollary is worth
  stating: **the `zeroFlight` and `zeroRound` penalty effects are unusable in any
  `LowerIsBetter` task**, so F3F's catalogue can only ever hold `deduct` and
  `disqualify`. A candidate adoption check.
- **`wholeFieldAsOneGroup` stays removed, on a second independent class.**
  `F3F.1.17` evaluates the whole field as one group when the weather is stable
  and splits only when it is not. "As few groups as possible, minimum ten"
  already yields one group at any field size, and the split is a weather ruling
  on the Competition. Second test of F11's removal, second confirmation.
- **The evaluation boundary held again.** `F3F.1.7`'s two thirty-second windows
  looked like they needed timing vocabulary and do not: the launch window is a
  flag the starter records, and the course-entry window only moves the instant
  `courseTime` starts, which is a measurement rule.
- **`GroupConstraint`, the `ParameterRef` slots, `TaskTiming` and the whole
  score-term vocabulary are unchanged.**

### Left open

`F3F.1.5` schedules a re-flight "after a fixed number of pilots (e.g. 5),
pre-defined and announced by the organiser". It has no `ReflightRule` field and
no `ParameterRef` slot, so the F3F definition declares it as a `param` the CD
binds — putting the choice in the event log — that nothing reads. It affects
running order, never a score, so it is recorded rather than fixed; a
`ParameterRef` slot for it is additive whenever the draw needs it.

---

## 12. Findings F24–F27 — the NZ probe

F1–F23 all came from FAI rulebooks. That is a narrower test than it looks:
seven classes drafted by one body over decades share drafting habits, and a
notation shaped by them will fit them. So the notation was pointed at a
**different rulebook** — NZMAA *Flying Rules, Section 5: Soaring* (March 2024),
the New Zealand national classes, which is also the rulebook this system's
actual users fly to (`users.md`).

Three classes were written: **Class M — ALES 200** (`NZ.3.12`), **Class N — ALES
123 Open** (`NZ.3.13`) and **Class P — ALES Radian** (`NZ.3.15`). They were
chosen because they are the NZ classes closest in shape to F5J, so a fit was
the expected result. It was not the result.

| # | Extension | Forced by |
|---|---|---|
| F24 | `score normalised` — `ScoreTerm.applyAt : ScoreStage` | `NZ.3.12.1 e`, `NZ.3.12.3 d`: Class M adds landing points to the *normalised* flight score, not to the raw score |
| F25 | `normalise` optional — `Task *-- 0..1 Normalisation` | `NZ.3.13.1 i`, `NZ.3.15.1 i`: Classes N and P do not normalise; rounds aggregate raw points |
| F26 | `NotPermitted` — a fourth `ReflightSelection` | `NZ.3.13.1 h`, `NZ.3.15.1 h`: "no re-flights are permitted" is a definite rule, not a silence |
| F27 | `param()` on `Band.from` / `Band.to` | `NZ.3.12.1 f, g`: Class M's +1/−1 turning point is the target time the CD announces on the day |

### Why F24 is the one that matters

Class M scores +1 pt/s to the target time and −1 pt/s beyond it, normalises
against the best in the group, and *then* adds the landing bonus. F5J and F5L
put their landing bonus in the raw score and normalise the sum. Both are
coherent; they are different rules, and the model could express only one of
them.

Target 600 s, one group of two. Pilot A flies 600 s and lands 9 m out (bonus
10). Pilot B flies 500 s and lands 1 m out (bonus 50).

| | A | B | Winner |
|---|---|---|---|
| Per `NZ.3.12.3 c, d` — normalise, then add | 1000 + 10 = **1010** | 1000×500/600 + 50 = **883** | A |
| Landing folded into the raw score | 1000×610/610 = **1000** | 1000×550/610 = **902** | A |

Same rulebook, different scores, and with the numbers moved a little the two
orders swap the *order* as well. Nothing in a definition written the wrong way
would say so: it adopts, it runs, it produces plausible numbers. That is the F22
failure mode again, and it is the reason a stage was added to the pipeline
rather than the landing bonus being quietly folded in "because F5J does it".

### Why F25 could not be worked around

There is no identity value for normalisation. `HigherIsBetter winner 1000`
rescales every score in the group; `minPerGroup 1` makes each pilot their own
group and awards everyone 1000. A mandatory `Normalisation` on a class that does
not normalise can only be satisfied by writing a rule the rulebook does not
contain, so the multiplicity was wrong rather than the classes.

### What held

- **The entire score-term vocabulary.** No new term kind, no new intrinsic, no
  arithmetic, no `anyOf`, no discipline keyword. The NZ +1/−1 target-time shape
  is F3B Task A's `piecewise`; the 50/25/0 bonus in N and P and the ten-row
  table in `NZ.2.4.5` are both `lookup`; `NZ.2.4.5`'s "rounded to the next full
  metre" is `Ceiling 1`, already in `RoundingMode`.
- **`flightValidWhen` earned its place a third time.** `NZ.2.4.6` cancels a
  flight whose nose does not come to rest within 75 m of the landing spot, and
  the motor-restart and still-airborne forfeits in N and P are conditions on the
  landing term. Same split as F3K and F3F, no new machinery.
- **`UndefinedRequiresRuling` was needed again, and F26 did not swallow it.**
  Class M's `NZ.3.12.5 l` grants a re-flight for an unexpected event and says
  nothing about which score counts — F5L's case exactly. That it sits in the
  same rulebook as two classes needing `NotPermitted` is the clearest evidence
  the two values are genuinely distinct.
- **The launch height limits are not scoring data.** 200 m, 123 m, 20 s, 30 s —
  the headline numbers of all three classes — are enforced by an onboard
  altitude limiter switch (`NZ.2.8`) and never reach the scorer. Nothing to
  model. Worth recording because the opposite was assumed at the outset.
- **The evaluation boundary held a third time.** Nothing in the three classes
  needed a term to see beyond the flight being scored.
- **`TaskTiming` and the penalty machinery are unchanged.** `GroupConstraint`
  and `DropPolicy` kept their shape but not their multiplicity, and the NZ
  classes are why: all three had to write a degenerate `minPerGroup 1` and a
  `drop none` to satisfy mandatory slots their rulebook says nothing to fill.
  That is F25's shape a second and a third time, and both were later resolved
  the same way — the multiplicity was wrong, not the classes (§4, §5). Neither
  value object itself was touched.

### Left open

- **Class P makes group scoring itself a CD choice.** Its preamble: the CD "may
  decide to mass launch groups of pilots … may use group scoring in this
  instance", with such results ineligible for records or NDC. Whether the task
  normalises is therefore a setup-time decision, and `Normalisation` is a value
  object, so no `ParameterRef` slot can reach it — the same residual F12 hit on
  `Rounding`. The seed definition writes the individual (un-normalised) form,
  which is the one that counts for NDC. Recorded, not fixed.
- **Class M has two scoring modes.** `NZ.3.12.7` defines an NDC format: four
  rounds, "the sum of the four rounds **raw** scores", no normalisation. Same
  class, different pipeline. Written as a second definition (`81-nz-m-ndc`),
  which is additive and consistent with the law in `CLAUDE.md` — but it does
  mean "competition class" and "class in the rulebook" are not one-to-one, and
  nothing in the model records that the two are related.
- **`NZ.2.8.3`'s zero is discretionary.** A launch exceeding the designated
  altitude by 10% means the CD "*may* assign a score of zero". Same category as
  `F3B.2.3 b`'s midair exception (§6): a ruling, not a predicate.
- **`faiDesignation` is empty for a national class.** Three definitions now
  leave it blank. It is presumably nullable, but the field name says otherwise
  and nothing states it.
- **A drafting error in `NZ.3.15.1 j`.** It reads "the model must be airborne at
  the end of the round the flight time for the flight & landing to count", where
  the parallel Class N clause `NZ.3.13.1 j` says the opposite — still airborne at
  the end of the round means the time stops there and no landing points. The
  seed definition follows Class N. **This is a question for the NZMAA, not a
  model gap**, and it is flagged in `85-nz-p-radian.class` so it is not silently
  inherited.
- **Tie-break: all three state none** — consistent with F15, now three of ten
  classes needing one and none able to write it.
