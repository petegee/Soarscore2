# Seed classes — the authoring source

The seventeen Competition Class definitions, authored as C# records per ADR-0002 §2,
plus the four-tape catalogue. The C# is the sole source of truth; the canonical
JSON under `json/` is checked in, byte-exact, and CI-enforced — the seed-corpus
drift guard fails the build when regenerating differs from what is committed.

**This project never ships.** The review surface is the C# itself — rule citations
carried across as comments — answering *Does this match the rulebook?*

Rule references stop at the repository boundary (ADR-0002 §7). They are an
authoring-side property and do not enter the model, the wire format, the stored
definition or `AdoptedRules`.

## Running it

```
dotnet run --project tools/Soarscore.SeedData
```

Regenerates the committed corpus (`json/*.json` and `json/tapes/*.json`) and checks
four things: the round trip
JSON → records → JSON is byte-identical, the source-generated context agrees with
reflection in both directions, the deepest path stays inside the ingestion depth
limit, and each definition's content hash is printed (ADR-0002 §5 — not a
version; it is what makes a replay provable and drift detectable).

The emitter owns those directories and keeps them reproducible: before emitting
it deletes the `*.json` files it owns — top level of `json/` only, and the same
in `json/tapes/` before the tape loop — so a definition removed from the C#
leaves no orphan JSON behind to ship, and every file is written with fixed `\n`
line endings so regenerating on any OS produces byte-identical files.

Byte comparison, not record comparison: `ImmutableArray<T>.Equals` is
reference-based, so `definition.Equals(reread)` is false for every definition in
the corpus even when the JSON matches exactly.

## Checking for drift

```
dotnet run --project tools/Soarscore.SeedData && git status --porcelain tools/Soarscore.SeedData/json
```

Empty output means no drift: the regenerated corpus is byte-identical to what is
committed, including the file set. Any line of output — added, modified, or
deleted — is drift; re-run the tool and commit `tools/Soarscore.SeedData/json/`
in the same commit as the C# change. CI runs this same recipe as the seed-corpus
drift guard.

## How the notation maps

Nothing here is a notation construct — notation §7.1's sugar expands before
adoption, so the model only ever holds the expanded instance.

| Notation | Here |
|---|---|
| `task X "…" like Y` + overrides | `TaskY with { … }` |
| `metricSet` / `use` | a shared `ImmutableArray<MetricDefinition>` property |
| `rows` / `bands` + `use` | a shared `ImmutableArray<LookupRow>` / `<Band>` property |
| `param(<name>)` | `NumberOrParam.Param(…)` / `FlagOrParam.Param(…)` |
| `score` / `score normalised` | `TaskDefinition.Score` / `.ScoreNormalised` |

`with` **is** `like`, including notation §7.2's edge: a restated block that omits
a keyword takes the *default*, not the parent's value — which is exactly what
constructing a fresh value object and leaving a member unset does.

## Status of the transcription

ADR-0002 §6 requires each class to be reviewed **against the rule refs**, class by
class, not against the notation — re-deriving from the source is what catches
an error the notation file and the C# now share. That review has **not** happened.
Until it has, treat the seventeen definitions as untranscribed-but-plausible, and
check F3K Tasks E and H against the worked examples in the rule text
(`F3K.11.5` → 142 s, `F3K.11.8` → 569 s) rather than against each other — the
failure there is silent in both directions.
