# LADR-0002 — Competition Class definition: representation, ingestion and identity

**Status:** Accepted · **Date:** 2026-08-03 · **Follows:** LADR-0001

## Decision

1. **Canonical JSON is the definition** — the wire format, the stored artifact, and
   what a Competition copies in.
2. **Seed classes are authored as C# records** that emit that JSON at build time. The
   JSON is checked in. The C# never ships.
3. **`seed-data/*.class` is transcribed once into C# and retired.** The notation
   document remains the specification; the `.class` files stop being a live source.
4. **One ingestion path** for seed and user-supplied definitions alike.
5. **No versioning semantics on `CompetitionClass`.** The Competition's copy is the
   only thing that matters; a content hash gives identity.

## 1. Users POST definitions

We provide the official FAI and NZMAA classes; users author their own on the shared
instance. That is a stated NFR-1 capability, and it is the fact that decides most of
what follows.

Two consequences dominate:

- **The runtime validator is the primary gate, not a backstop.** All sixteen adoption
  checks run against untrusted input, returning diagnostics in an HTTP response. No
  compile-time enforcement reaches this path. The type system's contribution is a
  convenience for the eleven definitions *we* author — which are already the
  best-reviewed artifacts in the repository — and contributes nothing where review is
  absent.
- **Seed classes must enter through the same door as user classes.** If seed data were
  compiled-in objects, the corpus that exists to be the model's test would bypass
  deserialisation, validation and storage entirely, and the real ingestion path would
  be tested only by synthetic fixtures. Emitting JSON at build and ingesting it at
  runtime closes that gap, and makes the checked-in seed JSON reviewable as *exactly
  what a user would POST*.

## 2. Authoring: C# records, not a fluent DSL

Records, object initialisers, `with`, and smart constructors. No builder.

**`with` is `like`.** C# record copy semantics reproduce notation §7.1/§7.2 exactly,
including the edge case that a restated block's omitted keyword takes the *default*
rather than the parent's value — that is what constructing a fresh value object and
leaving a member unset does. A fluent builder must reimplement this, and reimplements
it ambiguously: `.Score(s => s.Rate(…))` on a builder seeded from a parent reads as
*append*, where the notation mandates *replace*. F22 and F28 were both silent
mis-scores of exactly that shape; we do not introduce a third to gain IntelliSense.

**Smart constructors carry the checks worth carrying.** `Bands.Below/UpTo/Rest` makes
each band's lower bound implicitly the previous band's upper bound, so check 7 — the
gap-or-overlap silent mis-score — becomes unwritable. `Rows.UpTo(…).Rest(…)` makes
"at most one unbounded row, and it is last" unwritable. `All(a, b, params rest)`
carries `AllOf`'s `2..*`. `NumberOrParam` in exactly thirteen slot types makes check 4
unrepresentable.

These are properties of the **record types**, not of an authoring style: they hold
identically for a definition deserialised from a POSTed document. That is the point.
A fluent builder's marginal contribution over plain records is five checks (10, 11,
13, 14, 15), each of which is two lines of runtime validation, bought with a recursive
term grammar expressed through nested lambdas, generic type-state propagated through
`like`, forced call ordering, and roughly five hand-maintained interfaces per aggregate
taxed by every additive model change. NFR-2 promises those changes are cheap; the
builder makes them expensive.

## 3. No notation parser in the core

The notation is a better authoring surface than JSON. Building a parser for it is
nevertheless out of scope, and on NFR-3 grounds rather than cost: **authoring UX is
not ours.** The core makes no assumptions about how data is entered. An integrator
building the wider competition system can put a form-based class builder in front of
this API and emit JSON — that is the division of labour the whole architecture rests
on, and a notation parser would be the single place we contradicted it.

If a friendlier authoring path is ever wanted, it is a **standalone CLI outside the
core** compiling `.class` → JSON: no hardening, no untrusted input, no diagnostic
quality bar, fails loudly at build. Nothing here forecloses it.

## 4. Ingestion — one path

```
POST /competition-classes/define   (or seed load at startup)
  → deserialise into the sealed record hierarchy   ← closed vocabulary enforced here
  → Validate(definition) : IReadOnlyList<Defect>   ← all 16 checks, total, non-throwing
  → canonicalise + hash
  → append to the CompetitionClass stream
```

Required because the input is untrusted:

- **`Validate` is total and non-throwing.** It returns every defect, not the first.
- **`Defect` renders as an API error body** — check identity, path into the document,
  and a message naming the construct. This is the only feedback a user authoring a
  class ever gets.
- **Input limits**: payload size, nesting depth, band/row/term/parameter/task counts.
  Cheap, and the absence of them is the obvious denial-of-service surface.
- **No code execution, ever.** The definition is data. This is settled by LADR-0001's
  replay-determinism argument and by the trust model — the deployment is unauthenticated.

Deserialisation into sealed subtypes is what enforces the closed vocabulary (notation
§6) on the user path. There is no equivalent of a hand-written helper method or a loop
generating bands, because there is no host language.

## 5. Identity: content hash, not versions

`AdoptedRules` already copies the whole definition at Competition creation, so
`aggregate-roots.md` §"Scoring is cross-aggregate" holds: *the library can be edited or
retired without any live or historical event noticing*. The class diagram is explicit
that this **replaced** version pinning, which "only worked if nobody ever edited seed
data in place".

Therefore:

- **No immutability rule, no new-version-on-edit, no branching** on `CompetitionClass`.
  Edit and delete freely.
- **`CompetitionClass.version` stays a free-text human label**, and
  `AdoptedRules.sourceVersion` records what it said at copy time. **Provenance only —
  nothing may resolve it.** The moment code reads `sourceVersion` to locate a
  definition, the copy has stopped being the truth and we are back to pinning.
- **History is free.** `CompetitionClass` is event-sourced; "what did this class look
  like in March" is a fold to a stream position. No field, no design.
- **A content hash over the canonical serialisation** goes in the adoption event. It is
  not versioning. It does two things a text label cannot: it makes replay *provable*
  (a 2028 re-score can demonstrate it read the same bytes), and it makes drift
  detectable — because the hash is a function of the canonical form, it is computable
  for the library class at any time and compared against the Competition's copy.

  We expose the hashes and the comparison. **We do not build the warning** — whether an
  integrator surfaces "this class changed since your last event" is their concern
  (NFR-3).

The residual is user surprise: two competitions created from "the same" library class a
week apart can silently differ. That is not a correctness problem, and the hash makes it
detectable rather than invisible.

## 6. Transcribing `seed-data/*.class`

Approximately 1,418 lines of heavily annotated notation become C# records, once.

**The risk, stated plainly.** There is no existing implementation to diff against, so
the transcription cannot be verified by matching prior scores — no safety net exists
yet. Transcription errors are *local* and therefore individually invisible. (A parser
was the alternative and carries the mirror risk: its errors are systematic, hence more
likely to break loudly, at roughly 2,000 lines and permanent maintenance. We accepted
the local-error risk over the parser's cost.)

**Mitigation, and it is not optional:**

- **Carry every `#` rule citation across as a comment on the same construct.** Notation
  rule 3 — a rule-derived constant without a source ref is a defect — survives the
  transcription or the corpus loses its most valuable review property. F28 was found by
  reading a clause against a table, not by running anything.
- **Review each transcribed class against the rule refs**, class by class, not against
  the `.class` file. Re-deriving from the source is what catches an error the `.class`
  file and the C# now share.
- **Snapshot the emitted canonical JSON** for all eleven classes. From that point on,
  any change to a definition or to the emitter is a visible diff, and the transcription
  is frozen.
- **Round-trip test**: JSON → records → JSON is byte-identical. This also validates the
  wire contract the API depends on.

After transcription the `.class` files are removed as a live source.
`competition-class-notation.md` remains as the specification and the human-readable
description of the model — it is what a future CLI would implement, and it is where the
reasoning behind each construct lives.

## 7. Citations are not in the model — decided, rejected

A `sourceRef` on `ClassDefinition` constructs was proposed and **rejected**. Rule
references are an authoring-side property. They do not enter the model, the wire
format, the stored definition, or `AdoptedRules`.

Consequences, accepted knowingly:

- **An adopted rulebook carries no provenance.** A stored definition states what the
  arithmetic is, never which clause it implements. A scoring dispute is resolved by
  re-deriving from the rulebook by hand, as it is today.
- **A user-authored class has no paper trail at all.** Nothing records what rules it
  claims to implement. This is consistent with the trust model — the deployment is
  unauthenticated and there is no score sign-off — and the event log gives auditability
  of *what happened*, never of *what was intended*.
- **The core stays free of a field it could never interpret.** `F3K.7`, `NZ.3.12.3` and
  a club bylaw are all legitimate; any format the core recognised would be a
  class-specific assumption in the core.

**Two review surfaces, and they are not interchangeable** — this resolves the tension
between §1 and §6:

| Surface | Checked in | Ships | Answers |
|---|---|---|---|
| C# authoring source, `#` citations as comments | yes | no | *Does this match the rulebook?* |
| Canonical seed JSON | yes | yes | *Is this what a user would POST, and does it still say what it said?* |

§6's mitigation is therefore unchanged and still binding: the citations survive
transcription into the C# and the class-by-class review happens there, against the rule
refs. What the rejection settles is that the citations stop at the repository boundary
— they are a property of how we maintain the seed corpus, not of what the system holds.

Users authoring classes through the API get no equivalent, and we build them none.
