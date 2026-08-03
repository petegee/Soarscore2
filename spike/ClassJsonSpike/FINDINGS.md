# Spike — `System.Text.Json` and the class-definition hierarchy

Answers the open question in LADR-0003:

> **`System.Text.Json` polymorphic deserialisation of the deep `ScoreTerm`/`Predicate`
> hierarchy** — spike F3K or F5J before committing; custom converter vs explicit
> `$type` throughout the seed JSON is unresolved.

**F3K and F5K** were transcribed rather than F5J. F3K is the deepest `like` chain
in the corpus (27 expanded tasks, five of the six selection kinds, both target
orders, the only `UntilAllFlightsComplete`); F5K has the deepest *term* nesting
(84 score terms, `piecewise … from param()`, `cap … perTask`, the corpus's only
load-bearing `else` clauses, and both `flight.sequence` lookups). F5J is a
subset of what those two exercise.

Run it with `dotnet run`. 28 checks, all passing. Emitted JSON is in `out/`.

---

## Headline

**The hierarchies need no custom converter.** `[JsonPolymorphic]` +
`[JsonDerivedType]` with hand-written discriminators carries `ScoreTerm`,
`Predicate` and `FlightSelection` — including `ConditionalTerm` nesting a
`Predicate` that nests an `AllOf` that nests `Comparison`s, and a `then` branch
that is itself a `PiecewiseTerm` — with no hand-written serialisation code at
all. Round-trip is byte-identical for both classes. Source generation produces
byte-identical output to reflection in both directions.

The prediction that this would be *fiddly* did not hold, and the fallback it was
weighed against does not apply: **the notation-parser alternative is already
closed by ADR-0002 §3** (decided the same day, on NFR-3 grounds — authoring UX
is not ours), and by ADR-0002 §1–2, which make the seed JSON *machine-emitted
from C# records*. Nobody hand-writes `$type` in seed data, so verbosity was
never the cost it looked like. The live question was only ever converter vs
attributes, and attributes win.

**Four things did bite**, none of them the hierarchy nesting, and three of them
are silent. They are the reason the spike was worth running.

---

## Findings

### 1. A converter and `[JsonPolymorphic]` are mutually exclusive per type — hard failure

Declaring both throws at configuration time:

```
NotSupportedException: The converter for derived type 'NumberOrParam'
does not support metadata writes or reads.
```

So the choice in the open question is a genuine either/or, made once per
hierarchy, and cannot be blended. Loud, and it fails on the first serialise, so
it costs nothing beyond knowing it. (`Model.cs`, the note above `NumberOrParam`.)

### 2. An out-of-order discriminator fails by default — silent trap, one-line fix

A document containing the discriminator, but not as the object's **first**
property, is rejected:

```
NotSupportedException: The JSON payload for polymorphic … type 'Predicate'
must specify a type discriminator.  Path: $.phases[0].tasks[0].flightValidWhen
```

The message names a property the document already has. Anything that reorders
keys produces one — a formatter, a key-sorting pre-commit hook, most languages'
dictionary round trip, and a human writing the obvious
`{"metricRef": …, "$kind": "rate"}`. On the POST path of ADR-0002 §1 that is a
rejection the class author cannot act on.

`JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true` (.NET 9+) fixes
it, and the reordered document then reads back to a definition that re-emits
byte-identically to the canonical form — so canonical order is *recoverable*,
not merely tolerated. **Recommendation: set it on the ingestion options.** The
buffering cost it carries is irrelevant at 24–31 kB and a handful of writes a
day.

### 3. A discriminator that shadows a real property corrupts silently — rename it

`TypeDiscriminatorPropertyName = "kind"` on a type that also has a `Kind`
property emits **both**, with no error, no warning, no build failure:

```json
{"kind":"fixed","kind":"Fixed"}
```

It is written, hashed, and stored. It fails only when something reads it back —
on the far side of the event store, for a definition that was accepted without
complaint. That is exactly the F22/F24/F28 failure shape the notation document
spends its length guarding against, one layer down.

Nothing collides *today*, because none of the six records with a `Kind` property
is polymorphic. Two were one decision away: the class diagram records that
splitting `TaskTiming` into `Fixed`/`UntilAllFlightsComplete` subtypes "was
considered and declined" (§3), and says the same of `PromotionRule` (§2). Either
choice reversed and this fires.

**Adopted: the discriminator is `$kind`** — it cannot be a model property name,
`$` is the convention STJ already uses for its own metadata, and it is a
one-word change now against a corrupted stored definition later. `Hazards.cs`
keeps the collision demonstrated, since the reasoning is otherwise invisible.

A second, smaller benefit showed up on making the change: under `"kind"` the
corpus reported 343 and 251 "discriminators" because nothing could tell a
discriminator from a `MeasuredValue.kind`. Under `$kind` the true counts are 141
and 140. A name that cannot collide is also a name that can be grepped.

### 4. Record equality is not a round-trip check

`ImmutableArray<T>.Equals` compares the underlying array by *reference*, so
`definition.Equals(reread)` is `false` for every definition in the corpus even
when the JSON is byte-identical. Nothing is wrong; the trap is writing the
round-trip test as an equality assertion and having it fail for a reason that
has nothing to do with serialisation. **ADR-0002 §6's round-trip test must be a
byte comparison** — which is what §5 already implies by making the content hash
the identity.

---

## What the corpus actually measures

| | F3K | F5K |
|---|---|---|
| notation source | 234 lines | 221 lines |
| tasks, expanded | 27 | 10 |
| score terms, expanded | 29 | 84 |
| canonical JSON, indented | 66,562 B / 2,556 lines | 55,524 B / 2,028 lines |
| canonical JSON, minified (what is hashed) | 30,818 B | 24,023 B |
| `$kind` discriminators | 141 | 140 |
| **max nesting depth** | **9** | **11** |
| unions as tagged objects instead | +9.1 % | +18.9 % |

**Depth 11 against `MaxDepth` 64.** The nesting the open question worried about
is not deep. The deepest path in the corpus is F5K Task E's second term —
class → phases → tasks → score → conditional → `when` → `allOf` → children →
comparison → `rightValue`. There is no plausible class that doubles it, and
ADR-0002 §4's nesting-depth input limit can sit at something like 24 with room
for a class nobody has written yet.

**Expansion is ~10×** on notation lines, and it is `like` that does it: F3K's 13
authored tasks become 27, F5K's 5 become 10. This is a consequence of ADR-0002
§2 and notation §7.1, not a surprise, but it is the first time the number has
been on the page: the checked-in seed corpus will be roughly **250–300 kB of
JSON across eleven classes**. Reviewable as a diff, per §6; not reviewable by
reading.

## Two smaller things, both model rather than JSON

Flagged rather than acted on, per CLAUDE.md house-keeping rule 2. Written up in
full — evidence, options, recommendation and the exact edit — in
`MODEL-FLAGS.md`; summarised here. **Both were approved and applied on
2026-08-03**; see `MODEL-FLAGS.md` for what landed. The spike's own records still
carry `applyAt` and drop the parameter units — it transcribes the model as it
stood, and re-transcribing it is not what the spike is for.

- **`ScoreTerm.applyAt` is meaningless on a nested term.** It is serialised on
  every term because the class diagram puts it on the `ScoreTerm` base, but a
  `then`/`else` branch lands at the stage its *parent* lands at. 40 of F5K's 84
  `applyAt` sites are nested branches carrying a field that cannot vary
  independently. It is inert — nothing reads it there — but it is a second place
  a stage is recorded, and only one of them can be right. Worth deciding whether
  `applyAt` belongs on the term or on the `Task.score` / `Task.scoreNormalised`
  list that holds it.
- **`Parameter` has no `unit`, but the notation writes one.** `param
  workingTime.A s default 600` — the `s` has nowhere to go in the class diagram
  §2 `Parameter`, which carries name, kind, defaultValue, allowedValues and
  boundAt only. `MetricDefinition` *does* have a unit. Either the notation is
  writing something the model cannot store (breaking notation rule 1 and rule 2's
  isomorphism claim), or `Parameter.unit` is missing from the diagram. The
  transcriptions here drop it.

---

## Recommendation

Close the LADR-0003 open item in favour of **attribute-declared polymorphism, no
custom converter for the hierarchies**, with three amendments to the serialiser
configuration:

1. `TypeDiscriminatorPropertyName = "$kind"`, not `"kind"`.
2. `AllowOutOfOrderMetadataProperties = true` on the ingestion path.
3. Round-trip tests compare bytes, not records.

All three are applied in this spike, and LADR-0003 records them.

Keep the two small hand-written converters for `NumberOrParam` / `FlagOrParam`
(~60 lines together, `Json.cs`). They are not needed for correctness — the
tagged form round-trips identically — but they collapse thirteen slots' worth of
`{"kind":"literal","value":599}` to `599`, which is 9–19 % of the artefact and
rather more than that of its readability. That is a presentation decision about
the reviewable seed corpus, and it is reversible.

Also settled in passing, since LADR-0003 asserts both without evidence:

- **Source generation works** with the polymorphic hierarchies, byte-identically
  to reflection, *provided* `GenerationMode = Metadata` and every derived type
  gets its own `[JsonSerializable]`. The generator does not walk
  `[JsonDerivedType]`; omitting one fails at run time, not at build. See
  `SourceGen.cs`.
- **Deserialisation does enforce the closed vocabulary** on untrusted input, as
  ADR-0002 §4 claims. Unknown discriminators on all three hierarchies, a missing
  discriminator, missing `required` members, over-deep payloads and malformed
  parameter references are all rejected. What it does *not* catch is a
  `ParameterRef` in a `NumberOrParam` slot that is not one of the thirteen —
  that document is type-correct, and the check belongs to `Validate()`.

## What this spike is not

It transcribes two classes to test the wire format, not to be the transcription.
ADR-0002 §6 requires each class reviewed against the **rule refs**, class by
class, and neither of these has had that review. The citations are carried
across so that review is possible; treat the two `Seed*.cs` files as untrusted
until it has happened.
