# Two model flags raised by the serialisation spike

Both surfaced from transcribing F3K and F5K into records and reading the emitted
JSON. Neither is a serialisation problem; both are places where
`soaring-domain-class-diagram.md` and `competition-class-notation.md` disagree
with each other or with themselves.

**Both were approved and applied on 2026-08-03**, each per its recommendation
below — flag 1 by option 2, flag 2 by adding `Parameter.unit`. The edits landed
in `soaring-domain-class-diagram.md`, `competition-class-notation.md`,
`high-level-architecture.md`, `aggregate-roots.md` and one comment in
`seed-data/80-nz-m-ales200.class`; the "the edit, if approved" sections below
list only the class-diagram half and are kept as the reasoning, not as the
change record. Two consequences the write-ups did not anticipate: adoption check
14 ("a task whose terms are *all* `Normalised`") retired, because the raw list's
`1..*` now states it, and the `ScoreStage` enumeration went with `applyAt`,
since nothing is typed by it once the stage is a property of the list.

The flags are left below as written. House-keeping rule 2 asks for the conflict
plus a recommended resolution rather than a silent reconciliation, and the
evidence is why the resolutions were chosen.

---

## Flag 1 — `ScoreTerm.applyAt` is meaningless on a nested term

### The evidence

`ScoreTerm.applyAt : ScoreStage` sits on the abstract base, so every subtype
carries it — including a `ConditionalTerm`'s `then` and `else` children, which
are themselves `ScoreTerm`s (`ConditionalTerm "1" *-- "1..2" ScoreTerm`).

A nested branch cannot land at a different stage from the conditional that
contains it. The conditional is one contribution to one sum; its branches are
how that contribution is computed, not separate contributions. So on a nested
term the field is **unreadable in principle** — no pipeline stage can act on it
— while remaining freely settable.

In the emitted corpus:

| | terms | of which nested `then`/`else` |
|---|---|---|
| F3K | 29 | 2 |
| F5K | 84 | **40** |

Nearly half of F5K's `applyAt` sites are on branches. Every one is
`"RawScore"` today, because the transcription set them consistently; nothing in
the model requires that, and nothing would notice if one were `Normalised`.

### Why it is worth raising rather than ignoring

It is inert *now*. It stops being inert the moment a second class does what NZ
Class M does. F24 exists because the two stages produce different scores and, in
a close group, a different order — and the finding's own argument is that the
failure "adopts, runs, and produces plausible numbers". A `Normalised` on a
nested branch is that failure shape with an extra step: the stage is recorded in
two places, only one of them is read, and the unread one is the one a reader
checking a definition against `NZ.3.12.1 e` would most naturally look at.

It also weakens the diagram's own justification for splitting `ScoreTerm` into
five subtypes. That note says the base holds "nothing but `applyAt`" because
everything else populated only some instances — the split was made precisely to
stop the model admitting states the notation cannot write. `applyAt` on a nested
branch is exactly such a state, and it survived the split.

### Options

1. **Leave it.** Costs nothing today; keeps a second, unread record of the stage.
2. **Move `applyAt` off `ScoreTerm` and onto the Task's two term lists.** The
   notation already writes it that way — `score` and `score normalised` are two
   *blocks*, and §7.1 is explicit that they "are two blocks, replaced
   independently". The stage is a property of the list, and the model is the only
   place it became a property of the element.
3. **Keep it on `ScoreTerm` and add an adoption check** that a nested branch's
   `applyAt` equals its parent's.

### Recommendation — option 2

It is the one that makes the bad state unrepresentable rather than detected, and
it is what the notation already says. It also removes an adoption check rather
than adding one: "a `Normalised` term on a task with no `Normalisation` is
rejected" becomes a statement about a list that is either present or absent,
which is the same shape as `Task *-- 0..1 Normalisation` itself.

The cost is one multiplicity change and it is not additive — `ScoreTerm` loses
its only common attribute, which makes the abstract base attribute-free. That is
not an objection (`ConditionalTerm` is already attribute-free and the diagram
says so approvingly), but it does mean `ScoreStage` moves from §3's term
vocabulary to §2's task structure.

### The edit, if approved

In `docs/soaring-domain-class-diagram.md` §3:

- Delete `+ScoreStage applyAt` from `ScoreTerm`, and the `applyAt` paragraph from
  its note, keeping the F24 reasoning by moving it to the association below.
- Replace `Task "1" *-- "1..*" ScoreTerm : staged by applyAt` with two
  associations:
  - `Task "1" *-- "1..*" ScoreTerm : raw score terms`
  - `Task "1" *-- "0..* " ScoreTerm : normalised score terms (F24)`
- Keep `ScoreStage` as the name of the pipeline stage in §4, where it is read.

`competition-class-notation.md` needs no change: §5 already writes two blocks,
and §7.1 already treats them as two.

---

## Flag 2 — the notation writes a unit on `param`; the model has nowhere to put it

### The evidence

`competition-class-notation.md` §3:

```
param   <name> [<Number|Flag>] [<unit>] <default <value> | no default>
```

and every numeric parameter in `seed-data/` uses it:

```
param workingTime.A  s     default 600 allowed [420, 600] boundAt PerRound  # F3K.11.1
param nlh            m     default 60 boundAt BeforeFlying                  # 5.5.10.3
```

`Parameter` in the class diagram §2 has `name`, `kind`, `defaultValue`,
`allowedValues`, `boundAt`. There is no `unit`. `MetricDefinition` §3 *does*
have one.

This breaks both of the notation's first two rules at once: rule 1 ("one keyword
per model element, **and no keyword that is not one**") and rule 2 ("anything
writable here is storable in the model"). The `s` and the `m` are writable and
not storable — the transcriptions in this spike simply drop them, and nothing
notices.

### Which way the fix goes is a real question

**It is not obviously "add the field".** The argument each way:

- **Add `Parameter.unit`.** A parameter's binding is a `MeasuredValue`, and
  `MeasuredValue` carries a bare `decimal` with no unit either. A CD binding
  `workingTime.A` at setup is choosing 600 *seconds*; an integrator rendering
  the binding form has nothing to label the field with. The unit is also the
  only thing distinguishing F5K's `nlh` (metres) from a duration, and
  `allowedValues` are stated in it.
- **Remove the unit from the notation.** Every `ParameterRef` slot is typed by
  the model — `TaskTiming.workingTime` is seconds because working times are
  seconds, `PiecewiseTerm.origin` is in the metric's unit because bands are.
  The unit is therefore derivable at every one of the thirteen slots, and a
  second copy on the `Parameter` can *disagree* with the slot that consumes it.
  A parameter written `m` and referenced from `TaskTiming.workingTime` is a
  defect nothing currently detects.

The second argument is stronger than it first looks, but it has a hole: a
`Parameter` that no `ParameterRef` names is legal and deliberate (§3, the
`F3F.1.5` case). Such a parameter has no consuming slot, so its unit is
derivable from nothing — and `70-f3f.class` declares exactly one.

### Recommendation — add `Parameter.unit`, nullable

It matches `MetricDefinition.unit`, which is the same fact about the same kind
of declared quantity; it is the only option that covers the unreferenced
parameter; and it is additive, so it costs nothing under NFR-2. The disagreement
risk is then a one-line adoption check rather than an unmodelled field.

Recommend pairing it with a new check, in the same style as the existing
`ParameterRef` resolution rules:

> **A `ParameterRef`'s declared unit must match the unit of the slot that
> consumes it**, where the slot has one.

That check has no home today because the field it would compare does not exist,
which is the honest reason to add the field rather than the label-rendering one.

### The edit, if approved

In `docs/soaring-domain-class-diagram.md` §2, `Parameter`:

```
+string unit
```

with a note in the style of `MetricDefinition`'s:

> `unit` is nullable — a `Flag` parameter has no unit, and all four in the corpus
> are `carryPenalties`. Where a `ParameterRef` consumes a parameter in a slot
> that has its own unit the two must agree; checked at adoption.

In `docs/competition-class-notation.md`: no change to §3's grammar, which
already writes it. Add the new check to the adoption inventory in
`high-level-architecture.md`, per §3's standing instruction that "adding a check
here means adding a line there".

### Second-order note

If `Parameter.unit` is added, `MeasuredValue` is worth a look at the same time —
a `ParameterBinding` records a `MeasuredValue`, which is a bare number, so the
unit lives on the declaration and never on the recorded choice. That is probably
right (the declaration is the schema, the binding is the datum) but it is not
stated anywhere, and it is the same question this flag is about one level down.
Raised, not proposed.
